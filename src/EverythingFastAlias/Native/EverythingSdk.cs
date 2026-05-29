using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace EverythingFastAlias.Native
{
    public static class EverythingSdk
    {
        private const string DllName = "everything64.dll";

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        static EverythingSdk()
        {
            // DLL 동적 로딩 시도 (DllImport 이전에 실행)
            TryLoadEverythingDll();
        }

        private static void TryLoadEverythingDll()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // 1. Assets/dll/Everything64.dll 로드 시도 (개발 및 빌드 폴더)
            var targetPath = Path.Combine(baseDir, "Assets", "dll", "Everything64.dll");
            if (File.Exists(targetPath))
            {
                LoadLibrary(targetPath);
                return;
            }

            // 2. BaseDirectory의 Everything64.dll 로드 시도
            targetPath = Path.Combine(baseDir, "Everything64.dll");
            if (File.Exists(targetPath))
            {
                LoadLibrary(targetPath);
                return;
            }

            // 3. 기본 Windows PATH 탐색에 의존 (기본 DllImport 동작)
        }

        public const uint EVERYTHING_REQUEST_FILE_NAME = 0x00000001;
        public const uint EVERYTHING_REQUEST_PATH = 0x00000002;
        public const uint EVERYTHING_REQUEST_SIZE = 0x00000010;
        public const uint EVERYTHING_REQUEST_DATE_MODIFIED = 0x00000040;
        public const uint EVERYTHING_REQUEST_EXTENSION = 0x00000100;

        [DllImport(DllName)]
        public static extern void Everything_SetRequestFlags(uint dwRequestFlags);

        [DllImport(DllName, CharSet = CharSet.Unicode)]
        public static extern void Everything_SetSearchW(string lpSearchString);

        [DllImport(DllName)]
        public static extern void Everything_SetMatchCase(bool bEnable);

        [DllImport(DllName)]
        public static extern void Everything_SetMatchWholeWord(bool bEnable);

        [DllImport(DllName)]
        public static extern void Everything_SetRegex(bool bEnable);

        [DllImport(DllName)]
        public static extern void Everything_SetMax(uint dwMax);

        [DllImport(DllName)]
        public static extern void Everything_SetOffset(uint dwOffset);

        [DllImport(DllName)]
        public static extern bool Everything_QueryW(bool bWait);

        [DllImport(DllName)]
        public static extern uint Everything_GetNumResults();

        [DllImport(DllName)]
        public static extern uint Everything_GetResultListRequestFlags();

        [DllImport(DllName, CharSet = CharSet.Unicode)]
        private static extern IntPtr Everything_GetResultFileNameW(uint nIndex);

        [DllImport(DllName, CharSet = CharSet.Unicode)]
        private static extern IntPtr Everything_GetResultPathW(uint nIndex);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Everything_GetResultSize(uint nIndex, out long lpSize);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Everything_GetResultDateModified(uint nIndex, out long lpFileTime);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Everything_IsFolderResult(uint nIndex);

        [DllImport(DllName)]
        public static extern uint Everything_GetLastError();

        // wchar_t* 리턴값을 안전하게 C# string으로 마샬링하는 헬퍼 함수
        public static string GetResultFileName(uint nIndex)
        {
            var ptr = Everything_GetResultFileNameW(nIndex);
            return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUni(ptr) ?? string.Empty;
        }

        public static string GetResultPath(uint nIndex)
        {
            var ptr = Everything_GetResultPathW(nIndex);
            return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUni(ptr) ?? string.Empty;
        }
    }
}
