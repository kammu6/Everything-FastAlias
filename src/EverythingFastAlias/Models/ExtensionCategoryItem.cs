using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EverythingFastAlias.Config;

namespace EverythingFastAlias.Models
{
    public class ExtensionCategoryItem : ObservableObject
    {
        public string CategoryName { get; }
        public string DisplayName { get; }
        public string DefaultExtensions { get; set; } = string.Empty;

        private string _extensions = string.Empty;
        public string Extensions
        {
            get => _extensions;
            set
            {
                if (SetProperty(ref _extensions, value))
                {
                    OnPropertyChanged(nameof(IsModified));
                }
            }
        }

        public bool IsModified => !string.Equals(Extensions?.Trim(), DefaultExtensions?.Trim(), StringComparison.OrdinalIgnoreCase);

        public ICommand ResetCommand { get; }

        public ExtensionCategoryItem(string categoryName, string currentExtensions)
        {
            CategoryName = categoryName;
            DisplayName = GetCategoryDisplayName(categoryName);
            DefaultExtensions = FileExtensionConstants.GetDefaultExtensions(categoryName);
            _extensions = string.IsNullOrWhiteSpace(currentExtensions) ? DefaultExtensions : currentExtensions;

            ResetCommand = new RelayCommand(ResetToDefault);
        }

        public void ResetToDefault()
        {
            Extensions = DefaultExtensions;
        }

        public static string GetCategoryDisplayName(string category)
        {
            return category switch
            {
                FileExtensionConstants.CategoryVideo => "영상 (Video)",
                FileExtensionConstants.CategoryAudio => "음악 (Audio)",
                FileExtensionConstants.CategoryPicture => "사진 (Picture / Image)",
                FileExtensionConstants.CategoryDocument => "문서 (Document)",
                FileExtensionConstants.CategoryCode => "코드 (Code / Script)",
                FileExtensionConstants.CategoryExecutable => "실행 (Executable / App)",
                FileExtensionConstants.CategoryArchive => "압축 (Archive / Compressed)",
                _ => category
            };
        }
    }
}
