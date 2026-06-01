# 조언 AI 피드백 평가 및 안정성 개선 계획서 (Temporary Plan)

외부 조언 AI가 분석한 Everything FastAlias 코드 리뷰 내용에 대해 프로젝트 실제 구현 코드를 분석하여 타당성을 검증하고, 필요한 개선 및 보완점을 정리한 임시 계획서입니다.

---

## 1. 조언 AI 제안사항 타당성 검증 결과 (Evaluation)

### ① ApplySorting() O(N²) 루프 및 UI 프리징 문제
- **진단**: **매우 타당함 (Valid)**
- **이유**: `SearchViewModel.cs`의 `ApplySorting()`은 정렬된 결과 리스트(`sorted`)와 원래 컬렉션(`Results`)을 비교하여 O(N)의 `IndexOf` 탐색 후 `Move`를 매번 호출합니다. 이는 최악의 경우 O(N²)가 되어 수천~수만 건의 검색 결과 정렬 시 심각한 UI 쓰레드 프리징을 초래합니다. `ReplaceRange` 메서드를 통해 컬렉션 변경 알림을 단 한 번만 발생(Reset)시키는 것이 아키텍처 및 성능 상 적합합니다.

### ② 비동기 검색(Task.Run) 레이스 컨디션 문제
- **진단**: **매우 타당함 (Valid)**
- **이유**: 사용자가 빠른 타이핑 후 검색어를 지우면 `SearchQuery` Setter에서 `Results.Clear()`가 실행되지만, 백그라운드에서 진행 중이던 이전 쿼리의 비동기 검색 태스크가 완료되면서 `Results.ReplaceRange`를 호출해 빈 화면에 이전 결과를 다시 채우거나 화면 깜빡임(Flickering)이 유발되는 불일치 현상이 발생할 수 있습니다. 결과 갱신 전 캡처해 둔 `query`와 최신 `SearchQuery`가 일치하는지 검증하는 방어 코드가 필요합니다.

### ③ 확장자 필터(CustomExtensions) 공백 처리 버그
- **진단**: **매우 타당함 (Valid)**
- **이유**: 사용자가 확장자 필터에 `exe, dll` 처럼 공백을 포함해 입력하면 Everything 쿼리에 `ext:exe; dll` 형식으로 전달됩니다. Everything 문법에서 공백은 `AND` 연산자이므로 "확장자가 exe이면서 단어 dll을 포함하는 파일"을 검색하게 되어 비정상적인 결과가 초래됩니다. 세미콜론이나 쉼표 앞뒤의 공백을 제거하고 순수 세미콜론`;`으로만 연결되도록 정규식 변환 처리가 시급합니다.

### ④ 대량 검색 결과 내보내기(Export) UI 블로킹 및 OOM 문제
- **진단**: **매우 타당함 (Valid)**
- **이유**: `MainWindowViewModel.cs`의 `ExportResults()` 메서드는 대량의 검색 결과가 존재할 때 `StringBuilder`에 모두 올린 후 `File.WriteAllText`를 동기로 실행합니다. 이는 파일 저장 시간 동안 UI 스레드를 멈춰 앱을 "응답 없음"으로 만들 뿐만 아니라 가상 메모리를 대량 점유하여 OOM(Out of Memory) 크래시를 유발할 수 있습니다. `Task.Run` 비동기 스레드와 `StreamWriter` 버퍼 방식의 한 줄 쓰기 구조로 전환해야 합니다. 
- **보완 사항**: 비동기 작업 중 UI 컬렉션에 접근하면 크래시가 나므로, UI 스레드에서 먼저 `SearchVM.Results.ToList()` 스냅샷을 생성하여 넘겨주도록 안전하게 설계해야 합니다.

### ⑤ 동의어 치환 시 플레이스홀더(Placeholder) 간섭 충돌 위험
- **진단**: **타당함 (Valid)**
- **이유**: `QueryTransformer.cs`의 `ReplaceAliases()` 내에서 고정 키(`__ALIAS_PLACEHOLDER_0__`)를 사용하면, 사용자가 우연히 또는 악의적으로 해당 플레이스홀더와 겹치는 검색어를 입력할 때 치환 대상이 오염되어 문자열이 꼬이는 오류가 발생할 수 있습니다. 치환 수행 시마다 고유한 `Guid` 문자열을 접미사 등으로 조합한 유일무이한 임시 키를 동적 생성함으로써 해결할 수 있습니다.

---

## 2. 세부 구현 계획 (Implementation Plan)

### [Component 1] ViewModels

#### [MODIFY] [SearchViewModel.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/ViewModels/SearchViewModel.cs)
- `ApplySorting()` 내의 기존 `for` 루프와 `Move`를 제거하고, `Results.ReplaceRange(sorted)`를 적용하여 일괄 정렬 처리 구현.
- `ExecuteSearchAsync()` 내에서 비동기 `Task.Run` 돌입 전 `query` 변수를 지역변수로 캡처.
- 비동기 대기(`await Task.Run(...)`)에서 탈출한 후 `Results.ReplaceRange(...)` 및 `ApplySorting()` 호출 직전에 `if (query != SearchQuery) return;` 방어 로직 추가.

#### [MODIFY] [MainWindowViewModel.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/ViewModels/MainWindowViewModel.cs)
- `ExportResults()`를 `async void` 비동기 메서드로 선언.
- 파일 다이얼로그 확인 완료 후 UI 스레드 상에서 `SearchVM.Results.ToList()`를 수행하여 검색 결과의 로컬 스냅샷 `snapshot`을 안전하게 복사.
- `SearchVM.StatusMessage = "내보내는 중...";` 등으로 UI 상태 메시지 제공.
- `await Task.Run(() => { ... })` 내부에서 `StreamWriter`를 열어 복사한 `snapshot` 데이터를 스트림 방식으로 파일에 순차적 쓰기 진행.
- 완료 후 `SearchVM.StatusMessage`를 "Everything 서비스 활성화 완료..." 또는 이전 상태로 안전하게 복귀.

### [Component 2] Services

#### [MODIFY] [QueryTransformer.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Services/QueryTransformer.cs)
- `BuildOptionConstraints()` 메서드 내 `CustomExtensions` 처리 블록에서 정규식을 활용하여 세미콜론 및 콤마 좌우 공백을 제거하도록 개선:
  ```csharp
  var exts = Regex.Replace(options.CustomExtensions, @"\s*[,;]\s*", ";").Trim(';');
  sb.Append($"ext:{exts}");
  ```
- `ReplaceAliases()` 내에서 `Guid.NewGuid().ToString("N")`을 사용하여 매번 다른 세션용 고유 접미사(`placeholderKey`)를 생성.
- 임시 변경 플레이스홀더 규격을 `$"__ALIAS_{placeholderKey}_{tempReplacements.Count}__"` 형태로 동적 생성하여 대치 및 환원하도록 갱신.

---

## 3. 검증 계획 (Verification Plan)

### 자동 빌드 및 디버그 검증
1. `dotnet build` 명령어를 통한 정상 컴파일 완료 여부 확인.
2. 애플리케이션 실행 후 아래 항목에 대한 기능 및 신뢰성 테스트 진행:
   - **정렬 성능 테스트**: 검색 결과가 10,000건 이상 존재할 때 컬럼 헤더 클릭 시 즉각 정렬 여부(UI 프리징 확인).
   - **레이스 컨디션 테스트**: 검색창에 단어를 빠르게 입력한 후 바로 다 지웠을 때 화면에 검색 잔상이 남거나 플리커링이 발생하는지 확인.
   - **확장자 공백 처리 테스트**: 확장자 필터에 `mp4, mkv` (쉼표 뒤 공백 포함) 또는 `pdf ; txt` 입력 후 파일 분류 및 확장자가 정상 필터링되어 검색되는지 검증.
   - **내보내기 프리징 및 OOM 방지 테스트**: 대량의 데이터 결과에서 '결과 내보내기' 진행 시 UI 스레드가 멈추지 않고 실시간으로 파일이 비동기 작성되는지 검증.
   - **동의어 겹침 방지 테스트**: 동의어 매핑 목록이 존재하는 환경에서 유저가 의도적으로 `__ALIAS_` 단어를 검색어에 혼합하여 입력해도 비정상 치환으로 인한 오동작이 없는지 최종 교차 점검.

---

## 4. 자산화 계획 (Assetization Plan)
- 개선 완료 후, 실제 수행 결과 및 검증 결과를 바탕으로 `temporary_plan_ai_advice_evaluation.md` 문서를 폐기하거나 완료 보고서(`walkthrough_ai_advice.md`)로 갱신하여 `./docs/` 폴더에 asset화합니다.
- 변경 과정 중 얻은 MVVM/WPF 병목 극대화 방안 및 P/Invoke 최적화 교훈은 `./docs/memories/AGENTS.md`에 non-trivial 요약 내용으로 반영합니다.
