/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2018 MOARdV
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
using System;
using UnityEngine;

namespace AvionicsSystems
{
    internal partial class MASVesselComputer : MonoBehaviour
    {
        // Flight control / custom autopilot

        internal enum ReferenceAttitude
        {
            REF_INERTIAL,
            
            REF_ORBIT_PROGRADE,
            REF_ORBIT_HORIZONTAL, // Orbit prograde, horizontal

            REF_SURFACE_PROGRADE,
            REF_SURFACE_HORIZONTAL, // Surface prograde, horizontal
            REF_SURFACE_NORTH,

            REF_TARGET, // TGT +
            REF_TARGET_RELATIVE_VEL,
            REF_TARGET_ORIENTATION,

            REF_MANEUVER_NODE,
            REF_SUN,
        };

        /// <summary>
        /// Is the MAS pilot doing something?
        /// </summary>
        internal bool attitudePilotEngaged;

        /// <summary>
        /// What reference mode is currently active?
        /// </summary>
        internal ReferenceAttitude activeReference { get; private set; }

        /// <summary>
        /// Desired heading, pitch, roll relative to the current reference attitude.  Pre-processed
        /// with the additional rotation of 90 degrees around the x axis.
        /// </summary>
        private Quaternion relativeOrientation = Quaternion.Euler(90.0f, 0.0f, 0.0f);

        /// <summary>
        /// Heading, pitch, yaw of the current relativeOrientation.
        /// </summary>
        internal Vector3 relativeHPR { get; private set; }

        /// <summary>
        /// Used for when we have to switch to Stability Assist mode.
        /// </summary>
        private UIStateToggleButton[] SASbtns = null;

        /// <summary>
        /// Internal flight control processing for the vessel.
        /// </summary>
        private void PilotFixedUpdate()
        {
            if (attitudePilotEngaged && vessel.ActionGroups[KSPActionGroup.SAS])
            {
                if (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.StabilityAssist)
                {
                    Quaternion goalOrientation = getReferenceAttitude(activeReference) * relativeOrientation;// *Quaternion.Euler(90, 0, 0);

                    Quaternion currentOrientation = vessel.Autopilot.SAS.lockedRotation;
                    float delta = Quaternion.Angle(goalOrientation, currentOrientation);

                    Quaternion newOrientation;
                    // Reduce the angle updates as the vessel gets closer to the target orientation.
                    if (delta < 0.25f)
                    {
                        newOrientation = goalOrientation;
                    }
                    else if (delta > 10.0f)
                    {
                        newOrientation = Quaternion.Slerp(currentOrientation, goalOrientation, 2.0f / delta);
                    }
                    else if (delta > 3.0f)
                    {
                        newOrientation = Quaternion.Slerp(currentOrientation, goalOrientation, 0.5f / delta);
                    }
                    else // 0.25 - 3 degrees difference
                    {
                        newOrientation = Quaternion.Slerp(currentOrientation, goalOrientation, 0.15f / delta);
                    }
                    vessel.Autopilot.SAS.LockRotation(newOrientation);
                }
                else
                {
                    attitudePilotEngaged = false;
                }
            }
            else
            {
                attitudePilotEngaged = false;
            }
        }

        /// <summary>
        /// Configure and engage SAS.
        /// </summary>
        /// <param name="reference">Which attitude reference to apply.</param>
        /// <param name="HPR">Heading, Pitch, Roll</param>
        internal bool EngageAttitudePilot(ReferenceAttitude reference, Vector3 HPR)
        {
            if (!ValidReference(reference))
            {
                return false;
            }

            if (!vessel.ActionGroups[KSPActionGroup.SAS])
            {
                vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
            }
            else
            {
                if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.StabilityAssist))
                {
                    vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.StabilityAssist);

                    if (SASbtns == null)
                    {
                        SASbtns = UnityEngine.Object.FindObjectOfType<VesselAutopilotUI>().modeButtons;
                    }
                    // set our mode, note it takes the mode as an int, generally top to bottom, left to right, as seen on the screen. Maneuver node being the exception, it is 9
                    SASbtns[(int)VesselAutopilot.AutopilotMode.StabilityAssist].SetState(true);
                }
                else
                {
                    attitudePilotEngaged = false;
                    return false;
                }
            }

            relativeHPR = HPR;
            relativeOrientation = Quaternion.AngleAxis(relativeHPR.x, Vector3.up) * Quaternion.AngleAxis(-relativeHPR.y, Vector3.right) * Quaternion.AngleAxis(-relativeHPR.z, Vector3.forward) * Quaternion.Euler(90, 0, 0);

            activeReference = reference;

            attitudePilotEngaged = true;

            return true;
        }

        /// <summary>
        /// Is the selected reference attitude valid?
        /// </summary>
        /// <param name="reference"></param>
        /// <returns></returns>
        private bool ValidReference(ReferenceAttitude reference)
        {
            if (reference == ReferenceAttitude.REF_MANEUVER_NODE)
            {
                if (node == null)
                {
                    return false;
                }
            }
            else if(reference == ReferenceAttitude.REF_TARGET || reference == ReferenceAttitude.REF_TARGET_ORIENTATION || reference == ReferenceAttitude.REF_TARGET_RELATIVE_VEL)
            {
                if (activeTarget == null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Return the rotation that
        /// </summary>
        /// <param name="reference"></param>
        /// <returns></returns>
        internal Quaternion getReferenceAttitude(ReferenceAttitude reference)
        {
            Quaternion referenceQuat = Quaternion.identity;

            switch (reference)
            {
                case ReferenceAttitude.REF_INERTIAL:
                    referenceQuat = Quaternion.identity;
                    break;
                case ReferenceAttitude.REF_ORBIT_PROGRADE:
                    referenceQuat = Quaternion.LookRotation(prograde, radialOut);
                    break;
                case ReferenceAttitude.REF_ORBIT_HORIZONTAL:
                    referenceQuat = Quaternion.LookRotation(Vector3.ProjectOnPlane(prograde, up), up);
                    break;
                case ReferenceAttitude.REF_SURFACE_PROGRADE:
                    referenceQuat = Quaternion.LookRotation(surfacePrograde, up);
                    break;
                case ReferenceAttitude.REF_SURFACE_HORIZONTAL:
                    referenceQuat = Quaternion.LookRotation(Vector3.ProjectOnPlane(surfacePrograde, up), up);
                    break;
                case ReferenceAttitude.REF_SURFACE_NORTH:
                    referenceQuat = Quaternion.LookRotation(vessel.north, up);
                    break;
                case ReferenceAttitude.REF_TARGET:
                    referenceQuat = Quaternion.LookRotation(targetDirection, radialOut);
                    break;
                case ReferenceAttitude.REF_TARGET_RELATIVE_VEL:
                    referenceQuat = Quaternion.LookRotation(targetRelativeVelocity.normalized, radialOut);
                    break;
                case ReferenceAttitude.REF_TARGET_ORIENTATION:
                    // TODO!!!
                    referenceQuat = Quaternion.LookRotation(activeTarget.GetTransform().forward, activeTarget.GetTransform().up);
                    //if (targetType == TargetType.CelestialBody)
                    //{

                    //}
                    //else
                    //{
                    //    referenceQuat = Quaternion.LookRotation(targetDirection, radialOut);
                    //}
                    break;
                case ReferenceAttitude.REF_MANEUVER_NODE:
                    referenceQuat = Quaternion.LookRotation(maneuverNodeVector.normalized, radialOut);
                    break;
                case ReferenceAttitude.REF_SUN:
                    referenceQuat = Quaternion.LookRotation((Planetarium.fetch.Sun.transform.position - vessel.CoM).normalized, Vector3.up);
                    break;
            }

            return referenceQuat;
        }
    }
}
