using CommunityToolkit.Mvvm.ComponentModel;

namespace EverythingFastAlias.Models
{
    public class DriveOptionItem : ObservableObject
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private bool _isChecked = true;
        public bool IsChecked
        {
            get => _isChecked;
            set => SetProperty(ref _isChecked, value);
        }
    }
}
