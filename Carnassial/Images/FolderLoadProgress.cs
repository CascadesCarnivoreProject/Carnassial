﻿using System;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Carnassial.Images
{
    internal class FolderLoadProgress
    {
        public BitmapSource BitmapSource { get; set; }
        public int CurrentFile { get; set; }
        public string CurrentFileName { get; set; }
        public int TotalFiles { get; private set; }

        public FolderLoadProgress(int totalFiles)
        {
            this.BitmapSource = null;
            this.CurrentFile = 0;
            this.CurrentFileName = null;
            this.TotalFiles = totalFiles;
        }

        public string GetMessage()
        {
            return String.Format("Loading file {0} of {1} ({2})...", this.CurrentFile, this.TotalFiles, this.CurrentFileName);
        }
    }
}
