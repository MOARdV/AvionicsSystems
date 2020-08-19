# AvionicsSystems
MOARdV's Avionics Systems for Kerbal Space Program - a new generation of IVA enhancement.

![Mk1 Pod](https://imageshack.com/a/img924/694/3B7eyD.jpg)

## Short Intro

MOARdV's Avionics Systems (MAS) is a successor to RasterPropMonitor (RPM) that allows for immersive IVA gameplay in KSP.

### Players:

**NOTE:** MAS is *not* a 100% drop-in replacement for RasterPropMonitor.  
As of **MAS v1.2.0**, a Module Manager patch is included in MAS that replaces the basic RPM props with equivalent MAS props.  This patch does
not update ASET props, but it allows the IVAs included in RPM to use MAS-powered props.  An RPM ASET update patch is in the works.

Releases appear under [Releases](https://github.com/MOARdV/AvionicsSystems/releases).

**To Install:** select the latest released AvionicsSystems zip file from the "Releases" tab.  Do not download this repo, unless you intend to build MAS yourself.

Players do not need to download the PropConfig zip file.

For players who want to understand the advanced ASET props, please take a look at [MOARdVPlus](https://github.com/MOARdV/MOARdVPlus), my advanced IVA mod (which includes user guides for the props).

The KSP Forum has a [MAS thread](https://forum.kerbalspaceprogram.com/index.php?/topic/160856-wip-17x-moardvs-avionics-systems-mas-interactive-iva-v0980-27-may-2019/).

This mod includes version information compatible with MiniAVC and AVC.

The MAS distribution contains multiple licensed components.  The [license](https://github.com/MOARdV/AvionicsSystems/blob/master/LICENSE.md) document identifies these components and their licenses.

### IVA / Prop Creators:

The [wiki](https://github.com/MOARdV/AvionicsSystems/wiki) contains documentation for this mod geared towards creating new props.

Documentation of the props themselves is a work in progress.

## Where are the IVAs?

MAS is intended to be a toolkit for developing IVAs.  There are a few example IVAs.

Except for the RPM IVA upgrade patch, all of these IVAs require the ASET Props and Avionivs packs.

* Module Manager patches are included that upgrade the basic RPM props to MAS-equivalent props.  This patch allows the RPM IVAs included in RPM to use MAS instead.
* The stock Mk 1 v2 Command Pod (pictured above) demonstrates a retro-NASA style IVA suitable for suborbital or short orbital flights.  A flight manual is available [on the wiki](https://github.com/MOARdV/AvionicsSystems/wiki/Operations-Manual-Mk1).  This is a fully-featured IVA.
* The stock Mk 1-3 Command Pod includes a retro-NASA IVA for the center seat. This IVA *is not* complete, but it is a starting point if someone else wants to finish it.
* The stock Mk1 Lander Can by snakeru, a conversion of the [ASET Mk1 Lander Can](https://forum.kerbalspaceprogram.com/index.php?/topic/156131-mk1-lander-can-iva-replacement-by-aset11/).  Note that if RasterPropMonitor is installed, the MAS version of the IVA is switched off.  You must uninstall the ASET IVA to use the MAS IVA.
* The MRK DM command pod, previously part of the [Commonwealth Rockets](https://forum.kerbalspaceprogram.com/index.php?/topic/164365-13-commonwealth-rockets-tea-powered-spaceflight-in-development/) mod.
This command pod demonstrates a modern partial-glass cockpit design good for activity around Kerbin and its moons.  This pod has moved to a different mod, and I have not tested to see if the patch still works.

For some additional IVAs, the Flapjack cockpit in the [Kerbal Flying Saucer](https://forum.kerbalspaceprogram.com/index.php?/topic/173857-14x-pre-release-live-kerbal-flying-saucers-build-flying-saucers-in-ksp/) mod shows a glass cockpit design.
Additionally, the [MOARdVPlus](https://github.com/MOARdV/MOARdVPlus) mod includes a complete 1960s-style Apollo IVA compatible with
the Bluedog Design Bureau mod.  There is also a modernized (glass) Apollo IVA.

## Dependencies

In order to fly the IVAs included with this mod, you will need to download and install

* [Module Manager](https://forum.kerbalspaceprogram.com/index.php?/topic/50533-130-module-manager-281-june-29th-2017-with-n-cats-physics/),
* [ASET Props Pack](https://forum.kerbalspaceprogram.com/index.php?/topic/116430-aset-props-pack-v15-for-the-modders-who-create-iva/), and
* [ASET Avionics Pack](https://forum.kerbalspaceprogram.com/index.php?/topic/116479-aset-avionics-pack-v-21-for-the-modders-who-create-iva/).

**NOTE:** Although the ASET packs list RasterPropMonitor as a dependency, you do not need to install RPM in order to use MAS.

Refer to the [Installation Guide](https://github.com/MOARdV/AvionicsSystems/wiki/Installation)
on the wiki for more information, as well as a list of optional mods that enhance MAS gameplay.

## What about RasterPropMonitor?

My development on RasterPropMonitor has stopped. JohnnyOThan has adopted the mod at [RasterPropMonitor (adopted)](https://forum.kerbalspaceprogram.com/index.php?/topic/190737-18x-110x-rasterpropmonitor-adopted/).

## How do I Build MAS?

The [Building MAS](https://github.com/MOARdV/AvionicsSystems/blob/master/BuildingMAS.md) document provides links to required components and steps to build MAS for yourself.

## Other questions?

Look at the [FAQ](https://github.com/MOARdV/AvionicsSystems/wiki/FAQ) on the wiki.  If the answer is not there, please ask me.
