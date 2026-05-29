# Everything FastAlias 구현 계획서 (WPF 전환 및 누락 기능 보완)

이 문서는 C# WPF 데스크톱 어플리케이션 환경에서 원본 `요구사항명세서.md` 및 `UI명세서.md`를 바뀐 스택에 맞추어 해석하고, 현재 구현된 코드에서 누락된 세부 기능들을 보완하기 위해 수립한 구현 계획서입니다.

---

## User Review Required

> [!IMPORTANT]
> **Windows Forms 혼용 활성화 검토**: WPF 프로젝트에서 시스템 트레이 아이콘(`NotifyIcon`) 기능을 손쉽게 사용하기 위해 `.csproj`에 Windows Forms 혼용 설정(`<UseWindowsForms>true</UseWindowsForms>`)을 활성화하고자 합니다.

> [!WARNING]
> **시작프로그램 권한 범위**: 사용자 계정 컨트롤(UAC) 관리자 권한 요구를 최소화하고 안전하게 구동되도록 시작프로그램을 로컬 컴퓨터 레지스트리(`HKLM`) 대신 현재 사용자 레지스트리(`HKCU`) 수준에 등록할 예정입니다.

> [!TIP]
> **Slate-50 클린 라이트 테마 브러시 재정의**: UI명세서의 요구사항인 "Slate-50 기반 클린 라이트 테마" 구현을 위해 ModernWpfUI의 기본 윈도우 배경색 브러시 리소스를 `App.xaml` 단에서 Slate-50 컬러(Hex `#F8FAFC` 및 관련 밝은 Slate 톤)로 강제 재정의(Override)하여 테마에 녹여낼 계획입니다.

## Open Questions

> [!IMPORTANT]
> **질문 1**: WPF 트레이 아이콘 구현 시 .NET Windows Forms의 검증된 `System.Windows.Forms.NotifyIcon`을 연동하기 위해 `.csproj` 설정을 조정할 계획입니다. 이에 동의하시는지요? (동의하지 않으실 경우, Win32 DLL FFI `Shell_NotifyIcon` API 호출을 직접 구현하여 연동해야 하므로 복잡성이 올라갈 수 있습니다.)
> 
> **질문 2**: 시작프로그램 등록은 사용자 환경에 가장 안전한 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 경로에 쓰는 방식으로 충분할까요?

---

## Requirements

1. **[추가] 메뉴바 기능**:
   - `내보내기(TXT)`: 검색 결과를 텍스트 형식(`.txt`)으로 포맷팅하여 저장.
   - `시스템 트레이 ON/OFF`: 창 닫기 시 트레이로 숨기고, 더블클릭 시 메인 윈도우 복원.
   - `시작프로그램 등록 ON/OFF`: 윈도우 시작 시 자동 실행되도록 레지스트리 등록/해제.
   - `새창 띄우기`: 독립적인 메인 윈도우 인스턴스를 동적으로 생성 및 다중 활성화.
2. **[추가] 검색창 FastAlias 상태 인디케이터**:
   - 검색창 우측에 FastAlias 토글 스위치의 ON/OFF 상태를 시각화하는 인디케이터 및 툴팁 표기.
3. **[추가] 결과 테이블 헤더 정렬**:
   - `GridViewColumnHeader` 클릭 시 이름, 경로, 수정일, 크기, 확장자 기준 오름차순/내림차순 정렬 처리 및 ▲/▼ 인디케이터 헤더 텍스트 표시.
4. **[추가] 하단 StatusBar 선택 항목 카운터**:
   - 리스트뷰 선택 이벤트 발생 시 "선택 항목: X / 검색 결과: Y개 항목" 실시간 출력.
5. **[추가] Slate-50 클린 라이트 테마 적용**:
   - `App.xaml`에서 ModernWpf 기본 리소스 브러시를 오버라이드하여 Slate-50 톤 색감 적용.

## Tech Stack

- **Framework**: WPF, .NET 9.0, ModernWpfUI
- **Libraries**: CommunityToolkit.Mvvm, Microsoft.Data.Sqlite, ExcelDataReader
- **APIs**: Microsoft.Win32.Registry (시작프로그램), System.Windows.Forms.NotifyIcon (시스템 트레이)

## Folder Structure

```text
D:\3_Code\3_Apps\43_Search-Edit\Everything검색기\src\EverythingFastAlias\
├── App.xaml
├── App.xaml.cs
├── EverythingFastAlias.csproj
├── Models/
│   ├── SearchResultItem.cs
│   ├── SearchOptions.cs
│   └── AliasMapping.cs
├── ViewModels/
│   ├── MainWindowViewModel.cs
│   ├── SearchViewModel.cs
│   └── AliasManagerViewModel.cs
├── Views/
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── LeftSidebarView.xaml
│   ├── LeftSidebarView.xaml.cs
│   ├── ResultGridView.xaml
│   └── ResultGridView.xaml.cs
├── Native/
│   ├── EverythingSdk.cs
│   ├── EverythingBridge.cs
│   ├── Win32ClipboardHelper.cs
│   ├── ShellContextMenu.cs
│   └── TrayIconHelper.cs           [NEW]
└── Services/
    ├── QueryTransformer.cs
    ├── ExcelService.cs
    ├── DatabaseService.cs
    └── AutoStartService.cs         [NEW]
```

## Search/Retrieval Tools

- **grep_search**: 프로젝트 전반의 클래스 구조와 바인딩 속성 검색에 사용.
- **view_file**: 세부 파일 변경 내용 확인 및 수정 위치 식별에 사용.

## Verification Tools

- **dotnet build**: WPF 소스 빌드 무결성 검증.
- **dotnet test**: MSTest 단위 테스트 스크립트 실행 및 결과 검증.

## Proposed Changes

### [EverythingFastAlias Component]

#### [MODIFY] [EverythingFastAlias.csproj](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/EverythingFastAlias.csproj)
- `<UseWindowsForms>true</UseWindowsForms>` 속성을 `<PropertyGroup>`에 추가하여 트레이 아이콘 클래스 사용성 확보.

#### [NEW] [AutoStartService.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Services/AutoStartService.cs)
- 시작프로그램 레지스트리(`HKCU`) 등록 및 해제, 현재 활성화 여부를 점검하는 로직 추가.

#### [NEW] [TrayIconHelper.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Native/TrayIconHelper.cs)
- `NotifyIcon` 개체 생성, 아이콘 리소스 바인딩, 더블클릭 이벤트 복원 및 우클릭 컨텍스트 메뉴(열기/종료) 바인딩 구현.

#### [MODIFY] [MainWindowViewModel.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/ViewModels/MainWindowViewModel.cs)
- 내보내기(`ExportResultsCommand`), 새 창(`NewWindowCommand`), 시스템 트레이 설정 및 시작프로그램 설정 토글 바인딩 속성 구현.

#### [MODIFY] [SearchViewModel.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/ViewModels/SearchViewModel.cs)
- 정렬 기준 컬럼 및 정렬 방향을 다루는 `SortResults` 메서드 구현.
- 결과 리스트 뷰의 선택 항목 카운팅을 관리하는 `SelectedCount` 연동 프로퍼티 추가.

#### [MODIFY] [App.xaml](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/App.xaml)
- Slate-50 클린 라이트 테마 구현을 위한 컬러 브러시 키 재정의 추가:
  ```xml
  <SolidColorBrush x:Key="SystemControlPageBackgroundChromeLowBrush" Color="#F8FAFC"/>
  <SolidColorBrush x:Key="SystemControlBackgroundAltHighBrush" Color="#FFFFFF"/>
  <SolidColorBrush x:Key="SystemControlElevationBorderBrush" Color="#E2E8F0"/>
  ```

#### [MODIFY] [MainWindow.xaml](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Views/MainWindow.xaml)
- 메뉴바 하위 메뉴 추가: 내보내기, 시작프로그램 자동 실행 토글, 트레이 최소화 토글.
- 검색 입력 텍스트박스 우측에 FastAlias 켜짐/꺼짐 상태 인디케이터 전구/체크 아이콘 UI 추가.

#### [MODIFY] [MainWindow.xaml.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Views/MainWindow.xaml.cs)
- `Closing` 이벤트 리디렉션 구현 (트레이 설정 활성화 시 종료 대신 `Hide()`).
- `TrayIconHelper` 생명주기 관리 로직 연동.

#### [MODIFY] [ResultGridView.xaml](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Views/ResultGridView.xaml)
- `ListView` 헤더 클릭(`GridViewColumnHeader.Click`) 바인딩 및 `SelectionChanged` 핸들러 등록.

#### [MODIFY] [ResultGridView.xaml.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Views/ResultGridView.xaml.cs)
- `GridViewColumnHeader_Click` 구현: 클릭된 열의 헤더 컬럼 이름을 뷰모델로 전달하여 정렬 호출.
- `ResultsListView_SelectionChanged` 구현: 현재 다중 선택된 카운트를 세어 `SearchVM.SelectedCount`로 전달.

---

## Verification Plan

### Automated Tests
- `dotnet test` 명령을 활용하여 단위 테스트 프로젝트의 문법 변환기가 정상 동작하는지 테스트 스위트 확인.

### Manual Verification
- **정렬**: 각 헤더 클릭 시 정상 정렬되는지 UI 리스트 확인.
- **트레이**: 트레이 최소화 설정 활성화 후 창 닫기 시 트레이 아이콘으로 축소되며 복원 시 정상 윈도우 복구 확인.
- **내보내기**: 내보내기 완료 후 생성된 `.txt` 파일의 경로/구조 정합성 수동 검사.
- **시작프로그램**: 레지스트리 편집기(`regedit`)를 기동하여 `HKCU` 해당 경로에 앱 항목이 추가/제거되는지 확인.

---

## Assetization Plan

- **AGENTS.md 업데이트**: WPF 리소스 경로 이슈 외에 추가적으로 시작프로그램 등록 및 트레이 아이콘 생성과 관련된 Windows 데스크톱 네이티브 FFI/API 설계 노하우 누적.
- **overview.md 갱신**: 신규 추가된 자동 실행 관리 기능 및 시스템 트레이 아키텍처에 대한 흐름 다이어그램 추가.
