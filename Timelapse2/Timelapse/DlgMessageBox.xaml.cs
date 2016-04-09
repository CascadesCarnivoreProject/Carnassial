using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DlgMessageBox.xaml
    /// </summary>
    public partial class DlgMessageBox : Window
    {
        #region Properties
        private MessageBoxImage mIconType = MessageBoxImage.Exclamation;
        public MessageBoxImage IconType
        {
            get { return mIconType; }
            set { mIconType = value; SetIconType(); }
        }

        public MessageBoxButton mButtonType = MessageBoxButton.OK;
        public MessageBoxButton ButtonType
        {
            get { return mButtonType; }
            set { mButtonType = value; SetButtonType(); }
        }

        // Property: the Text of the Title Message
        private string mMessageTitle = "Message Box";
        public string MessageTitle 
        {
            get { return mMessageTitle; }
            set
            {
                mMessageTitle = value;
                txtBlockTitle.Text = mMessageTitle;
                Title = mMessageTitle;
                SetFieldVisibility();
            }
        }
        private string mMessageProblem = "";
        public string MessageProblem
        {
            get { return mMessageProblem; }
            set
            {
                mMessageProblem = value;
                tbProblemText.Text = mMessageProblem;
                SetFieldVisibility();
            }
        }

        private string mMessageReason = "";
        public string MessageReason
        {
            get { return mMessageReason; }
            set
            {
                mMessageReason = value;
                tbReasonText.Text = mMessageReason;
                SetFieldVisibility();
            }
        }

        private string mMessageSolution = "";
        public string MessageSolution
        {
            get { return mMessageSolution; }
            set
            {
                mMessageSolution = value;
                this.tbSolutionText.Text = mMessageSolution;
                SetFieldVisibility();
            }
        }

        private string mMessageResult = "";
            public string MessageResult
        {
            get { return mMessageResult; }
            set
            {
                mMessageResult = value;
                this.tbResultText.Text = mMessageResult;
                SetFieldVisibility(); }
        }

        private string mMessageHint = "";
        public string MessageHint
        {
            get { return mMessageHint; }
            set
            {
                mMessageHint = value;
                this.tbHintText.Text = mMessageHint;
                SetFieldVisibility();
            }
        }
        #endregion 

        public DlgMessageBox()
        {
            InitializeComponent();
            this.Title = "Message";
            SetFieldVisibility();
        }

        private void SetFieldVisibility ()
        {
            this.myGrid.RowDefinitions[1].Height = (MessageProblem == "") ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[2].Height = (MessageReason == "") ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[3].Height = (MessageSolution == "") ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[4].Height = (MessageResult == "") ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[5].Height = (MessageHint == "") ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
        }
        private void SetIconType ()
        {
            switch (this.IconType)
            {
                case MessageBoxImage.Question:
                    lblIconType.Content = "?";
                    break;
                case MessageBoxImage.Exclamation:
                    lblIconType.Content = "!";
                    break;
                case MessageBoxImage.None:
                case MessageBoxImage.Information:
                    lblIconType.Content = "i";
                    break;
                case MessageBoxImage.Error:
                    Run run = new Run(); // Create a symbol of a stopped hand
                    run.FontFamily = new FontFamily("Wingdings 2");
                    run.Text = "\u004e"; 
                    lblIconType.Content = run;
                    break;
                default:
                    return;
            }
        }

        private void SetButtonType()
        {
            switch (this.ButtonType)
            {
                case MessageBoxButton.OK:
                    this.OkButton.Content = "Okay";
                    this.OkButton.IsDefault = true;
                    this.OkButton.IsCancel = true;
                    this.CancelButton.IsCancel = false;
                    this.CancelButton.IsEnabled = false;
                    this.CancelButton.Visibility = Visibility.Collapsed;

                    break;
                case MessageBoxButton.OKCancel:
                    this.OkButton.Content = "Okay";
                    this.OkButton.IsCancel = false;
                    this.CancelButton.IsCancel = true;
                    this.CancelButton.Content = "Cancel";
                    this.CancelButton.IsEnabled = true;
                    this.CancelButton.IsDefault = true;
                    this.CancelButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNo:
                    this.OkButton.Content = "Yes";
                    this.CancelButton.Content = "No";
                    this.OkButton.IsCancel = false;
                    this.CancelButton.IsCancel = true;
                    this.CancelButton.IsEnabled = true;
                    this.CancelButton.IsDefault = true;
                    this.CancelButton.Visibility = Visibility.Visible;
                    break;
                default:
                    return;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            if (this.Left < 10 || this.Top < 10)
            {
                this.Left = this.Owner.Left + (this.Owner.Width - this.ActualWidth) / 2; //Center it horizontally
                this.Top = this.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; 
        }


    }
}
