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
                    string? val = key.GetValue(AppName) as string;
                    if (val != null)
                    {
                        // 기존 레지스트리 값에 /autostart 인자가 포함되어 있지 않다면 즉시 최신 형태로 등록 보정
                        if (!val.Contains("/autostart", StringComparison.OrdinalIgnoreCase))
                        {
                            Register();
                        }
                        return true;
                    }
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
