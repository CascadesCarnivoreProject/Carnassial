using System.Windows;

namespace Timelapse
{
    /// <summary>
    /// Create an  window that contains the help user control
    /// We do this because the help user control is used in two places: in the main window (at startup) or popped up via a help menu
    /// </summary>
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            this.InitializeComponent();
        }
    }
}
