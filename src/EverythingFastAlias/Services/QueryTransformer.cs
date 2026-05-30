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
            RegexOptions.Compiled
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
                        string inner = token;
                        bool isGroup = token.StartsWith("<") && token.EndsWith(">");

                        // Scope에 따른 쿼리 가공
                        string scopeQuery;
                        if (options.Scope == SearchScope.Path)
                        {
                            scopeQuery = isGroup ? $@"path:{inner}" : $@"path:<{inner}>";
                        }
                        else if (options.Scope == SearchScope.All)
                        {
                            if (isGroup)
                            {
                                scopeQuery = $@"<{inner} | path:{inner}>";
                            }
                            else
                            {
                                scopeQuery = $@"<<{inner}> | path:<{inner}>>";
                            }
                        }
                        else
                        {
                            // File 검색: 괄호를 임의로 추가하지 않고 그대로 둠 (동의어 그룹인 경우에만 이미 <...> 괄호가 적용되어 있음)
                            scopeQuery = inner;
                        }
                        queryParts.Add(scopeQuery);
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

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(baseQuery))
            {
                if (hasConstraints && baseQuery.Contains("|"))
                {
                    sb.Append($"<{baseQuery}>");
                }
                else
                {
                    sb.Append(baseQuery);
                }
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
            bool isFolderPreset = options.MediaPresets != null && options.MediaPresets.Contains("폴더");

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

            // 5-1. 폴더 프리셋: folder: 쿼리 추가 (전체+폴더 동시 선택 시에도 동작)
            if (isFolderPreset)
            {
                AppendSeparator(sb);
                sb.Append("folder:");
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
                    driveQueries.Add(drive.TrimEnd('\\')); // "C:" 형식 유지
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

            if (token.StartsWith("\"") && token.EndsWith("\""))
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
    }
}
