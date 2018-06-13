/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2017-2018 MOARdV
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
        /// <summary>
        /// which planet?
        /// </summary>
        public string celestialName;
        public double latitude;
        public double longitude;
        public double altitude; // ASL
        /// <summary>
        /// Maximum range, primary signal.  Internally computed.
        /// </summary>
        public double maximumRange;
        /// <summary>
        /// Maximum range, DME signal, or -1 if no DME.  Internally computed.
        /// </summary>
        public double maximumRangeDME;
        /// <summary>
        /// Maximum range of the ILS localizer transmitter (if applicable).  Configurable.
        /// </summary>
        public double maximumRangeLocalizer;
        public double maximumRangeGlidePath;
        public double glidePathDefault;
        /// <summary>
        /// Bearing that corresponds to the ILS.
        /// </summary>
        public float approachHeadingILS;
        /// <summary>
        /// Maximum deflection in degrees off of the approach bearing for a signal.
        /// </summary>
        public float localizerSectorILS;
        public float frequency;
        /// <summary>
        /// Primarily for distance measurement
        /// </summary>
        public Vector3d worldPosition;
        /// <summary>
        /// Used for ILS glide slope computations.
        /// </summary>
        public Vector3d worldNormal;
        public double heightAGL;
        public NavAidType type;

        public void UpdateHorizonDistance()
        {
            CelestialBody body = FlightGlobals.Bodies.Find(x => (x.name == celestialName));
            if (body != null)
            {
                double baseDistanceToHorizon = Math.Sqrt(altitude * (2.0 * body.Radius + altitude)) * MASConfig.navigation.generalPropagation;
                if (type == NavAidType.NDB || type == NavAidType.NDB_DME)
                {
                    maximumRange = baseDistanceToHorizon * MASConfig.navigation.NDBPropagation;
                }
                else if (type == NavAidType.VOR || type == NavAidType.VOR_DME)
                {
                    maximumRange = baseDistanceToHorizon * MASConfig.navigation.VORPropagation;
                }
                else if (type == NavAidType.ILS || type == NavAidType.ILS_DME)
                {
                    maximumRange = baseDistanceToHorizon;
                }

                if (type == NavAidType.ILS_DME || type == NavAidType.NDB_DME || type == NavAidType.VOR_DME)
                {
                    maximumRangeDME = baseDistanceToHorizon * MASConfig.navigation.DMEPropagation;
                }
                else
                {
                    maximumRangeDME = -1.0;
                }

                if (body.pqsController != null)
                {
                    // TODO: Add support for Kopernicus / Sigma Dimensions / etc to handle rescaled planets.
                    double groundHeight = body.pqsController.GetSurfaceHeight(QuaternionD.AngleAxis(longitude, Vector3d.down) * QuaternionD.AngleAxis(latitude, Vector3d.forward) * Vector3d.right) - body.Radius;
                    altitude = Math.Max(altitude, groundHeight + 10.0);
                    heightAGL = altitude - groundHeight;
                }
                else
                {
                    heightAGL = altitude;
                }
                worldPosition = body.GetRelSurfacePosition(latitude, longitude, altitude, out worldNormal);

                Utility.LogMessage(this, "Updating {0} {1}: altitude = {4:0}, horizon = {2:0}, h(DME) = {3:0}", type, identifier, maximumRange, maximumRangeDME, altitude);
            }
        }

        public string waypointName
        {
            get
            {
                return string.Format("{0} ({1}) {2} @ {3:0.00}", name, identifier, type, frequency);
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
                wp.altitude = heightAGL;
                wp.name = waypointName;
                wp.index = index;
                //wp.navigationId = new Guid(wp.name); // TODO: Generate a GUID based on wp.name
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
        /// Cached in NavRadio, cleared every FixedUpdate.
        /// </summary>
        public double slantDistance;

        /// <summary>
        /// Distance to the horizon for the vessel, scaled by the global Radio Propagation scalar.
        /// </summary>
        public double vesselLoS;

        /// <summary>
        /// The bearing to the beacon from the vessel.  Cached in NavRadio, cleared every FixedUpdate.
        /// </summary>
        public double bearing;

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

        public bool DMEInRange(Vessel vessel)
        {
            if (slantDistance <= 0.0 && beaconIndex >= 0)
            {
                Vector3d vesselPos = vessel.mainBody.GetRelSurfacePosition(vessel.CoMD);

                slantDistance = Vector3d.Distance(beacon[beaconIndex].worldPosition, vesselPos);
                vesselLoS = Math.Sqrt(vessel.altitude * (2.0 * vessel.mainBody.Radius + vessel.altitude)) * MASConfig.navigation.generalPropagation;
            }

            return (slantDistance < (vesselLoS + beacon[beaconIndex].maximumRangeDME));
        }

        public bool NavAidInRange(Vessel vessel)
        {
            if (slantDistance <= 0.0 && beaconIndex >= 0)
            {
                Vector3d vesselPos = vessel.mainBody.GetRelSurfacePosition(vessel.CoMD);

                slantDistance = Vector3d.Distance(beacon[beaconIndex].worldPosition, vesselPos);
                vesselLoS = Math.Sqrt(vessel.altitude * (2.0 * vessel.mainBody.Radius + vessel.altitude)) * MASConfig.navigation.generalPropagation;
            }

            if (isILS)
            {
                return (slantDistance < beacon[beaconIndex].maximumRangeLocalizer);
            }
            else
            {
                return (slantDistance < (vesselLoS + beacon[beaconIndex].maximumRange));
            }
        }

        public bool GlidePathInRange(Vessel vessel)
        {
            if (slantDistance <= 0.0 && beaconIndex >= 0)
            {
                Vector3d vesselPos = vessel.mainBody.GetRelSurfacePosition(vessel.CoMD);

                slantDistance = Vector3d.Distance(beacon[beaconIndex].worldPosition, vesselPos);
                vesselLoS = Math.Sqrt(vessel.altitude * (2.0 * vessel.mainBody.Radius + vessel.altitude)) * MASConfig.navigation.generalPropagation;
            }

            if (isILS)
            {
                return (slantDistance < beacon[beaconIndex].maximumRangeGlidePath);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the bearing to the radio from the vessel.  Returns -1 if no bearing exists.
        /// </summary>
        /// <param name="vesselLat"></param>
        /// <param name="vesselLon"></param>
        /// <returns></returns>
        public double GetBearing(double vesselLat, double vesselLon)
        {
            if (beaconIndex >= 0)
            {
                if (bearing < 0.0)
                {
                    double lat1 = vesselLat * Utility.Deg2Rad;
                    double lat2 = beacon[beaconIndex].latitude * Utility.Deg2Rad;
                    double dLon = (beacon[beaconIndex].longitude - vesselLon) * Utility.Deg2Rad;

                    double y = Math.Sin(dLon) * Math.Cos(lat2);
                    double x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);

                    bearing = Utility.NormalizeAngle(Math.Atan2(y, x) * Utility.Rad2Deg);
                }

                return bearing;
            }
            else
            {
                return -1.0;
            }
        }

        /// <summary>
        /// Determine the distance to the horizon for the vessel for radio propagation purposes.
        /// </summary>
        /// <param name="altitude"></param>
        /// <param name="bodyRadius"></param>
        /// <returns></returns>
        public double GetLineOfSight(double altitude, double bodyRadius)
        {
            if (vesselLoS <= 0.0)
            {
                vesselLoS = Math.Sqrt(altitude * (2.0 * bodyRadius + altitude)) * MASConfig.navigation.generalPropagation;

            }

            return vesselLoS;
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
                    radio.bearing = -1.0;
                    radio.vesselLoS = 0.0;

                    if (radio.beacon.Length > 1)
                    {
                        // If we have multiple radios on the same frequency, we need to determine the closest one every
                        // fixed update...

                        Quaternion vesselPosition = Quaternion.Euler(0.0f, (float)vessel.longitude, (float)vessel.latitude);
                        int oldIndex = radio.beaconIndex;
                        radio.beaconIndex = -1;
                        float angle = 180.0f;
                        for (int i = radio.beacon.Length - 1; i >= 0; --i)
                        {
                            Quaternion beaconPosition = Quaternion.Euler(0.0f, (float)radio.beacon[i].longitude, (float)radio.beacon[i].latitude);
                            float angleBetween = Quaternion.Angle(vesselPosition, beaconPosition);
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
                radio.bearing = -1.0;
                radio.vesselLoS = 0.0;
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
                radio.bearing = -1.0;
                radio.vesselLoS = 0.0;
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
        /// Returns the distance to the DME equipment on the selected radio.
        /// </summary>
        /// <param name="radioId"></param>
        /// <returns>Distance to DME in meters, or -1 if no DME equipment is in range on the given radio frequency.</returns>
        internal double GetDMESlantDistance(int radioId)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                if (radio.isDME && radio.DMEInRange(vessel))
                {
                    return radio.slantDistance;
                }
            }

            return -1.0;
        }

        internal double GetILSLocalizerError(int radioId)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                if (radio.isILS && radio.NavAidInRange(vessel))
                {
                    double absoluteBearing = radio.GetBearing(vessel.latitude, vessel.longitude);

                    // Yeah, I could probably write this a little more succinctly.
                    // I want deviation to be 0 if the vessel is heading directly along the
                    // ILS approach line.
                    double deviation = Utility.NormalizeLongitude(radio.beacon[radio.beaconIndex].approachHeadingILS - absoluteBearing);
                    if (Math.Abs(deviation) <= radio.beacon[radio.beaconIndex].localizerSectorILS)
                    {
                        return deviation;
                    }
                }
            }
            return 0.0;
        }

        internal bool GetILSLocalizerValid(int radioId)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                if (radio.isILS && radio.NavAidInRange(vessel))
                {
                    double absoluteBearing = radio.GetBearing(vessel.latitude, vessel.longitude);

                    // Yeah, I could probably write this a little more succinctly.
                    double deviation = Utility.NormalizeLongitude(radio.beacon[radio.beaconIndex].approachHeadingILS - absoluteBearing);
                    return (Math.Abs(deviation) <= radio.beacon[radio.beaconIndex].localizerSectorILS);
                }
            }
            return false;
        }

        internal double GetILSGlideSlopeDefault(int radioId)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                if (radio.isILS && radio.GlidePathInRange(vessel))
                {
                    return radio.beacon[radio.beaconIndex].glidePathDefault;
                }
            }

            return 0.0;
        }

        internal double GetILSGlideSlopeError(int radioId, double glideSlope)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                if (radio.isILS && radio.GlidePathInRange(vessel))
                {
                    NavAid beacon = radio.beacon[radio.beaconIndex];
                    double absoluteBearing = radio.GetBearing(vessel.latitude, vessel.longitude);

                    // Don't detect vertical deviation if we can't see horizontal deviation.
                    double deviation = Utility.NormalizeLongitude(beacon.approachHeadingILS - absoluteBearing);
                    if (Math.Abs(deviation) <= beacon.localizerSectorILS)
                    {
                        Vector3d vesselPosition = vc.mainBody.GetRelSurfacePosition(vessel.CoMD);
                        Vector3d displacement = vesselPosition - beacon.worldPosition;
                        double angle = 90.0 - Vector3d.Angle(beacon.worldNormal, displacement);

                        if (angle >= glideSlope * 0.45 && angle <= glideSlope * 1.75)
                        {
                            return angle - glideSlope;
                        }
                    }
                }
            }

            return 0.0;
        }

        internal bool GetILSGlideSlopeValid(int radioId, double glideSlope)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                if (radio.isILS && radio.GlidePathInRange(vessel))
                {
                    NavAid beacon = radio.beacon[radio.beaconIndex];
                    double absoluteBearing = radio.GetBearing(vessel.latitude, vessel.longitude);

                    // Don't detect vertical deviation if we can't see horizontal deviation.
                    double deviation = Utility.NormalizeLongitude(beacon.approachHeadingILS - absoluteBearing);
                    if (Math.Abs(deviation) <= beacon.localizerSectorILS)
                    {
                        Vector3d vesselPosition = vc.mainBody.GetRelSurfacePosition(vessel.CoMD);
                        Vector3d displacement = vesselPosition - beacon.worldPosition;
                        double angle = 90.0 - Vector3d.Angle(beacon.worldNormal, displacement);

                        if (angle >= glideSlope * 0.45 && angle <= glideSlope * 1.75)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        internal double GetNavAidBearing(int radioId, bool relative)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                if (radio.NavAidInRange(vessel) || (radio.isDME && radio.DMEInRange(vessel)))
                {
                    double absoluteBearing = radio.GetBearing(vessel.latitude, vessel.longitude);
                    if (relative)
                    {
                        return Utility.NormalizeAngle(absoluteBearing - vc.heading);
                    }
                    else
                    {
                        return absoluteBearing;
                    }
                }
            }
            return 0.0;
        }

        internal Vector2d GetNavAidPosition(int radioId)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                if ((radio.isDME && radio.DMEInRange(vessel)) || radio.NavAidInRange(vessel))
                {
                    return new Vector2d(radio.beacon[radio.beaconIndex].longitude, radio.beacon[radio.beaconIndex].latitude); ;
                }
            }

            return Vector2d.zero;
        }

        internal double GetNavAidLatitude(int radioId)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                if ((radio.isDME && radio.DMEInRange(vessel)) || radio.NavAidInRange(vessel))
                {
                    return radio.beacon[radio.beaconIndex].latitude;
                }
            }

            return 0.0;
        }

        internal double GetNavAidLongitude(int radioId)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                if ((radio.isDME && radio.DMEInRange(vessel)) || radio.NavAidInRange(vessel))
                {
                    return radio.beacon[radio.beaconIndex].longitude;
                }
            }

            return 0.0;
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
                //double beaconDmeDistance = radio.beacon[radio.beaconIndex].maximumRangeDME;
                if (radio.DMEInRange(vessel))
                {
                    return (radio.isDME) ? 1.0 : 0.0;
                }
            }

            return -1.0;
        }

        /// <summary>
        /// Get the identifier for the beacon on the selected radio.  The identifier is typically a
        /// three letter code, such as 'CST'.  If no beacon is selected, or no beacon is in range,
        /// returns an empty string.
        /// </summary>
        /// <param name="radioId"></param>
        /// <returns></returns>
        internal string GetNavAidIdentifier(int radioId)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                if (radio.NavAidInRange(vessel))
                {
                    return radio.beacon[radio.beaconIndex].identifier;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Get the name for the beacon on the selected radio.  If no beacon is selected, or no beacon is in range,
        /// returns an empty string.
        /// </summary>
        /// <param name="radioId"></param>
        /// <returns></returns>
        internal string GetNavAidName(int radioId)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                if (radio.NavAidInRange(vessel))
                {
                    return radio.beacon[radio.beaconIndex].name;
                }
            }

            return string.Empty;
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
                if (radio.NavAidInRange(vessel))
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
        /// Returns the NDB bearing for the given radio.  This is bearing relative to the vessel's heading, not
        /// absolute heading to the NDB beacon.
        /// </summary>
        /// <param name="radioId"></param>
        /// <returns>NDB heading (heading relative to the vessel's orientation), in the range [0, 360); -1 if the beacon is out of range, or it is not an NDB.</returns>
        internal double GetNDBBearing(int radioId)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                if (radio.isNDB && radio.NavAidInRange(vessel))
                {
                    return Utility.NormalizeAngle(radio.GetBearing(vessel.latitude, vessel.longitude) - vc.heading);
                }
            }

            return -1.0;
        }

        /// <summary>
        /// Get the frequency that the given radio is set to use.
        /// </summary>
        /// <param name="radioId"></param>
        /// <returns>Frequency in MHz, or 0 if the radio has never been set.</returns>
        internal double GetRadioFrequency(int radioId)
        {
            float radioFrequency;
            if (navRadioFrequency.TryGetValue(radioId, out radioFrequency))
            {
                return radioFrequency;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns a number representing TO/FROM on the given VOR / bearing.
        /// 
        /// * -1: FROM
        /// * 0: No VOR info
        /// * 1: TO
        /// </summary>
        /// <param name="radioId">Radio to use</param>
        /// <param name="bearing">VOR bearing</param>
        /// <returns>-1, 0, or 1 as described in the summary.</returns>
        internal double GetVORApproach(int radioId, double bearing)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                if (radio.isVOR && radio.NavAidInRange(vessel))
                {
                    double absoluteBearing = radio.GetBearing(vessel.latitude, vessel.longitude);
                    // Make it a range between -180 and +180.
                    double deviation = Utility.NormalizeLongitude(bearing - absoluteBearing);
                    if (deviation > 90.0 || deviation < -90.0)
                    {
                        return 1.0;
                    }
                    else
                    {
                        return -1.0;
                    }
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the deviation from the desired bearing line on the VOR in degrees.
        /// </summary>
        /// <param name="radioId">Radio to use</param>
        /// <param name="bearing">VOR bearing</param>
        /// <returns>Deviation in degrees, 0 if no in-range VOR.</returns>
        internal double GetVORDeviation(int radioId, double bearing)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                if (radio.isVOR && radio.NavAidInRange(vessel))
                {
                    double absoluteBearing = radio.GetBearing(vessel.latitude, vessel.longitude);
                    // Make it a range between -180 and +180.
                    double deviation = Utility.NormalizeLongitude(bearing - absoluteBearing);
                    if (deviation > 90.0)
                    {
                        deviation = 180.0 - deviation;
                    }
                    else if (deviation < -90.0)
                    {
                        deviation = -180.0 - deviation;
                    }

                    return deviation;
                }
            }

            return 0.0;
        }

        /// <summary>
        ///  Play the Morse code identifier for the selected radio.  If no valid radio is selected,
        ///  or no radio is in range, this function does nothing.
        /// </summary>
        /// <param name="radioId">Radio to use</param>
        /// <param name="volume">The volume to use for playback, between 0 and 1 (inclusive).</param>
        /// <param name="stopCurrent">If 'true', stops any current audio clip being played.</param>
        /// <returns>1 if a valid identifier is playing, 0 otherwise.</returns>
        internal double PlayNavAidIdentifier(int radioId, double volume, bool stopCurrent)
        {
            NavRadio radio;
            if (navRadio.TryGetValue(radioId, out radio) && radio.beaconIndex >= 0)
            {
                if (radio.NavAidInRange(vessel))
                {
                    return PlayMorseSequence(radio.beacon[radio.beaconIndex].identifier, Mathf.Clamp01((float)volume), stopCurrent);
                }
            }

            return 0.0;
        }
    }
}
