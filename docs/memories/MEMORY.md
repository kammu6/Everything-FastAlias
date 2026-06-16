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

### [F2 인라인 이름변경 ViewState 상태 머신 재설계] — 2026-06-16 22:00
- **문제**: F2 키 한 번 누르면 마우스 ban 아이콘 고착 + 앱 전체 먹통. ESC도 무응답.
- **근본 원인**: `DoDragDrop`(동기 블로킹) 실행 중 내부 메시지 루프에서 TextBox의 `LostFocus`가 발화 → `CancelRename` 호출 → `IsEditing=false`. 그런데 DoDragDrop이 아직 UI 스레드를 점유 중이라 WPF 메시지 루프가 비정상 상태로 빠짐.
- **해결 전략**: `ResultGridView.xaml.cs`에 `enum ViewState { Idle, Dragging, Editing }` 상태 머신 도입.
  - **Editing 상태**: `ListViewItem_MouseMove`에서 드래그 감지를 원천 차단 (`_viewState != Idle`이면 즉시 return)
  - **Dragging 상태**: `RenameBox_LostFocus`에서 처리 무시 (`_viewState == Dragging`이면 return)
  - **포커스**: `RenameBox_Loaded`에서 `DispatcherPriority.Loaded` + `Keyboard.Focus(tb)` + `tb.Focus()` 병행
  - **더블클릭**: `ListViewItem_PreviewMouseLeftButtonDown`에서 `e.ClickCount==2` 조기 감지 → `OpenFile()` 정적 헬퍼 호출 후 return (ListView.MouseDoubleClick 이벤트 제거)
  - **ESC/LostFocus**: `CancelRename` 호출 후 `_viewState = Idle` 복원 + `ResultsListView.Focus()` 복귀
- **XAML 변경**: `MouseDoubleClick="ResultsListView_MouseDoubleClick"` 이벤트 연결 제거 (중복 처리 방지)
- **빌드 결과**: 경고 0개, 오류 0개 ✅



- **DeepWiki Repositories 명세 문서화**: [overview.md](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything%EA%B2%80%EC%83%89%EA%B8%B0/docs/memories/overview.md)에 등재된 WPF 데스크톱 런타임, ModernWPF, CommunityToolkit.Mvvm, Everything SDK, Microsoft.Data.Sqlite, ExcelDataReader, Lucide 등의 공식 GitHub 레포지토리 정보와 라이브러리 개요를 [deepwiki_repos.md](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything%EA%B2%80%EC%83%89%EA%B8%B0/docs/memories/deepwiki_repos.md) 파일로 구축하여 자산화함.
- **보기 옵션(자세히/섬네일S/M/L) 구현 및 썸네일 비동기 지연 로드 반영**:
  - `IShellItemImageFactory` COM 인터페이스를 사용한 Win32 썸네일 로더 및 `SHGetFileInfo`를 사용한 기본 아이콘 헬퍼를 추가하여 이미지/비디오 등의 썸네일을 디스크 I/O 레벨에서 추출.
  - 리소스 누수(GDI Handle Leak) 방지를 위해 WPF `BitmapSource` 변환 직후 `DeleteObject`를 명시적으로 호출해 메모리를 환수하였으며, 백그라운드 스레드 크로스 도메인 예외를 막기 위해 `Freeze()` 캐싱 기법을 적용함.
  - 대량 데이터 조회 시의 UI 프리징 방지를 위해 `SemaphoreSlim(4)` 제한을 둔 비동기 지연 로딩 방식을 `SearchResultItem` 속성 게터에 설계하여 성능 오버헤드를 극소화하고, 고화질 렌더링을 위해 추출 해상도를 최대 192px로 상향함.
  - WPF `ListView.Style.Triggers`를 도입해 `ViewMode` 변경에 따라 Details(GridView)와 Thumbnail(VirtualizingWrapPanel) 간 뷰 구조를 동적으로 전환하며, 가상 스크롤(Virtual Scroll) 및 픽셀 스크롤링(`ScrollUnit=Pixel`)을 결합해 대량 데이터 바둑판 뷰의 렌더링 렉을 원천 제거함.
  - 사용자의 보기 방식 크기 가독성을 위해 M 모드는 S의 2배(96px), L 모드는 S의 3배(144px) 수치로 썸네일 카드 가로세로 스케일을 균일 증가 매핑함.
  - `AppSettings` SQLite 로컬 영속화 계층에 `ViewMode` 컬럼을 엮어 앱이 재구동되어도 마지막 사용 보기 상태가 유지되도록 보존 기능을 반영함.
  - **[트러블슈팅] ListView 뷰 공유 충돌**: WPF의 `GridView`는 단일 인스턴스 제한이 있어 여러 뷰 상태 변경 중 공유 충돌(`InvalidOperationException`)을 유발함. 리소스 딕셔너리의 `GridView` 정의부에 `x:Shared="False"` 속성을 명시해 매번 새로운 독립 인스턴스를 반환하도록 구성하여 해결함.
  - **[트러블슈팅] F2 이름변경 시 마우스/키보드 포커스 및 드래그 앤 드롭 락 해소**:
    - **원인**: 드래그 앤 드롭 및 더블클릭 이벤트 충돌 방지를 위해 `ListViewItem_PreviewMouseLeftButtonDown` 내부에서 `e.Handled = true`로 마우스 메시지 전파를 완전 차단함으로써 하위 자식 요소인 `TextBox`까지 마우스 클릭이 전달되지 않아 커서 활성화가 불가능했음. 또한, TextBox를 탭하여 포커스가 들어갔을 때 마우스를 미세하게 움직이면 `ListViewItem_MouseMove`가 이전 마우스 클릭의 좌표(`_startPoint`)를 기준으로 파일 드래그 앤 드롭(`DragDrop.DoDragDrop`)을 오발하였으며, 동기식 드래그 세션이 마우스/키보드 입력을 독점해 금지(ban) 마우스 아이콘이 뜨며 앱이 교착 상태에 빠지게 됨.
    - **해결**: `PreviewMouseLeftButtonDown`과 `ListViewItem_MouseMove` 시작 지점에서 마우스 발원지(`e.OriginalSource`)가 `TextBox` 자식인지 비주얼 트리 상에서 판별하여 TextBox 영역 내 마우스 동작 시 드래그 드롭 세션이나 가로채기가 타지 않고 즉시 조기 반환(`return;`)하도록 격리함. 또한, `TextBox.Loaded` 시 `Dispatcher.BeginInvoke(DispatcherPriority.Input, ...)` 지연 처리를 가미해 렌더링 완료 직후 포커스 획득 및 텍스트 전체 선택이 확실히 수행되도록 고도화함.

