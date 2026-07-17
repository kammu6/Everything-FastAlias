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
    public partial class SearchViewModel : ObservableObject
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
                }
            }
        }

        private string _excludedPaths = string.Empty;
        public string ExcludedPaths
        {
            get => _excludedPaths;
            set
            {
                if (SetProperty(ref _excludedPaths, value))
                {
                    Options.ExcludedPaths = value;
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
                    bool folderWasActive = Options.MediaPresets.Contains("폴더");
                    Options.MediaPresets.Clear();
                    Options.MediaPresets.Add("전체");
                    if (folderWasActive) Options.MediaPresets.Add("폴더");
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
                    bool allWasActive = Options.MediaPresets.Contains("전체") || Options.MediaPresets.Count == 0;
                    Options.MediaPresets.Clear();
                    if (allWasActive) Options.MediaPresets.Add("전체");
                    Options.MediaPresets.Add("폴더");
                }
                else
                {
                    Options.MediaPresets.Remove("폴더");
                }
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
                Options.MediaPresets.Remove("전체");
                Options.MediaPresets.Remove("폴더");
                Options.MediaPresets.Add(preset);
            }
            else
            {
                Options.MediaPresets.Remove(preset);
            }
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

        // ── 보기 옵션 (ViewMode) ──
        private ViewMode _viewMode = ViewMode.Details;
        public ViewMode ViewMode
        {
            get => _viewMode;
            set
            {
                if (SetProperty(ref _viewMode, value))
                {
                    NotifyViewModeProperties();
                }
            }
        }

        public bool ViewModeDetails
        {
            get => ViewMode == ViewMode.Details;
            set { if (value) ViewMode = ViewMode.Details; }
        }

        public bool ViewModeThumbnailS
        {
            get => ViewMode == ViewMode.ThumbnailS;
            set { if (value) ViewMode = ViewMode.ThumbnailS; }
        }

        public bool ViewModeThumbnailM
        {
            get => ViewMode == ViewMode.ThumbnailM;
            set { if (value) ViewMode = ViewMode.ThumbnailM; }
        }

        public bool ViewModeThumbnailL
        {
            get => ViewMode == ViewMode.ThumbnailL;
            set { if (value) ViewMode = ViewMode.ThumbnailL; }
        }

        public double ThumbnailItemWidth => ViewMode switch
        {
            ViewMode.ThumbnailS => 80,
            ViewMode.ThumbnailM => 160,
            ViewMode.ThumbnailL => 240,
            _ => 160
        };

        public double ThumbnailItemHeight => ViewMode switch
        {
            ViewMode.ThumbnailS => 100,
            ViewMode.ThumbnailM => 200,
            ViewMode.ThumbnailL => 300,
            _ => 200
        };

        public double ThumbnailImageSize => ViewMode switch
        {
            ViewMode.ThumbnailS => 64,
            ViewMode.ThumbnailM => 128,
            ViewMode.ThumbnailL => 192,
            _ => 128
        };

        private void NotifyViewModeProperties()
        {
            OnPropertyChanged(nameof(ViewModeDetails));
            OnPropertyChanged(nameof(ViewModeThumbnailS));
            OnPropertyChanged(nameof(ViewModeThumbnailM));
            OnPropertyChanged(nameof(ViewModeThumbnailL));
            OnPropertyChanged(nameof(ThumbnailItemWidth));
            OnPropertyChanged(nameof(ThumbnailItemHeight));
            OnPropertyChanged(nameof(ThumbnailImageSize));
        }

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
    }
}
