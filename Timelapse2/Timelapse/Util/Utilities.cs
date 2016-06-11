using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace Timelapse.Util
{
    /// <summary>
    /// Utilities collect a variety of miscellaneous utility functions
    /// </summary>
    internal class Utilities
    {
        #region Folder paths and folder names
        // get a location for the template database from the user
        public static bool TryGetFileFromUser(string title, string defaultFilePath, string filter, out string selectedFilePath)
        {
            // Get the template file, which should be located where the images reside
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = title;
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            openFileDialog.Multiselect = false;
            if (String.IsNullOrWhiteSpace(defaultFilePath))
            {
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            else
            {
                openFileDialog.InitialDirectory = Path.GetDirectoryName(defaultFilePath);
                openFileDialog.FileName = Path.GetFileName(defaultFilePath);
            }
            openFileDialog.AutoUpgradeEnabled = true;

            // Set filter for file extension and default file extension 
            openFileDialog.DefaultExt = Constants.File.TemplateDatabaseFileExtension;
            openFileDialog.Filter = filter;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                selectedFilePath = openFileDialog.FileName;
                return true;
            }

            selectedFilePath = null;
            return false;
        }

        /// <summary>Given a bitmap, load it with the image specified in the resource file</summary>
        /// <param name="bitmap">bitmap to populate with the image</param>
        /// <param name="resource">embedded resource to load bitmap data from</param>
        /// <param name="cache">true to enable caching of the bitmap, false to disable caching</param>
        /// <returns>the passed in bitmap</returns>
        public static BitmapImage BitmapFromResource(BitmapImage bitmap, string resource, bool cache)
        {
            bitmap.BeginInit();
            if (!cache)
            {
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            }
            bitmap.UriSource = new Uri("pack://application:,,/Resources/" + resource);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        public static BitmapImage BitmapFromFile(BitmapImage bi, string imageFilepath, bool use_cached_images)
        {
            bi.BeginInit();
            if (!use_cached_images)
            {
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            }
            bi.UriSource = new Uri(imageFilepath);
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            bi.Freeze(); // this makes the BitmapImage threadsafe!
            return bi;
        }
        #endregion

        // Calculate the point as a ratio of its position on the image, so we can locate it regardless of the actual image size
        public static Point ConvertPointToRatio(Point p, double width, double height)
        {
            Point ratioPt = new Point((double)p.X / (double)width, (double)p.Y / (double)height);
            return ratioPt;
        }

        // The inverse of the above operation
        public static Point ConvertRatioToPoint(System.Windows.Point p, double width, double height)
        {
            Point imagePt = new Point(p.X * width, p.Y * height);
            return imagePt;
        }

        /// <summary>
        /// Format the passed value for use as string value in a SQL statement or query.
        /// </summary>
        public static string QuoteForSql(string value)
        {
            // promote null values to empty strings
            if (value == null)
            {
                return "''";
            }

            // for an input of "foo's bar" the output is "'foo''s bar'"
            return "'" + value.Replace("'", "''") + "'";
        }
    }
}
