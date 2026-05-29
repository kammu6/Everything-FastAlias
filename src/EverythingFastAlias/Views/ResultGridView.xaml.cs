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

using UserControl = System.Windows.Controls.UserControl;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Point = System.Windows.Point;

namespace EverythingFastAlias.Views
{
    public partial class ResultGridView : UserControl
    {
        private Point _startPoint;
        private bool _isDragging;
        private SearchResultItem? _editingItem;

        public ResultGridView()
        {
            InitializeComponent();
        }

        #region 드래그 아웃 (Drag-out to external Explorer) 구현

        private void ListViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 드래그를 시작할 마우스 좌표 기록
            _startPoint = e.GetPosition(null);
        }

        private void ListViewItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point position = e.GetPosition(null);

                // 마우스 클릭 시 흔들림으로 인한 드래그 오동작 방지 임계치 비교
                if (Math.Abs(position.X - _startPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _startPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    StartDrag(e);
                }
            }
        }

        private void StartDrag(MouseEventArgs e)
        {
            _isDragging = true;
            try
            {
                var selectedItems = ResultsListView.SelectedItems.Cast<SearchResultItem>().ToList();
                if (selectedItems.Count > 0)
                {
                    // 선택 항목들의 전체 경로 수집
                    var paths = selectedItems.Select(item => item.FullPath).ToArray();

                    // Windows FileDrop 포맷 데이터 패킷 생성
                    var dataObject = new DataObject(DataFormats.FileDrop, paths);

                    // 드래그 아웃 실행
                    DragDrop.DoDragDrop(ResultsListView, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"드래그 드롭 실패: {ex.Message}");
            }
            finally
            {
                _isDragging = false;
            }
        }

        #endregion

        #region 네이티브 우클릭 메뉴 (Shell Context Menu) 구현

        private void ResultsListView_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var selectedItems = ResultsListView.SelectedItems.Cast<SearchResultItem>().ToList();
            if (selectedItems.Count == 0) return;

            var paths = selectedItems.Select(item => item.FullPath).ToList();
            
            // WPF Window 및 HWND 핸들을 찾아 네이티브 IContextMenu 호출
            var parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                e.Handled = true;
                ShellContextMenu.ShowContextMenu(parentWindow, paths);
            }
        }

        private void ResultsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var selectedItems = ResultsListView.SelectedItems.Cast<SearchResultItem>().ToList();
            if (selectedItems.Count == 0) return;

            foreach (var item in selectedItems)
            {
                var path = item.FullPath;
                if (File.Exists(path) || Directory.Exists(path))
                {
                    try
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true // 기본 연결프로그램으로 실행 보장
                        };
                        Process.Start(startInfo);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"파일 실행 실패: {ex.Message}", "에러", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        #endregion

        #region F2 인라인 이름변경 구현

        private void StartRename(SearchResultItem target)
        {
            // 1. 기존에 다른 항목이 편집 중이었다면 확실하게 취소
            if (_editingItem != null && _editingItem != target)
            {
                CancelRename(_editingItem);
            }

            // 2. 가상화 환경 대비하여 잔여 편집 플래그 일괄 강제 클리어
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

            _editingItem = target;
            target.EditingName = target.Name;
            target.IsEditing = true;
        }

        private void RenameBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.Focus();
                tb.SelectAll();
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
                {
                    CancelRename(item);
                }
                else
                {
                    CommitRename(item, tb.Text);
                }
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CancelRename(item);
            }
        }

        private void RenameBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is SearchResultItem item && item.IsEditing)
            {
                string newName = tb.Text.Trim();
                if (string.IsNullOrEmpty(newName) || newName == item.Name)
                {
                    CancelRename(item);
                }
                else
                {
                    CommitRename(item, tb.Text);
                }
            }
        }

        private void CommitRename(SearchResultItem item, string newBaseName)
        {
            newBaseName = newBaseName.Trim();
            if (_editingItem == item)
            {
                _editingItem = null;
            }
            item.IsEditing = false;

            if (string.IsNullOrEmpty(newBaseName) || newBaseName == item.Name)
                return;

            try
            {
                string oldPath = item.FullPath;
                string newPath = System.IO.Path.Combine(item.Path, newBaseName);

                if (item.IsFolder)
                    Directory.Move(oldPath, newPath);
                else
                    File.Move(oldPath, newPath);

                // 모델 갱신 (INotifyPropertyChanged 연동)
                item.Name = newBaseName;
                item.Extension = item.IsFolder ? string.Empty : System.IO.Path.GetExtension(newBaseName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이름 변경 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                // 실패 시 EditingName 원복
                item.EditingName = item.Name;
            }
        }

        private void CancelRename(SearchResultItem item)
        {
            if (_editingItem == item)
            {
                _editingItem = null;
            }
            item.EditingName = item.Name;
            item.IsEditing = false;
        }

        #endregion

        #region 단축키 (클립보드 Copy/Cut 및 Enter 파일 실행) 구현

        private void ResultsListView_KeyDown(object sender, KeyEventArgs e)
        {
            var selectedItems = ResultsListView.SelectedItems.Cast<SearchResultItem>().ToList();
            if (selectedItems.Count == 0) return;

            var paths = selectedItems.Select(item => item.FullPath).ToList();

            // 1. 복사 (Ctrl + C)
            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                try
                {
                    Win32ClipboardHelper.CopyFilesToClipboard(paths, isCut: false);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            // 2. 잘라내기 (Ctrl + X)
            else if (e.Key == Key.X && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                try
                {
                    Win32ClipboardHelper.CopyFilesToClipboard(paths, isCut: true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            // 3. F2 이름변경 (단일 선택 시에만)
            else if (e.Key == Key.F2)
            {
                e.Handled = true;
                if (selectedItems.Count == 1)
                {
                    StartRename(selectedItems[0]);
                }
            }
            // 4. 실행 (Enter)
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                foreach (var path in paths)
                {
                    if (File.Exists(path) || Directory.Exists(path))
                    {
                        try
                        {
                            var startInfo = new ProcessStartInfo
                            {
                                FileName = path,
                                UseShellExecute = true // 연결 프로그램 자동 실행 보장
                            };
                            Process.Start(startInfo);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"파일 실행 실패: {ex.Message}", "에러", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
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
                {
                    propertyName = header.Column.Header as string;
                }

                if (propertyName != null && DataContext is SearchViewModel vm)
                {
                    vm.SortResults(propertyName);
                }
            }
        }

        private void ResultsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is SearchViewModel vm)
            {
                vm.SelectedCount = ResultsListView.SelectedItems.Count;
            }

            if (_editingItem != null && !ResultsListView.SelectedItems.Contains(_editingItem))
            {
                CancelRename(_editingItem);
            }
        }

        #endregion
    }
}
