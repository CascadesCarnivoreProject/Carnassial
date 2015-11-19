### Overview
This repository contains a development branch of [Timelapse 2](http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.HomePage), created by Saul Greenberg of the University of Calgary and Greenberg Consulting.  The project's scope is to augment Timelapse with features for carnivore monitoring.  For other types of studies please contact Saul directly via the link above.

### Dev Environment
Install [Visual Studio 2015 Community](https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx) or similar with the below options in addition to the defaults:

* Windows and Web Development -> Universal Windows App Development Tools -> Tools and Windows SDK
* Common Tools -> GitHub Extension for Visual Studio

The Windows SDK provides SignTool.exe, which Visual Studio needs to sign the Template Editor.

After installation clone the repo locally through Visual Studio's Team Explorer.

### Dependencies
Timelapse and the template editor require .NET 4.0.  More recent .NET versions can't be used as not all Timelapse users have them available on their systems.