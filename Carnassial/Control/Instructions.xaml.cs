using Carnassial.Util;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace Carnassial.Control
{
    public partial class Instructions : UserControl
    {
        public Instructions()
        {
            this.InitializeComponent();

            if (this.ScrollViewer.Document.Tag == null)
            {
                Hyperlink tutorialLink = (Hyperlink)LogicalTreeHelper.FindLogicalNode(this.ScrollViewer.Document, Constant.DialogControlName.InstructionsTutorialLink);
                tutorialLink.NavigateUri = CarnassialConfigurationSettings.GetTutorialBrowserAddress();
                tutorialLink.ToolTip = tutorialLink.NavigateUri.AbsoluteUri;

                this.ScrollViewer.Document.Tag = true;
            }
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
