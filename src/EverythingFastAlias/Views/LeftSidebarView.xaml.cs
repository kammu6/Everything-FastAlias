using System.Windows;
using System.Windows.Controls;
using EverythingFastAlias.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

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
                // 크기 리셋 후 즉시 검색 방지
            }
        }
    }
}
