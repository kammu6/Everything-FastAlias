using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using EverythingFastAlias.Models;
using EverythingFastAlias.Native;
using EverythingFastAlias.Services;

namespace EverythingFastAlias.ViewModels
{
    public partial class SearchViewModel
    {
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
