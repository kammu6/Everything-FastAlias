namespace EverythingFastAlias.Models
{
    /// <summary>
    /// 애플리케이션 내에서 실행 가능한 단축키 액션 식별자
    /// </summary>
    public enum ShortcutAction
    {
        // ── 전역 (Global) 액션 ──
        ShowHelp,               // 도움말 창 호출 (기본: F1)
        Refresh,                // 검색 새로고침 (기본: F5)
        FocusSearch,            // 검색 입력창 포커스 (기본: Ctrl+F)
        NewWindow,              // 새 창 열기 (기본: Ctrl+N)
        ExportResults,          // 검색 결과 내보내기 (기본: Ctrl+E)
        ToggleSidebar,          // 좌측 옵션 패널 토글 (기본: Ctrl+B)
        OpenAliasManager,       // 매핑 테이블 관리자 (기본: Ctrl+M)
        OpenExtensionManager,   // 미디어 확장자 관리자 (기본: Ctrl+Shift+E)

        // ── 검색 결과 목록 (ResultGrid) 액션 ──
        ExecuteItem,            // 파일 실행 / 폴더 열기 (기본: Enter)
        ExplorePath,            // 상위 폴더 열기 및 위치 선택 (기본: Shift+Enter)
        ShowProperties,         // Windows 네이티브 속성창 (기본: Alt+Enter)
        RenameItem,             // 인라인 이름 변경 (기본: F2)
        CopyItem,               // 클립보드 파일 객체 복사 (기본: Ctrl+C)
        CutItem,                // 클립보드 파일 객체 잘라내기 (기본: Ctrl+X)
        CopyFileNames,          // 파일명(확장자 포함) 텍스트 복사 (기본: Ctrl+Shift+C)
        CopyFullPaths,          // 전체 절대경로 텍스트 복사 (기본: Ctrl+Shift+Alt+C)
        DeleteToRecycleBin,     // 휴지통으로 안전 삭제 (기본: Delete)
        PermanentDelete         // 영구 삭제 (기본: Shift+Delete)
    }
}
