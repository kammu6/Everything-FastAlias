using EverythingFastAlias.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EverythingFastAlias.Tests
{
    [TestClass]
    public class MediaFilterTests
    {
        [TestMethod]
        public void MediaAll_And_MediaFolder_ShouldBeMutuallyExclusive()
        {
            var vm = new SearchViewModel();

            // 기본 상태: 전체 ON, 폴더 OFF
            Assert.IsTrue(vm.MediaAll);
            Assert.IsFalse(vm.MediaFolder);

            // Case 1: 전체 ON -> 폴더 클릭 -> 폴더 ON, 전체 OFF
            vm.MediaFolder = true;
            Assert.IsTrue(vm.MediaFolder);
            Assert.IsFalse(vm.MediaAll);
            Assert.IsFalse(vm.MediaVideo);

            // Case 2: 폴더 ON -> 전체 클릭 -> 전체 ON, 폴더 OFF
            vm.MediaAll = true;
            Assert.IsTrue(vm.MediaAll);
            Assert.IsFalse(vm.MediaFolder);
            Assert.IsFalse(vm.MediaVideo);

            // Case 3: 전체 ON -> 영상 클릭 -> 영상 ON, 전체 OFF, 폴더 OFF
            vm.MediaVideo = true;
            Assert.IsTrue(vm.MediaVideo);
            Assert.IsFalse(vm.MediaAll);
            Assert.IsFalse(vm.MediaFolder);

            // Case 4: 영상 ON 상태에서 음악 다중 선택 -> 영상 ON, 음악 ON, 전체/폴더 OFF
            vm.MediaAudio = true;
            Assert.IsTrue(vm.MediaVideo);
            Assert.IsTrue(vm.MediaAudio);
            Assert.IsFalse(vm.MediaAll);
            Assert.IsFalse(vm.MediaFolder);

            // Case 5: 영상/음악 다중 선택 상태에서 폴더 클릭 -> 폴더 ON, 영상/음악/전체 OFF
            vm.MediaFolder = true;
            Assert.IsTrue(vm.MediaFolder);
            Assert.IsFalse(vm.MediaVideo);
            Assert.IsFalse(vm.MediaAudio);
            Assert.IsFalse(vm.MediaAll);

            // Case 6: 폴더 ON 상태에서 폴더 해제 -> 전체 ON 자동 복귀
            vm.MediaFolder = false;
            Assert.IsTrue(vm.MediaAll);
            Assert.IsFalse(vm.MediaFolder);
        }
    }
}
