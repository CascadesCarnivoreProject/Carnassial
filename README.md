### Overview
This repository contains the source code for and releases of Carnassial and its associated tenplate editor.  Refer to the wiki tab above to find out how to get started and learn more about Carnassial.

### Contributing
Bug reports, feature requests, and feedback are most welcome.  Let us know!  We'd also really appreciate sample images and videos to test our code on and expand some features.  Shoot us an email at carnassialdev@gmail.com if you've some you'd like to share.  We're particularly looking for samples from

* Bushnell Trophy or Trophy HD cameras from 2013 back to 2006
* Reconyx HyperFire, UltraFire, MicroFire, and RapidFire cameras
* SpyPoint Force-10D and 11D cameras from 2016 or newer

Having these in our archives helps us help you, so don't be shy.

If you're a developer and would like to submit a pull request please see below.

### Development Environment
Install [Visual Studio 2015 Community](https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx) Update 3 or newer with the below options in addition to the defaults:

* Common Tools -> GitHub Extension for Visual Studio

Higher Visual Studio SKUs such as Enterprise are fine.  After Visual Studio installation:

* clone the repo locally through Visual Studio's Team Explorer or GitHub's clone or download options
* install Visual StyleCop (Tools -> Extensions and Updates -> Online)
* install the [WiX Toolset](http://wixtoolset.org/releases/) build tools and Visual Studio extension

Commits should

* include appropriate test coverage
* have no build warnings, pass code analysis (Analyze -> Run Code Analysis), and be free of StyleCop issues (right click solution -> Run StyleCop)

Application and test development is done against .NET 4.6.  Carnassial is a 64 bit app and for the most part only an x64 build is needed for development and testing
(the installer automatically rewires itself to build x86 under an x64 build).  However, the Visual Studio development UI is a 32 bit app and is therefore unable to
load controls from the regular Carnassial build for display in the WPF designer.  As a result, Carnassial has a vestigial x86 build which needs to be selected when 
doing UI tasks if the view in the designer is to match what's displayed at x64 runtime.  (Building Carnassial as AnyCPU in the x64 build isn't an option as 
StockMessageControl hits call graphs which go into Carnassial.Native, an approach which is anyways undesirable as there's a moderate performance penalty to building 
AnyCPU rather than x64.)

Visual Studio behaves similarly for unit test discovery, looking for an x86 build unless Test -> Test Settings -> Default Processor Architecture -> x64 is set, which
unfortunately has to be done every time VS is started.  In some cases it's possible to specify a .runsettings without x64 selected and sometimes not, but test
discovery does not honor the target platform specified in the .runsettings file.  In such situations VS can fail to find any unit tests until restarted, though 
setting x64 and forcing a build typically gets test discovery unstuck.

Also helpful are

* Atlassian's [https://www.atlassian.com/software/sourcetree](SourceTree), a recommended augmentation of the git support available in Visual Studio's Team Explorer.
* The [Visual Studio Image Library](https://msdn.microsoft.com/en-us/library/ms246582.aspx) for icons.
* John Skeet's discussion of [DateTime, DateTimeOffset, and TimeZoneInfo limitations](http://blog.nodatime.org/2011/08/what-wrong-with-datetime-anyway.html).

### Dependencies
* Carnassial and the template editor are supported and tested on Windows 10 Anniversary Update.  They should also run without issue on Windows Server 2008 or newer and legacy Windows 8.1, 8, and 7 SP1 systems not updated to Windows 10.  Support is, however, limited.
* Windows Vista SP2 and earlier and 32 bit versions of Windows are not supported.  Windows 7 users will need to [install .NET 4.6 or newer](https://msdn.microsoft.com/en-us/library/bb822049.aspx) if it's not already present.
* Visual Studio 2015 Community Update 3 or higher is required for development.

Screen sizes of 1600 x 900 or larger are recommended.

### History
Carnassial is named for [carnassials](https://en.wikipedia.org/wiki/Carnassial) as its function is analogous (though unfortunately it lacks the teeth's self-sharpening properties).

Carnassial began as a substantial overhaul of Timelapse 2.0 for improved code quality and sufficient flexibility to accomodate typical carnivore studies.  [Timelapse 2.1](http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.HomePage) includes several months of the initial Carnassial coding effort but is now diverged.

### Alternatives
The need to analyze remote camera data is a common one.  In addition to Carnassial and Timelapse we're aware of [CPW Photo Warehouse​](http://cpw.state.co.us/learn/Pages/ResearchMammalsSoftware.aspx) and [eMammal](http://emammal.si.edu/).  Key differences are

* Carnassial is fully free whilst CPW Photo Warehouse​ requires a Microsoft Access license (and permissions configuration).  Carnassial is more flexible and smoother in data entry but presently lacks equivalents to CPW's station information, occupancy analysis, and mark recapture analysis.  [CritterShell](https://github.com/CascadesCarnivoreProject/CritterShell) offers detection and occupancy analysis.
* Carnassial is readily available.  Obtaining the eMammal client requires a logon be issued, which can be hard to get.
* Carnassial and Timelapse are broadly similar.  As of March 2017 Carnassial offers faster analysis, more flexibility, and fewer defects than Timelapse.

If you know of others please email carnassialdev@gmail.com to let us know.
