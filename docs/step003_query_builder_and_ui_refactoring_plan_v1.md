# step003_query_builder_and_ui_refactoring_plan_v1

본 계획서는 `수정사항명세서_v1.md`와 사용자 인터뷰를 통해 도출된 검색 쿼리 파서(Parser)의 부등호 중첩 그룹화 규칙을 정립하고, 신규 요구사항인 "제외경로" 기능과 UI 창 크기 버그 수정을 포함한 통합 아키텍처 개선 설계도입니다.

## 1. 요구사항 (Requirements)

### 1.1. UI 버그 수정 및 제외경로 TextBox 추가 (Front-end)
- **리스트 하단 가려짐 해결**: [ResultGridView.xaml](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Views/ResultGridView.xaml) 내 `ResultsListView`의 내부 하단 여백(`Padding="0,0,0,12"`)을 부여하여 세로 스크롤 시 마지막 줄이 잘리는 버그 차단.
- **제외경로 UI 대칭 추가**: [MainWindow.xaml](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Views/MainWindow.xaml) 의 검색 입력 영역에 "제외경로" TextBox 추가 및 Grid.RowDefinitions 행 확장.

### 1.2. 쿼리 카테고리별 부등호 `< >` 그룹화 및 path:`<...>` 래핑
- **안정적 경로 그룹화**: 연산자 우선순위 충돌 방지를 위해 `path` 수식어 조립 시 `<path:<경로>>` 또는 `<path:!<경로>>` 와 같이 부등호 중첩 그룹화를 기본 채택.
  - 예: `D:\공백 테스트` -> `<path:<D:\공백 테스트>>`
- **검색단어, 제외단어, 지정경로, 제외경로, 드라이브 등 모든 카테고리 래핑**: 최종 쿼리에 반영되는 각 블록을 `< >` 로 개별 래핑하여 결합.

### 1.3. 6단계 빌드 파이프라인 정렬 및 파서 규칙
1. **1단계: 검색단어 및 별칭(Alias) 결합**
   - 입력어는 최상위 `|`를 기준으로 조각(Term) 분할. (큰따옴표 안의 `|`는 분할 생략)
   - Alias 매핑 시 원본 및 치환어 전체를 쌍따옴표를 씌우고 `path` 중첩 래핑 처리.
     - 예: `<<path:"#back_to_freedom"> | <path:"Adriana Chechik"> | ...>`
     - (단, All 스코프이고 공백이 포함된 경우에는 `<path:<Adriana Chechik>>` 규칙 적용)
   - 일반 검색어는 공백이나 와일드카드가 있어도 따옴표를 쓰지 않고 부등호 중첩 래핑 `<path:<검색어>>`.
     - 예: `Lana Rhoades` -> `<path:<Lana Rhoades>>`
   - 사용자가 직접 따옴표를 준 단어는 보존.
   - `!` 부정 기호 특별 케이스:
     - Case A: `!"LIFE SELECTOR"` -> `<path:!<"LIFE SELECTOR">>`
     - Case B: `!LIFE SELECTOR` -> `<path:!<LIFE> | path: <SELECTOR>>`
2. **2단계: 지정경로(FolderPaths) 및 제외경로(ExcludedPaths) 추가 (공간적 제한/차단)**
   - 분리 기준: `,`, `;`, `|` (좌우 공백 포함)
   - **지정경로**: `<path:<경로>>` (재귀) 또는 `<parent:<경로>>` (비재귀) 빌드 후 `|`로 엮어 부등호 감쌈.
     - 예: `<<path:<c:\test>> | <path:<d:\test>>>`
   - **제외경로**: `<path:!<경로>>` (재귀) 또는 `<parent:!<경로>>` (비재귀) 빌드 후 `|`로 엮어 부등호 감쌈.
     - 예: `<<path:!<c:\test>> | <path:!<d:\test>>>`
3. **3단계: 제외단어(ExcludedWords) 추가 (최종 정제 1)**
   - 분리 기준: `,`, `;`, `|`
   - 각 단어 `w`에 대해 `!<w>` 형태로 구성하고 `|` 로 엮어 부등호 감쌈.
     - 예: `<<!<a> | !<b>>>`
4. **4단계: 타겟 드라이브 필터 추가 (공간적 제한 2)**
   - 검색 대상 드라이브들을 `|` 로 묶고 부등호 감쌈. (예: `<path:K:\ | path:N:\ | path:P:\>`)
5. **5단계: 미디어 및 파일 크기 필터 결합 (물리적 필터링)**
   - 미디어 프리셋(영상 등)은 폴더 아닐 시 `<file: ext:...>` 형태로 묶음.
   - 파일 크기는 `<size:>=10mb>` 와 같이 부등호 감쌈 (폴더 프리셋 시 크기 필터 제외).
   - 커스텀 확장자 필터도 `<ext:확장자>` 로 감쌈.
6. **6단계: 휴지통 제외 필터 (최종 정제 2)**
   - `<!$Recycle.Bin>` 추가 (휴지통 포함 true 시 생략).

## 2. 기술 스택 (Tech Stack)
- **런타임 및 언어**: C# .NET 9.0 (WPF 데스크톱 애플리케이션)
- **테스트 프레임워크**: MSTest (dotnet test)

## 3. 폴더 구조 (Folder Structure)
- `src/EverythingFastAlias/`
  - `Models/SearchOptions.cs` (ExcludedPaths 속성 추가)
  - `ViewModels/SearchViewModel.cs` (ExcludedPaths 프로퍼티 바인딩 및 OnPropertyChanged 중계)
  - `ViewModels/SearchViewModel.Settings.cs` (ExcludedPaths 설정 보존 및 Reset 구현)
  - `Services/QueryTransformer.cs` (6단계 파이프라인 리팩토링 및 path:<...> 래핑 구현)
  - `Views/MainWindow.xaml` (제외경로 UI 구성)
  - `Views/ResultGridView.xaml` (하단 패딩 12 적용)
- `src/EverythingFastAlias.Tests/`
  - `QueryTransformerTest.cs` (TDD 테스트 시나리오 작성)

## 4. 구현 계획 (Implementation Plan)

### Step 1. [MODIFY] UI 파일 수정 (MainWindow.xaml, ResultGridView.xaml) (난이도: ★★)
- `ResultGridView.xaml` 의 `ResultsListView` 에 `Padding="0,0,0,12"` 추가.
- `MainWindow.xaml` 의 라벨+검색 입력 영역에 제외경로 TextBlock 및 TextBox(Row 6) 추가.

### Step 2. [MODIFY] 데이터 모델 및 뷰모델 갱신 (SearchOptions, SearchViewModel) (난이도: ★★)
- `SearchOptions.cs` 에 `ExcludedPaths` string 프로퍼티 추가.
- `SearchViewModel.cs` 와 `SearchViewModel.Settings.cs` 에 `ExcludedPaths` 프로퍼티 바인딩, Reset, Load/SaveSettings 로직 대칭 구현.

### Step 3. [MODIFY] QueryTransformer.cs 쿼리 파서 핵심 로직 리팩토링 (난이도: ★★★★)
- `SplitByTopLevelPipe` 분할 로직 탑재.
- `BuildFirstStageQuery` 와 `BuildTermWithScope`를 수정하여 `<path:<단어>>` 중첩 래핑, `path:!<단어>` 부정단어 조립 반영.
- `Transform` 의 6단계 파이프라인 조립 및 카테고리별 개별 부등호 래핑 전면 교체.

### Step 4. [MODIFY] QueryTransformerTest.cs 단위 테스트 수정 (난이도: ★★)
- 수정사항명세서의 Case 1~6에 제외경로와 지정경로의 `<path:<경로>>` 및 `<path:!<경로>>` 래핑 테스트를 추가하여 전체 단위 테스트 동작 검증.

## 5. 검증 계획 (Verification Plan)
- `dotnet test`로 단위 테스트 무결성(100% 통과) 확인.
- `build-debug.bat`을 실행하여 빌드 에러/경고 유무 확인.

## 6. 자산화 및 승인 요청 (Capitalization & Approval)
- 작업 완료 후 `docs/memories/MEMORY.md` 갱신.
- 위 내용에 동의하신다면 검토 및 승인을 부탁드립니다.
