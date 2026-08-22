# description: "Scans the src directory for hardcoded file extensions and media preset mappings."

import os
import re
import json

def scan_hardcoded_extensions(src_dir):
    patterns = {
        "ext_filter_syntax": re.compile(r'ext:([a-zA-Z0-9;_\-,\s]+)', re.IGNORECASE),
        "preset_ext_assignment": re.compile(r'extPattern\s*=\s*"([^"]+)"', re.IGNORECASE),
        "literal_extensions": re.compile(r'"([a-zA-Z0-9]+;[a-zA-Z0-9;]+)"'),
    }
    
    findings = []
    
    for root, _, files in os.walk(src_dir):
        for file in files:
            if file.endswith((".cs", ".xaml")):
                filepath = os.path.join(root, file)
                relpath = os.path.relpath(filepath, src_dir)
                
                with open(filepath, "r", encoding="utf-8", errors="ignore") as f:
                    for line_num, line in enumerate(f, 1):
                        for p_name, p_regex in patterns.items():
                            matches = p_regex.findall(line)
                            for match in matches:
                                findings.append({
                                    "file": relpath,
                                    "line": line_num,
                                    "pattern_type": p_name,
                                    "content": match.strip(),
                                    "raw_line": line.strip()
                                })
    return findings

if __name__ == "__main__":
    current_dir = os.path.dirname(os.path.abspath(__file__))
    workspace_root = os.path.abspath(os.path.join(current_dir, "..", "..", ".."))
    src_path = os.path.join(workspace_root, "src")
    
    results = scan_hardcoded_extensions(src_path)
    
    print(f"=== Hardcoded Extension Scan Results (Total: {len(results)}) ===")
    for r in results:
        print(f"[{r['file']}:{r['line']}] ({r['pattern_type']}) -> {r['content']}")
        print(f"    Line: {r['raw_line']}")
