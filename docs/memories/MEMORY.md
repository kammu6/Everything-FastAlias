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
