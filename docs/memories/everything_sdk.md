# Everything SDK & Windows Shell Integration Guide

이 문서는 Everything SDK 연동, `everything64.dll` FFI 호출 및 Windows API/Shell Context Menu 통합 시 발생한 핵심 이슈와 해결 지식을 영구 자산화한 문서입니다.

---

## 1. Everything SDK & DLL P/Invoke 규칙

### W (Unicode) API 사용 및 문자열 무결성
- **이슈**: Everything DLL 바인딩 시 `wchar_t*` 기반의 Unicode API를 호출하지 않으면 한글 및 다국어 검색어가 깨지는 버그가 발생합니다.
- **규칙**: 반드시 `W` 접미사가 붙은 API(예: `Everything_SetSearchW`, `Everything_GetResultFileNameW`)를 엄격히 준수해 사용해야 합니다.

### SDK 함수 진입점 문제 (`Everything_GetLastError`)
- **이슈**: Win32 GetLastError를 조회하기 위한 DLL 함수는 `Everything_GetGetLastError`가 아닌 `Everything_GetLastError`입니다.
- **해결**: P/Invoke 시그니처 이름을 정확하게 `Everything_GetLastError`로 선언하고 호출해야 `EntryPointNotFoundException` 에러를 피할 수 있습니다.

### BOOL 리턴값 마샬링 규격
- **이슈**: C++의 `BOOL`은 4바이트 정수형이지만 C#의 `bool`은 1바이트 크기입니다. P/Invoke 정의 시 `[return: MarshalAs(UnmanagedType.Bool)]` 어트리뷰트가 누락되면 결과값이 참(`true`)으로 깨져서 파일이 폴더로 오인되는 등의 오동작이 발생합니다.
- **규칙**: 모든 C++ `BOOL` 반환 함수 정의에는 `[return: MarshalAs(UnmanagedType.Bool)]`을 누락 없이 적용해야 합니다.

---

## 2. 대용량 쿼리 최적화 및 안정성

### 대용량 FFI 검색 쿼리 조기 차단 (Short-circuit)
- **이슈**: 검색단어 입력란이 빈칸일 때 Everything 엔진에 무제한 쿼리를 보내면 수십만 건의 결과를 마샬링하는 과정에서 CPU 폭증과 심각한 GC 렉(팬 소음)이 유발됩니다.
- **해결**: 검색어가 비어 있으면 FFI 검색 쿼리 자체를 타지 않고 결과 목록을 즉각 클리어(`Results.Clear()`) 후 조기 리턴(Short-circuit)하도록 개선했습니다. 결과 출력의 한계치도 `MaxResults = 10000`으로 안전 제약했습니다.

### 파일 시간(FileTime) 마샬링 예외 방어
- **이슈**: `Everything_GetResultDateModified`를 통해 파일 수정일 정보를 가져올 때, 파일 속성 조회 실패 등으로 유효하지 않은 시간 값이 들어오면 `DateTime.FromFileTime`이 `ArgumentOutOfRangeException`을 발생시킵니다.
- **해결**: 값을 안전하게 검사하고 `try-catch` 블록으로 예외를 잡아 실패 시 `new DateTime(1601, 1, 1)`(Win32 Epoch 기본값)로 폴백하도록 방어 코드를 작성했습니다.

### Everything 쿼리 연산자 우선순위 (OR vs AND) 그룹화
- **이슈**: All 스코프(`SearchScope.All`) 쿼리 `<검색어> | path:<검색어>`에 미디어 필터(`ext:jpg...`)를 결합하면 AND 공백 연산자가 OR(|)보다 우선순위가 높아 필터가 무시되는 현상이 생깁니다.
- **해결**: 스코프 쿼리 자체를 대괄호(`< >` 괄호)로 확실히 묶어 `<<검색어> | path:<검색어>> file: ext:jpg;png...` 형태로 조립함으로써, 확장자 필터와 폴더 숨김 옵션이 항상 최우선으로 적용되도록 개선했습니다.

### Everything 쿼리 수식어(modifier) 스코프 제한 이슈 (folder:, path: 등)
- **이슈**: Everything 엔진에서 `folder:`, `file:`, `path:` 등 수식어의 영향 범위(Scope)는 `|` (OR) 연산자를 만나면 단절됩니다. 
  - 예: `<folder:A | B>` ➔ `folder:`는 A에만 적용되고, B는 일반 파일/폴더 전체에서 검색되어 원치 않는 드라이브나 파일 유형이 검색 결과에 누출되는 치명적인 버그가 발생합니다.
- **해결**: 복합 OR 조건이 포함된 별칭(Alias) 및 복합 검색의 경우, 각 개별 키워드와 Alias 단위마다 수식어를 개별적으로 하나씩 부착해 주어야 합니다.
  - 예: `<folder:A | folder:B>` (올바른 형태)
  - 드라이브 범위 제한 또한 `<path:P:\>`와 같이 명시적인 `path:` 변경자를 사용해야 다른 드라이브로의 범위 일탈이 완벽히 해결됩니다.

### 사용자 직접 입력 수식어 및 드라이브 기호 보호 규칙
- **이슈**: 사용자가 직접 검색창에 `path:p:`나 `folder:foo` 또는 `P:`와 같은 Everything 수식어나 드라이브 제한 기호를 직접 입력한 경우, `QueryTransformer`가 이를 일반 검색어로 오인하여 `path:path:p:` 나 `folder:path:p:`와 같이 수식어를 중첩 부착하여 쿼리가 누더기가 되고 파싱 에러를 유발했습니다.
- **해결**: 토큰이 이미 수식어(예: `path:`, `parent:`, `folder:`, `file:`, `ext:`, `size:`, `attrib:`)로 시작하거나, 독립된 드라이브 문자(예: `P:`, `C:\`)인 경우에는 `IsConstraintOrDrive()` 판정을 통해 자동 변환 대상(Scope 변경자 및 미디어 변경자 부착 대상)에서 제외하여 원본을 완벽히 보호하도록 개선했습니다.

### 🚨 Everything SDK 복합 디버깅 포스트모템 (Post-Mortem)
1. **단일 제약 조건 몰입(편향) 방지**:
   - Everything SDK에서 복합 검색 누수(예: 다른 드라이브 결과 노출)를 디버깅할 때는, 쿼리 가공 엔진(`QueryTransformer`) 하나만 보거나 미디어 프리셋(`folder:`) 수식어에만 매몰되어서는 안 됩니다. 
   - 반드시 사용자가 지정한 모든 복합 필터(드라이브 제한, 검색 범위, 미디어 프리셋 등)가 최종 Everything DLL API(`Everything_SetSearchW`)에 전송되는 원시 문자열에 모두 포함되어 있는지 확인해야 합니다.
2. **비동기 스레드 상태 전달 검증**:
   - WPF의 비동기 검색 구현 시, UI 경합 방지를 위해 검색 옵션을 얕은/깊은 복제(`optionsCopy`)하여 백그라운드 태스크로 넘기게 됩니다. 이 과정에서 `TargetDrives`와 같은 컬렉션 필드가 누락되는 어처구니없는 버그가 발생할 수 있습니다. 
   - 파라미터가 비동기/FFI 경계를 넘나들 때, 모든 옵션 필드가 누수 없이 100% 매핑 전달되는지 반드시 대조 체크 리스트를 작성해 검증해야 합니다.
3. **통합 시나리오 재현 단위 테스트 의무화**:
   - 가짜 성공 테스트(Green test)에 속지 않기 위해, 실제 UI 컨트롤 상태와 다중 매핑 규칙이 복합적으로 적용된 상태를 고스란히 재현하는 조합형(Combinatorial) 통합 테스트 코드를 설계해 검증을 완수해야 합니다.

---

## 3. Windows 네이티브 쉘 통합 (IContextMenu)

### 쉘 우클릭 서브메뉴(IContextMenu) 빈 렌더링 현상
- **이슈**: 리스트뷰 우클릭 시 호출되는 네이티브 쉘 메뉴(`IContextMenu`)의 하위 메뉴(예: 연결 프로그램, 7-Zip 등)에 마우스를 올리면 텅 빈 상자만 나오는 현상이 있습니다.
- **해결**:
  1. 하위 메뉴의 Owner Draw 및 메시지 처리를 지원하기 위해 `IContextMenu2` 및 `IContextMenu3` COM 인터페이스 정의를 도입합니다.
  2. WPF의 `HwndSource`를 활용해 부모 윈도우에 메시지 후크(`AddHook`)를 임시 연결하여 `WM_INITMENUPOPUP`, `WM_DRAWITEM`, `WM_MEASUREITEM`, `WM_MENUCHAR`를 수신합니다.
  3. 이 메시지들을 `IContextMenu2/3::HandleMenuMsg(2)`에 마샬링 중계해 주어 렌더링 병목을 해결하고 메뉴 표시 완료 후 후크를 해제합니다.
