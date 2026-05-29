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
            var matches = TokenRegex.Matches(query);
            var result = new StringBuilder();

            foreach (Match match in matches)
            {
                var token = match.Value;

                // 연산자나 큰따옴표 묶음이 아닌 일반 단어 토큰인 경우 동의어 치환 시도
                if (IsWordToken(token))
                {
                    if (mappings.TryGetValue(token, out var synonyms) && synonyms != null && synonyms.Count > 0)
                    {
                        // 동의어 목록 조합: <원래단어|동의어1|동의어2...>
                        var list = new List<string> { token };
                        foreach (var syn in synonyms)
                        {
                            if (!list.Contains(syn)) list.Add(syn);
                        }
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

                // 토큰 사이에 공백 보존용 (원문 공백 복원 헬퍼 대신 간단한 토큰 띄우기)
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
