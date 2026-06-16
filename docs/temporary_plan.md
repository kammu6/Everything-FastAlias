# 임시 계획서: 다중 선택 항목의 비활성 회색 표시 버그 수정 및 파란색 통일

본 계획서는 마우스 드래그 또는 키보드로 항목을 다중 선택했을 때, 특정 포커스 이탈 상황에서 선택 영역이 회색(Inactive Selection)으로 표시되는 현상을 해결하고, 항상 활성 상태인 파란색 테마 색상으로 통일하기 위한 버그 수정 설계서입니다.

---

## 1. 요구사항 (Requirements)

- **선택 영역 색상 파란색 통일**:
  - 리스트 뷰에서 마우스 드래그 선택 중 또는 선택 완료 시점에 선택된 항목들이 포커스 상태와 상관없이 항상 파란색 계열로 렌더링되도록 보완.
  - 드래그 선택 시작 즉시 리스트 뷰가 키보드/마우스 포커스를 획득하도록 보완하여 WPF 포커스 이탈로 인한 회색 처리를 1차 예방.
  - 앱 내 타 컨트롤(검색 창 등)로 포커스가 넘어가 포커스가 없는 비활성 상태(Inactive)가 되더라도 회색으로 변하지 않고 파란색 선택 배경이 유지되도록 설정 오버라이드 반영.

---

## 2. 상세 구현 계획 (Implementation Plan)

### 단계 1: C# 포커스 이동 코드 추가 (`Views/ResultGridView.xaml.cs` [MODIFY])
- `ResultsListView_PreviewMouseLeftButtonDown` 내에서 `_isDragSelecting = true;`가 세팅되는 즉시 `ResultsListView.Focus();`를 호출하여 강제로 포커스를 리스트 뷰 본체로 전이함.

### 단계 2: XAML 비활성 선택 색상 리소스 오버라이드 (`Views/ResultGridView.xaml` [MODIFY])
- `ResultsListView`의 `ItemContainerStyle` (`Style TargetType="ListViewItem"`) 내부에 `<Style.Resources>` 영역을 추가.
- WPF의 비활성 선택 상태 브러시 키인 `SystemColors.InactiveSelectionHighlightBrushKey`를 파란색 컬러(`#FF0078D7`) 브러시로 재정의.
- 비활성 선택 텍스트 색상 키인 `SystemColors.InactiveSelectionHighlightTextBrushKey`를 흰색(`White`) 브러시로 재정의.

---

## 3. 검증 계획 (Verification Plan)

### 수동 검증
1. 마우스 드래그 선택을 수행하여 파란색 드래그 선택 영역이 실시간으로 갱신되고, 선택된 항목들이 진한 파란색으로 통일되게 표시되는지 확인.
2. 드래그 선택 완료 후, 마우스로 상단 검색 단어 입력창(TextBox)을 클릭하여 포커스를 상단으로 이동시킴.
3. 포커스가 TextBox로 가 있는 상황에서도 결과 리스트의 다중 선택된 항목들의 배경색이 회색으로 옅어지지 않고 진한 파란색으로 계속 노출되는지 확인.
