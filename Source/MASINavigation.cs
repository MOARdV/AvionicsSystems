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
using System.Reflection;
using System.Text;

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

        [MoonSharpHidden]
        public MASINavigation(Vessel vessel)
        {
            this.vessel = vessel;
            this.body = vessel.mainBody;
            this.bodyRadius = this.body.Radius;
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
        /// **UNTESTED**
        /// 
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
            //double lon1 = longitude1 * Deg2Rad;
            //double lon2 = longitude2 * Deg2Rad;
            double dLon = (longitude2 - longitude1) * Deg2Rad;

            double y = Math.Sin(dLon) * Math.Cos(lat2);
            double x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
            //double y = Math.Sin(lon2 - lon1) * Math.Cos(lat2);
            //double x = Math.Cos(lat1) * Math.Sin(lat2) -
            //        Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(lon2 - lon1);
            return Math.Atan2(y, x) * Rad2Deg;
        }

        /// <summary>
        /// **UNTESTED**
        /// 
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
            /*
Formula:	
φ2 = asin( sin φ1 ⋅ cos δ + cos φ1 ⋅ sin δ ⋅ cos θ )
λ2 = λ1 + atan2( sin θ ⋅ sin δ ⋅ cos φ1, cos δ − sin φ1 ⋅ sin φ2 )
where	φ is latitude, λ is longitude, θ is the bearing (clockwise from north), δ is the angular distance d/R; d being the distance travelled, R the earth’s radius
             */
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
            /*
Formula:	
φ2 = asin( sin φ1 ⋅ cos δ + cos φ1 ⋅ sin δ ⋅ cos θ )
λ2 = λ1 + atan2( sin θ ⋅ sin δ ⋅ cos φ1, cos δ − sin φ1 ⋅ sin φ2 )
where	φ is latitude, λ is longitude, θ is the bearing (clockwise from north), δ is the angular distance d/R; d being the distance travelled, R the earth’s radius
             */
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
            return Math.Sqrt(2.0 * bodyRadius * altitude + altitude * altitude);
        }
    }

    /*
    public struct NavObject
    {
        double latitude;
        double longitude;
        double altitude;
        float frequency;
        string name;
        string identifier;
        int todo_navType;
    }
     */
}
