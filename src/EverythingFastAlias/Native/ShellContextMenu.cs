using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using System.Windows.Interop;

namespace EverythingFastAlias.Native
{
    public class ShellContextMenu
    {
        #region Win32 API 및 COM 인터페이스 정의

        private const uint CMD_FIRST = 1;
        private const uint CMD_LAST = 30000;

        private const uint CMF_NORMAL = 0x00000000;
        private const uint CMF_EXPLORER = 0x00000004;

        private const uint TPM_LEFTALIGN = 0x0000;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_RIGHTBUTTON = 0x0002;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetDesktopFolder(out IShellFolder ppshf);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHParseDisplayName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszName,
            IntPtr pbc,
            out IntPtr ppidl,
            uint sfgaoIn,
            out uint psfgaoOut);

        [DllImport("shell32.dll")]
        private static extern int SHBindToParent(IntPtr pidl, [In] ref Guid riid, out IShellFolder ppv, out IntPtr ppidlLast);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [ComImport]
        [Guid("000214E6-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellFolder
        {
            [PreserveSig]
            int ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, ref uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
            
            [PreserveSig]
            int EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);
            
            [PreserveSig]
            int BindToObject(IntPtr pidl, IntPtr pbc, [In] ref Guid riid, out IntPtr ppv);
            
            [PreserveSig]
            int BindToStorage(IntPtr pidl, IntPtr pbc, [In] ref Guid riid, out IntPtr ppv);
            
            [PreserveSig]
            int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
            
            [PreserveSig]
            int CreateViewObject(IntPtr hwndOwner, [In] ref Guid riid, out IntPtr ppv);
            
            [PreserveSig]
            int GetAttributesOf(uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref uint rgfInOut);
            
            [PreserveSig]
            int GetUIObjectOf(IntPtr hwndOwner, uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, [In] ref Guid riid, ref uint rgfInOut, out IntPtr ppv);
            
            [PreserveSig]
            int GetDisplayNameOf(IntPtr pidl, uint uFlags, out IntPtr pName);
            
            [PreserveSig]
            int SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
        }

        [ComImport]
        [Guid("000214E4-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IContextMenu
        {
            [PreserveSig]
            int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
            
            [PreserveSig]
            int InvokeCommand(ref CMINVOKECOMMANDINFO pici);
            
            [PreserveSig]
            int GetCommandString(IntPtr idCmd, uint uFlags, ref uint pwReserved, [MarshalAs(UnmanagedType.LPStr)] string pszName, uint cchMax);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct CMINVOKECOMMANDINFO
        {
            public int cbSize;
            public int fMask;
            public IntPtr hwnd;
            public IntPtr lpVerb;
            public string lpParameters;
            public string lpDirectory;
            public int nShow;
            public int dwHotKey;
            public IntPtr hIcon;
        }

        #endregion

        public static void ShowContextMenu(Window parentWindow, List<string> filePaths)
        {
            if (filePaths == null || filePaths.Count == 0) return;

            // 부모 윈도우의 HWND 핸들 추출
            var helper = new WindowInteropHelper(parentWindow);
            IntPtr hwndOwner = helper.Handle;

            if (hwndOwner == IntPtr.Zero) return;

            // 마우스 커서 위치 획득
            if (!GetCursorPos(out POINT pt)) return;

            List<IntPtr> pidlList = new();
            IShellFolder? parentFolder = null;
            IntPtr[]? relativePidls = null;

            try
            {
                // 다중 파일 선택 시, 모두 동일한 부모 폴더 아래에 속해있어야 IContextMenu에 바인딩 가능함
                // Everything 결과물 상 다른 폴더의 파일들이 섞여있다면 첫 번째 파일의 부모 폴더 기준으로 바인딩
                var firstPath = filePaths[0];
                var parentDirPath = Path.GetDirectoryName(firstPath);
                if (string.IsNullOrEmpty(parentDirPath)) return;

                // 1. 데스크톱 폴더 IShellFolder 획득
                int hr = SHGetDesktopFolder(out IShellFolder desktopFolder);
                if (hr != 0 || desktopFolder == null) return;

                // 2. 부모 폴더에 대한 PIDL 解析
                uint eaten0 = 0;
                uint attrib0 = 0;
                hr = desktopFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, parentDirPath, ref eaten0, out IntPtr parentPidl, ref attrib0);
                if (hr != 0 || parentPidl == IntPtr.Zero)
                {
                    Marshal.ReleaseComObject(desktopFolder);
                    return;
                }

                // 3. 부모 폴더를 가리키는 IShellFolder 획득
                Guid guidShellFolder = typeof(IShellFolder).GUID;
                hr = desktopFolder.BindToObject(parentPidl, IntPtr.Zero, ref guidShellFolder, out IntPtr parentFolderPtr);
                Marshal.FreeCoTaskMem(parentPidl);
                Marshal.ReleaseComObject(desktopFolder);

                if (hr != 0 || parentFolderPtr == IntPtr.Zero) return;
                parentFolder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(parentFolderPtr, typeof(IShellFolder));

                // 4. 각 파일의 상대 PIDL 목록 구축
                relativePidls = new IntPtr[filePaths.Count];
                for (int i = 0; i < filePaths.Count; i++)
                {
                    var fileName = Path.GetFileName(filePaths[i]);
                    uint eaten = 0;
                    uint attrib = 0;
                    hr = parentFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, fileName, ref eaten, out IntPtr filePidl, ref attrib);
                    if (hr == 0 && filePidl != IntPtr.Zero)
                    {
                        relativePidls[i] = filePidl;
                        pidlList.Add(filePidl);
                    }
                }

                if (pidlList.Count == 0) return;

                // 5. IContextMenu 인터페이스 포인터 획득
                Guid guidContextMenu = typeof(IContextMenu).GUID;
                uint reserved = 0;
                hr = parentFolder.GetUIObjectOf(
                    hwndOwner,
                    (uint)pidlList.Count,
                    relativePidls,
                    ref guidContextMenu,
                    ref reserved,
                    out IntPtr contextMenuPtr
                );

                if (hr != 0 || contextMenuPtr == IntPtr.Zero) return;

                // 6. 포인터에서 인터페이스 객체로 마샬링
                IContextMenu contextMenu = (IContextMenu)Marshal.GetTypedObjectForIUnknown(contextMenuPtr, typeof(IContextMenu));

                // 5. 팝업 메뉴 생성 및 아이템 쿼리
                IntPtr hMenu = CreatePopupMenu();
                if (hMenu == IntPtr.Zero) return;

                // CMF_EXPLORER 플래그로 윈도우 탐색기 전용 스타일 메뉴 쿼리
                contextMenu.QueryContextMenu(hMenu, 0, CMD_FIRST, CMD_LAST, CMF_EXPLORER | CMF_NORMAL);

                // 6. 메뉴 전시 및 선택된 명령 ID 획득
                uint selectedCmd = TrackPopupMenuEx(
                    hMenu,
                    TPM_LEFTALIGN | TPM_RIGHTBUTTON | TPM_RETURNCMD,
                    pt.X,
                    pt.Y,
                    hwndOwner,
                    IntPtr.Zero
                );

                // 7. 명령 수행 (CMD_FIRST 이상의 ID가 선택된 경우)
                if (selectedCmd >= CMD_FIRST && selectedCmd <= CMD_LAST)
                {
                    var pici = new CMINVOKECOMMANDINFO
                    {
                        cbSize = Marshal.SizeOf(typeof(CMINVOKECOMMANDINFO)),
                        hwnd = hwndOwner,
                        lpVerb = (IntPtr)(selectedCmd - CMD_FIRST),
                        nShow = 1 // SW_SHOWNORMAL
                    };
                    contextMenu.InvokeCommand(ref pici);
                }

                // 8. 정리
                DestroyMenu(hMenu);
                Marshal.Release(contextMenuPtr);
            }
            catch
            {
                // 예외 발생 시 무음 처리하여 크래시 방지
            }
            finally
            {
                // PIDL 할당 해제
                foreach (var pidl in pidlList)
                {
                    if (pidl != IntPtr.Zero)
                    {
                        Marshal.FreeCoTaskMem(pidl);
                    }
                }
                if (parentFolder != null)
                {
                    Marshal.ReleaseComObject(parentFolder);
                }
            }
        }
    }
}
