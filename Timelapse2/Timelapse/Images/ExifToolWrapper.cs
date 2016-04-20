/*
derived from the v2 C# wrapper for Exif Tool by Phil Harvey, retrieved from http://u88.n24.queensu.ca/exiftool/forum/index.php/topic,5262.0.html
see also http://www.sno.phy.queensu.ca/~phil/exiftool/

bug fixes and StyleCop cleanup pass applied
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Timelapse.Images
{
    internal class ExifToolWrapper : IDisposable
    {
        // -g for groups
        private const string Arguments = "-fast -m -q -q -stay_open True -@ - -common_args -d \"%Y.%m.%d %H:%M:%S\" -c \"%d %d %.6f\" -t";
        private const string ExeName = "exiftool(-k).exe";
        private static readonly object LockObj = new object();
        private static readonly int[] OrientationPositions = { 1, 6, 3, 8 };

        private readonly StringBuilder output = new StringBuilder();
        private readonly ProcessStartInfo psi;
        private readonly ManualResetEvent waitHandle = new ManualResetEvent(true);

        private bool disposed = false;
        private int cmdCnt = 1;
        private Process exifTool = null;
        private bool stopRequested = false;

        public enum Statuses
        {
            Stopped,
            Starting,
            Ready,
            Stopping
        }

        public string Exe { get; private set; }
        public string ExiftoolVersion { get; private set; }
        public bool Resurrect { get; set; }
        public Statuses Status { get; private set; }

        public ExifToolWrapper(string path = null)
        {
            this.Resurrect = true;

            this.Exe = string.IsNullOrEmpty(path) ? Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), ExeName) : path;
            if (!File.Exists(this.Exe))
            {
                throw new ExifToolException(ExeName + " not found");
            }

            this.psi = new ProcessStartInfo
            {
                FileName = this.Exe,
                Arguments = Arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };

            this.Status = Statuses.Stopped;
        }

        public static int OrientationDeg2Pos(int deg)
        {
            switch (deg)
            {
                case 270:
                    return 8;
                case 180:
                    return 3;
                case 90:
                    return 6;
                default:
                    return 1;
            }
        }

        public static string OrientationDeg2String(int deg)
        {
            switch (deg)
            {
                case 270:
                    return "Rotate 270 CW";
                case 180:
                    return "Rotate 180";
                case 90:
                    return "Rotate 90 CW";
                default:
                    return "Horizontal (normal)";
            }
        }

        /*
1        2       3      4         5            6           7          8

888888  888888      88  88      8888888888  88                  88  8888888888
88          88      88  88      88  88      88  88          88  88      88  88
8888      8888    8888  8888    88          8888888888  8888888888          88
88          88      88  88
88          88  888888  888888
                                                      at least 6 and 8 seems to be inverted
         */
        // TODO: maybe some kind of map would be better
        public static int OrientationPos2Deg(int pos)
        {
            switch (pos)
            {
                case 8:
                    return 270;
                case 3:
                    return 180;
                case 6:
                    return 90;
                default:
                    return 0;
            }
        }

        /*
        1 => 'Horizontal (normal)',
        2 => 'Mirror horizontal',
        3 => 'Rotate 180',
        4 => 'Mirror vertical',
        5 => 'Mirror horizontal and rotate 270 CW',
        6 => 'Rotate 90 CW',
        7 => 'Mirror horizontal and rotate 90 CW',
        8 => 'Rotate 270 CW',
        */
        public static int OrientationString2Deg(string pos)
        {
            switch (pos)
            {
                case "Rotate 270 CW":
                    return 270;
                case "Rotate 180":
                    return 180;
                case "Rotate 90 CW":
                    return 90;
                default:
                    return 0;
            }
        }

        public static int RotateOrientation(int crtOri, bool clockwise, int steps = 1)
        {
            int newOri = 1;
            int len = OrientationPositions.Length;

            if (steps % len == 0)
            {
                return crtOri;
            }

            for (int i = 0; i < len; i++)
            {
                if (crtOri == OrientationPositions[i])
                {
                    newOri = clockwise
                        ? OrientationPositions[(i + steps) % len]
                        : OrientationPositions[(i + (1 + steps / len) * len - steps) % OrientationPositions.Length];

                    break;
                }
            }

            return newOri;
        }

        public bool CloneExif(string source, string dest, bool backup = false)
        {
            if (!File.Exists(source) || !File.Exists(dest))
            {
                return false;
            }

            string exifToolOutput = this.SendCommand("{0}-tagsFromFile\n{1}\n{2}", backup ? String.Empty : "-overwrite_original\n", source, dest);

            return exifToolOutput.Contains("1 image files updated");
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            Debug.Assert(this.Status == Statuses.Ready || this.Status == Statuses.Stopped, "Invalid state");

            if (this.exifTool != null && this.Status == Statuses.Ready)
            {
                this.Stop();
            }

            this.waitHandle.Dispose();

            this.disposed = true;
            GC.SuppressFinalize(this);
        }

        public Dictionary<string, string> FetchExifFrom(string path, IEnumerable<string> tagsToKeep = null, bool keepKeysWithEmptyValues = true)
        {
            Dictionary<string, string> res = new Dictionary<string, string>();

            bool filter = tagsToKeep != null && tagsToKeep.Any();
            string lines = this.SendCommand(path);
            foreach (string line in lines.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = line.Split('\t');

                // if unexpectedly passed a directory ExifTool will list results for all files in the directory
                // this results in lines containing "==== <directory> name", "directories scanned", etc. which do not
                // conform to the tab separated format
                if (kv.Length != 2)
                {
                    throw new FormatException(String.Format("Expected line returned from {0} to be of the form <key>\t<value> but encountered the line {1} instead.", this.Exe, line));
                }

                if (kv.Length != 2 || (!keepKeysWithEmptyValues && string.IsNullOrEmpty(kv[1])))
                {
                    continue;
                }
                if (filter && !tagsToKeep.Contains(kv[0]))
                {
                    continue;
                }

                res[kv[0]] = kv[1];
            }

            return res;
        }

        public List<string> FetchExifToListFrom(string path, IEnumerable<string> tagsToKeep = null, bool keepKeysWithEmptyValues = true, string separator = ": ")
        {
            List<string> res = new List<string>();

            bool filter = tagsToKeep != null && tagsToKeep.Any();
            string exifToolOutput = this.SendCommand(path);
            foreach (string line in exifToolOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] keyValuePair = line.Split('\t');
                Debug.Assert(keyValuePair.Length == 2, "Can not parse line :'" + line + "'");

                if (keyValuePair.Length != 2 || (!keepKeysWithEmptyValues && string.IsNullOrEmpty(keyValuePair[1])))
                {
                    continue;
                }
                if (filter && !tagsToKeep.Contains(keyValuePair[0]))
                {
                    continue;
                }

                res.Add(string.Format("{0}{1}{2}", keyValuePair[0], separator, keyValuePair[1]));
            }

            return res;
        }

        public DateTime? GetCreationTime(string path)
        {
            DateTime dt;
            if (DateTime.TryParseExact(this.SendCommand("-DateTimeOriginal\n-s3\n{0}", path),
                                       "yyyy.MM.dd HH:mm:ss",
                                       CultureInfo.InvariantCulture,
                                       DateTimeStyles.AllowWhiteSpaces,
                                       out dt))
            {
                return dt;
            }

            return null;
        }

        public int GetOrientation(string path)
        {
            int o;
            if (Int32.TryParse(this.SendCommand("-Orientation\n-n\n-s3\n{0}", path).Trim(new[] { '\t', '\r', '\n' }), out o))
            {
                return o;
            }

            return 1;
        }

        public int GetOrientationDeg(string path)
        {
            return ExifToolWrapper.OrientationPos2Deg(this.GetOrientation(path));
        }

        public string SendCommand(string cmd, params object[] args)
        {
            if (this.Status != Statuses.Ready)
            {
                throw new ExifToolException("Process must be ready");
            }

            string exifToolOutput;
            lock (LockObj)
            {
                this.waitHandle.Reset();
                this.exifTool.StandardInput.WriteLine("{0}\n-execute{1}", args.Length == 0 ? cmd : string.Format(cmd, args), this.cmdCnt);
                this.waitHandle.WaitOne();

                this.cmdCnt++;

                exifToolOutput = this.output.ToString();
                this.output.Clear();
            }

            return exifToolOutput;
        }

        public bool SetExifInto(string path, Dictionary<string, string> data, bool overwriteOriginal = true)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            StringBuilder command = new StringBuilder();
            foreach (KeyValuePair<string, string> kv in data)
            {
                command.AppendFormat("-{0}={1}\n", kv.Key, kv.Value);
            }

            if (overwriteOriginal)
            {
                command.AppendLine("-overwrite_original");
            }

            command.Append(path);
            string exifToolOutput = this.SendCommand(command.ToString());

            return exifToolOutput.Contains("1 image files updated");
        }

        public bool SetOrientation(string path, int ori, bool overwriteOriginal = true)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            StringBuilder cmd = new StringBuilder();
            cmd.AppendFormat("-Orientation={0}\n-n\n-s3\n", ori);

            if (overwriteOriginal)
            {
                cmd.AppendLine("-overwrite_original");
            }

            cmd.Append(path);
            string exifToolOutput = this.SendCommand(cmd.ToString());

            return exifToolOutput.Contains("1 image files updated");
        }

        public bool SetOrientationDeg(string path, int ori, bool overwriteOriginal = true)
        {
            return this.SetOrientation(path, OrientationDeg2Pos(ori), overwriteOriginal);
        }

        public void Start()
        {
            this.stopRequested = false;

            if (this.Status != Statuses.Stopped)
            {
                throw new ExifToolException("Process is not stopped");
            }

            this.Status = Statuses.Starting;

            this.exifTool = new Process { StartInfo = this.psi, EnableRaisingEvents = true };
            this.exifTool.OutputDataReceived += this.OnOutputDataReceived;
            this.exifTool.Exited += this.OnExifToolExited;
            this.exifTool.Start();

            this.exifTool.BeginOutputReadLine();

            this.waitHandle.Reset();
            this.exifTool.StandardInput.WriteLine("-ver\n-execute0000");
            this.waitHandle.WaitOne();

            this.Status = Statuses.Ready;
        }

        public void Stop()
        {
            this.stopRequested = true;

            if (this.Status != Statuses.Ready)
            {
                throw new ExifToolException("Process must be ready");
            }

            this.Status = Statuses.Stopping;

            this.waitHandle.Reset();
            this.exifTool.StandardInput.WriteLine("-stay_open\nFalse\n");
            if (!this.waitHandle.WaitOne(5000))
            {
                if (this.exifTool != null)
                {
                    // silently swallow an eventual exception
                    try
                    {
                        this.exifTool.Kill();
                        this.exifTool.WaitForExit(2000);
                        this.exifTool.Dispose();
                    }
                    catch
                    {
                    }

                    this.exifTool = null;
                }

                this.Status = Statuses.Stopped;
            }
        }

        // detect if process is killed
        private void OnExifToolExited(object sender, EventArgs e)
        {
            if (this.exifTool != null)
            {
                this.exifTool.Dispose();
                this.exifTool = null;
            }

            this.Status = Statuses.Stopped;

            this.waitHandle.Set();

            if (!this.stopRequested && this.Resurrect)
            {
                this.Start();
            }
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
            {
                return;
            }

            if (this.Status == Statuses.Starting)
            {
                this.ExiftoolVersion = e.Data;
                this.waitHandle.Set();
            }
            else
            {
                if (e.Data.ToLower() == string.Format("{{ready{0}}}", this.cmdCnt))
                {
                    this.waitHandle.Set();
                }
                else
                {
                    this.output.AppendLine(e.Data);
                }
            }
        }
    }
}
