using Carnassial.Util;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;

namespace Carnassial.Dialog
{
    public partial class TemplateSynchronization : Window
    {
        public TemplateSynchronization(List<string> errors, Window owner)
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
            // if the user dismisses the dialog by clicking its close button neither button callback is invoked and DialogResult retains its default value of false
            this.DialogResult = false;
        }

        private void UseOldTemplate_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }
    }
}
