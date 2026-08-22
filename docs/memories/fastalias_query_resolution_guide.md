# FastAlias 별칭 쿼리 치환 및 매핑 해석 메커니즘 가이드 (Query Resolution Guide)

> **목적**: FastAlias 동의어 검색 엔진의 쿼리 변환 아키텍처, 원본 키워드 배제 원칙, 동적 전이 합집합(Transitive Union), 그리고 키워드 일치 우선(Keyword Priority) 모드의 케이스별 동작 원리를 체계적으로 정리하여 향후 기능 고도화 및 유지보수 시 핵심 참조 문서로 활용합니다.

---

## 1. 아키텍처 개요 및 핵심 설계 철학

FastAlias는 Everything 검색창에 입력된 검색어를 SQLite DB에 등록된 사전 매핑 데이터를 기반으로 Everything 네이티브 논리 쿼리(`<path:A | path:B | ...>`)로 실시간 고속 치환(O(1))하는 엔진입니다.

```
[사용자 검색어 입력]
        │
        ▼
[SearchViewModel.Search]
        │
        ├── 1. Options.UseFastAlias 검사
        │
        ▼
[QueryTransformer.Transform]
        │
        ├── 2. 키워드 일치 우선(PrioritizeKeywordMatch) 검사
        │       ├── [ON]  Direct 1:1 매핑 확인 ➡️ 존재 시 단일 정의 치환
        │       └── [OFF/미존재] Transitive Union 매핑 확인 ➡️ 소속 그룹 합집합 치환
        │
        ▼
[EverythingBridge.Search (FFI)] ➡️ Everything SDK 1.4/1.5 IPC 실행
```

---

## 2. FastAlias 데이터 스키마 구조

SQLite `AliasMappings` 테이블은 다음과 같은 구조로 데이터를 관리합니다:

| 컬럼명 | 구분 | 설명 및 역할 |
| :--- | :--- | :--- |
| **`Keyword`** (A열) | 키워드 / 태그명 | 사전의 인덱스 식별자. 일반 단어뿐만 아니라 `#a` 같은 가상 태그/그룹명을 포함할 수 있음. (Primary Key) |
| **`Words`** (B열) | 동의어 풀 (Aliases) | 세미콜론(`;`)으로 구분된 동의어 목록. (예: `b; c`) |

---

## 3. 3대 핵심 쿼리 해석 원칙

### 원칙 1: 원본 키워드 배제 (Option B: Keyword Exclusion)
- **개념**: `#a`와 같이 묶음 역할만 하는 가상 태그(Keyword)나 인덱스용 키워드는 실제 파일명에 존재할 가능성이 거의 없습니다.
- **처리**: 치환 결과(OR 목록) 내부에서 A열(Keyword)에 등록된 원본 키워드명(`#a`, `d`, `h`) 자체는 제외하고, **B열(Words)에 등록된 실제 검색용 별칭들(`b`, `c`, `e`, `i` 등)만 추출**하여 Everything 쿼리 길이를 최적화하고 오탐을 방지합니다.

### 원칙 2: 입력 단어 기준 동적 합집합 (Transitive Dynamic Union)
- **개념**: 여러 그룹에 걸쳐 있는 일반 동의어(예: `c`)를 검색할 경우, 해당 단어가 속한 **모든 그룹의 동의어를 합집합(OR)으로 결합**하여 검색합니다.
- **처리**: `c` 검색 시 ➡️ 그룹 1(`b; c`)과 그룹 2(`e; c`)에 걸쳐 있으므로 두 그룹의 합집합인 `<b | c | e>`로 치환합니다.

### 원칙 3: 키워드 일치 우선 모드 (Keyword Priority)
- **개념**: 검색어가 **[어떤 행의 Keyword(A열)]이자 [다른 행의 Word(B열)]로 중복 등장**하는 경우(예: `e`), 사용자의 의도에 따라 조회의 우선순위를 결정합니다.
  - **체크 ON (키워드 우선)**: "내가 입력한 키워드 `e`의 고유 정의만 보겠다" ➡️ 그룹 3(`e = f; g`)의 정의만 1:1 우선 조회 (`<f | g>`).
  - **체크 OFF (합집합)**: "키워드가 아닌 다른 그룹의 동의어로 보겠다" ➡️ `e`가 동의어로 속한 그룹 2와 그룹 4의 동의어를 합집합으로 결합 조회 (`<e | c | i>`).

---

## 4. 케이스별 종합 동작 매트릭스 (Case Matrix)

### [기본 시나리오 데이터셋 (알파벳 심볼)]

* **그룹 1**: `Keyword = #a` / `Words = b; c`
* **그룹 2**: `Keyword = d` / `Words = e; c`
* **그룹 3**: `Keyword = e` / `Words = f; g`
* **그룹 4**: `Keyword = h` / `Words = i; e`

---

### [케이스별 상세 쿼리 변환 결과]

| 검색어 (입력) | 키워드 일치 우선 (ON) | 키워드 일치 우선 (OFF) | 상세 동작 메커니즘 |
| :--- | :--- | :--- | :--- |
| **`#a`** <br>*(가상 태그 검색)* | `<b \| c>` | `<b \| c>` | **원본 키워드 배제**: 원본 키워드명(`#a`)은 배제되고, Words의 실제 멤버들(`b`, `c`)만 치환됨. |
| **`b`** <br>*(단일 그룹 멤버)* | `<b \| c>` | `<b \| c>` | **단일 그룹 조회**: 그룹 1에만 소속되어 있으므로 모드와 무관하게 그룹 1의 동의어 반환. |
| **`c`** <br>*(다중 소속 일반 단어)* | `<b \| c \| e>` | `<b \| c \| e>` | **동적 전이 합집합**: `c`는 어떤 행의 키워드도 아니므로, 두 모드 모두 그룹 1과 그룹 2의 **합집합** 반환. |
| **`e`** <br>*(그룹3의 키워드이자 그룹2,4의 멤버)* | **`<f \| g>`** <br>*(그룹 3 단독 1:1 매핑)* | **`<e \| c \| i>`** <br>*(그룹 2 + 그룹 4 합집합)* | **키워드 중복 검색**: <br>• **ON (키워드 우선)**: 키워드 `e`의 고유 정의(그룹 3)만 1:1 정밀 조회.<br>• **OFF (합집합)**: `e`가 속한 모든 그룹(그룹 2 + 그룹 4)을 합집합으로 확장 조회. |

---

## 5. 소스코드 참조 및 핵심 구현 위치

향후 동의어 치환 엔진을 확장하거나 디버깅할 때 다음 소스코드를 참조하십시오:

### 1. 인메모리 그룹 인덱싱 및 캐시 빌더
- **파일**: [DatabaseService.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Services/DatabaseService.cs)
- **메서드**: `BuildAliasGroupsCache()` (L94-L184)
  - `originalKeywords`: A열 키워드 블랙리스트 수집 (`#a`, `d`, `e`, `h`)
  - `wordToWordsLists`: 단어별 소속 로우 Words 목록 역인덱스 구성
  - `unionSet.RemoveWhere(originalKeywords.Contains)`: 원본 키워드 치환 목록 배제 로직

### 2. 쿼리 치환 및 스위칭 엔진
- **파일**: [QueryTransformer.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Services/QueryTransformer.cs)
- **메서드**: `BuildFirstStageQuery()` (L283-L353)
  - `options.UseFastAlias`: FastAlias 활성화 여부
  - `options.PrioritizeKeywordMatch`: 키워드 일치 우선 여부에 따른 Direct 매핑 vs Union 매핑 분기 처리
  - `BuildTermWithScope()`: Everything 문법(`path:`, `folder:`, `regex:`, 따옴표) 포맷팅

### 3. 검색 뷰모델 및 비동기 FFI 파이프라인
- **파일**: [SearchViewModel.Search.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/ViewModels/SearchViewModel.Search.cs)
- **메서드**: `ExecuteSearchAsync()` (L180-L208)
  - Stage 0: `DatabaseService.Instance.GetAliasGroupsCache()` 캐시 취득
  - Stage 1: `QueryTransformer.Transform()` 백그라운드 쿼리 변환
  - Stage 2: `EverythingBridge.Search()` FFI IPC 호출

---

## 6. 향후 고도화 시 확장 포인트 (Future Extensibility)

1. **가중치/정확도 기반 정렬 (Ranking)**:
   - 합집합 검색 시, 검색어와 직접 일치하는 그룹의 결과를 상위에 노출하고 전이된 그룹 결과에 랭킹 가중치 부여.
2. **동의어 그룹 시각화 팝업**:
   - 검색창 입력 시 자동완성 팝업에서 이 검색어가 어떤 태그 그룹에 걸쳐 있는지 미리보기(Preview) 제공.
3. **제외어(NOT) 연산자와의 결합 최적화**:
   - `e !i`와 같이 특정 전이 멤버만 선택적으로 제외하는 구문 처리 지원.

