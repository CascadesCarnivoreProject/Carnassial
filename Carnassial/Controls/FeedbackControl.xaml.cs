using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace Carnassial.Controls
{
    /// <summary>
    /// Interaction logic for the feedback control.
    /// </summary>
    public partial class FeedbackControl : UserControl
    {
        public int ShowProgress
        {
            set { this.progressPB.Value = value; }
        }

        public string ShowMessage
        {
            set { this.messageLbl.Content = value; }
        }

        public ImageSource Image
        {
            set { this.imageImg.Source = value; }
        }

        public FeedbackControl()
        {
            this.InitializeComponent();
            this.ShowMessage = String.Empty;
            this.Image = null;
            this.ShowProgress = 0;
        }
    }
}
