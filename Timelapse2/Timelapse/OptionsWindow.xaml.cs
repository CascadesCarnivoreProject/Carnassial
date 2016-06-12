using System.Windows;
using System.Windows.Controls;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for OptionsWindow.xaml
    /// </summary>
    public partial class OptionsWindow : Window
    {
        private Timelapse.Images.MarkableImageCanvas markableCanvas;
        private TimelapseWindow mainProgram;

        public OptionsWindow(TimelapseWindow mainWindow, Timelapse.Images.MarkableImageCanvas mcanvas)
        {
            this.InitializeComponent();
            this.Topmost = true;
            this.markableCanvas = mcanvas;
            this.mainProgram = mainWindow;

            // The Max Zoom Value
            sldrMaxZoom.Value = this.markableCanvas.MaxZoom;
            sldrMaxZoom.ToolTip = this.markableCanvas.MaxZoom;
            sldrMaxZoom.Maximum = this.markableCanvas.MaxZoomUpperBound;
            sldrMaxZoom.Minimum = 1;

            // Image Differencing Thresholds
            sldrDifferenceThreshold.Value = this.mainProgram.DifferenceThreshold;
            sldrDifferenceThreshold.ToolTip = this.mainProgram.DifferenceThreshold;
            sldrDifferenceThreshold.Maximum = Constants.Images.DifferenceThresholdMax;
            sldrDifferenceThreshold.Minimum = Constants.Images.DifferenceThresholdMin;

            /*
            // For swapping data within notes, fixed choices, or counters:
            // Get the counts of each type of code control, as we only need to do this once,
            // then generate the contents of the listboxes
            this.noteCount = this.mainProgram.codeControls.notes.Length;
            this.fixedchoiceCount = this.mainProgram.codeControls.fixedChoice.Length;
            this.counterCount = this.mainProgram.codeControls.counters.Length;
            GenerateLists();
            */
        }

        // Reset the maximum zoom to the amount specified in Max Zoom;
        private void ResetMaxZoomButton_Click(object sender, RoutedEventArgs e)
        {
            this.markableCanvas.ResetMaxZoom();
            sldrMaxZoom.Value = this.markableCanvas.MaxZoom;
            sldrMaxZoom.ToolTip = this.markableCanvas.MaxZoom;
        }

        // Callback: The user has changed the maximum zoom value
        private void MaxZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.markableCanvas.MaxZoom = (int)sldrMaxZoom.Value;
            sldrMaxZoom.ToolTip = this.markableCanvas.MaxZoom;
        }

        // Image Differencing
        private void ResetImageDifferencingButton_Click(object sender, RoutedEventArgs e)
        {
            this.mainProgram.ResetDifferenceThreshold();
            sldrDifferenceThreshold.Value = this.mainProgram.DifferenceThreshold;
            sldrDifferenceThreshold.ToolTip = this.mainProgram.DifferenceThreshold;
        }

        private void DifferenceThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.mainProgram.DifferenceThreshold = (byte)sldrDifferenceThreshold.Value;
            sldrDifferenceThreshold.ToolTip = this.mainProgram.DifferenceThreshold;
        }

        private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // The Swap button is clicked. Swap the data fields as selected in the listbox of the given type.
        private void SwapDataButton_Click(object sender, RoutedEventArgs e)
        {
            /*
            // Get the indexes from the two lists of data to swap.
            // These will match the indexes of the code controls and of the lists in the image data
            int idx1 = lbSwapList1.SelectedIndex;
            int idx2 = lbSwapList2.SelectedIndex;

            // Get the labels of the selected items so we can 
            // show a message to confirm that we really want to do this
            string label1 = (string)lbSwapList1.SelectedItem; 
            string label2 = (string)lbSwapList2.SelectedItem;
            if (Messages.DoYouWantToSwapData(label1, label2) == MessageBoxResult.No) return;

            string stmp;
            for (int i = 0; i < mainProgram.ximageSet.allCount; i++)
            {
                if (rbNotes.IsChecked == true)
                {
                    stmp = mainProgram.ximageData[i].NoteContents[idx1];
                    mainProgram.ximageData[i].NoteContents[idx1] = mainProgram.ximageData[i].NoteContents[idx2];
                    mainProgram.ximageData[i].NoteContents[idx2] = stmp;
                }
                else if (rbFixedChoices.IsChecked == true)
                {
                    stmp = mainProgram.ximageData[i].FixedChoicesContents[idx1];
                    mainProgram.ximageData[i].FixedChoicesContents[idx1] = mainProgram.ximageData[i].FixedChoicesContents[idx2];
                    mainProgram.ximageData[i].FixedChoicesContents[idx2] = stmp;
                }
                else // Only remaining options are counters
                {
                    stmp = mainProgram.ximageData[i].CounterContents[idx1];
                    mainProgram.ximageData[i].CounterContents[idx1] = mainProgram.ximageData[i].CounterContents[idx2];
                    mainProgram.ximageData[i].CounterContents[idx2] = stmp;
                    MetaTagCounter mtcTemp1 = mainProgram.ximageData[i].CounterCoords[idx1] ;
                    for (int j = 0; j < mtcTemp1.MetaTags.Count; j++)
                    {
                        TagFinder tf = (TagFinder) mtcTemp1.MetaTags[j].Object;
                        tf.controlIndex = idx2;
                        mtcTemp1.MetaTags[j].Object = tf;
                    }
                    MetaTagCounter mtcTemp2 = mainProgram.ximageData[i].CounterCoords[idx2];
                    for (int j = 0; j < mtcTemp2.MetaTags.Count; j++)
                    {
                        TagFinder tf = (TagFinder)mtcTemp2.MetaTags[j].Object;
                        tf.controlIndex = idx1;
                        mtcTemp2.MetaTags[j].Object = tf;
                    }
                    mainProgram.ximageData[i].CounterCoords[idx1] = mtcTemp2;
                    mainProgram.ximageData[i].CounterCoords[idx2] = mtcTemp1;
                }
            }
            // mainProgram.showImage(mainProgram.ximageSet.currentImageIndex);
            */
        }

        // Anytime we make a selection, check to see if both list have a selection. If so, enable the swap button
        private void SwapListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx1 = lbSwapList1.SelectedIndex;
            int idx2 = lbSwapList2.SelectedIndex;
            button1.IsEnabled = (idx1 >= 0 && idx2 >= 0) ? true : false;
        }

        // When a radio button is selected, regenerate the list so it matches the item type
        private void RadioButtonControl_Checked(object sender, RoutedEventArgs e)
        {
            this.GenerateLists();
        }

        // Populate the listbox with the items as specified by the selected radio button (i.e., notes, fixed choices, or counters)
        private void GenerateLists()
        {
            /*
            this.lbSwapList1.Items.Clear();
            this.lbSwapList2.Items.Clear();
            // Populate List Box with notes
            if (rbNotes.IsChecked == true)
            {
                for (int i = 0; i < this.noteCount; i++)
                {
                    this.lbSwapList1.Items.Add(this.mainProgram.codeControls.notes[i].Label);
                    this.lbSwapList2.Items.Add(this.mainProgram.codeControls.notes[i].Label);
                }
            }
            else if (rbFixedChoices.IsChecked == true)
            {

                // Populate List Box with fixed Choices   
                for (int i = 0; i < this.fixedchoiceCount; i++)
                {
                    this.lbSwapList1.Items.Add(this.mainProgram.codeControls.fixedChoice[i].Label);
                    this.lbSwapList2.Items.Add(this.mainProgram.codeControls.fixedChoice[i].Label);
                }
            }
            else
            {
                // Populate List Box with counters
                for (int i = 0; i < counterCount; i++)
                {
                    this.lbSwapList1.Items.Add(this.mainProgram.codeControls.counters[i].Label);
                    this.lbSwapList2.Items.Add(this.mainProgram.codeControls.counters[i].Label);
                }
            }
            */
        }
    }
}
