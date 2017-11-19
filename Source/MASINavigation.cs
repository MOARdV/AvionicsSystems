/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016 - 2017 MOARdV
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
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// The MASINavigation module encapsulates navigation-related functionality.
    /// </summary>
    /// <LuaName>nav</LuaName>
    /// <mdDoc>**CAUTION:** These methods are subject to change as this code matures.
    /// 
    /// MASINavigation encapsulates navigation functionality, including
    /// emulating radio navigation.  It provides methods to determine the distance
    /// from the vessel to a particular lat/lon location on the planet, and the
    /// distance between two arbitrary points on a planet.
    /// 
    /// All methods use the current vessel's
    /// parent body as the body for calculations.
    /// 
    /// Equations adapted from http://www.movable-type.co.uk/scripts/latlong.html.
    /// </mdDoc>
    internal class MASINavigation
    {
        private static readonly double Deg2Rad = Math.PI / 180.0;
        private static readonly double Rad2Deg = 180.0 / Math.PI;

        private Vessel vessel;
        private CelestialBody body;
        private double bodyRadius;
        private MASFlightComputer fc;

        [MoonSharpHidden]
        public MASINavigation(Vessel vessel, MASFlightComputer fc)
        {
            this.vessel = vessel;
            this.body = vessel.mainBody;
            this.bodyRadius = this.body.Radius;
            this.fc = fc;
        }

        ~MASINavigation()
        {
            vessel = null;
            body = null;
        }

        [MoonSharpHidden]
        internal void Update()
        {
            // Probably oughtn't need to poll this
            body = vessel.mainBody;
            bodyRadius = body.Radius;
        }

        [MoonSharpHidden]
        internal void UpdateVessel(Vessel vessel)
        {
            this.vessel = vessel;
            this.body = vessel.mainBody;
            this.bodyRadius = this.body.Radius;
        }

        /// <summary>
        /// The General Navigation section contains general-purpose navigational formulae that can be used
        /// for navigation near a planet's surface.
        /// </summary>
        #region General Navigation

        /// <summary>
        /// Returns the great-circle route bearing from (lat1, lon1) to (lat2, lon2).
        /// </summary>
        /// <param name="latitude1">Latitude of position 1 in degrees.  Negative values indicate south, positive is north.</param>
        /// <param name="longitude1">Longitude of position 1 in degrees.  Negative values indicate west, positive is east.</param>
        /// <param name="latitude2">Latitude of position 2 in degrees.  Negative values indicate south, positive is north.</param>
        /// <param name="longitude2">Longitude of position 2 in degrees.  Negative values indicate west, positive is east.</param>
        /// <returns>Bearing (heading) in degrees.</returns>
        public double Bearing(double latitude1, double longitude1, double latitude2, double longitude2)
        {
            double lat1 = latitude1 * Deg2Rad;
            double lat2 = latitude2 * Deg2Rad;
            double dLon = (longitude2 - longitude1) * Deg2Rad;

            double y = Math.Sin(dLon) * Math.Cos(lat2);
            double x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);

            return Utility.NormalizeAngle(Math.Atan2(y, x) * Rad2Deg);
        }

        /// <summary>
        /// Returns the great-circle bearing from the vessel to the specified
        /// lat/lon coordinates.
        /// </summary>
        /// <param name="latitude">Latitude of the destination point in degrees.  Negative values indicate south, positive is north.</param>
        /// <param name="longitude">Longitude of destination point in degrees.  Negative values indicate west, positive is east.</param>
        /// <returns>Hearing in degrees from the vessel to the destination.</returns>
        public double BearingFromVessel(double latitude, double longitude)
        {
            return Bearing(vessel.latitude, Utility.NormalizeLongitude(vessel.longitude), latitude, longitude);
        }

        /// <summary>
        /// Returns the latitude found at the given range along the given bearing.
        /// 
        /// **TODO:** Replace `fc.DestinationLatitude` and `fc.DestinationLongitude` with
        /// `fc.DestinationCoordinates` which returns latitude and longitude at the same time
        /// so computations do not need to be made twice.
        /// </summary>
        /// <param name="latitude">Latitude of the point of origin.  Negative values indicate south, positive is north.</param>
        /// <param name="longitude">Longitude of the point of origin.  Negative values indicate west, positive is east.</param>
        /// <param name="range">Distance to travel along the bearing, in meters.</param>
        /// <param name="bearing">Bearing in degrees to travel.</param>
        /// <returns>Latitude in degrees.</returns>
        public double DestinationLatitude(double latitude, double longitude, double range, double bearing)
        {
            //Formula:	
            //φ2 = asin( sin φ1 ⋅ cos δ + cos φ1 ⋅ sin δ ⋅ cos θ )
            //λ2 = λ1 + atan2( sin θ ⋅ sin δ ⋅ cos φ1, cos δ − sin φ1 ⋅ sin φ2 )
            //where	φ is latitude, λ is longitude, θ is the bearing (clockwise from north), δ is the angular distance d/R; d being the distance travelled, R the earth’s radius
            double phi1 = latitude * Deg2Rad;
            double theta = bearing * Deg2Rad;
            double delta = range / bodyRadius;

            double phi2 = Math.Asin(Math.Sin(phi1) * Math.Cos(delta) + Math.Cos(phi1) * Math.Sin(delta) * Math.Cos(theta));
            return phi2 * Rad2Deg;
        }

        /// <summary>
        /// Returns the latitude found at the given range along the given bearing from the current vessel.
        /// </summary>
        /// <param name="range">Distance to travel along the bearing, in meters.</param>
        /// <param name="bearing">Bearing in degrees to travel.</param>
        /// <returns>Latitude in degrees.</returns>
        public double DestinationLatitudeFromVessel(double range, double bearing)
        {
            return DestinationLatitude(vessel.latitude, Utility.NormalizeLongitude(vessel.longitude), range, bearing);
        }

        /// <summary>
        /// Returns the longitude found at the given range along the given bearing from the point of origin.
        /// </summary>
        /// <param name="latitude">Latitude of the point of origin.  Negative values indicate south, positive is north.</param>
        /// <param name="longitude">Longitude of the point of origin.  Negative values indicate west, positive is east.</param>
        /// <param name="range">Distance to travel along the bearing, in meters.</param>
        /// <param name="bearing">Bearing in degrees to travel.</param>
        /// <returns>Longitude in degrees.</returns>
        public double DestinationLongitude(double latitude, double longitude, double range, double bearing)
        {
            //Formula:	
            //φ2 = asin( sin φ1 ⋅ cos δ + cos φ1 ⋅ sin δ ⋅ cos θ )
            //λ2 = λ1 + atan2( sin θ ⋅ sin δ ⋅ cos φ1, cos δ − sin φ1 ⋅ sin φ2 )
            //where	φ is latitude, λ is longitude, θ is the bearing (clockwise from north), δ is the angular distance d/R; d being the distance travelled, R the earth’s radius
            double phi1 = latitude * Deg2Rad;
            double lambda1 = longitude * Deg2Rad;
            double theta = bearing * Deg2Rad;
            double delta = range / bodyRadius;

            double phi2 = Math.Asin(Math.Sin(phi1) * Math.Cos(delta) + Math.Cos(phi1) * Math.Sin(delta) * Math.Cos(theta));
            double lambda2 = lambda1 + Math.Atan2(Math.Sin(theta) * Math.Sin(delta) * Math.Cos(phi1), Math.Cos(delta) - Math.Sin(phi1) * Math.Sin(phi2));

            return Utility.NormalizeLongitude(lambda2 * Rad2Deg);
        }

        /// <summary>
        /// Returns the longitude found at the given range along the given bearing from the current vessel.
        /// </summary>
        /// <param name="range">Distance to travel along the bearing, in meters.</param>
        /// <param name="bearing">Bearing in degrees to travel.</param>
        /// <returns>Longitude in degrees.</returns>
        public double DestinationLongitudeFromVessel(double range, double bearing)
        {
            return DestinationLongitude(vessel.latitude, Utility.NormalizeLongitude(vessel.longitude), range, bearing);
        }

        /// <summary>
        /// Return the ground distance between two coordinates on a planet.
        /// 
        /// Assumes the planet is the one the vessel is currently orbiting / flying
        /// over.  Uses the sea-level altitude.
        /// </summary>
        /// <param name="latitude1">Latitude of position 1 in degrees.  Negative values indicate south, positive is north.</param>
        /// <param name="longitude1">Longitude of position 1 in degrees.  Negative values indicate west, positive is east.</param>
        /// <param name="latitude2">Latitude of position 2 in degrees.  Negative values indicate south, positive is north.</param>
        /// <param name="longitude2">Longitude of position 2 in degrees.  Negative values indicate west, positive is east.</param>
        /// <returns>Distance in meters between the two points, following the surface of the planet.</returns>
        public double GroundDistance(double latitude1, double longitude1, double latitude2, double longitude2)
        {
            double lat1 = latitude1 * Deg2Rad;
            double lat2 = latitude2 * Deg2Rad;
            double sinLat = Math.Sin((latitude2 - latitude1) * Deg2Rad * 0.5);
            double sinLon = Math.Sin((longitude2 - longitude1) * Deg2Rad * 0.5);
            double a = sinLat * sinLat + Math.Cos(lat1) * Math.Cos(lat2) * sinLon * sinLon;

            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

            return c * bodyRadius;
        }

        /// <summary>
        /// Return the ground distance between the vessel and a location on the surface
        /// of the planet.
        /// 
        /// Uses the sea-level altitude.
        /// </summary>
        /// <param name="latitude">Latitude of the destination point in degrees.  Negative values indicate south, positive is north.</param>
        /// <param name="longitude">Longitude of destination point in degrees.  Negative values indicate west, positive is east.</param>
        /// <returns>Distance in meters between the two points, following the surface of the planet.</returns>
        public double GroundDistanceFromVessel(double latitude, double longitude)
        {
            return GroundDistance(vessel.latitude, Utility.NormalizeLongitude(vessel.longitude), latitude, longitude);
        }

        /// <summary>
        /// Returns the distance to the horizon based on a given altitude ASL.
        /// 
        /// The horizon is assumed to be at sea level.  Due to the small sizes of
        /// most stock KSP worlds, and the relatively large mountains, this number
        /// will not be reliable for estimating whether a given land position is
        /// within line-of-sight.
        /// </summary>
        /// <param name="altitude">Altitude above sea level, in meters.</param>
        /// <returns>Distance to the horizon, in meters.</returns>
        public double LineOfSight(double altitude)
        {
            return Math.Sqrt(altitude * (2.0 * bodyRadius + altitude));
        }

        #endregion

        /// <summary>
        /// The Radio Navigation section provides methods for emulating navigational radio
        /// on board aircraft (or ships, or whatever).
        /// 
        /// Most methods are centered around a selected radio (the `radioId` parameter, and
        /// they assume all computations in relation to the current active vessel.  Because
        /// these methods emulate navigational radios, they account for limitations caused
        /// by the curvature of the planet as well as limited radio broadcasting distances.
        /// 
        /// Because of this, methods may return values indicating "no signal" even if the
        /// radio is tuned to a correct value.
        /// 
        /// The `radioId` parameter may be any integer (non-integer numbers are converted to
        /// integers).  MAS does not place any restrictions on how many radios are used
        /// on a vessel, not does it place restrictions on what radio ids may be used.  If
        /// the IVA creator wishes to use ids 2, 17, and 21, then MAS allows it.
        /// 
        /// Frequency is assumed to be in MHz, and MAS assumes radios have about a 10kHz
        /// minimum frequency separation (real-world VOR uses 50kHz), so setting a radio
        /// to 105.544 will select any navaids on a frequency between 105.494 and 105.594.
        /// </summary>
        #region Radio Navigation

        /// <summary>
        /// Queries the radio beacon selected by radioId to determine if it includes
        /// DME equipment.  Returns one of three values:
        /// 
        /// * -1: No navaid beacons are in range on the current frequency.
        /// * 0: A navaid beacon is in range, but it does not support DME.
        /// * 1: A navaid beacon is in range, and it supports DME.
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <returns>As described in the summary.</returns>
        public double GetNavAidDME(double radioId)
        {
            return fc.GetNavAidDME((int)radioId);
        }

        /// <summary>
        /// Returns the type of radio beacon the radio currently is detecting.  Returns
        /// one of three values:
        /// 
        /// * 0: No navaid beacons are in range on the current frequency.
        /// * 1: Beacon is NDB.
        /// * 2: Beacon is VOR.
        /// * 3: Beacon is ILS.
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <returns>As described in the summary.</returns>
        public double GetNavAidType(double radioId)
        {
            return fc.GetNavAidType((int)radioId);
        }

        /// <summary>
        /// Returns the radio frequency setting for the specified radio.
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <returns>0 if the radio does not have a frequency set, otherwise, the frequency.</returns>
        public double GetRadioFrequency(double radioId)
        {
            return fc.GetRadioFrequency((int)radioId);
        }

        /// <summary>
        /// Sets the specified navigational radio to the frequency.
        /// 
        /// If frequency is less than or equal to 0.0, the radio is
        /// switched off.
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <param name="frequency">The frequency of the radio, assumed in MHz.</param>
        /// <returns>1 if the radio was switched off, or if any radios have a frequency within 10kHz of the requested frequency (regardless of range).</returns>
        public double SetRadioFrequency(double radioId, double frequency)
        {
            //LatLon
            return (fc.SetRadioFrequency((int)radioId, (float)frequency)) ? 1.0 : 0.0;
        }
        #endregion
    }
}
