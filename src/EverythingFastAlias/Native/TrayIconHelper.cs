using System;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Forms;

namespace EverythingFastAlias.Native
{
    public static class TrayIconManager
    {
        private static NotifyIcon? _notifyIcon;
        private static Mutex? _trayMutex;
        private static readonly object _lock = new object();
        private const string TrayMutexName = "Global\\EverythingFastAlias_TrayMutex";

        /// <summary>
        /// 내 프로세스 또는 다른 프로세스가 이미 시스템 트레이 아이콘을 가지고 있는지 확인합니다.
        /// </summary>
        public static bool IsSystemTrayAlreadyActive()
        {
            lock (_lock)
            {
                if (_notifyIcon != null) return true;

                try
                {
                    using var existingMutex = Mutex.OpenExisting(TrayMutexName);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 트레이 아이콘 소유권을 시도하여 성공하면 트레이 아이콘을 생성하고 true를 반환, 이미 존재하면 false를 반환합니다.
        /// </summary>
        public static bool TryCreateTrayIcon()
        {
            lock (_lock)
            {
                if (_notifyIcon != null) return true;

                try
                {
                    bool createdNew;
                    _trayMutex = new Mutex(true, TrayMutexName, out createdNew);

                    if (!createdNew)
                    {
                        _trayMutex.Dispose();
                        _trayMutex = null;
                        return false;
                    }

                    _notifyIcon = new NotifyIcon();

                    string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        exePath = System.IO.Path.ChangeExtension(exePath, ".exe");
                    }

                    if (System.IO.File.Exists(exePath))
                    {
                        _notifyIcon.Icon = Icon.ExtractAssociatedIcon(exePath);
                    }
                    else
                    {
                        _notifyIcon.Icon = SystemIcons.Application;
                    }

                    _notifyIcon.Text = "Everything FastAlias";
                    _notifyIcon.Visible = true;

                    // 더블클릭 이벤트
                    _notifyIcon.DoubleClick += (s, e) => RestoreAllWindows();

                    // 컨텍스트 메뉴 설정
                    var contextMenu = new ContextMenuStrip();

                    var openItem = new ToolStripMenuItem("열기 (&O)");
                    openItem.Click += (s, e) => RestoreAllWindows();
                    contextMenu.Items.Add(openItem);

                    contextMenu.Items.Add(new ToolStripSeparator());

                    var exitItem = new ToolStripMenuItem("종료 (&X)");
                    exitItem.Click += (s, e) =>
                    {
                        RemoveTrayIcon();
                        System.Windows.Application.Current.Shutdown();
                    };
                    contextMenu.Items.Add(exitItem);

                    _notifyIcon.ContextMenuStrip = contextMenu;

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void ShowBalloonTip(int timeout, string tipTitle, string tipText, ToolTipIcon tipIcon)
        {
            lock (_lock)
            {
                _notifyIcon?.ShowBalloonTip(timeout, tipTitle, tipText, tipIcon);
            }
        }

        public static void RestoreAllWindows()
        {
            if (System.Windows.Application.Current == null) return;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window win in System.Windows.Application.Current.Windows)
                {
                    if (win is Views.MainWindow)
                    {
                        win.Show();
                        win.WindowState = WindowState.Normal;
                        win.Activate();
                    }
                }

                RemoveTrayIcon();
            });
        }

        public static void RemoveTrayIcon()
        {
            lock (_lock)
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }

                if (_trayMutex != null)
                {
                    try
                    {
                        _trayMutex.ReleaseMutex();
                    }
                    catch { }
                    _trayMutex.Dispose();
                    _trayMutex = null;
                }
            }
        }
    }
}
