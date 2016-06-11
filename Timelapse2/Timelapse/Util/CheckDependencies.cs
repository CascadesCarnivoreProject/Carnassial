using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Timelapse.Util
{
    class CheckDependencies
    {
        /// <summary>
        /// If any dependency files are missing, return false else true
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool AreDependenciesMissing(string path)
        {
            string directoryContainingTimelapse = Path.GetDirectoryName(path);
            string[] dependencies = new string[]
            {
                "System.Data.SQLite.dll",
                "System.Data.SQLite.xml",
                "x64/SQLite.Interop.dll",
                "x86/SQLite.Interop.dll",
                "exiftool(-k).exe",
                "Timelapse2.exe"
            };

            foreach (string dependency in dependencies)
            {
                if (false == File.Exists(Path.Combine(directoryContainingTimelapse, dependency)))
                    return true;
            }
            return false;
        }
    }
}
