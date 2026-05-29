namespace EverythingFastAlias.Config
{
    public static class AppConstants
    {
        public static class DllConfig
        {
            // DefaultMaxResults = 0xFFFFFFFF 은 Everything SDK에서 무제한(EVERYTHING_MAX_ALL) 조회를 뜻함
            public const uint DefaultMaxResults = 0xFFFFFFFF;
            public const string RecycleBinPath = "!$Recycle.Bin";
        }

        public static class GlobalShortcuts
        {
            // 추후 글로벌 단축키로 활용할 키 정의 (메인 윈도우 보이기/숨기기 토글)
            public const string ShowMainWindow = "Ctrl+Alt+F";
        }
    }
}
