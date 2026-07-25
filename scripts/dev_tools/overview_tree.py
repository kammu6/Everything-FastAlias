# description: "Universal 7-tier rule-based directory tree generator with smart annotation mapping (ljust-padded comments)"

import os
import re

# ==============================================================================
# 1. 경로 탐색 및 기본 설정
# ==============================================================================

def get_overview_path() -> str:
    """overview.md 파일의 절대 경로를 안전하게 탐색하여 반환합니다."""
    p1 = os.path.join("docs", "memories", "overview.md")
    if os.path.exists(p1):
        return p1
    script_dir = os.path.dirname(os.path.abspath(__file__))
    p2 = os.path.abspath(os.path.join(script_dir, "..", "..", "docs", "memories", "overview.md"))
    if os.path.exists(p2):
        return p2
    p3 = os.path.abspath(os.path.join(script_dir, "..", "docs", "memories", "overview.md"))
    if os.path.exists(p3):
        return p3
    return p1


OVERVIEW_PATH = get_overview_path()

# ==============================================================================
# 2. 7대 필터링 규칙 정의 (Rule-Based Filtering Hierarchy)
# ==============================================================================

# [1] 재귀적으로 모든 하위 폴더 및 파일 구조를 전부 전개하여 보여줄 폴더 목록
FULL_DIRS = {
    "src"
}

# [2] 트리 탐색 및 렌더링에서 완전히 제외할 폴더 목록 (재귀적 차단)
IGNORE_DIRS = {
    ".git",
    ".vs",
    ".vs-layout",
    "bin",
    "obj",
    "packages",
    "TestResults",
    "node_modules",
    ".venv",
    "__pycache__",
    "temp",
    ".agents",
    ".history",
    ".idea",
    ".vscode",
    "docs/archives",
    "docs/memories/backup",
    "docs/deepwiki",
    "dist",
    "dist-main",
    "dist-isolate",
    "out",
    "release",
    "build",
    "brain",
    "logs",
    "snapshots",
    "screenshots",
    "tavily",
    ".codegraph",
    "zip-tree",
    "tests"
}

# [3] 파일명 기준으로 숨길 파일 목록 (전역 적용)
IGNORE_FILES = {
    ".gitignore",
    ".gitkeep",
    "package-lock.json",
    "yarn.lock",
    "pnpm-lock.yaml",
    "poetry.lock",
    ".DS_Store",
    "Thumbs.db",
    "desktop.ini",
    "fastalias.db",
    "fastalias.db-journal",
    "fastalias.db-wal",
    "fastalias.db-shm",
    "project.lock.json",
    "project.assets.json"
}

# [4] 확장자 기준으로 숨길 확장자 목록 (점 표기 여부 무관)
IGNORE_EXTS = {
    "lnk",
    "tmp",
    "pyc",
    "blockmap",
    "suo",
    "swp",
    "user",
    "userosscache",
    "docstates",
    "vsidx",
    "dtbcache",
    "db",
    "db-journal",
    "db-wal",
    "db-shm",
    "sqlite",
    "log",
    "nupkg"
}

# [5] 비재귀적으로 직속(1단계) 파일 및 폴더들만 노출할 폴더 목록
SHOW_ALL = {
    "docs/memories"
}

# [6] 기본 파일 숨김 폴더 내라도 명시적으로 보여줄 개별 파일 경로 목록 (최우선)
SHOW_FILES = {
    # "scripts/dev_tools/memory_log.py",
}

# [7] IGNORE_DIRS 등으로 차단된 영역 내에서 명시적으로 보여줄 개별 폴더 목록 (비재귀)
SHOW_DIRS = {
    # "userdata_debug",
    # "userdata_debug/9222"
}

# 확장자 정규화 세트 (. 제거)
_NORMALIZED_IGNORE_EXTS = {ext.lstrip(".").lower() for ext in IGNORE_EXTS}

# ==============================================================================
# 2.5. 표준 기본 주석 정의 (Default Built-in Annotations)
# ==============================================================================
# 모든 세션의 에이전트 환경에 공통으로 설치되는 핵심 문서 및 표준 도구의 기본 주석입니다.
# 1순위 (최우선): overview.md에서 파싱된 사용자 수동 주석 (보존 원칙)
# 2순위: DEFAULT_ANNOTATIONS의 상대 경로 완전 일치 (docs/memories/MEMORY.md 등)
# 3순위: DEFAULT_ANNOTATIONS의 파일명/폴더명 단일 매칭 (MEMORY.md, overview.md 등)
DEFAULT_ANNOTATIONS = {
    # 1) 핵심 메모리 및 문서
    "docs/memories/MEMORY.md": "지식 자산화 로그",
    "docs/memories/overview.md": "본 개요 문서",
    "docs/memories/scripts_guide.md": "스크립트 가이드 및 색인 문서",
    "MEMORY.md": "지식 자산화 로그",
    "overview.md": "본 개요 문서",
    "scripts_guide.md": "스크립트 가이드 및 색인 문서",

    # 2) 표준 폴더
    "docs": "프로젝트 문서 보관 폴더",
    "docs/memories": "핵심 메모리 및 개요 보관 폴더",
    "scripts": "보조 분석 및 자동화 스크립트 폴더 (상세: docs/memories/scripts_guide.md)",
    "src": "메인 소스코드 폴더",

    # 3) EverythingFastAlias 프로젝트 핵심 컴포넌트
    "AppConstants.cs": "IPC 및 시스템 제어용 상수",
    "UIConstants.cs": "UI 크기 및 기본 핫키 설정",
    "BoolToVisibilityConverter.cs": "Bool → Visibility 전역 변환 서비스",
    "AliasMapping.cs": "MVVM 바인딩용 매핑 정보 모델",
    "SearchOptions.cs": "9가지 검색 조건 옵션 모델",
    "SearchResultItem.cs": "검색 행 데이터 모델 ( display size 및 날짜 자동 가공 )",
    "ViewMode.cs": "보기 모드(자세히/섬네일S/M/L) 설정을 위한 열거형",
    "EverythingBridge.cs": "Everything 엔진 상태 점검 및 검색 질의 래핑",
    "EverythingSdk.cs": "kernel32.dll LoadLibrary 기반 FFI 및 P/Invoke",
    "ShellContextMenu.cs": "COM 인터페이스 마샬링 기반 윈도우 네이티브 우클릭 메뉴 팝업",
    "ShellIconHelper.cs": "시스템 기본 폴더/파일 아이콘 캐시 헬퍼",
    "ShellThumbnailHelper.cs": "IShellItemImageFactory FFI 기반 썸네일 고화질 추출기",
    "TrayIconHelper.cs": "System.Windows.Forms.NotifyIcon 기반 시스템 트레이 아이콘 전담",
    "Win32ClipboardHelper.cs": "파일 클립보드 복사/잘라내기 네이티브 래퍼",
    "Win32FileOperationHelper.cs": "SHFileOperation FFI 기반 복사/이동 헬퍼",
    "Win32RecycleBinHelper.cs": "SHFileOperation FFI 기반 휴지통 삭제 헬퍼",
    "AutoStartService.cs": "시작프로그램 자동 실행 등록/해제 관리 서비스",
    "DatabaseService.cs": "SQLite 연결 싱글톤 및 Bulk Save 트랜잭션 구문",
    "ExcelService.cs": "ExcelDataReader 기반 고속 파싱",
    "QueryTransformer.cs": "동의어 치환 및 Everything 공식 문법 최종 변환 서비스",
    "AliasManagerViewModel.cs": "매핑 데이터 CRUD 및 엑셀 파싱 조율",
    "MainWindowViewModel.cs": "메인 레이아웃 및 윈도우 생성 이벤트 중계",
    "SearchViewModel.cs": "실시간 검색 뷰모델 (필드, 기본 속성 및 UI 바인딩 래퍼) [PARTIAL]",
    "SearchViewModel.Search.cs": "실시간 검색 실행 및 결과 정렬 로직 [PARTIAL]",
    "SearchViewModel.Settings.cs": "사용자 설정 저장/로드 및 드라이브 초기화 로직 [PARTIAL]",
    "LeftSidebarView.xaml": "좌측 스마트 컨트롤 패널 (UniformGrid, WrapPanel)",
    "LeftSidebarView.xaml.cs": "크기 필터 리셋 트리거",
    "MainWindow.xaml": "메인 윈도우 UI (3:7 Grid Splitter)",
    "MainWindow.xaml.cs": "모달 호출 및 엔진 미구동 감지 시 자동 시작 핸들러",
    "ResultGridView.xaml": "우측 파일 데이터 가상화 리스트뷰",
    "ResultGridView.xaml.cs": "Drag-out 마운트, 네이티브 ContextMenu 팝업, Ctrl+C/X 단축키 감지",
    "App.xaml": "ModernWpfUI 테마 리소스 병합",
    "EverythingFastAlias.csproj": "NuGet 패키지 및 Native DLL 복사 빌드 규칙 지정"
}


# ==============================================================================
# 3. 규칙 판별 및 가교(Bridge) 탐색 헬퍼 함수
# ==============================================================================

def normalize_path(path_str: str) -> str:
    """경로 구분자를 슬래시(/)로 통일하고 앞뒤 공백 및 슬래시를 정돈합니다."""
    return path_str.replace("\\", "/").strip("/")


def has_active_descendant(rel_path: str) -> bool:
    """해당 디렉터리 하위에 SHOW_FILES, SHOW_ALL, SHOW_DIRS, FULL_DIRS 대상이 있는지 확인하여 Bridge 노드 여부를 판별합니다."""
    norm_rel = normalize_path(rel_path)
    prefix = norm_rel + "/"

    for target in SHOW_FILES:
        if normalize_path(target).startswith(prefix):
            return True

    for target in SHOW_ALL:
        norm_t = normalize_path(target)
        if norm_t.startswith(prefix):
            return True

    for target in SHOW_DIRS:
        norm_t = normalize_path(target)
        if norm_t.startswith(prefix):
            return True

    for target in FULL_DIRS:
        norm_t = normalize_path(target)
        if norm_t.startswith(prefix):
            return True

    return False


def is_inside_full_dirs(rel_path: str) -> bool:
    """주어진 경로가 FULL_DIRS 대상 또는 그 하위에 포함되는지 확인합니다."""
    norm_rel = normalize_path(rel_path)
    for target in FULL_DIRS:
        norm_t = normalize_path(target)
        if norm_rel == norm_t or norm_rel.startswith(norm_t + "/"):
            return True
    return False


def is_inside_show_all(rel_path: str) -> bool:
    """주어진 경로의 직속 상위 폴더가 SHOW_ALL에 포함되어 1단계 노출 대상인지 확인합니다."""
    parent = normalize_path(os.path.dirname(rel_path))
    return parent in {normalize_path(p) for p in SHOW_ALL}


def should_ignore_dir(rel_path: str) -> bool:
    """IGNORE_DIRS 규칙에 따라 디렉터리를 무시할지 결정합니다."""
    norm_rel = normalize_path(rel_path)

    # 1. SHOW_DIRS 또는 SHOW_ALL에 명시된 경우 절대 무시하지 않음
    if norm_rel in {normalize_path(p) for p in SHOW_DIRS} or norm_rel in {normalize_path(p) for p in SHOW_ALL}:
        return False

    # 2. 경로 세그먼트 중 IGNORE_DIRS 또는 .vs 접두사에 해당하는 항목 검사
    parts = norm_rel.split("/")
    for part in parts:
        if part in IGNORE_DIRS or part.startswith(".vs"):
            return True

    # 3. 전체 경로 IGNORE_DIRS 매칭 검사
    for ign in IGNORE_DIRS:
        norm_ign = normalize_path(ign)
        if norm_rel == norm_ign or norm_rel.startswith(norm_ign + "/"):
            return True

    # 4. 하위에 SHOW_FILES 등 활성 타겟이 있으면 Bridge 노드로 유지
    if has_active_descendant(norm_rel):
        return False

    # 5. FULL_DIRS 내부이거나 SHOW_ALL 직속이면 포함
    if is_inside_full_dirs(norm_rel) or is_inside_show_all(norm_rel):
        return False

    # 6. 루트 직속 폴더이면 기본 1단계는 노출
    if "/" not in norm_rel:
        return False

    # 7. SHOW_DIRS의 하위 항목 중 SHOW_DIRS에 등록되지 않은 것은 무시 (캐시 폭발 방지)
    for sdir in SHOW_DIRS:
        norm_s = normalize_path(sdir)
        if norm_rel.startswith(norm_s + "/"):
            return True

    return True


def should_include_file(rel_path: str) -> bool:
    """7대 계층 규칙에 따라 파일의 트리 포함 여부를 판정합니다."""
    norm_rel = normalize_path(rel_path)
    file_name = os.path.basename(norm_rel)
    ext = os.path.splitext(file_name)[1].lstrip(".").lower()

    # 1. SHOW_FILES에 명시된 경우 무조건 포함 (최우선)
    if norm_rel in {normalize_path(p) for p in SHOW_FILES}:
        return True

    # 2. IGNORE_FILES 또는 IGNORE_EXTS에 해당하면 제외
    if file_name in IGNORE_FILES:
        return False
    if ext in _NORMALIZED_IGNORE_EXTS:
        return False

    # 3. 상위 경로 중 IGNORE_DIRS에 해당하는 것이 있는지 확인
    parts = norm_rel.split("/")
    for part in parts[:-1]:
        if part in IGNORE_DIRS or part.startswith(".vs"):
            return False

    for i in range(1, len(parts)):
        parent_segment = "/".join(parts[:i])
        if parent_segment in {normalize_path(p) for p in IGNORE_DIRS}:
            return False

    # 4. 루트 파일인 경우 기본 포함
    if "/" not in norm_rel:
        return True

    # 5. FULL_DIRS 내부 파일인 경우 포함
    if is_inside_full_dirs(norm_rel):
        return True

    # 6. SHOW_ALL 직속 파일인 경우 포함
    if is_inside_show_all(norm_rel):
        return True

    return False


# ==============================================================================
# 4. 스마트 주석 추출 및 캐싱 (Smart Annotation Mapper)
# ==============================================================================

def extract_comments_from_overview(overview_path: str) -> dict[str, str]:
    """기존 overview.md 트리 블록에서 파일/폴더별 주석(# ...)을 추출하여 경로 키 매핑을 생성합니다."""
    comments = {}
    if not os.path.exists(overview_path):
        return comments

    try:
        with open(overview_path, "r", encoding="utf-8") as f:
            content = f.read()

        match = re.search(r"<!-- START_TREE -->\s*```text\s*(.*?)\s*```\s*<!-- END_TREE -->", content, re.DOTALL)
        if not match:
            return comments

        tree_block = match.group(1)
        stack = []

        for line in tree_block.splitlines():
            stripped = line.strip()
            if not stripped or stripped.startswith("#"):
                continue

            comment = ""
            tree_part = line
            if " #" in line:
                idx = line.find(" #")
                tree_part = line[:idx]
                raw_comment = line[idx + 1:].strip()
                comment = re.sub(r"^#+\s*", "", raw_comment).strip()

            is_dir = "📁" in tree_part

            clean_part = re.sub(r"^[│\s├└─]+[📁📄]\s*", "", tree_part).strip().rstrip("/")
            if not clean_part:
                continue

            name = clean_part

            indent_match = re.match(r"^([│\s├└─]+)", tree_part)
            indent_str = indent_match.group(1) if indent_match else ""
            depth = len(indent_str) // 4

            target_parent_len = max(0, depth - 1)
            while len(stack) > target_parent_len:
                stack.pop()

            current_rel = "/".join(stack + [name]) if stack else name

            if comment:
                comments[normalize_path(current_rel)] = comment

            if is_dir:
                stack.append(name)

    except Exception as e:
        print(f"[WARN] Failed to extract comments: {e}")

    return comments


# ==============================================================================
# 5. 트리 노드 구축 및 렌더링 엔진
# ==============================================================================

class TreeNode:
    def __init__(self, name: str, rel_path: str, is_dir: bool):
        self.name = name
        self.rel_path = normalize_path(rel_path)
        self.is_dir = is_dir
        self.children: list[TreeNode] = []


def build_tree_structure(root_dir: str) -> TreeNode:
    """규칙에 따라 필터링된 디렉터리/파일 노드 트리를 구축합니다."""
    root_node = TreeNode(os.path.basename(os.path.abspath(root_dir)), "", True)

    def scan_dir(current_dir: str, current_node: TreeNode):
        try:
            entries = os.listdir(current_dir)
        except Exception:
            return

        dirs = []
        files = []

        for e in entries:
            full_path = os.path.join(current_dir, e)
            rel = normalize_path(os.path.relpath(full_path, root_dir))

            if os.path.isdir(full_path):
                if not should_ignore_dir(rel):
                    dirs.append((e, full_path, rel))
            else:
                if should_include_file(rel):
                    files.append((e, full_path, rel))

        dirs.sort(key=lambda x: x[0].lower())
        files.sort(key=lambda x: x[0].lower())

        for name, full_p, rel_p in dirs:
            dir_node = TreeNode(name, rel_p, True)
            current_node.children.append(dir_node)

            # SHOW_DIRS 단일 노드는 하위 탐색 중단 (캐시 수만 개 폭발 방지)
            if rel_p in {normalize_path(p) for p in SHOW_DIRS} and not has_active_descendant(rel_p):
                continue

            # scripts 디렉터리는 단일 폴더 노드만 표시하고 하위 전개 생략 (상세 관리는 docs/memories/scripts_guide.md로 위임)
            if rel_p == "scripts":
                continue

            scan_dir(full_p, dir_node)

        for name, full_p, rel_p in files:
            file_node = TreeNode(name, rel_p, False)
            current_node.children.append(file_node)

    scan_dir(root_dir, root_node)
    return root_node


def get_annotation(rel_path: str, comments_map: dict[str, str]) -> str:
    """주어진 경로에 대해 수동 주석(1순위) 또는 기본 내장 주석(2순위)을 반환합니다."""
    norm_rel = normalize_path(rel_path)

    # 1. overview.md에서 추출된 수동 주석 최우선
    if norm_rel in comments_map and comments_map[norm_rel]:
        return comments_map[norm_rel]

    # 2. DEFAULT_ANNOTATIONS 정확한 상대 경로 매칭
    for def_path, comment in DEFAULT_ANNOTATIONS.items():
        if normalize_path(def_path) == norm_rel:
            return comment

    # 3. 파일명/폴더명 단일 매칭 (예: MEMORY.md, overview.md 등)
    base_name = os.path.basename(norm_rel)
    if base_name in DEFAULT_ANNOTATIONS:
        return DEFAULT_ANNOTATIONS[base_name]

    return ""


def render_tree(node: TreeNode, comments_map: dict[str, str], prefix: str = "", is_root: bool = True) -> list[str]:
    """트리 노드를 ASCII 마크다운 형식으로 렌더링하고 주석을 ljust(48)로 깔끔히 정렬 결합합니다."""
    lines = []

    if is_root:
        lines.append(f"📂 {node.name}")
    else:
        icon = "📁" if node.is_dir else "📄"
        display_name = f"{node.name}/" if node.is_dir else node.name
        item_str = f"{prefix}{icon} {display_name}"

        comment = get_annotation(node.rel_path, comments_map)
        if comment:
            clean_comment = re.sub(r"^#+\s*", "", comment).strip()
            formatted_line = item_str.ljust(48) + f" # {clean_comment}"
        else:
            formatted_line = item_str

        lines.append(formatted_line)

    child_prefix = "" if is_root else (prefix.replace("├── ", "│   ").replace("└── ", "    "))
    total_children = len(node.children)

    for idx, child in enumerate(node.children):
        is_last = (idx == total_children - 1)
        branch = "└── " if is_last else "├── "
        lines.extend(render_tree(child, comments_map, child_prefix + branch, False))

    return lines


# ==============================================================================
# 6. 메인 실행 및 파일 패치 진입점
# ==============================================================================

def main():
    root_dir = os.getcwd()
    overview_file = get_overview_path()

    print(f"[INFO] Scanning workspace: {root_dir}")
    print(f"[INFO] Target overview: {overview_file}")

    if not os.path.exists(overview_file):
        print(f"[ERROR] overview.md not found at: {overview_file}")
        return

    # 1. 기존 주석 스마트 추출
    comments_map = extract_comments_from_overview(overview_file)
    print(f"[INFO] Preserved {len(comments_map)} manual annotations from overview.md")

    # 2. 트리 노드 구축
    tree_root = build_tree_structure(root_dir)

    # 3. 렌더링
    rendered_lines = render_tree(tree_root, comments_map)

    # 4. Note 가이드라인 덧붙임
    rendered_lines.append("")
    rendered_lines.append("# (Note) 에이전트의 토큰 절약을 위해 주요 파일만 선별하여 표시하고 있습니다.")

    new_tree_block = "\n".join(rendered_lines).strip()

    # 5. overview.md 패치
    with open(overview_file, "r", encoding="utf-8") as f:
        doc_content = f.read()

    pattern = r"<!-- START_TREE -->.*?<!-- END_TREE -->"
    if not re.search(pattern, doc_content, re.DOTALL):
        print("[ERROR] Could not find <!-- START_TREE --> and <!-- END_TREE --> tags in overview.md")
        return

    replacement = f"<!-- START_TREE -->\n```text\n{new_tree_block}\n```\n<!-- END_TREE -->"
    updated_doc = re.sub(pattern, replacement, doc_content, flags=re.DOTALL)

    with open(overview_file, "w", encoding="utf-8") as f:
        f.write(updated_doc)

    print(f"[SUCCESS] overview.md tree successfully updated!")


if __name__ == "__main__":
    main()
