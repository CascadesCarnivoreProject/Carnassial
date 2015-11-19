using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
