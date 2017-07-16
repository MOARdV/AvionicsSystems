# AvionicsSystems
MOARdV's Avionics Systems for Kerbal Space Program - a new generation of IVA enhancement.

## Short Intro

This is a project to rebuild RasterPropMonitor
from the ground up using the design techniques I retrofitted to RPM during the first half
of 2016.  The goal is to provide a leaner, more performant design without sacrificing
features that are available in RPM.  A longer-winded explanation is available in the
wiki's [Introduction](https://github.com/MOARdV/AvionicsSystems/wiki/Introduction).

MAS is under intermittent development (basically, when I feel like it and I have time).

The [wiki](https://github.com/MOARdV/AvionicsSystems/wiki) contains most of the documentation for this mod, including guides to
configuring props and integrating scripting.

Development builds appear under [Releases](https://github.com/MOARdV/AvionicsSystems/releases).

## Where are the IVAs?

MAS is intended to be a toolkit for developing IVAs.  I do not intend to include a wide range of example
IVAs like RPM had - they're too time consuming, and I'm not including sample props.  My expectation is that
IVAs using MAS will include at least one of alexustas's props packs - [ASET Props Pack](http://forum.kerbalspaceprogram.com/index.php?/topic/116430-aset-props-pack-v14-for-the-modders-who-create-iva/) and/or
[ASET Avionics Pack](http://forum.kerbalspaceprogram.com/index.php?/topic/116479-aset-avionics-pack-v-20-for-the-modders-who-create-iva/).

However, since it is important to have *something* to look at before deciding to adopt this mod for IVA
development, there are three IVAs included in this package.

Please understand that these IVAs are
not guaranteed to be a full-featured IVA ideal for regular gameplay.  Some IVAs may not
work if I have not updated them, since I may make changes to props, and not update older IVAs.
The Yarbrough08 Mk. 1-1 A2 is the current development pod, and it is mostly complete.

The included IVAs are:

* The Mk3-9 Orbital Command Pod from [Near Future Spacecraft](http://forum.kerbalspaceprogram.com/index.php?/topic/155465-130-near-future-technologies-new-pack-nf-launch-vehicles/):
the initial development IVA for MAS, not very developed.
* A modified version (included in this mod) of the Kane command pod from [Bluedog Design Bureau](http://forum.kerbalspaceprogram.com/index.php?/topic/122020-13-bluedog-design-bureau-stockalike-saturn-apollo-and-more-v12-текстура-3jun2017/):
more extensively developed, still not complete.
* The Yarbrough08 Mk. 1-1 A2 Command Pod from [Yarbrough08](http://forum.kerbalspaceprogram.com/index.php?/topic/88604-wip-105-2-kerbal-command-pod-mk-1-1-a2-alpha-04-spacedock/):
this pod is the current pod, and it is mostly feature complete.  Note that you may need to so some config file tweaking for this pod if you're
going to play with this pod a lot, since it was last updated for KSP 1.0.5.

## What about RPM?

RasterPropMonitor is in maintenance mode.  I will keep RPM compiling, and I will try to fix any
egregious bugs, but I do not plan to continue developing features for the mod.  It is far too
complex for me to test it adequately by myself in a reasonable time while working on another,
substantially similar mod.  As long as RPM is still
in wide use, I will strive to keep it working, but I can not guarantee much beyond that.

IVAs will still work with RPM.  Props packs designed for RPM will still work with RPM.

## Other questions?

Look at the [FAQ](https://github.com/MOARdV/AvionicsSystems/wiki/FAQ) on the wiki.  I may have already come up with an answer.  If not, please ask.
