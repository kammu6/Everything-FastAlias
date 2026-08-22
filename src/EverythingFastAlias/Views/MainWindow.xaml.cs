using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EverythingFastAlias.Models;
using EverythingFastAlias.Native;
using EverythingFastAlias.Services;
using EverythingFastAlias.ViewModels;
using EverythingFastAlias.Views.Modals;

namespace EverythingFastAlias.Views
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel? VM => DataContext as MainWindowViewModel;
        private bool _isClosingForReal = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ShortcutService.Instance.TryGetAction(e, ShortcutScope.Global, out var action))
            {
                e.Handled = true;
                ExecuteGlobalAction(action);
            }
        }

        private void ExecuteGlobalAction(ShortcutAction action)
        {
            if (VM == null) return;

            switch (action)
            {
                case ShortcutAction.ShowHelp:
                    OpenHelp();
                    break;
                case ShortcutAction.Refresh:
                    if (VM.SearchVM.RefreshCommand.CanExecute(null))
                        VM.SearchVM.RefreshCommand.Execute(null);
                    break;
                case ShortcutAction.FocusSearch:
                    SearchKeywordTextBox.Focus();
                    SearchKeywordTextBox.SelectAll();
                    break;
                case ShortcutAction.NewWindow:
                    OpenNewWindow();
                    break;
                case ShortcutAction.ExportResults:
                    if (VM.ExportResultsCommand.CanExecute(null))
                        VM.ExportResultsCommand.Execute(null);
                    break;
                case ShortcutAction.ToggleSidebar:
                    SidebarToggleSwitch.IsOn = !SidebarToggleSwitch.IsOn;
                    break;
                case ShortcutAction.OpenAliasManager:
                    OpenAliasManager();
                    break;
                case ShortcutAction.OpenExtensionManager:
                    OpenExtensionManager();
                    break;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (VM != null)
            {
                // 1. 모달창 오픈 요청 이벤트 구독
                VM.RequestOpenAliasManager += OpenAliasManager;
                VM.RequestOpenExtensionManager += OpenExtensionManager;
                VM.RequestOpenHelp += OpenHelp;
                VM.RequestNewWindow += OpenNewWindow;

                // 2. 엔진 미감지 경고 이벤트 구독
                VM.SearchVM.EngineNotRunningDetected += HandleEngineNotRunning;

                // 3. 옵션패널 표시 여부 복원
                var showSidebar = EverythingFastAlias.Services.DatabaseService.Instance.GetSetting("ShowOptionPanel", "true") == "true";
                SidebarToggleSwitch.IsOn = showSidebar;

                // 4. 앱 시작 시 창 표시 상태 제어 (자동 기동 vs 수동 기동)
                ApplyInitialWindowState();
            }
        }

        private static bool IsAutoStartLaunch()
        {
            string cmdLine = Environment.CommandLine;
            if (!string.IsNullOrEmpty(cmdLine) && cmdLine.IndexOf("autostart", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].IndexOf("autostart", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private void ApplyInitialWindowState()
        {
            bool isAutoStart = IsAutoStartLaunch();
            if (isAutoStart)
            {
                if (VM != null && VM.IsMinimizeToTrayEnabled)
                {
                    if (TrayIconManager.TryCreateTrayIcon())
                    {
                        this.Hide();
                        return;
                    }
                    else
                    {
                        this.WindowState = WindowState.Minimized;
                        return;
                    }
                }
                else
                {
                    this.WindowState = WindowState.Minimized;
                    return;
                }
            }

            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void OpenAliasManager()
        {
            var managerWindow = new AliasManagerWindow
            {
                Owner = this
            };
            managerWindow.ShowDialog();
            
            // 매핑 관리 창을 닫은 뒤, 변경 사항이 검색 엔진 캐시에 즉시 반영되도록 실시간 재질의 트리거
            VM?.SearchVM.ExecuteSearch();
        }

        private void OpenExtensionManager()
        {
            var extensionWindow = new ExtensionManagerWindow
            {
                Owner = this
            };
            extensionWindow.ShowDialog();

            // 확장자 설정 창을 닫은 뒤, 현재 선택된 프리셋의 확장자 필터와 검색 상태를 즉시 동기화
            VM?.SearchVM.RefreshExtensionFilterFromPresets();
        }

        private void OpenHelp()
        {
            var helpWindow = new HelpWindow
            {
                Owner = this
            };
            helpWindow.ShowDialog();
        }

        private void HandleEngineNotRunning()
        {
            var result = MessageBox.Show(
                "로컬 PC에 Everything 파일 검색 엔진(서비스 또는 백그라운드 프로세스)이 실행되고 있지 않습니다.\n" +
                "애플리케이션 구동을 위해 백그라운드에서 Everything 엔진을 강제 시작하시겠습니까?",
                "Everything 엔진 감지 실패",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.Yes)
            {
                if (VM != null)
                {
                    VM.SearchVM.StatusMessage = "Everything 엔진 기동 시도 중...";
                }
                
                bool started = EverythingBridge.StartEverythingEngine();
                if (started)
                {
                    MessageBox.Show("Everything 검색 엔진이 성공적으로 활성화되었습니다.", "엔진 기동 성공", MessageBoxButton.OK, MessageBoxImage.Information);
                    VM?.SearchVM.CheckEngineStatus();
                    VM?.SearchVM.ExecuteSearch();
                }
                else
                {
                    MessageBox.Show(
                        "Everything.exe 실행 파일을 레지스트리 및 기본 설치 경로(C:\\Program Files\\Everything)에서 찾지 못했거나 구동에 실패했습니다.\n" +
                        "Everything 프로그램이 설치되어 있는지 확인하고 수동으로 실행해주시기 바랍니다.", 
                        "기동 실패", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error
                    );
                    if (VM != null)
                    {
                        VM.SearchVM.StatusMessage = "Everything 서비스 비활성화됨 (수동 기동 필요)";
                    }
                }
            }
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null)
            {
                VM.SearchVM.SearchQuery = string.Empty;
            }
        }

        private void FastAliasSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ModernWpf.Controls.ToggleSwitch ts && VM != null)
            {
                VM.SearchVM.Options.UseFastAlias = ts.IsOn;
                EverythingFastAlias.Services.DatabaseService.Instance.SaveSetting("UseFastAlias", ts.IsOn ? "true" : "false");
                VM.SearchVM.ExecuteSearch();
            }
        }

        private void KeywordPriorityCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && VM != null)
            {
                bool isChecked = cb.IsChecked == true;
                VM.SearchVM.Options.PrioritizeKeywordMatch = isChecked;
                VM.SearchVM.PrioritizeKeywordMatch = isChecked;
                EverythingFastAlias.Services.DatabaseService.Instance.SaveSetting("PrioritizeKeywordMatch", isChecked ? "true" : "false");
                VM.SearchVM.ExecuteSearch();
            }
        }

        private void SidebarToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (SidebarToggleSwitch == null || SidebarColumn == null || SidebarSplitter == null || SidebarView == null)
                return;

            bool isOn = SidebarToggleSwitch.IsOn;
            if (isOn)
            {
                SidebarColumn.Width = new GridLength(320);
                SidebarColumn.MinWidth = 320;
                SidebarSplitter.Visibility = Visibility.Visible;
                SidebarView.Visibility = Visibility.Visible;
            }
            else
            {
                SidebarColumn.Width = new GridLength(0);
                SidebarColumn.MinWidth = 0;
                SidebarSplitter.Visibility = Visibility.Collapsed;
                SidebarView.Visibility = Visibility.Collapsed;
            }

            // 설정 저장
            EverythingFastAlias.Services.DatabaseService.Instance.SaveSetting("ShowOptionPanel", isOn ? "true" : "false");
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            _isClosingForReal = true;
            TrayIconManager.RemoveTrayIcon();
            Application.Current.Shutdown();
        }

        private void OpenNewWindow()
        {
            var newWindow = new MainWindow();
            newWindow.Show();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isClosingForReal)
            {
                TrayIconManager.RemoveTrayIcon();
                return;
            }

            // 트레이 최소화 옵션이 활성화되어 있고 사용자가 창을 닫은 경우
            if (VM != null && VM.IsMinimizeToTrayEnabled)
            {
                // 이미 트레이 아이콘이 등록되어 있는지(소유권 선점) 시도
                bool createdNew = TrayIconManager.TryCreateTrayIcon();
                if (createdNew)
                {
                    // 트레이 아이콘 선점 성공: 이 창을 트레이로 숨김
                    e.Cancel = true;
                    this.Hide();
                    TrayIconManager.ShowBalloonTip(2000, "Everything FastAlias", "프로그램이 백그라운드 트레이로 최소화되었습니다.", System.Windows.Forms.ToolTipIcon.Info);
                }
                else
                {
                    // 이미 시스템 트레이 아이콘이 존재하므로 트레이 OFF로 간주하고 창을 정상적으로 닫음 (종료)
                }
            }
        }

        private void QueryText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && !string.IsNullOrEmpty(tb.Text))
            {
                string text = tb.Text;
                string suffixMarker = " | 매핑 규칙:";

                string queryToCopy = text;
                int colonIndex = text.IndexOf("]: ");
                if (colonIndex >= 0)
                {
                    queryToCopy = text.Substring(colonIndex + 3);
                    int suffixIndex = queryToCopy.LastIndexOf(suffixMarker);
                    if (suffixIndex >= 0)
                    {
                        queryToCopy = queryToCopy.Substring(0, suffixIndex);
                    }
                }

                try
                {
                    Clipboard.SetText(queryToCopy.Trim());
                }
                catch (Exception)
                {
                    // 클립보드 예외 무시
                }
            }
        }
    }
}
