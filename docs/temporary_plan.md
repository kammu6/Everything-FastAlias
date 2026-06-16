# 임시 계획서: 정렬 기준(Sort Column & Direction) 영속화 개선

본 계획서는 파일 검색 결과의 정렬 기준(정렬 컬럼 및 정렬 방향) 정보를 SQLite 로컬 설정 데이터베이스에 영속적으로 저장하고 애플리케이션 시작 시 이를 복원하여, 사용자가 설정한 파일 정렬 뷰가 앱 재구동 후에도 유지되도록 하기 위한 추가 개선 설계서입니다.

---

## 1. 요구사항 (Requirements)

- **정렬 조건 저장 및 로딩**:
  - 결과 뷰의 정렬 기준이 변경될 때마다 정렬 컬럼(`SortColumn`) 및 정렬 방향(`SortDirection`) 정보를 SQLite 데이터베이스의 `AppSettings` 테이블에 저장.
  - 앱 기동 시(`LoadSettings`), 저장된 정렬 기준 정보를 복원하여 이후 수행되는 모든 검색 결과에 해당 정렬 필터가 자동으로 선반영되도록 조율.

---

## 2. 상세 구현 계획 (Implementation Plan)

### 단계 1: 설정 저장 및 복원 반영 (`ViewModels/SearchViewModel.Settings.cs` [MODIFY])
1. `LoadSettings()` 메소드 내부에 아래 항목 추가:
   - `SortColumn` 값 읽기 (기본값: `"이름"`):
     `SortColumn = db.GetSetting("SortColumn", "이름");`
   - `SortDirection` 값 읽기 및 파싱 (기본값: `"Ascending"`):
     `var sortDirStr = db.GetSetting("SortDirection", "Ascending");`
     `if (Enum.TryParse<System.ComponentModel.ListSortDirection>(sortDirStr, out var dir)) SortDirection = dir;`
2. `SaveSettings()` 메소드 내부에 아래 항목 추가:
   - `SortColumn` 값 저장:
     `db.SaveSetting("SortColumn", SortColumn);`
   - `SortDirection` 값 저장 (문자열로 직렬화):
     `db.SaveSetting("SortDirection", SortDirection.ToString());`

### 단계 2: 정렬 즉각 저장 트리거 반영 (`ViewModels/SearchViewModel.Search.cs` [MODIFY])
1. `SortResults(string columnName)` 메소드에서 정렬 방향 및 기준 계산이 끝나고 `ApplySorting()`을 호출한 직후, `SaveSettings()`를 명시적으로 트리거하여 DB에 영속화하도록 변경.

---

## 3. 검증 계획 (Verification Plan)

### 수동 검증
1. 앱 구동 후 정렬 기준을 "크기 / 내림차순"으로 클릭하여 정렬 방식을 변경.
2. 애플리케이션을 종료했다가 다시 구동.
3. 임의의 검색어를 입력하고 엔터를 눌러 검색이 완료되었을 때, 정렬 표시 및 정렬 순서가 이전 기동 시 적용했던 "크기 / 내림차순" 기준으로 부드럽게 자동 정렬되는지 확인.

---

## 4. 지식 자산화 계획 (Capitalization Plan)
- 수동 검증 완료 후, 본 임시 계획의 변경 세부사항과 자산 정보를 `./docs/memories/MEMORY.md` 및 메인 계획서(`implementation_plan.md`), 워크스루(`walkthrough.md`)에 병합 업데이트.
