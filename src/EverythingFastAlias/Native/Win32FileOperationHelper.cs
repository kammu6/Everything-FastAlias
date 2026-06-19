using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace EverythingFastAlias.Native
{
    public static class Win32FileOperationHelper
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

        private const int FO_MOVE = 0x0001;
        private const int FO_COPY = 0x0002;

        private const int FOF_ALLOWUNDO = 0x0040;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

        /// <summary>
        /// 파일 목록을 대상 폴더로 복사하거나 이동합니다. (윈도우 탐색기 표준 진행 및 대화상자 노출)
        /// </summary>
        /// <param name="sourcePaths">복사/이동할 원본 파일 목록</param>
        /// <param name="targetFolderPath">대상 폴더 경로</param>
        /// <param name="isMove">true이면 이동, false이면 복사</param>
        /// <returns>작업 성공 여부</returns>
        public static bool CopyOrMoveFiles(IEnumerable<string> sourcePaths, string targetFolderPath, bool isMove)
        {
            try
            {
                // pFrom: Double-null terminated list of paths
                var sbFrom = new StringBuilder();
                foreach (var path in sourcePaths)
                {
                    sbFrom.Append(path).Append('\0');
                }
                sbFrom.Append('\0');

                // pTo: Target folder must also be double-null terminated
                var sbTo = new StringBuilder();
                sbTo.Append(targetFolderPath).Append('\0').Append('\0');

                var fileop = new SHFILEOPSTRUCT
                {
                    wFunc = isMove ? FO_MOVE : FO_COPY,
                    pFrom = sbFrom.ToString(),
                    pTo = sbTo.ToString(),
                    fFlags = FOF_ALLOWUNDO // Ctrl+Z 되돌리기 지원
                };

                int result = SHFileOperation(ref fileop);
                return result == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
