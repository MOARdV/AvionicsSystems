/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2018 MOARdV
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
        private const string siPrefixes = " kMGTPEZY";

        [MoonSharpHidden]
        private string DoSIFormatLessThan1(double value, int length, int minDecimal, string delimiter, bool forceSign, bool showPrefix)
        {
            // '#.'
            int nonDecimalChars = 2;
            // Sign
            if (forceSign || value < 0.0)
            {
                ++nonDecimalChars;
            }
            // add the blank prefix char
            if (showPrefix)
            {
                ++nonDecimalChars;
                --length;
            }

            var result = StringBuilderCache.Acquire();
            result.AppendFormat("{{0,{0}:0", length);
            int netDecimal = Math.Max(minDecimal, length - nonDecimalChars);
            if (netDecimal > 0)
            {
                result.Append('.').Append('0', netDecimal);
            }
            result.Append("}");
            if (showPrefix)
            {
                result.Append(" ");
            }
            return string.Format(result.ToStringAndRelease(), value);
        }

        [MoonSharpHidden]
        private string DoSIFormat(double value, int length, int minDecimal, string delimiter, bool forceSign, bool showPrefix)
        {
            if (double.IsInfinity(value) || double.IsNaN(value))
            {
                // Force illegal values to 0.
                value = 0.0;
            }

            //Utility.LogMessage(this, "DoSIFormat {0}, {1}, {2}, x, {3}, {4}", value, length, minDecimal, forceSign, showPrefix);
            if (Math.Abs(value) < 1.0)
            {
                // special case: abs(value) < 1
                return DoSIFormatLessThan1(value, length, minDecimal, delimiter, forceSign, showPrefix);
            }
            int leadingDigitExponent;
            leadingDigitExponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));

            // How many characters need to be set aside?
            int reservedCharacters = (minDecimal > 0) ? (1 + minDecimal) : 0;
            if (showPrefix)
            {
                ++reservedCharacters;
            }
            if (value < 0.0 || forceSign)
            {
                ++reservedCharacters;
            }

            // How much space is there to work with?
            int freeCharacters = length - reservedCharacters;

            // Nearest SI exponent to the value.
            int siExponent = Math.Min((int)Math.Floor(leadingDigitExponent / 3.0), siPrefixes.Length - 1) * 3;

            int digits = leadingDigitExponent - siExponent;
            double scaledInputValue = Math.Round(value / Math.Pow(10.0, siExponent), minDecimal);
            int scaledLength = (Math.Abs(scaledInputValue) > 0.0) ? (int)Math.Floor(Math.Log10(Math.Abs(scaledInputValue))) + 1 : 1;
            int groupLen = (string.IsNullOrEmpty(delimiter)) ? 3 : 4;

            int freeCh2 = freeCharacters - ((scaledLength == 4) ? groupLen + 1 : scaledLength);
            //Utility.LogMessage(this, "freeCh2 => {0}, si = {1}", freeCh2, siExponent);
            while (freeCh2 >= groupLen && siExponent >= 3)
            {
                freeCh2 -= groupLen;
                siExponent -= 3;
                //Utility.LogMessage(this, "freeCh2 => {0}, si = {1}", freeCh2, siExponent);
            }
            if (minDecimal == 0 && freeCh2 > 0)
            {
                // Reserve a space for the decimal
                --freeCh2;
            }
            minDecimal += freeCh2;
            scaledInputValue = Math.Round(value / Math.Pow(10.0, siExponent), minDecimal);

            // TODO: Make a cache of format strings based on length, minDecimal (as adjusted at this point), and showPrefix.

            //Utility.LogMessage(this, "leadExp = {1}, siExp = {2}, reserve = {0}, freeCh = {3}, scaledIn = {6}, scaledLen = {7}, freeCh2 = {5}",
            //    reservedCharacters,
            //    leadingDigitExponent,
            //    siExponent,
            //    freeCharacters,
            //    digits,
            //    freeCh2,
            //    scaledInputValue,
            //    scaledLength);
            var result = StringBuilderCache.Acquire();
            result.AppendFormat("{{0,{0}:", length);
            if (groupLen == 4)
            {
                result.Append("#,#");
            }
            result.Append("0");
            if (minDecimal > 0)
            {
                result.Append('.').Append('0', minDecimal);
            }
            result.Append("}");
            if (showPrefix)
            {
                result.Append("{1}");
            }

            if (siExponent < 0 || siExponent >= siPrefixes.Length)
            {
                Utility.LogWarning(this, "Formatting the value {0}, got an exponent of {1} (out of bounds).  Jamming the value to 0 to avoid an exception.",
                    value, siExponent);
                scaledInputValue = 0.0;
                siExponent = 0;
            }
            //Utility.LogMessage(this, "\"{0}\" -> \"{1}\"", result.ToString(), string.Format(result.ToString(), scaledInputValue, 'Q'));
            return string.Format(result.ToStringAndRelease(), scaledInputValue, siPrefixes[siExponent / 3]);
        }

        /// <summary>
        /// Methods for querying and controlling maneuver nodes are in this category.
        /// </summary>
        #region Maneuver Node

        /// <summary>
        /// Replace any scheduled maneuver nodes with this maneuver node.
        /// </summary>
        /// <param name="progradedV">ΔV in the prograde direction at the time of the maneuver, in m/s.</param>
        /// <param name="normaldV">ΔV in the normal direction at the time of the maneuver, in m/s.</param>
        /// <param name="radialdV">ΔV in the radial direction at the time of the maneuver, in m/s.</param>
        /// <param name="timeUT">UT to schedule the maneuver, in seconds.</param>
        /// <returns>1 if the manuever node was created, 0 on any errors.</returns>
        public double AddManeuverNode(double progradedV, double normaldV, double radialdV, double timeUT)
        {
            if (vessel.patchedConicSolver != null)
            {
                if (double.IsNaN(progradedV) || double.IsInfinity(progradedV) ||
                    double.IsNaN(normaldV) || double.IsInfinity(normaldV) ||
                    double.IsNaN(radialdV) || double.IsInfinity(radialdV) ||
                    double.IsNaN(timeUT) || double.IsInfinity(timeUT))
                {
                    // bad parameters?
                    return 0.0;
                }

                Vector3d dV = new Vector3d(radialdV, normaldV, progradedV);

                // No living in the past.
                timeUT = Math.Max(timeUT, vc.universalTime);

                vessel.patchedConicSolver.maneuverNodes.Clear();
                ManeuverNode mn = vessel.patchedConicSolver.AddManeuverNode(timeUT);
                mn.OnGizmoUpdated(dV, timeUT);

                return 1.0;
            }

            return 0.0;
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
        /// Returns an estimate of the maneuver node burn time, in seconds.
        /// </summary>
        /// <returns>Approximate burn time in seconds, or 0 if no node is scheduled.</returns>
        public double ManeuverNodeBurnTime()
        {
            return vc.NodeBurnTime();
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
                return vc.maneuverNodeComponent.y;
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
                return vc.maneuverNodeComponent.z;
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
        /// Returns the index to the body that the vessel will encounter after a change in SoI caused by
        /// a scheduled maneuver node.  If there is no maneuver node, or the vessel does not change SoI
        /// because of the maneuver, returns -1 (current body).
        /// </summary>
        /// <returns></returns>
        public double ManeuverNodeNextBody()
        {
            if (vc.maneuverNodeValid && vesselSituationConverted > 2)
            {
                if (vc.nodeOrbit.patchEndTransition == Orbit.PatchTransitionType.ENCOUNTER || vc.nodeOrbit.patchEndTransition == Orbit.PatchTransitionType.ESCAPE)
                {
                    Orbit o = vc.nodeOrbit.nextPatch;
                    if (o != null)
                    {
                        int bodiesCount = FlightGlobals.Bodies.Count;
                        for (int i = 0; i < bodiesCount; ++i)
                        {
                            if (FlightGlobals.Bodies[i].name == o.referenceBody.name)
                            {
                                return i;
                            }
                        }
                    }
                }
            }

            return -1.0;
        }

        /// <summary>
        /// Returns 1 if the SoI change after a scheduled maneuver is an 'encounter', -1 if it is an
        /// 'escape', and 0 if the scheduled orbit does not change SoI.
        /// </summary>
        /// <returns>0 if the orbit does not transition.  1 if the vessel will encounter a body, -1 if the vessel will escape the current body.</returns>
        public double ManeuverNodeNextSoI()
        {
            if (vc.maneuverNodeValid && vesselSituationConverted > 2)
            {
                if (vc.nodeOrbit.patchEndTransition == Orbit.PatchTransitionType.ENCOUNTER)
                {
                    return 1.0;
                }
                else if (vc.nodeOrbit.patchEndTransition == Orbit.PatchTransitionType.ESCAPE)
                {
                    return -1.0;
                }
            }

            return 0.0;
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
        /// Closest approach to the target after the next maneuver completes, in meters.  If there
        /// is no maneuver scheduled, or no target, returns 0.
        /// </summary>
        /// <returns>Closest approach to the target, or 0.</returns>
        public double ManeuverNodeTargetClosestApproachDistance()
        {
            if (vc.maneuverNodeValid && vc.targetType > 0)
            {
                if (!nodeApproachSolver.resultsReady)
                {
                    if (vc.targetType == MASVesselComputer.TargetType.CelestialBody)
                    {
                        nodeApproachSolver.SolveBodyIntercept(vc.nodeOrbit, vc.activeTarget as CelestialBody);
                    }
                    else
                    {
                        nodeApproachSolver.SolveOrbitIntercept(vc.nodeOrbit, vc.targetOrbit);
                    }
                }

                if (vc.targetType == MASVesselComputer.TargetType.CelestialBody)
                {
                    return Math.Max(0.0, nodeApproachSolver.targetClosestDistance - (vc.activeTarget as CelestialBody).Radius);
                }
                else
                {
                    return nodeApproachSolver.targetClosestDistance;
                }
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Relative speed of the target at closest approach after the scheduled maneuver, in m/s.  If there
        /// is no maneuver scheduled, or no target, returns 0.
        /// </summary>
        /// <returns>Relative speed of the target at closest approach after the maneuver, m/s.</returns>
        public double ManeuverNodeTargetClosestApproachSpeed()
        {
            if (vc.maneuverNodeValid && vc.targetType > 0)
            {
                if (!nodeApproachSolver.resultsReady)
                {
                    if (vc.targetType == MASVesselComputer.TargetType.CelestialBody)
                    {
                        nodeApproachSolver.SolveBodyIntercept(vc.nodeOrbit, vc.activeTarget as CelestialBody);
                    }
                    else
                    {
                        nodeApproachSolver.SolveOrbitIntercept(vc.nodeOrbit, vc.targetOrbit);
                    }
                }
                return nodeApproachSolver.targetClosestSpeed;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Time when the closest approach with a target occurs after the scheduled maneuver, in seconds.  If there
        /// is no maneuver scheduled, or no target, returns 0.
        /// </summary>
        /// <returns>Time until closest approach after the maneuver.</returns>
        public double ManeuverNodeTargetClosestApproachTime()
        {
            if (vc.maneuverNodeValid && vc.targetType > 0)
            {
                if (!nodeApproachSolver.resultsReady)
                {
                    if (vc.targetType == MASVesselComputer.TargetType.CelestialBody)
                    {
                        nodeApproachSolver.SolveBodyIntercept(vc.nodeOrbit, vc.activeTarget as CelestialBody);
                    }
                    else
                    {
                        nodeApproachSolver.SolveOrbitIntercept(vc.nodeOrbit, vc.targetOrbit);
                    }
                }
                return Math.Max(nodeApproachSolver.targetClosestUT - Planetarium.GetUniversalTime(), 0.0);
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

        /// <summary>
        /// Returns the time to the next SoI transition in the scheduled maneuver, in seconds.  If the planned orbit does not change
        /// SoI, or no node is scheduled, returns 0.
        /// </summary>
        /// <returns>Time until the next SoI after the scheduled maneuver, or 0</returns>
        public double ManeuverNodeTimeToNextSoI()
        {
            if (vc.maneuverNodeValid &&
                vc.nodeOrbit.patchEndTransition == Orbit.PatchTransitionType.ENCOUNTER ||
                vc.nodeOrbit.patchEndTransition == Orbit.PatchTransitionType.ESCAPE)
            {
                return vc.nodeOrbit.UTsoi - Planetarium.GetUniversalTime();
            }
            return 0.0;
        }
        #endregion

        /// <summary>
        /// Vessel mass may be queried with these methods.
        /// </summary>
        #region Mass
        /// <summary>
        /// Returns the mass of the vessel in tonnes.
        /// </summary>
        /// <param name="wetMass">wet mass if true, dry mass otherwise</param>
        /// <returns>Vessel mass in tonnes.</returns>
        public double Mass(bool wetMass)
        {
            if (wetMass)
            {
                return vessel.totalMass;
            }
            else
            {
                return vessel.totalMass - vc.totalResourceMass;
            }
        }
        #endregion

        /// <summary>
        /// Provides MAS-native methods for common math primitives.  These methods generally
        /// duplicate the functions in the Lua math table, but by placing them in MAS, MAS
        /// can use native delegates instead of having to call into Lua (which is slower).
        /// 
        /// This region also contains other useful mathematical methods.
        /// </summary>
        #region Math

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the absolute value of `value`.
        /// </summary>
        /// <returns>The absolute value of `value`.</returns>
        public double Abs(double value)
        {
            return Math.Abs(value);
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns 1 if `value` is at least equal to `lowerBound` and not greater
        /// than `upperBound`.  Returns 0 otherwise.
        /// 
        /// In other words,
        /// * If `value` &gt;= `lowerBound` and `value` &lt;= `upperBound`, return 1.
        /// * Otherwise, reutrn 0.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <param name="lowerBound">The lower bound (inclusive) of the range to test.</param>
        /// <param name="upperBound">The upper bound (inclusive) of the range to test.</param>
        /// <returns>1 if `value` is between `lowerBound` and `upperBound`, 0 otherwise.</returns>
        public double Between(double value, double lowerBound, double upperBound)
        {
            return (value >= lowerBound && value <= upperBound) ? 1.0 : 0.0;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Rounds a number up to the next integer.
        /// </summary>
        /// <param name="value">The value to round</param>
        /// <returns></returns>
        public double Ceiling(double value)
        {
            return Math.Ceiling(value);
        }

        [MASProxy(Dependent = true)]
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

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Rounds a number down to the next integer.
        /// </summary>
        /// <param name="value">The value to round</param>
        /// <returns></returns>
        public double Floor(double value)
        {
            return Math.Floor(value);
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Provides an "inverse lerp" of value, returning a value between 0 and 1 if
        /// the value is between `range1` and range2`.  If `range1` &lt; `range2`,
        /// it returns 0 if value &lt; range1, and 1 if value &gt; range2.  If range1 &gt;
        /// range2, the out-of-range values are reversed.  If range1 == range2, this
        /// function returns 0.
        /// </summary>
        /// <param name="value">The value to evaluate.</param>
        /// <param name="range1">The first bound of the range.</param>
        /// <param name="range2">The second bound of the range.</param>
        /// <returns>A value in the range [0, 1] as described in the summary.</returns>
        public double InverseLerp(double value, double range1, double range2)
        {
            if (range1 == range2)
            {
                return 0.0;
            }
            else if (range1 > range2)
            {
                value = -value;
                range1 = -range1;
                range2 = -range2;
            }

            if (value <= range1)
            {
                return 0.0;
            }
            else if (value >= range2)
            {
                return 1.0;
            }

            return (value - range1) / (range2 - range1);
        }

        [MASProxy(Dependent = true)]
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

        [MASProxy(Dependent = true)]
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

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Normalizes an angle to the range [0, 360).
        /// </summary>
        /// <param name="angle">The de-normalized angle to correct.</param>
        /// <returns>A value between 0 (inclusive) and 360 (exclusive).</returns>
        public double NormalizeAngle(double angle)
        {
            return Utility.NormalizeAngle(angle);
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Normalizes an angle to the range [-180, 180).
        /// </summary>
        /// <param name="angle">The de-normalized angle to correct.</param>
        /// <returns>A value between -180 (inclusive) and +180 (exclusive).</returns>
        public double NormalizeLongitude(double angle)
        {
            return Utility.NormalizeLongitude(angle);
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Converts an angle to a pitch value in the range of [-90, +90].
        /// </summary>
        /// <param name="angle">The angle to convert</param>
        /// <returns>A value between -90 and +90, inclusive.</returns>
        public double NormalizePitch(double angle)
        {
            double pitch = Utility.NormalizeLongitude(angle);
            if (pitch > 90.0)
            {
                return 180.0 - pitch;
            }
            else if (pitch < -90.0)
            {
                return -180.0 - pitch;
            }
            else
            {
                return pitch;
            }
        }

        [MASProxy(Dependent = true)]
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

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Round the given value towards zero (round down for positive values,
        /// round up for negative values).
        /// </summary>
        /// <param name="sourceValue"></param>
        /// <returns></returns>
        public double RoundZero(double sourceValue)
        {
            if (sourceValue < 0.0)
            {
                return Math.Ceiling(sourceValue);
            }
            else
            {
                return Math.Floor(sourceValue);
            }
        }

        [MASProxy(Dependent = true)]
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

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the remainder of `numerator` divided by `denominator`.  If the denominator is zero, this method
        /// returns 0 instead of infinity or throwing a divide-by-zero exception.
        /// </summary>
        /// <param name="numerator">The numerator</param>
        /// <param name="denominator">The denominator</param>
        /// <returns>A value between 0 and `denominator`, or 0 if the denominator is zero.</returns>
        public double SafeModulo(double numerator, double denominator)
        {
            if (Math.Abs(denominator) > 0.0)
            {
                return numerator % denominator;
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
        /// Cancel time warp.
        /// </summary>
        /// <param name="instantCancel">If true, time warp is immediately set to x1.  If false, time warp counts downward like in normal gameplay.</param>
        /// <returns>1 if time warp was successfully adjusted, 0 if it could not be adjusted.</returns>
        public double CancelTimeWarp(bool instantCancel)
        {
            if (TimeWarp.fetch != null)
            {
                TimeWarp.fetch.CancelAutoWarp();
                TimeWarp.SetRate(0, instantCancel);

                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Converts the supplied RGB value into a MAS text color tag
        /// (eg, `fc.HexColor(255, 255, 0)` returns "[#ffff00]").
        /// This value is slightly more efficient if you do not need to
        /// change the alpha channel.
        /// 
        /// All values are clamped to the range 0 to 255.
        /// </summary>
        /// <param name="red">Red channel value, [0, 255]</param>
        /// <param name="green">Green channel value, [0, 255]</param>
        /// <param name="blue">Blue channel value, [0, 255]</param>
        /// <returns></returns>
        public string ColorTag(double red, double green, double blue)
        {
            int r = Mathf.Clamp((int)(red + 0.5), 0, 255);
            int g = Mathf.Clamp((int)(green + 0.5), 0, 255);
            int b = Mathf.Clamp((int)(blue + 0.5), 0, 255);
            return string.Format("[#{0:X2}{1:X2}{2:X2}]", r, g, b);
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Converts the supplied RGBA value into a MAS text color tag
        /// (eg, `fc.HexColor(255, 255, 0, 255)` returns "[#ffff00ff]").
        /// 
        /// All values are clamped to the range 0 to 255.
        /// </summary>
        /// <param name="red">Red channel value, [0, 255]</param>
        /// <param name="green">Green channel value, [0, 255]</param>
        /// <param name="blue">Blue channel value, [0, 255]</param>
        /// <param name="alpha">Alpha channel value, [0, 255]</param>
        /// <returns></returns>
        public string ColorTag(double red, double green, double blue, double alpha)
        {
            int r = Mathf.Clamp((int)(red + 0.5), 0, 255);
            int g = Mathf.Clamp((int)(green + 0.5), 0, 255);
            int b = Mathf.Clamp((int)(blue + 0.5), 0, 255);
            int a = Mathf.Clamp((int)(alpha + 0.5), 0, 255);
            return string.Format("[#{0:X2}{1:X2}{2:X2}{3:X2}]", r, g, b, a);
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
        /// 3) The optional variable `powerOnVariable` in MASFlightComputer returns
        /// 0 or less.
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
        public double Conditioned(double value)
        {
            if (fc.isPowered && UnityEngine.Random.value > fc.disruptionChance)
            {
                return value;
            }
            else
            {
                return 0.0;
            }
        }
        public double Conditioned(bool value)
        {
            if (value && fc.isPowered && UnityEngine.Random.value > fc.disruptionChance)
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
        /// Provides a custom SI formatter with more control than the basic SIP format.
        /// 
        /// **NOT IMPLEMENTED:** Custom delimiter.
        /// </summary>
        /// <param name="value">The number to format.</param>
        /// <param name="totalLength">The total length of the string.</param>
        /// <param name="minDecimal">The minimum decimal precision of the string.</param>
        /// <param name="delimiter">A custom delimiter, or an empty string "" to indicate no delimiter</param>
        /// <param name="forceSign">Require a space for the sign, even if the value is positive</param>
        /// <param name="showPrefix">Whether the SI prefix should be appended to the string.</param>
        /// <returns>The formatted string.</returns>
        public string SIFormatValue(double value, double totalLength, double minDecimal, string delimiter, bool forceSign, bool showPrefix)
        {
            return DoSIFormat(value, (int)totalLength, (int)minDecimal, delimiter, forceSign, showPrefix);
        }

        /// <summary>
        /// Returns the number of hours per day on Kerbin.  With a stock installation, this is 6 hours or
        /// 24 hours, depending on whether the Kerbin calendar or Earth calendar is selected.  Mods
        /// may change this to a different value.
        /// </summary>
        /// <returns>6 for Kerbin time, 24 for Earth time, or another value for modded installations.</returns>
        public double HoursPerDay()
        {
            return KSPUtil.dateTimeFormatter.Day / 3600;
        }

        /// <summary>
        /// Returns 1 if the KSP UI is configured for the Kerbin calendar (6 hour days);
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
        public double LogMessage(string message)
        {
            Utility.LogMessage(this, message);
            return 1.0;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the U texture shift required to display the map icon listed below.
        /// This function is intended to be used in conjunction with the '%MAP_ICON%' texture in
        /// MASMonitor IMAGE nodes.
        /// 
        /// * 0 - Invalid (not one of the below types)
        /// * 1 - Ship target
        /// * 2 - Plane target
        /// * 3 - Probe target
        /// * 4 - Lander target
        /// * 5 - Station target
        /// * 6 - Relay target
        /// * 7 - Rover target
        /// * 8 - Base target
        /// * 9 - EVA target
        /// * 10 - Flag target
        /// * 11 - Debris target
        /// * 12 - Space Object target
        /// * 13 - Unknown target
        /// * 14 - Celestial Body (planet)
        /// * 15 - Ap Icon
        /// * 16 - Pe Icon
        /// * 17 - AN Icon
        /// * 18 - DN Icon
        /// * 19 - Maneuver Node Icon
        /// * 20 - Ship location at intercept
        /// * 21 - Target location at intercept
        /// * 22 - Enter an SoI
        /// * 23 - Exit an SoI
        /// * 24 - Point of Impact (?)
        /// 
        /// Note that entries 0 - 14 correspond to the results of `fc.TargetTypeId()`.
        /// </summary>
        /// <param name="iconId">The Icon Id - either `fc.TargetTypeId()` or one of the numbers listed in the description.</param>
        /// <returns>The U shift to select the icon from the '%MAP_ICON%' texture.</returns>
        public double MapIconU(double iconId)
        {
            int index = (int)iconId;
            switch (index)
            {
                case 0:
                    return 0.6; // WRONG - using Unknown Target
                case 1:
                    return 0.0;
                case 2:
                    return 0.8;
                case 3:
                    return 0.2;
                case 4:
                    return 0.6;
                case 5:
                    return 0.6;
                case 6:
                    return 0.8;
                case 7:
                    return 0.0;
                case 8:
                    return 0.4;
                case 9:
                    return 0.4;
                case 10:
                    return 0.8;
                case 11:
                    return 0.2;
                case 12:
                    return 0.8;
                case 13:
                    return 0.6;
                case 14:
                    return 0.4;
                case 15:
                    return 0.2;
                case 16:
                    return 0.0;
                case 17:
                    return 0.4;
                case 18:
                    return 0.6;
                case 19:
                    return 0.4;
                case 20:
                    return 0.0;
                case 21:
                    return 0.2;
                case 22:
                    return 0.0;
                case 23:
                    return 0.2;
                case 24:
                    return 0.8;
            }

            return 0.0;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the V texture shift required to display the map icon listed below.
        /// This function is intended to be used in conjunction with the '%MAP_ICON%' texture in
        /// MASMonitor IMAGE nodes.
        /// 
        /// * 0 - Invalid (not one of the below types)
        /// * 1 - Ship target
        /// * 2 - Plane target
        /// * 3 - Probe target
        /// * 4 - Lander target
        /// * 5 - Station target
        /// * 6 - Relay target
        /// * 7 - Rover target
        /// * 8 - Base target
        /// * 9 - EVA target
        /// * 10 - Flag target
        /// * 11 - Debris target
        /// * 12 - Space Object target
        /// * 13 - Unknown target
        /// * 14 - Celestial Body (planet)
        /// * 15 - Ap Icon
        /// * 16 - Pe Icon
        /// * 17 - AN Icon
        /// * 18 - DN Icon
        /// * 19 - Maneuver Node Icon
        /// * 20 - Ship location at intercept
        /// * 21 - Target location at intercept
        /// * 22 - Enter an SoI
        /// * 23 - Exit an SoI
        /// * 24 - Point of Impact (?)
        /// 
        /// Note that entries 0 - 14 correspond to the results of `fc.TargetTypeId()`.
        /// </summary>
        /// <param name="iconId">The Icon Id - either `fc.TargetTypeId()` or one of the numbers listed in the description.</param>
        /// <returns>The V shift to select the icon from the '%MAP_ICON%' texture.</returns>
        public double MapIconV(double iconId)
        {
            int index = (int)iconId;
            switch (index)
            {
                case 0:
                    return 0.6; // WRONG - Using Unknown Target
                case 1:
                    return 0.6;
                case 2:
                    return 0.8;
                case 3:
                    return 0.0;
                case 4:
                    return 0.0;
                case 5:
                    return 0.2;
                case 6:
                    return 0.6;
                case 7:
                    return 0.0;
                case 8:
                    return 0.0;
                case 9:
                    return 0.4;
                case 10:
                    return 0.0;
                case 11:
                    return 0.6;
                case 12:
                    return 0.2;
                case 13:
                    return 0.6;
                case 14:
                    return 0.6;
                case 15:
                    return 0.8;
                case 16:
                    return 0.8;
                case 17:
                    return 0.8;
                case 18:
                    return 0.8;
                case 19:
                    return 0.2;
                case 20:
                    return 0.2;
                case 21:
                    return 0.2;
                case 22:
                    return 0.4;
                case 23:
                    return 0.4;
                case 24:
                    return 0.4;
            }

            return 0.0;
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

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the B color value for the navball marker icon selected.
        /// This function is intended to be used in conjunction with the '%NAVBALL_ICON%' texture in
        /// MASMonitor IMAGE nodes.
        /// 
        /// * 0 - Prograde
        /// * 1 - Retrograde
        /// * 2 - Radial Out
        /// * 3 - Radial In
        /// * 4 - Normal +
        /// * 5 - Normal - (anti-normal)
        /// * 6 - Maneuver Node
        /// * 7 - Target +
        /// * 8 - Target - (anti-target)
        /// 
        /// If an invalid number is supplied, this function treats is as "Prograde".
        /// </summary>
        /// <param name="iconId">The icon as explained in the description.</param>
        /// <returns>The B value to use in `passiveColor` or `activeColor`.</returns>
        public double NavballB(double iconId)
        {
            int index = (int)iconId;
            switch (index)
            {
                case 1:
                    return 0.0;
                case 2:
                    return 255.0;
                case 3:
                    return 255.0;
                case 4:
                    return 206.0;
                case 5:
                    return 206.0;
                case 6:
                    return 249.0;
                case 7:
                    return 255.0;
                case 8:
                    return 255.0;
            }
            return 0.0;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the G color value for the navball marker icon selected.
        /// This function is intended to be used in conjunction with the '%NAVBALL_ICON%' texture in
        /// MASMonitor IMAGE nodes.
        /// 
        /// * 0 - Prograde
        /// * 1 - Retrograde
        /// * 2 - Radial Out
        /// * 3 - Radial In
        /// * 4 - Normal +
        /// * 5 - Normal - (anti-normal)
        /// * 6 - Maneuver Node
        /// * 7 - Target +
        /// * 8 - Target - (anti-target)
        /// 
        /// If an invalid number is supplied, this function treats is as "Prograde".
        /// </summary>
        /// <param name="iconId">The icon as explained in the description.</param>
        /// <returns>The G value to use in `passiveColor` or `activeColor`.</returns>
        public double NavballG(double iconId)
        {
            int index = (int)iconId;
            switch (index)
            {
                case 1:
                    return 203.0;
                case 2:
                    return 155.0;
                case 3:
                    return 155.0;
                case 4:
                    return 0.0;
                case 5:
                    return 0.0;
                case 6:
                    return 102.0;
                case 7:
                    return 0.0;
                case 8:
                    return 0.0;
            }
            return 203.0;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the R color value for the navball marker icon selected.
        /// This function is intended to be used in conjunction with the '%NAVBALL_ICON%' texture in
        /// MASMonitor IMAGE nodes.
        /// 
        /// * 0 - Prograde
        /// * 1 - Retrograde
        /// * 2 - Radial Out
        /// * 3 - Radial In
        /// * 4 - Normal +
        /// * 5 - Normal - (anti-normal)
        /// * 6 - Maneuver Node
        /// * 7 - Target +
        /// * 8 - Target - (anti-target)
        /// 
        /// If an invalid number is supplied, this function treats is as "Prograde".
        /// </summary>
        /// <param name="iconId">The icon as explained in the description.</param>
        /// <returns>The R value to use in `passiveColor` or `activeColor`.</returns>
        public double NavballR(double iconId)
        {
            int index = (int)iconId;
            switch (index)
            {
                case 1:
                    return 255.0;
                case 2:
                    return 0.0;
                case 3:
                    return 0.0;
                case 4:
                    return 156.0;
                case 5:
                    return 156.0;
                case 6:
                    return 0.0;
                case 7:
                    return 255.0;
                case 8:
                    return 255.0;
            }
            return 255.0;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the U texture shift to select the navball marker icon as listed below.
        /// This function is intended to be used in conjunction with the '%NAVBALL_ICON%' texture in
        /// MASMonitor IMAGE nodes.
        /// 
        /// * 0 - Prograde
        /// * 1 - Retrograde
        /// * 2 - Radial Out
        /// * 3 - Radial In
        /// * 4 - Normal +
        /// * 5 - Normal - (anti-normal)
        /// * 6 - Maneuver Node
        /// * 7 - Target +
        /// * 8 - Target - (anti-target)
        /// 
        /// If an invalid number is supplied, this function treats is as "Prograde".
        /// </summary>
        /// <param name="iconId">The icon as explained in the description.</param>
        /// <returns>The U value to use in `uvShift`.</returns>
        public double NavballU(double iconId)
        {
            int index = (int)iconId;
            switch (index)
            {
                case 1:
                    return (1.0 / 3.0);
                case 2:
                    return (1.0 / 3.0);
                case 3:
                    return 0.0;
                case 4:
                    return 0.0;
                case 5:
                    return (1.0 / 3.0);
                case 6:
                    return (2.0 / 3.0);
                case 7:
                    return (2.0 / 3.0);
                case 8:
                    return (2.0 / 3.0);
            }
            return 0.0;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the V texture shift to select the navball marker icon as listed below.
        /// This function is intended to be used in conjunction with the '%NAVBALL_ICON%' texture in
        /// MASMonitor IMAGE nodes.
        /// 
        /// * 0 - Prograde
        /// * 1 - Retrograde
        /// * 2 - Radial Out
        /// * 3 - Radial In
        /// * 4 - Normal +
        /// * 5 - Normal - (anti-normal)
        /// * 6 - Maneuver Node
        /// * 7 - Target +
        /// * 8 - Target - (anti-target)
        /// 
        /// If an invalid number is supplied, this function treats is as "Prograde".
        /// </summary>
        /// <param name="iconId">The icon as explained in the description.</param>
        /// <returns>The V value to use in `uvShift`.</returns>
        public double NavballV(double iconId)
        {
            int index = (int)iconId;
            switch (index)
            {
                case 1:
                    return (2.0 / 3.0);
                case 2:
                    return (1.0 / 3.0);
                case 3:
                    return (1.0 / 3.0);
                case 4:
                    return 0.0;
                case 5:
                    return 0.0;
                case 6:
                    return 0.0;
                case 7:
                    return (2.0 / 3.0);
                case 8:
                    return (1.0 / 3.0);
            }
            return 2.0 / 3.0;
        }

        /// <summary>
        /// Play the audio file specified in `sound`, at the volume specified in `volume`.
        /// If `stopCurrent` is true, any current sound clip is canceled first.  If `stopCurrent`
        /// is false, and an audio clip is currently playing, this call to PlayAudio does nothing.
        /// </summary>
        /// <param name="sound">The name (URI) of the sound to play.</param>
        /// <param name="volume">The volume to use for playback, between 0 and 1 (inclusive).</param>
        /// <param name="stopCurrent">If 'true', stops any current audio clip being played.</param>
        /// <returns>Returns 1 if the audio was played, 0 if it was not found or otherwise not played.</returns>
        public double PlayAudio(string sound, double volume, bool stopCurrent)
        {
            return fc.PlayAudio(sound, Mathf.Clamp01((float)volume), stopCurrent) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Play the sequence of letters as a Morse Code sequence.  Letters play automatically,
        /// and a space ' ' inserts a pause in the sequence.  All other characters are skipped.
        /// </summary>
        /// <param name="sequence">The sequence of letters to play as a Morse Code.</param>
        /// <param name="volume">The volume to use for playback, between 0 and 1 (inclusive).</param>
        /// <param name="stopCurrent">If 'true', stops any current audio clip being played.</param>
        /// <returns></returns>
        public double PlayMorseSequence(string sequence, double volume, bool stopCurrent)
        {
            return fc.PlayMorseSequence(sequence, Mathf.Clamp01((float)volume), stopCurrent);
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
        /// Run the `startupScript` on every monitor and prop in the pod that has a defined `startupScript`.
        /// </summary>
        /// <returns>The number of scripts executed.</returns>
        public double RunMonitorStartupScript()
        {
            double scriptCount = 0.0;
            List<InternalProp> props = fc.part.internalModel.props;
            int numProps = props.Count;
            for (int propIndex = 0; propIndex < numProps; ++propIndex)
            {
                List<InternalModule> modules = props[propIndex].internalModules;
                int numModules = modules.Count;
                for (int moduleIndex = 0; moduleIndex < numModules; ++moduleIndex)
                {
                    if (modules[moduleIndex] is MASMonitor)
                    {
                        if ((modules[moduleIndex] as MASMonitor).RunStartupScript(fc))
                        {
                            scriptCount += 1.0;
                        }
                    }
                    else if (modules[moduleIndex] is MASComponent)
                    {
                        if ((modules[moduleIndex] as MASComponent).RunStartupScript(fc))
                        {
                            scriptCount += 1.0;
                        }
                    }
                }
            }

            return scriptCount;
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

                    StringBuilder sb = StringBuilderCache.Acquire();
                    sb.Append(inputString.Substring(start)).Append(' ').Append(inputString.Substring(0, tail));

                    return sb.ToStringAndRelease();
                }
            }
        }

        /// <summary>
        /// Attempt to set time warp rate to warpRate.  Because KSP has specific
        /// supported rates, the rate selected may not match the rate
        /// requested.  Warp rates are not changed when physics warp is enabled
        /// (such as while flying in the atmosphere).
        /// </summary>
        /// <param name="warpRate">The desired warp rate, such as '50'.</param>
        /// <param name="instantChange">Should the rate be changed instantly?</param>
        /// <returns>The warp rate selected.  This may not be the exact value requested.</returns>
        public double SetWarp(double warpRate, bool instantChange)
        {
            if (TimeWarp.fetch != null)
            {
                if (warpRate <= 1.0)
                {
                    TimeWarp.fetch.CancelAutoWarp();
                    TimeWarp.SetRate(0, instantChange);

                    return 1.0;
                }
                else if (warpRate != TimeWarp.CurrentRate && TimeWarp.WarpMode == TimeWarp.Modes.HIGH)
                {
                    int rateIndex;
                    int maxRate = (vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.PRELAUNCH || vessel.situation == Vessel.Situations.SPLASHED) ? TimeWarp.fetch.warpRates.Length : TimeWarp.fetch.GetMaxRateForAltitude(vessel.altitude, vessel.mainBody);

                    if (maxRate > 0)
                    {

                        for (rateIndex = 0; rateIndex <= maxRate; ++rateIndex)
                        {
                            if (TimeWarp.fetch.warpRates[rateIndex] > warpRate)
                            {
                                break;
                            }
                        }
                        --rateIndex;
                        TimeWarp.SetRate(rateIndex, instantChange);

                        return TimeWarp.fetch.warpRates[rateIndex];
                    }
                }
            }

            return TimeWarp.CurrentRate;
        }

        /// <summary>
        /// Returns 1 if the vessel is recoverable, 0 otherwise.
        /// </summary>
        /// <returns>1 if the craft can be recovered, 0 otherwise.</returns>
        public double VesselRecoverable()
        {
            return (vessel.IsRecoverable) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Warp to the specified universal time.  If the time is in the past, or
        /// the warp is otherwise not possible, nothing happens.
        /// </summary>
        /// <param name="UT">The Universal Time to warp to.  Must be in the future.</param>
        /// <returns>1 if the warp to time is successfully set, 0 if it was not.</returns>
        public double WarpTo(double UT)
        {
            if (TimeWarp.fetch != null && UT > Planetarium.GetUniversalTime())
            {
                TimeWarp.fetch.CancelAutoWarp();
                TimeWarp.fetch.WarpTo(UT);

                return 1.0;
            }
            else
            {
                return 0.0;
            }
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
                return vc.GetRelativePitch(vc.surfacePrograde) * (Math.Min(2.0, vessel.srfSpeed) * 0.5);
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
        /// Return the heading of the surface velocity vector relative to the surface in degrees [0, 360)
        /// </summary>
        /// <returns></returns>
        public double HeadingPrograde()
        {
            return vc.progradeHeading;
        }

        /// <summary>
        /// Return the instantaneous rate of change of the vessel heading in degrees per second.
        /// 
        /// Note that extreme rates may be misreported.
        /// </summary>
        /// <returns>A value in the range of [-180, 180).</returns>
        public double HeadingRate()
        {
            return vc.headingRate;
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
        /// Pitch of the vessel relative to the current SAS prograde mode (orbit, surface, or target).
        /// </summary>
        /// <returns></returns>
        public double PitchActivePrograde()
        {
            var mode = FlightGlobals.speedDisplayMode;

            if (mode == FlightGlobals.SpeedDisplayModes.Orbit)
            {
                return vc.GetRelativePitch(vc.prograde);
            }
            else if (mode == FlightGlobals.SpeedDisplayModes.Target)
            {
                return vc.GetRelativePitch(vc.targetRelativeVelocity.normalized);
            }
            else
            {
                return vc.GetRelativePitch(vc.surfacePrograde);
            }
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
        /// Pitch of the vessel relative to the current active SAS mode.  If SAS
        /// is not enabled, or the current mode is Stability Assist, returns 0.
        /// </summary>
        /// <returns>Pitch in the range [+90, -90]</returns>
        public double PitchSAS()
        {
            double relativePitch = 0.0;
            if (vessel.ActionGroups[KSPActionGroup.SAS] && vessel.Autopilot != null && vessel.Autopilot.SAS != null && autopilotMode != VesselAutopilot.AutopilotMode.StabilityAssist)
            {
                relativePitch = vc.GetRelativePitch(vessel.Autopilot.SAS.targetOrientation);
            }

            return relativePitch;
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
        /// Pitch of the vessel relative to the active waypoint.  0 if no active waypoint.
        /// </summary>
        /// <returns></returns>
        public double PitchWaypoint()
        {
            if (NavWaypoint.fetch.IsActive)
            {
                Vector3d srfPos = vessel.mainBody.GetWorldSurfacePosition(NavWaypoint.fetch.Latitude, NavWaypoint.fetch.Longitude, NavWaypoint.fetch.Altitude);

                return vc.GetRelativePitch(srfPos.normalized);
            }
            return 0.0;
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
                return vc.GetRelativeYaw(vc.surfacePrograde) * (Math.Min(2.0, vessel.srfSpeed) * 0.5);
            }
        }

        /// <summary>
        /// Returns the slope of the terrain directly below the vessel.  If the vessel's altitude
        /// is too high to read the slope, returns 0.
        /// </summary>
        /// <returns>Slope of the terrain below the vessel, or 0 if the slope cannot be read.</returns>
        public double SlopeAngle()
        {
            return vc.GetSlopeAngle();
        }

        /// <summary>
        /// Yaw of the vessel relative to the current SAS prograde mode (orbit, surface, or target).
        /// </summary>
        /// <returns></returns>
        public double YawActivePrograde()
        {
            var mode = FlightGlobals.speedDisplayMode;

            if (mode == FlightGlobals.SpeedDisplayModes.Orbit)
            {
                return vc.GetRelativeYaw(vc.prograde);
            }
            else if (mode == FlightGlobals.SpeedDisplayModes.Target)
            {
                return vc.GetRelativeYaw(vc.targetRelativeVelocity.normalized);
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
        /// Yaw of the vessel relative to the current active SAS mode.  If SAS
        /// is not enabled, or the current mode is Stability Assist, returns 0.
        /// </summary>
        /// <returns>Yaw in the range [+180, -180]</returns>
        public double YawSAS()
        {
            double relativeYaw = 0.0;
            if (vessel.ActionGroups[KSPActionGroup.SAS] && vessel.Autopilot != null && vessel.Autopilot.SAS != null && autopilotMode != VesselAutopilot.AutopilotMode.StabilityAssist)
            {
                relativeYaw = vc.GetRelativeYaw(vessel.Autopilot.SAS.targetOrientation);
            }

            return relativeYaw;
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

        /// <summary>
        /// Yaw of the vessel relative to the active waypoint.  0 if no active waypoint.
        /// </summary>
        /// <returns></returns>
        public double YawWaypoint()
        {
            if (NavWaypoint.fetch.IsActive)
            {
                Vector3d srfPos = vessel.mainBody.GetWorldSurfacePosition(NavWaypoint.fetch.Latitude, NavWaypoint.fetch.Longitude, NavWaypoint.fetch.Altitude);

                return vc.GetRelativeYaw(srfPos.normalized);
            }
            return 0.0;
        }
        #endregion

        /// <summary>
        /// Periodic variables change value over time, based on a requested
        /// frequency.
        /// </summary>
        #region Periodic Variables

        /// <summary>
        /// Returns a periodic variable that counts upwards from 0 to 'countTo'-1 before repeating,
        /// with each change based on the 'period'.  Note that the counter is not guaranteed to start
        /// at zero, since it is based on universal time.
        /// </summary>
        /// <param name="period">The period required to increase the counter, in cycles/second (Hertz).</param>
        /// <param name="countTo">The exclusive upper limit of the count.</param>
        /// <returns>An integer between [0 and countTo).</returns>
        public double PeriodCount(double period, double countTo)
        {
            if (period > 0.0 && countTo >= 2.0)
            {
                double invPeriod = Math.Floor(countTo) / period;

                double remainder = period * (vc.universalTime % invPeriod);

                return Math.Floor(remainder);
            }

            return 0.0;
        }

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

        [MASProxy(Persistent = true)]
        /// <summary>
        /// Return value of the persistent.  Strings are returned as strings,
        /// numbers are returned as numbers.  If the persistent does not exist
        /// yet, the name is returned.
        /// </summary>
        /// <param name="persistentName">The name of the persistent variable to query.</param>
        /// <returns>The value of the persistent, or its name if it does not exist.</returns>
        public object GetPersistent(string persistentName)
        {
            return fc.GetPersistent(persistentName);
        }

        [MASProxy(Persistent = true)]
        /// <summary>
        /// Return the value of the persistent as a number.  If the persistent
        /// does not exist yet, or it is a string that can not be converted to
        /// a number, return 0.
        /// </summary>
        /// <param name="persistentName">The name of the persistent variable to query.</param>
        /// <returns>The numeric value of the persistent, or 0 if it either does not
        /// exist, or it cannot be converted to a number.</returns>
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
        /// Set a persistent to `value`, but allow the persistent to change by at most `maxChangePerSecond`
        /// from its initial value.
        /// 
        /// If the persistent did not already exist, or if it was a string that could not be converted to
        /// a number, it is set to value immediately.
        /// 
        /// For other cases, the persistent's value is updated by adding or subtracting the minimum of
        /// (maxChangePerSecond * timestep) or Abs(old value - value).
        /// 
        /// While this method is a "setter" method, it is best applied where its results are displayed,
        /// such as a number on an MFD, or controlling the animation of a prop.  This will ensure that
        /// the number continually updates, whereas using this method in a collider action will cause it
        /// to only update the value when the collider is hit.
        /// </summary>
        /// <param name="persistentName">The name of the persistent variable to change.</param>
        /// <param name="value">The new value for this variable.</param>
        /// <param name="maxChangePerSecond">The maximum amount the existing variable may change per second.</param>
        /// <returns>The resulting value.</returns>
        public double SetPersistentBlended(string persistentName, double value, double maxChangePerSecond)
        {
            return fc.SetPersistentBlended(persistentName, value, maxChangePerSecond);
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
        /// 
        /// The landing predictions will use Kerbal Engineer Redux if that mod is installed.
        /// If KER is not available, MAS will fall back to using the MechJeb Landing
        /// Predictions module if it is enabled.
        /// Otherwise, MAS uses a very rudimentary predictor that assumes the planet
        /// has no atmosphere.  Because of these
        /// limitations, the results will change during atmospheric braking, and they
        /// may be wildly inaccurate in mountainous terrain.
        /// </summary>
        #region Position
        /// <summary>
        /// Returns the predicted altitude of the landing position.
        /// 
        /// See the [category description](#description) for limitations on this function.
        /// </summary>
        /// <seealso>MechJeb, Kerbal Engineer Redux</seealso>
        /// <returns>Predicted altitude (meters ASL) of the point of landing.</returns>
        public double LandingAltitude()
        {
            if (MASIKerbalEngineer.keFound)
            {
                return keProxy.LandingAltitude();
            }
            else if (mjProxy.LandingComputerActive() > 0.0)
            {
                return mjProxy.LandingAltitude();
            }
            else
            {
                return vc.landingAltitude;
            }
        }

        /// <summary>
        /// Returns the predicted latitude of the landing position.
        /// 
        /// See the [category description](#description) for limitations on this function.
        /// </summary>
        /// <seealso>MechJeb, Kerbal Engineer Redux</seealso>
        /// <returns>Latitude of estimated landing point, or 0 if the orbit does not lithobrake.</returns>
        public double LandingLatitude()
        {
            if (MASIKerbalEngineer.keFound)
            {
                return keProxy.LandingLatitude();
            }
            else if (mjProxy.LandingComputerActive() > 0.0 && mjProxy.LandingLatitude() != 0.0)
            {
                return mjProxy.LandingLatitude();
            }
            else
            {
                return vc.landingLatitude;
            }
        }

        /// <summary>
        /// Returns the predicted longitude of the landing position.
        /// 
        /// See the [category description](#description) for limitations on this function.
        /// </summary>
        /// <seealso>MechJeb, Kerbal Engineer Redux</seealso>
        /// <returns>Longitude of estimated landing point, or 0 if the orbit does not lithobrake.</returns>
        public double LandingLongitude()
        {
            if (MASIKerbalEngineer.keFound)
            {
                return keProxy.LandingLongitude();
            }
            else if (mjProxy.LandingComputerActive() > 0.0 && mjProxy.LandingLongitude() != 0.0)
            {
                return mjProxy.LandingLongitude();
            }
            else
            {
                return vc.landingLongitude;
            }
        }

        /// <summary>
        /// Returns the predicted time until landing in seconds.
        /// 
        /// See the [category description](#description) for limitations on this function.
        /// </summary>
        /// <seealso>MechJeb, Kerbal Engineer Redux</seealso>
        /// <returns>Estimated time until landing, or 0 if the orbit does not lithobrake.</returns>
        public double LandingTime()
        {
            if (MASIKerbalEngineer.keFound)
            {
                return keProxy.LandingTime();
            }
            else if (mjProxy.LandingComputerActive() > 0.0)
            {
                return mjProxy.LandingTime();
            }
            else
            {
                double landingTime = vc.timeToImpact;
                return (landingTime > 0.0) ? (landingTime) : 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if landing predictions are valid.  If Kerbal Engineer Redux is installed,
        /// MAS will use KER's predictions.  Otherwise, if MechJeb is installed, MAS will
        /// use MechJeb's landing computer (if it is active).  If neither of those options
        /// are available, MAS uses its own internal predictor.
        /// </summary>
        /// <seealso>MechJeb, Kerbal Engineer Redux</seealso>
        /// <returns></returns>
        public double LandingPredictorActive()
        {
            if (MASIKerbalEngineer.keFound)
            {
                return keProxy.LandingTime() > 0.0 ? 1.0 : 0.0;
            }
            // Landing predictor sometimes gets weird and insists it's on, but it spews
            // 0N, 0E as the landing point.
            else if (mjProxy.LandingComputerActive() > 0.0 && mjProxy.LandingLongitude() != 0.0)
            {
                return 1.0;
            }
            else
            {
                return (vc.timeToImpact > 0.0) ? 1.0 : 0.0;
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
        /// 
        /// The Fuel Cell methods track any ModuleResourceConverter that outputs
        /// ElectricCharge, unless a different resource converter tracker has been installed
        /// with a higher priority.  For general-purpose ModuleResourceConverter tracking,
        /// refer to Resource Converter category.
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
            return ResourceConverterCount(0.0);
        }

        /// <summary>
        /// Returns the current output of installed fuel cells.
        /// </summary>
        /// <returns>Units of ElectricCharge/second.</returns>
        public double FuelCellOutput()
        {
            return ResourceConverterOutput(0.0);
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
            return GetResourceConverterActive(0.0);
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
        /// Returns the net efficiency of solar panels.  This value can
        /// provide some information about blocked solar panels, or non-rotating solar panels that
        /// are not optimally positioned.  Because the amount of energy delivered depends on the
        /// distance from the star, maximum efficiency can be larger than 1 (or less than 1).
        /// </summary>
        /// <returns>The efficiency value, 0 or larger.</returns>
        public double SolarPanelEfficiency()
        {
            return vc.solarPanelsEfficiency;
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
            return ToggleResourceConverterActive(0.0);
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
        /// This section contains the functions used to interact with the stock procedural
        /// fairings system.
        /// </summary>
        #region Procedural Fairings

        /// <summary>
        /// Deploys all stock procedural fairings that are currently available to
        /// deploy.
        /// </summary>
        /// <returns>1 if any fairings deployed, 0 otherwise.</returns>
        public double DeployFairings()
        {
            bool deployed = false;

            for (int i = vc.moduleProceduralFairing.Length - 1; i >= 0; --i)
            {
                if (vc.moduleProceduralFairing[i].CanMove)
                {
                    vc.moduleProceduralFairing[i].DeployFairing();
                    deployed = true;
                }
            }

            return (deployed) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if at least one installed stock procedural fairing is available to
        /// deploy.
        /// </summary>
        /// <returns>1 if any fairings can deploy, 0 otherwise.</returns>
        public double FairingsCanDeploy()
        {
            return (vc.fairingsCanDeploy) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the number of stock procedural fairings installed on the vessel.
        /// </summary>
        /// <returns>The total number of stock p-fairings on the vessel.</returns>
        public double FairingsCount()
        {
            return vc.moduleProceduralFairing.Length;
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
        /// <returns>1 if any disabled RCS ports were enabled, 0 if there were no disabled ports.</returns>
        public double EnableAllRCS()
        {
            double anyEnabled = 0.0;
            for (int i = vc.moduleRcs.Length - 1; i >= 0; --i)
            {
                if (!vc.moduleRcs[i].rcsEnabled)
                {
                    vc.moduleRcs[i].rcsEnabled = true;
                    anyEnabled = 1.0;
                }
            }

            return anyEnabled;
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
        /// Returns 1 if any RCS thrusters are configured to allow rotation,
        /// 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double GetRCSRotate()
        {
            return (vc.anyRcsRotate) ? 1.0 : 0.0;
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
        /// Returns 1 if any RCS thrusters are configured to allow translation,
        /// 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double GetRCSTranslate()
        {
            return (vc.anyRcsTranslate) ? 1.0 : 0.0;
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
        /// Enable or disable RCS rotation control.
        /// </summary>
        /// <param name="active">Whether RCS should be used for rotation.</param>
        /// <returns>The number of RCS modules updated (0 if none, more than 0 if any RCS are installed).</returns>
        public double SetRCSRotate(bool active)
        {
            for (int i = vc.moduleRcs.Length - 1; i >= 0; --i)
            {
                vc.moduleRcs[i].enableRoll = active;
                vc.moduleRcs[i].enableYaw = active;
                vc.moduleRcs[i].enablePitch = active;
            }

            return vc.moduleRcs.Length;
        }

        /// <summary>
        /// Enable or disable RCS translation control.
        /// </summary>
        /// <param name="active">Whether RCS should be used for translation.</param>
        /// <returns>The number of RCS modules updated (0 if none, more than 0 if any RCS are installed).</returns>
        public double SetRCSTranslate(bool active)
        {
            for (int i = vc.moduleRcs.Length - 1; i >= 0; --i)
            {
                vc.moduleRcs[i].enableX = active;
                vc.moduleRcs[i].enableY = active;
                vc.moduleRcs[i].enableZ = active;
            }

            return vc.moduleRcs.Length;
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

        /// <summary>
        /// Toggle RCS rotation control.
        /// </summary>
        /// <returns>1 if rotation is now on, 0 otherwise.</returns>
        public double ToggleRCSRotate()
        {
            if (vc.anyRcsRotate)
            {
                for (int i = vc.moduleRcs.Length - 1; i >= 0; --i)
                {
                    vc.moduleRcs[i].enableRoll = false;
                    vc.moduleRcs[i].enableYaw = false;
                    vc.moduleRcs[i].enablePitch = false;
                }
                return 0.0;
            }
            else
            {
                for (int i = vc.moduleRcs.Length - 1; i >= 0; --i)
                {
                    vc.moduleRcs[i].enableRoll = true;
                    vc.moduleRcs[i].enableYaw = true;
                    vc.moduleRcs[i].enablePitch = true;
                }
                return 1.0;
            }
        }

        /// <summary>
        /// Toggle RCS translation control.
        /// </summary>
        /// <returns>1 if translation is now on, 0 otherwise.</returns>
        public double ToggleRCSTranslate()
        {
            if (vc.anyRcsTranslate)
            {
                for (int i = vc.moduleRcs.Length - 1; i >= 0; --i)
                {
                    vc.moduleRcs[i].enableX = false;
                    vc.moduleRcs[i].enableY = false;
                    vc.moduleRcs[i].enableZ = false;
                }
                return 0.0;
            }
            else
            {
                for (int i = vc.moduleRcs.Length - 1; i >= 0; --i)
                {
                    vc.moduleRcs[i].enableX = true;
                    vc.moduleRcs[i].enableY = true;
                    vc.moduleRcs[i].enableZ = true;
                }
                return 1.0;
            }
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
        /// Returns 1 if at least one reaction wheel is on the vessel and active.  Returns 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double GetReactionWheelActive()
        {
            return (vc.reactionWheelActive) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the current reaction wheel authority as a percentage of maximum.
        /// </summary>
        /// <returns>Reaction wheel authority in the range of [0, 1].</returns>
        public double GetReactionWheelAuthority()
        {
            return vc.reactionWheelAuthority;
        }

        /// <summary>
        /// Returns 1 if at least one reaction wheel is damaged.  Returns 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double GetReactionWheelDamaged()
        {
            return (vc.reactionWheelDamaged) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the net pitch being applied by reaction wheels as a value
        /// between -1 and +1.  Note that if two wheels are applying pitch in
        /// opposite directions, this value will cancel out and reprot 0.
        /// </summary>
        /// <returns>The net pitch, between -1 and +1.</returns>
        public double ReactionWheelPitch()
        {
            return vc.reactionWheelPitch;
        }

        /// <summary>
        /// Returns the net roll being applied by reaction wheels as a value
        /// between -1 and +1.  Note that if two wheels are applying roll in
        /// opposite directions, this value will cancel out and reprot 0.
        /// </summary>
        /// <returns>The net roll, between -1 and +1.</returns>
        public double ReactionWheelRoll()
        {
            return vc.reactionWheelRoll;
        }

        /// <summary>
        /// Returns the total torque percentage currently being applied via reaction wheels.
        /// </summary>
        /// <returns>A number between 0 (no torque) and 1 (maximum torque).</returns>
        public double ReactionWheelTorque()
        {
            return vc.reactionWheelNetTorque;
        }

        /// <summary>
        /// Returns the net yaw being applied by reaction wheels as a value
        /// between -1 and +1.  Note that if two wheels are applying yaw in
        /// opposite directions, this value will cancel out and reprot 0.
        /// </summary>
        /// <returns>The net yaw, between -1 and +1.</returns>
        public double ReactionWheelYaw()
        {
            return vc.reactionWheelYaw;
        }

        /// <summary>
        /// Update all active reaction wheels' authority.
        /// </summary>
        /// <param name="authority">The new authority percentage, between 0 and 1.  Value is clamped if it is outside that range.</param>
        /// <returns>The new reaction wheel authority, or 0 if no wheels are available.</returns>
        public double SetReactionWheelAuthority(double authority)
        {
            float newAuthority = Mathf.Clamp01((float)authority);
            for (int i = vc.moduleReactionWheel.Length - 1; i >= 0; --i)
            {
                vc.moduleReactionWheel[i].authorityLimiter = newAuthority * 100.0f;
            }

            return (vc.moduleReactionWheel.Length > 0) ? newAuthority : 0.0;
        }

        /// <summary>
        /// Toggle the reaction wheels.
        /// </summary>
        /// <returns>1 if any reaction wheels are installed, otherwise 0.</returns>
        public double ToggleReactionWheel()
        {
            for (int i = vc.moduleReactionWheel.Length - 1; i >= 0; --i)
            {
                vc.moduleReactionWheel[i].OnToggle();
            }

            return (vc.moduleReactionWheel.Length > 0) ? 1.0 : 0.0;
        }
        #endregion
    }
}
