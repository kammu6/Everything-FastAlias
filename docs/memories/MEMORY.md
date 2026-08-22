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

### 2026-08-22 (파일 확장자 하드코딩 일원화, 확장자 관리자 모달 및 Everything 쿼리 변환기 단일화 리팩토링)

- 🎯
  - 소스코드 전반에 파편화되어 있던 미디어 카테고리별 확장자 하드코딩을 제거하고, 40개 동영상 확장자를 포함한 7대 카테고리(영상, 음악, 사진, 문서, 실행, 압축, 코드) 표준 상수를 일원화하며, 도구(T) 메뉴에 확장자 관리자 모달을 추가하고 좌측 패널 확장자 필터와 Everything 쿼리 생성을 간결하게 단일화하는 과제.

- ✅
  - 1. FileExtensionConstants.cs를 신설하여 7개 카테고리 기본 확장자 및 enriched 목록 정의.
2. ExtensionSettingsService를 구현하여 SQLite AppSettings 테이블과 연동, 사용자 커스텀 확장자 영속화 및 카테고리별/전체 기본값 복원(Reset) 로직 중앙화.
3. ExtensionManagerWindow.xaml 및 ViewModel을 구현하여 도구(T) 메뉴에 연결.
4. LeftSidebarView.xaml의 '커스텀 확장자 필터'를 '확장자 필터'로 명칭 변경하고, 프리셋 토글 시 확장자 필터 텍스트박스에 실시간 동기화하여 사용자가 직접 미세조정 가능하게 개선.
5. QueryTransformer.cs 내 하드코딩 switch-case를 전면 제거하고, CustomExtensions 단일 필드만을 파싱하여 <file:<ext:...>> Everything 쿼리를 깔끔하게 생성.

- ❌
  - 1. [시도했던 접근]: QueryTransformer 내부에서 MediaPresets switch-case와 CustomExtensions를 각각 개별 Everything ext 쿼리로 빌드하여 OR 연산자로 병합하던 기존 방식.
2. [실패/한계 원인]: 하드코딩된 확장자 목록과 UI에서 입력한 커스텀 확장자가 파편화되어 불필요하게 긴 중복 쿼리(<file:<ext:...>> | <file:<ext:...>>)가 생성되고, 코드베이스 유지보수성과 확장성이 저하됨.
3. [차단 효과]: 프리셋 토글 시 UI 텍스트박스로 확장자 목록을 단일 투영하고 쿼리 변환기는 해당 텍스트만 처리하도록 역할을 분리하여, 쿼리 간결화 및 코드 중복 완벽 제거.

### 2026-08-22 (WPF Run.Text의 BindsTwoWayByDefault로 인한 읽기전용 속성 TwoWay 바인딩 예외 방지)

- 🎯
  - ExtensionManagerWindow.xaml 모달 템플릿 내부에서 TextBlock의 자식 요소인 <Run Text="{Binding DefaultExtensions}"/> 바인딩 시 모달 오픈 시점에 InvalidOperationException XamlParseException 발생.

- ✅
  - 1. WPF의 Run.Text DependencyProperty는 BindsTwoWayByDefault 메타데이터가 true로 설정되어 있어 읽기 전용 get 속성에 바인딩 시 TwoWay 역방향 업데이트를 시도하다가 실패함.
2. XAML에서 <Run Text="{Binding DefaultExtensions, Mode=OneWay}"/>로 명시적 OneWay 바인딩을 선언하고, ExtensionCategoryItem 모델의 DefaultExtensions 속성에 getter/setter를 모두 제공하여 런타임 바인딩 충돌을 완벽 차단.

- ❌
  - 1. [시도했던 접근]: TextBlock 내부의 <Run Text="{Binding ReadOnlyProp}"/>에 바인딩 모드를 생략하고 기본값으로 둔 채 모달을 띄움.
2. [실패 원인]: TextBlock.Text와 달리 Run.Text는 기본 바인딩 모드가 TwoWay이므로 읽기 전용 프로퍼티와 결합 시 런타임 XamlParseException 크래시 발생.
3. [차단 효과]: Run 요소에 바인딩할 때는 반드시 Mode=OneWay를 명시하거나 TextBlock.Text의 StringFormat/Inlines를 단방향으로 구성.
