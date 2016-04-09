using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DlgTemplatesDontMatch.xaml
    /// </summary>
    public partial class DlgTemplatesDontMatch : Window
    {
        public DlgTemplatesDontMatch(List<string> errors)
        {
            InitializeComponent();
            foreach (string error in errors)
            {
                this.TextBlockDetails.Inlines.Add(new Run { Text = "     " + error });
            }
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void useOriginalTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
