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

                            if (options.Scope == SearchScope.Path)
                            {
                                var pathWords = new List<string>();
                                foreach (var w in words)
                                {
                                    pathWords.Add($@"path:{w}");
                                }
                                queryParts.Add($"<{string.Join(" | ", pathWords)}>");
                            }
                            else if (options.Scope == SearchScope.All)
                            {
                                var allWords = new List<string>();
                                foreach (var w in words)
                                {
                                    allWords.Add(w);
                                }
                                foreach (var w in words)
                                {
                                    allWords.Add($@"path:{w}");
                                }
                                queryParts.Add($"<{string.Join(" | ", allWords)}>");
                            }
                            else
                            {
                                queryParts.Add(token);
                            }
                        }
                        else
                        {
                            if (options.Scope == SearchScope.Path)
                            {
                                queryParts.Add($@"path:{token}");
                            }
                            else if (options.Scope == SearchScope.All)
                            {
                                queryParts.Add($@"<{token} | path:{token}>");
                            }
                            else
                            {
                                queryParts.Add(token);
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
            bool hasConstraints = !string.IsNullOrWhiteSpace(options.FolderPaths) ||
                                  !string.IsNullOrWhiteSpace(options.ExcludedWords) ||
                                  (options.MediaPresets != null && options.MediaPresets.Count > 0 && !options.MediaPresets.Contains("전체")) ||
                                  !string.IsNullOrWhiteSpace(options.CustomExtensions) ||
                                  options.MinSize.HasValue ||
                                  options.MaxSize.HasValue ||
                                  !options.IncludeRecycleBin ||
                                  (options.TargetDrives != null && options.TargetDrives.Count > 0);

            // folder: 수식어는 | (OR) 연산자를 만나면 스코프가 끊어진다.
            // 따라서 folder: 프리셋이 활성화된 경우, 각 OR 항목에 개별적으로 folder: 를 부착해야 한다.
            // 예: <folder:"마키 호조" | folder:"Maki Hojo" | folder:path:"마키 호조" | ...>
            bool isFolderPreset = options.MediaPresets != null && options.MediaPresets.Contains("폴더");

            if (isFolderPreset && !string.IsNullOrEmpty(baseQuery))
            {
                baseQuery = ApplyModifierToEachTerm(baseQuery, "folder:");
            }

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
            // 주의: folder: 프리셋은 이미 위에서 baseQuery의 각 OR 항에 개별 부착 완료.
            //       여기서는 ext: 기반 파일 타입 필터만 처리한다.
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

            // 7. 파일 크기 필터 적용
            if (options.MinSize.HasValue)
            {
                AppendSeparator(sb);
                sb.Append($"size:>={options.MinSize.Value}{options.MinSizeUnit.ToString().ToLower()}");
            }
            if (options.MaxSize.HasValue)
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

        /// <summary>
        /// Everything의 수식어(folder:, file: 등)를 < > 그룹 내의 각 OR 항목에 개별 적용한다.
        /// Everything 엔진에서 수식어 스코프는 | (OR) 연산자에서 끊어지므로,
        /// 올바른 형태: <folder:A | folder:B>   (각 항목에 개별 적용)
        /// 잘못된 형태: <folder:A | B>           (folder:가 A에만 적용됨)
        /// </summary>
        private static string ApplyModifierToEachTerm(string query, string modifier)
        {
            if (string.IsNullOrEmpty(query)) return query;

            var matches = TokenRegex.Matches(query);
            var resultParts = new List<string>();

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
                        var modifiedWords = new List<string>();
                        foreach (var w in words)
                        {
                            var trimmed = w.Trim();
                            if (!string.IsNullOrEmpty(trimmed))
                            {
                                if (!trimmed.StartsWith(modifier, StringComparison.OrdinalIgnoreCase))
                                {
                                    modifiedWords.Add($"{modifier}{trimmed}");
                                }
                                else
                                {
                                    modifiedWords.Add(trimmed);
                                }
                            }
                        }
                        resultParts.Add($"<{string.Join(" | ", modifiedWords)}>");
                    }
                    else
                    {
                        if (!token.StartsWith(modifier, StringComparison.OrdinalIgnoreCase))
                        {
                            resultParts.Add($"{modifier}{token}");
                        }
                        else
                        {
                            resultParts.Add(token);
                        }
                    }
                }
                else
                {
                    resultParts.Add(token);
                }
            }

            var sb = new StringBuilder();
            for (int i = 0; i < resultParts.Count; i++)
            {
                AppendSeparator(sb);
                sb.Append(resultParts[i]);
            }
            return sb.ToString().Trim();
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

        private static bool IsSingleGroup(string query)
        {
            if (string.IsNullOrEmpty(query) || query.Length < 2) return false;
            if (query[0] != '<' || query[query.Length - 1] != '>') return false;

            int balance = 0;
            for (int i = 0; i < query.Length - 1; i++)
            {
                if (query[i] == '<') balance++;
                else if (query[i] == '>') balance--;

                if (balance <= 0) return false;
            }
            return balance == 1;
        }

        private static bool IsSingleParenthesisGroup(string query)
        {
            if (string.IsNullOrEmpty(query) || query.Length < 2) return false;
            if (query[0] != '(' || query[query.Length - 1] != ')') return false;

            int balance = 0;
            for (int i = 0; i < query.Length - 1; i++)
            {
                if (query[i] == '(') balance++;
                else if (query[i] == ')') balance--;

                if (balance <= 0) return false;
            }
            return balance == 1;
        }
    }
}
