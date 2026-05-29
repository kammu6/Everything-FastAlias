using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EverythingFastAlias.Models;
using EverythingFastAlias.Services;
using Microsoft.Win32;

namespace EverythingFastAlias.ViewModels
{
    public class AliasManagerViewModel : ObservableObject
    {
        private ObservableCollection<AliasMapping> _mappings = new();
        public ObservableCollection<AliasMapping> Mappings
        {
            get => _mappings;
            set => SetProperty(ref _mappings, value);
        }

        private AliasMapping? _selectedMapping;
        public AliasMapping? SelectedMapping
        {
            get => _selectedMapping;
            set => SetProperty(ref _selectedMapping, value);
        }

        private string _statusMessage = "정상";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ImportExcelCommand { get; }
        public ICommand ExportTemplateCommand { get; }

        public AliasManagerViewModel()
        {
            LoadCommand = new RelayCommand(LoadMappings);
            AddCommand = new RelayCommand(AddMapping);
            DeleteCommand = new RelayCommand(DeleteMapping);
            SaveCommand = new RelayCommand(SaveSelectedMapping);
            ImportExcelCommand = new RelayCommand(ImportExcel);
            ExportTemplateCommand = new RelayCommand(ExportTemplate);

            LoadMappings();
        }

        public void LoadMappings()
        {
            try
            {
                var dbList = DatabaseService.Instance.GetAllMappings();
                Mappings.Clear();
                foreach (var kvp in dbList)
                {
                    Mappings.Add(new AliasMapping(kvp.Key, kvp.Value));
                }
                StatusMessage = $"총 {Mappings.Count}개의 매핑 규칙을 로드했습니다.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"로드 실패: {ex.Message}";
            }
        }

        private void AddMapping()
        {
            var newMapping = new AliasMapping("새키워드", "동의어1;동의어2");
            Mappings.Add(newMapping);
            SelectedMapping = newMapping;
            StatusMessage = "새 행을 추가했습니다. 저장 버튼을 눌러 확정하세요.";
        }

        private void DeleteMapping()
        {
            if (SelectedMapping == null)
            {
                StatusMessage = "삭제할 행을 선택하세요.";
                return;
            }

            try
            {
                var result = MessageBox.Show(
                    $"'{SelectedMapping.Keyword}' 매핑을 삭제하시겠습니까?", 
                    "삭제 확인", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Yes)
                {
                    DatabaseService.Instance.DeleteMapping(SelectedMapping.Keyword);
                    Mappings.Remove(SelectedMapping);
                    SelectedMapping = null;
                    StatusMessage = "매핑이 삭제되었습니다.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"삭제 실패: {ex.Message}";
            }
        }

        private void SaveSelectedMapping()
        {
            if (SelectedMapping == null) return;

            if (string.IsNullOrWhiteSpace(SelectedMapping.Keyword))
            {
                MessageBox.Show("원본 단어(Keyword)는 비워둘 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                DatabaseService.Instance.SaveMapping(SelectedMapping.Keyword, SelectedMapping.Words);
                LoadMappings(); // 리프레시 및 캐싱 강제 동기화
                StatusMessage = "성공적으로 저장 및 갱신되었습니다.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"저장 실패: {ex.Message}";
            }
        }

        private void ImportExcel()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Excel 파일 (*.xlsx)|*.xlsx|CSV 파일 (*.csv)|*.csv",
                Title = "매핑 사전 가져오기"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    StatusMessage = "엑셀 가져오는 중...";
                    var list = ExcelService.ImportExcel(openFileDialog.FileName);
                    
                    if (list.Count == 0)
                    {
                        MessageBox.Show("가져올 유효한 데이터 행이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                        StatusMessage = "유효 데이터 없음";
                        return;
                    }

                    // SQLite Transaction 기반 벌크 로드
                    DatabaseService.Instance.SaveBulk(list);
                    LoadMappings();

                    MessageBox.Show(
                        $"총 {list.Count}개의 매핑이 성공적으로 대량 업로드 및 갱신되었습니다.", 
                        "성공", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information
                    );
                    StatusMessage = $"{list.Count}개 대량 업로드 완료";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"가져오기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusMessage = $"엑셀 임포트 에러: {ex.Message}";
                }
            }
        }

        private void ExportTemplate()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV 파일 (*.csv)|*.csv",
                FileName = "FastAlias_Import_Template.csv",
                Title = "양식 다운로드"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    ExcelService.GenerateTemplate(saveFileDialog.FileName);
                    MessageBox.Show("템플릿 양식이 저장되었습니다.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                    StatusMessage = "템플릿 양식 내보내기 완료";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"템플릿 내보내기 실패: {ex.Message}";
                }
            }
        }
    }
}
