using CommunityToolkit.Mvvm.ComponentModel;

namespace EverythingFastAlias.Models
{
    public class AliasMapping : ObservableObject
    {
        private string _keyword = string.Empty;
        public string Keyword
        {
            get => _keyword;
            set => SetProperty(ref _keyword, value);
        }

        private string _words = string.Empty;
        public string Words
        {
            get => _words;
            set => SetProperty(ref _words, value);
        }

        public AliasMapping() { }

        public AliasMapping(string keyword, string words)
        {
            Keyword = keyword;
            Words = words;
        }
    }
}
