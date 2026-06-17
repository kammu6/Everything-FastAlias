using System.Windows;
using System.Windows.Controls;
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
                vm.RequestEditMapping += Vm_RequestEditMapping;
            }
        }

        private void Vm_RequestScrollIntoView(Models.AliasMapping target)
        {
            if (MappingDataGrid != null)
            {
                MappingDataGrid.ScrollIntoView(target);
            }
        }

        private void Vm_RequestEditMapping(Models.AliasMapping target)
        {
            if (MappingDataGrid != null)
            {
                MappingDataGrid.UpdateLayout();
                MappingDataGrid.ScrollIntoView(target);
                MappingDataGrid.SelectedItem = target;
                MappingDataGrid.Focus();

                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new System.Action(() =>
                {
                    var cellInfo = new System.Windows.Controls.DataGridCellInfo(target, MappingDataGrid.Columns[0]);
                    MappingDataGrid.CurrentCell = cellInfo;
                    MappingDataGrid.BeginEdit();
                }));
            }
        }

        private void TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new System.Action(() =>
                {
                    tb.Focus();
                    tb.SelectAll();
                }));
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new System.Action(() =>
                {
                    tb.SelectAll();
                }));
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
