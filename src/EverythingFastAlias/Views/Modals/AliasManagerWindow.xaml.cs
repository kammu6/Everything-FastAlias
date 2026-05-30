using System.Windows;
using EverythingFastAlias.ViewModels;
using EverythingFastAlias.Models;

namespace EverythingFastAlias.Views.Modals
{
    public partial class AliasManagerWindow : Window
    {
        public AliasManagerWindow()
        {
            InitializeComponent();
            Loaded += AliasManagerWindow_Loaded;
        }

        private void AliasManagerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AliasManagerViewModel vm)
            {
                vm.RequestScrollIntoView += Vm_RequestScrollIntoView;
            }
        }

        private void Vm_RequestScrollIntoView(Models.AliasMapping target)
        {
            // DataGrid가 아직 완전히 렌더링되거나 로드되지 않았을 경우를 대비해 스크롤 처리
            if (MappingDataGrid != null)
            {
                MappingDataGrid.ScrollIntoView(target);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (MappingDataGrid != null)
            {
                MappingDataGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
            }
        }
    }
}
