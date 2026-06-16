# Everything FastAlias - DeepWiki Repositories

본 문서는 Everything FastAlias 프로젝트의 핵심 기술 스택이 참조하고 있는 오픈소스 라이브러리 및 SDK의 공식 GitHub 저장소(Repository) 링크와 그 목적 및 개요를 명세합니다.

---

## 1. 런타임 및 데스크톱 쉘

- **기술 스택**: C# .NET 9.0 (WPF 데스크톱 애플리케이션)
- **공식 GitHub 저장소**:
  - [.NET WPF Core](https://github.com/dotnet/wpf)
  - [.NET Runtime](https://github.com/dotnet/runtime)
- **개요**:
  - Windows 데스크톱 클라이언트 개발을 위한 핵심 프레임워크인 WPF와 .NET 9.0 런타임의 오픈소스 저장소입니다. Modern WPF 스타일링 및 OS 네이티브 윈도우 쉘 통합(Shell Context Menu, Drag and Drop 등)의 인프라적 근간이 됩니다.

## 2. UI 및 테마 라이브러리

- **기술 스택**: ModernWPF (Windows 11 스타일 WinUI UI 적용)
- **공식 GitHub 저장소**: [Kinnara/ModernWpf](https://github.com/Kinnara/ModernWpf)
- **개요**:
  - WPF 애플리케이션에 Windows 11 및 WinUI의 모던하고 깔끔한 비주얼 스타일(라이트/다크 테마, Fluent 제어 컨트롤 등)을 적용해주는 커뮤니티 라이브러리입니다. 프로젝트의 주 레이아웃과 아코디언 필터 칩의 스타일링을 담당합니다.

## 3. MVVM 프레임워크

- **기술 스택**: CommunityToolkit.Mvvm (ObservableObject, RelayCommand 모델 지원)
- **공식 GitHub 저장소**: [CommunityToolkit/dotnet](https://github.com/CommunityToolkit/dotnet)
- **개요**:
  - 마이크로소프트의 .NET 커뮤니티 툴킷 산하 프로젝트로, C# 소스 생성기(Source Generators)를 사용하여 간결하고 속도가 빠른 MVVM(Model-View-ViewModel) 구현을 지원합니다. ObservableProperty, RelayCommand 등 데이터 바인딩 보일러플레이트를 줄이는 핵심 프레임워크입니다.

## 4. 네이티브 DLL 바인딩 (FFI)

- **기술 스택**: P/Invoke (`DllImport`를 사용한 `everything64.dll` 연동)
- **공식 GitHub 저장소 / 자료**: [voidtools Everything SDK](https://github.com/voidtools/EverythingSDK)
- **개요**:
  - Windows의 대표적 초고속 파일 검색 도구인 Everything 엔진의 FFI 연동을 위해 voidtools에서 제공하는 공식 SDK 자원입니다. P/Invoke(FFI) 시그니처 래핑 클래스인 `EverythingSdk`를 구축하는 기준 명세서 역할을 수행합니다.

## 5. 로컬 데이터베이스

- **기술 스택**: Microsoft.Data.Sqlite (고성능 ADO.NET SQLite 드라이버)
- **공식 GitHub 저장소**: [dotnet/efcore](https://github.com/dotnet/efcore) (해당 저장소 내 `src/Microsoft.Data.Sqlite.Core` 경로에 소스 구현)
- **개요**:
  - EF Core 프로젝트의 일부로 개발되고 있는 공식 .NET용 SQLite ADO.NET 데이터 프로바이더입니다. 가볍고 현대적인 로컬 파일 데이터베이스 연결을 보장하여, 동의어(Alias) 매핑 테이블 및 사용자 설정 데이터를 고속 트랜잭션(WAL 모드)으로 관리하는 데 활용됩니다.

## 6. Excel 파서

- **기술 스택**: ExcelDataReader (경량 및 고속 `.xlsx` 파일 파서)
- **공식 GitHub 저장소**: [ExcelDataReader/ExcelDataReader](https://github.com/ExcelDataReader/ExcelDataReader)
- **개요**:
  - Microsoft Office Interop이나 외부 무거운 종속성 설치 없이, 엑셀 파일(.xlsx, .xls)을 원시 스트림 수준에서 초고속으로 읽어 들이는 경량 .NET 파서입니다. 스마트 매핑 관리자 내 엑셀 대용량 일괄 임포트 기능 개발에 사용됩니다.

## 7. 아이콘

- **기술 스택**: Lucide.Wpf (공식 Lucide 아이콘의 WPF 래퍼)
- **공식 GitHub 저장소**: [lucide-icons/lucide](https://github.com/lucide-icons/lucide)
- **개요**:
  - 현대적이고 직관적인 디자인을 제공하는 심플한 오픈소스 SVG 아이콘 팩입니다. WPF 환경에서는 XAML `Path` 데이터 형태로 가공하여 사용하거나 커뮤니티 WPF 래퍼(예: LucideIcons 등)를 통해 비주얼 컴포넌트 내에 자연스럽게 렌더링하도록 돕습니다.

---

## 📚 WPF 구현 참고 레포지토리 (Reference Repositories)

> 본 섹션은 라이브러리 의존성이 아닌, WPF 탐색기 수준 UX 구현 시 코드 패턴 및 API 참고용으로 활용하는 레포지토리를 명세합니다.

### R1. microsoft/WPF-Samples

- **GitHub**: [microsoft/WPF-Samples](https://github.com/microsoft/WPF-Samples)
- **활용 목적**:
  - WPF 공식 마이크로소프트 샘플 모음. ListView/DataGrid 키보드 내비게이션, Win32 Interop, Per-Monitor DPI, 접근성(Accessibility) 패턴 등 WPF 프레임워크의 정석 구현 방식을 확인할 때 1차 참조 소스.
  - 특히 `KeyGesture`, `ListViewPage`, `Win32 Interoperability` 섹션이 우리 앱의 단축키/셸 통합과 직결됨.
- **DeepWiki 질의 예시**: `"WPF ListView inline rename F2 keyboard focus"`, `"RequestBringIntoView suppress scroll"`, `"Win32 Interoperability HwndHost"`

### R2. JamesnetGroup/wpf-explorer

- **GitHub**: [JamesnetGroup/wpf-explorer](https://github.com/JamesnetGroup/wpf-explorer)
- **기술 스택**: .NET 8.0, WPF, CommunityToolkit.Mvvm, Prism, Jamesnet.Wpf
- **활용 목적**:
  - WPF로 구현된 실제 파일 탐색기 UI 아키텍처 참고용. MVVM 패턴 기반의 탐색기 디렉토리 트리, 파일 리스트 뷰 구성 방식 등 전체적인 WPF 탐색기 구조 설계를 파악할 때 참조.
  - 단, Jamesnet.Wpf + Prism 기반의 독자 프레임워크를 사용하므로 코드를 직접 이식하기보다는 **패턴 참고 수준**으로 활용.
- **주의**: 인덱싱 완료 전까지 GitHub에서 코드를 직접 열람해야 함.
