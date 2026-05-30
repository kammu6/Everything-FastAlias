# 조언 AI 3차 피드백 평가 및 안정성 개선 계획서 (Temporary Plan/v3)

외부 조언 AI가 분석한 Everything FastAlias 3차 코드 리뷰 내용(불필요한 설정 DB 저장 디스크 I/O 오버헤드)에 대해 실제 코드를 검증하고 해결책을 제시하는 임시 계획서입니다.

---

## 1. 조언 AI 제안사항 타당성 검증 결과 (Evaluation)

### ① 불필요한 설정 DB 저장(I/O) 오버헤드 문제
- **진단**: **타당함 (Minor)**
- **이유**: `SearchViewModel.cs`의 각 필터 프로퍼티(제외 단어, 폴더 경로, 확장자, 크기 등)가 변경될 때마다 `TriggerSearch()`가 즉각 호출되어 내부의 `SaveSettings()`가 디스크 쓰기를 유발합니다. 사용자가 제외 단어창에 타이핑을 한 글자씩 칠 때마다 동기식으로 DB에 쓰기 작업(Update)이 수행되므로 성능 및 디스크 수명에 악영향을 미칠 수 있습니다.
- **개선 방안**:
  - 디바운스 타이머만 작동시키는 순수 트리거용 `TriggerSearchOnly()` 메서드를 신설합니다.
  - 필터 프로퍼티 Setter 및 드라이브 선택 변경 시에는 `TriggerSearch()` 대신 `TriggerSearchOnly()`를 호출하여 타이핑 도중 DB 쓰기가 반복 수행되는 것을 방지합니다.
  - 실제 변경된 설정값들은 디바운스가 완료되고 검색 쿼리가 실행되는 시점인 `ExecuteSearchAsync()` 도입부에서 딱 1번만 `SaveSettings()`를 호출하여 안전하게 저장하도록 일원화합니다.

---

## 2. 세부 구현 계획 (Implementation Plan)

### [Component 1] ViewModels

#### [MODIFY] [SearchViewModel.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/ViewModels/SearchViewModel.cs)
- **메서드 신설**:
  ```csharp
  private void TriggerSearchOnly()
  {
      _debounceTimer.Stop();
      _debounceTimer.Start();
  }
  ```
- **Setter들 변경**:
  - `ExcludedWords`, `FolderPaths`, `CustomExtensions`, `MinSize`, `MaxSize`의 Setter 내부 및 드라이브 아이템 변경 핸들러(`DriveItem_PropertyChanged`)에서 기존 `TriggerSearch()` 대신 `TriggerSearchOnly()`를 호출하도록 갱신.
  - `Scope` 변경 속성(`ScopeAll`, `ScopeFile`, `ScopePath`) 및 `SizeUnit` 변경 속성(`SizeKB`, `SizeMB`, `SizeGB`), `Media` 프리셋 변경 속성(`MediaAll`, `MediaFolder` 등) 내부에서도 기존 `TriggerSearch()` 대신 `TriggerSearchOnly()`를 호출하도록 갱신.
- **TriggerSearch() 정리**:
  - 기존 `TriggerSearch()` 메서드는 불필요하게 사용되는 곳을 지우거나, 수동 저장용으로 남겨둠. (혹은 안전하게 `SaveSettings(); TriggerSearchOnly();` 구조를 유지).
- **디바운스 실행 시점 저장**:
  - `ExecuteSearchAsync()` 메서드 시작 부분에서 `SaveSettings();`를 실행하여, 사용자의 연속 타이핑이 완료되고 실제 검색 태스크가 가동되는 순간에 단 1회 디스크 쓰기가 수행되도록 최적화.

---

## 3. 검증 계획 (Verification Plan)

### 빌드 및 동작 검증
1. `dotnet build` 수행으로 코드 컴파일 오류 여부 체크.
2. 제외 단어나 경로 등을 텍스트박스에 빠른 속도로 연속 입력할 때, UI 지연이나 프리징 현상이 완전 해소되었는지 검증.
3. 타이핑 입력 후 엔터 검색 또는 디바운스 검색이 완료된 뒤 프로그램을 재시작했을 때 변경된 필터 값들이 정상적으로 SQLite DB로부터 읽혀서 복원되는지 영구 저장 무결성 테스트 진행.
