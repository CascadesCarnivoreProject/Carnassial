using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Carnassial.Images
{
    internal class FolderLoad
    {
        public List<string> FolderPaths { get; private set; }

        public FolderLoad()
        {
            this.FolderPaths = new List<string>();
        }

        public List<FileInfo> GetFiles()
        {
            List<FileInfo> filesToAdd = new List<FileInfo>();
            foreach (string folderPath in this.FolderPaths)
            {
                DirectoryInfo folder = new DirectoryInfo(folderPath);
                foreach (string extension in new List<string>() { Constant.File.AviFileExtension, Constant.File.Mp4FileExtension, Constant.File.JpgFileExtension })
                {
                    filesToAdd.AddRange(folder.GetFiles("*" + extension));
                }
            }
            filesToAdd = filesToAdd.OrderBy(file => file.FullName).ToList();
            return filesToAdd;
        }
    }
}
