using System;
using System.Windows;
using Utility.ModifyRegistry;

// These functions read and write values to the registry so that they persist between sessions.
// Using regedit, you will see the path stored under HKEY_CURRENT_USER/Software/Timelapse, with the name being LASTDIRECTORY.
namespace Timelapse
{
    public class PersistInRegistry
    {
        private string SUBKEY = "Software" + "\\" + "Timelapse";   // Defines the KEY path under HKEY_CURRENT_USER
        
        private string KEY_LASTIMAGEFOLDERPATH = "LASTIMAGEFOLDER";         // Key: The last image folder on exit (a full path)
        private string KEY_AUDIOFEEDBACK = "AUDIO_FEEDBACK";                // Key: The  image filter used on exit
        private string KEY_LASTIMAGETEMPLATENAME = "LASTIMAGETEMPLATNAME";  // Key: The last image template file name on exit (just the name)
        private string KEY_CONTROLWINDOW = "CONTROLSINSEPARATEWINDOW";  // Key:  Whether the controls are in a separate window (true) or in the Timelapse Window (false)
        private string KEY_CONTROLWINDOW_WIDTH = "CONTROLSINSEPARATEWINDOW_WIDTH";  // Key:  The width of the controlWindow
        private string KEY_CONTROLWINDOW_HEIGHT = "CONTROLSINSEPARATEWINDOW_HEIGHT";  // Key:  The width of the controlWindow
        private string KEY_DARKPIXELTHRESHOLD = "DARKPIXELTHRESHOLD";           // Key: The DarkPixelThreshold
        private string KEY_DARKPIXELRATIO = "DARKPIXELRATIO";                   // Key: The DarkPixelRatio
        private string KEY_SHOWCSVDIALOG = "SHOW_CSV_DIALOG";                   // Key: Whether to show the CSV Dialog WIndow

        // Functions  used to save and retrive the last image folder path to and from the Registry
        public  string ReadLastImageFolderPath()
        {
            return ReadFromRegistry(KEY_LASTIMAGEFOLDERPATH);
        }

        public  void WriteLastImageFolderPath(string path)
        {
            WriteToRegistry(KEY_LASTIMAGEFOLDERPATH, path);
        }

        public string ReadLastImageTemplateName()
        {
            return ReadFromRegistry(KEY_LASTIMAGETEMPLATENAME);
        }
        public void WriteLastImageTemplateName(string fname)
        {
            WriteToRegistry(KEY_LASTIMAGETEMPLATENAME, fname);
        }

        // Save and retrive the state of audio feedback  (i.e., if its on or not)
        public  bool ReadAudioFeedback ()
        {
            // Convert the string to a bool, with true as default if it fails
            string svalue = ReadFromRegistry(KEY_AUDIOFEEDBACK);
            if (null == svalue) return true;
            return (svalue.ToLower() == "false") ? false : true;
        }

        public  void WriteAudioFeedback(bool flag)
        {
            string svalue = flag.ToString();
            WriteToRegistry(KEY_AUDIOFEEDBACK, svalue.ToLower());
        }

        // Save and retrive the state of audio feedback  (i.e., if its on or not)
        public bool ReadControlWindow()
        {
            // Convert the string to a bool, with true as default if it fails
            string svalue = ReadFromRegistry(KEY_CONTROLWINDOW);
            if (null == svalue) return false;
            return (svalue.ToLower() == "false") ? false : true;
        }

        public void WriteControlWindow(bool flag)
        {
            string svalue = flag.ToString();
            WriteToRegistry(KEY_CONTROLWINDOW, svalue.ToLower());
        }

        // Save and retrive the state of audio feedback  (i.e., if its on or not)
        public Point ReadControlWindowSize()
        {
            // Convert the strings representin the width and hdeight of the control window to a point, with 0,0 as default if it fails
            Point point = new Point((double)0, (double) 0);
            string svaluewidth = ReadFromRegistry(KEY_CONTROLWINDOW_WIDTH);
            string svalueheight = ReadFromRegistry(KEY_CONTROLWINDOW_HEIGHT);
            double valuewidth = 0;
            double valueheight = 0;

            if (!Double.TryParse(svaluewidth, out valuewidth)) return point;
            if (!Double.TryParse(svalueheight, out valueheight)) return point;
            point.X = valuewidth;
            point.Y = valueheight;
            return (point);
        }

        public void WriteControlWindowSize(Point point)
        {
            string svaluewidth = (Double.IsNaN(point.X)) ? "0" : point.X.ToString();
            string svalueheight = (Double.IsNaN(point.Y)) ? "0" : point.Y.ToString();
            WriteToRegistry(KEY_CONTROLWINDOW_WIDTH, svaluewidth);
            WriteToRegistry(KEY_CONTROLWINDOW_HEIGHT, svalueheight);
        }


        public int ReadDarkPixelThreshold()
        {
            // Convert the strings representin the width and hdeight of the control window to a point, with 0,0 as default if it fails
            int threshold = 0;
            string svalue = ReadFromRegistry(KEY_DARKPIXELTHRESHOLD);

            if (!Int32.TryParse(svalue, out threshold)) return Constants.DEFAULT_DARK_PIXEL_THRESHOLD;
            return (threshold);
        }

        public void WriteDarkPixelThreshold(int threshold)
        {
            string svalue = Convert.ToString(threshold);
            WriteToRegistry(KEY_DARKPIXELTHRESHOLD, svalue);
        }

        public double ReadDarkPixelRatioThreshold()
        {
            // Convert the strings representin the width and hdeight of the control window to a point, with 0,0 as default if it fails
            double threshold = 0;
            string svalue = ReadFromRegistry(KEY_DARKPIXELRATIO);

            if (!Double.TryParse(svalue, out threshold)) return Constants.DEFAULT_DARK_PIXEL_RATIO_THRESHOLD;
            return (threshold);
        }

        public void WriteDarkPixelRatioThreshold(double threshold)
        {
            string svalue = Convert.ToString(threshold);
            WriteToRegistry(KEY_DARKPIXELRATIO, svalue);
        }

        public void WriteShowCSVDialog(bool flag)
        {
            string svalue = flag.ToString();
            WriteToRegistry(KEY_SHOWCSVDIALOG, svalue.ToLower());
        }

        // Save and retrive the state of audio feedback  (i.e., if its on or not)
        public bool ReadShowCSVDialog()
        {
            // Convert the string to a bool, with true as default if it fails
            string svalue = ReadFromRegistry(KEY_SHOWCSVDIALOG);
            if (null == svalue) return true;
            return (svalue.ToLower() == "false") ? false : true;
        }

        #region Private methods that actually do the reading/writing of values to the registry
        // Read a key's value from the registry
        private string ReadFromRegistry(string key)
        {
            ModifyRegistry myRegistry = new ModifyRegistry();
            myRegistry.SubKey = this.SUBKEY;
            return myRegistry.Read(key);
        }

        // Write a key's value to the registry
        private  void WriteToRegistry(string key, string svalue)
        {
            ModifyRegistry myRegistry = new ModifyRegistry();
            myRegistry.ShowError = false;
            myRegistry.SubKey = this.SUBKEY;

            myRegistry.Write(key, svalue);
        }

        #endregion
    }
}
