using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Carnassial.Controls
{
    public partial class StockMessageControl : UserControl
    {
        private MessageBoxImage iconType;

        public MessageBoxImage Icon
        {
            get
            {
                return this.iconType;
            }
            set
            {
                this.iconType = value;
                // the MessageBoxImage enum contains duplicate values:
                // Hand = Stop = Error
                // Exclamation = Warning
                // Asterisk = Information
                switch (value)
                {
                    case MessageBoxImage.Question:
                        this.IconLabel.Content = "?";
                        break;
                    case MessageBoxImage.Warning:
                        this.IconLabel.Content = "!";
                        break;
                    case MessageBoxImage.None:
                    case MessageBoxImage.Information:
                        this.IconLabel.Content = "i";
                        break;
                    case MessageBoxImage.Error:
                        Run run = new Run(); // Create a symbol of a stopped hand
                        run.FontFamily = new FontFamily("Wingdings 2");
                        run.Text = "\u004e";
                        this.IconLabel.Content = run;
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled icon type {0}.", this.Icon));
                }
                this.iconType = value;
            }
        }

        public string Title
        {
            get
            {
                return this.TitleText.Text;
            }
            set
            {
                this.TitleText.Text = value;
                this.SetExplanationVisibility();
            }
        }

        public string What
        {
            get
            {
                return this.WhatText.Text;
            }
            set
            {
                this.WhatText.Text = value;
                this.SetExplanationVisibility();
            }
        }

        public string Problem
        {
            get
            {
                return this.ProblemText.Text;
            }
            set
            {
                this.ProblemText.Text = value;
                this.SetExplanationVisibility();
            }
        }

        public string Reason
        {
            get
            {
                return this.ReasonText.Text;
            }
            set
            {
                this.ReasonText.Text = value;
                this.SetExplanationVisibility();
            }
        }

        public string Solution
        {
            get
            {
                return this.SolutionText.Text;
            }
            set
            {
                this.SolutionText.Text = value;
                this.SetExplanationVisibility();
            }
        }

        public string Result
        {
            get
            {
                return this.ResultText.Text;
            }
            set
            {
                this.ResultText.Text = value;
                this.SetExplanationVisibility();
            }
        }

        public string Hint
        {
            get
            {
                return this.HintText.Text;
            }
            set
            {
                this.HintText.Text = value;
                this.SetExplanationVisibility();
            }
        }

        public bool HideExplanationCheckboxIsVisible
        {
            get
            {
                return this.HideExplanation.Visibility == Visibility.Visible;
            }
            set
            {
                this.HideExplanation.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                this.SetExplanationVisibility();
            }
        }

        public StockMessageControl()
        {
            this.InitializeComponent();
            this.iconType = MessageBoxImage.Exclamation;

            this.SetExplanationVisibility();
        }

        private void SetExplanationVisibility()
        {
            GridLength zeroHeight = new GridLength(0.0);
            if (this.HideExplanation.IsChecked == true)
            {
                this.MessageGrid.RowDefinitions[1].Height = zeroHeight;
                this.MessageGrid.RowDefinitions[2].Height = zeroHeight;
                this.MessageGrid.RowDefinitions[3].Height = zeroHeight;
                this.MessageGrid.RowDefinitions[4].Height = zeroHeight;
                this.MessageGrid.RowDefinitions[5].Height = zeroHeight;
                this.MessageGrid.RowDefinitions[6].Height = zeroHeight;
                return;
            }

            GridLength autoHeight = new GridLength(1.0, GridUnitType.Auto);
            this.MessageGrid.RowDefinitions[1].Height = String.IsNullOrEmpty(this.Problem) ? zeroHeight : autoHeight;
            this.MessageGrid.RowDefinitions[2].Height = String.IsNullOrEmpty(this.What) ? zeroHeight : autoHeight;
            this.MessageGrid.RowDefinitions[3].Height = String.IsNullOrEmpty(this.Reason) ? zeroHeight : autoHeight;
            this.MessageGrid.RowDefinitions[4].Height = String.IsNullOrEmpty(this.Solution) ? zeroHeight : autoHeight;
            this.MessageGrid.RowDefinitions[5].Height = String.IsNullOrEmpty(this.Result) ? zeroHeight : autoHeight;
            this.MessageGrid.RowDefinitions[6].Height = String.IsNullOrEmpty(this.Hint) ? zeroHeight : autoHeight;
        }

        // This will toggle the visibility of the explanation panel
        private void HideTextButton_StateChange(object sender, RoutedEventArgs e)
        {
            this.SetExplanationVisibility();
        }
    }
}
