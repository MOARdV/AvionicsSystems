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

        //private double transferDeltaV;

        [MoonSharpHidden]
        public MASITransfer(Vessel vessel)
        {
            this.vessel = vessel;
        }

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
        /// </summary>
        /// <returns>Transfer ejection angle in degrees, or 0 if there is no ejection angle.</returns>
        public double TransferEjectionAngle()
        {
            if (vc.activeTarget != null)
            {
                if (invalid)
                {
                    UpdateTransferParameters();
                }

                return transferEjectionAngle;
            }
            else
            {
                return 0.0;
            }
        }

        #endregion

        /// <summary>
        /// The Phase Angle section provides measurements of the phase angle, the
        /// measure of the angle created by drawing lines from the object being
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

        /// <summary>
        /// Project vectors onto a plane, measure the angle
        /// between them.
        /// </summary>
        /// <param name="a">First ray</param>
        /// <param name="b">Second ray</param>
        /// <returns>Angle between the two vectors in degrees [0, 360).</returns>
        private static double ProjectAngle2D(Vector3d a, Vector3d b)
        {
            // TODO: atan2 instead.
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

            if (vc.activeTarget != null)
            {
                Orbit vesselOrbit = vessel.orbit;
                Orbit destinationOrbit = vc.activeTarget.GetOrbit();

                if (vesselOrbit.eccentricity >= 1.0 || destinationOrbit.eccentricity >= 1.0)
                {
                    // One or both orbits are escape orbits.  We can't work with them.
                    return;
                }

                // Figure out what sort of transfer we're doing.
                if (vesselOrbit.referenceBody != destinationOrbit.referenceBody)
                {
                    // We're not orbiting the same thing .. we need to compute proxy
                    // orbits

                    // HACK:
                    return;
                }

                // Transfer phase angle: use the mean radii of the orbits.
                double r1 = (vesselOrbit.PeR + vesselOrbit.ApR) * 0.5;
                double r2 = (destinationOrbit.PeR + destinationOrbit.ApR) * 0.5;

                // transfer phase angle from https://en.wikipedia.org/wiki/Hohmann_transfer_orbit
                transferPhaseAngle = 180.0 * (1.0 - 0.35355339 * Math.Pow(r1 / r2 + 1.0, 1.5));

                // current phase angle: the angle between the two positions as projected onto a 2D plane.
                Vector3d pos1 = vesselOrbit.getRelativePositionAtUT(vc.universalTime);
                Vector3d pos2 = destinationOrbit.getRelativePositionAtUT(vc.universalTime);

                currentPhaseAngle = ProjectAngle2D(pos1, pos2);

                double deltaRelativePhaseAngle = (360.0 / vesselOrbit.period) - (360.0 / destinationOrbit.period);
                if (deltaRelativePhaseAngle > 0.0)
                {
                    timeUntilTransfer = Utility.NormalizeAngle(currentPhaseAngle - transferPhaseAngle) / deltaRelativePhaseAngle;
                }
                else if (deltaRelativePhaseAngle <0.0)
                {
                    // isn't 360 - (current - transfer) == (transfer - current)?
                    timeUntilTransfer = (360.0 - Utility.NormalizeAngle(currentPhaseAngle - transferPhaseAngle)) / deltaRelativePhaseAngle;
                }
                // else can't compute it - the orbits have the exact same period.
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
