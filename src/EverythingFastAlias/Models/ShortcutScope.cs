namespace EverythingFastAlias.Models
{
    /// <summary>
    /// 단축키가 활성화되는 실행 스코프
    /// </summary>
    public enum ShortcutScope
    {
        /// <summary>
        /// 애플리케이션 어디서나 작동하는 전역 스코프
        /// </summary>
        Global,

        /// <summary>
        /// 검색 결과 목록에 포커스가 있거나 항목이 선택되었을 때만 작동하는 스코프
        /// </summary>
        ResultGrid
    }
}
