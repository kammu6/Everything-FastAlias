# 다중 선택 드래그 앤 드롭 버그 수정 계획 (Temporary Plan)

우측 패널 목록(ResultsListView)에서 여러 개의 파일/폴더를 범위 지정(예: Ctrl+A)한 후, 드래그할 때 단 하나의 항목만 드래그되는 현상을 해결하기 위한 임시 계획입니다.

## 요구사항
- `ResultsListView`에서 여러 항목이 선택된 상태에서 드래그 앤 드롭을 실행하면, 마우스 포인터 아래의 단일 파일만 드래그되는 대신 현재 선택된 모든 파일들이 하나의 `DataObject`에 묶여 외부로 드래그되어야 합니다.

## 원인 분석
- WPF ListView의 기본 동작으로 인해, 다중 선택 상태에서 마우스 왼쪽 버튼을 클릭하는 순간(MouseLeftButtonDown) 클릭한 해당 아이템 하나만 선택되는 상태로 변경됩니다.
- 이로 인해 `StartDrag`가 실행되는 시점에는 `ResultsListView.SelectedItems`에 단 1개의 아이템만 남게 됩니다.

## 해결 방법
- `ResultGridView.xaml`에 `PreviewMouseLeftButtonUp` 이벤트를 `ListViewItem`에 추가합니다.
- `ResultGridView.xaml.cs`에서 `PreviewMouseLeftButtonDown` 시 클릭한 아이템이 이미 선택된 상태라면 `e.Handled = true`를 선언하여 WPF의 기본 단일 선택 전환 동작을 차단하고, 드래그를 대비해 임시 변수에 저장합니다.
- `MouseMove`를 통해 드래그 임계값을 넘으면 저장된 다중 선택 파일 목록 전체를 대상으로 `DragDrop.DoDragDrop`을 수행합니다.
- 만약 드래그가 수행되지 않고 마우스가 떼어졌을 때(`PreviewMouseLeftButtonUp`), 클릭한 단일 아이템만 수동으로 선택해 주어 본래의 클릭 동작(단일 선택 전환)을 정상적으로 에뮬레이션합니다.

## 구현 계획
1. `ResultGridView.xaml` 수정: `EventSetter Event="PreviewMouseLeftButtonUp" Handler="ListViewItem_PreviewMouseLeftButtonUp"` 추가
2. `ResultGridView.xaml.cs` 수정: 
   - `ListViewItem_PreviewMouseLeftButtonDown` 수정
   - `ListViewItem_PreviewMouseLeftButtonUp` 구현
   - `ListViewItem_MouseMove`에서 상태 클리어 연동
3. 프로젝트 빌드 테스트 수행
