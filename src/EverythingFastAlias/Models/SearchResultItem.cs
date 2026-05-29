using System;
using System.ComponentModel;
using System.IO;

namespace EverythingFastAlias.Models
{
    public class SearchResultItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set { _name = value; Notify(nameof(Name)); Notify(nameof(FullPath)); }
        }

        public string Path { get; set; } = string.Empty;

        public string FullPath => System.IO.Path.Combine(Path, Name);

        public long Size { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string Extension { get; set; } = string.Empty;
        public bool IsFolder { get; set; }

        // ── F2 인라인 이름변경 지원 ──
        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set { _isEditing = value; Notify(nameof(IsEditing)); Notify(nameof(IsNotEditing)); }
        }
        public bool IsNotEditing => !_isEditing;

        private string _editingName = string.Empty;
        public string EditingName
        {
            get => _editingName;
            set { _editingName = value; Notify(nameof(EditingName)); }
        }

        public string DisplaySize
        {
            get
            {
                if (IsFolder) return "<DIR>";
                if (Size < 0) return string.Empty;

                string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
                int counter = 0;
                decimal number = Size;
                while (Math.Round(number / 1024) >= 1)
                {
                    number /= 1024;
                    counter++;
                }
                return $"{number:n1} {suffixes[counter]}";
            }
        }

        public string DisplayModifiedDate => ModifiedDate.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
