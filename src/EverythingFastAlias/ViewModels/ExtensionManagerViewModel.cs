using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EverythingFastAlias.Config;
using EverythingFastAlias.Models;
using EverythingFastAlias.Services;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace EverythingFastAlias.ViewModels
{
    public class ExtensionManagerViewModel : ObservableObject
    {
        public ObservableCollection<ExtensionCategoryItem> Categories { get; } = new();

        private string _statusMessage = "확장자 설정을 수정하고 [저장] 버튼을 누르면 즉시 적용됩니다.";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private Brush _statusForeground = Brushes.Gray;
        public Brush StatusForeground
        {
            get => _statusForeground;
            set => SetProperty(ref _statusForeground, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand ResetAllCommand { get; }
        public ICommand ResetCategoryCommand { get; }
        public ICommand CloseCommand { get; }

        public event Action? RequestClose;

        public ExtensionManagerViewModel()
        {
            SaveCommand = new RelayCommand(Save);
            ResetAllCommand = new RelayCommand(ResetAll);
            ResetCategoryCommand = new RelayCommand<ExtensionCategoryItem>(ResetCategory);
            CloseCommand = new RelayCommand(() => RequestClose?.Invoke());

            LoadCategories();
        }

        public void LoadCategories()
        {
            Categories.Clear();
            var service = ExtensionSettingsService.Instance;

            foreach (var category in FileExtensionConstants.AllCategories)
            {
                string currentExts = service.GetExtensions(category);
                Categories.Add(new ExtensionCategoryItem(category, currentExts));
            }
        }

        private void Save()
        {
            try
            {
                ExtensionSettingsService.Instance.SaveAll(Categories);

                // 다시 정규화된 값으로 UI 갱신
                foreach (var item in Categories)
                {
                    item.Extensions = ExtensionSettingsService.Instance.GetExtensions(item.CategoryName);
                }

                StatusMessage = "모든 확장자 설정이 성공적으로 저장 및 적용되었습니다.";
                StatusForeground = new SolidColorBrush(Color.FromRgb(16, 137, 62)); // Green
            }
            catch (Exception ex)
            {
                StatusMessage = $"저장 실패: {ex.Message}";
                StatusForeground = new SolidColorBrush(Color.FromRgb(209, 52, 56)); // Red
            }
        }

        private void ResetCategory(ExtensionCategoryItem? item)
        {
            if (item == null) return;

            item.ResetToDefault();
            ExtensionSettingsService.Instance.ResetCategory(item.CategoryName);

            StatusMessage = $"'{item.DisplayName}' 카테고리가 기본 확장자로 복원되었습니다.";
            StatusForeground = new SolidColorBrush(Color.FromRgb(0, 120, 212)); // Blue
        }

        private void ResetAll()
        {
            var result = MessageBox.Show(
                "모든 미디어 카테고리의 확장자를 시스템 기본값으로 복원하시겠습니까?\n기존에 수정한 커스텀 확장자 목록은 초기화됩니다.",
                "전체 기본값 복원 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                foreach (var item in Categories)
                {
                    item.ResetToDefault();
                }

                ExtensionSettingsService.Instance.ResetAll();

                StatusMessage = "모든 카테고리가 시스템 기본 확장자로 일괄 복원되었습니다.";
                StatusForeground = new SolidColorBrush(Color.FromRgb(0, 120, 212)); // Blue
            }
        }
    }
}
