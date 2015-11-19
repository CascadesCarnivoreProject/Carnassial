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
using System.Windows.Shapes;

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
