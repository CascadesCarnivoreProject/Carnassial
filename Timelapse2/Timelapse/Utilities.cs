using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace Timelapse
{
    /// <summary>
    /// Utilities collect a variety of miscellaneous utility functions
    /// </summary>
    internal class Utilities
    {
        #region Folder paths and folder names

        // Given a folder path, return only the folder name
        public static string GetFolderNameFromFolderPath(string folder_path)
        {
            string[] directories = folder_path.Split(Path.DirectorySeparatorChar);
            return directories[directories.Length - 1];
        }

        // get a location for the template database from the user
        public static bool TryGetTemplateFileFromUser(string defaultTemplateFilePath, out string templateDatabasePath)
        {
            // Get the template file, which should be located where the images reside
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Select a TimelapseTemplate.tdb file, which should be one located in your image folder";
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            openFileDialog.Multiselect = false;
            if (String.IsNullOrWhiteSpace(defaultTemplateFilePath))
            {
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            else
            {
                openFileDialog.InitialDirectory = Path.GetDirectoryName(defaultTemplateFilePath);
                openFileDialog.FileName = Path.GetFileName(defaultTemplateFilePath);
            }
            openFileDialog.AutoUpgradeEnabled = true;

            // Set filter for file extension and default file extension 
            openFileDialog.DefaultExt = Constants.File.TemplateDatabaseFileExtension;
            openFileDialog.Filter = String.Format("Template files ({0})|*{0}", Constants.File.TemplateDatabaseFileExtension);

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                templateDatabasePath = openFileDialog.FileName;
                return true;
            }

            templateDatabasePath = null;
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
    }

    // Classes for database updates. The main idea is to 
    // - supply a list of multiple column-value pairs, where list time indicates where it should apply

    // A tuple comprising a Column and a Value
    public class ColumnTuple
    {
        public string ColumnName { get; set; }
        public object ColumnValue { get; set; }

        public ColumnTuple(string column, object value)
        {
            this.ColumnName = column;
            this.ColumnValue = value;
        }
    }

    // A list of ColumnTuples
    public class ColumnTupleList : List<ColumnTuple>
    {
    }

    // A tuple where the first item is a columntuble and the second a string indicating 'where' it would apply
    public class ColumnTupleListWhere
    {
        public ColumnTupleList Listpair { get; set; }
        public string Where { get; set; }

        public ColumnTupleListWhere(ColumnTupleList listpair, string where)
        {
            this.Listpair = listpair;
            this.Where = where;
        }
        public ColumnTupleListWhere()
        {
        }
    }
}
