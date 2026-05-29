using System.Windows;
using System.Windows.Controls;
using EverythingFastAlias.ViewModels;

namespace EverythingFastAlias.Views
{
    public partial class LeftSidebarView : UserControl
    {
        public LeftSidebarView()
        {
            InitializeComponent();
        }

        private void ResetSize_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SearchViewModel vm)
            {
                vm.MinSize = null;
                vm.MaxSize = null;
                vm.MinSizeText = string.Empty;
                vm.MaxSizeText = string.Empty;
                vm.ExecuteSearch();
            }
        }
    }
}
