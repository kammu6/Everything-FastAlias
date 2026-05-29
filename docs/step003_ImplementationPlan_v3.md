# Everything FastAlias 구현 계획서 (5대 유저 피드백 반영 및 버그 수정)

이 문서는 C# WPF 데스크톱 어플리케이션 환경에서 유저로부터 접수된 5가지 버그 및 기능 개선 요청 사항에 대하여, 분석된 내용을 토대로 한 구체적인 수정 및 보완 계획을 담고 있습니다.

---

## User Review Required

> [!IMPORTANT]
> **P/Invoke bool 마샬링 문제 및 수정한 날짜/크기 미노출 해결**:
> `everything64.dll` 연동 시 `Everything_IsFolderResult` 등의 C++ `BOOL` 리턴값을 C# `bool`로만 마샬링하면 1바이트와 4바이트의 불일치로 인해 런타임에 항상 `true`로 해석되어 모든 검색 항목이 폴더로 오판(크기 `<DIR>` 고정 및 확장자 공백 현상)되었습니다.
> P/Invoke 서명에 `[return: MarshalAs(UnmanagedType.Bool)]` 어트리뷰트를 강제 지정하여 이 문제를 원천 해결합니다.

> [!WARNING]
> **옵션패널 접기 UI 컨트롤 정의**:
> 사이드패널 접기 토글 상태에 따라 WPF Grid의 `ColumnDefinition.Width`와 `MinWidth`를 비하인드 코드에서 동적으로 제어(접기 시 0, 열기 시 320)하고, 내부 컨트롤들을 `Collapsed` 처리하여 리사이저 스플리터와 함께 완벽히 화면에서 제거 및 복원할 수 있도록 설계합니다.

---

## Open Questions

> [!NOTE]
> **CSV 가져오기 인코딩 사양**:
> ExcelDataReader는 UTF-8 BOM이 없는 Csv 파일 로딩 시 인코딩 문제로 예외가 발생할 수 있습니다.
> Csv 로딩 시 `FallbackEncoding = Encoding.UTF8` 설정을 부여하여 CSV 임포트 시의 `HeaderException`을 완전히 방지하고자 합니다.

---

## Requirements

1. **[버그수정] CSV 파일 임포트 예외 해결**:
   - `ExcelService.ImportExcel`에서 CSV 파일 로딩 시 `ExcelReaderConfiguration`의 `FallbackEncoding`을 `Encoding.UTF8`로 명시하여 `HeaderException`을 해결합니다.
2. **[UI개선] 한글 텍스트 및 버튼 폰트/패딩 축소**:
   - 검색창 및 제외단어/직계경로 입력란의 폰트 사이즈를 `13` 내외로 소폭 축소합니다.
   - 좌측 사이드바 컨트롤들(CheckBox, RadioButton, ToggleButton)에 `FontSize="11"` 및 적정한 패딩(`Padding="4,2,4,2"`)을 명시하여 글씨가 잘리는 현상을 해결합니다.
3. **[버그수정/UI개선] 하단바 여백 및 검색 제한 해제**:
   - StatusBar 내에 가변 Grid를 바인딩하는 대신 `ItemsPanelTemplate`과 Grid Column을 활용하여 좌우에 고정 정렬하고, 마진 여백을 주어 "개" 짤림 현상을 해결합니다.
   - `AppConstants.cs`의 `DefaultMaxResults` 값을 `0`에서 무제한(EVERYTHING_MAX_ALL)을 뜻하는 `0xFFFFFFFF` (`uint.MaxValue`)로 변경하여 검색 한도를 완전히 해제합니다.
4. **[버그수정] 수정한 날짜 및 크기 정보 정상화**:
   - `EverythingSdk.cs`의 P/Invoke 시그니처(`IsFolderResult`, `GetResultSize`, `GetResultDateModified`, `QueryW` 등)에 `[return: MarshalAs(UnmanagedType.Bool)]` 어트리뷰트를 부여하여 데이터 왜곡 현상을 해결합니다.
5. **[기능추가] 옵션패널 ON/OFF 토글 및 접기**:
   - 상단 FastAlias 스위치 바로 밑에 `옵션패널` ON/OFF 토글 스위치를 추가합니다.
   - 비하인드 코드에서 토글 이벤트를 구독하여 좌측 사이드바 Column(MinWidth 및 Width), GridSplitter 및 LeftSidebarView의 가시성(`Visibility`)을 토글 상태에 따라 조절합니다.

---

## Tech Stack

- **Framework**: WPF, .NET 9.0, ModernWpfUI
- **Libraries**: CommunityToolkit.Mvvm, ExcelDataReader
- **APIs**: Windows Native Everything SDK (`everything64.dll` FFI)

---

## Folder Structure

```text
D:\3_Code\3_Apps\43_Search-Edit\Everything검색기\src\EverythingFastAlias\
├── App.xaml
├── App.xaml.cs
├── EverythingFastAlias.csproj
├── Config/
│   └── AppConstants.cs         [MODIFY] (DefaultMaxResults 변경)
├── Models/
│   └── SearchResultItem.cs
├── Native/
│   ├── EverythingSdk.cs        [MODIFY] (MarshalAs 어트리뷰트 및 GetResultListRequestFlags 선언 추가)
│   └── EverythingBridge.cs
├── Services/
│   └── ExcelService.cs         [MODIFY] (Csv FallbackEncoding 설정 추가)
├── ViewModels/
│   └── SearchViewModel.cs
└── Views/
    ├── MainWindow.xaml         [MODIFY] (옵션패널 토글 및 접기용 Grid Column 이름 바인딩, StatusBar 구조 변경)
    ├── MainWindow.xaml.cs      [MODIFY] (옵션패널 ON/OFF 비하인드 제어 핸들러 추가)
    └── LeftSidebarView.xaml    [MODIFY] (체크박스/토글/라디오 버튼 폰트 축소 및 패딩 조절)
```

---

## Search/Retrieval Tools

- **view_file**: 대상 파일의 구조 및 구체적 변경 라인 확인.
- **grep_search**: 참조 위치 분석.

---

## Verification Tools

- **dotnet build**: WPF 소스 코드 컴파일 성공 여부 검증.
- **dotnet test**: 작성된 SDK 네이티브 호출 테스트 및 가공 정합성 테스트 통과 여부 검증.

---

## Implementation Plan

### 1. CSV 가져오기 & 검색 한도 해제
- `ExcelService.ImportExcel`에서 `ExcelReaderFactory.CreateCsvReader(stream, new ExcelReaderConfiguration { FallbackEncoding = Encoding.UTF8 })` 구문으로 분기 적용.
- `AppConstants.cs`에서 `DefaultMaxResults`를 `0xFFFFFFFF` 로 교체.

### 2. Everything SDK 마샬링 적용
- `EverythingSdk.cs` 파일 내의 `BOOL` 리턴값을 갖는 P/Invoke 메서드들에 `[return: MarshalAs(UnmanagedType.Bool)]` 선언 추가.

### 3. 좌측 사이드바 및 UI 폰트 조정
- `LeftSidebarView.xaml` 내 모든 RadioButton, CheckBox, ToggleButton에 `FontSize="11"` 및 패딩 최소화 설정 적용.
- `MainWindow.xaml`에서 검색창 `FontSize`를 `14`로 소폭 내리고, 제외단어 및 직계경로 텍스트박스에도 `FontSize="11"`을 지정.

### 4. 하단 StatusBar 개선
- `MainWindow.xaml`의 StatusBar 레이아웃 구조를 `ItemsPanelTemplate` 기반 Grid로 개편하여 우측 문자 짤림 현상 방지 및 마진 15px 확보.

### 5. 옵션패널 ON/OFF 토글 접기 구현
- `MainWindow.xaml`에 `SidebarColumn`, `SidebarSplitter`, `SidebarView` 이름을 할당하고 옵션패널 토글 배치.
- `MainWindow.xaml.cs`에 `SidebarToggleSwitch_Toggled` 추가하여 ON 시 320 폭 복원, OFF 시 0 폭 및 Collapsed 제어.

---

## Verification Plan

### Automated Tests
- `dotnet test` 명령을 활용하여 단위 테스트 프로젝트가 정상 빌드 및 실행되는지 검증.
- (임시 작성된 SDK 검증 테스트 메서드는 진단 완료 후 제거)

### Manual Verification
- 빌드 완료된 `EverythingFastAlias.exe`를 실행하여:
  1. `.csv` 임포트 성공 메시지 확인.
  2. 한글 글자 및 버튼 내 텍스트 잘림 현상 없는지 육안 검증.
  3. 검색 시 1000개 이상 항목 노출 및 하단바 "개" 짤림 현상 해결 확인.
  4. 검색 그리드 내 파일들의 "수정한 날짜"와 "크기" 및 "확장자" 정상 표시 검증.
  5. 옵션패널 ON/OFF 토글 시 사이드바 접히고 펴지는 유연한 UI 검증.

---

## Assetization Plan

- **AGENTS.md 및 walkthrough.md 업데이트**:
  - C++ `BOOL` (4바이트) 대 C# `bool` (1바이트) 간 마샬링 어트리뷰트 부재 시 참/거짓 왜곡으로 인한 런타임 오동작 버그 진단 정보를 기록하고, WPF Grid Splitter 레이아웃 동적 Collapsed 제어 기법을 지식화합니다.
