# 임시 계획서: 빌드 배치 파일(bat) 비대화형(Non-Interactive) 모드 지원 및 exit code 전달 개선

본 계획서는 디버그 및 릴리즈 빌드용 배치 파일(`build-debug.bat`, `build-release.bat`)이 AI 에이전트 등 자동화 도구에 의해 구동될 때, `pause`에 걸려 무한 대기하는 현상을 해결하고 빌드 성공 여부에 따른 올바른 `exit code`를 반환하도록 하기 위한 보완 설계서입니다.

---

## 1. 요구사항 (Requirements)

- **비대화형 실행 지원**:
  - 배치 파일 실행 시 `--non-interactive` 인자가 전달되면 `pause` (키보드 대기) 명령을 건너뛰고 즉시 종료 처리.
- **올바른 exit code (ERRORLEVEL) 반환**:
  - `dotnet build` 수행 후 생성된 에러 레벨(`%ERRORLEVEL%`)을 환경 변수로 확보하여 최종 `exit /b` 시 이를 정확하게 반환.
  - 이를 통해 자동화 도구 및 AI 에이전트가 빌드의 실제 성공 여부를 감지할 수 있도록 보완.

---

## 2. 상세 구현 계획 (Implementation Plan)

### 단계 1: build-debug.bat 수정 (`build-debug.bat` [MODIFY])
- `dotnet build` 실행 후 `set BUILD_ERR=%ERRORLEVEL%` 로 빌드 결과 저장.
- 인자 `%1`이 `"--non-interactive"` 일 경우 `pause` 없이 `exit /b %BUILD_ERR%` 로 즉각 리턴 종료.
- 일반 실행 시에는 기존처럼 `pause` 후 `exit /b %BUILD_ERR%` 반환.

### 단계 2: build-release.bat 수정 (`build-release.bat` [MODIFY])
- 위 단계 1과 동일한 `exit code` 제어 및 `--non-interactive` 조건 분기 반영.

---

## 3. 검증 계획 (Verification Plan)

### 수동 및 자동 검증
1. `--non-interactive` 인자 없이 수동으로 배치 파일을 실행하여, 빌드 종료 후 `pause` 상태에서 키보드 대기가 정상적으로 뜨는지 확인.
2. 에이전트 쉘 명령어로 `cmd /c build-debug.bat --non-interactive`를 실행하여 무한 루프에 걸리지 않고 빌드 출력 로그가 표시된 뒤 즉시 프롬프트가 반환(자동 종료)되는지 검증.
