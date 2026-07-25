using System;
using Microsoft.Win32;

namespace EverythingFastAlias.Services
{
    public static class AutoStartService
    {
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "EverythingFastAlias";

        public static void Register()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, true);
                if (key != null)
                {
                    string appPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    if (!string.IsNullOrEmpty(appPath))
                    {
                        key.SetValue(AppName, $"\"{appPath}\" /autostart");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"시작프로그램 등록 실패: {ex.Message}");
            }
        }

        public static void Unregister()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, true);
                if (key != null)
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"시작프로그램 해제 실패: {ex.Message}");
            }
        }

        public static bool IsRegistered()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, false);
                if (key != null)
                {
                    return key.GetValue(AppName) != null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"시작프로그램 상태 조회 실패: {ex.Message}");
            }
            return false;
        }
    }
}
