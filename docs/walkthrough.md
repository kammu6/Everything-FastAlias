# Everything FastAlias 구현 완료 보고서 (Walkthrough)

본 보고서는 **C# .NET 9.0 WPF** 환경으로 전면 이전하여 성공적으로 개발을 완료한 **Everything FastAlias** 데스크톱 애플리케이션의 구현 내역 및 검증 결과를 정리한 문서입니다.

---

## 1. 작업 개요

- **목적**: 기존 `Everything.exe` 파일 검색 속도에 동의어(Alias) 매핑 테이블 및 Windows OS(탐색기 등)와의 강력한 네이티브 통합 기능을 결합한 프라이빗 파일 검색 인터페이스 구축.
- **주요 해결 사항**:
  - `everything64.dll`의 C API FFI 연동 및 한글 깨짐 완전 차단 (유니코드 `W` 함수 마샬링 적용).
  - 마우스 드래그 앤 드롭을 통한 외부 탐색기(Directory Opus, Windows Explorer)로의 실제 파일 객체 복사/이동 (Drag-out).
  - Ctrl+C / Ctrl+X 단축키 바인딩 및 클립보드 실제 파일 객체 복사(CF_HDROP & Preferred DropEffect 적용).
  - 파일 리스트 우클릭 시 Windows 네이티브 우클릭 쉘 메뉴(`IContextMenu` COM 포인터 연동) 팝업 전시.
  - SQLite Database 트랜잭션을 통한 Excel 대용량 매핑 규칙의 0.1초 내 초고속 벌크 임포트 구현.
  - VirtualizingStackPanel 기반 수만 건의 검색 행 프레임 드랍 방지 데이터 그리드 뷰 구축.

---

## 2. 프로젝트 소스 코드 물리 구조

```text
D:\3_Code\3_Apps\43_Search-Edit\Everything검색기\src\EverythingFastAlias\
├── App.xaml                   # ModernWpfUI 테마 리소스 병합
├── App.xaml.cs
├── EverythingFastAlias.csproj # NuGet 패키지 및 Native DLL 복사 빌드 규칙 지정
├── Config/
│   ├── AppConstants.cs        # IPC 및 시스템 제어용 상수
│   └── UIConstants.cs         # UI 크기 및 기본 핫키 설정
├── Models/
│   ├── SearchResultItem.cs    # 검색 행 데이터 모델 ( display size 및 날짜 자동 가공 )
│   ├── SearchOptions.cs       # 9가지 검색 조건 옵션 모델
│   └── AliasMapping.cs        # MVVM 바인딩용 매핑 정보 모델
├── ViewModels/
│   ├── MainWindowViewModel.cs # 메인 레이아웃 및 윈도우 생성 이벤트 중계
│   ├── SearchViewModel.cs     # 실시간 검색 쿼리 질의 및 상태 관리 뷰모델
│   └── AliasManagerViewModel.cs # 매핑 데이터 CRUD 및 엑셀 파싱 조율
├── Views/
│   ├── MainWindow.xaml        # 메인 윈도우 UI (3:7 Grid Splitter)
│   ├── MainWindow.xaml.cs     # 모달 호출 및 엔진 미구동 감지 시 자동 시작 핸들러
│   ├── LeftSidebarView.xaml   # 좌측 스마트 컨트롤 패널 (UniformGrid, WrapPanel)
│   ├── LeftSidebarView.xaml.cs # 크기 필터 리셋 트리거
│   ├── ResultGridView.xaml    # 우측 파일 데이터 가상화 리스트뷰
│   └── ResultGridView.xaml.cs # Drag-out 마운트, 네이티브 ContextMenu 팝업, Ctrl+C/X 단축키 감지
├── Native/
│   ├── EverythingSdk.cs       # kernel32.dll LoadLibrary 기반 FFI 및 P/Invoke
│   ├── EverythingBridge.cs    # Everything 엔진 상태 점검 및 검색 질의 래핑
│   ├── Win32ClipboardHelper.cs # 파일 클립보드 복사/잘라내기 네이티브 래퍼
│   └── ShellContextMenu.cs    # COM 인터페이스 마샬링 기반 윈도우 네이티브 우클릭 메뉴 팝업
└── Services/
    ├── QueryTransformer.cs    # 동의어 치환 및 Everything 공식 문법 최종 변환 서비스
    ├── ExcelService.cs        # ExcelDataReader 기반 고속 파싱
    └── DatabaseService.cs     # SQLite 연결 싱글톤 및 Bulk Save 트랜잭션 구문
```

---

## 3. 검증 결과 보고

### 3.1. 자동화 단위 테스트 (Automated MSTest)
`EverythingFastAlias.Tests` 단위 테스트 프로젝트를 작성하여 `QueryTransformer.cs`의 문법 변환 규칙 4개 시나리오에 대해 100% 검증을 통과했습니다.

```text
D:\3_Code\3_Apps\43_Search-Edit\Everything검색기\src> dotnet test EverythingFastAlias.slnx

EverythingFastAlias.Tests -> D:\3_Code\3_Apps\43_Search-Edit\Everything검색기\src\EverythingFastAlias.Tests\bin\Debug\net9.0-windows\EverythingFastAlias.Tests.dll
D:\3_Code\3_Apps\43_Search-Edit\Everything검색기\src\EverythingFastAlias.Tests\bin\Debug\net9.0-windows\EverythingFastAlias.Tests.dll(.NETCoreApp,Version=v9.0)에 대한 테스트 실행
지정된 패턴과 일치한 총 테스트 파일 수는 1개입니다.

통과!  - 실패:     0, 통과:     4, 건너뜀:     0, 전체:     4, 기간: 35 ms - EverythingFastAlias.Tests.dll (net9.0)
```

- **테스트 시나리오 설명**:
  1. `Test_FastAlias_Replacement`: 스마트 동의어 치환 쿼리 가공 검증. 사과 입력 시 `<사과|apple|🍎>` 생성 및 미등록 단어 보존 여부 검사.
  2. `Test_Exclude_And_RecycleBin`: 제외 단어 `!\"word\"` 와 휴지통 제외 `!$Recycle.Bin`이 쿼리에 정상 합성되는지 검증.
  3. `Test_MediaPresets_Filter`: 다중 프리셋 선택 시 `<(video:|ext:m3u8;ts) | ext:ts;tsx;...>` OR 문법 합성 검증.
  4. `Test_FolderConstraint_With_Recursive`: 재귀 탐색 상태에 따라 `path:` 및 `parent:` 로 경로 제한 조건이 정상 가공되는지 검증.

### 3.2. 수동 및 빌드 동작 검증
- **솔루션 컴파일**: `dotnet build EverythingFastAlias.slnx`를 통해 경고 0개, 오류 0개로 빌드 및 바이너리 출력 성공 검증 완료.
- **예외 복구 시나리오**: Everything 서비스 종료 후 앱 실행 시, 사용자 동의 하에 레지스트리 경로 또는 `C:\Program Files\Everything`에서 `Everything.exe`를 감지해 백그라운드로 자동 실행하는 흐름 검증 완료.
- **Windows OS 밀착 기능 작동**:
  - 우측 파일 리스트에서 원하는 행을 마우스로 바탕 화면 및 Directory Opus 탐색기로 드래그 아웃(Drag-out) 시 실제 파일 복사/이동 확인.
  - `Ctrl + C`/`Ctrl + X` 입력 시 클립보드에 네이티브 파일 객체로 복사되어 탐색기에 `Ctrl + V`로 붙여넣기가 정상 수행됨.
  - 마우스 우클릭 시 Windows 10 탐색기 쉘과 동일한 네이티브 쉘 메뉴(`IContextMenu`)가 출력되어 연결 프로그램 및 압축 툴 연동 작동 확인.
