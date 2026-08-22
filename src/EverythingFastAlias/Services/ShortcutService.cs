using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using EverythingFastAlias.Models;

namespace EverythingFastAlias.Services
{
    /// <summary>
    /// 단축키 중앙 레지스트리, DB 영속성 및 키보드 이벤트 디스패치 서비스
    /// </summary>
    public class ShortcutService
    {
        private static readonly Lazy<ShortcutService> _instance = new(() => new ShortcutService());
        public static ShortcutService Instance => _instance.Value;

        public ObservableCollection<ShortcutItem> Shortcuts { get; } = new();

        private readonly Dictionary<(ShortcutScope Scope, Key Key, ModifierKeys Modifiers), ShortcutAction> _lookupMap = new();

        public event Action? ShortcutsChanged;

        private ShortcutService()
        {
            InitializeDefaultShortcuts();
            LoadCustomShortcuts();
            RebuildLookupMap();
        }

        /// <summary>
        /// 기본 단축키 목록을 등록합니다.
        /// </summary>
        private void InitializeDefaultShortcuts()
        {
            Shortcuts.Clear();

            // ── 1. 전역 (Global) 스코프 ──
            AddDefault(ShortcutAction.ShowHelp, ShortcutScope.Global, "도움말 창 열기", "일반 / 창", "도움말 및 검색 문법 가이드 창을 엽니다.", Key.F1, ModifierKeys.None);
            AddDefault(ShortcutAction.Refresh, ShortcutScope.Global, "검색 새로고침", "검색", "Everything 검색 엔진을 즉시 재조회합니다.", Key.F5, ModifierKeys.None);
            AddDefault(ShortcutAction.FocusSearch, ShortcutScope.Global, "검색창 포커스", "검색", "상단 검색어 입력창으로 커서를 이동합니다.", Key.F, ModifierKeys.Control);
            AddDefault(ShortcutAction.NewWindow, ShortcutScope.Global, "새 창 열기", "일반 / 창", "새로운 Everything FastAlias 창을 띄웁니다.", Key.N, ModifierKeys.Control);
            AddDefault(ShortcutAction.ExportResults, ShortcutScope.Global, "결과 내보내기", "일반 / 창", "현재 검색 결과를 텍스트 파일로 내보냅니다.", Key.E, ModifierKeys.Control);
            AddDefault(ShortcutAction.ToggleSidebar, ShortcutScope.Global, "옵션 패널 토글", "보기", "좌측 옵션 패널을 표시하거나 숨깁니다.", Key.B, ModifierKeys.Control);
            AddDefault(ShortcutAction.OpenAliasManager, ShortcutScope.Global, "매핑 테이블 관리자", "도구", "동의어 매핑 사전 관리 모달을 엽니다.", Key.M, ModifierKeys.Control);
            AddDefault(ShortcutAction.OpenExtensionManager, ShortcutScope.Global, "미디어 확장자 관리자", "도구", "미디어 파일 확장자 설정 모달을 엽니다.", Key.E, ModifierKeys.Control | ModifierKeys.Shift);

            // ── 2. 검색 결과 목록 (ResultGrid) 스코프 ──
            AddDefault(ShortcutAction.ExecuteItem, ShortcutScope.ResultGrid, "파일 실행 / 폴더 열기", "결과 항목", "선택한 파일을 기본 프로그램으로 실행하거나 폴더를 엽니다.", Key.Enter, ModifierKeys.None);
            AddDefault(ShortcutAction.ExplorePath, ShortcutScope.ResultGrid, "탐색기에서 파일 위치 열기", "결과 항목", "파일 탐색기를 열고 해당 파일을 선택(포커스)합니다.", Key.Enter, ModifierKeys.Shift);
            AddDefault(ShortcutAction.ShowProperties, ShortcutScope.ResultGrid, "파일 속성창 열기", "결과 항목", "Windows 네이티브 파일 속성 대화상자를 엽니다.", Key.Enter, ModifierKeys.Alt);
            AddDefault(ShortcutAction.RenameItem, ShortcutScope.ResultGrid, "인라인 이름 변경", "결과 항목", "선택한 파일/폴더의 이름을 즉시 변경합니다.", Key.F2, ModifierKeys.None);
            AddDefault(ShortcutAction.CopyItem, ShortcutScope.ResultGrid, "클립보드 파일 객체 복사", "결과 항목", "선택한 파일들을 Windows 파일 객체로 복사합니다.", Key.C, ModifierKeys.Control);
            AddDefault(ShortcutAction.CutItem, ShortcutScope.ResultGrid, "클립보드 파일 객체 잘라내기", "결과 항목", "선택한 파일들을 Windows 파일 객체로 잘라냅니다.", Key.X, ModifierKeys.Control);
            AddDefault(ShortcutAction.CopyFileNames, ShortcutScope.ResultGrid, "파일명 복사", "결과 항목", "선택한 파일들의 이름(확장자 포함)을 텍스트로 클립보드에 복사합니다. (다중 선택 시 줄바꿈)", Key.C, ModifierKeys.Control | ModifierKeys.Shift);
            AddDefault(ShortcutAction.CopyFullPaths, ShortcutScope.ResultGrid, "전체 절대경로 복사", "결과 항목", "선택한 파일들의 전체 절대 경로를 텍스트로 클립보드에 복사합니다. (다중 선택 시 줄바꿈)", Key.C, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt);
            AddDefault(ShortcutAction.DeleteToRecycleBin, ShortcutScope.ResultGrid, "휴지통으로 삭제", "결과 항목", "선택한 파일을 Windows 휴지통으로 안전하게 삭제합니다.", Key.Delete, ModifierKeys.None);
            AddDefault(ShortcutAction.PermanentDelete, ShortcutScope.ResultGrid, "영구 삭제", "결과 항목", "확인 대화상자 노출 후 파일을 완전히 영구 삭제합니다.", Key.Delete, ModifierKeys.Shift);
        }

        private void AddDefault(ShortcutAction action, ShortcutScope scope, string displayName, string category, string description, Key defaultKey, ModifierKeys defaultModifiers)
        {
            Shortcuts.Add(new ShortcutItem
            {
                Action = action,
                Scope = scope,
                DisplayName = displayName,
                Category = category,
                Description = description,
                DefaultKey = defaultKey,
                DefaultModifiers = defaultModifiers,
                Key = defaultKey,
                Modifiers = defaultModifiers
            });
        }

        /// <summary>
        /// SQLite DB(DatabaseService)로부터 사용자 커스텀 단축키 설정을 불러옵니다.
        /// </summary>
        public void LoadCustomShortcuts()
        {
            foreach (var item in Shortcuts)
            {
                string keyName = $"Shortcut_{item.Action}";
                string savedVal = DatabaseService.Instance.GetSetting(keyName, string.Empty);

                if (!string.IsNullOrEmpty(savedVal))
                {
                    // 저장 포맷: "{Modifiers}:{Key}" (예: "Control,Shift:E", "None:F1")
                    var parts = savedVal.Split(':');
                    if (parts.Length == 2 &&
                        Enum.TryParse(parts[0], out ModifierKeys mods) &&
                        Enum.TryParse(parts[1], out Key key))
                    {
                        item.Key = key;
                        item.Modifiers = mods;
                    }
                }
            }
        }

        /// <summary>
        /// 특정 단축키 항목의 변경 사항을 DB에 영구 저장합니다.
        /// </summary>
        public void SaveShortcut(ShortcutItem item)
        {
            string keyName = $"Shortcut_{item.Action}";
            if (item.IsCustomized)
            {
                string saveVal = $"{item.Modifiers}:{item.Key}";
                DatabaseService.Instance.SaveSetting(keyName, saveVal);
            }
            else
            {
                // 기본값인 경우 DB 설정 제거
                DatabaseService.Instance.SaveSetting(keyName, string.Empty);
            }

            RebuildLookupMap();
            ShortcutsChanged?.Invoke();
        }

        /// <summary>
        /// 모든 단축키를 초기 기본값으로 복원합니다.
        /// </summary>
        public void ResetAll()
        {
            foreach (var item in Shortcuts)
            {
                item.ResetToDefault();
                string keyName = $"Shortcut_{item.Action}";
                DatabaseService.Instance.SaveSetting(keyName, string.Empty);
            }

            RebuildLookupMap();
            ShortcutsChanged?.Invoke();
        }

        /// <summary>
        /// 특정 액션 단축키를 기본값으로 복원합니다.
        /// </summary>
        public void ResetAction(ShortcutAction action)
        {
            var item = GetItem(action);
            if (item != null)
            {
                item.ResetToDefault();
                SaveShortcut(item);
            }
        }

        /// <summary>
        /// O(1) 고속 매칭을 위한 룩업 맵을 재구축합니다.
        /// </summary>
        public void RebuildLookupMap()
        {
            _lookupMap.Clear();
            foreach (var item in Shortcuts)
            {
                if (item.Key != Key.None)
                {
                    _lookupMap[(item.Scope, item.Key, item.Modifiers)] = item.Action;
                }
            }
        }

        /// <summary>
        /// 키보드 이벤트로부터 현재 스코프에 일치하는 단축키 액션이 있는지 판별합니다.
        /// </summary>
        public bool TryGetAction(KeyEventArgs e, ShortcutScope scope, out ShortcutAction action)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            ModifierKeys modifiers = Keyboard.Modifiers;

            return _lookupMap.TryGetValue((scope, key, modifiers), out action);
        }

        /// <summary>
        /// 키보드 이벤트를 검사하여 일치하는 단축키가 있으면 실행 콜백을 호출하고 e.Handled = true를 설정합니다.
        /// </summary>
        public bool TryHandle(KeyEventArgs e, ShortcutScope scope, Action<ShortcutAction> executeCallback)
        {
            if (TryGetAction(e, scope, out var action))
            {
                e.Handled = true;
                executeCallback(action);
                return true;
            }
            return false;
        }

        public ShortcutItem? GetItem(ShortcutAction action)
        {
            return Shortcuts.FirstOrDefault(x => x.Action == action);
        }

        public string GetGestureText(ShortcutAction action)
        {
            var item = GetItem(action);
            return item?.DisplayGesture ?? string.Empty;
        }
    }
}
