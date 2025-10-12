using Carnassial.Data;
using System;
using System.Data.SQLite;
using System.Globalization;

namespace Carnassial.Database
{
    public class SecondaryIndex
    {
        public string Column { get; private init; }
        public string Name { get; private init; }
        public string Table { get; private init; }

        public SecondaryIndex(string table, string name, string column)
        {
            this.Column = column;
            this.Name = name;
            this.Table = table;
        }

        public void Create(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            string createIndex = String.Create(CultureInfo.InvariantCulture, $"CREATE INDEX {SQLiteDatabase.QuoteIdentifier(this.Name)} ON {this.Table} ({SQLiteDatabase.QuoteIdentifier(this.Column)})");
            using SQLiteCommand command = new(createIndex, connection, transaction);
            command.ExecuteNonQuery();
        }

        public static SecondaryIndex CreateFileTableIndex(ControlRow control)
        {
            string indexName = $"File{control.DataLabel}Index";
            return new SecondaryIndex(Constant.DatabaseTable.Files, indexName, control.DataLabel);
        }

        public void Drop(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            string dropIndex = String.Create(CultureInfo.InvariantCulture, $"DROP INDEX {SQLiteDatabase.QuoteIdentifier(this.Name)}");
            using SQLiteCommand command = new(dropIndex, connection, transaction);
            command.ExecuteNonQuery();
        }
    }
}
