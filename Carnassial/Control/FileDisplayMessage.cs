using System.Windows.Controls;
using System.Windows.Documents;

namespace Carnassial.Control
{
    public class FileDisplayMessage
    {
        public string Detail { get; private init; }
        public string Header { get; private init; }
        public string Midsection { get; private init; }
        public int MidsectionFontSize { get; private init; }

        public FileDisplayMessage(string header, string midsection, int midsectionFontSize, string detail)
        {
            this.Detail = detail;
            this.Header = header;
            this.Midsection = midsection;
            this.MidsectionFontSize = midsectionFontSize;
        }

        public void SetSource(TextBlock messageBlock)
        {
            messageBlock.Inlines.Clear();

            messageBlock.Inlines.Add(this.Header);
            messageBlock.Inlines.Add(new LineBreak());
            messageBlock.Inlines.Add(new LineBreak());

            messageBlock.Inlines.Add(this.Midsection);
            messageBlock.Inlines.Add(new LineBreak());
            messageBlock.Inlines.Add(new LineBreak());

            messageBlock.Inlines.Add(this.Detail);
            messageBlock.Inlines.Add(new LineBreak());
            messageBlock.Inlines.Add(new LineBreak());
        }
    }
}
