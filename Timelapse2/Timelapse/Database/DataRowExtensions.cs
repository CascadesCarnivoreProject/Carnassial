using System;
using System.Data;

namespace Timelapse.Database
{
    public static class DataRowExtensions
    {
        public static bool GetBooleanField(this DataRow row, string column)
        {
            string fieldAsString = row.GetStringField(column);
            if (fieldAsString == null)
            {
                return false;
            }
            return String.Equals(Boolean.TrueString, fieldAsString, StringComparison.OrdinalIgnoreCase) ? true : false;
        }

        public static TEnum GetEnumField<TEnum>(this DataRow row, string column) where TEnum : struct, IComparable, IFormattable, IConvertible
        {
            string fieldAsString = row.GetStringField(column);
            if (String.IsNullOrEmpty(fieldAsString))
            {
                return default(TEnum);
            }
            return (TEnum)Enum.Parse(typeof(TEnum), fieldAsString);
        }

        public static long GetID(this DataRow row)
        {
            return row.GetLongField(Constants.DatabaseColumn.ID);
        }

        public static int GetIntegerField(this DataRow row, string column)
        {
            string fieldAsString = row.GetStringField(column);
            if (fieldAsString == null)
            {
                return -1;
            }
            return Int32.Parse(fieldAsString);
        }

        public static long GetLongField(this DataRow row, string column)
        {
            return (long)row[column];
        }

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

        public static void SetField(this DataRow row, string column, bool value)
        {
            row[column] = value.ToString().ToLowerInvariant();
        }

        public static void SetField(this DataRow row, string column, int value)
        {
            row[column] = value.ToString();
        }

        public static void SetField(this DataRow row, string column, long value)
        {
            row[column] = value;
        }

        public static void SetField(this DataRow row, string column, string value)
        {
            row[column] = value;
        }

        public static void SetField<TEnum>(this DataRow row, string column, TEnum value) where TEnum : struct, IComparable, IFormattable, IConvertible
        {
            row.SetField(column, value.ToString());
        }
    }
}
