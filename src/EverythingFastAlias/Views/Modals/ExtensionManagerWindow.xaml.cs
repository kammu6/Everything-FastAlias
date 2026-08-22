using System;
using System.Windows;
using EverythingFastAlias.ViewModels;

namespace EverythingFastAlias.Views.Modals
{
    public partial class ExtensionManagerWindow : Window
    {
        private ExtensionManagerViewModel? VM => DataContext as ExtensionManagerViewModel;

        public ExtensionManagerWindow()
        {
            InitializeComponent();
            if (VM != null)
            {
                VM.RequestClose += () => this.Close();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
