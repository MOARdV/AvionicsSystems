# Building MOARdV's Avionics Systems

The following documentation should provide adequate information to allow others to build MOARdV's Avionics Systems.

## Prerequisites

### MAS DLL

MAS is built using Microsoft Visual Studio Express 2013.  Other versions of MSVS may work, and other build systems may be usable,
but that is outside the scope of this document.

For Lua interpretation, MAS uses the [MoonSharp](https://github.com/xanathar/moonsharp) Lua interpreter.  MAS currently uses the
pre-built version 1.8.0.0 (a newer version, 2.0.0.0, is available, but I haven't tried integrating it).  MoonSharp is *required* for
MAS to compile.

The MAS solution includes three projects:

* AvionicsSystems: The KSP DLL.  This is where all of the mod's functionality resides.
* Documentor: A basic command-line tool that converts doxygen comments in selected files to Markdown-formatted text.  This tool is
invoked by the DLL build process to automate updating the MD files used on the wiki to document MAS.
* PropConfig: A command-line tool used to parse the many prop XML files into prop config files.  This tool is triggered manually.

### MAS Asset Bundles

MAS Asset Bundles require the use of Unity.  MAS currently uses Unity 2017.1.3p1 to build asset bundles.

All MAS shaders are included in the Shaders directory.

The following fonts are used by MAS, but are not included in the GitHub repo.  They must be downloaded separately:

* The [Digital-7](http://www.fontspace.com/style-7/digital-7) font by Sizenko Alexander [Style-7](http://www.styleseven.com)
* The [InconsolataGo](http://www.levien.com/type/myfonts/) font
* The [Liberation Sans](https://pagure.io/liberation-fonts) font
* The [Press Start K](https://www.1001fonts.com/press-start-font.html) font
* The [Repetition Scrolling](http://www.1001fonts.com/repetition-scrolling-font.html) font by Tepid Monkey Fonts

### MAS Props

PropConfig.exe reads one or more XML files and converts the contents into KSP-compatible prop config files.

## Building

### MAS DLL

Provided MoonSharp is installed, and the solution's references have been updated to point at your local KSP installation, MAS
should build.  The solution is hard-coded to copy the output to the GameData directory in the local instance of the MAS repo,
as well as a second location that I use for developing and testing IVAs.  The solution also triggers the Documentor tool to
update any markdown documentation that needs refreshed.  These steps are all part of the Post-build event command line.

### MAS Asset Bundles

**TODO:** Automation of Asset Bundle building.

MAS Asset Bundles are currently a manual build step.  I plan to use Unity batch building to automate this process.  Once the batch process
is working, this document and the GitHub repo will be updated to include it.  Until then, I recommend using the asset bundles shipped with
MAS.

The Unity editor extension used to generate the asset bundles is included in the AssetBundle directory (AssetBundleCompiler.cs).  You
will need to associate the shaders and fonts with their appropriate asset bundle by hand until the automated process is implemented.

### MAS Props

Once you've built
the PropConfig tool, you can use the batch file `makeProps.bat` in the root of the MAS repo to build/update props.  Note that
this tool unconditionally overwrites existing config files, so if you are making manual edits of any prop config files, they will
be stomped on.
