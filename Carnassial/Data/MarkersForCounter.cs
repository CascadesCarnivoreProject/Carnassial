using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;

namespace Carnassial.Data
{
    public class MarkersForCounter : INotifyPropertyChanged
    {
        public int Count { get; private set; }
        public string DataLabel { get; private set; }
        public List<Marker> Markers { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public MarkersForCounter(string dataLabel, int count)
        {
            this.Count = count;
            this.DataLabel = dataLabel;
            this.Markers = new List<Marker>();
        }

        public void AddMarker(Marker marker)
        {
            Debug.Assert(String.Equals(marker.DataLabel, this.DataLabel, StringComparison.Ordinal), "Marker is associated with a different counter.");

            ++this.Count;
            this.Markers.Add(marker);
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Markers)));
        }

        public static bool IsValidExcelString(string value)
        {
            if (String.IsNullOrEmpty(value))
            {
                // position string might not contain any positions
                return true;
            }

            // count string
            if (Int32.TryParse(value, out int ignored))
            {
                return true;
            }

            // position string
            for (int index = 0; index < value.Length; ++index)
            {
                char character = value[index];
                if (character == Constant.Excel.MarkerPositionSeparator)
                {
                    continue;
                }

                // must be kept in sync with MarkerPositionsToExcelString()
                // 0         1
                // 01234567890123456  7
                // d.dddddd,d.dddddd (|d.dddddd,d.dddddd)*
                if ((value.Length < (index + 2 * Constant.Excel.MarkerPositionFormat.Length + 1)) ||
                    (value[index + 1] != '.') ||
                    (value[index + 8] != ',') ||
                    (value[index + 10] != '.') ||
                    ((value[index] != '0') && (value[index] != '1')) ||
                    ((value[index + 9] != '0') && (value[index + 9] != '1')) ||
                    (Char.IsDigit(value, index + 2) == false) ||
                    (Char.IsDigit(value, index + 3) == false) ||
                    (Char.IsDigit(value, index + 4) == false) ||
                    (Char.IsDigit(value, index + 5) == false) ||
                    (Char.IsDigit(value, index + 6) == false) ||
                    (Char.IsDigit(value, index + 7) == false) ||
                    (Char.IsDigit(value, index + 11) == false) ||
                    (Char.IsDigit(value, index + 12) == false) ||
                    (Char.IsDigit(value, index + 13) == false) ||
                    (Char.IsDigit(value, index + 14) == false) ||
                    (Char.IsDigit(value, index + 15) == false) ||
                    (Char.IsDigit(value, index + 16) == false))
                {
                    return false;
                }

                index += 2 * Constant.Excel.MarkerPositionFormat.Length;
            }

            return true;
        }

        public byte[] MarkerPositionsToFloatArray()
        {
            if (this.Markers.Count < 1)
            {
                return null;
            }

            byte[] packedFloats = new byte[2 * this.Markers.Count * sizeof(float)];
            for (int byteIndex = -1, markerIndex = 0; markerIndex < this.Markers.Count; ++markerIndex)
            {
                Marker marker = this.Markers[markerIndex];
                byte[] xBytes = BitConverter.GetBytes((float)marker.Position.X);
                Debug.Assert(xBytes.Length == 4, "Expected 32 bit float for marker x position.");
                packedFloats[++byteIndex] = xBytes[0];
                packedFloats[++byteIndex] = xBytes[1];
                packedFloats[++byteIndex] = xBytes[2];
                packedFloats[++byteIndex] = xBytes[3];

                byte[] yBytes = BitConverter.GetBytes((float)marker.Position.Y);
                Debug.Assert(yBytes.Length == 4, "Expected 32 bit float for marker y position.");
                packedFloats[++byteIndex] = yBytes[0];
                packedFloats[++byteIndex] = yBytes[1];
                packedFloats[++byteIndex] = yBytes[2];
                packedFloats[++byteIndex] = yBytes[3];
            }
            return packedFloats;
        }

        public static byte[] MarkerPositionsFromExcelString(string value)
        {
            Debug.Assert(value != null, "Position string unexpectedly null.");

            string[] positions = value.Split(Constant.Excel.MarkerPositionSeparator);
            byte[] packedFloats = new byte[2 * positions.Length * sizeof(float)];
            int byteIndex = -1;
            foreach (string position in positions)
            {
                string[] xy = position.Split(Constant.Excel.MarkerCoordinateSeparator);
                Debug.Assert(xy.Length == 2, "IsValidExcelString() failed to detect malformed coordinate pair.");

                float x = float.Parse(xy[0]);
                byte[] xBytes = BitConverter.GetBytes(x);
                Debug.Assert(xBytes.Length == 4, "Expected 32 bit float for marker x position.");
                packedFloats[++byteIndex] = xBytes[0];
                packedFloats[++byteIndex] = xBytes[1];
                packedFloats[++byteIndex] = xBytes[2];
                packedFloats[++byteIndex] = xBytes[3];

                float y = float.Parse(xy[1]);
                byte[] yBytes = BitConverter.GetBytes(y);
                Debug.Assert(yBytes.Length == 4, "Expected 32 bit float for marker y position.");
                packedFloats[++byteIndex] = yBytes[0];
                packedFloats[++byteIndex] = yBytes[1];
                packedFloats[++byteIndex] = yBytes[2];
                packedFloats[++byteIndex] = yBytes[3];
            }

            return packedFloats;
        }

        public void MarkerPositionsFromFloatArray(byte[] packedFloats)
        {
            if (packedFloats == null)
            {
                // null argument indicates no positions
                return;
            }

            if (packedFloats.Length % (2 * sizeof(float)) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(packedFloats), "Position array does not contain an exact number of paired floats.");
            }

            for (int index = 0; index < packedFloats.Length; index += 2 * sizeof(float))
            {
                float x = BitConverter.ToSingle(packedFloats, index);
                float y = BitConverter.ToSingle(packedFloats, index + sizeof(float));
                this.Markers.Add(new Marker(this.DataLabel, new Point(x, y)));
            }
        }

        public string MarkerPositionsToExcelString()
        {
            if (this.Markers.Count < 1)
            {
                return null;
            }

            Marker marker = this.Markers[0];
            StringBuilder pointList = new StringBuilder(marker.Position.X.ToString(Constant.Excel.MarkerPositionFormat) + Constant.Excel.MarkerCoordinateSeparator + marker.Position.Y.ToString(Constant.Excel.MarkerPositionFormat));
            for (int index = 1; index < this.Markers.Count; ++index)
            {
                marker = this.Markers[index];
                pointList.Append(Constant.Excel.MarkerPositionSeparator + marker.Position.X.ToString(Constant.Excel.MarkerPositionFormat) + "," + marker.Position.Y.ToString(Constant.Excel.MarkerPositionFormat));
            }
            return pointList.ToString();
        }

        public static string MarkerPositionsToExcelString(byte[] packedFloats)
        {
            if ((packedFloats == null) || (packedFloats.Length == 0))
            {
                return null;
            }
            if (packedFloats.Length % (2 * sizeof(float)) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(packedFloats), "Position array does not contain an exact number of paired floats.");
            }

            float x = BitConverter.ToSingle(packedFloats, 0);
            float y = BitConverter.ToSingle(packedFloats, sizeof(float));
            StringBuilder pointList = new StringBuilder(x.ToString(Constant.Excel.MarkerPositionFormat) + Constant.Excel.MarkerCoordinateSeparator + y.ToString(Constant.Excel.MarkerPositionFormat));
            for (int index = 2 * sizeof(float); index < packedFloats.Length; index += 2 * sizeof(float))
            {
                x = BitConverter.ToSingle(packedFloats, index);
                y = BitConverter.ToSingle(packedFloats, index + sizeof(float));
                pointList.Append(Constant.Excel.MarkerPositionSeparator + x.ToString(Constant.Excel.MarkerPositionFormat) + "," + y.ToString(Constant.Excel.MarkerPositionFormat));
            }
            return pointList.ToString();
        }

        public void RemoveMarker(Marker marker)
        {
            Debug.Assert(String.Equals(marker.DataLabel, this.DataLabel, StringComparison.Ordinal), "Marker is associated with a different counter.");

            for (int index = 0; index < this.Markers.Count; ++index)
            {
                Marker candidate = this.Markers[index];
                if (marker.Position == candidate.Position)
                {
                    this.Markers.RemoveAt(index);
                    --this.Count;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Markers)));
                    return;
                }
            }

            Debug.Fail("Attempt to remove marker not associated with counter.");
        }
    }
}
