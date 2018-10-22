using System;
using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Control
{
    public partial class StockMessageControl : UserControl
    {
        private MessageBoxImage statusImage;

        public StockMessageControl()
        {
            this.InitializeComponent();
            this.Image = MessageBoxImage.Warning;

            this.SetVisibility();
        }

        public MessageBoxImage Image
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
                        this.StatusImage.Source = Constant.Images.StatusHelp.Value;
                        break;
                    case MessageBoxImage.Warning:
                        this.StatusImage.Source = Constant.Images.StatusWarning.Value;
                        break;
                    case MessageBoxImage.None:
                        this.StatusImage.Source = null;
                        break;
                    case MessageBoxImage.Information:
                        this.StatusImage.Source = Constant.Images.StatusInformation.Value;
                        break;
                    case MessageBoxImage.Error:
                        this.StatusImage.Source = Constant.Images.StatusError.Value;
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled icon type {0}.", this.Image));
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
                this.SetVisibility();
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
                this.SetVisibility();
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
                this.SetVisibility();
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
                this.SetVisibility();
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
                this.SetVisibility();
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
                this.SetVisibility();
            }
        }

        public bool DisplayHideExplanationCheckbox
        {
            get
            {
                return this.HideExplanation.Visibility == Visibility.Visible;
            }
            set
            {
                this.HideExplanation.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                this.SetVisibility();
            }
        }

        public static string GetHint(StockMessageControl message)
        {
            return message.HintText.Text;
        }

        public static string GetProblem(StockMessageControl message)
        {
            return message.ProblemText.Text;
        }

        public static string GetReason(StockMessageControl message)
        {
            return message.ReasonText.Text;
        }

        public static string GetResult(StockMessageControl message)
        {
            return message.ResultText.Text;
        }

        public static string GetSolution(StockMessageControl message)
        {
            return message.Solution.Text;
        }

        public static string GetTitle(StockMessageControl message)
        {
            return message.TitleText.Text;
        }

        public static string GetWhat(StockMessageControl message)
        {
            return message.WhatText.Text;
        }

        private void HideExplanation_CheckedChanged(object sender, RoutedEventArgs e)
        {
            this.SetVisibility();
        }

        public void SetVisibility()
        {
            if (this.HideExplanation.IsChecked == true)
            {
                this.HintText.Visibility = Visibility.Collapsed;
                this.ProblemText.Visibility = Visibility.Collapsed;
                this.ReasonText.Visibility = Visibility.Collapsed;
                this.ResultText.Visibility = Visibility.Collapsed;
                this.Solution.Visibility = Visibility.Collapsed;
                this.WhatText.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.HintText.Visibility = this.HintText.Inlines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                this.ProblemText.Visibility = this.ProblemText.Inlines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                this.ReasonText.Visibility = this.ResultText.Inlines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                this.ResultText.Visibility = this.ResultText.Inlines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                this.Solution.Visibility = this.Solution.Inlines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                this.WhatText.Visibility = this.WhatText.Inlines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public static void SetHint(StockMessageControl message, string value)
        {
            message.HintText.Text = value;
        }

        public static void SetProblem(StockMessageControl message, string value)
        {
            message.ProblemText.Text = value;
        }

        public static void SetReason(StockMessageControl message, string value)
        {
            message.ReasonText.Text = value;
        }

        public static void SetResult(StockMessageControl message, string value)
        {
            message.ResultText.Text = value;
        }

        public static void SetSolution(StockMessageControl message, string value)
        {
            message.Solution.Text = value;
        }

        public static void SetTitle(StockMessageControl message, string value)
        {
            message.TitleText.Text = value;
        }

        public static void SetWhat(StockMessageControl message, string value)
        {
            message.WhatText.Text = value;
        }
    }
}
