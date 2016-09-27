﻿using Microsoft.Win32;
using System;
using System.Windows;

namespace Carnassial.Util
{
    public static class RegistryKeyExtensions
    {
        public static bool ReadBoolean(this RegistryKey registryKey, string subKeyPath, bool defaultValue)
        {
            string valueAsString = registryKey.ReadString(subKeyPath);
            if (valueAsString != null)
            {
                bool value;
                if (Boolean.TryParse(valueAsString, out value))
                {
                    return value;
                }
            }

            return defaultValue;
        }

        public static double ReadDouble(this RegistryKey registryKey, string subKeyPath, double defaultValue)
        {
            string valueAsString = registryKey.ReadString(subKeyPath);
            if (valueAsString != null)
            {
                double value;
                if (Double.TryParse(valueAsString, out value))
                {
                    return value;
                }
            }

            return defaultValue;
        }

        public static TEnum ReadEnum<TEnum>(this RegistryKey registryKey, string subKeyPath, TEnum defaultValue) where TEnum : struct, IComparable, IConvertible, IFormattable
        {
            string valueAsString = registryKey.ReadString(subKeyPath);
            if (valueAsString != null)
            {
                return (TEnum)Enum.Parse(typeof(TEnum), valueAsString);
            }

            return defaultValue;
        }

        public static int ReadInteger(this RegistryKey registryKey, string subKeyPath, int defaultValue)
        {
            object value = registryKey.GetValue(subKeyPath);
            if (value == null)
            {
                return defaultValue;
            }

            if (value is Int32)
            {
                return (int)value;
            }

            if (value is string)
            {
                return Int32.Parse((string)value);
            }

            throw new NotSupportedException(String.Format("Registry key {0}\\{1} has unhandled type {2}.", registryKey.Name, subKeyPath, value.GetType().FullName));
        }

        public static Point ReadPoint(this RegistryKey registryKey, string subKeyPath, Point defaultValue)
        {
            string pointAsString = registryKey.ReadString(subKeyPath);
            if (pointAsString == null)
            {
                return defaultValue;
            }
            return Point.Parse(pointAsString);
        }

        public static Size ReadSize(this RegistryKey registryKey, string subKeyPath, Size defaultValue)
        {
            string sizeAsString = registryKey.ReadString(subKeyPath);
            if (sizeAsString == null)
            {
                return defaultValue;
            }
            return Size.Parse(sizeAsString);
        }

        // read a REG_SZ key's value from the registry
        public static string ReadString(this RegistryKey registryKey, string subKeyPath)
        {
            return (string)registryKey.GetValue(subKeyPath);
        }

        // read a series of REG_SZ keys' values from the registry
        public static MostRecentlyUsedList<string> ReadMostRecentlyUsedList(this RegistryKey registryKey, string subKeyPath)
        {
            RegistryKey subKey = registryKey.OpenSubKey(subKeyPath);
            MostRecentlyUsedList<string> values = new MostRecentlyUsedList<string>(Constants.NumberOfMostRecentDatabasesToTrack);

            if (subKey != null)
            {
                for (int index = subKey.ValueCount - 1; index >= 0; --index)
                {
                    string listItem = (string)subKey.GetValue(index.ToString());
                    if (listItem != null)
                    {
                        values.SetMostRecent(listItem);
                    }
                }
            }

            return values;
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, bool value)
        {
            registryKey.Write(subKeyPath, value.ToString().ToLowerInvariant());
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, double value)
        {
            registryKey.Write(subKeyPath, value.ToString());
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, MostRecentlyUsedList<string> values)
        {
            if (values != null)
            {
                // create the key whose values represent elements of the list
                RegistryKey subKey = registryKey.OpenSubKey(subKeyPath, true);
                if (subKey == null)
                {
                    subKey = registryKey.CreateSubKey(subKeyPath);
                }

                // write the values
                int index = 0;
                foreach (string value in values)
                {
                    subKey.SetValue(index.ToString(), value);
                    ++index;
                }

                // remove any additional values when the new list is shorter than the old one
                int maximumValueName = subKey.ValueCount;
                for (; index < maximumValueName; ++index)
                {
                    subKey.DeleteValue(index.ToString());
                }
            }
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, int value)
        {
            registryKey.SetValue(subKeyPath, value, RegistryValueKind.DWord);
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, Point value)
        {
            registryKey.Write(subKeyPath, value.ToString());
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, Size value)
        {
            registryKey.Write(subKeyPath, value.ToString());
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, string value)
        {
            registryKey.SetValue(subKeyPath, value, RegistryValueKind.String);
        }
    }
}
