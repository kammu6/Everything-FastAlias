using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EverythingFastAlias.Models;
using EverythingFastAlias.Services;
using Microsoft.Win32;

namespace EverythingFastAlias.ViewModels
{
    public class AliasManagerViewModel : ObservableObject
    {
        private ObservableCollection<AliasMapping> _mappings = new();
        public ObservableCollection<AliasMapping> Mappings
        {
            get => _mappings;
            set => SetProperty(ref _mappings, value);
        }

        private AliasMapping? _selectedMapping;
        public AliasMapping? SelectedMapping
        {
            get => _selectedMapping;
            set => SetProperty(ref _selectedMapping, value);
        }

        private string _statusMessage = "정상";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private System.Windows.Media.Brush _statusForeground = System.Windows.Media.Brushes.DimGray;
        public System.Windows.Media.Brush StatusForeground
        {
            get => _statusForeground;
            set => SetProperty(ref _statusForeground, value);
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    PerformSearch();
                }
            }
        }

        private int _currentSearchMatchIndex = -1;
        public int CurrentSearchMatchIndex
        {
            get => _currentSearchMatchIndex;
            set
            {
                if (SetProperty(ref _currentSearchMatchIndex, value))
                {
                    UpdateSearchStatus();
                    NavigateToMatch();
                }
            }
        }

        private int _totalSearchMatches = 0;
        public int TotalSearchMatches
        {
            get => _totalSearchMatches;
            set => SetProperty(ref _totalSearchMatches, value);
        }

        private string _searchStatusText = string.Empty;
        public string SearchStatusText
        {
            get => _searchStatusText;
            set => SetProperty(ref _searchStatusText, value);
        }

        private readonly System.Collections.Generic.List<AliasMapping> _matchedItems = new();

        public event Action<AliasMapping>? RequestScrollIntoView;

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ImportExcelCommand { get; }
        public ICommand ExportTemplateCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ClearAllCommand { get; }
        public ICommand FindNextCommand { get; }
        public ICommand FindPrevCommand { get; }

        public AliasManagerViewModel()
        {
            LoadCommand = new RelayCommand(LoadMappings);
            AddCommand = new RelayCommand(AddMapping);
            DeleteCommand = new RelayCommand(DeleteMapping);
            SaveCommand = new RelayCommand(SaveSelectedMapping);
            ImportExcelCommand = new RelayCommand(ImportExcel);
            ExportTemplateCommand = new RelayCommand(ExportTemplate);
            ExportExcelCommand = new RelayCommand(ExportExcel);
            ClearAllCommand = new RelayCommand(ClearAllMappings);
            FindNextCommand = new RelayCommand(FindNext);
            FindPrevCommand = new RelayCommand(FindPrev);

            LoadMappings();
        }
 
        public void LoadMappings()
        {
            try
            {
                var dbList = DatabaseService.Instance.GetAllMappings();
                Mappings.Clear();
                foreach (var kvp in dbList)
                {
                    Mappings.Add(new AliasMapping(kvp.Key, kvp.Value));
                }
                StatusForeground = System.Windows.Media.Brushes.DimGray;
                StatusMessage = $"총 {Mappings.Count}개의 매핑 규칙을 로드했습니다.";
                PerformSearch();
            }
            catch (Exception ex)
            {
                StatusForeground = System.Windows.Media.Brushes.Red;
                StatusMessage = $"로드 실패: {ex.Message}";
            }
        }
 
        private void AddMapping()
        {
            var newMapping = new AliasMapping("새키워드", "동의어1;동의어2");
            Mappings.Add(newMapping);
            SelectedMapping = newMapping;
            StatusForeground = System.Windows.Media.Brushes.DimGray;
            StatusMessage = "새 행을 추가했습니다. 저장 버튼을 눌러 확정하세요.";
        }
 
        private void ClearAllMappings()
        {
            try
            {
                var result = MessageBox.Show(
                    "스마트 매핑 사전을 정말로 초기화하시겠습니까?\n이 작업은 모든 매핑 데이터를 영구적으로 삭제하며 되돌릴 수 없습니다.",
                    "전체 초기화 경고",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No
                );
 
                if (result == MessageBoxResult.Yes)
                {
                    DatabaseService.Instance.ClearAllMappings();
                    LoadMappings();
                    SelectedMapping = null;
                    StatusForeground = System.Windows.Media.Brushes.Red;
                    StatusMessage = $"[초기화 완료 - {DateTime.Now:HH:mm:ss}] 모든 매핑 규칙이 초기화되었습니다.";
                    MessageBox.Show("초기화가 성공적으로 완료되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusForeground = System.Windows.Media.Brushes.Red;
                StatusMessage = $"초기화 실패: {ex.Message}";
                MessageBox.Show($"초기화 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
 
        private void DeleteMapping()
        {
            if (SelectedMapping == null)
            {
                StatusForeground = System.Windows.Media.Brushes.Red;
                StatusMessage = "삭제할 행을 선택하세요.";
                return;
            }

            try
            {
                var result = MessageBox.Show(
                    $"'{SelectedMapping.Keyword}' 매핑을 삭제하시겠습니까?", 
                    "삭제 확인", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Yes)
                {
                    DatabaseService.Instance.DeleteMapping(SelectedMapping.Keyword);
                    Mappings.Remove(SelectedMapping);
                    SelectedMapping = null;
                    StatusForeground = System.Windows.Media.Brushes.Red;
                    StatusMessage = $"[삭제 완료 - {DateTime.Now:HH:mm:ss}] 매핑이 안전하게 삭제 및 동기화되었습니다.";
                }
            }
            catch (Exception ex)
            {
                StatusForeground = System.Windows.Media.Brushes.Red;
                StatusMessage = $"삭제 실패: {ex.Message}";
            }
        }

        private void SaveSelectedMapping()
        {
            try
            {
                foreach (var mapping in Mappings)
                {
                    if (string.IsNullOrWhiteSpace(mapping.Keyword))
                    {
                        MessageBox.Show("원본 단어(Keyword)는 비워둘 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                var syncData = Mappings.Select(m => (m.Keyword, m.Words));
                DatabaseService.Instance.SaveAllSync(syncData);
                PerformSearch();
                StatusForeground = System.Windows.Media.Brushes.ForestGreen;
                StatusMessage = $"[저장 완료 - {DateTime.Now:HH:mm:ss}] 모든 수정사항이 데이터베이스 및 캐시에 저장 및 동기화되었습니다.";
            }
            catch (Exception ex)
            {
                StatusForeground = System.Windows.Media.Brushes.Red;
                StatusMessage = $"저장 실패: {ex.Message}";
            }
        }

        private void SaveSelectedMapping_Dummy()
        {
            if (SelectedMapping == null) return;

            if (string.IsNullOrWhiteSpace(SelectedMapping.Keyword))
            {
                MessageBox.Show("원본 단어(Keyword)는 비워둘 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                DatabaseService.Instance.SaveMapping(SelectedMapping.Keyword, SelectedMapping.Words);
                LoadMappings(); // 리프레시 및 캐싱 강제 동기화
                StatusForeground = System.Windows.Media.Brushes.ForestGreen;
                StatusMessage = $"[저장 완료 - {DateTime.Now:HH:mm:ss}] '{SelectedMapping.Keyword}' 규칙이 데이터베이스 및 캐시에 저장 및 동기화되었습니다.";
            }
            catch (Exception ex)
            {
                StatusForeground = System.Windows.Media.Brushes.Red;
                StatusMessage = $"저장 실패: {ex.Message}";
            }
        }

        private void ImportExcel()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Excel 파일 (*.xlsx)|*.xlsx|CSV 파일 (*.csv)|*.csv",
                Title = "매핑 사전 가져오기"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    StatusMessage = "엑셀 가져오는 중...";
                    var list = ExcelService.ImportExcel(openFileDialog.FileName);
                    
                    if (list.Count == 0)
                    {
                        MessageBox.Show("가져올 유효한 데이터 행이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                        StatusMessage = "유효 데이터 없음";
                        return;
                    }

                    // SQLite Transaction 기반 벌크 로드
                    DatabaseService.Instance.SaveBulk(list);
                    LoadMappings();

                    MessageBox.Show(
                        $"총 {list.Count}개의 매핑이 성공적으로 대량 업로드 및 갱신되었습니다.", 
                        "성공", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information
                    );
                    StatusForeground = System.Windows.Media.Brushes.ForestGreen;
                    StatusMessage = $"[가져오기 완료 - {DateTime.Now:HH:mm:ss}] 총 {list.Count}개의 매핑이 벌크 갱신되었습니다.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"가져오기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusForeground = System.Windows.Media.Brushes.Red;
                    StatusMessage = $"엑셀 임포트 에러: {ex.Message}";
                }
            }
        }

        private void ExportTemplate()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV 파일 (*.csv)|*.csv",
                FileName = "FastAlias_Import_Template.csv",
                Title = "양식 다운로드"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    ExcelService.GenerateTemplate(saveFileDialog.FileName);
                    MessageBox.Show("템플릿 양식이 저장되었습니다.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                    StatusMessage = "템플릿 양식 내보내기 완료";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"템플릿 내보내기 실패: {ex.Message}";
                }
            }
        }

        private void ExportExcel()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV 파일 (*.csv)|*.csv",
                FileName = "FastAlias_Export.csv",
                Title = "매핑 사전 내보내기"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    StatusMessage = "내보내는 중...";
                    var data = Mappings.Select(m => (m.Keyword, m.Words)).ToList();
                    ExcelService.ExportToCsv(saveFileDialog.FileName, data);
                    MessageBox.Show("매핑 사전을 성공적으로 내보냈습니다.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                    StatusMessage = $"총 {data.Count}개 내보내기 완료";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"내보내기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusMessage = $"내보내기 실패: {ex.Message}";
                }
            }
        }

        private void PerformSearch()
        {
            _matchedItems.Clear();
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                TotalSearchMatches = 0;
                CurrentSearchMatchIndex = -1;
                UpdateSearchStatus();
                return;
            }

            var searchTerms = SearchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (searchTerms.Length == 0)
            {
                TotalSearchMatches = 0;
                CurrentSearchMatchIndex = -1;
                UpdateSearchStatus();
                return;
            }

            foreach (var mapping in Mappings)
            {
                bool isAllMatch = true;
                foreach (var term in searchTerms)
                {
                    bool isTermMatch = (mapping.Keyword != null && mapping.Keyword.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                                       (mapping.Words != null && mapping.Words.Contains(term, StringComparison.OrdinalIgnoreCase));
                    if (!isTermMatch)
                    {
                        isAllMatch = false;
                        break;
                    }
                }

                if (isAllMatch)
                {
                    _matchedItems.Add(mapping);
                }
            }

            TotalSearchMatches = _matchedItems.Count;
            UpdateSearchStatus();

            if (TotalSearchMatches > 0)
            {
                if (_currentSearchMatchIndex != 0)
                {
                    _currentSearchMatchIndex = 0;
                    OnPropertyChanged(nameof(CurrentSearchMatchIndex));
                }
                NavigateToMatch();
            }
            else
            {
                if (_currentSearchMatchIndex != -1)
                {
                    _currentSearchMatchIndex = -1;
                    OnPropertyChanged(nameof(CurrentSearchMatchIndex));
                }
            }
        }

        private void PerformSearch_Dummy()
        {
            _matchedItems.Clear();
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                TotalSearchMatches = 0;
                CurrentSearchMatchIndex = -1;
                UpdateSearchStatus();
                return;
            }

            foreach (var mapping in Mappings)
            {
                bool isMatch = (mapping.Keyword != null && mapping.Keyword.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                               (mapping.Words != null && mapping.Words.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
                if (isMatch)
                {
                    _matchedItems.Add(mapping);
                }
            }

            TotalSearchMatches = _matchedItems.Count;
            if (TotalSearchMatches > 0)
            {
                CurrentSearchMatchIndex = 0;
            }
            else
            {
                CurrentSearchMatchIndex = -1;
            }
        }

        private void FindNext()
        {
            if (TotalSearchMatches <= 0) return;
            CurrentSearchMatchIndex = (CurrentSearchMatchIndex + 1) % TotalSearchMatches;
        }

        private void FindPrev()
        {
            if (TotalSearchMatches <= 0) return;
            CurrentSearchMatchIndex = (CurrentSearchMatchIndex - 1 + TotalSearchMatches) % TotalSearchMatches;
        }

        private void UpdateSearchStatus()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                SearchStatusText = string.Empty;
            }
            else if (TotalSearchMatches == 0)
            {
                SearchStatusText = "결과 없음";
            }
            else
            {
                SearchStatusText = $"{CurrentSearchMatchIndex + 1} / {TotalSearchMatches}";
            }
        }

        private void NavigateToMatch()
        {
            if (CurrentSearchMatchIndex >= 0 && CurrentSearchMatchIndex < _matchedItems.Count)
            {
                var target = _matchedItems[CurrentSearchMatchIndex];
                SelectedMapping = target;
                RequestScrollIntoView?.Invoke(target);
            }
        }
    }
}
