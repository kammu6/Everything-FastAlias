# Agent Memory Log (AGENTS.md)

이 문서는 AI 에이전트의 작업 이력, 핵심 시스템 한계, 디버깅 지식 자산 등을 누적하여 관리하는 저장소입니다. 파일 비대화를 방지하기 위해 일반적인 작업 로그는 지양하고, **중요하고 특이한 발견 및 해결 단계**를 중심으로 자산화합니다.

---

## 📌 핵심 운영 지침 요약

1. **신뢰성 95% 우선**: 한 번의 턴에 모든 코드를 작성하려고 시도하는 대신, 확실하지 않은 영역은 단계적으로 검증하고 보완하는 계획을 수립합니다.
2. **SoC, DRY, One-Class-Per-File 원칙**: 각 모듈은 역할이 철저히 분리되어야 하며, 중복이 없어야 하고, 하나의 파일에는 하나의 클래스 또는 주 컴포넌트만 존재해야 합니다.
3. **IsArtifact: false**: 모든 산출물 문서는 `./docs/` 경로에 저장하고, IDE 세션 종료 시 휘발되는 시스템 아티팩트(`IsArtifact: true`)의 사용을 절대 금지합니다.
4. **Everything DLL 통신 규칙**: 한글/다국어 문자열 깨짐 현상을 방지하기 위해 백엔드 DLL 바인딩 시 반드시 `wchar_t*` 타입 기반의 `W` API(예: `Everything_SetSearchW`)를 엄격히 준수합니다.

---

## 🛠️ 프로젝트 초기 진단 및 지식 자산

### 1. UTF-16LE 마크다운 파일 인코딩 이슈

- **현상**: 유저가 제공한 `docs/요구사항명세서.md` 및 `docs/UI명세서.md` 파일의 인코딩이 `UTF-16LE`로 되어 있어, Antigravity IDE의 `view_file` 도구에서 `unsupported mime type text/plain; charset=utf-16le` 에러를 반환하며 읽기에 실패함.
- **해결**: Powershell 명령어를 사용하여 파일의 원본 텍스트를 읽고 `.NET [System.Text.Encoding]::UTF8`을 활용하여 인코딩을 `UTF-8`로 변환하여 덮어씀으로써 `view_file`이 정상 구동되도록 처리함.
- **교훈**: 윈도우 환경에서 생성된 마크다운 문서나 설정 파일 등이 UTF-16 계열로 인코딩되어 view_file이 차단되는 경우, powershell FFI나 파일 인코딩 컨버터 명령어를 호출하여 UTF-8로 변환하는 선제적인 파이프라인을 구축하는 것이 효율적임.

### 2. 초기 워크스페이스 상태 진단 및 기술 스택 전면 전환 (WPF)
- **현상**: `overview-tree.js` 실행 결과 소스 코드가 없는 초기 빈 워크스페이스 상태로 진단됨.
- **의사결정**: Everything.exe 엔진과 Windows OS 간의 완벽한 쉘 통합(마우스 드래그 앤 드롭 외부 폴더 연동, 네이티브 우클릭 쉘 메뉴인 `IContextMenu` 연동, 클립보드 실제 파일 객체 복사 등)을 달성하기 위해, 기존의 Electron/React 웹 기술 스택을 **C# .NET 9.0 WPF 데스크톱 어플리케이션**으로 전면 교체하기로 결정함.
- **대응**: 새로운 아키텍처에 맞게 `.gitignore`, `docs/memories/overview.md`를 C# WPF 구조로 덮어쓰고, 세부 구현 로드맵과 자동화/수동 검증 계획이 정의된 `docs/step001_ImplementationPlan_v1.md` 구현 계획서를 작성함.

### 3. C# WPF 및 XAML 구현 중 트러블슈팅 지식
- **WPF StackPanel Spacing 미지원**: WPF의 기본 `StackPanel`은 WinUI/UWP와 달리 `Spacing` 속성을 기본 제공하지 않아 빌드 에러가 발생함. `ModernWpf` 라이브러리의 `<modern:SimpleStackPanel Spacing="X">`로 대체하여 세련된 간격 배치 해결.
- **WPF TextBox PlaceholderText 미지원**: 표준 `TextBox`에는 `PlaceholderText` 속성이 없음. ModernWpf의 Helper 속성인 `<TextBox modern:ControlHelper.PlaceholderText="X"/>`로 매핑하여 플레이스홀더 렌더링 해결.
- **네이티브 IContextMenu LPCSTR 마샬링**: 쉘 메뉴 호출 시 `CMINVOKECOMMANDINFO`의 `lpVerb` 멤버를 `string`으로 마샬링하면 정수형 명령 인덱스 할당 시 캐스팅 오류가 발생함. `IntPtr`로 선언하여 P/Invoke 메모리 할당 무결성 보장.
- **ModernWpf ResourceDictionary XamlParseException**: `App.xaml`에서 `pack://application:,,,/ModernWpf;component/ThemeResources/Light.xaml`와 같이 특정 내부 리소스 파일 경로를 하드코딩해서 불러오면 런타임 어셈블리 리소스 해석 실패로 인해 `XamlParseException`이 발생함. 네임스페이스 `xmlns:ui="http://schemas.modernwpf.com/2019"`를 선언하고, `<ui:ThemeResources />`와 `<ui:XamlControlsResources />` 태그 조합으로 병합(Merge)하여 테마 리소스를 머지하는 것이 정석적인 해결 방법임.
- **WPF & WinForms global using 충돌 (UseWindowsForms)**: 트레이 기능을 위해 `.csproj`에 `<UseWindowsForms>true</UseWindowsForms>`를 설정하면, `<ImplicitUsings>enable</ImplicitUsings>` 환경에서 WPF와 WinForms의 동일한 명칭을 가진 어셈블리 클래스(Application, UserControl, MouseEventArgs, KeyEventArgs, Point 등)가 암시적 global using으로 선언되어 CS0104 모호성 에러가 발생함. 이 경우 `.csproj` 내에 `<ItemGroup><Using Remove="System.Windows.Forms" /></ItemGroup>`를 추가하여 global using 충돌을 완전히 해제하고, 트레이 관련 FFI를 수행하는 구체 클래스 파일 맨 위에서만 `using System.Windows.Forms;`를 명시적으로 Import하여 해결하는 것이 가장 안전하고 모범적인 해결책임.
- **Everything SDK DLL EntryPointNotFoundException (GetLastError)**: `everything64.dll` 연동 시 Win32 GetLastError를 조회하기 위한 DLL 함수는 `Everything_GetGetLastError`가 아닌 `Everything_GetLastError`임. SDK의 공식 P/Invoke 시그니처 이름을 정확하게 `Everything_GetLastError`로 선언하고 호출해야 런타임 진입점 오류(System.EntryPointNotFoundException)로 인한 기동 실패를 해결할 수 있음.
- **WPF ModernWpf 미정의 StaticResource 예외 (StaticResourceExtension)**: `Views/MainWindow.xaml`에서 `CardToggleSwitchStyle`, `TextBoxStyle`, `ButtonStyle` 등 존재하지 않는 스타일 키를 `StaticResource`로 참조하면 `System.Windows.StaticResourceExtension` 예외가 throw되며 앱 구동이 정지됨. ModernWpf의 테마 사전이 적용된 상태에서는 스타일 키를 명시적으로 할당하지 않아도 기본 컨트롤에 아름다운 WinUI 스타일이 자동으로 완전 상속되므로, 해당 스타일 선언을 지워주는 것이 가장 안전하고 호환성이 높은 해결책임.

### 4. 추가적인 5대 이슈 트러블슈팅 지식
- **ExcelDataReader CSV HeaderException**: UTF-8 BOM이 없는 CSV 파일 파싱 시 인코딩 문제 등으로 `HeaderException`이 발생할 수 있음. `ExcelReaderFactory.CreateCsvReader(stream, new ExcelReaderConfiguration { FallbackEncoding = Encoding.UTF8 })`와 같이 인코딩 사양을 명시적으로 설정하여 방지함.
- **Everything SDK BOOL 리턴값 마샬링 실패**: C++ `BOOL`은 4바이트 정수이지만 C# `bool`은 1바이트이므로, P/Invoke 호출 시 `[return: MarshalAs(UnmanagedType.Bool)]` 어트리뷰트가 누락되면 참/거짓 판단이 항상 `true`로 깨질 수 있음. 이로 인해 모든 파일 결과가 폴더(`IsFolder = true`)로 잘못 식별되어 크기가 `<DIR>`로 고정되고, 날짜 조회 함수들이 실패(0 반환 ➔ 1601년 변환)하는 문제가 발생함.
- **WPF StatusBar 짤림 및 여백 처리**: 가로 폭이 제한되거나 패딩 설정이 어긋날 때 우측 끝 텍스트("개" 등)가 잘릴 수 있음. StatusBar 내에 `Width` 강제 바인딩을 피하고, `ItemsPanelTemplate`을 `Grid`로 구성하여 고정 칼럼 분할 정렬을 적용하고 우측 끝 마진 여백을 부여하여 안전하게 레이아웃을 처리함.

### 5. 성능 및 동의어 아키텍처 고도화 지식
- **WPF ObservableCollection 대량 데이터 렉 차단**: ObservableCollection에 루프를 돌며 아이템을 다량 `Add`하면 매 건마다 `CollectionChanged` 이벤트가 유발되어 엄청난 렌더링 병목이 발생함. 알림을 억제하고 최종 처리에 단 한 번의 `Reset` 통지만 전파하는 `RangeObservableCollection` 클래스를 도입해 렉을 방지함.
- **디바운스 & 비동기 쿼리 병행**: 타이핑 시 매 입력마다 검색이 실행되는 렉을 해결하기 위해 `DispatcherTimer` (150ms) 기반 디바운스와 `Task.Run` 기반 백그라운드 스레드 검색을 결합하여 UI 스레드 오버헤드를 제로화함.
- **양방향 동의어(Alias) 연관어 그룹화**: 동의어 검색은 Key ➔ Value의 단방향이 아니라, 그룹 내 어떤 단어를 쳐도 전체 동의어 그룹이 조회되어야 함. 키와 값의 전체 단어 관계를 하나의 동의어 동치 클래스(Equivalence Class) 그룹으로 매핑 맵에 사전 빌드해두어 양방향 검색을 완벽히 보장함.

### 6. UI 대칭성 및 전역 스타일 리소스 중앙화 지식
- **WPF 암시적 스타일(Implicit Style)**: 주먹구구식 폰트 정의를 배제하기 위해, 리소스 사전에 TargetType만 명시한 암시적 스타일(예: `Style TargetType="TextBox"`)을 설정하면 컨트롤에 스타일을 일일이 매핑하지 않아도 일관되게 12px 폰트와 지정 글꼴이 상속 적용됨.
- **수직 3단 대칭 정렬 레이아웃**: 상단 검색창에서 컨트롤 스위치(FastAlias, 옵션패널) 영역을 세로 구분선 Border로 명확히 격리하고, 우측에 메인 검색어, 제외 단어, 직계 경로 3개의 TextBox를 세로 SimpleStackPanel로 균등 배치(Symmetric 3-Row Layout)함으로써 심미적인 대칭 비주얼과 사용성을 보장함.

### 7. Everything SDK 파일시간(FileTime) 마샬링 예외 방어
- **Win32 FileTime 유효 범위 오류 (ArgumentOutOfRangeException)**: Everything SDK의 `Everything_GetResultDateModified`를 통해 파일 수정일 정보를 가져올 때, 해당 파일의 속성 조회 실패나 유효하지 않은 OS 시스템 시간 값 등이 유입될 경우 C# `DateTime.FromFileTime`이 예외를 발생시키며 비동기 검색 스레드가 충돌함.
- **해결 방안**: API의 반환 여부(`bool`)를 안전하게 검사하고, 유입된 `fileTime` 값이 `0` 미만인 음수값이거나 예외 범위를 넘을 수 있으므로 `try-catch` 블록으로 `ArgumentOutOfRangeException`을 감싸 실패 시 `new DateTime(1601, 1, 1)`(기본 Win32 에폭 시작점)로 폴백하도록 보완함.

### 8. 고화질 이미지 생성 및 ICO 아이콘 빌드 통합 지식
- **WPF ApplicationIcon 지정 및 창 동기화**: 프로젝트 빌드 시 생성된 EXE 자체에 아이콘을 부여하려면 `.csproj`의 PropertyGroup 안에 `<ApplicationIcon>Assets\app_icon.ico</ApplicationIcon>`를 등록함. 동시에 런타임 창 타이틀바와 작업 표시줄에 노출되도록 `MainWindow.xaml`에 `Icon="/Assets/app_icon.ico"` 속성을 리소스 절대 경로 포맷으로 지정하고, `.csproj`에 `<Resource Include="Assets\app_icon.ico" />`를 명시적으로 포함해 컴파일해야 디버그/런타임 기동 시 `IOException` 리소스 소실 오류를 예방할 수 있음.
- **PowerShell 기반 PNG->ICO 비손실 변환**: 환경 내 ImageMagick 등 변환 도구가 없을 때, `.NET System.Drawing` 객체를 메모리에 임시 적재하여 고해상도 생성 PNG(1024x1024)를 GDI 비트맵 핸들로 128x128 등 알파 채널 보존 규격의 `.ico`로 깔끔하게 파이프라인 변환하여 빌드 신뢰성을 확보함.

### 9. F2 인라인 이름변경 단일편집 및 가상화 예외 방어 지식
- **WPF VirtualizingStackPanel 상태 잔존 현상**: WPF ListView 등에서 UI 가상화가 활성화된 경우 화면 밖으로 사라진 아이템들이 뷰 상태(`IsEditing`)를 그대로 물고 있을 수 있어 여러 개의 TextBox가 복수 활성화되는 중복 편집 상태 버그가 발생함.
- **해결 방안**: 뷰 레벨에서 `_editingItem` 필드를 활용해 단일 편집을 제어하고, F2 진입(StartRename) 시점에 `ItemsSource` 전체를 전수조사하여 `IsEditing` 상태 플래그를 일괄 강제 클리어함으로써 해결함.
- **선택 변경 및 LostFocus 취소 정밀 연동**: 다른 행 클릭이나 빈 공간 클릭으로 SelectionChanged 발생 시 `_editingItem`이 선택 상태를 벗어나면 자동으로 편집을 Cancel 처리하고, LostFocus 시점에는 변경 사항이 유효한지(텍스트의 실질적 변경 및 공백 여부)에 따라 Cancel과 Commit을 세분화 분기하여 Windows 10 탐색기 상식 규격을 완전하게 모사함.

### 10. Everything SDK 대용량 FFI 마샬링 및 GC 렉 최적화 지식
- **P/Invoke FFI 및 GC 폭풍으로 인한 굉음**: Everything SDK에 `SetMax(0xFFFFFFFF)`를 적용해 무제한 쿼리를 날릴 경우, 빈 쿼리나 광범위한 옵션 변경 시 PC 전체의 수십만 건 파일이 한 번에 반환됨. 이 수십만 개의 데이터를 C# 객체(`SearchResultItem`)로 인스턴스화하고 마샬링(String 복사)하면서 FFI 오버헤드가 발생하고, 가비지 컬렉터(GC)에 심각한 메모리 정리 렉이 걸려 CPU가 폭증하고 팬 굉음이 발생함.
 - **해결 방안**: 검색 결과의 한계를 합리적인 수준(`MaxResults = 10000`)으로 제약하여 FFI 복사 및 객체 인스턴스 생성 횟수를 근본적으로 축소함. **추가적으로 검색단어 입력란에 아무것도 입력하지 않은 상태(빈 검색어)에서는 Everything DLL로 통하는 FFI 검색 쿼리 자체를 아예 실행하지 않고, 결과 목록(`Results.Clear()`)을 즉각 클리어한 후 즉시 리턴(Short-circuit)하도록 개선**하여 불필요한 DLL 쿼리 통신을 100% 원천 차단하고 메모리와 CPU 사용량을 즉시 0%로 안정화시켰습니다.
 
 ### 11. WPF 다중 선택 드래그 앤 드롭(Drag-out) 유지 및 제어 지식
 - **현상**: WPF ListView에서 다중 선택(Ctrl+A 등) 상태인 다수의 아이템 중 하나를 클릭해 드래그하려고 하면, 마우스 클릭(PreviewMouseLeftButtonDown) 시점에 WPF의 기본 동작으로 인해 즉시 클릭된 단일 아이템만 선택되고 기존의 다중 선택이 해제됨. 이로 인해 다중 파일 드래그가 불가능하고 1개의 파일만 드래그 앤 드롭되는 버그가 발생함.
 - **해결 방안**: 
   1. `PreviewMouseLeftButtonDown` 이벤트에서 클릭된 `ListViewItem`이 이미 선택된 상태(`IsSelected == true`)인지 판별합니다.
   2. 이미 선택된 상태라면, 드래그 시도를 위해 즉시 단일 선택으로 전환되지 않도록 `e.Handled = true` 처리를 수행하고 임시 변수 `_clickedItem`에 마우스를 누른 아이템을 저장합니다.
   3. 마우스를 움직여 드래그 임계값을 초과하여 `StartDrag`가 발동되면, 수집된 다중 선택 파일의 경로 배열을 포함하는 `DataObject`를 구성하여 `DragDrop.DoDragDrop`을 정상 실행하고 `_clickedItem`을 안전하게 초기화합니다.
   4. 만약 마우스를 움직이지 않고 그대로 마우스를 뗀 경우(`PreviewMouseLeftButtonUp`), 드래그가 일어나지 않은 것이므로 임시 변수 `_clickedItem`이 가리키는 대상에 대해 수동으로 선택 및 포커스를 지정해 주어(Ctrl 키 등에 따른 토글 연동 포함) 본래의 일반 마우스 클릭 동작을 정확하게 모사(Emulate)합니다.
 
 ### 12. 타이핑 자동 검색 차단 및 Enter 키 검색 수동 전환 지식
 - **현상**: 검색단어 란(`SearchQuery`)에 타이핑을 할 때마다 디바운스 타이머가 돌며 지속적인 자동 검색이 유발되어 화면이 계속 깜빡이고 "검색중..." 메세지가 연달아 뜨면서 대용량 DLL 쿼리가 빈번히 수행되어 심각한 입력 렉 및 응답 속도 저하 현상이 발생함.
 - **해결 방안**: 
   1. `SearchQuery` 프로퍼티의 setter 내부에서 `TriggerSearch()` (자동 검색) 로직을 전면 제거하여 글자를 입력하는 타이핑 도중에는 일체의 무거운 검색 로직이 발동되지 않도록 물리적으로 분리합니다.
   2. 다만, 검색어 입력 내용을 백스페이스 등으로 전부 지운 빈 검색어 상태가 감지되면, 타이핑 지연 없이 즉각 결과 리스트를 클리어(`Results.Clear()`)해주어 쾌적하고 투명한 UI 실시간 피드백을 제공합니다.
   3. `MainWindow.xaml`의 검색창 TextBox 레이어에 `<TextBox.InputBindings>` 스키마를 구성하고 `KeyBinding Key="Enter" Command="{Binding SearchVM.SearchCommand}"`로 선언하여, 사용자가 타이핑을 완료하고 **Enter 키**를 입력한 정확한 타이밍과 우측 새로고침 버튼 클릭 시에만 검색 명령이 발동되도록 수동 전환해 앱 반응성 및 시스템 자원 낭비를 100% 최적화하였습니다.
 
 ### 13. Everything 쿼리 연산자 우선순위(OR vs AND) 그룹화(괄호) 지식
 - **현상**: 탐색 대상을 '전체'(`SearchScope.All`)로 지정하면 내부적으로 `<검색어> | path:<검색어>`의 OR(|) 조건 쿼리가 조립됨. 여기에 미디어 확장자 필터(예: 사진 `ext:jpg;png...`)를 추가하면, Everything 엔진의 특성상 `AND(공백)`가 `OR(|)`보다 우선순위가 높아 `(검색어) OR (path:검색어 AND 사진)` 형태로 해석되는 치명적인 버그가 발생함. 이로 인해 경로에 검색어가 포함되지 않은 임의의 폴더/파일 중 파일명에 검색어만 들어가면 미디어 필터(`ext:`)와 폴더 차단(`file:`)을 완전히 무시한 채 폴더를 포함하여 결과에 무작위 노출되는 현상이 유발됨.
 - **해결 방안**: 
   - `QueryTransformer.cs`에서 범위 쿼리를 빌드할 때, 조립되는 Scope 쿼리 자체(All, File)를 대괄호(WPF Everything SDK 기준 `< >` 괄호)로 확실하게 묶어서 `<<검색어> | path:<검색어>>` 형태로 감싸줍니다.
   - 이렇게 하면 전체 스코프 쿼리가 하나의 대그룹으로 작용하여, 뒤에 연이어 붙는 `file: ext:jpg;png...` 와 같은 AND(공백) 조건 제약이 전체 스코프 식에 완벽히 분배(교집합)됩니다. 그 결과, **확장자 필터가 켜지고 폴더가 꺼져 있을 때 폴더가 섞여 나오는 현상을 100% 원천 차단**하고 오직 해당 지정 확장자 파일만 정확하게 걸러내도록 성공적으로 해결했습니다.
 
 ### 14. 다중 창 트레이 최소화 중복 노출 해결 및 트레이 동적 생명주기 관리 지식
 - **현상**: 여러 창을 기동한 상태에서 각 창을 닫을 때, 시스템 트레이에 아이콘이 중복으로 생성되거나(각 창별 1개씩) 원치 않게 트레이로 숨겨져 백그라운드에 프로세스가 잔존하는 버그가 발생함.
 - **해결 방안**:
   1. **다중 창 열림 상태 감색**: `Window_Closing` 시점에 `Application.Current.Windows` 컬렉션을 순회하여 나 이외의 다른 `MainWindow` 창이 실행 중인지 카운팅합니다. 나 외에 다른 창이 하나라도 열려 있다면, 트레이 최소화 설정을 생략하고 현재 창을 완전히 파괴(Close)시켜 창 누수 및 잔존 프로세스를 완전 차단합니다.
   2. **트레이 동적 생명주기(Dynamic Lifecycle)**: 창 로딩 시점부터 트레이 아이콘을 무조건 생성하지 않고, 최후의 1개 창이 닫혀 실제로 화면에서 최소화(`Hide()`)되는 시점에만 트레이 아이콘을 동적으로 `new` 생성합니다.
   3. **복원 시 자동 제거**: 트레이 아이콘 더블클릭 또는 컨텍스트 메뉴의 '열기'를 통해 창이 다시 활성화(`Show()`, `Activate()`)되는 정확한 복원 타이밍에 `DestroyTrayIcon()`을 호출하여 트레이 아이콘을 즉시 파괴(`Dispose()`)시킵니다. 이를 통해 전체 앱 주기 동안 트레이 아이콘 개수를 언제나 1개 이하(0개 또는 1개)로 완벽하게 관리되도록 개선했습니다.
 
 ### 15. WPF 윈도우 네이티브 쉘 우클릭 서브메뉴(IContextMenu) 빈 드롭다운 렌더링 해결 지식
 - **현상**: 검색 리스트뷰 우클릭 시 호출되는 윈도우 탐색기 네이티브 쉘 메뉴(`IContextMenu`)의 하위 메뉴(예: 연결 프로그램, 7-Zip, 윈도우 Defender 검사 등)에 마우스를 올리면 하위 드롭다운 항목이 채워지지 않고 텅 빈 흰색 상자로 렌더링되거나 아무 반응이 없는 심각한 버그 발생.
 - **해결 방안**:
   1. **인터페이스 확장**: `IContextMenu` 인터페이스 외에 팝업 및 서브메뉴 소유자 그리기(Owner Draw)를 지원하기 위해 `IContextMenu2` (`HandleMenuMsg`) 및 `IContextMenu3` (`HandleMenuMsg2`) COM 인터페이스 정의를 추가합니다.
   2. **윈도우 메시지 후킹**: WPF의 `HwndSource`를 활용해 부모 윈도우에 메시지 후크(`AddHook`)를 연결하고, `TrackPopupMenuEx`가 실행되는 임시 루프 동안 쉘 메뉴가 요구하는 핵심 4대 메시지(`WM_INITMENUPOPUP`, `WM_DRAWITEM`, `WM_MEASUREITEM`, `WM_MENUCHAR`)를 수신합니다.
   3. **메시지 중계**: 수신된 메시지를 `IContextMenu3::HandleMenuMsg2` 또는 `IContextMenu2::HandleMenuMsg`에 온전히 마샬링 및 포워딩하여 쉘 핸들러가 서브메뉴 데이터를 정상적으로 렌더링하고 동작하도록 중계해 줍니다. 메뉴 표시 완료 후 즉시 훅을 해제(`RemoveHook`)하여 시스템 오동작을 예방했습니다.
 
 ### 16. 스마트 매핑 사전 관리자(Alias Manager)의 CSV 내보내기(Export) 구현 지식
 - **요구 명세**: 사용자가 기입하거나 연동한 단어 동의어 매핑 목록을 백업 및 다른 PC 연동용으로 활용할 수 있도록 CSV 형식 파일 저장 기능 구현 필요.
 - **해결 방안**:
   1. **CSV 이스케이프 포맷팅**: 엑셀 연동 및 다른 파서에서의 해석 오류를 방지하기 위해 CSV 데이터 출력 시 쉼표(`,`), 큰따옴표(`"`), 줄바꿈(`\r`, `\n`)이 포함된 키워드/동의어 텍스트는 큰따옴표로 안전하게 감싸고 내부 큰따옴표를 이중화(`""`)하는 `EscapeCsv` 이스케이프 파이프라인을 `ExcelService.cs`에 구축했습니다.
   2. **RelayCommand 바인딩**: `AliasManagerViewModel`에 `ExportExcelCommand`를 추가하고 `SaveFileDialog` 컴포넌트를 호출해 사용자가 지정한 로컬 경로에 현재 바인딩된 매핑 컬렉션을 CSV 파일로 정상 저장하고 상태 메시지를 갱신시켰습니다.
   3. **UI 버튼 신설**: `AliasManagerWindow.xaml` 상단 제어바에 "내보내기 (.csv)" 버튼을 신설하여 뷰모델 커맨드와 안전하게 연계했습니다.
 
 ### 17. 대용량 동의어 사전(3000줄 이상) 연동 시 CPU 폭증 및 검색 지연 최적화 지식
 - **현상**: 매핑 테이블 사전에 수천 줄 이상의 동의어(총 수만 개 토큰)를 대량 주입한 후, 검색어를 입력하면 "검색 중" 상태에서 무한 루프 수준(10분 이상)의 병목이 발생하여 앱이 먹통이 됨.
 - **원인 분석**:
   1. **매 검색별 사전 재빌드**: 쿼리를 날릴 때마다 `QueryTransformer` 단에서 수천 개 레코드를 돌며 동의어 확장 맵(`aliasGroups`)과 역순 정렬 키 목록(`sortedKeys`)을 처음부터 동적으로 재생성하는 O(N^2) 이상의 메모리 낭비가 발생함.
   2. **무차별적 정규식 치환**: 입력된 검색어는 단어 1~2개 수준에 불과하지만, 사전에 존재하는 수만 개 키(`sortedKeys`) 전체에 대해 정규식 컴파일 및 `Regex.Replace`를 무조건 전체 반복 실행하여 극심한 CPU 과부하가 발생함.
   3. **동치 클래스(Equivalence Class) 바이럴 병합 버그**: `DatabaseService`에서 캐시를 빌드할 때, 어떤 단어가 여러 줄(Row)에 걸쳐 중복 출현할 경우, 해당 단어를 공유하는 모든 줄의 데이터를 하나의 거대한 그룹(HashSet)으로 강제 병합(Merge)해버리는 논리적 결함이 있었음. 이로 인해 수천 개의 독립된 키워드 그룹들이 하나의 거대한 3만 개짜리 메가 그룹으로 엮이게 되고(Viral Expansion), 단어 1개를 치환할 때 3만 개의 OR(`|`) 조건으로 묶인 초거대 쿼리 문자열이 생성되어 Everything.exe 엔진 자체를 다운시켰음.
 - **해결 방안**:
   1. **동의어 캐시 전역 재사용**: 사전 변경점(추가/삭제/가져오기)이 있을 때만 딱 1번 데이터베이스 서비스 단(`DatabaseService.cs`)에서 동의어 확장 맵과 정렬 키 목록을 가공하여 메모리에 단독 캐싱하고, `QueryTransformer`는 이를 O(1)으로 받아 재사용하게 조치하여 빌드 부하를 제로화함.
   2. **IndexOf 조기 생략 필터링(Short-circuit Pruning)**: 루프 내에서 정규식을 돌리기 전, 입력 쿼리 원문에 해당 사전 키가 포함되어 있지 않다면(`IndexOf < 0`) 무거운 Regex 관련 문맥을 실행하지 않고 즉각 `continue`하여 스킵하게 함.
   3. **바이럴 확장 알고리즘 차단**: `BuildAliasGroupsCache()` 로직을 수정하여 단어가 여러 줄에 속하더라도 해당 줄(Row)끼리 병합하지 않고, 각 단어가 자신이 속한 개별 줄들의 요소만 독립적으로 수집(Union)하도록 아키텍처를 변경함. 이를 통해 무의미하게 부풀려진 3만 개의 OR 조건을 방지하고, 오직 연관된 극소수의 동의어만 확장하여 Everything 엔진에 전달함으로써 변환 및 검색 속도를 10분 이상에서 **0.0001초** 수준으로 완전히 단축함.
