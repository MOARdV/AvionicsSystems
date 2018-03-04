# AvionicsSystems
MOARdV's Avionics Systems for Kerbal Space Program - a new generation of IVA enhancement.

# NOTE:

Due to updated Terms of Service on the Kerbal Space Program Forum, this mod is currently on hiatus.  [View here](https://github.com/MOARdV/AvionicsSystems/wiki/KSP-Forum-ToS) for more information.

## Short Intro

MOARdV's Avionics Systems (MAS) is a project to rebuild RasterPropMonitor
from the ground up.  The goal is to provide a leaner, more performant design without sacrificing
features that are available in RPM.  A longer-winded explanation is available in the
wiki's [Introduction](https://github.com/MOARdV/AvionicsSystems/wiki/Introduction).

MAS is under intermittent development (basically, when I feel like it and I have time).

The [wiki](https://github.com/MOARdV/AvionicsSystems/wiki) contains most of the documentation for this mod, including guides to
configuring props and integrating scripting.

Releases appear under [Releases](https://github.com/MOARdV/AvionicsSystems/releases).  Development builds
occasionally show up on DropBox, and they may be mentioned in the [MAS Beta Release Announcements](https://github.com/MOARdV/AvionicsSystems/issues/95) issue.

The MAS distribution contains multiple licensed components.  The [license](https://github.com/MOARdV/AvionicsSystems/blob/master/LICENSE.md) document identifies these
components and their licenses.

## Where are the IVAs?

MAS is intended to be a toolkit for developing IVAs.  I do not intend to include a multiple example
IVAs like RPM had - good IVAs take a lot of time to create, and there will not be stock-derived sample
props with this mod, so any IVAs included with MAS will have other dependencies.  My expectation is that
IVAs using MAS will include at least one of alexustas's props packs - [ASET Props Pack](http://forum.kerbalspaceprogram.com/index.php?/topic/116430-aset-props-pack-v14-for-the-modders-who-create-iva/) and/or
[ASET Avionics Pack](http://forum.kerbalspaceprogram.com/index.php?/topic/116479-aset-avionics-pack-v-20-for-the-modders-who-create-iva/) adapted to MAS.

However, since it is important to have *something* to look at before deciding to adopt this mod for IVA
development, there are two IVAs included in this package.

Please understand that the IVAs are under development, and they may be missing some features.
The MRK command pod is the primary development pod at this time.

Included in this distribution is an updated IVA for the Yarbrough08 Mk. 1-1 A2 Command Pod from [Yarbrough08](http://forum.kerbalspaceprogram.com/index.php?/topic/88604-wip-105-2-kerbal-command-pod-mk-1-1-a2-alpha-04-spacedock/).
Since the original mod has not been updated since KSP 1.0.5, I have included a MM patch in the MOARdV/FlightSystems directory
called YarbroughMk1-1A2_Update.cfg.  This file updates the command pod to KSP 1.3.1 standards, and it makes a few tweaks
based on my personal gameplay preferences.  **NOTE:** The career mode values (cost, tech tree placement) are guesses, since
I never play career mode.  Feedback on whether they're reasonable would be appreciated.

In addition, a MAS-enabled MRK DM command pod, part of the [Commonwealth Rockets](https://forum.kerbalspaceprogram.com/index.php?/topic/164365-13-commonwealth-rockets-tea-powered-spaceflight-in-development/) mod, is included.
The MRK DM(r) 6-Kerbal pod does not currently include MAS props.  It will be updated at a later time, once
the IVA prop layout has stabilized.

You will need to download and install [Module Manager](https://forum.kerbalspaceprogram.com/index.php?/topic/50533-130-module-manager-281-june-29th-2017-with-n-cats-physics/), the Yarbrough08 command pod, and
ASET Props Pack v1.5 (or later) in order to fly either IVA.

## What about RPM?

RasterPropMonitor is in maintenance mode.  I will update RPM so that it is compatible with future KSP releases,
but I do not plan to continue developing features for the mod.  It is far too
complex for me to test it adequately by myself in a reasonable time while working on another,
substantially similar mod.  As long as RPM is still
in wide use, I will strive to keep it working, but I can not guarantee much beyond that.

IVAs designed for RPM will still work with RPM.  Props packs designed for RPM will still work with RPM.

## Other questions?

Look at the [FAQ](https://github.com/MOARdV/AvionicsSystems/wiki/FAQ) on the wiki.  I may have already come up with an answer.  If not, please ask.
