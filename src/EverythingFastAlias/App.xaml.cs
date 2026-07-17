using System;
using System.IO;
using System.Windows;
using Application = System.Windows.Application;

namespace EverythingFastAlias;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ClearPerfLog();
    }

    /// <summary>앱 시작 시 perf.log를 초기화하여 세션 단위로 로그를 관리합니다.</summary>
    private static void ClearPerfLog()
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EverythingFastAlias",
                "perf.log");

            if (File.Exists(logPath))
            {
                File.WriteAllText(logPath, $"[SESSION START] {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n");
            }
        }
        catch { /* 무시 */ }
    }
}
