using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogMessageBox.xaml
    /// </summary>
    public partial class DialogMessageBox : Window
    {
        private MessageBoxButton buttonType = MessageBoxButton.OK;

        #region Properties
        public MessageBoxImage IconType
        {
            get
            {
                return this.Message.IconType;
            }
            set
            {
                this.Message.IconType = value;
            }
        }

        public MessageBoxButton ButtonType
        {
            get
            {
                return this.buttonType;
            }
            set
            {
                this.buttonType = value;
                this.SetButtonType();
            }
        }

        // Property: the Text of the Title Message
        public string MessageTitle
        {
            get
            {
                return this.Message.MessageTitle;
            }
            set
            {
                this.Message.MessageTitle = value;
                // if the window title is empty, also set it to the MessageTitle
                if (String.IsNullOrWhiteSpace(this.WindowTitle))
                {
                    this.WindowTitle = value;
                }
            }
        }

        // Property: the Text of the Title Message
        public string WindowTitle
        {
            get
            {
                return this.Title;
            }
            set
            {
                this.Title = value;
            }
        }
        public string MessageWhat
        {
            get
            {
                return this.Message.MessageWhat;
            }
            set
            {
                this.Message.MessageWhat = value;
            }
        }

        public string MessageProblem
        {
            get
            {
                return this.Message.MessageProblem;
            }
            set
            {
                this.Message.MessageProblem = value;
            }
        }

        public string MessageReason
        {
            get
            {
                return this.Message.MessageReason;
            }
            set
            {
                this.Message.MessageReason = value;
            }
        }

        public string MessageSolution
        {
            get
            {
                return this.Message.MessageSolution;
            }
            set
            {
                this.Message.MessageSolution = value;
            }
        }

        public string MessageResult
        {
            get
            {
                return this.Message.MessageResult;
            }
            set
            {
                this.Message.MessageResult = value;
            }
        }

        public string MessageHint
        {
            get
            {
                return this.Message.MessageHint;
            }
            set
            {
                this.Message.MessageHint = value;
            }
        }
        #endregion 

        public DialogMessageBox()
        {
            this.InitializeComponent();
            this.Title = "";
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
                this.Left = this.Owner.Left + (this.Owner.Width - this.ActualWidth) / 2; // Center it horizontally
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
