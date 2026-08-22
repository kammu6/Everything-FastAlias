# 📋 [Step006] 중앙 집중식 단축키 관리 아키텍처 및 F1 도움말 키 바인딩 구현 계획서

---

## 1. Requirements (요구사항)

### 1.1 배경 및 문제 정의
1. **단축키 코드의 파편화**:
   - `MainWindow.xaml` (`<Window.InputBindings>`에 F5만 등록), `MainWindow.xaml.cs` (메뉴 이벤트 핸들러), `ResultGridView.xaml.cs` (`ResultsListView_KeyDown` 내부에 Ctrl+C, Ctrl+X, F2, Alt+Enter, Shift+Enter, Enter, Shift+Delete, Delete 하드코딩) 등 단축키 처리 로직이 여러 파일에 분산되어 유지보수성과 확장성이 저하되어 있음.
2. **F1 단축키 미작동 이슈**:
   - 메뉴 헤더에만 `"도움말 보기 (F1)"` 텍스트로 적혀있고, 실제 `Window.InputBindings` 또는 키 이벤트 디스패처에 바인딩되지 않아 `F1` 키를 눌러도 도움말 모달이 호출되지 않음.
3. **사용자 정의 단축키 확장성 부재**:
   - 단축키가 상수 및 하드코딩된 `if-else` 분기문으로 처리되어 있어, 향후 사용자가 UI(설정 창)에서 자신만의 단축키를 커스텀 지정하거나 변경할 수 있는 구조적 기반이 없음.

### 1.2 목표 및 수용 기준 (Acceptance Criteria)
1. **단축키 중앙 관리 서비스(`ShortcutService`) 및 데이터 모델(`ShortcutItem`, `ShortcutAction`) 구축**:
   - 단축키를 액션 ID, 표시 이름, 카테고리, 기본 키/보조키, 현재 키/보조키, 스코프(`Global`, `ResultGrid`) 단위로 객체화.
2. **SQLite 영속성 및 기본값 복원 지원**:
   - `AppSettings` 테이블에 `Shortcut_{ActionId}` 키로 사용자 커스텀 단축키 저장 및 로드. 기본값 복원(`ResetAll`, `ResetAction`) 지원.
3. **통합 키보드 라우터(Central Dispatcher) 구축**:
   - `MainWindow.PreviewKeyDown` 및 `ResultGridView.PreviewKeyDown`에서 `ShortcutService.TryHandleKeyDown()`을 통해 단축키 일괄 라우팅 및 커맨드 실행.
   - `TextBox` 텍스트 입력 중 포커스 충돌을 방지하면서도 전역 키(`F1`, `F5`, `Ctrl+F` 등)가 안정적으로 동작하도록 보장.
4. **F1 단축키 즉시 해결**:
   - 검색창 포커스 상태, 결과 목록 포커스 상태 등 어떤 상황에서도 `F1`을 누르면 도움말 창이 즉각 팝업.
5. **단축키 설정 UI 연동을 위한 MVVM 프레임워크 완비**:
   - 향후 `도구(T) > 단축키 설정...` 모달창을 추가할 때 즉시 바인딩 가능한 `ObservableCollection<ShortcutItem>` 제공.

---

## 2. Tech Stack (기술 스택)

- **언어 및 프레임워크**: C# 13, .NET 9.0 (WPF)
- **MVVM 툴킷**: `CommunityToolkit.Mvvm` (ObservableObject, RelayCommand)
- **UI 테마**: ModernWPF UI (`modernwpf`)
- **데이터베이스**: SQLite (`System.Data.SQLite.Core`), `DatabaseService`
- **단축키 및 입력 시스템**: `System.Windows.Input` (`Key`, `ModifierKeys`, `KeyGesture`)

---

## 3. Hypotheses & Grounding Evidence (가설 및 근거)

### 3.1 가설 1: F1 키가 동작하지 않는 원인
- **근거**: WPF에서 `MenuItem`의 `InputGestureText="F1"` 또는 `Header="도움말 보기 (F1)"`은 단순 시각적 텍스트 표시일 뿐 실제 `ICommand`를 트리거하는 `KeyBinding`을 생성하지 않음. 또한 `TextBox` 포커스 시 WPF 기본 `Help` 커맨드가 `PreviewKeyDown`을 가로채므로, 최상단 Window 레벨의 `PreviewKeyDown` 디스패칭 또는 명시적 `InputBindings`가 필수적임.

### 3.2 가설 2: 단축키 구조의 스코프 분리 (Global vs ResultGrid)
- **근거**: `F1(도움말)`, `F5(새로고침)`, `Ctrl+N(새창)` 등은 앱 전체 어디서나 동작해야 하는 **전역(Global) 스코프**인 반면, `F2(이름변경)`, `Shift+Enter(위치 열기)`, `Delete(휴지통)`, `Shift+Delete(영구삭제)` 등은 검색 결과 목록에 포커스가 있거나 항목이 선택되었을 때만 발동해야 하는 **결과 목록(ResultGrid) 스코프**임.
- 따라서 단축키 모델에 `ShortcutScope` 열거형을 부여하여 스코프별 우선순위와 필터링을 수행하면 텍스트 입력 충돌을 완벽히 방지할 수 있음.

---

## 4. Folder Structure (폴더 구조 & 아키텍처)

`MVVM`, `SoC`, `DRY`, `One Class One File` 원칙 준수:

```text
src/EverythingFastAlias/
├── Models/
│   ├── ShortcutAction.cs        # [NEW] 단축키 액션 열거형 (ShowHelp, Refresh, Rename, Delete, ...)
│   ├── ShortcutScope.cs         # [NEW] 단축키 스코프 열거형 (Global, ResultGrid)
│   └── ShortcutItem.cs          # [NEW] 단축키 정의 모델 (Action, Name, Key, Modifiers, Scope, ...)
├── Services/
│   └── ShortcutService.cs       # [NEW] 단축키 레지스트리, DB 영속성, 키 판별 및 디스패치 중앙 서비스
├── ViewModels/
│   ├── MainWindowViewModel.cs   # [MODIFY] ShortcutService 연동 및 전역 커맨드 연결
│   └── SearchViewModel.cs       # [MODIFY] 검색 결과 관련 단축키 커맨드 연동
├── Views/
│   ├── MainWindow.xaml.cs       # [MODIFY] Window.PreviewKeyDown 중앙 디스패처 연결
│   └── ResultGridView.xaml.cs   # [MODIFY] 하드코딩된 KeyDown 분기를 ShortcutService 호출로 전면 리팩토링
```

---

## 5. Lookup Tools (조회 도구)

- `view_file` : `MainWindowViewModel.cs`, `ResultGridView.xaml.cs`, `DatabaseService.cs`
- `grep_search` : `KeyBinding`, `KeyDown`, `PreviewKeyDown` 패턴 조회

---

## 6. Verification Tools (검증 도구)

- `dotnet build src/EverythingFastAlias/EverythingFastAlias.csproj -c Debug` : 컴파일 및 문법 검증
- `dotnet test src/EverythingFastAlias.Tests/EverythingFastAlias.Tests.csproj --no-build` : 단위 테스트 검증
- `scripts/dev_tools/memory_log.py` : 지식 자산화 기록

---

## 7. Implementation Plan (구현 계획)

### Step 1: 단축키 열거형 및 데이터 모델 구축 (★☆☆☆☆)
- `ShortcutAction.cs`: `ShowHelp`, `Refresh`, `FocusSearch`, `NewWindow`, `ExportResults`, `ToggleSidebar`, `OpenAliasManager`, `OpenExtensionManager`, `ExecuteItem`, `ExplorePath`, `ShowProperties`, `RenameItem`, `CopyItem`, `CutItem`, `DeleteToRecycleBin`, `PermanentDelete` 정의.
- `ShortcutScope.cs`: `Global`, `ResultGrid` 정의.
- `ShortcutItem.cs`: `ObservableObject` 상속, `Key`, `ModifierKeys`, `DisplayGesture`, `IsCustomized`, `Matches(Key, ModifierKeys)` 메서드 제공.

### Step 2: 중앙 단축키 관리 서비스 `ShortcutService` 구현 (★★☆☆☆)
- 싱글톤 인스턴스 `ShortcutService.Instance`.
- 기본 단축키 테이블 초기화 (모든 기본 키 매핑 등록).
- SQLite DB (`DatabaseService`) 로드 및 저장 (`Shortcut_{ActionId}`).
- `TryHandle(KeyEventArgs e, ShortcutScope scope, Action<ShortcutAction> executeCallback)` 메서드 제공.
- 기본값 복원 (`ResetAll()`, `ResetAction(ShortcutAction action)`).

### Step 3: `MainWindowViewModel` 및 `MainWindow.xaml.cs` 전역 단축키 연동 (★★☆☆☆)
- `MainWindow.PreviewKeyDown`에서 `ShortcutService.Instance.TryHandle(e, ShortcutScope.Global, ...)` 호출.
- `F1` 키 입력 시 `OpenHelpCommand` 즉시 실행 보장.
- `F5`, `Ctrl+N`, `Ctrl+E` 등 전역 단축키 일원화.

### Step 4: `ResultGridView.xaml.cs` 결과 목록 단축키 리팩토링 (★★☆☆☆)
- `ResultsListView_KeyDown` 내 거대 `if-else` 분기를 `ShortcutService.Instance.TryHandle(e, ShortcutScope.ResultGrid, ...)` 기반 콜백 디스패치로 단순화.
- 이름변경 모드(`_viewState == ViewState.Editing`)와의 상호작용 격리 유지.

### Step 5: 빌드, 단위 테스트 및 자산화 (★☆☆☆☆)
- 프로젝트 컴파일 및 전체 테스트 수행.
- `MEMORY.md` 및 `overview.md` 업데이트.

---

## 8. Verification Plan (검증 계획)

1. **컴파일 검증**:
   - `dotnet build src/EverythingFastAlias/EverythingFastAlias.csproj -c Debug` 성공 확인 (오류 0, 경고 0).
2. **단위 테스트 검증**:
   - `dotnet test src/EverythingFastAlias.Tests/EverythingFastAlias.Tests.csproj` 전체 통과 확인.
3. **단축키 동작 시나리오 검증**:
   - [ ] 검색 입력창에 포커스가 있는 상태에서 `F1` 누름 ➔ 도움말 창 즉시 팝업.
   - [ ] 검색 결과 항목 선택 상태에서 `Shift+Enter` 누름 ➔ 탐색기에서 파일 위치 열림.
   - [ ] 검색 결과 항목 선택 상태에서 `Shift+Delete` 누름 ➔ 영구 삭제 확인창 팝업.
   - [ ] 검색 결과 항목 선택 상태에서 `Alt+Enter` 누름 ➔ 윈도우 속성창 팝업.
   - [ ] 검색 결과 항목 선택 상태에서 `F2` 누름 ➔ 인라인 이름 변경 진입.
   - [ ] 검색 결과 항목 선택 상태에서 `F5` 누름 ➔ 검색 새로고침 실행.

---

## 9. Capitalization Plan (자산화 계획)

- `MEMORY.md`: 중앙 집중식 단축키 관리 서비스 아키텍처 및 WPF PreviewKeyDown 디스패칭 패턴 기록.
- `overview.md`: `Models/`, `Services/` 신규 추가 모듈 반영 및 주석 갱신.

---

## 10. Request for Approval (승인 요청)

- **95% 확실성 근거**:
  - 파편화된 단축키 로직의 위치 및 WPF `F1` 키 미동작 원인을 완벽히 규명하였습니다.
  - 향후 단축키 설정 UI 연동을 완벽히 수용할 수 있도록 `ShortcutItem`, `ShortcutAction`, `ShortcutService` 3계층 아키텍처를 설계하였습니다.
- 본 계획서에 따라 작업을 진행해도 좋을지 검토 및 승인을 요청드립니다.
