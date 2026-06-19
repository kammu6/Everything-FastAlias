# 구현 계획서: 외부 파일 드롭 시 내부 폴더 복사/이동 (Drop-in) 연동

본 계획서는 외부 탐색기(윈도우 탐색기, Directory Opus 등)에서 파일 및 폴더를 마우스 드래그하여 앱 내 검색 결과의 특정 폴더(Folder) 행 위에 드롭(Drop)했을 때, 해당 폴더 경로로 파일들을 복사 또는 이동시키는 기능을 구현하기 위한 명세서입니다.

---

## 1. 요구사항 (Requirements)

1. **폴더 타겟 드롭 기능 (Drop-in)**:
   - 외부에서 드래그 중인 파일 객체(`DataFormats.FileDrop`)를 리스트뷰의 특정 행 위에 올렸을 때, 마우스 하위의 행이 폴더(`IsFolder == true`)인 경우에만 드롭을 활성화합니다.
   - 폴더가 아닌 빈 공간이거나 파일 행 위인 경우 드롭 효과를 불허(`DragDropEffects.None`)합니다.
2. **윈도우 탐색기 표준 동작 결정**:
   - `Ctrl` 키가 눌려있으면 **복사(Copy)**, `Shift` 키가 눌려있으면 **이동(Move)** 효과를 적용합니다.
   - 키보드가 제어되지 않은 일반 드롭 시: 원본 파일과 대상 폴더의 드라이브 루트(예: `C:\` vs `D:\`)를 대조하여 같으면 **이동(Move)**, 다르면 **복사(Copy)**를 기본값으로 결정합니다.
3. **윈도우 표준 파일 전송 대화상자 노출**:
   - 복사/이동 작업 실행 시 Win32 쉘 API인 `SHFileOperation`을 연동합니다.
   - 이를 통해 윈도우 OS 표준 전송 진행률 창(Progress Dialog)이 표시되고, 중복 파일 충돌 처리 대화상자가 동일하게 나타나 신뢰성을 극대화합니다.
   - 파일 작업 후 성공 시 실시간 검색 질의를 다시 실행하여 파일 현황을 최신화합니다.

---

## 2. 기술 스택 (Tech Stack)

- **언어 및 프레임워크**: C# .NET 9.0 (WPF)
- **네이티브 FFI**: Win32 Shell API (`SHFileOperation` in `shell32.dll`)
- **디자인 패턴**: MVVM, SoC (관심사 분리) 및 DRY (중복 제거) 준수

---

## 3. 폴더 및 파일 변경 구조 (Folder Structure)

본 구현은 기존의 관심사 분리 원칙과 하나의 클래스는 하나의 파일에 둔다는 원칙을 철저히 준수합니다.

```text
d:\3_Code\3_Apps\43_Search-Edit\Everything검색기\
├── docs/
│   └── memories/
│       └── MEMORY.md
│   └── implementation_plan.md     # [MODIFY] 본 계획서 (IsArtifact: false)
└── src/
    └── EverythingFastAlias/
        ├── Native/
        │   └── Win32FileOperationHelper.cs # [NEW] SHFileOperation 기반 복사/이동 유틸리티 클래스
        └── Views/
            ├── ResultGridView.xaml # [MODIFY] ListView에 AllowDrop="True", DragOver 및 Drop 이벤트 바인딩
            └── ResultGridView.xaml.cs # [MODIFY] DragOver, Drop 이벤트 핸들러 및 윈도우 표준 드롭 효과 탐색기 판단 로직 구현
```

---

## 4. 정보 조회 및 검증 도구 (Lookup & Verification Tools)

- **조회 도구 (Lookup Tools)**: 
  - `yik-parser` 분석 보고서를 통해 `ResultGridView.xaml.cs`의 상태 머신(`_viewState`) 및 visual 트리 탐색 헬퍼(`FindVisualParent`)의 명세를 확인 완료.
  - `Win32RecycleBinHelper.cs` 조회를 통해 `SHFileOperation` 및 `SHFILEOPSTRUCT` API 구조 및 속성을 분석 완료.
- **검증 도구 (Verification Tools)**:
  - 디버그 빌드 스크립트: `cmd /c build-debug.bat --non-interactive`로 검증.
  - 런타임 수동 동작 테스트를 통해 외부 탐색기 파일 복사/이동 수동 체크.

---

## 5. 상세 구현 계획 (Implementation Plan)

### 단계 1: 파일 복사/이동 헬퍼 클래스 개발 (`Win32FileOperationHelper.cs` [NEW])
1. `EverythingFastAlias.Native` 네임스페이스 아래 `Win32FileOperationHelper` 클래스를 생성합니다.
2. `SHFILEOPSTRUCT` 구조체 및 `SHFileOperation` Win32 API를 P/Invoke 선언합니다.
3. `CopyOrMoveFiles` 유틸리티 메소드를 구현합니다:
   - 복사/이동에 적합한 함수 플래그(`FO_COPY`, `FO_MOVE`)를 엮습니다.
   - 원본 경로 목록(`sourcePaths`)과 대상 경로(`targetFolderPath`)를 Double-null 종단(`\0\0`) 규격의 StringBuilder로 마샬링하여 `SHFileOperation`에 바인딩합니다.
   - `fFlags`에 `FOF_ALLOWUNDO`를 전달해 윈도우 전체 되돌리기(Ctrl+Z) 환경을 보장합니다.

### 단계 2: ListView 드롭 이벤트 바인딩 (`ResultGridView.xaml` [MODIFY])
1. `ResultsListView` 요소에 `AllowDrop="True"` 특성을 추가합니다.
2. `DragOver="ResultsListView_DragOver"`와 `Drop="ResultsListView_Drop"` 이벤트를 선언해 비하인드 코드에 연결합니다.

### 단계 3: 드래그 오버 및 드롭 비하인드 처리 (`ResultGridView.xaml.cs` [MODIFY])
1. `ResultsListView_DragOver` 이벤트 핸들러 구현:
   - 상태 머신 `_viewState`가 `Idle`일 때만 드래그 앤 드롭 접수.
   - `FileDrop` 포맷 데이터의 존재성을 확인.
   - `VisualTreeHelper.HitTest`를 통해 현재 마우스 포인트 아래에 위치한 `ListViewItem`을 가로채고, 이것이 폴더(`targetItem.IsFolder == true`)인지 식별.
   - `GetDragDropEffect` 판단 로직을 거쳐 탐색기 표준 효과(Copy/Move)를 `e.Effects`에 부여.
2. `ResultsListView_Drop` 이벤트 핸들러 구현:
   - 드롭 위치 아래에 위치한 대상 폴더 경로를 획득.
   - `Win32FileOperationHelper.CopyOrMoveFiles`를 비동기(`Dispatcher.BeginInvoke`)로 호출하여 UI 스레드 락 없이 파일 전송을 실행.
   - 파일 작업이 정상 완료된 경우 ViewModel에 재조회(`searchVM.ExecuteSearch()`) 명령을 내려 갱신 유도.
3. `GetDragDropEffect` 헬퍼 메소드 구현:
   - 키보드의 Shift/Ctrl 결합 조합을 식별해 복사/이동 효과 우선 부여.
   - 키보드가 미압박된 경우, 첫 번째 드래그 파일의 루트 경로 드라이브와 대상 폴더의 드라이브 문자를 떼어내 비교 분석 후 Copy/Move 기본 사양 결정.

---

## 6. 검증 계획 (Verification Plan)

### 수동 검증 및 시나리오 테스트
1. 임의의 폴더 항목이 검색되는 검색어를 메인 창에 쳐서 폴더 행을 확보합니다.
2. 윈도우 탐색기 또는 Directory Opus를 띄워, 임의의 테스트용 텍스트 파일을 마우스로 집어 앱의 폴더 행 위로 끌어다 놓습니다(드래그).
3. **효과 검증 (DragOver)**:
   - 마우스가 폴더가 아닌 빈 곳에 있을 때는 금지 모양이 뜨는지 검사.
   - 폴더 행 위에 닿았을 때 마우스 포인터에 [+] 또는 복사/이동 아이콘이 활성화되는지 검사.
4. **키 조합 및 드라이브 효과 검증**:
   - `Ctrl` 키를 누르면 화면에 [+] (복사) 효과가 뜨는지 확인.
   - `Shift` 키를 누르면 이동 효과가 뜨는지 확인.
   - 같은 드라이브 내 다른 폴더에 드롭할 때 이동이 되고, 타 드라이브(예: C:에서 USB 드라이브 등)로 올릴 때 복사가 기본으로 도출되는지 확인.
5. **표준 전송창 및 갱신 검증**:
   - 드롭 시 윈도우 표준 파일 전송 대화상자(복사 진행 상태)가 뜨는지 확인.
   - 전송 성공 후 검색 결과 뷰에 복사/이동된 최신 파일 정보가 자동으로 노출되는지(실시간 갱신 여부) 확인.
