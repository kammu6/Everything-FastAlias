using System.Collections.Generic;

namespace EverythingFastAlias.Models
{
    public enum SearchScope
    {
        All,
        FileOnly,
        FolderOnly
    }

    public enum SizeUnit
    {
        KB,
        MB,
        GB
    }

    public class SearchOptions
    {
        // FastAlias 기능 On/Off
        public bool UseFastAlias { get; set; } = true;

        // 대소문자 구분
        public bool MatchCase { get; set; } = false;

        // 전체 단어 일치
        public bool MatchWholeWord { get; set; } = false;

        // 정규식 사용
        public bool UseRegex { get; set; } = false;

        // 휴지통 포함 여부
        public bool IncludeRecycleBin { get; set; } = false;

        // 탐색 대상 범위 (전체 / 파일만 / 폴더만)
        public SearchScope Scope { get; set; } = SearchScope.FileOnly;

        // 프리셋 미디어 필터 (영상, 음악, 사진, 문서, 코드, 실행, 압축 등 다중 선택)
        public HashSet<string> MediaPresets { get; set; } = new();

        // 커스텀 확장자 필드 (예: zip;7z;rar)
        public string CustomExtensions { get; set; } = string.Empty;

        // 단어 제외 필드 (최종 쿼리에서 !<단어> 자동 제외)
        public string ExcludedWords { get; set; } = string.Empty;

        // 폴더 경로 입력창 (특정 경로 내 탐색)
        public string FolderPaths { get; set; } = string.Empty;

        // 하위 폴더 재귀 포함 여부
        public bool RecursiveSearch { get; set; } = true;

        // 파일 크기 필터
        public long? MinSize { get; set; }
        public SizeUnit MinSizeUnit { get; set; } = SizeUnit.MB;

        public long? MaxSize { get; set; }
        public SizeUnit MaxSizeUnit { get; set; } = SizeUnit.MB;
    }
}
