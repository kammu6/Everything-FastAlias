# Agent Memory Log

이 문서는 AI 에이전트의 작업 원칙 및 핵심 운영 지침을 정의하는 공간입니다. 복잡한 플랫폼별 트러블슈팅 지식은 관련 개발 문서로 분리하고, 본 파일에는 핵심 행동 강령과 지침 링크만을 압축 요약하여 100줄 이내로 콤팩트하게 관리합니다.

---

## 📌 핵심 운영 지침 (Core Guidelines)

1. **신뢰성 95% 우선**: 단일 턴에 모든 해결책을 적용하려는 무모함을 지양합니다. 불확실한 요소가 존재할 경우, 다수의 턴에 걸쳐 정보를 수집하고 점진적으로 계획을 수립 및 검증합니다.
2. **아키텍처 3대 원칙**: 각 모듈의 명확한 역할 분리(SoC), 중복 코드 최소화(DRY), 그리고 하나의 파일에는 하나의 클래스만을 명시하는 원칙(One-Class-Per-File)을 철저히 준수합니다.
3. **IsArtifact: false 준수**: 세션 종료 시 소멸되는 시스템 아티팩트(`IsArtifact: true`)의 사용을 엄격히 배제하고, 작성 및 수정이 필요한 모든 산출물은 `./docs/` 아래의 물리 마크다운 문서로 기록합니다.
4. **UTF-8 표준 인코딩**: 작업 대상 텍스트 및 마크다운 파일은 `UTF-8` 인코딩 표준을 기본으로 채택하여, 에이전트 도구 간의 파싱 호환 오류를 예방합니다.
5. **프로젝트 기틀 기록**: 기술 스택 전면 전환 결정(WPF 데스크톱 어플리케이션 채택) 및 쉘 통합 명세 등 초기 결정 사항은 [overview.md](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/docs/memories/overview.md)를 참고하십시오.
6. **워크스루 제한**: 사용자의 명시적인 별도 지시가 있기 전까지는 `walkthrough.md` 문서(작업 완료 보고서)를 신규 생성하거나 수정하지 않습니다.

---

## 🛠️ 최근 작업 기록 (시간 순 정렬)

- **2026-06-16 (WPF 인터랙션 & 쉘 통합)**: WPF 이름 변경 ViewState 도입, Rubber Band 다중 선택 구현, F5 새로고침 단축키 추가, 네이티브 휴지통 삭제 및 복사 진행창 연동 완료 ➔ [wpf_coding_guidelines.md](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything%EA%B2%80%EC%83%89%EA%B8%B0/docs/memories/wpf_coding_guidelines.md) 이관 완료.
- **2026-06-17 (Alias Manager 고도화)**: Alias Manager 행 추가 최상단 삽입, GotFocus 자동 포커싱 및 SelectAll 비동기 렌더링, 정렬 기준 SQLite 로컬 DB 연동 완료 ➔ [wpf_coding_guidelines.md](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything%EA%B2%80%EC%83%89%EA%B8%B0/docs/memories/wpf_coding_guidelines.md) 이관 완료.
- **2026-06-20 (상태창 복사 & 제외 조건)**: 상태창 원시 Everything 쿼리 클립보드 복사 기능 및 세미콜론`;` 구분자 기반의 AND 제외 쿼리 자동 조립 규칙 완료 ➔ [everything_sdk.md](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything%EA%B2%80%EC%83%89%EA%B8%B0/docs/memories/everything_sdk.md) 이관 완료.
- **2026-06-29 (정규식 API 왜곡 우회 & 사전 조립)**: Everything 정규식 검색 활성화 시 전체 쿼리가 정규식화 되어 깨지는 부작용을 방지하기 위해 SDK `SetRegex`는 비활성화하고 쿼리 파서 단에서 일반 검색 단어들에만 개별 `regex:` 수식어를 씌우는 우회 기법 적용, 그리고 사전 토큰 조립(Pre-compilation) 및 폴더 프리셋 시 사이즈 필터 생략 리팩토링 완료 ➔ [everything_sdk.md](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything%EA%B2%80%EC%83%89%EA%B8%B0/docs/memories/everything_sdk.md) 이관 및 글로벌 [everything-sdk](file:///D:/3_Code/3_Apps/31_Principle/12_Skills/01_적용/gemini/everything-sdk/SKILL.md) 공용 스킬 자산화 완료.
- **2026-06-29 (SQLite 쓰기 & 썸네일 가상화 성능 최적화)**: 옵션 세터의 `SaveSettings()` 중복 호출을 소거하고, `SaveSettingsBulk` SQLite 트랜잭션 처리를 구축하여 텍스트 타이핑 시의 UI 스레드 동결 현상을 해결함. `WeakReference` 글로벌 썸네일 캐시 및 `CancellationToken` 스레드 작업 취소 메커니즘을 적용해 스크롤 I/O 부하와 메모리 누수를 극대화 차단함. `IShellItemImageFactory` COM 객체 사용 즉시 `ReleaseComObject` 적용. **추가적으로 SQLite의 동시성 락 충돌 방지를 위해 연결 문자열에 `Default Timeout=5`를 반영하고, Excel 엑스포트/임포트 시 한글 깨짐 예방을 위해 CSV fallback 인코딩을 `CP949(EUC-KR)`로 개선함** ➔ [everything_sdk.md](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything%EA%B2%80%EC%83%89%EA%B8%B0/docs/memories/everything_sdk.md) 이관 완료.
- **2026-07-17 (프로젝트 개요 트리 자동화 & C# 빌드 소음 차단)**: `overview.md`에 `<!-- START_TREE -->` 마커를 복원하고 `update_overview_tree.js`를 C# WPF 환경에 최적화함. `bin`, `obj`, `.vs`, `TestResults`, `TempRunner` 등 임시 빌드/테스트 캐시 폴더를 차단 필터에 삽입하여 핵심 파일 위주의 트리를 제공하고, `tests` 디렉토리 하위 스캔을 무력화하여 토큰 낭비 및 트리 비대화를 성공적으로 억제함. 또한 수동으로 남겨둔 설명 주석들이 스마트 캐싱(`extractComments`)을 통해 트리 자동 갱신 시에도 누락 없이 재매핑 및 정렬되도록 검증 완료.
- **2026-07-17 (쿼리 파서 개편 및 제외경로 대칭 구현)**: 연산자 우선순위 충돌 방지를 위한 카테고리별 개별 부등호 래핑 및 중첩 `<path:<...>>`, `<path:!<...>>` 그룹화 규칙이 적용된 6단계 빌드 파이프라인 전면 도입. UI 및 모델/뷰모델 레이어에 "제외경로" TextBox와 프로퍼티 바인딩, DB 영속성 관리를 대칭 구현하고 8개 MSTest TDD 검증 및 zero-warning 컴파일/빌드 성공 확인.
- **2026-07-17 (Alias 다중 그룹 동적 합집합 - Option B)**: `DatabaseService.BuildAliasGroupsCache()`를 무방향 그래프 연결 요소 알고리즘에서 **동적 다중 그룹 매칭(Precomputed Union)**으로 전면 교체. `originalKeywords` 블랙리스트 구성 → 단어별 소속 로우(Words)들의 합집합 계산 → 원본 키워드 제거(Option B) → 자기 자신 보존의 4단계 사전 계산 방식으로, `#back_to_freedom` 같은 가상 태그 키워드가 쿼리 치환 목록에 포함되지 않도록 하여 불필요한 인덱스 검색 차단. 동의어 입력 시 역방향 매핑 및 공통 원소를 통한 그룹 전이 합집합도 정상 작동 확인 (예: `c` → `a | b | c | d | f`). `QueryTransformer` 시그니처도 `Dictionary<string, HashSet<string>>`로 통일.
- **2026-07-17 (검색 성능 병목 분석 및 최적화)**: 단계별 Stopwatch 프로파일링 코드를 `SearchViewModel.ExecuteSearchAsync`에 삽입하여 `%APPDATA%\EverythingFastAlias\perf.log`로 출력. 실측 결과: Stage 1(QueryTransformer) 191ms, Stage 2(FFI) 16,884ms (결과 10건). **핵심 발견**: Stage 1의 191ms는 32,316개 키를 순차 `foreach`로 탐색하는 O(n) 버그였음. `Dictionary.TryGetValue` O(1) 직접 조회로 교체하여 **<1ms로 개선**. Stage 2의 16,884ms는 17개 OR alias × 3개 대용량 드라이브 교차 스캔으로 인한 Everything 엔진 부하로, **TTL 3초 쿼리 결과 캐시를 `EverythingBridge`에 추가**하여 동일 쿼리 반복 호출 시 즉시 반환하도록 구현. `EverythingBridge.InvalidateQueryCache()` 메서드로 DB 업데이트/옵션 변경 시 강제 무효화 지원.
- **2026-07-17 (크기 필터 및 자동 검색 버그 수정)**: 
  1. 조건 초기화(`ExecuteReset`) 시 백킹 필드만 초기화되고 `Options.MinSize` 및 `MaxSize`가 `null`로 할당되지 않아 쿼리에 필터가 잔존하던 버그 수정.
  2. "크기 초기화" 버튼 클릭 시 다른 옵션을 지정하기도 전에 자동으로 검색이 즉각 구동되던 `vm.ExecuteSearch()` 종속성을 `LeftSidebarView.xaml.cs`에서 제거하여 사용자 편의성 증대.

