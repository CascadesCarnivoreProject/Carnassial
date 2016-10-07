﻿using System;
using System.Data;
using System.IO;

namespace Carnassial.Database
{
    public class FileTable : DataTableBackedList<ImageRow>
    {
        public FileTable(DataTable imageDataTable)
            : base(imageDataTable, FileTable.CreateRow)
        {
        }

        private static ImageRow CreateRow(DataRow row)
        {
            string fileName = row.GetStringField(Constants.DatabaseColumn.File);
            string fileExtension = Path.GetExtension(fileName);
            if (String.Equals(fileExtension, Constants.File.AviFileExtension, StringComparison.OrdinalIgnoreCase) ||
                String.Equals(fileExtension, Constants.File.Mp4FileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return new VideoRow(row);
            }
            if (String.Equals(fileExtension, Constants.File.JpgFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return new ImageRow(row);
            }
            throw new NotSupportedException(String.Format("Unhandled extension '{0}' for file '{1}'.", fileExtension, fileName));
        }

        public ImageRow NewRow(FileInfo file)
        {
            DataRow row = this.DataTable.NewRow();
            row[Constants.DatabaseColumn.File] = file.Name;
            return FileTable.CreateRow(row);
        }
    }
}