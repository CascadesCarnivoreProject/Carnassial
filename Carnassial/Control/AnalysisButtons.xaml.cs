using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Carnassial.Control
{
    public partial class AnalysisButtons : UserControl
    {
        public AnalysisButtons()
        {
            this.InitializeComponent();
        }

        public event Action<object, MouseEventArgs> MouseEnterButton;
        public event Action<object, MouseEventArgs> MouseLeaveButton;
        public event Action<object, int> PasteAnalysis;
        public event Action<object, RoutedEventArgs> PasteNext;
        public event Action<object, RoutedEventArgs> PastePrevious;

        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            this.MouseEnterButton?.Invoke(this, e);
        }

        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            this.MouseLeaveButton?.Invoke(this, e);
        }

        public void EnableOrDisable(bool filesSelected, List<Dictionary<string, object>> analyses)
        {
            this.PasteAnalysis1.IsEnabled = filesSelected && (analyses[0] != null);
            this.PasteAnalysis2.IsEnabled = filesSelected && (analyses[1] != null);
            this.PasteAnalysis3.IsEnabled = filesSelected && (analyses[2] != null);
            this.PasteAnalysis4.IsEnabled = filesSelected && (analyses[3] != null);
            this.PasteAnalysis5.IsEnabled = filesSelected && (analyses[4] != null);
            this.PasteAnalysis6.IsEnabled = filesSelected && (analyses[5] != null);
            this.PasteAnalysis7.IsEnabled = filesSelected && (analyses[6] != null);
            this.PasteAnalysis8.IsEnabled = filesSelected && (analyses[7] != null);
            this.PasteAnalysis9.IsEnabled = filesSelected && (analyses[8] != null);
            this.PasteNextValues.IsEnabled = filesSelected;
            this.PastePreviousValues.IsEnabled = filesSelected;
        }

        private void PasteAnalysis1_Click(object sender, RoutedEventArgs e)
        {
            this.PasteAnalysis?.Invoke(this, 0);
        }

        private void PasteAnalysis2_Click(object sender, RoutedEventArgs e)
        {
            this.PasteAnalysis?.Invoke(this, 1);
        }

        private void PasteAnalysis3_Click(object sender, RoutedEventArgs e)
        {
            this.PasteAnalysis?.Invoke(this, 2);
        }

        private void PasteAnalysis4_Click(object sender, RoutedEventArgs e)
        {
            this.PasteAnalysis?.Invoke(this, 3);
        }

        private void PasteAnalysis5_Click(object sender, RoutedEventArgs e)
        {
            this.PasteAnalysis?.Invoke(this, 4);
        }

        private void PasteAnalysis6_Click(object sender, RoutedEventArgs e)
        {
            this.PasteAnalysis?.Invoke(this, 5);
        }

        private void PasteAnalysis7_Click(object sender, RoutedEventArgs e)
        {
            this.PasteAnalysis?.Invoke(this, 6);
        }

        private void PasteAnalysis8_Click(object sender, RoutedEventArgs e)
        {
            this.PasteAnalysis?.Invoke(this, 7);
        }

        private void PasteAnalysis9_Click(object sender, RoutedEventArgs e)
        {
            this.PasteAnalysis?.Invoke(this, 8);
        }

        private void PasteNextValues_Click(object sender, RoutedEventArgs e)
        {
            this.PasteNext?.Invoke(this, e);
        }

        private void PastePreviousValues_Click(object sender, RoutedEventArgs e)
        {
            this.PastePrevious?.Invoke(this, e);
        }

        public void SetAnalysis(int analysisSlot, Dictionary<string, object> analysisValuesByDataLabel, HashSet<string> analysisLabelsByDataLabel)
        {
            if ((analysisSlot < 0) || (analysisSlot > 8))
            {
                throw new ArgumentOutOfRangeException(nameof(analysisSlot));
            }
            Debug.Assert(analysisLabelsByDataLabel != null, nameof(analysisLabelsByDataLabel) + " unexpectedly null.");
            Debug.Assert(analysisValuesByDataLabel != null, nameof(analysisValuesByDataLabel) + " unexpectedly null.");

            Button pasteAnalysisButton;
            switch (analysisSlot)
            {
                case 0:
                    pasteAnalysisButton = this.PasteAnalysis1;
                    break;
                case 1:
                    pasteAnalysisButton = this.PasteAnalysis2;
                    break;
                case 2:
                    pasteAnalysisButton = this.PasteAnalysis3;
                    break;
                case 3:
                    pasteAnalysisButton = this.PasteAnalysis4;
                    break;
                case 4:
                    pasteAnalysisButton = this.PasteAnalysis5;
                    break;
                case 5:
                    pasteAnalysisButton = this.PasteAnalysis6;
                    break;
                case 6:
                    pasteAnalysisButton = this.PasteAnalysis7;
                    break;
                case 7:
                    pasteAnalysisButton = this.PasteAnalysis8;
                    break;
                case 8:
                    pasteAnalysisButton = this.PasteAnalysis9;
                    break;
                default:
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled analysis slot {0}.", analysisSlot));
            }

            int analysisNumber = analysisSlot + 1;
            StringBuilder buttonLabel = new StringBuilder("_" + analysisNumber + ": ");
            StringBuilder buttonTooltip = new StringBuilder("Analysis " + analysisNumber + ": ");
            bool analysisSpecificValues = false;
            bool copyableValueEncountered = false;
            foreach (KeyValuePair<string, object> analysisValue in analysisValuesByDataLabel)
            {
                if (analysisValue.Value != null)
                {
                    string valueAsString = analysisValue.Value.ToString();
                    if (String.IsNullOrWhiteSpace(valueAsString) == false)
                    {
                        if (analysisLabelsByDataLabel.Contains(analysisValue.Key))
                        {
                            if (analysisSpecificValues)
                            {
                                buttonLabel.Append(", ");
                            }
                            buttonLabel.Append(valueAsString);
                            analysisSpecificValues = true;
                        }

                        if (copyableValueEncountered)
                        {
                            buttonTooltip.Append(", ");
                        }
                        buttonTooltip.Append(valueAsString);
                        copyableValueEncountered = true;
                    }
                }
            }
            if (analysisSpecificValues)
            {
                pasteAnalysisButton.Content = buttonLabel.ToString();
            }
            else
            {
                pasteAnalysisButton.Content = "Analysis _" + analysisNumber;
            }
            if (copyableValueEncountered)
            {
                pasteAnalysisButton.ToolTip = buttonTooltip.ToString();
            }
            else
            {
                pasteAnalysisButton.ToolTip = "Apply copyable values stored in analysis " + analysisNumber + ".";
            }
            pasteAnalysisButton.IsEnabled = true;
        }
    }
}
