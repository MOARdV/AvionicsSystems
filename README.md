# AvionicsSystems
MOARdV's Avionics Systems for Kerbal Space Program - a new generation of IVA enhancement.

![Mk1 Pod](https://imageshack.com/a/img924/694/3B7eyD.jpg)

## Short Intro

MOARdV's Avionics Systems (MAS) is a replacement for RasterPropMonitor.
The goal is to provide a more powerful and more performant design without sacrificing
features that are available in RPM.  A longer-winded explanation is available in the
wiki's [Introduction](https://github.com/MOARdV/AvionicsSystems/wiki/Introduction).

**NOTE:** MAS is a separate mod from RPM.  It is not a drop-in replacement.  IVAs created for RasterPropMonitor must
use RPM.  They will not magically work with MAS.  MAS uses different props, and it has different requirements than
RPM.

MAS is under intermittent development (basically, when I feel like it and I have time).

The [wiki](https://github.com/MOARdV/AvionicsSystems/wiki) contains most of the documentation for this mod, including guides to
configuring props and integrating scripting.

Releases appear under [Releases](https://github.com/MOARdV/AvionicsSystems/releases).  Development builds
occasionally show up on DropBox - the home page of the wiki will usually contain a link to the current dev build.

The MAS distribution contains multiple licensed components.  The [license](https://github.com/MOARdV/AvionicsSystems/blob/master/LICENSE.md) document identifies these
components and their licenses.

This mod includes version checking using [MiniAVC](https://forum.kerbalspaceprogram.com/index.php?/topic/173126-141-ksp-avc-add-on-version-checker-plugin-120-miniavc/). If you opt-in, it will use the
internet to check whether there is a new version available. Data is only read from the internet and no personal information is sent.

## Where are the IVAs?

MAS is intended to be a toolkit for developing IVAs.  I do not intend to include many example
IVAs like RPM had - good IVAs take a lot of time to create, and I have no plans for stock-derived sample
props with this mod.  IVAs included with MAS have other dependencies.
In particular, alexustas's props packs - [ASET Props Pack](http://forum.kerbalspaceprogram.com/index.php?/topic/116430-aset-props-pack-v14-for-the-modders-who-create-iva/) and
[ASET Avionics Pack](http://forum.kerbalspaceprogram.com/index.php?/topic/116479-aset-avionics-pack-v-20-for-the-modders-who-create-iva/) must be installed
for the MAS props to appear.

Since it is important to have *something* to look at before deciding to adopt this mod for IVA
development, there are IVAs included in this package:

* The stock Mk 1 v2 Command Pod (pictured above), which demonstrates a retro-NASA style stripped-down IVA suitable for suborbital or short orbital flights.
* The original Mk 1 Command Pod IVA is still included, since the original pod and IVA are still available (as of KSP 1.5.0).
* The stock Mk 1-3 Command Pod, which includes a more complete retro-NASA IVA for the center seat (with stock props still in place on the rest of the panel).
* The MRK DM command pod, part of the [Commonwealth Rockets](https://forum.kerbalspaceprogram.com/index.php?/topic/164365-13-commonwealth-rockets-tea-powered-spaceflight-in-development/) mod.
This command pod demonstrates a modern partial-glass cockpit design good for activity around Kerbin and its moons.
* The Flapjack cockpit in the [Kerbal Flying Saucer](https://forum.kerbalspaceprogram.com/index.php?/topic/173857-14x-pre-release-live-kerbal-flying-saucers-build-flying-saucers-in-ksp/) mod shows a glass cockpit design, including HUD.

You will need to download and install [Module Manager](https://forum.kerbalspaceprogram.com/index.php?/topic/50533-130-module-manager-281-june-29th-2017-with-n-cats-physics/),
ASET Props Pack v1.5 (or later), and ASET Avionics Pack in order to fly these IVAs.

## Where are the Props?

Converting props to MAS is an easy but repetitive task.  To help out with it, I've created the [Prop Config](https://github.com/MOARdV/AvionicsSystems/wiki/Prop-Config)
tool to automate a good deal of the process.  The Prop Config tool is released separately from the main MAS distribution.
The Prop Config Tool includes the master XML files used to generate most of the props.

## What about RasterPropMonitor?

My development on RasterPropMonitor has stopped.  I have announced that it is available if another modder wants to
pick it up, since I have done nothing with the mod since KSP 1.4.0 came out.

## Other questions?

Look at the [FAQ](https://github.com/MOARdV/AvionicsSystems/wiki/FAQ) on the wiki.  I may have already come up with an answer.  If not, please ask.
