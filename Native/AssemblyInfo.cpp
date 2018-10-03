#include "StdafxClr.h"

using namespace System;
using namespace System::Reflection;
using namespace System::Resources;
using namespace System::Runtime::CompilerServices;
using namespace System::Runtime::InteropServices;
using namespace System::Security::Permissions;

//
// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
//
[assembly:AssemblyTitleAttribute(L"Carnassial.Native.dll")];
[assembly:AssemblyDescriptionAttribute(L"Carnassial C++/CLI and C++ components.")];
[assembly:AssemblyCompanyAttribute(L"Cascades Carnivore Project")];
[assembly:AssemblyProductAttribute(L"Carnassial")];
[assembly:AssemblyCopyrightAttribute(L"Copyright © 2018 Cascades Carnivore Project")];
[assembly:AssemblyTrademarkAttribute(L"")];
[assembly:AssemblyCultureAttribute(L"")];

#ifdef _DEBUG
[assembly:AssemblyConfigurationAttribute(L"Debug")];
#else
[assembly:AssemblyConfigurationAttribute(L"Release")];
#endif

[assembly:AssemblyVersionAttribute("2.2.4.0")];
[assembly:AssemblyFileVersionAttribute("2.2.4.0")];

[assembly:ComVisible(false)];
[assembly:Guid(L"768bba9c-aea6-47e3-b4ed-49177db0ef78")]

[assembly:CLSCompliantAttribute(true)];

[assembly:NeutralResourcesLanguage("en", UltimateResourceFallbackLocation::MainAssembly)];