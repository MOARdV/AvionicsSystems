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
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// The MASINavigation module encapsulates navigation-related functionality.
    /// </summary>
    /// <LuaName>nav</LuaName>
    /// <mdDoc>
    /// MASINavigation encapsulates navigation functionality, including
    /// emulating radio navigation and GNSS navigation (by using stock waypoints).
    /// It provides methods to determine the distance
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
        private Vessel vessel;
        private CelestialBody body;
        private double bodyRadius;
        private MASFlightComputer fc;

        private int activeWaypoint = -1;

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

            // Be nice if this could be done more efficiently.
            activeWaypoint = -1;
            if (NavWaypoint.fetch.IsActive)
            {
                double wpLat = NavWaypoint.fetch.Latitude;
                double wpLon = NavWaypoint.fetch.Longitude;

                var waypoints = FinePrint.WaypointManager.Instance().Waypoints;
                int numWP = waypoints.Count;
                for (int i = 0; i < numWP; ++i)
                {
                    if (waypoints[i].latitude == wpLat && waypoints[i].longitude == wpLon)
                    {
                        activeWaypoint = i;
                        break;
                    }
                }
            }
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
            double lat1 = latitude1 * Utility.Deg2Rad;
            double lat2 = latitude2 * Utility.Deg2Rad;
            double dLon = (longitude2 - longitude1) * Utility.Deg2Rad;

            double y = Math.Sin(dLon) * Math.Cos(lat2);
            double x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);

            return Utility.NormalizeAngle(Math.Atan2(y, x) * Utility.Rad2Deg);
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
        /// **UNTESTED**
        /// 
        /// Returns the cross-track distance, in meters, of the specified location relative to the vessel's current heading.
        /// </summary>
        /// <param name="latitude">Latitude of the location of interest.</param>
        /// <param name="longitude">Longitude of the location of interest.</param>
        /// <returns>Distance in meters from the current route to the location.</returns>
        public double CrossTrackDistance(double latitude, double longitude)
        {
            double targetBearing = BearingFromVessel(latitude, longitude);

            // TODO: More efficient way to do this:
            Vector3d vesselNormal = QuaternionD.AngleAxis(vessel.longitude, Vector3d.down) * QuaternionD.AngleAxis(vessel.latitude, Vector3d.forward) * Vector3d.right;
            Vector3d targetNormal = QuaternionD.AngleAxis(longitude, Vector3d.down) * QuaternionD.AngleAxis(latitude, Vector3d.forward) * Vector3d.right;
            double targetAngularDistance = Vector3d.Angle(vesselNormal, targetNormal) * Utility.Deg2Rad;

            return Math.Asin(Math.Sin(targetAngularDistance) * Math.Sin((targetBearing - fc.vc.progradeHeading) * Utility.Deg2Rad)) * vessel.mainBody.Radius;
        }

        /// <summary>
        /// Returns the latitude found at the given range along the given bearing.
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
            double phi1 = latitude * Utility.Deg2Rad;
            double theta = bearing * Utility.Deg2Rad;
            double delta = range / bodyRadius;

            double phi2 = Math.Asin(Math.Sin(phi1) * Math.Cos(delta) + Math.Cos(phi1) * Math.Sin(delta) * Math.Cos(theta));
            return phi2 * Utility.Rad2Deg;
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
            double phi1 = latitude * Utility.Deg2Rad;
            double lambda1 = longitude * Utility.Deg2Rad;
            double theta = bearing * Utility.Deg2Rad;
            double delta = range / bodyRadius;

            double phi2 = Math.Asin(Math.Sin(phi1) * Math.Cos(delta) + Math.Cos(phi1) * Math.Sin(delta) * Math.Cos(theta));
            double lambda2 = lambda1 + Math.Atan2(Math.Sin(theta) * Math.Sin(delta) * Math.Cos(phi1), Math.Cos(delta) - Math.Sin(phi1) * Math.Sin(phi2));

            return Utility.NormalizeLongitude(lambda2 * Utility.Rad2Deg);
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
            double lat1 = latitude1 * Utility.Deg2Rad;
            double lat2 = latitude2 * Utility.Deg2Rad;
            double sinLat = Math.Sin((latitude2 - latitude1) * Utility.Deg2Rad * 0.5);
            double sinLon = Math.Sin((longitude2 - longitude1) * Utility.Deg2Rad * 0.5);
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
        /// ** UNIMPLEMENTED **
        /// Return the latitude where two great-circle paths intersect.
        /// </summary>
        /// <param name="latitude1">Latitude of the start of path 1.</param>
        /// <param name="longitude1">Longitude of the start of path 1.</param>
        /// <param name="bearing1">Initial bearing of path 1.</param>
        /// <param name="latitude2">Latitude of the start of path 2.</param>
        /// <param name="longitude2">Longitude of the start of path 2.</param>
        /// <param name="bearing2">Initial bearing of path 2.</param>
        /// <returns>The latitude where the two paths intersect.</returns>
        public double IntersectionOfTwoPathsLatitude(double latitude1, double longitude1, double bearing1, double latitude2, double longitude2, double bearing2)
        {
            /*
            δ12 = 2⋅asin( √(sin²(Δφ/2) + cos φ1 ⋅ cos φ2 ⋅ sin²(Δλ/2)) )	angular dist. p1–p2
            θa = acos( ( sin φ2 − sin φ1 ⋅ cos δ12 ) / ( sin δ12 ⋅ cos φ1 ) )
            θb = acos( ( sin φ1 − sin φ2 ⋅ cos δ12 ) / ( sin δ12 ⋅ cos φ2 ) )	initial / final bearings
            between points 1 & 2
            if sin(λ2−λ1) > 0
                θ12 = θa
                θ21 = 2π − θb
            else
                θ12 = 2π − θa
                θ21 = θb	
            α1 = θ13 − θ12
            α2 = θ21 − θ23	angle p2–p1–p3
            angle p1–p2–p3
            α3 = acos( −cos α1 ⋅ cos α2 + sin α1 ⋅ sin α2 ⋅ cos δ12 )	angle p1–p2–p3
            δ13 = atan2( sin δ12 ⋅ sin α1 ⋅ sin α2 , cos α2 + cos α1 ⋅ cos α3 )	angular dist. p1–p3
            φ3 = asin( sin φ1 ⋅ cos δ13 + cos φ1 ⋅ sin δ13 ⋅ cos θ13 )	p3 lat
            Δλ13 = atan2( sin θ13 ⋅ sin δ13 ⋅ cos φ1 , cos δ13 − sin φ1 ⋅ sin φ3 )	long p1–p3
            λ3 = λ1 + Δλ13  
            where	
                φ1, λ1, θ13 : 1st start point & (initial) bearing from 1st point towards intersection point
                φ2, λ2, θ23 : 2nd start point & (initial) bearing from 2nd point towards intersection point
                φ3, λ3 : intersection point

                % = (floating point) modulo
             */
            return 0.0;
        }

        /// <summary>
        /// ** UNIMPLEMENTED **
        /// Return the latitude where two great-circle paths intersect.
        /// </summary>
        /// <param name="latitude1">Latitude of the start of path 1.</param>
        /// <param name="longitude1">Longitude of the start of path 1.</param>
        /// <param name="bearing1">Initial bearing of path 1.</param>
        /// <param name="latitude2">Latitude of the start of path 2.</param>
        /// <param name="longitude2">Longitude of the start of path 2.</param>
        /// <param name="bearing2">Initial bearing of path 2.</param>
        /// <returns>The latitude where the two paths intersect.</returns>
        public double IntersectionOfTwoPathsLongitude(double latitude1, double longitude1, double bearing1, double latitude2, double longitude2, double bearing2)
        {
            /*
            δ12 = 2⋅asin( √(sin²(Δφ/2) + cos φ1 ⋅ cos φ2 ⋅ sin²(Δλ/2)) )	angular dist. p1–p2
            θa = acos( ( sin φ2 − sin φ1 ⋅ cos δ12 ) / ( sin δ12 ⋅ cos φ1 ) )
            θb = acos( ( sin φ1 − sin φ2 ⋅ cos δ12 ) / ( sin δ12 ⋅ cos φ2 ) )	initial / final bearings
            between points 1 & 2
            if sin(λ2−λ1) > 0
                θ12 = θa
                θ21 = 2π − θb
            else
                θ12 = 2π − θa
                θ21 = θb	
            α1 = θ13 − θ12
            α2 = θ21 − θ23	angle p2–p1–p3
            angle p1–p2–p3
            α3 = acos( −cos α1 ⋅ cos α2 + sin α1 ⋅ sin α2 ⋅ cos δ12 )	angle p1–p2–p3
            δ13 = atan2( sin δ12 ⋅ sin α1 ⋅ sin α2 , cos α2 + cos α1 ⋅ cos α3 )	angular dist. p1–p3
            φ3 = asin( sin φ1 ⋅ cos δ13 + cos φ1 ⋅ sin δ13 ⋅ cos θ13 )	p3 lat
            Δλ13 = atan2( sin θ13 ⋅ sin δ13 ⋅ cos φ1 , cos δ13 − sin φ1 ⋅ sin φ3 )	long p1–p3
            λ3 = λ1 + Δλ13             
            where	
                φ1, λ1, θ13 : 1st start point & (initial) bearing from 1st point towards intersection point
                φ2, λ2, θ23 : 2nd start point & (initial) bearing from 2nd point towards intersection point
                φ3, λ3 : intersection point

                % = (floating point) modulo
             */
            return 0.0;
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

        /// <summary>
        /// Returns the slant distance (direct distance) between two locations on the current world.
        /// </summary>
        /// <param name="latitude1">Latitude of the origin.</param>
        /// <param name="longitude1">Longitude of the origin.</param>
        /// <param name="altitude1">Altitude of the origin.</param>
        /// <param name="latitude2">Latitude of the destination.</param>
        /// <param name="longitude2">Longitude of the destination.</param>
        /// <param name="altitude2">Altitude of the destination.</param>
        /// <returns></returns>
        public double SlantDistance(double latitude1, double longitude1, double altitude1, double latitude2, double longitude2, double altitude2)
        {
            Vector3d origin = vessel.mainBody.GetRelSurfacePosition(latitude1, longitude1, altitude1);
            Vector3d destination = vessel.mainBody.GetRelSurfacePosition(latitude2, longitude2, altitude2);

            return Vector3d.Distance(origin, destination);
        }

        /// <summary>
        /// Returns the slant distance (direct distance) between the vessel and a given location and altitude on the current
        /// planet.
        /// </summary>
        /// <param name="latitude">Latitude of the destination.</param>
        /// <param name="longitude">Longitude of the destination.</param>
        /// <param name="altitude">Altitude of the destination.</param>
        /// <returns>Distance in meters from the vessel to the selected location.</returns>
        public double SlantDistanceFromVessel(double latitude, double longitude, double altitude)
        {
            return SlantDistance(vessel.latitude, Utility.NormalizeLongitude(vessel.longitude), vessel.altitude, latitude, longitude, altitude);
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
        /// radio is tuned to a valid beacon.
        /// 
        /// The `radioId` parameter should be an integer (non-integer numbers are converted to
        /// integers).  MAS does not place any restrictions on how many radios are used
        /// on a vessel, not does it place restrictions on what radio ids may be used.  If
        /// the IVA creator wishes to use ids 2, 17, and 21, then MAS allows it.
        /// 
        /// Frequency is assumed to be in MHz, and MAS assumes radios have about a 10kHz
        /// minimum frequency separation (real-world VOR uses 50kHz), so setting a radio
        /// to 105.544 will select any navaids on a frequency between 105.499 and 105.599.
        /// </summary>
        #region Radio Navigation

        /// <summary>
        /// Returns the slant distance in meters to a DME beacon selected on the given radio.  If there is
        /// no DME equipment in range, returns -1.
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <returns>Distance to DME in meters, or -1 if no DME equipment is in range on the given radio frequency.</returns>
        public double GetDMESlantDistance(double radioId)
        {
            return fc.GetDMESlantDistance((int)radioId);
        }

        /// <summary>
        /// Returns the horizontal error from the ILS localizer beam, up to the
        /// limit set by the ILS beacon's `localizerSectorILS` parameter.  If there
        /// is no ILS localizer, or the vessel is outside the localizer's
        /// sector, returns 0.
        /// 
        /// A negative value indicates that the vessel is to the left of the localizer
        /// beam, while a positive value indicates that the vessel is to the right of the beam.
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <returns>Localizer error, or 0.</returns>
        public double GetILSLocalizerError(double radioId)
        {
            return fc.GetILSLocalizerError((int)radioId);
        }

        /// <summary>
        /// Returns 1 if the localizer signal is valid, 0 if it is not (or the beacon is not an ILS).
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <returns>1 if a valid ILS localizer is detected; 0 otherwise.</returns>
        public double GetILSLocalizerValid(double radioId)
        {
            return fc.GetILSLocalizerValid((int)radioId) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Return the default glide slope for the given ILS beacon.  Returns 0
        /// if ILS out of glide slope range, or radio is not tuned to an ILS beacon.
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <returns>Default glide slope, or 0.</returns>
        public double GetILSGlideSlopeDefault(double radioId)
        {
            return fc.GetILSGlideSlopeDefault((int)radioId);
        }

        /// <summary>
        /// Returns the vertical deviation from the ILS glide path beam, up to the
        /// limit set by the ILS beacon's `glidePathSectorILS`.  If there is no ILS
        /// beacon, or the vessel is outside the glide path's sector, returns 0.
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <param name="glideSlope">The desired glide slope.</param>
        /// <returns>The deflection from the desired glide slope, or 0.</returns>
        public double GetILSGlideSlopeError(double radioId, double glideSlope)
        {
            return fc.GetILSGlideSlopeError((int)radioId, glideSlope);
        }

        /// <summary>
        /// Returns whether the glide slope error is valid.
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <param name="glideSlope">The desired glide slope.</param>
        /// <returns>1 if the vessel is within the glide slope limits of a valid ILS glide path, 0 otherwise</returns>
        public double GetILSGlideSlopeValid(double radioId, double glideSlope)
        {
            return fc.GetILSGlideSlopeValid((int)radioId, glideSlope) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the bearing to the navaid selected by radioId.  If no radio is in range,
        /// returns 0.
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <param name="relativeBearing">If true, returns the bearing to the beacon relative to the vessel's front.
        /// If false, returns the absolute bearing to the beacon (relative to north).</param>
        /// <returns>Absolute or relative bearing to the navaid beacon, or 0.</returns>
        public double GetNavAidBearing(double radioId, bool relativeBearing)
        {
            return fc.GetNavAidBearing((int)radioId, relativeBearing);
        }

        /// <summary>
        /// Returns the cross-track distance in meters, to the navaid specified by radioId.  If no radio
        /// is in range, returns 0.
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <returns>Cross-track distance in meters.</returns>
        public double GetNavAidCrossTrackDistance(double radioId)
        {
            Vector2d latlon = fc.GetNavAidPosition((int)radioId);
            return CrossTrackDistance(latlon.y, latlon.x);
        }

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
        /// Get the identifier for the beacon on the selected radio.  The identifier is typically a
        /// three letter code, such as 'CST'.  If no beacon is selected, or no beacon is in range,
        /// returns an empty string.
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <returns>Beacon identifier, or an empty string if no beacon is in range on the current frequency.</returns>
        public string GetNavAidIdentifier(double radioId)
        {
            return fc.GetNavAidIdentifier((int)radioId);
        }

        /// <summary>
        /// Returns the latitude of the active beacon.  If no beacon is selected, or no beacon is
        /// in range, returns 0.
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <returns>Latitude, or 0</returns>
        public double GetNavAidLatitude(double radioId)
        {
            return fc.GetNavAidLatitude((int)radioId);
        }

        /// <summary>
        /// Returns the longitude of the active beacon.  If no beacon is selected, or no beacon is
        /// in range, returns 0.
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <returns>Longitude, or 0</returns>
        public double GetNavAidLongitude(double radioId)
        {
            return fc.GetNavAidLongitude((int)radioId);
        }

        /// <summary>
        /// Get the name for the beacon on the selected radio.  If no beacon is selected, or no beacon is in range,
        /// returns an empty string.
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <returns>Beacon name, or an empty string if no beacon is in range on the current frequency.</returns>
        public string GetNavAidName(double radioId)
        {
            return fc.GetNavAidName((int)radioId);
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
        /// Returns the NDB bearing for the given radio.  This is bearing relative to the vessel's heading, not
        /// absolute heading to the NDB beacon.
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <returns>Vessel-relative bearing to the NDB beacon, in the range [0, 360).  If the radio is not tuned to an NDB, or the NDB is out of range, returns -1.</returns>
        public double GetNDBBearing(double radioId)
        {
            return fc.GetNDBBearing((int)radioId);
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
        /// Returns a number representing TO/FROM on the given VOR / bearing.
        /// 
        /// * -1: FROM
        /// * 0: No VOR info
        /// * 1: TO
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <param name="bearing">VOR bearing</param>
        /// <returns>-1, 0, or 1 as described in the summary.</returns>
        public double GetVORApproach(double radioId, double bearing)
        {
            return fc.GetVORApproach((int)radioId, bearing);
        }

        /// <summary>
        /// Returns the deviation from the desired bearing line on the VOR in degrees.
        /// </summary>
        /// <param name="radioId">The id of the radio, any integer value.</param>
        /// <param name="bearing">VOR bearing</param>
        /// <returns>Deviation in degrees, 0 if no in-range VOR.</returns>
        public double GetVORDeviation(double radioId, double bearing)
        {
            return fc.GetVORDeviation((int)radioId, bearing);
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

        /// <summary>
        /// The Waypoint Navigation section provides methods used to interact with stock and custom waypoints.
        /// 
        /// These methods differ from the Radio Nagivation section in that they do not include any gameplay
        /// simulation of radio navigation - these methods are more suited for Global Navigation Satellite Systems.
        /// 
        /// Most waypoint queries take a `waypointIndex` parameter.  This parameter must be in the range
        /// of [0, nav.GetWaypointCount()-1] to select a custom waypoint, or -1 to select the current active
        /// waypoint.
        /// </summary>
        #region Waypoint Navigation

        /// <summary>
        /// Get the custom waypoint index of the current waypoint.  If there is no active
        /// waypoint, or the current waypoint is not a custom waypoint, return -1.
        /// </summary>
        /// <returns>Index of the active waypoint, or -1 if no waypoint is active or the waypoint is not a custom waypoint.</returns>
        public double GetWaypointIndex()
        {
            return activeWaypoint;
        }

        /// <summary>
        /// Set the stock waypoint system to the waypoint number selected.
        /// 
        /// A negative value, or a value equal to or greater than `nav.GetWaypointCount()`
        /// will clear the current waypoint.
        /// </summary>
        /// <param name="waypointIndex">The waypoint to select.</param>
        /// <returns>1 if a waypoint was selected, 0 otherwise.</returns>
        public double SetWaypoint(double waypointIndex)
        {
            int index = (int)waypointIndex;

            if (NavWaypoint.fetch.IsActive)
            {
                NavWaypoint.fetch.Clear();
                NavWaypoint.fetch.Deactivate();
            }

            var waypoints = FinePrint.WaypointManager.Instance().Waypoints;

            if (index >= 0 && index < waypoints.Count)
            {
                NavWaypoint.fetch.Setup(waypoints[index]);
                NavWaypoint.fetch.Activate();
            }

            return 0.0;
        }

        /// <summary>
        /// Returns 1 if there is a waypoint active.
        /// </summary>
        /// <returns>1 if a waypoint is active, 0 otherwise.</returns>
        public double WaypointActive()
        {
            return (NavWaypoint.fetch.IsActive) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Get the altitude of the waypoint selected by waypointIndex, or the current active
        /// waypoint if waypointIndex is -1.
        /// </summary>
        /// <param name="waypointIndex">The waypoint to name, or -1 to select the current active waypoint.</param>
        /// <returns>Altitude of the selected waypoint, or 0.</returns>
        public double WaypointAltitude(double waypointIndex)
        {
            int index = (int)waypointIndex;

            var waypoints = FinePrint.WaypointManager.Instance().Waypoints;
            if (index >= 0 && index < waypoints.Count)
            {
                return waypoints[index].altitude;
            }
            else if (index == -1 && NavWaypoint.fetch.IsActive)
            {
                return NavWaypoint.fetch.Altitude;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Get the absolute bearing to the selected waypoint (bearing relative to North).
        /// </summary>
        /// <param name="waypointIndex">The waypoint index, or -1 to select the current active waypoint.</param>
        /// <returns>The bearing to the waypoint, or -1 if there is no selected waypoint.</returns>
        public double WaypointBearing(double waypointIndex)
        {
            int index = (int)waypointIndex;

            var waypoints = FinePrint.WaypointManager.Instance().Waypoints;
            if (index >= 0 && index < waypoints.Count)
            {
                return BearingFromVessel(waypoints[index].latitude, waypoints[index].longitude);
            }
            else if (index == -1 && NavWaypoint.fetch.IsActive)
            {
                return BearingFromVessel(NavWaypoint.fetch.Latitude, NavWaypoint.fetch.Longitude);
            }
            else
            {
                return -1.0;
            }
        }

        /// <summary>
        /// Returns the number of waypoints in the custom waypoints table.
        /// </summary>
        /// <returns>The number of waypoints.</returns>
        public double WaypointCount()
        {
            return FinePrint.WaypointManager.Instance().Waypoints.Count;
        }

        /// <summary>
        /// Get the cross-track distance to the selected waypoint in meters.
        /// </summary>
        /// <param name="waypointIndex">The waypoint index, or -1 to select the current active waypoint.</param>
        /// <returns>Returns the cross-track distance in meters, or 0 if the selected waypoint is invalid.</returns>
        public double WaypointCrossTrackDistance(double waypointIndex)
        {
            int index = (int)waypointIndex;

            var waypoints = FinePrint.WaypointManager.Instance().Waypoints;
            if (index >= 0 && index < waypoints.Count)
            {
                return CrossTrackDistance(waypoints[index].latitude, waypoints[index].longitude);
            }
            else if (index == -1 && NavWaypoint.fetch.IsActive)
            {
                return CrossTrackDistance(NavWaypoint.fetch.Latitude, NavWaypoint.fetch.Longitude);
            }
            else
            {
                return -1.0;
            }
        }

        /// <summary>
        /// Get the slant distance to the selected waypoint in meters.
        /// </summary>
        /// <param name="waypointIndex">The waypoint index, or -1 to select the current active waypoint.</param>
        /// <returns>The distance to the waypoint in meters, or -1 if there is no selected waypoint.</returns>
        public double WaypointDistance(double waypointIndex)
        {
            int index = (int)waypointIndex;

            var waypoints = FinePrint.WaypointManager.Instance().Waypoints;
            if (index >= 0 && index < waypoints.Count)
            {
                return SlantDistanceFromVessel(waypoints[index].latitude, waypoints[index].longitude, waypoints[index].altitude);
            }
            else if (index == -1 && NavWaypoint.fetch.IsActive)
            {
                return SlantDistanceFromVessel(NavWaypoint.fetch.Latitude, NavWaypoint.fetch.Longitude, NavWaypoint.fetch.Altitude);
            }
            else
            {
                return -1.0;
            }
        }

        /// <summary>
        /// Get the latitude of the waypoint selected by waypointIndex, or the current active
        /// waypoint if waypointIndex is -1.
        /// </summary>
        /// <param name="waypointIndex">The waypoint index, or -1 to select the current active waypoint.</param>
        /// <returns>Latitude of the selected waypoint, or 0.</returns>
        public double WaypointLatitude(double waypointIndex)
        {
            int index = (int)waypointIndex;

            var waypoints = FinePrint.WaypointManager.Instance().Waypoints;
            if (index >= 0 && index < waypoints.Count)
            {
                return waypoints[index].latitude;
            }
            else if (index == -1 && NavWaypoint.fetch.IsActive)
            {
                return NavWaypoint.fetch.Latitude;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Get the longitude of the waypoint selected by waypointIndex, or the current active
        /// waypoint if waypointIndex is -1.
        /// </summary>
        /// <param name="waypointIndex">The waypoint index, or -1 to select the current active waypoint.</param>
        /// <returns>Longitude of the selected waypoint, or 0.</returns>
        public double WaypointLongitude(double waypointIndex)
        {
            int index = (int)waypointIndex;

            var waypoints = FinePrint.WaypointManager.Instance().Waypoints;
            if (index >= 0 && index < waypoints.Count)
            {
                return waypoints[index].longitude;
            }
            else if (index == -1 && NavWaypoint.fetch.IsActive)
            {
                return NavWaypoint.fetch.Longitude;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the name of waypoint identified by waypointIndex.
        /// 
        /// If waypointIndex is -1, returns the name of the active waypoint.
        /// If waypointIndex is between 0 and `nav.GetWaypointCount()`, returns that
        /// waypoint's name.  Otherwise, returns an empty string.
        /// </summary>
        /// <param name="waypointIndex">The waypoint to name, or -1 to select the current active waypoint.</param>
        /// <returns>The name of the waypoint, or an empty string.</returns>
        public string WaypointName(double waypointIndex)
        {
            int index = (int)waypointIndex;
            if (index == -1)
            {
                index = activeWaypoint;
            }

            var waypoints = FinePrint.WaypointManager.Instance().Waypoints;
            if (index >= 0 && index < waypoints.Count)
            {
                return waypoints[index].name;
            }
            else
            {
                return string.Empty;
            }
        }

        #endregion
    }
}
