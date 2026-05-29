# WPF & Code Architecture Guidelines

이 문서는 WPF UI 컨트롤 핸들링, 비동기 데이터 처리, 대용량 동의어 검색 아키텍처 등 어플리케이션 코딩 전반에 걸쳐 유용한 개발 지식을 자산화한 문서입니다.

---

## 1. WPF UI & Interaction Issues

### ModernWpf 스타일 및 리소스 충돌 방지
- **ResourceDictionary XamlParseException**: ModernWpf 적용 시 테마 리소스를 어셈블리 사전 파일 경로로 강제 주입하면 로드 실패가 생길 수 있습니다. `xmlns:ui="http://schemas.modernwpf.com/2019"` 선언 후 `<ui:ThemeResources />`와 `<ui:XamlControlsResources />`를 병합(Merge)하여 사용하는 것이 안전합니다.
- **존재하지 않는 StaticResource 예외**: 존재하지 않는 스타일(예: `CardToggleSwitchStyle`)을 `StaticResource`로 참조하면 크래시가 유발되므로 스타일 지정을 생략하고 컨트롤에 기본 스타일이 자동 상속되게 합니다.
- **StackPanel Spacing 및 TextBox Placeholder**: 표준 WPF 컨트롤에는 `Spacing`이나 `PlaceholderText` 속성이 기본 제공되지 않으므로, ModernWpf의 `SimpleStackPanel` 및 `ControlHelper.PlaceholderText`를 사용해 문제를 대체 해결합니다.

### WPF & WinForms Global Using 충돌
- **이슈**: 트레이 최소화 구현 등을 위해 `.csproj`에 `<UseWindowsForms>true</UseWindowsForms>`를 설정하면 `Application`, `UserControl` 등 공통 명칭 클래스에 모호성 빌드 오류가 생깁니다.
- **해결**: `.csproj`에 `<ItemGroup><Using Remove="System.Windows.Forms" /></ItemGroup>`를 기입하여 Forms의 global using 선언을 자동 제거하고, 트레이 제어 클래스 내부에서만 `using System.Windows.Forms;`를 명시적으로 선언합니다.

### 윈도우 다중 창 기동 시 트레이 아이콘 중복 방지
- **이슈**: 다중 창 기동 상태에서 창을 닫을 때 트레이 아이콘이 불필요하게 늘어나거나 프로세스가 좀비로 남는 버그가 생깁니다.
- **해결**:
  - `Window_Closing` 단계에서 다른 메인 윈도우가 열려 있는지 카운팅한 후, 최후의 1개 창이 닫힐 때에만 트레이 아이콘을 동적 생성(`new`)하고 창을 숨깁니다(`Hide()`). 다른 창이 존재하면 트레이로 보내지 않고 현재 창을 완전 파괴(`Close()`) 처리합니다.
  - 트레이 아이콘을 통해 창이 활성화(`Show()`, `Activate()`)되는 즉시 트레이 아이콘을 `Dispose()`하여 리소스를 완전히 소거합니다.

### 드래그 앤 드롭(Drag-out) 간섭과 더블클릭 이벤트 무산
- **이슈**: ListView 다중 선택 드래그 앤 드롭 구현을 위해 `PreviewMouseLeftButtonDown`에서 `e.Handled = true`를 선언하면, 더블클릭 메시지 전파가 막혀 `MouseDoubleClick` 이벤트가 구동되지 않습니다.
- **해결**: `PreviewMouseLeftButtonDown` 상단에서 `e.ClickCount == 2`를 우선적으로 감지해 더블클릭을 판별하고, 해당 시점에 기본 연결 프로그램이나 탐색기로 대상을 실행한 후 핸들링 처리를 마칩니다.

### F2 인라인 이름변경 가상화 오동작
- **이슈**: UI 가상화(`VirtualizingStackPanel`)로 인해 화면을 벗어난 아이템이 편집 상태를 물고 있어 다중 텍스트 박스가 동시 활성화되는 오동작이 발생합니다.
- **해결**: `StartRename` 진입 시 `ItemsSource`를 순회하여 `IsEditing` 상태 플래그를 강제로 일괄 클리어하고, 단일 편집 상태만 추적하는 뷰 레벨 필드(`_editingItem`)로 편집 활성화를 독점 제어합니다.

---

## 2. 데이터 처리 & 아키텍처 최적화

### ObservableCollection 대량 데이터 렌더링 병목
- **이슈**: 수천 수만 건의 검색 결과를 ObservableCollection에 루프로 추가할 경우 뷰 렌더링 렉이 심하게 걸립니다.
- **해결**: 알림을 일시 억제한 뒤 모든 삽입이 끝나면 단 한 번만 `CollectionChanged`를 전달하는 Custom `RangeObservableCollection`을 도입해 렉을 방지했습니다.

### 디바운스 및 자동 검색 전환
- **이슈**: 글자 입력 시마다 검색이 자동 트리거되면 화면이 깜빡이고 렉이 유발됩니다.
- **해결**: 타이핑 시에는 자동 쿼리를 배제하고(단, 빈 입력란 감지 시 즉시 클리어는 유지), `TextBox.InputBindings`에 Enter 키를 통한 `SearchCommand` 수동 실행 구조를 명확히 매핑하여 제어력을 극대화했습니다.

### 대용량 동의어(Alias) 사전 최적화
- **이슈**: 수천 줄 이상의 사전 데이터 연동 시 검색어를 입력하면 매핑 루프와 정규식 매칭 오버헤드로 인해 수십 분간 먹통이 됩니다.
- **해결**:
  1. **사전 전역 캐싱**: 검색 시마다 맵과 키를 매번 재생성하던 로직을 DB 변경 시 1회 사전 생성하여 메모리에 올리는 방식으로 개선했습니다.
  2. **IndexOf 생략 필터링**: 입력 쿼리에 해당 동의어 키 단어가 없을 경우(`IndexOf < 0`) 무거운 `Regex` 파이프라인 처리를 skip 하도록 제어했습니다.
  3. **바이럴 확장 결합 제거**: 단어가 여러 행(Row)에 출현해도 모든 행을 단일 Equivalence Class로 강제 병합하지 않고, 각 단어가 속한 행들의 동의어들만 독립 수집(Union)하게 캐싱 구조를 바꿈으로써 3만 개 이상의 불필요한 OR 연산을 차단했습니다.
