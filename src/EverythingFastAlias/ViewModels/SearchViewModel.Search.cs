using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EverythingFastAlias.Models;
using EverythingFastAlias.Native;
using EverythingFastAlias.Services;

namespace EverythingFastAlias.ViewModels
{
    public partial class SearchViewModel
    {
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
            SaveSettings();
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
                    ExcludedPaths = Options.ExcludedPaths,
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
    }
}
