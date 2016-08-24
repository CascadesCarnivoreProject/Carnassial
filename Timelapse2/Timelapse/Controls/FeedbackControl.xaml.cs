using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace Timelapse.Controls
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

        public ImageSource ShowImage
        {
            set { this.imageImg.Source = value; }
        }

        public FeedbackControl()
        {
            this.InitializeComponent();
            this.ShowMessage = String.Empty;
            this.ShowImage = null;
            this.ShowProgress = 0;
        }
    }
}
