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

        #region Unassigned Region
        public double GetGForce()
        {
            // TODO: Implement this.
            return 1.0;
        }

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
        #endregion

        #region Action Groups
        private static readonly KSPActionGroup[] ags = { KSPActionGroup.Custom10, KSPActionGroup.Custom01, KSPActionGroup.Custom02, KSPActionGroup.Custom03, KSPActionGroup.Custom04, KSPActionGroup.Custom05, KSPActionGroup.Custom06, KSPActionGroup.Custom07, KSPActionGroup.Custom08, KSPActionGroup.Custom09 };
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

        public void SetActionGroup(double groupID, bool active)
        {
            if (groupID >= 0.0 && groupID <= 9.0)
            {
                vessel.ActionGroups.SetGroup(ags[(int)groupID], active);
            }
        }

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
        public double GetBrakes()
        {
            return (vessel.ActionGroups[KSPActionGroup.Brakes]) ? 1.0 : 0.0;
        }

        public void SetBrakes(bool active)
        {
            vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, active);
        }

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
        #endregion

        #region Gear
        public double GetGear()
        {
            return (vessel.ActionGroups[KSPActionGroup.Gear]) ? 1.0 : 0.0;
        }

        public void SetGear(bool active)
        {
            vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, active);
        }

        public void ToggleGear()
        {
            vessel.ActionGroups.ToggleGroup(KSPActionGroup.Gear);
        }
        #endregion

        #region Lights
        public double GetLights()
        {
            return (vessel.ActionGroups[KSPActionGroup.Light]) ? 1.0 : 0.0;
        }

        public void SetLights(bool active)
        {
            vessel.ActionGroups.SetGroup(KSPActionGroup.Light, active);
        }

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
        public object AddPersistent(string persistentName, double amount)
        {
            return fc.AddPersistent(persistentName, amount);
        }

        public object GetPersistent(string persistentName)
        {
            return fc.GetPersistent(persistentName);
        }

        public object SetPersistent(string persistentName, object value)
        {
            return fc.SetPersistent(persistentName, value);
        }

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
        public double GetRandom()
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
        public double GetRandomNormal(double mean, double stdDev)
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
        public double GetRCS()
        {
            return (vessel.ActionGroups[KSPActionGroup.RCS]) ? 1.0 : 0.0;
        }

        public void SetRCS(bool active)
        {
            vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, active);
        }

        public void ToggleRCS()
        {
            vessel.ActionGroups.ToggleGroup(KSPActionGroup.RCS);
        }
        #endregion RCS

        #region Resources
        public double GetPowered()
        {
            // TODO: Implement this...
            return 1.0;
        }
        #endregion

        #region SAS
        public double GetSAS()
        {
            return (vessel.ActionGroups[KSPActionGroup.SAS]) ? 1.0 : 0.0;
        }

        public void SetSAS(bool active)
        {
            vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, active);
        }

        public void ToggleSAS()
        {
            vessel.ActionGroups.ToggleGroup(KSPActionGroup.SAS);
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
