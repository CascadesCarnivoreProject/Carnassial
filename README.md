### Overview
This repository contains the source code for and releases of Carnassial and its associated tenplate editor.  Refer to the wiki tab above to find out how to get started and learn more about Carnassial.

### Contributing
Bug reports, feature requests, and feedback are most welcome.  Let us know!  We'd also really appreciate sample images and videos to test our code on.  Shoot us an email at carnassialdev@gmail.com if you've some you'd like to share.  We're particularly looking for samples from

* Bushnell Trophy or Trophy HD cameras from 2013 back to 2006 or software
* Reconyx HyperFire, UltraFire, MicroFire, and RapidFire cameras
* SpyPoint Force-10D and 11D cameras from 2016 or newer

Having these in our archives helps us help you, so don't be shy.

See below if you're a developer and would like to submit a pull request.

### Dev Environment
Install [Visual Studio 2015 Community](https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx) Update 3 or newer with the below options in addition to the defaults:

* Common Tools -> GitHub Extension for Visual Studio

Higher Visual Studio SKUs are fine.  After Visual Studio installation:

* clone the repo locally through Visual Studio's Team Explorer or GitHub's clone or download options
* install Visual StyleCop (Tools -> Extensions and Updates -> Online)

Atlassian's [https://www.atlassian.com/software/sourcetree](SourceTree) is a recommended augmentation of the git support available in Visual Studio's Team Explorer.

Commits should

* include appropriate test coverage
* have no build warnings, pass code analysis (Analyze -> Run Code Analysis), and be free of StyleCop issues (right click solution -> Run StyleCop)

Application development is be done against .NET 4.0 as as not all users have newer versions available on their systems or the ability to update .NET.  Test code uses .NET 4.5 but a date hasn't been set for application migration.

### Dependencies
* Carnassial and the template editor require .NET 4.0 or newer.
* Carnassial and the template editor are tested on Windows 10 and should run without issue on Windows 8.1, 8, 7 SP1, and Vista SP2.
* Windows XP SP3 and earlier are not supported.

A screen size of 1600 x 900 or larger is recommended.

### History
Carnassial began as a substantial overhaul of Timelapse 2.0 for improved code quality and sufficient flexibility to accomodate typical carnivore studies.  [Timelapse 2.1](http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.HomePage) includes much of the Carnassial effort through September 2016 and the two projects remain parallel due to the similar needs they serve.

