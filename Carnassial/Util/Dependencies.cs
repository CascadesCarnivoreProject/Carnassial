using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;

namespace Carnassial.Util
{
    public class Dependencies
    {
        private static readonly List<string> CarnassialRequiredBinaries = new List<string>()
        {
            "DocumentFormat.OpenXml.dll", // OpenXML SDK
            "Microsoft.WindowsAPICodePack.dll", // required by Microsoft.WindowsAPICodePack.Shell.dll
            "Microsoft.WindowsAPICodePack.Shell.dll", // just for CarnassialWindow's use of CommonOpenFileDialog
            "System.IO.Packaging.dll" // OpenXML SDK (System.IO.FileSystem.Primitives.dll not currently needed)
        };

        private static readonly List<string> CommonRequiredBinaries = new List<string>()
        {
            "Newtonsoft.Json.dll",
            // MetadataExtractor
            "MetadataExtractor.dll",
            "XmpCore.dll",
            // SQLite
            "System.Data.SQLite.dll",
            "System.Data.SQLite.xml",
            "x64/SQLite.Interop.dll",
            "x86/SQLite.Interop.dll"
        };

        private static readonly List<string> EditorRequiredBinaries = new List<string>()
        {
            "Carnassial.exe"
        };

        /// <summary>
        /// If any dependency files are missing, return false else true
        /// </summary>
        public static bool AreRequiredBinariesPresent(string applicationName, Assembly executingAssembly)
        {
            string directoryContainingCurrentExecutable = Path.GetDirectoryName(executingAssembly.Location);
            foreach (string binaryName in Dependencies.CommonRequiredBinaries)
            {
                if (File.Exists(Path.Combine(directoryContainingCurrentExecutable, binaryName)) == false)
                {
                    return false;
                }
            }

            if (String.Equals(applicationName, Constant.ApplicationName, StringComparison.Ordinal))
            {
                foreach (string binaryName in Dependencies.CarnassialRequiredBinaries)
                {
                    if (File.Exists(Path.Combine(directoryContainingCurrentExecutable, binaryName)) == false)
                    {
                        return false;
                    }
                }
            }
            else
            {
                foreach (string binaryName in Dependencies.EditorRequiredBinaries)
                {
                    if (File.Exists(Path.Combine(directoryContainingCurrentExecutable, binaryName)) == false)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static void ShowMissingBinariesDialog(string applicationName)
        {
            // can't use DialogMessageBox to show this message as that class requires the Carnassial window to be displayed.
            string messageTitle = String.Format("{0} needs to be in its original downloaded folder.", applicationName);
            StringBuilder message = new StringBuilder("Problem:" + Environment.NewLine);
            message.AppendFormat("{0} won't run properly as it was not correctly installed.", applicationName);
            message.AppendLine();
            message.AppendLine();
            message.AppendLine("Reason:");
            message.AppendFormat("When {0} was installed it was in a folder with several other files and folders it needs. Was it moved out of that folder?", applicationName);
            message.AppendLine();
            message.AppendLine();
            message.AppendLine("Solution:");
            message.AppendFormat("Move {0} back to its original folder or reinstall it.", applicationName);
            message.AppendLine();
            message.AppendLine();
            message.AppendLine("Hint:");
            message.AppendFormat("Create a shortcut if you want to access {0} outside its folder:", applicationName);
            message.AppendLine();
            message.AppendLine("1. From its original folder, right-click the Carnassial program icon.");
            message.AppendLine("2. Select 'Create Shortcut' from the menu.");
            message.Append("3. Drag the shortcut icon to the location of your choice.");
            MessageBox.Show(message.ToString(), messageTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
