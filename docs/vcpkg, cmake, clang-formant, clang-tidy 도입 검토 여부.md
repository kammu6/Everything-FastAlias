Viewed overview.md:56-123

C# .NET 9.0 WPF로의 기술 스택 전환에 따라, 제안해주신 C++ 표준 도구 체인들(`vcpkg`, `cmake`, `clang-format`, `clang-tidy`)의 필요성을 C# 생태계와 매핑하여 객관적으로 검토해 드립니다.

결론부터 말씀드리면, **C# .NET 환경에서는 이와 완벽히 1:1로 대응되는 더욱 강력한 자체 표준 도구들이 제공되므로, C++ 헬퍼 DLL을 별도로 제작하지 않는 한 해당 도구들의 도입 필요성은 매우 낮습니다.** 

---

### 📊 C++ 도구와 C# 표준 대안 도구 비교 및 검토

| C++ 도구 | C# 표준 대안 (도입 권장) | 필요성 검토 및 추천 사항 |
| :--- | :--- | :--- |
| **vcpkg** (C++ 패키지 관리자) | **NuGet Package Manager** | **불필요 (대체)**: WPF 및 SQLite, Excel 파서 등 모든 종속성은 C# 전용 패키지 관리자인 NuGet을 통해 단 한 줄로 안전하게 설치 및 버전 관리됩니다. |
| **CMake** (C++ 빌드 시스템) | **MSBuild (`.csproj`)** | **불필요 (대체)**: .NET SDK에 내장된 MSBuild 엔진을 사용합니다. XML 규격의 `.csproj` 파일 단 하나로 컴파일 설정 및 패키지 의존성이 아주 직관적으로 관리되므로 CMake가 개입할 여지가 없습니다. |
| **Clang-format** (C++ 코드 포매터) | **dotnet-format** / **CSharpier** | **불필요 (대체)**: .NET CLI에 포함된 `dotnet format` 명령이나 C# 진영의 표준 포매터인 `CSharpier`를 사용하여 코드 컨벤션을 강제할 수 있습니다. |
| **Clang-tidy** (C++ 린터 및 정적분석) | **Roslyn Analyzers** / **.editorconfig** | **불필요 (대체)**: .NET 컴파일러 엔진(Roslyn)에 내장된 정적 분석 엔진(`Microsoft.CodeAnalysis.NetAnalyzers`)과 `.editorconfig` 규칙 설정을 통해 C# 코드의 버그 가능성 및 성능 안티패턴을 컴파일 타임에 즉각 잡아냅니다. |

---

### 💡 예외적인 C++ 도구 도입 시나리오 (C++ Helper DLL 작성 시)

만약 Windows 10의 쉘 우클릭 메뉴(`IContextMenu`) COM 포인터 제어 및 드래그 앤 드롭 구현 시, **C# P/Invoke 코드가 지나치게 복잡해지는 것을 막기 위해 C++로 작고 가벼운 네이티브 Helper DLL(`ShellHelper.dll`)을 직접 개발하여 연동하기로 결정하는 경우**에는 다음과 같이 부분 도입을 고려할 수 있습니다.

1.  **CMake**: 헬퍼 DLL 프로젝트의 윈도우용 C++ 빌드 자동화를 위해 사용 가능.
2.  **Clang-format / Clang-tidy**: 작성할 C++ 헬퍼 코드의 정적 분석 및 컨벤션 정리를 위해 사용 가능.
3.  **vcpkg**: 헬퍼 DLL에 외부 C++ 라이브러리가 필요하지 않다면(Win32 SDK 내장 API만 사용하므로) 필요성 없음.

#### 🛠️ 에이전트 권장안
*   C#은 Windows OS API 호출 성능과 상호 운용성(P/Invoke)이 매우 훌륭합니다. **C++ Helper DLL 없이 C# 네이티브 포인터 제어 클래스(`Native/ShellContextMenu.cs` 등)로 단독 구현하는 방향이 배포 크기를 줄이고 빌드 복잡도를 최소화하므로 가장 추천**합니다.
*   따라서 C++ 도구들 대신, C# 프로젝트 구조에 **`.editorconfig` 파일**과 **`dotnet-format` 빌드 타겟**을 설정하여 프로젝트 품질을 자동 제어할 수 있도록 [구현 계획서](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/docs/step001_ImplementationPlan_v1.md) 및 아키텍처에 반영하겠습니다.

---

검토 내용에 동의하시거나 헬퍼 DLL 개발 여부에 관한 의견이 있으시다면 피드백 부탁드립니다. 확정 시 최종 계획 승인 후 환경 구성을 시작하도록 하겠습니다!