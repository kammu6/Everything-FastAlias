using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using EverythingFastAlias.Models;

namespace EverythingFastAlias.Services
{
    public class QueryTransformer
    {
        private static readonly Regex TokenRegex = new(
            @"("".*?""|<(?:[^<>]+|(?<angle><)|(?<-angle>>))*(?(angle)(?!))>|[^|&\s()""!<>]+|\||&|\(|\)|!|<|>)", 
            RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(150)
        );

        private static readonly HashSet<string> EverythingKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            // 변경자 (Modifiers)
            "path", "parent", "folder", "file", "regex", "ascii", "noascii", 
            "case", "nocase", "diacritics", "nodiacritics", "wfn", "nowfn", 
            "wholefilename", "nowholefilename", "wholeword", "nowholeword", 
            "wildcards", "nowildcards", "ww", "noww", "utf8",
            // 함수 (Functions)
            "ext", "size", "datemodified", "dm", "datecreated", "dc", "dateaccessed", 
            "da", "daterun", "dr", "attrib", "attributes", "empty", "dupe", 
            "child", "childcount", "childfilecount", "childfoldercount", "infolder", 
            "parents", "len", "startwith", "endwith", "album", "artist", "comment", 
            "genre", "title", "track", "type", "content", "ansicontent", "utf8content", 
            "utf16content", "utf16becontent"
        };

        public static string Transform(string rawQuery, SearchOptions options, Dictionary<string, List<string>> mappings)
        {
            // 1. FastAlias 동의어 치환 처리
            string processedQuery;
            if (options.UseFastAlias && !string.IsNullOrWhiteSpace(rawQuery))
            {
                processedQuery = ReplaceAliases(rawQuery, mappings);
            }
            else
            {
                processedQuery = rawQuery ?? "";
            }

            processedQuery = processedQuery.Trim();

            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(processedQuery))
            {
                bool isFolderPreset = options.MediaPresets != null && options.MediaPresets.Contains("폴더");
                string modifier = isFolderPreset ? "folder:" : "";
                string regPrefix = options.UseRegex ? "regex:" : "";

                string BuildTerm(string word, string mod, string rgp, string pathPrefix = "")
                {
                    return $"{mod}{pathPrefix}{rgp}{word}";
                }

                var matches = TokenRegex.Matches(processedQuery);
                var queryParts = new List<string>();

                foreach (Match match in matches)
                {
                    string token = match.Value;
                    if (IsWordToken(token))
                    {
                        bool isGroup = token.StartsWith("<") && token.EndsWith(">");

                        if (isGroup)
                        {
                            string innerContent = token.Substring(1, token.Length - 2);
                            var words = SplitGroupWords(innerContent);
                            var processedWords = new List<string>();

                            foreach (var w in words)
                            {
                                if (IsConstraintOrDrive(w))
                                {
                                    processedWords.Add(w);
                                }
                                else
                                {
                                    if (options.Scope == SearchScope.Path)
                                    {
                                        processedWords.Add(BuildTerm(w, modifier, regPrefix, "path:"));
                                    }
                                    else if (options.Scope == SearchScope.All)
                                    {
                                        processedWords.Add(BuildTerm(w, modifier, regPrefix));
                                        processedWords.Add(BuildTerm(w, modifier, regPrefix, "path:"));
                                    }
                                    else
                                    {
                                        processedWords.Add(BuildTerm(w, modifier, regPrefix));
                                    }
                                }
                            }
                            string separator = " | ";
                            queryParts.Add($"<{string.Join(separator, processedWords)}>");
                        }
                        else
                        {
                            if (IsConstraintOrDrive(token))
                            {
                                queryParts.Add(token);
                            }
                            else
                            {
                                if (options.Scope == SearchScope.Path)
                                {
                                    queryParts.Add(BuildTerm(token, modifier, regPrefix, "path:"));
                                }
                                else if (options.Scope == SearchScope.All)
                                {
                                    string term1 = BuildTerm(token, modifier, regPrefix);
                                    string term2 = BuildTerm(token, modifier, regPrefix, "path:");
                                    queryParts.Add($"<{term1} | {term2}>");
                                }
                                else
                                {
                                    queryParts.Add(BuildTerm(token, modifier, regPrefix));
                                }
                            }
                        }
                    }
                    else
                    {
                        queryParts.Add(token);
                    }
                }

                for (int i = 0; i < queryParts.Count; i++)
                {
                    AppendSeparator(sb);
                    sb.Append(queryParts[i]);
                }
            }

            return BuildOptionConstraints(sb.ToString().Trim(), options);
        }

        private static string BuildOptionConstraints(string baseQuery, SearchOptions options)
        {
            bool isFolderPreset = options.MediaPresets != null && options.MediaPresets.Contains("폴더");

            bool hasConstraints = !string.IsNullOrWhiteSpace(options.FolderPaths) ||
                                  !string.IsNullOrWhiteSpace(options.ExcludedWords) ||
                                  (options.MediaPresets != null && options.MediaPresets.Count > 0 && !options.MediaPresets.Contains("전체")) ||
                                  !string.IsNullOrWhiteSpace(options.CustomExtensions) ||
                                  (options.MinSize.HasValue && !isFolderPreset) ||
                                  (options.MaxSize.HasValue && !isFolderPreset) ||
                                  !options.IncludeRecycleBin ||
                                  (options.TargetDrives != null && options.TargetDrives.Count > 0);

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(baseQuery))
            {
                sb.Append(baseQuery);
            }

            // 3. 폴더 제약 조건 및 재귀 탐색 제어
            if (!string.IsNullOrWhiteSpace(options.FolderPaths))
            {
                AppendSeparator(sb);
                
                var paths = options.FolderPaths.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                var pathQueries = new List<string>();

                foreach (var path in paths)
                {
                    var trimmedPath = path.Trim().Trim('"');
                    if (string.IsNullOrEmpty(trimmedPath)) continue;

                    if (options.RecursiveSearch)
                    {
                        // 재귀 탐색 ON: path:"경로"
                        pathQueries.Add($@"path:""{trimmedPath}""");
                    }
                    else
                    {
                        // 직계 하위만 탐색 OFF: parent:"경로"
                        pathQueries.Add($@"parent:""{trimmedPath}""");
                    }
                }

                if (pathQueries.Count > 0)
                {
                    if (pathQueries.Count == 1)
                    {
                        sb.Append(pathQueries[0]);
                    }
                    else
                    {
                        sb.Append($"<{string.Join(" | ", pathQueries)}>");
                    }
                }
            }

            // 4. 단어 제외 조건 적용
            if (!string.IsNullOrWhiteSpace(options.ExcludedWords))
            {
                var excluded = options.ExcludedWords.Split(new[] { ',', ';', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in excluded)
                {
                    var trimmed = word.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        AppendSeparator(sb);
                        sb.Append($"!\"{trimmed}\"");
                    }
                }
            }

            // 5. 미디어 프리셋 필터 적용 (다중 선택 가능)
            var mediaQueries = new List<string>();

            if (options.MediaPresets != null && options.MediaPresets.Count > 0 && !options.MediaPresets.Contains("전체"))
            {
                foreach (var preset in options.MediaPresets)
                {
                    switch (preset)
                    {
                        case "영상":
                            mediaQueries.Add("ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts");
                            break;
                        case "음악":
                            mediaQueries.Add("ext:mp3;wav;flac;ogg;wma;m4a;aac");
                            break;
                        case "사진":
                            mediaQueries.Add("ext:jpg;jpeg;jfif;png;gif;bmp;webp;tiff;psd;ai;svg");
                            break;
                        case "문서":
                            mediaQueries.Add("ext:pdf;txt;hwp;hwpx;doc;docx;xls;xlsx;ppt;pptx;rtf");
                            break;
                        case "실행":
                            mediaQueries.Add("ext:exe;bat;cmd;msi;lnk;scr;sh;pyw");
                            break;
                        case "압축":
                            mediaQueries.Add("ext:zip;7z;rar;tar;gz;bz2;iso;alz;egg");
                            break;
                        case "코드":
                            mediaQueries.Add("ext:ts;tsx;js;jsx;json;java;py;pyw;cpp;c;h;cs;html;css;go;rs;sh;md;yml;yaml");
                            break;
                    }
                }
            }

            if (mediaQueries.Count > 0)
            {
                AppendSeparator(sb);
                if (!isFolderPreset)
                {
                    sb.Append("file: ");
                }
                
                if (mediaQueries.Count == 1)
                {
                    sb.Append(mediaQueries[0]);
                }
                else
                {
                    sb.Append($"<{string.Join(" | ", mediaQueries)}>");
                }
            }

            // 6. 커스텀 확장자 필터 적용
            if (!string.IsNullOrWhiteSpace(options.CustomExtensions))
            {
                AppendSeparator(sb);
                var exts = Regex.Replace(options.CustomExtensions, @"\s*[,;]\s*", ";").Trim(';');
                sb.Append($"ext:{exts}");
            }

            // 7. 파일 크기 필터 적용 (폴더 프리셋인 경우 무시)
            if (options.MinSize.HasValue && !isFolderPreset)
            {
                AppendSeparator(sb);
                sb.Append($"size:>={options.MinSize.Value}{options.MinSizeUnit.ToString().ToLower()}");
            }
            if (options.MaxSize.HasValue && !isFolderPreset)
            {
                AppendSeparator(sb);
                sb.Append($"size:<={options.MaxSize.Value}{options.MaxSizeUnit.ToString().ToLower()}");
            }

            // 8. 휴지통(Recycle Bin) 포함 제어
            if (!options.IncludeRecycleBin)
            {
                AppendSeparator(sb);
                sb.Append("!$Recycle.Bin");
            }

            // 9. 검색 대상 로컬 드라이브 필터 적용
            if (options.TargetDrives != null && options.TargetDrives.Count > 0)
            {
                var driveQueries = new List<string>();
                foreach (var drive in options.TargetDrives)
                {
                    string driveLetter = drive.TrimEnd('\\') + "\\";
                    driveQueries.Add($@"path:{driveLetter}");
                }
                if (driveQueries.Count > 0)
                {
                    AppendSeparator(sb);
                    sb.Append($"<{string.Join(" | ", driveQueries)}>");
                }
            }

            return sb.ToString().Trim();
        }

        private static string ReplaceAliases(string query, Dictionary<string, List<string>> mappings)
        {
            if (string.IsNullOrWhiteSpace(query)) return query;

            Dictionary<string, HashSet<string>> aliasGroups;
            List<string> sortedKeys;

            var globalCache = DatabaseService.Instance.GetAliasGroupsCache();
            var dbCacheSnapshot = DatabaseService.Instance.GetCacheSnapshot();

            if (mappings != null && mappings.Count > 0 && mappings.Count != dbCacheSnapshot.Count)
            {
                var localGroups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in mappings)
                {
                    var keyword = kvp.Key.Trim();
                    if (string.IsNullOrEmpty(keyword)) continue;

                    var rowElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { keyword };
                    if (kvp.Value != null)
                    {
                        foreach (var syn in kvp.Value)
                        {
                            var trimmedSyn = syn.Trim().TrimEnd(';');
                            if (!string.IsNullOrEmpty(trimmedSyn))
                            {
                                rowElements.Add(trimmedSyn);
                            }
                        }
                    }

                    foreach (var member in rowElements)
                    {
                        if (!localGroups.TryGetValue(member, out var existingGroup))
                        {
                            existingGroup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            localGroups[member] = existingGroup;
                        }
                        foreach (var m in rowElements)
                        {
                            existingGroup.Add(m);
                        }
                    }
                }
                aliasGroups = localGroups;
                sortedKeys = new List<string>(aliasGroups.Keys);
                sortedKeys.Sort((a, b) => b.Length.CompareTo(a.Length));
            }
            else
            {
                aliasGroups = globalCache.Groups;
                sortedKeys = globalCache.SortedKeys;
            }

            if (aliasGroups == null || sortedKeys == null || sortedKeys.Count == 0)
            {
                return query;
            }

            string processed = query;
            var tempReplacements = new List<string>();
            string placeholderKey = Guid.NewGuid().ToString("N");

            foreach (var key in sortedKeys)
            {
                if (processed.IndexOf(key, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (!aliasGroups.TryGetValue(key, out var synonyms) || synonyms == null || synonyms.Count <= 1)
                    continue;

                string escapedKey = Regex.Escape(key);
                string pattern = $@"(?<=^|[\s|&()!<>])" + escapedKey + @"(?=$|[\s|&()!<>])";

                var list = new List<string>(synonyms);
                list.Remove(key);
                list.Insert(0, key);

                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Contains(" ") && !list[i].StartsWith("\""))
                    {
                        list[i] = $"\"{list[i]}\"";
                    }
                }

                string replacementValue = $"<{string.Join("|", list)}>";

                processed = Regex.Replace(processed, pattern, m =>
                {
                    string placeholder = $"__ALIAS_{placeholderKey}_{tempReplacements.Count}__";
                    tempReplacements.Add(replacementValue);
                    return placeholder;
                }, RegexOptions.IgnoreCase);
            }

            for (int i = 0; i < tempReplacements.Count; i++)
            {
                processed = processed.Replace($"__ALIAS_{placeholderKey}_{i}__", tempReplacements[i]);
            }

            return processed;
        }

        private static bool IsWordToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            
            if (token == "|" || token == "&" || token == "(" || token == ")" || token == "!")
                return false;

            return true;
        }

        private static void AppendSeparator(StringBuilder sb)
        {
            if (sb.Length > 0 && sb[^1] != ' ')
            {
                sb.Append(' ');
            }
        }

        private static List<string> SplitGroupWords(string content)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(content)) return result;

            var parts = content.Split('|');
            foreach (var p in parts)
            {
                var trimmed = p.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    result.Add(trimmed);
                }
            }
            return result;
        }

        private static bool IsConstraintOrDrive(string term)
        {
            if (string.IsNullOrEmpty(term)) return false;

            var trimmed = term.Trim().Trim('"');

            // 1. 드라이브 문자 및 네트워크 공유 경로 감지 (예: C:, D:\, \\server\share)
            if (trimmed.StartsWith(@"\\") || (trimmed.Length >= 2 && trimmed[1] == ':'))
            {
                return true;
            }

            // 2. Everything 수식어 및 함수 접두사 감지 (예: path:..., regex:...)
            int colonIndex = trimmed.IndexOf(':');
            if (colonIndex > 0)
            {
                string prefix = trimmed.Substring(0, colonIndex);
                if (EverythingKeywords.Contains(prefix))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDriveLetter(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            var trimmed = value.Trim().Trim('"');
            if (trimmed.Length == 2 && trimmed[1] == ':') return true;
            if (trimmed.Length == 3 && trimmed[1] == ':' && (trimmed[2] == '\\' || trimmed[2] == '/')) return true;
            return false;
        }
    }
}
