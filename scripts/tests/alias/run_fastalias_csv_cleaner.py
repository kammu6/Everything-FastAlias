# description: "Cleans Words column in FastAlias CSV export by filtering year-pattern words, 1-char noise, >10 char Japanese text, removing parentheses, and excluding single-word Korean/English in Type 3 keywords."

import argparse
import csv
import os
import re
import sys
import time
from typing import Tuple, List, Set, Dict

# [규칙 1] 출생연도 및 연도 표기 패턴 (-1985, -2001, 【2018年】, (1982), （2015） 등)
RE_YEAR_PATTERN = re.compile(r'[-_](?:19|20)\d{2}|[【（\(].*?(?:19|20)\d{2}.*?[】）\)]|(?:19|20)\d{2}年')

# [규칙 3] 일본어 가나(히라가나/가타카나) 감지 정규식
RE_JAPANESE_KANA = re.compile(r'[\u3040-\u309F\u30A0-\u30FF]')

# 일본어 가나 및 CJK 한자 감지 정규식
RE_JAPANESE_OR_KANJI = re.compile(r'[\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FFF\u3400-\u4DBF]')

# 한글 문자 감지 정규식
RE_KOREAN = re.compile(r'[\uac00-\ud7a3\u1100-\u11ff\u3130-\u318f]')

# 순수 영문 정규식
RE_ENGLISH = re.compile(r'^[A-Za-z_\.\-]+$')


def clean_parens(s: str) -> str:
    """
    문자열 내의 괄호 및 괄호 내부 내용, 주변 공백을 안전하게 제거하고 정돈합니다.
    예: 'Hara Nozomi (原望美)' -> 'Hara Nozomi'
        '(原望美) Nozomi Hara' -> 'Nozomi Hara'
    """
    if not s:
        return ""
    # 1. 괄호 쌍 및 내부 내용 제거 (중첩 괄호 반복 처리)
    pattern = r'\s*[\(（\[【][^\(\)（）\[\]【】]*[\)）\]】]\s*'
    while re.search(pattern, s):
        s = re.sub(pattern, ' ', s)
    # 2. 짝이 맞지 않는 잔여 괄호 기호 제거
    s = re.sub(r'[\(\)（）\[\]【】]', ' ', s)
    # 3. 연속 공백 단일화 및 trim
    return re.sub(r'\s+', ' ', s).strip()


def should_filter_keyword(kw: str) -> Tuple[bool, str]:
    """
    Keyword(A열)에 대한 필터링 규칙을 검사합니다.
    - #으로 시작하거나 한글이 포함된 키워드는 보존 (예: #back_to_freedom, 박라희)
    - 공백이 포함된 영문 이름(성+이름)은 보존 (타입 3: 예: Hara Nozomi, Abe Ai)
    - 공백 없는 단일 영문 단어는 제외 (예: Hinako, Mayu, Mei, Mio)
    Returns:
        (is_valid, reason) - is_valid가 True이면 유지, False이면 행 제외
    """
    kw_clean = kw.strip()
    if not kw_clean:
        return False, "Empty keyword"

    # 1. #으로 시작하는 해시태그 키워드는 보존
    if kw_clean.startswith("#"):
        return True, "Keep (Hashtag)"

    # 2. 한글이 포함된 키워드는 보존
    if RE_KOREAN.search(kw_clean):
        return True, "Keep (Korean)"

    # 3. 공백이 포함된 키워드는 성+이름으로 간주하여 보존 (타입 3)
    if " " in kw_clean:
        return True, "Keep (Type 3: Full Name with Space)"

    # 4. 공백이 없는 순수 영문/알파벳 단일 단어 키워드는 제외
    if re.match(r'^[A-Za-z_\.\-]+$', kw_clean):
        return False, "Drop (Single English keyword without space)"

    return True, "Keep (Other)"


def is_type3_keyword(kw: str) -> bool:
    """
    키워드가 '타입 3'(공백이 포함된 성+이름 키워드)인지 확인합니다.
    """
    kw_clean = kw.strip()
    return bool(kw_clean and not kw_clean.startswith("#") and " " in kw_clean)


def should_filter_word(token: str, is_type3: bool = False) -> Tuple[bool, str]:
    """
    단어(Words)가 필터링 규칙에 걸리는지 판별합니다.
    Returns:
        (is_valid, reason) - is_valid가 True이면 유지, False이면 제외 대상
    """
    token_clean = token.strip()
    if not token_clean:
        return False, "Empty token"

    # 규칙 1: 출생연도/연도 표기 패턴 제거 (예: Suzu-1997, 美咲-1985, 白雪ひなの【2018年】, Honoka (穂花-1982))
    if RE_YEAR_PATTERN.search(token_clean):
        return False, "Rule 1 (Year pattern)"

    # 규칙 2: 공백이 없으면서 길이가 1글자인 단어 제외 (예: 똥, 에, 칸, 瞳, 짱 등)
    if " " not in token_clean and len(token_clean) <= 1:
        return False, "Rule 2 (1 char no space)"

    # 규칙 3: 일본어가 포함된 10자 초과 긴 단어/문장 제외 (예: 잘못 파싱된 비디오 제목, 긴 문장, 긴 복합표기)
    if len(token_clean) > 10 and RE_JAPANESE_KANA.search(token_clean):
        return False, "Rule 3 (>10 chars Japanese)"

    # 규칙 4: 타입 3 키워드 전용 - 일본어(한자)를 제외하고 한글 또는 영문 중 띄어쓰기 없이 1단어로 된 단어 제거
    # (예: '세토히마리' -> 제거, '세토 히마리' -> 유지, '瀬戸ひまり' -> 유지)
    if is_type3 and " " not in token_clean:
        # 일본어(가나) 또는 CJK 한자가 포함되어 있지 않은 경우
        if not RE_JAPANESE_OR_KANJI.search(token_clean):
            # 1단어 한글 또는 1단어 영문인 경우 제거
            if RE_KOREAN.search(token_clean) or RE_ENGLISH.match(token_clean):
                return False, "Rule 4 (Type 3: single-word Korean/English without space)"

    return True, "Keep"


def process_csv(
    input_path: str,
    output_path: str,
    dry_run: bool = True,
    sample_limit: int = 20,
    report_path: str = ""
):
    if not os.path.exists(input_path):
        print(f"[ERROR] Input file not found: {input_path}")
        sys.exit(1)

    start_time = time.time()
    total_rows = 0
    valid_rows = 0
    empty_rows_dropped = 0
    kw_dropped_count = 0

    total_words = 0
    kept_words_count = 0
    rule1_dropped_count = 0
    rule2_dropped_count = 0
    rule3_dropped_count = 0
    rule4_dropped_count = 0

    sample_kw_dropped: List[str] = []
    sample_rule1: Set[str] = set()
    sample_rule2: Set[str] = set()
    sample_rule3: Set[str] = set()
    sample_rule4: Set[str] = set()
    sample_kept: Set[str] = set()
    sample_empty_keywords: List[str] = []
    rule4_by_keyword: Dict[str, List[str]] = {}

    temp_output_path = output_path + ".tmp" if not dry_run else None
    out_file = None
    writer = None

    if not dry_run:
        os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
        # Windows/Excel 및 .NET 호환성을 위해 UTF-8 with BOM(utf-8-sig)으로 임시 파일에 저장
        out_file = open(temp_output_path, "w", encoding="utf-8-sig", newline="")
        writer = csv.writer(out_file, delimiter=",", quoting=csv.QUOTE_MINIMAL)

    try:
        # utf-8-sig로 BOM 자동 처리
        with open(input_path, "r", encoding="utf-8-sig", errors="replace") as in_f:
            reader = csv.reader(in_f)

            is_first = True
            for row in reader:
                if not row:
                    continue

                if is_first:
                    is_first = False
                    # 헤더 처리
                    if len(row) >= 1 and ("keyword" in row[0].lower() or "원본" in row[0]):
                        if not dry_run and writer:
                            writer.writerow(["Keyword", "Words"])
                        continue

                total_rows += 1
                raw_keyword = row[0].strip() if len(row) > 0 else ""
                raw_words = row[1] if len(row) > 1 else ""

                if not raw_keyword:
                    continue

                # 괄호 정리
                keyword = clean_parens(raw_keyword)
                if not keyword:
                    keyword = raw_keyword

                # Keyword 검사 (단일 영문 키워드 제외 규칙)
                is_kw_valid, _ = should_filter_keyword(keyword)
                if not is_kw_valid:
                    kw_dropped_count += 1
                    if len(sample_kw_dropped) < 50:
                        sample_kw_dropped.append(keyword)
                    continue

                # 타입 3 키워드 여부 판정
                is_type3 = is_type3_keyword(keyword)

                # Words 분할 (세미콜론 기준)
                tokens = [t.strip() for t in raw_words.split(";") if t.strip()]
                filtered_tokens = []
                seen_tokens = set()

                for raw_token in tokens:
                    total_words += 1
                    
                    # 1. 단어 내 괄호 및 주변 공백 정리
                    cleaned_token = clean_parens(raw_token)
                    if not cleaned_token:
                        continue

                    # 2. 필터링 규칙 검사
                    is_valid, reason = should_filter_word(cleaned_token, is_type3=is_type3)

                    if not is_valid:
                        if reason.startswith("Rule 1"):
                            rule1_dropped_count += 1
                            if len(sample_rule1) < 100:
                                sample_rule1.add(raw_token)
                        elif reason.startswith("Rule 2"):
                            rule2_dropped_count += 1
                            if len(sample_rule2) < 100:
                                sample_rule2.add(raw_token)
                        elif reason.startswith("Rule 3"):
                            rule3_dropped_count += 1
                            if len(sample_rule3) < 100:
                                sample_rule3.add(raw_token)
                        elif reason.startswith("Rule 4"):
                            rule4_dropped_count += 1
                            sample_rule4.add(cleaned_token)
                            rule4_by_keyword.setdefault(keyword, []).append(cleaned_token)
                    else:
                        token_key = cleaned_token.lower()
                        if token_key not in seen_tokens:
                            seen_tokens.add(token_key)
                            filtered_tokens.append(cleaned_token)
                            kept_words_count += 1
                            if len(sample_kept) < 50:
                                sample_kept.add(cleaned_token)

                if filtered_tokens:
                    valid_rows += 1
                    if not dry_run and writer:
                        writer.writerow([keyword, ";".join(filtered_tokens)])
                else:
                    empty_rows_dropped += 1
                    if len(sample_empty_keywords) < 20:
                        sample_empty_keywords.append(keyword)

    finally:
        if out_file:
            out_file.close()
        # 프로세스 완료 후 임시 파일을 대상 경로로 원자적 교체
        if not dry_run and temp_output_path and os.path.exists(temp_output_path):
            os.replace(temp_output_path, output_path)

    elapsed = time.time() - start_time

    # 통계 출력 (토큰 절약을 위해 간결하고 핵심적인 테이블로 출력)
    mode_str = "[DRY-RUN 모드 (파일 수정 없음)]" if dry_run else f"[APPLY 모드 (저장: {output_path})]"
    total_words_dropped = rule1_dropped_count + rule2_dropped_count + rule3_dropped_count + rule4_dropped_count
    
    summary_lines = [
        "=" * 75,
        f"📊 FastAlias CSV 정제 결과 요약 {mode_str}",
        "=" * 75,
        f"⏱️ 소요 시간: {elapsed:.2f}초",
        f"📁 입력 파일: {input_path}",
        f"📁 출력 파일: {output_path if not dry_run else '(Dry-run: 생성 안 됨)'}",
        "-" * 75,
        "📌 [행(Row) 처리 통계]",
        f"  • 총 검사 데이터 행: {total_rows:,} 행",
        f"  • 최종 유지 행: {valid_rows:,} 행 ({valid_rows/max(1, total_rows)*100:.1f}%)",
        f"  • [키워드 규칙] 단일 영문 키워드 행 제외: {kw_dropped_count:,} 행 ({kw_dropped_count/max(1, total_rows)*100:.1f}%)",
        f"  • 빈 단어 행 (단어 소진 제외): {empty_rows_dropped:,} 행 ({empty_rows_dropped/max(1, total_rows)*100:.1f}%)",
        "-" * 75,
        "📌 [유효 행 내 단어(Words) 필터링 통계]",
        f"  • 총 검사 단어 수: {total_words:,} 개",
        f"  • 유지된 단어 수: {kept_words_count:,} 개 ({kept_words_count/max(1, total_words)*100:.1f}%)",
        f"  • [규칙 1] 연도 표기 제외: {rule1_dropped_count:,} 개 ({rule1_dropped_count/max(1, total_words)*100:.1f}%)",
        f"  • [규칙 2] 1글자 단어 제외: {rule2_dropped_count:,} 개 ({rule2_dropped_count/max(1, total_words)*100:.1f}%)",
        f"  • [규칙 3] 10자 초과 일어 제외: {rule3_dropped_count:,} 개 ({rule3_dropped_count/max(1, total_words)*100:.1f}%)",
        f"  • [규칙 4] 타입3 무공백 한글/영문 1단어 제외: {rule4_dropped_count:,} 개 ({rule4_dropped_count/max(1, total_words)*100:.1f}%)",
        f"  • 총 제외된 단어 수: {total_words_dropped:,} 개 ({total_words_dropped/max(1, total_words)*100:.1f}%)",
        "=" * 75,
    ]

    print("\n".join(summary_lines))

    # 규칙 4 샘플 목록 출력 (상위 25개)
    print(f"\n🔍 [규칙 4 (타입3 무공백 한글/영문 1단어) 제외 단어 샘플 - 상위 {min(sample_limit, len(sample_rule4))}개 (총 {len(sample_rule4):,}종류)]:")
    print("  " + ", ".join(sorted(sample_rule4)[:sample_limit]))

    # 키워드별 규칙 4 제외 예시 (상위 10개)
    print(f"\n🔍 [규칙 4 키워드별 제외 내역 예시 - 상위 10개 키워드]:")
    for kw in list(rule4_by_keyword.keys())[:10]:
        print(f"  • {kw} ➡️ 제외된 단어: {', '.join(rule4_by_keyword[kw])}")

    # 리포트 파일 저장 (전체 1,000+개 전체 목록을 사용자가 언제든 조회할 수 있도록 파일에 영구 기록)
    if report_path:
        os.makedirs(os.path.dirname(os.path.abspath(report_path)), exist_ok=True)
        with open(report_path, "w", encoding="utf-8-sig") as rep_f:
            rep_f.write("\n".join(summary_lines) + "\n\n")
            rep_f.write(f"=== [규칙 4] 타입3 키워드별 제외된 단어 전체 목록 (총 {len(rule4_by_keyword)}개 키워드, {rule4_dropped_count}개 단어) ===\n")
            for kw in sorted(rule4_by_keyword.keys()):
                rep_f.write(f"[{kw}]\n  제외 단어: {', '.join(rule4_by_keyword[kw])}\n")
            rep_f.write(f"\n=== [규칙 4] 제외 단어 전체 고유 목록 (총 {len(sample_rule4)}개) ===\n")
            rep_f.write("\n".join(sorted(sample_rule4)) + "\n\n")
            if sample_kw_dropped:
                rep_f.write(f"=== 단일 영문 키워드로 제외된 행 목록 (총 {len(sample_kw_dropped)}개) ===\n")
                rep_f.write("\n".join(sorted(sample_kw_dropped)) + "\n\n")
            if sample_rule1:
                rep_f.write(f"=== 규칙 1 제외 단어 (총 {len(sample_rule1)}개) ===\n")
                rep_f.write("\n".join(sorted(sample_rule1)) + "\n\n")
            if sample_rule2:
                rep_f.write(f"=== 규칙 2 제외 단어 (총 {len(sample_rule2)}개) ===\n")
                rep_f.write("\n".join(sorted(sample_rule2)) + "\n\n")
            if sample_rule3:
                rep_f.write(f"=== 규칙 3 제외 단어 (총 {len(sample_rule3)}개) ===\n")
                rep_f.write("\n".join(sorted(sample_rule3)) + "\n\n")
        print(f"\n[REPORT] 상세 전체 목록 리포트가 저장되었습니다: {report_path}")


def main():
    parser = argparse.ArgumentParser(description="FastAlias CSV Words Filter & Cleaner")
    parser.add_argument(
        "--input", "-i",
        default=os.path.join("tests", "FastAlias_Cleaned_Inverted_260822.csv"),
        help="Input CSV file path"
    )
    parser.add_argument(
        "--output", "-o",
        default=os.path.join("tests", "FastAlias_Cleaned_Inverted_260822.csv"),
        help="Output CSV file path"
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        default=False,
        help="Perform a dry run without modifying files"
    )
    parser.add_argument(
        "--apply",
        action="store_true",
        default=False,
        help="Apply changes and generate output CSV"
    )
    parser.add_argument(
        "--sample-limit",
        type=int,
        default=25,
        help="Max number of sample words to print to console (default: 25)"
    )
    parser.add_argument(
        "--report",
        default=os.path.join("tests", "dry_run_report.txt"),
        help="Path to save full dry-run summary report"
    )

    args = parser.parse_args()

    # --apply가 명시되지 않으면 기본적으로 dry-run으로 동작
    is_dry_run = not args.apply

    process_csv(
        input_path=args.input,
        output_path=args.output,
        dry_run=is_dry_run,
        sample_limit=args.sample_limit,
        report_path=args.report
    )


if __name__ == "__main__":
    main()
