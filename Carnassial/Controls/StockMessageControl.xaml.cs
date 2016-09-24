using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Carnassial.Controls
{
    public partial class StockMessageControl : UserControl
    {
        private MessageBoxImage iconType = MessageBoxImage.Exclamation;

        public MessageBoxImage Icon
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

        public string Title
        {
            get
            {
                return this.TitleTextBox.Text;
            }
            set
            {
                this.TitleTextBox.Text = value;
                this.SetFieldVisibility();
            }
        }

        public string What
        {
            get
            {
                return this.WhatTextBox.Text;
            }
            set
            {
                this.WhatTextBox.Text = value;
                this.SetFieldVisibility();
            }
        }

        public string Problem
        {
            get
            {
                return this.ProblemTextBox.Text;
            }
            set
            {
                this.ProblemTextBox.Text = value;
                this.SetFieldVisibility();
            }
        }

        public string Reason
        {
            get
            {
                return this.ReasonTextBox.Text;
            }
            set
            {
                this.ReasonTextBox.Text = value;
                this.SetFieldVisibility();
            }
        }

        public string Solution
        {
            get
            {
                return this.SolutionTextBox.Text;
            }
            set
            {
                this.SolutionTextBox.Text = value;
                this.SetFieldVisibility();
            }
        }

        public string Result
        {
            get
            {
                return this.ResultTextBox.Text;
            }
            set
            {
                this.ResultTextBox.Text = value;
                this.SetFieldVisibility();
            }
        }

        public string Hint
        {
            get
            {
                return this.HintTextBox.Text;
            }
            set
            {
                this.HintTextBox.Text = value;
                this.SetFieldVisibility();
            }
        }

        public bool ShowExplanationVisibility
        {
            get
            {
                return this.HideText.Visibility == Visibility.Visible;
            }
            set
            {
                this.HideText.Visibility = (value == true) ? Visibility.Visible : Visibility.Collapsed;
                this.SetFieldVisibility();
            }
        }

        public StockMessageControl()
        {
            this.InitializeComponent();
            this.SetFieldVisibility();
        }

        private void SetFieldVisibility()
        {
            this.myGrid.RowDefinitions[1].Height = (String.IsNullOrEmpty(this.Problem) || this.HideText.IsChecked == true) ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[2].Height = (String.IsNullOrEmpty(this.What) || this.HideText.IsChecked == true) ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[3].Height = (String.IsNullOrEmpty(this.Reason) || this.HideText.IsChecked == true) ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[4].Height = (String.IsNullOrEmpty(this.Solution) || this.HideText.IsChecked == true) ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[5].Height = (String.IsNullOrEmpty(this.Result) || this.HideText.IsChecked == true) ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[6].Height = (String.IsNullOrEmpty(this.Hint) || this.HideText.IsChecked == true) ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
        }

        // This will toggle the visibility of the explanation panel
        private void HideTextButton_StateChange(object sender, RoutedEventArgs e)
        {
            this.SetFieldVisibility();
        }

        private void SetIconType()
        {
            // the MessageBoxImage enum contains duplicate values:
            // Hand = Stop = Error
            // Exclamation = Warning
            // Asterisk = Information
            switch (this.Icon)
            {
                case MessageBoxImage.Question:
                    this.lblIconType.Content = "?";
                    break;
                case MessageBoxImage.Warning:
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
                    throw new NotSupportedException(String.Format("Unhandled icon type {0}.", this.Icon));
            }
        }
    }
}
