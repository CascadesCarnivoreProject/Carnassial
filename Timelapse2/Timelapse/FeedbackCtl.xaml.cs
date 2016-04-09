using System.Windows.Controls;
using System.Windows.Media;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for FeedbackCtl.xaml
    /// </summary>
    public partial class FeedbackCtl : UserControl
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
            set { this.imageImg.Source = (ImageSource) value; }
        }
        public FeedbackCtl()
        {
            InitializeComponent();
            this.ShowMessage = "";
            this.ShowImage = null;
            this.ShowProgress = 0;
        }
    }
}
