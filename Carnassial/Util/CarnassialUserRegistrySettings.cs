using Carnassial.Database;
using Microsoft.Win32;
using System;
using System.Windows;

namespace Carnassial.Util
{
    public class CarnassialUserRegistrySettings : UserRegistrySettings
    {
        public bool AudioFeedback { get; set; }
        public Rect CarnassialWindowPosition { get; set; }
        public int ControlGridWidth { get; set; }
        public LogicalOperator CustomSelectionTermCombiningOperator { get; set; }
        public double DarkLuminosityThreshold { get; set; }
        public DateTime MostRecentCheckForUpdates { get; set; }
        public MostRecentlyUsedList<string> MostRecentImageSets { get; private set; }
        public bool OrderFilesByDateTime { get; set; }
        public bool SkipFileClassification { get; set; }
        public bool SuppressAmbiguousDatesDialog { get; set; }
        public bool SuppressFileCountOnImportDialog { get; set; }
        public bool SuppressSpreadsheetImportPrompt { get; set; }
        public Throttles Throttles { get; private set; }

        public CarnassialUserRegistrySettings() :
            this(Constant.Registry.RootKey)
        {
        }

        internal CarnassialUserRegistrySettings(string registryKey)
            : base(registryKey)
        {
            this.Throttles = new Throttles();

            this.ReadFromRegistry();
        }

        public void ReadFromRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                this.AudioFeedback = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.AudioFeedback, false);
                this.CarnassialWindowPosition = registryKey.ReadRect(Constant.Registry.CarnassialKey.CarnassialWindowPosition, new Rect(0.0, 0.0, 1350.0, 900.0));
                this.ControlGridWidth = registryKey.ReadInteger(Constant.Registry.CarnassialKey.ControlGridWidth, (int)Constant.DefaultControlGridWidth);
                this.CustomSelectionTermCombiningOperator = registryKey.ReadLogicalOperator(Constant.Registry.CarnassialKey.CustomSelectionTermCombiningOperator, LogicalOperator.And);
                this.DarkLuminosityThreshold = registryKey.ReadDouble(Constant.Registry.CarnassialKey.DarkLuminosityThreshold, Constant.Images.DarkLuminosityThresholdDefault);
                this.MostRecentCheckForUpdates = registryKey.ReadDateTime(Constant.Registry.CarnassialKey.MostRecentCheckForUpdates, DateTime.UtcNow);
                this.MostRecentImageSets = registryKey.ReadMostRecentlyUsedList(Constant.Registry.CarnassialKey.MostRecentlyUsedImageSets);
                this.OrderFilesByDateTime = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.OrderFilesByDateTime, false);
                this.SkipFileClassification = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SkipFileClassification, false);
                this.SuppressAmbiguousDatesDialog = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SuppressAmbiguousDatesDialog, false);
                this.SuppressFileCountOnImportDialog = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SuppressFileCountOnImportDialog, false);
                this.SuppressSpreadsheetImportPrompt = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SuppressSpreadsheetImportPrompt, false);
                this.Throttles.ImageClassificationChangeSlowdown = registryKey.ReadDouble(Constant.Registry.CarnassialKey.ImageClassificationChangeSlowdown, Constant.ThrottleValues.ImageClassificationSlowdownDefault);
                this.Throttles.SetDesiredImageRendersPerSecond(registryKey.ReadDouble(Constant.Registry.CarnassialKey.DesiredImageRendersPerSecond, Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault));
                this.Throttles.VideoSlowdown = registryKey.ReadDouble(Constant.Registry.CarnassialKey.VideoSlowdown, Constant.ThrottleValues.VideoSlowdownDefault);
            }
        }

        public void WriteToRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(Constant.Registry.CarnassialKey.AudioFeedback, this.AudioFeedback);
                registryKey.Write(Constant.Registry.CarnassialKey.CarnassialWindowPosition, this.CarnassialWindowPosition);
                registryKey.Write(Constant.Registry.CarnassialKey.ControlGridWidth, this.ControlGridWidth);
                registryKey.Write(Constant.Registry.CarnassialKey.CustomSelectionTermCombiningOperator, this.CustomSelectionTermCombiningOperator.ToString());
                registryKey.Write(Constant.Registry.CarnassialKey.DarkLuminosityThreshold, this.DarkLuminosityThreshold);
                registryKey.Write(Constant.Registry.CarnassialKey.DesiredImageRendersPerSecond, this.Throttles.DesiredImageRendersPerSecond);
                registryKey.Write(Constant.Registry.CarnassialKey.ImageClassificationChangeSlowdown, this.Throttles.ImageClassificationChangeSlowdown);
                registryKey.Write(Constant.Registry.CarnassialKey.MostRecentCheckForUpdates, this.MostRecentCheckForUpdates);
                registryKey.Write(Constant.Registry.CarnassialKey.MostRecentlyUsedImageSets, this.MostRecentImageSets);
                registryKey.Write(Constant.Registry.CarnassialKey.OrderFilesByDateTime, this.OrderFilesByDateTime);
                registryKey.Write(Constant.Registry.CarnassialKey.SkipFileClassification, this.SkipFileClassification);
                registryKey.Write(Constant.Registry.CarnassialKey.SuppressAmbiguousDatesDialog, this.SuppressAmbiguousDatesDialog);
                registryKey.Write(Constant.Registry.CarnassialKey.SuppressFileCountOnImportDialog, this.SuppressFileCountOnImportDialog);
                registryKey.Write(Constant.Registry.CarnassialKey.SuppressSpreadsheetImportPrompt, this.SuppressSpreadsheetImportPrompt);
                registryKey.Write(Constant.Registry.CarnassialKey.VideoSlowdown, this.Throttles.VideoSlowdown);
            }
        }
    }
}
