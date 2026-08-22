# description: "FastAlias 키워드 일치 우선(Keyword Priority) 모델의 케이스별 쿼리 변환 알고리즘 시뮬레이션 및 단위 검증"
"""
Author: Antigravity
Date: 2026-08-22
Usage: python scripts/tests/alias/chk_keyword_priority_model.py
"""

from typing import Dict, List, Set, Tuple


def build_dual_index_cache(
    raw_mappings: Dict[str, List[str]]
) -> Tuple[Dict[str, Set[str]], Dict[str, Set[str]]]:
    """
    DatabaseService.BuildAliasGroupsCache()의 C# 로직을 100% 동일하게 시뮬레이션합니다.
    1. direct_groups: A열 키워드의 1:1 고유 정의 (Words 목록 전체가 고유 정의)
    2. alias_groups: B열 동의어(Word)가 소속된 다른 행들의 Words 합집합
    """
    direct_groups: Dict[str, Set[str]] = {}
    word_to_words_lists: Dict[str, List[List[str]]] = {}

    for keyword, words in raw_mappings.items():
        keyword_clean = keyword.strip()
        if not keyword_clean:
            continue

        words_list: List[str] = []
        direct_set: Set[str] = set()

        for syn in words:
            syn_clean = syn.strip().rstrip(";")
            if syn_clean:
                words_list.append(syn_clean)
                direct_set.add(syn_clean)

        # A열 키워드의 1:1 고유 정의 저장 (Words 목록 전체)
        if direct_set:
            direct_groups[keyword_clean.lower()] = direct_set

        # B열 Words에 등장하는 각 단어(동의어)에 대해 이 로우의 Words 목록 등록
        for w in words_list:
            w_lower = w.lower()
            if w_lower not in word_to_words_lists:
                word_to_words_lists[w_lower] = []
            word_to_words_lists[w_lower].append(words_list)

    # 단어별 최종 동적 합집합 계산 (_aliasGroups)
    alias_groups: Dict[str, Set[str]] = {}
    for word_lower, lists in word_to_words_lists.items():
        union_set: Set[str] = set()
        for lst in lists:
            for item in lst:
                union_set.add(item)

        if union_set:
            alias_groups[word_lower] = union_set

    return direct_groups, alias_groups


def transform_query(
    term: str,
    prioritize_keyword_match: bool,
    direct_groups: Dict[str, Set[str]],
    alias_groups: Dict[str, Set[str]]
) -> str:
    """
    QueryTransformer.BuildFirstStageQuery()의 C# 분기 로직을 100% 동일하게 시뮬레이션합니다.
    """
    term_lower = term.strip().lower()
    synonyms: Set[str] = set()

    if prioritize_keyword_match:
        # [ON] 키워드 일치 우선: A열 Keyword 고유 정의 우선 조회
        if term_lower in direct_groups:
            synonyms = direct_groups[term_lower]
        elif term_lower in alias_groups:
            synonyms = alias_groups[term_lower]
    else:
        # [OFF] 동의어 합집합 모드: B열 동의어 소속 그룹 합집합 우선 조회
        if term_lower in alias_groups:
            synonyms = alias_groups[term_lower]
        elif term_lower in direct_groups:
            synonyms = direct_groups[term_lower]

    if not synonyms:
        return f"<{term}>"

    # 정렬하여 Everything OR 쿼리 형식 생성
    sorted_syns = sorted(list(synonyms))
    alias_parts = [f"<<{s}>>" for s in sorted_syns]
    return f"<{ ' | '.join(alias_parts) }>"


def run_all_tests():
    print("=" * 80)
    print(" [FastAlias Step 002] 키워드 일치 우선(Keyword Priority) 로직 케이스별 검증")
    print("=" * 80)

    # 1. 4개 그룹 표준 데이터셋 정의
    dataset = {
        "#a": ["b", "c"],
        "d":  ["e", "c"],
        "e":  ["f", "g"],
        "h":  ["i", "e"]
    }

    print("\n[1] 표준 데이터셋:")
    for k, v in dataset.items():
        print(f"  • Keyword: {k:<5} ➔ Words: {'; '.join(v)}")

    # 2. 인덱스 캐시 빌드
    direct_groups, alias_groups = build_dual_index_cache(dataset)

    print("\n[2] 빌드된 DirectKeywordMap (1:1 키워드 정의):")
    for k, v in direct_groups.items():
        print(f"  • Direct[{k}] = {sorted(list(v))}")

    print("\n[3] 빌드된 WordOccurrenceMap (동의어 소속 그룹 합집합):")
    for k, v in alias_groups.items():
        print(f"  • Alias[{k}] = {sorted(list(v))}")

    # 3. 케이스별 기대 결과 정의 및 검증
    test_cases = [
        (
            "#a",
            "<<<b>> | <<c>>>",
            "<<<b>> | <<c>>>",
            "가상 태그 검색 (원본 키워드명 #a 배제)"
        ),
        (
            "b",
            "<<<b>> | <<c>>>",
            "<<<b>> | <<c>>>",
            "단일 그룹 단어 검색 (그룹 1 동의어 반환)"
        ),
        (
            "c",
            "<<<b>> | <<c>> | <<e>>>",
            "<<<b>> | <<c>> | <<e>>>",
            "다중 소속 일반 단어 검색 (그룹 1+2 합집합)"
        ),
        (
            "e",
            "<<<f>> | <<g>>>",
            "<<<c>> | <<e>> | <<i>>>",
            "⭐️ 키워드 겸 동의어 (ON: 그룹3 정의 1:1, OFF: 그룹2+4 합집합)"
        ),
    ]

    print("\n[4] 케이스별 쿼리 변환 검증 실행:")
    print("-" * 80)
    print(f"{'검색어':<6} | {'모드':<5} | {'실제 결과':<30} | {'기대 결과':<30} | {'결과'}")
    print("-" * 80)

    all_passed = True

    for term, expected_on, expected_off, desc in test_cases:
        # ON 검증
        actual_on = transform_query(term, True, direct_groups, alias_groups)
        passed_on = (actual_on == expected_on)
        print(f"{term:<6} | {'ON':<5} | {actual_on:<30} | {expected_on:<30} | {'✅ PASS' if passed_on else '❌ FAIL'}")
        if not passed_on:
            all_passed = False

        # OFF 검증
        actual_off = transform_query(term, False, direct_groups, alias_groups)
        passed_off = (actual_off == expected_off)
        print(f"{term:<6} | {'OFF':<5} | {actual_off:<30} | {expected_off:<30} | {'✅ PASS' if passed_off else '❌ FAIL'}")
        if not passed_off:
            all_passed = False
        print(f"       └─ 💡 {desc}")
        print("-" * 80)

    print("\n" + "=" * 80)
    if all_passed:
        print(" 🎉 모든 케이스(ON/OFF)의 수학적 집합 및 쿼리 변환 결과가 100% 일치합니다!")
    else:
        print(" ⚠️ 일부 케이스에서 불일치가 발생했습니다.")
    print("=" * 80)


if __name__ == "__main__":
    run_all_tests()
