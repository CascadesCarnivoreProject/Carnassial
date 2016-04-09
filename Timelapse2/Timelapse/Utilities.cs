using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;


namespace Timelapse
{
    /// <summary>
    /// Utilities collect a variety of miscellaneous utility functions
    /// </summary>

    class Utilities
    {
        #region Folder paths and folder names

        // Given a folder path, return only the folder name
        static public string GetFolderNameFromFolderPath(string folder_path)
        {
            string[] directories = folder_path.Split(System.IO.Path.DirectorySeparatorChar);
            return (directories[directories.Length - 1]);
        }

        // Get a location for the Template file from the user. Return null on failure
        public static string GetTemplateFileFromUser(string defaultPath, string filename)
        {
            // Get the template file, which should be located where the images reside
            var fbd = new System.Windows.Forms.OpenFileDialog();
            fbd.Title = "Select a TimelapseTemplate.tdb file,  which should be one located in your image folder";
            fbd.CheckFileExists = true;
            fbd.CheckPathExists = true;
            fbd.Multiselect = false;
            fbd.InitialDirectory = defaultPath;
            fbd.FileName = filename;
            
            fbd.AutoUpgradeEnabled = true;

            // Set filter for file extension and default file extension 
            fbd.DefaultExt = ".tdb";
            fbd.Filter = "Template files (.tdb)|*.tdb";

            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return (fbd.FileName);                                      // Standard user-selected path
            }
            return null;                                                    // User must have aborted the operation
        }
        
        // Get a folder from the user. Return null on failure
        public static string GetFolderPathFromUser(string defaultPath)
        {
            // Get the folder where the images reside
            var fbd = new System.Windows.Forms.FolderBrowserDialog();
           
            fbd.ShowNewFolderButton = false; 
            fbd.Description = "Select a folder containing images to analyze,  where the folder should include a CodeTemplate.xml file";
            string path = defaultPath;                     // Retrieve the last opened image path from the registry
            fbd.SelectedPath = path;
  
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return (fbd.SelectedPath);                                      // Standard user-selected path
            }
            return null;                                                        // User must have aborted the operation
        }

        /// <summary> Given a bitmap, load it with the image specified in the resource file, scaled to match the width and height</summary>
        /// <param name="bi"></param>
        /// <param name="resource"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static BitmapImage BitmapFromResource(BitmapImage bi, string resource, bool to_cache, int width, int height)
        {   
            bi.BeginInit();
            if (!to_cache)
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bi.UriSource = new Uri("pack://application:,,/Resources/" + resource);
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }

        public static BitmapImage BitmapFromFile(BitmapImage bi, string imageFilepath, bool use_cached_images)
        {   
            bi.BeginInit();
            if (!use_cached_images)
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
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
