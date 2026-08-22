using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EverythingFastAlias.Config;
using EverythingFastAlias.Models;
using EverythingFastAlias.Services;

namespace EverythingFastAlias.Tests
{
    [TestClass]
    public class QueryTransformerTest
    {
        private Dictionary<string, HashSet<string>> _testMappings = null!;
        private Dictionary<string, HashSet<string>> _directMappings = null!;
        private Dictionary<string, HashSet<string>> _aliasMappings = null!;

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
                { "A", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "b", "c" } },
                { "B", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "c", "d", "f" } },
                { "c", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "b", "c", "d", "f" } }
            };

            // 공식 가이드 4그룹 모델 (#a, d, e, h)
            _directMappings = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "#a", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "b", "c" } },
                { "d", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "e", "c" } },
                { "e", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "f", "g" } },
                { "h", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "i", "e" } }
            };

            _aliasMappings = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "b", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "b", "c" } },
                { "c", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "b", "c", "e" } },
                { "e", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "e", "c", "i" } },
                { "f", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "f", "g" } },
                { "g", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "f", "g" } },
                { "i", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "i", "e" } }
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
                CustomExtensions = FileExtensionConstants.VideoExtensions,
                MinSize = 10,
                MinSizeUnit = SizeUnit.MB,
                IncludeRecycleBin = true
            };

            var result = QueryTransformer.Transform("#back_to_freedom | Lana Rhoades", options, _testMappings);
            
            // To-Be (B안 반영): #back_to_freedom 원본은 소거되고 순수 동의어만 치환 결합됨
            Assert.AreEqual($@"<<<path:<Adriana Chechik>> | <path:<Megan Rain>> | <path:<Ava Addams>>> | <path:<Lana Rhoades>>> <path:K:\ | path:N:\ | path:P:\> <file:<ext:{FileExtensionConstants.VideoExtensions}>> <size:>=10mb>", result);
        }

        [TestMethod]
        public void Test_Case2_General_Search_With_Wildcard()
        {
            var options = new SearchOptions
            {
                UseFastAlias = true,
                TargetDrives = new HashSet<string> { "K:", "N:", "P:" },
                Scope = SearchScope.All,
                CustomExtensions = FileExtensionConstants.VideoExtensions,
                MinSize = 10,
                MinSizeUnit = SizeUnit.MB,
                IncludeRecycleBin = true
            };

            var result = QueryTransformer.Transform("Adriana *", options, _testMappings);
            
            Assert.AreEqual($@"<path:<Adriana *>> <path:K:\ | path:N:\ | path:P:\> <file:<ext:{FileExtensionConstants.VideoExtensions}>> <size:>=10mb>", result);
        }

        [TestMethod]
        public void Test_Case3_Single_General_Search()
        {
            var options = new SearchOptions
            {
                UseFastAlias = true,
                TargetDrives = new HashSet<string> { "K:", "N:", "P:" },
                Scope = SearchScope.All,
                CustomExtensions = FileExtensionConstants.VideoExtensions,
                MinSize = 10,
                MinSizeUnit = SizeUnit.MB,
                IncludeRecycleBin = true
            };

            var result = QueryTransformer.Transform("Lana Rhoades", options, _testMappings);
            
            Assert.AreEqual($@"<path:<Lana Rhoades>> <path:K:\ | path:N:\ | path:P:\> <file:<ext:{FileExtensionConstants.VideoExtensions}>> <size:>=10mb>", result);
        }

        [TestMethod]
        public void Test_Case4_Multiple_General_Search()
        {
            var options = new SearchOptions
            {
                UseFastAlias = true,
                TargetDrives = new HashSet<string> { "K:", "N:", "P:" },
                Scope = SearchScope.All,
                CustomExtensions = FileExtensionConstants.VideoExtensions,
                MinSize = 10,
                MinSizeUnit = SizeUnit.MB,
                IncludeRecycleBin = true
            };

            var result = QueryTransformer.Transform("Lana Rhoades | India Summer", options, _testMappings);
            
            Assert.AreEqual($@"<<path:<Lana Rhoades>> | <path:<India Summer>>> <path:K:\ | path:N:\ | path:P:\> <file:<ext:{FileExtensionConstants.VideoExtensions}>> <size:>=10mb>", result);
        }

        [TestMethod]
        public void Test_Case5_Negative_Search_With_Quotes()
        {
            var options = new SearchOptions
            {
                UseFastAlias = true,
                TargetDrives = new HashSet<string> { "K:", "N:", "P:" },
                Scope = SearchScope.All,
                CustomExtensions = FileExtensionConstants.VideoExtensions,
                MinSize = 10,
                MinSizeUnit = SizeUnit.MB,
                IncludeRecycleBin = true
            };

            var result = QueryTransformer.Transform("!\"LIFE SELECTOR\"", options, _testMappings);
            
            Assert.AreEqual($@"<path:!<""LIFE SELECTOR"">> <path:K:\ | path:N:\ | path:P:\> <file:<ext:{FileExtensionConstants.VideoExtensions}>> <size:>=10mb>", result);
        }

        [TestMethod]
        public void Test_Case6_Negative_Search_Without_Quotes()
        {
            var options = new SearchOptions
            {
                UseFastAlias = true,
                TargetDrives = new HashSet<string> { "K:", "N:", "P:" },
                Scope = SearchScope.All,
                CustomExtensions = FileExtensionConstants.VideoExtensions,
                MinSize = 10,
                MinSizeUnit = SizeUnit.MB,
                IncludeRecycleBin = true
            };

            var result = QueryTransformer.Transform("!LIFE SELECTOR", options, _testMappings);
            
            Assert.AreEqual($@"<path:!<LIFE> | path: <SELECTOR>> <path:K:\ | path:N:\ | path:P:\> <file:<ext:{FileExtensionConstants.VideoExtensions}>> <size:>=10mb>", result);
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
            StringAssert.Contains(result, @"<<path:<c:\test>> | <path:<d:\test>>>");
            // 제외경로: <<path:!<k:\test>> | <path:!<n:\test>>>
            StringAssert.Contains(result, @"<<path:!<k:\test>> | <path:!<n:\test>>>");
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
            string expected = @"<regex:<test>> <<path:<c:\test>> | <path:<d:\test>> | <path:<k:\test>> | <path:<n:\test>>> <path:!<p:\test>> <!<a> | !<b> | !<c> | !<d>> <path:K:\ | path:N:\ | path:P:\> <file:<ext:test1;test2>> <size:>=10mb>";
            
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

        [TestMethod]
        public void Test_Case_Tag_Search_Excludes_Original_Keyword()
        {
            var options = new SearchOptions { UseFastAlias = true, PrioritizeKeywordMatch = true, IncludeRecycleBin = true };
            var result = QueryTransformer.Transform("#a", options, _directMappings, _aliasMappings);
            Assert.AreEqual("<<<b>> | <<c>>>", result);

            options.PrioritizeKeywordMatch = false;
            var resultOff = QueryTransformer.Transform("#a", options, _directMappings, _aliasMappings);
            Assert.AreEqual("<<<b>> | <<c>>>", resultOff);
        }

        [TestMethod]
        public void Test_Case_Single_Group_Word()
        {
            var options = new SearchOptions { UseFastAlias = true, PrioritizeKeywordMatch = true, IncludeRecycleBin = true };
            var result = QueryTransformer.Transform("b", options, _directMappings, _aliasMappings);
            Assert.AreEqual("<<<b>> | <<c>>>", result);

            options.PrioritizeKeywordMatch = false;
            var resultOff = QueryTransformer.Transform("b", options, _directMappings, _aliasMappings);
            Assert.AreEqual("<<<b>> | <<c>>>", resultOff);
        }

        [TestMethod]
        public void Test_Case_Transitive_Common_Word()
        {
            var options = new SearchOptions { UseFastAlias = true, PrioritizeKeywordMatch = true, IncludeRecycleBin = true };
            var resultOn = QueryTransformer.Transform("c", options, _directMappings, _aliasMappings);
            Assert.AreEqual("<<<b>> | <<c>> | <<e>>>", resultOn);

            options.PrioritizeKeywordMatch = false;
            var resultOff = QueryTransformer.Transform("c", options, _directMappings, _aliasMappings);
            Assert.AreEqual("<<<b>> | <<c>> | <<e>>>", resultOff);
        }

        [TestMethod]
        public void Test_Case_Keyword_Priority_ON_Direct_Mapping()
        {
            // [ON] 검색어 'e'는 그룹 3의 키워드이므로 고유 정의인 f, g 만 1:1 매핑 반환
            var options = new SearchOptions { UseFastAlias = true, PrioritizeKeywordMatch = true, IncludeRecycleBin = true };
            var result = QueryTransformer.Transform("e", options, _directMappings, _aliasMappings);
            Assert.AreEqual("<<<f>> | <<g>>>", result);
        }

        [TestMethod]
        public void Test_Case_Keyword_Priority_OFF_Group_Union_Mapping()
        {
            // [OFF] 검색어 'e'는 동의어로 취급되어 e가 속한 그룹 2(e, c)와 그룹 4(i, e)의 합집합인 e, c, i 반환
            var options = new SearchOptions { UseFastAlias = true, PrioritizeKeywordMatch = false, IncludeRecycleBin = true };
            var result = QueryTransformer.Transform("e", options, _directMappings, _aliasMappings);
            Assert.AreEqual("<<<e>> | <<c>> | <<i>>>", result);
        }

        [TestMethod]
        public void Test_Real_World_User_Dataset_Sumire_Mizukawa()
        {
            // 실제 사용자 데이터셋 시뮬레이션
            // 그룹 1: Keyword = Sumire Mizukawa / Words = Sumire Mizukawa; Mizukawa Sumire; 水川スミレ
            // 그룹 2: Keyword = #지민 / Words = Mizuno Asahi; Sumire Mizukawa
            var direct = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Sumire Mizukawa"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sumire Mizukawa", "Mizukawa Sumire", "水川スミレ" },
                ["#지민"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Mizuno Asahi", "Sumire Mizukawa" }
            };

            var alias = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Sumire Mizukawa"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sumire Mizukawa", "Mizukawa Sumire", "水川スミレ", "Mizuno Asahi" },
                ["Mizukawa Sumire"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sumire Mizukawa", "Mizukawa Sumire", "水川スミレ" },
                ["水川スミレ"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sumire Mizukawa", "Mizukawa Sumire", "水川スミレ" },
                ["Mizuno Asahi"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Mizuno Asahi", "Sumire Mizukawa" }
            };

            // ON (키워드 우선): 그룹 1의 고유 정의만 1:1 조회 (Mizuno Asahi 제외)
            var optionsOn = new SearchOptions { UseFastAlias = true, PrioritizeKeywordMatch = true, IncludeRecycleBin = true };
            var resultOn = QueryTransformer.Transform("Sumire Mizukawa", optionsOn, direct, alias);
            Assert.IsTrue(resultOn.Contains("<<Mizukawa Sumire>>") && resultOn.Contains("<<Sumire Mizukawa>>") && resultOn.Contains("<<水川スミレ>>"));
            Assert.IsFalse(resultOn.Contains("Mizuno Asahi"));

            // OFF (동의어 합집합): Sumire Mizukawa가 속한 그룹 1 + 그룹 2 합집합 조회 (Mizuno Asahi 포함)
            var optionsOff = new SearchOptions { UseFastAlias = true, PrioritizeKeywordMatch = false, IncludeRecycleBin = true };
            var resultOff = QueryTransformer.Transform("Sumire Mizukawa", optionsOff, direct, alias);
            Assert.IsTrue(resultOff.Contains("<<Mizukawa Sumire>>") && resultOff.Contains("<<Sumire Mizukawa>>") && resultOff.Contains("<<水川スミレ>>"));
            Assert.IsTrue(resultOff.Contains("<<Mizuno Asahi>>"));
        }

        [TestMethod]
        public void Test_FileExtensionConstants_Integrity()
        {
            Assert.AreEqual(7, FileExtensionConstants.AllCategories.Count);
            Assert.IsTrue(FileExtensionConstants.VideoExtensions.Contains("mp4"));
            Assert.IsTrue(FileExtensionConstants.VideoExtensions.Contains("ts"));
            Assert.IsTrue(FileExtensionConstants.VideoExtensions.Contains("mkv"));
            Assert.IsTrue(FileExtensionConstants.VideoExtensions.Contains("hevc"));

            Assert.AreEqual(FileExtensionConstants.VideoExtensions, FileExtensionConstants.GetDefaultExtensions("영상"));
            Assert.AreEqual(FileExtensionConstants.AudioExtensions, FileExtensionConstants.GetDefaultExtensions("음악"));
            Assert.AreEqual(FileExtensionConstants.PictureExtensions, FileExtensionConstants.GetDefaultExtensions("사진"));
            Assert.AreEqual(FileExtensionConstants.DocumentExtensions, FileExtensionConstants.GetDefaultExtensions("문서"));
            Assert.AreEqual(FileExtensionConstants.CodeExtensions, FileExtensionConstants.GetDefaultExtensions("코드"));
            Assert.AreEqual(FileExtensionConstants.ExecutableExtensions, FileExtensionConstants.GetDefaultExtensions("실행"));
            Assert.AreEqual(FileExtensionConstants.ArchiveExtensions, FileExtensionConstants.GetDefaultExtensions("압축"));
        }

        [TestMethod]
        public void Test_ExtensionSettingsService_Normalization()
        {
            string raw = " .mp4, .mkv; avi   wmv|flv ; ; .mp4 ";
            string normalized = ExtensionSettingsService.NormalizeExtensions(raw);
            Assert.AreEqual("mp4;mkv;avi;wmv;flv", normalized);
        }
    }
}
