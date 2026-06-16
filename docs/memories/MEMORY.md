# Agent Memory Log

이 문서는 AI 에이전트의 작업 원칙 및 핵심 운영 지침을 정의하는 공간입니다. 복잡한 플랫폼별 트러블슈팅 지식은 관련 개발 문서로 분리하고, 본 파일에는 핵심 행동 강령과 지침 링크만을 압축 요약하여 100줄 이내로 콤팩트하게 관리합니다.

---

## 📌 핵심 운영 지침 (Core Guidelines)

1. **신뢰성 95% 우선**: 단일 턴에 모든 해결책을 적용하려는 무모함을 지양합니다. 불확실한 요소가 존재할 경우, 다수의 턴에 걸쳐 정보를 수집하고 점진적으로 계획을 수립 및 검증합니다.
2. **아키텍처 3대 원칙**: 각 모듈의 명확한 역할 분리(SoC), 중복 코드 최소화(DRY), 그리고 하나의 파일에는 하나의 클래스만을 명시하는 원칙(One-Class-Per-File)을 철저히 준수합니다.
3. **IsArtifact: false 준수**: 세션 종료 시 소멸되는 시스템 아티팩트(`IsArtifact: true`)의 사용을 엄격히 배제하고, 작성 및 수정이 필요한 모든 산출물은 `./docs/` 아래의 물리 마크다운 문서로 기록합니다.
4. **UTF-8 표준 인코딩**: 작업 대상 텍스트 및 마크다운 파일은 `UTF-8` 인코딩 표준을 기본으로 채택하여, 에이전트 도구 간의 파싱 호환 오류를 예방합니다.
5. **프로젝트 기틀 기록**: 기술 스택 전면 전환 결정(WPF 데스크톱 어플리케이션 채택) 및 쉘 통합 명세 등 초기 결정 사항은 [overview.md](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything%EA%B2%80%EC%83%89%EA%B8%B0/docs/memories/overview.md)를 참고하십시오.

---

## 🛠️ 최근 작업 기록 (2026-06-16)

- **DeepWiki Repositories 명세 문서화**: [overview.md](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything%EA%B2%80%EC%83%89%EA%B8%B0/docs/memories/overview.md)에 등재된 WPF 데스크톱 런타임, ModernWPF, CommunityToolkit.Mvvm, Everything SDK, Microsoft.Data.Sqlite, ExcelDataReader, Lucide 등의 공식 GitHub 레포지토리 정보와 라이브러리 개요를 [deepwiki_repos.md](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything%EA%B2%80%EC%83%89%EA%B8%B0/docs/memories/deepwiki_repos.md) 파일로 구축하여 자산화함.
- **보기 옵션(자세히/섬네일S/M/L) 구현 및 썸네일 비동기 지연 로드 반영**:
  - `IShellItemImageFactory` COM 인터페이스를 사용한 Win32 썸네일 로더 및 `SHGetFileInfo`를 사용한 기본 아이콘 헬퍼를 추가하여 이미지/비디오 등의 썸네일을 디스크 I/O 레벨에서 추출.
  - 리소스 누수(GDI Handle Leak) 방지를 위해 WPF `BitmapSource` 변환 직후 `DeleteObject`를 명시적으로 호출해 메모리를 환수하였으며, 백그라운드 스레드 크로스 도메인 예외를 막기 위해 `Freeze()` 캐싱 기법을 적용함.
  - 대량 데이터 조회 시의 UI 프리징 방지를 위해 `SemaphoreSlim(4)` 제한을 둔 비동기 지연 로딩 방식을 `SearchResultItem` 속성 게터에 설계하여 성능 오버헤드를 극소화하고, 고화질 렌더링을 위해 추출 해상도를 최대 192px로 상향함.
  - WPF `ListView.Style.Triggers`를 도입해 `ViewMode` 변경에 따라 Details(GridView)와 Thumbnail(VirtualizingWrapPanel) 간 뷰 구조를 동적으로 전환하며, 가상 스크롤(Virtual Scroll) 및 픽셀 스크롤링(`ScrollUnit=Pixel`)을 결합해 대량 데이터 바둑판 뷰의 렌더링 렉을 원천 제거함.
  - 사용자의 보기 방식 크기 가독성을 위해 M 모드는 S의 2배(96px), L 모드는 S의 3배(144px) 수치로 썸네일 카드 가로세로 스케일을 균일 증가 매핑함.
  - `AppSettings` SQLite 로컬 영속화 계층에 `ViewMode` 컬럼을 엮어 앱이 재구동되어도 마지막 사용 보기 상태가 유지되도록 보존 기능을 반영함.
  - **[트러블슈팅] ListView 뷰 공유 충돌**: WPF의 `GridView`는 단일 인스턴스 제한이 있어 여러 뷰 상태 변경 중 공유 충돌(`InvalidOperationException`)을 유발함. 리소스 딕셔너리의 `GridView` 정의부에 `x:Shared="False"` 속성을 명시해 매번 새로운 독립 인스턴스를 반환하도록 구성하여 해결함.

