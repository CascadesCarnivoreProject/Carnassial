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

namespace TimelapseTemplateEditor
{
    /// <summary>
    /// Interaction logic for DlgAboutTImelapseEditor.xaml
    /// </summary>
    public partial class DlgAboutTimelapseEditor : Window
    {
        // TO DO: The Version string is not used. Can delete all references to this argument.
        public DlgAboutTimelapseEditor()
        {
            InitializeComponent();
            Version curVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Version.Text = curVersion.ToString();
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void BtnCheckForUpdate_Click(object sender, RoutedEventArgs e)
        {
            CheckForUpdate.GetAndParseVersion(this, true);
        }
    }
}
