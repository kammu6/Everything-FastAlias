using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using EverythingFastAlias.Models;
using EverythingFastAlias.Native;
using EverythingFastAlias.ViewModels;
using System.Windows.Media;
using System.Windows.Threading;

using UserControl = System.Windows.Controls.UserControl;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Point = System.Windows.Point;

namespace EverythingFastAlias.Views
{
    public partial class ResultGridView : UserControl
    {
        // ─────────────────────────────────────────────────────────────────
        // 상태 머신 (State Machine)
        // 세 가지 상태를 명확히 분리하여 이벤트 간 교착을 원천 차단합니다.
        //   Idle    : 기본 대기 상태. 드래그/더블클릭/F2 모두 허용.
        //   Dragging: DoDragDrop 실행 중. LostFocus 무시, Editing 진입 차단.
        //   Editing : F2 이름변경 모드. 드래그 시작 완전 차단.
        // ─────────────────────────────────────────────────────────────────
        private enum ViewState { Idle, Dragging, Editing }
        private ViewState _viewState = ViewState.Idle;

        private Point _startPoint;
        private SearchResultItem? _editingItem;
        private ListViewItem? _clickedItem;
        // ESC/커밋 후 포커스 복원 시 RequestBringIntoView 자동 스크롤 억제 플래그
        private bool _suppressBringIntoView;

        public ResultGridView()
        {
            InitializeComponent();
        }

        #region 드래그 아웃 (Drag-out to external Explorer) 구현

        private void ListViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // [Guard] 편집 모드 중: 드래그/선택 로직 완전 차단.
            // TextBox가 독점적으로 마우스 이벤트를 처리하도록 이벤트 전파를 허용합니다.
            if (_viewState == ViewState.Editing)
            {
                _clickedItem = null;
                return;
            }

            // 더블클릭: 기본 연결 프로그램으로 파일 열기
            // (PreviewMouseLeftButtonDown에서 처리해야 e.Handled=true 이후에도 동작 보장)
            if (e.ClickCount == 2)
            {
                if (sender is ListViewItem item && item.DataContext is SearchResultItem searchItem)
                {
                    OpenFile(searchItem.FullPath);
                }
                e.Handled = true;
                return;
            }

            // 드래그 시작 좌표 기록
            _startPoint = e.GetPosition(null);
            _clickedItem = null;

            if (sender is ListViewItem item2)
            {
                // 이미 선택된 아이템 클릭 시: 즉시 선택 해제를 막아 드래그가 가능하도록 지연 처리
                if (item2.IsSelected)
                {
                    _clickedItem = item2;
                    e.Handled = true;
                }
            }
        }

        private void ListViewItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // [Guard] 편집 모드 중에는 선택 로직 개입 금지
            if (_viewState == ViewState.Editing)
            {
                _clickedItem = null;
                return;
            }

            if (_clickedItem != null)
            {
                // 드래그를 하지 않고 마우스를 뗀 경우: 수동 선택 처리
                if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == 0)
                {
                    ResultsListView.SelectedItem = _clickedItem.DataContext;
                }
                else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    _clickedItem.IsSelected = !_clickedItem.IsSelected;
                }
                _clickedItem.Focus();
                _clickedItem = null;
            }
        }

        private void ListViewItem_MouseMove(object sender, MouseEventArgs e)
        {
            // [Guard] Idle 상태가 아니면 드래그 감지 완전 차단
            // - Editing 상태: 텍스트 커서 이동을 드래그로 잘못 인식 방지
            // - Dragging 상태: 중복 DoDragDrop 호출 방지
            if (_viewState != ViewState.Idle) return;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point position = e.GetPosition(null);

                // 시스템 임계치를 초과하는 마우스 이동이 있을 때만 드래그 시작
                if (Math.Abs(position.X - _startPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _startPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    StartDrag();
                }
            }
        }

        private void StartDrag()
        {
            var selectedItems = ResultsListView.SelectedItems.Cast<SearchResultItem>().ToList();
            if (selectedItems.Count == 0) return;

            // 상태 전환: Idle → Dragging
            _viewState = ViewState.Dragging;
            _clickedItem = null;

            try
            {
                var paths = selectedItems.Select(item => item.FullPath).ToArray();
                var dataObject = new DataObject(DataFormats.FileDrop, paths);

                // DoDragDrop은 동기 블로킹 메서드입니다.
                // Dragging 상태 보호 덕분에 LostFocus가 발생해도 이름변경 모드로 잘못 전환되지 않습니다.
                DragDrop.DoDragDrop(ResultsListView, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"드래그 드롭 실패: {ex.Message}");
            }
            finally
            {
                // 상태 복원: Dragging → Idle
                _viewState = ViewState.Idle;
            }
        }

        #endregion

        #region 네이티브 우클릭 메뉴 (Shell Context Menu) 구현

        private void ResultsListView_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            DependencyObject dep = (DependencyObject)e.OriginalSource;
            ListViewItem? item = FindVisualParent<ListViewItem>(dep);

            if (item != null)
            {
                // 아이템 우클릭 시, 선택되지 않았다면 유일하게 선택하여 쉘 메뉴에 연결되도록 유도
                if (!item.IsSelected)
                {
                    if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == 0)
                    {
                        ResultsListView.SelectedItem = item.DataContext;
                    }
                    else
                    {
                        item.IsSelected = true;
                    }
                }

                var selectedItems = ResultsListView.SelectedItems.Cast<SearchResultItem>().ToList();
                if (selectedItems.Count == 0) return;

                var paths = selectedItems.Select(x => x.FullPath).ToList();
                var parentWindow = Window.GetWindow(this);
                if (parentWindow != null)
                {
                    e.Handled = true;
                    ShellContextMenu.ShowContextMenu(parentWindow, paths);
                }
            }
            else
            {
                // 빈 공간 우클릭 시, 배경 컨텍스트 메뉴 동적 구성
                e.Handled = true;
                ShowBackgroundContextMenu();
            }
        }

        private void ResultsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // ListViewItem_PreviewMouseLeftButtonDown에서 더블클릭을 이미 처리했으므로
            // 이곳에서 재처리하지 않습니다. (중복 실행 방지)
        }

        #endregion

        #region F2 인라인 이름변경 구현

        private void StartRename(SearchResultItem target)
        {
            // 기존에 다른 항목이 편집 중이었다면 먼저 취소
            if (_editingItem != null && _editingItem != target)
            {
                CancelRename(_editingItem);
            }

            // 가상화 환경 대비: 화면 밖 아이템의 IsEditing 잔여 플래그 일괄 초기화
            if (ResultsListView.ItemsSource is System.Collections.IEnumerable items)
            {
                foreach (var obj in items)
                {
                    if (obj is SearchResultItem item && item != target && item.IsEditing)
                    {
                        item.IsEditing = false;
                        item.EditingName = item.Name;
                    }
                }
            }

            // 상태 전환: Idle → Editing
            _viewState = ViewState.Editing;
            _editingItem = target;
            target.EditingName = target.Name;
            target.IsEditing = true;
        }

        private void RenameBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb) return;

            // TextBox가 XAML 렌더링 트리에 완전히 삽입된 후 포커스를 강제 획득합니다.
            // DispatcherPriority.Loaded: 레이아웃 및 렌더링 완료 후 실행 보장.
            tb.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                // Keyboard.Focus: 키보드 입력 포커스 (실제 타이핑 수신)
                // tb.Focus(): WPF 논리 포커스 (UI 하이라이트)
                // 두 가지를 모두 설정해야 TextBox에 커서가 완전히 활성화됩니다.
                Keyboard.Focus(tb);
                tb.Focus();
                tb.SelectAll();
            }));
        }

        private void RenameBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is TextBox tb && tb.IsVisible)
            {
                tb.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                {
                    Keyboard.Focus(tb);
                    tb.Focus();
                    tb.SelectAll();
                }));
            }
        }

        private void RenameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox tb || tb.DataContext is not SearchResultItem item) return;

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                string newName = tb.Text.Trim();
                if (string.IsNullOrEmpty(newName) || newName == item.Name)
                    CancelRename(item);
                else
                    CommitRename(item, newName);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                // CancelRename 전에 참조 저장 (Cancel 후 _editingItem은 null이 됨)
                var target = item;
                CancelRename(item);
                RestoreFocusToItem(target);
            }
        }

        private void RenameBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // [Guard] Dragging 상태에서 발생하는 LostFocus는 무시합니다.
            // DoDragDrop 실행 중 내부 메시지 루프에서 LostFocus가 올라오는 경우를 차단합니다.
            if (_viewState == ViewState.Dragging) return;

            if (sender is TextBox tb && tb.DataContext is SearchResultItem item && item.IsEditing)
            {
                string newName = tb.Text.Trim();
                if (string.IsNullOrEmpty(newName) || newName == item.Name)
                    CancelRename(item);
                else
                    CommitRename(item, newName);
            }
        }

        private void CommitRename(SearchResultItem item, string newBaseName)
        {
            newBaseName = newBaseName.Trim();

            // 상태 초기화 먼저 (파일 I/O 도중 다른 이벤트가 재진입하지 못하도록)
            if (_editingItem == item) _editingItem = null;
            item.IsEditing = false;

            // 상태 복원: Editing → Idle
            if (_viewState == ViewState.Editing) _viewState = ViewState.Idle;

            if (string.IsNullOrEmpty(newBaseName) || newBaseName == item.Name) return;

            try
            {
                string oldPath = item.FullPath;
                string newPath = System.IO.Path.Combine(item.Path, newBaseName);

                if (item.IsFolder)
                    Directory.Move(oldPath, newPath);
                else
                    File.Move(oldPath, newPath);

                // 모델 갱신
                item.Name = newBaseName;
                item.Extension = item.IsFolder ? string.Empty : System.IO.Path.GetExtension(newBaseName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이름 변경 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                item.EditingName = item.Name;
            }
        }

        private void CancelRename(SearchResultItem item)
        {
            if (_editingItem == item) _editingItem = null;
            item.EditingName = item.Name;
            item.IsEditing = false;

            // 상태 복원: Editing → Idle
            if (_viewState == ViewState.Editing) _viewState = ViewState.Idle;
        }

        #endregion

        #region 단축키 (클립보드 Copy/Cut, F2 이름변경, Enter 파일 실행) 구현

        private void ResultsListView_KeyDown(object sender, KeyEventArgs e)
        {
            // [Guard] 편집 상태: TextBox 포커스 획득 타이밍 경합과 무관하게 ESC를 확실히 처리.
            // TextBox.KeyDown에서 e.Handled=true를 설정하면 여기에 도달하지 않지만,
            // TextBox가 포커스를 아직 받지 못한 상태에서 ESC가 눌린 경우 여기서 catch.
            if (_viewState == ViewState.Editing)
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    var editItem = _editingItem;
                    if (editItem != null)
                    {
                        CancelRename(editItem);
                        RestoreFocusToItem(editItem);
                    }
                }
                // 편집 중에는 방향키 내비게이션 등 모든 ListView 단축키 차단
                return;
            }

            var selectedItems = ResultsListView.SelectedItems.Cast<SearchResultItem>().ToList();
            if (selectedItems.Count == 0) return;

            var paths = selectedItems.Select(item => item.FullPath).ToList();

            // 1. 복사 (Ctrl + C)
            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                try { Win32ClipboardHelper.CopyFilesToClipboard(paths, isCut: false); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
            // 2. 잘라내기 (Ctrl + X)
            else if (e.Key == Key.X && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                try { Win32ClipboardHelper.CopyFilesToClipboard(paths, isCut: true); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
            // 3. F2 이름변경 (단일 선택 시에만, Idle 상태에서만)
            else if (e.Key == Key.F2 && _viewState == ViewState.Idle)
            {
                e.Handled = true;
                if (selectedItems.Count == 1)
                {
                    StartRename(selectedItems[0]);
                }
            }
            // 4. 실행 (Enter) — 편집 중이 아닐 때만
            else if (e.Key == Key.Enter && _viewState != ViewState.Editing)
            {
                e.Handled = true;
                foreach (var path in paths)
                    OpenFile(path);
            }
            // 5. 휴지통 삭제 (Delete)
            else if (e.Key == Key.Delete)
            {
                e.Handled = true;
                DeleteSelectedItems();
            }
        }

        #endregion

        #region 정렬 및 선택 항목 이벤트 연동

        private void ResultsGridViewHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader header && header.Column != null)
            {
                var binding = header.Column.DisplayMemberBinding as Binding;
                string? propertyName = binding?.Path?.Path;

                if (string.IsNullOrEmpty(propertyName))
                    propertyName = header.Column.Header as string;

                if (propertyName != null && DataContext is SearchViewModel vm)
                    vm.SortResults(propertyName);
            }
        }

        private void ResultsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is SearchViewModel vm)
                vm.SelectedCount = ResultsListView.SelectedItems.Count;

            // 편집 중인 항목이 선택 해제되면 편집 취소
            if (_editingItem != null && !ResultsListView.SelectedItems.Contains(_editingItem))
                CancelRename(_editingItem);
        }

        #endregion

        #region 헬퍼 메서드

        /// <summary>
        /// 편집/커밋 완료 후 해당 ListViewItem에 직접 포커스를 복원합니다.
        /// ListView.Focus() 대신 이 메서드를 사용해야 하는 이유:
        /// ListView.Focus()는 내부적으로 첫 번째 아이템에 포커스를 이동하고
        /// ScrollIntoView를 발동시켜 스크롤이 맨 위로 올라가는 부작용이 있습니다.
        /// </summary>
        private void RestoreFocusToItem(SearchResultItem item)
        {
            // RequestBringIntoView 자동 스크롤을 일시 억제하여 포커스 복원 중 스크롤 점프 방지
            _suppressBringIntoView = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                try
                {
                    if (ResultsListView.ItemContainerGenerator.ContainerFromItem(item) is ListViewItem lvi)
                        lvi.Focus();
                    else
                        // 가상화로 인해 컨테이너가 없는 경우 — 스크롤 억제 없이 ListView에 포커스
                        ResultsListView.Focus();
                }
                finally
                {
                    _suppressBringIntoView = false;
                }
            }));
        }

        /// <summary>
        /// ListView 아이템 포커스 이동 시 자동 스크롤(RequestBringIntoView)을 억제합니다.
        /// XAML에서 ListView.RequestBringIntoView 이벤트와 연결됩니다.
        /// </summary>
        private void ResultsListView_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            if (_suppressBringIntoView)
                e.Handled = true;
        }

        private void ShowBackgroundContextMenu()
        {
            if (DataContext is not SearchViewModel vm) return;

            var menu = new ContextMenu();

            // 1. 보기 서브메뉴
            var viewItem = new MenuItem { Header = "보기" };
            
            var detailsMenu = new MenuItem { Header = "자세히", IsCheckable = true, IsChecked = vm.ViewMode == ViewMode.Details };
            detailsMenu.Click += (s, e) => vm.ViewMode = ViewMode.Details;
            
            var thumbSMenu = new MenuItem { Header = "섬네일S", IsCheckable = true, IsChecked = vm.ViewMode == ViewMode.ThumbnailS };
            thumbSMenu.Click += (s, e) => vm.ViewMode = ViewMode.ThumbnailS;
            
            var thumbMMenu = new MenuItem { Header = "섬네일M", IsCheckable = true, IsChecked = vm.ViewMode == ViewMode.ThumbnailM };
            thumbMMenu.Click += (s, e) => vm.ViewMode = ViewMode.ThumbnailM;
            
            var thumbLMenu = new MenuItem { Header = "섬네일L", IsCheckable = true, IsChecked = vm.ViewMode == ViewMode.ThumbnailL };
            thumbLMenu.Click += (s, e) => vm.ViewMode = ViewMode.ThumbnailL;

            viewItem.Items.Add(detailsMenu);
            viewItem.Items.Add(thumbSMenu);
            viewItem.Items.Add(thumbMMenu);
            viewItem.Items.Add(thumbLMenu);
            menu.Items.Add(viewItem);

            // 2. 정렬 기준 서브메뉴
            var sortItem = new MenuItem { Header = "정렬 기준" };
            
            var sortByName = new MenuItem { Header = "이름", IsCheckable = true, IsChecked = vm.SortColumn == "이름" || vm.SortColumn == "Name" };
            sortByName.Click += (s, e) => { vm.SortColumn = "이름"; vm.SortResults("이름"); };
            
            var sortByPath = new MenuItem { Header = "경로", IsCheckable = true, IsChecked = vm.SortColumn == "경로" || vm.SortColumn == "Path" };
            sortByPath.Click += (s, e) => { vm.SortColumn = "경로"; vm.SortResults("경로"); };
            
            var sortByDate = new MenuItem { Header = "수정한 날짜", IsCheckable = true, IsChecked = vm.SortColumn == "수정한 날짜" || vm.SortColumn == "DisplayModifiedDate" || vm.SortColumn == "ModifiedDate" };
            sortByDate.Click += (s, e) => { vm.SortColumn = "수정한 날짜"; vm.SortResults("수정한 날짜"); };
            
            var sortBySize = new MenuItem { Header = "크기", IsCheckable = true, IsChecked = vm.SortColumn == "크기" || vm.SortColumn == "DisplaySize" || vm.SortColumn == "Size" };
            sortBySize.Click += (s, e) => { vm.SortColumn = "크기"; vm.SortResults("크기"); };

            var sortAsc = new MenuItem { Header = "오름차순", IsCheckable = true, IsChecked = vm.SortDirection == System.ComponentModel.ListSortDirection.Ascending };
            sortAsc.Click += (s, e) => { vm.SortDirection = System.ComponentModel.ListSortDirection.Ascending; vm.SortResults(vm.SortColumn); };
            
            var sortDesc = new MenuItem { Header = "내림차순", IsCheckable = true, IsChecked = vm.SortDirection == System.ComponentModel.ListSortDirection.Descending };
            sortDesc.Click += (s, e) => { vm.SortDirection = System.ComponentModel.ListSortDirection.Descending; vm.SortResults(vm.SortColumn); };

            sortItem.Items.Add(sortByName);
            sortItem.Items.Add(sortByPath);
            sortItem.Items.Add(sortByDate);
            sortItem.Items.Add(sortBySize);
            sortItem.Items.Add(new Separator());
            sortItem.Items.Add(sortAsc);
            sortItem.Items.Add(sortDesc);
            menu.Items.Add(sortItem);

            menu.Items.Add(new Separator());

            // 3. 새로고침
            var refreshItem = new MenuItem { Header = "새로고침", InputGestureText = "F5" };
            refreshItem.Click += (s, e) => { if (vm.RefreshCommand.CanExecute(null)) vm.RefreshCommand.Execute(null); };
            menu.Items.Add(refreshItem);

            menu.PlacementTarget = ResultsListView;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        private void DeleteSelectedItems()
        {
            var selectedItems = ResultsListView.SelectedItems.Cast<SearchResultItem>().ToList();
            if (selectedItems.Count == 0) return;

            if (DataContext is not SearchViewModel vm) return;

            // 1. 삭제 후 포커스를 가질 대상 미리 선정
            SearchResultItem? nextSelectedItem = null;
            int maxIndex = -1;
            int minIndex = int.MaxValue;
            foreach (var item in selectedItems)
            {
                int idx = ResultsListView.Items.IndexOf(item);
                if (idx > maxIndex) maxIndex = idx;
                if (idx < minIndex) minIndex = idx;
            }

            if (maxIndex != -1)
            {
                // 삭제 대상 목록 뒤에 있는 다음 아이템
                int nextIdx = maxIndex + 1;
                while (nextIdx < ResultsListView.Items.Count)
                {
                    var candidate = (SearchResultItem)ResultsListView.Items[nextIdx];
                    if (!selectedItems.Contains(candidate))
                    {
                        nextSelectedItem = candidate;
                        break;
                    }
                    nextIdx++;
                }

                // 만약 다음 아이템이 없다면 이전 아이템
                if (nextSelectedItem == null)
                {
                    int prevIdx = minIndex - 1;
                    while (prevIdx >= 0)
                    {
                        var candidate = (SearchResultItem)ResultsListView.Items[prevIdx];
                        if (!selectedItems.Contains(candidate))
                        {
                            nextSelectedItem = candidate;
                            break;
                        }
                        prevIdx--;
                    }
                }
            }

            // 2. 휴지통으로 실제 삭제 및 리스트 뷰에서 제거
            var removedItems = new List<SearchResultItem>();
            foreach (var item in selectedItems)
            {
                if (Win32RecycleBinHelper.SendToRecycleBin(new[] { item.FullPath }))
                {
                    vm.Results.Remove(item);
                    removedItems.Add(item);
                }
            }

            if (removedItems.Count == 0) return;

            // 3. ViewModel 카운트 갱신
            vm.SelectedCount = ResultsListView.SelectedItems.Count;
            vm.UpdateResultCountMessage();

            // 4. 후속 포커스 복원 (자동 스크롤 억제 포함)
            if (nextSelectedItem != null && vm.Results.Contains(nextSelectedItem))
            {
                _suppressBringIntoView = true;
                try
                {
                    ResultsListView.SelectedItem = nextSelectedItem;
                    RestoreFocusToItem(nextSelectedItem);
                }
                finally
                {
                    _suppressBringIntoView = false;
                }
            }
            else
            {
                ResultsListView.SelectedIndex = -1;
            }
        }

        /// <summary>파일 또는 폴더를 기본 연결 프로그램으로 엽니다.</summary>
        private static void OpenFile(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return;
            try
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"파일 실행 실패: {ex.Message}", "에러", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindVisualParent<T>(parentObject);
        }

        #endregion
    }
}
