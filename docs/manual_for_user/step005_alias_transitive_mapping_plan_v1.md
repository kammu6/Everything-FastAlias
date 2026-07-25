# 📑 Alias 다중 매핑 및 동적 그룹 합집합(Option B) 구현 계획서 (v1.1)

이 계획서는 Alias Manager에 등록된 단어 매핑 테이블에서 공통 동의어를 공유하는 복수 그룹들을 실시간 합집합으로 엮어내되, 불필요한 원본 키워드(가상 태그)들은 쿼리 변환 목록에서 완벽하게 배제하는 **동적 다중 그룹 매칭(Dynamic Multi-Group Matching - Option B)** 알고리즘 설계를 정의합니다.

---

## 🔍 1. 현황 및 문제점 분석

### [사용자 예시 시나리오]
* **그룹 1 (태그 A):** 원본 키워드 `A` ➔ 변환 단어 목록 `a, b, c`
* **그룹 2 (태그 B):** 원본 키워드 `B` ➔ 변환 단어 목록 `c, d, f`

### [As-Is 및 단순 연결 요소(Connected Component)의 한계]
* **As-Is 단방향 한계:** 동의어 `a`나 `c`를 입력했을 때 역방향 매칭이 되지 않고 일반 단어로 처리됨.
* **단순 그래프 연결 요소의 한계 (A안):**
  * `A`와 `B`가 공통 동의어 `c`로 엮여 있다고 해서, `A`를 검색할 때 전혀 무관한 `B` 그룹의 동의어들(`d, f`)까지 무조건 하나의 전체 합집합으로 치환되어 검색 성능 저하 및 결과 오염을 유발함.
  * 예: `#back_to_freedom`을 검색했을 때 이와 무관한 `#Destroyer` 그룹의 `test1, test2`까지 튀어나옴.

### [To-Be 목표 동작 (Option B)]
* **원본 키워드 배제:** `#back_to_freedom` 같이 묶음 역할만 하는 가상 태그(Keyword)는 쿼리 변환 목록에서 완전히 제외하여 검색 성능을 최적화합니다.
* **입력 단어 기준 동적 합집합:** 입력된 단어가 직접 멤버로 속해 있는 모든 그룹들의 동의어 목록만 결합(OR)합니다:
  * `A` 검색 시 ➔ `a | b | c` (원본 키워드 A는 제외, 그룹 1만 해당)
  * `a` 검색 시 ➔ `a | b | c`
  * `c` 검색 시 ➔ `a | b | c | d | f` (그룹 1과 그룹 2에 동시에 걸쳐 있으므로 두 그룹의 합집합)

---

## 🎯 2. 해결 방안: 동적 다중 그룹 매칭 알고리즘

`BuildAliasGroupsCache()` 실행 시, 모든 단어에 대해 이 동적 합집합을 선계산(Precompute)하여 캐싱함으로써 O(1) 탐색 속도와 검색 결과 정밀도를 모두 확보합니다.

### 1단계: DatabaseService.cs 캐시 빌더 수정 (Precomputation)

1. DB에서 로드된 `_cache` (원본 키워드 -> 동의어 리스트)를 기반으로 **원본 키워드 블랙리스트 세트**(`originalKeywords`)를 구성합니다.
2. 모든 고유 단어(원본 및 동의어)에 대해 그 단어가 포함된 모든 로우의 **동의어 목록(`Words`)들의 합집합**을 구합니다.
3. 이 합집합에서 `originalKeywords`에 속하는 단어들을 완전히 제거(Filter)합니다.
4. 검색 대상이 일반 단어(원본이 아님)일 경우, 검색어 자체는 치환 대상에 반드시 포함되도록 유지합니다.

```csharp
private void BuildAliasGroupsCache()
{
    lock (_aliasLock)
    {
        var newGroups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        
        // _cache 복사본 획득
        Dictionary<string, List<string>> tempCache;
        lock (_cacheLock)
        {
            tempCache = new Dictionary<string, List<string>>(_cache, StringComparer.OrdinalIgnoreCase);
        }

        // 1. 원본 키워드 블랙리스트 구성
        var originalKeywords = new HashSet<string>(tempCache.Keys, StringComparer.OrdinalIgnoreCase);

        // 2. 고유 단어별 소속 로우(Words 리스트) 매핑용 인덱스 준비
        var wordToWordsLists = new Dictionary<string, List<List<string>>>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in tempCache)
        {
            var keyword = kvp.Key.Trim();
            if (string.IsNullOrEmpty(keyword)) continue;

            // 로우 전체 멤버 수집 (Keyword + Words)
            var rowElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { keyword };
            var wordsList = new List<string>();

            if (kvp.Value != null)
            {
                foreach (var syn in kvp.Value)
                {
                    var trimmedSyn = syn.Trim().TrimEnd(';');
                    if (!string.IsNullOrEmpty(trimmedSyn))
                    {
                        rowElements.Add(trimmedSyn);
                        wordsList.Add(trimmedSyn);
                    }
                }
            }

            // 모든 멤버에 대해 이 로우의 Words(동의어 풀) 목록을 등록
            foreach (var member in rowElements)
            {
                if (!wordToWordsLists.TryGetValue(member, out var lists))
                {
                    lists = new List<List<string>>();
                    wordToWordsLists[member] = lists;
                }
                lists.Add(wordsList);
            }
        }

        // 3. 단어별 최종 동적 합집합 계산 및 캐싱
        foreach (var kvp in wordToWordsLists)
        {
            var word = kvp.Key;
            var lists = kvp.Value;

            var unionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var list in lists)
            {
                foreach (var w in list)
                {
                    unionSet.Add(w);
                }
            }

            // 원본 키워드(A, B 등)는 치환 목록에서 원천 배제 (Option B 핵심)
            unionSet.RemoveWhere(w => originalKeywords.Contains(w));

            // 입력 단어 자체가 일반 동의어라면 자기 자신은 치환 결과에 보존
            if (!originalKeywords.Contains(word))
            {
                unionSet.Add(word);
            }

            if (unionSet.Count > 0)
            {
                newGroups[word] = unionSet;
            }
        }

        _aliasGroups = newGroups;

        // 4. 고유 키들을 글자 수 역순으로 정렬 캐시 갱신
        var keys = new List<string>(_aliasGroups.Keys);
        keys.Sort((a, b) => b.Length.CompareTo(a.Length));
        _sortedAliasKeys = keys;
    }
}
```

---

## 🛠️ 3. 이행 및 검증 계획

1. **DatabaseService.cs 캐시 엔진 수정:** 위의 동적 다중 그룹 매칭(B안) 알고리즘 반영.
2. **QueryTransformer.cs 동의어 치환 로직 개편:**
   - 기존 `mappings` 대신 `DatabaseService.Instance.GetAliasGroupsCache()` 사전을 직접 활용하여 쿼리 토큰 매핑 수행.
3. **단위 테스트 추가:** `QueryTransformerTest.cs`에 사용자가 제시한 전이성 동의어 케이스(A ➔ a\|b\|c, c ➔ 합집합)를 완벽히 검증하는 유닛 테스트 구현 및 `dotnet test` 확인.

