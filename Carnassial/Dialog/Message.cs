using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;

namespace Carnassial.Dialog
{
    public class Message
    {
        public MessageBoxButton Buttons { get; set; }
        public bool DisplayDontShowAgain { get; set; }
        public bool DisplayHideExplanation { get; set; }
        public List<Inline> Hint { get; set; }
        public MessageBoxImage Image { get; set; }
        public List<Inline> Problem { get; set; }
        public List<Inline> Reason { get; set; }
        public List<Inline> Result { get; set; }
        public List<Inline> Solution { get; set; }
        public string? Title { get; set; }
        public List<Inline> What { get; set; }
        public string? WindowTitle { get; set; }

        public Message()
        {
            this.Buttons = MessageBoxButton.OKCancel;
            this.DisplayDontShowAgain = false;
            this.DisplayHideExplanation = false;
            this.Hint = [];
            this.Image = MessageBoxImage.Exclamation;
            this.Problem = [];
            this.Reason = [];
            this.Result = [];
            this.Solution = [];
            this.Title = null;
            this.What = [];
            this.WindowTitle = null;
        }
    }
}
