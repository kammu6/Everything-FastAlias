using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace EverythingFastAlias.Native
{
    public static class Win32RecycleBinHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.U4)]
            public int wFunc;
            public string pFrom;
            public string pTo;
            public short fFlags;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        private const int FO_DELETE = 0x0003;
        private const int FOF_ALLOWUNDO = 0x0040;
        private const int FOF_NOCONFIRMATION = 0x0010;
        private const int FOF_NOERRORUI = 0x0400;
        private const int FOF_SILENT = 0x0004;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

        /// <summary>
        /// 지정한 파일/폴더 경로 목록을 휴지통으로 이동시킵니다. (경고창 없음)
        /// </summary>
        /// <param name="paths">삭제할 경로 목록</param>
        /// <returns>삭제 작업 성공 여부</returns>
        public static bool SendToRecycleBin(IEnumerable<string> paths)
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var path in paths)
                {
                    sb.Append(path).Append('\0');
                }
                sb.Append('\0'); // Double-null termination

                var fileop = new SHFILEOPSTRUCT
                {
                    wFunc = FO_DELETE,
                    pFrom = sb.ToString(),
                    fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
                };

                int result = SHFileOperation(ref fileop);
                return result == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 지정한 파일/폴더 경로 목록을 휴지통을 거치지 않고 완전히 영구 삭제합니다. (시스템 확인 대화상자 노출)
        /// </summary>
        /// <param name="paths">삭제할 경로 목록</param>
        /// <param name="hwnd">부모 윈도우 핸들</param>
        /// <returns>삭제 작업 성공 여부 (사용자가 확인 창에서 '아니오'를 누르면 false)</returns>
        public static bool DeletePermanently(IEnumerable<string> paths, IntPtr hwnd = default)
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var path in paths)
                {
                    sb.Append(path).Append('\0');
                }
                sb.Append('\0'); // Double-null termination

                var fileop = new SHFILEOPSTRUCT
                {
                    hwnd = hwnd,
                    wFunc = FO_DELETE,
                    pFrom = sb.ToString(),
                    fFlags = 0 // FOF_ALLOWUNDO 없음 -> 영구 삭제 (시스템 확인창 팝업)
                };

                int result = SHFileOperation(ref fileop);
                return result == 0 && !fileop.fAnyOperationsAborted;
            }
            catch
            {
                return false;
            }
        }
    }
}
