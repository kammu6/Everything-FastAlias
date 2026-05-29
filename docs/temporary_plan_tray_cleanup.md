# 다중 창 트레이 아이콘 중복 생성 해결 및 생명주기 개선 계획 (Temporary Plan)

애플리케이션 창이 2개 이상 켜져 있는 상태에서 창을 닫을 때, 시스템 트레이에 아이콘이 중복으로 생성되거나 원치 않게 숨겨지는 문제를 해결하고 트레이 아이콘 생명주기를 동적으로 개선하기 위한 임시 계획입니다.

## 요구사항
- 창이 여러 개 켜져 있는 상태(다중 창)에서 개별 창을 닫을 때, 다른 활성 창이 존재한다면 트레이로 숨겨지지 않고 완전히 닫혀야 합니다.
- 시스템 트레이 아이콘은 언제나 최대 1개만 활성화되어 노출되어야 합니다.

## 해결 방법
1. **트레이 아이콘 동적 생명주기 모델 도입**:
   - 기존의 `Window_Loaded` 시점에 무조건 트레이 아이콘을 생성하던 방식에서 탈피하여, 창이 실제로 트레이로 최소화(`Hide()`)되는 시점에만 트레이 아이콘(`TrayIconHelper`)을 동적으로 생성합니다.
   - 트레이 아이콘 더블클릭 또는 '열기' 메뉴를 통해 창이 다시 복원(`Show()` 및 `Activate()`)되는 시점에는 트레이 아이콘을 즉시 파괴(`Dispose()`)하여 화면 노출을 제거합니다.
2. **다중 창 검사 로직 적용 (`MainWindow.xaml.cs`)**:
   - `Window_Closing` 이벤트 시점에 `Application.Current.Windows` 컬렉션을 순회하여 나 이외의 다른 `MainWindow` 인스턴스가 1개 이상 존재하는지 확인합니다.
   - 다른 창이 존재한다면 트레이 최소화 설정을 무시하고 창을 완전히 종료시킵니다.
   - 마지막 남은 단 하나의 창이 닫힐 때만 트레이로 숨기며 트레이 아이콘을 생성합니다.

## 구현 파일 및 변경 사항
- **[MODIFY] [TrayIconHelper.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Native/TrayIconHelper.cs)**: `RestoreOwnerWindow` 메서드에서 창이 다시 복원될 때 소유자 창의 `DestroyTrayIcon()` 메서드를 호출하도록 수정합니다.
- **[MODIFY] [MainWindow.xaml.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Views/MainWindow.xaml.cs)**:
  - `Window_Loaded`에서 트레이 생성 코드 제거
  - `DestroyTrayIcon()` 공용 메서드 정의
  - `Window_Closing`에서 다중 창 개수 체크 및 동적 트레이 아이콘 생성 분기 처리

## 검증 계획
- 빌드 성공 여부 검증 (`dotnet build`)
- 다중 창 기동 후 하나씩 닫을 때 트레이 아이콘이 생기지 않고 완전 종료되는지 확인
- 마지막 창 닫을 때 트레이로 정상 숨김 및 트레이 아이콘 단 1개 생성되는지 확인
