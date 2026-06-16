using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using EverythingFastAlias.Native;

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

        // ── 썸네일 지연 로드 및 캐시 ──
        private static readonly SemaphoreSlim _thumbnailSemaphore = new SemaphoreSlim(4);
        private ImageSource? _thumbnail;
        private bool _thumbnailLoadingStarted;

        public ImageSource? Thumbnail
        {
            get
            {
                if (_thumbnail == null && !_thumbnailLoadingStarted)
                {
                    _thumbnailLoadingStarted = true;
                    LoadThumbnailAsync();
                }
                return _thumbnail ?? (IsFolder ? ShellIconHelper.FolderIcon : ShellIconHelper.FileIcon);
            }
            private set
            {
                _thumbnail = value;
                Notify(nameof(Thumbnail));
            }
        }

        public void ResetThumbnail()
        {
            _thumbnail = null;
            _thumbnailLoadingStarted = false;
            Notify(nameof(Thumbnail));
        }

        private void LoadThumbnailAsync()
        {
            string path = FullPath;
            bool isFolder = IsFolder;

            Task.Run(async () =>
            {
                await _thumbnailSemaphore.WaitAsync();
                try
                {
                    ImageSource? img = null;
                    if (File.Exists(path) || Directory.Exists(path))
                    {
                        // 썸네일은 최대 256 크기로 균일하게 긁어옵니다. (디스크 I/O 최적화 및 고화질 보장)
                        img = ShellThumbnailHelper.GetThumbnail(path, 256);
                    }

                    if (img == null)
                    {
                        img = isFolder ? ShellIconHelper.FolderIcon : ShellIconHelper.FileIcon;
                    }

                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        Thumbnail = img;
                    }));
                }
                catch
                {
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        Thumbnail = isFolder ? ShellIconHelper.FolderIcon : ShellIconHelper.FileIcon;
                    }));
                }
                finally
                {
                    _thumbnailSemaphore.Release();
                }
            });
        }
    }
}
