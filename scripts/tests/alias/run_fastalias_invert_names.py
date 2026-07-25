# description: "Transforms 2-word English keywords and words from Last-First to First-Last with capitalized casing (e.g. omori Shizuka -> Shizuka Omori)."

import argparse
import csv
import os
import re
import sys
import time
from typing import Tuple, List, Dict


def format_part(part: str) -> str:
    """
    단어의 앞글자를 대문자로 변환합니다 (하이픈 연결 단어도 각각 대문자화).
    예: 'omori' -> 'Omori', 'jean-pierre' -> 'Jean-Pierre'
    """
    if not part:
        return ""
    if "-" in part:
        return "-".join(p.capitalize() for p in part.split("-"))
    return part.capitalize()


def is_two_word_english_keyword(kw: str) -> bool:
    """
    '영문(공백)영문' 형태의 2단어 키워드인지 검사합니다.
    """
    kw_clean = kw.strip()
    if not kw_clean or kw_clean.startswith("#"):
        return False
    
    parts = kw_clean.split()
    if len(parts) == 2 and all(re.match(r'^[A-Za-z\-]+$', p) for p in parts):
        return True
    return False


def transform_name_row(keyword: str, raw_words: str) -> Tuple[str, str, bool]:
    """
    행 데이터를 변환합니다.
    - 대상: 영문(공백)영문 키워드
    - 변환:
        1. Keyword: 성 이름 -> 이름 성 (각 앞글자 대문자화)
        2. Words 1번째 토큰: 이름 성
        3. Words 2번째 토큰: 성 이름
        4. Words 3번째 이후: 기존 나머지 동의어(한글, 일어 등) 보존 (중복 제거)
    Returns:
        (new_keyword, new_words_str, was_transformed)
    """
    kw_clean = keyword.strip()
    words = [t.strip() for t in raw_words.split(";") if t.strip()]

    if is_two_word_english_keyword(kw_clean):
        parts = kw_clean.split()
        last_name = format_part(parts[0])   # 성 (Last name)
        first_name = format_part(parts[1])  # 이름 (First name)

        new_keyword = f"{first_name} {last_name}"
        token1 = f"{first_name} {last_name}"  # Words 1번째: 이름 성
        token2 = f"{last_name} {first_name}"  # Words 2번째: 성 이름

        new_words_list = [token1, token2]
        seen = {token1.lower(), token2.lower()}

        # 3번째 이후: 기존 동의어 중 1, 2번째와 중복되지 않는 것들 보존
        for w in words:
            w_clean = w.strip()
            if not w_clean:
                continue
            if w_clean.lower() not in seen:
                seen.add(w_clean.lower())
                new_words_list.append(w_clean)

        return new_keyword, ";".join(new_words_list), True
    else:
        # 비영어 또는 2단어가 아닌 키워드는 원본 그대로 유지
        return kw_clean, ";".join(words), False


def process_csv(
    input_path: str,
    output_path: str,
    dry_run: bool = True,
    sample_limit: int = 15
):
    if not os.path.exists(input_path):
        print(f"[ERROR] Input file not found: {input_path}")
        sys.exit(1)

    start_time = time.time()
    total_rows = 0
    transformed_count = 0
    kept_count = 0

    samples: List[Dict[str, str]] = []

    temp_output_path = output_path + ".tmp" if not dry_run else None
    out_file = None
    writer = None

    if not dry_run:
        os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
        # Windows/Excel 및 .NET 호환성을 위해 UTF-8 with BOM(utf-8-sig)으로 임시 파일에 저장
        out_file = open(temp_output_path, "w", encoding="utf-8-sig", newline="")
        writer = csv.writer(out_file, delimiter=",", quoting=csv.QUOTE_MINIMAL)

    try:
        with open(input_path, "r", encoding="utf-8-sig", errors="replace") as in_f:
            reader = csv.reader(in_f)

            is_first = True
            for row in reader:
                if not row:
                    continue

                if is_first:
                    is_first = False
                    if len(row) >= 1 and ("keyword" in row[0].lower() or "원본" in row[0]):
                        if not dry_run and writer:
                            writer.writerow(["Keyword", "Words"])
                        continue

                total_rows += 1
                kw = row[0].strip() if len(row) > 0 else ""
                raw_words = row[1] if len(row) > 1 else ""

                if not kw:
                    continue

                new_kw, new_words, was_transformed = transform_name_row(kw, raw_words)

                if was_transformed:
                    transformed_count += 1
                    if len(samples) < sample_limit:
                        samples.append({
                            "orig_kw": kw,
                            "orig_words": raw_words,
                            "new_kw": new_kw,
                            "new_words": new_words
                        })
                else:
                    kept_count += 1

                if not dry_run and writer:
                    writer.writerow([new_kw, new_words])

    finally:
        if out_file:
            out_file.close()
        if not dry_run and temp_output_path and os.path.exists(temp_output_path):
            os.replace(temp_output_path, output_path)

    elapsed = time.time() - start_time

    mode_str = "[DRY-RUN 모드 (파일 수정 없음)]" if dry_run else f"[APPLY 모드 (저장: {output_path})]"

    print("=" * 75)
    print(f"📊 FastAlias 영문 이름 도치(성 이름 -> 이름 성) 및 대문자화 결과 {mode_str}")
    print("=" * 75)
    print(f"⏱️ 소요 시간: {elapsed:.2f}초")
    print(f"📁 입력 파일: {input_path}")
    print(f"📁 출력 파일: {output_path if not dry_run else '(Dry-run: 생성 안 됨)'}")
    print("-" * 75)
    print("📌 [행(Row) 변환 통계]")
    print(f"  • 총 처리 데이터 행: {total_rows:,} 행")
    print(f"  • 영문 이름 도치 변환 완료: {transformed_count:,} 행 ({transformed_count/max(1, total_rows)*100:.1f}%)")
    print(f"  • 비영어/기타 키워드 유지: {kept_count:,} 행 ({kept_count/max(1, total_rows)*100:.1f}%)")
    print("=" * 75)

    print(f"\n🔍 [AS-IS ➡️ TO-BE 변환 샘플 (상위 {len(samples)}개)]:\n")
    for i, s in enumerate(samples, 1):
        print(f"[{i}] AS-IS:  {s['orig_kw']}")
        print(f"    Words:  {s['orig_words'][:75]}...")
        print(f"    TO-BE:  {s['new_kw']}")
        print(f"    Words:  {s['new_words'][:75]}...\n")


def main():
    parser = argparse.ArgumentParser(description="Inverts Last-First English names to First-Last with capitalization")
    parser.add_argument(
        "--input", "-i",
        default=os.path.join("tests", "FastAlias_Cleaned_260822.csv"),
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
        help="Perform a dry run without writing files"
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
        default=10,
        help="Max number of sample transformations to print (default: 10)"
    )

    args = parser.parse_args()
    is_dry_run = not args.apply

    process_csv(
        input_path=args.input,
        output_path=args.output,
        dry_run=is_dry_run,
        sample_limit=args.sample_limit
    )


if __name__ == "__main__":
    main()
