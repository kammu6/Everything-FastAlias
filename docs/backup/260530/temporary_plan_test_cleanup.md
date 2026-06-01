# Temporary Plan: QueryTransformerTest Cleanup & SearchViewModel Partial Review

이 계획서는 `QueryTransformerTest.cs`에서 불필요한 중복 테스트를 정리하여 핵심 비즈니스 로직에 집중하고, `SearchViewModel.cs`의 partial class 분할 리팩토링 현황을 검토하기 위한 임시 계획입니다.

## 1. Requirements
- **테스트 코드 슬림화**: `QueryTransformerTest.cs` 내에서 중복되거나 비핵심적인 부가 검증 부분을 축소하고, 핵심 동의어 치환 로직(FastAlias 양방향, 드라이브 경로 보존, 폴더 프리셋 등) 위주로 깔끔하게 정리.
- **SearchViewModel.cs 분할 검토**: 현재 `SearchViewModel.cs`가 여러 partial class 파일로 분할되어 정상 컴파일되고 있는 상태임을 검증하고, 이를 `overview.md` 설명과 맞추어 일관성 있게 문서화.

## 2. Proposed Changes

### 2.1. [EverythingFastAlias.Tests]

#### [MODIFY] [QueryTransformerTest.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias.Tests/QueryTransformerTest.cs)
- `Test_FastAlias_Space_Contain_Replacement` (공백 포함 치환 검증)을 `Test_FastAlias_Bidirectional_Replacement` (양방향 치환 검증)와 하나로 병합.
- 무의미하게 길게 중복되는 셋업 구조나 주석들을 간소화하여 한눈에 파악하기 좋게 리팩토링.
- 최종적으로 7개의 정제된 단위 테스트 케이스 유지:
  1. `Test_FastAlias_Replacement` (단일/복합 기본 치환)
  2. `Test_Exclude_And_RecycleBin` (제외 단어 및 휴지통 처리)
  3. `Test_MediaPresets_Filter` (미디어 프리셋 필터 확장자 변환)
  4. `Test_FolderPreset_With_FastAlias_And_OR` (폴더 프리셋 단어별 folder: 결합 검증)
  5. `Test_UserManual_Drive_And_Constraints_Preserved` (가장 중요한 path: 드라이브 제한 조건 보존 검증)
  6. `Test_FolderConstraint_With_Recursive` (재귀 여부에 따른 path/parent 변환)
  7. `Test_FastAlias_Bidirectional_Replacement` (공백 포함 다국어 및 양방향 치환 통합 검증)

### 2.2. [EverythingFastAlias]

#### [REVIEW] [SearchViewModel.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/ViewModels/SearchViewModel.cs)
- 이미 분할된 3개의 partial 파일(`SearchViewModel.cs`, `SearchViewModel.Search.cs`, `SearchViewModel.Settings.cs`) 구조가 .NET 9 빌드 시스템 및 WPF MVVM과 정상 연동되고 있음을 확인.
- 800줄 이상 시 조회 불가능했던 에러가 완벽히 해결되었음을 명시.

#### [MODIFY] [overview.md](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/docs/memories/overview.md)
- `overview.md` 파일 내의 `SearchViewModel.cs` 설명 영역에서 단일 파일로 묘사되어 있는 문장을 partial class 분할 구조에 맞게 명확히 보완.

## 3. Verification Plan

### Automated Tests
- `D:\3_Code\3_Apps\43_Search-Edit\Everything검색기\src` 경로에서 `dotnet test EverythingFastAlias.slnx`를 실행하여 7개의 핵심 단위 테스트가 모두 빌드 및 통과함을 확인.

### Manual Verification
- `dotnet build EverythingFastAlias.slnx`를 구동하여 리팩토링 과정에서 컴파일 에러나 경고가 발생하지 않는지 확인.

## 4. Assetization Plan
- 작업 완료 후, `docs/memories/AGENTS.md`에 단위 테스트 정비 및 partial class 쪼개기에 대한 교훈과 이점 내용을 추가하고 지식 자산화 진행.
