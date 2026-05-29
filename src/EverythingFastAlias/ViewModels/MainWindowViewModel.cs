using System;
using System.IO;
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
                }
            }
        }

        private bool _isMinimizeToTrayEnabled = true;
        public bool IsMinimizeToTrayEnabled
        {
            get => _isMinimizeToTrayEnabled;
            set => SetProperty(ref _isMinimizeToTrayEnabled, value);
        }

        public ICommand OpenAliasManagerCommand { get; }
        public ICommand OpenHelpCommand { get; }
        public ICommand ExportResultsCommand { get; }
        public ICommand NewWindowCommand { get; }

        public event Action? RequestOpenAliasManager;
        public event Action? RequestOpenHelp;
        public event Action? RequestNewWindow;

        public MainWindowViewModel()
        {
            SearchVM = new SearchViewModel();

            OpenAliasManagerCommand = new RelayCommand(() => RequestOpenAliasManager?.Invoke());
            OpenHelpCommand = new RelayCommand(() => RequestOpenHelp?.Invoke());
            NewWindowCommand = new RelayCommand(() => RequestNewWindow?.Invoke());
            ExportResultsCommand = new RelayCommand(ExportResults);

            _isAutoStartEnabled = AutoStartService.IsRegistered();
        }

        private void ExportResults()
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
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("[Everything FastAlias 검색 내역 내보내기]");
                    sb.AppendLine($"내보낸 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine($"검색어: {SearchVM.SearchQuery}");
                    sb.AppendLine($"FastAlias 활성화: {(SearchVM.Options.UseFastAlias ? "ON" : "OFF")}");
                    sb.AppendLine($"총 결과 수: {SearchVM.Results.Count}개");
                    sb.AppendLine("================================================================================================");
                    sb.AppendLine("이름\t|\t경로\t|\t수정한 날짜\t|\t크기\t|\t확장자");
                    sb.AppendLine("------------------------------------------------------------------------------------------------");

                    foreach (var item in SearchVM.Results)
                    {
                        sb.AppendLine($"{item.Name}\t|\t{item.FullPath}\t|\t{item.DisplayModifiedDate}\t|\t{item.DisplaySize}\t|\t{item.Extension}");
                    }

                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("검색 결과가 성공적으로 내보내졌습니다.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"내보내기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
