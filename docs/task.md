# 작업 관리 목록 (Task Checklists)

- [x] 단계 1: 모델 및 Native 헬퍼 계층 구축
  - [x] `ViewMode.cs` 생성 (자세히/섬네일S/M/L)
  - [x] `ShellIconHelper.cs` 생성 (기본 폴더/파일 아이콘 캐시)
  - [x] `ShellThumbnailHelper.cs` 생성 (Win32 P/Invoke 썸네일)
  - [x] `SearchResultItem.cs` 수정 (비동기 `Thumbnail` 로드 바인딩 추가)
- [x] 단계 2: 뷰 모델 및 설정 보존 계층 반영
  - [x] `SearchViewModel.cs` 수정 (ViewMode 속성 및 UI 크기 도우미)
  - [x] `SearchViewModel.Settings.cs` 수정 (SQLite 로드/저장에 ViewMode 추가)
- [x] 단계 3: UI 마크업(XAML) 레이어 개편 및 연결
  - [x] `LeftSidebarView.xaml` 수정 (초기화 최상단, 보기 옵션 추가, 옵션 최하단)
  - [x] `ResultGridView.xaml` 수정 (DataTrigger 스타일 전환 및 썸네일 전용 DataTemplate 구성)
- [x] 단계 4: 통합 빌드 및 검증
  - [x] 빌드 무결성 검증 (dotnet build)
  - [x] 썸네일 비동기 렌더링 스크롤 성능 및 F2 변경 테스트
  - [x] 설정값 SQLite 로드/저장 복원 확인
- [x] 추가 고도화: F2 인라인 편집 활성화 및 포커싱 버그 해결
  - [x] Loaded 이벤트 시 Dispatcher.BeginInvoke를 통한 비동기 키보드 포커스 확보
  - [x] PreviewMouseLeftButtonDown 내 TextBox 터치 시 이벤트 전파 가로채기(e.Handled=true) 조기 리턴 처리
