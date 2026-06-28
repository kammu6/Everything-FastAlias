# implementation_plan: step005_query_builder_refactoring_plan_v1

본 계획서는 Everything FastAlias 앱의 검색 쿼리 조립 로직을 고도화하여, 사용자가 입력한 Everything 고유 문법(예: `path:`, `regex:` 등) 및 경로 제약조건에 `folder:`와 같은 미디어 수식어가 이중 결합되는 결함을 방지하고, 미디어 필터가 '폴더'일 때 무의미한 파일 크기 제한 필터를 바이패스하도록 체계적인 쿼리 빌딩 알고리즘을 구축하는 설계도입니다.

추가로, "정규식 검색" 옵션 활성화 시 Everything 엔진이 전체 쿼리를 정규식으로 오인하여 검색 결과가 항상 0이 되는 원인을 분석하고, 이를 해결하기 위해 개별 검색어 토큰 단위로 `regex:` 수식어를 결합하는 방식의 리팩토링 설계를 반영합니다.

---

## 1. Requirements (요구사항 및 목표)

1. **미디어 필터가 '폴더'일 때 파일 크기 제한 제거**
   - 사용자가 미디어 필터에서 "폴더"를 지정한 경우, 파일 크기 필터(`MinSize`, `MaxSize`)가 활성화되어 있더라도 쿼리에 `size:` 제약조건이 붙지 않도록 예외 처리하여 폴더 검색 성능 및 무결성 확보.
2. **사용자 직접 지정 Everything 고유 문법과 일반 검색어의 스마트한 분리**
   - 검색창 입력 시 사용자가 직접 입력한 Everything 고유 변경자/함수(예: `path:P:\...`, `regex:...`, `ext:...`) 및 물리 드라이브 경로는 **제약조건(Constraint)**으로 인식하여 보존.
   - 반면, 일반 단어 및 동의어 치환 대상(예: `BEST`)은 미디어 수식어(`folder:`) 및 범위 스코프(`path:`)가 정상적으로 조합/결합되도록 처리.
3. **이중 결합 결함 제거**
   - 사용자가 `BEST path:P:\1_이미지\$eropuru\`를 입력하고 '폴더' 미디어 필터를 활성화했을 때, 기존에 발생하던 `folder:path:P:\...`와 같이 경로 제약에 `folder:`가 중첩 부착되는 결함을 완전히 제거하여 `path:P:\...` 그대로 보존되도록 구현.
4. **"정규식 검색" 옵션 오동작 해결 및 쿼리 최적화**
   - **현상**: 정규식 검색 옵션 활성화 시 `Everything_SetRegex(true)`를 호출하면 괄호 `< >`, OR `|`, `$Recycle.Bin`, 드라이브 `P:` 등 전체 쿼리 텍스트가 모두 정규식으로 강제 해석되어 매칭 실패 및 결과 0건 유발.
   - **해결**: SDK API의 `Everything_SetRegex(true)` 호출은 배제(항상 `false`로 유지)하고, 대신 `QueryTransformer` 내부에서 일반 검색어 토큰별로 `regex:` 수식어를 결합하여 주입하는 방식으로 아키텍처 변경.
5. **대소문자 구분 및 전체 단어 일치 하단바 시각화 관련 설명 정리**
   - 대소문자 구분 및 전체 단어 일치 옵션은 Everything SDK API(`Everything_SetMatchCase`, `Everything_SetMatchWholeWord`)로 세팅되기 때문에 쿼리 텍스트 자체를 지저분하게 만들지 않고도 완벽하게 작동하는 것이 정상 동작임(버그가 아님).

---

## 2. Tech Stack (기술 스택)

- **언어 및 런타임**: C# .NET 9.0 (WPF 데스크톱 애플리케이션)
- **핵심 모듈**: 
  - `EverythingFastAlias.Services.QueryTransformer` (쿼리 치환 및 변환 서비스)
  - `EverythingFastAlias.Native.EverythingBridge` (Everything SDK API 래핑 클래스)
  - `EverythingFastAlias.Tests.QueryTransformerTest` (단위 테스트 어셈블리)
- **로컬 검증 스크립트**: MSBuild / Dotnet CLI (컴파일 및 테스트 수행)

---

## 3. Folder Structure (아키텍처 및 원칙)

본 작업은 기존 WPF MVVM 아키텍처 및 SoC(관심사 분리) 원칙을 유지하며, 쿼리 파싱 단일 책임을 맡은 `QueryTransformer.cs` 내부 로직과 SDK API 호출 정책을 담은 `EverythingBridge.cs` 내부 로직에 한하여 수행합니다. (One Class One File 및 DRY 원칙 준수)

```text
src/
├── EverythingFastAlias/
│   ├── Services/
│   │   └── QueryTransformer.cs           # [MODIFY] 쿼리 변환 및 regex: 조립 알고리즘 개선
│   ├── Native/
│   │   └── EverythingBridge.cs           # [MODIFY] Everything_SetRegex(false) 강제화
│   └── ViewModels/
│       └── SearchViewModel.Search.cs     # [LOCKED] 옵션 복제 및 쿼리 요청 주체
└── EverythingFastAlias.Tests/
    └── QueryTransformerTest.cs           # [MODIFY] 고도화된 쿼리 빌드 케이스 테스트 검증
```

---

## 4. Lookup & Verification Tools (도구 정보)

- **Lookup Tools**: 
  - [QueryTransformer.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything%EA%B2%80%EC%83%89%EA%B8%B0/src/EverythingFastAlias/Services/QueryTransformer.cs) 파일 및 `EverythingSearchSyntax.md` 공식 문법 리소스.
- **Verification Tools**:
  - `dotnet test` 명령을 이용한 `EverythingFastAlias.Tests` 단위 테스트 실행 및 결과 모니터링.
  - 디버그 빌드 배치 파일 실행을 통한 빌드 경고/에러 여부(zero-warning) 확인.

---

## 5. Proposed Changes (변경 설계안)

### 5.1. Everything 예약어 접두사 목록 종합 및 상수 관리
`QueryTransformer.cs` 클래스 내부에 Everything 공식 문서([EverythingSearchSyntax.md](file:///C:/Users/Administrator/.gemini/config/skills/everything-sdk/resources/EverythingSearchSyntax.md))를 준수하는 모든 수식어 및 함수 목록을 상수로 정의하여 판별 신뢰성을 극대화합니다.

```csharp
private static readonly HashSet<string> EverythingKeywords = new(StringComparer.OrdinalIgnoreCase)
{
    // 변경자 (Modifiers)
    "path", "parent", "folder", "file", "regex", "ascii", "noascii", 
    "case", "nocase", "diacritics", "nodiacritics", "wfn", "nowfn", 
    "wholefilename", "nowholefilename", "wholeword", "nowholeword", 
    "wildcards", "nowildcards", "ww", "noww", "utf8",
    // 함수 (Functions)
    "ext", "size", "datemodified", "dm", "datecreated", "dc", "dateaccessed", 
    "da", "daterun", "dr", "attrib", "attributes", "empty", "dupe", 
    "child", "childcount", "childfilecount", "childfoldercount", "infolder", 
    "parents", "len", "startwith", "endwith", "album", "artist", "comment", 
    "genre", "title", "track", "type", "content", "ansicontent", "utf8content", 
    "utf16content", "utf16becontent"
};
```

### 5.2. 고유 제약조건 판별 메서드(`IsConstraintOrDrive`) 고도화
기존의 빈약한 하드코딩 리스트 대신, `:`를 기준으로 접두사 파싱을 추가하여 Everything 수식어나 드라이브 문자(네트워크 경로 포함)를 정교하게 판별합니다.

```csharp
private static bool IsConstraintOrDrive(string term)
{
    if (string.IsNullOrEmpty(term)) return false;

    var trimmed = term.Trim().Trim('"');

    // 1. 드라이브 문자 및 네트워크 경로 감지 (예: C:, D:\, \\server\share)
    if (trimmed.StartsWith(@"\\") || (trimmed.Length >= 2 && trimmed[1] == ':'))
    {
        return true;
    }

    // 2. Everything 수식어 및 함수 접두사 감지 (예: path:..., regex:...)
    int colonIndex = trimmed.IndexOf(':');
    if (colonIndex > 0)
    {
        string prefix = trimmed.Substring(0, colonIndex);
        if (EverythingKeywords.Contains(prefix))
        {
            return true;
        }
    }

    return false;
}
```

### 5.3. 사후 적용 차단 및 즉각 조합 적용 설계
- 기존의 복잡하고 중복 수식어 결합을 유발하던 `ApplyModifierToEachTerm` 메소드를 **완전히 제거**합니다.
- 대신, `Transform` 메소드 내의 1차 토큰화 루프 단계에서 각 토큰에 대해:
  1. `IsConstraintOrDrive`가 참이면 **아무런 수식어(modifier, path)도 붙이지 않고 원본 그대로 보존**합니다.
  2. 일반 단어 토큰인 경우에만 **`options.UseRegex` 여부, `isFolderPreset` 여부 및 `options.Scope` 정책에 맞춰 수식어를 즉각 조립**합니다.

#### 일반 단어 토큰 조립 규칙
- 일반 단어 `w`에 대해:
  - `regPrefix` = `options.UseRegex ? "regex:" : ""`
  - `wordBody` = `{regPrefix}{w}`
  - `modifier` = `isFolderPreset ? "folder:" : ""`
  - `Scope == SearchScope.All`: `< {modifier}{wordBody} | {modifier}path:{wordBody} >`
  - `Scope == SearchScope.Path`: `{modifier}path:{wordBody}`
  - `Scope == SearchScope.None` (이름 검색): `{modifier}{wordBody}`

### 5.4. 미디어 필터 '폴더' 시 파일 크기 필터 바이패스
`BuildOptionConstraints` 내부에서:
```csharp
// 7. 파일 크기 필터 적용 (폴더 프리셋인 경우 무시)
if (options.MinSize.HasValue && !isFolderPreset)
{
    AppendSeparator(sb);
    sb.Append($"size:>={options.MinSize.Value}{options.MinSizeUnit.ToString().ToLower()}");
}
if (options.MaxSize.HasValue && !isFolderPreset)
{
    AppendSeparator(sb);
    sb.Append($"size:<={options.MaxSize.Value}{options.MaxSizeUnit.ToString().ToLower()}");
}
```

### 5.5. Everything SDK API 호출에서의 SetRegex 비활성화
- [EverythingBridge.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything%EA%B2%80%EC%83%89%EA%B8%B0/src/EverythingFastAlias/Native/EverythingBridge.cs)의 라인 119:
```csharp
// 정규식 스위치를 API에 직접 세팅하면 괄호/파이프 등 쿼리 텍스트 전체가 깨지므로 항상 false로 세팅
EverythingSdk.Everything_SetRegex(false); 
```

---

## 6. Implementation Plan (구현 일정)

### [Phase 1] 상수 추가 및 판별 로직 수정 (★☆☆☆☆)
- `QueryTransformer.cs`에 `EverythingKeywords` 종합 상수 목록 추가.
- `IsConstraintOrDrive` 판별 로직 고도화 반영.

### [Phase 2] Transform 내 토큰 조립 루프 전면 리팩토링 및 사후 함수 제거 (★★★☆☆)
- `Transform`의 토큰 루프에서 일반 검색어 조건 판별 및 Scope & Folder 조합 즉각 렌더링 로직으로 교체.
- 정규식 옵션 활성화 시 `regex:`를 토큰 앞에 결합하도록 쿼리 조립 로직 적용.
- 기존의 `ApplyModifierToEachTerm` 메소드 및 관련 불필요 헬퍼 제거.
- `BuildOptionConstraints`에 `isFolderPreset` 여부에 따른 파일 크기 필터 바이패스 분기 추가.

### [Phase 3] SDK API 호출 수정 (★☆☆☆☆)
- `EverythingBridge.cs`에서 `Everything_SetRegex(false)`로 강제 설정하여 쿼리 깨짐 방지.

---

## 7. Verification Plan (검증 및 테스트 계획)

### Automated Tests (자동 단위 테스트)
- [QueryTransformerTest.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything%EA%B2%80%EC%83%89%EA%B8%B0/src/EverythingFastAlias.Tests/QueryTransformerTest.cs)에 사용자가 제시한 구체적 케이스 검증용 테스트 어서트 신설:
  1. `Test_FolderPreset_Ignores_FileSizeFilter`: 폴더 프리셋이 켜져 있을 때 MinSize, MaxSize 가 있어도 `size:` 필터가 최종 쿼리에 결합되지 않음을 검증.
  2. `Test_UserSpecified_PathConstraint_NotWrappedByFolder`: 사용자가 직접 `path:P:\...` 형태로 쿼리를 입력했을 때 `folder:path:` 이중 래핑 없이 `path:P:\...` 그대로 원본 보존됨을 검증.
  3. `Test_GeneralSearchWord_ScopeAndFolderWrapped`: 일반 검색어 `BEST`는 `<folder:BEST | folder:path:BEST>`와 같이 정확히 수식어 조합이 일어남을 검증.
  4. `Test_RegexOption_Applies_RegexModifier_To_GeneralWords`: 정규식 옵션을 켰을 때, 일반 단어 토큰 `1[2-9]分` 등이 `regex:1[2-9]分` 과 같이 결합되어 쿼리가 빌드되는지 검증.
- 명령 실행: `dotnet test` (전체 테스트 100% 통과 검증)

### Manual Verification (수동 검증)
- 빌드 정상 완수를 검증하기 위해 디버그 배치 파일(`build-debug.bat`)을 실행하여 컴파일 오류나 경고가 유발되지 않는지 확인.

---

## 8. Capitalization Plan (자산화 계획)

- 수정 완료 후 발견된 팩트 및 개선 사항을 `docs/memories/MEMORY.md`에 지식 자산으로 등록.
- 신규 계획서 작성 및 소스코드 수정 내용을 `docs/memories/overview.md`에 동기화.

---

## 9. Request for Approval (승인 요청)

- **95% 확신 근거**: "대소문자 구분" 및 "전체 단어 일치"가 보이지 않던 정상적인 이유와 "정규식 검색" 시 쿼리 전체가 정규식으로 파싱되어 0개의 결과를 뱉던 치명적 결함을 Everything 엔진 관점에서 정교하게 밝혀내어, `regex:` 토큰 개별 적용이라는 가장 안전하고 이상적인 정답을 도출하였습니다.
- **피드백 요청**: 위의 추가된 설계 방향과 제약 조건 예외 처리에 동의하신다면 승인해 주시기 바랍니다.
