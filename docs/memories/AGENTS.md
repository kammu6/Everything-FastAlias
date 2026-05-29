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
