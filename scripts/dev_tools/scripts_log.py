# description: "Universal script governance CLI: audit, in-place annotations, 3-tier promotion, and scripts_guide.md synchronization"

import os
import sys
import re
import json
import shutil
import argparse
from datetime import datetime

# Exclusion rules for noise, binaries, and temporary artifacts
EXCLUDE_DIRS = {
    ".git",
    "node_modules",
    ".gemini",
    ".agents",
    ".history",
    ".vscode",
    ".idea",
    "__pycache__",
    ".pytest_cache",
    "temp",
    "dist",
    "build",
    "out",
    "coverage",
    ".venv",
    "venv"
}

EXCLUDE_EXTS = {
    ".bak",
    ".old",
    ".tmp",
    ".orig",
    ".backup",
    ".pyc",
    ".pyo",
    ".pyd",
    ".log",
    ".exe",
    ".dll",
    ".pdb",
    ".obj",
    ".bin",
    ".gitkeep",
    ".map",
    ".DS_Store",
    "thumbs.db"
}


def is_excluded_file(file_name: str) -> bool:
    lower = file_name.lower()
    if lower in EXCLUDE_DIRS:
        return True
    for ext in EXCLUDE_EXTS:
        if lower.endswith(ext):
            return True
    if lower.startswith("temp_") or lower.startswith("output_") or lower.startswith("task-"):
        return True
    return False


def clean_path(path_str: str) -> str:
    if not path_str:
        return ""
    cleaned = path_str.strip()
    cleaned = re.sub(r'^["]+|["]+$', '', cleaned)
    cleaned = re.sub(r"^[']+|[']+$", '', cleaned)
    return cleaned.strip()


def to_file_uri(abs_path: str) -> str:
    normalized = os.path.abspath(clean_path(abs_path)).replace("\\", "/")
    return f"file:///{normalized}"


def get_temp_dir() -> str:
    local_app_data = os.environ.get("LOCALAPPDATA")
    if os.name == "nt" and local_app_data:
        base_temp = os.path.join(local_app_data, "Temp")
    else:
        import tempfile
        base_temp = tempfile.gettempdir()
    target_dir = os.path.join(base_temp, "scripts_log")
    os.makedirs(target_dir, exist_ok=True)
    return target_dir


def cleanup_temp_files(temp_dir: str, max_age_seconds: int = 3600):
    try:
        if not os.path.exists(temp_dir):
            return
        now = datetime.now().timestamp()
        for item in os.listdir(temp_dir):
            if item.startswith("scripts_log_") and item.endswith(".md"):
                file_path = os.path.join(temp_dir, item)
                stat = os.stat(file_path)
                if now - stat.st_mtime > max_age_seconds:
                    try:
                        os.remove(file_path)
                    except Exception:
                        pass
    except Exception:
        pass


def resolve_comment_prefix(ext: str, manual_prefix: str = None) -> str:
    if manual_prefix:
        return manual_prefix.strip()
    ext_lower = ext.lower()
    if ext_lower in [".py", ".sh", ".bash", ".zsh", ".ps1", ".psm1", ".psd1", ".yaml", ".yml", ".toml", ".rb", ".pl", ".r", ".dockerfile"]:
        return "#"
    elif ext_lower in [".js", ".mjs", ".cjs", ".ts", ".mts", ".cts", ".jsx", ".tsx", ".c", ".cpp", ".h", ".hpp", ".cs", ".java", ".go", ".rs", ".swift", ".kt", ".kts", ".scala", ".php", ".dart"]:
        return "//"
    elif ext_lower in [".lua", ".sql", ".hs", ".vhd", ".vhdl"]:
        return "--"
    elif ext_lower in [".bat", ".cmd"]:
        return "::"
    elif ext_lower in [".html", ".xml", ".svg"]:
        return "<!--"
    return "#"


def extract_script_comment(file_path: str, header_keyword: str = "description") -> str | None:
    try:
        with open(file_path, "r", encoding="utf-8", errors="replace") as f:
            lines = [f.readline() for _ in range(15)]
        
        # 1. Explicit header search (e.g. description:, summary:, desc:)
        pattern = re.compile(
            r'^(?:#|//|--|@REM|REM|::|;|%|!|/\*|<!--|\(\*|{-|\'\'\'|""")\s*' + re.escape(header_keyword) + r':\s*["\']?(.*?)["\']?(?:\s*\*\/|\s*-->|\s*\*\)|\s*-}|\s*\'\'\'|\s*""")?$',
            re.IGNORECASE
        )
        for line in lines:
            trimmed = line.strip()
            match = pattern.match(trimmed)
            if match and match.group(1):
                return match.group(1).strip()

        # 2. Markdown frontmatter description
        if file_path.lower().endswith(".md"):
            joined = "".join(lines)
            fm_match = re.search(r'^---\r?\n([\s\S]*?)\r?\n---', joined)
            if fm_match:
                for fm_line in fm_match.group(1).splitlines():
                    if ":" in fm_line:
                        k, v = fm_line.split(":", 1)
                        if k.strip().lower() == header_keyword.lower():
                            return v.strip().strip('"\'')

        # 3. Fallback: First meaningful comment line
        comment_prefix_pattern = re.compile(r'^(?:#|//|--|@REM|REM|::|;|%|!|/\*|<!--|\(\*|{-)')
        for line in lines:
            trimmed = line.strip()
            if comment_prefix_pattern.match(trimmed):
                clean_comment = re.sub(r'^[#/\-@REM;%!:\*<({"\']+|\*/|-->|\*\)|-}|\'\'\'|"""', '', trimmed).strip()
                if clean_comment and "-*-" not in clean_comment and "!/usr/bin" not in clean_comment:
                    return clean_comment
        return None
    except Exception:
        return None


def parse_existing_guide_annotations(guide_path: str) -> dict[str, str]:
    """Parses existing manual annotations from scripts_guide.md for files like .json, .md, or unannotated scripts."""
    annotations = {}
    if not os.path.exists(guide_path):
        return annotations

    try:
        with open(guide_path, "r", encoding="utf-8") as f:
            content = f.read()

        match = re.search(r'<!-- START_LIST -->([\s\S]*?)<!-- END_LIST -->', content)
        if not match:
            return annotations

        list_block = match.group(1)
        for line in list_block.splitlines():
            trimmed = line.strip()
            if not trimmed.startswith("- ["):
                continue
            # Format: - [basename](file:///...): `badge` description  OR  - [basename](file:///...): description
            line_match = re.match(r'^-\s*\[(.*?)\]\((.*?)\):\s*(?:`.*?`\s*)?(.*)$', trimmed)
            if line_match:
                basename = line_match.group(1).strip()
                desc = line_match.group(3).strip()
                if desc and desc != "(No description)" and desc != "(설명 없음)":
                    annotations[basename] = desc
    except Exception:
        pass
    return annotations


def evaluate_description(desc: str | None) -> tuple[str, str]:
    if not desc or not isinstance(desc, str):
        return ("MISSING", "(No description)")
    trimmed = desc.strip()
    if not trimmed:
        return ("MISSING", "(No description)")
    lower = trimmed.lower()
    invalid_keywords = {"-", ".", "none", "null", "todo", "tbd", "fixme", "description", "(no description)", "(설명 없음)"}
    if lower in invalid_keywords or lower.startswith("todo:") or lower.startswith("description:"):
        return ("INVALID", trimmed or "(Invalid)")
    return ("OK", trimmed)


def get_safety_badge(category: str, file_name: str) -> str:
    """Returns safety badges strictly for scripts/tests/ directory."""
    if category.startswith("tests"):
        base = os.path.basename(file_name).lower()
        if base.startswith("chk_"):
            return "`🟢 [Read-Only]`"
        elif base.startswith("run_"):
            return "`🟡 [Mutating]`"
        return "`⚪ [Sandbox]`"
    return ""


def inject_script_comment(file_path: str, new_desc: str, manual_prefix: str = None, header_keyword: str = "description") -> str:
    with open(file_path, "r", encoding="utf-8", errors="replace") as f:
        content = f.read()

    ext = os.path.splitext(file_path)[1].lower()
    prefix = resolve_comment_prefix(ext, manual_prefix)
    
    if prefix == "<!--":
        new_comment_line = f'<!-- {header_keyword}: "{new_desc}" -->'
    else:
        new_comment_line = f'{prefix} {header_keyword}: "{new_desc}"'

    lines = content.splitlines()
    line_ending = "\r\n" if "\r\n" in content else "\n"

    insert_idx = 0
    # Shebang preservation
    if lines and lines[0].startswith("#!"):
        insert_idx = 1
    # Python encoding header preservation
    if len(lines) > insert_idx and re.match(r'^#.*?coding[:=]\s*[-\w.]+', lines[insert_idx], re.IGNORECASE):
        insert_idx += 1

    # Search for existing header within top 15 lines
    desc_pattern = re.compile(r'^(?:#|//|--|@REM|REM|::|;|%|!|/\*|<!--|\(\*|{-|\'\'\'|""")\s*' + re.escape(header_keyword) + r':\s*', re.IGNORECASE)
    existing_idx = -1
    for i in range(min(len(lines), 15)):
        if desc_pattern.match(lines[i].strip()):
            existing_idx = i
            break

    if existing_idx != -1:
        lines[existing_idx] = new_comment_line
        action = "replaced"
    else:
        lines.insert(insert_idx, new_comment_line)
        action = "injected"

    updated_content = line_ending.join(lines)
    if not updated_content.endswith(line_ending):
        updated_content += line_ending

    with open(file_path, "w", encoding="utf-8") as f:
        f.write(updated_content)

    return action


def read_stdin_safely() -> str:
    if sys.stdin.isatty():
        print("\n[ERROR] --stdin option was specified but no piped input was provided.", file=sys.stderr)
        print("[USAGE] @'\n{\n  \"scripts/tools/foo.py\": \"Tool description\"\n}\n'@ | python scripts_log.py annotate --stdin\n", file=sys.stderr)
        sys.exit(1)
    try:
        return sys.stdin.read().strip()
    except Exception as e:
        print(f"\n[ERROR] Failed to read from stdin: {e}", file=sys.stderr)
        sys.exit(1)


def find_workspace_root(start_path: str = None) -> str:
    curr = os.path.abspath(start_path or os.getcwd())
    if os.path.isfile(curr):
        curr = os.path.dirname(curr)
    
    while curr:
        if os.path.exists(os.path.join(curr, "scripts")) or os.path.exists(os.path.join(curr, "docs", "memories")):
            return curr
        parent = os.path.dirname(curr)
        if parent == curr:
            break
        curr = parent
    return os.path.abspath(start_path or os.getcwd())


def scan_workspace_scripts(workspace_root: str, header_keyword: str = "description"):
    scripts_dir = os.path.join(workspace_root, "scripts")
    if not os.path.exists(scripts_dir):
        return []

    guide_path = os.path.join(workspace_root, "docs", "memories", "scripts_guide.md")
    guide_annotations = parse_existing_guide_annotations(guide_path)

    results = []
    for root, dirs, files in os.walk(scripts_dir):
        dirs[:] = [d for d in dirs if d.lower() not in EXCLUDE_DIRS]

        rel_root = os.path.relpath(root, scripts_dir).replace("\\", "/")
        for file in files:
            if is_excluded_file(file):
                continue
            full_path = os.path.join(root, file)
            rel_from_scripts = os.path.relpath(full_path, scripts_dir).replace("\\", "/")
            rel_from_ws = os.path.relpath(full_path, workspace_root).replace("\\", "/")
            
            parts = rel_from_scripts.split("/")
            if len(parts) == 1:
                category = "root"
                group_display = "scripts/ (Uncategorized Root)"
            elif parts[0] == "tests" and len(parts) == 2:
                category = "tests_flat"
                group_display = "tests/ (Uncategorized Flat)"
            else:
                category = "/".join(parts[:-1])
                group_display = f"{category}/"

            in_file_desc = extract_script_comment(full_path, header_keyword)
            # Fallback to guide manual annotation (useful for .json and existing files)
            desc = in_file_desc if in_file_desc else guide_annotations.get(file)

            results.append({
                "basename": file,
                "full_path": full_path,
                "rel_scripts": rel_from_scripts,
                "rel_workspace": rel_from_ws,
                "category": category,
                "group_display": group_display,
                "desc": desc,
                "badge": get_safety_badge(category, file)
            })

    def sort_key(item):
        cat = item["category"]
        if cat == "dev_tools":
            return (0, cat, item["basename"])
        elif cat.startswith("tools"):
            return (1, cat, item["basename"])
        elif cat.startswith("tests"):
            return (2, cat, item["basename"])
        else:
            return (3, cat, item["basename"])

    results.sort(key=sort_key)
    return results


def save_audit_report(workspace_root: str, scripts: list, summary: dict, custom_output: str = None) -> tuple[str, str]:
    now_str = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    ws_name = os.path.basename(workspace_root)

    report = f"# 🔍 Scripts Audit Report: {ws_name}\n\n"
    report += f"- **Workspace Root**: [{ws_name}]({to_file_uri(workspace_root)})\n"
    report += f"- **Generated At**: {now_str}\n"
    report += f"- **Summary**: Total {len(scripts)} | OK: {summary['ok_count']} | Missing: {summary['missing_count']} | Invalid: {summary['invalid_count']} | Violations: {summary['violation_count']}\n\n"

    if summary["violations"]:
        report += f"## ⚠️ Architecture Violations ({len(summary['violations'])})\n\n"
        for v in summary["violations"]:
            report += f"- [{v['rel_workspace']}]({to_file_uri(v['full_path'])}): `{v['reason']}`\n"
        report += "\n"

    if summary["missing_items"]:
        report += f"## ❌ Missing Descriptions ({len(summary['missing_items'])})\n\n"
        for item in summary["missing_items"]:
            report += f"- [{item['rel_workspace']}]({to_file_uri(item['full_path'])})\n"
        report += "\n"

    if summary["invalid_items"]:
        report += f"## ⚠️ Invalid Descriptions ({len(summary['invalid_items'])})\n\n"
        for item in summary["invalid_items"]:
            report += f"- [{item['res']['rel_workspace']}]({to_file_uri(item['res']['full_path'])}): \"{item['text']}\"\n"
        report += "\n"

    report += "## 📜 Complete Script Inspection Table\n\n"
    report += "| Status | Script File | Category | Safety | Description |\n"
    report += "| :--- | :--- | :--- | :--- | :--- |\n"

    for res in scripts:
        eval_res = evaluate_description(res["desc"])
        status_badge = "✅ `[OK]`" if eval_res[0] == "OK" else ("❌ `[MISSING]`" if eval_res[0] == "MISSING" else "⚠️ `[INVALID]`")
        clean_desc = (eval_res[1] or "").replace("|", "\\|")
        report += f"| {status_badge} | [{res['basename']}]({to_file_uri(res['full_path'])} | `{res['group_display']}` | {res['badge'] or '-'} | {clean_desc} |\n"

    report += "\n"

    temp_dir = get_temp_dir()
    if custom_output:
        dest_path = os.path.abspath(clean_path(custom_output))
    else:
        dest_path = os.path.join(temp_dir, f"scripts_log_audit_{int(datetime.now().timestamp() * 1000)}.md")

    os.makedirs(os.path.dirname(dest_path), exist_ok=True)
    with open(dest_path, "w", encoding="utf-8") as f:
        f.write(report)

    if not custom_output:
        cleanup_temp_files(temp_dir)

    return dest_path, to_file_uri(dest_path)


# ------------------------------------------------------------------------------
# Subcommand 1: audit
# ------------------------------------------------------------------------------
def cmd_audit(args):
    target_ws = find_workspace_root(args.target)
    header_kw = getattr(args, "header", None) or "description"
    scripts = scan_workspace_scripts(target_ws, header_kw)

    ok_count = 0
    missing_count = 0
    invalid_count = 0
    missing_items = []
    invalid_items = []
    violations = []

    for s in scripts:
        eval_res = evaluate_description(s["desc"])
        if eval_res[0] == "OK":
            ok_count += 1
        elif eval_res[0] == "MISSING":
            missing_count += 1
            missing_items.append(s)
        else:
            invalid_count += 1
            invalid_items.append({"res": s, "text": eval_res[1]})

        # Architecture violations
        if s["category"] == "root":
            violations.append({"res": s, "rel_workspace": s["rel_workspace"], "full_path": s["full_path"], "reason": "Located in scripts/ root -> move to dev_tools/, tools/{domain}/, or tests/{domain}/"})
        elif s["category"] == "tests_flat":
            violations.append({"res": s, "rel_workspace": s["rel_workspace"], "full_path": s["full_path"], "reason": "Located in tests/ root -> isolate under tests/{domain}/"})

    summary = {
        "ok_count": ok_count,
        "missing_count": missing_count,
        "invalid_count": invalid_count,
        "violation_count": len(violations),
        "missing_items": missing_items,
        "invalid_items": invalid_items,
        "violations": violations
    }

    dest_path, report_uri = save_audit_report(target_ws, scripts, summary, args.output)

    terminal_lines = []
    scripts_dir = os.path.join(target_ws, "scripts")

    if args.verbose:
        terminal_lines.append(f"\n🔍 [AUDIT] Scanning scripts at: {scripts_dir}")
        terminal_lines.append("=" * 100)
        terminal_lines.append(f"{'Status':<10} | {'Safety':<18} | {'Relative Path':<40} | Description")
        terminal_lines.append("-" * 100)
        for s in scripts:
            eval_res = evaluate_description(s["desc"])
            status_tag = f"[{eval_res[0]}]"
            safety_str = s["badge"] if s["badge"] else "-"
            terminal_lines.append(f"{status_tag:<10} | {safety_str:<18} | {s['rel_scripts']:<40} | {eval_res[1]}")
        terminal_lines.append("=" * 100)
        terminal_lines.append(f"Summary: Total {len(scripts)} | OK: {ok_count} | Missing: {missing_count} | Invalid: {invalid_count} | Violations: {len(violations)}\n")
    else:
        terminal_lines.append(f"\n🔍 [AUDIT] {scripts_dir}")

        if len(violations) == 0 and missing_count == 0 and invalid_count == 0:
            terminal_lines.append(f"[SUCCESS] All {len(scripts)} script(s) comply with architecture and description rules.\n")
        else:
            if violations:
                terminal_lines.append(f"\n### Architecture Violations ({len(violations)})")
                for v in violations:
                    terminal_lines.append(f"- {v['rel_workspace']}: {v['reason']}")
            if missing_items:
                terminal_lines.append(f"\n### Missing Description ({len(missing_items)})")
                for m in missing_items:
                    terminal_lines.append(f"- {m['rel_workspace']}")
            if invalid_items:
                terminal_lines.append(f"\n### Invalid Description ({len(invalid_items)})")
                for inv in invalid_items:
                    terminal_lines.append(f"- {inv['res']['rel_workspace']}: \"{inv['text']}\"")
            terminal_lines.append("")

        terminal_lines.append(f"Summary: Total {len(scripts)} | OK: {ok_count} | Missing: {missing_count} | Invalid: {invalid_count} | Violations: {len(violations)}")

    terminal_text = "\n".join(terminal_lines)
    byte_size = len(terminal_text.encode("utf-8"))

    if byte_size < 7200:
        print(terminal_text)
        print(f"[REPORT] {report_uri}\n")
    else:
        print(f"\n🔍 [AUDIT] {scripts_dir}")
        print(f"[INFO] Audit results exceed terminal output limit ({byte_size} bytes). Inspect the full report file below:")
        print(f"Summary: Total {len(scripts)} | OK: {ok_count} | Missing: {missing_count} | Invalid: {invalid_count} | Violations: {len(violations)}")
        print(f"[REPORT] {report_uri}\n")

    if (missing_count > 0 or invalid_count > 0) and not args.verbose:
        print(f"💡 [TIP] Batch fix descriptions via:")
        print(f"         @'{{ \"{scripts[0]['rel_workspace'] if scripts else 'scripts/...'}\": \"Tool description\" }}'@ | python scripts/dev_tools/scripts_log.py annotate --stdin\n")


# ------------------------------------------------------------------------------
# Subcommand 2: annotate
# ------------------------------------------------------------------------------
def cmd_annotate(args):
    header_kw = getattr(args, "header", None) or "description"
    target_ws = find_workspace_root(args.target if (args.target and os.path.isdir(args.target)) else None)

    batch_map = None
    stdin_text = None

    if args.stdin:
        stdin_raw = read_stdin_safely()
        if stdin_raw.startswith("{") and stdin_raw.endswith("}"):
            try:
                batch_map = json.loads(stdin_raw)
            except Exception as e:
                print(f"[ERROR] Failed to parse JSON batch from stdin: {e}", file=sys.stderr)
                sys.exit(1)
        else:
            stdin_text = stdin_raw

    if batch_map and isinstance(batch_map, dict):
        processed = 0
        for rel_path, desc_text in batch_map.items():
            if not isinstance(desc_text, str) or not desc_text.strip():
                continue
            full_file = os.path.abspath(os.path.join(target_ws, rel_path))
            if not os.path.exists(full_file):
                print(f"[WARN] File not found for annotation: {rel_path} (Skipped)")
                continue
            action = inject_script_comment(full_file, desc_text.strip(), args.prefix, header_kw)
            print(f"[SUCCESS] Annotated description in: {rel_path} ({action})")
            processed += 1
        
        print(f"\n[BATCH] Successfully processed {processed} file(s).")
        if not args.no_sync:
            cmd_sync_internal(target_ws, header_kw)
        return

    # Single file mode
    if not args.target:
        print("[ERROR] Target script path is required for annotation.", file=sys.stderr)
        sys.exit(1)

    target_file = os.path.abspath(os.path.join(target_ws, clean_path(args.target)) if not os.path.isabs(args.target) else args.target)
    if os.path.isdir(target_file):
        print(f"[ERROR] Target is a directory, not a script file: {target_file}", file=sys.stderr)
        sys.exit(1)
    if not os.path.exists(target_file):
        print(f"[ERROR] Target file not found: {target_file}", file=sys.stderr)
        sys.exit(1)

    desc = args.desc or stdin_text
    if not desc:
        print("[ERROR] Description (-d or --stdin) is required.", file=sys.stderr)
        sys.exit(1)

    rel_from_ws = os.path.relpath(target_file, target_ws).replace("\\", "/")
    action = inject_script_comment(target_file, desc.strip(), args.prefix, header_kw)
    print(f"[SUCCESS] Annotated description in: {rel_from_ws} ({action})")

    if not args.no_sync:
        cmd_sync_internal(target_ws, header_kw)


# ------------------------------------------------------------------------------
# Subcommand 3: promote (tests/{domain}/ -> tools/{domain}/)
# ------------------------------------------------------------------------------
def cmd_promote(args):
    target_ws = find_workspace_root()
    source_path = os.path.abspath(os.path.join(target_ws, clean_path(args.source)) if not os.path.isabs(args.source) else args.source)

    if not os.path.exists(source_path):
        print(f"[ERROR] Source script not found: {source_path}", file=sys.stderr)
        sys.exit(1)

    scripts_dir = os.path.join(target_ws, "scripts")
    rel_from_scripts = os.path.relpath(source_path, scripts_dir).replace("\\", "/")
    parts = rel_from_scripts.split("/")

    file_name = args.name or parts[-1]
    if parts[0] == "tests" and len(parts) >= 3:
        domain = parts[1]
        dest_rel = f"tools/{domain}/{file_name}"
    elif parts[0] == "tests" and len(parts) == 2:
        domain = args.domain or "general"
        dest_rel = f"tools/{domain}/{file_name}"
    elif args.domain:
        dest_rel = f"tools/{args.domain}/{file_name}"
    else:
        dest_rel = f"tools/general/{file_name}"

    dest_full = os.path.join(scripts_dir, dest_rel)
    os.makedirs(os.path.dirname(dest_full), exist_ok=True)

    shutil.move(source_path, dest_full)
    rel_ws_dest = os.path.relpath(dest_full, target_ws).replace("\\", "/")
    print(f"[SUCCESS] Promoted script: {rel_from_scripts} -> {dest_rel}")

    header_kw = getattr(args, "header", None) or "description"
    if args.desc:
        inject_script_comment(dest_full, args.desc.strip(), header_keyword=header_kw)
        print(f"[SUCCESS] Updated description in: {rel_ws_dest}")

    if not args.no_sync:
        cmd_sync_internal(target_ws, header_kw)


# ------------------------------------------------------------------------------
# Subcommand 4: sync
# ------------------------------------------------------------------------------
def cmd_sync_internal(workspace_root: str, header_keyword: str = "description"):
    guide_path = os.path.join(workspace_root, "docs", "memories", "scripts_guide.md")
    os.makedirs(os.path.dirname(guide_path), exist_ok=True)

    scripts = scan_workspace_scripts(workspace_root, header_keyword)

    grouped = {}
    for s in scripts:
        grp = s["group_display"]
        if grp not in grouped:
            grouped[grp] = []
        grouped[grp].append(s)

    list_lines = ["<!-- START_LIST -->"]
    if not scripts:
        list_lines.append("*등록된 스크립트가 없습니다.*")
    else:
        for grp_name, items in grouped.items():
            list_lines.append(f"### {grp_name}")
            for item in items:
                eval_res = evaluate_description(item["desc"])
                desc_text = eval_res[1]
                uri = to_file_uri(item["full_path"])
                # Safety badge is only present for tests/ files
                badge_prefix = f"{item['badge']} " if item["badge"] else ""
                list_lines.append(f"- [{item['basename']}]({uri}): {badge_prefix}{desc_text}")
            list_lines.append("")

        if list_lines and list_lines[-1] == "":
            list_lines.pop()

    list_lines.append("<!-- END_LIST -->")
    list_block = "\n".join(list_lines)

    if not os.path.exists(guide_path):
        skeleton = f"""# 🌌 Workspace Scripts Guide

이 문서는 워크스페이스의 `scripts/` 디렉터리에 배치된 모든 개발 도구, 테스트 스위트, 빌드 유틸리티의 역할과 안전 실행 가이드를 제공하는 색인 문서입니다.

## 1. 3-Tier Directory Architecture
- **`dev_tools/`**: 워크스페이스 개발 및 자동화 보조 CLI 도구 (반영구 시스템 메타 도구)
- **`tools/{{domain}}/`**: 기능 검증이 완료되어 지속적으로 사용되는 도메인별 확정 유틸리티
- **`tests/{{domain}}/`**: 새로운 기능 검증, 버그 재현, 단위/통합 테스트용 샌드박스 (`chk_`: `🟢 [Read-Only]`, `run_`: `🟡 [Mutating]`)

## 2. Script Registry
{list_block}

## 3. Maintenance Commands
```powershell
# 1. 스크립트 주석 및 아키텍처 규격 진단 (토큰 절약형)
python scripts/dev_tools/scripts_log.py audit

# 2. 다중 스크립트 주석 일괄 주입 (Here-String Stdin)
@'
{{
  "scripts/dev_tools/memory_log.py": "구조화된 지식 자산화 기록 유틸리티"
}}
'@ | python scripts/dev_tools/scripts_log.py annotate --stdin

# 3. 임시 스크립트를 확정 도구로 원클릭 승격
python scripts/dev_tools/scripts_log.py promote scripts/tests/media/chk_mpv.py

# 4. scripts_guide.md 리스트 자동 동기화
python scripts/dev_tools/scripts_log.py sync
```
"""
        with open(guide_path, "w", encoding="utf-8") as f:
            f.write(skeleton)
        print(f"[CREATED] {to_file_uri(guide_path)}")
    else:
        with open(guide_path, "r", encoding="utf-8") as f:
            content = f.read()

        marker_pattern = re.compile(r'<!-- START_LIST -->[\s\S]*?<!-- END_LIST -->')
        if marker_pattern.search(content):
            updated = marker_pattern.sub(list_block, content)
        else:
            updated = content + f"\n\n## 2. Script Registry\n{list_block}\n"

        with open(guide_path, "w", encoding="utf-8") as f:
            f.write(updated)
        print(f"[SUCCESS] Synchronized scripts_guide.md ({len(scripts)} script(s) indexed).")
        print(f"[UPDATED] {to_file_uri(guide_path)}")


def cmd_sync(args):
    target_ws = find_workspace_root(args.target)
    header_kw = getattr(args, "header", None) or "description"
    cmd_sync_internal(target_ws, header_kw)


# ------------------------------------------------------------------------------
# Main Entry Point & CLI Parser
# ------------------------------------------------------------------------------
def main():
    parser = argparse.ArgumentParser(
        prog="scripts_log",
        description="Universal Workspace Script Governance CLI (Audit, Annotate, Promote, Sync)"
    )
    parser.add_argument("--header", default="description", help="Custom comment header keyword to parse/inject (default: 'description')")
    subparsers = parser.add_subparsers(dest="command", help="Available subcommands")

    # 1. audit
    p_audit = subparsers.add_parser("audit", help="Audit scripts for missing descriptions and architecture violations")
    p_audit.add_argument("target", nargs="?", default=None, help="Workspace root or scripts directory")
    p_audit.add_argument("-v", "--verbose", action="store_true", help="Show full ASCII diagnostic table")
    p_audit.add_argument("-o", "--output", help="Custom markdown report output path")
    p_audit.set_defaults(func=cmd_audit)

    # 2. annotate
    p_annotate = subparsers.add_parser("annotate", help="Inject or update description comments in script files")
    p_annotate.add_argument("target", nargs="?", default=None, help="Target script path")
    p_annotate.add_argument("-d", "--desc", help="Description text to inject")
    p_annotate.add_argument("-s", "--stdin", action="store_true", help="Read description or batch JSON mapping from stdin")
    p_annotate.add_argument("-p", "--prefix", help="Manual comment prefix override (e.g. '#', '//', '::', '<!--')")
    p_annotate.add_argument("--no-sync", action="store_true", help="Skip automatic scripts_guide.md sync")
    p_annotate.set_defaults(func=cmd_annotate)

    # 3. promote
    p_promote = subparsers.add_parser("promote", help="Promote a script from tests/{domain}/ to tools/{domain}/")
    p_promote.add_argument("source", help="Path to source test script")
    p_promote.add_argument("-d", "--desc", help="Updated description for promoted tool")
    p_promote.add_argument("-n", "--name", help="New filename in destination domain folder")
    p_promote.add_argument("--domain", help="Explicit target domain name override")
    p_promote.add_argument("--no-sync", action="store_true", help="Skip automatic scripts_guide.md sync")
    p_promote.set_defaults(func=cmd_promote)

    # 4. sync
    p_sync = subparsers.add_parser("sync", help="Synchronize scripts_guide.md registry with current scripts/ layout")
    p_sync.add_argument("target", nargs="?", default=None, help="Workspace root")
    p_sync.set_defaults(func=cmd_sync)

    # Normalize -p "--" to --prefix="--" to avoid argparse treating bare "--" as option terminator
    argv = list(sys.argv[1:])
    i = 0
    while i < len(argv) - 1:
        if argv[i] in ("-p", "--prefix") and argv[i + 1] == "--":
            argv[i] = "--prefix=--"
            del argv[i + 1]
        else:
            i += 1

    if not argv:
        parser.print_help()
        sys.exit(0)

    args = parser.parse_args(argv)
    if hasattr(args, "func"):
        args.func(args)
    else:
        parser.print_help()


if __name__ == "__main__":
    main()
