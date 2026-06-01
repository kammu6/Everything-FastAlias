# Everything FFI 전달 쿼리 실시간 디버그 모니터링 계획서 (Temporary Plan)

실제 검색 요청 시 Everything64.dll로 전달되는 최종 가공 쿼리를 UI 상에서 모니터링할 수 있도록 설계하여, 검색 드라이브 범위 일탈 현상이 쿼리 가공 오염에서 기인한 것인지 아니면 엔진 내부의 문제인지 명확하게 규명하기 위한 디버깅용 임시 계획서입니다.

---

## 1. 요구사항 및 진단 (Evaluation)

### ① 드라이브 유출 현상 진단 요구
- 유닛 테스트 상에서 `<마키 호조 | "Maki Hojo" ...> <P:>` 쿼리를 Everything SDK에 질의했을 때는 오직 `P:` 드라이브의 파일만 정확히 출력되며 테스트가 완전 통과(Green)했습니다.
- 그럼에도 실제 애플리케이션 상에서 O: 드라이브 등 타 드라이브 폴더가 조회되는 현상은 아래 두 가지 원인 중 하나로 압축됩니다:
  1. 실제 런타임 시점의 드라이브 바인딩(`Drives` 컬렉션의 IsChecked 상태) 및 DB 영구 저장 설정 복원 로직에 꼬임이 생겨, 겉으로 보이는 체크 상태와 무관하게 실제 쿼리에 `<N: | O: | P:>` 가 모두 포함되어 전달되는 현상.
  2. Everything SDK 또는 Service 인스턴스가 실행될 때 다른 환경 변수로 인해 필터를 오해석하는 현상.
- 이를 명확하게 판별하기 위해 **Everything SDK로 직접 전송되는 최종 파싱 쿼리 문자열(transformed)**을 메인 화면의 하단 상태표시줄(`StatusMessage`)에 출력하여 유저가 쿼리 원형을 눈으로 교차 검증할 수 있도록 지원합니다.

---

## 2. 세부 구현 계획 (Implementation Plan)

### [Component 1] ViewModels

#### [MODIFY] [SearchViewModel.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/ViewModels/SearchViewModel.cs)
- `ExecuteSearchAsync()` 내의 비동기 검색 작업 블록을 리팩토링하여, 백그라운드 스레드에서 생성된 최종 가공 쿼리(`transformed`)를 로컬 변수 `transformedQuery`에 캡처합니다.
- 검색 완료 후 `StatusMessage`에 기존의 고정 상태 텍스트 대신 `[Everything 쿼리]: {transformedQuery} | 매핑 규칙: {ruleCount}개` 형태로 최종 쿼리를 덤프합니다.

---

## 3. Verification Plan (검증 계획)

### 빌드 및 결과 검증
1. `dotnet build`를 실행하여 컴파일 오류가 없는지 검증.
2. 애플리케이션 가동 후 `마키 호조` 검색 시, 하단 상태바에 어떤 쿼리가 Everything으로 넘어갔는지 덤프된 텍스트 확인:
   - 예: 만약 덤프 쿼리에 `<P:>`만 찍혔는데 O:가 나왔다면 Everything FFI 필터링 결함.
   - 예: 만약 덤프 쿼리에 `<O: | P:>` 등 타 드라이브가 같이 포함되어 찍혔다면 드라이브 토글 바인딩 및 세션 싱크 결함으로 판독.
