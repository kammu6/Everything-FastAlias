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
                { "바나나", new List<string> { "banana", "🍌" } },
                { "Akari Asagiri", new List<string> { "Akari Asayiri", "朝桐光" } }
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
            Assert.AreEqual("<사과 | apple | 🍎>", result);

            // 2. 미등록 단어 유지
            var result2 = QueryTransformer.Transform("오렌지", options, _testMappings);
            Assert.AreEqual("오렌지", result2);

            // 3. 복합 검색식 치환 (OR 및 공백)
            var result3 = QueryTransformer.Transform("사과 | 바나나", options, _testMappings);
            Assert.AreEqual("<사과 | apple | 🍎> | <바나나 | banana | 🍌>", result3);
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
            StringAssert.Contains(result, "apple");
            StringAssert.Contains(result, "!\"tmp\"");
            StringAssert.Contains(result, "!\"temp\"");
            StringAssert.Contains(result, "!$Recycle.Bin");
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
            StringAssert.Contains(result, "<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts | ext:ts;tsx;js;jsx;json;java;py;pyw;cpp;c;h;cs;html;css;go;rs;sh;md;yml;yaml>");
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
        public void Test_UserManual_Drive_And_Constraints_Preserved()
        {
            var options = new SearchOptions
            {
                UseFastAlias = true,
                IncludeRecycleBin = true,
                Scope = SearchScope.All,
                MediaPresets = new HashSet<string> { "폴더" }
            };

            var result = QueryTransformer.Transform("path:p: 사과", options, _testMappings);
            Assert.AreEqual("path:p: <folder:사과 | folder:path:사과 | folder:apple | folder:path:apple | folder:🍎 | folder:path:🍎>", result);
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
            StringAssert.Contains(result, @"<path:""C:\Project1"" | path:""D:\Project2"">");
            
            // 재귀 OFF ➔ parent:
            options.RecursiveSearch = false;
            var resultNonRecursive = QueryTransformer.Transform("banana", options, _testMappings);
            StringAssert.Contains(resultNonRecursive, @"<parent:""C:\Project1"" | parent:""D:\Project2"">");
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
            Assert.AreEqual("<apple | 사과 | 🍎>", result1);

            // 2. 다른 동의어인 '🍎'로 검색 시
            var result2 = QueryTransformer.Transform("🍎", options, _testMappings);
            Assert.AreEqual("<🍎 | 사과 | apple>", result2);

            // 3. 공백 포함 원본 키워드로 검색 시 양방향 치환 검증
            var result3 = QueryTransformer.Transform("Akari Asagiri", options, _testMappings);
            Assert.IsTrue(result3.Contains("\"Akari Asagiri\"") || result3.Contains("Akari Asagiri"));
            StringAssert.Contains(result3, "\"Akari Asayiri\"");
            StringAssert.Contains(result3, "朝桐光");

            // 4. 공백 포함 동의어로 검색 시에도 양방향 치환 검증
            var result4 = QueryTransformer.Transform("Akari Asayiri", options, _testMappings);
            Assert.IsTrue(result4.Contains("\"Akari Asayiri\"") || result4.Contains("Akari Asayiri"));
            StringAssert.Contains(result4, "\"Akari Asagiri\"");
            StringAssert.Contains(result4, "朝桐光");
        }

        [TestMethod]
        public void Test_FolderPreset_Ignores_FileSizeFilter()
        {
            var options = new SearchOptions
            {
                UseFastAlias = false,
                IncludeRecycleBin = true,
                MediaPresets = new HashSet<string> { "폴더" },
                MinSize = 10,
                MinSizeUnit = SizeUnit.MB,
                MaxSize = 100,
                MaxSizeUnit = SizeUnit.GB
            };

            var result = QueryTransformer.Transform("BEST", options, _testMappings);
            
            // folder가 활성화되어 있으면 size 조건은 생략되어야 함
            StringAssert.Contains(result, "folder:BEST");
            Assert.IsFalse(result.Contains("size:"), "Size filter should be bypassed when Folder media preset is active.");
        }

        [TestMethod]
        public void Test_UserSpecified_PathConstraint_NotWrappedByFolder()
        {
            var options = new SearchOptions
            {
                UseFastAlias = false,
                IncludeRecycleBin = true,
                MediaPresets = new HashSet<string> { "폴더" },
                Scope = SearchScope.All
            };

            var result = QueryTransformer.Transform(@"BEST path:P:\1_이미지\$eropuru\", options, _testMappings);

            // path:P:\... 는 그대로 보존되고 folder:가 씌워지지 않아야 함
            StringAssert.Contains(result, @"path:P:\1_이미지\$eropuru\");
            Assert.IsFalse(result.Contains(@"folder:path:P:\1_이미지\$eropuru\"));
            
            // 일반 단어 BEST는 올바르게 folder로 매핑되어야 함
            StringAssert.Contains(result, "<folder:BEST | folder:path:BEST>");
        }

        [TestMethod]
        public void Test_GeneralSearchWord_ScopeAndFolderWrapped()
        {
            var options = new SearchOptions
            {
                UseFastAlias = false,
                IncludeRecycleBin = true,
                MediaPresets = new HashSet<string> { "폴더" },
                Scope = SearchScope.All
            };

            var result = QueryTransformer.Transform("BEST", options, _testMappings);
            Assert.AreEqual("<folder:BEST | folder:path:BEST>", result);
        }

        [TestMethod]
        public void Test_RegexOption_Applies_RegexModifier_To_GeneralWords()
        {
            var options = new SearchOptions
            {
                UseFastAlias = false,
                IncludeRecycleBin = true,
                UseRegex = true,
                Scope = SearchScope.All,
                MediaPresets = new HashSet<string> { "폴더" }
            };

            var result = QueryTransformer.Transform("1[2-9]分", options, _testMappings);
            // regex 옵션이 켜져 있으면 일반 단어 토큰 앞에 regex: 가 자동으로 결합되어야 함
            Assert.AreEqual("<folder:regex:1[2-9]分 | folder:path:regex:1[2-9]分>", result);
        }

    }
}
