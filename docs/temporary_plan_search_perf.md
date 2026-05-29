# 타이핑 자동 검색 제어 및 Enter 검색 수동 전환 계획 (Temporary Plan)

검색단어 입력창에 글자를 입력하는 매 타이핑 순간마다 자동 검색이 유발되어 화면에 "검색중..."이 뜨고 성능이 저하되는 현상을 해결하고, 사용자가 타이핑을 완료한 후 Enter 키를 누르거나 새로고침 버튼을 누를 때만 실질적인 Everything FFI 검색 로직이 수행되도록 정밀 타겟팅 수정하기 위한 임시 계획입니다.

## 요구사항
- 검색단어 란(`SearchQuery`)에 글자를 타이핑할 때는 실질적인 자동 검색(`TriggerSearch()`)이 일어나지 않아야 합니다.
- 다만, 검색어 입력을 완전히 지운 경우(빈 검색어)에는 즉시 결과 리스트를 클리어(`Results.Clear()`)하여 실시간 피드백을 제공합니다.
- 검색단어 란에서 글자를 다 입력하고 `Enter` 키를 눌렀을 때 `SearchCommand`가 작동해 실질적인 "검색중..." 로직 및 Everything FFI 쿼리가 가동되어야 합니다.
- 이미 구현된 "새로고침" 버튼 클릭 시에도 수동 검색이 정상 수행되어야 합니다.
- 다른 필터 옵션(라디오 버튼, 지정 경로 등)은 기존의 유연한 피드백을 위해 자동 검색 동작을 유지합니다.

## 해결 방법
1. **ViewModel 수정 (`SearchViewModel.cs`)**:
   - `SearchQuery` 프로퍼티의 setter 내부에서 `TriggerSearch()` 호출을 제거합니다.
   - 단, 검색어가 완전히 지워진 경우(`string.IsNullOrWhiteSpace(value)`)에 한해서는 UI 리스트를 즉시 청소(`Results.Clear()`)하고 상태를 복구시킵니다.
2. **View 수정 (`MainWindow.xaml`)**:
   - 검색단어 입력용 `TextBox` 태그 내부에 `<TextBox.InputBindings>`를 추가하여 `Key="Enter"`일 때 `SearchVM.SearchCommand`가 연동 실행되도록 키 바인딩을 바인딩합니다.

## 검증 계획
1. 빌드 성공 확인 (`dotnet build`)
2. 검색창 입력 시 자동으로 "검색중..."이 뜨지 않는지 및 Enter 입력 시에만 실시간 검색 결과가 갱신되는지 확인
