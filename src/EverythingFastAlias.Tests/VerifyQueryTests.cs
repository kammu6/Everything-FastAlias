using EverythingFastAlias.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EverythingFastAlias.Tests
{
    [TestClass]
    public class VerifyQueryTests
    {
        [TestMethod]
        public void ExecuteVerifyQuery_ShouldSetStatusMessageWithoutExecutingEverythingEngine()
        {
            var vm = new SearchViewModel();
            vm.SearchQuery = "test";
            vm.ExecuteVerifyQuery();

            Assert.IsNotNull(vm.StatusMessage);
            Assert.IsTrue(vm.StatusMessage.StartsWith("[Everything 쿼리 (검증)]:"));
            Assert.IsTrue(vm.StatusMessage.Contains("test"));
        }
    }
}
