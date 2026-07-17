using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EverythingFastAlias.Models;
using EverythingFastAlias.Services;

namespace EverythingFastAlias.Tests
{
    [TestClass]
    public class QueryTransformerTest
    {
        private Dictionary<string, HashSet<string>> _testMappings = null!;

        [TestInitialize]
        public void Setup()
        {
            // Option B(원본 제외 및 동적 합집합)가 반영된 테스트 스냅샷 구성
            _testMappings = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "사과", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "apple", "🍎" } },
                { "apple", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "apple", "🍎" } },
                { "🍎", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "apple", "🍎" } },

                { "바나나", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "banana", "🍌" } },
                { "banana", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "banana", "🍌" } },
                { "🍌", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "banana", "🍌" } },

                { "#back_to_freedom", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Adriana Chechik", "Megan Rain", "Ava Addams" } },
                { "Adriana Chechik", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Adriana Chechik", "Megan Rain", "Ava Addams" } },
                { "Megan Rain", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Adriana Chechik", "Megan Rain", "Ava Addams" } },
                { "Ava Addams", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Adriana Chechik", "Megan Rain", "Ava Addams" } },

                { "Akari Asagiri", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Akari Asayiri", "朝桐光" } },
                { "Akari Asayiri", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Akari Asayiri", "朝桐光" } },
                { "朝桐光", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Akari Asayiri", "朝桐光" } },

                // 전이성(Transitive) 다중 그룹 합집합 테스트 데이터
                // A -> a,b,c
                // B -> c,d,f
                // c는 공통 동의어이므로 합집합 { a, b, c, d, f } 를 가짐
                { "A", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "b", "c" } },
                { "B", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "c", "d", "f" } },
                { "c", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "b", "c", "d", "f" } }
            };
        }

        [TestMethod]
        public void Test_Case1_Mixed_Alias_And_General_Search()
        {
            var options = new SearchOptions
            {
                UseFastAlias = true,
                TargetDrives = new HashSet<string> { "K:", "N:", "P:" },
                Scope = SearchScope.All, // 전체
                MediaPresets = new HashSet<string> { "영상" },
                MinSize = 10,
                MinSizeUnit = SizeUnit.MB,
                IncludeRecycleBin = true
            };

            var result = QueryTransformer.Transform("#back_to_freedom | Lana Rhoades", options, _testMappings);
            
            // To-Be (B안 반영): #back_to_freedom 원본은 소거되고 순수 동의어만 치환 결합됨
            // Expected: <<path:"Adriana Chechik"> | <path:"Megan Rain"> | <path:"Ava Addams"> | <path:<Lana Rhoades>>> ...
            Assert.AreEqual(@"<<<path:<Adriana Chechik>> | <path:<Megan Rain>> | <path:<Ava Addams>>> | <path:<Lana Rhoades>>> <path:K:\ | path:N:\ | path:P:\> <file:<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts>> <size:>=10mb>", result);
        }

        [TestMethod]
        public void Test_Case2_General_Search_With_Wildcard()
        {
            var options = new SearchOptions
            {
                UseFastAlias = true,
                TargetDrives = new HashSet<string> { "K:", "N:", "P:" },
                Scope = SearchScope.All,
                MediaPresets = new HashSet<string> { "영상" },
                MinSize = 10,
                MinSizeUnit = SizeUnit.MB,
                IncludeRecycleBin = true
            };

            var result = QueryTransformer.Transform("Adriana *", options, _testMappings);
            
            Assert.AreEqual(@"<path:<Adriana *>> <path:K:\ | path:N:\ | path:P:\> <file:<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts>> <size:>=10mb>", result);
        }

        [TestMethod]
        public void Test_Case3_Single_General_Search()
        {
            var options = new SearchOptions
            {
                UseFastAlias = true,
                TargetDrives = new HashSet<string> { "K:", "N:", "P:" },
                Scope = SearchScope.All,
                MediaPresets = new HashSet<string> { "영상" },
                MinSize = 10,
                MinSizeUnit = SizeUnit.MB,
                IncludeRecycleBin = true
            };

            var result = QueryTransformer.Transform("Lana Rhoades", options, _testMappings);
            
            Assert.AreEqual(@"<path:<Lana Rhoades>> <path:K:\ | path:N:\ | path:P:\> <file:<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts>> <size:>=10mb>", result);
        }

        [TestMethod]
        public void Test_Case4_Multiple_General_Search()
        {
            var options = new SearchOptions
            {
                UseFastAlias = true,
                TargetDrives = new HashSet<string> { "K:", "N:", "P:" },
                Scope = SearchScope.All,
                MediaPresets = new HashSet<string> { "영상" },
                MinSize = 10,
                MinSizeUnit = SizeUnit.MB,
                IncludeRecycleBin = true
            };

            var result = QueryTransformer.Transform("Lana Rhoades | India Summer", options, _testMappings);
            
            Assert.AreEqual(@"<<path:<Lana Rhoades>> | <path:<India Summer>>> <path:K:\ | path:N:\ | path:P:\> <file:<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts>> <size:>=10mb>", result);
        }

        [TestMethod]
        public void Test_Case5_Negative_Search_With_Quotes()
        {
            var options = new SearchOptions
            {
                UseFastAlias = true,
                TargetDrives = new HashSet<string> { "K:", "N:", "P:" },
                Scope = SearchScope.All,
                MediaPresets = new HashSet<string> { "영상" },
                MinSize = 10,
                MinSizeUnit = SizeUnit.MB,
                IncludeRecycleBin = true
            };

            var result = QueryTransformer.Transform("!\"LIFE SELECTOR\"", options, _testMappings);
            
            Assert.AreEqual(@"<path:!<""LIFE SELECTOR"">> <path:K:\ | path:N:\ | path:P:\> <file:<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts>> <size:>=10mb>", result);
        }

        [TestMethod]
        public void Test_Case6_Negative_Search_Without_Quotes()
        {
            var options = new SearchOptions
            {
                UseFastAlias = true,
                TargetDrives = new HashSet<string> { "K:", "N:", "P:" },
                Scope = SearchScope.All,
                MediaPresets = new HashSet<string> { "영상" },
                MinSize = 10,
                MinSizeUnit = SizeUnit.MB,
                IncludeRecycleBin = true
            };

            var result = QueryTransformer.Transform("!LIFE SELECTOR", options, _testMappings);
            
            Assert.AreEqual(@"<path:!<LIFE> | path: <SELECTOR>> <path:K:\ | path:N:\ | path:P:\> <file:<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts>> <size:>=10mb>", result);
        }

        [TestMethod]
        public void Test_FolderPaths_And_ExcludedPaths_NestedGrouping()
        {
            var options = new SearchOptions
            {
                UseFastAlias = false,
                IncludeRecycleBin = true,
                FolderPaths = "c:\\test,d:\\test",
                ExcludedPaths = "k:\\test|n:\\test",
                RecursiveSearch = true
            };

            var result = QueryTransformer.Transform("test", options, _testMappings);
            
            // 지정경로: <<path:<c:\test>> | <path:<d:\test>>>
            StringAssert.Contains(result, "<<path:<c:\\test>> | <path:<d:\\test>>>");
            // 제외경로: <<path:!<k:\test>> | <path:!<n:\test>>>
            StringAssert.Contains(result, "<<path:!<k:\\test>> | <path:!<n:\\test>>>");
        }

        [TestMethod]
        public void Test_Exclude_Words_Grouping()
        {
            var options = new SearchOptions
            {
                UseFastAlias = false,
                IncludeRecycleBin = true,
                ExcludedWords = "a,b;c|d"
            };

            var result = QueryTransformer.Transform("test", options, _testMappings);
            
            // 제외단어: <!<a> | !<b> | !<c> | !<d>>
            StringAssert.Contains(result, "<!<a> | !<b> | !<c> | !<d>>");
        }

        [TestMethod]
        public void Test_UserComplexScenario()
        {
            var options = new SearchOptions
            {
                UseFastAlias = false,
                TargetDrives = new HashSet<string> { "K:", "N:", "P:" },
                Scope = SearchScope.File,
                MediaPresets = new HashSet<string> { "영상", "음악" },
                CustomExtensions = "test1;test2",
                MinSize = 10,
                MinSizeUnit = SizeUnit.MB,
                IncludeRecycleBin = true,
                ExcludedWords = "a,b;c|d",
                FolderPaths = "c:\\test,d:\\test;k:\\test|n:\\test",
                ExcludedPaths = "p:\\test",
                RecursiveSearch = true,
                UseRegex = true
            };

            var result = QueryTransformer.Transform("test", options, _testMappings);

            // 예상 To-Be:
            // <regex:<test>> <<path:<c:\test>> | <path:<d:\test>> | <path:<k:\test>> | <path:<n:\test>>> <path:!<p:\test>> <!<a> | !<b> | !<c> | !<d>> <path:K:\ | path:N:\ | path:P:\> <<file:<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts>> | <file:<ext:mp3;wav;flac;ogg;wma;m4a;aac>> | <file:<ext:test1;test2>>> <size:>=10mb>
            string expected = @"<regex:<test>> <<path:<c:\test>> | <path:<d:\test>> | <path:<k:\test>> | <path:<n:\test>>> <path:!<p:\test>> <!<a> | !<b> | !<c> | !<d>> <path:K:\ | path:N:\ | path:P:\> <<file:<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts>> | <file:<ext:mp3;wav;flac;ogg;wma;m4a;aac>> | <file:<ext:test1;test2>>> <size:>=10mb>";
            
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void Test_Transitive_Alias_Mapping_OptionB()
        {
            var options = new SearchOptions
            {
                UseFastAlias = true,
                IncludeRecycleBin = true
            };

            // 1. 원본 키워드 'A' 검색 시 ➔ a | b | c (A 소거됨)
            var resultA = QueryTransformer.Transform("A", options, _testMappings);
            Assert.AreEqual("<<<a>> | <<b>> | <<c>>>", resultA);

            // 2. 동의어 'a' 검색 시 ➔ a | b | c
            var result_a = QueryTransformer.Transform("a", options, _testMappings);
            Assert.AreEqual("<<<a>> | <<b>> | <<c>>>", result_a);

            // 3. 다중 그룹 공통 동의어 'c' 검색 시 ➔ a | b | c | d | f (합집합)
            var result_c = QueryTransformer.Transform("c", options, _testMappings);
            Assert.AreEqual("<<<a>> | <<b>> | <<c>> | <<d>> | <<f>>>", result_c);
        }
    }
}
