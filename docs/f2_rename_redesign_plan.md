# F2 인라인 이름변경 워크플로우 재설계 계획서

## 요구사항 (Requirements)

- **문제**: F2 키를 한 번만 눌러도 마우스 커서가 ban 아이콘으로 굳어버리고 앱 전체가 먹통이 됨
- **원인**: 드래그 앤 드롭(`DoDragDrop`)과 이름변경 TextBox(`LostFocus`) 이벤트가 WPF 메시지 루프 수준에서 충돌
- **목표**: 아래 3가지 동작이 완벽히 독립적으로 동작하도록 재설계
  1. **더블클릭** → 기본 프로그램으로 파일 열기
  2. **드래그 아웃** → 외부 탐색기로 파일 복사/이동
  3. **F2** → 인라인 이름 변경 편집 모드

---

## 근본 원인 (Root Cause)

### 원인 1: `LostFocus` ↔ `DoDragDrop` 교착 루프
```
F2 → IsEditing=true → TextBox 포커스 획득
→ 마우스 조금 움직임 → MouseMove 드래그 감지
→ DoDragDrop() [동기 블로킹, UI 스레드 점유]
→ TextBox LostFocus 발생 (DoDragDrop 내부 메시지 루프에서)
→ CancelRename 호출 → IsEditing=false
→ DoDragDrop이 아직 블로킹 중 → WPF 메시지 루프 비정상
→ 마우스 Ban 아이콘 + 앱 먹통
```

### 원인 2: `_clickedItem` 처리에서 `e.Handled=true`로 TextBox 포커스 완전 차단
- 편집 중인 아이템이 이미 선택된 상태에서 TextBox를 클릭하면
- `PreviewMouseLeftButtonDown` → `e.Handled=true` → TextBox에 클릭 이벤트 전달 차단

### 원인 3: 편집 상태 중에도 드래그 감지가 작동함
- `_isDragging` 플래그가 있지만, **편집 상태(`IsEditing`)를 드래그 시작 조건에서 배제하는 로직이 없음**

---

## 해결 전략 (Strategy)

### 핵심 원칙: 상태 머신(State Machine) 도입
뷰 수준에 3가지 상태를 명시적으로 정의하고, 상태에 따라 이벤트 처리를 분기:

```
enum ViewState { Idle, Dragging, Editing }
```

- `Editing` 상태 진입 시 → 드래그/더블클릭 완전 비활성화
- `Dragging` 상태 진입 시 → LostFocus 무시 (IsEditing 변경 금지)
- `Idle` 상태에서만 드래그 감지 시작 가능

---

## 구현 계획 (Implementation Plan)

### 변경 파일

#### [MODIFY] `ResultGridView.xaml.cs`

**1단계: 상태 머신 필드 추가**
```csharp
private enum ViewState { Idle, Dragging, Editing }
private ViewState _viewState = ViewState.Idle;
```

**2단계: 드래그 이벤트 핸들러 재작성**

`ListViewItem_PreviewMouseLeftButtonDown`:
- **편집 상태(`_viewState == Editing`)이면 즉시 return** (드래그 시작 차단)
- TextBox 자식인 경우 → 이벤트 전파 허용(return)
- 더블클릭인 경우 → 파일 열기 후 `e.Handled=true`, return

`ListViewItem_MouseMove`:
- **`_viewState != Idle`이면 return** (드래그 감지 완전 차단)
- 드래그 임계치 초과 시 → `StartDrag()` 호출

`StartDrag()`:
- 진입 시 `_viewState = ViewState.Dragging` 설정
- `DoDragDrop()` 동기 호출
- finally에서 `_viewState = ViewState.Idle` 복원

**3단계: F2 이름변경 핸들러 재작성**

`StartRename()`:
- `_viewState = ViewState.Editing` 설정
- `_editingItem` 설정, `IsEditing = true`

`RenameBox_Loaded()`:
- **`DispatcherPriority.Render`** (현재 `Input`보다 높은 우선순위)로 포커스 강제 획득
- `Keyboard.Focus(tb)` + `tb.Focus()` 병행

`RenameBox_LostFocus()`:
- **`_viewState == Dragging`이면 무시** → 드래그 중 LostFocus로 인한 오조작 차단
- 정상 케이스에서만 Commit 또는 Cancel 처리
- 처리 완료 후 `_viewState = ViewState.Idle` 복원

`CancelRename()` / `CommitRename()`:
- 완료 후 `_viewState = ViewState.Idle` 복원

**4단계: `_clickedItem` 로직에서 편집 상태 배제**

`ListViewItem_PreviewMouseLeftButtonDown`:
- `_viewState == Editing`이면 `_clickedItem = null` 유지 (선택 로직 개입 금지)

`ListViewItem_PreviewMouseLeftButtonUp`:
- `_viewState == Editing`이면 `_clickedItem` 처리 skip

#### [MODIFY] `ResultGridView.xaml` (옵션, 필요 시)
- TextBox의 `PreviewMouseLeftButtonDown`에 `e.Handled=false` 명시적 설정 (TextBox 자체 클릭 이벤트 보장)

---

## 검증 계획 (Verification Plan)

### 빌드 검증
```bat
build-debug.bat
```

### 기능 시나리오 검증 (수동)

| 시나리오 | 기대 결과 |
|----------|----------|
| 파일 선택 → F2 | TextBox 즉시 표시, 커서 활성화, 파일명 전체 선택 |
| TextBox 활성 → ESC | 편집 취소, TextBox 사라짐, 마우스 정상 동작 |
| TextBox 활성 → Enter | 이름 변경 완료, 정상 복귀 |
| TextBox 활성 → 다른 곳 클릭 (LostFocus) | 편집 취소 또는 커밋, 마우스 정상 동작 |
| TextBox 활성 중 마우스 이동 | 드래그 시작되지 않음 |
| Idle 상태 → 파일 드래그 | 외부로 드래그 아웃 정상 작동 |
| Idle 상태 → 더블클릭 | 기본 프로그램으로 파일 열기 |
| 썸네일 모드 → F2 | 동일하게 정상 동작 |

---

## 자본화 계획 (Capitalization Plan)

구현 완료 후 `./docs/memories/MEMORY.md`에 아래 내용을 기록:
- `DoDragDrop` + TextBox `LostFocus` 교착 패턴 및 ViewState 상태 머신 해결법
- `Editing` 상태 중 드래그 이벤트 차단 전략
- `wpf_coding_guidelines.md`의 "F2 인라인 이름변경" 섹션 보완
