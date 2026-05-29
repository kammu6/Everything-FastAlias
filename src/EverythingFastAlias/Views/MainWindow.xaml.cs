using System;
using System.Windows;
using EverythingFastAlias.Native;
using EverythingFastAlias.ViewModels;
using EverythingFastAlias.Views.Modals;

namespace EverythingFastAlias.Views
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel? VM => DataContext as MainWindowViewModel;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (VM != null)
            {
                // 1. 모달창 오픈 요청 이벤트 구독
                VM.RequestOpenAliasManager += OpenAliasManager;
                VM.RequestOpenHelp += OpenHelp;

                // 2. 엔진 미감지 경고 이벤트 구독
                VM.SearchVM.EngineNotRunningDetected += HandleEngineNotRunning;
            }
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
            // 스위치가 토글될 때 실시간 재조회 트리거
            VM?.SearchVM.ExecuteSearch();
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
