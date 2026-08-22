using System.Windows.Input;
using EverythingFastAlias.Models;
using EverythingFastAlias.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EverythingFastAlias.Tests
{
    [TestClass]
    public class ShortcutServiceTests
    {
        [TestMethod]
        public void DefaultShortcuts_ShouldContainCoreActions()
        {
            var service = ShortcutService.Instance;

            Assert.IsTrue(service.Shortcuts.Count >= 16);

            var helpItem = service.GetItem(ShortcutAction.ShowHelp);
            Assert.IsNotNull(helpItem);
            Assert.AreEqual(Key.F1, helpItem.DefaultKey);
            Assert.AreEqual(ModifierKeys.None, helpItem.DefaultModifiers);
            Assert.AreEqual("F1", helpItem.DefaultDisplayGesture);
            Assert.AreEqual(ShortcutScope.Global, helpItem.Scope);

            var refreshItem = service.GetItem(ShortcutAction.Refresh);
            Assert.IsNotNull(refreshItem);
            Assert.AreEqual(Key.F5, refreshItem.DefaultKey);

            var exploreItem = service.GetItem(ShortcutAction.ExplorePath);
            Assert.IsNotNull(exploreItem);
            Assert.AreEqual(Key.Enter, exploreItem.DefaultKey);
            Assert.AreEqual(ModifierKeys.Shift, exploreItem.DefaultModifiers);
            Assert.AreEqual("Shift + Enter", exploreItem.DefaultDisplayGesture);
            Assert.AreEqual(ShortcutScope.ResultGrid, exploreItem.Scope);

            var permDeleteItem = service.GetItem(ShortcutAction.PermanentDelete);
            Assert.IsNotNull(permDeleteItem);
            Assert.AreEqual(Key.Delete, permDeleteItem.DefaultKey);
            Assert.AreEqual(ModifierKeys.Shift, permDeleteItem.DefaultModifiers);
            Assert.AreEqual("Shift + Delete", permDeleteItem.DefaultDisplayGesture);

            var copyNamesItem = service.GetItem(ShortcutAction.CopyFileNames);
            Assert.IsNotNull(copyNamesItem);
            Assert.AreEqual(Key.C, copyNamesItem.DefaultKey);
            Assert.AreEqual(ModifierKeys.Control | ModifierKeys.Shift, copyNamesItem.DefaultModifiers);
            Assert.AreEqual("Ctrl + Shift + C", copyNamesItem.DefaultDisplayGesture);

            var copyPathsItem = service.GetItem(ShortcutAction.CopyFullPaths);
            Assert.IsNotNull(copyPathsItem);
            Assert.AreEqual(Key.C, copyPathsItem.DefaultKey);
            Assert.AreEqual(ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt, copyPathsItem.DefaultModifiers);
            Assert.AreEqual("Ctrl + Shift + Alt + C", copyPathsItem.DefaultDisplayGesture);
        }

        [TestMethod]
        public void FormatGesture_ShouldFormatCorrectly()
        {
            Assert.AreEqual("F1", ShortcutItem.FormatGesture(Key.F1, ModifierKeys.None));
            Assert.AreEqual("Ctrl + F", ShortcutItem.FormatGesture(Key.F, ModifierKeys.Control));
            Assert.AreEqual("Ctrl + Shift + C", ShortcutItem.FormatGesture(Key.C, ModifierKeys.Control | ModifierKeys.Shift));
            Assert.AreEqual("Ctrl + Shift + Alt + C", ShortcutItem.FormatGesture(Key.C, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt));
            Assert.AreEqual("Ctrl + Shift + E", ShortcutItem.FormatGesture(Key.E, ModifierKeys.Control | ModifierKeys.Shift));
            Assert.AreEqual("Alt + Enter", ShortcutItem.FormatGesture(Key.Enter, ModifierKeys.Alt));
            Assert.AreEqual("없음", ShortcutItem.FormatGesture(Key.None, ModifierKeys.None));
        }

        [TestMethod]
        public void Customization_TrackingAndReset_ShouldWork()
        {
            var item = new ShortcutItem
            {
                Action = ShortcutAction.ShowHelp,
                Scope = ShortcutScope.Global,
                DefaultKey = Key.F1,
                DefaultModifiers = ModifierKeys.None,
                Key = Key.F1,
                Modifiers = ModifierKeys.None
            };

            Assert.IsFalse(item.IsCustomized);

            item.Key = Key.H;
            item.Modifiers = ModifierKeys.Control;

            Assert.IsTrue(item.IsCustomized);
            Assert.AreEqual("Ctrl + H", item.DisplayGesture);

            item.ResetToDefault();
            Assert.IsFalse(item.IsCustomized);
            Assert.AreEqual(Key.F1, item.Key);
            Assert.AreEqual(ModifierKeys.None, item.Modifiers);
            Assert.AreEqual("F1", item.DisplayGesture);
        }
    }
}
