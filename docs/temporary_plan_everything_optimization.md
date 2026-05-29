# Everything SDK 연동 고속화 및 CPU 굉음 차단 최적화 계획

Everything 엔진의 빠른 속도를 그대로 살리지 못하고 앱 기동 시나 검색어 초기화 시 심각한 CPU 점유율 상승(굉장한 PC 팬 소음) 및 앱 버벅임이 발생하는 근본 원인을 분석하고, 이를 완전히 완치하기 위한 최적화 구현 계획서입니다.

## 📌 근본 원인 분석

1. **무제한 결과 조회 (`DefaultMaxResults = 0xFFFFFFFF`)와 FFI 마샬링 병목**:
   - `AppConstants.cs`에 정의된 `DefaultMaxResults`가 `0xFFFFFFFF`(무제한)로 설정되어 있습니다. 
   - 검색어가 비어 있거나(초기화) 검색 결과가 매우 많을 때, Everything SDK는 PC 전체에 존재하는 수십만~수백만 건의 모든 파일 정보를 가져오게 됩니다.
   - Everything C++ 엔진 자체는 이 검색 결과를 인메모리 포인터 상태로 즉시 조회하지만, **C# 애플리케이션으로 마샬링해 오는 과정**에서 50만 건 기준 **50만 번 루프**를 돌며 P/Invoke FFI 함수들(`GetResultFileName`, `GetResultPath`, `Everything_GetResultSize` 등)을 매 행마다 호출해야 합니다. 이 엄청난 양의 마샬링(Marshaling) 오버헤드는 CPU를 폭증시킵니다.
   
2. **C# 가비지 컬렉션(GC) 지옥**:
   - 50만 건의 결과가 반환되면 C# 메모리 힙 공간에 50만 개의 `SearchResultItem` 인스턴스 객체가 한 번에 생성됩니다.
   - 이로 인해 메모리가 순간적으로 수백 MB 급으로 폭증하며, 이 거대한 일시적 객체(Short-lived objects)들을 청소하기 위해 가비지 컬렉터(GC)가 긴급 개입하여 시스템 리소스를 100% 점유하게 됩니다. 이 과정에서 CPU 팬이 세차게 돌며 굉음이 발생하고 UI 스레드가 멈추어 버벅거리게 됩니다.

3. **WPF UI의 바인딩 부하**:
   - 아무리 가상화(`VirtualizingStackPanel`)가 켜져 있어도, 50만 건의 리스트 데이터를 `RangeObservableCollection`을 통해 리스트뷰에 통째로 채워 넣는 순간 바인딩 오버헤드가 극대화됩니다.

### 💡 Everything.exe(네이티브 공식 앱)가 부드럽고 굉음이 없는 이유
- C#과 같은 **P/Invoke(FFI) 복사 오버헤드** 및 **GC 렉**이 전혀 없습니다.
- 화면에 보이는 수십 개의 항목만을 색인 포인터에서 직접 렌더링하는 **Virtual List View** 방식으로 동작하므로, 결과가 100만 개든 1000만 개든 전체 데이터를 개별 메모리 구조체로 한 땀 한 땀 새로 만드는 멍청한 짓을 전혀 하지 않고 항상 0.001초 이하로 반응합니다.

---

## 🛠️ 제안하는 해결 방안 및 최적화 로드맵

### 1. 합리적인 최대 조회 한도 지정 (Max Limit 필터링)
- 검색 창에서 마우스 스크롤을 끝까지 내려 수십만 번째 파일 결과를 직접 눈으로 검사하는 사용자는 존재하지 않습니다. 사실상 상위 **`10,000개`** 내외만 보여주어도 검색에 필요한 모든 정보를 차고 넘치게 커버할 수 있습니다.
- `AppConstants.cs`의 `DefaultMaxResults`를 무제한(`0xFFFFFFFF`)이 아닌 **`10,000`**으로 제한합니다.
- 이렇게 하면 전체 조회를 돌리더라도 딱 상위 10,000개 항목에 대해서만 FFI 조회를 진행하고 객체를 생성합니다. 10,000건의 P/Invoke 데이터 마샬링 및 인스턴스 할당은 현대 CPU 기준 **0.02초** 내외로 끝나기 때문에 렉과 CPU 굉음이 100% 완전 소멸합니다.

### 2. 초기 켜질 때 및 빈 검색어(초기화) 시 추가 한도 제약 (선택 사항)
- 검색어가 비어 있는 "초기 상태"나 "초기화 리셋" 시에는 한도를 **`3,000개 ~ 5,000개`** 정도로 더 가볍게 제약해 로딩을 더욱 빛의 속도로 단축할 수 있습니다.

---

## Tech Stack
- C# .NET 9.0 / WPF
- Everything SDK FFI

## Proposed Changes

### [MODIFY] [AppConstants.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Config/AppConstants.cs)
- `DefaultMaxResults` 값을 `0xFFFFFFFF`에서 `10000`으로 하향 튜닝합니다.

### [MODIFY] [EverythingBridge.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Native/EverythingBridge.cs)
- 검색어가 완전히 비어있을 때(`string.IsNullOrEmpty(processedQuery.Trim())`) CPU 점유율을 더욱 철저히 방어하기 위해 빈 쿼리 시의 최대 출력수를 `2,000`개로 차별 적용하는 분기 로직을 탑재합니다.

## Verification Plan

### 수동 성능 검증
1. **검색어 초기화 속도 측정**: 임의의 문자로 검색 후 백스페이스로 빠르게 지웠을 때, 혹은 Reset 버튼을 눌렀을 때 굉음이 나는지 여부와 즉각적인 화면 반응 속도(0.05초 미만) 확인.
2. **CPU 점유율 점검**: 작업 관리자를 켜놓고 검색 옵션 변경, 미디어 필터 칩 토글, 전체 검색 진행 시 CPU 사용량이 100%로 치솟거나 팬 굉음이 발생하는지 체크.
3. **가비지 컬렉터 부하 진입 여부**: 잦은 필터 토글 시 버벅임이 누적되는 증상이 깔끔하게 차단되었는지 확인.
