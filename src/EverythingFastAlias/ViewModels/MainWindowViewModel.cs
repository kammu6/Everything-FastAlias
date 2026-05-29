using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EverythingFastAlias.ViewModels
{
    public class MainWindowViewModel : ObservableObject
    {
        public SearchViewModel SearchVM { get; }

        public ICommand OpenAliasManagerCommand { get; }
        public ICommand OpenHelpCommand { get; }

        public event Action? RequestOpenAliasManager;
        public event Action? RequestOpenHelp;

        public MainWindowViewModel()
        {
            SearchVM = new SearchViewModel();

            OpenAliasManagerCommand = new RelayCommand(() => RequestOpenAliasManager?.Invoke());
            OpenHelpCommand = new RelayCommand(() => RequestOpenHelp?.Invoke());
        }
    }
}
