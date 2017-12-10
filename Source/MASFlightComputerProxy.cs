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

        private ApproachSolverBW solver;

        private double timeToImpact;

        [MoonSharpHidden]
        public MASFlightComputerProxy(MASFlightComputer fc, MASIFAR farProxy, MASIMechJeb mjProxy)
        {
            this.fc = fc;
            this.farProxy = farProxy;
            this.mjProxy = mjProxy;
            this.solver = new ApproachSolverBW();
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
        /// Helper function to convert a vessel situation into
        /// </summary>
        /// <param name="vesselSituation"></param>
        /// <returns></returns>
        [MoonSharpHidden]
        internal int ConvertVesselSituation(Vessel.Situations vesselSituation)
        {
            int situation = (int)vessel.situation;
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
            vesselPowered = (vc.ResourceCurrent(MASConfig.ElectricCharge) > 0.0001);

            vesselSituationConverted = ConvertVesselSituation(vessel.situation);

            for (int i = neighboringVessels.Length - 1; i >= 0; --i)
            {
                neighboringVessels[i] = null;
            }
            neighboringVesselsCurrent = false;

            if (vessel.orbit.PeA < 0.0)
            {
                timeToImpact = TimeToAltitude(0.0);
            }
            else
            {
                timeToImpact = 0.0;
            }
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
        /// Returns the number of natural satellites (moons) orbiting the selected body.
        /// </summary>
        /// <param name="id">The name or index of the body of interest.</param>
        /// <returns>The number of moons, or 0.</returns>
        public double BodyMoonCount(object id)
        {
            CelestialBody cb = SelectBody(id);
            double numBodies = 0.0;
            if (cb != null && cb.orbitingBodies != null)
            {
                numBodies = cb.orbitingBodies.Count;
            }

            return numBodies;
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
            return ((useThrottleLimits) ? vc.currentLimitedMaxThrust : vc.currentMaxThrust) / (vessel.totalMass * vc.surfaceAccelerationFromGravity);
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
