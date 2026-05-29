using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EverythingFastAlias.Models;
using EverythingFastAlias.Native;
using EverythingFastAlias.Services;
using EverythingFastAlias.Config;

namespace EverythingFastAlias.ViewModels
{
    public class SearchViewModel : ObservableObject
    {
        private readonly System.Windows.Threading.DispatcherTimer _debounceTimer;
        private bool _isSearching = false;
        private string? _pendingQuery = null;

        private string _searchQuery = string.Empty;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        Results.Clear();
                        _selectedCount = 0;
                        UpdateResultCountMessage();
                        if (EverythingBridge.IsEverythingRunning())
                        {
                            var ruleCount = DatabaseService.Instance.GetAllMappings().Count;
                            StatusMessage = $"Everything 서비스 활성화 완료 | 매핑 테이블 규칙: {ruleCount}개 로드됨";
                        }
                        else
                        {
                            StatusMessage = "Everything 서비스 비활성화됨";
                        }
                    }
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
                    TriggerSearch();
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
                    TriggerSearch();
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
                    TriggerSearch();
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
                    TriggerSearch();
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
                    TriggerSearch();
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
        public RangeObservableCollection<SearchResultItem> Results { get; } = new();

        public ICommand SearchCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand RefreshCommand { get; }

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
                    TriggerSearch();
                    NotifyScopeProperties();
                }
            }
        }

        public bool ScopeFile
        {
            get => Options.Scope == SearchScope.File;
            set
            {
                if (value)
                {
                    Options.Scope = SearchScope.File;
                    TriggerSearch();
                    NotifyScopeProperties();
                }
            }
        }

        public bool ScopePath
        {
            get => Options.Scope == SearchScope.Path;
            set
            {
                if (value)
                {
                    Options.Scope = SearchScope.Path;
                    TriggerSearch();
                    NotifyScopeProperties();
                }
            }
        }

        private void NotifyScopeProperties()
        {
            OnPropertyChanged(nameof(ScopeAll));
            OnPropertyChanged(nameof(ScopeFile));
            OnPropertyChanged(nameof(ScopePath));
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
                    TriggerSearch();
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
                    TriggerSearch();
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
                    TriggerSearch();
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

        // 4. 미디어 프리셋 토글 래퍼 (영상, 음악, 사진, 문서, 코드, 실행, 압축, 폴더)
        public bool MediaAll
        {
            get => Options.MediaPresets.Count == 0 || Options.MediaPresets.Contains("전체");
            set
            {
                if (value)
                {
                    // 폴더 선택 상태 보존 후 나머지 초기화
                    bool folderWasActive = Options.MediaPresets.Contains("폴더");
                    Options.MediaPresets.Clear();
                    Options.MediaPresets.Add("전체");
                    if (folderWasActive) Options.MediaPresets.Add("폴더");
                    TriggerSearch();
                    NotifyMediaProperties();
                }
            }
        }

        // 폴더 토글: 전체와 동시 선택 가능, 확장자 필터와는 상호배타
        public bool MediaFolder
        {
            get => Options.MediaPresets.Contains("폴더");
            set
            {
                if (value)
                {
                    // 폴더 ON: 확장자 필터는 모두 해제, 전체 상태는 유지
                    bool allWasActive = Options.MediaPresets.Contains("전체") || Options.MediaPresets.Count == 0;
                    Options.MediaPresets.Clear();
                    if (allWasActive) Options.MediaPresets.Add("전체");
                    Options.MediaPresets.Add("폴더");
                }
                else
                {
                    Options.MediaPresets.Remove("폴더");
                }
                TriggerSearch();
                NotifyMediaProperties();
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
                // 확장자 필터 ON: 폴더·전체 해제, 해당 필터만 추가
                Options.MediaPresets.Remove("전체");
                Options.MediaPresets.Remove("폴더");
                Options.MediaPresets.Add(preset);
            }
            else
            {
                Options.MediaPresets.Remove(preset);
            }
            TriggerSearch();
            NotifyMediaProperties();
        }

        private void NotifyMediaProperties()
        {
            OnPropertyChanged(nameof(MediaAll));
            OnPropertyChanged(nameof(MediaFolder));
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
            RefreshCommand = new RelayCommand(ExecuteSearch);
            
            // 디바운스 타이머 설정 (150ms 대기)
            _debounceTimer = new System.Windows.Threading.DispatcherTimer();
            _debounceTimer.Interval = TimeSpan.FromMilliseconds(150);
            _debounceTimer.Tick += DebounceTimer_Tick;
            
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

        private void TriggerSearch()
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();
            ExecuteSearchAsync();
        }

        public void ExecuteSearch()
        {
            // 즉각 검색을 위해 타이머를 중단하고 비동기 검색을 수행
            _debounceTimer.Stop();
            ExecuteSearchAsync();
        }

        public async void ExecuteSearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                Results.Clear();
                _selectedCount = 0;
                UpdateResultCountMessage();
                if (EverythingBridge.IsEverythingRunning())
                {
                    var ruleCount = DatabaseService.Instance.GetAllMappings().Count;
                    StatusMessage = $"Everything 서비스 활성화 완료 | 매핑 테이블 규칙: {ruleCount}개 로드됨";
                }
                else
                {
                    StatusMessage = "Everything 서비스 비활성화됨";
                }
                _pendingQuery = null;
                return;
            }

            if (_isSearching)
            {
                _pendingQuery = SearchQuery;
                return;
            }
            _isSearching = true;
            _pendingQuery = null;

            try
            {
                if (!EverythingBridge.IsEverythingRunning())
                {
                    StatusMessage = "Everything 서비스 비활성화됨 (검색 불가)";
                    return;
                }

                StatusMessage = "검색 중...";

                // 스레드 안정성을 위해 로컬 변수로 검색어 획득 및 쿼리 파라미터 복사
                var query = SearchQuery;
                var optionsCopy = new SearchOptions
                {
                    UseFastAlias = Options.UseFastAlias,
                    MatchCase = Options.MatchCase,
                    MatchWholeWord = Options.MatchWholeWord,
                    UseRegex = Options.UseRegex,
                    IncludeRecycleBin = Options.IncludeRecycleBin,
                    Scope = Options.Scope,
                    FolderPaths = Options.FolderPaths,
                    ExcludedWords = Options.ExcludedWords,
                    CustomExtensions = Options.CustomExtensions,
                    MinSize = Options.MinSize,
                    MaxSize = Options.MaxSize,
                    MinSizeUnit = Options.MinSizeUnit,
                    MaxSizeUnit = Options.MaxSizeUnit,
                    RecursiveSearch = Options.RecursiveSearch
                };
                optionsCopy.MediaPresets.UnionWith(Options.MediaPresets);

                var mappings = DatabaseService.Instance.GetCacheSnapshot();

                // FFI 통신 및 동의어 치환 가공은 백그라운드 스레드에서 전담하여 UI 스레드 블로킹 제거
                var searchItems = await Task.Run(() =>
                {
                    string transformed = QueryTransformer.Transform(query, optionsCopy, mappings);
                    return EverythingBridge.Search(transformed, optionsCopy);
                });

                // RangeObservableCollection의 ReplaceRange를 사용하여 단 한 번의 UI 갱신으로 대량 바인딩
                Results.ReplaceRange(searchItems);

                ApplySorting();

                var ruleCount = mappings.Count;
                StatusMessage = $"Everything 서비스 활성화 완료 | 매핑 테이블 규칙: {ruleCount}개 로드됨";
                UpdateResultCountMessage();
            }
            catch (Exception ex)
            {
                StatusMessage = $"검색 중 오류 발생: {ex.Message}";
            }
            finally
            {
                _isSearching = false;
                if (_pendingQuery != null)
                {
                    ExecuteSearchAsync();
                }
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
            Options.Scope = SearchScope.File;
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
