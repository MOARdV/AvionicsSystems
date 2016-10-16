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
    /// </mdDoc>
    internal class MASITransfer
    {
        private bool invalid = true;

        internal Vessel vessel;
        internal MASVesselComputer vc;

        private double currentPhaseAngle;
        private double transferPhaseAngle;
        //private double transferDeltaV;

        [MoonSharpHidden]
        public MASITransfer(Vessel vessel)
        {
            this.vessel = vessel;
        }

        /// <summary>
        /// Returns the current phase angle between the vessel and its target.
        /// 
        /// **BUG:** Currently, this value is always between 0 and 180, not 0 and 360.
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
        /// 
        /// **BUG:** Currently, this value is always between 0 and 180.
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

        /// <summary>
        /// Updater method - called at most once per FixedUpdate when the
        /// transfer parameters are being queried.
        /// </summary>
        private void UpdateTransferParameters()
        {
            // Initialize values
            currentPhaseAngle = 0.0;
            transferPhaseAngle = 0.0;

            if (vc.activeTarget != null)
            {
                Orbit vesselOrbit = vessel.orbit;
                Orbit destinationOrbit = vc.activeTarget.GetOrbit();

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
                pos1 = new Vector3d(pos1.x, pos1.y, 0.0);
                pos2 = new Vector3d(pos2.x, pos2.y, 0.0);

                // TODO: What happens when the relative inclination > 90*? Need to account for that
                // TODO: Vector3d.Angle takes the smallest angle, so it's always <=180.
                currentPhaseAngle = Utility.NormalizeAngle(Vector3d.Angle(pos1, pos2));
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
