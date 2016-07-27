using System;
using System.Windows;

/// <summary>
/// These functions read and write values to HKCU\Constants.Registry.RootKey to persist them between sessions.
/// </summary>
namespace Timelapse.Util
{
    internal class TimelapseRegistryUserSettings : RegistryUserSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TimelapseRegistryUserSettings"/> class for use by code within Timelapse proper.
        /// </summary>
        public TimelapseRegistryUserSettings()
            : this(Constants.Registry.RootKey)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimelapseRegistryUserSettings"/> class for use by Timelapse unit tests.
        /// </summary>
        /// <param name="keyPath">Path to top level key for Timelapse settings.</param>
        internal TimelapseRegistryUserSettings(string keyPath)
            : base(keyPath)
        {
        }

        // Functions used to save and retrive image sets to and from the Registry
        public MostRecentlyUsedList<string> ReadMostRecentImageSets()
        {
            return this.ReadMostRecentlyUsedListFromRegistry(Constants.Registry.TimelapseKey.MostRecentlyUsedImageSets);
        }

        public void WriteMostRecentImageSets(MostRecentlyUsedList<string> paths)
        {
            this.WriteToRegistry(Constants.Registry.TimelapseKey.MostRecentlyUsedImageSets, paths);
        }

        // Save and retrive the state of audio feedback (i.e., if its on or not)
        public bool ReadAudioFeedback()
        {
            return this.ReadBooleanFromRegistry(Constants.Registry.TimelapseKey.AudioFeedback, false);
        }

        public void WriteAudioFeedback(bool audioFeedback)
        {
            this.WriteToRegistry(Constants.Registry.TimelapseKey.AudioFeedback, audioFeedback);
        }

        // Save and retrive the state of audio feedback (i.e., if its on or not)
        public bool ReadControlsInSeparateWindow()
        {
            return this.ReadBooleanFromRegistry(Constants.Registry.TimelapseKey.ControlsInSeparateWindow, false);
        }

        public void WriteControlsInSeparateWindow(bool separateWindow)
        {
            this.WriteToRegistry(Constants.Registry.TimelapseKey.ControlsInSeparateWindow, separateWindow);
        }

        /// <summary>
        /// retrieve the size of the control window
        /// </summary>
        /// <returns>a Point with the width (x) and height (y) of the control window or (0, 0) if the size is not specified</returns>
        public Point ReadControlWindowSize()
        {
            double controlWindowWidth = this.ReadDoubleFromRegistry(Constants.Registry.TimelapseKey.ControlWindowWidth, 0);
            double controlWindowHeight = this.ReadDoubleFromRegistry(Constants.Registry.TimelapseKey.ControlWindowHeight, 0);
            return new Point(controlWindowWidth, controlWindowHeight);
        }

        public void WriteControlWindowSize(Point windowSize)
        {
            string width = Double.IsNaN(windowSize.X) ? "0" : windowSize.X.ToString();
            string height = Double.IsNaN(windowSize.Y) ? "0" : windowSize.Y.ToString();
            this.WriteToRegistry(Constants.Registry.TimelapseKey.ControlWindowWidth, width);
            this.WriteToRegistry(Constants.Registry.TimelapseKey.ControlWindowHeight, height);
        }

        public int ReadDarkPixelThreshold()
        {
            return this.ReadIntegerFromRegistry(Constants.Registry.TimelapseKey.DarkPixelThreshold, Constants.Images.DarkPixelThresholdDefault);
        }

        public void WriteDarkPixelThreshold(int threshold)
        {
            this.WriteToRegistry(Constants.Registry.TimelapseKey.DarkPixelThreshold, threshold);
        }

        public double ReadDarkPixelRatioThreshold()
        {
            return this.ReadDoubleFromRegistry(Constants.Registry.TimelapseKey.DarkPixelRatio, Constants.Images.DarkPixelRatioThresholdDefault);
        }

        public void WriteDarkPixelRatioThreshold(double threshold)
        {
            this.WriteToRegistry(Constants.Registry.TimelapseKey.DarkPixelRatio, Convert.ToString(threshold));
        }

        public void WriteShowCsvDialog(bool showDialog)
        {
            this.WriteToRegistry(Constants.Registry.TimelapseKey.ShowCsvDialog, showDialog);
        }

        public bool ReadShowCsvDialog()
        {
            return this.ReadBooleanFromRegistry(Constants.Registry.TimelapseKey.ShowCsvDialog, true);
        }
    }
}
