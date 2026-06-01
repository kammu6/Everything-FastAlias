# F2 인라인 이름변경 취소 로직 보완 및 단일 편집 보장 계획

우측 패널 파일 목록에서 F2를 눌러 이름을 변경할 때 발생하는 로직 불안정(중복 편집 상태 활성화 및 다른 항목 클릭 시 취소 불능 버그)을 완벽히 해결하기 위한 임시 구현 계획서입니다.

## Requirements

1. **단일 편집 상태 엄격 보장**: 한 번에 하나의 파일만 이름변경 TextBox가 활성화되어야 함.
2. **자연스러운 취소 지원 (Windows 10 Explorer 방식)**:
   - F2 편집 중 다른 파일(행)을 클릭하여 선택이 변경되면 기존 편집이 자연스럽게 종료(취소)되어야 함.
   - 포커스를 잃었을 때(LostFocus) 텍스트에 변화가 없거나 빈 값이면 안전하게 편집이 취소되어야 함.
   - Esc 키 입력 시 당연히 편집 취소.
   - Enter 키 입력 또는 포커스를 잃었을 때 올바르게 수정된 텍스트가 존재하면 Commit 처리.
3. **가상화 환경 방어**: WPF ListView는 가상화(`VirtualizingStackPanel`)가 활성화되어 있으므로 화면 밖으로 밀려난 아이템의 상태 변화까지 일괄 대응할 수 있도록 UI 스레드 상의 상태 변수를 완전하게 클리어해야 함.

## Tech Stack
- C# .NET 9.0 / WPF
- ModernWPF UI

## Proposed Changes

### [MODIFY] [ResultGridView.xaml.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Views/ResultGridView.xaml.cs)
- `_editingItem` 필드를 추가하여 뷰단에서 현재 활성화된 편집 대상 모델 객체를 추적.
- `StartRename(SearchResultItem target)` 헬퍼 메서드를 추가하여 기존 편집 상태의 일괄 초기화와 신규 타겟 활성화를 격리.
- `CancelRename`을 인스턴스 메서드로 리팩토링하고 `_editingItem = null;` 초기화 추가.
- `CommitRename`에서도 성공/실패 여부와 상관없이 `_editingItem = null;` 안전 초기화 추가.
- `ResultsListView_SelectionChanged` 이벤트에서 선택 범위 이탈 시 `CancelRename` 자동 연동.

## Verification Plan

### 수동 검증 계획
1. **F2 연속 누름 테스트**: 한 항목을 F2 눌러 편집 모드로 진입한 후, 아래 방향키로 내려가 다른 항목에 F2를 연속으로 눌러도 두 개 이상이 동시에 편집 모드로 남아있지 않고 오직 마지막 항목만 편집 모드가 되는지 확인.
2. **마우스 다른 파일 클릭 테스트**: F2 편집 상태에서 이름변경 입력란이 활성화되었을 때, 아무것도 고치지 않고 마우스로 다른 행을 클릭하면 이전 행의 편집 상태가 정상 해제(취소)되고 새 행이 깔끔하게 선택되는지 확인.
3. **이름 변경 적용 테스트**: F2 편집 상태에서 이름을 적절히 수정하고 Enter를 누르면 실제로 파일 이름이 바뀌고 편집 상태가 자연스럽게 종료되는지 확인.
4. **Esc 취소 테스트**: 이름을 수정하다가 Esc를 누르면 원래 이름으로 원복되고 편집창이 닫히는지 확인.
