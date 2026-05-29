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




