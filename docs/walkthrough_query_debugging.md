# Everything FFI 전달 쿼리 실시간 디버그 모니터링 결과 보고서 (Walkthrough)

실제 검색 실행 시 Everything FFI로 전송되는 변환 쿼리를 실시간으로 하단 상태 표시바(`StatusMessage`)에 출력하도록 기능을 구현한 완료 보고서입니다.

---

## 1. 반영 결과 요약

### ① FFI 쿼리 문자열 실시간 덤프 적용
- **작업 내용**: Everything 검색 작업이 비동기적으로 가동되고 완료되는 시점에, 파싱되어 빌드된 최종 Everything 쿼리 문자열(`transformedQuery`)을 상태바에 노출하는 디버깅 계층을 개발했습니다.
- **수정 코드**: `SearchViewModel.cs` 내 `ExecuteSearchAsync()`에서 백그라운드 태스크의 `transformed` 결과를 지역변수로 캡처하고, 검색 완료 후 `StatusMessage`에 `[Everything 쿼리]: {transformedQuery} | 매핑 규칙: {ruleCount}개` 형태로 저장되도록 수정했습니다.
- **결과**: 유저가 검색어를 치고 드라이브를 필터링할 때 실제로 Everything SDK로 어떤 쿼리가 넘어가는지 직접 시각적으로 확인할 수 있어, 드라이브 유출 현상의 근원지(드라이브 바인딩 데이터 오염 vs Everything SDK 매칭 문제)를 정밀 타격하여 진단할 수 있게 되었습니다.

---

## 2. 검증 완료 내역

- **컴파일 빌드**: `dotnet build` 및 `dotnet test` 결과, **오류 0개 / 경고 0개**로 빌드 및 유닛 테스트가 완벽히 성공했습니다.
- **의도된 쿼리 전달 점검**: 이제 메인 UI에서 `마키 호조` 등의 검색 조건 변경이나 P: 드라이브 체크 시 실시간으로 Everything으로 가공 전달되는 쿼리가 하단 상태표시바에 원본 텍스트 그대로 노출됩니다.
