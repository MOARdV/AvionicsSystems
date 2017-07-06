/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2017 MOARdV
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
    /// *NOTE:* If a variable listed below includes an entry for 'Required Mod(s)',
    /// then the mod listed (or any of the mods, if more than one) must be installed
    /// for that particular feature to work.
    /// </mdDoc>
    internal class MASFlightComputerProxy
    {
        internal const double KelvinToCelsius = -273.15;

        private MASFlightComputer fc;
        internal MASVesselComputer vc;
        internal MASIFAR farProxy;
        internal MASIMechJeb mjProxy;
        internal Vessel vessel;
        private UIStateToggleButton[] SASbtns = null;

        private VesselAutopilot.AutopilotMode autopilotMode = VesselAutopilot.AutopilotMode.StabilityAssist;
        private bool vesselPowered;
        private int vesselSituationConverted;

        [MoonSharpHidden]
        public MASFlightComputerProxy(MASFlightComputer fc, MASIFAR farProxy, MASIMechJeb mjProxy)
        {
            this.fc = fc;
            this.farProxy = farProxy;
            this.mjProxy = mjProxy;
        }

        ~MASFlightComputerProxy()
        {
            fc = null;
            vc = null;
            farProxy = null;
            mjProxy = null;
            vessel = null;
            SASbtns = null;
        }

        /// <summary>
        /// Per-FixedUpdate updater method to read some of those values that are used a lot.
        /// </summary>
        [MoonSharpHidden]
        internal void Update()
        {
            autopilotMode = vessel.Autopilot.Mode;
            vesselPowered = (vc.ResourceCurrent(MASConfig.ElectricCharge) > 0.0001);

            int situation = (int)vessel.situation;
            for (int i = 0; i < 0x10; ++i)
            {
                if ((situation & (1 << i)) != 0)
                {
                    vesselSituationConverted = i;
                    break;
                }
            }

            for (int i = neighboringVessels.Length - 1; i >= 0; --i)
            {
                neighboringVessels[i] = null;
            }
            neighboringVesselsCurrent = false;
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
                if (idx == -1)
                {
                    cb = vessel.mainBody;
                }
                else if (idx == -2)
                {
                    if (vc.targetType == MASVesselComputer.TargetType.CelestialBody)
                    {
                        cb = vc.activeTarget as CelestialBody;
                    }
                }
                else if (idx >= 0 && idx < FlightGlobals.Bodies.Count)
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
        private bool neighboringVesselsCurrent = false;

        [MoonSharpHidden]
        private void UpdateNeighboringVessels()
        {
            if (!neighboringVesselsCurrent)
            {
                // Populate 
                var allVessels = FlightGlobals.fetch.vessels;
                int allVesselCount = allVessels.Count;
                int localVesselCount = 0;
                CelestialBody mainBody = vessel.mainBody;
                for (int i = 0; i < allVesselCount; ++i)
                {
                    Vessel v = allVessels[i];
                    if (v.mainBody == mainBody && v.vesselType != global::VesselType.Debris)
                    {
                        ++localVesselCount;
                    }
                }

                --localVesselCount;

                if (neighboringVessels.Length != localVesselCount)
                {
                    neighboringVessels = new Vessel[localVesselCount];
                }

                int arrayIndex = 0;
                for (int i = 0; i < allVesselCount; ++i)
                {
                    Vessel v = allVessels[i];
                    if (v.mainBody == mainBody && v.vesselType != global::VesselType.Debris && v != vessel)
                    {
                        neighboringVessels[arrayIndex++] = v;
                    }
                }

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
        /// Variables that have not been assigned to a different category are
        /// dumped in this region until I figured out where to put them.
        /// </summary>
        #region Unassigned Region

        /// <summary>
        /// Returns 1 if `value` is at least equal to `lowerBound` and not greater
        /// than `upperBound`.  Returns 0 otherwise.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <param name="lowerBound">The lower bound of the range to test.</param>
        /// <param name="upperBound">The upper bound of the range to test.</param>
        /// <returns>1 if `value` is between `lowerBound` and `upperBound`, 0 otherwise.</returns>
        public double Between(double value, double lowerBound, double upperBound)
        {
            return (value >= lowerBound && value <= upperBound) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Remaps `value` from the range [`bound1`, `bound2`] to the range
        /// [`map1`, `map2`].
        /// 
        /// The order of the bound and map parameters will be interpreted
        /// correctly.  For instance, `fc.Remap(var, 1, 0, 0, 1)` will
        /// have the same effect as `1 - var`.
        /// </summary>
        /// <param name="value">An input number</param>
        /// <param name="bound1">One of the two bounds of the source range.</param>
        /// <param name="bound2">The other bound of the source range.</param>
        /// <param name="map1">The first value of the destination range.</param>
        /// <param name="map2">The second value of the destination range.</param>
        /// <returns></returns>
        public double Remap(double value, double bound1, double bound2, double map1, double map2)
        {
            return value.Remap(bound1, bound2, map1, map2);
        }

        /// <summary>
        /// Returns a Vector2 proxy object initialized to the specified parameters.
        /// </summary>
        /// <param name="x">The X parameter of the vector</param>
        /// <param name="y">The Y parameter of the vector</param>
        /// <returns>An object that represents a two-element vector.</returns>
        public DynValue Vector2(double x, double y)
        {
            return UserData.Create(new MASVector2((float)x, (float)y));
        }
        #endregion

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
        /// <param name="groupID">A number between 0 and 9 (inclusive).</param>
        /// <returns>1 if there are actions for this action group, 0 otherwise.</returns>
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
        /// Returns the terrain height relative to the planet's datum (sea
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
        /// Returns 1 if the body the vessel is orbiting has an atmosphere.
        /// </summary>
        /// <returns>1 if there is an atmosphere, 0 otherwise.</returns>
        public double HasAtmosphere()
        {
            return (vc.mainBody.atmosphere) ? 1.0 : 0.0;
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
        /// `fc.BodyIndex`, or one of two special values may be used:
        /// 
        /// * -1: Return information about the body the vessel is currently
        /// orbiting.
        /// * -2: Return information about the body currently being targeted.
        /// 
        /// Obviously, if no body is being targted, no data will be returned
        /// when -2 is used.
        /// </summary>
        #region Body
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

        /// <summary>
        /// Returns the numeric identifier for the named body.  If the name is invalid
        /// (no such body exists), returns -1.  May also use the index, which is useful
        /// for -1 and -2.
        /// </summary>
        /// <param name="bodyName">The name of the body, eg. `"Kerbin"` or one of the indices (including -1 and -2).</param>
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

        /// <summary>
        /// Returns the number of worlds orbiting the selected body.  If the body
        /// is a planet, this is the number of moons.  If the body is the Sun, this
        /// number is the number of planets.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>The number of moons, or 0 if an invalid value was provided.</returns>
        public double BodyNumMoons(object id)
        {
            CelestialBody cb = SelectBody(id);
            return (cb != null) ? cb.orbitingBodies.Count : 0.0;
        }

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

        /// <summary>
        /// The methods in this section are focused around controlling external
        /// cameras installed on the vessel.  They provide an interface between
        /// the MASCamera part module and CAMERA nodes in a monitor page.
        /// </summary>
        #region Cameras

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
                pan = vc.moduleCamera[i].currentPan;
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
                tilt = vc.moduleCamera[i].currentTilt;
            }

            return tilt;
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
            try
            {
                return vessel.connection.ControlPath.Last.b.name;
            }
            catch { }
            return string.Empty;
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
        /// Returns the signal strength of the CommNet signal.
        /// </summary>
        /// <returns>A value between 0 (no signal) and 1 (maximum signal strength).</returns>
        public double CommNetSignalStrength()
        {
            return vessel.connection.SignalStrength;
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
        /// Most functions in this category require two parameters: `localSeat` and
        /// `seatNumber`.
        /// 
        /// `localSeat` is a boolean.  When it is true, the only seats
        /// that are considered for the function are the seats local to the current
        /// pod.  When it is false, all seats on the vessel are considered.
        /// 
        /// `seatNumber` is a 0-based index to select which seat is being queried.  
        /// For local seats, this
        /// means that a 3-seat pod has valid seat numbers 0, 1, and 2.  A single-seat
        /// pod as a valid seat number 0.  When `localSeat` is false, this means that
        /// any number between 0 and fc.NumberSeats(false) - 1 may be used.
        /// </summary>
        #region Crew
        /// <summary>
        /// **INCOMPLETE:** Full-vessel crew information (`localSeat` = false)
        /// is not yet implemented.
        /// 
        /// Returns 1 if the crew in `seatNumber` has the 'BadS' trait.  Returns 0 if
        /// `seatNumber` is invalid or there is no crew in that seat, or the crew does
        /// not possess the 'BadS' trait.
        /// </summary>
        /// <param name="localSeat">When `true`, only seats in the current IVA pod are considered;
        /// when `false`, all seats in the current vessel are considered.</param>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>1 or 0 (see summary)</returns>
        public double CrewBadS(bool localSeat, double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            if (localSeat)
            {
                return (seatIdx < fc.localCrew.Length && fc.localCrew[seatIdx] != null && fc.localCrew[seatIdx].isBadass) ? 1.0 : 0.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// **INCOMPLETE:** Full-vessel crew information (`localSeat` = false)
        /// is not yet implemented.
        /// 
        /// Returns the number of experience points for the selected crew member.
        /// </summary>
        /// <param name="localSeat">When `true`, only seats in the current IVA pod are considered;
        /// when `false`, all seats in the current vessel are considered.</param>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>A number 0 or higher; 0 if the requested seat is invalid or empty.</returns>
        public double CrewExperience(bool localSeat, double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            if (localSeat)
            {
                if (seatIdx < fc.localCrew.Length && fc.localCrew[seatIdx] != null)
                {
                    return fc.localCrew[seatIdx].experience;
                }
            }
            else
            {
            }

            return 0.0;
        }

        /// <summary>
        /// **INCOMPLETE:** Full-vessel crew information (`localSeat` = false)
        /// is not yet implemented.
        /// 
        /// Returns the experience level of the selected crew member.
        /// </summary>
        /// <param name="localSeat">When `true`, only seats in the current IVA pod are considered;
        /// when `false`, all seats in the current vessel are considered.</param>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>A number 0-5; 0 if the requested seat is invalid or empty.</returns>
        public double CrewLevel(bool localSeat, double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            if (localSeat)
            {
                if (seatIdx < fc.localCrew.Length && fc.localCrew[seatIdx] != null)
                {
                    return fc.localCrew[seatIdx].experienceLevel;
                }
            }
            else
            {
            }

            return 0.0;
        }

        /// <summary>
        /// **INCOMPLETE:** Full-vessel crew information (`localSeat` = false)
        /// is not yet implemented.
        /// 
        /// Returns the name of the crew member seated in `seatNumber`.  If
        /// the number is invalid, or no Kerbal is in the seat, returns an
        /// empty string.
        /// </summary>
        /// <param name="localSeat">When `true`, only seats in the current IVA pod are considered;
        /// when `false`, all seats in the current vessel are considered.</param>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>The crew name, or an empty string if there is no crew in the
        /// given seat.</returns>
        public string CrewName(bool localSeat, double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            if (localSeat)
            {
                if (seatIdx < fc.localCrew.Length && fc.localCrew[seatIdx] != null)
                {
                    return fc.localCrew[seatIdx].name;
                }
            }
            else
            {
            }

            return string.Empty;
        }

        /// <summary>
        /// **INCOMPLETE:** Full-vessel crew information (`localSeat` = false)
        /// is not yet implemented.
        /// 
        /// Returns the 'PANIC' level of the selected crew member.
        /// </summary>
        /// <param name="localSeat">When `true`, only seats in the current IVA pod are considered;
        /// when `false`, all seats in the current vessel are considered.</param>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>A number between 0 and 1; 0 if the requested seat is invalid or empty.</returns>
        public double CrewPanic(bool localSeat, double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            if (localSeat)
            {
                if (seatIdx < fc.localCrewMedical.Length && fc.localCrewMedical[seatIdx] != null)
                {
                    return fc.localCrewMedical[seatIdx].panicLevel;
                }
            }
            else
            {
            }

            return 0.0;
        }

        /// <summary>
        /// **INCOMPLETE:** Full-vessel crew information (`localSeat` = false)
        /// is not yet implemented.
        /// 
        /// Returns the stupidity rating of the selected crew member.
        /// </summary>
        /// <param name="localSeat">When `true`, only seats in the current IVA pod are considered;
        /// when `false`, all seats in the current vessel are considered.</param>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>A number between 0 and 1; 0 if the requested seat is invalid or empty.</returns>
        public double CrewStupidity(bool localSeat, double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            if (localSeat)
            {
                if (seatIdx < fc.localCrew.Length && fc.localCrew[seatIdx] != null)
                {
                    return fc.localCrew[seatIdx].stupidity;
                }
            }
            else
            {
            }

            return 0.0;
        }

        /// <summary>
        /// **INCOMPLETE:** Full-vessel crew information (`localSeat` = false)
        /// is not yet implemented.
        /// 
        /// Returns the job title of the selected crew member.
        /// </summary>
        /// <param name="localSeat">When `true`, only seats in the current IVA pod are considered;
        /// when `false`, all seats in the current vessel are considered.</param>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>The name of the job title, or an empty string if `seatNumber` is invalid or
        /// unoccupied.</returns>
        public string CrewTitle(bool localSeat, double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            if (localSeat)
            {
                if (seatIdx < fc.localCrew.Length && fc.localCrew[seatIdx] != null)
                {
                    return fc.localCrew[seatIdx].experienceTrait.Title;
                }
            }
            else
            {
            }

            return string.Empty;
        }

        /// <summary>
        /// **INCOMPLETE:** Full-vessel crew information (`localSeat` = false)
        /// is not yet implemented.
        /// 
        /// Returns the 'WHEE' level of the selected crew member.
        /// </summary>
        /// <param name="localSeat">When `true`, only seats in the current IVA pod are considered;
        /// when `false`, all seats in the current vessel are considered.</param>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>A number between 0 and 1; 0 if the requested seat is invalid or empty.</returns>
        public double CrewWhee(bool localSeat, double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            if (localSeat)
            {
                if (seatIdx < fc.localCrewMedical.Length && fc.localCrewMedical[seatIdx] != null)
                {
                    return fc.localCrewMedical[seatIdx].wheeLevel;
                }
            }
            else
            {
            }

            return 0.0;
        }

        /// <summary>
        /// **INCOMPLETE:** Full-vessel crew information (`localSeat` = false)
        /// is not yet implemented.
        /// 
        /// Returns the number of seats in the current IVA pod or the overall
        /// vessel, depending on whether `localSeat` is true or false.
        /// </summary>
        /// <param name="localSeat">When `true`, the number of seats in the current IVA pod is returned;
        /// when `false`, the total number of seats in the current vessel is returned.</param>
        /// <returns>The selected number of seats (1 or more).</returns>
        public double NumberSeats(bool localSeat)
        {
            if (localSeat)
            {
                return fc.localCrew.Length;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// **INCOMPLETE:** Full-vessel crew information (`localSeat` = false)
        /// is not yet implemented.
        /// 
        /// Returns 1 if the `seatNumber` refers to a valid seat index.  0 otherwise.
        /// </summary>
        /// <param name="localSeat">When `true`, only seats in the current IVA pod are considered;
        /// when `false`, all seats in the current vessel are considered.</param>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>1 if `seatNumber` is a valid seat; 0 otherwise.</returns>
        public double SeatExists(bool localSeat, double seatNumber)
        {
            if (localSeat)
            {
                return ((int)seatNumber < fc.localCrew.Length) ? 1.0 : 0.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// **INCOMPLETE:** Full-vessel crew information (`localSeat` = false)
        /// is not yet implemented.
        /// 
        /// Indicates whether a given seat is occupied by a Kerbal.  Returns 1 when `seatNumber` is
        /// valid and there is a Kerbal in the given seat, and returns 0 in all other instances.
        /// </summary>
        /// <param name="localSeat">When `true`, only seats in the current IVA pod are considered;
        /// when `false`, all seats in the current vessel are considered.</param>
        /// <param name="seatNumber">The index of the seat to check.  Indices start at 0.</param>
        /// <returns>1 if `seatNumber` is a valid seat; 0 otherwise.</returns>
        public double SeatOccupied(bool localSeat, double seatNumber)
        {
            int seatIdx = (int)seatNumber;
            if (localSeat)
            {
                return (seatIdx < fc.localCrew.Length && fc.localCrew[seatIdx] != null) ? 1.0 : 0.0;
            }
            else
            {
                return 0.0;
            }
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
        /// Return 1 if the dock is attached to something (either in-flight
        /// docking or attached in the VAB); return 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double Docked()
        {
            return (vc.dockingNodeState == MASVesselComputer.DockingNodeState.DOCKED || vc.dockingNodeState == MASVesselComputer.DockingNodeState.PREATTACHED) ? 1.0 : 0.0;
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
        /// Set the primary docking port to be the reference transform.
        /// </summary>
        public void SetDockToReference()
        {
            if (vc.dockingNode != null)
            {
                vessel.SetReferenceTransform(vc.dockingNode.part);
            }
        }

        /// <summary>
        /// Set the current IVA pod to be the reference transform.
        /// </summary>
        public void SetPodToReference()
        {
            vessel.SetReferenceTransform(fc.part);
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
            return vc.currentThrust / (vessel.totalMass * vc.surfaceAccelerationFromGravity);
        }

        /// <summary>
        /// If MechJeb is installed, returns the total delta-V remaining for the vessel.
        /// 
        /// Otherwise, 0 is returned.
        /// </summary>
        /// <seealso>MechJeb</seealso>
        /// <returns>Remaining delta-V in m/s.</returns>
        public double DeltaV()
        {
            if (mjProxy.mjAvailable)
            {
                return mjProxy.DeltaV();
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// If MechJeb is installed, returns the total delta-V remaining for the current stage.
        /// 
        /// Otherwise, 0 is returned.
        /// </summary>
        /// <seealso>MechJeb</seealso>
        /// <returns>Remaining delta-V for this stage in m/s.</returns>
        public double DeltaVStage()
        {
            if (mjProxy.mjAvailable)
            {
                return mjProxy.StageDeltaV();
            }
            else
            {
                return 0.0;
            }
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
        /// <returns></returns>
        public double MaxThrustkN(bool useThrottleLimits)
        {
            return (useThrottleLimits) ? vc.currentLimitedMaxThrust : vc.currentMaxThrust;
        }

        /// <summary>
        /// Returns the maximum thrust-to-weight ratio.
        /// </summary>
        /// <returns></returns>
        public double MaxTWR()
        {
            return vc.currentMaxThrust / (vessel.totalMass * vc.surfaceAccelerationFromGravity);
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
                Utility.LogErrorMessage(this, "SetThrottle({0:0.00}) threw {1}", throttle, e);
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
            return (vc.ToggleEnginesEnabled()) ? 1.0 : 0.0;
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
            if (vessel.Landed != (vesselSituationConverted <= 2))
            {
                Utility.LogMessage(this, "vessel.Landed and vesselSituationConverted disagree!");
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
        /// category.
        /// </summary>
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

        /// <summary>
        /// The Life Support region provides specialized functionality for interfacing with
        /// the MASClimateControl cabin temperature system.
        /// </summary>
        #region Life Support

        /// <summary>
        /// When the MASClimateControl module is installed, returns the current load on
        /// the heating / cooling system as a percentage (from 0 to 1).  If the module is
        /// off, or it is not installed, this method returns 0.
        /// </summary>
        /// <returns>A value between 0 and 1.</returns>
        public double ClimateControlLoad()
        {
            if (fc.cc != null && fc.cc.enableHeater && fc.cc.podHeaterOutput > 0.0f)
            {
                return fc.cc.podHeaterDraw / fc.cc.podHeaterOutput;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if the MASClimateControl module is installed in this pod, and it
        /// is enabled.  Returns 0 otherwise.
        /// </summary>
        /// <returns>1 if MASClimateControl is installed and active, 0 otherwise.</returns>
        public double GetClimateControl()
        {
            return (fc.cc != null && fc.cc.enableHeater) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Toggle the MASClimateControl module on or off.
        /// </summary>
        public void ToggleClimateControl()
        {
            if (fc.cc != null)
            {
                fc.cc.enableHeater = !fc.cc.enableHeater;
            }
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

        /// <summary>
        /// Methods for querying and controlling maneuver nodes are in this category.
        /// </summary>
        #region Maneuver Node

        /// <summary>
        /// **UNTESTED**
        /// 
        /// Replace any scheduled maneuver nodes with this maneuver node.
        /// </summary>
        /// <param name="progradedV">ΔV in the prograde direction at the time of the maneuver, in m/s.</param>
        /// <param name="normaldV">ΔV in the normal direction at the time of the maneuver, in m/s.</param>
        /// <param name="radialdV">ΔV in the radial direction at the time of the maneuver, in m/s.</param>
        /// <param name="timeUT">UT to schedule the maneuver, in seconds.</param>
        public void AddManeuverNode(double progradedV, double normaldV, double radialdV, double timeUT)
        {
            if (vessel.patchedConicSolver != null)
            {
                if (double.IsNaN(progradedV) || double.IsInfinity(progradedV) ||
                    double.IsNaN(normaldV) || double.IsInfinity(normaldV) ||
                    double.IsNaN(radialdV) || double.IsInfinity(radialdV) ||
                    double.IsNaN(timeUT) || double.IsInfinity(timeUT))
                {
                    // bad parameters?
                    return;
                }

                // Swizzle parameters and sign-shift normal.
                Vector3d dV = new Vector3d(radialdV, -normaldV, progradedV);

                // No living in the past.
                timeUT = Math.Max(timeUT, vc.universalTime);

                vessel.patchedConicSolver.maneuverNodes.Clear();
                ManeuverNode mn = vessel.patchedConicSolver.AddManeuverNode(timeUT);
                mn.OnGizmoUpdated(dV, timeUT);
            }
        }

        /// <summary>
        /// Clear all scheduled maneuver nodes.
        /// </summary>
        /// <returns>1 if any nodes were cleared, 0 if no nodes were cleared.</returns>
        public double ClearManeuverNode()
        {
            if (vessel.patchedConicSolver != null)
            {
                int nodeCount = vessel.patchedConicSolver.maneuverNodes.Count;
                // TODO: what is vessel.patchedConicSolver.flightPlan?  And do I care?
                vessel.patchedConicSolver.maneuverNodes.Clear();

                return (nodeCount > 0) ? 1.0 : 0.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the apoapsis of the orbit that results from the scheduled maneuver.
        /// </summary>
        /// <returns>New Ap in meters, or 0 if no node is scheduled.</returns>
        public double ManeuverNodeAp()
        {
            if (vc.maneuverNodeValid)
            {
                return vc.nodeOrbit.ApA;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Delta-V of the next scheduled node.
        /// </summary>
        /// <returns>ΔV in m/s, or 0 if no node is scheduled.</returns>
        public double ManeuverNodeDV()
        {
            return vc.maneuverNodeDeltaV;
        }

        /// <summary>
        /// The normal component of the next scheduled maneuver.
        /// </summary>
        /// <returns>ΔV in m/s; negative values indicate anti-normal.</returns>
        public double ManeuverNodeDVNormal()
        {
            if (vc.maneuverNodeValid && vc.nodeOrbit != null)
            {
                return vc.maneuverNodeComponent.z;
            }
            return 0.0;
        }

        /// <summary>
        /// The prograde component of the next scheduled maneuver.
        /// </summary>
        /// <returns>ΔV in m/s; negative values indicate retrograde.</returns>
        public double ManeuverNodeDVPrograde()
        {
            if (vc.maneuverNodeValid && vc.nodeOrbit != null)
            {
                return vc.maneuverNodeComponent.x;
            }
            return 0.0;
        }

        /// <summary>
        /// The radial component of the next scheduled maneuver.
        /// </summary>
        /// <returns>ΔV in m/s; negative values indicate anti-radial.</returns>
        public double ManeuverNodeDVRadial()
        {
            if (vc.maneuverNodeValid && vc.nodeOrbit != null)
            {
                return vc.maneuverNodeComponent.y;
            }
            return 0.0;
        }

        /// <summary>
        /// Returns the eccentricity of the orbit that results from the scheduled maneuver.
        /// </summary>
        /// <returns>New eccentricity, or 0 if no node is scheduled.</returns>
        public double ManeuverNodeEcc()
        {
            if (vc.maneuverNodeValid)
            {
                return vc.nodeOrbit.eccentricity;
            }
            else
            {
                return 0.0;
            }
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
        /// Returns the inclination of the orbit that results from the scheduled maneuver.
        /// </summary>
        /// <returns>New inclination in degrees, or 0 if no node is scheduled.</returns>
        public double ManeuverNodeInc()
        {
            if (vc.maneuverNodeValid)
            {
                return vc.nodeOrbit.inclination;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the periapsis of the orbit that results from the scheduled maneuver.
        /// </summary>
        /// <returns>New Pe in meters, or 0 if no node is scheduled.</returns>
        public double ManeuverNodePe()
        {
            if (vc.maneuverNodeValid)
            {
                return vc.nodeOrbit.PeA;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the relative inclination of the target that will result from the
        /// scheduled maneuver.
        /// </summary>
        /// <returns>New relative inclination in degrees, or 0 if there is no maneuver node, 
        /// no target, or the target orbits a different body.</returns>
        public double ManeuverNodeRelativeInclination()
        {
            if (vc.maneuverNodeValid && vc.targetType > 0 && vc.targetOrbit.referenceBody == vc.nodeOrbit.referenceBody)
            {
                return Vector3.Angle(vc.nodeOrbit.GetOrbitNormal(), vc.targetOrbit.GetOrbitNormal());
            }
            else
            {
                return 0.0;
            }
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

        /// <summary>
        /// Vessel mass may be queried with these methods.
        /// </summary>
        #region Mass
        /// <summary>
        /// Returns the mass of the vessel
        /// </summary>
        /// <param name="wetMass">wet mass if true, dry mass otherwise</param>
        /// <returns>Vessel mass in kg.</returns>
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

        /// <summary>
        /// Provides MAS-native methods for common math primitives.  These methods generally
        /// duplicate the functions in the Lua math table, but by placing them in MAS, MAS
        /// can use native delegates instead of having to call into Lua (which is slower).
        /// </summary>
        #region Math

        /// <summary>
        /// Returns the absolute value of `value`.
        /// </summary>
        /// <returns>The absolute value of `value`.</returns>
        public double Abs(double value)
        {
            return Math.Abs(value);
        }

        /// <summary>
        /// Rounds a number up to the next integer.
        /// </summary>
        /// <param name="value">The value to round</param>
        /// <returns></returns>
        public double Ceiling(double value)
        {
            return Math.Ceiling(value);
        }

        /// <summary>
        /// Clamps `value` to stay within the range `a` to `b`, inclusive.  `a` does not
        /// have to be less than `b`.
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="a">The first bound.</param>
        /// <param name="b">The second bound.</param>
        /// <returns>The clamped value.</returns>
        public double Clamp(double value, double a, double b)
        {
            double max = Math.Max(a, b);
            double min = Math.Min(a, b);
            return Math.Max(Math.Min(value, max), min);
        }


        /// <summary>
        /// Rounds a number down to the next integer.
        /// </summary>
        /// <param name="value">The value to round</param>
        /// <returns></returns>
        public double Floor(double value)
        {
            return Math.Floor(value);
        }

        /// <summary>
        /// Return the larger value
        /// </summary>
        /// <param name="a">The first value to test.</param>
        /// <param name="b">The second value to test.</param>
        /// <returns>`a` if `a` is larger than `b`; `b` otherwise.</returns>
        public double Max(double a, double b)
        {
            return Math.Max(a, b);
        }

        /// <summary>
        /// Return the smaller value
        /// </summary>
        /// <param name="a">The first value to test.</param>
        /// <param name="b">The second value to test.</param>
        /// <returns>`a` if `a` is smaller than `b`; `b` otherwise.</returns>
        public double Min(double a, double b)
        {
            return Math.Min(a, b);
        }

        /// <summary>
        /// Apply a log10-like curve to the value.
        /// 
        /// The exact formula is:
        /// 
        /// ```
        /// if (abs(sourceValue) &lt; 1.0)
        ///   return sourceValue;
        /// else
        ///   return (1 + Log10(abs(sourceValue))) * Sign(sourceValue);
        /// end
        /// ```
        /// </summary>
        /// <param name="sourceValue">An input number</param>
        /// <returns>A Log10-like representation of the input value.</returns>
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
        /// Divides `numerator` by `denominator`.  If the denominator is zero, this method
        /// returns 0 instead of infinity or throwing a divide-by-zero exception.
        /// </summary>
        /// <param name="numerator">The numerator</param>
        /// <param name="denominator">The denominator</param>
        /// <returns>numerator / denominator, or 0 if the denominator is zero.</returns>
        public double SafeDivide(double numerator, double denominator)
        {
            if (Math.Abs(denominator) > 0.0)
            {
                return numerator / denominator;
            }
            else
            {
                return 0.0;
            }
        }

        #endregion

        /// <summary>
        /// Meta variables and functions are variables provide information about the
        /// game, as opposed to the vessel.  They also include the `fc.Conditioned()`
        /// functions, which can provide some realism by disrupting lighting under
        /// low power or high G situations.
        /// </summary>
        #region Meta

        [MASProxyAttribute(Immutable = true)]
        /// <summary>
        /// Checks for the existence of the named assembly (eg, `fc.AssemblyLoaded("MechJeb2")`).
        /// This can be used to determine
        /// if a particular mod has been installed when that mod is not directly supported by
        /// Avionics Systems.
        /// </summary>
        /// <returns>1 if the named assembly is loaded, 0 otherwise.</returns>
        public double AssemblyLoaded(string assemblyName)
        {
            return MASLoader.knownAssemblies.Contains(assemblyName) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Applies some "realism" conditions to the variable to cause it to
        /// return zero under two general conditions:
        /// 
        /// 1) When there is no power available (the config-file-specified
        /// power variable is below 0.0001), or
        /// 
        /// 2) The craft is under high g-loading.  G-loading limits are defined
        /// in the per-pod config file.  When these limits are exceeded, there
        /// is a chance (also defined in the config file) of the variable being
        /// interrupted.  This chance increases as the g-forces exceed the
        /// threshold using a square-root curve.
        /// 
        /// The variable `fc.Conditioned(1)` behaves the same as the RasterPropMonitor
        /// ASET Props custom variable `CUSTOM_ALCOR_POWEROFF`, with an inverted
        /// value (`CUSTOM_ALCOR_POWEROFF` returns 1 to indicate "disrupt", but
        /// `fc.Conditioned(1)` returns 0 instead).
        /// 
        /// For boolean parameters, `true` is treated as 1, and `false` is treated
        /// as 0.
        /// </summary>
        /// <param name="value">A numeric value or a boolean</param>
        /// <returns>`value` if the conditions above are not met.</returns>
        public double Conditioned(object value)
        {
            double state = 0.0;
            if (value is bool)
            {
                state = ((bool)value) ? 1.0 : 0.0;
            }
            else if (value is double)
            {
                state = (double)value;
            }
            else
            {
                Utility.LogMessage(this, "fc.Conditioned no-op: {0}", value.GetType());
            }

            if (fc.isPowered && UnityEngine.Random.value > fc.disruptionChance)
            {
                return state;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the number of hours per day, depending on whether the game
        /// is configured for the Earth calendar or the Kerbin calendar.
        /// </summary>
        /// <returns>6 for Kerbin time, 24 for Earth time</returns>
        public double HoursPerDay()
        {
            return (GameSettings.KERBIN_TIME) ? 6.0 : 24.0;
        }

        /// <summary>
        /// Returns 1 if KSP is configured for the Kerbin calendar (6 hour days);
        /// returns 0 for Earth days (24 hour).
        /// </summary>
        /// <returns></returns>
        public double KerbinTime()
        {
            return (GameSettings.KERBIN_TIME) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Log messages to the KSP.log.  Messages will be prefixed with
        /// [MASFlightComputerProxy].
        /// </summary>
        /// <param name="message">The string to write.  Strings may be formatted using the Lua string library, or using the `..` concatenation operator.</param>
        public void LogMessage(string message)
        {
            Utility.LogMessage(this, message);
        }

        [MASProxyAttribute(Immutable = true)]
        /// <summary>
        /// Returns the version number of the MAS plugin, as a string,
        /// such as `1.0.1.12331`.
        /// </summary>
        /// <returns>MAS Version in string format.</returns>
        public string MASVersion()
        {
            return MASLoader.masVersion;
        }

        /// <summary>
        /// Play the audio file specified in `sound`, at the volume specified in `volume`.
        /// 
        /// **NOT IMPLEMENTED YET.**
        /// </summary>
        /// <param name="sound">The name of the sound to play.</param>
        /// <param name="volume">The volume to use for playback, with 1.0 equal to default volume.</param>
        /// <returns>Returns 1 if the audio was played, 0 if it was not found or otherwise not played.</returns>
        public double PlayAudio(string sound, double volume)
        {
            return 0.0;
        }

        /// <summary>
        /// Recover the vessel if it is recoverable.  Has no effect if the craft can not be
        /// recovered.
        /// </summary>
        /// <returns>1 if the craft can be recovered (although it is also recovered immediately), 0 otherwise.</returns>
        public double RecoverVessel()
        {
            if (vessel.IsRecoverable)
            {
                GameEvents.OnVesselRecoveryRequested.Fire(vessel);
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// The ScrollingMarquee function takes a string, `input`, and it returns a substring 
        /// of maximum length `maxChars`.  The substring that is returned changes every
        /// `scrollRate` seconds if the string length is greater than `maxChars`, allowing
        /// for a scrolling marquee effect.  Using this method with the Repetition Scrolling
        /// font can simulate an LED / LCD display.
        /// 
        /// Note that characters advance one character width at a time - it is not a smooth
        /// sliding movement.
        /// </summary>
        /// <param name="inputString">The string to use for the marquee.</param>
        /// <param name="maxChars">The maximum number of characters in the string to display.</param>
        /// <param name="scrollRate">The frequency, in seconds, that the marquee advances.</param>
        /// <returns>A substring of no more than `maxChars` length.</returns>
        public string ScrollingMarquee(string inputString, double maxChars, double scrollRate)
        {
            int maxCh = (int)maxChars;
            int strlen = inputString.Length;
            if (strlen <= maxCh)
            {
                return inputString;
            }
            else if (scrollRate <= 0.0)
            {
                return inputString.Substring(0, maxCh);
            }
            else
            {
                double adjustedTime = vc.universalTime / scrollRate;
                double startD = adjustedTime % (double)(strlen + 1);
                int start = (int)startD;

                if (start + maxCh <= strlen)
                {
                    return inputString.Substring(start, maxCh);
                }
                else
                {
                    int tail = maxCh - strlen + start - 1;

                    StringBuilder sb = Utility.GetStringBuilder();
                    sb.Append(inputString.Substring(start)).Append(' ').Append(inputString.Substring(0, tail));

                    return sb.ToString();
                }
            }
        }

        /// <summary>
        /// Returns 1 if the vessel is recoverable, 0 otherwise.
        /// </summary>
        /// <returns>1 if the craft can be recovered, 0 otherwise.</returns>
        public double VesselRecoverable()
        {
            return (vessel.IsRecoverable) ? 1.0 : 0.0;
        }
        #endregion

        /// <summary>
        /// Information on the vessel's current orbit are available in this category.
        /// </summary>
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
        /// Return the eccentricity of the orbit.
        /// </summary>
        /// <returns></returns>
        public double Eccentricity()
        {
            return vc.orbit.eccentricity;
        }

        /// <summary>
        /// Return the vessel's orbital inclination.
        /// </summary>
        /// <returns>Inclination in degrees.</returns>
        public double Inclination()
        {
            return vc.orbit.inclination;
        }

        /// <summary>
        /// Returns the name of the body that the vessel will be orbiting after the
        /// next SoI change.  If the craft is not changing SoI, returns an empty string.
        /// </summary>
        /// <returns>Name of the body, or an empty string if the orbit does not change SoI.</returns>
        public string NextBodyName()
        {
            if (vesselSituationConverted > 2)
            {
                if (vc.orbit.patchEndTransition == Orbit.PatchTransitionType.ENCOUNTER)
                {
                    return vessel.orbit.nextPatch.referenceBody.bodyName;
                }
                else if (vc.orbit.patchEndTransition == Orbit.PatchTransitionType.ESCAPE)
                {
                    return vessel.mainBody.referenceBody.bodyName;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the time to the next SoI transition.  If the current orbit does not change
        /// SoI, returns 0.
        /// </summary>
        /// <returns></returns>
        public double TimeToNextSoI()
        {
            if (vessel.orbit.patchEndTransition == Orbit.PatchTransitionType.ENCOUNTER ||
                vessel.orbit.patchEndTransition == Orbit.PatchTransitionType.ESCAPE)
            {
                return vessel.orbit.UTsoi - Planetarium.GetUniversalTime();
            }
            return 0.0;
        }

        /// <summary>
        /// Returns 1 if the next SoI change is an 'encounter', -1 if it is an
        /// 'escape', and 0 if the orbit is not changing SoI.
        /// </summary>
        /// <returns>0 if the orbit does not transition.  1 if the vessel will encounter a body, -1 if the vessel will escape the current body.</returns>
        public double NextSoI()
        {
            if (vesselSituationConverted > 2)
            {
                if (vc.orbit.patchEndTransition == Orbit.PatchTransitionType.ENCOUNTER)
                {
                    return 1.0;
                }
                else if (vc.orbit.patchEndTransition == Orbit.PatchTransitionType.ESCAPE)
                {
                    return -1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the orbital period, in seconds.
        /// </summary>
        /// <returns>Orbital period, seconds.  Zero if the craft is not in flight.</returns>
        public double OrbitPeriod()
        {
            return (vesselSituationConverted > 2) ? vc.orbit.period : 0.0;
        }

        /// <summary>
        /// Returns the orbits periapsis (from datum) in meters.
        /// </summary>
        /// <returns></returns>
        public double Periapsis()
        {
            return vc.periapsis;
        }

        /// <summary>
        /// Returns the semi-major axis of the current orbit.  When the SMA
        /// matches a body's synchronous orbit SMA, the vessel is in a synchronous orbit.
        /// </summary>
        /// <returns>SMA in meters.</returns>
        public double SemiMajorAxis()
        {
            return vc.orbit.semiMajorAxis;
        }
        #endregion

        /// <summary>
        /// Variables related to the vessel's orientation in space, relative to a target,
        /// or relative to the surface, are here.
        /// </summary>
        #region Orientation

        /// <summary>
        /// Returns the angle of attack of the vessel.  If FAR is installed,
        /// FAR's results are used.
        /// </summary>
        /// <returns>The angle of attack, in degrees</returns>
        public double AngleOfAttack()
        {
            if (MASIFAR.farFound)
            {
                return farProxy.AngleOfAttack();
            }
            else
            {
                return vc.GetRelativePitch(vc.surfacePrograde);
            }
        }

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
        /// Pitch of the vessel relative to the orbit anti-normal vector.
        /// </summary>
        /// <returns></returns>
        public double PitchAntiNormal()
        {
            return vc.GetRelativePitch(-vc.normal);
        }

        /// <summary>
        /// Pitch of the vessel relative to the vector pointing away from the target.
        /// </summary>
        /// <returns></returns>
        public double PitchAntiTarget()
        {
            if (vc.activeTarget == null)
            {
                return 0.0;
            }
            else
            {
                return vc.GetRelativePitch(-vc.targetDirection);
            }
        }

        /// <summary>
        /// Returns the pitch component of the angle between a target docking
        /// port and a reference (on Vessel) docking port; 0 if the target is
        /// not a docking port or if the reference transform is not a docking
        /// port.
        /// </summary>
        /// <returns></returns>
        public double PitchDockingAlignment()
        {
            if (vc.targetType == MASVesselComputer.TargetType.DockingPort && vc.targetDockingTransform != null)
            {
                Vector3 projectedVector = Vector3.ProjectOnPlane(-vc.targetDockingTransform.forward, vc.referenceTransform.right);
                projectedVector.Normalize();

                // Dot the projected vector with the 'top' direction so we can find
                // the relative pitch.
                float dotPitch = Vector3.Dot(projectedVector, vc.referenceTransform.forward);
                float pitch = Mathf.Asin(dotPitch);
                if (float.IsNaN(pitch))
                {
                    pitch = (dotPitch > 0.0f) ? 90.0f : -90.0f;
                }
                else
                {
                    pitch *= Mathf.Rad2Deg;
                }

                return pitch;
            }

            return 0.0;
        }

        /// <summary>
        /// Pitch of the vessel relative to the next scheduled maneuver vector.
        /// </summary>
        /// <returns></returns>
        public double PitchManeuver()
        {
            if (vc.maneuverNodeValid)
            {
                return vc.GetRelativePitch(vc.maneuverNodeVector.normalized);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Pitch of the vessel relative to the orbit normal vector.
        /// </summary>
        /// <returns></returns>
        public double PitchNormal()
        {
            return vc.GetRelativePitch(vc.normal);
        }

        /// <summary>
        /// Returns the pitch rate of the vessel in degrees/sec
        /// </summary>
        /// <returns></returns>
        public double PitchRate()
        {
            return -vessel.angularVelocity.x * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Pitch of the vessel relative to the orbital prograde vector.
        /// </summary>
        /// <returns></returns>
        public double PitchPrograde()
        {
            return vc.GetRelativePitch(vc.prograde);
        }

        /// <summary>
        /// Pitch of the vessel relative to the orbital Radial In vector.
        /// </summary>
        /// <returns></returns>
        public double PitchRadialIn()
        {
            return vc.GetRelativePitch(-vc.radialOut);
        }

        /// <summary>
        /// Pitch of the vessel relative to the orbital Radial Out vector.
        /// </summary>
        /// <returns></returns>
        public double PitchRadialOut()
        {
            return vc.GetRelativePitch(vc.radialOut);
        }

        /// <summary>
        /// Pitch of the vessel relative to the orbital retrograde vector.
        /// </summary>
        /// <returns></returns>
        public double PitchRetrograde()
        {
            return vc.GetRelativePitch(-vc.prograde);
        }

        /// <summary>
        /// Pitch of the vessel relative to the surface prograde vector.
        /// </summary>
        /// <returns></returns>
        public double PitchSurfacePrograde()
        {
            return vc.GetRelativePitch(vc.surfacePrograde);
        }

        /// <summary>
        /// Pitch of the vessel relative to the surface retrograde vector.
        /// </summary>
        /// <returns></returns>
        public double PitchSurfaceRetrograde()
        {
            return vc.GetRelativePitch(-vc.surfacePrograde);
        }

        /// <summary>
        /// Pitch of the vessel relative to the vector pointing at the target.
        /// </summary>
        /// <returns></returns>
        public double PitchTarget()
        {
            if (vc.activeTarget == null)
            {
                return 0.0;
            }
            else
            {
                return vc.GetRelativePitch(vc.targetDirection);
            }
        }

        /// <summary>
        /// Pitch of the vessel relative to the target relative prograde vector.
        /// </summary>
        /// <returns></returns>
        public double PitchTargetPrograde()
        {
            if (vc.activeTarget == null)
            {
                return 0.0;
            }
            else
            {
                return vc.GetRelativePitch(vc.targetRelativeVelocity.normalized);
            }
        }

        /// <summary>
        /// Pitch of the vessel relative to the target relative retrograde vector.
        /// </summary>
        /// <returns></returns>
        public double PitchTargetRetrograde()
        {
            if (vc.activeTarget == null)
            {
                return 0.0;
            }
            else
            {
                return vc.GetRelativePitch(-vc.targetRelativeVelocity.normalized);
            }
        }

        /// <summary>
        /// Returns a number identifying what the current reference transform is:
        /// 1: The current IVA pod (if in IVA)
        /// 2: A command pod or probe control part.
        /// 3: A docking port
        /// 4: A Grapple Node (Claw)
        /// 0: Unknown.
        /// </summary>
        /// <returns></returns>
        public double ReferenceTransformType()
        {
            switch (vc.referenceTransformType)
            {
                case MASVesselComputer.ReferenceType.Unknown:
                    return 0.0;
                case MASVesselComputer.ReferenceType.Self:
                    return 1.0;
                case MASVesselComputer.ReferenceType.RemoteCommand:
                    return 2.0;
                case MASVesselComputer.ReferenceType.DockingPort:
                    return 3.0;
                case MASVesselComputer.ReferenceType.Claw:
                    return 4.0;
                default:
                    return 0.0;
            }
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
        /// Returns the roll angle between the vessel's reference transform and a targeted docking port.
        /// If the target is not a docking port, returns 0;
        /// </summary>
        /// <returns></returns>
        public double RollDockingAlignment()
        {
            if (vc.targetType == MASVesselComputer.TargetType.DockingPort && vc.targetDockingTransform != null)
            {
                Vector3 projectedVector = Vector3.ProjectOnPlane(vc.targetDockingTransform.up, vc.referenceTransform.up);
                projectedVector.Normalize();

                float dotLateral = Vector3.Dot(projectedVector, vc.referenceTransform.right);
                float dotLongitudinal = Vector3.Dot(projectedVector, vc.referenceTransform.forward);

                // Taking arc tangent of x/y lets us treat the front of the vessel
                // as the 0 degree location.
                float roll = Mathf.Atan2(dotLateral, dotLongitudinal);
                roll *= Mathf.Rad2Deg;

                return roll;
            }

            return 0.0;
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
        /// Returns the vessel's current sideslip.  If FAR is installed,
        /// it will use FAR's computation of sideslip.
        /// </summary>
        /// <returns>Sideslip in degrees.</returns>
        public double Sideslip()
        {
            if (MASIFAR.farFound)
            {
                return farProxy.Sideslip();
            }
            else
            {
                return vc.GetRelativeYaw(vc.surfacePrograde);
            }
        }

        /// <summary>
        /// Yaw of the vessel relative to the orbit's anti-normal vector.
        /// </summary>
        /// <returns></returns>
        public double YawAntiNormal()
        {
            return vc.GetRelativeYaw(-vc.normal);
        }

        /// <summary>
        /// Yaw of the vessel relative to the vector pointing away from the target.
        /// </summary>
        /// <returns></returns>
        public double YawAntiTarget()
        {
            if (vc.activeTarget == null)
            {
                return 0.0;
            }
            else
            {
                return vc.GetRelativeYaw(vc.targetDirection);
            }
        }

        /// <summary>
        /// Returns the yaw angle between the vessel's reference transform and a targeted docking port.
        /// If the target is not a docking port, returns 0;
        /// </summary>
        /// <returns></returns>
        public double YawDockingAlignment()
        {
            if (vc.targetType == MASVesselComputer.TargetType.DockingPort && vc.targetDockingTransform != null)
            {
                Vector3 projectedVector = Vector3.ProjectOnPlane(-vc.targetDockingTransform.forward, vc.referenceTransform.forward);
                projectedVector.Normalize();

                // Determine the lateral displacement by dotting the vector with
                // the 'right' vector...
                float dotLateral = Vector3.Dot(projectedVector, vc.referenceTransform.right);
                // And the forward/back displacement by dotting with the forward vector.
                float dotLongitudinal = Vector3.Dot(projectedVector, vc.referenceTransform.up);

                // Taking arc tangent of x/y lets us treat the front of the vessel
                // as the 0 degree location.
                float yaw = Mathf.Atan2(dotLateral, dotLongitudinal);
                yaw *= Mathf.Rad2Deg;

                return yaw;
            }

            return 0.0;
        }

        /// <summary>
        /// Yaw of the vessel relative to the next scheduled maneuver vector.
        /// </summary>
        /// <returns></returns>
        public double YawManeuver()
        {
            if (vc.maneuverNodeValid)
            {
                return vc.GetRelativeYaw(vc.maneuverNodeVector.normalized);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Yaw of the vessel relative to the orbit's normal vector.
        /// </summary>
        /// <returns></returns>
        public double YawNormal()
        {
            return vc.GetRelativeYaw(vc.normal);
        }

        /// <summary>
        /// Returns the yaw rate of the vessel in degrees/sec
        /// </summary>
        /// <returns></returns>
        public double YawRate()
        {
            return -vessel.angularVelocity.z * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Yaw of the vessel relative to the orbital prograde vector.
        /// </summary>
        /// <returns></returns>
        public double YawPrograde()
        {
            return vc.GetRelativeYaw(vc.prograde);
        }

        /// <summary>
        /// Yaw of the vessel relative to the radial in vector.
        /// </summary>
        /// <returns></returns>
        public double YawRadialIn()
        {
            return vc.GetRelativeYaw(-vc.radialOut);
        }

        /// <summary>
        /// Yaw of the vessel relative to the radial out vector.
        /// </summary>
        /// <returns></returns>
        public double YawRadialOut()
        {
            return vc.GetRelativeYaw(vc.radialOut);
        }

        /// <summary>
        /// Yaw of the vessel relative to the orbital retrograde vector.
        /// </summary>
        /// <returns></returns>
        public double YawRetrograde()
        {
            return vc.GetRelativeYaw(-vc.prograde);
        }

        /// <summary>
        /// Yaw of the vessel relative to the surface prograde vector.
        /// </summary>
        /// <returns></returns>
        public double YawSurfacePrograde()
        {
            return vc.GetRelativeYaw(vc.surfacePrograde);
        }

        /// <summary>
        /// Yaw of the vessel relative to the surface retrograde vector.
        /// </summary>
        /// <returns></returns>
        public double YawSurfaceRetrograde()
        {
            return vc.GetRelativeYaw(-vc.surfacePrograde);
        }

        /// <summary>
        /// Yaw of the vessel relative to the vector pointing at the target.
        /// </summary>
        /// <returns></returns>
        public double YawTarget()
        {
            if (vc.activeTarget == null)
            {
                return 0.0;
            }
            else
            {
                return vc.GetRelativeYaw(vc.targetDirection);
            }
        }

        /// <summary>
        /// Yaw of the vessel relative to the target relative prograde vector.
        /// </summary>
        /// <returns></returns>
        public double YawTargetPrograde()
        {
            if (vc.activeTarget == null)
            {
                return 0.0;
            }
            else
            {
                return vc.GetRelativeYaw(vc.targetRelativeVelocity.normalized);
            }
        }

        /// <summary>
        /// Yaw of the vessel relative to the target relative retrograde vector.
        /// </summary>
        /// <returns></returns>
        public double YawTargetRetrograde()
        {
            if (vc.activeTarget == null)
            {
                return 0.0;
            }
            else
            {
                return vc.GetRelativeYaw(-vc.targetRelativeVelocity.normalized);
            }
        }
        #endregion

        /// <summary>
        /// Periodic variables change value over time, based on a requested
        /// frequency.
        /// </summary>
        #region Periodic Variables

        /// <summary>
        /// Returns a periodic variable that follows a sine-wave curve.
        /// </summary>
        /// <param name="period">The period of the change, in cycles/second (Hertz).</param>
        /// <returns>A number between -1 and +1.</returns>
        public double PeriodSine(double period)
        {
            if (period > 0.0)
            {
                double invPeriod = 1.0 / period;

                double remainder = vc.universalTime % invPeriod;

                return Math.Sin(remainder * period * Math.PI * 2.0);
            }

            return 0.0;
        }

        /// <summary>
        /// Returns a stair-step periodic variable (changes from 0 to 1 to 0 with
        /// no ramps between values).
        /// </summary>
        /// <param name="period">The period of the change, in cycles/second (Hertz).</param>
        /// <returns>0 or 1</returns>
        public double PeriodStep(double period)
        {
            if (period > 0.0)
            {
                double invPeriod = 1.0 / period;

                double remainder = vc.universalTime % invPeriod;

                return (remainder > invPeriod * 0.5) ? 1.0 : 0.0;
            }

            return 0.0;
        }
        #endregion

        /// <summary>
        /// Persistent variables are the primary means of data storage in Avionics Systems.
        /// As such, there are many ways to set, alter, or query these variables.
        /// 
        /// Persistent variables may be numbers or strings.  Several of the setter and
        /// getter functions in this category will convert the variable automatically
        /// from one to the other (whenever possible), but it is the responsibility
        /// of the prop config maker to make sure that text and numbers are not
        /// intermingled when a specific persistent variable will be used as a number.
        /// </summary>
        #region Persistent Vars
        /// <summary>
        /// This method adds an amount to the named persistent.  If the variable
        /// did not already exist, it is created and initialized to 0 before
        /// adding `amount`.  If the variable was a string, it is converted to
        /// a number before adding `amount`.
        /// 
        /// If the variable cannot converted to a number, the variable's name is
        /// returned, instead.
        /// </summary>
        /// <param name="persistentName">The name of the persistent variable to change.</param>
        /// <param name="amount">The amount to add to the persistent variable.</param>
        /// <returns>The new value of the persistent variable, or the name of the variable if it could not be converted to a number.</returns>
        public object AddPersistent(string persistentName, double amount)
        {
            return fc.AddPersistent(persistentName, amount);
        }

        /// <summary>
        /// This method adds an amount to the named persistent.  The result
        /// is clamped to the range [minValue, maxValue].
        /// 
        /// If the variable
        /// did not already exist, it is created and initialized to 0 before
        /// adding `amount`.  If the variable was a string, it is converted to
        /// a number before adding `amount`.
        /// 
        /// If the variable cannot converted to a number, the variable's name is
        /// returned, instead.
        /// </summary>
        /// <param name="persistentName">The name of the persistent variable to change.</param>
        /// <param name="amount">The amount to add to the persistent variable.</param>
        /// <param name="minValue">The minimum value of the variable.  If adding `amount` to the variable
        /// causes it to be less than this value, the variable is set to this value, instead.</param>
        /// <param name="maxValue">The maximum value of the variable.  If adding `amount` to the variable
        /// causes it to be greater than this value, the variable is set to this value, instead.</param>
        /// <returns>The new value of the persistent variable, or the name of the variable if it could not be
        /// converted to a number.</returns>
        public object AddPersistentClamped(string persistentName, double amount, double minValue, double maxValue)
        {
            return fc.AddPersistentClamped(persistentName, amount, minValue, maxValue);
        }

        /// <summary>
        /// This method adds an amount to the named persistent.  The result
        /// wraps around the range [minValue, maxValue].  This feature is used,
        /// for instance, for
        /// adjusting a heading between 0 and 360 degrees without having to go
        /// from 359 all the way back to 0.  `maxValue` is treated as an alias
        /// for `minValue`, so if adding to a persistent value makes it equal
        /// exactly `maxValue`, it is set to `minValue` instead.  With the heading
        /// example above, for instance, you would use `fc.AddPersistentWrapped("SomeVariableName", 1, 0, 360)`.
        /// 
        /// To make a counter that runs from 0 to 2 before wrapping back to 0
        /// again, `fc.AddPersistentWrapped("SomeVariableName", 1, 0, 3)`.
        /// 
        /// If the variable
        /// did not already exist, it is created and initialized to 0 before
        /// adding `amount`.  If the variable was a string, it is converted to
        /// a number before adding `amount`.
        /// 
        /// If the variable cannot converted to a number, the variable's name is
        /// returned, instead.
        /// 
        /// If minValue and maxValue are the same, `amount` is treated as zero (nothing is added).
        /// </summary>
        /// <param name="persistentName">The name of the persistent variable to change.</param>
        /// <param name="amount">The amount to add to the persistent variable.</param>
        /// <param name="minValue">The minimum value of the variable.  If adding `amount` would make the
        /// variable less than `minValue`, MAS sets the variable to `maxValue` minus the
        /// difference.</param>
        /// <param name="maxValue">The maximum value of the variable.  If adding `amount` would make the
        /// variable greather than `maxValue`, MAS sets the variable to `minValue` plus the overage.</param>
        /// <returns>The new value of the persistent variable, or the name of the variable if it could not be
        /// converted to a number.</returns>
        public object AddPersistentWrapped(string persistentName, double amount, double minValue, double maxValue)
        {
            return fc.AddPersistentWrapped(persistentName, amount, minValue, maxValue);
        }

        /// <summary>
        /// Append the string `addon` to the persistent variable `persistentName`, but
        /// only up to the specified maximum length.  If the persistent does not exist,
        /// it is created and initialized to `addon`.  If the persistent is a numeric value, 
        /// it is converted to a string, and then `addon` is added.
        /// </summary>
        /// <param name="persistentName">The name of the persistent variable to change.</param>
        /// <param name="amount">The amount to add to the persistent variable.</param>
        /// <param name="maxLength">The maximum number of characters allowed in the
        /// string.  Characters in excess of this amount are not added to the persistent.</param>
        /// <returns>The new string.</returns>
        public object AppendPersistent(string persistentName, string addon, double maxLength)
        {
            return fc.AppendPersistent(persistentName, addon, (int)maxLength);
        }

        /// <summary>
        /// Return value of the persistent.  Strings are returned as strings,
        /// numbers are returned as numbers.  If the persistent does not exist
        /// yet, the name is returned.
        /// </summary>
        /// <param name="persistentName">The name of the persistent variable to query.</param>
        /// <returns>The value of the persistent, or its name if it does not exist.</returns>
        [MASProxyAttribute(Pushable = true)]
        public object GetPersistent(string persistentName)
        {
            return fc.GetPersistent(persistentName);
        }

        /// <summary>
        /// Return the value of the persistent as a number.  If the persistent
        /// does not exist yet, or it is a string that can not be converted to
        /// a number, return 0.
        /// </summary>
        /// <param name="persistentName">The name of the persistent variable to query.</param>
        /// <returns>The numeric value of the persistent, or 0 if it either does not
        /// exist, or it cannot be converted to a number.</returns>
        [MASProxyAttribute(Pushable = true)]
        public double GetPersistentAsNumber(string persistentName)
        {
            return fc.GetPersistentAsNumber(persistentName);
        }

        /// <summary>
        /// Returns 1 if the named persistent variable has been initialized.  Returns 0
        /// if the variable does not exist yet.
        /// </summary>
        /// <param name="persistentName">The persistent variable name to check.</param>
        /// <returns>1 if the variable contains initialized data, 0 if it does not.</returns>
        [MASProxyAttribute(Pushable = true)]
        public double GetPersistentExists(string persistentName)
        {
            return fc.GetPersistentExists(persistentName) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Set a persistent to `value`.  `value` may be either a string or
        /// a number.  The existing value of the persistent is replaced.
        /// </summary>
        /// <param name="persistentName">The name of the persistent variable to change.</param>
        /// <param name="value">The new number or text string to use for this persistent.</param>
        /// <returns>`value`</returns>
        public object SetPersistent(string persistentName, object value)
        {
            return fc.SetPersistent(persistentName, value);
        }

        /// <summary>
        /// Toggle a persistent between 0 and 1.
        /// 
        /// If the persistent is a number, it becomes 0 if it was a
        /// positive number and it becomes 1 if it was previously %lt;= 0.
        /// 
        /// If the persistent was a string, it is converted to a number, and
        /// the same rule is applied.
        /// </summary>
        /// <param name="persistentName">The name of the persistent variable to change.</param>
        /// <returns>0 or 1.  If the variable was a string, and it could not be converted
        /// to a number, `persistentName` is returned, instead.</returns>
        public object TogglePersistent(string persistentName)
        {
            return fc.TogglePersistent(persistentName);
        }
        #endregion

        /// <summary>
        /// The Position category provides information about the vessel's position
        /// relative to a body (latitude and longitude) as well as landing predictions
        /// and the like.
        /// </summary>
        #region Position
        /// <summary>
        /// Returns the predicted altitude of landing.  Uses
        /// MechJeb if its landing computer is active.
        /// </summary>
        /// <returns></returns>
        public double LandingAltitude()
        {
            if (mjProxy.LandingComputerActive() > 0.0)
            {
                return mjProxy.LandingAltitude();
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the predicted latitude of landing.  Uses
        /// MechJeb if its landing computer is active.
        /// </summary>
        /// <returns></returns>
        public double LandingLatitude()
        {
            if (mjProxy.LandingComputerActive() > 0.0)
            {
                return mjProxy.LandingLatitude();
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the predicted longitude of landing.  Uses
        /// MechJeb if its landing computer is active.
        /// </summary>
        /// <returns></returns>
        public double LandingLongitude()
        {
            if (mjProxy.LandingComputerActive() > 0.0)
            {
                return mjProxy.LandingLongitude();
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if landing predictions are valid.  Automatically selects
        /// MechJeb if its landing computer is active.
        /// </summary>
        /// <returns></returns>
        public double LandingPredictorActive()
        {
            if (mjProxy.LandingComputerActive() > 0.0)
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

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
            // longitude seems to be unnormalized.
            return Utility.NormalizeLongitude(vessel.longitude);
        }
        #endregion

        /// <summary>
        /// Queries and controls related to power production belong in this category.
        /// 
        /// For all of these components, if the player has changed the `ElectricCharge` field
        /// in the MAS config file, these components will track that resource instead.
        /// </summary>
        #region Power Production
        /// <summary>
        /// Returns the number of alternators on the vessel.
        /// </summary>
        /// <returns>Number of alternator modules.</returns>
        public double AlternatorCount()
        {
            return vc.moduleAlternator.Length;
        }

        /// <summary>
        /// Returns the current net output of the alternators.
        /// </summary>
        /// <returns>Units of ElectricCharge/second</returns>
        public double AlternatorOutput()
        {
            return vc.netAlternatorOutput;
        }

        /// <summary>
        /// Returns the number of fuel cells on the vessel.  Fuel cells are defined
        /// as ModuleResourceConverter units that output `ElectricCharge` (or whatever
        /// the player-selected override is in the MAS config file).
        /// </summary>
        /// <returns>Number of fuel cells.</returns>
        public double FuelCellCount()
        {
            return vc.moduleFuelCell.Length;
        }

        /// <summary>
        /// Returns the current output of installed fuel cells.
        /// </summary>
        /// <returns>Units of ElectricCharge/second.</returns>
        public double FuelCellOutput()
        {
            return vc.netFuelCellOutput;
        }

        /// <summary>
        /// Returns the number of generators on the vessel.  Generators
        /// are and ModuleGenerator that outputs `ElectricCharge`.
        /// </summary>
        /// <returns>Number of generator.s</returns>
        public double GeneratorCount()
        {
            return vc.moduleGenerator.Length;
        }

        /// <summary>
        /// Returns the current output of installed generators.
        /// </summary>
        /// <returns>Output in ElectricCharge/sec.</returns>
        public double GeneratorOutput()
        {
            return vc.netGeneratorOutput;
        }

        /// <summary>
        /// Returns 1 if at least one fuel cell is enabled; 0 otherwise.
        /// </summary>
        /// <returns>1 if any fuel cell is switched on; 0 otherwise.</returns>
        public double GetFuelCellActive()
        {
            return (vc.fuelCellActive) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the number of solar panels on the vessel.
        /// </summary>
        /// <returns>The number of solar panel modules on the vessel.</returns>
        public double SolarPanelCount()
        {
            return vc.moduleSolarPanel.Length;
        }

        /// <summary>
        /// Returns 1 if all solar panels are damaged.
        /// </summary>
        /// <returns>1 is all solar panels are damaged; 0 otherwise.</returns>
        public double SolarPanelDamaged()
        {
            return (vc.solarPanelPosition == 0) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if at least one solar panel may be deployed.
        /// </summary>
        /// <returns>1 if any solar panel is retracted and available to deploy; 0 otherwise.</returns>
        public double SolarPanelDeployable()
        {
            return (vc.solarPanelsDeployable) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if at least one solar panel is moving.
        /// </summary>
        /// <returns>1 if any solar panels are moving (deploying or retracting).</returns>
        public double SolarPanelMoving()
        {
            return (vc.solarPanelsMoving) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the current output of installed solar panels.
        /// </summary>
        /// <returns>Solar panel output in ElectricCharge/sec.</returns>
        public double SolarPanelOutput()
        {
            return vc.netSolarOutput;
        }

        /// <summary>
        /// Returns a number representing deployable solar panel position:
        /// 
        /// * 0 = Broken
        /// * 1 = Retracted
        /// * 2 = Retracting
        /// * 3 = Extending
        /// * 4 = Extended
        /// 
        /// If there are multiple panels, the first non-broken panel's state
        /// is reported; if all panels are broken, the state will be 0.
        /// </summary>
        /// <returns>Panel Position (a number between 0 and 4); 1 if no panels are installed.</returns>
        public double SolarPanelPosition()
        {
            return vc.solarPanelPosition;
        }

        /// <summary>
        /// Returns 1 if at least one solar panel is retractable.
        /// </summary>
        /// <returns>1 if a solar panel is deployed, and it may be retracted; 0 otherwise.</returns>
        public double SolarPanelRetractable()
        {
            return (vc.solarPanelsRetractable) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Toggles fuel cells from off to on or vice versa.  Fuel cells that can
        /// not be manually controlled are not toggled.
        /// </summary>
        /// <returns>1 if fuel cells are now active, 0 if they're off or they could not be toggled.</returns>
        public double ToggleFuelCellActive()
        {
            bool state = !vc.fuelCellActive;
            bool anyChanged = false;
            for (int i = vc.moduleFuelCell.Length - 1; i >= 0; --i)
            {
                if (!vc.moduleFuelCell[i].AlwaysActive)
                {
                    anyChanged = true;
                    if (state)
                    {
                        vc.moduleFuelCell[i].StartResourceConverter();
                    }
                    else
                    {
                        vc.moduleFuelCell[i].StopResourceConverter();
                    }
                }
            }

            return (state && anyChanged) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Deploys / undeploys solar panels.
        /// </summary>
        /// <returns>1 if at least one panel is moving; 0 otherwise.</returns>
        public double ToggleSolarPanel()
        {
            bool anyMoving = false;
            if (vc.solarPanelsDeployable)
            {
                for (int i = vc.moduleSolarPanel.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleSolarPanel[i].useAnimation && vc.moduleSolarPanel[i].deployState == ModuleDeployablePart.DeployState.RETRACTED)
                    {
                        vc.moduleSolarPanel[i].Extend();
                        anyMoving = true;
                    }
                }
            }
            else if (vc.solarPanelsRetractable)
            {
                for (int i = vc.moduleSolarPanel.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleSolarPanel[i].useAnimation && vc.moduleSolarPanel[i].retractable && vc.moduleSolarPanel[i].deployState == ModuleDeployablePart.DeployState.EXTENDED)
                    {
                        vc.moduleSolarPanel[i].Retract();
                        anyMoving = true;
                    }
                }
            }

            return (anyMoving) ? 1.0 : 0.0;
        }
        #endregion

        /// <summary>
        /// The Radar category provides the interface for controlling MASRadar
        /// modules installed on the craft.
        /// </summary>
        #region Radar
        /// <summary>
        /// Returns 1 if any radars are turned on; 0 otherwise.
        /// </summary>
        /// <returns>1 if any radar is switched on; 0 otherwise.</returns>
        public double RadarActive()
        {
            return (vc.radarActive) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the number of radar modules available on the vessel.
        /// </summary>
        /// <returns>The count of the number of radar units installed on the vessel, 0 or higher.</returns>
        public double RadarCount()
        {
            return vc.moduleRadar.Length;
        }

        /// <summary>
        /// Toggle any installed radar from active to inactive.
        /// </summary>
        public void ToggleRadar()
        {
            bool state = !vc.radarActive;
            for (int i = vc.moduleRadar.Length - 1; i >= 0; --i)
            {
                vc.moduleRadar[i].radarEnabled = state;
            }
        }
        #endregion

        /// <summary>
        /// Random number generators are in this category.
        /// </summary>
        #region Random
        [MASProxyAttribute(Uncacheable = true)]
        /// <summary>
        /// Return a random number in the range of [0, 1]
        /// </summary>
        /// <returns>A uniformly-distributed pseudo-random number in the range [0, 1].</returns>
        public double Random()
        {
            return UnityEngine.Random.Range(0.0f, 1.0f);
        }

        [MASProxyAttribute(Uncacheable = true)]
        /// <summary>
        /// Return an approximation of a normal distribution with a mean and
        /// standard deviation as specified.  The actual result falls in the
        /// range of (-7, +7) for a mean of 0 and a standard deviation of 1.
        /// 
        /// fc.RandomNormal uses a Box-Muller approximation method modified
        /// to prevent a 0 in the u component (to avoid trying to take the
        /// log of 0).  The number was tweaked so for all practical purposes
        /// the range of numbers is about (-7, +7), as explained above.
        /// </summary>
        /// <param name="mean">The desired mean of the normal distribution.</param>
        /// <param name="stdDev">The desired standard deviation of the normal distribution.</param>
        /// <returns>A pseudo-random number that emulates a normal distribution.  See the summary for more detail.</returns>
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

        /// <summary>
        /// The RCS controls may be accessed in this category along with status
        /// variables.
        /// </summary>
        #region RCS
        /// <summary>
        /// Returns 1 if any RCS ports are disabled on the vessel.
        /// </summary>
        /// <returns>1 if any ports are disabled; 0 if all are enabled or there are no RCS ports.</returns>
        public double AnyRCSDisabled()
        {
            return (vc.anyRcsDisabled) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the current thrust percentage of all enabled RCS thrusters.  This number counts only active
        /// RCS ports.  Even so, it is possible for the result to be less than 1.0. For instance, if some thrusters
        /// are firing at less than full power to maintain orientation while translating, the net thrust will be
        /// less than 1.0.
        /// 
        /// The result does not account for thrust reductions in the atmosphere due to lower ISP, so sea level thrust
        /// will be a fraction of full thrust.
        /// </summary>
        /// <returns>A value between 0.0 and 1.0.</returns>
        public double CurrentRCSThrust()
        {
            return vc.rcsActiveThrustPercent;
        }

        /// <summary>
        /// Enables any RCS ports that have been disabled.
        /// </summary>
        public void EnableAllRCS()
        {
            for (int i = vc.moduleRcs.Length - 1; i >= 0; --i)
            {
                if (!vc.moduleRcs[i].rcsEnabled)
                {
                    // UNTESTED
                    vc.moduleRcs[i].rcsEnabled = true;
                    //vc.moduleRcs[i].Enable();
                }
            }
        }

        /// <summary>
        /// Returns 1 if the RCS action group has any actions attached to it.  Note that
        /// RCS thrusters don't neccessarily appear here.
        /// </summary>
        /// <returns>1 if any actions are assigned to the RCS group.</returns>
        public double RCSHasActions()
        {
            return (vc.GroupHasActions(KSPActionGroup.RCS)) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if RCS is on, 0 otherwise.
        /// </summary>
        /// <returns>1 if the RCS group is enabled, 0 otherwise.</returns>
        public double GetRCS()
        {
            return (vessel.ActionGroups[KSPActionGroup.RCS]) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if any RCS thrusters are firing, 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double GetRCSActive()
        {
            return (vc.anyRcsFiring) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the thrust-weighted average of the RCS thrust limit for
        /// all enabled RCS thrusters.
        /// </summary>
        /// <returns>A weighted average between 0 (no thrust) and 1 (full rated thrust).</returns>
        public double GetRCSThrustLimit()
        {
            return vc.rcsWeightedThrustLimit;
        }

        /// <summary>
        /// Returns 1 if there is at least once RCS module on the vessel.
        /// </summary>
        /// <returns></returns>
        public double HasRCS()
        {
            return (vc.moduleRcs.Length > 0) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Set the state of RCS.
        /// </summary>
        /// <param name="active">`true` to enable RCS, `false` to disable RCS.</param>
        public void SetRCS(bool active)
        {
            vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, active);
        }

        /// <summary>
        /// Set the maximum thrust limit of the RCS thrusters.
        /// </summary>
        /// <param name="limit">A value between 0 (no thrust) and 1 (full thrust).</param>
        public void SetRCSThrustLimit(double limit)
        {
            float flimit = Math.Max(0.0f, Math.Min(1.0f, (float)limit)) * 100.0f;

            for (int i = vc.moduleRcs.Length - 1; i >= 0; --i)
            {
                vc.moduleRcs[i].thrustPercentage = flimit;
            }
        }

        /// <summary>
        /// Toggle RCS off-to-on or vice versa.
        /// </summary>
        public void ToggleRCS()
        {
            vessel.ActionGroups.ToggleGroup(KSPActionGroup.RCS);
        }
        #endregion RCS

        /// <summary>
        /// Methods for controlling and reporting information from reaction wheels are in this
        /// category.
        /// 
        /// Unlike other categories, the reaction wheels methods can be used to inspect the
        /// reaction wheels installed in the current pod (when `currentPod` is true), or the
        /// methods can be used to inspect all reaction wheels *not* in the current pod (when
        /// `currentPod` is false).  To inspect values for all reaction wheels (current pod
        /// and rest of vessel), sum the results together (with the exception of ReactionWheelState).
        /// </summary>
        #region Reaction Wheels
        /// <summary>
        /// Returns the current amount of torque being applied for pitch.
        /// 
        /// When `absoluteTorque` is true, the value that is returns is a signed
        /// value that ranges from the -PitchTorque to + PitchTorque.
        /// 
        /// When `absoluteTorque` is false, the value is a percentage of maximum,
        /// ranging from -1 to +1, where -1 indicates maximum torque in the negative
        /// direction.
        /// </summary>
        /// <param name="currentPod">If `true`, the state of the current pod's reaction wheel is reported.
        /// If `false`, the state of all other reaction wheels are reported.</param>
        /// <param name="absoluteTorque">If `true`, returns the pitch torque in kN.  If `false`, returns the
        /// torque as a percentage of maximum.</param>
        /// <returns>Pitch torque.  Negative values indicate pitch down.  See summary for details.</returns>
        public double ReactionWheelPitch(bool currentPod, bool absoluteTorque)
        {
            Part fcPart = fc.part;

            float netTorque = 0.0f;
            if (currentPod)
            {
                for (int i = vc.moduleReactionWheel.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleReactionWheel[i].part == fcPart)
                    {
                        netTorque = vc.moduleReactionWheel[i].inputVector.x / ((absoluteTorque) ? 1.0f : vc.moduleReactionWheel[i].PitchTorque);
                        break;
                    }
                }
            }
            else
            {
                for (int i = vc.moduleReactionWheel.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleReactionWheel[i].part != fcPart)
                    {
                        netTorque += vc.moduleReactionWheel[i].inputVector.x / ((absoluteTorque) ? 1.0f : vc.moduleReactionWheel[i].PitchTorque);
                    }
                }
            }

            return netTorque;
        }

        /// <summary>
        /// Returns the current amount of torque being applied for roll.
        /// 
        /// When `absoluteTorque` is true, the value that is returns is a signed
        /// value that ranges from the -PitchTorque to + PitchTorque.
        /// 
        /// When `absoluteTorque` is false, the value is a percentage of maximum,
        /// ranging from -1 to +1, where -1 indicates maximum torque in the negative
        /// direction.
        /// </summary>
        /// <param name="currentPod">If `true`, the state of the current pod's reaction wheel is reported.
        /// If `false`, the state of all other reaction wheels are reported.</param>
        /// <param name="absoluteTorque">If `true`, returns the roll torque in kN.  If `false`, returns the
        /// torque as a percentage of maximum.</param>
        /// <returns>Roll torque.  Negative values indicate roll anti-clockwise.  See summary for details.</returns>
        public double ReactionWheelRoll(bool currentPod, bool absoluteTorque)
        {
            Part fcPart = fc.part;

            float netTorque = 0.0f;
            if (currentPod)
            {
                for (int i = vc.moduleReactionWheel.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleReactionWheel[i].part == fcPart)
                    {
                        netTorque = vc.moduleReactionWheel[i].inputVector.y / ((absoluteTorque) ? 1.0f : vc.moduleReactionWheel[i].RollTorque);
                        break;
                    }
                }
            }
            else
            {
                for (int i = vc.moduleReactionWheel.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleReactionWheel[i].part != fcPart)
                    {
                        netTorque += vc.moduleReactionWheel[i].inputVector.y / ((absoluteTorque) ? 1.0f : vc.moduleReactionWheel[i].RollTorque);
                    }
                }
            }

            return netTorque;
        }

        /// <summary>
        /// Returns the state of any reaction wheels installed in the current IVA pod.
        /// Possible values are:
        /// 
        /// * **-1**: Reaction wheel damaged, disabled, or not installed.
        /// * **0**: Reaction wheel enabled and idle.
        /// * **+1**: Reaction wheel enabled and applying torque.
        /// 
        /// When `currentPod` is `false`, the value indicates whether *any* wheel is
        /// applying torque.  If none are active, the value indicates whether *any*
        /// wheel is idle.
        /// </summary>
        /// <param name="currentPod">If `true`, the state of the current pod's reaction wheel is reported.
        /// If `false`, the state of all other reaction wheels are reported.</param>
        /// <returns>-1, 0, or 1 as described in the summary.</returns>
        public double ReactionWheelState(bool currentPod)
        {
            Part fcPart = fc.part;
            if (currentPod)
            {
                ModuleReactionWheel rWheel = null;
                for (int i = vc.moduleReactionWheel.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleReactionWheel[i].part == fcPart)
                    {
                        rWheel = vc.moduleReactionWheel[i];
                        break;
                    }
                }

                if (rWheel != null)
                {
                    if (rWheel.wheelState == ModuleReactionWheel.WheelState.Active)
                    {
                        if (rWheel.inputSum > 0.001f)
                        {
                            return 1.0;
                        }
                        else
                        {
                            return 0.0;
                        }
                    }
                    else
                    {
                        return -1.0;
                    }
                }
                else
                {
                    return -1.0;
                }
            }
            else
            {
                bool anyEnabled = false;
                bool anyActive = false;

                for (int i = vc.moduleReactionWheel.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleReactionWheel[i].part != fcPart)
                    {
                        if (vc.moduleReactionWheel[i].wheelState == ModuleReactionWheel.WheelState.Active)
                        {
                            anyEnabled = true;

                            if (vc.moduleReactionWheel[i].inputSum > 0.001f)
                            {
                                // Since anyActive takes priority, we exit the loop early here.
                                anyActive = true;
                                break;
                            }
                        }
                    }
                }

                if (anyActive)
                {
                    return 1.0;
                }
                else if (anyEnabled)
                {
                    return 0.0;
                }
                else
                {
                    return -1.0;
                }
            }
        }

        /// <summary>
        /// Toggle the reaction wheels, either on the current pod or all wheels
        /// outside of the current pod.
        /// </summary>
        /// <param name="currentPod">If `true`, the current pod's reaction wheel is toggled.
        /// If `false`, all other reaction wheels are toggled.</param>
        public void ToggleReactionWheel(bool currentPod)
        {
            Part fcPart = fc.part;
            if (currentPod)
            {
                for (int i = vc.moduleReactionWheel.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleReactionWheel[i].part == fcPart)
                    {
                        vc.moduleReactionWheel[i].OnToggle();
                        return;
                    }
                }
            }
            else
            {
                for (int i = vc.moduleReactionWheel.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleReactionWheel[i].part != fcPart)
                    {
                        vc.moduleReactionWheel[i].OnToggle();
                    }
                }
            }
        }

        /// <summary>
        /// Returns the current amount of torque being applied for yaw.
        /// 
        /// When `absoluteTorque` is true, the value that is returns is a signed
        /// value that ranges from the -YawTorque to +YawTorque.
        /// 
        /// When `absoluteTorque` is false, the value is a percentage of maximum,
        /// ranging from -1 to +1, where -1 indicates maximum torque in the negative
        /// direction.
        /// </summary>
        /// <param name="currentPod">If `true`, the state of the current pod's reaction wheel is reported.
        /// If `false`, the state of all other reaction wheels are reported.</param>
        /// <param name="absoluteTorque">If `true`, returns the yaw torque in kN.  If `false`, returns the
        /// torque as a percentage of maximum.</param>
        /// <returns>Yaw torque.  Negative values indicate yaw left.  See summary for details.</returns>
        public double ReactionWheelYaw(bool currentPod, bool absoluteTorque)
        {
            Part fcPart = fc.part;

            float netTorque = 0.0f;
            if (currentPod)
            {
                for (int i = vc.moduleReactionWheel.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleReactionWheel[i].part == fcPart)
                    {
                        netTorque = vc.moduleReactionWheel[i].inputVector.z / ((absoluteTorque) ? 1.0f : vc.moduleReactionWheel[i].YawTorque);
                        break;
                    }
                }
            }
            else
            {
                for (int i = vc.moduleReactionWheel.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleReactionWheel[i].part != fcPart)
                    {
                        netTorque += vc.moduleReactionWheel[i].inputVector.z / ((absoluteTorque) ? 1.0f : vc.moduleReactionWheel[i].YawTorque);
                    }
                }
            }

            return netTorque;
        }
        #endregion

        /// <summary>
        /// The resource methods report the availability of various resources aboard the
        /// vessel.  They are grouped into three types.
        /// 
        /// 'Power' methods `PowerCurrent()`, etc,
        /// report the state of the resource identified in the MAS config file.  By default,
        /// this is `ElectricCharge`, but mods may use a different resource for power, instead.
        /// By using the 'Power' methods, IVA makers do not have to worry about adapting their
        /// IVA configurations for use with modded configurations, as long as the player has
        /// correctly configured MAS to use the alternative power name.
        /// 
        /// 'Propellant' methods track all of the active fuel types being used by ModuleEngines
        /// and ModuleEnginesFX.  Instead of reporting the current and maximum amounts in units,
        /// like the 'Resource' methods do, these methods report amounts in kilograms.  Using
        /// the propellant mass allows these methods to track alternate fuel types (such as mods
        /// using LHyd + Oxidizer), with the downside being mixed engine configurations, such as
        /// solid rockets + liquid-fueled engines) may be less helpful
        /// 
        /// 'Rcs' methods work similarly to the 'Propellant' methods, but they track resource types
        /// consumed by ModuleRCS and ModuleRCSFX.
        /// 
        /// 'Resource' methods that take a numeric parameter are ordinal resource listers.  The
        /// numeric parameter is a number from 0 to fc.ResourceCount() - 1.  This allows the
        /// IVA maker to display an alphabetized list of resources (on an MFD, for instance).
        /// 
        /// 'Resource' methods that take a string parameter return the named resource.  The name
        /// must match the `name` field of a `RESOURCE_DEFINITION` config node, or 0 will be returned.
        /// </summary>
        #region Resources
        /// <summary>
        /// Returns the current level of available power for the designated
        /// "Power" resource; by default, this is ElectricCharge.
        /// </summary>
        /// <returns>Current units of power.</returns>
        public double PowerCurrent()
        {
            return vc.ResourceCurrent(MASConfig.ElectricCharge);
        }

        /// <summary>
        /// Returns the rate of change in available power (units/sec) for the
        /// designated "Power" resource; by default, this is ElectricCharge.
        /// </summary>
        /// <returns></returns>
        public double PowerDelta()
        {
            return vc.ResourceDelta(MASConfig.ElectricCharge);
        }

        /// <summary>
        /// Returns the maximum capacity of the resource defined as "power" in
        /// the config.  By default, this is ElectricCharge.
        /// </summary>
        /// <returns></returns>
        public double PowerMax()
        {
            return vc.ResourceMax(MASConfig.ElectricCharge);
        }

        /// <summary>
        /// Returns the current percentage of maximum capacity of the resource
        /// designated as "power" - in a stock installation, this would be
        /// ElectricCharge.
        /// </summary>
        /// <returns></returns>
        public double PowerPercent()
        {
            return vc.ResourcePercent(MASConfig.ElectricCharge);
        }
        /// <summary>
        /// Reports whether the vessel's power percentage falls between the two listed bounds.
        /// The bounds do not need to be in numerical order.
        /// 
        /// If there is no power onboard, returns 0.  Doing so makes this
        /// function useful for alerts, for example.
        /// </summary>
        /// <param name="firstBound">The first boundary percentage, between 0 and 1.</param>
        /// <param name="secondBound">The second boundary percentage, between 0 and 1.</param>
        /// <returns>1 if the power percentage is between the listed bounds.</returns>
        public double PowerThreshold(double firstBound, double secondBound)
        {
            double vesselMax = vc.ResourceMax(MASConfig.ElectricCharge);
            if (vesselMax > 0.0f)
            {
                double min = Math.Min(firstBound, secondBound);
                double max = Math.Max(firstBound, secondBound);
                double percent = vc.ResourcePercent(MASConfig.ElectricCharge);

                if (percent >= min && percent <= max)
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Reports the total mass, in kg, of resources consumed by all currently-active engines on the vessel.
        /// </summary>
        /// <returns>The current mass of the active propellants in the vessel, in kg.</returns>
        public double PropellantCurrent()
        {
            return vc.enginePropellant.currentQuantity;
        }

        /// <summary>
        /// Reports the propellant consumption rate in kg/s for all active engines on the vessel.
        /// </summary>
        /// <returns>The current propellant consumption rate, in kg/s.</returns>
        public double PropellantDelta()
        {
            return vc.enginePropellant.deltaPerSecond;
        }

        /// <summary>
        /// Reports the maximum amount of propellant, in kg, that may be carried aboard the vessel.
        /// </summary>
        /// <returns>The maximum propellant capacity, in kg.</returns>
        public double PropellantMax()
        {
            return vc.enginePropellant.maxQuantity;
        }

        /// <summary>
        /// Reports the current percentage of propellant aboard the vessel.
        /// </summary>
        /// <returns>The percentage of maximum propellant capacity that contains propellant, between 0 and 1.</returns>
        public double PropellantPercent()
        {
            return (vc.enginePropellant.maxQuantity > 0.0f) ? (vc.enginePropellant.currentQuantity / vc.enginePropellant.maxQuantity) : 0.0;
        }

        /// <summary>
        /// Reports the current amount of propellant available, in kg, to active engines on the current stage.
        /// </summary>
        /// <returns>The current mass of propellant accessible by the current stage, in kg.</returns>
        public double PropellantStageCurrent()
        {
            return vc.enginePropellant.currentStage;
        }

        /// <summary>
        /// Reports the maximum amount of propellant available, in kg, to the active engiens on the
        /// current stage.
        /// </summary>
        /// <returns>The maximum mass of propellant accessibly by the current stage, in kg.</returns>
        public double PropellantStageMax()
        {
            return vc.enginePropellant.maxStage;
        }

        /// <summary>
        /// Reports the percentage of propellant remaining on the current stage for the active engines.
        /// </summary>
        /// <returns>The percentage of maximum stage propellant capacity that contains propellant, between 0 and 1.</returns>
        public double PropellantStagePercent()
        {
            return (vc.enginePropellant.maxStage > 0.0f) ? (vc.enginePropellant.currentStage / vc.enginePropellant.maxStage) : 0.0;
        }

        /// <summary>
        /// Reports whether the current stage propellant percentage falls between the two listed bounds.
        /// The bounds do not need to be in numerical order.
        /// 
        /// If there is no propellant on the current stage, returns 0.  Doing so makes this
        /// function useful for alerts, for example.
        /// </summary>
        /// <param name="firstBound">The first boundary percentage, between 0 and 1.</param>
        /// <param name="secondBound">The second boundary percentage, between 0 and 1.</param>
        /// <returns>1 if current stage propellant is between the listed bounds.</returns>
        public double PropellantStageThreshold(double firstBound, double secondBound)
        {
            if (vc.enginePropellant.maxStage > 0.0f)
            {
                double min = Math.Min(firstBound, secondBound);
                double max = Math.Max(firstBound, secondBound);
                double percent = vc.enginePropellant.currentStage / vc.enginePropellant.maxStage;

                if (percent >= min && percent <= max)
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Reports whether the vessel's propellant percentage falls between the two listed bounds.
        /// The bounds do not need to be in numerical order.
        /// 
        /// If there is no propellant or active engines, returns 0.  Doing so makes this
        /// function useful for alerts, for example.
        /// </summary>
        /// <param name="firstBound">The first boundary percentage, between 0 and 1.</param>
        /// <param name="secondBound">The second boundary percentage, between 0 and 1.</param>
        /// <returns>1 if propellant is between the listed bounds.</returns>
        public double PropellantThreshold(double firstBound, double secondBound)
        {
            if (vc.enginePropellant.maxQuantity > 0.0f)
            {
                double min = Math.Min(firstBound, secondBound);
                double max = Math.Max(firstBound, secondBound);
                double percent = vc.enginePropellant.currentQuantity / vc.enginePropellant.maxQuantity;

                if (percent >= min && percent <= max)
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Tracks the current total mass of all resources consumed by installed RCS thrusters.
        /// </summary>
        /// <returns>Total RCS propellant mass in kg.</returns>
        public double RcsCurrent()
        {
            return vc.rcsPropellant.currentQuantity;
        }

        /// <summary>
        /// Tracks the current resource consumption rate by installed RCS thrusters.
        /// </summary>
        /// <returns>RCS propellant consumption rate in kg/s.</returns>
        public double RcsDelta()
        {
            return vc.rcsPropellant.deltaPerSecond;
        }

        /// <summary>
        /// Tracks the total mass that can be carried in all RCS propellant tanks.
        /// </summary>
        /// <returns>Maximum propellant capacity in kg.</returns>
        public double RcsMax()
        {
            return vc.rcsPropellant.maxQuantity;
        }

        /// <summary>
        /// Tracks the percentage of total RCS propellant mass currently onboard.
        /// </summary>
        /// <returns>Current RCS propellant supply, between 0 and 1.</returns>
        public double RcsPercent()
        {
            return (vc.rcsPropellant.maxQuantity > 0.0f) ? (vc.rcsPropellant.currentQuantity / vc.rcsPropellant.maxQuantity) : 0.0;
        }

        /// <summary>
        /// Reports the current amount of RCS propellant available to the active stage.
        /// </summary>
        /// <returns>Available RCS propellant, in kg.</returns>
        public double RcsStageCurrent()
        {
            return vc.rcsPropellant.currentStage;
        }

        /// <summary>
        /// Reports the maximum amount of RCS propellant storage accessible by the current stage.
        /// </summary>
        /// <returns>Maximum stage RCS propellant mass, in kg.</returns>
        public double RcsStageMax()
        {
            return vc.rcsPropellant.maxStage;
        }

        /// <summary>
        /// Reports the percentage of RCS propellant mass available to the current stage.
        /// </summary>
        /// <returns>Current stage percentage, between 0 and 1.</returns>
        public double RcsStagePercent()
        {
            return (vc.rcsPropellant.maxStage > 0.0f) ? (vc.rcsPropellant.currentStage / vc.rcsPropellant.maxStage) : 0.0;
        }

        /// <summary>
        /// Reports whether the current stage RCS propellant percentage falls between the two listed bounds.
        /// The bounds do not need to be in numerical order.
        /// 
        /// If there is no RCS propellant on the current stage, returns 0.  Doing so makes this
        /// function useful for alerts, for example.
        /// </summary>
        /// <param name="firstBound">The first boundary percentage, between 0 and 1.</param>
        /// <param name="secondBound">The second boundary percentage, between 0 and 1.</param>
        /// <returns>1 if current stage RCS propellant is between the listed bounds.</returns>
        public double RcsStageThreshold(double firstBound, double secondBound)
        {
            if (vc.rcsPropellant.maxStage > 0.0f)
            {
                double min = Math.Min(firstBound, secondBound);
                double max = Math.Max(firstBound, secondBound);
                double percent = vc.rcsPropellant.currentStage / vc.rcsPropellant.maxStage;

                if (percent >= min && percent <= max)
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Reports whether the vessel's RCS propellant percentage falls between the two listed bounds.
        /// The bounds do not need to be in numerical order.
        /// 
        /// If there is no RCS propellant, returns 0.  Doing so makes this
        /// function useful for alerts, for example.
        /// </summary>
        /// <param name="firstBound">The first boundary percentage, between 0 and 1.</param>
        /// <param name="secondBound">The second boundary percentage, between 0 and 1.</param>
        /// <returns>1 if RCS propellant is between the listed bounds.</returns>
        public double RcsThreshold(double firstBound, double secondBound)
        {
            if (vc.rcsPropellant.maxQuantity > 0.0f)
            {
                double min = Math.Min(firstBound, secondBound);
                double max = Math.Max(firstBound, secondBound);
                double percent = vc.rcsPropellant.currentQuantity / vc.rcsPropellant.maxQuantity;

                if (percent >= min && percent <= max)
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

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
        /// 
        /// A positive number means the resource is being consumed (burning fuel,
        /// for instance).
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
        /// 
        /// A positive number means the resource is being consumed (burning fuel,
        /// for instance).
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
        /// <returns>Density in kg / unit</returns>
        public double ResourceDensity(double resourceId)
        {
            return vc.ResourceDensity((int)resourceId) * 1000.0;
        }

        /// <summary>
        /// Returns the density of the named resource, or zero if it wasn't found.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns>Density in kg / unit</returns>
        public double ResourceDensity(string resourceName)
        {
            return vc.ResourceDensity(resourceName) * 1000.0;
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
        /// Returns the current mass of the Nth resource in kg.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public double ResourceMass(double resourceId)
        {
            return vc.ResourceMass((int)resourceId) * 1000.0;
        }

        /// <summary>
        /// Returns the mass of the current resource supply
        /// in kg.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public double ResourceMass(string resourceName)
        {
            return vc.ResourceMass(resourceName) * 1000.0;
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
        /// Returns the max amount of the Nth resource in the current stage.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public double ResourceStagePercent(double resourceId)
        {
            int id = (int)resourceId;
            double stageMax = vc.ResourceStageMax(id);
            if (stageMax > 0.0)
            {
                return vc.ResourceStageCurrent(id) / stageMax;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the maximum amount of the resource in the current stage.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public double ResourceStagePercent(string resourceName)
        {
            double stageMax = vc.ResourceStageMax(resourceName);
            if (stageMax > 0.0)
            {
                return vc.ResourceStageCurrent(resourceName) / stageMax;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Reports whether the named resource's current stage percentage falls between the two listed bounds.
        /// The bounds do not need to be in numerical order.
        /// 
        /// If there is no such resource on the current stage, returns 0.  Doing so makes this
        /// function useful for alerts, for example.
        /// </summary>
        /// <param name="firstBound">The first boundary percentage, between 0 and 1.</param>
        /// <param name="secondBound">The second boundary percentage, between 0 and 1.</param>
        /// <returns>1 if current stage resource percentage is between the listed bounds.</returns>
        public double ResourceStageThreshold(string resourceName, double firstBound, double secondBound)
        {
            double stageMax = vc.ResourceStageMax(resourceName);
            if (stageMax > 0.0f)
            {
                double min = Math.Min(firstBound, secondBound);
                double max = Math.Max(firstBound, secondBound);
                double percent = vc.ResourceStageCurrent(resourceName) / stageMax;

                if (percent >= min && percent <= max)
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Reports whether the vessel's total resource percentage falls between the two listed bounds.
        /// The bounds do not need to be in numerical order.
        /// 
        /// If there is no resource capacity onboard, returns 0.  Doing so makes this
        /// function useful for alerts, for example.
        /// </summary>
        /// <param name="firstBound">The first boundary percentage, between 0 and 1.</param>
        /// <param name="secondBound">The second boundary percentage, between 0 and 1.</param>
        /// <returns>1 if the resource percentage is between the listed bounds.</returns>
        public double ResourceThreshold(string resourceName, double firstBound, double secondBound)
        {
            double vesselMax = vc.ResourceMax(resourceName);
            if (vesselMax > 0.0f)
            {
                double min = Math.Min(firstBound, secondBound);
                double max = Math.Max(firstBound, secondBound);
                double percent = vc.ResourcePercent(resourceName);

                if (percent >= min && percent <= max)
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns 1 when there is at least 0.0001 units of power available
        /// to the craft.  By default, 'power' is the ElectricCharge resource,
        /// but users may change that in the MAS config file.
        /// </summary>
        /// <returns>1 if there is ElectricCharge, 0 otherwise.</returns>
        public double VesselPowered()
        {
            return (vesselPowered) ? 1.0 : 0.0;
        }
        #endregion

        /// <summary>
        /// The SAS section provides methods to control and query the state of
        /// a vessel's SAS stability system.
        /// </summary>
        #region SAS
        /// <summary>
        /// Returns whether the controls are configured for precision mode.
        /// </summary>
        /// <returns>1 if the controls are in precision mode, 0 if they are not.</returns>
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
        /// Returns a number representing the SAS mode:
        ///
        /// * 0 = StabilityAssist
        /// * 1 = Prograde
        /// * 2 = Retrograde
        /// * 3 = Normal
        /// * 4 = Anti-Normal
        /// * 5 = Radial In
        /// * 6 = Radial Out
        /// * 7 = Target
        /// * 8 = Anti-Target
        /// * 9 = Maneuver Node
        /// </summary>
        /// <returns>A number between 0 and 9, inclusive.</returns>
        public double GetSASMode()
        {
            double mode;
            switch (autopilotMode)
            {
                case VesselAutopilot.AutopilotMode.StabilityAssist:
                    mode = 0.0;
                    break;
                case VesselAutopilot.AutopilotMode.Prograde:
                    mode = 1.0;
                    break;
                case VesselAutopilot.AutopilotMode.Retrograde:
                    mode = 2.0;
                    break;
                case VesselAutopilot.AutopilotMode.Normal:
                    mode = 3.0;
                    break;
                case VesselAutopilot.AutopilotMode.Antinormal:
                    mode = 4.0;
                    break;
                case VesselAutopilot.AutopilotMode.RadialIn:
                    mode = 5.0;
                    break;
                case VesselAutopilot.AutopilotMode.RadialOut:
                    mode = 6.0;
                    break;
                case VesselAutopilot.AutopilotMode.Target:
                    mode = 7.0;
                    break;
                case VesselAutopilot.AutopilotMode.AntiTarget:
                    mode = 8.0;
                    break;
                case VesselAutopilot.AutopilotMode.Maneuver:
                    mode = 9.0;
                    break;
                default:
                    mode = 0.0;
                    break;
            }

            return mode;
        }

        /// <summary>
        /// Return the current speed display mode: 1 for orbit, 0 for surface,
        /// and -1 for target.
        /// </summary>
        /// <returns></returns>
        public double GetSASSpeedMode()
        {
            var mode = FlightGlobals.speedDisplayMode;

            if (mode == FlightGlobals.SpeedDisplayModes.Orbit)
            {
                return 1.0;
            }
            else if (mode == FlightGlobals.SpeedDisplayModes.Target)
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
        /// Set the SAS mode.  Note that while you can set this mode when SAS is off, KSP
        /// sets it back to Stability Assist when SAS is switched on.  Valid modes are:
        /// 
        /// * 0 = StabilityAssist
        /// * 1 = Prograde
        /// * 2 = Retrograde
        /// * 3 = Normal
        /// * 4 = Anti-Normal
        /// * 5 = Radial In
        /// * 6 = Radial Out
        /// * 7 = Target
        /// * 8 = Anti-Target
        /// * 9 = Maneuver Node
        /// </summary>
        /// <param name="mode">One of the modes listed above.  If an invalid value is provided, Stability Assist is set.</param>
        /// <returns>1 if the mode was set, 0 if an invalid mode was specified</returns>
        public double SetSASMode(double mode)
        {
            int iMode = (int)mode;
            double returnVal = 1.0;
            switch (iMode)
            {
                case 0:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.StabilityAssist);
                    break;
                case 1:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.Prograde);
                    break;
                case 2:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.Retrograde);
                    break;
                case 3:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.Normal);
                    break;
                case 4:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.Antinormal);
                    break;
                case 5:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.RadialIn);
                    break;
                case 6:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.RadialOut);
                    break;
                case 7:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.Target);
                    break;
                case 8:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.AntiTarget);
                    break;
                case 9:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.Maneuver);
                    break;
                default:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.StabilityAssist);
                    returnVal = 0.0;
                    break;
            }

            return returnVal;
        }

        /// <summary>
        /// Toggle precision control mode
        /// </summary>
        /// <returns>1 if precision mode is now on, 0 if it is now off.</returns>
        public double TogglePrecisionMode()
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

            return (state) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Toggles SAS on-to-off or vice-versa
        /// </summary>
        /// <returns>1 if SAS is now on, 0 if it is now off.</returns>
        public double ToggleSAS()
        {
            vessel.ActionGroups.ToggleGroup(KSPActionGroup.SAS);
            return (vessel.ActionGroups[KSPActionGroup.SAS]) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Toggles the SAS speed mode.
        /// </summary>
        /// <returns>The new speed mode (see `fc.GetSASSpeedMode()`).</returns>
        public double ToggleSASSpeedMode()
        {
            FlightGlobals.CycleSpeedModes();
            return GetSASSpeedMode();
        }

        /// <summary>
        /// Internal method to set SAS mode and update the UI.
        /// TODO: Radial Out / Radial In may be backwards (either in the display,
        /// or in the enums).
        /// </summary>
        /// <param name="mode">Mode to set</param>
        private void TrySetSASMode(VesselAutopilot.AutopilotMode mode)
        {
            if (vessel.Autopilot.CanSetMode(mode))
            {
                vessel.Autopilot.SetMode(mode);

                if (SASbtns == null)
                {
                    SASbtns = UnityEngine.Object.FindObjectOfType<VesselAutopilotUI>().modeButtons;
                }
                // set our mode, note it takes the mode as an int, generally top to bottom, left to right, as seen on the screen. Maneuver node being the exception, it is 9
                SASbtns[(int)mode].SetState(true);
            }
        }
        #endregion

        /// <summary>
        /// Variables related to the vessels speed, velocity, and accelerations are grouped
        /// in this category.
        /// </summary>
        #region Speed, Velocity, and Acceleration

        /// <summary>
        /// Returns the current acceleration of the vessel from engines, in m/s^2.
        /// </summary>
        /// <returns>Acceleration in m/s^2.</returns>
        public double Acceleration()
        {
            return vc.currentThrust / vessel.totalMass;
        }

        /// <summary>
        /// Returns the rate at which the vessel's distance to the ground
        /// is changing.  This is the vertical speed as measured from vessel
        /// to surface, as opposed to measuring from a fixed altitude.  When
        /// over an ocean, sea level is used as the ground height (in other
        /// words, `fc.AltitudeTerrain(false)`).
        /// 
        /// Because terrain may be rough, this value may be noisy.  It is
        /// smoothed using exponential smoothing, so the rate is not
        /// instantaneously precise.
        /// </summary>
        /// <returns>Rate of change of terrain altitude in m/s.</returns>
        public double AltitudeTerrainRate()
        {
            return vc.altitudeTerrainRate;
        }

        /// <summary>
        /// Returns the approach speed (the rate of closure directly towards
        /// the target).  Returns 0 if there's no target or all relative
        /// movement is perpendicular to the approach direction.
        /// </summary>
        /// <returns>Approach speed in m/s.  Returns 0 if there is no target.</returns>
        public double ApproachSpeed()
        {
            if (vc.activeTarget != null)
            {
                return Vector3d.Dot(vc.targetRelativeVelocity, vc.targetDirection);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the speed selected by the speed mode (surface, orbit, or target)
        /// in m/s.
        /// This value is equivalent to the speed displayed over the NavBall in the UI.
        /// </summary>
        /// <returns>Current speed in m/s.</returns>
        public double CurrentSpeedModeSpeed()
        {
            switch (FlightGlobals.speedDisplayMode)
            {
                case FlightGlobals.SpeedDisplayModes.Orbit:
                    return vessel.obt_speed;
                case FlightGlobals.SpeedDisplayModes.Surface:
                    return vessel.srfSpeed;
                case FlightGlobals.SpeedDisplayModes.Target:
                    return vc.targetSpeed;
                default:
                    return 0.0;
            }
        }

        /// <summary>
        /// Compute equivalent airspeed based on current surface speed and atmospheric density.
        /// 
        /// https://en.wikipedia.org/wiki/Equivalent_airspeed
        /// </summary>
        /// <returns>EAS in m/s.</returns>
        public double EquivalentAirspeed()
        {
            double densityRatio = vessel.atmDensity / 1.225;
            return vessel.srfSpeed * Math.Sqrt(densityRatio);
        }

        /// <summary>
        /// Returns the magnitude g-forces currently affecting the craft, in gees.
        /// </summary>
        /// <returns>Current instantaneous force in Gs.</returns>
        public double GForce()
        {
            return vessel.geeForce_immediate;
        }

        /// <summary>
        /// Measure of the surface speed of the vessel after removing the
        /// vertical component, in m/s.
        /// </summary>
        /// <returns>Horizontal surface speed in m/s.</returns>
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
        /// Returns the indicated airspeed in m/s, based on current surface speed, atmospheric density, and Mach number.
        /// </summary>
        /// <returns>IAS in m/s.</returns>
        public double IndicatedAirspeed()
        {
            // We compute this because this formula is basically what FAR uses; Vessel.indicatedAirSpeed
            // gives drastically different results while in motion.
            double densityRatio = vessel.atmDensity / 1.225;
            double pressureRatio = Utility.StagnationPressure(vc.mainBody.atmosphereAdiabaticIndex, vessel.mach);
            return vessel.srfSpeed * Math.Sqrt(densityRatio) * pressureRatio;
        }

        /// <summary>
        /// Returns the vessel's current Mach number (multiple of the speed of sound).
        /// This number only makes sense in an atmosphere.
        /// </summary>
        /// <returns>Vessel speed as a factor of the speed of sound.</returns>
        public double MachNumber()
        {
            return vessel.mach;
        }

        /// <summary>
        /// Return the orbital speed of the vessel in m/s
        /// </summary>
        /// <returns>Orbital speed in m/s.</returns>
        public double OrbitSpeed()
        {
            return vessel.obt_speed;
        }

        /// <summary>
        /// Returns +1 if the KSP automatic speed display is set to "Orbit",
        /// +0 if it's "Surface", and -1 if it's "Target".  This mode affects
        /// SAS behaviors, so it's useful to know.
        /// </summary>
        /// <returns>1 for "Orbit" mode, 0 for "Surface" mode, and -1 for "Target" mode.</returns>
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
        /// Returns the component of surface velocity relative to the nose of
        /// the craft, in m/s.  If the vessel is near vertical, the 'forward'
        /// vector is treated as the vector that faces 'down' in a horizontal
        /// cockpit configuration.
        /// </summary>
        /// <returns>The vessel's velocity fore/aft velocity in m/s.</returns>
        public double SurfaceForwardSpeed()
        {
            return Vector3.Dot(vc.surfacePrograde, vc.surfaceForward);
        }

        /// <summary>
        /// Returns the lateral (right/left) component of surface velocity in
        /// m/s.  This value could become zero at extreme roll orientations.
        /// Positive values are to the right, negative to the left.
        /// </summary>
        /// <returns>The vessel's left/right velocity in m/s.  Right is positive; left is negative.</returns>
        public double SurfaceLateralSpeed()
        {
            return Vector3.Dot(vc.surfacePrograde, vc.surfaceRight);
        }

        /// <summary>
        /// Return the surface-relative speed of the vessel in m/s.
        /// </summary>
        /// <returns>Surface speed in m/s.</returns>
        public double SurfaceSpeed()
        {
            return vessel.srfSpeed;
        }

        /// <summary>
        /// Target-relative speed in m/s.  0 if no target.
        /// </summary>
        /// <returns>Speed relative to the target in m/s.  0 if there is no target.</returns>
        public double TargetSpeed()
        {
            return vc.targetSpeed;
        }

        /// <summary>
        /// Returns the vertical speed of the vessel in m/s.
        /// </summary>
        /// <returns>Surface-relative vertical speed in m/s.</returns>
        public double VerticalSpeed()
        {
            return vessel.verticalSpeed;
        }
        #endregion

        /// <summary>
        /// Controls for staging a vessel, and controlling the stage lock, and information
        /// related to both staging and stage locks are all in the Staging category.
        /// </summary>
        #region Staging
        /// <summary>
        /// Returns the current stage.
        /// </summary>
        /// <returns>A whole number 0 or larger.</returns>
        public double CurrentStage()
        {
            return StageManager.CurrentStage;
        }

        /// <summary>
        /// Returns 1 if staging is locked, 0 otherwise.
        /// </summary>
        /// <returns>1 if staging is locked, 0 if staging is unlocked.</returns>
        public double GetStageLocked()
        {
            return (InputLockManager.IsLocked(ControlTypes.STAGING)) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Sets stage locking to the specified setting (true or false).
        /// </summary>
        /// <param name="locked">`true` to lock staging, `false` to unlock staging.</param>
        public void SetStageLocked(bool locked)
        {
            if (locked)
            {
                InputLockManager.SetControlLock(ControlTypes.STAGING, "manualStageLock");
            }
            else
            {
                InputLockManager.RemoveControlLock("manualStageLock");
            }
        }

        /// <summary>
        /// Activate the next stage.
        /// </summary>
        /// <returns>1 if the vessel staged; 0 otherwise.</returns>
        public double Stage()
        {
            if (StageManager.CanSeparate && InputLockManager.IsUnlocked(ControlTypes.STAGING))
            {
                StageManager.ActivateNextStage();
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Can the vessel stage?
        /// </summary>
        /// <returns>1 if the vessel can stage, and staging is unlocked; 0 otherwise.</returns>
        public double StageReady()
        {
            return (StageManager.CanSeparate && InputLockManager.IsUnlocked(ControlTypes.STAGING)) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Toggle the stage lock on or off.  Returns the new state.
        /// </summary>
        /// <returns>1 if staging is now locked; 0 if staging is now unlocked.</returns>
        public double ToggleStageLocked()
        {
            bool state = !InputLockManager.IsLocked(ControlTypes.STAGING);
            SetStageLocked(state);
            return (state) ? 1.0 : 0.0;
        }
        #endregion

        /// <summary>
        /// The Target and Rendezvous section provides functions and methods related to
        /// targets and rendezvous operations with a target.  These methods include raw
        /// distance and velocities as well as target name and classifiers (is it a vessel,
        /// a celestial body, etc).
        /// </summary>
        #region Target and Rendezvous
        /// <summary>
        /// Clears any targets being tracked.
        /// </summary>
        /// <returns>1 if the target was cleared, 0 otherwise.</returns>
        public double ClearTarget()
        {
            if (vc.targetValid)
            {
                FlightGlobals.fetch.SetVesselTarget((ITargetable)null);
                return 1.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the altitude of the target, or 0 if there is no target.
        /// </summary>
        /// <returns>Target altitude in meters.</returns>
        public double TargetAltitude()
        {
            if (vc.activeTarget != null)
            {
                return vc.targetOrbit.altitude;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the raw angle between the target and the nose of the vessel,
        /// or 0 if there is no target.
        /// </summary>
        /// <returns>Returns 0 if the target is directly in front of the vessel, or
        /// if there is no target; returns a number up to 180 in all other cases.  Value is in degrees.</returns>
        public double TargetAngle()
        {
            if (vc.targetType > 0)
            {
                return Vector3.Angle(vc.forward, vc.targetDirection);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the target's apoapsis.
        /// </summary>
        /// <returns>Target's Ap in meters, or 0 if there is no target.</returns>
        public double TargetApoapsis()
        {
            if (vc.targetType > 0)
            {
                return vc.targetOrbit.ApA;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the name of the body that the target orbits, or an empty string if
        /// there is no target.
        /// </summary>
        /// <returns></returns>
        public string TargetBodyName()
        {
            if (vc.targetType > 0)
            {
                return vc.targetOrbit.referenceBody.bodyName;
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Returns the distance of the closest approach to the target during the
        /// next orbit.  If the target is a celestial body, the closest approach
        /// distance reports the predicted periapsis, with a value of 0 indicating
        /// lithobraking (impact).
        /// </summary>
        /// <returns>Closest approach distance in meters, or 0 if there is no target.</returns>
        public double TargetClosestApproachDistance()
        {
            return vc.targetClosestDistance;
        }

        /// <summary>
        /// Returns the time until the closest approach to the target.
        /// </summary>
        /// <returns>Time to closest approach in seconds, or 0 if there is no target.</returns>
        public double TargetClosestApproachTime()
        {
            if (vc.targetType > 0)
            {
                return vc.targetClosestUT - vc.universalTime;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the distance to the current target in meters, or 0 if there is no target.
        /// </summary>
        /// <returns>Target distance in meters, or 0 if there is no target.</returns>
        public double TargetDistance()
        {
            return vc.targetDisplacement.magnitude;
        }

        /// <summary>
        /// Returns the displacement between the target vessel and the reference
        /// transform on the horizontal (reference-transform relative) plane in
        /// meters, with target to the right = +X and left = -X.
        /// </summary>
        /// <returns>Distance in meters.  Positive means the target is to the right,
        /// negative means to the left.</returns>
        public double TargetDistanceX()
        {
            return Vector3.Dot(vc.targetDisplacement, vc.referenceTransform.right);
        }

        /// <summary>
        /// Returns the displacement between the target vessel and the reference
        /// transform on the vertical (rt-relative) plane in meters, with target
        /// up = +Y and down = -Y.
        /// </summary>
        /// <returns>Distance in meters.  Positive means the target is above the
        /// craft, negative means below.</returns>
        public double TargetDistanceY()
        {
            // The sign is reversed because it appears that the forward vector actually
            // points down, not up, which also means not having to flip the sign for the
            // Z axis.
            return -Vector3.Dot(vc.targetDisplacement, vc.referenceTransform.forward);
        }

        /// <summary>
        /// Returns the displacement between the target vessel and the reference
        /// transform on the Z (fore/aft) axis in meters, with target ahead = +Z
        /// and behind = -Z
        /// </summary>
        /// <returns>Distance in meters.  Positive indicates a target in front
        /// of the craft, negative indicates behind.</returns>
        public double TargetDistanceZ()
        {
            return Vector3.Dot(vc.targetDisplacement, vc.referenceTransform.up);
        }

        /// <summary>
        /// Returns the eccentricity of the target's orbit, or 0 if there is no
        /// target.
        /// </summary>
        /// <returns>Returns the target orbit's eccentricity.</returns>
        public double TargetEccentricity()
        {
            if (vc.targetType > 0)
            {
                return vc.targetOrbit.eccentricity;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the orbital inclination of the target, or 0 if there is no target.
        /// </summary>
        /// <returns>Target orbital inclination in degrees, or 0 if there is no target.</returns>
        public double TargetInclination()
        {
            if (vc.targetType > 0)
            {
                return vc.targetOrbit.inclination;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if the target is a vessel (vessel or Docking Port); 0 otherwise.
        /// </summary>
        /// <returns>1 for vessel or docking port targets, 0 otherwise.</returns>
        public double TargetIsVessel()
        {
            return (vc.targetType == MASVesselComputer.TargetType.Vessel || vc.targetType == MASVesselComputer.TargetType.DockingPort) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns whether the latitude / longitude of the target are
        /// currently valid.  Only vessels, docking ports, and position
        /// targets will have valid lat/lon.
        /// </summary>
        /// <returns>1 for vessel, docking port, or waypoint targets, 0 otherwise.</returns>
        public double TargetLatLonValid()
        {
            return (vc.targetType == MASVesselComputer.TargetType.Vessel || vc.targetType == MASVesselComputer.TargetType.DockingPort || vc.targetType == MASVesselComputer.TargetType.PositionTarget) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the target latitude for targets that have valid latitudes
        /// (vessel, docking port, position targets).
        /// </summary>
        /// <returns>Latitude in degrees.  Positive values are north of the
        /// equator, and negative values are south.</returns>
        public double TargetLatitude()
        {
            switch (vc.targetType)
            {
                case MASVesselComputer.TargetType.Vessel:
                    return vc.activeTarget.GetVessel().latitude;
                case MASVesselComputer.TargetType.DockingPort:
                    return vc.activeTarget.GetVessel().latitude;
                case MASVesselComputer.TargetType.PositionTarget:
                    // TODO: Is there a better way to do this?  Can I use GetVessel?
                    return vessel.mainBody.GetLatitude(vc.activeTarget.GetTransform().position);
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the target longitude for targets that have valid longitudes
        /// (vessel, docking port, position targets).
        /// </summary>
        /// <returns>Longitude in degrees.  Negative values are west of the prime
        /// meridian, and positive values are east of it.</returns>
        public double TargetLongitude()
        {
            switch (vc.targetType)
            {
                case MASVesselComputer.TargetType.Vessel:
                    return Utility.NormalizeLongitude(vc.activeTarget.GetVessel().longitude);
                case MASVesselComputer.TargetType.DockingPort:
                    return Utility.NormalizeLongitude(vc.activeTarget.GetVessel().longitude);
                case MASVesselComputer.TargetType.PositionTarget:
                    // TODO: Is there a better way to do this?
                    return vessel.mainBody.GetLongitude(vc.activeTarget.GetTransform().position);
            }

            return 0.0;
        }

        /// <summary>
        /// Get the name of the current target, or an empty string if there
        /// is no target.
        /// </summary>
        /// <returns>The name of the current target, or "" if there is no target.</returns>
        public string TargetName()
        {
            return vc.targetName;
        }

        /// <summary>
        /// Sets the target to the next moon of the body that vessel currently orbits.  If there
        /// are no moons orbiting the current body, nothing happens.
        /// 
        /// If the vessel is currently targeting anything other than a moon of the current body,
        /// that target is cleared and the first moon is selected, instead.
        /// 
        /// Moon order is based on the order that the moons appear in the CelestialBody's list of
        /// worlds.
        /// 
        /// If the vessel is currently orbiting the Sun, this method will target planets.
        /// </summary>
        /// <returns>Returns 1 if a moon was targeted.  0 otherwise.</returns>
        public double TargetNextMoon()
        {
            if (vc.mainBody.orbitingBodies != null)
            {
                int numMoons = vc.mainBody.orbitingBodies.Count;

                if (numMoons > 0)
                {
                    int moonIndex = -1;

                    if (vc.targetType == MASVesselComputer.TargetType.CelestialBody)
                    {
                        CelestialBody targetWorld = vc.activeTarget as CelestialBody;
                        moonIndex = vc.mainBody.orbitingBodies.FindIndex(t => (t == targetWorld));
                    }

                    if (moonIndex >= 0)
                    {
                        moonIndex = (moonIndex + 1) % numMoons;
                    }
                    else
                    {
                        moonIndex = 0;
                    }

                    FlightGlobals.fetch.SetVesselTarget(vc.mainBody.orbitingBodies[moonIndex]);

                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Sets the target to the nearest vessel in same SoI as the current vessel.
        /// 
        /// If the vessel is alreadying targeting a vessel in the same SoI, the next closest one will
        /// be targeted, instead.  If the current target is the closest vessel, the most distant one
        /// is selected.
        /// </summary>
        /// <returns>1 if a vessel was targeted, 0 otherwise.</returns>
        public double TargetNextVessel()
        {
            UpdateNeighboringVessels();

            int numVessels = neighboringVessels.Length;
            if (numVessels > 0)
            {
                if (vc.targetType != MASVesselComputer.TargetType.Vessel && vc.targetType != MASVesselComputer.TargetType.DockingPort)
                {
                    // Simple case: We're not currently targeting a vessel.
                    FlightGlobals.fetch.SetVesselTarget(neighboringVessels[0]);
                }
                else
                {
                    Vessel targetVessel;
                    if (vc.targetType == MASVesselComputer.TargetType.Vessel)
                    {
                        targetVessel = vc.activeTarget as Vessel;
                    }
                    else // Docking port
                    {
                        targetVessel = (vc.activeTarget as ModuleDockingNode).vessel;
                    }

                    int vesselIdx = Array.FindIndex(neighboringVessels, v => v.id == targetVessel.id);
                    int selectedIdx = 0;
                    if (vesselIdx == 0)
                    {
                        selectedIdx = neighboringVessels.Length - 1;
                    }
                    else
                    {
                        selectedIdx = vesselIdx - 1;
                    }

                    FlightGlobals.fetch.SetVesselTarget(neighboringVessels[selectedIdx]);
                }
                return 1.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the target's periapsis.
        /// </summary>
        /// <returns>Target's Pe in meters, or 0 if there is no target.</returns>
        public double TargetPeriapsis()
        {
            if (vc.targetType > 0)
            {
                return vc.targetOrbit.PeA;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the relative inclination between the vessel and the target.
        /// </summary>
        /// <returns>Inclination in degrees.  Returns 0 if there is no target, or the
        /// target orbits a different celestial body.</returns>
        public double TargetRelativeInclination()
        {
            if (vc.targetType > 0 && vc.targetOrbit.referenceBody == vc.orbit.referenceBody)
            {
                return Vector3.Angle(vc.orbit.GetOrbitNormal(), vc.targetOrbit.GetOrbitNormal());
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if there is a target, and it is in the same SoI as the
        /// vessel (for example: both orbiting Kerbin, or both orbiting the Mun, but not
        /// one orbiting Kerbin, and the other orbiting the Mun).
        /// </summary>
        /// <returns>1 if the target is in the same SoI; 0 if not, or if there is no target.</returns>
        public double TargetSameSoI()
        {
            if (vc.activeTarget != null)
            {
                return (vc.targetOrbit.referenceBody == vc.orbit.referenceBody) ? 1.0 : 0.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the semi-major axis of the target's orbit.
        /// </summary>
        /// <returns>SMA in meters, or 0 if there is no target.</returns>
        public double TargetSMA()
        {
            if (vc.activeTarget != null)
            {
                return vc.targetOrbit.semiMajorAxis;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the time until the target's next apoapsis.
        /// </summary>
        /// <returns>Time to Ap in seconds, or 0 if there's no target.</returns>
        public double TargetTimeToAp()
        {
            if (vc.targetType > 0)
            {
                return vc.targetOrbit.timeToAp;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the time until the target's next periapsis.
        /// </summary>
        /// <returns>Time to Pe in seconds, or 0 if there's no target.</returns>
        public double TargetTimeToPe()
        {
            if (vc.targetType > 0)
            {
                return vc.targetOrbit.timeToPe;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns a number identifying the target type.  Valid results are:
        /// 
        /// * 0: No target
        /// * 1: Target is a Vessel
        /// * 2: Target is a Docking Port
        /// * 3: Target is a Celestial Body
        /// * 4: Target is a Waypoint
        /// * 5: Target is an asteroid *(not yet implemented)*
        /// </summary>
        /// <returns>A number between 0 and 5 (inclusive)</returns>
        public double TargetType()
        {
            switch (vc.targetType)
            {
                case MASVesselComputer.TargetType.None:
                    return 0.0;
                case MASVesselComputer.TargetType.Vessel:
                    return 1.0;
                case MASVesselComputer.TargetType.DockingPort:
                    return 2.0;
                case MASVesselComputer.TargetType.CelestialBody:
                    return 3.0;
                case MASVesselComputer.TargetType.PositionTarget:
                    return 4.0;
                case MASVesselComputer.TargetType.Asteroid:
                    return 5.0;
                default:
                    return 0.0;
            }
        }

        /// <summary>
        /// **UNTESTED:** signs may be incorrect.  Please report results testing this
        /// method.
        /// 
        /// Returns the target's velocity relative to the left-right axis of the vessel.
        /// </summary>
        /// <returns>Velocity in m/s.  Positive means the vessel is moving 'right' relative
        /// to the target, and negative means 'left'.</returns>
        public double TargetVelocityX()
        {
            return Vector3.Dot(vc.targetRelativeVelocity, vc.referenceTransform.right);
        }

        /// <summary>
        /// **UNTESTED:** signs may be incorrect.  Please report results testing this
        /// method.
        /// 
        /// Returns the target's velocity relative to the top-bottom axis of the
        /// vessel (the top / bottom of the vessel from the typical inline IVA's
        /// perspective).
        /// </summary>
        /// <returns>Velocity in m/s.  Positive means the vessel is moving 'up'
        /// relative to the target, negative means relative 'down'.</returns>
        public double TargetVelocityY()
        {
            return -Vector3.Dot(vc.targetRelativeVelocity, vc.referenceTransform.forward);
        }

        /// <summary>
        /// **UNTESTED:** signs may be incorrect.  Please report results testing this
        /// method.
        /// 
        /// Returns the target's velocity relative to the forward-aft axis of
        /// the vessel (the nose of an aircraft, the 'top' of a vertically-launched
        /// craft).
        /// </summary>
        /// <returns>Velocity in m/s.  Positive means approaching, negative means departing.</returns>
        public double TargetVelocityZ()
        {
            return Vector3.Dot(vc.targetRelativeVelocity, vc.referenceTransform.up);
        }

        /// <summary>
        /// Returns the number of other non-debris vessels in the current SoI.  This count
        /// includes landed vessels as well as vessels in flight, but it does not count the
        /// current vessel.
        /// </summary>
        /// <returns>The number of other non-debris vessels, or 0 if there are none.</returns>
        public double TargetVesselCount()
        {
            UpdateNeighboringVessels();

            return neighboringVessels.Length;
        }
        #endregion

        /// <summary>
        /// The Thermal section contains temperature monitoring values.
        /// </summary>
        #region Thermal

        /// <summary>
        /// Returns the current atmosphere / ambient temperature outside the
        /// craft.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Ambient temperature in Kelvin or Celsius.</returns>
        public double AmbientTemperature(bool useKelvin)
        {
            return vessel.atmosphericTemperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
        }

        /// <summary>
        /// Returns the temperature of the current IVA's cabin atmosphere, if the
        /// pod has the appropriate PartModule (MASClimateControl).  Otherwise,
        /// the part's internal temperature is provided.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Cabin temperature in Kelvin or Celsius.</returns>
        public double CabinTemperature(bool useKelvin)
        {
            return ((fc.cc == null) ? fc.part.temperature : fc.cc.cabinTemperature)
                + ((useKelvin) ? 0.0 : KelvinToCelsius);
        }

        /// <summary>
        /// Returns the current temperature outside the vessel.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>External temperature in Kelvin or Celsius.</returns>
        public double ExternalTemperature(bool useKelvin)
        {
            return vessel.externalTemperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
        }

        /// <summary>
        /// Returns the current temperature of the hottest engine, where hottest engine
        /// is defined as "closest to its maximum temperature".
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Current temperature of the hottest engine in Kelvin or Celsius.</returns>
        public double HottestEngineTemperature(bool useKelvin)
        {
            return vc.hottestEngineTemperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
        }

        /// <summary>
        /// Returns the maximum temperature of the hottest engine, where hottest engine
        /// is defined as "closest to its maximum temperature".
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Current temperature of the hottest engine in Kelvin or Celsius.</returns>
        public double HottestEngineTemperatureMax(bool useKelvin)
        {
            return vc.hottestEngineMaxTemperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
        }

        /// <summary>
        /// Returns the interior temperature of the current IVA pod.  This is the part's interior
        /// temperature.  For cabin temperature (with the appropriate mods), use
        /// `fc.CabinTemperature()`.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Current temperature of the interior of the current IVA pod in Kelvin or Celsius.</returns>
        public double InternalTemperature(bool useKelvin)
        {
            return fc.part.temperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
        }

        /// <summary>
        /// Returns 1 if there is at least one radiator active on the vessel.
        /// </summary>
        /// <returns>1 if any radiators are active, or 0 if no radiators are active or no radiators
        /// are installed.</returns>
        public double RadiatorActive()
        {
            return (vc.radiatorActive) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the number of radiators installed on the craft, regardless of their status
        /// (enabled / disabled / damaged).
        /// </summary>
        /// <returns></returns>
        public double RadiatorCount()
        {
            return vc.moduleRadiator.Length;
        }

        /// <summary>
        /// Returns 1 if the deployable radiators are damaged.
        /// </summary>
        /// <returns></returns>
        public double RadiatorDamaged()
        {
            return (vc.radiatorPosition == 0) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if at least one radiator may be deployed.
        /// </summary>
        /// <returns>1 if any radiators may be deployed, or 0 if no radiators may be deployed
        /// or no radiators are installed.</returns>
        public double RadiatorDeployable()
        {
            return (vc.radiatorDeployable) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if there is at least one radiator inactive on the vessel.
        /// </summary>
        /// <returns>1 if any radiators are inactive, or 0 if no radiators are inactive or no radiators
        /// are installed.</returns>
        public double RadiatorInactive()
        {
            return (vc.radiatorInactive) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if at least one radiator on the vessel is deploying or
        /// retracting.
        /// </summary>
        /// <returns>1 if a deployable radiator is moving, or 0 if none are moving.
        /// </returns>
        public double RadiatorMoving()
        {
            return (vc.radiatorMoving) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns a number representing deployable radiator position:
        /// 
        /// * 0 = Broken
        /// * 1 = Retracted
        /// * 2 = Retracting
        /// * 3 = Extending
        /// * 4 = Extended
        /// 
        /// If there are multiple radiators, the first non-broken radiator's state
        /// is reported; if all radiators are broken, the state will be 0.
        /// </summary>
        /// <returns>Radiator Position (a number between 0 and 4); 1 if no radiators are installed.</returns>
        public double RadiatorPosition()
        {
            return vc.radiatorPosition;
        }

        /// <summary>
        /// Returns 1 if at least one radiator on the vessel may be retracted or
        /// undeployed.
        /// </summary>
        /// <returns>1 if a deployable radiator may be retracted, or 0 if none may be
        /// retracted or no deployable radiators are installed.</returns>
        public double RadiatorRetractable()
        {
            return (vc.radiatorRetractable) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns current radiator utilization as a percentage of maximum of active
        /// radiators.
        /// </summary>
        /// <returns>Current utilization, in the range of 0 to 1.  If no active radiators are installed,
        /// or none are active, returns 0.</returns>
        public double RadiatorUtilization()
        {
            return (vc.maxEnergyTransfer > 0.0) ? (vc.currentEnergyTransfer / vc.maxEnergyTransfer) : 0.0;
        }

        /// <summary>
        /// Returns the skin temperature of the current IVA pod.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Current temperature of the interior of the current IVA pod in Kelvin or Celsius.</returns>
        public double SkinTemperature(bool useKelvin)
        {
            return fc.part.skinTemperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
        }

        /// <summary>
        /// Deploys deployable radiators, or retracts retractable radiators.
        /// </summary>
        public void ToggleRadiator()
        {
            if (vc.radiatorDeployable)
            {
                for (int i = vc.moduleDeployableRadiator.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleDeployableRadiator[i].useAnimation && vc.moduleDeployableRadiator[i].deployState == ModuleDeployablePart.DeployState.RETRACTED)
                    {
                        vc.moduleDeployableRadiator[i].Extend();
                    }
                }
            }
            else if (vc.radiatorRetractable)
            {
                for (int i = vc.moduleDeployableRadiator.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleDeployableRadiator[i].useAnimation && vc.moduleDeployableRadiator[i].retractable && vc.moduleDeployableRadiator[i].deployState == ModuleDeployablePart.DeployState.EXTENDED)
                    {
                        vc.moduleDeployableRadiator[i].Retract();
                    }
                }
            }
        }
        #endregion

        /// <summary>
        /// The Time section provides access to the various timers in MAS (and KSP).
        /// </summary>
        #region Time
        /// <summary>
        /// Returns the hour of the day (0-5.999... using the Kerbin clock, 0-23.999... using the
        /// Earth clock).  Fraction of the hour is retained.
        /// </summary>
        /// <param name="time">Time in seconds (eg, `fc.UT()`).</param>
        /// <returns>The hour of the day, accounting for Kerbin time vs. Earth time.</returns>
        public double HourOfDay(double time)
        {
            return (time / 3600.0) % ((GameSettings.KERBIN_TIME) ? 6.0 : 24.0);
        }

        /// <summary>
        /// Fetch the current MET (Mission Elapsed Time) for the vessel in
        /// seconds.
        /// </summary>
        /// <returns>Mission time, in seconds.</returns>
        public double MET()
        {
            return vessel.missionTime;
        }

        /// <summary>
        /// Given a standard time in seconds, return the minutes of the hour (a
        /// number from 0 to 60).  Fractions of a minute are retained and negative
        /// values are converted to positive.
        /// </summary>
        /// <param name="time">Time in seconds (eg, `fc.MET()`).</param>
        /// <returns>A number representing the minutes in the hour in the range [0, 60).</returns>
        public double MinutesOfHour(double time)
        {
            return (Math.Abs(time) / 60.0) % 60.0;
        }

        /// <summary>
        /// Given a standard time in seconds, return the seconds of the minute (the
        /// number from 0 to 60).  Fractions of a second are retained and negative
        /// values are converted to positive.
        /// </summary>
        /// <param name="time">Time in seconds (eg, `fc.MET()`).</param>
        /// <returns>A number representing the seconds in the minute in the range [0, 60).</returns>
        public double SecondsOfMinute(double time)
        {
            return Math.Abs(time) % 60.0;
        }

        /// <summary>
        /// Similar to `fc.HourOfDay()`, but returning the answer in seconds instead
        /// of hours.
        /// 
        /// When used with `fc.UT()`, for instance, it returns the number of seconds since midnight UT.
        /// </summary>
        /// <returns>Number of seconds since the latest day began.</returns>
        public double TimeOfDay(double time)
        {
            return 3600.0 * HourOfDay(time);
        }

        /// <summary>
        /// Given an altitude in meters, return the number of seconds until the vessel
        /// next crosses that altitude.  If the vessel is on a hyperbolic orbit, or
        /// if the orbit never crosses the given altitude, return 0.0.
        /// </summary>
        /// <param name="altitude">Altitude above the datum, in meters.</param>
        /// <returns>Time in seconds until the altitude is crossed, or 0 if the orbit does not cross that altitude.</returns>
        public double TimeToAltitude(double altitude)
        {
            if (vc.orbit.ApA >= altitude && vc.orbit.PeA <= altitude && vc.orbit.eccentricity < 1.0)
            {
                // How do I do this?  Like so:
                // TrueAnomalyAtRadius returns a TA between 0 and PI, representing
                // when the orbit crosses that altitude while ascending from Pe (0) to Ap (PI).
                double taAtAltitude = vc.orbit.TrueAnomalyAtRadius(altitude + vc.mainBody.Radius);
                // GetUTForTrueAnomaly gives us a time for when that will occur.  I don't know
                // what parameter 2 is really supposed to do (wrapAfterSeconds), because after
                // subtracting vc.UT, I see values sometimes 2 orbits in the past.  Which is why...
                double timeToTa1 = vc.orbit.GetUTforTrueAnomaly(taAtAltitude, vc.orbit.period) - vc.universalTime;
                // ... we have to normalize it here to the next time we cross that TA.
                while (timeToTa1 < 0.0)
                {
                    timeToTa1 += vc.orbit.period;
                }
                // Now, what about the other time we cross that altitude (in the range of -PI to 0)?
                // Easy.  The orbit is symmetrical around 0, so the other TA is -taAtAltitude.
                // I *could* use TrueAnomalyAtRadius and normalize the result, but I don't know the
                // complexity of that function, and there's an easy way to compute it: since
                // the TA is symmetrical, the time from the Pe to TA1 is the same as the time
                // from TA2 to Pe.

                // First, find the time-to-Pe that occurs before the time to TA1:
                double relevantPe = vc.orbit.timeToPe;
                if (relevantPe > timeToTa1)
                {
                    // If we've passed the Pe, but we haven't reached TA1, we
                    // need to find the previous Pe
                    relevantPe -= vc.orbit.period;
                }

                // Then, we subtract the interval from TA1 to the Pe from the time
                // until the Pe (that is, T(Pe) - (T(TA1) - T(Pe)), rearranging terms:
                double timeToTa2 = 2.0 * relevantPe - timeToTa1;
                if (timeToTa2 < 0.0)
                {
                    // If the relevant Pe occurred in the past, advance the time to
                    // the next time in the future.  I could probably do some
                    // optimizations by saying "well, this is in the past, so I know
                    // TA1 is the future", but I doubt that buys enough of a
                    // performance difference.
                    timeToTa2 += vc.orbit.period;
                }

                // Whichever occurs first is the one we care about:
                return Math.Min(timeToTa1, timeToTa2);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// For non-hyperbolic orbits, returns time to the next equatorial
        /// Ascending Node.
        /// </summary>
        /// <returns>Time to AN, seconds, or 0 if the orbit is hyperbolic.</returns>
        public double TimeToANEq()
        {
            if (vc.orbit.eccentricity < 1.0)
            {
                Vector3d ANVector = vc.orbit.GetANVector();
                ANVector.Normalize();
                double taAN = vc.orbit.GetTrueAnomalyOfZupVector(ANVector);
                double timeAN = vc.orbit.GetUTforTrueAnomaly(taAN, vc.orbit.period) - vc.universalTime;

                return Utility.NormalizeOrbitTime(timeAN, vc.orbit);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the time until the next ascending node with the current target,
        /// provided the target is orbiting the same body as the vessel (and the target
        /// exists).
        /// </summary>
        /// <returns>Time in seconds to the next ascending node, in seconds, or 0.</returns>
        public double TimeToANTarget()
        {
            if (vc.orbit.eccentricity < 1.0 && vc.targetType != MASVesselComputer.TargetType.None && vc.orbit.referenceBody == vc.activeTarget.GetOrbit().referenceBody)
            {
                Vector3d vesselNormal = vc.orbit.GetOrbitNormal();
                Vector3d targetNormal = vc.activeTarget.GetOrbit().GetOrbitNormal();
                Vector3d cross = Vector3d.Cross(vesselNormal, targetNormal);
                double taAN = vc.orbit.GetTrueAnomalyOfZupVector(-cross);
                double timeAN = vc.orbit.GetUTforTrueAnomaly(taAN, vc.orbit.period) - vc.universalTime;

                return Utility.NormalizeOrbitTime(timeAN, vc.orbit);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Fetch the time to the next apoapsis.  If the orbit is hyperbolic,
        /// or the vessel is not flying, return 0.
        /// </summary>
        /// <returns>Time until Ap in seconds, or 0 if the time would be invalid.</returns>
        public double TimeToAp()
        {
            if (vesselSituationConverted > 2 && vc.orbit.eccentricity < 1.0)
            {
                return vc.orbit.timeToAp;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Fetch the time until the vessel's orbit next enters or exits the
        /// body's atmosphere.  If there is no atmosphere, or the orbit does not
        /// cross that threshold, return 0.
        /// </summary>
        /// <returns>Time until the atmosphere boundary is crossed, in seconds; 0 for invalid times.</returns>
        public double TimeToAtmosphere()
        {
            if (vc.mainBody.atmosphere)
            {
                return TimeToAltitude(vc.mainBody.atmosphereDepth);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the time to the equatorial descending node, in seconds.
        /// </summary>
        /// <returns>Time in seconds to the next descending node, or 0 if the orbit is hyperbolic.</returns>
        public double TimeToDNEq()
        {
            if (vc.orbit.eccentricity < 1.0)
            {
                Vector3d DNVector = vc.orbit.GetANVector();
                DNVector.Normalize();
                double taDN = vc.orbit.GetTrueAnomalyOfZupVector(-DNVector);
                double timeDN = vc.orbit.GetUTforTrueAnomaly(taDN, vc.orbit.period) - vc.universalTime;

                return Utility.NormalizeOrbitTime(timeDN, vc.orbit);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the time until the next descending node with the current target,
        /// provided the target is orbiting the same body as the vessel (and the target
        /// exists).
        /// </summary>
        /// <returns>Time in seconds to the next descending node, in seconds, or 0.</returns>
        public double TimeToDNTarget()
        {
            if (vc.orbit.eccentricity < 1.0 && vc.targetType != MASVesselComputer.TargetType.None && vc.orbit.referenceBody == vc.activeTarget.GetOrbit().referenceBody)
            {
                Vector3d vesselNormal = vc.orbit.GetOrbitNormal();
                Vector3d targetNormal = vc.activeTarget.GetOrbit().GetOrbitNormal();
                Vector3d cross = Vector3d.Cross(vesselNormal, targetNormal);
                double taDN = vc.orbit.GetTrueAnomalyOfZupVector(cross);
                double timeDN = vc.orbit.GetUTforTrueAnomaly(taDN, vc.orbit.period) - vc.universalTime;

                return Utility.NormalizeOrbitTime(timeDN, vc.orbit);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the time until the vessel lands.  If MechJeb is available and the
        /// landing prediction module is enabled, MechJeb's results are used.  
        /// 
        /// If the orbit does not intercept the
        /// surface, 0 is returned.
        /// </summary>
        /// <seealso>MechJeb</seealso>
        /// <returns>Time in seconds until landing; 0 for invalid times.</returns>
        public double TimeToLanding()
        {
            if (mjProxy.LandingComputerActive() > 0.0)
            {
                return mjProxy.LandingTime();
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Fetch the time to the next periapsis.  If the vessel is not
        /// flying, the value will be zero.  If the vessel is on a hyperbolic
        /// orbit, and it has passed the periapsis already, the value will
        /// be negative.
        /// </summary>
        /// <returns>Time until the next Pe in seconds, or 0 if the time would
        /// be invalid.  May return a negative number in hyperbolic orbits.</returns>
        public double TimeToPe()
        {
            if (vesselSituationConverted > 2)
            {
                return vc.orbit.timeToPe;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the number of seconds until the vessel's orbit transitions to
        /// another sphere of influence (leaving the current one and entering another).
        /// </summary>
        /// <returns>Time until transition in seconds; 0 if the orbit does not cross a
        /// Sphere of Influence.</returns>
        public double TimeToSoI()
        {
            if (vc.orbit.patchEndTransition == Orbit.PatchTransitionType.ESCAPE || vc.orbit.patchEndTransition == Orbit.PatchTransitionType.ENCOUNTER)
            {
                return vc.orbit.UTsoi - vc.universalTime;
            }

            return 0.0;
        }

        /// <summary>
        /// Fetch the current UT (universal time) in seconds.
        /// </summary>
        /// <returns>Universal Time, in seconds.</returns>
        public double UT()
        {
            return vc.universalTime;
        }

        /// <summary>
        /// Returns the current time warp multiplier.
        /// </summary>
        /// <returns>1 for normal speed, larger values for various warps.</returns>
        public double WarpRate()
        {
            return TimeWarp.CurrentRate;
        }
        #endregion

        /// <summary>
        /// The Vessel Info group contains non-flight information about the vessel (such
        /// as vessel name, type, etc.).
        /// </summary>
        #region Vessel Info

        /// <summary>
        /// Returns the name of the vessel.
        /// </summary>
        /// <returns></returns>
        public string VesselName()
        {
            return vessel.vesselName;
        }

        /// <summary>
        /// Returns a string naming the type of vessel.
        /// </summary>
        /// <returns></returns>
        public string VesselType()
        {
            return Utility.typeDict[vessel.vesselType];
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
    /// A method flagged as Pushable is a value that can change, but it does not need
    /// to be queried each FixedUpdate.  This method is primarily intended for the
    /// GetPersistent and GetPersistentAsNumber queries, which are called often but
    /// do not update frequently.  The various persistent variable manipulation routines
    /// will update the variable directly, instead of MAS polling them each FixedUpdate.
    /// 
    /// A method flagged as Uncacheable is expected to change each time it's called,
    /// such as random number generators.
    /// 
    /// Both of these attributes affect only variables that can be transformed to a
    /// native evaluator - Lua scripts are always cacheable + mutable.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class MASProxyAttribute : System.Attribute
    {
        private bool immutable;
        private bool pushable;
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

        public bool Pushable
        {
            get
            {
                return pushable;
            }
            set
            {
                pushable = value;
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
