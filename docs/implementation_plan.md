# 구현 계획서: 좌측 패널 구조 변경 및 썸네일 보기 옵션 추가

본 계획서는 Everything FastAlias 애플리케이션의 사용자 인터페이스 개선을 위해 좌측 스마트 제어 패널의 섹션 순서를 조정하고, 파일 검색 결과 목록의 썸네일 보기(S, M, L) 옵션을 신규 추가하기 위한 상세 설계서입니다.

---

## 1. 요구사항 (Requirements)

1. **좌측 패널 레이아웃 순서 재배치**:
   - `조건 초기화` 버튼을 좌측 패널의 최상단(A-Drive 위)으로 변경.
   - `검색 엔진 옵션` 섹션을 패널의 최하단으로 이동.
2. **보기 옵션(ViewMode) 제어 기능 추가**:
   - `보기 옵션` 섹션을 신설하고 라디오 버튼 그룹 형태로 제공.
   - 옵션 종류: `자세히`, `섬네일S` (작은 썸네일), `섬네일M` (중간 썸네일), `섬네일L` (큰 썸네일).
3. **썸네일 뷰 레이아웃 및 비동기 렌더링 구현**:
   - `자세히` 모드: 기존 `GridView` 컬럼 테이블 형식 유지.
   - `섬네일S / M / L` 모드: 윈도우 OS의 폴더/파일 기본 아이콘 또는 실제 미디어 파일(이미지/비디오 등)의 썸네일을 비동기 로딩하여 랩패널(WrapPanel) 형태로 배치.
   - 썸네일 보기 모드에서도 `더블클릭 실행`, `드래그 아웃(Drag Drop)`, `클립보드 복사/잘라내기`, `F2 인라인 이름변경` 기능이 완벽히 연동되도록 설계.
   - 대량 데이터 조회 시의 UI 프리징 방지를 위한 백그라운드 스레드 풀 기반 썸네일 디스크 I/O 처리 및 메모리 캐싱 적용.

---

## 2. 기술 스택 (Tech Stack)

- **언어 및 런타임**: C# .NET 9.0 (WPF 데스크톱 애플리케이션)
- **UI 라이브러리**: ModernWPF (Windows 11 스타일)
- **Win32 Shell API 연동 (P/Invoke)**:
  - `SHGetFileInfo` (기본 폴더 및 파일 확장자별 대형 아이콘 추출용)
  - `IShellItemImageFactory` & `SHCreateItemFromParsingName` (실제 고화질 썸네일 및 파일 아이콘 렌더링용)
  - `gdi32.dll` -> `DeleteObject` (HBITMAP 핸들 소멸을 통한 메모리 누수 원천 해결)

---

## 3. 폴더 및 파일 변경 구조 (Folder Structure)

본 구현은 기존의 `MVVM 패턴`, `SoC(관심사 분리)`, `DRY`, 및 `One Class One File` 원칙을 철저히 고수합니다.

```text
d:\3_Code\3_Apps\43_Search-Edit\Everything검색기\
├── docs/
│   └── memories/
│       └── deepwiki_repos.md
│   └── implementation_plan.md    # [NEW] 본 계획서
└── src/
    └── EverythingFastAlias/
        ├── Models/
        │   ├── SearchResultItem.cs   # [MODIFY] Thumbnail 바인딩 속성 및 비동기 로드 트리거 추가
        │   └── ViewMode.cs           # [NEW] 자세히/섬네일S/M/L 열거형(Enum) 정의
        ├── Native/
        │   ├── ShellIconHelper.cs    # [NEW] 기본 폴더 및 파일 아이콘 추출 정적 헬퍼 (SHGetFileInfo)
        │   └── ShellThumbnailHelper.cs # [NEW] 썸네일 추출 및 GDI 리소스 반환 P/Invoke 헬퍼
        ├── ViewModels/
        │   ├── SearchViewModel.cs    # [MODIFY] ViewMode 양방향 바인딩 래퍼 추가
        │   └── SearchViewModel.Settings.cs # [MODIFY] ViewMode 설정 저장(SQLite AppSettings) 및 복원
        └── Views/
            ├── LeftSidebarView.xaml  # [MODIFY] 조건 초기화 최상단화, 검색 엔진 옵션 최하단화, 보기 옵션 추가
            ├── ResultGridView.xaml   # [MODIFY] ViewMode에 따른 Details(GridView)/Thumbnail(WrapPanel) 스타일 트리거 전환 구조 설계
            └── ResultGridView.xaml.cs # [MODIFY] 썸네일 뷰 모달 및 F2 Rename 렌더링 상태 변경 이벤트 호환 보장
```

---

## 4. 정보 조회 및 검증 도구 (Lookup & Verification Tools)

- **조회 도구 (Lookup Tools)**: 
  - `yik-parser` 및 `codegraph`를 이용한 기존 뷰 모델과 XAML 바인딩 로직의 종속성 추가 점검.
- **검증 도구 (Verification Tools)**:
  - PowerShell 터미널 빌드: `dotnet build src/EverythingFastAlias/EverythingFastAlias.csproj` 명령어를 이용해 정적 컴파일 무결성 검증.
  - 단위 테스트 및 동작 검증: Everything 엔진 실행 후 실제 이미지 검색 결과를 썸네일 뷰로 전환하여 정상 렌더링 여부 확인.

---

## 5. 상세 구현 계획 (Implementation Plan)

### 단계 1: 모델 및 Native 헬퍼 계층 구축
1. **`ViewMode.cs` 생성**:
   - `Details`, `ThumbnailS`, `ThumbnailM`, `ThumbnailL` 멤버를 가지는 enum 타입 정의.
2. **`ShellIconHelper.cs` 생성**:
   - `SHGetFileInfo`를 사용해 OS에 내장된 기본 파일 아이콘과 폴더 아이콘을 로드 및 WPF `ImageSource`로 캐싱.
3. **`ShellThumbnailHelper.cs` 생성**:
   - `IShellItemImageFactory`를 통한 썸네일 비트맵 데이터 비동기 추출.
   - WPF `Interop.Imaging.CreateBitmapSourceFromHBitmap` 완료 즉시 `DeleteObject`를 안전하게 호출하여 GDI 핸들 누수 완벽 차단.
4. **`SearchResultItem.cs` 수정**:
   - `ImageSource? Thumbnail` 프로퍼티 신설.
   - `Thumbnail` 속성 조회 시 캐시가 비어 있으면 내부적으로 `SemaphoreSlim(4)` 제한을 둔 백그라운드 비동기 태스크를 구동해 파일의 실제 썸네일 또는 기본 아이콘(ShellIconHelper)을 읽어와 UI 스레드 바인딩 갱신.

### 단계 2: 뷰 모델 및 설정 보존 계층 반영
1. **`SearchViewModel.cs` 수정**:
   - `ViewMode` 및 바인딩용 Boolean 래퍼 프로퍼티(Details, ThumbnailS/M/L) 구현.
   - 보기 옵션 전환에 따른 UI 레이아웃의 크기 바인딩용 읽기전용 도우미 프로퍼티 구성 (예: ThumbnailItemWidth, ThumbnailItemHeight, ThumbnailImageSize).
2. **`SearchViewModel.Settings.cs` 수정**:
   - `LoadSettings()` 및 `SaveSettings()` 시 `ViewMode` 상태를 SQLite 데이터베이스의 `AppSettings`에 문자열로 추가 기록 및 구동 시 정상 복원.

### 단계 3: UI 마크업(XAML) 레이어 개편
1. **`LeftSidebarView.xaml` 수정**:
   - 최하단의 `Button Content="조건 초기화"` 엘리먼트를 최상단(A-Drive 위)으로 배치 이동.
   - `SECTION E: 검색 엔진 옵션`을 패널의 최하단으로 강제 이동.
   - 드라이브 및 탐색 범위 사이에 `보기 옵션` 섹션을 배치하고 라디오 버튼 추가.
2. **`ResultGridView.xaml` 수정**:
   - `ListView` 컨트롤의 `Style` 내부에 `ViewMode` 값을 판별하는 `DataTrigger`들을 바인딩.
   - `Details`일 때는 `View` 프로퍼티에 `ResultsGridView(GridView)`를 바인딩하고 `ItemTemplate`을 `null`로 지정.
   - `ThumbnailS/M/L`일 때는 `View`를 `null`로 강제 지정하고, `ItemsPanel`을 `WrapPanel`로 세팅하며, 썸네일용 `ItemTemplate`을 렌더링.
   - 썸네일 템플릿 내의 텍스트 영역에 F2 인라인 이름변경용 `TextBlock`/`TextBox` 토글을 결합하여 기능 누락 방지.

---

## 6. 검증 계획 (Verification Plan)

### 수동 검증 및 동작 테스트
1. **패널 배치 검증**: 좌측 패널 레이아웃의 버튼 위치가 기획 설계대로 상단/하단에 조화롭게 노출되는지 확인.
2. **동기화 및 복원 검증**: 보기 모드를 `섬네일M` 등으로 바꾸고 애플리케이션을 재구동했을 때 설정 데이터베이스로부터 보기 모드 상태가 온전히 로드되는지 확인.
3. **기능 통합 검증**: 썸네일 뷰 모드에서 마우스 더블클릭을 통한 파일 실행, Ctrl+C 복사 후 탐색기 붙여넣기, 외부 탐색기로의 마우스 드래그 앤 드롭, F2 인라인 이름변경 등이 깨지지 않고 모두 정상적으로 적용되는지 수동 교차 검증.
4. **성능 및 누수 검증**: 수천 개의 검색 결과 상태에서 썸네일 뷰로 스크롤을 무작위로 위아래로 휠링할 때 CPU 및 메모리 점유율 안정성 체크 (GDI 핸들 누수가 없는지 확인).

---

## 7. 지식 자산화 계획 (Capitalization Plan)

- 썸네일 및 네이티브 아이콘을 비동기 처리하고 GDI 리소스를 메모리 리크 없이 폐기하는 정적 헬퍼 기법 및 WPF `ListView.Style.Triggers`를 사용한 유연한 뷰 교체 레이아웃 아키텍처 지식을 작업 완료 후 `./docs/memories/MEMORY.md` 및 `wpf_coding_guidelines.md`에 등재하여 지식 공유.
