using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EverythingFastAlias.Services;
using Microsoft.Win32;

namespace EverythingFastAlias.ViewModels
{
    public class MainWindowViewModel : ObservableObject
    {
        public SearchViewModel SearchVM { get; }

        private bool _isAutoStartEnabled;
        public bool IsAutoStartEnabled
        {
            get => _isAutoStartEnabled;
            set
            {
                if (SetProperty(ref _isAutoStartEnabled, value))
                {
                    if (value)
                        AutoStartService.Register();
                    else
                        AutoStartService.Unregister();

                    DatabaseService.Instance.SaveSetting("IsAutoStartEnabled", value.ToString());
                }
            }
        }

        private bool _isMinimizeToTrayEnabled = true;
        public bool IsMinimizeToTrayEnabled
        {
            get => _isMinimizeToTrayEnabled;
            set
            {
                if (SetProperty(ref _isMinimizeToTrayEnabled, value))
                {
                    DatabaseService.Instance.SaveSetting("IsMinimizeToTrayEnabled", value.ToString());
                }
            }
        }

        public ICommand OpenAliasManagerCommand { get; }
        public ICommand OpenExtensionManagerCommand { get; }
        public ICommand OpenHelpCommand { get; }
        public ICommand ExportResultsCommand { get; }
        public ICommand NewWindowCommand { get; }

        public event Action? RequestOpenAliasManager;
        public event Action? RequestOpenExtensionManager;
        public event Action? RequestOpenHelp;
        public event Action? RequestNewWindow;

        public MainWindowViewModel()
        {
            SearchVM = new SearchViewModel();

            OpenAliasManagerCommand = new RelayCommand(() => RequestOpenAliasManager?.Invoke());
            OpenExtensionManagerCommand = new RelayCommand(() => RequestOpenExtensionManager?.Invoke());
            OpenHelpCommand = new RelayCommand(() => RequestOpenHelp?.Invoke());
            NewWindowCommand = new RelayCommand(() => RequestNewWindow?.Invoke());
            ExportResultsCommand = new RelayCommand(ExportResults);

            // DB에서 설정 불러오기
            string traySetting = DatabaseService.Instance.GetSetting("IsMinimizeToTrayEnabled", "true");
            _isMinimizeToTrayEnabled = bool.TryParse(traySetting, out bool trayVal) ? trayVal : true;

            _isAutoStartEnabled = AutoStartService.IsRegistered();
            if (_isAutoStartEnabled)
            {
                AutoStartService.Register();
            }
            DatabaseService.Instance.SaveSetting("IsAutoStartEnabled", _isAutoStartEnabled.ToString());
        }

        private async void ExportResults()
        {
            if (SearchVM.Results.Count == 0)
            {
                MessageBox.Show("내보낼 검색 결과가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "텍스트 파일 (*.txt)|*.txt",
                FileName = $"Everything_Search_Export_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                Title = "검색 결과 내보내기"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var filePath = saveFileDialog.FileName;
                var snapshot = SearchVM.Results.ToList();
                var searchQuery = SearchVM.SearchQuery;
                var useFastAlias = SearchVM.Options.UseFastAlias;
                var initialStatus = SearchVM.StatusMessage;

                try
                {
                    SearchVM.StatusMessage = "내보내는 중...";

                    await Task.Run(() =>
                    {
                        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
                        writer.WriteLine("[Everything FastAlias 검색 내역 내보내기]");
                        writer.WriteLine($"내보낸 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        writer.WriteLine($"검색어: {searchQuery}");
                        writer.WriteLine($"FastAlias 활성화: {(useFastAlias ? "ON" : "OFF")}");
                        writer.WriteLine($"총 결과 수: {snapshot.Count}개");
                        writer.WriteLine("================================================================================================");
                        writer.WriteLine("이름\t|\t경로\t|\t수정한 날짜\t|\t크기\t|\t확장자");
                        writer.WriteLine("------------------------------------------------------------------------------------------------");

                        foreach (var item in snapshot)
                        {
                            writer.WriteLine($"{item.Name}\t|\t{item.FullPath}\t|\t{item.DisplayModifiedDate}\t|\t{item.DisplaySize}\t|\t{item.Extension}");
                        }
                    });

                    MessageBox.Show("검색 결과가 성공적으로 내보내졌습니다.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"내보내기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    SearchVM.StatusMessage = initialStatus;
                }
            }
        }
    }
}
