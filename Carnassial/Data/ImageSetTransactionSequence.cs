using Carnassial.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Text;

namespace Carnassial.Data
{
    public class ImageSetTransactionSequence : TransactionSequence
    {
        private readonly SQLiteCommand insertOrUpdateImageSet;
        private readonly bool isInsert;

        protected ImageSetTransactionSequence(StringBuilder command, SQLiteDatabase database, SQLiteTransaction transaction)
            : base(database, transaction)
        {
            this.insertOrUpdateImageSet = new SQLiteCommand(command.ToString(), this.Database.Connection, this.Transaction);
            this.insertOrUpdateImageSet.Parameters.Add(new SQLiteParameter("@" + Constant.ImageSetColumn.FileSelection));
            this.insertOrUpdateImageSet.Parameters.Add(new SQLiteParameter("@" + Constant.ImageSetColumn.InitialFolderName));
            this.insertOrUpdateImageSet.Parameters.Add(new SQLiteParameter("@" + Constant.ImageSetColumn.Log));
            this.insertOrUpdateImageSet.Parameters.Add(new SQLiteParameter("@" + Constant.ImageSetColumn.MostRecentFileID));
            this.insertOrUpdateImageSet.Parameters.Add(new SQLiteParameter("@" + Constant.ImageSetColumn.Options));
            this.insertOrUpdateImageSet.Parameters.Add(new SQLiteParameter("@" + Constant.ImageSetColumn.TimeZone));
            this.insertOrUpdateImageSet.Parameters.Add(new SQLiteParameter("@" + Constant.DatabaseColumn.ID));
            this.isInsert = this.insertOrUpdateImageSet.CommandText.StartsWith("INSERT", StringComparison.Ordinal);
        }

        public static ImageSetTransactionSequence CreateInsert(SQLiteDatabase database)
        {
            return ImageSetTransactionSequence.CreateInsert(database, null);
        }

        public static ImageSetTransactionSequence CreateInsert(SQLiteDatabase database, SQLiteTransaction transaction)
        {
            List<string> columns = new List<string>(Constant.ImageSetColumn.Columns.Count);
            List<string> parameterNames = new List<string>(Constant.ImageSetColumn.Columns.Count);
            foreach (string column in Constant.ImageSetColumn.Columns)
            {
                columns.Add(column);
                parameterNames.Add("@" + column);
            }

            StringBuilder insertCommand = new StringBuilder("INSERT INTO " + Constant.DatabaseTable.ImageSet + " (" + String.Join(", ", columns) + ") VALUES (" + String.Join(", ", parameterNames) + ")");
            return new ImageSetTransactionSequence(insertCommand, database, transaction);
        }

        public static ImageSetTransactionSequence CreateUpdate(SQLiteDatabase database)
        {
            return ImageSetTransactionSequence.CreateUpdate(database, null);
        }

        public static ImageSetTransactionSequence CreateUpdate(SQLiteDatabase database, SQLiteTransaction transaction)
        {
            StringBuilder updateCommand = new StringBuilder("UPDATE " + Constant.DatabaseTable.ImageSet + " SET ");
            List<string> parameters = new List<string>(Constant.ImageSetColumn.Columns.Count);
            foreach (string column in Constant.ImageSetColumn.Columns)
            {
                parameters.Add(column + "=@" + column);
            }
            updateCommand.Append(String.Join(", ", parameters));
            updateCommand.Append(" WHERE " + Constant.DatabaseColumn.ID + "=@" + Constant.DatabaseColumn.ID);

            return new ImageSetTransactionSequence(updateCommand, database, transaction);
        }

        public void AddImageSet(ImageSetRow imageSet)
        {
            if ((this.isInsert == false) && (imageSet.HasChanges == false))
            {
                return;
            }

            this.insertOrUpdateImageSet.Parameters[0].Value = (int)imageSet.FileSelection;
            this.insertOrUpdateImageSet.Parameters[1].Value = imageSet.InitialFolderName;
            this.insertOrUpdateImageSet.Parameters[2].Value = imageSet.Log;
            this.insertOrUpdateImageSet.Parameters[3].Value = imageSet.MostRecentFileID;
            this.insertOrUpdateImageSet.Parameters[4].Value = (int)imageSet.Options;
            this.insertOrUpdateImageSet.Parameters[5].Value = imageSet.TimeZone;
            this.insertOrUpdateImageSet.Parameters[6].Value = imageSet.ID;

            this.insertOrUpdateImageSet.ExecuteNonQuery();
            imageSet.AcceptChanges();
        }
    }
}