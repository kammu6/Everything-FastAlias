# Step 001: FastAlias CSV Words 필터링 및 정제 스크립트 구축 계획서 (v1)

## 1. Requirements (요구사항)

### 1.1. 배경 및 목적
- FastAlias 매핑 사전 DB를 추출한 CSV 파일(`D:\3_Code\3_Apps\43_Search-Edit\Everything검색기\tests\FastAlias_Export_260820.csv`)의 B열(`Words`)에 잘못 파싱된 영문-숫자(4자리 등) 및 불필요한 초단문(1~3글자 공백 없음) 단어들이 포함되어 있어, Everything 검색 품질을 저하시키고 있음.
- 대용량 CSV 파일의 메모리 부하를 최소화하면서 규칙 기반으로 B열의 `Words` 목록을 정제하고, 정제된 CSV를 앱에 다시 안전하게 재임포트(`ImportExcel`)할 수 있도록 지원.

### 1.2. 세부 필터링 규칙
1. **규칙 1 (숫자 포함 단어 제거)**:
   - 단어 내에 0~9 숫자가 1개 이상 포함된 모든 `word` 제거 (예: `apple2024`, `v1`, `1234`, `abc_10`).
2. **규칙 2 (공백 없는 1~3글자 단어 제거)**:
   - 단어 내에 공백(` `)이 없으면서 글자 수가 1~3글자 이하인 `word` 제거.
   - 대상 문자: 영문, 한글, 일어(히라가나/가타카나/한자) 등 모든 유니코드 문자.
   - 예시:
     - `안녕` (2글자, 공백 없음) ❌ 제외
     - `abc` (3글자, 공백 없음) ❌ 제외
     - `안 녕` (3글자, 공백 포함) ✅ 유지
     - `a b` (3글자, 공백 포함) ✅ 유지
     - `abcd` (4글자, 공백 없음) ✅ 유지
     - `안녕하세요` (5글자, 공백 없음) ✅ 유지
3. **행/단어 정규화**:
   - `Words` 분리 구분자: `;` (세미콜론) 기준 분할 및 불필요한 앞뒤 공백 제거.
   - 중복 단어 제거 (대소문자 보존 또는 대소문자 무시 고유화).
   - 모든 단어가 제거되어 `Words`가 빈 문자열이 된 행은 최종 CSV에서 자동 제외(앱의 `ImportExcel`은 빈 `Words` 행을 무시하므로 무효 행 정리).

### 1.3. 수락 기준 (Acceptance Criteria)
- [ ] 대용량 CSV 스트리밍 처리로 메모리 폭발(OOM) 없이 빠른 시간 내 파싱 완료.
- [ ] **Dry-run 모드 우선 지원**: 실제 파일을 덮어쓰거나 수정하지 않고, 제외 대상 통계(규칙별 제외 건수, 샘플 제외 단어 목록)를 리포트로 출력.
- [ ] **Apply 모드 지원**: 원본 파일은 보존하고 지정된 출력 경로(`FastAlias_Cleaned_260822.csv`)로 안전하게 정제 파일 생성.
- [ ] 정제된 CSV가 FastAlias 앱의 `ExcelService.ImportExcel` 스키마(`Keyword,Words` 헤더, UTF-8 인코딩)와 100% 호환.

---

## 2. Tech Stack (기술 스택)

- **언어**: Python 3.10+
- **모듈**: `csv` (표준 라이브러리), `re` (정규식), `argparse`, `io`
- **인코딩**: UTF-8 (BOM 호환 `utf-8-sig`)
- **실행 환경**: Windows 10 PowerShell 5.1 / 7

---

## 3. Hypotheses & Grounding Evidence (가설 및 기술적 근거)

### 3.1. CSV 구조 및 FastAlias 앱 연동성 검증
- **가설 1**: FastAlias 앱의 `ExcelService.ExportToCsv`는 `Keyword,Words` 2개 열로 내보내며, `Words` 열은 세미콜론(`;`)으로 구분된 문자열로 구성된다.
  - **검증 근거**: `src/EverythingFastAlias/Services/ExcelService.cs` 및 `AliasManagerViewModel.cs` 소스 확인 결과, `EscapeCsv`를 적용하여 `Keyword,Words` 형태로 출력됨.
- **가설 2**: `ExcelService.ImportExcel`은 첫 번째 행을 헤더(`Keyword`)로 건너뛰고, `Keyword` 또는 `Words`가 `string.IsNullOrEmpty`인 경우 행을 자동 제외한다.
  - **검증 근거**: `ExcelService.cs:42-58` 라인에서 `!string.IsNullOrEmpty(keyword) && !string.IsNullOrEmpty(words)` 조건을 검사함. 따라서 `Words`가 모두 제거된 행은 출력 파일에서 배제하는 것이 안전함.

---

## 4. Folder Structure (폴더 및 파일 구조)

```text
scripts/
└── tests/alias/
    └── run_fastalias_csv_cleaner.py  # [Mutating/Dry-Run] CSV Words 필터링 및 Dry-run 리포트 생성 스크립트
```

---

## 5. Implementation Plan (구현 계획)

### Step 1: `run_fastalias_csv_cleaner.py` 스크립트 제작 (난이도: ★★☆☆☆)
- 위치: `scripts/tests/alias/run_fastalias_csv_cleaner.py`
- 주요 기능:
  1. **CLI 옵션 구성**:
     - `--input` (기본값: `tests/FastAlias_Export_260820.csv` 또는 입력 경로)
     - `--output` (기본값: `tests/FastAlias_Cleaned_YYYYMMDD.csv`)
     - `--dry-run` (기본 동작 모드: 파일 쓰기 없이 통계 및 샘플 출력)
     - `--apply` (실제 정제 CSV 파일 생성)
     - `--report` (상세 제외 단어 리포트 파일 출력 경로)
  2. **필터링 함수 (`should_filter_word`)**:
     - Rule 1 (숫자 포함): `any(ch.isdigit() for ch in token)` ➡️ 제외
     - Rule 2 (공백 없이 1~3자): `' ' not in token and len(token) <= 3` ➡️ 제외
  3. **스트리밍 파서**:
     - `csv.reader`를 사용하여 행 단위로 읽고 메모리 사용량을 O(1) 수준으로 유지.
     - 각 행의 `Words`를 `;`로 분할한 뒤 유효한 단어만 수집.
  4. **통계 및 리포트 집계**:
     - 총 처리 행 수, 유지된 행 수, 완전 제거된 행 수
     - 총 단어 수, 유지된 단어 수, 규칙 1로 제외된 단어 수, 규칙 2로 제외된 단어 수
     - 규칙별 대표 샘플 50개 미리보기 출력

---

## 6. Verification Plan (검증 계획)

### 6.1. 단위 테스트 및 Dry-run 검증
1. **스크립트 Dry-run 실행**:
   ```powershell
   python scripts/tests/alias/run_fastalias_csv_cleaner.py --input "D:\3_Code\3_Apps\43_Search-Edit\Everything검색기\tests\FastAlias_Export_260820.csv" --dry-run
   ```
2. **출력 결과 확인**:
   - 규칙 1 (숫자 포함) 제외 단어 샘플이 정확히 숫자 포함 단어인지 확인.
   - 규칙 2 (1~3자 공백 없음) 제외 단어 샘플이 `안녕`, `abc` 등 공백 없는 단어인지 확인.
   - `안 녕`, `a b` 등 공백 포함 단어가 유지되었는지 확인.

### 6.2. Apply 실행 및 정제 파일 검증
1. **정제 파일 생성**:
   ```powershell
   python scripts/tests/alias/run_fastalias_csv_cleaner.py --input "D:\3_Code\3_Apps\43_Search-Edit\Everything검색기\tests\FastAlias_Export_260820.csv" --output "D:\3_Code\3_Apps\43_Search-Edit\Everything검색기\tests\FastAlias_Cleaned_260822.csv" --apply
   ```
2. **무결성 검사**:
   - 상위 10행 샘플 확인 (인코딩 깨짐 없는지 확인).
   - FastAlias 앱 `ImportExcel` 호환성 검증.

---

## 7. Capitalization Plan (자산화 계획)

- `docs/memories/MEMORY.md`: 대용량 동의어 CSV 정제 규칙 및 1~3자 공백 분기 처리 노하우 기록.
- `docs/memories/scripts_guide.md`: `scripts/tests/alias/run_fastalias_csv_cleaner.py` 스크립트 색인 동기화.

---

## 8. Request for Approval (승인 요청)

FastAlias 내보내기/가져오기 아키텍처 및 요구사항에 대한 95% 확신을 확보하였으며, 안전한 **Dry-run 우선 실행 방식**으로 설계를 완료하였습니다. 계획서에 대한 사용자 승인 후 스크립트 구현 및 Dry-run을 진행하겠습니다.
