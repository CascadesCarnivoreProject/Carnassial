using Carnassial.Util;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;

namespace Carnassial.Dialog
{
    /// <summary>
    /// Interaction logic for the mismatched templates dialog.
    /// </summary>
    public partial class TemplatesDontMatch : Window
    {
        public TemplatesDontMatch(List<string> errors, Window owner)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);

            this.Owner = owner;
            foreach (string error in errors)
            {
                this.TextBlockDetails.Inlines.Add(new Run { Text = "     " + error });
            }
        }

        private void ExitProgram_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void UseOldTemplate_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
