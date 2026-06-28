# step006_update_overview_tree_plan_v1

이 계획서는 `update_overview_tree.js` 스크립트를 C# WPF 프로젝트 환경에 맞게 고도화하고, `overview.md`에 `<!-- START_TREE -->` 와 `<!-- END_TREE -->` 마커를 삽입하여 자동화 동기화가 정상 작동하도록 구성하기 위한 작업 계획입니다.

## 1. 요구사항 (Requirements)
- **C# / WPF 빌드 폴더 무시**: `.vs`, `bin`, `obj` 폴더가 트리 구조에 생성되거나 스캔되는 것을 방지하기 위해 `IGNORE_DIRS` 필터에 등록합니다.
- **HTML 주석 마커 연동**: `overview.md`에 누락된 `<!-- START_TREE -->` 및 `<!-- END_TREE -->` 주석 마커를 `3.1. 폴더 및 파일 트리 구조` 아래에 추가합니다.
- **통합 트리 빌드**: 이전의 이중 트리(루트 구조와 src/ 구조 분리)를 지우고, 마커 사이의 단일 통합 디렉터리 트리로 자동 치환되도록 구현합니다.
- **주석(Comment) 캐싱 보존**: 파일/폴더 옆에 수동으로 입력해 둔 설명 주석(예: `# ModernWpfUI 테마 리소스 병합`)이 스크립트 실행 후에도 유실되지 않고 자동 매핑 및 정렬되도록 보장합니다.

## 2. 기술 스택 (Tech Stack)
- **Runtime**: Node.js (v18+)
- **OS**: Windows 10
- **Build/Scripting**: Pure JavaScript (`fs`, `path` 내장 모듈 사용)

## 3. 폴더 구조 (Folder Structure)
- `d:\3_Code\3_Apps\43_Search-Edit\Everything검색기\`
  - `docs/memories/`
    - [overview.md](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/docs/memories/overview.md) (수정 예정)
  - `scripts/`
    - [update_overview_tree.js](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/scripts/update_overview_tree.js) (수정 예정)

## 4. 조회 도구 (Lookup Tools)
- [view_file](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/scripts/update_overview_tree.js): 원본 스크립트 정밀 분석
- [view_file](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/docs/memories/overview.md): 마커 삽입 위치 및 기존 트리 주석 내용 수집

## 5. 검증 도구 (Verification Tools)
- `run_command`를 통한 `node scripts/update_overview_tree.js` 실행 및 실행 결과 확인.
- `git diff`를 통한 `overview.md` 트리 갱신 및 주석 보존 무결성 체크.

## 6. 구현 계획 (Implementation Plan)

### Step 1. [MODIFY] `docs/memories/overview.md` 수정 (난이도: ★)
- `### 3.1. 폴더 및 파일 트리 구조` 아래의 기존 이중 코드 블록들을 모두 소거합니다.
- 해당 위치에 다음과 같이 HTML 마커를 삽입합니다:
  ```markdown
  ### 3.1. 폴더 및 파일 트리 구조

  <!-- START_TREE -->
  ```text
  // 여기에 스크립트가 트리를 동적으로 생성할 예정
  ```
  <!-- END_TREE -->
  ```

### Step 2. [MODIFY] `scripts/update_overview_tree.js` 수정 (난이도: ★★)
- `IGNORE_DIRS` Set에 C# WPF 빌드 관련 폴더인 `.vs`, `bin`, `obj`를 추가합니다.
- `IGNORE_FILES` Set에 `.user` (C# 프로젝트 유저 설정 파일) 등을 추가할 수 있도록 보강합니다.
- C# 프로젝트 최적화 필터:
  ```javascript
  const IGNORE_DIRS = new Set([
    ".git",
    "node_modules",
    "temp",
    ".history",
    ".agents",
    "screenshots",
    "snapshots",
    "tavily",
    "evaluate_scripts",
    "websavers",
    "manuals",
    ".venv",
    "__pycache__",
    "bin",              // C# 빌드 아티팩트
    "obj",              // C# 빌드 아티팩트
    ".vs"               // Visual Studio 캐시
  ]);
  ```

## 7. 검증 계획 (Verification Plan)
- **스크립트 구동 검증**: `node scripts/update_overview_tree.js` 실행 시 에러 없이 `=== Overview Update Complete ===` 로그가 출력되는지 검증합니다.
- **문서 무결성 검증**: `overview.md`를 열어 `<!-- START_TREE -->` 와 `<!-- END_TREE -->` 사이에 정상적으로 이그노어 폴더가 제외된 트리가 주석(Comment)들과 함께 이쁘게 정렬되어 삽입되었는지 체크합니다.
- **기존 주석 보존 검증**: 기존에 수동으로 달려있던 주석들(예: `# SQLite 연결 싱글톤 및 Bulk Save 트랜잭션 구문` 등)이 올바른 매칭 파일 옆에 잘 붙어 있는지 확인합니다.

## 8. 자산화 계획 (Capitalization Plan)
- 작업 완료 후 `docs/memories/MEMORY.md`에 지식 자산화 로그를 추가하고, `overview.md` 파일의 동기화 상태를 최종 확인합니다.

## 9. 승인 요청 (Request for Approval)
- 사용자의 지시 및 요구사항에 맞추어 `overview.md`에 주석 마커를 삽입하고 C# 관련 빌드 폴더 차단 필터를 추가하는 95% 이상 명확한 해결책을 도출하였습니다. 이 계획서에 대한 검토 및 승인을 요청드립니다.
