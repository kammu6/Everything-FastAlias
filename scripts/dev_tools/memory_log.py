# description: "Safely appends anti-pattern knowledge logs to MEMORY.md with quote escaping defenses, stdin pipe support, validation guards, and JSON/CLI input support"

"""
Automated append-only script for MEMORY.md.
Safely logs anti-pattern focused knowledge entries with trailing newline trimming,
shell quote escaping defenses, stdin pipeline support, and schema validation.
"""

import sys
import os
import json
import datetime


def get_memory_file_path() -> str:
    # 1. 현재 작업 디렉토리 기준
    p1 = os.path.join("docs", "memories", "MEMORY.md")
    if os.path.exists(p1):
        return p1
    # 2. 스크립트 위치 기준 상위 2단계 (scripts/dev_tools/ -> 프로젝트 루트)
    script_dir = os.path.dirname(os.path.abspath(__file__))
    p2 = os.path.abspath(os.path.join(script_dir, "..", "..", "docs", "memories", "MEMORY.md"))
    if os.path.exists(p2):
        return p2
    # 3. 스크립트 위치 기준 상위 1단계 (scripts/ -> 프로젝트 루트)
    p3 = os.path.abspath(os.path.join(script_dir, "..", "docs", "memories", "MEMORY.md"))
    if os.path.exists(p3):
        return p3
    return p1


def validate_entry(title: str, context: str, solution: str, anti_patterns: str) -> tuple[bool, str]:
    """
    내용의 유효성을 검증하고 쉘 이스케이프 단절(예: 문장 끝이 단독 역슬래시로 끊김)을 탐지합니다.
    """
    if not title or len(title.strip()) < 3:
        return False, "Title must be at least 3 characters long."
    if not context or len(context.strip()) < 5:
        return False, "Context (🎯) must be at least 5 characters long."
    if not solution or len(solution.strip()) < 5:
        return False, "Solution (✅) must be at least 5 characters long."
    if not anti_patterns or len(anti_patterns.strip()) < 5:
        return False, "Anti-patterns (❌) must be at least 5 characters long."

    # 단독 역슬래시로 문장이 비정상 종료되었는지 감지 (Shell Quote 파싱 누락 증상)
    if solution.strip().endswith("\\"):
        return False, "Solution string ends with an unescaped trailing backslash, indicating shell quote truncation."
    if context.strip().endswith("\\") or anti_patterns.strip().endswith("\\"):
        return False, "Input string ends with an unescaped trailing backslash, indicating shell quote truncation."

    return True, ""


def append_log(title: str, context: str, solution: str, anti_patterns: str) -> bool:
    """
    MEMORY.md 최하단의 불필요한 빈 줄을 스마트하게 정리하고,
    일관된 간격으로 새로운 지식 자산화 로그를 안전하게 추가합니다.
    """
    is_valid, err_msg = validate_entry(title, context, solution, anti_patterns)
    if not is_valid:
        print(f"[-] Validation Error: {err_msg}")
        return False

    memory_path = get_memory_file_path()
    if not os.path.exists(memory_path):
        print(f"[-] Error: {memory_path} does not exist.")
        return False

    today_str = datetime.datetime.now().strftime("%Y-%m-%d")

    new_entry = f"""### {today_str} ({title.strip()})

- 🎯
  - {context.strip()}

- ✅
  - {solution.strip()}

- ❌
  - {anti_patterns.strip()}"""

    try:
        with open(memory_path, "r", encoding="utf-8") as f:
            content = f.read()

        trimmed_content = content.rstrip()

        if trimmed_content:
            final_content = trimmed_content + "\n\n" + new_entry + "\n"
        else:
            final_content = new_entry + "\n"

        with open(memory_path, "w", encoding="utf-8") as f:
            f.write(final_content)

        print(f"[SUCCESS] Safely appended new memory log to {memory_path}")
        return True
    except Exception as e:
        print(f"[-] Failed to append memory log: {e}")
        return False


def main():
    # Windows 표준 입출력 UTF-8 인코딩 보장
    if hasattr(sys.stdin, "reconfigure"):
        try:
            sys.stdin.reconfigure(encoding="utf-8")
        except Exception:
            pass

    # 1. 표준 입력(stdin) 파이프라인 수신: PowerShell Here-String (@'...'@ | python memory_log.py --stdin)
    #    플래그가 --stdin/-s 이거나 인자 없이 파이프로 데이터가 인입된 경우
    has_stdin_flag = len(sys.argv) >= 2 and sys.argv[1] in ("--stdin", "-s", "--json-stdin")
    is_piped = not sys.stdin.isatty() if hasattr(sys.stdin, "isatty") else False

    if has_stdin_flag or (len(sys.argv) == 1 and is_piped):
        try:
            raw_input = sys.stdin.read().strip()
            if not raw_input:
                print("[-] Error: Empty standard input received.")
                sys.exit(1)
            data = json.loads(raw_input)
            success = append_log(
                data.get("title", ""),
                data.get("context", ""),
                data.get("solution", ""),
                data.get("anti_patterns", "")
            )
            sys.exit(0 if success else 1)
        except json.JSONDecodeError as jde:
            print(f"[-] JSON Parse Error from stdin: {jde}")
            sys.exit(1)
        except Exception as e:
            print(f"[-] Failed to process stdin: {e}")
            sys.exit(1)

    # 2. JSON 파일 경로가 인자로 전달된 경우: python memory_log.py --file <path.json>
    if len(sys.argv) == 3 and sys.argv[1] in ("--file", "-f"):
        json_path = sys.argv[2]
        if not os.path.exists(json_path):
            print(f"[-] JSON file not found: {json_path}")
            sys.exit(1)
        with open(json_path, "r", encoding="utf-8") as f:
            data = json.load(f)
        success = append_log(
            data.get("title", ""),
            data.get("context", ""),
            data.get("solution", ""),
            data.get("anti_patterns", "")
        )
        sys.exit(0 if success else 1)

    # 3. JSON 문자열이 단일 인자로 전달된 경우: python memory_log.py --json '{"title":...}'
    if len(sys.argv) == 3 and sys.argv[1] in ("--json", "-j"):
        try:
            data = json.loads(sys.argv[2])
            success = append_log(
                data.get("title", ""),
                data.get("context", ""),
                data.get("solution", ""),
                data.get("anti_patterns", "")
            )
            sys.exit(0 if success else 1)
        except Exception as e:
            print(f"[-] Failed to parse JSON argument: {e}")
            sys.exit(1)

    # 4. 일반 4개 인자 CLI 전달 (따옴표 분할 복원 가드 포함)
    if len(sys.argv) >= 5:
        title = sys.argv[1]
        context = sys.argv[2]
        solution = sys.argv[3]
        anti_patterns = sys.argv[4]

        # 만약 인자가 5개 이상으로 쪼개졌다면 Shell Quote 파싱 결함으로 판단하고 뒷부분 자동 결합 복구 시도
        if len(sys.argv) > 5:
            print(f"[!] Warning: Shell argument split detected ({len(sys.argv)} args). Merging remaining tokens...")
            anti_patterns = " ".join(sys.argv[4:])

        success = append_log(title, context, solution, anti_patterns)
        sys.exit(0 if success else 1)

    print("Usage Options:")
    print("  1) PowerShell Here-String stdin mode (1-Step Recommended for special chars & quotes):")
    print("     @'")
    print('     {"title":"...", "context":"...", "solution":"...", "anti_patterns":"..."}')
    print("     '@ | python scripts/dev_tools/memory_log.py --stdin")
    print("  2) Direct 4-argument CLI mode:")
    print("     python scripts/dev_tools/memory_log.py <title> <context> <solution> <anti_patterns>")
    print("  3) JSON file payload mode:")
    print("     python scripts/dev_tools/memory_log.py --file <data.json>")
    print("  4) JSON inline string mode:")
    print("     python scripts/dev_tools/memory_log.py --json '{\"title\":\"...\", \"context\":\"...\", \"solution\":\"...\", \"anti_patterns\":\"...\"}'")
    sys.exit(1)


if __name__ == "__main__":
    main()
