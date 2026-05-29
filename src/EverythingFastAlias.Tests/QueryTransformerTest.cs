using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EverythingFastAlias.Models;
using EverythingFastAlias.Services;

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
            Assert.IsTrue(result.Contains("<(video:|ext:m3u8;ts) | ext:ts;tsx;js;jsx;java;py;cpp;cs;html;css>"));
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
    }
}
