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

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags, IntPtr uIDNewItem, string? lpNewItem);

        private const uint MF_BYPOSITION = 0x00000400;
        private const uint MF_STRING = 0x00000000;
        private const uint MF_SEPARATOR = 0x00000800;

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

        [ComImport]
        [Guid("000214F4-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IContextMenu2 : IContextMenu
        {
            [PreserveSig]
            new int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
            [PreserveSig]
            new int InvokeCommand(ref CMINVOKECOMMANDINFO pici);
            [PreserveSig]
            new int GetCommandString(IntPtr idCmd, uint uFlags, ref uint pwReserved, [MarshalAs(UnmanagedType.LPStr)] string pszName, uint cchMax);

            [PreserveSig]
            int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
        }

        [ComImport]
        [Guid("30F105C9-681E-11D0-A83B-00A0C9054129")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IContextMenu3 : IContextMenu2
        {
            [PreserveSig]
            new int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
            [PreserveSig]
            new int InvokeCommand(ref CMINVOKECOMMANDINFO pici);
            [PreserveSig]
            new int GetCommandString(IntPtr idCmd, uint uFlags, ref uint pwReserved, [MarshalAs(UnmanagedType.LPStr)] string pszName, uint cchMax);
            [PreserveSig]
            new int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);

            [PreserveSig]
            int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);
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

        private const int WM_INITMENUPOPUP = 0x0117;
        private const int WM_DRAWITEM = 0x002B;
        private const int WM_MEASUREITEM = 0x002C;
        private const int WM_MENUCHAR = 0x0120;

        #endregion

        private static IContextMenu? _contextMenu;
        private static IContextMenu2? _contextMenu2;
        private static IContextMenu3? _contextMenu3;

        private static IntPtr HookWindowMessages(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_INITMENUPOPUP:
                case WM_DRAWITEM:
                case WM_MEASUREITEM:
                case WM_MENUCHAR:
                    if (_contextMenu3 != null)
                    {
                        int hr = _contextMenu3.HandleMenuMsg2((uint)msg, wParam, lParam, out IntPtr lResult);
                        if (hr >= 0) // S_OK
                        {
                            handled = true;
                            return lResult;
                        }
                    }
                    else if (_contextMenu2 != null)
                    {
                        int hr = _contextMenu2.HandleMenuMsg((uint)msg, wParam, lParam);
                        if (hr >= 0) // S_OK
                        {
                            handled = true;
                            return IntPtr.Zero;
                        }
                    }
                    break;
            }
            return IntPtr.Zero;
        }

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

                // 서브메뉴 소유자 그리기(Owner Draw) 처리를 위해 IContextMenu2/3 캐스팅
                _contextMenu = contextMenu;
                _contextMenu2 = contextMenu as IContextMenu2;
                _contextMenu3 = contextMenu as IContextMenu3;

                // 7. 팝업 메뉴 생성 및 아이템 쿼리
                IntPtr hMenu = CreatePopupMenu();
                if (hMenu == IntPtr.Zero) return;

                // CMF_EXPLORER 플래그로 윈도우 탐색기 전용 스타일 메뉴 쿼리
                contextMenu.QueryContextMenu(hMenu, 0, CMD_FIRST, CMD_LAST, CMF_EXPLORER | CMF_NORMAL);

                // 커스텀 메뉴 항목을 메뉴의 최상단(위치 0부터)에 삽입
                uint CUSTOM_CMD_OPEN = CMD_LAST + 1;
                uint CUSTOM_CMD_OPEN_PATH = CMD_LAST + 2;
                uint CUSTOM_CMD_COPY_PATH = CMD_LAST + 3;

                InsertMenu(hMenu, 0, MF_BYPOSITION | MF_STRING, (IntPtr)CUSTOM_CMD_OPEN, "열기(O)");
                InsertMenu(hMenu, 1, MF_BYPOSITION | MF_STRING, (IntPtr)CUSTOM_CMD_OPEN_PATH, "경로 열기");
                InsertMenu(hMenu, 2, MF_BYPOSITION | MF_STRING, (IntPtr)CUSTOM_CMD_COPY_PATH, "전체 경로를 클립보드에 복사(F)");
                InsertMenu(hMenu, 3, MF_BYPOSITION | MF_SEPARATOR, IntPtr.Zero, null);

                // WPF HwndSource 메시지 후크 연결 (TrackPopupMenuEx 도중 발생하는 메시지 위임)
                HwndSource hwndSource = HwndSource.FromHwnd(hwndOwner);
                HwndSourceHook hook = HookWindowMessages;
                hwndSource.AddHook(hook);

                // 8. 메뉴 전시 및 선택된 명령 ID 획득
                uint selectedCmd = TrackPopupMenuEx(
                    hMenu,
                    TPM_LEFTALIGN | TPM_RIGHTBUTTON | TPM_RETURNCMD,
                    pt.X,
                    pt.Y,
                    hwndOwner,
                    IntPtr.Zero
                );

                // 메시지 후크 즉시 해제
                hwndSource.RemoveHook(hook);
                _contextMenu = null;
                _contextMenu2 = null;
                _contextMenu3 = null;

                // 9. 명령 수행 (CMD_FIRST 이상의 ID가 선택된 경우)
                if (selectedCmd == CUSTOM_CMD_OPEN)
                {
                    foreach (var path in filePaths)
                    {
                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
                    }
                }
                else if (selectedCmd == CUSTOM_CMD_OPEN_PATH)
                {
                    foreach (var path in filePaths)
                    {
                        try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\""); } catch { }
                    }
                }
                else if (selectedCmd == CUSTOM_CMD_COPY_PATH)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var path in filePaths)
                    {
                        sb.AppendLine(path);
                    }
                    try { Clipboard.SetText(sb.ToString().TrimEnd()); } catch { }
                }
                else if (selectedCmd >= CMD_FIRST && selectedCmd <= CMD_LAST)
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

                // 10. 정리
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
