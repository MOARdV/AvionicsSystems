## Previous Releases of MOARdV's Avionics Systems

### MAS v1.3.3
For KSP 1.12.3, 7 May 2022.

**ATTENTION PLAYERS:** MAS includes a Module Manager patch that replaces all of the stock RPM props with MAS-enabled props.  If, for some reason, you do not want your RPM IVAs upgraded, you will need to rename GameData/MOARdV/Patches/JsiToMasUpgrade.cfg to JsiToMasUpgrade.nocfg.

**NOTE:** A partial ASET Avionics and ASET Props upgrade patch is included, but disabled.  To try it, please rename GameData/MOARdV/Patches/AsetToMasUpgrade.nocfg to GameData/MOARdV/Patches/AsetToMasUpgrade.cfg.  Currently, you will need RasterPropMonitor installed to enable any props that are not already updated.  There are no guarantees that partially-updated IVAs will work perfectly.

Outstanding known issues are logged at the [GitHub Issues](https://github.com/MOARdV/AvionicsSystems/issues) page.

#### New Features
* The MAS MFD2 now can display maps of Dres, Duna, Eeloo, Eve, and Laythe courtesy forum user Manul.  PR #347
* The MAS TS1 touchscreen (MAS_MFD_Touch1) MFD Flight page is close to flight-ready.  Issue #288.

---

### MAS v1.3.2
For KSP 1.12.3, 1 May 2022.

**ATTENTION PLAYERS:** MAS includes a Module Manager patch that replaces all of the stock RPM props with MAS-enabled props.  If, for some reason, you do not want your RPM IVAs upgraded, you will need to rename GameData/MOARdV/Patches/JsiToMasUpgrade.cfg to JsiToMasUpgrade.nocfg.

**NOTE:** A partial ASET Avionics and ASET Props upgrade patch is included, but disabled.  To try it, please rename GameData/MOARdV/Patches/AsetToMasUpgrade.nocfg to GameData/MOARdV/Patches/AsetToMasUpgrade.cfg.  Currently, you will need RasterPropMonitor installed to enable any props that are not already updated.  There are no guarantees that partially-updated IVAs will work perfectly.

Outstanding known issues are logged at the [GitHub Issues](https://github.com/MOARdV/AvionicsSystems/issues) page.

#### Fixes
* The ASET Props upgrade patch has been disabled.  It was accidentally enabled in 1.3.0 and 1.3.1.
* All MFD map displays use correct dimensions for the map, even if the map isn't the assumed 1024x512 dimensions.
* Another stab at fixing multi-mode engine behavior.  Issue #341.

#### New Features
* A prototype touch screen MFD prop is now available (MAS_MFD_Touch1).  It is *not* flight-ready, but the work-in-progress design is available for people wanting to experiment with it.  Issue #288.

---
### MAS v1.3.1
For KSP 1.12.3, 27 April 2022.

**ATTENTION PLAYERS:** MAS includes a Module Manager patch that replaces all of the stock RPM props with MAS-enabled props.  If, for some reason, you do not want your RPM IVAs upgraded, you will need to rename GameData/MOARdV/Patches/JsiToMasUpgrade.cfg to JsiToMasUpgrade.nocfg.

**ATTENTION IVA MAKERS:** A number of MAS props (rotary switches, push buttons, toggle switches) were renamed so that MAS and ASET props have consistent naming conventions.  MAS includes an MM patch to update obsolete prop names in config files to their current names.  Some of the text layout on those props may have changed to be consistent with the ASET props style.

**NOTE:** A partial ASET Avionics and ASET Props upgrade patch is included, but disabled.  To try it, please rename GameData/MOARdV/Patches/AsetToMasUpgrade.nocfg to GameData/MOARdV/Patches/AsetToMasUpgrade.cfg.  Currently, you will need RasterPropMonitor installed to enable any props that are not already updated.  There are no guarantees that partially-updated IVAs will work perfectly.

#### Known Issues
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* Map View lines occasionally are visible in MFD Camera views.  Issue #238.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.
* The HORIZON MASMonitor component is not working correctly.  Issue #302.

#### Fixes
* Multi-mode engines should now behave correctly when changing modes.  Issue #341.
* Assorted Tablo props that were causing initialization errors have been updated.

---
### MAS v1.3.0
For KSP 1.12.3, 22 April 2022.

**ATTENTION PLAYERS:** MAS includes a Module Manager patch that replaces all of the stock RPM props with MAS-enabled props.  If, for some reason, you do not want your RPM IVAs upgraded, you will need to rename GameData/MOARdV/Patches/JsiToMasUpgrade.cfg to JsiToMasUpgrade.nocfg.

**ATTENTION IVA MAKERS:** A number of MAS props (rotary switches, push buttons, toggle switches) were renamed so that MAS and ASET props have consistent naming conventions.  MAS includes an MM patch to update obsolete prop names in config files to their current names.  Some of the text layout on those props may have changed to be consistent with the ASET props style.

**NOTE:** A partial ASET Avionics and ASET Props upgrade patch is included, but disabled.  To try it, please rename GameData/MOARdV/Patches/AsetToMasUpgrade.nocfg to GameData/MOARdV/Patches/AsetToMasUpgrade.cfg.  Currently, you will need RasterPropMonitor installed to enable any props that are not already updated.  There are no guarantees that partially-updated IVAs will work perfectly.

#### Known Issues
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* Map View lines occasionally are visible in MFD Camera views.  Issue #238.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.
* The HORIZON MASMonitor component is not working correctly.  Issue #302.

#### Fixes
* MAS has been built against KSP version 1.2.3.
* MAS has been updated to work with the current release of MechJeb.  Thanks to forum user 610yesnolovely.

---
### MAS 1.2.3
For KSP 1.11.x, 21 May 2021

**ATTENTION PLAYERS:** MAS includes a Module Manager patch that replaces all of the stock RPM props with MAS-enabled props.  If, for some reason, you do not want your RPM IVAs upgraded, you will need to rename GameData/MOARdV/Patches/JsiToMasUpgrade.cfg.

**ATTENTION IVA MAKERS:** A number of MAS props (rotary switches, push buttons, toggle switches) were renamed so that MAS and ASET props have consistent naming conventions.  MAS includes an MM patch to update obsolete prop names in config files to their current names.  Some of the text layout on those props may have changed to be consistent with the ASET props style.

**NOTE:** A partial ASET Avionics and ASET Props upgrade patch is included, but disabled.  To try it, please rename GameData/MOARdV/Patches/AsetToMasUpgrade.nocfg to GameData/MOARdV/Patches/AsetToMasUpgrade.cfg.  Currently, you will need RasterPropMonitor installed to enable any props that are not already updated.  There are no guarantees that partially-updated IVAs will work perfectly.

#### Known Issues
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* Map View lines occasionally are visible in MFD Camera views.  Issue #238.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.
* The HORIZON MASMonitor component is not working correctly.  Issue #302.

#### Fixes
* Incorrect model paths for the 2m interior Spotlights have been fixed.

#### New Features
* Most of the remaining ASET Avionics props have been converted, courtesy a large contribution from vulkans22.
* IVA for the OPT-J HT cockpit, courtesy vulkans22.

---
### MAS v1.2.2
For KSP 1.11.0, 19 December 2020.

**ATTENTION PLAYERS:** MAS includes a Module Manager patch that replaces all of the stock RPM props with MAS-enabled props.  If, for some reason, you do not want your RPM IVAs upgraded, you will need to rename GameData/MOARdV/Patches/JsiToMasUpgrade.cfg.

**ATTENTION IVA MAKERS:** A number of props (rotary switches, push buttons, toggle switches) have been renamed so that MAS and ASET props have consistent naming conventions.  MAS includes an MM patch to update existing props in config files to their new names.  Some of the text layout on those props may have changed to be consistent with the ASET props style.

**NOTE:** A partial ASET Avionics and ASET Props upgrade patch is in progress.  It is disabled by default.  To experiment with it, please rename GameData/MOARdV/Patches/AsetToMasUpgrade.nocfg to GameData/MOARdV/Patches/AsetToMasUpgrade.cfg.

#### Known Issues
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* Map View lines occasionally are visible in MFD Camera views.  Issue #238.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.
* The HORIZON MASMonitor component is not working correctly.  Issue #302.

#### Fixes
* Setting or toggling multi-mode engine modes no longer triggers an exception.  Thanks to vulkans22 for finding and diagnosing the issue.  Issue #324.
* Radio Navigation functionality should be working again.  Thanks (again!) to vulkans22 for finding and diagnosing the issue.  Issue #325.

#### New Features
* `fc.FlightUIMode()` returns a number indicating which mode the KSP UI is currently in (staging, docking, map, etc).
* `fc.TimeInDays(time)` converts time (in seconds) into days, accounting for Kerbin's day length.
* `fc.SeekCameraHome(index)` tells the selected camera to reset both its tilt and pan positions to 0.
* `fc.CrewCourage(seatNumber)` and `fc.VesselCrewCourage(crewIndex)` return the courage rating of the Kerbal occupying the selected pod or vessel seat, respectively.
* MAS parses correctly-formatted HTML-style colors in configs (such as the `textColor` field of a MASMonitor TEXT node), such as `#FFFF00` for yellow.  Colors must be either 6 hexadecimal digits (for RGB with an alpha of 255), or 8 hexadecimal digits (for RGBA).
* Many more props, including a number of ASET Avionics conversions by theonegalen.

---
### MAS v1.2.1
For KSP 1.9.1, 21 August 2020.

**ATTENTION PLAYERS:** Starting with MAS 1.2.0, MAS includes a Module Manager patch that replaces all of the stock RPM props with MAS-enabled props.  If, for some reason, you do not want your RPM IVAs upgraded, you will need to rename GameData/MOARdV/Patches/JsiToMasUpgrade.cfg.

**NOTE:** A partial ASET Avionics and ASET Props upgrade patch is in progress.  It was accidentally enabled in this release of MAS.  To disable it, please rename GameData/MOARdV/Patches/AsetToMasUpgrade.cfg to GameData/MOARdV/Patches/AsetToMasUpgrade.nocfg or delete the file.

#### Known Issues
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* Map View lines occasionally are visible in MFD Camera views.  Issue #238.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.
* ASET Avionics and ASET Props RPM to MAS upgrade is incomplete.  Issue #277.
* The HORIZON MASMonitor component is not working correctly.  Issue #302.

#### Fixes
* The alignment crosshairs on the JSI Basic MFD have been fixed (and the associated exception resolved).  Issue #310.

#### New Features
* Some initial resource harvester and resource scanner functions have been added, courtesy cyberKerb.  Pull Request #309.

---
### MAS v1.2.0
For KSP 1.9.1, 19 August 2020.

**ATTENTION PLAYERS:** Starting with MAS 1.2.0, MAS includes a Module Manager patch that replaces all of the stock RPM props with MAS-enabled props.  If, for some reason, you do not want your RPM IVAs upgraded, you will need to rename GameData/MOARdV/Patches/JsiToMasUpgrade.cfg.

**NOTE:** A partial ASET Avionics and ASET Props upgrade patch is in progress.  It is disabled by default.  To experiment with it, please rename GameData/MOARdV/Patches/AsetToMasUpgrade.nocfg to GameData/MOARdV/Patches/AsetToMasUpgrade.cfg.

#### Known Issues
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* Map View lines occasionally are visible in MFD Camera views.  Issue #238.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.
* ASET Avionics and ASET Props RPM to MAS upgrade is incomplete.  Issue #277.
* The HORIZON MASMonitor component is not working correctly.  Issue #302.

#### Fixes
* The MASMonitor [LINE_GRAPH](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#line_graph) has been fixed.
* Localization strings are now handled correctly by `fc.TargetName()`.

#### New Features
* MAS replaces the core JSI RPM props in IVAs with MAS-enabled versions, and MAS adds everything needed to update the IVA to a MAS-enabled IVA.
* MAS can control the MechJeb SmartASS Force Roll feature.  Functions are available to set the roll angle, query the roll angle, and query or change the state of the Enable feature.  Issue #294.
* The [TEXTURE_REPLACEMENT](https://github.com/MOARdV/AvionicsSystems/wiki/MASComponent#texture_replacement) MASComponent allows parts of a prop's texture to be replaced.  This feature is a more flexible version of the JSIInternalFlagDecal in RPM that allows textures other than the current mission flag to be used.  Issue #297.
* COLLIDER_EVENT and COLLIDER_ADVANCED no longer require `volume` if `sound` is used.  `volume` defaults to 1.0 if it is omitted.
* `fc.GetSASModeName(double mode)` returns the localized name for the SAS mode selected by the mode parameter.
* The [Prop Config](https://github.com/MOARdV/AvionicsSystems/wiki/Prop-Config) tool generates config files only if the config file is missing, or it is older than the XML file that defines it.  The `--force` parameter will force the config files to regenerate, even if they are newer.
* `fc.PeriodRandom(double period)` returns a random number in the range of 0 to 1 that changes at `period` frequency in Hertz.
* The [`%PROPCOUNT%`](https://github.com/MOARdV/AvionicsSystems/wiki/Keyword#propcount) keyword provides the number of props in the current IVA.
* `fc.TargetVesselIndex()` returns the index of the currently-targeted vessel for use with the other TargetVessel queries.
* `fc.BodyDistance(id)` returns the distance from the active vessel to the selected celestial body.
* MAS can interact with grapples (arming/disarming, releasing them, reporting status). See the Grapple category for available functions.
* `fc.HasDock()` returns 1 if a primary dock is installed on the current vessel.
* `fc.DockedObjectName()` returns the name of the object that the vessel is docked to.
* `fc.LandingSpeed()` returns a rough estimate of the speed of the vessel when it impacts the surface, based on current thrust.  Issue #298.
* INTERNAL_TEXT has been added to allow MAS to interact with the KSP stock "LED Speed Panel" prop.  Prop makers should not use it under normal circumstances.  It's intended to be used only with this specific prop to allow RPM feature parity.  Issue #301.
* `fc.TargetOrbitPeriod()` returns the orbital period of the current target, in seconds.
* `fc.ScienceDataTotal()` returns the total size of the science data carried aboard the vessel, in Mits.

---
### MAS v1.1.1
For KSP 1.9.1, 14 July 2020. May work in 1.10.0.

**NOTE:** The beginning of a RasterPropMonitor to MAS upgrade patch is included in this update.  When enabled, it will add a basic MASFlightComputer to any part that includes a RasterPropMonitorComputer module.  This patch will also replace RPM-enabled props with MAS-enabled props.  However, since the patch is not complete, it is disabled by default.  The patch is in GameData/MOARdV/Patches/RpmToMasUpgrade.nocfg.  Renaming this file to RpmToMasUpgrade.cfg will enable it.  Because RPM and MAS do not share data, and this patch is not complete, some props may not work as intended when this patch is enabled.

#### Known Issues
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* Map View lines occasionally are visible in MFD Camera views.  Issue #238.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.

#### Fixes
* Compiled against KSP 1.9.1
* MAS_ASET_ClockTimer prop config now points at the correct location (the prop is finally visible), thanks to SingABrightSong.  Issue #283.
* Cameras now work in Windows.  Issue #285.
* MAS no longer reports an error in the log if MechJeb is not installed.  Issue #290.
* Multi-mode engines no longer trigger an index out of range exception, courtesy forum member Manul.
* KSP 1.10.0 NRE related to changing SAS modes in IVA has been fixed.  Issue #291.
* Navball and atmosphere gauge values work in KSP 1.10.0 IVA, instead of freezing.  Issue #291.
* MAS no longer fails to initialize when launching from launch sites added by mods.  Issue #293.

#### New Features
* VesselViewer is now accessible on the MAS MFD1 prop (when VV is installed), thanks to Kerbas-ad-astra.  Issue #282.

---
### MAS v1.1.0
For KSP 1.8.x, 4 November 2019

#### Known Issues
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* Map View lines occasionally are visible in MFD Camera views.  Issue #238.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.

#### Fixes
* Compiled against KSP 1.8.1

---
### MAS v1.0.1
For KSP 1.7.x, 27 July 2019

#### Known Issues
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* Map View lines occasionally are visible in MFD Camera views.  Issue #238.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.

#### New Features
* New Props: ASET Aircraft temperature gauges (skin temp, interior temp, and engine temp) have been converted to MAS.  MAS_swRotary3_RCSMode allows controlling RCS rotation and translation functionality from a single control.
* [COLLIDER_ADVANCED](https://github.com/MOARdV/AvionicsSystems/wiki/MASComponent#collider_advanced) supports actions directly, allowing it to be used for non-MFD props.  For example, the prop MAS_IndADV_Touch_Throttle is an ASET Advanced Indicator that allows the throttle setting to be changed by clicking and dragging on the indicator bar.

---
### MAS v1.0.0
For KSP 1.7.x, 6 July 2019

#### Known Issues
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* Map View lines occasionally are visible in MFD Camera views.  Issue #238.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.

#### New Features
* [COLLIDER_ADVANCED](https://github.com/MOARdV/AvionicsSystems/wiki/MASComponent#collider_advanced) supports actions directly, allowing it to be used for non-MFD props.  For example, the prop MAS_IndADV_Touch_Throttle is an ASET Advanced Indicator that allows the throttle setting to be changed by clicking and dragging on the indicator bar.

---
### MAS v0.98.0
For KSP 1.6.0-1.7.0, 27 May 2019.

#### Known Issues
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.  This is a Scatterer issue that may have been fixed (untested).  Issue #263.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* Map View lines occasionally are visible in MFD Camera views.  Issue #238.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.

#### New Features
* `fc.VesselDescription()` returns the vessel description entered in the editor.  Issue #266.
* `vtol.SetVerticalSpeed(speed)` allows setting the WBI VTOL Manager's Hover Mode vertical speed directly.
* [COLLIDER_ADVANCED](https://github.com/MOARdV/AvionicsSystems/wiki/MASComponent#collider_advanced) allows a collider to report where it was hit.  This feature currently supports touch screen MFDs, but a future update will allow it to be used to control props.  Issue #271.
* MAS_PAGE configurations now support [hitbox](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#hitboxes) nodes.  These nodes accept a coordinate and size, and they can trigger events when a click takes place within the hitbox, when the mouse is dragged within the hitbox, and/or when the mouse is released within the hitbox.  This feature supports touch screen MFDs.  Issue #271.
* `fc.CanSetSASMode(mode)` can be used to determine if a specified SAS mode is currently valid.
* `fc.GetCameraCanPan(index)`, `fc.GetCameraCanTilt(index)`, and `fc.GetCameraCanZoom(index)` indicate whether the selected camera is capable of panning, tilting, or zooming, respectively.

---
### MAS v0.97.0
For KSP 1.6.0-1.7.0, 27 April 2019.

#### Known Issues
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.  This is a Scatterer issue that may have been fixed (untested).  Issue #263.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* Map View lines occasionally are visible in MFD Camera views.  Issue #238.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.

#### Fixes
* NREs triggered when in an IVA that is no longer the active vessel after staging have been corrected.  Issue #269.
* Some potential NREs related to coroutines have been addressed.  Issue #270.

#### New Features
* When Galileo's Planet Pack is installed, the Globus instrument displays Gael instead of Kerbin (MFDs are not updated).  Map courtesy snakeru.  Issue #210.

---
### MAS v0.96.0
For KSP 1.6.0-1.6.1, 8 March 2019.

#### Known Issues
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.  This is a Scatterer issue.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* Map View lines occasionally are visible in MFD Camera views.  Issue #238.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.

#### ATTENTION IVA MAKERS
* The MASContextMenu part module was removed.
* The MAS_ACTION_GROUP feature is being completely redesigned for v0.97.0.  This change will require updates to any configs that use the feature.

#### Fixes
* Evaluation ordering problems related to persistent variables have been resolved.  Issue #261.

#### New Features
* The experiments and science types are now sorted based on the experiment ID.
* `fc.ExperimentCount(scienceTypeId)` provides information on how many experiments of the selected type are installed.  Issue #260.
* `fc.ExperimentId(scienceTypeId, experimentIndex)` provides a number suitable for the `experimentId` of various science functions.  Issue #260.
* `fc.ScienceTypeId(scienceTypeName)` provides a number suitable as the `scienceTypeId` parameter for science functions.  Issue #260.

---
### MAS v0.95.4
For KSP 1.6.0-1.6.1, 1 March 2019.

*The MAS Science update.*

#### Known Issues
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.  This is a Scatterer issue.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* Map View lines occasionally are visible in MFD Camera views.  Issue #238.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.

#### ATTENTION IVA MAKERS
* The MASContextMenu part module is deprecated. It will be removed in v0.96.0.

#### Fixes
* `fc.ResourceStageCurrent(resourceId)` now reports the available resources for the active stage, not the entire vessel.
* `fc.ResourceMax(resourceId)` (and the other 'Max' resource functions) now count the locked resource quantities, not just available quantities.  Issue #256.
* `parachute.DeploymentSafe()` now correctly reports when stock parachute deployment is unsafe vs. risky.  Issue #259.

#### New Features
* `fc.ResourceReserve(resourceId)` (and equivalents for Rcs and Propellant) reports the number of units of the selected resource that are locked (present, but not available for consumption).
* More Science progress: `fc.DataTransmitterCount()` and `fc.DataTransmitterAvailable(transmitterId)` tell you how many transmitters on board can send science, and how which ones are currently available.  `fc.TransmitExperiment(transmitterId, experimentId)` sends the data from one experiment.  Issue #141.
* `fc.TransmitScienceContainer(transmitterId, scienceContainerId)` sends the contents of one science container.  That container may be filled using `fc.CollectExperiments(scienceContainerId)`, queried with `fc.ScienceContainerDataCount(scienceContainerId)` and `fc.ScienceContainerCapacity(scienceContainerId)`, and reviewed with `fc.ReviewScienceContainer(scienceContainerId)`.  Issue #141.
* `fc.RunAvailableExperiment(scienceTypeId)` will run an available experiment (if there is one) for the selected science (experiment) type.  Issue #141.
* `fc.ResetExperiment(experimentId)` resets the selected experiment, dumping whatever data is stored.  Issue #141.
* `fc.DumpScienceContainer(scienceContainerId)` discards all of the data stored in the selected science container.  Issue #141.
* `fc.ExperimentResults(experimentId)` reports the results of an experiment after it has run.  Issue #141.
* `fc.DuplicateExperiment(experimentId)` and `fc.DuplicateScienceType(scienceTypeId)` can be used to determine if a given experiment (or type of experiment) has already been run for the given circumstances.  Issue #141.

---
### MAS v0.95.3
For KSP 1.6.0-1.6.1, 26 February 2019.

#### Known Issues
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.  This is a Scatterer issue.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* Map View lines occasionally are visible in MFD Camera views.  Issue #238.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.

#### ATTENTION IVA MAKERS
* The MASContextMenu part module is deprecated. It will be removed in v0.96.0.

#### Fixes
* Initialization errors related to IL code generation for certain function signatures has been fixed.  Issue #253.

#### New Features
* `fc.SetPodColorChanger(newState)` and `fc.TogglePodColorChanger()` allow control over a ModuleColorChanger installed on the current IVA's part.  This module typically controls external window glow effects.  `fc.GetPodColorChanger()` returns the current state of the module (on or off), `fc.PodColorChangerExists()` returns 1 if there is a color changer available.
* `fc.SetColorChanger(newState)`, `fc.ToggleColorChanger()`, and `fc.GetColorChanger()` behave similarly to the pod-specific functions, but they apply to all color changer modules.  See also the Color Changer category in [MASFlightComputerProxy](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy) for further details.
* `fc.MASLandingLatitude()`, `fc.MASLandingLongitude()`, `fc.MASLandingAltitude()`, and `fc.MASLandingTime()` return landing predictions generated using only the MAS landing computer, instead of using installed mods when available.  Issue #255.
* `fc.StockDeltaV()` and `fc.StockDeltaVStage()` report the total vessel delta-V and current stage delta-V as computed by the stock KSP delta-V calculator.  Issue #254.

---
### MAS v0.95.1 and v0.95.2
For KSP 1.6.0-1.6.1, 24 February 2019.

*NOTE:* v0.95.2 is a hotfix containing a fix to `fc.FormatString()`.  The two releases are otherwise identical.

#### Known Issues
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.  This is a Scatterer issue.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.

#### For Players
* The Mk1 Command Pod [Operations Manual](https://github.com/MOARdV/AvionicsSystems/wiki/Operations-Manual-Mk1) is now available.

#### ATTENTION IVA MAKERS
* The MAS_IndADV_2Scales props have been removed to make way for modernized versions of the props (some have been added).
* The MASContextMenu part module is deprecated.  It will be removed in v0.96.0.
* `fc.TogglePersistent(persistentName)` no longer returns a string in the corner case where `persistentName` was a string that could not be converted to a number.  In those cases, the persistent is treated like it was 0, and it allows for more efficient evaluation of `TogglePersistent` (it always returns 0 or 1).

#### Fixes
* `fc.FormatString()` now may use the MAS custom formatter options.

#### New Features
* The [ANIMATION_PLAYER](https://github.com/MOARdV/AvionicsSystems/wiki/MASComponent#animation_player) now supports looped animations and variable animation speeds.
* `fc.ColorTag(colorName)` returns the [Color Tag](https://github.com/MOARdV/AvionicsSystems/wiki/Formatted-Rich-Text#color-tags-rrggbbaa) that corresponds to the selected named color.  Issue #251.
* Similarly, `fc.ColorComponent(colorName, channel)` will return the R, G, B, or A value for the named color identified by `colorName`.  Issue #251.

#### Miscellaneous
* MiniAVC is no longer packaged with MAS.  I recommend using KSP-AVC for tracking update availability.
* The Wiki pages have been reorganized, and some of the older pages removed.  IVA creator and Prop creator pages have been started, although they haven't been updated completely.

---
### MAS v0.95.0
For KSP 1.6.0-1.6.1, 18 February 2019.

#### Known Issues
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.  This is a Scatterer issue.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.
* Requires MechJeb 2.8.2 or later to interface with MJ.

#### ATTENTION PROP MAKERS
* `fc.SetHeading(reference, heading, pitch, roll)` was removed.  Please use `fc.EngageAttitudePilot(reference, heading, pitch, roll)`.

#### Fixes
* MAS v0.95.0 is compatible with MechJeb 2.8.2.

#### New Features
* `fc.FormatString(format, arg0)` formats a single argument using C# string formatting.  This allows the format string to be a variable.
* `fc.EngageAttitudePilot(reference, heading, pitch)` allows the vessel heading to be fixed towards an off-axis direction without locking roll.
* The MAS attitude control system now has an "UP" reference option for controlling vessel orientation.
* IndicatorADV props have been upgraded for MAS thanks to alexustas.  All current Indicator ADV props have been updated.
* IFMS integrated flight management system has been updated again.

---
### MAS v0.94.0
For KSP 1.6.0-1.6.1, 2 February 2019.

#### Known Issues
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.
* Not compatible with MechJeb 2.8.2.  Issue #202.

#### ATTENTION PROP MAKERS
* MAS no longer adds custom waypoints for ground stations and radio navigation beacons.  Issue #239.
* `fc.SetHeading(reference, heading, pitch, roll)` is deprecated.  It will be removed in a future update.  Instead, please use `fc.EngageAttitudePilot(reference, heading, pitch, roll)`.

#### Fixes
* The MAS Autopilot is now much better behaved.  Issue #234.
* `fc.RollDockingAlignment()` has been fixed.  Issue #244.

#### New Features
* `fc.DeployableGearCount()` reports the total number of deployable landing gear.  Issue #233.
* `fc.ManeuverNodeTotalDV()` reports the total delta V required for the next maneuver node.  Issue #236.
* `fc.EngageAttitudePilot(reference, heading, pitch, roll)` replaces `fc.SetHeading()`.  `fc.EngageAttitudePilot(reference)` will point the vessel at the specified reference without locking roll.  Issue #234.
* More [Science](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy3#science-category) features.
* `fc.GroundStationCount()` reports the number of ground stations on Kerbin.  Functions in the [CommNet](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#commnet-category) category also report ground station latitude, longitude, and altitude.  
* `nav.SetWaypointToGroundStation(dsnIndex)` will set the waypoint navigation system to the selected ground station.
* `mechjeb.GetLandingSiteCount()` reports the number of landing sites available to MechJeb.  `mechjeb.LandingSiteLatitude(siteIndex)` reports the latitude of the selected MJ landing site.  Longitude, Altitude, and Name are also available.
* `nav.SetWaypointToLandingSite(siteIndex)` sets the waypoint navigation system to the selected MechJeb landing site.

---
### MAS v0.93.2
For KSP 1.6.0-1.6.1, 26 January 2019.

#### Known Issues
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* KCT appears to interfere with MAS functionality.  Issue #221.
* MAS Autopilot heading control has a tendency to overshoot.  It needs tuning.  Issue #234.
* KSP resets the reference transform when switching seats in IVA.  Issue #243.

#### ATTENTION
* The MAS feature that adds optional custom waypoints for ground stations and radio navigation beacons **will be removed** in v0.94.0.  If you have activated either of these features, you should deactivate them before installing v0.94.0.  MAS v0.94.0 will include new functions to target ground stations or nav beacons without cluttering the custom waypoints database.
* The official ASET documentation for Modular Push Buttons and Modular Toggle Switches has been added to this GitHub repo under the Documents directory.  Thanks to alexustas for permission to include them here.

#### Fixes
* Missing first menu entry, and NREs with 0-sized menus, have been fixed.  Issue #235.
* Reference transform management has been fixed for 1.6.x.  Issue #241.

#### New Features
* `fc.Select(condition, trueValue, falseValue)` accepts numeric values for 'condition' as well as boolean values.
* `fc.BoolToNumber(condition)` returns 1 if 'condition' is true, 0 otherwise.  Issue #237.
* `fc.TargetAxialDistance()`, `fc.TargetAxialVelocity()`, and `fc.TargetAxialAngle()` provide another representation of target-relative position.  Issue #233.
* `fc.GetAirBrakeCount()` returns the number of stock air brake modules installed on the craft.  `fc.GetAirBrakes()`, `fc.SetAirBrakes(active)`, and `fc.ToggleAirBrakes()` report on the state of the air brakes and control their deployment.
* `fc.FlightPathAngle(altitude)` returns the angle between the horizon and the orbital vector at the provided altitude.  Issue #233.
* `fc.ActiveDockingPortCameraIndex()` returns the camera index of an active docking port camera, or -1 if there is no valid docking port camera.
* More functions have been added to the [Science Category](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy3#science-category).  Issue #141.
* More props, including the initial pages for a new integrated MFD system (it's not complete, but the Launch and Orbit pages are mostly done).

---
### MAS v0.93.1
For KSP 1.6.0-1.6.1, 19 January 2019.

#### Known Issues
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* KCT appears to interfere with MAS functionality.  Issue #221.
* MAS Autopilot heading control has a tendency to overshoot.  It needs tuning.  Issue #234.
* KSP resets the reference transform when switching seats in IVA.  Issue #241.

#### Attention IVA Makers
* MAS_swRotary_GenericIntLight now controls every interior light in an IVA.  It is no longer *required* to make custom switches for IVAs that use different light names, unless you want to have separate control over each light.  Use MAS_swRotary_PointLight_IntLight to control only the "Point light" interior light transforms (which is the previous behavior of GenericIntLight).

#### Fixes
* The GROUND_TRACK monitor feature works again.
* `engine.SetEnginesEnabled(engineId, true)` now works for `engineId` > 0.  Issue #229.
* `fc.GForcesVertical()` has been changed again.  Maybe this time it's right.  Issue #229.
* `fc.ClearManeuverNodes()` no longer leaves maneuver node debris in Map View.  Issue #230.

#### New Features
* `fc.CircularOrbitSpeed(altitude)` returns the orbital speed in m/s to have a circular orbit at the specified altitude.
* The [Launch Site](https://github.com/MOARdV/AvionicsSystems/wiki/MASINavigation#launch-site-category) category provides information about the vessel's launch site.
* `lightName` in INT_LIGHT is now optional.  When omitted, the prop will control every interior light.
* `fc.AppendPersistentDigit(persistentName, digit, maxLength)` allows a persistent to be treated as a numeric input.
* `fc.ToggleResource(resourceId)` toggles the availability of resource containers holding a particular resource.
* The [SUB_PAGE](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#sub_page) MAS_PAGE node now supports nested `SUB_PAGE` entries.

---
### MAS v0.93.0
For KSP 1.6.0-1.6.1, 13 January 2019.

#### Known Issues
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* KCT appears to interfere with MAS functionality.  Issue #221.

#### Fixes
* `fc.ReleaseLaunchClamps()` and `fc.GetLaunchClampCount()` actually work now, courtesy @snakeru.
* Trim control / management functions now work.
* The ASET DSKY instrument is now fully implemented.  Issue #222.
* Fixed conflict between the MAS Mk1 lander can and the [ASET Mk1 lander can](https://forum.kerbalspaceprogram.com/index.php?/topic/156131-mk1-lander-can-iva-replacement-by-aset11/).  If the ASET version is installed, it will be enabled.  To use the MAS version, the ASET version needs to be uninstalled.  Issue #228.

#### New Features
* `fc.DeltaVStageMax()` returns the maximum delta-V for the current stage, assuming current atmospheric conditions.
* `fc.MaxRatedThrustkN()` returns the maximum thrust of all active engines, assuming a throttle limit of 100% and maximum ISP.
* `fc.CurrentRatedThrust()` returns the current thrust of active engines as a percentage of maximum possible thrust.
* The Engine [Intakes](https://github.com/MOARdV/AvionicsSystems/wiki/MASIEngine#intakes-category) category has been changed to support any intake types, not just Intake Air.
* `nav.SetWaypointToLaunchSite()` sets the waypoint manager to point at the vessel's launch site (provided the launch site and vessel are in the same SoI).
* New Circular Indicator props and 5-digit LED displays for the common stock resources (power, monoprop, LF, O).
* Finer-grained gimbal control: `fc.GetGimbalPitch()` reports if any active, unlocked gimbals provide pitch control.  `fc.SetGimbalPitch(enable)` can be used to enable or disable pitch control on active, unlocked gimbals.  `fc.ToggleGimbalPitch()` can be used to toggle pitch control.  Equivalent functions for `Yaw` and `Roll` were added.
* More detailed gimbal deflection info: `fc.GetGimbalDeflectionX()` and `fc.GetGimbalDeflectionY()` report the average normalized deflection (in the range -1 to +1) of active, unlocked gimbals.  Note that rotated engines may cancel each other out.
* `fc.ChangePowerDraw(rateChange)` can be used to increase the amount of power the MASFlightComputer uses each second.  `fc.GetPowerDraw()` returns the current power demand of the MASFlightComputer.  This feature only works when `requiresPower` is true.
* The MASFlightComputer can now be configured to draw power by setting a `rate` in the MASFlightComputer MODULE field.  This value only applies when `requiresPower` is true.
* `fc.InitializePersistent(persistentName, value)` can be used to set a persistent variable to a specific value if the persistent does not already exist.  If the persistent exists, this function does nothing.
* `fc.CrewConscious(seatIdx)` returns 1 if the selected crew member is conscious.  It returns 0 if the Kerbal has blacked out due to g-forces.  Issue #225.
* A COLLIDER_EVENT will stop functioning if the current crew member is unconscious.  Issue #225.
* INT_LIGHT accepts a comma-delimited list of light names, allowing one prop to control multiple interior lights with different names.
* Some initial Science category functions have been enabled.
* More props.

---
### MAS v0.92.1
For KSP 1.6.0-1.6.1, 5 January 2019.

#### Known Issues
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* KCT appears to interfere with MAS functionality.  Issue #221.

#### ATTENTION IVA Prop Makers
* The 'Moving' functions, such as `fc.GearMoving()`, now return -1, 0, or +1 so that they can indicate which direction the component in question is moving.

#### Fixes
* FAR support is re-enabled.

#### New Features
* New MAS Mk1 Lander Can IVA based on the ASET Mk1 Lander Can, courtesy @snakeru.  PR #218.
* Mk1-3 IVA updated with a "CLEAR NODE" button to clear maneuver nodes, suggested @it0uchpods.
* `fc.CrewEva(double seatNumber)` allows crew to be kicked out of the command pod ... or willingly go on EVA.
* The Engine category now contains methods for controlling [Air Intakes](https://github.com/MOARdV/AvionicsSystems/wiki/MASIEngine#air-intakes-category).
* Launch clamps may now be triggered using `fc.ReleaseLaunchClamps()`.  The number of launch clamps may be queried using `fc.GetLaunchClampCount()`.  Issue #220.
* There is now a [three-state](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy3#fcselectdouble-condition-object-negativevalue-object-zerovalue-object-positivevalue) version of `fc.Select()`.
* `fc.SetBits(persistentName, bits)` and `fc.ClearBits(persistentName, bits)` treat the specified persistent variable as a 32 bit bitfield, allowing bits to be set and cleared.
* The [Math Category](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy2#math-category) includes bitwise operators (AND, OR, XOR, negation).

---
### MAS v0.91.0
For KSP 1.6.0, 2 Jan 2019.

#### Known Issues
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* Until there is an official release of FAR, **FAR is no longer supported in MAS**.  The existing functions in the `far` group will simply return 0, the same as if FAR was not installed.

#### ATTENTION IVA Prop Makers

The following methods have changed return values: `fc.AntennaPosition()`, `fc.CargoBayPosition()`, `fc.GearPosition()`, `fc.RadiatorPosition()`, `fc.SolarPanelPosition()`.

These methods now return a normalized position (a value between 0 and 1) indicating position.  0 indicates fully retracted (or not movable).  1 indicates fully extended.  Values between 0 and 1 provide the average location of the tracked object.  If you only care about whether a given component is moving, but not its position, use the related "Moving" functions (eg, `fc.GearMoving()`).

#### Fixes
* Updated MiniAVC.dll to 1.2.0.4 (although the DLL still reports 1.0.3.2).
* Module Manager warnings from malformed patches for DPAI_RPM and SCANsat were fixed, courtesy JohnnyOThan.
* Various CommNet functions (`fc.CommNetCanCommunicate()`, `fc.CommNetCanScience()` etc) now return 0 if there is no CommNet signal.
* Fixed the IMP Lat/Lon tape instantly snapping to position.

#### New Features
* Updated MoonSharp Lua interpreter to v2.0.0.
* Reordered the IMP/Globus selector switch to place VESSEL in the center position.

---
### MAS v0.90.0
For KSP 1.5.x, 18 October 2018.

#### Known Issues
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* Until an official FAR release becomes available, **FAR is no longer supported in MAS**.  The existing functions in the `far` group will simply return 0, the same as if FAR was not installed.

#### ATTENTION Players
* The Action Group NASA-style switches in the Mk1 and Mk1-3 pod can show custom captions.  To configure these captions, add lines to your ship's description in the VAB such as:
```
AG1=Retro
AG2=Something
```
(Use AG0, not AG10, for the tenth action group).  Keep the descriptions short, or they will overlap.

#### ATTENTION IVA Prop Makers
* Many more props, including the ASET CRT (green-screen) monitor.
* The function `fc.TargetAvailableDockingPorts()`, which returns the number of valid docking ports on the target vessel, has been renamed `fc.TargetDockCount()`.  Functions to target the next valid docking port on the target, or to select the docking port directly, have been added to the [Docking](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#docking-category) category.  Issue #198.
* `fc.Docked()` now returns 1 only if the vessel was docked to a compatible docking port during flight.  To detect if the docking port was connected to something while in the VAB, use `fc.DockConnected()`.

#### Fixes
* MAS can now activate the MechJeb Ascent Autopilot without the player needing to open the MJ Ascent Guidance GUI first.  Issue #202.
* Minor tweaks for KSP 1.5.0.

#### New Features
* [TEXTURE_SCALE](https://github.com/MOARdV/AvionicsSystems/wiki/MASComponent#texture_scale) allows the UV texture scaling of a transform to be changed.  Issue #193.
* Added the [Press Start K](https://www.1001fonts.com/press-start-font.html) retro-bitmap style font to the included font list for those old-school CRT monitors.
* The [MAS Settings](https://github.com/MOARdV/AvionicsSystems/wiki/MAS-Settings) App Button may be hidden so that it is not visible in the Space Center view.  This is an advanced feature that requires editing the persistent file for your save game.  Issue #199.
* The [MAS_ACTION_GROUP](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputer#mas_action_group) can further enhance IVAs by allowing custom props to control specific parts on the vessel.  For instance, a MAS_ACTION_GROUP can be configured to activate and deactivate the RCS thrusters on a command pod.  Or, a MAS AG can be used to activate the animation on a specific part.  These MAS AGs are in addition to the ten stock KSP action groups.  Issue #197.
* The [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) node of a MASPage definition supports post-processing shaders.  This feature is primarily intended to allow retro-style monochrome displays without having to customize the source cameras.
* Many new `Set` functions have been added to complement the `Toggle` functions currently in MAS.  Issue #198.
* `fc.TargetDockError()` will provide information for aligning docking ports that have orientation restrictions using the 'snapOffset' field of the ModuleDockingNode.  Issue #198.
* The Named Docking Node name for a docking port will be provided when the Docking Port Alignment Indicator mod is installed.
* `fc.GForceVertical()` provides the acceleration, in Gs, along the head-foot direction of seated Kerbals in typical vessel configurations.  Issue #198.
* MAS includes Module Manager patches to support the Hullcam VDS cameras.  Issue #201.

---
### MAS v0.21.2
For KSP 1.4.0 - KSP 1.4.5, 21 July 2018.

#### Known Issues
* **TRANSLATORS WANTED** MAS supports localization in the KSP GUI.  If you are able to provide translations, please take a look at [the localization file](https://github.com/MOARdV/AvionicsSystems/blob/master/GameData/MOARdV/AvionicsSystems/Localization/en-us.cfg).
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* KSP 1.4.4 has a bug where aero effects (re-entry flames) may appear inside the IVA.  When these effects are active, colliders (buttons) inside the effects may not be clickable.  This is a stock bug.

#### ATTENTION Players
* MAS also includes an IVA for the stock Mk1-3 "Apollo-ish" pod.  This IVA updates the center seat's controls.
* The Yarbrough08 Mk1-1A2 pod has been removed.

#### ATTENTION IVA Prop Makers
* More retro (1960's NASA) props have been added.

#### Fixes
* TRIGGER_EVENT 'autoRepeat' now works correctly.

#### New Features
* `fc.Conditioned(value, defaultValue)` allows a non-zero result for fc.Conditioned failure conditions.  This is ideally suited for animations / rotations / translations where the neutral positions is not the zero position.
* IVA camera pan and pitch limits may be set in MASFlightComputer.  Issue #189.

---
### MAS v0.21.1
For KSP 1.4.0 - KSP 1.4.4, 14 JUL 2018.

#### Known Issues
* **TRANSLATORS WANTED** MAS supports localization in the KSP GUI.  If you are able to provide translations, please take a look at [the localization file](https://github.com/MOARdV/AvionicsSystems/blob/master/GameData/MOARdV/AvionicsSystems/Localization/en-us.cfg).
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* KSP 1.4.4 has a bug where aero effects (re-entry flames) can appear inside the IVA.  When these effects are active, colliders (buttons) inside the effects are not clickable.  This is a stock bug.

#### ATTENTION Players
* MAS includes an IVA for the stock Mk1 pod.  This IVA is minimal, as is appropriate for an early barely-orbital pod.

#### ATTENTION IVA Prop Makers
* Removed `fc.BodyNumMoons(id)`.  It duplicated `fc.BodyMoonCount(id)`.
* The `fc.Body` functions no longer accept the special `id` values of -1 and -2.  To get the index of the current body, use `fc.CurrentBodyIndex()`.  To get the index of the current target (when it is a body), use `fc.TargetBodyIndex()`.
* A number of early NASA-styled switches and buttons have been added.  The ASET DSKY has been imported, although it has not been completed.

#### Fixes
* MFD fonts have been improved (particularly, the Digital-7 font family).  Issue #186.

#### New Features
* `fc.HottestPartTemperature(useKelvin)`, `fc.HottestPartMaxTemperature(useKelvin)`, and `fc.HottestPartSign()` provide information on the hottest part, its maximum temperature, and whether it is heating or cooling.
* `nav.TerrainHeight(range, bearing)` returns the height of the terrain (relative to datum) relative to the current vessel.
* MAS includes [MiniAVC](https://forum.kerbalspaceprogram.com/index.php?/topic/173126-141-ksp-avc-add-on-version-checker-plugin-120-miniavc/) to provide players the option to receive update notifications when starting KSP.
* MAS allows the use of `load()` in Lua to create functions dynamically from text.  NOTE: Functions generated using this feature can not be called directly from MAS.  They must be called through a wrapper function in the Lua script file.  Issue #184.
* The [MENU](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#menu) node of a MASPage can be used to create interactive menus from either a static list of ITEM elements, or a dynamically-sized list.  Issue #30.
* `fc.BodyMoonId(id, moonIndex)` provides the body ID for moon number 'moonIndex' orbiting body 'id'.  This allows querying that moon's information directly.
* `fc.CurrentBodyIndex()` and `fc.TargetBodyIndex()` replace the special indices '-1' and '-2' in the fc.Body functions.  This change allows some performance optimizations.
* `fc.BodyCount()` returns the total number of celestial bodies in the database.
* The [COLLIDER_EVENT](https://github.com/MOARdV/AvionicsSystems/wiki/MASComponent#collider_event) node supports mouse drag inputs (click the mouse, drag left/right, up/down, or both).  Issue #113.
* `fc.GetThrottleKeyPressed()` will return 1 if the player is pressing any of the keys associated with throttle control (full throttle, cut throttle, throttle up, or throttle down).  Issue #188.

---
### MAS v0.21.0
For KSP 1.4.0 - KSP 1.4.4, 10 JUL 2018.

#### Known Issues
* **TRANSLATORS WANTED** MAS supports localization in the KSP GUI.  If you are able to provide translations, please take a look at [the localization file](https://github.com/MOARdV/AvionicsSystems/blob/master/GameData/MOARdV/AvionicsSystems/Localization/en-us.cfg).
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* KSP 1.4.4 has a bug where aero effects (re-entry flames) can appear inside the IVA.  When these effects are active, colliders (buttons) inside the effects are not clickable.  This is a stock bug.

#### ATTENTION Players
* MAS uses its own maneuver planner (accessible from the MFD2 PLAN page) as well as its own Maneuver Autopilot (accessible from the MFD2 PLAN and MFD2 MNVR pages).  These *are* buggy, but I would like feedback on both.  If you prefer using MechJeb to plan or to execute maneuvers, it is still accessible from the MechJeb control panel in the lower-central portion of the MRK-DM IVA.
* MFD2 (the modern MAS MFD design) has an [operations guide](https://github.com/MOARdV/AvionicsSystems/wiki/MFD2-Operations-Guide) on the wiki.  This is still a work-in-progress.

#### ATTENTION IVA Prop Makers
* Removed `fc.BodyNumMoons(id)`.  It duplicated `fc.BodyMoonCount(id)`.
* The `fc.Body` functions no longer accept the special `id` values of -1 and -2.  To get the index of the current body, use `fc.CurrentBodyIndex()`.  To get the index of the current target (when it is a body), use `fc.TargetBodyIndex()`.

#### New Features
* `fc.HottestPartTemperature(useKelvin)`, `fc.HottestPartMaxTemperature(useKelvin)`, and `fc.HottestPartSign()` provide information on the hottest part, its maximum temperature, and whether it is heating or cooling.
* `nav.TerrainHeight(range, bearing)` returns the height of the terrain (relative to datum) relative to the current vessel.
* MAS includes [MiniAVC](https://forum.kerbalspaceprogram.com/index.php?/topic/173126-141-ksp-avc-add-on-version-checker-plugin-120-miniavc/) to provide players the option to receive update notifications when starting KSP.
* MAS allows the use of `load()` in Lua to create functions dynamically from text.  NOTE: Functions generated using this feature can not be called directly from MAS.  They must be called through a wrapper function in the Lua script file.  Issue #184.
* The [MENU](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#menu) node of a MASPage can be used to create interactive menus from either a static list of ITEM elements, or a dynamically-sized list.  Issue #30.
* `fc.BodyMoonId(id, moonIndex)` provides the body ID for moon number 'moonIndex' orbiting body 'id'.  This allows querying that moon's information directly.
* `fc.CurrentBodyIndex()` and `fc.TargetBodyIndex()` replace the special indices '-1' and '-2' in the fc.Body functions.  This change allows some performance optimizations.
* `fc.BodyCount()` returns the total number of celestial bodies in the database.
* The [COLLIDER_EVENT](https://github.com/MOARdV/AvionicsSystems/wiki/MASComponent#collider_event) node supports mouse drag inputs (click the mouse, drag left/right, up/down, or both).  Issue #113.

---
### MAS v0.20.1
For KSP 1.4.0 - KSP 1.4.4, 3 JUL 2018.

#### Known Issues
* **TRANSLATORS WANTED** MAS supports localization in the KSP GUI.  If you are able to provide translations, please take a look at [the localization file](https://github.com/MOARdV/AvionicsSystems/blob/master/GameData/MOARdV/AvionicsSystems/Localization/en-us.cfg).
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* KSP 1.4.4 has a bug where aero effects (re-entry flames) can appear inside the IVA.  When these effects are active, colliders (buttons) inside the effects are not clickable.  This is a stock bug.

#### ATTENTION Players
* MAS uses its own maneuver planner (accessible from the MFD2 PLAN page) as well as its own Maneuver Autopilot (accessible from the MFD2 PLAN and MFD2 MNVR pages).  These *are* buggy, but I would like feedback on both.  If you prefer using MechJeb to plan or to execute maneuvers, it is still accessible from the MechJeb control panel in the lower-central portion of the MRK-DM IVA.
* MFD2 (the modern MAS MFD design) has an [operations guide](https://github.com/MOARdV/AvionicsSystems/wiki/MFD2-Operations-Guide) on the wiki.  This is still a work-in-progress.

#### ATTENTION IVA Prop Makers
* [MASMonitor](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor) page nodes and [MASComponent](https://github.com/MOARdV/AvionicsSystems/wiki/MASComponent) no longer support the `range` parameter.  Use `fc.Between(variable, range1, range2)` for threshold mode.  When `blend` is true, use `fc.InverseLerp(variable, range1, range2)` if range1 and range2 are not 0 and 1 respectively (if the range is '0, 1' for blend mode, simple remove the `range` field).  Issue #181.
* The kOS Terminal prop has been ported to MAS.  All keys are functional.  To customize your own prop using that model, take a look at the config in MOARdV/MAS_ASET/kOSTerminal.

#### Fixes
* Divide-by-zero errors caused by some variables being used too soon have been fixed.  Issue #182.
* Issue detecting custom tracked resource converters has been resolved.

#### New Features
* The [ROLLING_DIGIT](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#rolling_digit) can be used to simulate a mechanical odometer on a MASMonitor display.  Very old Issue #3.
* MAS monitor pages may now use a [SUB_PAGE](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#sub_page) node, which imports a group of nodes into the page.  These sub-pages allow the MFD creator to assemble libraries of commonly-used components that can be updated by editing a single location, instead of editing multiple files.  Issue #180.
* MAS now has `fc.RoundZero(sourceValue)` to round a value towards zero, as opposed to Floor or Ceiling.  `fc.InverseLerp(value, range1, range2)` will provide the lerp interpolant in the range [0, 1] that results in 'value'.
* `fc.PropellantStageDisplayName(index)` and `fc.ResourceDisplayName(index)` show the display (localized) name of the selected resource, as opposed to the internal resource name.
* Internal changes that should help with performance a little bit more.

---
### MAS v0.19.0
For KSP 1.4.0 - KSP 1.4.4, 20 JUN 2018.

#### Known Issues
* MAS supports localization in the KSP GUI!  If you are able to provide translations, please take a look at [the localization file](https://github.com/MOARdV/AvionicsSystems/blob/master/GameData/MOARdV/AvionicsSystems/Localization/en-us.cfg).  Currently supported languages are *en-us*.
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* KSP 1.4.4 has a bug where aero effects (re-entry flames) can appear inside the IVA.  When these effects are active, colliders (buttons) inside the effects are not clickable.  This is a stock bug.

#### ATTENTION Players
* MAS uses its own maneuver planner (accessible from the MFD2 PLAN page) as well as its own Maneuver Autopilot (accessible from the MFD2 PLAN and MFD2 MNVR pages).  These may be buggy, but I would like feedback on both.  If you prefer using MechJeb to plan or to execute maneuvers, it is still accessible from the MechJeb control panel in the lower-central portion of the MRK-DM IVA.
* MFD2 (the modern MAS MFD design) now has an [operations guide](https://github.com/MOARdV/AvionicsSystems/wiki/MFD2-Operations-Guide) on the wiki.  This is still a work-in-progress.

#### ATTENTION IVA Prop Makers
* The `fc.Rcs` family of functions has changed as well.  Instead of returning RCS fuel in terms of mass, it returns RCS fuel in terms of units, like the generic resource queries.  There is now a `fc.RcsDensity()` function that can be used to query the current density of remaining propellant, and a `fc.RcsMass()` to query the current mass.
* The ASET Crew Manuals now have MAS equivalents.  The MAS versions of these props use the same name as the original ASET prop, with "MAS_" prefixed to the name.
* The ASET Advanced RCS control has been ported to MAS.
* A few other props have been added to MAS.

#### Fixes
* Misplaced / wrong-sized celestial bodies in MASCamera views have been fixed.  Issue #177.

#### New Features
* `fc.HeadingRate()` reports the instantaneous heading rate of change.

---
### MAS v0.18.0
For KSP 1.4.0 - KSP 1.4.3, 16 JUN 2018.

#### Known Issues
* MAS supports localization in the KSP GUI!  If you are able to provide translations, please take a look at [the localization file](https://github.com/MOARdV/AvionicsSystems/blob/master/GameData/MOARdV/AvionicsSystems/Localization/en-us.cfg).  Currently supported languages are *en-us*.
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.

#### ATTENTION Players
* MAS uses its own maneuver planner (accessible from the MFD2 PLAN page) as well as its own Maneuver Autopilot (accessible from the MFD2 PLAN and MFD2 MNVR pages).  These may be buggy, but I would like feedback on both.  If you prefer using MechJeb to plan or to execute maneuvers, it is still accessible from the MechJeb control panel (in the lower-central portion of the MRK-DM IVA).
* MFD2 (the modern MAS MFD design) now has an [operations guide](https://github.com/MOARdV/AvionicsSystems/wiki/MFD2-Operations-Guide) on the wiki.  This is still a work-in-progress.

#### ATTENTION IVA Prop Makers
* The `fc.Propellant` family of functions has changed.  Instead of returning propellant in terms of mass, it returns propellant in terms of units, like the generic resource queries.  There is now a `fc.PropellantDensity()` function that can be used to query the current density of remaining propellant, and a `fc.PropellantMass()` to query the current mass.  Issue #171.

#### New Features
* [COMPOUND_TEXT](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#compound_text) supports a master `variable` to control whether all of the child texts appear or not.
* The [MASIVTOL](github.com/MOARdV/AvionicsSystems/wiki/MASIVTOL) interface can now interact with the Air Park and Rotation Controller features of the WBI VTOL Manager.  It may also query which VTOL Manager features are available.  Issue #169.
* [IMAGE](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#image) MASMonitor nodes can specify `%NAVBALL_ICON%` to select icons from the Navball (prograde, retrograde, etc).  The icon can be configured using `fc.NavballU(iconId)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy2#fcnavballudouble-iconid)) and its related functions `fc.NavballV(iconId)`, `fc.NavballR(iconId)`, `fc.NavballG(iconId)`, and `fc.NavballB(iconId)`.  Issue #174.
* `nav.PlayNavAidIdentifier(radioId, volume, stopCurrent)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASINavigation#navplaynavaididentifierdouble-radioid-double-volume-bool-stopcurrent)) will play the three-letter Morse Code sequence identifying the navigation beacon selected by `radioId`.  Issue #87.
* `fc.PlayMorseSequence(sequence, volume, stopCurrent)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy2#fcplaymorsesequencestring-sequence-double-volume-bool-stopcurrent)) will play any arbitrary string of letters and blanks as a Morse Code sequence.  Issue #87.
* `fc.SuicideBurnTime()` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy3#fcsuicideburntime)) provides an estimate of how many seconds a vessel can wait before it must start a maximum-thrust burn to kill velocity before lithobraking.  Issue #137.
* MAS can add Kerbin's ground stations to the KSP custom waypoints table.  This feature can be triggered using the [MAS Settings](https://github.com/MOARdV/AvionicsSystems/wiki/MAS-Settings) dialog from the Space Center scene.  Issue #175.
* `fc.LandingTime()` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy2#fclandingtime)) returns the time until impact, or 0 if the current orbit does not intercept the surface.
* MAS supports interactions with [Kerbal Engineer Redux](https://github.com/MOARdV/AvionicsSystems/wiki/MASIKerbalEngineer).  The current list of supported functions is limited, but it may grow in the future.  Issue #176.

---
### MAS v0.17.0
For KSP 1.4.0 - KSP 1.4.3, 11 June 2018.

#### Known Issues
* MAS supports localization in the KSP GUI!  If you are able to provide translations, please take a look at [the localization file](https://github.com/MOARdV/AvionicsSystems/blob/master/GameData/MOARdV/AvionicsSystems/Localization/en-us.cfg).  Currently supported languages are *en-us*.
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.

#### ATTENTION Players
* MAS uses its own maneuver planner (accessible from the MFD2 PLAN page) as well as its own Maneuver Autopilot (accessible from the MFD2 PLAN and MFD2 MNVR pages).  These may be buggy, but I would like feedback on both.  If you prefer using MechJeb to plan or to execute maneuvers, it is still accessible from the MechJeb control panel (in the lower-central portion of the MRK-DM IVA).
* MFD2 (the modern MAS MFD design) now has an [operations guide](https://github.com/MOARdV/AvionicsSystems/wiki/MFD2-Operations-Guide) on the wiki.  This is still a work-in-progress.
* MFD2 now has a page to access some VTOL manager functionality from the WBI VTOL Manager mod (page accessed from the STBY / Standby page, select VTOL).  It also has a configurable Primary Flight Display page for aircraft operations (from the STBY page, select FLIGHT).  This PFD has several panels that can be reconfigured using the left/right and up/down arrows on the MFD.

#### Fixes
* `fc.SurfaceHorizontalSpeed()` and `fc.SurfaceForwardSpeed()` now return speed in meters/sec (like the documentation says they should do), instead of values between -1 and +1.
* Terminal velocity computations for `fc.TerminalVelocity()` are more stable at low speeds, and more accurate in flight.
* MASCamera no longer throws exceptions when a vessel equipped with MASCamera modules enters physics range.  Issue #173.

#### New Features
* MAS can interact with the Wild Blue Industries VTOL Manager, as found in the [Flying Saucers](https://forum.kerbalspaceprogram.com/index.php?/topic/173857-14x-pre-release-live-kerbal-flying-saucers-build-flying-saucers-in-ksp/) mod.  Interaction is presently limited to controlling thrust vector direction and hover mode.  Issue #169.
* The [MASFlightComputer](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputer) now supports running an optional startup script when it initializes, allowing IVA-specific customization of the MAS state.
* The [Resource Converter](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy3#resource-converter-category) category contains functions for interacting with parts that have ModuleResourceConverter.  The IVA creator can register for resource conversions that need to be tracked by using `startupScript` in MASFlightComputer, as mentioned in the previous entry.  Once a resource is registered, these functions to control toggling the tracked resource converters on or off, tracking how much resource they generate, or even whether they were installed.  By default, MAS installs "ElectricCharge" in group 0, which is used by the Fuel Cell functions in the Power category.  Issue #170.
* MASMonitor now supports [COMPOUND_TEXT](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#compound_text) nodes, which can be used to display checklists that automatically remove completed tasks, or prioritized notification systems.  Issue #6. (yes, it's one of the first issues I opened)
* `fc.GravityForce()` provides the effect of gravity on the vessel, in kN.  `fc.LiftUpForce()` provides the force of lift directly opposed to gravity.
* [LINE_STRING](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#line_string) has a `loop` configuration parameter to automatically generate closed-loop polygons without having to duplicate the first vertex.

---
### MAS v0.16.0
For KSP 1.4.0 - KSP 1.4.3, 1 June 2018.

#### Known Issues
* MAS supports localization in the KSP GUI!  If you are able to provide translations, please take a look at [the localization file](https://github.com/MOARdV/AvionicsSystems/blob/master/GameData/MOARdV/AvionicsSystems/Localization/en-us.cfg).  Currently supported languages are *en-us*.
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* [LINE_STRING](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#line_string) segments occasionally appear as bowties.  In addition, the texture UV pattern repeats at a different rate on line segments that are a different length.  Both of these are Unity issues.  **NOTE:** The Unity update should have fixed this issue.  It has not been verified.

#### ATTENTION Players
* MAS uses its own maneuver planner (accessible from the MFD2 PLAN page) as well as its own Maneuver Autopilot (accessible from the MFD2 PLAN and MFD2 MNVR pages).  These may be buggy, but I would like feedback on both.  If you prefer using MechJeb to plan or to execute maneuvers, it is still accessible from the MechJeb control panel (in the lower-central portion of the MRK-DM IVA).
* MFD2 (the modern MAS MFD design) now has an [operations guide](https://github.com/MOARdV/AvionicsSystems/wiki/MFD2-Operations-Guide) on the wiki.  This is still a work-in-progress.

#### ATTENTION IVA Prop Makers
* MFD2's maneuver control pages now use the (still experimental) MAS Maneuver Autopilot (see New Features, below).  MechJeb is no longer required to execute maneuver nodes automatically.
* The ASET_HUD has been imported to MAS (prop name MAS_ASET_HUD).  This prop requires [ASET Avionics](http://forum.kerbalspaceprogram.com/index.php?/topic/116479-aset-avionics-pack-v-20-for-the-modders-who-create-iva/) to be installed.

#### Fixes
* MFD1's engine status page no longer throws an error due to a function name changing in a previous MAS version.
* -force-glcore on Windows no longer causes textures, the nav ball, and most other MFD components to disappear.  Issue #168.

#### New Features
* `fc.SetBodyTarget(id)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#fcsetbodytargetobject-id)) allows MAS to target any body using either the numeric index of the body or its name.  Issue #162.
* The [MAS Maneuver Autopilot](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#autopilot-category) is now available.  Issue #137.
* The [INT_LIGHT](https://github.com/MOARdV/AvionicsSystems/wiki/MASComponent#int_light) component may now control the color and intensity of interior lights in addition to switching them on or off.  Issue #164.
* MAS's FixedUpdate processing overhead has been reduced.  Local testing has shown about a 20% reduction in time spent updating variables.  This change may help lower-spec computers.  Issue #166.

---
### MAS v0.15.0 / v0.15.1
For KSP 1.4.0 - KSP 1.4.3, 24 May 2018.

#### Known Issues
* MAS supports localization in the KSP GUI!  If you are able to provide translations, please take a look at [the localization file](https://github.com/MOARdV/AvionicsSystems/blob/master/GameData/MOARdV/AvionicsSystems/Localization/en-us.cfg).  Currently supported languages are *en-us*.
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* [LINE_STRING](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#line_string) segments occasionally appear as bowties.  In addition, the texture UV pattern repeats at a different rate on line segments that are a different length.  Both of these are Unity issues.  **NOTE:** The Unity update should have fixed this issue.  It has not been verified.

#### Fixes
* The closest approach computations should be more stable, and the solver should behave better with targets orbiting other bodies.  Issue #101.
* Temperature queries always return 0 if there are no applicable components (for instance, `fc.HottestEngineTemperature()` will return 0 if there are no engines).  Previously, these values returned 0 for Kelvin, or -273.15 for Celsius.
* LINE_GRAPH functionality has been restored.
* MAS Radio Navigation Beacons are no longer added to the custom waypoints database by default.  Instead, the player must opt-in from the [MAS Settings](https://github.com/MOARdV/AvionicsSystems/wiki/MAS-Settings) dialog in the Space Center scene.  Issue #161.

#### New Features
* `fc.TargetClosestApproachSpeed()` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy3#fctargetclosestapproachspeed)) and `fc.ManeuverNodeTargetClosestApproachSpeed()`([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy2#fcmaneuvernodetargetclosestapproachspeed)) report the relative speed of a target at closest approach, and at closest approach after the scheduled maneuver node.  Issue #157.
* `fc.ActionGroupActiveMemo(groupID)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#fcactiongroupactivememodouble-groupid)) and `fc.ActionGroupMemo(groupID, active)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#fcactiongroupmemodouble-groupid-bool-active)) report the action group memo as specified in the ship's description.  This feature works the same as RPM's AGMEMO.  Issue #147.
* `fc.Select(condition, trueValue, falseValue)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy3#fcselectbool-condition-object-truevalue-object-falsevalue)) can be used to return one of two different values depending on `condition`.
* Some internal code changes may reduce the impact MAS has on games with many vessels / debris.  Issue #159.
* The [POLYGON](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#polygon) MASMonitor node allows the creation of arbitrary solid-filled polygons on an MFD display.  These polygons can have variable colors, and all vertices can be variable (allowing the shape to be altered).  Issue #158.
* `fc.ResourceStageMass(id)` and `fc.ResourceStageMassMax(id)` returns the mass of the selected resource available to the current stage, and the maximum mass of that resource on the current stage.  Issue #160.
* Methods related to [Cargo Bays](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#cargo-bay) have been added.  These work with ModuleAnimateGeneric cargo bays (such as the stock 1.25m Service Bay).  Issue #160.
* The MechJeb [Ascent Autopilot and Guidance](https://github.com/MOARdV/AvionicsSystems/wiki/MASIMechJeb#ascent-autopilot-and-guidance-category) Category now supports controlling "Force Roll" during the ascent guidance, as well as setting both the vertical roll and turn roll values.  Issue #154.
* The [MAS Attitude Control](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#autopilot-category) is the first autopilot for MAS.  It provides attitude control similar to the advanced MechJeb SASS modes.  Issue #137.

---
### MAS v0.14.0
For KSP 1.4.0 - KSP 1.4.3, released 5 May 2018.

#### Known Issues
* MAS supports localization in the KSP GUI!  If you are able to provide translations, please take a look at [the localizations file](https://github.com/MOARdV/AvionicsSystems/blob/master/GameData/MOARdV/AvionicsSystems/Localization/en-us.cfg).  Currently supported languages are *en-us*.
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* [LINE_STRING](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#line_string) segments occasionally appear as bowties.  In addition, the texture UV pattern repeats at a different rate on line segments that are a different length.  Both of these are Unity issues.  **NOTE:** The Unity update should have fixed this issue.  It has not been verified.

#### ATTENTION IVA Prop Makers
* `parachute.Available()` has been renamed to `parachute.RealChuteAvailable()` to make it clear that it's specific to whether RealChute is installed.
* The [Prop Config](https://github.com/MOARdV/AvionicsSystems/wiki/Prop-Config) tool is now available for MAS.  This tool allows for the automation of prop configuration.  Issue #135.
* Many of the prop configs in MAS_ASET have been processed by the Prop Config tool.  In most cases, this will have no effect on an IVA.  However, a small number of nearly-identical props were removed, a few had stylistic changes, and some have new names.  In particular, the original MFD1 mode buttons have been removed.  You are recommended to delete the MAS_ASET directory and overwrite it when updating.
* Error reporting for incorrect parameters is improved.  Instead of a NullReferenceException, MAS will tell you that a parameter did not match what MAS expected, and it will tell you the variable that it was evaluating.  Issue #155.

#### Fixes
* Multiple [VERTICAL_BAR](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#vertical_bar) texture and position errors have been fixed. Issue #123.
* The FoV marker for the [MASCamera](https://github.com/MOARdV/AvionicsSystems/wiki/MASCamera) has been fixed so that it updates for animated cameras.  Issue #133.
* Multiple [MASCamera](https://github.com/MOARdV/AvionicsSystems/wiki/MASCamera) modules may be installed on a single part, with each module containing unique MODE settings.  Issue #133.
* `transfer.MatchPlane()` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASITransfer#transfermatchplane)) now does not increase relative inclination for some maneuvers.  Issue #107.
* Multiple props that were still referencing `realchute` instead of `parachute` were updated.
* Various bugs related to the atmosphere/skybox in [MASCamera](https://github.com/MOARdV/AvionicsSystems/wiki/MASCamera) rendering on the MFD have been fixed.  Issue #139.
* MAS props no longer stop updating when using internal camera views (such as double-clicking IVA windows). Issue #152.

#### New Features
* `fc.Lift()` and `fc.Drag()` return the lift force and drag force on the vessel, in kN.  `fc.DragAccel()` returns the effect of drag on the vessel in m/s^2.  `fc.TerminalVelocity()` reports the current terminal velocity, or 0 if the vessel is out of the atmosphere.  Issue #122.
* `fc.GetActiveEnginesGimbal()` returns 1 if any currently-active engines have a gimbal.  Issue #126.
* The `position`, `sourceColor`, and `borderColor` fields for VERTICAL_BAR and HORIZONTAL_BAR accept variables.  Issue #123
* `fc.Acceleration()` has been renamed `fc.AccelEngines()`.  New acceleration methods `fc.Acceleration()` (net acceleration), `fc.AccelTop()`, `fc.AccelForward()`, `fc.AccelRight()`, `fc.AccelUp()`, `fc.AccelSurfacePrograde()`, `fc.AccelSurfaceForward()`, and `fc.AccelSurfaceRight()` have been added to the [Speed, Velocity, and Acceleration](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy3#speed-velocity-and-acceleration-category) category.  Issue #121.
* RCS translation control and rotation control can be queried, toggled, and enabled/disabled using `fc.GetRCSRotate()`, `fc.GetRCSTranslate()`, `fc.ToggleRCSRotate()`, `fc.ToggleRCSTranslate()`, `fc.SetRCSRotate(bool)`, and `fc.SetRCSTranslate(bool)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy2#rcs-category)).  Issue #128.
* [TEXT](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#text) page nodes support the use of variables for `position`.  Issue #130.
* The [Thermal](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy3#thermal-category) category has multiple new methods: `fc.InternalMaxTemperature(bool)` for max pod internal temperature, `fc.InternalTemperatureSign()` to indicate warming/cooling.  `fc.PodTemperature(bool)`, `fc.PodMaxTemperature(bool)`, and `fc.PodTemperatureSign()` for pod skin temperature.  `fc.HeatShieldTemperature(bool)`, `fc.HeatShieldMaxTemperature(bool)`, and `fc.HeatShieldTemperatureSign()` for the hottest heat shield.  `fc.HottestEngineTemperatureSign()` for heating/cooling of the hottest engine.  `fc.HottestEngineTemperatureMax(bool)` has been renamed `fc.HottestEngineMaxTemperature(bool)` to be consistent with the other methods.  Issue #120.
* The MASClimateControl module and methods related to it have been removed.
* `fc.GetReactionWheelAuthority()` and `fc.SetReactionWheelAuthority(authority)` allow changing reaction wheel authority in-flight.  Issue #22.
* The [Gear](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#gear-category) category now includes `fc.GearCount()`, `fc.GearBrokenCount()`, `fc.GearMoving()`, `fc.GearPosition()`, and `fc.GearStress()`.  Issue #125.
* The `[n]` or `[n#]` tag ([see](https://github.com/MOARdV/AvionicsSystems/wiki/Formatted-Rich-Text#newline-tag-n-or-n)) may be used to create vertical text in [TEXT](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#text) nodes of a MASMonitor.  Issue #136.
* [COLOR_SHIFT](https://github.com/MOARdV/AvionicsSystems/wiki/MASComponent#color_shift) and [TEXT_LABEL](https://github.com/MOARdV/AvionicsSystems/wiki/MASComponent#text_label) `activeColor` and `passiveColor` fields support variables for the color channels (R, G, B, and A).  [LINE_GRAPH](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#line_graph) `borderColor` and `sourceColor` fields likewise support variables for the color channels.  Issue #74.
* `fc.ActiveDockingPortCamera()` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#fcactivedockingportcamera)) returns the name of a MASCamera attached to the current reference transform when that transform is a docking port.
* `fc.GetCameraMaxTilt(index)`, `fc.GetCameraMinTilt(index)`, `fc.GetCameraMaxPan(index)`, and `fc.GetCameraMinPan(index)` in the [Camera](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#cameras-category) section report the maximum and minimum tilt and pan limits of the selected camera.  Issue #133.
* `fc.ActiveDockingPortCamera()` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#fcactivedockingportcamera)) returns the name of the [MASCamera](https://github.com/MOARdV/AvionicsSystems/wiki/MASCamera) installed on the current reference transform when that transform is a docking port.  If the reference transform is not a docking port, this function returns "", indicating "no active camera".  Issue #133.
* Unnamed [MASCamera](https://github.com/MOARdV/AvionicsSystems/wiki/MASCamera) modules will automatically be named and numbered, "Camera #" by default (ie, "Camera 1", or "Dock #" if the camera is on a docking port.  Cameras with duplicate names will automatically be numbered (ie, if two cameras are called "ExtCam", MAS will name them "ExtCam 1" and "ExtCam 2").  Issue #133.
* [MASCameraMount](https://github.com/MOARdV/AvionicsSystems/wiki/MASCamera#mascameramount) is a new part module that controls a pan transform and tilt transform on a camera's part.  This allows for the part to move as the camera is panned or tilted.  **NOTE:** This functionality was previously part of MASCamera (`panTransformName` and `tiltTransformName`), so any cameras including those fields need to updated to add the new MASCameraMount module.  Issue #133.
* `engine.GetCoreThrottle(engineId)`, `engine.GetAfterburningThrottle(engineId)`, `engine.GetCurrentJetTemperature(engineId, useKelvin)`, and `engine.GetMaxJetTemperature(engineId, useKelvin)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASIEngine#aje-jet-category)) report the core throttle and afterburning throttle positions and the current and maximum temperatures for Advanced Jet Engines jet engines (ModuleEnginesAJEJet).  Issue #56.
* [MASComponent](https://github.com/MOARdV/AvionicsSystems/wiki/MASComponent) modules now support an optional `startupScript`.  Issue #148.
* The [MASFlightComputer](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputer) module supports a new optional field, `powerOnVariable`.  When present, this variable can affect the results of `fc.Conditioned()`.  Refer to [Power Disruption](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputer#power-disruption) for details.  Issue #146.
* The MRK-DM IVA has had some minor updates, in addition to some MFD page updates.
* [TEXTURE_SHIFT](https://github.com/MOARdV/AvionicsSystems/wiki/MASComponent#texture_shift) supports variables for both `startUV` and `endUV` fields.  Issue #151.

---
### MAS v0.13.0
For KSP 1.4.0 - KSP 1.4.3, released 18 March 2018.

#### Known Issues
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* [LINE_STRING](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#line_string) segments occasionally appear as bowties.  In addition, the texture UV pattern repeats at a different rate on line segments that are a different length.  Both of these are Unity issues.  **NOTE:** The Unity update should have fixed this issue.  It has not been verified.

#### ATTENTION IVA Prop Makers
* The `realchute` module, [MASIRealChute](https://github.com/MOARdV/AvionicsSystems/wiki/MASIRealChute), was renamed to `parachute` in v0.13.0.  Errors will occur if any props attempt to call `realchute`.  Issue #108.

#### New Features
* `fc.BodyIsMoon(id)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#fcbodyismoonobject-id)) returns 0 if the body orbits the sun, 1 if it is a moon of another body.  `fc.BodyParent(id)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#fcbodyparentobject-id)) returns the id of the parent of the selected body.  Issue #114.

#### Fixes
* Shaders and code updated for Unity 2017.1.3p1.  Issue #117.
* `fc.SIFormatValue()` guards against NaN and INF values.  Issue #90.

---
### MAS v0.12.0
For KSP 1.3.0 - KSP 1.3.1, released 27 January 2018.

#### Known Issues
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* [LINE_STRING](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#line_string) segments occasionally appear as bowties.  In addition, the texture UV pattern repeats at a different rate on line segments that are a different length.  Both of these are Unity issues.

#### ATTENTION IVA Prop Makers
* The `realchute` module, [MASIRealChute](https://github.com/MOARdV/AvionicsSystems/wiki/MASIRealChute), will be renamed to `parachute` in v0.13.0.  Starting in v0.12.0, both `realchute` and `parachute` may be used.  However, with v0.13.0, `realchute` will no longer work.  Props should be updated before v0.13.0 by finding `realchute.` and replacing it with `parachute.`.
* Applying post-processing effects to a [MASCamera](https://github.com/MOARdV/AvionicsSystems/wiki/MASCamera) has changed.  Post-processing effects are now attached to the camera instead of the monitor.  See below in "New Features" for more information.

#### New Features
* `fc.CancelTimeWarp(instantCancel)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy2#fccanceltimewarp)) cancels time warp / auto warp.  `fc.SetWarp(warpRate, instantChange)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy2#fcsetwarpdouble-warprate-bool-instantchange)) sets the time warp rate.  `fc.WarpTo(UT)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy2#fcwarptodouble-ut)) warps to the specified UT.  Issue #103.
* Preliminary MAS-enabled MRK IVA is now included with MAS.  The MRK is a recent addition to [Commonwealth Rockets](https://forum.kerbalspaceprogram.com/index.php?/topic/164365-13-commonwealth-rockets-tea-powered-spaceflight-in-development/).
* `fc.CommNetSignalQuality()` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#fccommnetsignalquality)) returns a value between 0 and 4 representing the CommNet signal quality (none, red, orange, yellow, green).
* `fc.ColorTag(r, g, b)` and `fc.ColorTag(r, g, b, a)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy2#fccolortagdouble-red-double-green-double-blue)) compose a Formatted Rich Text [Color Tag](https://github.com/MOARdV/AvionicsSystems/wiki/Formatted-Rich-Text#color-tags-rrggbbaa) from the supplied variables.
* [MASThrustReverser](https://github.com/MOARdV/AvionicsSystems/wiki/MASThrustReverser) provides a way to identify engines that contain thrust reversers, allowing MAS to control the thrust reversers and report on their status.  The [MASIEngine](https://github.com/MOARdV/AvionicsSystems/wiki/MASIEngine) module provides methods to control the thrust reversers.  Issue #104.
* The [Crew Category](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#crew-category) functions have been substantially changed, which will require changes to prop configurations.  Issue #100.
* `fc.PlayAudio()` is now functional.  Issue #100.
* `transfer` ejection angle parameters and Oberth-effect transfer parameters are now implemented.  Issue #100, Issue #38.
* [MASCamera](https://github.com/MOARdV/AvionicsSystems/wiki/MASCamera) allows the FoV parameters to range between 1 and 179.  Issue #71.
* The MAS [Maneuver Planner](https://github.com/MOARdV/AvionicsSystems/wiki/MASITransfer#maneuver-planning-category) can generate maneuver nodes for a variety of basic operations (change Ap, change Pe, circularize, Hohmann transfer, etc) without the use of an external mod.  Issue #107.
* Stock parachute support has been improved - deployed parachutes no longer cause `parachute.DeploymentSafe()` to return 0 or -1, and `parachute.ToggleParachuteArmed()` will activate stock parachutes that are configured to deploy when safe.  Issue #108.
* The filter used to list the number of vessel targets can now be modified using `fc.SetTargetFilter(vesselType)`, `fc.ClearTargetFilter(vesselType)`, and `fc.ToggleTargetFilter(vesselType)`.  The current state of the filter can be queried with `fc.GetTargetFilter(vesselType)`.  All methods are in the [Target and Rendezvous Category](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy3#target-and-rendezvous-category).  Issue #109.
* The [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) monitor page node no longer apples post-processing effects.  Effects are now configured in the camera directly by adding one or more [MODE](https://github.com/MOARdV/AvionicsSystems/wiki/MASCamera#camera-modes) nodes to MASCamera.  Issue #71.
* [MASCamera](https://github.com/MOARdV/AvionicsSystems/wiki/MASCamera) allows the IVA maker to specify one or both of a pan transform name and a tilt transform name.  These fields allow MAS to move the camera's model in response to user pan and tilt changes.  Issue #71.
* [MASCamera](https://github.com/MOARdV/AvionicsSystems/wiki/MASCamera) now supports rate limits on Field of View, Pan, and Tilt, allowing the MASCamera to simulate mechanical limits.  Issue #57.
* All MAS part modules support localization in the VAB/SPH and flight right-click menus.  In addition, the MAS Settings menu in the Space Center scene also supports localization.  Currently, only English (en-us) is available.  Issue #111.
* [MASIEngine](https://github.com/MOARdV/AvionicsSystems/wiki/MASIEngine#aje-propellers-category) reports data from [Advanced Jet Engine](https://forum.kerbalspaceprogram.com/index.php?/topic/139868-131-advanced-jet-engine-v2100-january-13/) propeller engines, and allows adjusting RPM, mixture, and charger boost settings.  Issue #56.

#### Fixes
* MFD1 (older style) camera select controls (Prev/Next) work.
* MAS_BITMAP_FONTs work once more.  Issue #105.
* Closest approach computations when the target is a planet or moon, and the vessel enters its SoI, are now correct.  Issue #101.

---
### MAS v0.11.0
For KSP 1.3.0 - KSP 1.3.1, released 16 Dec 2017.

#### Known Issues
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes display artifacts when the Scatterer mod is installed.
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.
* [LINE_STRING](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#line_string) segments occasionally appear as bowties.  In addition, the texture UV pattern repeats at a different rate on line segments that are a different length.  Both of these are Unity issues.

#### New Features
* `fc.SetPersistentBlended(persistentName, value, maxChangePerSecond)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy2#fcsetpersistentblendedstring-persistentname-double-value-double-maxchangepersecond)) can be used to blend a numeric persistent value from one value to the next over a period of time.  Issue #10.
* [[Radio Navigation]] has been implemented, as has custom Waypoint navigtion.  Issue #11 and Issue #61.
* The [LINE_STRING](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#line_string) MASMonitor node displays a line string of two or more points with fully-configurable vertex positions, beginning and end colors, and beginning and end line widths.  A texture may be applied to create dotted-line effects.  In addition, a `rotation` variable can be used to rotate the entire line string as a single unit.  Issue #17.
* The [ELLIPSE](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#ellipse) MASMonitor nodes displays a line string ellipse or circle around an origin.  Line the LINE_STRING, above, the colors and widths may be changed at run-time, a texture may be applied to apply effects, and a `rotation` variable may be used to rotate it.  Issue #17.
* `missingTexture` on a [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) node in a monitor specifies the texture to use when a missing camera is selected, or the active camera is lost.  Issue #60.
* `fc.HeadingPrograde()` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy2#fcheadingprograde)) provides the heading of the surface prograde vector, as opposed to `fc.Heading()`, which reports the heading of the front of the vessel.  Issue #70.
* `fc.BodyTerrainHeight(id, latitude, longitude)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#fcbodyterrainheightobject-id-double-latitude-double-longitude)) and `fc.BodyTerrainSlope(id, latitude, longitude)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#fcbodyterrainslopeobject-id-double-latitude-double-longitude)) report the terrain altitude and terrain slope, respectively, at a given location on the selected body.  Issue #73.
* `fc.SlopeAngle()` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy2#fcslopeangle)) reports the slope of the ground beneath the vessel.
* The [RPM_MODULE](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#rpm_module) node in MASMonitor allows MAS to interact with modules written for RasterPropMonitor.  Issue #69.
* The landing prediction methods (altitude, latitude, longitude, and time) no longer require MechJeb to generate results. 
 They will still prefer MJ if it's available and active.  Issue #75.
* `fc.MaxTWR(useThrottleLimits)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#fcmaxtwrbool-usethrottlelimits)) now requires a `useThrottleLimits` parameter.  Issue #79.
* [GROUND_TRACK](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#ground_track) nodes in MASMonitor render the ground track of the vessel's orbit, as well as the current target's orbit and the orbit resulting from a maneuver.  Issue #81.
* [IMAGE](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#image) nodes in MASMonitor have more variable-controlled options (dynamic position, scale, UV tiling, and colors).  Issue #82.
* Documentation now includes a [Index of Functions](https://github.com/MOARdV/AvionicsSystems/wiki/Index-of-Functions) that lists all functions in MAS, with partial summaries and links to their full entries.  Issue #86.
* [MAS_PAGE](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#pages) supports custom actions bound to soft keys.  These custom actions are defined per-page.  In addition, each page may define an `onEntry` and `onExit` script which executes when the page is selected or de-selected, respectively.  Issue #89.
* Apply a numerically stable computation for maneuver node delta-V decomposition.  Issue #92.
* Multiple methods in the [Maneuver Node](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy2#maneuver-node-category) section to support post-maneuver SoI changes, burn times, and closest approach computations.  Issue #92.
* Retired the modified BDB Kane and Mk3-9 command pods, since they are woefully out of date.
* Updated the Yarbrough08 command pod with modernized MFDs.
* The `DYNAMIC_TEXTURE_SHIFT` MASComponent node has been removed.  A future release will incorporate its capabilities into `TEXTURE_SHIFT`.
* `fc.NormalizeAngle(double)`, `fc.NormalizeLongitutde(double)`, and `fc.NormalizePitch(double)` all may be used to normalize various angle measurements.
* `fc.BodySunLongitude(object id)` returns the longitude of the selected body that is directly below the sun (local noon).
* `fc.PeriodCount(double period, double countTo)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy2#fcperiodcountdouble-period-double-countto)) provides an integer counter from 0 to (countTo - 1) that advances by 1 every 1/period seconds.
* [Named Colors](https://github.com/MOARdV/AvionicsSystems/wiki/Named-Colors) now include the XKCD color table, as well as some KSP-specific named colors.
* `fc.BodyMoonCount(id)` returns the number of moons orbiting the selected body.
* `fc.SetTargetVessel(id)` sets target to the vessel indexed by 'id'.
* `fc.TargetVesselDistance(id)` and `fc.TargetVesselName(id)` report the distance and name of the vessel indexed by 'id'.
* `fc.PitchActivePrograde()` and `fc.YawActivePrograde()` return the vessel's yaw and pitch relative to the current SAS mode prograde (orbit, surface, or target).
* `fc.PitchSAS()` and `fc.YawSAS()` return the pitch and yaw from the current selected SAS mode.  If SAS is disabled, or set to Stability Assist, returns 0.
* `fc.PitchWaypoint()` and `fc.YawWaypoint()` return the pitch and yaw to the current waypoint.  If there is no active waypoint, returns 0.
* `fc.BodyBiome(id, lat, long)` returns the biome name at the specified location of the selected body.
* [ANIMATION](https://github.com/MOARdV/AvionicsSystems/wiki/MASComponent#animation) and [ANIMATION_PLAYER](https://github.com/MOARdV/AvionicsSystems/wiki/MASComponent#animation_player) MASComponent nodes support playing animations on the part the IVA is in, in addition to the prop that MASComponent is installed on.
* [%MAP_ICON%](https://github.com/MOARdV/AvionicsSystems/wiki/Keyword#map_icon) can be used to select the Map View texture atlas, which contains the icons for ships and orbital nodes that are displayed in map view.  Icons may be selected using `fc.MapIconU(iconId)` and `fc.MapIconV(iconId)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy2#fcmapiconudouble-iconid)) in the texture's `shiftUV` configuration.  Issue #97.

#### Fixes
* [CAMERA](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#camera) nodes detect when the active camera is lost (through staging, for instance), and they now revert to the `missingTexture`, if provided, or blank display.  Issue #60.
* [NAVBALL](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#navball) nodes render once again.  Issue #77.
* [HORIZONTAL_BAR](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#horizontal_bar) and [VERTICAL_BAR](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#vertical_bar) no longer fail to function if their optional border lines are not used.  Issue #78.
* `fc.GetRCSActive()`([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy2#fcgetrcsactive)) returns 1 only if an RCS thruster is actively firing, not just armed.  Issue #80.
* Radial In and Radial Out have been swapped so that the reported mode matches the mode KSP shows.
* [HORIZONTAL_BAR](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#horizontal_bar) and [VERTICAL_BAR](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#vertical_bar) borders have been fixed (previously, only the bottom line of the border was displayed).
* `fc.BodyIsHome(id)` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#fcbodyishomeobject-id)) returns 1 if the selected body is "Home", as defined in the KSP Planetarium.  Using the id -1 will report if the current vessel is orbiting home.
* `fc.CommNetLatitude()` and `fc.CommNetLongitude()` ([see](https://github.com/MOARdV/AvionicsSystems/wiki/MASFlightComputerProxy#fccommnetlatitude)) return the latitude and longitude of the active CommNet relay endpoint.  If there is no connection home, both return 0.  Issue #110.

---
### MAS v0.10.0
For KSP 1.3.0 - KSP 1.3.1, released 5 Nov 2017.

#### Known Issues
* CAMERA nodes display artifacts when the Scatterer mod is installed.
* CAMERA nodes do not update correctly when a part containing a camera is removed during flight (either staging or undocking).
* Docking / undocking occasionally causes the MAS system to become unresponsive.  Changing scenes or switching to an unloaded vessel will reset it.

#### New Features
* [IMAGE](https://github.com/MOARdV/AvionicsSystems/wiki/MASMonitor#image) nodes in a MASMonitor page may now be rotated by a variable.  The rotation defaults around the center of the image, but the rotation anchor point may be offset.
* [MASContextMenu](https://github.com/MOARdV/AvionicsSystems/wiki/MASContextMenu) has been added.  This is a part module that can allow MAS actions to be triggered from the context menu (right-click menu) of a part as well as from an action group.

#### Fixes
* The font rendering system for TEXT nodes in a MASMonitor page has been substantially improved.

---
### Pre-MAS v0.10.0
Various development builds.
