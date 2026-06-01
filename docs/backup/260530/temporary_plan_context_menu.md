# 네이티브 우클릭 쉘 서브메뉴 빈칸 렌더링 버그 해결 계획 (Temporary Plan)

우측 패널 결과 목록에서 파일 우클릭 시 호출되는 윈도우 네이티브 쉘 컨텍스트 메뉴(`IContextMenu`)의 하위 드롭다운 메뉴(예: 연결 프로그램, 7-Zip 등)가 빈 흰색 칸으로 표시되고 하위 항목이 보이지 않는 렌더링 오류를 해결하기 위한 임시 계획입니다.

## 원인 분석
- 윈도우 탐색기 쉘 확장 메뉴 중 일부(연결 프로그램 등)는 소유자 그리기(Owner Draw) 방식을 사용하거나 하위 팝업 구성 시 `IContextMenu2` 및 `IContextMenu3` 인터페이스를 요구합니다.
- 또한 이 서브메뉴가 팝업되거나 아이템을 렌더링할 때 발생하는 특정 윈도우 메시지(`WM_INITMENUPOPUP`, `WM_DRAWITEM`, `WM_MEASUREITEM`, `WM_MENUCHAR`)를 윈도우 메시지 루프(`WndProc`)에서 가로채 쉘 메뉴 객체(`IContextMenu2::HandleMenuMsg` 또는 `IContextMenu3::HandleMenuMsg2`)로 중계해 주어야 합니다.
- 현재 `ShellContextMenu.cs`에는 이 인터페이스 정의와 윈도우 메시지 후킹 및 쉘 포워딩 로직이 완전히 누락되어 있어 하위 메뉴 내용이 그려지지 못하고 빈칸으로 노출된 것입니다.

## 해결 방법
1. **인터페이스 정의 추가 (`ShellContextMenu.cs`)**:
   - `IContextMenu`를 상속받는 `IContextMenu2` 및 `IContextMenu3` COM 인터페이스를 정의합니다.
2. **윈도우 메시지 후킹 구현**:
   - `WM_INITMENUPOPUP`, `WM_DRAWITEM`, `WM_MEASUREITEM`, `WM_MENUCHAR` 메시지 번호를 정의합니다.
   - WPF의 `HwndSource`를 사용하여 `TrackPopupMenuEx`가 실행되는 동안 부모 윈도우에 메시지 훅(`AddHook`)을 연결합니다.
3. **메시지 중계 콜백 함수 추가**:
   - 메시지 훅 콜백(`HookWindowMessages`)을 구현하여 쉘 팝업 관련 4대 메시지 수신 시 `IContextMenu3` 또는 `IContextMenu2` 인터페이스의 메뉴 메시지 핸들러로 이를 포워딩하도록 처리합니다.
   - `TrackPopupMenuEx`가 반환되면 훅을 안전하게 해제(`RemoveHook`)합니다.

## 구현 파일
- **[MODIFY] [ShellContextMenu.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Native/ShellContextMenu.cs)**

## 검증 계획
- 빌드 정상 수행 여부 검증 (`dotnet build`)
- 프로그램 실행 후 임의의 이미지 파일을 우클릭하여 서브메뉴(예: 연결 프로그램 등) 내용이 빈칸이 아닌 탐색기처럼 정상적으로 노출되는지 수동 검사
