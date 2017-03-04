using System;
using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Controls
{
    public partial class StockMessageControl : UserControl
    {
        private MessageBoxImage statusImage;

        public MessageBoxImage StatusImage
        {
            get
            {
                return this.statusImage;
            }
            set
            {
                this.statusImage = value;
                switch (value)
                {
                    // the MessageBoxImage enum has some duplicate values, so not all of them needed cases
                    //   - Hand = Stop = Error
                    //   - Exclamation = Warning
                    //   - Asterisk = Information
                    case MessageBoxImage.Question:
                        this.Image.Source = Constant.Images.StatusHelp.Value;
                        break;
                    case MessageBoxImage.Warning:
                        this.Image.Source = Constant.Images.StatusWarning.Value;
                        break;
                    case MessageBoxImage.None:
                        this.Image.Source = null;
                        break;
                    case MessageBoxImage.Information:
                        this.Image.Source = Constant.Images.StatusInformation.Value;
                        break;
                    case MessageBoxImage.Error:
                        this.Image.Source = Constant.Images.StatusError.Value;
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled icon type {0}.", this.StatusImage));
                }
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
            this.StatusImage = MessageBoxImage.Warning;

            this.SetExplanationVisibility();
        }

        private void HideExplanation_CheckedChanged(object sender, RoutedEventArgs e)
        {
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
    }
}
