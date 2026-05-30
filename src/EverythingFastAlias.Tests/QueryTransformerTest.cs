using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EverythingFastAlias.Models;
using EverythingFastAlias.Services;
using EverythingFastAlias.Native;

namespace EverythingFastAlias.Tests
{
    [TestClass]
    public class QueryTransformerTest
    {
        private Dictionary<string, List<string>> _testMappings = null!;

        [TestInitialize]
        public void Setup()
        {
            _testMappings = new Dictionary<string, List<string>>
            {
                { "사과", new List<string> { "apple", "🍎" } },
                { "바나나", new List<string> { "banana", "🍌" } }
            };
        }

        [TestMethod]
        public void Test_FastAlias_Replacement()
        {
            var options = new SearchOptions
            {
                UseFastAlias = true,
                IncludeRecycleBin = true // 테스트 간소화를 위해 휴지통 포함
            };

            // 1. 단일 단어 치환
            var result = QueryTransformer.Transform("사과", options, _testMappings);
            Assert.AreEqual("<사과|apple|🍎>", result);

            // 2. 미등록 단어 유지
            var result2 = QueryTransformer.Transform("오렌지", options, _testMappings);
            Assert.AreEqual("오렌지", result2);

            // 3. 복합 검색식 치환 (OR 및 공백)
            var result3 = QueryTransformer.Transform("사과 | 바나나", options, _testMappings);
            Assert.AreEqual("<사과|apple|🍎> | <바나나|banana|🍌>", result3);
        }

        [TestMethod]
        public void Test_Exclude_And_RecycleBin()
        {
            var options = new SearchOptions
            {
                UseFastAlias = false,
                IncludeRecycleBin = false, // 휴지통 제외 (!Recycle)
                ExcludedWords = "tmp temp"
            };

            var result = QueryTransformer.Transform("apple", options, _testMappings);
            
            // 제외 단어는 !로 묶이고 휴지통도 제외 처리
            Assert.IsTrue(result.Contains("apple"));
            Assert.IsTrue(result.Contains("!\"tmp\""));
            Assert.IsTrue(result.Contains("!\"temp\""));
            Assert.IsTrue(result.Contains("!$Recycle.Bin"));
        }

        [TestMethod]
        public void Test_MediaPresets_Filter()
        {
            var options = new SearchOptions
            {
                UseFastAlias = false,
                IncludeRecycleBin = true,
                MediaPresets = new HashSet<string> { "영상", "코드" }
            };

            var result = QueryTransformer.Transform("apple", options, _testMappings);

            // 두 개 이상의 미디어 프리셋이 지정된 경우 OR(< ... | ... >)로 조립됨
            Assert.IsTrue(result.Contains("<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts | ext:ts;tsx;js;jsx;json;java;py;pyw;cpp;c;h;cs;html;css;go;rs;sh;md;yml;yaml>"));
        }

        [TestMethod]
        public void Test_FolderPreset_With_FastAlias_And_OR()
        {
            var options = new SearchOptions
            {
                UseFastAlias = true,
                IncludeRecycleBin = true,
                MediaPresets = new HashSet<string> { "폴더" }
            };

            var result = QueryTransformer.Transform("사과 | 바나나", options, _testMappings);
            Assert.AreEqual("<folder:사과 | folder:apple | folder:🍎> | <folder:바나나 | folder:banana | folder:🍌>", result);
        }

        [TestMethod]
        public void Test_FolderConstraint_With_Recursive()
        {
            var options = new SearchOptions
            {
                UseFastAlias = false,
                IncludeRecycleBin = true,
                FolderPaths = @"C:\Project1;D:\Project2",
                RecursiveSearch = true // 재귀 ON ➔ path:
            };

            var result = QueryTransformer.Transform("banana", options, _testMappings);
            Assert.IsTrue(result.Contains(@"<path:""C:\Project1"" | path:""D:\Project2"">"));

            // 재귀 OFF ➔ parent:
            options.RecursiveSearch = false;
            var resultNonRecursive = QueryTransformer.Transform("banana", options, _testMappings);
            Assert.IsTrue(resultNonRecursive.Contains(@"<parent:""C:\Project1"" | parent:""D:\Project2"">"));
        }

        [TestMethod]
        public void Test_FastAlias_Bidirectional_Replacement()
        {
            var options = new SearchOptions
            {
                UseFastAlias = true,
                IncludeRecycleBin = true
            };

            // 1. 역방향 치환 (동의어 값인 'apple'로 검색 시)
            var result1 = QueryTransformer.Transform("apple", options, _testMappings);
            Assert.AreEqual("<apple|사과|🍎>", result1);

            // 2. 다른 동의어인 '🍎'로 검색 시
            var result2 = QueryTransformer.Transform("🍎", options, _testMappings);
            Assert.AreEqual("<🍎|사과|apple>", result2);
        }

        [TestMethod]
        public void Test_FastAlias_Space_Contain_Replacement()
        {
            var options = new SearchOptions
            {
                UseFastAlias = true,
                IncludeRecycleBin = true
            };

            var spaceMappings = new Dictionary<string, List<string>>
            {
                { "Akari Asagiri", new List<string> { "Akari Asayiri", "朝桐光" } }
            };

            // 1. 공백이 포함된 원본 키워드로 검색 시 양방향 치환 검증
            var result1 = QueryTransformer.Transform("Akari Asagiri", options, spaceMappings);
            // 공백이 포함된 토큰은 큰따옴표가 입혀진 형태로 포함되어야 함
            Assert.IsTrue(result1.Contains("\"Akari Asagiri\"") || result1.Contains("Akari Asagiri"));
            Assert.IsTrue(result1.Contains("\"Akari Asayiri\""));
            Assert.IsTrue(result1.Contains("朝桐光"));

            // 2. 동의어(공백 포함)로 검색 시에도 정상 치환 검증
            var result2 = QueryTransformer.Transform("Akari Asayiri", options, spaceMappings);
            Assert.IsTrue(result2.Contains("\"Akari Asayiri\"") || result2.Contains("Akari Asayiri"));
            Assert.IsTrue(result2.Contains("\"Akari Asagiri\""));
            Assert.IsTrue(result2.Contains("朝桐光"));
        }

        [TestMethod]
        public void Test_Everything_Dll_Nesting_Bug()
        {
            if (!EverythingBridge.IsEverythingRunning())
            {
                Assert.Inconclusive("Everything 서비스가 켜져 있지 않아 DLL 테스트를 건너뜁니다.");
                return;
            }

            var options = new SearchOptions { Scope = SearchScope.All };

            // O드라이브 누수가 발생하는지 확인하는 테스트 케이스들
            string[] testQueries = new[]
            {
                // 1. 단순 OR에 folder: 붙임 (유저가 겪는 버그 재현 시도)
                "<마키 호조 | Maki Hojo | 北条麻妃> folder: <P:>",
                
                // 2. folder: 태그를 가장 앞에 배치
                "folder: <마키 호조 | Maki Hojo | 北条麻妃> <P:>",
                
                // 3. folder: 대신 attrib:d (디렉토리 속성) 사용
                "<마키 호조 | Maki Hojo | 北条麻妃> attrib:d <P:>",
                
                // 4. 경로 검색(Scope=All 흉내)을 명시적으로 포함
                "<마키 호조 | Maki Hojo | 北条麻妃 | path:\"마키 호조\" | path:\"Maki Hojo\"> folder: <P:>",
                
                // 5. 경로 검색 포함 + attrib:d
                "<마키 호조 | Maki Hojo | 北条麻妃 | path:\"마키 호조\" | path:\"Maki Hojo\"> attrib:d <P:>",
                
                // 6. 꺾쇠 괄호(<>) 안에 중첩 (< < > >)
                "< <마키 호조 | Maki Hojo | 北条麻妃 | path:\"마키 호조\" | path:\"Maki Hojo\"> > folder: <P:>"
            };

            foreach (var query in testQueries)
            {
                System.Diagnostics.Debug.WriteLine($"\n--- [Query]: {query} ---");
                var results = EverythingBridge.Search(query, options);

                int nonPDriveCount = 0;
                int oDriveCount = 0;
                for (int i = 0; i < results.Count; i++)
                {
                    if (!results[i].Path.StartsWith("P:", System.StringComparison.OrdinalIgnoreCase))
                    {
                        nonPDriveCount++;
                        if (results[i].Path.StartsWith("O:", System.StringComparison.OrdinalIgnoreCase))
                        {
                            oDriveCount++;
                            if (oDriveCount <= 3) // 처음 3개만 출력
                                System.Diagnostics.Debug.WriteLine($"  [LEAK]: {results[i].Path}\\{results[i].Name}");
                        }
                    }
                }
                System.Diagnostics.Debug.WriteLine($"[Count]: {results.Count}, [Non-P]: {nonPDriveCount}, [O-Drive]: {oDriveCount}");
            }
        }
    }
}
