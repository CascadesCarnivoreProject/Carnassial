using System;
using System.Data;

namespace Timelapse.Database
{
    public static class DataRowExtensions
    {
        public static string GetStringField(this DataRow row, string column)
        {
            // throws ArgumentException if column is not present in table
            object field = row[column];

            // SQLite assigns both String.Empty and null to DBNull on input
            if (field is DBNull)
            {
                return null;
            }
            return (string)field;
        }
    }
}
