using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ExcelDataReader;

namespace EverythingFastAlias.Services
{
    public class ExcelService
    {
        static ExcelService()
        {
            // .NET Core/9.0+ 환경에서 ExcelDataReader 인코딩 에러 방지를 위해 인코딩 공급자 등록
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static List<(string Keyword, string Words)> ImportExcel(string filePath)
        {
            var resultList = new List<(string Keyword, string Words)>();

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("지정된 엑셀 파일을 찾을 수 없습니다.", filePath);
            }

            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = Path.GetExtension(filePath).Equals(".csv", StringComparison.OrdinalIgnoreCase)
                ? ExcelReaderFactory.CreateCsvReader(stream, new ExcelReaderConfiguration { FallbackEncoding = Encoding.GetEncoding(949) })
                : ExcelReaderFactory.CreateReader(stream);

            // 첫 번째 행은 헤더로 간주하여 건너뛰기 여부 결정 (Keyword, Words/Aliases 등으로 매핑)
            bool isFirstRow = true;

            while (reader.Read())
            {
                if (isFirstRow)
                {
                    isFirstRow = false;
                    
                    // 만약 첫 줄이 데이터 형태인 경우(헤더가 아니거나 비어있지 않은 경우) 헤더 판단 로직
                    // 통상적으로 엑셀 임포트 템플릿의 첫 열 이름이 'Keyword' 혹은 '원본'이면 스킵
                    var firstCol = reader.GetValue(0)?.ToString();
                    if (firstCol != null && (firstCol.Equals("Keyword", StringComparison.OrdinalIgnoreCase) || 
                                             firstCol.Contains("원본") || 
                                             firstCol.Contains("키워드")))
                    {
                        continue;
                    }
                }

                var keyword = reader.GetValue(0)?.ToString()?.Trim();
                var words = reader.GetValue(1)?.ToString()?.Trim();

                if (!string.IsNullOrEmpty(keyword) && !string.IsNullOrEmpty(words))
                {
                    resultList.Add((keyword, words));
                }
            }

            return resultList;
        }

        public static void GenerateTemplate(string filePath)
        {
            // 간단하게 CSV 양식으로 생성하거나, 엑셀 템플릿이 필요한 경우를 위해 양식 안내 제공
            // 여기서는 사용자 편의를 위해 CSV 포맷의 파일을 생성해주는 헬퍼
            var sb = new StringBuilder();
            sb.AppendLine("Keyword,Words");
            sb.AppendLine("사과,apple;🍎;red_apple");
            sb.AppendLine("바나나,banana;🍌;yellow");
            sb.AppendLine("코드,ts;tsx;js;jsx;java;py;cpp;cs;html;css");
            
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public static void ExportToCsv(string filePath, List<(string Keyword, string Words)> data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Keyword,Words");
            foreach (var item in data)
            {
                string cleanKeyword = EscapeCsv(item.Keyword);
                string cleanWords = EscapeCsv(item.Words);
                sb.AppendLine($"{cleanKeyword},{cleanWords}");
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            bool mustQuote = value.Contains(",") || value.Contains("\"") || value.Contains("\r") || value.Contains("\n");
            if (mustQuote)
            {
                value = value.Replace("\"", "\"\"");
                return $"\"{value}\"";
            }
            return value;
        }
    }
}
