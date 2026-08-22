# Step 002: FastAlias 키워드 일치 우선 모드 (Keyword Priority Mode) 구현 계획서 (v2)

## 1. Requirements (요구사항 정의)
- **목적**: FastAlias 검색 시, 검색어가 **[어떤 행의 키워드(A열)]이자 [다른 행의 동의어(B열)]로 중복 등장**하는 경우(예: `e`), 사용자의 의도에 따라 **키워드 본래의 1:1 정의(`f, g`)를 조회할지**, 아니면 **동의어로서 속한 다른 그룹들(`e, c, i`)을 합집합으로 조회할지** 선택할 수 있는 **"키워드 일치 우선 (Keyword Priority)"** 기능 구현.

### [표준 데이터셋 및 동작 기준]
```text
* 그룹 1: Keyword = #a / Words = b; c
* 그룹 2: Keyword = d  / Words = e; c
* 그룹 3: Keyword = e  / Words = f; g
* 그룹 4: Keyword = h  / Words = i; e
```

| 검색어 (입력) | 키워드 일치 우선 (ON) | 키워드 일치 우선 (OFF) | 핵심 메커니즘 |
| :--- | :--- | :--- | :--- |
| **`#a`** (가상 태그) | `<b \| c>` | `<b \| c>` | **원본 키워드명(`#a`) 배제**: Words 멤버(`b, c`)만 치환. |
| **`b`** (단일 그룹 단어) | `<b \| c>` | `<b \| c>` | **단일 그룹 조회**: 그룹 1 동의어 반환. |
| **`c`** (다중 소속 일반 단어) | `<b \| c \| e>` | `<b \| c \| e>` | **동적 전이 합집합**: `c`가 속한 그룹 1+2 합집합 반환. |
| **`e`** (⭐️ 키워드 겸 동의어) | **`<f \| g>`** <br>*(그룹 3 단독 1:1)* | **`<e \| c \| i>`** <br>*(그룹 2 + 그룹 4 합집합)* | • **ON**: 키워드 `e`의 고유 정의(그룹 3: `f, g`)만 1:1 조회.<br>• **OFF**: `e`가 동의어로 속한 그룹(그룹 2, 4: `e, c, i`) 합집합 조회. |

### [UI 및 영속성 요구사항]
1. `MainWindow.xaml`의 `FastAlias 별칭` 텍스트와 토글 스위치 사이에 콤팩트한 체크박스(`☑`) 추가.
2. 공간 절약을 위해 체크박스 자체 텍스트 레이블 대신, **마우스 호버 시 상세 설명 및 예시를 담은 오버레이 툴팁(Tooltip)** 표시.
3. `FastAlias 별칭` 토글이 OFF되면 체크박스도 연동되어 비활성화(Disabled).
4. SQLite `Settings` 테이블에 `PrioritizeKeywordMatch` 키로 상태를 저장/복원하여 앱 재시작 시에도 체크 상태 영구 기억 (기본값: ON).

---

## 2. Tech Stack (기술 스택)
- **Framework**: C# .NET 9.0 (WPF, MVVM CommunityToolkit)
- **Database**: SQLite (Microsoft.Data.Sqlite, WAL Mode)
- **Controls**: ModernWpf ToggleSwitch, CheckBox, Custom XAML ToolTip
- **Test Engine**: MSTest Framework (.NET 9.0)

---

## 3. Hypotheses & Grounding Evidence (가설 및 근거)

### 가설 1: 두 개의 독립된 O(1) 인덱스 맵으로 완벽한 분기 처리 가능
`DatabaseService`가 다음 2가지 인덱스를 인메모리에 빌드하여 관리합니다:
1. `_directKeywordMap` (A열 키워드 1:1 매핑):
   - `_directKeywordMap["#a"]` = `{ b, c }`
   - `_directKeywordMap["e"]` = `{ f, g }`
2. `_wordOccurrenceMap` (B열 동의어가 속한 행들의 Words 합집합 매핑):
   - `_wordOccurrenceMap["c"]` = `{ b, c, e }` (그룹 1 + 그룹 2)
   - `_wordOccurrenceMap["e"]` = `{ e, c, i }` (그룹 2 + 그룹 4)

*근거*:
- **ON (키워드 우선)**: `directKeywordMap.TryGetValue(term)` ➡️ 없으면 `wordOccurrenceMap.TryGetValue(term)`
- **OFF (동의어 합집합)**: `wordOccurrenceMap.TryGetValue(term)` ➡️ 없으면 `directKeywordMap.TryGetValue(term)`
- 이 두 단계 검사로 4가지 모든 케이스를 O(1) 해시 조회로 완벽히 해결할 수 있습니다.

### 가설 2: WPF ToolTip의 Rich Content 렌더링
`<CheckBox.ToolTip>` 내부에 `<StackPanel>`을 두어 제목, 설명, ON/OFF 예시를 깔끔한 카드 팝업 형태로 제공할 수 있습니다.

---

## 4. Folder Structure (수정 대상 파일 맵)
```
src/EverythingFastAlias/
├── Models/
│   └── SearchOptions.cs                        # [MODIFY] PrioritizeKeywordMatch 프로퍼티 추가 (기본값: true)
├── Services/
│   ├── DatabaseService.cs                      # [MODIFY] _directKeywordMap과 _wordOccurrenceMap 빌드 및 캐시 제공
│   └── QueryTransformer.cs                     # [MODIFY] PrioritizeKeywordMatch에 따른 정확한 O(1) 분기 로직 구현
├── ViewModels/
│   ├── SearchViewModel.Search.cs               # [MODIFY] QueryTransformer 호출 시 두 인덱스 맵 전달
│   └── SearchViewModel.Settings.cs             # [MODIFY] PrioritizeKeywordMatch 설정 저장/복원 및 리셋 구현
└── Views/
    └── MainWindow.xaml                         # [MODIFY] FastAlias 토글 옆 체크박스 및 호버 오버레이 툴팁 추가

src/EverythingFastAlias.Tests/
└── QueryTransformerTest.cs                     # [MODIFY] #a, b, c, e 케이스별 ON/OFF 단위 테스트 작성
```

---

## 5. Lookup Tools (조회 도구)
- `docs/memories/fastalias_query_resolution_guide.md` (공식 매핑 해석 가이드)
- `SearchViewModel.Settings.cs` (기존 설정 프로토콜)

---

## 6. Verification Tools (검증 도구)
- `dotnet test src/EverythingFastAlias.Tests/EverythingFastAlias.Tests.csproj`: C# MSTest 단위 테스트 스위트 실행.
- `scripts/tests/alias/test_keyword_priority_model.py`: 파이썬 기반 수학적 집합 검증 스크립트.

---

## 7. Implementation Plan (구현 단계)

### Phase 1: 모델 및 DB 설정 영속성 구현 (난이도: ★☆☆)
1. `SearchOptions.cs`: `public bool PrioritizeKeywordMatch { get; set; } = true;` 추가.
2. `SearchViewModel.Settings.cs`:
   - `LoadSettings()`: `Options.PrioritizeKeywordMatch = db.GetSetting("PrioritizeKeywordMatch", "true") == "true";`
   - `SaveSettings()`: `{ "PrioritizeKeywordMatch", Options.PrioritizeKeywordMatch ? "true" : "false" }`
   - `ResetCondition()`: `Options.PrioritizeKeywordMatch = true;`

### Phase 2: DatabaseService 듀얼 인덱스 맵 구축 (난이도: ★★☆)
1. `DatabaseService.cs` 내 `BuildAliasGroupsCache()` 개선:
   - `DirectKeywordMap`: 키워드(A열) ➡️ 해당 행의 Words(A열 키워드 자체 배제).
   - `WordOccurrenceMap`: 동의어(B열) ➡️ 해당 단어가 B열 Words로 속해 있는 다른 행들의 Words 합집합.
   - 캐시 컨테이너 `AliasCacheSnapshot`에 `DirectKeywordMap`과 `WordOccurrenceMap`을 함께 담아 반환.

### Phase 3: QueryTransformer 분기 로직 구현 (난이도: ★★☆)
1. `QueryTransformer.cs`:
   - `PrioritizeKeywordMatch == true (ON)`:
     - `DirectKeywordMap.TryGetValue(term, out var direct)` 우선 시도 (키워드 정의 1:1 매핑)
     - 없으면 `WordOccurrenceMap.TryGetValue(term, out var group)` 시도 (일반 동의어 합집합)
   - `PrioritizeKeywordMatch == false (OFF)`:
     - `WordOccurrenceMap.TryGetValue(term, out var group)` 우선 시도 (동의어 소속 그룹 합집합)
     - 없으면 `DirectKeywordMap.TryGetValue(term, out var direct)` 시도 (키워드 단독 태그 fallback)

### Phase 4: MainWindow UI 체크박스 및 호버 툴팁 구현 (난이도: ★★☆)
1. `MainWindow.xaml`의 Row 0 (FastAlias 별칭 토글 영역):
   - `TextBlock("FastAlias 별칭")` ➡️ `CheckBox` ➡️ `ToggleSwitch` 순서로 배치.
   - `CheckBox`:
     - `IsChecked="{Binding SearchVM.Options.PrioritizeKeywordMatch}"`
     - `IsEnabled="{Binding SearchVM.Options.UseFastAlias}"`
     - 호버 시 미려한 툴팁 오버레이 제공:
       ```xml
       <CheckBox.ToolTip>
           <ToolTip MaxWidth="320">
               <StackPanel Margin="4">
                   <TextBlock Text="키워드 일치 우선 (Keyword Priority)" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                   <TextBlock Text="• 체크 시 (ON): 검색어가 키워드(A열)와 일치하면, 다른 그룹에 포함된 관계는 제외하고 해당 키워드의 고유 동의어만 우선 조회합니다. (예: e ➡️ f | g)" TextWrapping="Wrap" Margin="0,0,0,4"/>
                   <TextBlock Text="• 해제 시 (OFF): 검색어가 동의어(B열)로 포함된 다른 그룹들의 동의어를 합집합으로 결합하여 조회합니다. (예: e ➡️ e | c | i)" TextWrapping="Wrap" Foreground="#888888"/>
               </StackPanel>
           </ToolTip>
       </CheckBox.ToolTip>
       ```

### Phase 5: MSTest 단위 테스트 케이스 검증 (난이도: ★☆☆)
1. `QueryTransformerTest.cs`에 4개 그룹(`#a, d, e, h`)을 기반으로 단위 테스트 추가:
   - `Test_Case_Tag_Search`: `#a` ➡️ `<b | c>`
   - `Test_Case_Single_Group_Word`: `b` ➡️ `<b | c>`
   - `Test_Case_Transitive_Common_Word`: `c` ➡️ `<b | c | e>` (ON/OFF 동일)
   - `Test_Case_Keyword_Priority_ON`: `e` ➡️ `<f | g>`
   - `Test_Case_Keyword_Priority_OFF`: `e` ➡️ `<e | c | i>`

---

## 8. Verification Plan (검증 계획)
1. **단위 테스트 통과 검증**: `dotnet test src/EverythingFastAlias.Tests/EverythingFastAlias.Tests.csproj`
2. **설정 저장/복원 검증**: 체크박스 토글 후 앱 재시작 시 상태 유지 확인.

---

## 9. Capitalization Plan (지식 자산화)
- `docs/memories/MEMORY.md`에 듀얼 인덱스 맵(`DirectKeywordMap` vs `WordOccurrenceMap`) 아키텍처 기록.
- `docs/memories/overview.md` 기능 명세 동기화.

---

## 10. Request for Approval (승인 요청)
- 사용자분께서 정의해주신 정확한 4그룹 모델과 논리에 100% 일치하도록 계획서를 재작성했습니다. 승인해 주시면 구현을 진행하겠습니다.
