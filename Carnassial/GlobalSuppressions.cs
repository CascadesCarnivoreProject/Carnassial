﻿// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.
using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Data.SearchTerm.ToString~System.String")]
[assembly: SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time", Justification = "not supported by source generated p/invoke as of .NET 8.0", Scope = "member", Target = "~M:Carnassial.Interop.NativeMethods.SHCreateItemFromParsingName(System.String,System.Runtime.InteropServices.ComTypes.IBindCtx,System.Guid@)~System.Object")]
[assembly: SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time", Justification = "not supported by source generated p/invoke as of .NET 8.0", Scope = "member", Target = "~M:Carnassial.Interop.NativeMethods.SHFileOperation(Carnassial.Interop.NativeMethods.SHFILEOPSTRUCT@)~System.Int32")]
[assembly: SuppressMessage("Interoperability", "SYSLIB1096:Convert to 'GeneratedComInterface'", Justification = "not supported by source generated COM as of .NET 8.0", Scope = "type", Target = "~T:Carnassial.Interop.IFileOperation")]
[assembly: SuppressMessage("Interoperability", "SYSLIB1096:Convert to 'GeneratedComInterface'", Justification = "not supported by source generated COM as of .NET 8.0", Scope = "type", Target = "~T:Carnassial.Interop.IFileOperationProgressSink")]
[assembly: SuppressMessage("Interoperability", "SYSLIB1096:Convert to 'GeneratedComInterface'", Justification = "not supported by source generated COM as of .NET 8.0", Scope = "type", Target = "~T:Carnassial.Interop.IShellItem")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Data.AddFilesTransactionSequence.#ctor(Carnassial.Database.SQLiteDatabase,Carnassial.Data.ControlTable)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Data.ControlTransactionSequence.#ctor(System.Text.StringBuilder,Carnassial.Database.SQLiteDatabase,System.Data.SQLite.SQLiteTransaction)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Data.FileDatabase.DeleteFiles(System.Collections.Generic.List{System.Int64})~System.Int32")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Data.FileTransactionSequence.#ctor(System.Text.StringBuilder,Carnassial.Database.SQLiteDatabase,Carnassial.Data.FileTable)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Data.ImageSetTransactionSequence.#ctor(System.Text.StringBuilder,Carnassial.Database.SQLiteDatabase,System.Data.SQLite.SQLiteTransaction)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Data.TemplateDatabase.RemoveUserDefinedControl(Carnassial.Data.ControlRow)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Data.UpdateFileColumnTransactionSequence.#ctor(System.String,Carnassial.Database.SQLiteDatabase)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Data.UpdateFileDateTimeOffsetTransactionSequence.#ctor(Carnassial.Database.SQLiteDatabase)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.SecondaryIndex.Create(System.Data.SQLite.SQLiteConnection,System.Data.SQLite.SQLiteTransaction)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.SecondaryIndex.Drop(System.Data.SQLite.SQLiteConnection,System.Data.SQLite.SQLiteTransaction)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.Select.CreateSelect(System.Data.SQLite.SQLiteConnection,System.String)~System.Data.SQLite.SQLiteCommand")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.SQLiteDatabase.AddColumnToTable(System.Data.SQLite.SQLiteTransaction,System.String,System.Int32,Carnassial.Database.ColumnDefinition)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.SQLiteDatabase.ConvertBooleanStringColumnToInteger(System.Data.SQLite.SQLiteTransaction,System.String,System.String)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.SQLiteDatabase.ConvertNonFlagEnumStringColumnToInteger``1(System.Data.SQLite.SQLiteTransaction,System.String,System.String)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.SQLiteDatabase.CopyTableToNewSchema(System.Data.SQLite.SQLiteTransaction,System.String,Carnassial.Database.SQLiteTableSchema,System.Collections.Generic.List{Carnassial.Database.ColumnDefinition},System.Collections.Generic.List{Carnassial.Database.ColumnDefinition})")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.SQLiteDatabase.DropAndReplaceTable(System.Data.SQLite.SQLiteTransaction,System.String,System.String)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.SQLiteDatabase.GetDistinctValuesInColumn(System.String,System.String)~System.Collections.Generic.List{System.Object}")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.SQLiteDatabase.GetTableSchema(System.String)~Carnassial.Database.SQLiteTableSchema")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.SQLiteDatabase.RenameTable(System.Data.SQLite.SQLiteTransaction,System.String,System.String)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.SQLiteDatabase.SetAutoVacuum(Carnassial.Database.SQLiteAutoVacuum)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.SQLiteDatabase.SetLockingMode(Carnassial.Database.SQLiteLockMode)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.SQLiteDatabase.SetTemporaryStore(System.Data.SQLite.SQLiteConnection,Carnassial.Database.SQLiteTemporaryStore)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.SQLiteDatabase.SetUserVersion(System.Data.SQLite.SQLiteTransaction,System.Version)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.SQLiteDatabase.SetWalAutocheckpoint(System.Int32)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.SQLiteDatabase.WalCheckpoint(Carnassial.Database.SQLiteWalCheckpoint)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.SQLiteTableSchema.CreateTable(System.Data.SQLite.SQLiteConnection,System.Data.SQLite.SQLiteTransaction)")]
[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Database.TransactionSequence.CommitAndBeginNew(System.Data.SQLite.SQLiteCommand)~System.Data.SQLite.SQLiteCommand")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "debuggability", Scope = "member", Target = "~M:Carnassial.Images.FileIOComputeTransactionManager`1.GetNextComputeAtom(System.Int32)~Carnassial.Images.FileLoadAtom")]
[assembly: SuppressMessage("Style", "IDE0270:Use coalesce expression", Justification = "readability", Scope = "member", Target = "~M:Carnassial.Data.Spreadsheet.SharedStringIndex.GetSharedStrings(DocumentFormat.OpenXml.Packaging.SharedStringTablePart,Carnassial.Data.Spreadsheet.SpreadsheetReadWriteStatus)~System.Collections.Generic.List{System.String}")]
[assembly: SuppressMessage("Style", "IDE0270:Use coalesce expression", Justification = "readability", Scope = "member", Target = "~M:Carnassial.Data.Spreadsheet.SpreadsheetReaderWriter.TryImportXlsx(System.String,Carnassial.Data.FileDatabase)~Carnassial.Data.FileImportResult")]
[assembly: SuppressMessage("Style", "IDE0270:Use coalesce expression", Justification = "readability", Scope = "member", Target = "~M:Carnassial.Data.TemplateDatabase.#ctor(System.String)")]
[assembly: SuppressMessage("Style", "IDE0270:Use coalesce expression", Justification = "readability", Scope = "member", Target = "~M:Carnassial.Database.SQLiteDatabase.GetBackupFilePath~System.String")]
[assembly: SuppressMessage("Style", "IDE0270:Use coalesce expression", Justification = "readability", Scope = "member", Target = "~M:Carnassial.Interop.UnbufferedSequentialReader.#ctor(System.String)")]
[assembly: SuppressMessage("Style", "IDE0270:Use coalesce expression", Justification = "readability", Scope = "member", Target = "~M:Carnassial.Util.MostRecentlyUsedList`1.#ctor(System.Collections.IList,System.Int32)")]
[assembly: SuppressMessage("Style", "IDE0270:Use coalesce expression", Justification = "readability", Scope = "member", Target = "~M:Carnassial.Util.RegistryKeyExtensions.ReadString(Microsoft.Win32.RegistryKey,System.String)~System.String")]
[assembly: SuppressMessage("Style", "IDE0270:Use coalesce expression", Justification = "readability", Scope = "type", Target = "~T:Carnassial.CarnassialWindow")]
[assembly: SuppressMessage("Style", "IDE0270:Use coalesce expression", Justification = "readability", Scope = "type", Target = "~T:Carnassial.Data.FileDatabase")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Command.FileOrdering.#ctor(Carnassial.Data.FileTableEnumerator)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Control.DateChangesFeedbackControl.FeedbackRowTuple.#ctor(System.String,System.String,System.String,System.String,System.String)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Control.FileDisplayMessage.#ctor(System.String,System.String,System.Int32,System.String)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Control.IndexedDateTimePart.#ctor(System.Char,System.Int32,System.Int32)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Data.AutocompletionCache.#ctor(Carnassial.Data.FileDatabase)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Data.DataImportProgress.#ctor(System.Action{Carnassial.Data.DataImportProgress},System.TimeSpan)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Data.FileTable.UserControlIndices.#ctor(System.Int32,System.Int32)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Data.FileTableEnumerator.#ctor(Carnassial.Data.FileDatabase)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Data.ImageRow.#ctor(System.String,System.String,Carnassial.Data.FileTable)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Data.Marker.#ctor(System.String,System.Windows.Point)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Data.MarkersForCounter.#ctor(System.String,System.Int32)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Data.Spreadsheet.SpreadsheetReaderWriter.#ctor(System.Action{Carnassial.Data.Spreadsheet.SpreadsheetReadWriteStatus},System.TimeSpan)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Data.Spreadsheet.SpreadsheetReadWriteStatus.#ctor(System.Action{Carnassial.Data.Spreadsheet.SpreadsheetReadWriteStatus},System.TimeSpan)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Database.IndexedPropertyChangedEventArgs`1.#ctor(`0)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Database.SecondaryIndex.#ctor(System.String,System.String,System.String)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Database.Select.#ctor(System.String)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Database.SQLiteTableSchema.#ctor(System.String)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Database.WindowedTransactionSequence`1.#ctor(Carnassial.Database.SQLiteDatabase)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Dialog.DateCorrectAmbiguous.AmbiguousDate.#ctor(System.Int32,System.Int32,System.Int32,System.Boolean)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Dialog.ReclassifyIOComputeTransaction.#ctor(System.Action{Carnassial.Dialog.ReclassifyStatus},System.TimeSpan)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Images.AddFilesIOComputeTransactionManager.#ctor(System.Action{Carnassial.Images.FileLoadStatus},System.TimeSpan)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Images.MarkerCreatedOrDeletedEventArgs.#ctor(Carnassial.Data.Marker,System.Boolean)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debuggability", Scope = "member", Target = "~M:Carnassial.Util.MostRecentlyUsedList`1.#ctor(System.Int32)")]
[assembly: SuppressMessage("Usage", "CA2214:Do not call overridable methods in constructors", Justification = "reviewed", Scope = "member", Target = "~M:Carnassial.Data.FileTableEnumerator.#ctor(Carnassial.Data.FileDatabase,System.Int32)")]
