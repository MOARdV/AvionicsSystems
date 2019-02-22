# AvionicsSystems
MOARdV's Avionics Systems for Kerbal Space Program - a new generation of IVA enhancement.

![Mk1 Pod](https://imageshack.com/a/img924/694/3B7eyD.jpg)

## Short Intro

MOARdV's Avionics Systems (MAS) is a replacement for RasterPropMonitor.
The goal is to provide a more powerful and more performant design without sacrificing
features that are available in RPM.  A longer-winded explanation is available in the
wiki's [Introduction](https://github.com/MOARdV/AvionicsSystems/wiki/Introduction).

**NOTE:** MAS is a separate mod from RPM.  It is **not** a drop-in replacement.  IVAs created for RasterPropMonitor must
use RPM.  They will not automatically work with MAS.  MAS uses different props, and it has different requirements than
RPM.

MAS is under intermittent development (basically, when I feel like it and I have time).

The [wiki](https://github.com/MOARdV/AvionicsSystems/wiki) contains the documentation for this mod, including guides to
configuring props, integrating scripting, and the 900+ functions that MAS makes available.  Most of this documentation is geared towards IVA creators and prop developers.

For players who want to make sense of the props, please take a look at [MOARdVPlus](https://github.com/MOARdV/MOARdVPlus), my advanced IVA mod (which includes user
guides for the props).

Releases appear under [Releases](https://github.com/MOARdV/AvionicsSystems/releases).

The MAS distribution contains multiple licensed components.  The [license](https://github.com/MOARdV/AvionicsSystems/blob/master/LICENSE.md) document identifies these
components and their licenses.

This mod includes version information compatible with MiniAVC and AVC.

## Where are the IVAs?

MAS is intended to be a toolkit for developing IVAs.  I do not intend to include many example
IVAs like RPM had - good IVAs take a lot of time to create.
The example IVAs included with MAS require alexustas's props packs - [ASET Props Pack](http://forum.kerbalspaceprogram.com/index.php?/topic/116430-aset-props-pack-v14-for-the-modders-who-create-iva/) and
[ASET Avionics Pack](http://forum.kerbalspaceprogram.com/index.php?/topic/116479-aset-avionics-pack-v-20-for-the-modders-who-create-iva/) must be installed
for the MAS props to appear.

Since it is important to have *something* to look at before deciding to adopt this mod for IVA
development, there are IVAs included in this package:

* The stock Mk 1 v2 Command Pod (pictured above), which demonstrates a retro-NASA style stripped-down IVA suitable for suborbital or short orbital flights.  A flight manual is available [on the wiki](https://github.com/MOARdV/AvionicsSystems/wiki/Operations-Manual-Mk1).
* The original Mk 1 Command Pod IVA is still included, since the original pod and IVA are still available (as of KSP 1.5.0).
* The stock Mk 1-3 Command Pod, which includes a more complete retro-NASA IVA for the center seat (with stock props still in place on the rest of the panel).
* The stock Mk1 Lander Can by snakeru, a conversion of the [ASET Mk1 Lander Can](https://forum.kerbalspaceprogram.com/index.php?/topic/156131-mk1-lander-can-iva-replacement-by-aset11/).  Note that if the ASET mod is installed, the MAS version of the IVA is switched off.  You must uninstall the ASET IVA to use the MAS IVA.
* The MRK DM command pod, part of the [Commonwealth Rockets](https://forum.kerbalspaceprogram.com/index.php?/topic/164365-13-commonwealth-rockets-tea-powered-spaceflight-in-development/) mod.
This command pod demonstrates a modern partial-glass cockpit design good for activity around Kerbin and its moons.

For some additional IVAs, the Flapjack cockpit in the [Kerbal Flying Saucer](https://forum.kerbalspaceprogram.com/index.php?/topic/173857-14x-pre-release-live-kerbal-flying-saucers-build-flying-saucers-in-ksp/) mod shows a glass cockpit design, including HUD.
Additionally, the [MOARdVPlus](https://github.com/MOARdV/MOARdVPlus) mod includes a complete 1960s-style Apollo IVA compatible with
the Bluedog Design Bureau mod, as well as a glass Apollo cockpit.

You will need to download and install [Module Manager](https://forum.kerbalspaceprogram.com/index.php?/topic/50533-130-module-manager-281-june-29th-2017-with-n-cats-physics/),
ASET Props Pack v1.5 (or later), and ASET Avionics Pack in order to fly these IVAs.  Refer to the [Installation Guide](https://github.com/MOARdV/AvionicsSystems/wiki/Installation)
on the wiki for more information, as well as a list of optional mods that enhance MAS gameplay.

## What about RasterPropMonitor?

My development on RasterPropMonitor has stopped.  It is available for adoption, but I have no plans to release any additional updates.

## How do I Build MAS?

The [Building MAS](https://github.com/MOARdV/AvionicsSystems/blob/master/BuildingMAS.md) document provides links to required components and steps to build MAS for yourself.

## Other questions?

Look at the [FAQ](https://github.com/MOARdV/AvionicsSystems/wiki/FAQ) on the wiki.  If the answer is not there, please ask.
