using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace EverythingFastAlias.Converters
{
    public static class HighlightBehavior
    {
        public static readonly DependencyProperty OriginalTextProperty =
            DependencyProperty.RegisterAttached(
                "OriginalText",
                typeof(string),
                typeof(HighlightBehavior),
                new PropertyMetadata(string.Empty, OnTextChanged));

        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.RegisterAttached(
                "SearchText",
                typeof(string),
                typeof(HighlightBehavior),
                new PropertyMetadata(string.Empty, OnTextChanged));

        public static string GetOriginalText(DependencyObject obj) => (string)obj.GetValue(OriginalTextProperty);
        public static void SetOriginalText(DependencyObject obj, string value) => obj.SetValue(OriginalTextProperty, value);

        public static string GetSearchText(DependencyObject obj) => (string)obj.GetValue(SearchTextProperty);
        public static void SetSearchText(DependencyObject obj, string value) => obj.SetValue(SearchTextProperty, value);

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBlock textBlock) return;

            string originalText = GetOriginalText(textBlock) ?? string.Empty;
            string searchText = GetSearchText(textBlock) ?? string.Empty;

            textBlock.Inlines.Clear();

            if (string.IsNullOrEmpty(searchText) || string.IsNullOrEmpty(originalText))
            {
                textBlock.Text = originalText;
                return;
            }

            int index = 0;
            while (index < originalText.Length)
            {
                int matchIndex = originalText.IndexOf(searchText, index, StringComparison.OrdinalIgnoreCase);
                if (matchIndex < 0)
                {
                    textBlock.Inlines.Add(new Run(originalText.Substring(index)));
                    break;
                }

                if (matchIndex > index)
                {
                    textBlock.Inlines.Add(new Run(originalText.Substring(index, matchIndex - index)));
                }

                string matchedText = originalText.Substring(matchIndex, searchText.Length);
                var run = new Run(matchedText)
                {
                    Background = System.Windows.Media.Brushes.Yellow,
                    Foreground = System.Windows.Media.Brushes.Black,
                    FontWeight = FontWeights.Bold
                };
                textBlock.Inlines.Add(run);

                index = matchIndex + searchText.Length;
            }
        }
    }
}
