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
        private readonly ObservableCollection<DriveOptionItem> _drives = new();
        public ObservableCollection<DriveOptionItem> Drives => _drives;
        private bool _isUpdatingDrives = false;

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
                    TriggerSearchOnly();
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
                    TriggerSearchOnly();
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
                    TriggerSearchOnly();
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
                    TriggerSearchOnly();
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
                    TriggerSearchOnly();
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
                    TriggerSearchOnly();
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
                    TriggerSearchOnly();
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
                    TriggerSearchOnly();
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
                    TriggerSearchOnly();
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
                    TriggerSearchOnly();
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
                    TriggerSearchOnly();
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
                    TriggerSearchOnly();
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
                TriggerSearchOnly();
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
            TriggerSearchOnly();
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
            
            LoadSettings();
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

            Results.ReplaceRange(sorted);
        }

        private void TriggerSearchOnly()
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void TriggerSearch()
        {
            SaveSettings();
            TriggerSearchOnly();
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

            // 실제 검색이 가동되는 순간에 설정을 1회 DB에 일괄 저장 (디스크 I/O 최적화)
            SaveSettings();

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
                optionsCopy.TargetDrives.UnionWith(Options.TargetDrives);

                var mappings = DatabaseService.Instance.GetCacheSnapshot();

                string transformedQuery = string.Empty;
                // FFI 통신 및 동의어 치환 가공은 백그라운드 스레드에서 전담하여 UI 스레드 블로킹 제거
                var searchItems = await Task.Run(() =>
                {
                    string transformed = QueryTransformer.Transform(query, optionsCopy, mappings);
                    transformedQuery = transformed;
                    return EverythingBridge.Search(transformed, optionsCopy);
                });

                // 방어 코드 추가: 비동기 처리 도중 검색어가 변경되었거나 삭제된 경우 결과 폐기
                if (query != SearchQuery)
                {
                    return;
                }

                // RangeObservableCollection의 ReplaceRange를 사용하여 단 한 번의 UI 갱신으로 대량 바인딩
                Results.ReplaceRange(searchItems);

                ApplySorting();

                var ruleCount = mappings.Count;
                StatusMessage = $"[Everything 쿼리]: {transformedQuery} | 매핑 규칙: {ruleCount}개";
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

            // 드라이브 선택 리셋 (기본 전체 활성화)
            _isUpdatingDrives = true;
            foreach (var d in Drives)
            {
                d.IsChecked = true;
            }
            _isUpdatingDrives = false;
            UpdateTargetDrivesFromList();

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

            SaveSettings();
            ExecuteSearch();
        }

        private void InitializeDrives()
        {
            try
            {
                var savedDrivesStr = DatabaseService.Instance.GetSetting("TargetDrives", "");
                var savedDrives = savedDrivesStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                .Select(d => d.Trim())
                                                .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var fixedDrives = new List<string>();
                foreach (var d in System.IO.DriveInfo.GetDrives())
                {
                    try
                    {
                        if (d.DriveType == System.IO.DriveType.Fixed)
                        {
                            fixedDrives.Add(d.Name.Substring(0, 2));
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"드라이브 속성 접근 무시 ({d.Name}): {ex.Message}");
                    }
                }

                Drives.Clear();
                foreach (var drive in fixedDrives)
                {
                    bool isChecked = true;
                    if (savedDrivesStr.Length > 0)
                    {
                        isChecked = savedDrives.Contains(drive);
                    }

                    var item = new DriveOptionItem { Name = drive, IsChecked = isChecked };
                    item.PropertyChanged += DriveItem_PropertyChanged;
                    Drives.Add(item);
                }

                UpdateTargetDrivesFromList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"드라이브 로드 오류: {ex.Message}");
            }
        }

        private void DriveItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_isUpdatingDrives) return;
            if (e.PropertyName == nameof(DriveOptionItem.IsChecked) && sender is DriveOptionItem changedItem)
            {
                int checkedCount = Drives.Count(d => d.IsChecked);
                if (checkedCount == 0)
                {
                    _isUpdatingDrives = true;
                    changedItem.IsChecked = true;
                    _isUpdatingDrives = false;
                    MessageBox.Show("최소한 1개의 검색 대상 드라이브가 선택되어 있어야 합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                UpdateTargetDrivesFromList();
                TriggerSearchOnly();
            }
        }

        private void UpdateTargetDrivesFromList()
        {
            Options.TargetDrives.Clear();
            foreach (var drive in Drives.Where(d => d.IsChecked))
            {
                Options.TargetDrives.Add(drive.Name);
            }
        }

        public void LoadSettings()
        {
            try
            {
                var db = DatabaseService.Instance;

                Options.UseFastAlias = db.GetSetting("UseFastAlias", "true") == "true";
                Options.MatchCase = db.GetSetting("MatchCase", "false") == "true";
                Options.MatchWholeWord = db.GetSetting("MatchWholeWord", "false") == "true";
                Options.UseRegex = db.GetSetting("UseRegex", "false") == "true";
                Options.IncludeRecycleBin = db.GetSetting("IncludeRecycleBin", "false") == "true";
                
                var scopeStr = db.GetSetting("Scope", "File");
                if (Enum.TryParse<SearchScope>(scopeStr, out var scope))
                    Options.Scope = scope;
                
                var presetsStr = db.GetSetting("MediaPresets", "전체");
                Options.MediaPresets.Clear();
                foreach (var preset in presetsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = preset.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        Options.MediaPresets.Add(trimmed);
                }

                _customExtensions = db.GetSetting("CustomExtensions", "");
                Options.CustomExtensions = _customExtensions;

                var minSizeStr = db.GetSetting("MinSize", "");
                if (long.TryParse(minSizeStr, out long minVal))
                {
                    _minSize = minVal;
                    Options.MinSize = minVal;
                }
                else
                {
                    _minSize = null;
                    Options.MinSize = null;
                }

                var maxSizeStr = db.GetSetting("MaxSize", "");
                if (long.TryParse(maxSizeStr, out long maxVal))
                {
                    _maxSize = maxVal;
                    Options.MaxSize = maxVal;
                }
                else
                {
                    _maxSize = null;
                    Options.MaxSize = null;
                }

                var minUnitStr = db.GetSetting("MinSizeUnit", "MB");
                if (Enum.TryParse<SizeUnit>(minUnitStr, out var minUnit))
                    Options.MinSizeUnit = minUnit;

                var maxUnitStr = db.GetSetting("MaxSizeUnit", "MB");
                if (Enum.TryParse<SizeUnit>(maxUnitStr, out var maxUnit))
                    Options.MaxSizeUnit = maxUnit;

                _excludedWords = db.GetSetting("ExcludedWords", "");
                Options.ExcludedWords = _excludedWords;

                _folderPaths = db.GetSetting("FolderPaths", "");
                Options.FolderPaths = _folderPaths;

                OnPropertyChanged(nameof(SearchQuery));
                OnPropertyChanged(nameof(ExcludedWords));
                OnPropertyChanged(nameof(FolderPaths));
                OnPropertyChanged(nameof(CustomExtensions));
                OnPropertyChanged(nameof(MinSizeText));
                OnPropertyChanged(nameof(MaxSizeText));
                NotifyScopeProperties();
                NotifySizeUnitProperties();
                NotifyMediaProperties();

                InitializeDrives();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"설정 복원 실패: {ex.Message}");
            }
        }

        public void SaveSettings()
        {
            try
            {
                var db = DatabaseService.Instance;

                db.SaveSetting("UseFastAlias", Options.UseFastAlias ? "true" : "false");
                db.SaveSetting("MatchCase", Options.MatchCase ? "true" : "false");
                db.SaveSetting("MatchWholeWord", Options.MatchWholeWord ? "true" : "false");
                db.SaveSetting("UseRegex", Options.UseRegex ? "true" : "false");
                db.SaveSetting("IncludeRecycleBin", Options.IncludeRecycleBin ? "true" : "false");
                db.SaveSetting("Scope", Options.Scope.ToString());
                db.SaveSetting("MediaPresets", string.Join(",", Options.MediaPresets));
                db.SaveSetting("CustomExtensions", Options.CustomExtensions);
                db.SaveSetting("MinSize", Options.MinSize?.ToString() ?? "");
                db.SaveSetting("MaxSize", Options.MaxSize?.ToString() ?? "");
                db.SaveSetting("MinSizeUnit", Options.MinSizeUnit.ToString());
                db.SaveSetting("MaxSizeUnit", Options.MaxSizeUnit.ToString());
                db.SaveSetting("ExcludedWords", Options.ExcludedWords);
                db.SaveSetting("FolderPaths", Options.FolderPaths);

                var activeDrives = Drives.Where(d => d.IsChecked).Select(d => d.Name);
                db.SaveSetting("TargetDrives", string.Join(",", activeDrives));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"설정 저장 실패: {ex.Message}");
            }
        }
    }
}
