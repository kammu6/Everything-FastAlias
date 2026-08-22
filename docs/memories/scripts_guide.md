# 🌌 Workspace Scripts Guide

이 문서는 워크스페이스의 `scripts/` 디렉터리에 배치된 모든 개발 도구, 테스트 스위트, 빌드 유틸리티의 역할과 안전 실행 가이드를 제공하는 색인 문서입니다.

## 1. 3-Tier Directory Architecture
- **`dev_tools/`**: 워크스페이스 개발 및 자동화 보조 CLI 도구 (반영구 시스템 메타 도구)
- **`tools/{domain}/`**: 기능 검증이 완료되어 지속적으로 사용되는 도메인별 확정 유틸리티
- **`tests/{domain}/`**: 새로운 기능 검증, 버그 재현, 단위/통합 테스트용 샌드박스 (`chk_`: `🟢 [Read-Only]`, `run_`: `🟡 [Mutating]`)

## 2. Script Registry
<!-- START_LIST -->
### dev_tools/
- [memory_log.py](file:///D:/3_Code/3_Apps/43_Search-Edit/Everything검색기/scripts/dev_tools/memory_log.py): Safely appends anti-pattern knowledge logs to MEMORY.md with quote escaping defenses, stdin pipe support, validation guards, and JSON/CLI input support
- [overview_tree.py](file:///D:/3_Code/3_Apps/43_Search-Edit/Everything검색기/scripts/dev_tools/overview_tree.py): Universal 7-tier rule-based directory tree generator with smart annotation mapping (ljust-padded comments)
- [scripts_log.py](file:///D:/3_Code/3_Apps/43_Search-Edit/Everything검색기/scripts/dev_tools/scripts_log.py): Universal script governance CLI: audit, in-place annotations, 3-tier promotion, and scripts_guide.md synchronization

### tests/alias/
- [chk_keyword_priority_model.py](file:///D:/3_Code/3_Apps/43_Search-Edit/Everything검색기/scripts/tests/alias/chk_keyword_priority_model.py): `🟢 [Read-Only]` FastAlias 키워드 일치 우선(Keyword Priority) 모델의 케이스별 쿼리 변환 알고리즘 시뮬레이션 및 단위 검증
- [run_fastalias_csv_cleaner.py](file:///D:/3_Code/3_Apps/43_Search-Edit/Everything검색기/scripts/tests/alias/run_fastalias_csv_cleaner.py): `🟡 [Mutating]` Cleans Words column in FastAlias CSV export by filtering year-pattern words, 1-char noise, >10 char Japanese text, removing parentheses, and excluding single-word Korean/English in Type 3 keywords.
- [run_fastalias_invert_names.py](file:///D:/3_Code/3_Apps/43_Search-Edit/Everything검색기/scripts/tests/alias/run_fastalias_invert_names.py): `🟡 [Mutating]` Transforms 2-word English keywords and words from Last-First to First-Last with capitalized casing (e.g. omori Shizuka -> Shizuka Omori).
<!-- END_LIST -->

## 3. Maintenance Commands
```powershell
# 1. 스크립트 주석 및 아키텍처 규격 진단 (토큰 절약형)
python scripts/dev_tools/scripts_log.py audit

# 2. 다중 스크립트 주석 일괄 주입 (Here-String Stdin)
@'
{
  "scripts/dev_tools/memory_log.py": "구조화된 지식 자산화 기록 유틸리티"
}
'@ | python scripts/dev_tools/scripts_log.py annotate --stdin

# 3. 임시 스크립트를 확정 도구로 원클릭 승격
python scripts/dev_tools/scripts_log.py promote scripts/tests/media/chk_mpv.py

# 4. scripts_guide.md 리스트 자동 동기화
python scripts/dev_tools/scripts_log.py sync
```
