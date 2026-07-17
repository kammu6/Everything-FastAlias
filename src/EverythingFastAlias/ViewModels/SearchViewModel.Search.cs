using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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

                // ─── [PERF] 단계별 시간 측정 ────────────────────────────────
                var perfLog = new StringBuilder();
                var totalSw = Stopwatch.StartNew();

                // Stage 0: alias cache snapshot 취득
                var sw0 = Stopwatch.StartNew();
                var mappings = DatabaseService.Instance.GetAliasGroupsCache().Groups;
                sw0.Stop();
                perfLog.AppendLine($"[PERF] Stage 0 - GetAliasGroupsCache: {sw0.ElapsedMilliseconds} ms  (규칙 수: {mappings.Count})");

                string transformedQuery = string.Empty;
                List<SearchResultItem> searchItems;

                // FFI 통신 및 동의어 치환 가공은 백그라운드 스레드에서 전담하여 UI 스레드 블로킹 제거
                (transformedQuery, searchItems) = await Task.Run(() =>
                {
                    // Stage 1: QueryTransformer
                    var sw1 = Stopwatch.StartNew();
                    string transformed = QueryTransformer.Transform(query, optionsCopy, mappings);
                    sw1.Stop();
                    perfLog.AppendLine($"[PERF] Stage 1 - QueryTransformer.Transform: {sw1.ElapsedMilliseconds} ms");
                    perfLog.AppendLine($"[PERF]           transformed query: {transformed}");

                    // Stage 2: Everything FFI (SetSearch + QueryW)
                    var sw2 = Stopwatch.StartNew();
                    var items = EverythingBridge.Search(transformed, optionsCopy);
                    sw2.Stop();
                    perfLog.AppendLine($"[PERF] Stage 2 - EverythingBridge.Search (FFI+마샬링): {sw2.ElapsedMilliseconds} ms  (결과 수: {items.Count})");

                    return (transformed, items);
                });

                // 방어 코드 추가: 비동기 처리 도중 검색어가 변경되었거나 삭제된 경우 결과 폐기
                if (query != SearchQuery)
                {
                    return;
                }

                // Stage 3: UI 바인딩 (ReplaceRange)
                var sw3 = Stopwatch.StartNew();
                Results.ReplaceRange(searchItems);
                sw3.Stop();
                perfLog.AppendLine($"[PERF] Stage 3 - Results.ReplaceRange (UI bind): {sw3.ElapsedMilliseconds} ms");

                // Stage 4: 정렬
                var sw4 = Stopwatch.StartNew();
                ApplySorting();
                sw4.Stop();
                perfLog.AppendLine($"[PERF] Stage 4 - ApplySorting: {sw4.ElapsedMilliseconds} ms");

                totalSw.Stop();
                perfLog.AppendLine($"[PERF] ─── Total elapsed: {totalSw.ElapsedMilliseconds} ms ─────────────────────");

                // 로그를 %APPDATA%\EverythingFastAlias\perf.log 에 누적 저장
                try
                {
                    var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EverythingFastAlias");
                    Directory.CreateDirectory(logDir);
                    var logPath = Path.Combine(logDir, "perf.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] query=\"{query}\"\n{perfLog}\n");
                }
                catch { /* 로그 실패 시 무시 */ }
                // ──────────────────────────────────────────────────────────────

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
