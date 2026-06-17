# 구현 계획서: Alias Manager 사용자 편의성 고도화 및 편집 자동화

본 계획서는 스마트 매핑 사전 관리자(Alias Manager)에서 새로운 동의어 규칙 추가 시, 작업 단계를 극소화하여 사용자 피로도를 낮추고 데이터 편집 편의성을 탐색기 수준으로 고도화하기 위한 설계서입니다.

---

## 1. 요구사항 (Requirements)

1. **행 추가 위치 상단화 (Index 0)**:
   - "행 추가 (+)" 버튼 클릭 시, 신규 행이 리스트의 맨 끝이 아닌 **가장 최상단(첫 번째 행, Index 0)**에 임시 추가되도록 구현.
   - 행 추가 후 스크롤을 맨 아래로 내릴 필요 없이 바로 편집에 집중할 수 있도록 유도.
2. **원본 키워드(1열) 자동 편집 및 텍스트 전체 선택**:
   - 행 추가 버튼 클릭 시, 원본 키워드(Keyword) 셀이 자동으로 **편집 모드(Cell Edit Mode)**로 진입하며 포커스를 획득하고, 디폴트 프리셋 값("새키워드")이 **전체 선택(SelectAll)** 처리되어 깜박이도록 구현.
   - 마우스 추가 클릭 없이 바로 키보드 타이핑만으로 프리셋을 덮어쓸 수 있도록 보완.
3. **동의어 목록(2열) 텍스트 자동 전체 선택**:
   - 원본 키워드 입력을 마치고 동의어 셀(Words)로 포커스를 이동해 편집 모드를 켤 때도, 프리셋 값("동의어1;동의어2" 등)이 **자동으로 전체 선택** 처리되어 깜박이도록 구현.
4. **저장 시 오름차순 정렬 뷰 유지 및 선택 상태 보존**:
   - 최상단에 임시 추가된 행은 데이터 편집 후 "수정사항 적용 (저장)" 버튼 클릭 시, 데이터베이스에 오름차순 정렬되어 영구 저장됨.
   - 저장 완료 직후 리스트를 오름차순 상태로 즉시 재로드(`LoadMappings`)하여 뷰가 자동 갱신되도록 동기화.
   - 이때 저장하기 전 선택되어 있던 키워드를 기억하여, 리프레시 후에도 해당 키워드로 **선택(SelectedMapping) 및 스크롤 포커스(ScrollIntoView)**를 복원하여 편집 연속성을 향상함.

---

## 2. 기술 스택 (Tech Stack)

- **언어 및 프레임워크**: C# .NET 9.0 (WPF)
- **UI 컨트롤**: WPF `DataGrid`, `TextBox`
- **MVVM 패턴**: `CommunityToolkit.Mvvm` (RelayCommand, ObservableObject)

---

## 3. 폴더 및 파일 변경 구조 (Folder Structure)

본 구현은 기존의 `MVVM 패턴`, `SoC(관심사 분리)`, `DRY`, 및 `One Class One File` 원칙을 철저히 준수합니다.

```text
d:\3_Code\3_Apps\43_Search-Edit\Everything검색기\
├── docs/
│   └── memories/
│       └── MEMORY.md
│   └── implementation_plan.md    # [MODIFY] 본 계획서 (IsArtifact: false)
└── src/
    └── EverythingFastAlias/
        ├── ViewModels/
        │   └── AliasManagerViewModel.cs # [MODIFY] AddCommand 시 Insert(0) 수정, 편집 요청 이벤트 신설, 저장 성공 시 로드 및 선택 행 복원 호출
        └── Views/
            └── Modals/
                ├── AliasManagerWindow.xaml # [MODIFY] 1열 및 2열 TextBox.Loaded 및 GotFocus 이벤트 핸들러 바인딩
                └── AliasManagerWindow.xaml.cs # [MODIFY] ViewModel의 편집 요청 수신 및 DataGrid.BeginEdit 트리거, Loaded/GotFocus 핸들러 구현
```

---

## 4. 정보 조회 및 검증 도구 (Lookup & Verification Tools)

- **조회 도구 (Lookup Tools)**: 
  - `view_file`를 이용해 Alias Manager 윈도우와 뷰모델의 구조 및 데이터 바인딩 명세를 분석 완료.
- **검증 도구 (Verification Tools)**:
  - 디버그 빌드 스크립트: `cmd /c build-debug.bat --non-interactive`를 실행하여 컴파일 무결성 검증.
  - 런타임 동작 테스트를 통해 행 추가 및 텍스트 선택 UX 수동 체크.

---

## 5. 상세 구현 계획 (Implementation Plan)

### 단계 1: 뷰모델 편집 이벤트 신설 및 로직 변경 (`AliasManagerViewModel.cs` [MODIFY])
1. `RequestEditMapping` 이벤트 신설:
   - `public event Action<AliasMapping>? RequestEditMapping;`
2. `AddMapping()` 메소드 수정:
   - `Mappings.Add(newMapping)` -> `Mappings.Insert(0, newMapping)` 으로 수정하여 가장 상단에 추가.
   - 추가 즉시 `RequestEditMapping?.Invoke(newMapping);`을 발화해 뷰가 편집 모드로 진입하도록 유도.
3. `SaveSelectedMapping()` 메소드 수정:
   - 저장 실행 전 `SelectedMapping?.Keyword`를 변수 `selectedKeyword`에 임시 확보.
   - 저장 쿼리(`DatabaseService.Instance.SaveAllSync`) 실행 성공 완료 직후, `LoadMappings()`를 호출해 데이터베이스 상의 정렬된 최신 상태를 리스트에 재로드 및 리프레시.
   - 재로드 완료 후 `selectedKeyword`와 일치하는 키워드를 가진 매핑 행을 탐색하여 `SelectedMapping`으로 다시 지정하고 `RequestScrollIntoView` 이벤트를 트리거함.

### 단계 2: XAML 텍스트박스 Loaded 및 GotFocus 이벤트 바인딩 (`AliasManagerWindow.xaml` [MODIFY])
1. 1열(Keyword) `CellEditingTemplate` 내 TextBox 선언에 `Loaded="TextBox_Loaded" GotFocus="TextBox_GotFocus"` 등록.
2. 2열(Words) `CellEditingTemplate` 내 TextBox 선언에 `Loaded="TextBox_Loaded" GotFocus="TextBox_GotFocus"` 등록.

### 단계 3: 비하인드 코드 DataGrid 편집 진입 및 TextBox 전체 선택 처리 (`AliasManagerWindow.xaml.cs` [MODIFY])
1. `AliasManagerWindow_Loaded` 시점에 뷰모델의 `RequestEditMapping` 이벤트를 추가 구독.
2. `RequestEditMapping` 이벤트 핸들러(`Vm_RequestEditMapping`) 구현:
   - `MappingDataGrid.UpdateLayout();`을 먼저 수행하여 신규 행 렌더링 컨테이너 확보.
   - `MappingDataGrid.Focus();`로 포커스를 획득.
   - 첫 번째 행의 첫 번째 컬럼 정보를 `DataGridCellInfo`로 구성하여 `CurrentCell`에 지정.
   - `MappingDataGrid.BeginEdit();`을 비동기(`Dispatcher`)로 실행하여 셀 편집 박스(TextBox)를 즉시 활성화.
3. `TextBox_Loaded` 및 `TextBox_GotFocus` 이벤트 핸들러 구현:
   - 1열 및 2열의 편집용 TextBox가 화면에 렌더링되어 로드될 때, 그리고 마우스 또는 키보드 포커스를 획득할 때 작동.
   - `tb.Focus();` 및 `tb.SelectAll();`을 `DispatcherPriority.Input` 지연 처리로 기동하여, 편집 모드로 진입할 때마다 프리셋 값이 전체 블록 지정되어 깜박이도록 유도.

---

## 6. 검증 계획 (Verification Plan)

### 수동 검증 및 시나리오 테스트
1. 스마트 매핑 사전 관리자 기동 후 하단의 "행 추가 (+)" 버튼을 클릭.
2. 추가된 행이 맨 위(1행)에 표시됨과 동시에 첫 번째 열인 원본 키워드 란에 포커스가 주어지고 "새키워드" 텍스트가 전체 선택되어 깜박이는지 확인.
3. 마우스 클릭 없이 키보드로 바로 타이핑하여 원본 키워드가 정상 입력되는지 확인.
4. Tab 키 또는 마우스 클릭으로 2번째 열인 Words(동의어) 셀로 진입했을 때, 디폴트 값인 "동의어1;동의어2"가 전체 선택되어 깜박이는지 확인.
5. "수정사항 적용(저장)"을 눌렀을 때, 임시로 상단에 있던 새 행이 데이터베이스에 저장됨과 동시에 가나다/알파벳 순서에 맞게 오름차순으로 정렬되어 자동 뷰 리프레시가 정상 수행되는지 검증.
6. 또한 저장 완료 후 리프레시가 진행되었을 때 방금 추가한 키워드로 선택 바와 스크롤이 자동으로 따라가 정렬된 올바른 위치를 가리키고 있는지 확인.
