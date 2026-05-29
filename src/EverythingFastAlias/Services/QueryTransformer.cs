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
                            // video: 매크로와 함께 주요 영상 확장자 및 스트리밍 파일(m3u8, ts) 포함
                            mediaQueries.Add("(video:|ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts)");
                            break;
                        case "음악":
                            // audio: 매크로와 함께 주요 음원 확장자 포함
                            mediaQueries.Add("(audio:|ext:mp3;wav;flac;ogg;wma;m4a;aac)");
                            break;
                        case "사진":
                            // pic: 매크로와 함께 주요 이미지 확장자(웹용, 원본, 그래픽) 포함
                            mediaQueries.Add("(pic:|ext:jpg;jpeg;jfif;png;gif;bmp;webp;tiff;psd;ai;svg)");
                            break;
                        case "문서":
                            // doc: 매크로와 함께 오피스 계열, PDF, 한글(hwp/hwpx), 텍스트 포함
                            mediaQueries.Add("(doc:|ext:pdf;txt;hwp;hwpx;doc;docx;xls;xlsx;ppt;pptx;rtf)");
                            break;
                        case "실행":
                            // 주요 실행 파일 및 스크립트/설치 파일
                            mediaQueries.Add("ext:exe;bat;cmd;msi;lnk;scr;sh;pyw;lnk");
                            break;
                        case "압축":
                            // 주요 압축 포맷 및 분할 압축 포맷 포함
                            mediaQueries.Add("ext:zip;7z;rar;tar;gz;bz2;iso;alz;egg");
                            break;
                        case "코드":
                            // 웹, 모바일, 백엔드, 스크립트 및 구성 파일을 망라한 확장자 (ts 포함)
                            mediaQueries.Add("ext:ts;tsx;js;jsx;json;java;py;pyw;cpp;c;h;cs;html;css;go;rs;sh;md;yml;yaml");
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
            if (string.IsNullOrWhiteSpace(query)) return query;

            // 1. 역방향 및 그룹 확장을 위한 전역 동의어 확장 맵 빌드
            var aliasGroups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in mappings)
            {
                var keyword = kvp.Key.Trim();
                if (string.IsNullOrEmpty(keyword)) continue;

                var group = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { keyword };
                if (kvp.Value != null)
                {
                    foreach (var syn in kvp.Value)
                    {
                        var trimmedSyn = syn.Trim().TrimEnd(';');
                        if (!string.IsNullOrEmpty(trimmedSyn))
                        {
                            group.Add(trimmedSyn);
                        }
                    }
                }

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

                foreach (var member in group)
                {
                    aliasGroups[member] = group;
                }
            }

            // 2. 동의어 키 목록을 글자 수가 긴 순으로 정렬 (Longest Match First)
            var sortedKeys = new List<string>(aliasGroups.Keys);
            sortedKeys.Sort((a, b) => b.Length.CompareTo(a.Length));

            // 3. 쿼리 내에서 각 키워드 직접 치환
            string processed = query;
            var tempReplacements = new List<string>();

            foreach (var key in sortedKeys)
            {
                if (!aliasGroups.TryGetValue(key, out var synonyms) || synonyms == null || synonyms.Count <= 1)
                    continue;

                // 정규식으로 단어 경계 및 연산자 경계를 탐색 (다국어 및 공백 포함 완벽 대응)
                string escapedKey = Regex.Escape(key);
                string pattern = $@"(?<=^|[\s|&()!])" + escapedKey + @"(?=$|[\s|&()!])";

                var list = new List<string>(synonyms);
                list.Remove(key);
                list.Insert(0, key);

                // Everything 검색에서 공백이 포함된 동의어 토큰은 큰따옴표로 자동 감싸주어 검색 호환성 보장
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Contains(" ") && !list[i].StartsWith("\""))
                    {
                        list[i] = $"\"{list[i]}\"";
                    }
                }

                string replacementValue = $"<{string.Join("|", list)}>";

                // 중복 치환 방지를 위해 임시 플레이스홀더 사용
                processed = Regex.Replace(processed, pattern, m =>
                {
                    string placeholder = $"__ALIAS_PLACEHOLDER_{tempReplacements.Count}__";
                    tempReplacements.Add(replacementValue);
                    return placeholder;
                }, RegexOptions.IgnoreCase);
            }

            // 4. 최종적으로 플레이스홀더를 실제 치환값으로 복원
            for (int i = 0; i < tempReplacements.Count; i++)
            {
                processed = processed.Replace($"__ALIAS_PLACEHOLDER_{i}__", tempReplacements[i]);
            }

            return processed;
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
