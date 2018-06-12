using Carnassial.Database;
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Data;
using System.Data.SQLite;

namespace Carnassial.Data
{
    // implements IList and INotifyCollectionChanged for use with data binding
    // WPF's DataGrid requires IList as it does not support IList<T>.
    public class ControlTable : SQLiteTable<ControlRow>, IList, INotifyCollectionChanged
    {
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        int ICollection.Count
        {
            get { return this.RowCount; }
        }

        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        object ICollection.SyncRoot
        {
            get { throw new NotSupportedException(); }
        }

        object IList.this[int index]
        {
            get
            {
                return this.Rows[index];
            }
            set
            {
                ControlRow control = this.Rows[index];
                this.Rows[index] = (ControlRow)value;
                this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, control, index));
            }
        }

        bool IList.IsFixedSize
        {
            get { return false; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        void ICollection.CopyTo(Array array, int arrayIndex)
        {
            ((IList)this.Rows).CopyTo(array, arrayIndex);
        }

        int IList.Add(object control)
        {
            int index = ((IList)this.Rows).Add(control);
            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, control, index));
            return index;
        }

        void IList.Clear()
        {
            this.Rows.Clear();
            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        bool IList.Contains(object control)
        {
            return this.Rows.Contains((ControlRow)control);
        }

        int IList.IndexOf(object control)
        {
            return this.Rows.IndexOf((ControlRow)control);
        }

        void IList.Insert(int index, object control)
        {
            this.Rows.Insert(index, (ControlRow)control);
            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, control, index));
        }

        void IList.Remove(object control)
        {
            bool removed = this.Rows.Remove((ControlRow)control);
            if (removed)
            {
                this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, control));
            }
        }

        void IList.RemoveAt(int index)
        {
            ControlRow control = this.Rows[index];
            this.Rows.RemoveAt(index);
            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, control, index));
        }

        public override void Load(SQLiteDataReader reader)
        {
            this.Rows.Clear();

            int controlOrderIndex = -1;
            int copyableIndex = -1;
            int dataLabelIndex = -1;
            int defaultValueIndex = -1;
            int idIndex = -1;
            int labelFileIndex = -1;
            int listIndex = -1;
            int maxWidthIndex = -1;
            int spreadsheetOrderIndex = -1;
            int tooltipIndex = -1;
            int typeIndex = -1;
            int visibleIndex = -1;
            for (int column = 0; column < reader.FieldCount; ++column)
            {
                string columnName = reader.GetName(column);
                switch (columnName)
                {
                    case Constant.Control.ControlOrder:
                        controlOrderIndex = column;
                        break;
                    case Constant.Control.Copyable:
                        copyableIndex = column;
                        break;
                    case Constant.Control.DataLabel:
                        dataLabelIndex = column;
                        break;
                    case Constant.Control.DefaultValue:
                        defaultValueIndex = column;
                        break;
                    case Constant.DatabaseColumn.ID:
                        idIndex = column;
                        break;
                    case Constant.Control.Label:
                        labelFileIndex = column;
                        break;
                    case Constant.Control.List:
                        listIndex = column;
                        break;
                    case Constant.Control.SpreadsheetOrder:
                        spreadsheetOrderIndex = column;
                        break;
                    case Constant.Control.Tooltip:
                        tooltipIndex = column;
                        break;
                    case Constant.Control.Type:
                        typeIndex = column;
                        break;
                    case Constant.Control.Visible:
                        visibleIndex = column;
                        break;
                    case Constant.Control.Width:
                        maxWidthIndex = column;
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled column '{0}' in {1} table schema.", columnName, reader.GetTableName(0)));
                }
            }

            bool allStandardColumnsPresent = (controlOrderIndex != -1) &&
                                             (copyableIndex != -1) &&
                                             (dataLabelIndex != -1) &&
                                             (defaultValueIndex != -1) &&
                                             (idIndex != -1) &&
                                             (labelFileIndex != -1) &&
                                             (listIndex != -1) &&
                                             (maxWidthIndex != -1) &&
                                             (spreadsheetOrderIndex != -1) &&
                                             (tooltipIndex != -1) &&
                                             (typeIndex != -1) &&
                                             (visibleIndex != -1);
            if (allStandardColumnsPresent == false)
            {
                throw new SQLiteException(SQLiteErrorCode.Schema, "At least one standard column is missing from table " + reader.GetTableName(0));
            }

            while (reader.Read())
            {
                // read file values
                IDataRecord row = (IDataRecord)reader;
                ControlRow control = new ControlRow()
                {
                    ControlOrder = row.GetInt64(controlOrderIndex),
                    Copyable = Boolean.Parse(row.GetString(copyableIndex)),
                    DataLabel = row.GetString(dataLabelIndex),
                    DefaultValue = row.GetString(defaultValueIndex),
                    ID = row.GetInt64(idIndex),
                    Label = row.GetString(labelFileIndex),
                    List = row.GetString(listIndex),
                    MaxWidth = row.GetInt64(maxWidthIndex),
                    SpreadsheetOrder = row.GetInt64(spreadsheetOrderIndex),
                    Tooltip = row.GetString(tooltipIndex),
                    Type = (ControlType)Enum.Parse(typeof(ControlType), row.GetString(typeIndex)),
                    Visible = Boolean.Parse(row.GetString(visibleIndex))
                };
                control.AcceptChanges();

                this.Rows.Add(control);
            }

            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
