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
            @"("".*?""|[^|&\s()""!]+|\||&|\(|\)|!)", 
            RegexOptions.Compiled
        );

        public static string Transform(string rawQuery, SearchOptions options, Dictionary<string, List<string>> mappings)
        {
            var sb = new StringBuilder();

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

            sb.Append(processedQuery);

            // 2. 탐색 대상 범위 (세그먼트) 적용 (파일만 / 폴더만)
            if (options.Scope == SearchScope.FileOnly)
            {
                AppendSeparator(sb);
                sb.Append("file:");
            }
            else if (options.Scope == SearchScope.FolderOnly)
            {
                AppendSeparator(sb);
                sb.Append("folder:");
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
                            // ts는 코드이기도 하지만 영상(.ts)도 포함하고 .m3u8도 포함
                            mediaQueries.Add("(video:|ext:m3u8;ts)");
                            break;
                        case "음악":
                            mediaQueries.Add("audio:");
                            break;
                        case "사진":
                            mediaQueries.Add("pic:");
                            break;
                        case "문서":
                            mediaQueries.Add("doc:");
                            break;
                        case "실행":
                            mediaQueries.Add("ext:exe;bat;cmd;msi");
                            break;
                        case "압축":
                            mediaQueries.Add("ext:zip;7z;rar;tar;gz");
                            break;
                        case "코드":
                            mediaQueries.Add("ext:ts;tsx;js;jsx;java;py;cpp;cs;html;css");
                            break;
                    }
                }
            }

            if (mediaQueries.Count > 0)
            {
                AppendSeparator(sb);
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
                var exts = options.CustomExtensions.Replace(",", ";").Trim();
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

            return sb.ToString().Trim();
        }

        private static string ReplaceAliases(string query, Dictionary<string, List<string>> mappings)
        {
            // 1. 역방향 및 그룹 확장을 위한 전역 동의어 확장 맵 빌드
            var aliasGroups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in mappings)
            {
                var keyword = kvp.Key.Trim();
                if (string.IsNullOrEmpty(keyword)) continue;

                // 단어와 매핑된 동의어들을 하나의 그룹으로 취합
                var group = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { keyword };
                if (kvp.Value != null)
                {
                    foreach (var syn in kvp.Value)
                    {
                        var trimmedSyn = syn.Trim().TrimEnd(';'); // 세미콜론 등 불필요 후행 문자 제거
                        if (!string.IsNullOrEmpty(trimmedSyn))
                        {
                            group.Add(trimmedSyn);
                        }
                    }
                }

                // 그룹 내 모든 원소에 대하여, 기존에 존재하는 그룹들과 상호 병합
                foreach (var member in group)
                {
                    if (aliasGroups.TryGetValue(member, out var existingGroup))
                    {
                        foreach (var m in group)
                        {
                            existingGroup.Add(m);
                        }
                        group = existingGroup;
                    }
                    else
                    {
                        aliasGroups[member] = group;
                    }
                }

                // 참조 그룹이 최신 셋으로 업데이트되었으므로 각 멤버들의 가리키는 셋 참조를 일치화
                foreach (var member in group)
                {
                    aliasGroups[member] = group;
                }
            }

            // 2. 검색 쿼리 토큰 단위 치환
            var matches = TokenRegex.Matches(query);
            var result = new StringBuilder();

            foreach (Match match in matches)
            {
                var token = match.Value;

                if (IsWordToken(token))
                {
                    if (aliasGroups.TryGetValue(token, out var synonyms) && synonyms != null && synonyms.Count > 1)
                    {
                        // 동의어 목록 조합: <원래단어|동의어1|동의어2...> (원래 입력 단어가 맨 처음에 오도록 정렬)
                        var list = new List<string>(synonyms);
                        list.Remove(token);
                        list.Insert(0, token);

                        result.Append($"<{string.Join("|", list)}>");
                    }
                    else
                    {
                        result.Append(token);
                    }
                }
                else
                {
                    result.Append(token);
                }

                result.Append(" ");
            }

            return result.ToString().Trim().Replace(" ( ", " (").Replace(" ) ", ") ").Replace(" ! ", " !");
        }

        private static bool IsWordToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            
            // 공백, 연산자, 부괄호 등은 치환 대상에서 제외
            if (token == "|" || token == "&" || token == "(" || token == ")" || token == "!")
                return false;

            // 큰따옴표로 감싸진 검색어는 그대로 둠
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
