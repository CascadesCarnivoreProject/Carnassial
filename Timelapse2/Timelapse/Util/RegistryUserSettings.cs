using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace Timelapse.Util
{
    /// <summary>
    /// Base class for manipulating an application's user preferences and related information in the registry.
    /// </summary>
    internal class RegistryUserSettings : IDisposable
    {
        private bool disposed;
        protected RegistryKey RegistryKey { get; private set; }

        public RegistryUserSettings(string keyPath)
        {
            this.disposed = false;
            this.RegistryKey = Registry.CurrentUser.OpenSubKey(keyPath, true);
            if (this.RegistryKey == null)
            {
                this.RegistryKey = Registry.CurrentUser.CreateSubKey(keyPath);
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.RegistryKey.Dispose();
            }

            this.disposed = true;
        }

        protected bool ReadBooleanFromRegistry(string subKeyPath, bool defaultValue)
        {
            string valueAsString = this.ReadStringFromRegistry(subKeyPath);
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

        protected double ReadDoubleFromRegistry(string subKeyPath, double defaultValue)
        {
            string valueAsString = this.ReadStringFromRegistry(subKeyPath);
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

        protected int ReadIntegerFromRegistry(string subKeyPath, int defaultValue)
        {
            object value = this.RegistryKey.GetValue(subKeyPath);
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

            throw new NotSupportedException(String.Format("Registry key {0}\\{1} has unhandled type {2}.", this.RegistryKey.Name, subKeyPath, value.GetType().FullName));
        }

        // read a REG_SZ key's value from the registry
        protected string ReadStringFromRegistry(string subKeyPath)
        {
            return (string)this.RegistryKey.GetValue(subKeyPath);
        }

        // read a series of REG_SZ keys' values from the registry
        protected MostRecentlyUsedList<string> ReadMostRecentlyUsedListFromRegistry(string subKeyPath)
        {
            RegistryKey subKey = this.RegistryKey.OpenSubKey(subKeyPath);
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

        protected void WriteToRegistry(string subKeyPath, bool value)
        {
            this.WriteToRegistry(subKeyPath, value.ToString().ToLowerInvariant());
        }

        protected void WriteToRegistry(string subKeyPath, MostRecentlyUsedList<string> values)
        {
            if (values != null)
            {
                // create the key whose values represent elements of the list
                RegistryKey subKey = this.RegistryKey.OpenSubKey(subKeyPath, true);
                if (subKey == null)
                {
                    subKey = this.RegistryKey.CreateSubKey(subKeyPath);
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

        protected void WriteToRegistry(string subKeyPath, int value)
        {
            this.RegistryKey.SetValue(subKeyPath, value, RegistryValueKind.DWord);
        }

        protected void WriteToRegistry(string subKeyPath, string value)
        {
            this.RegistryKey.SetValue(subKeyPath, value, RegistryValueKind.String);
        }
    }
}
