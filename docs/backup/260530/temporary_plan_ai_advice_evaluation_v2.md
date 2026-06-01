# 조언 AI 2차 피드백 평가 및 안정성 개선 계획서 (Temporary Plan v2)

외부 조언 AI가 분석한 Everything FastAlias 2차 코드 리뷰 내용에 대해 실제 프로젝트 코드를 분석하여 타당성을 검증하고, 필요한 개선 및 보완점을 정리한 두 번째 임시 계획서입니다.

---

## 1. 조언 AI 제안사항 타당성 검증 결과 (Evaluation)

### ① Everything SDK 전역 상태 스레드 경합 문제 (Lock 도입)
- **진단**: **매우 타당함 (Critical)**
- **이유**: `Everything64.dll` FFI 호출 API는 내부적으로 전역 상태 버퍼를 활용하므로 멀티스레드 세이프하지 않습니다. 백그라운드 스레드에서 `Search()`가 실행되는 동시에 메인 스레드 등에서 `IsEverythingRunning()` (상태 진단을 위해 내부적으로 `"test"` 검색 쿼리를 실행함)이 실행되면, 전역 쿼리 버퍼가 충돌하여 쿼리가 뒤섞이거나 오동작 및 IPC 예외 크래시를 유발합니다.
- **해결 방안**: `EverythingBridge.cs` 내부의 모든 Everything SDK 호출 구간을 전역 락 객체(`_engineLock`)로 동기화 처리합니다.

### ② SQLite 대소문자 구분과 인메모리 캐시 불일치
- **진단**: **매우 타당함 (Critical)**
- **이유**: `DatabaseService.cs` 내부 인메모리 캐시(`_cache`)는 `StringComparer.OrdinalIgnoreCase`로 대소문자를 구분하지 않지만, SQLite 기본 설정은 `TEXT PRIMARY KEY` 시 대소문자를 엄격히 구분합니다. 이에 따라 DB에 `test`와 `Test`가 동시에 등록될 수 있어 캐시 일관성이 훼손되고 삭제 작업이 꼬이는 오류가 발생할 수 있습니다.
- **해결 방안**: DB 테이블 생성 시 `Keyword TEXT PRIMARY KEY COLLATE NOCASE`와 `SettingKey TEXT PRIMARY KEY COLLATE NOCASE`를 적용하여 대소문자를 무시하도록 무결성을 확보합니다.

### ③ 따옴표 포함 검색어의 Scope(경로/전체) 적용 누락
- **진단**: **매우 타당함 (Major)**
- **이유**: `QueryTransformer.cs`의 `IsWordToken`에서 큰따옴표로 둘러싸인 토큰(예: `"Program Files"`)을 무조건 단어 토큰이 아닌 것으로 간주해 `false`를 반환합니다. 이로 인해 경로 검색 모드 등에서 접두사(`path:`) 조립이 누락되어 단순 파일명 매칭으로 오동작합니다.
- **해결 방안**: `IsWordToken` 메서드 내 큰따옴표 감지 예외 처리 코드를 제거하여, 따옴표로 감싸진 경로 및 키워드 또한 정상적으로 Scope 가공을 받도록 조치합니다.

### ④ DataGrid 편집 상태에서 즉시 저장 시 변경사항 누락
- **진단**: **이미 해결됨 / 수정을 보류함 (Already Resolved)**
- **이유**: 현재 코드비하인드(`AliasManagerWindow.xaml.cs`)의 `SaveButton_Click` 메서드 내부에 `MappingDataGrid.CommitEdit(Row, true)` 호출 처리가 이미 견고하게 구현되어 있으며, XAML 바인딩 식에도 `UpdateSourceTrigger=PropertyChanged` 설정이 완료되어 있습니다. 따라서 별도의 코드 수정 없이 현상을 보존합니다.

### ⑤ 정규식(Regex)의 ReDoS(백트래킹 지연) 보호
- **진단**: **매우 타당함 (Minor)**
- **이유**: `QueryTransformer.cs` 내 중첩 밸런싱 정규식(`TokenRegex`)은 매칭되지 않는 깨진 괄호 문자열(예: `<<<<<<<<<<<<<`)을 다량 입력받을 때 기하급수적 백트래킹(ReDoS)으로 UI 프리징을 야기합니다.
- **해결 방안**: 정규식 인스턴스 컴파일 선언부에 `TimeSpan.FromMilliseconds(150)` 타임아웃 매개변수를 추가하여 임의의 ReDoS 취약점으로부터 방어합니다.

### ⑥ SQLite 'Database is Locked' 에러 방지 (WAL 모드 활성화)
- **진단**: **매우 타당함 (Minor)**
- **이유**: UI 스레드와 백그라운드 스레드 간 동시 읽기/쓰기가 발생할 때 SQLite 기본 `DELETE` 저널 모드는 엄격한 락을 취하므로 `Database is Locked` 예외가 발생할 우려가 있습니다.
- **해결 방안**: 데이터베이스 파일 연결 및 테이블 초기화 시점에 `PRAGMA journal_mode=WAL;` 설정을 실행하여 다중 세션 동시 액세스 성능 및 안정성을 확보합니다.

### ⑦ 드라이브 초기화 중 예외 발생 시 전체 중단 해결
- **진단**: **매우 타당함 (Minor)**
- **이유**: `SearchViewModel.cs`의 `InitializeDrives()` 내에서 전체 드라이브에 대해 `d.DriveType` 필터링 시, 권한이 없거나 준비되지 않은 미디어 장치로 인해 예외가 터지면 정상적인 C, D 드라이브를 포함한 전체 초기화가 중단됩니다.
- **해결 방안**: 드라이브 타입을 판별하고 수집하는 루프 내부에 개별 `try-catch`문을 보강하여 특정 예외 장치를 안전하게 스킵하도록 조치합니다.

---

## 2. 세부 구현 계획 (Implementation Plan)

### [Component 1] Native & Services

#### [MODIFY] [EverythingBridge.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Native/EverythingBridge.cs)
- `EverythingBridge` 클래스 내부 전역 정적 락 객체 `private static readonly object _engineLock = new object();` 추가.
- `IsEverythingRunning()` 및 `Search(...)` 메서드의 SDK API 진입 및 파싱 데이터 수집 전체 구간을 `lock (_engineLock) { ... }`으로 래핑하여 스레드 직렬화 보장.

#### [MODIFY] [DatabaseService.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Services/DatabaseService.cs)
- `InitializeDatabase()` 내 `SqliteConnection` 오픈 직후 `PRAGMA journal_mode=WAL;` 실행문 적용.
- `AliasMappings` 테이블 생성 쿼리 내 `Keyword TEXT PRIMARY KEY` -> `Keyword TEXT PRIMARY KEY COLLATE NOCASE`로 변경.
- `AppSettings` 테이블 생성 쿼리 내 `SettingKey TEXT PRIMARY KEY` -> `SettingKey TEXT PRIMARY KEY COLLATE NOCASE`로 변경.

#### [MODIFY] [QueryTransformer.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Services/QueryTransformer.cs)
- `IsWordToken()` 메서드 내 `if (token.StartsWith("\"") && token.EndsWith("\"")) return false;` 라인을 삭제.
- `TokenRegex` 정규식 선언 필드에 `TimeSpan.FromMilliseconds(150)` 생성 인자 추가.

### [Component 2] ViewModels

#### [MODIFY] [SearchViewModel.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/ViewModels/SearchViewModel.cs)
- `InitializeDrives()` 메서드 내 드라이브 수집 부분을 개별 `try-catch`가 포함된 루프로 리팩토링:
  ```csharp
  var fixedDrives = new List<string>();
  foreach (var driveInfo in System.IO.DriveInfo.GetDrives())
  {
      try
      {
          if (driveInfo.DriveType == System.IO.DriveType.Fixed)
          {
              fixedDrives.Add(driveInfo.Name.Substring(0, 2));
          }
      }
      catch (Exception ex)
      {
          System.Diagnostics.Debug.WriteLine($"드라이브 속성 접근 무시 ({driveInfo.Name}): {ex.Message}");
      }
  }
  ```

---

## 3. 검증 계획 (Verification Plan)

### 빌드 및 기본 동작 검증
1. `dotnet build`를 통한 컴파일 여부 점검.
2. 애플리케이션 실행을 통한 기본 Everything 쿼리 동작성 점검.

### 동시성 및 예외 케이스 집중 점검
1. **스레드 세이프티 교차 테스트**: 검색창에 초고속으로 입력을 갱신하며 동시에 백그라운드 엔진 상태 체커가 교차할 때 FFI 크래시나 검색어 뒤섞임 현상이 완전히 예방되었는지 관찰.
2. **대소문자 무결성 검증**: 데이터베이스에 수동으로 `Apple`과 `apple`을 번갈아 저장하려 할 때, `PRIMARY KEY` 락을 받아 단 하나의 키만 정상 저장/업데이트되는지 확인.
3. **따옴표 경로 검색 검증**: 경로 검색 옵션을 지정하고 검색창에 `"C:\Program Files"` 처럼 따옴표를 기입했을 때 Everything으로 최종 전달되는 변환 쿼리에 `path:` 접두사가 정상 포함되는지 디버그 점검.
4. **Regex ReDoS 복원 검증**: 검색창에 깨진 패턴(`<<<<<<<<<<<<<`)을 마구 기입해도 타임아웃 예외로 안전하게 우회되어 애플리케이션 프리징 현상이 발생하지 않는지 검증.
5. **드라이브 스킵 검증**: 장치가 준비되지 않은 미디어나 가상 디바이스가 꽂혀 있어도 정상 하드디스크 드라이브(C:, D:)가 컨트롤 칩 목록에 원활히 로딩되는지 확인.

---

## 4. 자산화 계획 (Assetization Plan)
- 최종 수정 반영 및 테스트가 정상적으로 완료되면, 완료 보고서(`walkthrough_ai_advice_evaluation_v2.md`)로 갱신 보존하며 `./docs/memories/AGENTS.md`에 non-trivial 자산 기록을 갱신합니다.
