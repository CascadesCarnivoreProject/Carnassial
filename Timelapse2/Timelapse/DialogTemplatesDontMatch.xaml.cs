using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using Timelapse.Util;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for the mismatched templates dialog.
    /// </summary>
    public partial class DialogTemplatesDontMatch : Window
    {
        public DialogTemplatesDontMatch(List<string> errors)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);

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
