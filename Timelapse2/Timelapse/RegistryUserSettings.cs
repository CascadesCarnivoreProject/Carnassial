using System;
using Microsoft.Win32;

namespace Utility.ModifyRegistry
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

        protected void WriteToRegistry(string subKeyPath, bool value)
        {
            this.WriteToRegistry(subKeyPath, value.ToString().ToLowerInvariant());
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
