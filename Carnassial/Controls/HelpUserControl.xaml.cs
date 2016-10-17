using Carnassial.Util;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Carnassial.Controls
{
    public partial class HelpUserControl : UserControl
    {
        public HelpUserControl()
        {
            this.InitializeComponent();
            this.TutorialLink.NavigateUri = CarnassialConfigurationSettings.GetTutorialBrowserAddress();
            this.TutorialLink.ToolTip = this.TutorialLink.NavigateUri.AbsoluteUri;
        }

        private void TutorialLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (e.Uri != null)
            {
                Process.Start(e.Uri.AbsoluteUri);
                e.Handled = true;
            }
        }
    }
}
