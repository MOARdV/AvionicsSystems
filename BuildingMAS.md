# Building MOARdV's Avionics Systems

The following documentation should provide adequate information to allow others to build MOARdV's Avionics Systems.

## Prerequisites

### MAS DLL

MAS is built using Microsoft Visual Studio Express 2013.  Other versions of MSVS may work, and other build systems may be usable,
but that is outside the scope of this document.

For Lua interpretation, MAS uses the [MoonSharp](https://github.com/xanathar/moonsharp) Lua interpreter.  MAS currently uses the
pre-built version 2.0.0.0.  MoonSharp is *required* for MAS to compile.

The MAS solution includes three projects:

* AvionicsSystems: The KSP DLL.  This is where all of the mod's functionality resides.
* Documentor: A basic command-line tool that converts doxygen comments in selected files to Markdown-formatted text.  This tool is
invoked by the DLL build process to automate updating the MD files used on the wiki to document MAS.
* PropConfig: A command-line tool used to parse the many prop XML files into prop config files.  This tool is triggered manually.

### MAS Asset Bundles

MAS Asset Bundles require the use of Unity.  MAS currently uses Unity 2017.1.3p1 to build asset bundles.

All MAS shaders are included in the Assets/Shaders directory.

The following fonts are used by MAS, but are not included in the GitHub repo.  They must be downloaded separately.  The
TTF files must be copied to the Assets/Fonts directory.

* The [Digital-7](http://www.fontspace.com/style-7/digital-7) font by Sizenko Alexander [Style-7](http://www.styleseven.com)
* The [InconsolataGo](http://www.levien.com/type/myfonts/) font
* The [Liberation Sans](https://pagure.io/liberation-fonts) font
* The [Press Start K](https://www.1001fonts.com/press-start-font.html) font
* The [Repetition Scrolling](http://www.1001fonts.com/repetition-scrolling-font.html) font by Tepid Monkey Fonts

Once the TTF files are installed, you will need to run Unity at least once to configure the asset bundles.  The following convention
should be followed:

All fonts in the Fonts directory should be assigned the asset bundle name `mas-font`.
The key is that they have the "-font" suffix on the asset bundle name, since that is how the asset builder script recognizes
the assets as part of the fonts asset bundle.  While adding each font to the asset bundle, change the Font Size to 32 (the default is 16).

All shaders in the Shaders directory should be assigned the asset bundle name `mas`.  As long as the shaders are marked to
build for all platforms, there should be no additional settings that need changed.

### MAS Props

PropConfig.exe reads one or more XML files and converts the contents into KSP-compatible prop config files.

## Building

### MAS DLL

Provided MoonSharp is installed, and the solution's references have been updated to point at your local KSP installation, MAS
should build.  The solution is hard-coded to copy the output to the GameData directory in the local instance of the MAS repo,
as well as a second location that I use for developing and testing IVAs.  The solution also triggers the Documentor tool to
update any markdown documentation that needs refreshed.  These steps are all part of the Post-build event command line.

### MAS Asset Bundles

MAS Asset Bundles can be generated in one of two ways: Interactively and batch.

For interactive builds, launch Unity and load the AvionicsSystems project.  Under the Assets menu is "Build AssetBundles".
Selecting this menu item will generate the asset bundles for you.

For batch builds, you will need to use a command line such as

```
"C:\Program Files\Unity\Editor\Unity.exe" -quit -projectPath { path-to-AvionicsSystems } -batchmode -executeMethod AssetBundleCompiler.BuildBundles
```

Replace `{ path-to-AvionicsSystems }` with the full patch to the root of the Avionics Systems repo, such as `D:\GitHub\AvionicsSystems`.

In both cases, the asset bundles are written to Assets/AssetBundles.  You will need to copy them to GameData/MOARdV/AvionicsSystems for distribution.

### MAS Props

Once you've built the PropConfig tool, you can build the prop config files.  One way to do this is to set up a batch
file in the AvionicsSystems repo root with the following commands:

```
cd GameData\MOARdV\MAS_ASET
..\..\..\PropConfig.exe ..\..\..\prop.xml ..\..\..\propBags.xml ..\..\..\propFlagIndicator.xml ..\..\..\propIndicatorCircular.xml ..\..\..\propPushButton.xml ..\..\..\propRetroButton.xml ..\..\..\propSwitchPanels.xml ..\..\..\propSwitchToggle.xml
cd ..\Props
..\..\..\PropConfig.exe ..\..\..\propAutopilot.xml ..\..\..\propTimer.xml
cd ..\..\..
```

This script assumes PropConfig.exe is in the root of the repo, next to the XML files.  It changes directories to place the
outputs in the right place.

Note that this tool unconditionally overwrites existing config files, so if you are making manual edits of any prop
config files, they will be stomped on.
