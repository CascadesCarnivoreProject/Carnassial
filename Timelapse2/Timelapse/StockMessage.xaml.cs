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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for StockMessage.xaml
    /// </summary>
    public partial class StockMessage : UserControl
    {
        private MessageBoxImage iconType = MessageBoxImage.Exclamation;

        #region Properties
        public MessageBoxImage IconType
        {
            get
            {
                return this.iconType;
            }
            set
            {
                this.iconType = value;
                this.SetIconType();
            }
        }

        public string MessageTitle
        {
            get
            {
                return this.tbTitleText.Text;
            }
            set
            {
                this.tbTitleText.Text = value;
                this.SetFieldVisibility();
            }
        }

        public string MessageWhat
        {
            get
            {
                return this.tbWhatText.Text;
            }
            set
            {
                this.tbWhatText.Text = value;
                this.SetFieldVisibility();
            }
        }
        public string MessageProblem
        {
            get
            {
                return this.tbProblemText.Text;
            }
            set
            {
                this.tbProblemText.Text = value;
                this.SetFieldVisibility();
            }
        }

        public string MessageReason
        {
            get
            {
                return this.tbReasonText.Text;
            }
            set
            {
                this.tbReasonText.Text = value;
                this.SetFieldVisibility();
            }
        }

        public string MessageSolution
        {
            get
            {
                return this.tbSolutionText.Text;
            }
            set
            {
                this.tbSolutionText.Text = value;
                this.SetFieldVisibility();
            }
        }

        public string MessageResult
        {
            get
            {
                return this.tbResultText.Text;
            }
            set
            {
                this.tbResultText.Text = value;
                this.SetFieldVisibility();
            }
        }

        public string MessageHint
        {
            get
            {
                return this.tbHintText.Text;
            }
            set
            {
                this.tbHintText.Text = value;
                this.SetFieldVisibility();
            }
        }
        #endregion 
        public StockMessage()
        {
            this.InitializeComponent();
            this.SetFieldVisibility();
        }

        private void SetFieldVisibility()
        {
            this.myGrid.RowDefinitions[1].Height = (this.MessageProblem == String.Empty) ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[2].Height = (this.MessageWhat == String.Empty) ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[3].Height = (this.MessageReason == String.Empty) ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[4].Height = (this.MessageSolution == String.Empty) ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[5].Height = (this.MessageResult == String.Empty) ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[6].Height = (this.MessageHint == String.Empty) ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
        }

        private void SetIconType()
        {
            switch (this.IconType)
            {
                case MessageBoxImage.Question:
                    this.lblIconType.Content = "?";
                    break;
                case MessageBoxImage.Exclamation:
                    this.lblIconType.Content = "!";
                    break;
                case MessageBoxImage.None:
                case MessageBoxImage.Information:
                    this.lblIconType.Content = "i";
                    break;
                case MessageBoxImage.Error:
                    Run run = new Run(); // Create a symbol of a stopped hand
                    run.FontFamily = new FontFamily("Wingdings 2");
                    run.Text = "\u004e";
                    this.lblIconType.Content = run;
                    break;
                default:
                    return;
            }
        }
    }
}
