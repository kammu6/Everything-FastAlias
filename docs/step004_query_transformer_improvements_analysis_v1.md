# 📑 QueryTransformer 분석 및 개선 보고서 (v1.1)

본 문서는 사용자가 제시한 복합 검색 테스트 예시 및 피드백을 바탕으로 현재 구현 상태의 문제점과 한계를 재분석하고, Everything 엔진 최적 파싱을 위한 이상적인 To-Be 설계 방향을 최종 확정합니다.

---

## 📌 1. 테스트 시나리오 및 As-Is 결과 비교

### [검색 설정]
* **대상 드라이브:** K, N, P
* **탐색 범위:** 파일 (`SearchScope.File`)
* **미디어 필터:** 영상, 음악
* **커스텀 확장자:** `test1;test2`
* **파일 크기:** `10mb` 이상
* **검색 옵션:** 정규식 활성화 (`UseRegex: true`), 휴지통 포함 (`IncludeRecycleBin: true`)
* **입력 검색어:**
  * **검색단어:** `test`
  * **제외단어:** `a,b;c|d`
  * **지정경로:** `c:\test,d:\test;k:\test|n:\test`
  * **제외경로:** `p:\test`

---

## 🔍 2. 핵심 문제점 및 원인 분석 (피드백 수렴 완료)

### 🟢 문제 1: 휴지통 포함 옵션의 쿼리 미출력 (검증 완료)
* **결론:** 휴지통을 "포함"할 경우 제외 필터(`!$Recycle.Bin`)를 생략하는 것이 정상 동작입니다.

### 🔴 문제 2: 신규 제외경로 (`p:\test`) 반영 누락
* **원인:** `SearchViewModel.Search.cs`의 `optionsCopy` 인스턴스 복사 생성 시 새로 추가한 `ExcludedPaths` 복사문이 누락됨.
* **개선안:** `ExcludedPaths = Options.ExcludedPaths` 복사문 추가로 해결.

### 🔴 문제 3 & 4: 다중 미디어 필터와 커스텀 확장자의 OR 결합 및 괄호 안정성
* **사용자 의견 수렴:** Everything 엔진은 `|`로 나뉠 때 부등호만 사용하면 인식이 안 되는 특징이 있어, `file:` 지시어를 개별 `|` 조건마다 각각 명확히 부여하는 것이 가장 안전합니다.
* **최종 개선안:** 각 미디어 프리셋(및 커스텀 확장자) 마다 개별 `<file:<ext:...>>`을 적용한 뒤 이들을 `|` OR 결합하고 전체를 큰 괄호 `< >`로 묶어 우선순위를 강제합니다. (단, 폴더 프리셋일 때는 `file:` 생략)
  * **To-Be:** `<<file:<ext:mp4;mkv;...>> | <file:<ext:mp3;wav;...>> | <file:<ext:test1;test2>>>`

---

## 💬 3. 추가 질문에 대한 분석 및 토의

### ❓ Q1. regex 수식어도 `<regex:<단어>>` 형태로 완전 래핑하는가?
* **분석:** Everything 공식 문법상 `regex:`는 함수(function)가 아닌 접두 수식어(modifier)입니다. 이를 `<regex:<...>>` 처럼 콜론 앞에 괄호를 씌우면 파서 엔진이 문법 오류로 간주하여 올바른 검색이 작동하지 않습니다.
* **결론:** 현행 방식인 접두사 결합 방식(`<path:regex:<단어>>` 또는 `regex:<단어>`)을 유지하는 것이 문법상 안전합니다.

### ❓ Q2. 탐색 범위가 "파일"일 때 `<nopath:<단어>>` 대신 `<단어>`로 단순 래핑하는가?
* **분석:** `nopath:` 수식어가 항상 강제되면, 사용자가 검색창에 직접 `c:\folder\file.txt` 와 같이 경로가 포함된 복잡한 정규식이나 커스텀 Everything 검색 쿼리를 수동 입력할 때 경로 검색이 작동하지 않는 장애 요인이 됩니다. Everything 엔진은 디폴트로 파일명 우선 매칭을 제공하므로 `nopath:`는 불필요한 제약입니다.
* **결론:** 사용자의 날카로운 피드백대로, 파일 스코프일 때는 `nopath:` 수식어를 생략하고 그냥 일반 `<단어>` (또는 `<regex:<단어>>`) 형태로 단순화하여 입력 유연성을 확보합니다.

---

## 🎯 4. 개선된 이상적인 To-Be 쿼리 설계

### [이상적인 To-Be 최종 쿼리]
```text
<regex:<test>> <<path:<c:\test>> | <path:<d:\test>> | <path:<k:\test>> | <path:<n:\test>>> <path:!<p:\test>> <!<a> | !<b> | !<c> | !<d>> <path:K:\ | path:N:\ | path:P:\> <<file:<ext:mp4;mkv;avi;wmv;flv;mov;webm;m3u8;ts>> | <file:<ext:mp3;wav;flac;ogg;wma;m4a;aac>> | <file:<ext:test1;test2>>> <size:>=10mb>
```

### [세부 구조 비교표]

| 카테고리 구분 | As-Is 결합 형태 | 개선안 To-Be 결합 형태 | 개선 기대효과 |
| :--- | :--- | :--- | :--- |
| **검색어 (File 스코프)** | `<nopath:regex:<test>>` | `<regex:<test>>` | `nopath:` 제거로 커스텀 쿼리 입력 유연성 보장 |
| **지정 경로** | `<<path:<c:\test>> | ...>` | `<<path:<c:\test>> | ...>` | 개별 경로의 `<path:<>>` 완전 래핑 |
| **제외 경로** | *누락* | `<path:!<p:\test>>` | 제외 경로 정상 반영 |
| **제외 단어** | `<!<a> | !<b> | ...>` | `<!<a> | !<b> | ...>` | 단어 제외 유지 |
| **드라이브 제한** | `<path:K:\ | ...>` | `<path:K:\ | ...>` | 드라이브 제한 유지 |
| **확장자 필터** | `<file: ext:mp3;...>` `<ext:test1;test2>` | `<<file:<ext:mp4;...>> | <file:<ext:mp3;...>> | <file:<ext:test1;test2>>>` | 개별 `file:` 래핑 후 OR 병합하여 구문 해석 오류 완벽 차단 |
| **크기 제한** | `<size:>=10mb>` | `<size:>=10mb>` | 파일 크기 제한 유지 |

---

## 🛠️ 5. 이행 계획 (Action Items)

1. **ViewModel 복사 누락 교정:** [SearchViewModel.Search.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/ViewModels/SearchViewModel.Search.cs)에 `ExcludedPaths = Options.ExcludedPaths` 추가.
2. **SearchScope.File 시 nopath: 접두사 소거:** [QueryTransformer.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Services/QueryTransformer.cs)에서 범위가 `File`일 때 `scopePrefix`를 빈 문자열(`""`)로 설정.
3. **확장자 필터 개별 file: OR 결합 구현:** [QueryTransformer.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Services/QueryTransformer.cs)에서 미디어 프리셋별 확장자 그룹과 커스텀 확장자 그룹에 각각 `<file:<ext:...>>`을 개별 부여한 뒤 `|` 로 결합하도록 수정.
4. **단위 테스트 업데이트 및 검증:** `QueryTransformerTest.cs`에 위 시나리오 검증 케이스를 통합하고 `dotnet test`로 확인.

