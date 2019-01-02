/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2019 MOARdV
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 * 
 ****************************************************************************/
using KSP.UI;
using KSP.UI.Screens;
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    // ΔV - put this somewhere where I can find it easily to copy/paste

    /// <summary>
    /// The flight computer proxy provides the interface between the flight
    /// computer module and the Lua environment.  It is a thin wrapper over
    /// the flight computer that prevents in-Lua access to some elements.
    /// 
    /// Note that this class must be stateless - it can not maintain variables
    /// between calls because there is no guarantee it'll exist next time it's
    /// called.
    /// 
    /// Also note that, while it is a wrapper for ASFlightComputer, not all
    /// values are plumbed through to the flight computer (for instance, the
    /// action group control and state are all handled in this class).
    /// </summary>
    /// <LuaName>fc</LuaName>
    /// <mdDoc>
    /// The `fc` group contains the core interface between KSP, Avionics
    /// Systems, and props in an IVA.  It consists of many 'variable' functions
    /// that can be used to get information as well as numerous 'action' functions
    /// that are used to do things.
    /// 
    /// Due to the number of methods in the `fc` group, this document has been split
    /// across three pages:
    ///
    /// * [[MASFlightComputerProxy]] (Abort - Lights),
    /// * [[MASFlightComputerProxy2]] (Maneuver Node - Reaction Wheel), and
    /// * [[MASFlightComputerProxy3]] (Resources - Vessel Info).
    /// 
    /// *NOTE:* If a variable listed below includes an entry for 'Required Mod(s)',
    /// then the mod listed (or any of the mods, if more than one) must be installed
    /// for that particular feature to work.
    /// </mdDoc>
    internal partial class MASFlightComputerProxy
    {

        private static readonly MASVesselComputer.ReferenceAttitude[] referenceAttitudes =
        {
            MASVesselComputer.ReferenceAttitude.REF_INERTIAL,
            MASVesselComputer.ReferenceAttitude.REF_ORBIT_PROGRADE,
            MASVesselComputer.ReferenceAttitude.REF_ORBIT_HORIZONTAL,
            MASVesselComputer.ReferenceAttitude.REF_SURFACE_PROGRADE,
            MASVesselComputer.ReferenceAttitude.REF_SURFACE_HORIZONTAL,
            MASVesselComputer.ReferenceAttitude.REF_SURFACE_NORTH,
            MASVesselComputer.ReferenceAttitude.REF_TARGET,
            MASVesselComputer.ReferenceAttitude.REF_TARGET_RELATIVE_VEL,
            MASVesselComputer.ReferenceAttitude.REF_TARGET_ORIENTATION,
            MASVesselComputer.ReferenceAttitude.REF_MANEUVER_NODE,
            MASVesselComputer.ReferenceAttitude.REF_SUN,
        };

        internal const double KelvinToCelsius = -273.15;

        private MASFlightComputer fc;
        internal MASVesselComputer vc;
        internal MASIFAR farProxy;
        internal MASIKerbalEngineer keProxy;
        internal MASIMechJeb mjProxy;
        internal Vessel vessel;
        private UIStateToggleButton[] SASbtns = null;

        private VesselAutopilot.AutopilotMode autopilotMode = VesselAutopilot.AutopilotMode.StabilityAssist;
        private int vesselSituationConverted;

        private ApproachSolver nodeApproachSolver;

        private CommNet.CommLink lastLink;

        [MoonSharpHidden]
        public MASFlightComputerProxy(MASFlightComputer fc, MASIFAR farProxy, MASIKerbalEngineer keProxy, MASIMechJeb mjProxy)
        {
            this.fc = fc;
            this.farProxy = farProxy;
            this.keProxy = keProxy;
            this.mjProxy = mjProxy;
            this.nodeApproachSolver = new ApproachSolver();
        }

        ~MASFlightComputerProxy()
        {
            fc = null;
            vc = null;
            farProxy = null;
            keProxy = null;
            mjProxy = null;
            vessel = null;
            SASbtns = null;
        }

        /// <summary>
        /// Helper function to convert a vessel situation into a number.
        /// </summary>
        /// <param name="vesselSituation"></param>
        /// <returns></returns>
        [MoonSharpHidden]
        static internal int ConvertVesselSituation(Vessel.Situations vesselSituation)
        {
            int situation = (int)vesselSituation;
            for (int i = 0; i < 0x10; ++i)
            {
                if ((situation & (1 << i)) != 0)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>
        /// Per-FixedUpdate updater method to read some of those values that are used a lot.
        /// </summary>
        [MoonSharpHidden]
        internal void Update()
        {
            autopilotMode = vessel.Autopilot.Mode;
            nodeApproachSolver.ResetComputation();

            vesselSituationConverted = ConvertVesselSituation(vessel.situation);

            for (int i = neighboringVessels.Length - 1; i >= 0; --i)
            {
                neighboringVessels[i] = null;
            }
            neighboringVesselsCurrent = false;

            try
            {
                lastLink = vessel.connection.ControlPath.Last;
            }
            catch
            {
                lastLink = null;
            }
        }

        private kerbalExpressionSystem[] crewExpression = new kerbalExpressionSystem[0];
        [MoonSharpHidden]
        private kerbalExpressionSystem GetVesselCrewExpression(int index)
        {
            if (crewExpression.Length != vessel.GetCrewCount())
            {
                crewExpression = new kerbalExpressionSystem[vessel.GetCrewCount()];
            }

            vessel.GetVesselCrew()[index].KerbalRef.GetComponentCached<kerbalExpressionSystem>(ref crewExpression[index]);

            return crewExpression[index];
        }

        /// <summary>
        /// Private method to map a string or number to a CelestialBody.
        /// </summary>
        /// <param name="id">A string or number identifying the celestial body.</param>
        [MoonSharpHidden]
        private CelestialBody SelectBody(object id)
        {
            CelestialBody cb = null;

            if (id is double)
            {
                int idx = (int)(double)id;
                if (idx >= 0 && idx < FlightGlobals.Bodies.Count)
                {
                    cb = FlightGlobals.Bodies[idx];
                }
            }
            else if (id is string)
            {
                string bodyName = id as string;
                cb = FlightGlobals.Bodies.Find(x => (x.bodyName == bodyName));
            }

            return cb;
        }


        // Keep a scratch list handy.  The members of the array are null'd after TargetNextVessel
        // executes to make sure we're not holding dangling references.  This could be written more
        // efficiently, but I don't see this being used extensively.
        private Vessel[] neighboringVessels = new Vessel[0];
        private VesselDistanceComparer distanceComparer = new VesselDistanceComparer();
        private List<Vessel> localVessels = new List<Vessel>();
        private bool neighboringVesselsCurrent = false;

        [MoonSharpHidden]
        private bool EnabledType(global::VesselType type)
        {
            return fc.activeVesselFilter.FindIndex(x => x == type) != -1;
        }

        [MoonSharpHidden]
        private void UpdateNeighboringVessels()
        {
            if (!neighboringVesselsCurrent)
            {
                // Populate 
                var allVessels = FlightGlobals.fetch.vessels;
                int allVesselCount = allVessels.Count;
                CelestialBody mainBody = vessel.mainBody;
                for (int i = 0; i < allVesselCount; ++i)
                {
                    Vessel v = allVessels[i];
                    if (v.mainBody == mainBody && EnabledType(v.vesselType) && v != vessel)
                    {
                        localVessels.Add(v);
                    }
                }

                int arrayLength = neighboringVessels.Length;
                if (arrayLength != localVessels.Count)
                {
                    neighboringVessels = localVessels.ToArray();
                }
                else
                {
                    for (int i = 0; i < arrayLength; ++i)
                    {
                        neighboringVessels[i] = localVessels[i];
                    }
                }
                localVessels.Clear();

                distanceComparer.vesselPosition = vessel.GetTransform().position;
                Array.Sort(neighboringVessels, distanceComparer);

                neighboringVesselsCurrent = true;
            }
        }

        private class VesselDistanceComparer : IComparer<Vessel>
        {
            internal Vector3 vesselPosition;
            public int Compare(Vessel a, Vessel b)
            {
                float distA = Vector3.SqrMagnitude(a.GetTransform().position - vesselPosition);
                float distB = Vector3.SqrMagnitude(b.GetTransform().position - vesselPosition);
                return (int)(distA - distB);
            }
        }

        /// <summary>
        /// The Abort action and the GetAbort query belong in this category.
        /// </summary>
        #region Abort
        /// <summary>
        /// Trigger the Abort action group.
        /// </summary>
        /// <returns>1 (abort is always a SET, not a toggle).</returns>
        public double Abort()
        {
            vessel.ActionGroups.SetGroup(KSPActionGroup.Abort, true);
            return 1.0;
        }

        /// <summary>
        /// Returns 1 if the Abort action has been triggered.
        /// </summary>
        /// <returns></returns>
        public double GetAbort()
        {
            return (vessel.ActionGroups[KSPActionGroup.Abort]) ? 1.0 : 0.0;
        }
        #endregion

        /// <summary>
        /// Variables and actions related to player-configured action groups are in this
        /// category.
        /// </summary>
        #region Action Groups
        private static readonly KSPActionGroup[] ags = { KSPActionGroup.Custom10, KSPActionGroup.Custom01, KSPActionGroup.Custom02, KSPActionGroup.Custom03, KSPActionGroup.Custom04, KSPActionGroup.Custom05, KSPActionGroup.Custom06, KSPActionGroup.Custom07, KSPActionGroup.Custom08, KSPActionGroup.Custom09 };

        /// <summary>
        /// Returns 1 if there is at least one action associated with the action
        /// group.  0 otherwise, or if an invalid action group is specified.
        /// </summary>
        /// <param name="groupID">A number between 0 and 9 (inclusive) for stock action groups, 10 or larger for MAS action groups.</param>
        /// <returns>1 if there are actions for this action group, 0 otherwise.</returns>
        public double ActionGroupHasActions(double groupID)
        {
            if (groupID >= 10.0)
            {
                MASActionGroup ag;
                if (fc.masActionGroup.TryGetValue((int)groupID, out ag))
                {
                    return (ag.HasActions()) ? 1.0 : 0.0;
                }
                else
                {
                    return 0.0;
                }
            }
            else if (groupID < 0.0)
            {
                return 0.0;
            }
            else
            {
                return (vc.GroupHasActions(ags[(int)groupID])) ? 1.0 : 0.0;
            }
        }

        /// <summary>
        /// Returns the current memo from the action group selected by groupID.  If
        /// the memo was configured with active and inactive descriptions, this memo
        /// will change.  If an invalid groupID is provided, the result is an
        /// empty string.  If no memo was specified, the result is "AG0" for action
        /// group 0, "AG1" for action group 1, etc.
        /// </summary>
        /// <param name="groupID">A number between 0 and 9 (inclusive).  Note that MAS action groups do not have an AG Memo.</param>
        /// <returns>The memo for the requested group, or an empty string.</returns>
        public string ActionGroupActiveMemo(double groupID)
        {
            int ag = (int)groupID;
            if (ag < 0 || ag > 9)
            {
                return string.Empty;
            }
            else if (vessel.ActionGroups[ags[ag]])
            {
                return fc.agMemoOn[ag];
            }
            else
            {
                return fc.agMemoOff[ag];
            }
        }

        /// <summary>
        /// Returns the action group memo specified by the groupID, with active
        /// selecting whether the memo is for the active mode or the inactive mode.
        /// If the selected memo does not differentiate between active and inactive,
        /// the result is the same.  If an invalid groupID is provided, the result is an
        /// empty string.  If no memo was specified, the result is "AG0" for action
        /// group 0, "AG1" for action group 1, etc.
        /// </summary>
        /// <param name="groupID">A number between 0 and 9 (inclusive).  Note that MAS action groups do not have an AG Memo.</param>
        /// <param name="active">Whether the memo is for the active (true) or inactive (false) setting.</param>
        /// <returns>The memo for the requested group and state, or an empty string.</returns>
        public string ActionGroupMemo(double groupID, bool active)
        {
            if (groupID < 0.0 || groupID > 9.0)
            {
                return string.Empty;
            }
            else if (active)
            {
                return fc.agMemoOn[(int)groupID];
            }
            else
            {
                return fc.agMemoOff[(int)groupID];
            }
        }

        /// <summary>
        /// Get the current state of the specified action group.
        /// </summary>
        /// <param name="groupID">A number between 0 and 9 (inclusive) for stock action groups, 10 or larger for MAS action groups.</param>
        /// <returns>1 if active, 0 if inactive</returns>
        public double GetActionGroup(double groupID)
        {
            if (groupID >= 10.0)
            {
                MASActionGroup actionGroup;
                if (fc.masActionGroup.TryGetValue((int)groupID, out actionGroup))
                {
                    return (actionGroup.GetState()) ? 1.0 : 0.0;
                }
                else
                {
                    return 0.0;
                }
            }
            else if (groupID < 0.0)
            {
                return 0.0;
            }
            else
            {
                return (vessel.ActionGroups[ags[(int)groupID]]) ? 1.0 : 0.0;
            }
        }

        /// <summary>
        /// Set the specified action group to the requested state.
        /// </summary>
        /// <param name="groupID">A number between 0 and 9 (inclusive) for stock action groups, 10 or larger for MAS action groups.</param>
        /// <param name="active">true or false to set the state.</param>
        /// <returns>1 if the action group ID was valid, 0 otherwise.</returns>
        public double SetActionGroup(double groupID, bool active)
        {
            if (groupID >= 10.0)
            {
                MASActionGroup actionGroup;
                if (fc.masActionGroup.TryGetValue((int)groupID, out actionGroup))
                {
                    actionGroup.SetState(active);

                    return 1.0;
                }
            }
            else if (groupID >= 0.0)
            {
                vessel.ActionGroups.SetGroup(ags[(int)groupID], active);
                return 1.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Toggle the selected action group or MAS action group.
        /// </summary>
        /// <param name="groupID">A number between 0 and 9 (inclusive) for stock action groups, 10 or larger for MAS action groups.</param>
        /// <returns>1 if the action group ID was valid, 0 otherwise.</returns>
        public double ToggleActionGroup(double groupID)
        {
            if (groupID >= 10.0)
            {
                MASActionGroup actionGroup;
                if (fc.masActionGroup.TryGetValue((int)groupID, out actionGroup))
                {
                    actionGroup.Toggle();
                    return 1.0;
                }
            }
            else if (groupID >= 0.0)
            {
                vessel.ActionGroups.ToggleGroup(ags[(int)groupID]);
                return 1.0;
            }

            return 0.0;
        }
        #endregion

        /// <summary>
        /// Variables relating to the current vessel's altitude are found in this category.
        /// </summary>
        #region Altitudes
        /// <summary>
        /// Returns the vessel's altitude above the datum (sea level where
        /// applicable), in meters.
        /// </summary>
        /// <returns></returns>
        public double Altitude()
        {
            return vc.altitudeASL;
        }

        /// <summary>
        /// Returns altitude above datum (or sea level) for vessels in an
        /// atmosphere.  Returns 0 otherwise.  Altitude in meters.
        /// </summary>
        /// <returns></returns>
        public double AltitudeAtmospheric()
        {
            if (vc.mainBody.atmosphere)
            {
                return (vc.altitudeASL < vc.mainBody.atmosphereDepth) ? vc.altitudeASL : 0.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the distance from the lowest point of the craft to the
        /// surface of the planet.  Ocean is treated as surface for this
        /// purpose.  Precision reporting sets in at 500m (above 500m it
        /// reports the same as AltitudeTerrain(false)).  Distance in
        /// meters.
        /// </summary>
        /// <returns></returns>
        public double AltitudeBottom()
        {
            return vc.altitudeBottom;
        }

        /// <summary>
        /// Returns the height above the ground, optionally treating the ocean
        /// surface as ground.  Altitude in meters.
        /// </summary>
        /// <param name="ignoreOcean">When false, returns height above sea level
        /// when over the ocean; when true, always returns ground height.</param>
        /// <returns>Altitude above the terrain in meters.</returns>
        public double AltitudeTerrain(bool ignoreOcean)
        {
            return (ignoreOcean) ? vc.altitudeTerrain : Math.Min(vc.altitudeASL, vc.altitudeTerrain);
        }

        /// <summary>
        /// Returns the terrain height beneath the vessel relative to the planet's datum (sea
        /// level or equivalent).  Height in meters.
        /// </summary>
        /// <returns></returns>
        public double TerrainHeight()
        {
            return vessel.terrainAltitude;
        }
        #endregion

        /// <summary>
        /// Atmosphere and airflow variables are found in this category.
        /// </summary>
        #region Atmosphere
        /// <summary>
        /// Returns the atmospheric depth as reported by the KSP atmosphere
        /// gauge, a number ranging between 0 and 1.
        /// </summary>
        /// <returns></returns>
        public double AtmosphereDepth()
        {
            return vc.atmosphereDepth;
        }

        /// <summary>
        /// Returns the altitude of the top of atmosphere, or 0 if there is no
        /// atmo.  Altitude in meters.
        /// </summary>
        /// <returns></returns>
        public double AtmosphereTop()
        {
            return vc.mainBody.atmosphereDepth;
        }

        /// <summary>
        /// Returns the atmospheric density.
        /// </summary>
        /// <returns></returns>
        public double AtmosphericDensity()
        {
            return vessel.atmDensity;
        }

        /// <summary>
        /// Returns the drag force on the vessel.  If FAR is installed, this variable uses
        /// FAR's computation for drag.
        /// </summary>
        /// <returns>Drag in kN.</returns>
        public double Drag()
        {
            if (vc.mainBody.atmosphere == false || vc.altitudeASL > vc.mainBody.atmosphereDepth)
            {
                return 0.0;
            }

            if (MASIFAR.farFound)
            {
                return farProxy.DragForce();
            }
            else
            {
                return vc.DragForce();
            }
        }

        /// <summary>
        /// Returns the drag effect on the vessel as acceleration.  If FAR is installed, this variable uses
        /// FAR's computation for drag.
        /// </summary>
        /// <returns>Drag acceleration in m/s^2.</returns>
        public double DragAccel()
        {
            if (vc.mainBody.atmosphere == false || vc.altitudeASL > vc.mainBody.atmosphereDepth)
            {
                return 0.0;
            }

            if (MASIFAR.farFound)
            {
                return farProxy.DragForce() / vessel.totalMass;
            }
            else
            {
                return vc.DragForce() / vessel.totalMass;
            }
        }

        /// <summary>
        /// Returns the current dynamic pressure on the vessel in kPa.  If FAR
        /// is installed, this variable uses FAR's computation instead.
        /// </summary>
        /// <returns>Dynamic pressure in kPa.</returns>
        public double DynamicPressure()
        {
            if (MASIFAR.farFound)
            {
                return farProxy.DynamicPressure();
            }
            else
            {
                return vessel.dynamicPressurekPa;
            }
        }

        /// <summary>
        /// Returns the force of gravity affecting the vessel.
        /// </summary>
        /// <returns>Force of gravity in kN.</returns>
        public double GravityForce()
        {
            return vc.GravForce();
        }

        /// <summary>
        /// Returns 1 if the body the vessel is orbiting has an atmosphere.
        /// </summary>
        /// <returns>1 if there is an atmosphere, 0 otherwise.</returns>
        public double HasAtmosphere()
        {
            return (vc.mainBody.atmosphere) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the lift force on the vessel.  If FAR is installed, this variable uses
        /// FAR's computation for lift.
        /// </summary>
        /// <returns>Lift in kN.</returns>
        public double Lift()
        {
            if (vc.mainBody.atmosphere == false || vc.altitudeASL > vc.mainBody.atmosphereDepth)
            {
                return 0.0;
            }

            if (MASIFAR.farFound)
            {
                return farProxy.LiftForce();
            }
            else
            {
                return vc.LiftForce();
            }
        }

        /// <summary>
        /// Returns the force of lift opposed to gravity.  If FAR is installed, this variable uses
        /// FAR's computations for lift.
        /// </summary>
        /// <returns>Lift opposed to gravity in kN.</returns>
        public double LiftUpForce()
        {
            if (vc.mainBody.atmosphere == false || vc.altitudeASL > vc.mainBody.atmosphereDepth)
            {
                return 0.0;
            }

            if (MASIFAR.farFound)
            {
                return farProxy.LiftForce() * Vector3d.Dot(vc.up, vc.top);
            }
            else
            {
                return vc.LiftUpForce();
            }
        }

        /// <summary>
        /// Returns the static atmospheric pressure in standard atmospheres.
        /// </summary>
        /// <returns></returns>
        public double StaticPressureAtm()
        {
            return vessel.staticPressurekPa * PhysicsGlobals.KpaToAtmospheres;
        }

        /// <summary>
        /// Returns the static atmospheric pressure in kiloPascals.
        /// </summary>
        /// <returns>Static pressure in kPa.</returns>
        public double StaticPressureKPa()
        {
            return vessel.staticPressurekPa;
        }

        /// <summary>
        /// Returns the current terminal velocity of the vessel.  If the vessel is not in
        /// an atmosphere, returns 0.  If FAR is installed, returns FAR's terminal velocity
        /// result.
        /// </summary>
        /// <returns>Terminal velocity in m/s.</returns>
        public double TerminalVelocity()
        {
            if (vc.mainBody.atmosphere == false || vc.altitudeASL > vc.mainBody.atmosphereDepth)
            {
                return 0.0;
            }

            if (MASIFAR.farFound)
            {
                return farProxy.TerminalVelocity();
            }
            else
            {
                return vc.TerminalVelocity();
            }
        }

        #endregion

        /// <summary>
        /// The Autopilot region provides information about and control over the MAS Vessel
        /// Computer Control system (which needs a cool name amenable to acronyms).
        /// 
        /// The attitude control pilot is very similar to MechJeb's advanced SASS modes.
        /// 
        /// Some caveats about the autopilot subsystems:
        /// 
        /// The attitude control pilot uses the stock SAS feature to provide steering control.
        /// When it is engaged, SAS is in Stability Control mode.  If SAS is changed to a
        /// different mode (such as Prograde), the attitude control pilot is disengaged.
        /// Likewise, if Stability Control is selected, the attitude pilot disengages.
        /// 
        /// Other autopilots may use the attitude control system to steer the vessel.  If
        /// the attitude control pilot is disengaged, the other autopilot is also disengaged.
        /// </summary>
        #region Autopilot

        /// <summary>
        /// Reports if the attitude control pilot is actively attempting to control
        /// the vessel's heading.  This pilot could be active if the crew used
        /// `fc.SetHeading()` to set the vessel's heading, or if another pilot module
        /// is using the attitude pilot's service.
        /// </summary>
        /// <returns>1 if the attitude control pilot is active, 0 otherwise.</returns>
        public double GetAttitudePilotActive()
        {
            return (vc.attitudePilotEngaged) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the currently stored heading offset in the atittude control pilot.
        /// </summary>
        /// <returns>Heading relative to the reference attitude, in degrees.</returns>
        public double GetAttitudePilotHeading()
        {
            return vc.relativeHPR.x;
        }

        /// <summary>
        /// Returns the currently stored pitch offset in the atittude control pilot.
        /// </summary>
        /// <returns>Pitch relative to the reference attitude, in degrees.</returns>
        public double GetAttitudePilotPitch()
        {
            return vc.relativeHPR.y;
        }

        /// <summary>
        /// Returns the currently stored roll offset in the atittude control pilot.
        /// </summary>
        /// <returns>Roll relative to the reference attitude, in degrees.</returns>
        public double GetAttitudePilotRoll()
        {
            return vc.relativeHPR.z;
        }

        /// <summary>
        /// Returns the current attitude reference mode.  This value may be one of
        /// the following:
        /// 
        /// * 0 - Inertial Frame
        /// * 1 - Orbital Prograde
        /// * 2 - Orbital Prograde Horizontal
        /// * 3 - Surface Prograde
        /// * 4 - Surface Prograde Horizontal
        /// * 5 - Surface North
        /// * 6 - Target
        /// * 7 - Target Prograde
        /// * 8 - Target Orientation
        /// * 9 - Maneuver Node
        /// * 10 - Sun
        ///
        /// This reference mode does not indicate whether the attitude control pilot is
        /// active, but it does indicate which reference attitude will take effect if the
        /// pilot is engaged.  Refer to the documentation for `fc.SetHeading()` for a
        /// detailed explanation of the attitude references.
        /// </summary>
        /// <returns>One of the numbers listed in the summary.</returns>
        public double GetAttitudeReference()
        {
            return (int)vc.activeReference;
        }

        /// <summary>
        /// Returns 1 if the maneuver autopilot is active, 0 if it is idle.
        /// </summary>
        /// <returns></returns>
        public double GetManeuverPilotActive()
        {
            return (vc.maneuverPilotEngaged) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if any MAS autopilot is active, 0 if all are idle.
        /// </summary>
        /// <returns></returns>
        public double GetPilotActive()
        {
            return (vc.PilotActive()) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Set the attitude pilot to the selected state.  If another pilot is using
        /// the attitude pilot (such as the launch pilot), switching off the attitude
        /// pilot will disengage the other pilot as well.
        /// 
        /// **CAUTION:** If the attitude system has not been initialized, it selects an inertial reference
        /// attitude, which will cause problems during launch or reentry.
        /// </summary>
        /// <param name="active">If true, engage the autopilot and restore the previous attitude.</param>
        /// <returns>Returns 1 if the autopilot is now on, 0 if it is now off.</returns>
        public double SetAttitudePilotActive(bool active)
        {
            if (active != vc.attitudePilotEngaged)
            {
                if (!active)
                {
                    // Shutoff is easy.
                    vc.CancelAutopilots();
                }
                else
                {
                    // Engaging takes a couple of extra steps
                    vc.EngageAttitudePilot(vc.activeReference, vc.relativeHPR);
                }
            }

            return (vc.attitudePilotEngaged) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Engages SAS and sets the vessel's heading based on the reference attitude, heading, pitch, and roll.
        /// The reference attitude is one of the following:
        /// 
        /// * 0 - Inertial Frame - the universe's inertial frame of reference, relative to no bodies or vessels.
        /// * 1 - Orbital Prograde - The orbital prograde direction with Radial Out up.
        /// * 2 - Orbital Prograde Horizontal - The orbital prograde direction with a surface-relative up.
        /// * 3 - Surface Prograde - The surface prograde direction with Radial Out up.
        /// * 4 - Surface Prograde Horizontal - The surface prograde direction with a surface-relative up.
        /// * 5 - Surface North - Local planetary north with a surface-relative up.
        /// * 6 - Target - Pointed towards the target with an up direction based on Radial Out.
        /// * 7 - Target Relative Prograde - Target-relative prograde with an up direction based on Radial Out.
        /// * 8 - Target Orientation - target's "forward" and "up" directions (for celestial bodies, this
        /// is an arbitrary direction).
        /// * 9 - Maneuver Node - towards the maneuver node, with up based on Radial Out.
        /// * 10 - Sun - towards the Sun, with an inertial reference frame "up".
        /// </summary>
        /// <param name="reference">Reference attitude, as described in the summary.</param>
        /// <param name="heading">Heading (yaw) relative to the reference attitude.</param>
        /// <param name="pitch">Pitch relative to the reference attitude.</param>
        /// <param name="roll">Roll relative to the reference attitude.</param>
        /// <returns>1 if the SetHeading command succeeded, 0 otherwise.</returns>
        public double SetHeading(double reference, double heading, double pitch, double roll)
        {
            int refAtt = (int)reference;
            if (refAtt < 0 || refAtt > 10)
            {
                return 0.0;
            }

            return (vc.EngageAttitudePilot(referenceAttitudes[refAtt], new Vector3((float)heading, (float)pitch, (float)roll))) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Sets the maneuver autopilot state to active or not based on 'active'.
        /// If no valid maneuver node exists, activating the maneuver pilot has no effect.
        /// </summary>
        /// <param name="active">If true, attempts to activate the maneuver autopilot; if false, deactivates it.</param>
        /// <returns>1 if the maneuver autopilot is active, 0 if it is not active.</returns>
        public double SetManeuverPilotActive(bool active)
        {
            if (active != vc.maneuverPilotEngaged)
            {
                if (!active)
                {
                    // Shutoff is easy.
                    vc.CancelAutopilots();
                }
                else
                {
                    // Engaging takes a couple of extra steps
                    vc.EngageManeuverPilot();
                }
            }

            return (vc.maneuverPilotEngaged) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Toggle the MAS attitude pilot.  The exisiting reference attitude and heading, pitch, and roll
        /// are restored.  If another pilot is using
        /// the attitude pilot (such as the launch pilot), switching off the attitude
        /// pilot will disengage the other pilot as well.
        /// 
        /// **CAUTION:** If the attitude system has not been initialized, it defaults to an inertial reference
        /// attitude, which will cause problems during launch or reentry.
        /// </summary>
        /// <returns>Returns 1 if the autopilot is now on, 0 if it is now off.</returns>
        public double ToggleAttitudePilot()
        {
            if (vc.attitudePilotEngaged)
            {
                vc.CancelAutopilots();
            }
            else
            {
                vc.EngageAttitudePilot(vc.activeReference, vc.relativeHPR);
            }

            return (vc.attitudePilotEngaged) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Toggles the maneuver autopilot.
        /// </summary>
        /// <returns>1 if the maneuver pilot is now active, 0 if it is now inactive.</returns>
        public double ToggleManeuverPilot()
        {
            if (vc.maneuverPilotEngaged)
            {
                vc.CancelAutopilots();
            }
            else
            {
                vc.EngageManeuverPilot();
            }

            return (vc.maneuverPilotEngaged) ? 1.0 : 0.0;
        }
        #endregion

        /// <summary>
        /// Information about the Celestial Bodies may be fetched using the
        /// methods in this category.
        /// 
        /// Most of these methods function in one of two ways: by name or
        /// by number.  By name means using the name of the body to select
        /// it (eg, `fc.BodyMass("Jool")`).  However, since strings are
        /// slightly slower than numbers for lookups, these methods will
        /// accept numbers.  The number for a world can be fetched using
        /// `fc.BodyIndex()`, `fc.CurrentBodyIndex()`, or `fc.TargetBodyIndex()`.
        /// </summary>
        #region Body
        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the surface area of the selected body.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>Surface area of the planet in m^2.</returns>
        public double BodyArea(object id)
        {
            CelestialBody cb = SelectBody(id);

            return (cb == null) ? 0.0 : cb.SurfaceArea;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the altitude of the top of the selected body's
        /// atmosphere.  If the body does not have an atmosphere, or
        /// an invalid body is selected, 0 is returned.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>The altitude of the top of the atmosphere, in meters, or 0.</returns>
        public double BodyAtmosphereTop(object id)
        {
            CelestialBody cb = SelectBody(id);

            double atmoTop = 0.0;
            if (cb != null && cb.atmosphere)
            {
                atmoTop = cb.atmosphereDepth;
            }

            return atmoTop;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the name of the biome at the given location on the selected body.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <param name="latitude">Latitude of the location of interest.</param>
        /// <param name="longitude">Longitude of the location of interest.</param>
        /// <returns>The name of the biome at the specified location, or an empty string.</returns>
        public string BodyBiome(object id, double latitude, double longitude)
        {
            CelestialBody cb = SelectBody(id);
            if (cb != null)
            {
                string biome = ScienceUtil.GetExperimentBiome(cb, latitude, longitude);
                //ScienceUtil.GetBiomedisplayName(cb, biome);
                // string biome = ScienceUtil.GetExperimentBiomeLocalized(cb, latitude, longitude);
                if (ScienceUtil.BiomeIsUnlisted(cb, biome))
                {
                    // What causes this?  And what action should I take?
                    Utility.LogWarning(this, "BodyBiome(): biome '{0}' is unlisted", biome);
                }
                return biome;
            }

            return string.Empty;
        }

        [MASProxy(Immutable = true)]
        /// <summary>
        /// The number of Celestial Bodies in the database.
        /// </summary>
        /// <returns></returns>
        public double BodyCount()
        {
            return FlightGlobals.Bodies.Count;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the escape velocity of the body.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>Escape velocity in m/s, or 0 if the body was invalid.</returns>
        public double BodyEscapeVelocity(object id)
        {
            CelestialBody cb = SelectBody(id);

            double escapeVelocity = 0.0;
            if (cb != null)
            {
                escapeVelocity = Math.Sqrt(2.0 * cb.gravParameter / cb.Radius);
            }

            return escapeVelocity;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the acceleration from gravity as the surface
        /// for the selected body.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>Acceleration in G's, or 0 if the body was invalid.</returns>
        public double BodyGeeASL(object id)
        {
            CelestialBody cb = SelectBody(id);

            return (cb != null) ? cb.GeeASL : 0.0;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the standard gravitational parameter of the body.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>GM in m^3/s^2, or 0 if the body is invalid.</returns>
        public double BodyGM(object id)
        {
            CelestialBody cb = SelectBody(id);

            return (cb != null) ? cb.gravParameter : 0.0;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Indicates if the selected body has an atmosphere.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>1 if the body has an atmosphere, 0 if it does not, or an invalid body was selected.</returns>
        public double BodyHasAtmosphere(object id)
        {
            CelestialBody cb = SelectBody(id);
            bool atmo = (cb != null) ? cb.atmosphere : false;

            return (atmo) ? 1.0 : 0.0;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Indicates if the selected body's atmosphere has oxygen.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>1 if the body has an atmosphere that contains oxygen, 0 if it does not, or an invalid body was selected.</returns>
        public double BodyHasOxygen(object id)
        {
            CelestialBody cb = SelectBody(id);
            bool atmo = (cb != null) ? cb.atmosphere : false;
            bool oxy = atmo && ((cb != null) ? cb.atmosphereContainsOxygen : false);

            return (oxy) ? 1.0 : 0.0;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the numeric identifier for the named body.  If the name is invalid
        /// (no such body exists), returns -1.  May also use the index, which is useful
        /// for -1 and -2.
        /// </summary>
        /// <param name="id">The name of the body, eg. `"Kerbin"` or one of the indices (including -1 and -2).</param>
        /// <returns>An index from 0 to (number of Celestial Bodies - 1), or -1 if the named body was not found.</returns>
        public double BodyIndex(object id)
        {
            string bodyName = string.Empty;
            if (id is double)
            {
                CelestialBody cb = SelectBody(id);
                if (cb != null)
                {
                    bodyName = cb.bodyName;
                }
            }
            else if (id is string)
            {
                bodyName = id as string;
            }

            return (double)FlightGlobals.Bodies.FindIndex(x => x.bodyName == bodyName);
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns 1 if the selected body is "Home" (Kerbin in un-modded KSP).
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>1 if the body is home, 0 otherwise.</returns>
        public double BodyIsHome(object id)
        {
            CelestialBody cb = SelectBody(id);
            if (cb != null && cb.GetName() == Planetarium.fetch.Home.GetName())
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns 0 if the selected body orbits the star; returns 1 if the
        /// body is a moon of another body.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>1 if the body is a moon, 0 if it is a planet.</returns>
        public double BodyIsMoon(object id)
        {
            CelestialBody cb = SelectBody(id);
            if (cb != null && cb.referenceBody != null && cb.referenceBody.GetName() != Planetarium.fetch.Sun.GetName())
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the mass of the requested body.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>Mass in kg.</returns>
        public double BodyMass(object id)
        {
            CelestialBody cb = SelectBody(id);

            return (cb != null) ? cb.Mass : 0.0;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the number of worlds orbiting the selected body.  If the body
        /// is a planet, this is the number of moons.  If the body is the Sun, this
        /// number is the number of planets.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>The number of moons, or 0 if an invalid value was provided.</returns>
        public double BodyMoonCount(object id)
        {
            CelestialBody cb = SelectBody(id);
            if (cb != null && cb.orbitingBodies != null)
            {
                return cb.orbitingBodies.Count;
            }
            else
            {
                return 0.0;
            }
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the numeric ID of the moon selected by moonIndex that orbits the body
        /// selected by 'id'.  If 'id' is the Sun, moonIndex selects the planet.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <param name="moonIndex">The index of the moon to select, between 0 and 'fc.BodyMoonCount(id)' - 1.</param>
        /// <returns>Returns an index 0 or greater, or -1 for an invalid combination of 'id' and 'moonIndex'.</returns>
        public double BodyMoonId(object id, double moonIndex)
        {
            int moonIdx = (int)moonIndex;
            CelestialBody cb = SelectBody(id);
            if (cb != null && cb.orbitingBodies != null && moonIdx >= 0 && moonIdx < cb.orbitingBodies.Count)
            {
                return (double)FlightGlobals.Bodies.FindIndex(x => x.bodyName == cb.orbitingBodies[moonIdx].bodyName);
            }
            else
            {
                return -1.0;
            }
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the name of the requested body.  While this method can be used
        /// with a name for its parameter, that is kind of pointless.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>The name of the body, or an empty string if invalid.</returns>
        public string BodyName(object id)
        {
            CelestialBody cb = SelectBody(id);
            return (cb != null) ? cb.bodyName : string.Empty;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the radius of the selected body.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>Radius in meters, or 0 if the body is invalid.</returns>
        public double BodyRadius(object id)
        {
            CelestialBody cb = SelectBody(id);

            return (cb != null) ? cb.Radius : 0.0;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the index of the parent of the selected body.  Returns 0 (the Sun)
        /// on an invalid id.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>Returns the index of the body that the current body orbits.</returns>
        public double BodyParent(object id)
        {
            CelestialBody cb = SelectBody(id);

            if (cb != null && cb.referenceBody != null)
            {
                string bodyName = cb.referenceBody.bodyName;

                return (double)FlightGlobals.Bodies.FindIndex(x => x.bodyName == bodyName);
            }
            else
            {
                return 0.0;
            }
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the rotation period of the body.  If the body does not
        /// rotate, 0 is returned.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>Rotation period in seconds, or 0.</returns>
        public double BodyRotationPeriod(object id)
        {
            CelestialBody cb = SelectBody(id);

            return (cb != null && cb.rotates) ? cb.rotationPeriod : 0.0;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the radius of the body's Sphere of Influence.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>SoI in meters</returns>
        public double BodySoI(object id)
        {
            CelestialBody cb = SelectBody(id);

            return (cb != null) ? cb.sphereOfInfluence : 0.0;
        }

        /// <summary>
        /// Returns the longitude on the body that is directly below the sun (longitude of local noon).
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>Longitude of local noon, or 0 if it could not be determined.</returns>
        public double BodySunLongitude(object id)
        {
            CelestialBody cb = SelectBody(id);

            if (cb != null)
            {
                CelestialBody sun = Planetarium.fetch.Sun;

                Vector3d sunDirection = sun.position - cb.position;

                if (sunDirection.sqrMagnitude > 0.0)
                {
                    sunDirection.Normalize();

                    return Utility.NormalizeLongitude(cb.GetLongitude(cb.position + sunDirection * cb.Radius));
                }
            }

            return 0.0;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the temperature of the body at sea level.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <param name="useKelvin">If true, temperature is in Kelvin; if false, temperature is in Celsius.</param>
        /// <returns>Surface temperature in K or C; 0 if the selected object was invalid</returns>
        public double BodySurfaceTemp(object id, bool useKelvin)
        {
            CelestialBody cb = SelectBody(id);

            double temperature = 0.0;

            if (cb != null)
            {
                temperature = cb.atmosphereTemperatureSeaLevel;
                if (!useKelvin)
                {
                    temperature += KelvinToCelsius;
                }
            }

            return temperature;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the semi-major axis of a synchronous orbit with the selected body.
        /// When a vessel's SMA matches the sync orbit SMA, a craft is in a synchronous
        /// orbit.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>SMA in meters, or 0 if the body is invalid, or the synchronous orbit
        /// is out side the body's SoI.</returns>
        public double BodySyncOrbitSMA(object id)
        {
            CelestialBody cb = SelectBody(id);

            double syncOrbit = 0.0;

            if (cb != null)
            {
                syncOrbit = Math.Pow(cb.gravParameter * Math.Pow(cb.rotationPeriod / (2.0 * Math.PI), 2.0), 1.0 / 3.0);

                if (syncOrbit > cb.sphereOfInfluence)
                {
                    syncOrbit = 0.0;
                }
            }

            return syncOrbit;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the speed of a synchronous orbit.  Provided an orbit is
        /// perfectly circular, an orbit that has this velocity will be
        /// synchronous.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>Velocity in m/s, or 0 if there is no synchronous orbit.</returns>
        public double BodySyncOrbitVelocity(object id)
        {
            CelestialBody cb = SelectBody(id);

            double syncOrbitPeriod = 0.0;

            if (cb != null && cb.rotates)
            {
                double syncOrbit = Math.Pow(cb.gravParameter * Math.Pow(cb.rotationPeriod / (2.0 * Math.PI), 2.0), 1.0 / 3.0);

                if (syncOrbit > cb.sphereOfInfluence)
                {
                    syncOrbit = 0.0;
                }

                // Determine the circumference of the orbit.
                syncOrbit *= 2.0 * Math.PI;

                syncOrbitPeriod = syncOrbit / cb.rotationPeriod;
            }

            return syncOrbitPeriod;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the terrain height at a given latitude and longitude relative to the
        /// planet's datum (sea level or equivalent).  Height in meters.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <param name="latitude">Latitude of the location of interest in degrees.</param>
        /// <param name="longitude">Longitude of the location of interest in degrees.</param>
        /// <returns>Terrain height in meters.  Will return negative values for locations below sea level.</returns>
        public double BodyTerrainHeight(object id, double latitude, double longitude)
        {
            CelestialBody cb = SelectBody(id);
            if (cb != null)
            {
                if (cb.pqsController != null)
                {
                    return cb.pqsController.GetSurfaceHeight(QuaternionD.AngleAxis(longitude, Vector3d.down) * QuaternionD.AngleAxis(latitude, Vector3d.forward) * Vector3d.right) - cb.Radius;
                }
                else
                {
                    return cb.TerrainAltitude(latitude, longitude, true);
                }
            }
            else
            {
                return 0.0;
            }
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns an estimate of the terrain slope at a given latitude and longitude.
        /// If the location is beneath the ocean, it provides the slope of the ocean floor.
        /// Values near the poles may be very inaccurate.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <param name="latitude">Latitude of the location of interest in degrees.</param>
        /// <param name="longitude">Longitude of the location of interest in degrees.</param>
        /// <returns>Slope in degrees.</returns>
        public double BodyTerrainSlope(object id, double latitude, double longitude)
        {
            CelestialBody cb = SelectBody(id);
            if (cb != null)
            {
                double displacementInMeters = 5.0;

                // We compute a simple normal for the point of interest by sampling
                // altitudes approximately 5m meters away from the location in the four
                // cardinal directions and computing the cross product of the two vectors
                // we generate.

                double displacementInDegreesLatitude = 360.0 * displacementInMeters / (2.0 * Math.PI * cb.Radius);
                // Clamp latitude
                latitude = Math.Max(-90.0 + (displacementInDegreesLatitude * 1.5), Math.Min(90.0 - (displacementInDegreesLatitude * 1.5), latitude));
                // Account for longitudinal compression.
                double displacementInDegreesLongitude = displacementInDegreesLatitude / Math.Cos(latitude * Mathf.Deg2Rad);

                PQS pqs = cb.pqsController;
                if (pqs != null)
                {
                    double westAltitude = pqs.GetSurfaceHeight(QuaternionD.AngleAxis(longitude - displacementInDegreesLongitude, Vector3d.down) * QuaternionD.AngleAxis(latitude, Vector3d.forward) * Vector3d.right);
                    double eastAltitude = pqs.GetSurfaceHeight(QuaternionD.AngleAxis(longitude + displacementInDegreesLongitude, Vector3d.down) * QuaternionD.AngleAxis(latitude, Vector3d.forward) * Vector3d.right);
                    Vector3d westEastSlope = new Vector3d(displacementInMeters * 2.0, 0.0, eastAltitude - westAltitude);
                    westEastSlope.Normalize();

                    double southAltitude = pqs.GetSurfaceHeight(QuaternionD.AngleAxis(longitude, Vector3d.down) * QuaternionD.AngleAxis(latitude - displacementInDegreesLatitude, Vector3d.forward) * Vector3d.right);
                    double northAltitude = pqs.GetSurfaceHeight(QuaternionD.AngleAxis(longitude, Vector3d.down) * QuaternionD.AngleAxis(latitude + displacementInDegreesLatitude, Vector3d.forward) * Vector3d.right);
                    Vector3d southNorthSlope = new Vector3d(0.0, displacementInMeters * 2.0, northAltitude - southAltitude);
                    southNorthSlope.Normalize();

                    Vector3d normal = Vector3d.Cross(westEastSlope, southNorthSlope);

                    return Vector3d.Angle(normal, Vector3d.forward);
                }
                else
                {
                    // No PQS controller?  Have to use TerrainAltitude(), which seems to report bogus values.
                    double westAltitude = cb.TerrainAltitude(longitude - displacementInDegreesLongitude, latitude, true);
                    double eastAltitude = cb.TerrainAltitude(longitude + displacementInDegreesLongitude, latitude, true);
                    Vector3d westEastSlope = new Vector3d(displacementInMeters * 2.0, 0.0, eastAltitude - westAltitude);
                    westEastSlope.Normalize();

                    double southAltitude = cb.TerrainAltitude(longitude, latitude - displacementInDegreesLatitude, true);
                    double northAltitude = cb.TerrainAltitude(longitude, latitude + displacementInDegreesLatitude, true);
                    Vector3d southNorthSlope = new Vector3d(0.0, displacementInMeters * 2.0, northAltitude - southAltitude);
                    southNorthSlope.Normalize();

                    Vector3d normal = Vector3d.Cross(westEastSlope, southNorthSlope);

                    return Vector3d.Angle(normal, Vector3d.forward);
                }
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the index of the body currently being orbited, for use as an input for other body query functions..
        /// </summary>
        /// <returns>The index of the current body, used as the 'id' parameter in other body query functions.</returns>
        public double CurrentBodyIndex()
        {
            string bodyName = vc.mainBody.bodyName;
            return (double)FlightGlobals.Bodies.FindIndex(x => x.bodyName == bodyName);
        }

        /// <summary>
        /// Set the vessel's target to the selected body.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>1 if the command succeeds, 0 if an invalid body name or index was provided.</returns>
        public double SetBodyTarget(object id)
        {
            CelestialBody cb = SelectBody(id);
            if (cb != null)
            {
                FlightGlobals.fetch.SetVesselTarget(cb);

                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the body index of the current target, provided the target is a celestial body.
        /// If there is no target, or the current target is not a body, returns -1.
        /// </summary>
        /// <returns>The index of the targeted body, or -1.</returns>
        public double BodyTargetIndex()
        {
            if (vc.targetType == MASVesselComputer.TargetType.CelestialBody)
            {
                string bodyName = (vc.activeTarget as CelestialBody).bodyName;
                return (double)FlightGlobals.Bodies.FindIndex(x => x.bodyName == bodyName);
            }
            return -1.0;
        }

        #endregion

        /// <summary>
        /// Variables related to a vessel's brakes are in this category.
        /// </summary>
        #region Brakes
        /// <summary>
        /// Returns 1 if the brakes action group has at least one action assigned to it.
        /// </summary>
        /// <returns></returns>
        public double BrakesHasActions()
        {
            return (vc.GroupHasActions(KSPActionGroup.Brakes)) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the average brake force setting of all wheel brakes installed on the vessel.
        /// </summary>
        /// <returns>The brake force as a percentage of maximum, in the range of [0, 2].</returns>
        public double GetBrakeForce()
        {
            int numBrakes = vc.moduleBrakes.Length;
            if (numBrakes > 0)
            {
                float netBrakeForce = 0.0f;
                for (int i = 0; i < numBrakes; ++i)
                {
                    netBrakeForce += vc.moduleBrakes[i].brakeTweakable;
                }
                return netBrakeForce / (float)(100 * numBrakes);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the current state of the Brakes action group
        /// </summary>
        /// <returns>1 if the brake action group is active, 0 otherwise.</returns>
        public double GetBrakes()
        {
            return (vessel.ActionGroups[KSPActionGroup.Brakes]) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Sets the brake force setting of all wheel brakes installed on the vessel.
        /// </summary>
        /// <param name="force">The new brake force setting, in the range of 0 to 2.</param>
        /// <returns>The brake force as a percentage of maximum, in the range of [0, 2].</returns>
        public double SetBrakeForce(double force)
        {
            int numBrakes = vc.moduleBrakes.Length;
            if (numBrakes > 0)
            {
                float clampedForce = Mathf.Clamp((float)force, 0.0f, 2.0f);
                float newForce = clampedForce * 100.0f;

                for (int i = 0; i < numBrakes; ++i)
                {
                    vc.moduleBrakes[i].brakeTweakable = newForce;
                }

                return clampedForce;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Set the brake action group to the specified state.
        /// </summary>
        /// <param name="active">Sets the state of the brakes</param>
        /// <returns>1 if the brake action group is active, 0 otherwise.</returns>
        public double SetBrakes(bool active)
        {
            vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, active);
            return (active) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Toggle the state of the brake action group.
        /// </summary>
        /// <returns>1 if the brake action group is active, 0 otherwise.</returns>
        public double ToggleBrakes()
        {
            vessel.ActionGroups.ToggleGroup(KSPActionGroup.Brakes);
            return (vessel.ActionGroups[KSPActionGroup.Brakes]) ? 1.0 : 0.0;
        }
        #endregion

        /// <summary>
        /// The methods in this section are focused around controlling external
        /// cameras installed on the vessel.  They provide an interface between
        /// the MASCamera part module and CAMERA nodes in a monitor page.
        /// </summary>
        #region Cameras

        /// <summary>
        /// Returns the name of the camera (if any) attached to the current reference docking port.
        /// If the reference transform is not a docking port, or there is no camera on the reference
        /// docking port, an empty string is returned.
        /// </summary>
        /// <returns>The name of the MASCamera on the reference docking port, or an empty string.</returns>
        public string ActiveDockingPortCamera()
        {
            if (vc.dockingNode != null && vc.dockingNode.part == vessel.GetReferenceTransformPart())
            {
                MASCamera cam = vc.dockingNode.part.FindModuleImplementing<MASCamera>();
                if (cam != null)
                {
                    return cam.cameraName;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Adjusts the field of view setting on the selected camera.
        /// </summary>
        /// <returns>The new field of view setting, or 0 if an invalid index was supplied.</returns>
        public double AddFoV(double index, double deltaFoV)
        {
            double pan = 0.0;

            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                pan = vc.moduleCamera[i].AddFoV((float)deltaFoV);
            }

            return pan;
        }

        /// <summary>
        /// Adjusts the pan setting on the selected camera.
        /// </summary>
        /// <returns>The new pan setting, or 0 if an invalid index was supplied.</returns>
        public double AddPan(double index, double deltaPan)
        {
            double pan = 0.0;

            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                pan = vc.moduleCamera[i].AddPan((float)deltaPan);
            }

            return pan;
        }

        /// <summary>
        /// Adjusts the tilt setting on the selected camera.
        /// </summary>
        /// <returns>The new tilt setting, or 0 if an invalid index was supplied.</returns>
        public double AddTilt(double index, double deltaTilt)
        {
            double tilt = 0.0;

            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                tilt = vc.moduleCamera[i].AddTilt((float)deltaTilt);
            }

            return tilt;
        }

        /// <summary>
        /// Returns a count of the valid MASCamera modules found on this vessel.
        /// </summary>
        /// <returns>The number of valid MASCamera modules installed on this vessel.</returns>
        public double CameraCount()
        {
            return vc.moduleCamera.Length;
        }

        /// <summary>
        /// Returns 1 if the selected camera is damaged, 0 otherwise.  Deployable cameras may be damaged.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.CameraCount()` - 1.</param>
        /// <returns></returns>
        public double GetCameraDamaged(double index)
        {
            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                return vc.moduleCamera[i].IsDamaged() ? 1.0 : 0.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns 1 if the selected camera is deployable, 0 otherwise.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.CameraCount()` - 1.</param>
        /// <returns></returns>
        public double GetCameraDeployable(double index)
        {
            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                return vc.moduleCamera[i].GetDeployable() ? 1.0 : 0.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns 1 if the selected camera is deployed, 0 otherwise.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.CameraCount()` - 1.</param>
        /// <returns></returns>
        public double GetCameraDeployed(double index)
        {
            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                return vc.moduleCamera[i].IsDeployed() ? 1.0 : 0.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the maximum field of view supported by the selected camera.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.CameraCount()` - 1.</param>
        /// <returns>The maximum field of view in degrees, or 0 for an invalid camera index.</returns>
        public double GetCameraMaxFoV(double index)
        {
            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                return vc.moduleCamera[i].fovRange.y;
            }
            return 0.0;
        }

        /// <summary>
        /// Returns the maximum pan angle supported by the selected camera.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.CameraCount()` - 1.</param>
        /// <returns>The maximum pan in degrees, or 0 for an invalid camera index.</returns>
        public double GetCameraMaxPan(double index)
        {
            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                return vc.moduleCamera[i].GetPanRange().y;
            }
            return 0.0;
        }

        /// <summary>
        /// Returns the maximum tilt angle supported by the selected camera.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.CameraCount()` - 1.</param>
        /// <returns>The maximum tilt in degrees, or 0 for an invalid camera index.</returns>
        public double GetCameraMaxTilt(double index)
        {
            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                return vc.moduleCamera[i].GetTiltRange().y;
            }
            return 0.0;
        }

        /// <summary>
        /// Returns the minimum field of view supported by the selected camera.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.CameraCount()` - 1.</param>
        /// <returns>The minimum field of view in degrees, or 0 for an invalid camera index.</returns>
        public double GetCameraMinFoV(double index)
        {
            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                return vc.moduleCamera[i].fovRange.x;
            }
            return 0.0;
        }

        /// <summary>
        /// Returns the minimum pan angle supported by the selected camera.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.CameraCount()` - 1.</param>
        /// <returns>The minimum pan in degrees, or 0 for an invalid camera index.</returns>
        public double GetCameraMinPan(double index)
        {
            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                return vc.moduleCamera[i].GetPanRange().x;
            }
            return 0.0;
        }

        /// <summary>
        /// Returns the minimum tilt angle supported by the selected camera.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.CameraCount()` - 1.</param>
        /// <returns>The minimum tilt in degrees, or 0 for an invalid camera index.</returns>
        public double GetCameraMinTilt(double index)
        {
            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                return vc.moduleCamera[i].GetTiltRange().x;
            }
            return 0.0;
        }

        /// <summary>
        /// Returns the id number of the currently-active mode on the MASCamera selected by `index`.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.CameraCount()` - 1.</param>
        /// <returns>The number of the modes (between 0 and fc.GetCameraModeCount(index)-1), or 0 if an invalid camera was selected.</returns>
        public double GetCameraMode(double index)
        {
            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                return vc.moduleCamera[i].GetMode();
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the number of modes available to the MASCamera selected by `index`.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.CameraCount()` - 1.</param>
        /// <returns>The number of modes (1 or higher), or 0 if an invalid camera was selected.</returns>
        public double GetCameraModeCount(double index)
        {
            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                return vc.moduleCamera[i].GetModeCount();
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the id number of the currently-active mode on the MASCamera selected by `index`.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.CameraCount()` - 1.</param>
        /// <param name="mode">A number between 0 and `fc.GetCameraModeCount(index)` - 1.</param>
        /// <returns>The name of the selected mode, or an empty string if an invalid camera or mode was selected.</returns>
        public string GetCameraModeName(double index, double mode)
        {
            int i = (int)index;
            int whichMode = (int)mode;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                return vc.moduleCamera[i].GetModeName(whichMode);
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns 1 if the selected camera is moving, 0 otherwise.  Only deployable cameras move.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.CameraCount()` - 1.</param>
        /// <returns></returns>
        public double GetCameraMoving(double index)
        {
            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                return vc.moduleCamera[i].IsMoving() ? 1.0 : 0.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the name of the camera selected by `index`, or an empty string
        /// if the index is invalid.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.CameraCount()` - 1.</param>
        /// <returns>The name of the camera, or an empty string.</returns>
        public string GetCameraName(double index)
        {
            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                return vc.moduleCamera[i].cameraName;
            }
            return string.Empty;
        }

        /// <summary>
        /// Retrieve the field of view setting on the selected camera.
        /// </summary>
        /// <returns>The current field of view setting, or 0 if an invalid index was supplied.</returns>
        public double GetFoV(double index)
        {
            double fov = 0.0;

            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                fov = vc.moduleCamera[i].currentFov;
            }

            return fov;
        }

        /// <summary>
        /// Retrieve the pan setting on the selected camera.
        /// </summary>
        /// <returns>The current pan setting, or 0 if an invalid index was supplied.</returns>
        public double GetPan(double index)
        {
            double pan = 0.0;

            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                pan = vc.moduleCamera[i].GetPan();
            }

            return pan;
        }

        /// <summary>
        /// Retrieve the tilt setting on the selected camera.
        /// </summary>
        /// <returns>The current tilt setting, or 0 if an invalid index was supplied.</returns>
        public double GetTilt(double index)
        {
            double tilt = 0.0;

            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                tilt = vc.moduleCamera[i].GetTilt();
            }

            return tilt;
        }

        /// <summary>
        /// Extends or retracts a deployable camera.  Has
        /// no effect on non-deployable cameras.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.CameraCount()` - 1.</param>
        /// <param name="deploy">true to deploy the camera, false to retract it.</param>
        /// <returns>1 if the camera deploys / undeploys, 0 otherwise.</returns>
        public double SetCameraDeployment(double index, bool deploy)
        {
            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length && vc.moduleCamera[i].IsDeployed() != deploy)
            {
                return vc.moduleCamera[i].ToggleDeployment() ? 1.0 : 0.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the id number of the currently-active mode on the MASCamera selected by `index`.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.CameraCount()` - 1.</param>
        /// <param name="mode">A number between 0 and `fc.GetCameraModeCount(index)` - 1.</param>
        /// <returns>The mode that was selected, or 0 if an invalid camera was selected.</returns>
        public double SetCameraMode(double index, double mode)
        {
            int i = (int)index;
            int newMode = (int)mode;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                return vc.moduleCamera[i].SetMode(newMode);
            }

            return 0.0;
        }

        /// <summary>
        /// Adjusts the field of view setting on the selected camera.
        /// </summary>
        /// <returns>The new field of view setting, or 0 if an invalid index was supplied.</returns>
        public double SetFoV(double index, double newFoV)
        {
            double pan = 0.0;

            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                pan = vc.moduleCamera[i].SetFoV((float)newFoV);
            }

            return pan;
        }

        /// <summary>
        /// Adjusts the pan setting on the selected camera.
        /// </summary>
        /// <returns>The new pan setting, or 0 if an invalid index was supplied.</returns>
        public double SetPan(double index, double setPan)
        {
            double pan = 0.0;

            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                pan = vc.moduleCamera[i].SetPan((float)setPan);
            }

            return pan;
        }

        /// <summary>
        /// Adjusts the tilt setting on the selected camera.
        /// </summary>
        /// <returns>The new tilt setting, or 0 if an invalid index was supplied.</returns>
        public double SetTilt(double index, double setTilt)
        {
            double tilt = 0.0;

            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                tilt = vc.moduleCamera[i].SetTilt((float)setTilt);
            }

            return tilt;
        }

        /// <summary>
        /// Toggles a deployable camera (retracts it if extended, extends it if retracted).  Has
        /// no effect on non-deployable cameras.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.CameraCount()` - 1.</param>
        /// <returns>1 if the camera deploys / undeploys, 0 otherwise.</returns>
        public double ToggleCameraDeployment(double index)
        {
            int i = (int)index;
            if (i >= 0 && i < vc.moduleCamera.Length)
            {
                return vc.moduleCamera[i].ToggleDeployment() ? 1.0 : 0.0;
            }

            return 0.0;
        }
        #endregion

        /// <summary>
        /// The methods in this section provide information on cargo bays, including the
        /// number of such bays and their deployment state.  There are also methods to
        /// open and close such bays.
        /// 
        /// Note that, for the purpose of this section, cargo bays are defined as parts
        /// that use ModuleAnimateGeneric to control access to the cargo bay.  The
        /// ModuleServiceModule introduced for the KSP Making History expansion is not
        /// counted, since it does not provide a method that MAS can use to deploy the
        /// covers.
        /// </summary>
        #region Cargo Bay

        /// <summary>
        /// Returns a count of the number of controllable cargo bays on the vessel.
        /// </summary>
        /// <returns>The number of controllable cargo bays on the vessel.</returns>
        public double CargoBayCount()
        {
            return vc.moduleCargoBay.Length;
        }

        /// <summary>
        /// Provides the status of cargo bay doors.
        /// 
        /// * 0 - No cargo bays on the vessel.
        /// * 1 - At least one cargo bay is closed.
        /// * 2 - At least cargo bay is moving (opening or closing).
        /// * 3 - All cargo bays are open.
        /// </summary>
        /// <returns>0, 1, 2, or 3.</returns>
        public double CargoBayPosition()
        {
            return vc.cargoBayPosition;
        }

        /// <summary>
        /// Open or close cargo bays.
        /// Will not affect any cargo bays that are already in motion.
        /// </summary>
        /// <returns>1 if at least one cargo bay is now opening or closing, 0 otherwise.</returns>
        public double SetCargoBay(bool open)
        {
            bool anyMoved = false;

            for (int i = vc.moduleCargoBay.Length - 1; i >= 0; --i)
            {
                ModuleCargoBay me = vc.moduleCargoBay[i];
                PartModule deployer = me.part.Modules[me.DeployModuleIndex];
                if (deployer is ModuleAnimateGeneric)
                {
                    ModuleAnimateGeneric mag = deployer as ModuleAnimateGeneric;
                    if (mag.CanMove && open != mag.Extended())
                    {
                        mag.Toggle();
                        anyMoved = true;
                    }
                }
            }

            return (anyMoved) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Opens closed cargo bays, closes open cargo bays.  Will not try to toggle any cargo bays
        /// that are already in motion.
        /// </summary>
        /// <returns>1 if at least one cargo bay is now moving, 0 otherwise.</returns>
        public double ToggleCargoBay()
        {
            bool anyToggled = false;

            for (int i = vc.moduleCargoBay.Length - 1; i >= 0; --i)
            {
                ModuleCargoBay me = vc.moduleCargoBay[i];
                PartModule deployer = me.part.Modules[me.DeployModuleIndex];
                if (deployer is ModuleAnimateGeneric)
                {
                    ModuleAnimateGeneric mag = deployer as ModuleAnimateGeneric;
                    if (mag.CanMove)
                    {
                        mag.Toggle();
                        anyToggled = true;
                    }
                }
            }

            return (anyToggled) ? 1.0 : 0.0;
        }
        #endregion

        /// <summary>
        /// Variables related to CommNet connectivity are in this category.
        /// </summary>
        #region CommNet

        /// <summary>
        /// Returns the number of antennae on the vessel.
        /// </summary>
        /// <returns>The number of deployable antennae on the vessel.</returns>
        public double AntennaCount()
        {
            return vc.moduleAntenna.Length;
        }

        /// <summary>
        /// Returns 1 if all antennae are damaged.
        /// </summary>
        /// <returns>1 if all antennae are damaged; 0 otherwise.</returns>
        public double AntennaDamaged()
        {
            return (vc.antennaPosition == 0) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if at least one antenna may be deployed.
        /// </summary>
        /// <returns>1 if any antenna is retracted and available to deploy; 0 otherwise.</returns>
        public double AntennaDeployable()
        {
            return (vc.antennaDeployable) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if at least one antenna is moving.
        /// </summary>
        /// <returns>1 if any antenna is moving (deploying or retracting).</returns>
        public double AntennaMoving()
        {
            return (vc.antennaMoving) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns a number representing deployable antenna position:
        /// 
        /// * 0 = Broken
        /// * 1 = Retracted
        /// * 2 = Retracting
        /// * 3 = Extending
        /// * 4 = Extended
        /// 
        /// If there are multiple antennae, the first non-broken antenna's state
        /// is reported; if all antennae are broken, the state will be 0.
        /// </summary>
        /// <returns>Antenna Position (a number between 0 and 4); 1 if no antennae are installed.</returns>
        public double AntennaPosition()
        {
            return vc.antennaPosition;
        }

        /// <summary>
        /// Returns 1 if at least one antenna is retractable.
        /// </summary>
        /// <returns>1 if a antenna is deployed, and it may be retracted; 0 otherwise.</returns>
        public double AntennaRetractable()
        {
            return (vc.antennaRetractable) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Reports if the vessel can communicate.
        /// </summary>
        /// <returns>1 if the vessel can communicate, 0 otherwise.</returns>
        public double CommNetCanCommunicate()
        {
            return vessel.connection.CanComm ? 1.0 : 0.0;
        }

        /// <summary>
        /// Reports if the vessel can transmit science.
        /// </summary>
        /// <returns>1 if the vessel can transmit science, 0 otherwise.</returns>
        public double CommNetCanScience()
        {
            return vessel.connection.CanScience ? 1.0 : 0.0;
        }

        /// <summary>
        /// Reports if the vessel is connected to CommNet.
        /// </summary>
        /// <returns>1 if the vessel is connected, 0 otherwise.</returns>
        public double CommNetConnected()
        {
            return vessel.connection.IsConnected ? 1.0 : 0.0;
        }

        /// <summary>
        /// Reports if the vessel has a connection home.
        /// </summary>
        /// <returns>1 if the vessel can talk to home, 0 otherwise.</returns>
        public double CommNetConnectedHome()
        {
            return vessel.connection.IsConnectedHome ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the current control state of the vessel:
        /// 
        /// * 2: Full Kerbal control
        /// * 1: Partial Kerbal control
        /// * 0: No Kerbal control
        /// * -1: Other control state
        /// </summary>
        /// <returns>A value between -1 and +2</returns>
        public double CommNetControlState()
        {
            switch (vessel.connection.ControlState)
            {
                case CommNet.VesselControlState.KerbalFull:
                    return 2.0;
                case CommNet.VesselControlState.KerbalPartial:
                    return 1.0;
                case CommNet.VesselControlState.KerbalNone:
                    return 0.0;
            }
            return -1.0;
        }

        /// <summary>
        /// Returns the name of the endpoint of the CommNet connection.
        /// </summary>
        /// <returns>The name of the endpoint.</returns>
        public string CommNetEndpoint()
        {
            if (lastLink != null)
            {
                return lastLink.b.name;
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the latitude on Kerbin of the current CommNet deep space relay.
        /// If there is no link home, returns 0.
        /// </summary>
        /// <returns>Latitude of the DSN relay, or 0.</returns>
        public double CommNetLatitude()
        {
            if (lastLink != null && lastLink.hopType == CommNet.HopType.Home)
            {
                Vector2d ll;
                if (MASLoader.deepSpaceNetwork.TryGetValue(lastLink.b.name, out ll))
                {
                    return ll.x;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the longitude on Kerbin of the current CommNet deep space relay.
        /// If there is no link home, returns 0.
        /// </summary>
        /// <returns>Longitude of the DSN relay, or 0.</returns>
        public double CommNetLongitude()
        {
            if (lastLink != null && lastLink.hopType == CommNet.HopType.Home)
            {
                Vector2d ll;
                if (MASLoader.deepSpaceNetwork.TryGetValue(lastLink.b.name, out ll))
                {
                    return ll.y;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the signal delay between the vessel and its CommNet endpoint.
        /// </summary>
        /// <returns>Delay in seconds.</returns>
        public double CommNetSignalDelay()
        {
            return vessel.connection.SignalDelay;
        }

        /// <summary>
        /// Returns a quality value for the CommNet signal.  The quality value correlates to
        /// the "signal bars" display on the KSP UI.
        /// 
        /// * 0 - No signal
        /// * 1 - Red
        /// * 2 - Orange 
        /// * 3 - Yellow
        /// * 4 - Green
        /// </summary>
        /// <returns>A value from 0 to 4 as described in the summary.</returns>
        public double CommNetSignalQuality()
        {
            switch (vessel.connection.Signal)
            {
                case CommNet.SignalStrength.None:
                    return 0.0;
                case CommNet.SignalStrength.Red:
                    return 1.0;
                case CommNet.SignalStrength.Orange:
                    return 2.0;
                case CommNet.SignalStrength.Yellow:
                    return 3.0;
                case CommNet.SignalStrength.Green:
                    return 4.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the signal strength of the CommNet signal.
        /// </summary>
        /// <returns>A value between 0 (no signal) and 1 (maximum signal strength).</returns>
        public double CommNetSignalStrength()
        {
            return vessel.connection.SignalStrength;
        }

        /// <summary>
        /// Deploys antennae (when 'deployed' is true) or undeploys antennae (when 'deployed' is false).
        /// </summary>
        /// <param name="deploy">Whether the function should deploy the antennae or undeploy them.</param>
        /// <returns>1 if any antenna changes, 0 if all are already in the specified state.</returns>
        public double SetAntenna(bool deploy)
        {
            bool anyMoved = false;

            if (deploy)
            {
                for (int i = vc.moduleAntenna.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleAntenna[i].useAnimation && vc.moduleAntenna[i].deployState == ModuleDeployablePart.DeployState.RETRACTED)
                    {
                        vc.moduleAntenna[i].Extend();
                        anyMoved = true;
                    }
                }
            }
            else
            {
                for (int i = vc.moduleAntenna.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleAntenna[i].useAnimation && vc.moduleAntenna[i].retractable && vc.moduleAntenna[i].deployState == ModuleDeployablePart.DeployState.EXTENDED)
                    {
                        vc.moduleAntenna[i].Retract();
                        anyMoved = true;
                    }
                }
            }

            return (anyMoved) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Deploys / undeploys antennae.
        /// </summary>
        /// <returns>1 if any antennas were toggled, 0 otherwise</returns>
        public double ToggleAntenna()
        {
            bool anyMoved = false;
            if (vc.antennaDeployable)
            {
                for (int i = vc.moduleAntenna.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleAntenna[i].useAnimation && vc.moduleAntenna[i].deployState == ModuleDeployablePart.DeployState.RETRACTED)
                    {
                        vc.moduleAntenna[i].Extend();
                        anyMoved = true;
                    }
                }
            }
            else if (vc.antennaRetractable)
            {
                for (int i = vc.moduleAntenna.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleAntenna[i].useAnimation && vc.moduleAntenna[i].retractable && vc.moduleAntenna[i].deployState == ModuleDeployablePart.DeployState.EXTENDED)
                    {
                        vc.moduleAntenna[i].Retract();
                        anyMoved = true;
                    }
                }
            }

            return (anyMoved) ? 1.0 : 0.0;
        }
        #endregion

        /// <summary>
        /// Variables and actions related to the controls (roll / pitch / yaw / translation)
        /// are in this category.
        /// </summary>
        #region Control Input State
        /// <summary>
        /// Returns 1 when roll/translation controls are near neutral.
        /// </summary>
        /// <returns></returns>
        public double ControlNeutral()
        {
            float netinputs = Math.Abs(vessel.ctrlState.pitch) + Math.Abs(vessel.ctrlState.roll) + Math.Abs(vessel.ctrlState.yaw) + Math.Abs(vessel.ctrlState.X) + Math.Abs(vessel.ctrlState.Y) + Math.Abs(vessel.ctrlState.Z);

            return (netinputs > 0.01) ? 0.0 : 1.0;
        }

        /// <summary>
        /// Returns the current pitch control state.
        /// </summary>
        /// <returns></returns>
        public double StickPitch()
        {
            return vessel.ctrlState.pitch;
        }

        /// <summary>
        /// Returns the current roll control state.
        /// </summary>
        /// <returns></returns>
        public double StickRoll()
        {
            return vessel.ctrlState.roll;
        }

        /// <summary>
        /// Returns the current X translation state.  Note that this value is the direction
        /// the thrust is firing, not the direction the vessel will move.
        /// </summary>
        /// <returns>A value between -1 (full left) and +1 (full right).</returns>
        public double StickTranslationX()
        {
            return vessel.ctrlState.X;
        }

        /// <summary>
        /// Returns the current Y translation state.  Note that this value is the direction
        /// the thrust is firing, not the direction the vessel will move.
        /// </summary>
        /// <returns>A value between -1 (full top) and +1 (full bottom).</returns>
        public double StickTranslationY()
        {
            return vessel.ctrlState.Y;
        }

        /// <summary>
        /// Returns the current Z translation state.  Note that this value is the direction
        /// the thrust is firing, not the direction the vessel will move.
        /// </summary>
        /// <returns>A value between -1 (full aft) and +1 (full forward).</returns>
        public double StickTranslationZ()
        {
            return vessel.ctrlState.Z;
        }

        /// <summary>
        /// Returns the current pitch trim setting.
        /// </summary>
        /// <returns>Trim setting, between -1 and +1</returns>
        public double StickTrimPitch()
        {
            return vessel.ctrlState.pitchTrim;
        }

        /// <summary>
        /// Returns the current roll trim setting.
        /// </summary>
        /// <returns>Trim setting, between -1 and +1</returns>
        public double StickTrimRoll()
        {
            return vessel.ctrlState.rollTrim;
        }

        /// <summary>
        /// Returns the current yaw trim setting.
        /// </summary>
        /// <returns>Trim setting, between -1 and +1</returns>
        public double StickTrimYaw()
        {
            return vessel.ctrlState.yawTrim;
        }

        /// <summary>
        /// Returns the current yaw control state.
        /// </summary>
        /// <returns></returns>
        public double StickYaw()
        {
            return vessel.ctrlState.yaw;
        }
        #endregion

        /// <summary>
        /// The Crew category provides information about the crew aboard the vessel.
        /// 
        /// `seatNumber` is a 0-based index to select which seat is being queried.  This
        /// means that a 3-seat pod has valid seat numbers 0, 1, and 2.  A single-seat
        /// pod has a valid seat number 0.
        /// 
        /// One difference to be aware of between RPM and MAS: The full-vessel crew info
        /// (those methods starting 'VesselCrew') provide info on crew members without
        /// regards to seating arrangements.  For instance, if the command pod has 2 of 3
        /// seats occupied, and a passenger pod as 1 of 4 seats occupied, VesselCrewCount
        /// will return 3, and the crew info (eg, VesselCrewName) will provide values for
        /// indices 0, 1, and 2.
        /// </summary>
        #region Crew
        /// <summary>
        /// Returns 1 if the crew in `seatNumber` has the 'BadS' trait.  Returns 0 if
        /// `seatNumber` is invalid or there is no crew in that seat, or the crew does
        /// not possess the 'BadS' trait.
        /// </summary>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>1 or 0 (see summary)</returns>
        public double CrewBadS(double seatNumber)
        {
            int seatIdx = (int)seatNumber;

            return (seatIdx >= 0 && seatIdx < fc.localCrew.Length && fc.localCrew[seatIdx] != null && fc.localCrew[seatIdx].isBadass) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the number of experience points for the selected crew member.
        /// </summary>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>A number 0 or higher; 0 if the requested seat is invalid or empty.</returns>
        public double CrewExperience(double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            if (seatIdx >= 0 && seatIdx < fc.localCrew.Length && fc.localCrew[seatIdx] != null)
            {
                return fc.localCrew[seatIdx].experience;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns a number representing the gender of the selected crew member.
        /// </summary>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>1 if the crew is male, 2 if the crew is female, 0 if the seat is empty.</returns>
        public double CrewGender(double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            if (seatIdx >= 0 && seatIdx < fc.localCrew.Length && fc.localCrew[seatIdx] != null)
            {
                return (fc.localCrew[seatIdx].gender == ProtoCrewMember.Gender.Male) ? 1.0 : 2.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the experience level of the selected crew member.
        /// </summary>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>A number 0-5; 0 if the requested seat is invalid or empty.</returns>
        public double CrewLevel(double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            if (seatIdx >= 0 && seatIdx < fc.localCrew.Length && fc.localCrew[seatIdx] != null)
            {
                return fc.localCrew[seatIdx].experienceLevel;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the name of the crew member seated in `seatNumber`.  If
        /// the number is invalid, or no Kerbal is in the seat, returns an
        /// empty string.
        /// </summary>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>The crew name, or an empty string if there is no crew in the
        /// given seat.</returns>
        public string CrewName(double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            if (seatIdx >= 0 && seatIdx < fc.localCrew.Length && fc.localCrew[seatIdx] != null)
            {
                return fc.localCrew[seatIdx].name;
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the 'PANIC' level of the selected crew member.
        /// </summary>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>A number between 0 and 1; 0 if the requested seat is invalid or empty.</returns>
        public double CrewPanic(double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            var expression = fc.GetLocalKES(seatIdx);
            if (expression != null)
            {
                return expression.panicLevel;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the stupidity rating of the selected crew member.
        /// </summary>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>A number between 0 and 1; 0 if the requested seat is invalid or empty.</returns>
        public double CrewStupidity(double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            if (seatIdx >= 0 && seatIdx < fc.localCrew.Length && fc.localCrew[seatIdx] != null)
            {
                return fc.localCrew[seatIdx].stupidity;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the job title of the selected crew member.
        /// </summary>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>The name of the job title, or an empty string if `seatNumber` is invalid or
        /// unoccupied.</returns>
        public string CrewTitle(double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            if (seatIdx >= 0 && seatIdx < fc.localCrew.Length && fc.localCrew[seatIdx] != null)
            {
                return fc.localCrew[seatIdx].experienceTrait.Title;
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the 'WHEE' level of the selected crew member.
        /// </summary>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>A number between 0 and 1; 0 if the requested seat is invalid or empty.</returns>
        public double CrewWhee(double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            var expression = fc.GetLocalKES(seatIdx);
            if (expression != null)
            {
                return expression.wheeLevel;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the number of seats in the current IVA pod.
        /// </summary>
        /// <returns>The selected number of seats (1 or more).</returns>
        public double NumberSeats()
        {
            return fc.localCrew.Length;
        }

        /// <summary>
        /// Indicates whether a given seat is occupied by a Kerbal.  Returns 1 when `seatNumber` is
        /// valid and there is a Kerbal in the given seat, and returns 0 in all other instances.
        /// </summary>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>1 if `seatNumber` is a valid seat; 0 otherwise.</returns>
        public double SeatOccupied(double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            return (seatIdx >= 0 && seatIdx < fc.localCrew.Length && fc.localCrew[seatIdx] != null) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if the selected crew has the 'BadS' trait.  Returns 0 if
        /// `crewIndex` is invalid or the crew does
        /// not possess the 'BadS' trait.
        /// </summary>
        /// <param name="crewIndex">The index of the crewmember to check.  Indices start at 0.</param>
        /// <returns>1 or 0 (see summary)</returns>
        public double VesselCrewBadS(double crewIndex)
        {
            int index = (int)crewIndex;

            if (index >= 0 && index < vessel.GetCrewCount())
            {
                return (vessel.GetVesselCrew()[index].isBadass) ? 1.0 : 0.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Capacity of all crewed locations on the vessel.
        /// </summary>
        /// <returns>0 or higher.</returns>
        public double VesselCrewCapacity()
        {
            return vessel.GetCrewCapacity();
        }

        /// <summary>
        /// Total count of crew aboard the vessel.
        /// </summary>
        /// <returns>0 or higher.</returns>
        public double VesselCrewCount()
        {
            return vessel.GetCrewCount();
        }

        /// <summary>
        /// Returns the number of experience points for the selected crew member.
        /// </summary>
        /// <param name="crewIndex">The index of the crewmember to check.  Indices start at 0.</param>
        /// <returns>A number 0 or higher; 0 if the requested seat is invalid.</returns>
        public double VesselCrewExperience(double crewIndex)
        {
            int index = (int)crewIndex;

            if (index >= 0 && index < vessel.GetCrewCount())
            {
                return vessel.GetVesselCrew()[index].experience;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns a number representing the gender of the selected crew member.
        /// </summary>
        /// <param name="crewIndex">The index of the crewmember to check.  Indices start at 0.</param>
        /// <returns>1 if the crew is male, 2 if the crew is female, 0 if the index is invalid.</returns>
        public double VesselCrewGender(double crewIndex)
        {
            int index = (int)crewIndex;

            if (index >= 0 && index < vessel.GetCrewCount())
            {
                return (vessel.GetVesselCrew()[index].gender == ProtoCrewMember.Gender.Male) ? 1.0 : 2.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the experience level of the selected crew member.
        /// </summary>
        /// <param name="crewIndex">The index of the crewmember to check.  Indices start at 0.</param>
        /// <returns>A number 0-5; 0 if the requested index is invalid.</returns>
        public double VesselCrewLevel(double crewIndex)
        {
            int index = (int)crewIndex;

            if (index >= 0 && index < vessel.GetCrewCount())
            {
                return vessel.GetVesselCrew()[index].experienceLevel;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the name of the crew member seated in `seatNumber`.  If
        /// the number is invalid, or no Kerbal is in the seat, returns an
        /// empty string.
        /// </summary>
        /// <param name="crewIndex">The index of the crewmember to check.  Indices start at 0.</param>
        /// <returns>The crew name, or an empty string if index is invalid.</returns>
        public string VesselCrewName(double crewIndex)
        {
            int index = (int)crewIndex;

            if (index >= 0 && index < vessel.GetCrewCount())
            {
                return vessel.GetVesselCrew()[index].name;
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the 'PANIC' level of the selected crew member.
        /// </summary>
        /// <param name="crewIndex">The index of the crewmember to check.  Indices start at 0.</param>
        /// <returns>A number between 0 and 1; 0 if the requested index is invalid.</returns>
        public double VesselCrewPanic(double crewIndex)
        {
            int index = (int)crewIndex;

            if (index >= 0 && index < vessel.GetCrewCount())
            {
                return GetVesselCrewExpression(index).panicLevel;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the stupidity rating of the selected crew member.
        /// </summary>
        /// <param name="crewIndex">The index of the crewmember to check.  Indices start at 0.</param>
        /// <returns>A number between 0 and 1; 0 if the requested index is invalid.</returns>
        public double VesselCrewStupidity(double crewIndex)
        {
            int index = (int)crewIndex;

            if (index >= 0 && index < vessel.GetCrewCount())
            {
                return vessel.GetVesselCrew()[index].stupidity;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the job title of the selected crew member.
        /// </summary>
        /// <param name="crewIndex">The index of the crewmember to check.  Indices start at 0.</param>
        /// <returns>The name of the job title, or an empty string if `crewIndex` is invalid.</returns>
        public string VesselCrewTitle(double crewIndex)
        {
            int index = (int)crewIndex;

            if (index >= 0 && index < vessel.GetCrewCount())
            {
                return vessel.GetVesselCrew()[index].experienceTrait.Title;
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the 'WHEE' level of the selected crew member.
        /// </summary>
        /// <param name="crewIndex">The index of the crewmember to check.  Indices start at 0.</param>
        /// <returns>A number between 0 and 1; 0 if the requested index is invalid.</returns>
        public double VesselCrewWhee(double crewIndex)
        {
            int index = (int)crewIndex;

            if (index >= 0 && index < vessel.GetCrewCount())
            {

                return GetVesselCrewExpression(index).wheeLevel;
            }

            return 0.0;
        }

        #endregion

        /// <summary>
        /// Docking control and status are in the Docking category.
        /// 
        /// Many of these methods use the concept of "Primary Docking Port".
        /// The primary docking port is defined as the first or only docking
        /// port found on the vessel.  These features are primarily centered
        /// around CTV / OTV type spacecraft where there is a single dock,
        /// not space stations or large craft with many docks.
        /// </summary>
        #region Docking
        /// <summary>
        /// Return 1 if the dock is attached to something (this includes parts that
        /// are not compatible to the docking port, such as boost protective covers or
        /// launch escape systems that are attached in the VAB).
        /// 
        /// To determine if the dock is connected to a compatible docking port, use fc.Docked().
        /// </summary>
        /// <returns></returns>
        public double DockConnected()
        {
            return (vc.dockingNodeState == MASVesselComputer.DockingNodeState.DOCKED || vc.dockingNodeState == MASVesselComputer.DockingNodeState.PREATTACHED) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Return 1 if the dock is attached to a compatible dock; return 0 otherwise.
        /// 
        /// Note that this function will return 0 if the compatible dock was connected in the
        /// VAB.  fc.Docked() only detects docking events that take place during Flight.
        /// </summary>
        /// <returns></returns>
        public double Docked()
        {
            return (vc.dockingNodeState == MASVesselComputer.DockingNodeState.DOCKED) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Return 1 if the dock is ready; return 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double DockReady()
        {
            return (vc.dockingNodeState == MASVesselComputer.DockingNodeState.READY) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if the primary docking port on the vessel is the current reference transform.
        /// 
        /// Returns 0 if the primary docking port is not the reference transform, or if there is no docking port,
        /// or if a docking port other than the primary port is the reference transform.
        /// </summary>
        /// <returns></returns>
        public double GetDockIsReference()
        {
            return (vc.dockingNode != null && vc.dockingNode.part == vessel.GetReferenceTransformPart()) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if the current IVA pod is the reference transform.  Returns 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double GetPodIsReference()
        {
            return (fc.part == vessel.GetReferenceTransformPart()) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the index of the docking port currently tracked by the vessel.
        /// 
        /// If no port is being tracked, returns -1.
        /// </summary>
        /// <returns>For a valid docking port, a number between 0 and 'fc.TargetDockCount()' - 1.  Otherwise, -1.</returns>
        public double GetTargetDockIndex()
        {
            if (vc.targetType == MASVesselComputer.TargetType.DockingPort)
            {
                if (vc.targetDockingPorts.Length == 1)
                {
                    // The only docking port.
                    return 0.0;
                }

                ModuleDockingNode activeNode = vc.activeTarget as ModuleDockingNode;

                return Array.FindIndex(vc.targetDockingPorts, x => x == activeNode);
            }

            return -1.0;
        }

        /// <summary>
        /// Set the primary docking port to be the reference transform.
        /// </summary>
        /// <returns>1 if the reference was changed, 0 otherwise.</returns>
        public double SetDockToReference()
        {
            if (vc.dockingNode != null)
            {
                vessel.SetReferenceTransform(vc.dockingNode.part);
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Set the current IVA pod to be the reference transform.
        /// </summary>
        /// <returns>1 if the reference was changed.</returns>
        public double SetPodToReference()
        {
            vessel.SetReferenceTransform(fc.part);
            return 1.0;
        }

        /// <summary>
        /// Targets the docking port on the target vessel identified by 'idx'.
        /// 
        /// Note that KSP only allows targeting docking ports within a certain range (typically
        /// 200 meters).
        /// </summary>
        /// <param name="idx">A value between 0 and `fc.TargetDockCount()` - 1.</param>
        /// <returns>1 the dock could be targeted, 0 otherwise.</returns>
        public double SetTargetDock(double idx)
        {
            int index = (int)idx;
            if (index < 0 || index >= vc.targetDockingPorts.Length)
            {
                return 0.0;
            }

            FlightGlobals.fetch.SetVesselTarget(vc.targetDockingPorts[index]);

            return 1.0;
        }

        /// <summary>
        /// Returns the number of available docking ports found on the target vessel when the following
        /// conditions are met:
        /// 
        /// 1) There are docking ports on the target vessel compatible with the designated
        /// docking port on the current vessel.  The designated docking port is either the
        /// only docking port on the current vessel, or it is one of the docking ports selected
        /// arbitrarily.
        /// 
        /// 2) There are no docking ports on the current vessel.  In this case, all docking
        /// ports on the target vessel are counted.
        /// 
        /// Note that if the target is unloaded, this method will return 0.  If the target is
        /// not a vessel, it also returns 0.
        /// 
        /// This function is identical to `fc.TargetAvailableDockingPorts()`.
        /// </summary>
        /// <returns>Number of available compatible docking ports, or total available docking ports, or 0.</returns>
        public double TargetDockCount()
        {
            return vc.targetDockingPorts.Length;
        }

        /// <summary>
        /// Returns the angle in degrees that the vessel must roll in order to align with the
        /// currently-targeted docking port.
        /// 
        /// If no dock is targeted, there are no alignment requirements, or a compatible docking port
        /// is not the reference part, this function returns 0.
        /// </summary>
        /// <returns>The roll required to align docking ports, or 0.</returns>
        public double TargetDockError()
        {
            if (vc.dockingNode != null && vc.dockingNode.part.transform == vc.referenceTransform && vc.targetType == MASVesselComputer.TargetType.DockingPort && vc.targetDockingTransform != null)
            {
                ModuleDockingNode activeNode = vc.activeTarget as ModuleDockingNode;
                // TODO: If either is false, does that disable snapRotation?  Or do both need to be false?
                if (vc.dockingNode.snapRotation == false && activeNode.snapRotation == false)
                {
                    return 0.0;
                }

                float snapOffset;
                if (vc.dockingNode.snapRotation)
                {
                    if (activeNode.snapRotation)
                    {
                        snapOffset = Mathf.Min(activeNode.snapOffset, vc.dockingNode.snapOffset);
                    }
                    else
                    {
                        snapOffset = vc.dockingNode.snapOffset;
                    }
                }
                else
                {
                    snapOffset = activeNode.snapOffset;
                }

                Vector3 projectedVector = Vector3.ProjectOnPlane(vc.targetDockingTransform.up, vc.referenceTransform.up);
                projectedVector.Normalize();

                float dotLateral = Vector3.Dot(projectedVector, vc.referenceTransform.right);
                float dotLongitudinal = Vector3.Dot(projectedVector, vc.referenceTransform.forward);

                // Taking arc tangent of x/y lets us treat the front of the vessel
                // as the 0 degree location.
                float roll = Mathf.Atan2(dotLateral, dotLongitudinal) * Mathf.Rad2Deg;
                // Normalize it
                if (roll < 0.0f)
                {
                    roll += 360.0f;
                }

                float rollError = roll % snapOffset;
                if (rollError > (snapOffset * 0.5f))
                {
                    rollError -= snapOffset;
                }

                return rollError;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the name of the target dock selected by 'idx'.  If the Docking Port Alignment
        /// Indicator mod is installed, the name given to the dock from that mod is returned.  Otherwise,
        /// only the part's name is returned.
        /// </summary>
        /// <returns>The name of the selected dock, or an empty string.</returns>
        public string TargetDockName(double idx)
        {
            int index = (int)idx;
            if (index >= 0 && idx < vc.targetDockingPorts.Length)
            {
                return fc.GetDockingPortName(vc.targetDockingPorts[index].part);
            }

            return string.Empty;
        }

        /// <summary>
        /// Targets the next valid docking port on the target vessel.
        /// 
        /// Note that KSP only allows targeting docking ports within a certain range (typically
        /// 200 meters).
        /// </summary>
        /// <returns>1 if a dock could be targeted, 0 otherwise.</returns>
        public double TargetNextDock()
        {
            if (vc.targetDockingPorts.Length == 0)
            {
                return 0.0;
            }
            else if (vc.targetType == MASVesselComputer.TargetType.Vessel)
            {
                FlightGlobals.fetch.SetVesselTarget(vc.targetDockingPorts[0]);
                return 1.0;
            }
            else if (vc.targetType == MASVesselComputer.TargetType.DockingPort)
            {
                if (vc.targetDockingPorts.Length == 1)
                {
                    // We're already targeting the only docking port.
                    return 1.0;
                }

                ModuleDockingNode activeNode = vc.activeTarget as ModuleDockingNode;
                int currentIndex = Array.FindIndex(vc.targetDockingPorts, x => x == activeNode);
                if (currentIndex == -1)
                {
                    FlightGlobals.fetch.SetVesselTarget(vc.targetDockingPorts[0]);
                }
                else
                {
                    FlightGlobals.fetch.SetVesselTarget(vc.targetDockingPorts[(currentIndex + 1) % vc.targetDockingPorts.Length]);
                }
                return 1.0;
            }
            return 0.0;
        }

        /// <summary>
        /// Undock / detach (if pre-attached) the active docking node.
        /// </summary>
        /// <returns>If the active dock undocked from something.</returns>
        public double Undock()
        {
            if (vc.dockingNode != null)
            {
                if (vc.dockingNodeState == MASVesselComputer.DockingNodeState.DOCKED)
                {
                    vc.dockingNode.Undock();
                    return 1.0;
                }
                else if (vc.dockingNodeState == MASVesselComputer.DockingNodeState.PREATTACHED)
                {
                    vc.dockingNode.Decouple();
                    return 1.0;
                }
            }

            return 0.0;
        }
        #endregion

        /// <summary>
        /// Engine status and control methods are in the Engine category.
        /// </summary>
        #region Engine

        /// <summary>
        /// Returns the current fuel flow in grams/second
        /// </summary>
        /// <returns></returns>
        public double CurrentFuelFlow()
        {
            return vc.currentEngineFuelFlow;
        }

        /// <summary>
        /// Returns the average deflection of active, unlocked gimbals, from 0 (no deflection) to 1 (max deflection).
        /// 
        /// The direction of the deflection is ignored, but the value accounts for assymetrical gimbal configurations,
        /// eg, if X+ is 5.0, and X- is -3.0, the deflection percentage accounts for this difference.
        /// </summary>
        /// <returns></returns>
        public double CurrentGimbalDeflection()
        {
            return vc.gimbalDeflection;
        }

        /// <summary>
        /// Return the current specific impulse in seconds.
        /// </summary>
        /// <returns>The current Isp.</returns>
        public double CurrentIsp()
        {
            return vc.currentIsp;
        }

        /// <summary>
        /// Returns the current thrust output, from 0.0 to 1.0.
        /// </summary>
        /// <returns>Thrust output, ranging from 0 to 1.</returns>
        public double CurrentThrust(bool useThrottleLimits)
        {
            if (vc.currentThrust > 0.0f)
            {
                return vc.currentThrust / ((useThrottleLimits) ? vc.currentLimitedMaxThrust : vc.currentMaxThrust);
            }
            else
            {
                return 0.0f;
            }
        }

        /// <summary>
        /// Returns the current thrust in kiloNewtons
        /// </summary>
        /// <returns></returns>
        public double CurrentThrustkN()
        {
            return vc.currentThrust;
        }

        /// <summary>
        /// Returns the current thrust-to-weight ratio.
        /// </summary>
        /// <returns></returns>
        public double CurrentTWR()
        {
            if (vc.currentThrust > 0.0f)
            {
                return vc.currentThrust / (vessel.totalMass * vc.surfaceAccelerationFromGravity);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the total delta-V remaining for the vessel based on its current
        /// altitude.
        /// 
        /// If Kerbal Engineer or MechJeb is installed, those mods are used for the computation.
        /// </summary>
        /// <seealso>MechJeb, Kerbal Engineer Redux</seealso>
        /// <returns>Remaining delta-V in m/s.</returns>
        public double DeltaV()
        {
            if (MASIKerbalEngineer.keFound)
            {
                return keProxy.DeltaV();
            }
            else if (mjProxy.mjAvailable)
            {
                return mjProxy.DeltaV();
            }
            else
            {
                VesselDeltaV vdV = vessel.VesselDeltaV;
                if (vdV.IsReady)
                {
                    return vdV.TotalDeltaVActual;
                }
                else
                {
                    return 0.0;
                }
            }
        }

        /// <summary>
        /// Returns an estimate of the delta-V remaining for the current stage.
        /// </summary>
        /// <returns>Remaining delta-V for this stage in m/s.</returns>
        public double DeltaVStage()
        {
            // mass in tonnes.
            double stagePropellantMass = vc.enginePropellant.currentStage * vc.enginePropellant.density;

            if (stagePropellantMass > 0.0)
            {
                return vc.currentIsp * Utility.StandardG * Math.Log(vessel.totalMass / (vessel.totalMass - stagePropellantMass));
            }

            return 0.0;
        }

        /// <summary>
        /// Returns a count of the total number of engines that are active.
        /// </summary>
        /// <returns></returns>
        public double EngineCountActive()
        {
            return vc.activeEngineCount;
        }

        /// <summary>
        /// Returns a count of the total number of engines tracked.  This
        /// count includes engines that have not staged.
        /// </summary>
        /// <returns></returns>
        public double EngineCountTotal()
        {
            return vc.moduleEngines.Length;
        }

        /// <summary>
        /// Returns 1 if any active engines are in a flameout condition.
        /// </summary>
        /// <returns></returns>
        public double EngineFlameout()
        {
            return (vc.anyEnginesFlameout) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if at least one engine is enabled.
        /// </summary>
        /// <returns></returns>
        public double GetEnginesEnabled()
        {
            return (vc.anyEnginesEnabled) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if any currently-active engines have gimbals.  Returns 0 if no active engine has a gimbal.
        /// </summary>
        /// <returns></returns>
        public double GetActiveEnginesGimbal()
        {
            return (vc.activeEnginesGimbal) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the currently-configured limit of active gimbals, as set in the right-click part menus.
        /// This value ranges between 0 (no gimbal) and 1 (100% gimbal).
        /// </summary>
        /// <returns></returns>
        public double GetGimbalLimit()
        {
            return vc.gimbalLimit;
        }

        /// <summary>
        /// Returns 1 if any gimbals are currently active.
        /// </summary>
        /// <returns></returns>
        public double GetGimbalsActive()
        {
            return (vc.anyGimbalsActive) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if at least one active gimbal is locked.
        /// </summary>
        /// <returns></returns>
        public double GetGimbalsLocked()
        {
            return (vc.anyGimbalsLocked) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if any multi-mode engine is in secondary mode, 0 if no engines are,
        /// or there are no multi-mode engines.
        /// </summary>
        /// <returns></returns>
        public double GetMultiModeEngineMode()
        {
            for (int i = vc.multiModeEngines.Length; i >= 0; --i)
            {
                if (vc.multiModeEngines[i].runningPrimary == false)
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the current main throttle setting, from 0.0 to 1.0.
        /// </summary>
        /// <returns></returns>
        public double GetThrottle()
        {
            return vessel.ctrlState.mainThrottle;
        }

        /// <summary>
        /// Returns the average of the throttle limit for the active engines,
        /// ranging from 0 (no thrust) to 1 (maximum thrust).
        /// </summary>
        /// <returns></returns>
        public double GetThrottleLimit()
        {
            return vc.throttleLimit;
        }

        /// <summary>
        /// Returns the maximum fuel flow in grams/second
        /// </summary>
        /// <returns></returns>
        public double MaxFuelFlow()
        {
            return vc.maxEngineFuelFlow;
        }

        /// <summary>
        /// Returns the maximum specific impulse in seconds.
        /// </summary>
        /// <returns></returns>
        public double MaxIsp()
        {
            return vc.maxIsp;
        }

        /// <summary>
        /// Returns the maximum thrust in kN for the current altitude.
        /// </summary>
        /// <param name="useThrottleLimits">Apply throttle limits?</param>
        /// <returns>Maximum thrust in kN</returns>
        public double MaxThrustkN(bool useThrottleLimits)
        {
            return (useThrottleLimits) ? vc.currentLimitedMaxThrust : vc.currentMaxThrust;
        }

        /// <summary>
        /// Returns the maximum thrust-to-weight ratio.
        /// </summary>
        /// <param name="useThrottleLimits">Apply throttle limits?</param>
        /// <returns>Thrust-to-weight ratio, between 0 and 1.</returns>
        public double MaxTWR(bool useThrottleLimits)
        {
            double thrust = ((useThrottleLimits) ? vc.currentLimitedMaxThrust : vc.currentMaxThrust);
            if (thrust > 0.0)
            {
                return ((useThrottleLimits) ? vc.currentLimitedMaxThrust : vc.currentMaxThrust) / (vessel.totalMass * vc.surfaceAccelerationFromGravity);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Turns on/off engines for the current stage.
        /// </summary>
        /// <returns>1 if engines are now enabled, 0 if they are disabled.</returns>
        public double SetEnginesEnabled(bool enable)
        {
            return (vc.SetEnginesEnabled(enable)) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Change the gimbal limit for active gimbals.  Values less than 0 or greater than 1 are
        /// clamped to that range.
        /// </summary>
        /// <param name="newLimit">The new gimbal limit, between 0 and 1.</param>
        /// <returns>1 if any gimbals were updated, 0 otherwise.</returns>
        public double SetGimbalLimit(double newLimit)
        {
            float limit = Mathf.Clamp01((float)newLimit) * 100.0f;
            bool updated = false;

            for (int i = vc.moduleGimbals.Length - 1; i >= 0; --i)
            {
                if (vc.moduleGimbals[i].gimbalActive)
                {
                    vc.moduleGimbals[i].gimbalLimiter = limit;
                }
            }

            return (updated) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Locks or unlocks engine gimbals for the current stage.
        /// </summary>
        /// <returns>1 if any gimbals changed, 0 if none changed.</returns>
        public double SetGimbalLock(bool locked)
        {
            bool changed = false;
            for (int i = vc.moduleGimbals.Length - 1; i >= 0; --i)
            {
                if (vc.moduleGimbals[i].gimbalActive && vc.moduleGimbals[i].gimbalLock != locked)
                {
                    changed = true;
                    vc.moduleGimbals[i].gimbalLock = locked;
                }
            }

            return (changed) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Selects the primary or secondary mode for multi-mode engines.
        /// </summary>
        /// <param name="runPrimary">Selects the primary mode when true, the secondary mode when false.</param>
        /// <returns>1 if any engines were toggled, 0 if no multi-mode engines are installed.</returns>
        public double SetMultiModeEngineMode(bool runPrimary)
        {
            bool anyChanged = false;
            for (int i = vc.multiModeEngines.Length; i >= 0; --i)
            {
                if (vc.multiModeEngines[i].runningPrimary != runPrimary)
                {
                    vc.multiModeEngines[i].ToggleMode();
                    anyChanged = true;
                }
            }

            return (anyChanged) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Set the throttle.  May be set to any value between 0 and 1.  Values outside
        /// that range are clamped to [0, 1].
        /// </summary>
        /// <param name="throttlePercentage">Throttle setting, between 0 and 1.</param>
        /// <returns>The new throttle setting.</returns>
        public double SetThrottle(double throttlePercentage)
        {
            float throttle = Mathf.Clamp01((float)throttlePercentage);
            try
            {
                FlightInputHandler.state.mainThrottle = throttle;
            }
            catch (Exception e)
            {
                // RPM had a try-catch.  Why?
                Utility.LogError(this, "SetThrottle({0:0.00}) threw {1}", throttle, e);
            }
            return throttle;
        }

        /// <summary>
        /// Set the throttle limit.  May be set to any value between 0 and 1.  Values outside
        /// that range are clamped to [0, 1].
        /// </summary>
        /// <param name="newLimit"></param>
        /// <returns></returns>
        public double SetThrottleLimit(double newLimit)
        {
            float limit = Mathf.Clamp01((float)newLimit) * 100.0f;
            bool updated = vc.SetThrottleLimit(limit);

            return (updated) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Turns on/off engines for the current stage
        /// </summary>
        /// <returns>1 if engines are now enabled, 0 if they are disabled.</returns>
        public double ToggleEnginesEnabled()
        {
            return (vc.SetEnginesEnabled(!vc.anyEnginesEnabled)) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Toggles gimbal lock on/off for the current stage.
        /// </summary>
        /// <returns>1 if active gimbals are now locked, 0 if they are unlocked.</returns>
        public double ToggleGimbalLock()
        {
            bool newState = !vc.anyGimbalsLocked;
            for (int i = vc.moduleGimbals.Length - 1; i >= 0; --i)
            {
                if (vc.moduleGimbals[i].gimbalActive)
                {
                    vc.moduleGimbals[i].gimbalLock = newState;
                }
            }

            return (newState) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Toggles the mode for any multi-mode engines.
        /// </summary>
        /// <returns>1 if any engines were toggled, 0 if no multi-mode engines are installed.</returns>
        public double ToggleMultiModeEngineMode()
        {
            for (int i = vc.multiModeEngines.Length; i >= 0; --i)
            {
                vc.multiModeEngines[i].ToggleMode();
            }
            return (vc.multiModeEngines.Length > 0) ? 1.0 : 0.0;
        }
        #endregion

        /// <summary>
        /// Flight status variables are in this category.
        /// </summary>
        #region Flight Status
        /// <summary>
        /// Returns 1 if the vessel is in a landed state (LANDED, SPLASHED,
        /// or PRELAUNCH); 0 otherwise
        /// </summary>
        /// <returns></returns>
        public double VesselLanded()
        {
            return (vesselSituationConverted < 3) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if the vessel is in a flying state (FLYING, SUB_ORBITAL,
        /// ORBITING, ESCAPING, DOCKED).
        /// </summary>
        /// <returns></returns>
        public double VesselFlying()
        {
            if ((vessel.Landed || vessel.Splashed) != (vesselSituationConverted <= 2))
            {
                Utility.LogMessage(this, "vessel.Landed {0} and vesselSituationConverted {1} disagree! - vessel.situation is {2}", vessel.Landed, vesselSituationConverted, vessel.situation);
            }
            return (vesselSituationConverted > 2) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the vessel's situation, based on the KSP variable:
        /// 
        /// * 0 - LANDED
        /// * 1 - SPLASHED
        /// * 2 - PRELAUNCH
        /// * 3 - FLYING
        /// * 4 - SUB_ORBITAL
        /// * 5 - ORBITING
        /// * 6 - ESCAPING
        /// * 7 - DOCKED
        /// </summary>
        /// <returns>A number between 0 and 7 (inclusive).</returns>
        public double VesselSituation()
        {
            return vesselSituationConverted;
        }
        #endregion

        /// <summary>
        /// Variables and control methods for the Gear action group are in this
        /// category.  In addition, status and information methods for deployable
        /// landing gear and wheels are in this category.  For simplicity, landing gear
        /// and wheels may be simply called "landing gear" in the descriptions.
        /// </summary>
        #region Gear

        /// <summary>
        /// Returns the number of landing gear or wheels that are broken.  Returns 0 if none are, or if there
        /// are no gear.
        /// </summary>
        /// <returns>The number of landing gear that are broken.</returns>
        public double GearBrokenCount()
        {
            int brokenCount = 0;

            for (int i = vc.moduleWheelDamage.Length - 1; i >= 0; --i)
            {
                if (vc.moduleWheelDamage[i].isDamaged)
                {
                    return ++brokenCount;
                }
            }

            return (double)brokenCount;
        }

        /// <summary>
        /// Returns the number of wheels / landing gear installed on the craft.  This counts all
        /// landing gear and wheels, including those that do not deploy.
        /// </summary>
        /// <returns>Number of gear, or 0.</returns>
        public double GearCount()
        {
            return vc.moduleWheelBase.Length;
        }

        /// <summary>
        /// Returns 1 if there are actions assigned to the landing gear AG.
        /// </summary>
        /// <returns></returns>
        public double GearHasActions()
        {
            return (vc.GroupHasActions(KSPActionGroup.Gear)) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if any deployable landing gear or wheels are moving; otherwise
        /// returns 0.
        /// </summary>
        /// <returns>1 if any landing gear are moving, 0 otherwise.</returns>
        public double GearMoving()
        {
            for (int i = vc.moduleWheelDeployment.Length - 1; i >= 0; --i)
            {
                if (!(Mathf.Approximately(vc.moduleWheelDeployment[i].position, vc.moduleWheelDeployment[i].retractedPosition) ||
                    Mathf.Approximately(vc.moduleWheelDeployment[i].position, vc.moduleWheelDeployment[i].deployedPosition)))
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns a number representing the average position of undamaged deployable landing gear or wheels.
        /// 
        /// * 0 - No deployable gear, no undamaged gear, or all undamaged gear are retracted.
        /// * 1 - All deployable gear extended.
        /// 
        /// If the gear are moving, a number between 0 and 1 is returned.
        /// </summary>
        /// <returns>An number between 0 and 1 as described in the summary.</returns>
        public double GearPosition()
        {
            float numWheels = 0.0f;
            float lerpPosition = 0.0f;
            for (int i = vc.moduleWheelDeployment.Length - 1; i >= 0; --i)
            {
                if (!vc.moduleWheelDamage[i].isDamaged)
                {
                    numWheels += 1.0f;

                    lerpPosition += Mathf.InverseLerp(vc.moduleWheelDeployment[i].retractedPosition, vc.moduleWheelDeployment[i].deployedPosition, vc.moduleWheelDeployment[i].position);
                }
            }

            if (numWheels > 0.0f)
            {
                return lerpPosition / numWheels;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the highest stress percentage of any non-broken landing gear in the
        /// range [0, 1].
        /// </summary>
        /// <returns>Highest stress percentage, or 0 if no gear/wheels.</returns>
        public double GearStress()
        {
            float maxStress = 0.0f;
            for (int i = vc.moduleWheelDamage.Length - 1; i >= 0; --i)
            {
                if (!vc.moduleWheelDamage[i].isDamaged)
                {
                    maxStress = Mathf.Max(maxStress, vc.moduleWheelDamage[i].stressPercent);
                }
            }

            // stressPercent is a [0, 100] - convert it here for consistency
            return maxStress * 0.01f;
        }

        /// <summary>
        /// Returns 1 if the landing gear action group is active.
        /// </summary>
        /// <returns></returns>
        public double GetGear()
        {
            return (vessel.ActionGroups[KSPActionGroup.Gear]) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Set the landing gear action group to the specified state.
        /// </summary>
        /// <param name="active"></param>
        /// <returns>1 if active is true, 0 otherwise.</returns>
        public double SetGear(bool active)
        {
            vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, active);
            return (active) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Toggle the landing gear action group
        /// </summary>
        /// <returns>1 if the gear action group is active, 0 if not.</returns>
        public double ToggleGear()
        {
            vessel.ActionGroups.ToggleGroup(KSPActionGroup.Gear);
            return (vessel.ActionGroups[KSPActionGroup.Gear]) ? 1.0 : 0.0;
        }
        #endregion

        /// <summary>
        /// The Lights action group can be controlled and queried through this category.
        /// </summary>
        #region Lights
        /// <summary>
        /// Returns 1 if the Lights action group has at least one action assigned to it.
        /// </summary>
        /// <returns></returns>
        public double LightsHasActions()
        {
            return (vc.GroupHasActions(KSPActionGroup.Light)) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if the Lights action group is active.
        /// </summary>
        /// <returns>1 if the lights action group is active, 0 otherwise.</returns>
        public double GetLights()
        {
            return (vessel.ActionGroups[KSPActionGroup.Light]) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Set the state of the lights action group.
        /// </summary>
        /// <param name="active"></param>
        /// <returns>1 if the lights action group is active, 0 otherwise.</returns>
        public double SetLights(bool active)
        {
            vessel.ActionGroups.SetGroup(KSPActionGroup.Light, active);
            return (active) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Toggle the lights action group.
        /// </summary>
        /// <returns>1 if the lights action group is active, 0 otherwise.</returns>
        public double ToggleLights()
        {
            vessel.ActionGroups.ToggleGroup(KSPActionGroup.Light);
            return (vessel.ActionGroups[KSPActionGroup.Light]) ? 1.0 : 0.0;
        }
        #endregion
    }

    /// <summary>
    /// The MASProxyAttribute class is used to mark specific methods in the various
    /// proxy classes as either Immutable or Uncacheable (both would be nonsensical).
    /// 
    /// A method flagged as Immutable is evaluated once when it's created, and never
    /// again (useful for values that never change in a game session).
    /// 
    /// A method flagged as Dependent is a value that can change, but it does not need
    /// to be queried each FixedUpdate.
    /// 
    /// A method flagged as Persistent is a persistent value query.  Provided the string
    /// parameter is a constant, it will only be re-evaluated when the persistent value
    /// is updated.
    /// 
    /// A method flagged as Uncacheable is expected to change each time it's called,
    /// such as random number generators.
    /// 
    /// These attributes affect only variables that can be transformed to a
    /// native evaluator - Lua scripts are always cacheable + mutable.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class MASProxyAttribute : System.Attribute
    {
        private bool immutable;
        private bool dependent;
        private bool persistent;
        private bool uncacheable;

        public bool Immutable
        {
            get
            {
                return immutable;
            }
            set
            {
                immutable = value;
            }
        }

        public bool Dependent
        {
            get
            {
                return dependent;
            }
            set
            {
                dependent = value;
            }
        }

        public bool Persistent
        {
            get
            {
                return persistent;
            }
            set
            {
                persistent = value;
            }
        }

        public bool Uncacheable
        {
            get
            {
                return uncacheable;
            }
            set
            {
                uncacheable = value;
            }
        }
    }
}
