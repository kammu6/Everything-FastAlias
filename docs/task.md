# 작업 진행 상황 (Task Tracker)

## 이전 완료 작업 (Alias Manager 고도화)
- [x] AliasManagerViewModel.cs 수정 (이벤트 신설, Insert(0) 변경, 저장 성공 시 로드 및 선택 상태 복원)
- [x] AliasManagerWindow.xaml 수정 (TextBox Loaded 및 GotFocus 이벤트 핸들러 바인딩)
- [x] AliasManagerWindow.xaml.cs 수정 (이벤트 구독 및 DataGrid.BeginEdit 호출, TextBox 포커스 및 SelectAll 구현)
- [x] 빌드 및 컴파일 검증
- [x] MEMORY.md 업데이트

## 신규 작업 (상태창 쿼리 클립보드 복사 및 제외 단어 원인 분석)
- [x] MainWindow.xaml 수정 (StatusMessage TextBlock에 Cursor="Hand", ToolTip, MouseLeftButtonDown 이벤트 추가)
- [x] MainWindow.xaml.cs 수정 (QueryText_MouseLeftButtonDown 구현하여 쿼리만 추출 후 클립보드 복사)
- [x] 빌드 및 컴파일 검증
- [x] MEMORY.md 업데이트

## 신규 작업 (도움말 메뉴 개선 및 예외 조건 가이드 추가)
- [x] HelpWindow.xaml 수정 (정규식 오타 수정, 제외 단어 다중 입력 가이드 팁 추가, ESC 미구현 단축키 제거)
- [x] 빌드 및 컴파일 검증
- [x] MEMORY.md 업데이트
