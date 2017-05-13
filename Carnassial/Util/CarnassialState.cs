using Carnassial.Controls;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace Carnassial.Util
{
    // A class that tracks various states and flags.
    public class CarnassialState : CarnassialUserRegistrySettings
    {
        private int keyRepeatCount;
        private KeyEventArgs mostRecentKey;

        public List<Dictionary<string, object>> Analysis { get; private set; }
        public byte DifferenceThreshold { get; set; } // The threshold used for calculating combined differences
        public bool FileNavigatorSliderDragging { get; set; }
        public DateTime MostRecentDragEvent { get; set; }
        public string MostRecentFileAddFolderPath { get; set; }
        public int MostRecentlFocusedControlIndex { get; set; }
        public string MouseOverCounter { get; set; }
        public UndoRedoChain UndoRedoChain { get; private set; }

        public CarnassialState()
        {
            this.keyRepeatCount = 0;
            this.mostRecentKey = null;

            this.Analysis = new List<Dictionary<string, object>>(Constant.AnalysisSlots);
            for (int analysisSlot = 0; analysisSlot < Constant.AnalysisSlots; ++analysisSlot)
            {
                this.Analysis.Add(null);
            }
            this.DifferenceThreshold = Constant.Images.DifferenceThresholdDefault;
            this.FileNavigatorSliderDragging = false;
            this.MostRecentDragEvent = DateTime.UtcNow - this.Throttles.DesiredIntervalBetweenRenders;
            this.MostRecentFileAddFolderPath = null;
            this.MostRecentlFocusedControlIndex = -1;
            this.MouseOverCounter = null;
            this.UndoRedoChain = new UndoRedoChain();
        }

        public int GetKeyRepeatCount(KeyEventArgs key)
        {
            // check mostRecentKey for null as key delivery is not entirely deterministic
            // it's possible WPF will send the first key as a repeat if the user holds a key down or starts typing while the main window is opening
            if (key.IsRepeat && this.mostRecentKey != null && this.mostRecentKey.IsRepeat && (key.Key == this.mostRecentKey.Key))
            {
                ++this.keyRepeatCount;
            }
            else
            {
                this.keyRepeatCount = 0;
            }
            this.mostRecentKey = key;
            return this.keyRepeatCount;
        }

        public void Reset()
        {
            for (int analysisSlot = 0; analysisSlot < this.Analysis.Count; ++analysisSlot)
            {
                this.Analysis[analysisSlot] = null;
            }
            this.FileNavigatorSliderDragging = false;
            this.MostRecentlFocusedControlIndex = -1;
            this.MouseOverCounter = null;
            this.UndoRedoChain.Clear();
        }
    }
}
