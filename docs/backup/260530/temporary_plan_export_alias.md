# 스마트 매핑 사전 내보내기(Export) 기능 구현 계획 (Temporary Plan)

스마트 매핑 사전 관리자(Alias Manager)에서 사용자가 편집했거나 데이터베이스에 보관 중인 단어 동의어 매핑 테이블을 CSV 파일 포맷으로 PC에 저장할 수 있도록 내보내기 버튼 및 로직을 추가 구현하기 위한 임시 계획입니다.

## 요구사항
- `AliasManagerWindow`의 상단 제어바에 "내보내기 (.csv)" 버튼을 생성합니다.
- 사용자가 이 버튼을 누르면 PC에 저장할 파일 경로를 선택하는 대화상자(SaveFileDialog)가 뜹니다.
- 선택된 파일 경로에 데이터그리드의 모든 매핑 데이터들을 쉼표(,) 및 큰따옴표 이스케이프 처리가 가미된 올바른 CSV 파일 포맷으로 출력하여 저장합니다.

## 구현 상세 및 변경 내용
1. **[MODIFY] [ExcelService.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Services/ExcelService.cs)**:
   - `ExportToCsv(string filePath, List<(string Keyword, string Words)> data)` 메서드 구현 추가
   - CSV 인라인 콤마와 큰따옴표를 정밀 방어하기 위한 `EscapeCsv` 이스케이프 유틸리티 메서드 추가
2. **[MODIFY] [AliasManagerViewModel.cs](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/ViewModels/AliasManagerViewModel.cs)**:
   - `ExportExcelCommand` ICommand 추가 및 바인딩
   - `ExportExcel()` 메서드 구현 (SaveFileDialog 활용 및 ExcelService 호출)
3. **[MODIFY] [AliasManagerWindow.xaml](file:///d:/3_Code/3_Apps/43_Search-Edit/Everything검색기/src/EverythingFastAlias/Views/Modals/AliasManagerWindow.xaml)**:
   - 상단 버튼 바에 "내보내기 (.csv)" 버튼 추가 및 `ExportExcelCommand` 바인딩 지정

## 검증 계획
- 빌드 결과 확인 (`dotnet build`)
- 관리자 화면 실행 후 임의의 데이터를 기입하고 "내보내기" 버튼을 통해 정상적으로 CSV 파일이 생성되는지 검증
