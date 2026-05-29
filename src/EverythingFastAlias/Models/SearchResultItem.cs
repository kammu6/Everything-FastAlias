using System;
using System.IO;

namespace EverythingFastAlias.Models
{
    public class SearchResultItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        
        public string FullPath => System.IO.Path.Combine(Path, Name);
        
        public long Size { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string Extension { get; set; } = string.Empty;
        public bool IsFolder { get; set; }

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
