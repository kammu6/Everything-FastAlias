using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EverythingFastAlias.Models;
using EverythingFastAlias.Native;

namespace EverythingFastAlias.Views
{
    public partial class ResultGridView : UserControl
    {
        private Point _startPoint;
        private bool _isDragging;

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
            // 3. 실행 (Enter)
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
    }
}
