# 구현 계획서: 컨텍스트 메뉴 확장, 새로고침 단축키 신설 및 휴지통 삭제 연동

본 계획서는 파일 검색 결과 목록의 빈 공간 우클릭 시 보기/정렬/새로고침을 제어할 수 있는 컨텍스트 메뉴를 추가하고, F5 키를 통한 실시간 새로고침 및 Delete 키 입력을 통한 네이티브 휴지통 삭제 기능을 윈도우 탐색기 수준의 편의성으로 구현하기 위한 설계서입니다.

---

## 1. 요구사항 (Requirements)

1. **리스트 뷰 빈 공간 우클릭 컨텍스트 메뉴 지원**:
   - 자세히 모드 및 섬네일(S, M, L) 모드의 빈 공간 우클릭 시, 탐색기 배경과 유사한 ContextMenu 노출.
   - 아이템 위에서 우클릭 시에는 기존대로 Windows 네이티브 쉘 메뉴(IContextMenu)를 팝업함.
2. **배경 컨텍스트 메뉴 구성 및 F5 단축키 연동**:
   - **보기** 서브메뉴: 자세히, 섬네일S, 섬네일M, 섬네일L 제공 (체크 및 라디오 상태 동기화)
   - **정렬 기준** 서브메뉴: 이름, 경로, 수정한 날짜, 크기 (체크/라디오 상태 동기화) 및 오름차순, 내림차순 정렬 방향 전환 제공
   - **새로고침**: 메뉴 항목 제공 및 앱 전체 단축키인 **F5** 신설
3. **Delete 키 입력 시 휴지통으로 삭제**:
   - 파일명 선택 상태에서 `Delete` 키 클릭 시 경고창(Confirm Dialog) 없이 즉시 휴지통으로 보내기.
   - 삭제 작업 후 포커스가 첫 번째 줄로 강제 리셋되거나 스크롤이 튀지 않아야 함.
   - 현재 삭제된 행 위치의 다음(또는 맨 하단일 경우 이전) 아이템으로 자연스럽게 선택 및 포커스를 보존하고, 리스트 컬렉션에서 해당 항목만 제거(`Remove`)하여 자연스럽게 노출 해제 처리.
4. **정렬 기준 정보 영속화**:
   - 사용자가 결과 목록을 정렬한 기준(정렬 컬럼, 정렬 방향)을 SQLite 로컬 DB에 자동 저장.
   - 앱을 종료 후 재기동할 때, 로컬 DB로부터 정렬 기준 정보를 정상 복원하여 실시간 검색 시 이전 정렬 방식이 온전히 반영되도록 처리.
5. **이름 변경 시 입력 커서 자동 깜박임 및 전체 선택**:
   - F2 키를 입력해 개명 모드로 진입했을 때, 별도의 마우스 클릭 없이 편집용 TextBox가 즉각 키보드 포커스를 획득하고 입력 커서가 깜박이며 기존 파일명이 전체 선택된 상태로 대기하도록 처리.
6. **빌드 배치 파일(bat) 비대화형 실행 및 exit code 개선**:
   - `build-debug.bat` 및 `build-release.bat` 파일 실행 시 `--non-interactive` 인자가 전달되면 `pause` (키보드 대기)를 생략하고 즉시 정상 종료하도록 분기 처리.
   - `dotnet build` 수행 결과로 발생한 `%ERRORLEVEL%`을 확보해 최종 `exit /b` 시 올바른 에러 코드를 반환하도록 설계.

---

## 2. 기술 스택 (Tech Stack)

- **언어 및 프레임워크**: C# .NET 9.0 (WPF)
- **Win32 Shell API 연동 (P/Invoke)**:
  - `SHFileOperation` (shell32.dll) - 파일 목록을 복수로 안전하게 휴지통으로 제거하는 Shell API
- **WPF MVVM 및 UI 제어**:
  - `CommunityToolkit.Mvvm` (RelayCommand, ObservableObject)
  - WPF `InputBindings` (`KeyBinding`)

---

## 3. 폴더 및 파일 변경 구조 (Folder Structure)

본 구현은 기존의 `MVVM 패턴`, `SoC(관심사 분리)`, `DRY`, 및 `One Class One File` 원칙을 고수합니다.

```text
d:\3_Code\3_Apps\43_Search-Edit\Everything검색기\
├── docs/
│   └── memories/
│       └── MEMORY.md
│   └── implementation_plan.md    # [MODIFY] 본 계획서
├── build-debug.bat               # [MODIFY] 비대화형 매개변수 대응 및 exit code 처리 추가
├── build-release.bat             # [MODIFY] 비대화형 매개변수 대응 및 exit code 처리 추가
└── src/
    └── EverythingFastAlias/
        ├── Native/
        │   └── Win32RecycleBinHelper.cs # [NEW] SHFileOperation 기반 휴지통 삭제 기능 구현
        ├── ViewModels/
        │   ├── SearchViewModel.Search.cs # [MODIFY] 정렬 수행 시 설정 즉각 저장 로직 추가
        │   └── SearchViewModel.Settings.cs # [MODIFY] 정렬 기준 정보 SQLite 로드/저장 추가
        └── Views/
            ├── MainWindow.xaml        # [MODIFY] F5 새로고침 단축키 등록
            ├── ResultGridView.xaml    # [MODIFY] 인라인 편집 TextBox에 IsVisibleChanged 이벤트 연동 추가
            ├── ResultGridView.xaml.cs # [MODIFY] 우클릭 판별, ContextMenu 동적 빌드, Delete 삭제 포커스 보존 및 IsVisibleChanged 포커스 핸들러 추가
```

---

## 4. 정보 조회 및 검증 도구 (Lookup & Verification Tools)

- **조회 도구 (Lookup Tools)**: 
  - `yik-parser` 및 `view_file`를 이용해 기존 마우스 이벤트 핸들러와 ViewModel의 정렬/검색 구조를 분석 완료.
- **검증 도구 (Verification Tools)**:
  - 디버그 빌드 스크립트: `build-debug.bat`를 실행하여 컴파일 무결성 검증.
  - 런타임 수동 테스트를 통해 기능의 정상 동작 여부 체크.

---

## 5. 상세 구현 계획 (Implementation Plan)

### 단계 1: 네이티브 휴지통 삭제 헬퍼 구현 (`Native/Win32RecycleBinHelper.cs` [NEW])
1. `shell32.dll`의 `SHFileOperation` API를 P/Invoke로 정의.
2. `SendToRecycleBin(IEnumerable<string> paths)` 메소드 구현:
   - 복수 개의 파일 경로를 널 문자(`\0`)로 구분하고 최종 끝에 이중 널 문자(`\0\0`)를 배치해 전달.
   - `FOF_ALLOWUNDO` (휴지통으로 전송), `FOF_NOCONFIRMATION` (경고창 없음), `FOF_SILENT` (진행 창 없음), `FOF_NOERRORUI` (에러 UI 감춤) 플래그를 조합해 호출.
   - 삭제 처리 성공 시 true를 반환하고, 예외나 API 에러 시 false 반환.

### 단계 2: F5 새로고침 단축키 등록 (`Views/MainWindow.xaml` [MODIFY])
1. `MainWindow.xaml`의 `<Window>` 루트 엘리먼트 직하에 `<Window.InputBindings>` 선언.
2. `<KeyBinding Key="F5" Command="{Binding SearchVM.RefreshCommand}"/>`를 설정하여 윈도우 전체 포커스 환경에서 F5 누를 시 새로고침 검색이 트리거되도록 유도.

### 단계 3: 우클릭 이벤트 분기 및 배경 ContextMenu 동적 빌드 (`Views/ResultGridView.xaml.cs` [MODIFY])
1. `ResultsListView_MouseRightButtonUp`에서 `e.OriginalSource`를 기준으로 visual parent 중 `ListViewItem`이 존재하는지 판별.
2. **아이템 우클릭 시**:
   - 우클릭한 아이템이 비선택 상태라면 유일하게 선택하여 쉘 메뉴에 안전하게 전달되도록 유도.
   - 기존의 Windows 네이티브 `ShellContextMenu.ShowContextMenu` 팝업 기동.
3. **빈 공간 우클릭 시**:
   - `ContextMenu`를 코드 상에서 동적으로 인스턴스화하여 보기, 정렬 기준, 구분선, 새로고침 메뉴를 추가.
   - `SearchViewModel`의 `ViewMode`, `SortColumn`, `SortDirection` 값을 읽어와 메뉴 항목 옆에 라디오/체크 마크 상태를 체크(`IsChecked = true`)하여 일관되게 표시.
   - 메뉴 클릭 이벤트를 바인딩해 `ViewMode` 변경, `SortResults(컬럼)`, `ApplySorting()` 정렬 방향 재반영, `RefreshCommand` 새로고침 기동 명령 등을 바로 호출하도록 연결.

### 단계 4: Delete 단축키 삭제 및 포커스 보존 구현 (`Views/ResultGridView.xaml.cs` [MODIFY])
1. `ResultsListView_KeyDown` 메서드에 `Key.Delete` 감지 추가.
2. 삭제 처리 함수 `DeleteSelectedItems()` 호출:
   - 현재 리스트 뷰에서 선택된 아이템 목록(`selectedItems`)을 획득.
   - 삭제 완료 후 포커스를 넘겨받을 후속 아이템(`nextSelectedItem`)을 미리 결정:
     - 선택 항목들 중 가장 뒤쪽 인덱스 다음의 미삭제 아이템을 우선 선정.
     - 뒤쪽에 없다면, 앞쪽 인덱스 이전의 미삭제 아이템을 차선으로 선정.
   - 루프를 돌며 개별 파일/폴더 경로를 `Win32RecycleBinHelper.SendToRecycleBin`을 통해 휴지통으로 제거.
   - 제거 성공한 아이템에 한해 `SearchViewModel.Results.Remove`를 호출하여 리스트에서 안전하게 제외.
   - 삭제 완료 후, 미리 파악한 `nextSelectedItem`이 컬렉션에 존재한다면 `ResultsListView.SelectedItem = nextSelectedItem`으로 선택을 복원하고, `RestoreFocusToItem`을 비동기로 실행해 가상화 상태 하에서도 스크롤 튀지 않고 안전하게 포커스가 복원되도록 처리.

---

## 6. 검증 계획 (Verification Plan)

### 수동 검증 및 시나리오 테스트
1. **우클릭 컨텍스트 메뉴 확인**:
   - 검색 결과 빈 곳을 우클릭하여 보기(자세히, 섬네일S/M/L) 모드가 실시간 연동 및 SQLite 설정 저장이 제대로 이루어지는지 체크.
   - 정렬 기준(이름/경로/날짜/크기 및 오름/내림차순)을 변경했을 때 리스트가 정렬 순서에 맞게 즉시 업데이트되는지 체크.
2. **F5 새로고침 확인**:
   - 검색어가 있는 상태에서 F5 키를 클릭했을 때 검색이 다시 실행되어 데이터가 재갱신되는지 확인.
3. **Delete 키 삭제 및 포커스 보존 확인**:
   - 한 개 또는 여러 개의 파일을 선택하고 `Delete` 키를 입력했을 때, 경고 창 없이 휴지통으로 즉시 삭제 처리되는지 확인.
   - 삭제 후 스크롤이 맨 처음으로 튀지 않고 삭제된 파일 바로 다음 파일에 선택/포커스가 유지되는지 확인.
   - 휴지통 폴더를 열어 삭제했던 파일이 안전하게 휴지통에 들어가 있는지 최종 확인.

---

## 7. 지식 자산화 계획 (Capitalization Plan)

- 윈도우 휴지통 삭제 API P/Invoke 연동 노하우, 리스트 뷰의 가상화 상태에서 특정 항목 제거 후 포커스가 첫 줄로 튀지 않게 하는 스크롤 보존 처리 아키텍처에 대한 최종 개발 경험을 수동 검증 완료 후 `./docs/memories/MEMORY.md` 파일에 영구 자산으로 기록함.
