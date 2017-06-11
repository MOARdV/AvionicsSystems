//#define COMPARE_PHASE_ANGLE_PROTRACTOR
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
    /// <summary>
    /// The MASITransfer class contains functionality equivalent to the KSP
    /// mod Protractor.  However, the code here was written from scratch, since
    /// Protractor is GPL.
    /// </summary>
    /// <LuaName>transfer</LuaName>
    /// <mdDoc>The MASITransfer module does calculations to find phase angles
    /// and ejection angles for Hohmann transfer orbits.  It provides functionality
    /// equivalent to the Protractor mod, but focused strictly on computations
    /// involving the current vessel and a target (either another vessel or a
    /// Celestial Body).
    /// 
    /// Note that MASITransfer assumes the target has a small relative inclination.
    /// It will generate erroneous results for high inclination and retrograde relative
    /// orbits.
    /// </mdDoc>
    internal class MASITransfer
    {
        private bool invalid = true;

        internal Vessel vessel;
        internal MASVesselComputer vc;

        private double currentPhaseAngle;
        private double transferPhaseAngle;
        private double timeUntilTransfer;

        private double currentEjectionAngle;
        private double transferEjectionAngle;
        private double timeUntilEjection;

        private double initialDeltaV;
        private double finalDeltaV;

        [MoonSharpHidden]
        public MASITransfer(Vessel vessel)
        {
            this.vessel = vessel;
        }


        /// <summary>
        /// The Delta-V section provides information on the amount of velocity
        /// change needed to change orbits.  This information can be computed
        /// based on the current target, or a target altitude, depending on the
        /// specific method called.
        /// 
        /// These values are estimates based on circular orbits, assuming
        /// no plane change is required.  Eccentric orbits, or non-coplanar
        /// orbits, will not reflect the total ΔV required.
        /// </summary>
        #region Delta-V

        /// <summary>
        /// Returns an estimate of the ΔV required to circularize a Hohmann transfer at
        /// the target's orbit.
        /// 
        /// Negative values indicate a retrograde burn.  Positive values indicate a
        /// prograde burn.
        /// </summary>
        /// <returns>The ΔV in m/s to finialize the transfer.</returns>
        public double DeltaVFinal()
        {
            if (vc.activeTarget != null)
            {
                if (invalid)
                {
                    UpdateTransferParameters();
                }

                return finalDeltaV;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns and estimate of the ΔV required to circularize the vessel's orbit
        /// at the altitude provided.
        /// 
        /// Negative values indicate a retrograde burn.  Positive values indicate a
        /// prograde burn.
        /// </summary>
        /// <param name="destinationAltitude">Destination altitude, in meters.</param>
        /// <returns>ΔV in m/s to circularize at the requested altitude, or 0 if the vessel is not in flight.</returns>
        public double DeltaVFinal(double destinationAltitude)
        {
            if (!vessel.Landed)
            {
                double GM = vessel.mainBody.gravParameter;
                double rA = vessel.orbit.semiMajorAxis;
                double rB = destinationAltitude + vessel.mainBody.Radius;

                double atx = 0.5 * (rA + rB);
                double Vf = Math.Sqrt(GM / rB);

                double Vtxf = Math.Sqrt(GM * (2.0 / rB - 1.0 / atx));

                return Vf - Vtxf;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns an estimate of the ΔV required to start a Hohmann transfer to
        /// the target's orbit.
        /// 
        /// Negative values indicate a retrograde burn.  Positive values indicate a
        /// prograde burn.
        /// </summary>
        /// <returns>The ΔV in m/s to start the transfer.</returns>
        public double DeltaVInitial()
        {
            if (vc.activeTarget != null)
            {
                if (invalid)
                {
                    UpdateTransferParameters();
                }

                return initialDeltaV;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns and estimate of the ΔV required to change the vessel's orbit
        /// to the altitude provided.
        /// 
        /// Negative values indicate a retrograde burn.  Positive values indicate a
        /// prograde burn.
        /// </summary>
        /// <param name="destinationAltitude">Destination altitude, in meters.</param>
        /// <returns>ΔV in m/s to reach the requested altitude, or 0 if the vessel is not in flight.</returns>
        public double DeltaVInitial(double destinationAltitude)
        {
            if (!vessel.Landed)
            {
                double GM = vessel.mainBody.gravParameter;
                double rA = vessel.orbit.semiMajorAxis;
                double rB = destinationAltitude + vessel.mainBody.Radius;

                double atx = 0.5 * (rA + rB);
                double Vi = Math.Sqrt(GM / rA);

                double Vtxi = Math.Sqrt(GM * (2.0 / rA - 1.0 / atx));

                return Vtxi - Vi;
            }
            else
            {
                return 0.0;
            }
        }
        #endregion

        /// <summary>
        /// The Ejection Angle provides information on the ejection angle.  The
        /// ejection angle is used on interplanetary transfers to determine when
        /// the vessel should start its burn to escape the world it currently orbits.
        /// 
        /// When the vessel is orbiting a moon in preparation for an interplanetary
        /// transfer, the target ejection angle will reflect the ejection angle
        /// required to take advantage of the Oberth effect during the ejection.
        /// </summary>
        #region Ejection Angle

        /// <summary>
        /// Reports the vessel's current ejection angle.  When this value matches
        /// the transfer ejection angle, it is time to start an interplanetary burn.
        /// 
        /// This angle is a measurement of the vessel from the planet's prograde direction.
        /// </summary>
        /// <returns>Current ejection angle in degrees, or 0 if there is no ejection angle.</returns>
        public double CurrentEjectionAngle()
        {
            if (vc.activeTarget != null)
            {
                if (invalid)
                {
                    UpdateTransferParameters();
                }

                return currentEjectionAngle;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Reports the difference between the vessel's current ejection angle
        /// and the transfer ejection angle.  When this value is 0, it is time to
        /// start an interplanetary burn.
        /// 
        /// **NOT IMPLEMENTED**
        /// </summary>
        /// <returns>Relative ejection angle in degrees, or 0 if there is no ejection angle.</returns>
        public double RelativeEjectionAngle()
        {
            if (vc.activeTarget != null)
            {
                if (invalid)
                {
                    UpdateTransferParameters();
                }

                return Utility.NormalizeAngle(currentEjectionAngle - transferEjectionAngle);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Provides the time until the vessel reaches the transfer ejection angle.
        /// 
        /// **NOT IMPLEMENTED**
        /// </summary>
        /// <returns>Time until the relative ejection angle is 0, in seconds, or 0 if there is no ejection angle.</returns>
        public double TimeUntilEjection()
        {
            if (vc.activeTarget != null)
            {
                if (invalid)
                {
                    UpdateTransferParameters();
                }

                return timeUntilEjection;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Reports the ejection angle when an interplanetary Hohmann transfer
        /// orbit should begin.  This is of use for transfers from one planet
        /// to another - once the transfer phase angle has been reached, the
        /// vessel should launch when the next transfer ejection angle is reached.
        /// 
        /// **NOT IMPLEMENTED**
        /// </summary>
        /// <returns>Transfer ejection angle in degrees, or 0 if there is no ejection angle.</returns>
        public double TransferEjectionAngle()
        {
            //if (vc.activeTarget != null)
            //{
            //    if (invalid)
            //    {
            //        UpdateTransferParameters();
            //    }

            //    return transferEjectionAngle;
            //}
            //else
            {
                return 0.0;
            }
        }

        #endregion

        /// <summary>
        /// The Phase Angle section provides measurements of the phase angle, the
        /// measure of the angle created by drawing lines from the body being
        /// orbited to the vessel and to the target.  This angle shows relative
        /// position of the two objects, and it is continuously changing as long as
        /// the craft are not in the same orbit.
        /// 
        /// To do a Hohmann transfer between orbits, the vessel should initiate a
        /// burn when its current phase angle reaches the transfer phase angle.
        /// Alternatively, when the relative phase angle reaches 0, initiate a burn.
        /// </summary>
        #region Phase Angle
        /// <summary>
        /// Returns the current phase angle between the vessel and its target.
        /// </summary>
        /// <returns>Current phase angle in degrees, from 0 to 360.</returns>
        public double CurrentPhaseAngle()
        {
            if (vc.activeTarget != null)
            {
                if (invalid)
                {
                    UpdateTransferParameters();
                }

                return currentPhaseAngle;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the difference (in degrees) between the current phase angle
        /// and the transfer phase angle.  When this value reaches 0, it is time
        /// to start the transfer burn.  If there is no valid target, this value
        /// is 0.
        /// </summary>
        /// <returns>The difference between the transfer phase angle and the current
        /// phase angle in degrees, ranging from 0 to 360.</returns>
        public double RelativePhaseAngle()
        {
            if (vc.activeTarget != null)
            {
                if (invalid)
                {
                    UpdateTransferParameters();
                }

                return Utility.NormalizeAngle(currentPhaseAngle - transferPhaseAngle);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the time in seconds until the vessel reaches the correct
        /// phase angle for initiating a burn to transfer to the target.
        /// </summary>
        /// <returns>Time until transfer, in seconds, or 0 if there is no solution.</returns>
        public double TimeUntilPhaseAngle()
        {
            if (vc.activeTarget != null)
            {
                if (invalid)
                {
                    UpdateTransferParameters();
                }

                return timeUntilTransfer;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the phase angle required to initiate a Hohmann
        /// transfer orbit.  This is the absolute phase angle, so it
        /// does not vary over time when comparing two stable orbits.
        /// Use `fc.RelativePhaseAngle()` to count down to a transfer.
        /// 
        /// Returns 0 if there is no active target.
        /// </summary>
        /// <returns>Required phase angle in degrees (always betweeen 0
        /// and 180).</returns>
        public double TransferPhaseAngle()
        {
            if (vc.activeTarget != null)
            {
                if (invalid)
                {
                    UpdateTransferParameters();
                }

                return transferPhaseAngle;
            }
            else
            {
                return 0.0;
            }
        }

        #endregion

#if COMPARE_PHASE_ANGLE_PROTRACTOR
        /// <summary>
        /// Project vectors onto a plane, measure the angle
        /// between them.
        /// </summary>
        /// <param name="a">First ray</param>
        /// <param name="b">Second ray</param>
        /// <returns>Angle between the two vectors in degrees [0, 360).</returns>
        private static double ProjectAngle2D(Vector3d a, Vector3d b)
        {
            Vector3d ray1 = Vector3d.Project(new Vector3d(a.x, 0.0, a.z), a);
            Vector3d ray2 = Vector3d.Project(new Vector3d(b.x, 0.0, b.z), b);

            double phase = Vector3d.Angle(ray1, ray2);

            Vector3d ap = Quaternion.AngleAxis(90.0f, Vector3d.forward) * a;
            Vector3d ray1p = Vector3d.Project(new Vector3d(ap.x, 0.0, ap.z), ap);
            if (Vector3d.Angle(ray1p, ray2) > 90.0)
            {
                phase = 360.0 - phase;
            }

            return Utility.NormalizeAngle(phase);
        }
        private static double Angle2d(Vector3d vector1, Vector3d vector2)
        {
            Vector3d v1 = Vector3d.Project(new Vector3d(vector1.x, 0, vector1.z), vector1);
            Vector3d v2 = Vector3d.Project(new Vector3d(vector2.x, 0, vector2.z), vector2);
            return Vector3d.Angle(v1, v2);
        }
#endif

        /// <summary>
        /// For a given orbit, find the orbit of the object that orbits the sun.
        /// 
        /// If the orbit provided orbits the sun, this orbit is returned.  If the
        /// orbit is around a body that orbits the sun, the body's orbit is returned.
        /// If the orbit is around a moon, return the orbit of the moon's parent.
        /// </summary>
        static private Orbit GetSolarOrbit(Orbit orbit, out int numHops)
        {
            // Does this object orbit the sun?
            if (orbit.referenceBody == Planetarium.fetch.Sun)
            {
                numHops = 0;
                return orbit;
            }
            // Does this object orbit something that orbits the sun?
            else if (orbit.referenceBody.GetOrbit().referenceBody == Planetarium.fetch.Sun)
            {
                numHops = 1;
                return orbit.referenceBody.GetOrbit();
            }
            // Does this object orbit the moon of something that orbits the sun?
            else if (orbit.referenceBody.GetOrbit().referenceBody.GetOrbit().referenceBody == Planetarium.fetch.Sun)
            {
                numHops = 2;
                return orbit.referenceBody.GetOrbit().referenceBody.GetOrbit();
            }
            else
            {
                // Nothing in stock KSP orbits more than two levels deep.
                throw new ArgumentException("GetSolarOrbit(): Unable to find a valid solar orbit.");
            }
        }

        /// <summary>
        /// Find the orbits we can use to determine phase angle.  These orbits
        /// need to share a common reference body.  We also report how many parent
        /// bodies we had to look at to find the returned vesselOrbit, so we can
        /// compute the correct ejection angle (either ejection angle, or moon ejection
        /// angle for Oberth transfers).
        /// </summary>
        static private void GetCommonOrbits(ref Orbit vesselOrbit, ref Orbit destinationOrbit, out int vesselOrbitSteps)
        {
            if (vesselOrbit.referenceBody == destinationOrbit.referenceBody)
            {
                // Orbiting the same body.  Easy case.  We're done.
                vesselOrbitSteps = 0;
            }
            else if (vesselOrbit.referenceBody == Planetarium.fetch.Sun)
            {
                // We orbit the sun.  Find the orbit of whichever parent
                // of the target orbits the sun:
                int dontCare;
                destinationOrbit = GetSolarOrbit(destinationOrbit, out dontCare);
                vesselOrbitSteps = 0;
            }
            else if (destinationOrbit.referenceBody == Planetarium.fetch.Sun)
            {
                // The target orbits the sun, but we don't.
                vesselOrbit = GetSolarOrbit(vesselOrbit, out vesselOrbitSteps);
            }
            else
            {
                // Complex case...
                int dontCare;
                Orbit newVesselOrbit = GetSolarOrbit(vesselOrbit, out vesselOrbitSteps);
                Orbit newDestinationOrbit = GetSolarOrbit(destinationOrbit, out dontCare);

                if (newVesselOrbit == newDestinationOrbit)
                {
                    // Even more complex case.  Source and destination orbit are in the
                    // same planetary system, but one or both orbit moons.
                    if (vesselOrbitSteps == 2)
                    {
                        // Vessel orbits a moon.
                        vesselOrbit = vesselOrbit.referenceBody.GetOrbit();
                        vesselOrbitSteps = 1;
                    }
                    if (dontCare == 2)
                    {
                        destinationOrbit = destinationOrbit.referenceBody.GetOrbit();
                    }
                }
                else
                {
                    vesselOrbit = newVesselOrbit;
                    destinationOrbit = newDestinationOrbit;
                }
            }
        }

        /// <summary>
        /// Compute the delta-V required for the injection and circularization burns for the
        /// given orbits.
        /// 
        /// Equation from http://www.braeunig.us/space/
        /// </summary>
        /// <param name="startOrbit"></param>
        /// <param name="destinationOrbit"></param>
        private void UpdateTransferDeltaV(Orbit startOrbit, Orbit destinationOrbit)
        {
            double GM = startOrbit.referenceBody.gravParameter;
            double rA = startOrbit.semiMajorAxis;
            double rB = destinationOrbit.semiMajorAxis;

            double atx = 0.5 * (rA + rB);
            double Vi = Math.Sqrt(GM / rA);
            double Vf = Math.Sqrt(GM / rB);

            double Vtxi = Math.Sqrt(GM * (2.0 / rA - 1.0 / atx));
            double Vtxf = Math.Sqrt(GM * (2.0 / rB - 1.0 / atx));

            initialDeltaV = Vtxi - Vi;
            finalDeltaV = Vf - Vtxf;
        }

        /// <summary>
        /// Updater method - called at most once per FixedUpdate when the
        /// transfer parameters are being queried.
        /// </summary>
        private void UpdateTransferParameters()
        {
            // Initialize values
            currentPhaseAngle = 0.0;
            transferPhaseAngle = 0.0;
            timeUntilTransfer = 0.0;

            currentEjectionAngle = 0.0;
            transferEjectionAngle = 0.0;
            timeUntilEjection = 0.0;

            initialDeltaV = 0.0;
            finalDeltaV = 0.0;

            if (vc.activeTarget != null)
            {
                Orbit vesselOrbit = vessel.orbit;
                Orbit destinationOrbit = vc.activeTarget.GetOrbit();

                if (vesselOrbit.eccentricity >= 1.0 || destinationOrbit.eccentricity >= 1.0)
                {
                    // One or both orbits are escape orbits.  We can't work with them.
                    return;
                }

                int vesselOrbitSteps;
                GetCommonOrbits(ref vesselOrbit, ref destinationOrbit, out vesselOrbitSteps);

                // Figure out what sort of transfer we're doing.
                if (vesselOrbit.referenceBody != destinationOrbit.referenceBody)
                {
                    // We can't find a common orbit?
                    Utility.LogErrorMessage(this, "Bailing out computer transfer parameters: unable to reconcile orbits");
                    return;
                }

                // TODO: At what relative inclination should it bail out?
                if (Vector3.Angle(vesselOrbit.GetOrbitNormal(), destinationOrbit.GetOrbitNormal()) > 30.0)
                {
                    // Relative inclination is very out-of-spec.  Bail out.
                    return;
                }

                UpdateTransferDeltaV(vesselOrbit, destinationOrbit);

                // Transfer phase angle: use the mean radii of the orbits.
                double r1 = (vesselOrbit.PeR + vesselOrbit.ApR) * 0.5;
                double r2 = (destinationOrbit.PeR + destinationOrbit.ApR) * 0.5;

                // transfer phase angle from https://en.wikipedia.org/wiki/Hohmann_transfer_orbit
                // This does not do anything special for Oberth effect transfers
                transferPhaseAngle = 180.0 * (1.0 - 0.35355339 * Math.Pow(r1 / r2 + 1.0, 1.5));

#if COMPARE_PHASE_ANGLE_PROTRACTOR
                // current phase angle: the angle between the two positions as projected onto a 2D plane.
                Vector3d pos1 = vesselOrbit.getRelativePositionAtUT(vc.universalTime);
                Vector3d pos2 = destinationOrbit.getRelativePositionAtUT(vc.universalTime);

                double protractorPhaseAngle = ProjectAngle2D(pos1, pos2);
#endif
                // Use orbital parameters.  Note that the argumentOfPeriapsis and LAN
                // are both in degrees, while true anomaly is in radians.
                double tA1 = (vesselOrbit.trueAnomaly * Orbit.Rad2Deg + vesselOrbit.argumentOfPeriapsis + vesselOrbit.LAN);
                double tA2 = (destinationOrbit.trueAnomaly * Orbit.Rad2Deg + destinationOrbit.argumentOfPeriapsis + destinationOrbit.LAN);
                currentPhaseAngle = Utility.NormalizeAngle(tA2 - tA1);

#if COMPARE_PHASE_ANGLE_PROTRACTOR
                if (Math.Abs(currentPhaseAngle - protractorPhaseAngle) > 0.5)
                {
                    Utility.LogMessage(this, "Protractor phase angle = {0,7:0.00}; trueAnomaly pa = {1,7:0.00}, diff = {2,7:0.00}", protractorPhaseAngle, currentPhaseAngle, currentPhaseAngle - protractorPhaseAngle);
                }
#endif

                // The difference in mean motion tells us how quickly the phase angle is changing.
                // Since Orbit.meanMotion is in rad/sec, we need to convert the difference to deg/sec.
                double deltaRelativePhaseAngle = (vesselOrbit.meanMotion - destinationOrbit.meanMotion) * Orbit.Rad2Deg;

                if (deltaRelativePhaseAngle > 0.0)
                {
                    timeUntilTransfer = Utility.NormalizeAngle(currentPhaseAngle - transferPhaseAngle) / deltaRelativePhaseAngle;
                }
                else if (deltaRelativePhaseAngle < 0.0)
                {
                    timeUntilTransfer = Utility.NormalizeAngle(transferPhaseAngle - currentPhaseAngle) / deltaRelativePhaseAngle;
                }
                // else can't compute it - the orbits have the exact same period.

                // Compute current ejection angle
                //if (vesselOrbitSteps > 0)
                {
#if COMPARE_PHASE_ANGLE_PROTRACTOR
                    //--- PROTRACTOR
                    Vector3d vesselvec = vessel.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());

                    // get planet's position relative to universe
                    Vector3d bodyvec = vessel.mainBody.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());

                    Vector3d forwardVec = Quaternion.AngleAxis(90.0f, Vector3d.forward) * bodyvec;
                    forwardVec.Normalize();
                    double protractorEject = Angle2d(vesselvec, Quaternion.AngleAxis(90.0f, Vector3d.forward) * bodyvec);

                    if (Angle2d(vesselvec, Quaternion.AngleAxis(180.0f, Vector3d.forward) * bodyvec) > Angle2d(vesselvec, bodyvec))
                    {
                        protractorEject = 360.0 - protractorEject;//use cross vector to determine up or down
                    }
                    //--- PROTRACTOR
#endif
                    Vector3d vesselPos = vessel.orbit.pos;
                    vesselPos.Normalize();
                    Vector3d bodyProgradeVec = vessel.mainBody.orbit.vel;
                    bodyProgradeVec.Normalize();
                    Vector3d bodyPosVec = vessel.mainBody.orbit.pos;
                    currentEjectionAngle = Vector3d.Angle(vesselPos, bodyProgradeVec);
                    if (Vector3d.Dot(vesselPos, bodyPosVec) > 0.0)
                    {
                        currentEjectionAngle = Utility.NormalizeAngle(360.0 - currentEjectionAngle);
                    }
#if COMPARE_PHASE_ANGLE_PROTRACTOR
                    Utility.LogMessage(this, "Protractor ejection angle = {0,5:0.0} , computed = {1,5:0.0}", protractorEject, currentEjectionAngle);
#endif
                }

                //if (vesselOrbitSteps == 1)
                //{
                //    // transferEjectionAngle = something
                //    transferEjectionAngle = Utility.NormalizeAngle(CalcEjectionValues(vesselOrbit.referenceBody,) * Orbit.Rad2Deg);
                //}
                //else if(vesselOrbitSteps == 2)
                //{
                //    // Compute moon ejection angle to take advantage of the Oberth effect.
                //    // transferEjectionAngle = something different than for vesselOrbitSteps = 1?
                //}
            }

            invalid = false;
        }

        /// <summary>
        /// Called per-FixedUpdate to invalidate computations.
        /// </summary>
        [MoonSharpHidden]
        internal void Update()
        {
            invalid = true;
        }
    }
}
