using System.Windows;

namespace TimelapseTemplateEditor
{
    /// <summary>
    /// Interaction logic for DlgMessageBox.xaml
    /// </summary>
    public partial class DlgMessageBox : Window
    {
        public DlgMessageBox(string title, string problem, string reason, string solution, string result, string hint)
        {
            InitializeComponent();

            // Set the titles
            txtBlockTitle.Text = title;
            this.Title = title;

            this.tbProblemText.Text = problem;

            if (reason == "") this.myGrid.RowDefinitions[1].Height = new GridLength(0);
            else this.tbReasonText.Text = reason;

            if (solution == "") this.myGrid.RowDefinitions[3].Height = new GridLength(0);
            else this.tbSolutionText.Text = solution;

            if (result == "") this.myGrid.RowDefinitions[4].Height = new GridLength(0);
            else this.tbResultText.Text = result;

            if (hint == "") this.myGrid.RowDefinitions[5].Height = new GridLength(0);
            else this.tbHintText.Text = hint;
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
