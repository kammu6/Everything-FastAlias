using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EverythingFastAlias.Models;
using EverythingFastAlias.Native;
using EverythingFastAlias.Services;

namespace EverythingFastAlias.ViewModels
{
    public class SearchViewModel : ObservableObject
    {
        private string _searchQuery = string.Empty;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    ExecuteSearch();
                }
            }
        }

        private string _excludedWords = string.Empty;
        public string ExcludedWords
        {
            get => _excludedWords;
            set
            {
                if (SetProperty(ref _excludedWords, value))
                {
                    Options.ExcludedWords = value;
                    ExecuteSearch();
                }
            }
        }

        private string _folderPaths = string.Empty;
        public string FolderPaths
        {
            get => _folderPaths;
            set
            {
                if (SetProperty(ref _folderPaths, value))
                {
                    Options.FolderPaths = value;
                    ExecuteSearch();
                }
            }
        }

        private string _customExtensions = string.Empty;
        public string CustomExtensions
        {
            get => _customExtensions;
            set
            {
                if (SetProperty(ref _customExtensions, value))
                {
                    Options.CustomExtensions = value;
                    ExecuteSearch();
                }
            }
        }

        private long? _minSize;
        public long? MinSize
        {
            get => _minSize;
            set
            {
                if (SetProperty(ref _minSize, value))
                {
                    Options.MinSize = value;
                    ExecuteSearch();
                    OnPropertyChanged(nameof(MinSizeText));
                }
            }
        }

        private long? _maxSize;
        public long? MaxSize
        {
            get => _maxSize;
            set
            {
                if (SetProperty(ref _maxSize, value))
                {
                    Options.MaxSize = value;
                    ExecuteSearch();
                    OnPropertyChanged(nameof(MaxSizeText));
                }
            }
        }

        private string _statusMessage = "검색 엔진 초기화 대기 중...";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _resultCountMessage = "검색 결과: 0개 항목";
        public string ResultCountMessage
        {
            get => _resultCountMessage;
            set => SetProperty(ref _resultCountMessage, value);
        }

        public SearchOptions Options { get; } = new();
        public ObservableCollection<SearchResultItem> Results { get; } = new();

        public ICommand SearchCommand { get; }
        public ICommand ResetCommand { get; }

        public event Action? EngineNotRunningDetected;

        #region UI 전용 양방향 바인딩 래퍼 속성들

        // 1. 탐색 범위 (Scope) 래퍼
        public bool ScopeAll
        {
            get => Options.Scope == SearchScope.All;
            set
            {
                if (value)
                {
                    Options.Scope = SearchScope.All;
                    ExecuteSearch();
                    NotifyScopeProperties();
                }
            }
        }

        public bool ScopeFile
        {
            get => Options.Scope == SearchScope.FileOnly;
            set
            {
                if (value)
                {
                    Options.Scope = SearchScope.FileOnly;
                    ExecuteSearch();
                    NotifyScopeProperties();
                }
            }
        }

        public bool ScopeFolder
        {
            get => Options.Scope == SearchScope.FolderOnly;
            set
            {
                if (value)
                {
                    Options.Scope = SearchScope.FolderOnly;
                    ExecuteSearch();
                    NotifyScopeProperties();
                }
            }
        }

        private void NotifyScopeProperties()
        {
            OnPropertyChanged(nameof(ScopeAll));
            OnPropertyChanged(nameof(ScopeFile));
            OnPropertyChanged(nameof(ScopeFolder));
        }

        // 2. 파일 크기 텍스트 래퍼
        public string MinSizeText
        {
            get => MinSize?.ToString() ?? string.Empty;
            set
            {
                if (long.TryParse(value, out long val))
                    MinSize = val;
                else
                    MinSize = null;
            }
        }

        public string MaxSizeText
        {
            get => MaxSize?.ToString() ?? string.Empty;
            set
            {
                if (long.TryParse(value, out long val))
                    MaxSize = val;
                else
                    MaxSize = null;
            }
        }

        // 3. 파일 크기 단위 (SizeUnit) 래퍼
        public bool SizeKB
        {
            get => Options.MinSizeUnit == SizeUnit.KB;
            set
            {
                if (value)
                {
                    Options.MinSizeUnit = SizeUnit.KB;
                    Options.MaxSizeUnit = SizeUnit.KB;
                    ExecuteSearch();
                    NotifySizeUnitProperties();
                }
            }
        }

        public bool SizeMB
        {
            get => Options.MinSizeUnit == SizeUnit.MB;
            set
            {
                if (value)
                {
                    Options.MinSizeUnit = SizeUnit.MB;
                    Options.MaxSizeUnit = SizeUnit.MB;
                    ExecuteSearch();
                    NotifySizeUnitProperties();
                }
            }
        }

        public bool SizeGB
        {
            get => Options.MinSizeUnit == SizeUnit.GB;
            set
            {
                if (value)
                {
                    Options.MinSizeUnit = SizeUnit.GB;
                    Options.MaxSizeUnit = SizeUnit.GB;
                    ExecuteSearch();
                    NotifySizeUnitProperties();
                }
            }
        }

        private void NotifySizeUnitProperties()
        {
            OnPropertyChanged(nameof(SizeKB));
            OnPropertyChanged(nameof(SizeMB));
            OnPropertyChanged(nameof(SizeGB));
        }

        // 4. 미디어 프리셋 토글 래퍼 (영상, 음악, 사진, 문서, 코드, 실행, 압축)
        public bool MediaAll
        {
            get => Options.MediaPresets.Count == 0 || Options.MediaPresets.Contains("전체");
            set
            {
                if (value)
                {
                    Options.MediaPresets.Clear();
                    Options.MediaPresets.Add("전체");
                    ExecuteSearch();
                    NotifyMediaProperties();
                }
            }
        }

        public bool MediaVideo
        {
            get => Options.MediaPresets.Contains("영상");
            set => UpdateMediaPreset("영상", value);
        }

        public bool MediaAudio
        {
            get => Options.MediaPresets.Contains("음악");
            set => UpdateMediaPreset("음악", value);
        }

        public bool MediaPic
        {
            get => Options.MediaPresets.Contains("사진");
            set => UpdateMediaPreset("사진", value);
        }

        public bool MediaDoc
        {
            get => Options.MediaPresets.Contains("문서");
            set => UpdateMediaPreset("문서", value);
        }

        public bool MediaCode
        {
            get => Options.MediaPresets.Contains("코드");
            set => UpdateMediaPreset("코드", value);
        }

        public bool MediaExe
        {
            get => Options.MediaPresets.Contains("실행");
            set => UpdateMediaPreset("실행", value);
        }

        public bool MediaZip
        {
            get => Options.MediaPresets.Contains("압축");
            set => UpdateMediaPreset("압축", value);
        }

        private void UpdateMediaPreset(string preset, bool isChecked)
        {
            if (isChecked)
            {
                Options.MediaPresets.Remove("전체");
                Options.MediaPresets.Add(preset);
            }
            else
            {
                Options.MediaPresets.Remove(preset);
            }
            ExecuteSearch();
            NotifyMediaProperties();
        }

        private void NotifyMediaProperties()
        {
            OnPropertyChanged(nameof(MediaAll));
            OnPropertyChanged(nameof(MediaVideo));
            OnPropertyChanged(nameof(MediaAudio));
            OnPropertyChanged(nameof(MediaPic));
            OnPropertyChanged(nameof(MediaDoc));
            OnPropertyChanged(nameof(MediaCode));
            OnPropertyChanged(nameof(MediaExe));
            OnPropertyChanged(nameof(MediaZip));
        }

        #endregion

        public SearchViewModel()
        {
            SearchCommand = new RelayCommand(ExecuteSearch);
            ResetCommand = new RelayCommand(ExecuteReset);
            
            // 옵션의 체크박스 값이 바뀔 때 실시간 검색이 트리거되도록 이벤트를 물려야 하지만,
            // XAML 상에서 체크박스 IsChecked가 바인딩되는 Options 객체의 속성에 이벤트를 다는 대신,
            // 간단하게 해당 속성이 바뀔 때 ExecuteSearch가 실행되도록 View 비하인드나 
            // 커맨드 형태로 연결할 수도 있으나, CheckBox의 Command나 Click 이벤트를 활용해 ExecuteSearch를 바인딩하면 깔끔함.
            // (혹은 XAML 바인딩 상에서 Options.MatchCase 체크박스 변경 시 UI 이벤트를 뷰모델에 전파)
            
            CheckEngineStatus();
        }

        public void CheckEngineStatus()
        {
            if (EverythingBridge.IsEverythingRunning())
            {
                var ruleCount = DatabaseService.Instance.GetAllMappings().Count;
                StatusMessage = $"Everything 서비스 활성화 완료 | 매핑 테이블 규칙: {ruleCount}개 로드됨";
            }
            else
            {
                StatusMessage = "Everything 서비스 비활성화됨";
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    EngineNotRunningDetected?.Invoke();
                }));
            }
        }

        private int _selectedCount = 0;
        public int SelectedCount
        {
            get => _selectedCount;
            set
            {
                if (SetProperty(ref _selectedCount, value))
                {
                    UpdateResultCountMessage();
                }
            }
        }

        public void UpdateResultCountMessage()
        {
            ResultCountMessage = $"선택 항목: {SelectedCount:n0} / 검색 결과: {Results.Count:n0}개 항목";
        }

        private string _sortColumn = string.Empty;
        public string SortColumn
        {
            get => _sortColumn;
            set => SetProperty(ref _sortColumn, value);
        }

        private System.ComponentModel.ListSortDirection _sortDirection = System.ComponentModel.ListSortDirection.Ascending;
        public System.ComponentModel.ListSortDirection SortDirection
        {
            get => _sortDirection;
            set => SetProperty(ref _sortDirection, value);
        }

        public void SortResults(string columnName)
        {
            if (string.IsNullOrEmpty(columnName)) return;

            if (SortColumn == columnName)
            {
                SortDirection = SortDirection == System.ComponentModel.ListSortDirection.Ascending 
                    ? System.ComponentModel.ListSortDirection.Descending 
                    : System.ComponentModel.ListSortDirection.Ascending;
            }
            else
            {
                SortColumn = columnName;
                SortDirection = System.ComponentModel.ListSortDirection.Ascending;
            }

            ApplySorting();
        }

        private void ApplySorting()
        {
            if (Results.Count == 0 || string.IsNullOrEmpty(SortColumn)) return;

            List<SearchResultItem> sorted;
            bool asc = SortDirection == System.ComponentModel.ListSortDirection.Ascending;

            switch (SortColumn)
            {
                case "이름":
                case "Name":
                    sorted = asc ? Results.OrderBy(r => r.Name).ToList() : Results.OrderByDescending(r => r.Name).ToList();
                    break;
                case "경로":
                case "Path":
                    sorted = asc ? Results.OrderBy(r => r.Path).ToList() : Results.OrderByDescending(r => r.Path).ToList();
                    break;
                case "수정한 날짜":
                case "DisplayModifiedDate":
                case "ModifiedDate":
                    sorted = asc ? Results.OrderBy(r => r.ModifiedDate).ToList() : Results.OrderByDescending(r => r.ModifiedDate).ToList();
                    break;
                case "크기":
                case "DisplaySize":
                case "Size":
                    sorted = asc ? Results.OrderBy(r => r.Size).ToList() : Results.OrderByDescending(r => r.Size).ToList();
                    break;
                case "확장자":
                case "Extension":
                    sorted = asc ? Results.OrderBy(r => r.Extension).ToList() : Results.OrderByDescending(r => r.Extension).ToList();
                    break;
                default:
                    return;
            }

            for (int i = 0; i < sorted.Count; i++)
            {
                int oldIndex = Results.IndexOf(sorted[i]);
                if (oldIndex != i && oldIndex != -1)
                {
                    Results.Move(oldIndex, i);
                }
            }
        }

        public void ExecuteSearch()
        {
            try
            {
                if (!EverythingBridge.IsEverythingRunning())
                {
                    StatusMessage = "Everything 서비스 비활성화됨 (검색 불가)";
                    return;
                }

                var mappings = DatabaseService.Instance.GetCacheSnapshot();
                string transformedQuery = QueryTransformer.Transform(SearchQuery, Options, mappings);

                var searchItems = EverythingBridge.Search(transformedQuery, Options);

                Results.Clear();
                foreach (var item in searchItems)
                {
                    Results.Add(item);
                }

                ApplySorting();

                var ruleCount = mappings.Count;
                StatusMessage = $"Everything 서비스 활성화 완료 | 매핑 테이블 규칙: {ruleCount}개 로드됨";
                UpdateResultCountMessage();
            }
            catch (Exception ex)
            {
                StatusMessage = $"검색 중 오류 발생: {ex.Message}";
            }
        }

        private void ExecuteReset()
        {
            _searchQuery = string.Empty;
            _excludedWords = string.Empty;
            _folderPaths = string.Empty;
            _customExtensions = string.Empty;
            _minSize = null;
            _maxSize = null;
            _selectedCount = 0;
            _sortColumn = string.Empty;
            _sortDirection = System.ComponentModel.ListSortDirection.Ascending;

            Options.UseFastAlias = true;
            Options.MatchCase = false;
            Options.MatchWholeWord = false;
            Options.UseRegex = false;
            Options.IncludeRecycleBin = false;
            Options.Scope = SearchScope.All;
            Options.MediaPresets.Clear();
            Options.RecursiveSearch = true;

            // 모든 UI 전송 알림
            OnPropertyChanged(nameof(SearchQuery));
            OnPropertyChanged(nameof(ExcludedWords));
            OnPropertyChanged(nameof(FolderPaths));
            OnPropertyChanged(nameof(CustomExtensions));
            OnPropertyChanged(nameof(MinSizeText));
            OnPropertyChanged(nameof(MaxSizeText));
            OnPropertyChanged(nameof(Options));
            OnPropertyChanged(nameof(SelectedCount));

            NotifyScopeProperties();
            NotifySizeUnitProperties();
            NotifyMediaProperties();

            ExecuteSearch();
        }
    }
}
