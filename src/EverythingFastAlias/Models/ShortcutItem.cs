using System.Text;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EverythingFastAlias.Models
{
    /// <summary>
    /// 단일 단축키 정의 및 UI 데이터 바인딩을 지원하는 모델 클래스
    /// </summary>
    public class ShortcutItem : ObservableObject
    {
        public ShortcutAction Action { get; set; }
        public ShortcutScope Scope { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public Key DefaultKey { get; set; }
        public ModifierKeys DefaultModifiers { get; set; }

        private Key _key;
        public Key Key
        {
            get => _key;
            set
            {
                if (SetProperty(ref _key, value))
                {
                    OnPropertyChanged(nameof(DisplayGesture));
                    OnPropertyChanged(nameof(IsCustomized));
                }
            }
        }

        private ModifierKeys _modifiers;
        public ModifierKeys Modifiers
        {
            get => _modifiers;
            set
            {
                if (SetProperty(ref _modifiers, value))
                {
                    OnPropertyChanged(nameof(DisplayGesture));
                    OnPropertyChanged(nameof(IsCustomized));
                }
            }
        }

        /// <summary>
        /// 기본 단축키와 다른 사용자 지정 단축키인지 여부
        /// </summary>
        public bool IsCustomized => Key != DefaultKey || Modifiers != DefaultModifiers;

        /// <summary>
        /// 현재 설정된 단축키 문자열 표현 (예: "Shift + Enter", "F1", "Ctrl + C")
        /// </summary>
        public string DisplayGesture => FormatGesture(Key, Modifiers);

        /// <summary>
        /// 기본 단축키 문자열 표현
        /// </summary>
        public string DefaultDisplayGesture => FormatGesture(DefaultKey, DefaultModifiers);

        /// <summary>
        /// 입력된 키와 보조키가 현재 단축키와 일치하는지 판별합니다.
        /// </summary>
        public bool Matches(Key key, ModifierKeys modifiers)
        {
            return Key == key && Modifiers == modifiers;
        }

        /// <summary>
        /// 단축키를 초기 기본값으로 복원합니다.
        /// </summary>
        public void ResetToDefault()
        {
            Key = DefaultKey;
            Modifiers = DefaultModifiers;
        }

        /// <summary>
        /// Key 및 ModifierKeys를 사용자 친화적 텍스트로 서식화합니다.
        /// </summary>
        public static string FormatGesture(Key key, ModifierKeys modifiers)
        {
            if (key == Key.None) return "없음";

            var sb = new StringBuilder();
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                sb.Append("Ctrl + ");
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                sb.Append("Shift + ");
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                sb.Append("Alt + ");
            if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
                sb.Append("Win + ");

            sb.Append(FormatKeyName(key));
            return sb.ToString();
        }

        public static string FormatKeyName(Key key)
        {
            return key switch
            {
                Key.Return => "Enter",
                Key.Back => "Backspace",
                Key.Escape => "Esc",
                Key.D0 => "0",
                Key.D1 => "1",
                Key.D2 => "2",
                Key.D3 => "3",
                Key.D4 => "4",
                Key.D5 => "5",
                Key.D6 => "6",
                Key.D7 => "7",
                Key.D8 => "8",
                Key.D9 => "9",
                _ => key.ToString()
            };
        }
    }
}
