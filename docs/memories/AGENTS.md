# Agent Memory Log (AGENTS.md)

이 문서는 AI 에이전트의 작업 원칙 및 핵심 운영 지침을 정의하는 공간입니다. 복잡한 플랫폼별 트러블슈팅 지식은 관련 개발 문서로 분리하고, 본 파일에는 핵심 행동 강령과 지침 링크만을 압축 요약하여 100줄 이내로 콤팩트하게 관리합니다.

---

## 📌 핵심 운영 지침 (Core Guidelines)

1. **신뢰성 95% 우선**: 단일 턴에 모든 해결책을 적용하려는 무모함을 지양합니다. 불확실한 요소가 존재할 경우, 다수의 턴에 걸쳐 정보를 수집하고 점진적으로 계획을 수립 및 검증합니다.
2. **아키텍처 3대 원칙**: 각 모듈의 명확한 역할 분리(SoC), 중복 코드 최소화(DRY), 그리고 하나의 파일에는 하나의 클래스만을 명시하는 원칙(One-Class-Per-File)을 철저히 준수합니다.
3. **IsArtifact: false 준수**: 세션 종료 시 소멸되는 시스템 아티팩트(`IsArtifact: true`)의 사용을 엄격히 배제하고, 작성 및 수정이 필요한 모든 산출물은 `./docs/` 아래의 물리 마크다운 문서로 기록합니다.
4. **UTF-8 표준 인코딩**: 작업 대상 텍스트 및 마크다운 파일은 `UTF-8` 인코딩 표준을 기본으로 채택하여, 에이전트 도구 간의 파싱 호환 오류를 예방합니다.
5. **프로젝트 기틀 기록**: 기술 스택 전면 전환 결정(WPF 데스크톱 어플리케이션 채택) 및 쉘 통합 명세 등 초기 결정 사항은 [overview.md](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything%EA%B2%80%EC%83%89%EA%B8%B0/docs/memories/overview.md)를 참고하십시오.

---

## 📂 기술 도메인별 세부 지식 자산 링크 (Knowledge Bases)

### 🔗 [Everything SDK & Windows Shell Integration](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything%EA%B2%80%EC%83%89%EA%B8%B0/docs/memories/everything_sdk.md)
Everything SDK 연동, 바인딩 마샬링, 대용량 FFI 쿼리 단축 및 Windows Context Menu 연동과 관련된 모든 전문 지식이 요약되어 있습니다.
- `wchar_t*` 기반 Unicode API 사용 규칙
- `BOOL` 마샬링 크래시 방지 및 `Everything_GetLastError` 진입점 바인딩
- 수십만 건 대량 쿼리 및 빈 검색어 단락 회로(Short-circuit) 최적화
- `IContextMenu2/3` Owner Draw 메시지 후킹 및 서브메뉴 렌더링 해결

### 🔗 [WPF & Code Architecture Guidelines](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything%EA%B2%80%EC%83%89%EA%B8%B0/docs/memories/wpf_coding_guidelines.md)
WPF 프레임워크 제어, UI 컴포넌트 커스터마이징, 빌드 충돌 해결 및 대용량 연관어 매핑 검색과 관련된 코딩 레벨의 노하우가 수록되어 있습니다.
- ModernWpf 테마 로딩 예외 방지 및 XAML 호환 해결법
- Windows Forms FFI 명칭 충돌(global using) 차단
- 다중 창 기동 시 트레이 아이콘 생명주기 관리
- 다중 선택 드래그 앤 드롭과 더블클릭 이벤트 간섭 해소 기법
- ObservableCollection 렌더링 병목 및 대용량 사전(Regex) O(1) 캐싱 최적화

---

## 🛠️ 최근 작업 기록 (2026-05-30)
- **스마트 매핑 사전 관리자 모달 전체 초기화**: 사고 방지용 경고 팝업(`MessageBox.Show`) 및 SQLite DB 연동 삭제(`DELETE FROM AliasMappings`)와 메모리 캐시 강제 리로딩을 조합해 구현 완료.
- **로컬 드라이브 동적 토글 칩 (WrapPanel + ToggleButton)**: `System.IO.DriveInfo.GetDrives()`로 고정 드라이브(Fixed)를 동적 감지하여 `ItemsControl`의 `ItemTemplate` 내 `ToggleButton`에 바인딩해 구현. 드라이브가 변경되면 Everything 쿼리에 `<C:|D:>` 형태로 통합 조립.
- **최소 1개 드라이브 선택 보장**: 드라이브 변경 시 체크된 개수가 0개이면 롤백(이벤트 피드백 무한 루프 차단 플래그 적용)시키고 경고 팝업을 발생시켜 최소 1개의 대상 드라이브 검색을 강제화.
- **DB(Sqlite) 기반 영구 설정 실시간 저장**: `AppSettings` 설정 테이블을 구축하여 `ShowOptionPanel`, `UseFastAlias`, 탐색 대상 범위, 제외단어, 지정경로, 드라이브 선택 목록 등을 `SaveSettings()`를 통해 실시간으로 DB에 적재. 앱 구동 시 `LoadSettings()`를 통해 완벽히 복원 처리.
- **스마트 매핑 관리자 내 가로 스크롤 강화**: `DataGrid`에 가로/세로 `ScrollViewer` 표시 여부를 `Auto`로 명시하고 두 번째 컬럼 `MinWidth="450"` 제약을 부여해 행이 짤릴 경우 횡 스크롤링이 완벽히 활성화되도록 개선.
- **WPF TextBlock Custom Highlight Attached Property 구현**: `HighlightBehavior` 클래스를 추가하여 원본 텍스트(`OriginalText`)와 검색어(`SearchText`) 변경 시 TextBlock의 Inlines를 분석하여 일치 부분을 `Background = Yellow`인 `Run` 객체들로 대체 적용하는 고성능 실시간 하이라이팅을 XAML `DataGridTemplateColumn` 형태로 설계/적용.
- **VSCode 스타일 사전 내 텍스트 조회 및 F3/Shift+F3 단축키 바인딩**:
  - `AliasManagerWindow` 우측 상단에 미니 검색 텍스트박스 및 매칭 카운트(`현재 일치 인덱스 / 총 매칭 개수`)를 배치.
  - 이전(▲), 다음(▼) 버튼 클릭 또는 `F3` / `Shift+F3` 단축키 바인딩(`KeyBinding` 구성) 발생 시 매칭 인덱스를 가리키며 `DataGrid.ScrollIntoView` 이벤트를 발생시켜 해당 행으로 포커스 자동 스크롤 연동.
- **모달창 가로 폭 확장 및 가이드 문구 분리**: 모달창 기본 가로 크기를 `980`, 최소 가로 크기를 `920`으로 확장하고, 겹치던 동의어 가이드 문구를 하단 버튼바 우측 공간으로 이동 배치하여 상단 툴바의 버튼 영역과 검색바가 겹치지 않고 온전히 표시되도록 레이아웃 개선.
- **검색 매칭 상태 텍스트 겹침 버그 해결**: ModernWpf TextBox의 자동 `✕` 지우기 버튼과 겹치던 `SearchStatusText` (몇 번째 / 총 개수) 블록을 TextBox 내부에서 꺼내어 TextBox 우측 외부에 격리 배치하여 텍스트 및 지우기 기능이 정상 렌더링되도록 수정.
