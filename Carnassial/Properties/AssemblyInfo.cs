using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Carnassial")]
[assembly: AssemblyDescription("A tool to simplify analysis of images and videos from remote cameras.")]
[assembly: AssemblyCompany("Cascades Carnivore Project")]
[assembly: AssemblyProduct("Carnassial")]
[assembly: AssemblyCopyright("Copyright ©2018 Cascades Carnivore Project")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: AssemblyVersion("2.2.4.0")]
[assembly: AssemblyFileVersion("2.2.4.0")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: ComVisible(false)]
[assembly: Guid("7c1e8e08-af8f-44e6-93dd-9f9f5824139f")]
[assembly: InternalsVisibleTo("Carnassial.UnitTests")]

[assembly: NeutralResourcesLanguage("en", UltimateResourceFallbackLocation.MainAssembly)]
////                 no theme support                 styling is in this assembly
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]