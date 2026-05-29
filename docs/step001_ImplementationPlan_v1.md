# Everything FastAlias 구현 계획서 (C# WPF 스택 전면 교체)

본 계획서는 `Everything.exe` 파일 검색 엔진과 Windows 10 OS(탐색기, Directory Opus 등)와의 완벽한 네이티브 연동(마우스 드래그 앤 드롭, 클립보드 복사/잘라내기/붙여넣기, 윈도우 네이티브 우클릭 쉘 메뉴 지원 등)을 달성하기 위해, 기존 Electron/React 스택을 **C# .NET 9.0 WPF** 환경으로 전면 교체하여 구현하기 위한 마스터 계획서입니다.

---

## 1. Requirements (요구사항)

### 1.1. 기능적 요구사항
1. **Everything 엔진 FFI 연동**:
   - P/Invoke를 사용하여 `everything64.dll`의 C API를 C# 환경에 바인딩.
   - 유니코드 와이드 캐릭터 API(`Everything_SetSearchW`, `Everything_GetResultFullPathNameW` 등)를 사용하여 다국어(한글 등) 깨짐 방지.
   - Everything 서비스/프로세스 미감지 시 경고 팝업 안내 및 사용자 동의 하에 `Everything.exe` 프로세스를 백그라운드로 자동 구동하는 예외 처리 파이프라인 탑재.
2. **FastAlias 스마트 쿼리 가공**:
   - `SQLite`에 단어 매핑 테이블을 생성하여 동의어(예: 사과 ➔ apple, 🍎) 정보 영속화.
   - 사용자가 입력한 검색어를 분리하여 매핑 사전에 따라 `<사과|apple|🍎>` 형태로 변환하는 `QueryTransformer` 구현.
   - 9가지 검색 조건(대소문자, 전체 단어 일치, 정규식, 휴지통 제외, 파일/폴더 타겟팅, 미디어/확장자 프리셋, 크기 필터, 재귀 탐색 제어 등)에 맞추어 최종 Everything 쿼리를 구성하고 DLL에 전달.
3. **Windows OS 완벽 연동**:
   - **드래그 아웃 (Drag-out)**: 리스트 뷰에서 마우스로 항목을 드래그하여 외부의 윈도우 탐색기나 Directory Opus, 카카오톡 등에 드롭 시 해당 파일 객체가 복사/이동될 수 있도록 Windows DataObject 기반 `FileDrop` 구현.
   - **클립보드 액션 (Ctrl+C, Ctrl+X, Ctrl+V)**: 검색 리스트에서 단축키 입력 시 해당 파일들을 클립보드에 네이티브 파일 객체로 복사/잘라내기하여 외부 윈도우 탐색기에서 붙여넣기가 가능하도록 연동.
   - **네이티브 우클릭 메뉴 (Shell Context Menu)**: 리스트 뷰 항목 우클릭 시, Windows 10 탐색기에서 지원하는 실제 네이티브 쉘 메뉴(`IContextMenu` 인터페이스)를 팝업으로 노출하여 7-Zip 압축, 연결 프로그램 등의 탐색기 기능을 그대로 사용 가능하게 함.
4. **대용량 파일 리스트 고속 UI**:
   - WPF의 VirtualizingStackPanel을 활용하여 수만 건의 검색 결과 유입 시에도 스크롤 끊김 및 프레임 드랍이 전혀 없는 초고속 가상화 리스트 구축.
   - 테이블 헤더 클릭 시 오름차순/내림차순 정렬 및 컬럼 너비 조절 기능 지원.
5. **Excel 매핑 데이터 고속 업로드**:
   - `ExcelDataReader` 또는 `EPPlus` 라이브러리를 활용하여 대량의 `.xlsx` 데이터 파싱.
   - DB에 데이터를 기록할 때 SQLite Connection 트랜잭션(`db.BeginTransaction()`)을 수행하여 수천 건의 단어 매핑을 0.1초 내로 벌크 업로드 완료.

### 1.2. 비기능적 요구사항
- **디자인 & Aesthetics**: WPF 기본 스타일을 탈피하고 ModernWPF 또는 Wpf.Ui 라이브러리를 사용하여 Slate 라이트 테마 및 다크 테마를 미려하게 디자인.
- **아키텍처**: MVVM 패턴을 준수하고, 관심사 분리(SoC) 및 One-Class-Per-File 원칙을 지켜 유지보수성을 극대화.

---

## 2. Tech Stack (기술 스택)

- **언어 및 런타임**: C# .NET 9.0 (WPF Desktop Application)
- **UI 프레임워크 및 테마**: ModernWPF (Windows 11 스타일의 깔끔한 WinUI UI 적용) 및 CommunityToolkit.Mvvm (MVVM 뷰모델 지원)
- **로컬 데이터베이스**: Microsoft.Data.Sqlite (EF Core 제외, 고성능 네이티브 ADO.NET 클라이언트 라이브러리 사용)
- **Excel 라이브러리**: ExcelDataReader (경량 및 초고속 엑셀 스트림 리더)
- **네이티브 쉘 바인딩**: Win32 API Shell / COM 인터페이스 (IContextMenu, DragDrop)
- **아이콘**: Lucide.Wpf 또는 FontAwesome.Wpf

---

## 3. Folder Structure (폴더 구조)

```text
D:\3_Code\3_Apps\43_Search-Edit\Everything검색기\
├── .gitignore
├── EverythingFastAlias.sln
├── src/
│   └── EverythingFastAlias/
│       ├── EverythingFastAlias.csproj
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── Config/
│       │   ├── AppConstants.cs            # IPC 및 설정 상수
│       │   └── UIConstants.cs             # UI 크기 및 기본 핫키 설정
│       ├── Models/
│       │   ├── SearchResultItem.cs        # 검색 결과 데이터 객체
│       │   ├── SearchOptions.cs           # 9가지 검색 토글 옵션 상태 객체
│       │   └── AliasMapping.cs            # 동의어 매핑 도메인 객체
│       ├── ViewModels/
│       │   ├── MainWindowViewModel.cs     # 메인 창 컨트롤 뷰모델
│       │   ├── SearchViewModel.cs         # 검색창 및 옵션 뷰모델
│       │   └── AliasManagerViewModel.cs   # 매핑 관리자 뷰모델
│       ├── Views/
│       │   ├── MainWindow.xaml            # 메인 레이아웃 뷰
│       │   ├── MainWindow.xaml.cs
│       │   ├── LeftSidebarView.xaml       # 좌측 옵션 컨트롤 패널 뷰
│       │   ├── LeftSidebarView.xaml.cs
│       │   ├── ResultGridView.xaml        # 우측 버추얼 테이블 뷰
│       │   ├── ResultGridView.xaml.cs
│       │   ├── Modals/
│       │   │   ├── HelpWindow.xaml        # 도움말 및 검색 문법 가이드 창
│       │   │   └── AliasManagerWindow.xaml# 매핑 사전 에디터 창
│       │   └── Components/
│       │       └── ToggleChip.xaml        # 컴팩트 토글 칩 스타일
│       ├── Native/
│       │   ├── EverythingSdk.cs           # everything64.dll P/Invoke 래퍼
│       │   ├── EverythingBridge.cs        # 고레벨 Everything API 컨트롤러
│       │   ├── Win32ClipboardHelper.cs    # 클립보드 FileDrop 및 Ctrl+C/X/V 구현
│       │   └── ShellContextMenu.cs        # Windows 네이티브 IContextMenu 팝업 호출기
│       ├── Services/
│       │   ├── QueryTransformer.cs        # 매핑 치환 및 문법 가공 서비스
│       │   ├── ExcelService.cs            # ExcelDataReader 기반 고속 파서
│       │   └── DatabaseService.cs         # SQLite Connection 및 벌크 트랜잭션 CRUD
│       └── Assets/
│           ├── dll/
│           │   └── Everything64.dll       # FFI 타겟 DLL 파일
│           └── fonts/
└── docs/
    ├── memories/
    │   ├── overview.md
    │   └── AGENTS.md
    └── step001_ImplementationPlan_v1.md   # 본 구현 계획서
```

---

## 4. Search/Retrieval Tools (조회 및 검색 도구)

- **`grep_search`**: C# 클래스 구조 검색 및 특정 API 구현 패턴 확인용
- **`codegraph`**: P/Invoke 구조 및 클래스 의존 관계 분석 (codegraph init 완료)
- **`view_file`**: C# 소스 파일 및 XAML 마크업 작성 상태 검사

---

## 5. Verification Tools (검증 도구)

- **MSBuild / Dotnet CLI (`dotnet build`, `dotnet run`)**: 컴파일 성공 여부 및 애플리케이션 구동 검증
- **단위 테스트 프로젝트 (`EverythingFastAlias.Tests`)**: `QueryTransformer.cs` 문자열 치환 알고리즘의 정확성을 검증하는 MSTest/xUnit 단위 테스트 빌드 및 `dotnet test` 실행

---

## 6. Implementation Plan (구현 일정)

### Phase 1: 개발 환경 구성 및 프로젝트 뼈대 생성
- `dotnet new wpf`로 신규 프로젝트 생성 및 `.sln` 구성.
- NuGet 패키지 추가: `Microsoft.Data.Sqlite`, `ExcelDataReader`, `ModernWpfUI`, `CommunityToolkit.Mvvm`.
- 폴더 트리 구조 물리적으로 생성 및 기본 설정 파일 탑재.

### Phase 2: 데이터베이스 및 비즈니스 로직 레이어 구현
- `DatabaseService` 싱글톤 및 SQLite 매핑 사전을 위한 DDL 작성.
- `ExcelService` 구현: ExcelDataReader를 통한 벌크 로드 및 DB 트랜잭션 바인딩.
- `QueryTransformer` 구현: `<단어|매핑단어>` 치환 및 제외, 크기 필터, 미디어 필터 규칙 적용.

### Phase 3: 네이티브 FFI 및 OS 연동 레이어 구현
- `EverythingSdk` P/Invoke 유니코드 선언 및 `EverythingBridge` 래퍼 작성.
- Everything 서비스 체크 및 자동 실행 유틸리티 구현.
- `Win32ClipboardHelper` 작성: 파일 클립보드 복사(CF_HDROP), 잘라내기 처리.
- `ShellContextMenu` 구현: 리스트뷰의 HWND에 네이티브 `IContextMenu` 팝업을 포인터 바인딩하여 출력.

### Phase 4: UI 컴포넌트 및 MVVM 구현
- `MainWindow.xaml` 및 Left/Right 뷰 구성 (WPF Grid 및 ModernWpf 테마 적용).
- `ResultGridView` 내부 ListView의 UI 가상화(`VirtualizingStackPanel`) 활성화 및 헤더 정렬 설계.
- 드래그 아웃 트리거: 마우스 드래그 이동 발생 시 WPF `DragDrop.DoDragDrop` 바인딩.
- `SearchViewModel`, `AliasManagerViewModel` 제작 및 View 바인딩.

---

## 7. Verification Plan (검증 방안)

### 7.1. Automated Tests (자동화 테스트)
- `QueryTransformerTest.cs` 단위 테스트 실행:
  - `사과` 입력 시 ➔ `<사과|apple|🍎>` 치환 확인.
  - 제외 단어 `tmp` 지정 시 ➔ `!tmp` 연산자 자동 결합 확인.
  - 사진 필터 켜고 확장자 `zip` 지정 시 ➔ `pic: ext:zip` 등의 복합 Everything 구문 정상 변환 확인.
- `dotnet test` 명령어를 통한 자동 빌드 및 테스트 통과 확인.

### 7.2. Manual Verification (수동 테스트)
- Everything 서비스 강제 종료 후 앱 실행 시 ➔ "Everything 엔진이 감지되지 않았습니다. 시작하시겠습니까?" 모달 팝업이 출력되는지 확인.
- 검색 결과 목록에서 임의의 파일을 마우스로 바탕 화면 및 외부 탐색기(Directory Opus 등)에 드래그하여 파일 복사가 성공적으로 수행되는지 검토.
- 결과 목록 우클릭 시 7-Zip 등 실제 윈도우 OS의 기본 마우스 쉘 메뉴가 깨지지 않고 네이티브 팝업 형태로 출력되는지 검토.

---

## 8. Assetization Plan (자산화 계획)

- 구현 완료 시, WPF 네이티브 연동(IContextMenu, Drag-out, Clipboard HDROP) 중에 발생한 예외들과 해결책을 `AGENTS.md`에 정밀 기록.
- `overview.md` 파일의 기술 스택 및 구조 다이어그램 정보를 C# WPF 기준에 맞춰 전면 갱신.
