Nertea's Near Future Spacecraft Mk3-9 Command Pod README

The Mk3-9 command pod is the current development platform for MOARdV's
Avionics Systems.  It is not a completed MAS command pod, but it
illustrates some of what is possible in MAS.  Some of those capabilities
are very difficult to duplicate using RasterPropMonitor.

This is *not* a complete IVA.  You can probably do most orbital transfer
and rendezvous tasks from this IVA, but it's not complete, and it's not
meant to be a play IVA yet - it's intended to showcase MAS and give me an
in-game environment to test things out.

You will need to install this patch in a KSP installation that does
*not* have RasterPropMonitor installed, or you will need to remove
the patch that adds RPM to the Mk3-9 (that patch is 
GameData/NearFutureSpacecraft/Patches/NFSpacecraftRPM.cfg).  Eventually
I will figure out the Module Manager magic that allows me to override that
patch, but for development purposes, I don't need to.

You will also need to install Module Manager and the ASET Props development
build (dev 1.4.3 from 3-May-2017 or later - see the ASET Props thread).
Obviously, you need Near Future Spacecraft (at least the Mk3-9 pod and
IVA, and the Near Future Props).

MechJeb is very strongly recommended.  Chatterer and RealChute are
recommended.

A short orientation to the cockpit layout:

Most of the functionality is focused on the right (first IVA) seat.
The MFD is mostly non-functional - only the ASCENT page (ASC) is
configured.  The instrument panel backlight control is on the right-hand
bulkhead, as are the external lights action group switch and the right
cockpit cabin and instrument lamps switches.  The left seat's left-hand
bulkhead has a dimmer switch as well, along with the left side cabin and
instrument lamps.

Several sections of the cockpit have separate power switches located near
them (the digital fuel monitor, the ARRT, the X-pointer, the FDAI, the
IMP/Globus, the clocks, and the MechJeb data entry panels).

Some of the features to look at in this build:

The instrument backlight is a dimmer switch, not an on-off switch.

Action Group buttons do not illuminate if there are no actions assigned
to that group.

The MechJeb control panel (upper central column) has a numeric
keypad that allows the crew to type in numbers, and mode buttons
that allow the panel to read from or write to various MJ autopilots
(launch altitude, launch inclination, Ap, Pe, and Altitude).

Some multi-mode buttons have dimmed backlights on some captions -
for instance, the speed mode button (switch between surface, orbit,
and target) and the FDAI scale buttons illuminate when enabled,
but inactive options are dimmer than the active one.
