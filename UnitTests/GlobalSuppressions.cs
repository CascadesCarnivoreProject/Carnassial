// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.
using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Design", "MSTEST0016:Test class should have test method", Justification = "rule doesn't recognize inheritance", Scope = "type", Target = "~T:Carnassial.UnitTests.CarnassialTest")]
[assembly: SuppressMessage("Style", "IDE0270:Use coalesce expression", Justification = "readability", Scope = "member", Target = "~M:Carnassial.UnitTests.CarnassialTest.CreateFile(Carnassial.Data.FileDatabase,System.TimeZoneInfo,Carnassial.UnitTests.FileExpectations,Carnassial.Images.MetadataReadResults@)~Carnassial.Data.ImageRow")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "readability, debug flow", Scope = "member", Target = "~M:Carnassial.UnitTests.FileExpectations.#ctor(System.TimeZoneInfo)")]
[assembly: SuppressMessage("Style", "IDE0350:Use implicitly typed lambda", Justification = "readability, type safety", Scope = "member", Target = "~M:Carnassial.UnitTests.DatabaseTests.CreateReuseDefaultFileDatabase")]
[assembly: SuppressMessage("Style", "IDE0350:Use implicitly typed lambda", Justification = "readability, type safety", Scope = "member", Target = "~M:Carnassial.UnitTests.DatabaseTests.ImportData")]
[assembly: SuppressMessage("Style", "IDE0350:Use implicitly typed lambda", Justification = "readability, type safety", Scope = "member", Target = "~M:Carnassial.UnitTests.DatabaseTests.RoundtripSpreadsheets")]
[assembly: SuppressMessage("Style", "IDE0350:Use implicitly typed lambda", Justification = "readability, type safety", Scope = "member", Target = "~M:Carnassial.UnitTests.UserInterfaceTests.DataEntryHandler")]
[assembly: SuppressMessage("Style", "IDE0350:Use implicitly typed lambda", Justification = "readability, type safety", Scope = "member", Target = "~M:Carnassial.UnitTests.UserInterfaceTests.ShowDialog(System.Windows.Window)")]
[assembly: SuppressMessage("Style", "IDE0350:Use implicitly typed lambda", Justification = "readability, type safety", Scope = "member", Target = "~M:Carnassial.UnitTests.UserInterfaceTests.WaitForRenderingComplete")]
[assembly: SuppressMessage("Usage", "MSTEST0037:Use proper 'Assert' methods", Justification = "readability", Scope = "member", Target = "~T:Carnassial.UnitTests.CarnassialTest")]
[assembly: SuppressMessage("Usage", "MSTEST0037:Use proper 'Assert' methods", Justification = "readability", Scope = "member", Target = "~T:Carnassial.UnitTests.ControlExpectations")]
[assembly: SuppressMessage("Usage", "MSTEST0037:Use proper 'Assert' methods", Justification = "readability", Scope = "member", Target = "~T:Carnassial.UnitTests.ControlsExpectation")]
[assembly: SuppressMessage("Usage", "MSTEST0037:Use proper 'Assert' methods", Justification = "readability", Scope = "member", Target = "~T:Carnassial.UnitTests.DatabaseTests")]
[assembly: SuppressMessage("Usage", "MSTEST0037:Use proper 'Assert' methods", Justification = "readability", Scope = "member", Target = "~T:Carnassial.UnitTests.FileExpectations")]
[assembly: SuppressMessage("Usage", "MSTEST0037:Use proper 'Assert' methods", Justification = "readability", Scope = "member", Target = "~T:Carnassial.UnitTests.FileTests")]
[assembly: SuppressMessage("Usage", "MSTEST0037:Use proper 'Assert' methods", Justification = "readability", Scope = "member", Target = "~T:Carnassial.UnitTests.LowLevelTests")]
[assembly: SuppressMessage("Usage", "MSTEST0037:Use proper 'Assert' methods", Justification = "readability", Scope = "member", Target = "~T:Carnassial.UnitTests.SettingsTests")]
[assembly: SuppressMessage("Usage", "MSTEST0037:Use proper 'Assert' methods", Justification = "readability", Scope = "member", Target = "~T:Carnassial.UnitTests.UserInterfaceTests")]
