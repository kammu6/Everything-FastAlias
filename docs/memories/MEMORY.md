# Agent Memory Log (d:\3_Code\3_Apps\43_Search-Edit\Everything검색기)

이 문서는 프로젝트 운영 도중 에이전트가 획득한 지식을 자산화하고, 이전 세션과 다음 세션 간의 Context를 끊김 없이 이어주기 위한 메모리 로그입니다.

## 규칙
- **과거 메모리**: `/docs/memories/backup/` 디렉토리에 이전 세션의 MEMORY 및 과거 memories 백업이 존재합니다. 필요 시 참조하십시오.
- **Append-only**: 기존 기록을 임의 삭제하지 않고, 새로운 기록은 항상 최하단에 추가합니다. `memory_log.py` 스크립트를 사용하면 안전하게 Append-only 방식으로 기록을 추가할 수 있습니다.
```powershell
  # PowerShell Here-String stdin mode (1-Step Recommended: 100% safe for special chars, quotes & multiline)
  @'
  {
    "title": "Title here...",
    "context": "Context & Goal here...",
    "solution": "Verified Solution here...",
    "anti_patterns": "Anti-Patterns & Root Cause here..."
  }
  '@ | python scripts/dev_tools/memory_log.py --stdin
```

## 범례
🎯 = 콘텍스트 (Context & Goal)
✅ = 확정된 해결 솔루션 (Final Success Path)
❌ = 차단된 우회로 및 안티패턴 (Failed Attempts & Anti-Patterns - 필수)

## 로그  
```예시
### YYYY-MM-DD (주제 및 핵심 현상 요약)
- 🎯
  - [문제가 발생한 환경, 재현 시나리오, 개발 목표 또는 구체적인 에러 코드/메시지 명시]
- ✅
  - **구체적 해결책:** [어떤 코딩 변경, 빌드 옵션, 라이브러리 교체를 통해 해결했는지 기술]
  - **동작 원리:** [이 방법이 왜 정상 작동하는지에 대한 기술적 메커니즘 설명]
- ❌
  - _시도했던 접근:_ [성공하기 전, 혹은 트러블슈팅 과정에서 시도했던 잘못되거나 실패한 접근 방식]
  - _실패 원인 분석:_ [왜 이 방법이 통하지 않았는지, 어떤 사이드 이펙트나 컴파일 에러가 터졌는지 기술]
  - _차단 효과:_ [이 기록을 통해 다음 세션의 에이전트가 방지할 수 있는 불필요한 시도/리소스 낭비 정의]
```

### 2026-08-22 (FastAlias 대용량 동의어(Words) CSV 정제 및 다국어 필터링 규칙 (연도 패턴, 1글자 노이즈, 10자 초과 일어))

- 🎯
  - FastAlias 매핑 사전 내보내기 CSV(FastAlias_Export_260820.csv)에서 잘못 파싱된 연도 표기(예: Suzu-1997, 【2018年】), 1글자 노이즈, 10자 초과 긴 일본어 문장/비디오 제목을 O(1) 메모리 스트리밍으로 정제하고 검증하는 요구사항.

- ✅
  - 1. run_fastalias_csv_cleaner.py 스크립트를 구현하여 csv.reader/writer 스트리밍 처리 (2,950행, 32,587단어를 0.83초 만에 무결 처리).
2. 규칙 1 (연도 표기): [-_](?:19|20)\d{2}|[【（\(].*?(?:19|20)\d{2}.*?[】）\)]|(?:19|20)\d{2}年 정규식으로 연도 표기만 정밀 타겟팅하여 계정명(rmrm1313) 보존.
3. 규칙 2 (1글자 노이즈): len <= 1 and ' ' not in token으로 1글자 노이즈 필터링.
4. 규칙 3 (10자 초과 일어): len > 10 and re.search(r'[\u3040-\u309F\u30A0-\u30FF]', token)으로 잘못 파싱된 긴 일어 문장 선별 제거.
5. Dry-run 모드를 기본 지원하여 변경 전 샘플 및 통계 검증 후 Apply 실행.

- ❌
  - 1. 시도했던 접근: any(c.isdigit() for c in token)으로 숫자가 포함된 모든 단어를 일괄 제거하려고 시도.
2. 실패 원인 분석: '박라희'의 유일한 Words인 인스타그램 계정명 rmrm1313 등 정상적인 계정/아이디형 단어까지 삭제되어 키워드가 완전히 비는 문제 발생.
3. 차단 효과: 단순 숫자 배제 대신 연도 정규식([-_](?:19|20)\d{2} 등)을 통해 실제 파싱 오류인 연도 표기만 선택적으로 제거하고 유효 계정명을 안전하게 보존함.

### 2026-08-22 (Windows/Excel CSV 내보내기 및 정제 시 UTF-8 BOM(utf-8-sig) 필수 적용 규칙)

- 🎯
  - Python 스크립트에서 CSV 생성 시 일반 UTF-8(BOM 없음)로 저장하면, Windows 엑셀 및 기본 뷰어에서 ANSI(CP949)로 오인식하여 한글/일어 문자가 모두 깨지는 현상 발생.

- ✅
  - 1. Python csv.writer 파일 열기 시 encoding='utf-8-sig'를 명시하여 3바이트 UTF-8 BOM(\xef\xbb\xbf)을 헤더에 삽입.
2. C# .NET의 File.WriteAllText(..., Encoding.UTF8)와 100% 동일한 인코딩 호환성을 확보하여 엑셀, 윈도우 메모장, FastAlias 앱 모두에서 글자 깨짐 없이 완벽하게 인식되도록 수정.

- ❌
  - 1. 시도했던 접근: open(..., encoding='utf-8')로 저장.
2. 실패 원인 분석: Windows 환경의 Excel/CSV 뷰어는 UTF-8 BOM이 없으면 시스템 로케일(CP949)로 간주하여 멀티바이트 한글/일어를 깨진 문자로 렌더링함.
3. 차단 효과: 향후 모든 CSV 가공 및 내보내기 스크립트 작성 시 반드시 utf-8-sig를 사용하여 인코딩 깨짐을 원천 차단함.

### 2026-08-22 (FastAlias Keyword/Words 내 괄호 및 중첩 괄호 표기 정규화 규칙)

- 🎯
  - 웹 스크랩 과정에서 'Hara Nozomi (原望美)'처럼 키워드 및 Words 토큰 내에 괄호식 본명/한자 표기가 잘못 삽입되어 Everything 별칭 매핑 시 중복 노이즈가 발생하는 문제.

- ✅
  - 1. clean_parens 함수를 구현하여 반복적 re.sub로 중첩 괄호([\(（\[【]...[\)）\]】]) 및 잔여 괄호 기호, 불필요한 연속 공백을 완전 제거.
2. 'Hara Nozomi (原望美)' -> 'Hara Nozomi', '(原望美) Nozomi Hara' -> 'Nozomi Hara'로 깔끔히 정돈하면서도, 이미 개별 토큰으로 존재하는 '原望美'는 독립 동의어로 안전하게 유지.
3. Keyword와 Words 전체에 적용 후 FastAlias_Cleaned_260822.csv에 반영 완료.

- ❌
  - 1. 시도했던 접근: 단순 1회성 regex(.*?괄호 매칭)로 치환 시도.
2. 실패 원인 분석: 'Hinata (ひなた（葉月凛）)'와 같이 ASCII 괄호와 전각 괄호가 중첩된 경우 내부 괄호만 지워지고 닫는 괄호 ')'가 문자열 끝에 잔류하는 버그 발생.
3. 차단 효과: while 루프로 괄호 쌍이 소진될 때까지 재귀 치환 후 잔여 단일 괄호까지 정리하여 중첩 괄호 버그를 완벽히 해결함.

### 2026-08-22 (FastAlias 단일 영문(성/이름 누락) 키워드 행 필터링 및 해시태그/한글 보존 규칙)

- 🎯
  - 키워드(Keyword) 중 'Hinako', 'Mayu', 'Mei'처럼 성 또는 이름 없이 단일 단어로만 되어 있는 영문 키워드 행을 일괄 제외하고, #으로 시작하는 해시태그(#back_to_freedom) 및 한글 키워드(박라희), 영문 풀네임(Hara Nozomi)은 안전하게 보존하는 규칙 구현.

- ✅
  - 1. should_filter_keyword 함수를 구현하여 ^[A-Za-z_\.\-]+$ 패턴에 매칭되면서 공백(' ')이 없고 #이나 한글이 없는 단일 영문 키워드 116개 행을 정확히 선별 제거.
2. #으로 시작하는 태그(#back_to_freedom 등) 및 한글 키워드(박라희 등), 성+이름으로 구성된 영문 키워드(2,834행)는 100% 보존.
3. UTF-8 with BOM(utf-8-sig)으로 tests/FastAlias_Cleaned_260822.csv에 반영 완료.

- ❌
  - 1. 시도했던 접근: 단순 알파벳 길이 검사로 키워드를 필터링하려 시도.
2. 실패 원인 분석: #back_to_freedom이나 박라희 등 특수 케이스가 오탐으로 제거될 위험이 있음.
3. 차단 효과: # 시작 여부와 유니코드 한글 검사를 우선 통과(Early return)시킨 후 순수 영문 무공백 단어만 정밀 타겟팅하여 안전하게 처리함.

### 2026-08-22 (FastAlias 영문 2단어 이름 도치(성 이름 -> 이름 성) 및 대문자화 자동 변환 파이프라인)

- 🎯
  - 동의어 사전에서 영문 이름이 동양식 순서(Last First: omori Shizuka)로 되어 있는 경우, Everything 검색 UI에서의 가독성을 위해 서양식(First Last: Shizuka Omori)으로 도치하고 Words의 1, 2순위 정렬을 표준화하는 요구사항.

- ✅
  - 1. run_fastalias_invert_names.py 스크립트를 구현하여 '영문(공백)영문' 형태 2,823개 행을 타겟팅.
2. Keyword를 '이름 성'으로 도치하고 앞글자를 Title/Capitalize화 (omori Shizuka -> Shizuka Omori).
3. Words 1번째 = '이름 성', 2번째 = '성 이름'으로 고정 배치하고, 3번째 이후 기존 다국어(한글/일어) 동의어 보존 및 중복 제거.
4. #태그 및 한글 키워드(박라희 등 11개 행)는 100% 원형 보존.
5. UTF-8 with BOM(utf-8-sig)으로 tests/FastAlias_Cleaned_Inverted_260822.csv에 저장 완료.

- ❌
  - 1. 시도했던 접근: 단순 .title() 메서드만 적용하여 split 변환.
2. 실패 원인 분석: 하이픈이 포함된 복합 성/이름(Jean-Pierre 등)의 경우 하이픈 뒤의 첫 글자가 소문자로 남거나 단어 분리에서 3단어로 오인식될 수 있음.
3. 차단 효과: format_part 함수에서 하이픈 단위 분할 대문자화를 적용하고 정규식 ^[A-Za-z\-]+$으로 2단어 영문을 정밀 검증하여 안전하게 처리함.

### 2026-08-22 (타입3(공백 포함 성+이름) 키워드 대상 Words 내 무공백 한글/영문 1단어 필터링 규칙 (규칙 4))

- 🎯
  - 타입3 키워드(예: Ai Abe, Shizuka Omori)의 Words 목록 내에서 '아베아이', '사사노히마리'처럼 띄어쓰기 없이 붙여쓴 한글/영문 1단어 노이즈를 일괄 제거하되, 일본어/한자(あべあい, 瀬戸ひまり) 및 공백이 있는 정상 단어(아베 아이, 사사노 히마리)는 100% 보존하는 규칙.

- ✅
  - 1. should_filter_word에 규칙 4 추가: is_type3이고 공백이 없는 단어 중, 일본어/한자(RE_JAPANESE_OR_KANJI)가 포함되지 않은 순수 한글/영문 1단어만 정확히 판별하여 제외.
2. 총 1,020개 키워드에서 1,235개 무공백 단어(아베아이, 히나히마리 등)를 제외 대상으로 선별.
3. 전체 제외 단어 목록을 tests/dry_run_report.txt에 키워드별 및 알파벳순으로 100% 상세 기록.

- ❌
  - 1. 시도했던 접근: 단순 공백 없는 단어 일괄 제외.
2. 실패 원인 분석: 일본어/한자 이름(瀬戸ひまり, 原望美)까지 모두 지워지는 치명적 결함 발생.
3. 차단 효과: RE_JAPANESE_OR_KANJI를 적용하여 한자 및 가나가 포함된 토큰은 안전하게 제외(유지)시킴으로써 완벽한 필터링 달성.

### 2026-08-22 (FastAlias CSV 최종 정제(규칙 1~4, 괄호/단일영문 제외, 이름 도치) 파일 생성 완료)

- 🎯
  - 대용량 FastAlias Export CSV(2,950행)로부터 4대 정제 규칙(연도/1글자/10자초과일어/타입3무공백단어 제거), 중첩 괄호 정규화, 단일 영문 키워드 제외, 영문 이름 도치(성 이름 -> 이름 성)를 일괄 적용하여 최종본 생성.

- ✅
  - 1. run_fastalias_csv_cleaner.py와 run_fastalias_invert_names.py의 파일 쓰기 로직에 temp_path 및 os.replace 원자적 교체를 적용하여 in-place 덮어쓰기 무결성 확보.
2. 최종 2,834행, 30,212개 단어가 정제된 FastAlias_Cleaned_Inverted_260822.csv 및 FastAlias_Cleaned_260822.csv 생성 완료.
3. UTF-8 with BOM(utf-8-sig) 인코딩으로 FastAlias 앱 및 엑셀에서 완벽 호환.

- ❌
  - 1. 시도했던 접근: input_path == output_path인 상태에서 open(output_path, 'w') 즉시 호출.
2. 실패 원인 분석: 파이썬의 'w' 모드는 파일을 여는 순간 0바이트로 truncate하므로 읽기 시도 시 빈 파일이 되는 치명적 버그 유발.
3. 차단 효과: 항상 .tmp 임시 파일에 전체 데이터를 쓴 뒤 파일 핸들을 닫고 os.replace로 교체하는 원자적 쓰기 패턴을 영구 표준화함.
