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

        public static string Transform(string rawQuery, SearchOptions options, Dictionary<string, HashSet<string>> mappings)
        {
            // 1단계: 검색어 및 별칭(Alias) 결합 (우선순위 1)
            string firstStageQuery = BuildFirstStageQuery(rawQuery, options, mappings);

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(firstStageQuery))
            {
                sb.Append(firstStageQuery);
            }

            // 2단계: 지정경로(FolderPaths) 및 제외경로(ExcludedPaths) 추가 (공간적 제한/차단)
            bool isFolderPreset = options.MediaPresets != null && options.MediaPresets.Contains("폴더");

            // 지정경로 (FolderPaths)
            if (!string.IsNullOrWhiteSpace(options.FolderPaths))
            {
                var paths = options.FolderPaths.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                var pathQueries = new List<string>();
                foreach (var path in paths)
                {
                    var trimmedPath = path.Trim().Trim('"');
                    if (string.IsNullOrEmpty(trimmedPath)) continue;

                    string folderConstraint = options.RecursiveSearch ? $"<path:<{trimmedPath}>>" : $"<parent:<{trimmedPath}>>";
                    pathQueries.Add(folderConstraint);
                }

                if (pathQueries.Count > 0)
                {
                    AppendSeparator(sb);
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

            // 제외경로 (ExcludedPaths)
            if (!string.IsNullOrWhiteSpace(options.ExcludedPaths))
            {
                var paths = options.ExcludedPaths.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                var pathQueries = new List<string>();
                foreach (var path in paths)
                {
                    var trimmedPath = path.Trim().Trim('"');
                    if (string.IsNullOrEmpty(trimmedPath)) continue;

                    string folderConstraint = options.RecursiveSearch ? $"<path:!<{trimmedPath}>>" : $"<parent:!<{trimmedPath}>>";
                    pathQueries.Add(folderConstraint);
                }

                if (pathQueries.Count > 0)
                {
                    AppendSeparator(sb);
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

            // 3단계: 제외단어(ExcludedWords) 추가 (최종 정제 1)
            if (!string.IsNullOrWhiteSpace(options.ExcludedWords))
            {
                var excluded = options.ExcludedWords.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                var excludedQueries = new List<string>();
                foreach (var word in excluded)
                {
                    var trimmed = word.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        excludedQueries.Add($"!<{trimmed}>");
                    }
                }

                if (excludedQueries.Count > 0)
                {
                    AppendSeparator(sb);
                    if (excludedQueries.Count == 1)
                    {
                        sb.Append(excludedQueries[0]);
                    }
                    else
                    {
                        sb.Append($"<{string.Join(" | ", excludedQueries)}>");
                    }
                }
            }

            // 4단계: 타겟 드라이브 필터 추가 (공간적 제한 2)
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

            // 5단계: 미디어 및 파일 크기 필터 결합 (물리적 필터링)
            var filterParts = new List<string>();

            // 미디어 프리셋 필터
            if (options.MediaPresets != null && options.MediaPresets.Count > 0 && !options.MediaPresets.Contains("전체"))
            {
                foreach (var preset in options.MediaPresets)
                {
                    string? extPattern = null;
                    switch (preset)
                    {
                        case "영상":
                            extPattern = "mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts";
                            break;
                        case "음악":
                            extPattern = "mp3;wav;flac;ogg;wma;m4a;aac";
                            break;
                        case "사진":
                            extPattern = "jpg;jpeg;jfif;png;gif;bmp;webp;tiff;psd;ai;svg";
                            break;
                        case "문서":
                            extPattern = "pdf;txt;hwp;hwpx;doc;docx;xls;xlsx;ppt;pptx;rtf";
                            break;
                        case "실행":
                            extPattern = "exe;bat;cmd;msi;lnk;scr;sh;pyw";
                            break;
                        case "압축":
                            extPattern = "zip;7z;rar;tar;gz;bz2;iso;alz;egg";
                            break;
                        case "코드":
                            extPattern = "ts;tsx;js;jsx;json;java;py;pyw;cpp;c;h;cs;html;css;go;rs;sh;md;yml;yaml";
                            break;
                    }

                    if (extPattern != null)
                    {
                        if (!isFolderPreset)
                        {
                            filterParts.Add($"<file:<ext:{extPattern}>>");
                        }
                        else
                        {
                            filterParts.Add($"<ext:{extPattern}>");
                        }
                    }
                }
            }

            // 커스텀 확장자 필터
            if (!string.IsNullOrWhiteSpace(options.CustomExtensions))
            {
                var exts = Regex.Replace(options.CustomExtensions, @"\s*[,;]\s*", ";").Trim(';');
                if (!string.IsNullOrEmpty(exts))
                {
                    if (!isFolderPreset)
                    {
                        filterParts.Add($"<file:<ext:{exts}>>");
                    }
                    else
                    {
                        filterParts.Add($"<ext:{exts}>");
                    }
                }
            }

            // 확장자 필터 결합
            if (filterParts.Count > 0)
            {
                AppendSeparator(sb);
                if (filterParts.Count == 1)
                {
                    sb.Append(filterParts[0]);
                }
                else
                {
                    sb.Append($"<{string.Join(" | ", filterParts)}>");
                }
            }

            // 파일 크기 필터 적용 (폴더 프리셋인 경우 무시)
            if (!isFolderPreset)
            {
                if (options.MinSize.HasValue)
                {
                    AppendSeparator(sb);
                    sb.Append($"<size:>={options.MinSize.Value}{options.MinSizeUnit.ToString().ToLower()}>");
                }
                if (options.MaxSize.HasValue)
                {
                    AppendSeparator(sb);
                    sb.Append($"<size:<={options.MaxSize.Value}{options.MaxSizeUnit.ToString().ToLower()}>");
                }
            }

            // 6단계: 휴지통 및 시스템 폴더 제외 (최종 정제 2)
            if (!options.IncludeRecycleBin)
            {
                AppendSeparator(sb);
                sb.Append("<!$Recycle.Bin>");
            }

            return sb.ToString().Trim();
        }

        private static List<string> SplitByTopLevelPipe(string query)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(query)) return result;

            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < query.Length; i++)
            {
                char c = query[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    sb.Append(c);
                }
                else if (c == '|' && !inQuotes)
                {
                    result.Add(sb.ToString().Trim());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            if (sb.Length > 0)
            {
                result.Add(sb.ToString().Trim());
            }
            return result;
        }

        private static string BuildFirstStageQuery(string rawQuery, SearchOptions options, Dictionary<string, HashSet<string>> mappings)
        {
            if (string.IsNullOrWhiteSpace(rawQuery)) return "";

            bool isFolderPreset = options.MediaPresets != null && options.MediaPresets.Contains("폴더");
            string modifier = isFolderPreset ? "folder:" : "";
            string regPrefix = options.UseRegex ? "regex:" : "";

            string scopePrefix = "";
            string scopeSuffix = "";
            if (options.Scope == SearchScope.Path)
            {
                scopePrefix = "path:";
                scopeSuffix = "\\";
            }
            else if (options.Scope == SearchScope.File) // nopath
            {
                scopePrefix = "";
            }
            else // SearchScope.All
            {
                scopePrefix = "path:";
            }

            var terms = SplitByTopLevelPipe(rawQuery);
            var processedTerms = new List<string>();

            foreach (var term in terms)
            {
                if (string.IsNullOrWhiteSpace(term)) continue;

                if (IsConstraintOrDrive(term))
                {
                    processedTerms.Add(term);
                    continue;
                }

                // Alias 검출: Dictionary TryGetValue O(1) 직접 조회 (foreach O(n) 순회 제거)
                string aliasKey = term;
                bool isAlias = false;
                HashSet<string>? synonyms = null;

                if (options.UseFastAlias && mappings != null)
                {
                    if (mappings.TryGetValue(aliasKey, out var found))
                    {
                        isAlias = true;
                        synonyms = found;
                    }
                }

                if (isAlias)
                {
                    var aliasParts = new List<string>();
                    if (synonyms != null)
                    {
                        foreach (var syn in synonyms)
                        {
                            var trimmedSyn = syn.Trim();
                            if (string.IsNullOrEmpty(trimmedSyn)) continue;

                            bool isQuoted = trimmedSyn.StartsWith("\"") && trimmedSyn.EndsWith("\"");
                            string body = isQuoted ? trimmedSyn.Substring(1, trimmedSyn.Length - 2) : trimmedSyn;

                            string synPart = BuildTermWithScope(body, modifier, regPrefix, scopePrefix, scopeSuffix, isQuoted);
                            aliasParts.Add(synPart);
                        }
                    }

                    processedTerms.Add($"<{string.Join(" | ", aliasParts)}>");
                }
                else if (term.StartsWith("!"))
                {
                    string negContent = term.Substring(1).Trim();
                    
                    if (negContent.StartsWith("\"") && negContent.EndsWith("\""))
                    {
                        string result = $"<{modifier}{scopePrefix}!<{negContent}>>";
                        processedTerms.Add(result);
                    }
                    else if (negContent.Contains(" "))
                    {
                        int spaceIndex = negContent.IndexOf(' ');
                        string firstWord = negContent.Substring(0, spaceIndex);
                        string remaining = negContent.Substring(spaceIndex + 1).Trim();

                        string part1 = $"{modifier}{scopePrefix}!<{firstWord}>";
                        string part2 = $"{modifier}{scopePrefix} <{remaining}>";

                        processedTerms.Add($"<{part1} | {part2}>");
                    }
                    else
                    {
                        processedTerms.Add($"<{modifier}{scopePrefix}!<{negContent}>>");
                    }
                }
                else
                {
                    bool isQuoted = term.StartsWith("\"") && term.EndsWith("\"");
                    string termBody = isQuoted ? term.Substring(1, term.Length - 2) : term;

                    if (options.Scope == SearchScope.All)
                    {
                        string processedTerm = BuildTermWithScope(termBody, modifier, regPrefix, "path:", scopeSuffix, isQuoted);
                        processedTerms.Add(processedTerm);
                    }
                    else
                    {
                        string processedTerm = BuildTermWithScope(termBody, modifier, regPrefix, scopePrefix, scopeSuffix, isQuoted);
                        processedTerms.Add(processedTerm);
                    }
                }
            }

            if (processedTerms.Count == 0) return "";
            if (processedTerms.Count == 1) return processedTerms[0];

            return $"<{string.Join(" | ", processedTerms)}>";
        }

        private static string BuildTermWithScope(string word, string modifier, string regPrefix, string scopePrefix, string scopeSuffix, bool isQuoted)
        {
            if (isQuoted)
            {
                return $"<{modifier}{scopePrefix}{regPrefix}\"{word}{scopeSuffix}\">";
            }
            else
            {
                return $"<{modifier}{scopePrefix}{regPrefix}<{word}{scopeSuffix}>>";
            }
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
