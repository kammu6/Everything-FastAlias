# 구현 계획서: 확장자 하드코딩 일원화, 확장자 관리자 모달 및 필터 연동 리팩토링

## 1. 개요 및 요구사항

### 1.1 목적
코드베이스 전반에 흩어져 있던 파일 확장자 하드코딩을 제거하고, 단일 상수 클래스(`FileExtensionConstants.cs`)로 일원화 및 보강합니다. 또한 `도구(T)` 메뉴에 **확장자 관리자(Extension Manager)** 모달을 추가하여 카테고리별/전체 기본값 복원 기능을 제공하고, 좌측 패널의 미디어 토글과 `확장자 필터` 텍스트박스를 깔끔하게 단일 연동하여 사용자가 직관적으로 미세 조정할 수 있도록 리팩토링합니다.

### 1.2 사용자 요구사항 및 인수 조건
1. **임시 점검 스크립트 작성 및 진단**:
   - `scripts/tests/extensions/chk_hardcoded_extensions.py`를 통해 소스코드 내 하드코딩된 확장자 문자열 진단 완료 (총 29개 항목 식별).
2. **확장자 상수 일원화 및 보강 (`FileExtensionConstants.cs`)**:
   - 7개 카테고리(영상, 음악, 사진, 문서, 실행, 압축, 코드)의 기본 확장자를 상수로 일원화.
   - 사용자가 제공한 40개 동영상 확장자(`mp4;mkv;avi;wmv;flv;mov;mpg;mpeg;m4v;webm;vob;ogv;divx;ogm;m2ts;mts;tp;trp;3gp;3g2;asf;rm;rmvb;f4v;mxf;qt;m1v;m2v;mpv;mpe;wtv;dvr-ms;amv;k3g;skm;h264;hevc;bik;dav;ts`) 및 타 카테고리 확장자 대폭 보강.
3. **확장자 관리자 모달 구현 (`도구(T) -> 확장자 관리자...`)**:
   - `MainWindow.xaml`의 `도구(T)` 메뉴에 항목 추가.
   - `ExtensionManagerWindow.xaml` 및 `ExtensionManagerViewModel.cs` 구현.
   - 7개 카테고리 전체 목록 표시, 카테고리별 `기본값 복원` 버튼 및 `전체 기본값 복원` 버튼 필수 제공.
   - 수정된 확장자 설정은 SQLite `AppSettings` 테이블에 저장되어 영속화.
4. **좌측 패널 및 확장자 필터 연동 개선**:
   - `커스텀 확장자 필터` ➡️ `확장자 필터`로 명칭 변경.
   - 영상/음악 등 프리셋 토글 시 해당 카테고리의 확장자가 `확장자 필터` 입력란에 자동 표시되며, 사용자가 입력란에서 자유롭게 가감/수정 가능.
   - `전체` 선택 시 또는 프리셋 해제 시 입력란 초기화.
5. **Everything 쿼리 변환기(QueryTransformer) 개선**:
   - 기존 하드코딩 `switch-case`를 전면 제거하고, `확장자 필터`에 입력된 단일 확장자 목록만으로 깔끔하게 Everything 쿼리(`<file:<ext:...>>`) 생성.
6. **단위 테스트 및 무결성 검증**:
   - `QueryTransformerTest.cs` 업데이트 및 `dotnet test` 100% 통과.

---

## 2. 기술 스택

- **프레임워크**: C# .NET 9.0 (WPF)
- **UI / 테마**: ModernWPF (Windows 11 스타일 WinUI 테마)
- **MVVM 라이브러리**: CommunityToolkit.Mvvm (ObservableObject, RelayCommand)
- **데이터베이스**: Microsoft.Data.Sqlite (`AppSettings` 테이블 키-값 영속화)
- **테스트**: MSTest (Microsoft.VisualStudio.TestTools.UnitTesting)

---

## 3. 기술적 근거 및 설계 원칙

1. **단일 진실 공급원(SSOT) 기반 설정 관리 (확신도 95%)**:
   - 기본값은 `FileExtensionConstants.cs`에 정의하고, 사용자 변경값은 `AppSettings` 테이블에 `ExtensionPreset_{카테고리}` 키로 저장합니다.
   - `ExtensionSettingsService`를 통해 DB에 값이 있으면 커스텀 값을, 없으면 기본 상수를 반환하여 기본값 복원과 영속성을 완벽하게 보장합니다.
2. **단일 입력란 기반의 쿼리 정규화 (확신도 95%)**:
   - 기존에는 "하드코딩 프리셋 쿼리 + 커스텀 확장자 쿼리"가 결합되어 중복이나 복잡한 OR 절이 발생했습니다.
   - UI에서 프리셋 선택 시 `확장자 필터` 텍스트박스로 확장자가 즉시 투영되고, `QueryTransformer`는 해당 텍스트박스의 값 하나만 파싱하여 `<file:<ext:...>>`로 변환하므로 Everything 쿼리가 매우 간결하고 일관성 있게 생성됩니다.
3. **안전한 DB 스키마 재사용 (확신도 95%)**:
   - `AppSettings (SettingKey TEXT PRIMARY KEY, SettingValue TEXT)` 테이블이 이미 존재하므로 별도의 마이그레이션 없이 즉시 키 저장이 가능합니다.

---

## 4. 폴더 및 파일 구조

```text
src/EverythingFastAlias/
├── Config/
│   ├── AppConstants.cs                     # 시스템 공통 상수
│   ├── FileExtensionConstants.cs           # [신규] 7개 카테고리 기본 확장자 및 보강 목록 일원화
│   └── UIConstants.cs                      # 레거시 중복 확장자 정의 제거 및 정리
├── Models/
│   ├── ExtensionCategoryItem.cs            # [신규] 확장자 관리자 모달 바인딩용 행 모델
│   ├── SearchOptions.cs                    # 검색 조건 모델
│   └── SearchResultItem.cs
├── Services/
│   ├── DatabaseService.cs                  # SQLite 연결 싱글톤
│   ├── ExtensionSettingsService.cs         # [신규] 카테고리별 확장자 로드/저장/복원 전담 서비스
│   └── QueryTransformer.cs                 # [수정] 단일 확장자 필터 기반 간결한 쿼리 생성 리팩토링
├── ViewModels/
│   ├── ExtensionManagerViewModel.cs        # [신규] 확장자 관리자 모달 뷰모델
│   ├── MainWindowViewModel.cs              # [수정] 모달 오픈 커맨드 연결
│   ├── SearchViewModel.cs                  # [수정] 프리셋 토글 <-> 확장자 필터 텍스트 동기화
│   ├── SearchViewModel.Search.cs
│   └── SearchViewModel.Settings.cs         # [수정] 설정 복원 및 초기화 로직
├── Views/
│   ├── LeftSidebarView.xaml                # [수정] "확장자 필터" 라벨 및 플레이스홀더 변경
│   ├── MainWindow.xaml                     # [수정] 도구(T) -> 확장자 관리자 메뉴 항목 추가
│   ├── MainWindow.xaml.cs                  # [수정] 모달 창 표시 핸들러
│   └── Modals/
│       ├── AliasManagerWindow.xaml
│       └── ExtensionManagerWindow.xaml     # [신규] 확장자 관리자 ModernWPF 모달 UI
└── EverythingFastAlias.Tests/
    └── QueryTransformerTest.cs             # [수정] 신규 확장자 쿼리 생성 방식 테스트 케이스
```

---

## 5. 진단 및 검증 도구

- **점검 스크립트**: `scripts/tests/extensions/chk_hardcoded_extensions.py` (소스코드 내 하드코딩 확장자 검사)
- **단위 테스트 실행**: `dotnet test src/EverythingFastAlias.Tests/EverythingFastAlias.Tests.csproj`
- **빌드 검증**: `dotnet build src/EverythingFastAlias/EverythingFastAlias.csproj -c Debug`

---

## 6. 단계별 구현 계획

### Step 1: 확장자 상수 파일 생성 (`FileExtensionConstants.cs`) (난이도: ★☆☆☆☆)
- 7개 카테고리 정의 및 대폭 보강:
  - `Video (영상)`: `mp4;mkv;avi;wmv;flv;mov;mpg;mpeg;m4v;webm;vob;ogv;divx;ogm;m2ts;mts;tp;trp;3gp;3g2;asf;rm;rmvb;f4v;mxf;qt;m1v;m2v;mpv;mpe;wtv;dvr-ms;amv;k3g;skm;h264;hevc;bik;dav;ts`
  - `Audio (음악)`: `mp3;wav;flac;ogg;wma;m4a;aac;alac;aiff;ape;opus;mid;midi;mka;ac3;dts;ra;ram;amr;m4b;m4p`
  - `Picture (사진)`: `jpg;jpeg;jfif;png;gif;bmp;webp;tiff;tif;psd;ai;svg;ico;raw;cr2;nef;arw;dng;heic;heif;avif`
  - `Document (문서)`: `pdf;txt;hwp;hwpx;doc;docx;xls;xlsx;ppt;pptx;rtf;csv;tsv;odt;ods;odp;pages;numbers;key;epub;mobi;azw3;xps`
  - `Executable (실행)`: `exe;bat;cmd;msi;lnk;scr;sh;pyw;ps1;vbs;com;jar;appimage`
  - `Archive (압축)`: `zip;7z;rar;tar;gz;bz2;iso;alz;egg;xz;z;tgz;tbz2;cab;dmg;wim`
  - `Code (코드)`: `ts;tsx;js;jsx;json;java;py;pyw;cpp;c;h;hpp;cs;html;css;scss;less;go;rs;sh;md;yml;yaml;xml;sql;php;rb;kt;swift;lua;vue;svelte;dart`
- `GetDefault(category)`, `GetAllCategories()`, `GetAllDefaults()` 헬퍼 메서드 제공.
- `UIConstants.cs` 내 레거시 중복 정의 정리.

### Step 2: ExtensionSettingsService 및 모델 구현 (난이도: ★★☆☆☆)
- `ExtensionCategoryItem.cs`: 모달 UI 바인딩용 모델 (카테고리명, 현재 확장자, 기본 확장자, 개별 기본값 복원 커맨드).
- `ExtensionSettingsService.cs`:
  - `GetExtensions(string category)`: SQLite `AppSettings`에 저장된 커스텀 값 조회 (없으면 기본값 fallback).
  - `SaveExtensions(string category, string extensions)`: 커스텀 확장자 DB 저장.
  - `ResetCategory(string category)`: 특정 카테고리 커스텀 설정 삭제(기본값 복원).
  - `ResetAll()`: 전체 카테고리 기본값 일괄 복원.

### Step 3: 확장자 관리자 모달 구현 (UI & ViewModel) (난이도: ★★★☆☆)
- `ExtensionManagerViewModel.cs`:
  - 7개 카테고리 `ObservableCollection<ExtensionCategoryItem>` 로드.
  - `SaveCommand`, `ResetCategoryCommand`, `ResetAllCommand`, `CloseCommand` 제공.
- `ExtensionManagerWindow.xaml` & `.xaml.cs`:
  - ModernWPF 디자인 적용, 7개 카테고리 카드/리스트 뷰, 각 행별 `기본값 복원` 버튼 및 하단 `전체 기본값 복원` 버튼 배치.
- `MainWindow.xaml` 및 `MainWindowViewModel.cs`:
  - `도구(T)` 메뉴에 `확장자 관리자 (Extension Manager)...` 추가 및 다이얼로그 호출 연결.

### Step 4: 좌측 패널 및 QueryTransformer 리팩토링 (난이도: ★★★☆☆)
- `LeftSidebarView.xaml`:
  - `커스텀 확장자 필터` ➡️ `확장자 필터` 텍스트 변경.
- `SearchViewModel.cs` / `SearchViewModel.Settings.cs`:
  - 미디어 프리셋 토글 시, 활성화된 프리셋들의 확장자를 `ExtensionSettingsService`에서 취합하여 `CustomExtensions`에 자동 반영.
  - `전체` 클릭 시 또는 프리셋 전체 해제 시 `CustomExtensions` 공백 처리.
  - 사용자가 텍스트박스에서 직접 수정한 내용은 그대로 유지.
- `QueryTransformer.cs`:
  - 하드코딩된 `extPattern` switch-case 제거.
  - `options.CustomExtensions` 값만을 기반으로 단일 `<file:<ext:{extensions}>>` 생성.

### Step 5: 단위 테스트 및 전체 빌드 검증 (난이도: ★★☆☆☆)
- `QueryTransformerTest.cs` 테스트 케이스를 신규 확장자 및 쿼리 방식에 맞추어 업데이트.
- `dotnet test`로 전체 테스트 통과 확인.
- `chk_hardcoded_extensions.py`를 실행하여 `src/` 내 잔여 하드코딩 0건 검증.

---

## 7. 검증 계획

### 7.1 자동화 테스트
```powershell
# 1. 빌드 검증
dotnet build src/EverythingFastAlias/EverythingFastAlias.csproj -c Debug

# 2. MSTest 단위 테스트 실행
dotnet test src/EverythingFastAlias.Tests/EverythingFastAlias.Tests.csproj

# 3. 하드코딩 확장자 검사 스크립트 실행
python scripts/tests/extensions/chk_hardcoded_extensions.py
```

### 7.2 수동 UI 검증
1. 앱 실행 후 좌측 패널에서 "영상" 클릭 ➔ `확장자 필터`란에 40개 비디오 확장자 자동 입력 확인.
2. 텍스트박스 끝에 `;xyz` 추가 입력 후 검색 ➔ 하단 Everything 쿼리에 `<file:<ext:...;xyz>>` 정상 반영 확인.
3. `도구(T) -> 확장자 관리자` 메뉴 오픈 ➔ 7개 카테고리 및 확장자 표시 확인.
4. "영상" 확장자를 임의로 수정 후 [저장] ➔ DB 저장 및 좌측 패널 즉시 동기화 확인.
5. "기본값 복원" 및 "전체 기본값 복원" 클릭 ➔ 즉시 표준 확장자로 리셋되는지 확인.

---

## 8. 지식 자산화 계획

- `docs/memories/MEMORY.md`에 확장자 일원화 아키텍처, 프리셋-필터 동기화 메커니즘 및 안티패턴 기록.
- `docs/memories/overview.md`의 구조도 및 컴포넌트 목록 업데이트.
