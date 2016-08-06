/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016 MOARdV
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
    public class MASProxyAttribute : System.Attribute
    {
        private bool immutable;
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
    internal class MASFlightComputerProxy
    {
        private MASFlightComputer fc;
        internal MASVesselComputer vc;
        internal Vessel vessel;
        private UIStateToggleButton[] SASbtns=null;

        private VesselAutopilot.AutopilotMode autopilotMode = VesselAutopilot.AutopilotMode.StabilityAssist;

        [MoonSharpHidden]
        public MASFlightComputerProxy(MASFlightComputer fc)
        {
            this.fc = fc;
        }

        ~MASFlightComputerProxy()
        {
            fc = null;
            vc = null;
            vessel = null;
        }

        /// <summary>
        /// Per-FixedUpdate updater method to read some of those values that are used a lot.
        /// </summary>
        [MoonSharpHidden]
        internal void Update()
        {
            autopilotMode = vessel.Autopilot.Mode;
        }

        #region Unassigned Region
        /// <summary>
        /// Apply a log10-like curve to the value.
        /// </summary>
        /// <param name="sourceValue"></param>
        /// <returns></returns>
        public double PseudoLog10(double sourceValue)
        {
            double absValue = Math.Abs(sourceValue);
            if (absValue <= 1.0)
            {
                return sourceValue;
            }
            else
            {
                return (1.0f + Math.Log10(absValue)) * Math.Sign(sourceValue);
            }
        }

        /// <summary>
        /// Returns a Vector2 proxy object initialized to the specified parameters.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public DynValue Vector2(double x, double y)
        {
            return UserData.Create(new MASVector2((float)x, (float)y));
        }
        #endregion

        #region Action Groups
        private static readonly KSPActionGroup[] ags = { KSPActionGroup.Custom10, KSPActionGroup.Custom01, KSPActionGroup.Custom02, KSPActionGroup.Custom03, KSPActionGroup.Custom04, KSPActionGroup.Custom05, KSPActionGroup.Custom06, KSPActionGroup.Custom07, KSPActionGroup.Custom08, KSPActionGroup.Custom09 };

        /// <summary>
        /// Returns 1 if at least one action is associated with the action
        /// group.  0 otherwise.
        /// </summary>
        /// <param name="groupID"></param>
        /// <returns></returns>
        public double ActionGroupHasActions(double groupID)
        {
            if (groupID < 0.0 || groupID > 9.0)
            {
                return 0.0;
            }
            else
            {
                return (vc.GroupHasActions(ags[(int)groupID])) ? 1.0 : 0.0;
            }
        }

        /// <summary>
        /// Get the current state of the specified action group.
        /// </summary>
        /// <param name="groupID">A number between 0 and 9 (inclusive)</param>
        /// <returns>1 if active, 0 if inactive</returns>
        public double GetActionGroup(double groupID)
        {
            if (groupID < 0.0 || groupID > 9.0)
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
        /// <param name="groupID">A number between 0 and 9 (inclusive)</param>
        /// <param name="active">true or false to set the state.</param>
        public void SetActionGroup(double groupID, bool active)
        {
            if (groupID >= 0.0 && groupID <= 9.0)
            {
                vessel.ActionGroups.SetGroup(ags[(int)groupID], active);
            }
        }

        /// <summary>
        /// Toggle the action group.
        /// </summary>
        /// <param name="groupID">A number between 0 and 9 (inclusive)</param>
        public void ToggleActionGroup(double groupID)
        {
            if (groupID >= 0.0 && groupID <= 9.0)
            {
                vessel.ActionGroups.ToggleGroup(ags[(int)groupID]);
            }
        }
        #endregion

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
        /// purpose.  Precision reporting sets in at 500m (until then, it
        /// reports the same as GetTerrainAltitude(false)).  Distance in
        /// meters.
        /// </summary>
        /// <returns></returns>
        public double AltitudeBottom()
        {
            return vc.altitudeBottom;
        }

        /// <summary>
        /// Returns the height above the terrain, optionally treating the ocean
        /// surface as ground.  Altitude in meters.
        /// </summary>
        /// <param name="ignoreOcean">When false, returns height above sea level
        /// if over the ocean; when true, always returns ground height.</param>
        /// <returns></returns>
        public double AltitudeTerrain(bool ignoreOcean)
        {
            return (ignoreOcean) ? vc.altitudeTerrain : Math.Min(vc.altitudeASL, vc.altitudeTerrain);
        }
        #endregion

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
        /// <returns></returns>
        public double StaticPressureKPa()
        {
            return vessel.staticPressurekPa;
        }
        #endregion

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
        /// Returns the current state of the Brakes action group
        /// </summary>
        /// <returns></returns>
        public double GetBrakes()
        {
            return (vessel.ActionGroups[KSPActionGroup.Brakes]) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Set the brakes to the specified state.
        /// </summary>
        /// <param name="active"></param>
        public void SetBrakes(bool active)
        {
            vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, active);
        }

        /// <summary>
        /// Toggle the state of the brakes.
        /// </summary>
        public void ToggleBrakes()
        {
            vessel.ActionGroups.ToggleGroup(KSPActionGroup.Brakes);
        }
        #endregion

        #region Engine

        /// <summary>
        /// Returns the current fuel flow in grams/second
        /// </summary>
        /// <returns></returns>
        public double GetFuelFlow()
        {
            return vc.currentEngineFuelFlow;
        }

        /// <summary>
        /// Return the current specific impulse in seconds.
        /// </summary>
        /// <returns></returns>
        public double GetIsp()
        {
            return vc.currentIsp;
        }

        /// <summary>
        /// Returns the maximum fuel flow in grams/second
        /// </summary>
        /// <returns></returns>
        public double GetMaxFuelFlow()
        {
            return vc.maxEngineFuelFlow;
        }

        /// <summary>
        /// Returns the maximum specific impulse in seconds.
        /// </summary>
        /// <returns></returns>
        public double GetMaxIsp()
        {
            return vc.maxIsp;
        }

        /// <summary>
        /// Returns the maximum thrust in kN for the current altitude.
        /// </summary>
        /// <param name="useThrottleLimits">Apply throttle limits?</param>
        /// <returns></returns>
        public double GetMaxThrustkN(bool useThrottleLimits)
        {
            return (useThrottleLimits) ? vc.currentLimitedThrust : vc.currentMaxThrust;
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
        /// Returns the current thrust output, from 0.0 to 1.0.
        /// </summary>
        /// <returns></returns>
        public double GetThrust(bool useThrottleLimits)
        {
            if (vc.currentThrust > 0.0f)
            {
                return vc.currentThrust / ((useThrottleLimits) ? vc.currentLimitedThrust : vc.currentMaxThrust);
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
        public double GetThrustkN()
        {
            return vc.currentThrust;
        }

        /// <summary>
        /// Returns the current thrust-to-weight ratio.
        /// </summary>
        /// <returns></returns>
        public double TWR()
        {
            return vc.currentThrust / (vessel.totalMass * vc.surfaceAccelerationFromGravity);
        }

        /// <summary>
        /// Returns the maximum thrust-to-weight ratio.
        /// </summary>
        /// <returns></returns>
        public double MaxTWR()
        {
            return vc.currentMaxThrust / (vessel.totalMass * vc.surfaceAccelerationFromGravity);
        }
        #endregion

        #region Gear
        /// <summary>
        /// Returns 1 if there are actions assigned to the landing gear AG.
        /// </summary>
        /// <returns></returns>
        public double GearHasActions()
        {
            return (vc.GroupHasActions(KSPActionGroup.Gear)) ? 1.0 : 0.0;

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
        public void SetGear(bool active)
        {
            vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, active);
        }

        /// <summary>
        /// Toggle the landing gear action group
        /// </summary>
        public void ToggleGear()
        {
            vessel.ActionGroups.ToggleGroup(KSPActionGroup.Gear);
        }
        #endregion

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
        /// <returns></returns>
        public double GetLights()
        {
            return (vessel.ActionGroups[KSPActionGroup.Light]) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Set the state of the lights action group.
        /// </summary>
        /// <param name="active"></param>
        public void SetLights(bool active)
        {
            vessel.ActionGroups.SetGroup(KSPActionGroup.Light, active);
        }

        /// <summary>
        /// Toggle the lights action group.
        /// </summary>
        public void ToggleLights()
        {
            vessel.ActionGroups.ToggleGroup(KSPActionGroup.Light);
        }
        #endregion

        #region Maneuver Node
        /// <summary>
        /// Delta-V of the scheduled node, or 0 if there is no node.
        /// </summary>
        /// <returns></returns>
        public double ManeuverNodeDV()
        {
            return vc.maneuverNodeDeltaV;
        }

        /// <summary>
        /// Returns 1 if there is a valid maneuver node; 0 otherwise
        /// </summary>
        /// <returns></returns>
        public double ManeuverNodeExists()
        {
            return (vc.maneuverNodeValid) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns time in seconds until the maneuver node; 0 if no node is
        /// valid.
        /// </summary>
        /// <returns></returns>
        public double ManeuverNodeTime()
        {
            return vc.maneuverNodeTime;
        }
        #endregion

        #region Mass
        /// <summary>
        /// Returns the mass of the vessel
        /// </summary>
        /// <param name="wetMass">wet mass if true, dry mass otherwise</param>
        /// <returns></returns>
        public double Mass(bool wetMass)
        {
            if (wetMass)
            {
                return vessel.totalMass;
            }
            else
            {
                return 1.0;
            }
        }
        #endregion

        #region Meta
        /// <summary>
        /// Recover the vessel if it is recoverable.
        /// </summary>
        public void RecoverVessel()
        {
            if (vessel.IsRecoverable)
            {
                GameEvents.OnVesselRecoveryRequested.Fire(vessel);
            }
        }

        /// <summary>
        /// Returns 1 if the vessel is recoverable, 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double VesselRecoverable()
        {
            return (vessel.IsRecoverable) ? 1.0 : 0.0;
        }
        #endregion

        #region Orbit Parameters
        /// <summary>
        /// Returns the orbit's apoapsis (from datum) in meters.
        /// </summary>
        /// <returns></returns>
        public double Apoapsis()
        {
            return vc.apoapsis;
        }

        /// <summary>
        /// Returns the orbits periapsis (from datum) in meters.
        /// </summary>
        /// <returns></returns>
        public double Periapsis()
        {
            return vc.periapsis;
        }
        #endregion

        #region Orientation
        /// <summary>
        /// Return heading relative to the surface in degrees [0, 360)
        /// </summary>
        /// <returns></returns>
        public double Heading()
        {
            return vc.heading;
        }

        /// <summary>
        /// Return pitch relative to the surface [-90, 90]
        /// </summary>
        /// <returns></returns>
        public double Pitch()
        {
            return vc.pitch;
        }

        /// <summary>
        /// Returns the pitch rate of the vessel in degrees/sec
        /// </summary>
        /// <returns></returns>
        public double PitchRate()
        {
            return -vessel.angularVelocity.z * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Return roll relative to the surface. [-180, 180]
        /// </summary>
        /// <returns></returns>
        public double Roll()
        {
            return vc.roll;
        }

        /// <summary>
        /// Returns the roll rate of the vessel in degrees/sec
        /// </summary>
        /// <returns></returns>
        public double RollRate()
        {
            return -vessel.angularVelocity.y * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Returns the yaw rate of the vessel in degrees/sec
        /// </summary>
        /// <returns></returns>
        public double YawRate()
        {
            return -vessel.angularVelocity.x * Mathf.Rad2Deg;
        }
        #endregion

        #region Persistent Vars
        /// <summary>
        /// Add an amount to a persistent (converting it to numeric as needed).
        /// </summary>
        /// <param name="persistentName"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public object AddPersistent(string persistentName, double amount)
        {
            return fc.AddPersistent(persistentName, amount);
        }

        /// <summary>
        /// Add an amount to a persistent, converting it to a number if needed,
        /// and clamp the result between 'minValue' and 'maxValue'
        /// </summary>
        /// <param name="persistentName"></param>
        /// <param name="amount"></param>
        /// <param name="minValue"></param>
        /// <param name="maxValue"></param>
        /// <returns></returns>
        public object AddPersistentClamped(string persistentName, double amount, double minValue, double maxValue)
        {
            return fc.AddPersistentClamped(persistentName, amount, minValue, maxValue);
        }

        /// <summary>
        /// Add an amount to a persistent, converting to a number if needed,
        /// and wrap the value between 'minValue' and 'maxValue'
        /// </summary>
        /// <param name="persistentName"></param>
        /// <param name="amount"></param>
        /// <param name="minValue"></param>
        /// <param name="maxValue"></param>
        /// <returns></returns>
        public object AddPersistentWrapped(string persistentName, double amount, double minValue, double maxValue)
        {
            return fc.AddPersistentWrapped(persistentName, amount, minValue, maxValue);
        }

        /// <summary>
        /// Return the persistent value (as a string or number, depending on
        /// its current state).
        /// </summary>
        /// <param name="persistentName"></param>
        /// <returns></returns>
        public object GetPersistent(string persistentName)
        {
            return fc.GetPersistent(persistentName);
        }

        /// <summary>
        /// Return the persistent value as a number.  If it does not exist, or
        /// it can not be converted to a number, return 0.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <returns></returns>
        public double GetPersistentAsNumber(string persistentName)
        {
            return fc.GetPersistentAsNumber(persistentName);
        }

        /// <summary>
        /// Set a persistent to either a string or numeric value.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public object SetPersistent(string persistentName, object value)
        {
            return fc.SetPersistent(persistentName, value);
        }

        /// <summary>
        /// Toggle a persistent between 0 and 1 (converting it to a number if
        /// needed).
        /// </summary>
        /// <param name="persistentName"></param>
        /// <returns></returns>
        public object TogglePersistent(string persistentName)
        {
            return fc.TogglePersistent(persistentName);
        }
        #endregion

        #region Position
        /// <summary>
        /// Return the vessel's latitude.
        /// </summary>
        /// <returns></returns>
        public double Latitude()
        {
            return vessel.latitude;
        }

        /// <summary>
        /// Return the vessel's longitude.
        /// </summary>
        /// <returns></returns>
        public double Longitude()
        {
            return vessel.longitude;
        }
        #endregion

        #region Random
        /// <summary>
        /// Return a random number in the range of [0, 1]
        /// </summary>
        /// <returns></returns>
        public double Random()
        {
            return UnityEngine.Random.value;
        }

        /// <summary>
        /// Return an approximation of a normal distribution with a mean and
        /// standard deviation as specified.  The actual result falls in the
        /// range of (-7, +7) for a standard deviation of 1.
        /// </summary>
        /// <param name="mean"></param>
        /// <param name="stdDev"></param>
        /// <returns></returns>
        public double RandomNormal(double mean, double stdDev)
        {
            // Box-Muller method tweaked to prevent a 0 in u: for a stddev of 1
            // the range is (-7, 7).
            float u = UnityEngine.Random.Range(0.0009765625f, 1.0f);
            float v = UnityEngine.Random.Range(0.0f, 2.0f * Mathf.PI);
            double x = Mathf.Sqrt(-2.0f * Mathf.Log(u)) * Mathf.Cos(v) * stdDev;
            return x + mean;
        }
        #endregion

        #region RCS
        /// <summary>
        /// Returns 1 if the RCS action group has any actions attached to it.
        /// </summary>
        /// <returns></returns>
        public double RCSHasActions()
        {
            return (vc.GroupHasActions(KSPActionGroup.RCS)) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if RCS is on, 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double GetRCS()
        {
            return (vessel.ActionGroups[KSPActionGroup.RCS]) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Set the state of RCS.
        /// </summary>
        /// <param name="active"></param>
        public void SetRCS(bool active)
        {
            vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, active);
        }

        /// <summary>
        /// Toggle RCS off-to-on or vice versa.
        /// </summary>
        public void ToggleRCS()
        {
            vessel.ActionGroups.ToggleGroup(KSPActionGroup.RCS);
        }
        #endregion RCS

        #region Resources
        /// <summary>
        /// Returns the total number of resources found on this vessel.
        /// </summary>
        /// <returns></returns>
        public double ResourceCount()
        {
            return vc.ResourceCount();
        }

        /// <summary>
        /// Returns the current amount of the Nth resource from a name-sorted
        /// list of resources.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public double ResourceCurrent(double resourceId)
        {
            return vc.ResourceCurrent((int)resourceId);
        }

        /// <summary>
        /// Return the current amount of the named resource, or zero if the
        /// resource does not exist.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public double ResourceCurrent(string resourceName)
        {
            return vc.ResourceCurrent(resourceName);
        }

        /// <summary>
        /// Returns the instantaneous change-per-second of the Nth resource,
        /// or zero if the Nth resource is invalid.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public double ResourceDelta(double resourceId)
        {
            return vc.ResourceDelta((int)resourceId);
        }

        /// <summary>
        /// Returns the instantaneous change-per-second of the resource, or
        /// zero if the resource wasn't found.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public double ResourceDelta(string resourceName)
        {
            return vc.ResourceDelta(resourceName);
        }

        /// <summary>
        /// Returns the density of the Nth resource, or zero if it is invalid.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public double ResourceDensity(double resourceId)
        {
            return vc.ResourceDensity((int)resourceId);
        }

        /// <summary>
        /// Returns the density of the named resource, or zero if it wasn't found.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public double ResourceDensity(string resourceName)
        {
            return vc.ResourceDensity(resourceName);
        }

        /// <summary>
        /// Returns 1 if resourceId is valid (there is a resource with that
        /// index on the craft).
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public double ResourceExists(double resourceId)
        {
            return vc.ResourceExists((int)resourceId);
        }

        /// <summary>
        /// Returns 1 if the named resource is valid (the vessel has storage for
        /// that resource).
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public double ResourceExists(string resourceName)
        {
            return vc.ResourceExists(resourceName);
        }

        /// <summary>
        /// Returns the current mass of the Nth resource.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public double ResourceMass(double resourceId)
        {
            return vc.ResourceMass((int)resourceId);
        }

        /// <summary>
        /// Returns the mass of the current resource supply
        /// in (units).
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public double ResourceMass(string resourceName)
        {
            return vc.ResourceMass(resourceName);
        }

        /// <summary>
        /// Returns the maximum mass of the Nth resource.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public double ResourceMassMax(double resourceId)
        {
            return vc.ResourceMassMax((int)resourceId);
        }

        /// <summary>
        /// Returns the maximum mass of the resource in (units).
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public double ResourceMassMax(string resourceName)
        {
            return vc.ResourceMassMax(resourceName);
        }

        /// <summary>
        /// Returns the maximum quantity of the Nth resource.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public double ResourceMax(double resourceId)
        {
            return vc.ResourceMax((int)resourceId);
        }

        /// <summary>
        /// Return the maximum capacity of the resource, or zero if the resource
        /// doesn't exist.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public double ResourceMax(string resourceName)
        {
            return vc.ResourceMax(resourceName);
        }

        /// <summary>
        /// Returns the name of the Nth resource, or an empty string if it doesn't
        /// exist.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public string ResourceName(double resourceId)
        {
            return vc.ResourceName((int)resourceId);
        }

        /// <summary>
        /// Returns the amount of the Nth resource remaining as a percentage in
        /// the range [0, 1].
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public double ResourcePercent(double resourceId)
        {
            return vc.ResourcePercent((int)resourceId);
        }

        /// <summary>
        /// Returns the amount of the resource remaining as a percentage in the
        /// range [0, 1].
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public double ResourcePercent(string resourceName)
        {
            return vc.ResourcePercent(resourceName);
        }

        /// <summary>
        /// Returns the current amount of the Nth resource in the current stage.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public double ResourceStageCurrent(double resourceId)
        {
            return vc.ResourceCurrent((int)resourceId);
        }

        /// <summary>
        /// Returns the amount of the resource remaining in the current stage.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public double ResourceStageCurrent(string resourceName)
        {
            return vc.ResourceStageCurrent(resourceName);
        }

        /// <summary>
        /// Returns the max amount of the Nth resource in the current stage.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public double ResourceStageMax(double resourceId)
        {
            return vc.ResourceStageMax((int)resourceId);
        }

        /// <summary>
        /// Returns the maximum amount of the resource in the current stage.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public double ResourceStageMax(string resourceName)
        {
            return vc.ResourceStageMax(resourceName);
        }

        /// <summary>
        /// Returns 1 when there is at least 0.0001 units of power available
        /// to the craft.  By default, 'power' is the ElectricCharge resource,
        /// but users may change that in the MAS config file.
        /// </summary>
        /// <returns></returns>
        public double VesselPowered()
        {
            return (vc.ResourceCurrent(MASLoader.ElectricCharge) > 0.0001) ? 1.0 : 0.0;
        }
        #endregion

        #region SAS
        /// <summary>
        /// Returns 1 if the vessel is in precision control mode, 0 otherwise
        /// </summary>
        /// <returns></returns>
        public double GetPrecisionMode()
        {
            return (FlightInputHandler.fetch.precisionMode) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if SAS is on, 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double GetSAS()
        {
            return (vessel.ActionGroups[KSPActionGroup.SAS]) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if SAS is currently set for anti-normal.
        /// </summary>
        /// <returns></returns>
        public double GetSASModeAntiNormal()
        {
            return (autopilotMode == VesselAutopilot.AutopilotMode.Antinormal) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if SAS is currently set for anti-target.
        /// </summary>
        /// <returns></returns>
        public double GetSASModeAntiTarget()
        {
            return (autopilotMode == VesselAutopilot.AutopilotMode.AntiTarget) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if SAS is currently set for maneuver.
        /// </summary>
        /// <returns></returns>
        public double GetSASModeManeuver()
        {
            return (autopilotMode == VesselAutopilot.AutopilotMode.Maneuver) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if SAS is currently set for normal.
        /// </summary>
        /// <returns></returns>
        public double GetSASModeNormal()
        {
            return (autopilotMode == VesselAutopilot.AutopilotMode.Normal) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if SAS is currently set for prograde.
        /// </summary>
        /// <returns></returns>
        public double GetSASModePrograde()
        {
            return (autopilotMode == VesselAutopilot.AutopilotMode.Prograde) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if SAS is currently set for radial in.
        /// </summary>
        /// <returns></returns>
        public double GetSASModeRadialIn()
        {
            return (autopilotMode == VesselAutopilot.AutopilotMode.RadialIn) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if SAS is currently set for radial out.
        /// </summary>
        /// <returns></returns>
        public double GetSASModeRadialOut()
        {
            return (autopilotMode == VesselAutopilot.AutopilotMode.RadialOut) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if SAS is currently set for retrograde.
        /// </summary>
        /// <returns></returns>
        public double GetSASModeRetrograde()
        {
            return (autopilotMode == VesselAutopilot.AutopilotMode.Retrograde) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if SAS is currently set for stability assist.
        /// </summary>
        /// <returns></returns>
        public double GetSASModeStabilityAssist()
        {
            return (autopilotMode == VesselAutopilot.AutopilotMode.StabilityAssist) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if SAS is currently set for target +.
        /// </summary>
        /// <returns></returns>
        public double GetSASModeTarget()
        {
            return (autopilotMode == VesselAutopilot.AutopilotMode.Target) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Return the current speed display mode: 1 for orbit, 0 for surface,
        /// and -1 for target.
        /// </summary>
        /// <returns></returns>
        public double GetSASSpeedMode()
        {
            var mode = FlightGlobals.speedDisplayMode;

            if(mode == FlightGlobals.SpeedDisplayModes.Orbit)
            {
                return 1.0;
            }
            else if(mode == FlightGlobals.SpeedDisplayModes.Target)
            {
                return -1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if the SAS action group has actions assigned to it.
        /// </summary>
        /// <returns></returns>
        public double SASHasActions()
        {
            return (vc.GroupHasActions(KSPActionGroup.SAS)) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Set the SAS state to on or off per the parameter.
        /// </summary>
        /// <param name="active"></param>
        public void SetSAS(bool active)
        {
            vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, active);
        }

        /// <summary>
        /// Sets SAS mode to anti-normal.
        /// </summary>
        public void SetSASModeAntiNormal()
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.Antinormal))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Antinormal);
                UpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.Antinormal);
            }
        }

        /// <summary>
        /// Sets SAS mode to anti-target.
        /// </summary>
        public void SetSASModeAntiTarget()
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.AntiTarget))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.AntiTarget);
                UpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.AntiTarget);
            }
        }

        /// <summary>
        /// Sets SAS mode to maneuver.
        /// </summary>
        public void SetSASModeManeuver()
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.Maneuver))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Maneuver);
                UpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.Maneuver);
            }
        }

        /// <summary>
        /// Sets SAS mode to normal.
        /// </summary>
        public void SetSASModeNormal()
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.Normal))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Normal);
                UpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.Normal);
            }
        }

        /// <summary>
        /// Sets SAS mode to prograde.
        /// </summary>
        public void SetSASModePrograde()
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.Prograde))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Prograde);
                UpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.Prograde);
            }
        }

        /// <summary>
        /// Sets SAS mode to radial in.
        /// </summary>
        public void SetSASModeRadialIn()
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.RadialIn))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.RadialIn);
                UpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.RadialIn);
            }
        }

        /// <summary>
        /// Sets SAS mode to radial out.
        /// </summary>
        public void SetSASModeRadialOut()
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.RadialOut))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.RadialOut);
                UpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.RadialOut);
            }
        }

        /// <summary>
        /// Sets SAS mode to retrograde.
        /// </summary>
        public void SetSASModeRetrograde()
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.Retrograde))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Retrograde);
                UpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.Retrograde);
            }
        }

        /// <summary>
        /// Sets SAS mode to stability assist.
        /// </summary>
        public void SetSASModeStabilityAssist()
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.StabilityAssist))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.StabilityAssist);
                UpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.StabilityAssist);
            }
        }

        /// <summary>
        /// Sets SAS mode to target +.
        /// </summary>
        public void SetSASModeTarget()
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.Target))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Target);
                UpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.Target);
            }
        }

        /// <summary>
        /// Toggle precision control mode
        /// </summary>
        public void TogglePrecisionMode()
        {
            bool state = !FlightInputHandler.fetch.precisionMode;

            FlightInputHandler.fetch.precisionMode = state;

            var gauges = UnityEngine.Object.FindObjectOfType<KSP.UI.Screens.Flight.LinearControlGauges>();
            if (gauges != null)
            {
                for (int i = gauges.inputGaugeImages.Count - 1; i >= 0; --i)
                {
                    gauges.inputGaugeImages[i].color = (state) ? XKCDColors.BrightCyan : XKCDColors.Orange;
                }
            }

        }

        /// <summary>
        /// Toggles SAS on-to-off or vice-versa
        /// </summary>
        public void ToggleSAS()
        {
            vessel.ActionGroups.ToggleGroup(KSPActionGroup.SAS);
        }

        /// <summary>
        /// Toggles the SAS speed mode.
        /// </summary>
        public void ToggleSASSpeedMode()
        {
            FlightGlobals.CycleSpeedModes();
        }

        /// <summary>
        /// Internal method to update the mode buttons in the UI.
        /// TODO: Radial Out / Radial In may be backwards (either in the display,
        /// or in the enums).
        /// </summary>
        /// <param name="newMode"></param>
        private void UpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode newMode)
        {
            // find the UI object on screen
            if (SASbtns == null)
            {
                SASbtns = UnityEngine.Object.FindObjectOfType<VesselAutopilotUI>().modeButtons;
            }
            // set our mode, note it takes the mode as an int, generally top to bottom, left to right, as seen on the screen. Maneuver node being the exception, it is 9
            SASbtns[(int)newMode].SetState(true);
        }
        #endregion

        #region Speed, Velocity, and Acceleration
        public double Acceleration()
        {
            return 0.0;
        }

        /// <summary>
        /// Compute equivalent airspeed.
        /// 
        /// https://en.wikipedia.org/wiki/Equivalent_airspeed
        /// </summary>
        /// <returns></returns>
        public double EquivalentAirspeed()
        {
            double densityRatio = vessel.atmDensity / 1.225;
            return vessel.srfSpeed * Math.Sqrt(densityRatio);
        }

        /// <summary>
        /// Returns the magnitude g-forces currently affecting the craft, in gees.
        /// </summary>
        /// <returns></returns>
        public double GForce()
        {
            return vessel.geeForce_immediate;
        }

        /// <summary>
        /// Measure of the surface speed of the vessel after removing the
        /// vertical component, in m/s.
        /// </summary>
        /// <returns></returns>
        public double HorizontalSpeed()
        {
            double speedHorizontal;
            if (Math.Abs(vessel.verticalSpeed) < Math.Abs(vessel.srfSpeed))
            {
                speedHorizontal = Math.Sqrt(vessel.srfSpeed * vessel.srfSpeed - vessel.verticalSpeed * vessel.verticalSpeed);
            }
            else
            {
                speedHorizontal = 0.0;
            }

            return speedHorizontal;
        }

        /// <summary>
        /// Returns the indicated airspeed in m/s.
        /// </summary>
        /// <returns></returns>
        public double IndicatedAirspeed()
        {
            double densityRatio = vessel.atmDensity / 1.225;
            double pressureRatio = Utility.StagnationPressure(vc.mainBody.atmosphereAdiabaticIndex, vessel.mach);
            return vessel.srfSpeed * Math.Sqrt(densityRatio) * pressureRatio;
        }

        /// <summary>
        /// Return the orbital speed of the vessel in m/s
        /// </summary>
        /// <returns></returns>
        public double OrbitSpeed()
        {
            return vessel.obt_speed;
        }

        /// <summary>
        /// Returns +1 if the KSP automatic speed display is set to "Orbit",
        /// +0 if it's "Surface", and -1 if it's "Target".
        /// </summary>
        /// <returns></returns>
        public double SpeedDisplayMode()
        {
            var displayMode = FlightGlobals.speedDisplayMode;
            if (displayMode == FlightGlobals.SpeedDisplayModes.Orbit)
            {
                return 1.0;
            }
            else if (displayMode == FlightGlobals.SpeedDisplayModes.Surface)
            {
                return 0.0;
            }
            else
            {
                return -1.0;
            }
        }

        /// <summary>
        /// Return the surface-relative speed of the vessel in m/s.
        /// </summary>
        /// <returns></returns>
        public double SurfaceSpeed()
        {
            return vessel.srfSpeed;
        }

        /// <summary>
        /// Returns the vertical speed of the vessel in m/s.
        /// </summary>
        /// <returns></returns>
        public double VerticalSpeed()
        {
            return vessel.verticalSpeed;
        }
        #endregion
    }
}
