using Carnassial.Database;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace Carnassial.Data
{
    public class FileTable : DataTableBackedList<ImageRow>
    {
        public FileTable(DataTable imageDataTable)
            : base(imageDataTable, FileTable.CreateRow)
        {
        }

        private static ImageRow CreateRow(DataRow row)
        {
            string fileName = row.GetStringField(Constant.DatabaseColumn.File);
            string fileExtension = Path.GetExtension(fileName);
            if (String.Equals(fileExtension, Constant.File.AviFileExtension, StringComparison.OrdinalIgnoreCase) ||
                String.Equals(fileExtension, Constant.File.Mp4FileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return new VideoRow(row);
            }
            if (String.Equals(fileExtension, Constant.File.JpgFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return new ImageRow(row);
            }
            throw new NotSupportedException(String.Format("Unhandled extension '{0}' for file '{1}'.", fileExtension, fileName));
        }

        public ImageRow NewRow(FileInfo file)
        {
            // NewRow() isn't thread safe and this function is called concurrently during folder loading
            DataRow row;
            lock (this.DataTable)
            {
                row = this.DataTable.NewRow();
            }

            row[Constant.DatabaseColumn.ID] = Constant.Database.InvalidID;
            row[Constant.DatabaseColumn.File] = file.Name;
            return FileTable.CreateRow(row);
        }

        public List<ImageRow> Select(string relativePath, string fileName)
        {
            string where = String.Format("{0} = '{1}'", Constant.DatabaseColumn.File, fileName);
            if (String.IsNullOrEmpty(relativePath) == false)
            {
                where += String.Format(" AND {0} = '{1}'", Constant.DatabaseColumn.RelativePath, relativePath);
            }

            DataRow[] matchingRows = this.DataTable.Select(where);
            if (matchingRows == null)
            {
                return new List<ImageRow>();
            }

            List<ImageRow> files = new List<ImageRow>(matchingRows.Length);
            foreach (DataRow row in matchingRows)
            {
                files.Add(FileTable.CreateRow(row));
            }
            return files;
        }
    }
}
