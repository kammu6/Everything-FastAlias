using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EverythingFastAlias.Native
{
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItemImageFactory
    {
        void GetImage(
            [In, MarshalAs(UnmanagedType.Struct)] SIZE size,
            [In] SIIGBF flags,
            [Out] out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SIZE
    {
        public int cx;
        public int cy;
        public SIZE(int w, int h) { cx = w; cy = h; }
    }

    [Flags]
    internal enum SIIGBF
    {
        SIIGBF_RESIZETOFIT = 0x00,
        SIIGBF_BIGGERSIZEOK = 0x01,
        SIIGBF_MEMORYONLY = 0x02,
        SIIGBF_ICONONLY = 0x04,
        SIIGBF_THUMBNAILONLY = 0x08,
        SIIGBF_INCACHEONLY = 0x10
    }

    public static class ShellThumbnailHelper
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [Out, MarshalAs(UnmanagedType.Interface)] out object ppv);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        public static ImageSource? GetThumbnail(string filePath, int size)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return null;

            try
            {
                Guid iid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
                SHCreateItemFromParsingName(filePath, IntPtr.Zero, iid, out object shellItem);
                
                if (shellItem is IShellItemImageFactory factory)
                {
                    IntPtr hBitmap = IntPtr.Zero;
                    // SIIGBF_RESIZETOFIT를 주어 원하는 크기로 맞추어 가져옵니다.
                    factory.GetImage(new SIZE(size, size), SIIGBF.SIIGBF_RESIZETOFIT, out hBitmap);

                    if (hBitmap != IntPtr.Zero)
                    {
                        try
                        {
                            ImageSource img = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                hBitmap,
                                IntPtr.Zero,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());

                            img.Freeze(); // 크로스 스레드 안전성 보장
                            return img;
                        }
                        finally
                        {
                            DeleteObject(hBitmap); // GDI 메모리 누수 방지
                        }
                    }
                }
            }
            catch
            {
                // 디렉토리가 없거나, 썸네일 지원하지 않거나, 파일 접근이 불가능할 경우 등
            }

            return null;
        }
    }
}
