using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EverythingFastAlias.Native
{
    public static class ShellIconHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0; // 32x32 아이콘
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static ImageSource? _folderIcon;
        private static ImageSource? _fileIcon;

        public static ImageSource FolderIcon => _folderIcon ??= GetDefaultIcon(null, true);
        public static ImageSource FileIcon => _fileIcon ??= GetDefaultIcon(null, false);

        private static ImageSource GetDefaultIcon(string? path, bool isFolder)
        {
            SHFILEINFO shinfo = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES;
            uint attribs = isFolder ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
            
            string targetPath = path ?? (isFolder ? "folder" : "file.txt");

            SHGetFileInfo(targetPath, attribs, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);

            if (shinfo.hIcon == IntPtr.Zero)
            {
                // 실패 시 투명 비트맵 등으로 폴백
                return BitmapSource.Create(1, 1, 96, 96, PixelFormats.Pbgra32, null, new byte[4], 4);
            }

            try
            {
                ImageSource img = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    shinfo.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                // WPF 이미지 렌더링에 적절하게 Freeze 적용해 줍니다. (스레드 간 안전 공유 목적)
                img.Freeze();
                return img;
            }
            finally
            {
                DestroyIcon(shinfo.hIcon);
            }
        }
    }
}
