using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogTemplatesDontMatch.xaml
    /// </summary>
    public partial class DialogTemplatesDontMatch : Window
    {
        public DialogTemplatesDontMatch(List<string> errors)
        {
            this.InitializeComponent();
            foreach (string error in errors)
            {
                this.TextBlockDetails.Inlines.Add(new Run { Text = "     " + error });
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void UseOriginalTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
