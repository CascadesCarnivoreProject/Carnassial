### Overview
This repository contains the source code for and releases of Carnassial and its associated template editor. Refer to GitHub's wiki
tab above to find out how to get started and learn more about Carnassial.

### Contributing
Bug reports, feature requests, and feedback are most welcome. Let us know! If Carnassial is crashing without putting up an error dialog please
use Event Viewer to check for .NET runtime and application errors in Windows Logs -> Application and include that information in the issue.

### Requirements
Carnassial and the template editor are tested on current releases of Windows 11 and supported through [end of servicing](https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information). 
A 64 bit processor with AVX2 instructions (circa 2014 and newer; AMD Excavator, Intel Core 4<sup>th</sup> gen) is required but code design and 
optimization targets more recent processors (currently AMD Zen 3 and newer). Display resolutions of 1600 x 900 or higher are recommended for 
viewing details of camera images and user interface layout.

Carnassial and the template editor should also run on Windows versions as far back as Windows 7 or Windows Server 2008 or newer. This is not 
supported or tested, however, and the Recycle Bin integration has never been tested.

Known limitations:

* Carnassial's image add rate slows as the number of images in a database (.ddb file) increases. This is probably a SQLite limitation but
  hasn't been investigated.
* Microsoft Windows does not report file times consistently at sub-millisecond precision. While it's not been observed, it's 
  possible rounding within the operating system may cause rereading date times from files without metadata to change the millisecond
  component of timestamps.

Known limitations with earlier versions of Windows:

* Users may need to [install .NET 9 or newer](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core) if 
  it's not already present using, for example, the [.NET installer](https://dotnet.microsoft.com/download).
* Users may need to [install the Universal C Runtime](https://www.microsoft.com/en-us/download/details.aspx?id=48234) if it's not 
  already installed.

### History
Carnassial is named for [carnassial teeth](https://en.wikipedia.org/wiki/Carnassial) as its function is analogous (though unfortunately
it lacks the teeth's self-sharpening properties). Carnassial began as a substantial overhaul of [Timelapse](https://timelapse.ucalgary.ca/) 
2.0 for improved code quality, release reliability, and sufficient flexibility to accommodate typical carnivore studies. Timelapse 2.1 
included several months of Carnassial coding effort but Timelapse has since diverged.

### Alternatives
The need to analyze remote camera data is a common one. In addition to Carnassial and Timelapse we're aware of 
[eMammal](http://emammal.si.edu/) and [CPW Photo Warehouse​](http://cpw.state.co.us/learn/Pages/ResearchMammalsSoftware.aspx).
Key differences are

* Carnassial is readily available. Obtaining the eMammal client requires a logon be issued, which can be hard to get.
* Carnassial is fully free and maintained whilst CPW Photo Warehouse​ requires a Microsoft Access license (and permissions configuration)
  and doesn't seem to have been updated since 2015.

If you know of other image logging tools please let us know.

### Contacting the Carnassial Development Team
Feel free to open new discussions or issues here on GitHub.

### Development Environment
Install [Visual Studio 2022 Community](https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx) or newer 
with the C# and C++ desktop workloads (other Visual Studio SKUs such as Enterprise are fine). After Visual Studio installation:

* add WiX 6 support via the [HeatWave extension](https://www.firegiant.com/docs/heatwave/) 
* clone the repo locally through Visual Studio's Team Explorer or GitHub's clone or download options
* build Carnassial (currently Carnassial.Native compilation is tested with MSVC v143 and the Win 10.0.19041.0 SDK)

Commits should

* include appropriate test coverage
* have no build warnings and raise no code analysis messages

Application and test development is done against .NET 9. Carnassial is a 64 bit app and for the most part only an x64 build 
is needed for development and testing. However, the Visual Studio development UI is a 32 bit app and is therefore unable to load 
controls from the regular Carnassial build for display in the WPF designer. As a result, Carnassial has a vestigial x86 build which
needs to be selected when doing UI tasks if the view in the designer is to match what's displayed at x64 runtime. (Building 
Carnassial as AnyCPU in the x64 build isn't an option as StockMessageControl hits call graphs which go into Carnassial.Native, an 
approach which is anyways undesirable as there's a moderate performance penalty to building AnyCPU rather than x64.)

To build Carnassial's installer, publish both Carnassial and the template editor and then build the installer project. Since the
installer picks up published, ready to run binaries the regular release build does not produce a .msi.

Historically, Visual Studio's discovery and honoring of test.runsettings has been unreliable, requiring manual selection of x64 
test execution. In such situations VS can fail to find any unit tests until restarted, though setting x64 and forcing a build 
typically resulted in test discovery. This appears to be less commonly an issue in Visual Studio 2018 and newer.

Carnassial is not currently MVVM. In general, greater use of MVVM would be beneficial but current UX development effort is 
primarily directed to model-view adoption in order to enable refactoring to view models. Carnassial uses WPF resource 
dictionaries for localization as the approach is lighter weight and more flexible than .resx files or locbaml type methods. 
Culture specific resources are merged in Carnassial\LocalizedApplication.cs

Also helpful are

* Atlassian's [https://www.sourcetreeapp.com/](SourceTree), a recommended augmentation of the git support available in Visual Studio's Team Explorer.
* [DB Browser for SQLite](http://sqlitebrowser.org/)
* The [Visual Studio Image Library](https://learn.microsoft.com/en-us/visualstudio/designers/the-visual-studio-image-library) for icons.
* John Skeet's discussion of [DateTime, DateTimeOffset, and TimeZoneInfo limitations](http://blog.nodatime.org/2011/08/what-wrong-with-datetime-anyway.html).

### Dependencies
* Visual Studio with the .NET desktop development and desktop development with C++ workloads installed
* nuget packages as referenced by the solution
* libjpeg-turbo, which is managed manually as described below

Multiple nuget packages for libjpeg-turbo exist but none seem reliably maintained. So, instead, the .libs are comitted to the Carnassial 
repo. To update, download the [libjpeg-turbo](https://libjpeg-turbo.org/) VC and VC64 installers, copy the updated bits from 
%SYSTEMDRIVE%\libjpeg-turbox64\{bin, include, lib} and %SYSTEMDRIVE%\libjpeg-turbo\lib to $(SolutionDir)Native\libjpeg-turbo\{bin, include,
lib}, and update the additional dependencies in Native.vcxproj's Win32 and x64 linker settings to point to the new .libs. Either a rebuild
or code changes within Carnassial.Native are required for Visual Studio to copy the updated turbojpeg.dll to the build output.