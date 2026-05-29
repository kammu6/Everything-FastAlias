using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace EverythingFastAlias.Native
{
    public class TrayIconHelper : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Window _ownerWindow;

        public TrayIconHelper(Window ownerWindow)
        {
            _ownerWindow = ownerWindow;

            // 1. NotifyIcon 개체 생성
            _notifyIcon = new NotifyIcon();

            try
            {
                // 실행 프로그램 아이콘 추출 시도, 실패 시 기본 애플리케이션 아이콘 사용
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
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }

            _notifyIcon.Text = "Everything FastAlias";
            _notifyIcon.Visible = true;

            // 2. 더블클릭 이벤트 등록
            _notifyIcon.DoubleClick += (s, e) => RestoreOwnerWindow();

            // 3. 컨텍스트 메뉴 설정
            var contextMenu = new ContextMenuStrip();
            
            var openItem = new ToolStripMenuItem("열기 (&O)");
            openItem.Click += (s, e) => RestoreOwnerWindow();
            contextMenu.Items.Add(openItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("종료 (&X)");
            exitItem.Click += (s, e) =>
            {
                System.Windows.Application.Current.Shutdown();
            };
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        public void ShowBalloonTip(int timeout, string tipTitle, string tipText, ToolTipIcon tipIcon)
        {
            _notifyIcon.ShowBalloonTip(timeout, tipTitle, tipText, tipIcon);
        }

        private void RestoreOwnerWindow()
        {
            _ownerWindow.Dispatcher.Invoke(() =>
            {
                _ownerWindow.Show();
                _ownerWindow.WindowState = WindowState.Normal;
                _ownerWindow.Activate();
            });
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
