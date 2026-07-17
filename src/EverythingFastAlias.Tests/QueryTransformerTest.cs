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
                { "바나나", new List<string> { "banana", "🍌" } },
                { "#back_to_freedom", new List<string> { "Adriana Chechik", "Megan Rain", "Ava Addams" } },
                { "Akari Asagiri", new List<string> { "Akari Asayiri", "朝桐光" } }
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
            
            // To-Be: <<<path:"#back_to_freedom"> | <path:"Adriana Chechik"> | <path:"Megan Rain"> | <path:"Ava Addams">> | <path:<Lana Rhoades>>> <path:K:\ | path:N:\ | path:P:\> <file:<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts>> <size:>=10mb>
            Assert.AreEqual(@"<<<path:""#back_to_freedom""> | <path:""Adriana Chechik""> | <path:""Megan Rain""> | <path:""Ava Addams"">> | <path:<Lana Rhoades>>> <path:K:\ | path:N:\ | path:P:\> <file:<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts>> <size:>=10mb>", result);
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
            
            // To-Be: <path:<Adriana *>> <path:K:\ | path:N:\ | path:P:\> <file:<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts>> <size:>=10mb>
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
            
            // To-Be: <path:<Lana Rhoades>> <path:K:\ | path:N:\ | path:P:\> <file:<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts>> <size:>=10mb>
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
            
            // To-Be: <<path:<Lana Rhoades>> | <path:<India Summer>>> <path:K:\ | path:N:\ | path:P:\> <file:<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts>> <size:>=10mb>
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
            
            // To-Be: <path:!<"LIFE SELECTOR">> <path:K:\ | path:N:\ | path:P:\> <file:<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts>> <size:>=10mb>
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
            
            // To-Be: <path:!<LIFE> | path: <SELECTOR>> <path:K:\ | path:N:\ | path:P:\> <file:<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts>> <size:>=10mb>
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
    }
}
