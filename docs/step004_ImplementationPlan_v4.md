# Everything FastAlias 구현 계획서 (UX 최적화, 양방향 별칭 검색 및 상단바 리디자인)

이 문서는 대량 검색 결과로 인한 극심한 렉 현상을 해결하고, 동의어 매핑이 양방향으로 완벽히 작동하도록 보완하며, 스타일 통일성 확보 및 상단바 레이아웃 리디자인 요구사항을 반영하기 위해 수립된 구현 계획서입니다.

---

## User Review Required

> [!IMPORTANT]
> **대량 검색 렌더링 렉(성능 최적화) 개선 핵심 기법**:
> 1. **입력 디바운스(Debounce)**: 타이핑 시 150ms 대기 타이머를 거쳐 쿼리를 전송함으로써, 폭발적인 실시간 호출을 억제합니다.
> 2. **비동기 Task.Run**: Everything FFI 검색 및 치환 연산을 UI 스레드로부터 격리하여 백그라운드 스레드에서 처리함으로써, 검색 중의 입출력 프리징 현상을 완전히 제거합니다.
> 3. **RangeObservableCollection 도입**: 대량 결과 삽입 시 변경 통지가 매번 루프마다 발생하지 않도록, `Reset` 액션 하나로 바인딩을 통제하여 렌더링 성능을 획기적으로 개선합니다.

> [!WARNING]
> **양방향 동의어(Alias) 매핑 설계**:
> `QueryTransformer.cs` 내에서 기존의 단방향(`Key ➔ Value`) 치환 방식을 개선하여, 매핑 사전에 등록된 모든 연관어들(Keyword 및 Words)을 상호 연동되는 하나의 동의어 집합(Equivalence Group)으로 자동 묶어 맵을 재빌드합니다. 이를 통해 동의어 그룹 내 어느 단어를 입력해도 상호 간에 양방향 치환(`<사과|apple|🍎>`)이 완벽히 동작하도록 변경합니다.

---

## Open Questions

> [!NOTE]
> **스타일 통합 관리 정의**:
> WPF `Menu`, `MenuItem`, `StatusBar` 등의 폰트 크기 및 스타일을 개별 컨트롤에서 제어하지 않고, `MainWindow.xaml` 리소스 사전 영역에 통합 정의하여 일괄 적용(스타일 가이드라인 규격화)하겠습니다.

---

## Requirements

1. **[성능최적화] 실시간 입력 렉 및 반응 속도 획기적 개선**:
   - `RangeObservableCollection.cs` 클래스 추가 및 적용.
   - `SearchViewModel.cs`에 `DispatcherTimer` 기반의 150ms 디바운스 구조 반영.
   - Everything SDK 검색 질의를 비동기(`Task.Run`) 스레드에서 백그라운드 수행하도록 전환.
2. **[스타일통일] 메뉴 글씨 크기 일괄 적용**:
   - `MainWindow.xaml` 리소스에 `Menu`, `MenuItem`, `StatusBar` 폰트 스타일 중앙화 정의.
3. **[버그수정] 양방향 동의어(Alias) 매핑 보완**:
   - `QueryTransformer.ReplaceAliases` 메서드를 전면 개편하여, 동의어 키워드와 번환 대상 단어들을 하나의 동의어 묶음으로 묶어 상호 대칭 치환되도록 개선.
4. **[디자인개선] 상단바 레이아웃 정돈 및 세로 대칭 배열**:
   - 상단 검색 영역 레이아웃 분할: 좌측 토글 스위치 영역(FastAlias, 옵션패널) / 중앙 세로 경계선 / 우측 검색 입력창 3단 수직 배치.
   - 3단 입력창(검색어 입력, 제외 단어, 직계 경로)을 세로로 완벽히 정렬하여 대칭 대형을 구현.

---

## Tech Stack

- **Framework**: WPF, .NET 9.0, ModernWpfUI
- **Design Pattern**: MVVM, Style Resources

---

## Folder Structure

```text
D:\3_Code\3_Apps\43_Search-Edit\Everything검색기\src\EverythingFastAlias\
├── Config/
│   ├── AppConstants.cs
│   └── RangeObservableCollection.cs  [NEW] (대량 통지 제어용 컬렉션)
├── Services/
│   └── QueryTransformer.cs           [MODIFY] (양방향 별칭 맵 빌드 및 치환 로직 반영)
├── ViewModels/
│   └── SearchViewModel.cs            [MODIFY] (디바운스 타이머 탑재 및 비동기 검색 Task.Run 적용)
└── Views/
    └── MainWindow.xaml               [MODIFY] (상단바 레이아웃 전면 리디자인, 폰트 스타일 전역 중앙 정의)
```

---

## Search/Retrieval Tools

- **view_file**: 코드 세부 블록 식별.
- **grep_search**: 클래스 필드 추적.

---

## Verification Tools

- **dotnet build**: 컴파일 무결성 검사.
- **dotnet test**: 양방향 동의어 치환에 따른 단위 테스트 보강 및 검증.

---

## Implementation Plan

### 1. 양방향 동의어 치환 & 신규 컬렉션 생성
- `RangeObservableCollection.cs` 신규 작성 및 구성.
- `QueryTransformer.ReplaceAliases`에 역방향/양방향 맵 빌더 코드 보완.

### 2. 비동기 검색 및 입력 디바운스 연동
- `SearchViewModel.cs`의 `Results` 속성 타입을 `RangeObservableCollection`으로 전환.
- `DispatcherTimer` (150ms 간격) 추가하여 입력 버퍼링 수집.
- `ExecuteSearchAsync` 비동기 메서드를 구현하여 `Task.Run` 으로 검색 처리.

### 3. 상단바 리디자인 & 전역 스타일 정의
- `MainWindow.xaml`에 `Menu`, `MenuItem`, `StatusBar` 폰트 스타일 리소스 구성.
- 상단바 Border 내부를 3개의 Column(토글 스택 / 세로 라인 / 3층 텍스트박스)으로 재배치하여 완벽한 대칭 레이아웃 달성.

---

## Verification Plan

### Automated Tests
- `dotnet test` 명령을 활용하여 양방향 동의어 치환 기능 검증 및 단위 테스트 상태 확인.

### Manual Verification
- 빌드 후 앱을 띄워:
  1. `apple` 검색 시 `사과.txt`가 정상적으로 목록에 나타나는지 검증.
  2. 한 글자씩 연속 입력 시 렉 없이 즉각적이고 부드러운 반응성 확인.
  3. 메뉴 및 전체 폰트 크기가 안정적으로 통일되어 노출되는지 확인.
  4. 좌우 및 상단바의 수직 대칭형 3층 레이아웃 구성 무결성 수동 확인.
