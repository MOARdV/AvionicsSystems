#define VERBOSE_AUTOPILOT_LOGGING
//#define USE_THIS
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
#if USE_THIS
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
        /// Is the MAS maneuver autopilot doing something?
        /// </summary>
        internal bool maneuverPilotEngaged;

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

        private KerbalFSM maneuverPilot = new KerbalFSM();

        /// <summary>
        /// Used for when we have to switch to Stability Assist mode.
        /// </summary>
        private UIStateToggleButton[] SASbtns = null;

        /// <summary>
        /// Shut off all of the autopilots.
        /// </summary>
        internal void CancelAutopilots()
        {
            if (attitudePilotEngaged)
            {
                if (vessel.ActionGroups[KSPActionGroup.SAS])
                {
                    vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.StabilityAssist);
                }

                attitudePilotEngaged = false;
            }
            maneuverPilotEngaged = false;
        }

        /// <summary>
        /// Are any pilots active?
        /// </summary>
        /// <returns></returns>
        internal bool PilotActive()
        {
            return attitudePilotEngaged || maneuverPilotEngaged;
        }

        /// <summary>
        /// Initialize the autopilot FSMs.
        /// </summary>
        private void PilotInitialize()
        {
            ManeuverPilotInitialize();
        }

        /// <summary>
        /// Intiialize the maneuver pilot FSM.
        /// </summary>
        private void ManeuverPilotInitialize()
        {
            KFSMState idleState = new KFSMState("Idle");
            idleState.updateMode = KFSMUpdateMode.FIXEDUPDATE;

            KFSMState coastState = new KFSMState("Coasting");
            coastState.updateMode = KFSMUpdateMode.FIXEDUPDATE;

            MASFlyState flyState = new MASFlyState("Flying", this);
            flyState.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            flyState.OnEnter = flyState.OnEnterImpl;
            flyState.OnFixedUpdate = flyState.OnFixedUpdateImpl;

            KFSMEvent stopPilot = new KFSMEvent("Stop Pilot");
            stopPilot.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            stopPilot.OnCheckCondition = (KFSMState currentState) =>
            {
                bool stopThisPilot = (maneuverPilotEngaged == false || attitudePilotEngaged == false || node == null);
                if (stopThisPilot)
                {
                    CancelAutopilots();
                    //Utility.LogMessage(this, "StopPilot  event: Transitioning");
                }
                return (stopThisPilot);
            };
            stopPilot.GoToStateOnEvent = idleState;

            KFSMEvent startPilot = new KFSMEvent("Start Pilot");
            startPilot.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            startPilot.OnCheckCondition = (KFSMState currentState) =>
            {
                return maneuverPilotEngaged;
            };
            startPilot.GoToStateOnEvent = coastState;

            KFSMEvent flyPilot = new KFSMEvent("Fly Pilot");
            flyPilot.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            flyPilot.OnCheckCondition = (KFSMState currentState) =>
                {
                    if (maneuverPilotEngaged) // Should be redundant?
                    {
                        double burnTime = NodeBurnTime();

                        if (burnTime > 0.0 && burnTime * 0.5 >= -maneuverNodeTime)
                        {
#if VERBOSE_AUTOPILOT_LOGGING
                            Utility.LogMessage(this, "FlyPilot   event: Transitioning");
#endif
                            return true;
                        }
                    }
                    return false;
                };
            flyPilot.GoToStateOnEvent = flyState;

            KFSMEvent doneFlying = new KFSMEvent("Done Event");
            doneFlying.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            doneFlying.OnCheckCondition = (KFSMState currentState) =>
                {
                    if (maneuverNodeDeltaV < 0.15)
                    {
                        CancelAutopilots();
                        if (vessel.patchedConicSolver != null)
                        {
                            vessel.patchedConicSolver.maneuverNodes.Clear();
                        }
                        try
                        {
                            FlightInputHandler.state.mainThrottle = 0.0f;
                        }
                        catch
                        {

                        }
#if VERBOSE_AUTOPILOT_LOGGING
                        Utility.LogMessage(this, "Done flying...");
#endif
                        return true;
                    }
                    return false;
                };
            doneFlying.GoToStateOnEvent = idleState;

            idleState.AddEvent(startPilot);
            coastState.AddEvent(stopPilot);
            coastState.AddEvent(flyPilot);
            flyState.AddEvent(stopPilot);
            flyState.AddEvent(doneFlying);

            maneuverPilot.AddState(idleState);
            maneuverPilot.AddState(coastState);
            maneuverPilot.AddState(flyState);

            maneuverPilot.StartFSM(idleState);
        }

        /// <summary>
        /// Internal flight control processing for the vessel.
        /// </summary>
        private void PilotFixedUpdate()
        {
            if (maneuverPilotEngaged)
            {
                //Utility.LogMessage(this, "FixedUpdate: FSM state is {0}, started = {1}", maneuverPilot.currentStateName, maneuverPilot.Started);
                maneuverPilot.FixedUpdateFSM();
            }
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
                    CancelAutopilots();
                }
            }
            else
            {
                CancelAutopilots();
            }
        }

        /// <summary>
        /// Engage SAS for custom attitude control.
        /// </summary>
        /// <returns>true if SAS engaged, false otherwise.</returns>
        private bool EngageSAS()
        {
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
                    CancelAutopilots();
                    return false;
                }
            }

            return true;
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

            if (!EngageSAS())
            {
                return false;
            }

            relativeHPR = HPR;
            relativeOrientation = Quaternion.AngleAxis(relativeHPR.x, Vector3.up) * Quaternion.AngleAxis(-relativeHPR.y, Vector3.right) * Quaternion.AngleAxis(-relativeHPR.z, Vector3.forward) * Quaternion.Euler(90, 0, 0);

            activeReference = reference;

            attitudePilotEngaged = true;

            return true;
        }

        /// <summary>
        /// Configure SAS for maneuver, activate the maneuver pilot's state machine.
        /// </summary>
        /// <returns></returns>
        internal bool EngageManeuverPilot()
        {
            if (!ValidReference(ReferenceAttitude.REF_MANEUVER_NODE))
            {
                return false;
            }

            if (!EngageSAS())
            {
                return false;
            }

            relativeHPR = Vector3.zero;
            relativeOrientation = Quaternion.AngleAxis(relativeHPR.x, Vector3.up) * Quaternion.AngleAxis(-relativeHPR.y, Vector3.right) * Quaternion.AngleAxis(-relativeHPR.z, Vector3.forward) * Quaternion.Euler(90, 0, 0);

            activeReference = ReferenceAttitude.REF_MANEUVER_NODE;

            attitudePilotEngaged = true;
            maneuverPilotEngaged = true;

            maneuverPilot.StartFSM("Idle");

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
            else if (reference == ReferenceAttitude.REF_TARGET || reference == ReferenceAttitude.REF_TARGET_ORIENTATION || reference == ReferenceAttitude.REF_TARGET_RELATIVE_VEL)
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
                    // TODO: use radialOut at the time of the maneuver, not the current radialOut.  Or use the current top vector
                    referenceQuat = Quaternion.LookRotation(maneuverNodeVector.normalized, radialOut);
                    //referenceQuat = Quaternion.LookRotation(maneuverNodeVector.normalized, top); // This doesn't work right...
                    //referenceQuat = Quaternion.LookRotation(maneuverNodeVector.normalized, vessel.GetTransform().up); // this just spins....
                    break;
                case ReferenceAttitude.REF_SUN:
                    referenceQuat = Quaternion.LookRotation((Planetarium.fetch.Sun.transform.position - vessel.CoM).normalized, Vector3.up);
                    break;
            }

            return referenceQuat;
        }
    }

    /// <summary>
    /// The maneuver autopilot's Fly State needs to maintain some state in order to make
    /// decisions on the throttle limit, so it's implemented here as a superset of the
    /// KFSMState.
    /// </summary>
    internal class MASFlyState : KFSMState
    {
        private MASVesselComputer vc;
        private double startDV;
        private double lastDV;

        internal MASFlyState(string name, MASVesselComputer vc) : base(name) { this.vc = vc; }

        internal void OnEnterImpl(KFSMState previousState)
        {
            startDV = Math.Max(vc.maneuverNodeDeltaV, 0.01);
            lastDV = startDV;
        }

        internal void OnFixedUpdateImpl()
        {
            float maxThrottle = 1.0f;

#if VERBOSE_AUTOPILOT_LOGGING
            Utility.LogMessage(this, "We're flying... {0:0.00}m/s remain", vc.maneuverNodeDeltaV);
#endif

            if (vc.maneuverNodeDeltaV > lastDV)
            {
                Utility.LogWarning(this, " ... remaining dV is increasing!");
            }

            /*
            double netDVPercent = Math.Min((startDV - vc.maneuverNodeDeltaV) / startDV, 1.0);
            if (netDVPercent > 0.90)
            {
                float throttleLimit = Mathf.SmoothStep(0.9f, 0.1f, ((float)netDVPercent - 0.90f) * 10.0f);
                maxThrottle = Mathf.Min(maxThrottle, throttleLimit);
#if VERBOSE_AUTOPILOT_LOGGING
                Utility.LogMessage(this, " ... limit {0:0.00} due to netDVPercent {1:0.00}", throttleLimit, netDVPercent);
#endif
            }
            else if (netDVPercent < 0.10)
            {
                float throttleLimit = Mathf.SmoothStep(0.1f, 1.0f, ((float)netDVPercent) * 10.0f);
                maxThrottle = Mathf.Min(maxThrottle, throttleLimit);
#if VERBOSE_AUTOPILOT_LOGGING
                Utility.LogMessage(this, " ... limit {0:0.00} due to netDVPercent {1:0.00}", throttleLimit, netDVPercent);
#endif
            }
            */
            
            float netDV = (float)(startDV - vc.maneuverNodeDeltaV);
            if (netDV < 2.0f)
            {
                float throttleLimit = Mathf.SmoothStep(0.1f, 1.0f, netDV * 0.5f);
                maxThrottle = Mathf.Min(maxThrottle, throttleLimit);
#if VERBOSE_AUTOPILOT_LOGGING
                Utility.LogMessage(this, " ... limit {0:0.00} due to netDV {1:0.00}", throttleLimit, netDV);
#endif
            }

            if (vc.maneuverNodeDeltaV < 2.5)
            {
                float throttleLimit = Mathf.SmoothStep(0.1f, 1.0f, ((float)vc.maneuverNodeDeltaV) * 0.4f);
                maxThrottle = Mathf.Min(maxThrottle, throttleLimit);
#if VERBOSE_AUTOPILOT_LOGGING
                Utility.LogMessage(this, " ... limit {0:0.00} due to vc.maneuverNodeDeltaV {1:0.00}", throttleLimit, vc.maneuverNodeDeltaV);
#endif
            }

            float headingError = Vector3.Angle(vc.forward, vc.maneuverNodeVector.normalized);
            if (headingError > 0.1f)
            {
                float throttleLimit = Mathf.SmoothStep(1.0f, 0.0f, (headingError - 0.1f) * 0.2f);
                maxThrottle = Mathf.Min(maxThrottle, throttleLimit);
#if VERBOSE_AUTOPILOT_LOGGING
                Utility.LogMessage(this, " ... limit {0:0.00} due to headingError {1:0.0}", throttleLimit, headingError);
#endif
            }

#if VERBOSE_AUTOPILOT_LOGGING
            Utility.LogMessage(this, " ... throttle is {0:0.00}", maxThrottle);
#endif
            FlightInputHandler.state.mainThrottle = maxThrottle;

            lastDV = vc.maneuverNodeDeltaV;
        }
    }
#endif
}
