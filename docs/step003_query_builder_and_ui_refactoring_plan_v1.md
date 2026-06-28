# step003_query_builder_and_ui_refactoring_plan_v1

본 계획서는 `수정사항명세서_v1.md`에서 도출된 검색 쿼리 파서(Parser)의 치명적인 버그와 복합 우선순위 해석 결함을 수정하고, 창 크기 비최대화 시 발생하던 UI 하단 가려짐 버그를 원천적으로 해결하기 위한 작업 계획서입니다.

## 1. 요구사항 (Requirements)

### 1.1. UI 버그 수정 (Front-end)
- **현상**: 창이 최대화 상태가 아닐 때 세로 스크롤바가 생기면 검색 결과의 가장 하단 줄이 스크롤바에 가려짐.
- **해결**: [ResultGridView.xaml](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Views/ResultGridView.xaml) 내 `ResultsListView`의 내부 하단 여백(`Padding="0,0,0,12"`)을 조정하여 하단 가려짐 현상을 완전 차단.

### 1.2. 검색 쿼리 파서(Parser) 4단계 빌드 파이프라인 정렬 및 고도화
- **1단계: 검색어 및 별칭(Alias) 결합**
  - 입력어는 최상위 `|`를 기준으로 조각(Term) 분할. 큰따옴표 안의 `|`는 분할하지 않는 안전한 파서 구축.
  - 조각 단위로 Alias 맵핑 여부를 검사하고, Alias로 치환된 이름들은 따옴표(`""`)를 유지하여 정확하게 일치시킴 (`<<path:"#back_to_freedom"> | <path:"Adriana Chechik"> | ...>`).
  - 일반 검색어는 공백(띄어쓰기)이 있거나 와일드카드(`*`, `?`)가 섞여 있어도 **따옴표를 절대로 쓰지 않고** 부등호로만 감싸서 경로/파일명 조합 (`<path:Lana Rhoades>`, `<path:Adriana *>`).
  - 단, 사용자가 직접 쌍따옴표를 붙인 단어는 보존 (`"Lana Rhoades"` -> `<path:"Lana Rhoades">`).
  - `!` 부정 기호 특별 케이스 처리:
    - Case A: `!"LIFE SELECTOR"` -> `<path:!<"LIFE SELECTOR">>`
    - Case B: `!LIFE SELECTOR` -> `<path:!<LIFE> | path: <SELECTOR>>`
  - 스코프(`options.Scope`) 전체(All) 지정 시, 기존 As-Is인 이중 결합(`<Lana | path:Lana>`)을 제거하고 단일 `<path:Lana Rhoades>` 로 간결하게 렌더링.
  - 조각들이 2개 이상일 때만 전체를 `< >` 로 한번 더 그룹화.
- **2단계: 타겟 드라이브 필터 추가 (공간적 제한)**
  - 검색 대상 드라이브들을 `|` 로 묶고 부등호로 감쌈. (예: `<path:K:\ | path:N:\ | path:P:\>`)
- **3단계: 미디어 및 파일 크기 필터 결합 (물리적 필터링)**
  - 폴더 검색 방지용 `file:` 필터와 파일 크기(size) 조건을 각각 독립된 부등호로 결합. (예: `<file: ext:mp4;mkv;avi>` `<size:>=10mb>`)
  - 미디어 필터가 폴더일 때 `file:` 필터와 크기 필터 바이패스.
  - 커스텀 확장자 필터도 부등호로 안전하게 감싸서 렌더링 (예: `<ext:확장자>`).
- **4단계: 휴지통 및 시스템 폴더 제외 (최종 정제)**
  - `<!$Recycle.Bin>`을 최종 쿼리에 덧붙임 (휴지통 포함 true 시 생략).

## 2. 기술 스택 (Tech Stack)
- **런타임 및 언어**: C# .NET 9.0 (WPF 데스크톱 애플리케이션)
- **테스트 프레임워크**: MSTest (dotnet test CLI 연동)

## 3. 폴더 구조 (Folder Structure)
- `src/`
  - `EverythingFastAlias/`
    - `Services/`
      - [QueryTransformer.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Services/QueryTransformer.cs) (쿼리 조립 및 4단계 빌드 파이프라인 리팩토링)
    - `Views/`
      - [ResultGridView.xaml](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Views/ResultGridView.xaml) (하단 패딩 조정)
  - `EverythingFastAlias.Tests/`
    - [QueryTransformerTest.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias.Tests/QueryTransformerTest.cs) (수정사항명세서에 기초한 TDD 테스트 케이스 작성 및 검증)

## 4. 조회 도구 (Lookup Tools)
- [QueryTransformer.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Services/QueryTransformer.cs) 정밀 분석
- [수정사항명세서_v1.md](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/docs/수정사항명세서_v1.md) 요구사항 비교

## 5. 검증 도구 (Verification Tools)
- `dotnet test` 명령을 이용한 `EverythingFastAlias.Tests` 단위 테스트 실행 및 결과 모니터링.
- 디버그 빌드 배치 파일 실행을 통한 컴파일 오류 여부 확인.

## 6. 구현 계획 (Implementation Plan)

### Step 1. [MODIFY] `ResultGridView.xaml` 수정 (난이도: ★)
- `ResultsListView` 컨트롤에 `Padding="0,0,0,12"` 속성을 부여하여 세로 스크롤바 발생 시 마지막 열 가려짐 오류를 물리적으로 해결합니다.

### Step 2. [MODIFY] `QueryTransformer.cs` 쿼리 파서 대대적 리팩토링 (난이도: ★★★)
- 기존의 TokenRegex 기반 단어 분절 파싱 로직을 걷어냅니다.
- 최상위 `|`를 파싱하여 개별 검색어 조각을 추출하는 `SplitByTopLevelPipe` 헬퍼 메서드를 추가합니다.
- 조각 분석 및 빌드 전담 메서드 `BuildFirstStageQuery` 와 `BuildTermWithScope`를 추가하여 Alias, 부정기호 `!`, 공백 포함 일반 단어, 따옴표 적용 여부를 명세서의 Case 1~6 규격에 맞게 완벽하게 처리합니다.
- `Transform` 메서드 내에서 1단계부터 4단계까지의 빌드 파이프라인을 순서대로 정렬하여 조립하는 구조로 개편합니다.

### Step 3. [MODIFY] `QueryTransformerTest.cs` 단위 테스트 수정 (난이도: ★★)
- `수정사항명세서_v1.md` 의 Case 1~6에 대한 TDD 기반 단위 테스트 코드를 작성합니다.
- 기존의 변경된 빌드 파이프라인에 대응하여 레거시 테스트 코드가 실패할 경우 이를 보완/수정합니다.

## 7. 검증 계획 (Verification Plan)
- **단위 테스트 통과 검증**: `dotnet test` CLI를 작동하여, 작성된 TDD 테스트 케이스 6개 및 기존 테스트들이 100% 정상 통과(Green)함을 확인합니다.
- **컴파일 성공 검증**: `build-debug.bat`을 실행하여 솔루션 전체 빌드가 성공적으로 완수되는지 확인합니다.

## 8. 자산화 계획 (Capitalization Plan)
- 작업 완료 후 `docs/memories/MEMORY.md`에 지식 자산화 로그를 추가하고, `overview.md` 파일의 최신 구조 상태를 최종 확인합니다.

## 9. 승인 요청 (Request for Approval)
- 사용자의 수정사항명세서의 구체적인 To-Be 결과값에 기초하여 4단계 빌드 파이프라인을 완전히 새롭게 재설계하고, UI 하단 가려짐 문제를 100% 물리적으로 정밀 타격하는 계획을 제시하였습니다. 본 계획서에 대한 검토 및 승인을 요청드립니다.
