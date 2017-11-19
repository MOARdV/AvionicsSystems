/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2017 MOARdV
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
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// Categories of navigational aids.
    /// </summary>
    public enum NavAidType
    {
        NDB,
        NDB_DME,
        VOR,
        VOR_DME,
        ILS,
        ILS_DME,
    };

    /// <summary>
    /// NavAid represents a single navigational aid.
    /// </summary>
    public class NavAid
    {
        public string name;
        public string identifier;
        public string celestialName;
        public double latitude;
        public double longitude;
        public double altitude; // ASL
        public double distanceToHorizon;
        public double distanceToHorizonDME; // -1 if no DME
        public float frequency;
        public NavAidType type;

        public void UpdateHorizonDistance()
        {
            CelestialBody body = FlightGlobals.Bodies.Find(x => (x.name == celestialName));
            if (body != null)
            {
                double baseDistanceToHorizon = Math.Sqrt(altitude * (2.0 * body.Radius + altitude)) * MASConfig.navigation.generalPropagation;
                if (type == NavAidType.NDB || type == NavAidType.NDB_DME)
                {
                    distanceToHorizon = baseDistanceToHorizon * MASConfig.navigation.NDBPropagation;
                }
                else if (type == NavAidType.VOR || type == NavAidType.VOR_DME)
                {
                    distanceToHorizon = baseDistanceToHorizon * MASConfig.navigation.VORPropagation;
                }
                else // ILS doesn't have a scalar ATM.
                {
                    distanceToHorizon = baseDistanceToHorizon;
                }

                if (type == NavAidType.ILS_DME || type == NavAidType.NDB_DME || type == NavAidType.VOR_DME)
                {
                    distanceToHorizonDME = baseDistanceToHorizon * MASConfig.navigation.DMEPropagation;
                }
                else
                {
                    distanceToHorizonDME = -1.0;
                }

                Utility.LogMessage(this, "Updating {0} {1}: altitude = {4:0}, horizon = {2:0}, h(DME) = {3:0}", type, identifier, distanceToHorizon, distanceToHorizonDME, altitude);
            }
        }

        public FinePrint.Waypoint ToWaypoint(int index)
        {
            string cName = celestialName;
            CelestialBody cb = FlightGlobals.Bodies.Find(x => x.name == cName);
            if (cb == null)
            {
                return null;
            }
            else
            {
                FinePrint.Waypoint wp = new FinePrint.Waypoint();

                wp.latitude = latitude;
                wp.longitude = longitude;
                wp.celestialName = celestialName;
                // TODO: Add support for Kopernicus / Sigma Dimensions / etc.
                wp.height = cb.pqsController.GetSurfaceHeight(QuaternionD.AngleAxis(wp.longitude, Vector3d.down) * QuaternionD.AngleAxis(wp.latitude, Vector3d.forward) * Vector3d.right) - cb.Radius;
                wp.altitude = Math.Max(altitude - wp.height, 0.0);
                wp.name = string.Format("{0} ({1}) {2} @ {3:0.00}", name, identifier, type, frequency);
                wp.index = index;
                wp.id = "vessel"; // seems to be icon name.  May be WPM-specific.

                return wp;
            }
        }
    };

    public class NavRadio
    {
        /// <summary>
        /// Frequency that the radio is set to.
        /// </summary>
        public float frequency;

        /// <summary>
        /// The nearest beacon, or -1.
        /// </summary>
        public int beaconIndex;

        /// <summary>
        /// The distance to the beacon.  If less than distanceToHorizon, the radio is in range.
        /// </summary>
        public double slantDistance;

        /// <summary>
        /// Convenience variable to determine if the current beacon has a DME transmitter.
        /// </summary>
        public bool isDME;

        public bool isNDB;

        public bool isVOR;

        public bool isILS;

        /// <summary>
        /// Beacons corresponding to that frequency.  May be null.
        /// </summary>
        public NavAid[] beacon;

        /// <summary>
        /// Recompute the slant distance (if needed) and return it.  Should be called instead
        /// of reading slantDistance directly.
        /// </summary>
        /// <param name="vessel">The active vessel.</param>
        /// <returns></returns>
        public double GetSlantDistance(Vessel vessel)
        {
            if (slantDistance <= 0.0 && beaconIndex >= 0)
            {
                CelestialBody mainBody = vessel.mainBody;
                Vector3d beaconPos = mainBody.GetWorldSurfacePosition(beacon[beaconIndex].latitude, beacon[beaconIndex].longitude, beacon[beaconIndex].altitude);
                Vector3d vesselPos = mainBody.GetWorldSurfacePosition(vessel.latitude, vessel.longitude, vessel.altitude);

                slantDistance = Vector3d.Distance(beaconPos, vesselPos);
            }

            return slantDistance;
        }

        /// <summary>
        /// Update the accessor convenience fields for the simple cases of 0 or 1 beacons.
        /// </summary>
        public void UpdateAccessors()
        {
            if (beaconIndex >= 0)
            {
                NavAidType type = beacon[beaconIndex].type;
                isDME = (type == NavAidType.ILS_DME || type == NavAidType.NDB_DME || type == NavAidType.VOR_DME);

                isNDB = (type == NavAidType.NDB || type == NavAidType.NDB_DME);
                isVOR = (type == NavAidType.VOR || type == NavAidType.VOR_DME);
                isILS = (type == NavAidType.ILS || type == NavAidType.ILS_DME);
            }
            else
            {
                isDME = false;
                isNDB = false;
                isVOR = false;
                isILS = false;
            }
        }
    };

    /// <summary>
    /// Encompasses the navigation / navradio functionality of the flight computer.
    /// </summary>
    public partial class MASFlightComputer : PartModule
    {
        /// <summary>
        /// Collection of all nav radios on board.
        /// </summary>
        internal Dictionary<int, NavRadio> navRadio = new Dictionary<int, NavRadio>();

        /// <summary>
        /// Collection only of nav radio frequencies (for save / restore).
        /// </summary>
        internal Dictionary<int, float> navRadioFrequency = new Dictionary<int, float>();

        /// <summary>
        /// Do the per-FixedUpdate refresh of active radios.
        /// </summary>
        private void UpdateRadios()
        {
            if (navRadio.Count > 0)
            {
                foreach (NavRadio radio in navRadio.Values)
                {
                    // Reset slant distance each FixedUpdate.
                    radio.slantDistance = 0.0;

                    if (radio.beacon.Length > 1)
                    {
                        // If we have multiple radios on the same frequency, we need to determine the closest one every
                        // fixed update...

                        QuaternionD vesselPosition = QuaternionD.Euler(0.0, vessel.longitude, vessel.latitude);
                        int oldIndex = radio.beaconIndex;
                        radio.beaconIndex = -1;
                        double angle = 180.0;
                        for (int i = radio.beacon.Length - 1; i >= 0; --i)
                        {
                            QuaternionD beaconPosition = QuaternionD.Euler(0.0, radio.beacon[i].longitude, radio.beacon[i].latitude);
                            double angleBetween = QuaternionD.Angle(vesselPosition, beaconPosition);
                            if (angleBetween < angle)
                            {
                                angle = angleBetween;
                                radio.beaconIndex = i;
                            }
                        }

                        if (radio.beaconIndex != oldIndex)
                        {
                            radio.UpdateAccessors();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Private method used to restore navRadio at vessel load time.  Does not affect
        /// navRadioFrequency.
        /// </summary>
        /// <param name="radioId"></param>
        /// <param name="frequency"></param>
        private void ReloadRadio(int radioId, float frequency)
        {
            if (frequency > 0.0f)
            {
                List<NavAid> navaids = MASLoader.navaids.FindAll(x => (x.celestialName == vessel.mainBody.name) && (Mathf.Abs(x.frequency - frequency) <= 0.005f));

                NavRadio radio = new NavRadio();
                radio.frequency = frequency;
                radio.beaconIndex = -1;
                radio.isDME = false;
                radio.slantDistance = 0.0;
                radio.beacon = navaids.ToArray();

                if (radio.beacon.Length == 1)
                {
                    radio.beaconIndex = 0;
                }
                radio.UpdateAccessors();

                navRadio[radioId] = radio;
            }
        }

        /// <summary>
        /// Set the specified radio to the desired frequency.
        /// </summary>
        /// <param name="radioId">Radio ID (arbitrary integer)</param>
        /// <param name="frequency">Frequency, MHz.  Set to 0.0 or less to shut down the radio.</param>
        /// <returns>true if at least one radio exists at that frequency, false otherwise.  If sending an invalid frequency, returns true if the radio was previously set.</returns>
        internal bool SetRadioFrequency(int radioId, float frequency)
        {
            if (frequency <= 0.0f)
            {
                navRadioFrequency.Remove(radioId);
                return navRadio.Remove(radioId);
            }
            else
            {
                List<NavAid> navaids = MASLoader.navaids.FindAll(x => (x.celestialName == vessel.mainBody.name) && (Mathf.Abs(x.frequency - frequency) <= 0.005f));

                NavRadio radio = new NavRadio();
                radio.frequency = frequency;
                radio.beaconIndex = -1;
                radio.isDME = false;
                radio.slantDistance = 0.0;
                radio.beacon = navaids.ToArray();

                if (radio.beacon.Length == 1)
                {
                    radio.beaconIndex = 0;
                }

                radio.UpdateAccessors();

                navRadio[radioId] = radio;
                navRadioFrequency[radioId] = frequency;

                return (navaids.Count > 0);
            }
        }

        /// <summary>
        /// Reports whether the navigational aid on the selected radio includes DME capability.
        /// 
        /// </summary>
        /// <param name="radioId">Radio ID (arbitrary integer).</param>
        /// <returns>-1 if there is no navaid in range, 0 if the navaid does not support DME, 1 if the navaid supports DME.</returns>
        internal double GetNavAidDME(int radioId)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                double beaconDmeDistance = radio.beacon[radio.beaconIndex].distanceToHorizonDME;
                if (radio.GetSlantDistance(vessel) < beaconDmeDistance)
                {
                    return (radio.isDME) ? 1.0 : 0.0;
                }
            }

            return -1.0;
        }

        /// <summary>
        /// Returns the basic type of radio navigational aid the selected radio detects:
        /// 
        /// * 0: No NavAid in range.
        /// * 1: Non-Directional Beacon (NDB).
        /// * 2: VHF Omnidirectional Range (VOR).
        /// * 3: Instrument Landing System (ILS).
        /// 
        /// Note that DME capabilities are detected using `nav.GetNavAidDME(radioId)`.
        /// </summary>
        /// <param name="radioId"></param>
        /// <returns></returns>
        internal double GetNavAidType(int radioId)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                if (radio.GetSlantDistance(vessel) < radio.beacon[radio.beaconIndex].distanceToHorizon)
                {
                    if (radio.isNDB)
                    {
                        return 1.0;
                    }
                    else if (radio.isVOR)
                    {
                        return 2.0;
                    }
                    else if (radio.isILS)
                    {
                        return 3.0;
                    }
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Get the bearing to the radio.  This method is a generic method that ignores the beacon type.
        /// </summary>
        /// <param name="radioId">Radio ID (arbitrary integer).</param>
        /// <returns>-1 if no valid nav beacon is in range, otherise a bearing in the range [0, 360).</returns>
        internal float GetNavAidBearing(int radioId)
        {
            return -1.0f;
        }

        /// <summary>
        /// Get the frequency that the given radio is set to use.
        /// </summary>
        /// <param name="radioId"></param>
        /// <returns>Frequency in MHz, or 0 if the radio has never been set.</returns>
        internal float GetRadioFrequency(int radioId)
        {
            float radioFrequency;
            if (navRadioFrequency.TryGetValue(radioId, out radioFrequency))
            {
                return radioFrequency;
            }
            else
            {
                return 0.0f;
            }
        }
    }
}
