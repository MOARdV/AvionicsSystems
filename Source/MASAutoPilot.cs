/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2019 MOARdV
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
using System.Collections.Generic;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// The MASAutoPilot is intended to be a rudimentary pilot system.  It is
    /// nowhere near as full-featured as MechJeb, and it uses stock control
    /// systems (SAS, in particular) to manage functionality.
    /// </summary>
    public class MASAutoPilot : MonoBehaviour
    {
        public enum ReferenceAttitude
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
        /// Create or return the MASAutoPilot attached to this vessel.
        /// </summary>
        /// <param name="vessel">The vessel we want to control</param>
        /// <returns>The MASAutoPilot</returns>
        public static MASAutoPilot Get(Vessel vessel)
        {
            MASAutoPilot masap = vessel.gameObject.AddOrGetComponent<MASAutoPilot>();

            masap.vessel = vessel;

            return masap;
        }

        /// <summary>
        /// Who we control.
        /// </summary>
        private Vessel vessel;

        /// <summary>
        /// Active target, or null;
        /// </summary>
        private ITargetable activeTarget = null;

        //--- Ascent Pilot Fields ---------------------------------------------

        private double apoapsis, periapsis;
        private float heading, roll;

        /// <summary>
        /// Is the MAS ascent pilot doing something?
        /// </summary>
        public bool ascentPilotEngaged { get; private set; }

        /// <summary>
        /// State machine to manage the ascent pilot module.
        /// </summary>
        private KerbalFSM ascentPilot = new KerbalFSM();

        private KSP.UI.Screens.Flight.NavBall navBall;

        //--- Attitude Pilot Fields -------------------------------------------

        /// <summary>
        /// Is the MAS attitude pilot doing something?
        /// </summary>
        public bool attitudePilotEngaged { get; private set; }

        /// <summary>
        /// State machine to manage the attitude hold module.
        /// </summary>
        private KerbalFSM attitudePilot = new KerbalFSM();

        /// <summary>
        /// What reference mode is currently active?
        /// </summary>
        public ReferenceAttitude activeReference { get; private set; }

        /// <summary>
        /// Heading, pitch, roll to hold relative to the current activeReference.
        /// If lockOrientation is false, then the roll component is "don't-care".
        /// </summary>
        public Vector3 relativeHPR { get; private set; }

        /// <summary>
        /// Reference to the UI buttons that display the current SAS mode, so we can keep
        /// them updated.
        /// </summary>
        private UIStateToggleButton[] SASbtns = null;

        /// <summary>
        /// Were we asked to hold heading, pitch, and roll (or even just roll) relative to the reference vector?
        /// </summary>
        private bool lockOrientation = false;

        /// <summary>
        /// Quaternion representing the desired orientation relative to the vector.
        /// </summary>
        private Quaternion orientation = Quaternion.identity;

        //--- Maneuver Pilot Fields -------------------------------------------

        /// <summary>
        /// Is the MAS maneuver autopilot doing something?
        /// </summary>
        public bool maneuverPilotEngaged { get; private set; }

        /// <summary>
        /// State machine to manage the maneuver execution module.
        /// </summary>
        private KerbalFSM maneuverPilot = new KerbalFSM();

        /// <summary>
        /// Active maneuver node, or null.
        /// </summary>
        private ManeuverNode node = null;

        #region General Interface

        /// <summary>
        /// Disengage all pilots.
        /// </summary>
        public void DisengageAutopilots()
        {
            ascentPilotEngaged = false;
            attitudePilotEngaged = false;
            maneuverPilotEngaged = false;
        }

        /// <summary>
        /// Returns true if any MAS Auto Pilot is engaged.
        /// </summary>
        /// <returns></returns>
        public bool PilotActive()
        {
            return ascentPilotEngaged || attitudePilotEngaged || maneuverPilotEngaged;
        }

        #endregion

        #region Ascent Interface

        /// <summary>
        /// Disengage the ascent autopilot.
        /// </summary>
        /// <returns>True if the AP was previously engaged, false if it was already idle.</returns>
        public bool DisengageAscentPilot()
        {
            if (ascentPilotEngaged)
            {
                ascentPilotEngaged = false;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Engage the ascent auto pilot to put the spacecraft into the specified orbit.
        /// </summary>
        /// <param name="apoapsis">Target apoapsis, in meters.</param>
        /// <param name="periapsis">Target periapsis, in meters.</param>
        /// <param name="inclination">Orbital inclination, degrees.</param>
        /// <param name="roll">Relative roll to hold during ascent, degrees.</param>
        /// <returns>True if the pilot can be engaged, false otherwise.</returns>
        public bool EngageAscentPilot(double apoapsis, double periapsis, double inclination, double roll)
        {
            if (!ValidReference(ReferenceAttitude.REF_SURFACE_NORTH) || !ValidReference(ReferenceAttitude.REF_SURFACE_PROGRADE) || !ValidReference(ReferenceAttitude.REF_ORBIT_PROGRADE))
            {
                Utility.LogMessage(this, "EngageAP: Failed because of references");
                return false;
            }
            if (periapsis > apoapsis)
            {
                Utility.LogMessage(this, "EngageAP: Failed because Pe > Ap");
                return false;
            }
            if (!(vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.PRELAUNCH || vessel.situation == Vessel.Situations.SPLASHED))
            {
                Utility.LogMessage(this, "EngageAP: Failed because of situation {0}", vessel.situation);
                return false;
            }

            if (navBall == null)
            {
                navBall = UnityEngine.Object.FindObjectOfType<KSP.UI.Screens.Flight.NavBall>();
            }
            Quaternion relativeGimbal = navBall.relativeGymbal;
            Vector3 surfaceAttitude = Quaternion.Inverse(relativeGimbal).eulerAngles;
            // Heading is in Y.  Pitch and roll are X and Z, respectively,
            // but they require a little more processing:
            if (surfaceAttitude.x > 180.0f)
            {
                surfaceAttitude.x = 360.0f - surfaceAttitude.x;
            }
            else
            {
                surfaceAttitude.x = -surfaceAttitude.x;
            }

            if (surfaceAttitude.z > 180.0f)
            {
                surfaceAttitude.z = 360.0f - surfaceAttitude.z;
            }
            else
            {
                surfaceAttitude.z = -surfaceAttitude.z;
            }
            // Check pitch
            if (surfaceAttitude.x < 85.0f)
            {
                // Vessel is not close enough to vertical to safely engage.
                Utility.LogMessage(this, "EngageAP: Failed because pitch is {0:0.0}", surfaceAttitude.x);
                return false;
            }

            inclination = Utility.NormalizeLongitude(inclination);
            roll = Utility.NormalizeLongitude(roll);

            this.apoapsis = apoapsis;
            this.periapsis = periapsis;
            this.heading = 90.0f - (float)inclination;
            this.roll = (float)roll;

            lockOrientation = true;

            activeReference = ReferenceAttitude.REF_SURFACE_NORTH;
            relativeHPR = new Vector3(surfaceAttitude.y, 89.0f, 0.0f);
            orientation = Quaternion.AngleAxis(relativeHPR.x, Vector3.up) * Quaternion.AngleAxis(-relativeHPR.y, Vector3.right) * Quaternion.AngleAxis(-relativeHPR.z, Vector3.forward) * Quaternion.Euler(90, 0, 0);

            attitudePilotEngaged = true;
            ascentPilotEngaged = true;
            maneuverPilotEngaged = false;

            return true;
        }
        #endregion

        #region Attitude Interface

        /// <summary>
        /// Set the autopilot to hold the selected heading, pitch, and roll relative
        /// to the reference attitude
        /// </summary>
        /// <param name="reference">The reference vector.</param>
        /// <param name="HPR">The heading, pitch, roll to maintain relative to the framework.</param>
        /// <returns>true if engaged, false otherwise.</returns>
        public bool EngageAttitudePilot(ReferenceAttitude reference, Vector3 HPR)
        {
            if (!ValidReference(reference))
            {
                return false;
            }

            lockOrientation = true;

            activeReference = reference;
            relativeHPR = HPR;
            orientation = Quaternion.AngleAxis(relativeHPR.x, Vector3.up) * Quaternion.AngleAxis(-relativeHPR.y, Vector3.right) * Quaternion.AngleAxis(-relativeHPR.z, Vector3.forward) * Quaternion.Euler(90, 0, 0);

            attitudePilotEngaged = true;
            ascentPilotEngaged = false;
            maneuverPilotEngaged = false;

            return true;
        }

        /// <summary>
        /// Set the autopilot to hold towards the selected reference vector.  Roll is
        /// considered unimportant.
        /// </summary>
        /// <param name="reference">The reference vector.</param>
        /// <returns>true if engaged, false otherwise.</returns>
        public bool EngageAttitudePilot(ReferenceAttitude reference)
        {
            if (!ValidReference(reference))
            {
                return false;
            }

            lockOrientation = false;

            activeReference = reference;
            relativeHPR = Vector3.zero;
            orientation = Quaternion.identity; // Updated during FixedUpdate

            attitudePilotEngaged = true;
            ascentPilotEngaged = false;
            maneuverPilotEngaged = false;

            return true;
        }

        /// <summary>
        /// Resume the attitude hold using the previous settings.
        /// </summary>
        /// <returns></returns>
        public bool ResumeAttitudePilot()
        {
            if (!ValidReference(activeReference))
            {
                return false;
            }
            attitudePilotEngaged = true;

            return true;
        }

        #endregion

        #region Maneuver Interface

        /// <summary>
        /// Engage the attitude pilot to hold heading on the maneuver node.  Simultaneously
        /// engage the maneuver pilot to handle maneuver.
        /// </summary>
        /// <returns>True if the pilot can be engaged, false otherwise.</returns>
        public bool EngageManeuverPilot()
        {
            // TODO: VALIDATION
            if (!ValidReference(ReferenceAttitude.REF_MANEUVER_NODE))
            {
                return false;
            }

            lockOrientation = false;

            activeReference = ReferenceAttitude.REF_MANEUVER_NODE;
            relativeHPR = Vector3.zero;
            orientation = Quaternion.identity; // Updated during FixedUpdate

            attitudePilotEngaged = true;
            ascentPilotEngaged = false;
            maneuverPilotEngaged = true;

            return true;
        }

        #endregion

        #region Internals

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
        /// Ensure SAS is configured the way we expect/require.
        /// </summary>
        /// <param name="mode">The SAS mode we want.</param>
        private void TrySetSASMode(VesselAutopilot.AutopilotMode mode)
        {
            if (vessel.Autopilot.Mode != mode && vessel.Autopilot.CanSetMode(mode))
            {
                vessel.Autopilot.SetMode(mode);

                if (SASbtns == null)
                {
                    SASbtns = UnityEngine.Object.FindObjectOfType<VesselAutopilotUI>().modeButtons;
                }
                // set our mode, note it takes the mode as an int, generally top to bottom, left to right, as seen on the screen. Maneuver node being the exception, it is 9
                SASbtns[(int)mode].SetState(true);
            }
        }

        /// <summary>
        /// Make sure SAS is in the right mode for how we're configured.
        /// </summary>
        /// <returns>true if additional steps may be taken.</returns>
        private bool SetMode()
        {
            // Special cases - just use the stock SAS configuration.
            if (lockOrientation == false)
            {
                if (activeReference == ReferenceAttitude.REF_MANEUVER_NODE)
                {
                    TrySetSASMode(VesselAutopilot.AutopilotMode.Maneuver);

                    return false;
                }
                else if (activeReference == ReferenceAttitude.REF_TARGET)
                {
                    TrySetSASMode(VesselAutopilot.AutopilotMode.Target);

                    return false;
                }
            }

            // General cases.
            TrySetSASMode(VesselAutopilot.AutopilotMode.StabilityAssist);

            return true;
        }

        /// <summary>
        /// Find the ancestral orbit that circles the sun.
        /// </summary>
        /// <param name="startOrbit"></param>
        /// <returns></returns>
        static private Orbit FindSolarOrbit(Orbit startOrbit)
        {
            Orbit result = startOrbit;
            while (result.referenceBody != Planetarium.fetch.Sun)
            {
                result = result.referenceBody.orbit;
            }

            return result;
        }

        /// <summary>
        /// Compute the reference orientation for the given reference attitude.
        /// </summary>
        /// <param name="reference">The attitude we care about</param>
        /// <returns>Quaternion representing the orientation.</returns>
        private Quaternion GetReferenceOrientation(ReferenceAttitude reference)
        {
            Vector3 fwd, up;
            Quaternion referenceOrientation = Quaternion.identity;

            switch (reference)
            {
                case ReferenceAttitude.REF_INERTIAL:
                    referenceOrientation = Quaternion.identity;
                    break;

                case ReferenceAttitude.REF_ORBIT_PROGRADE:
                    referenceOrientation = Quaternion.LookRotation(vessel.obt_velocity.normalized, vessel.up);
                    break;

                case ReferenceAttitude.REF_ORBIT_HORIZONTAL:
                    up = vessel.up;
                    referenceOrientation = Quaternion.LookRotation(Vector3.ProjectOnPlane(vessel.obt_velocity.normalized, up), up);
                    break;

                case ReferenceAttitude.REF_SURFACE_PROGRADE:
                    referenceOrientation = Quaternion.LookRotation(vessel.srf_vel_direction, vessel.up);
                    break;

                case ReferenceAttitude.REF_SURFACE_HORIZONTAL:
                    up = vessel.up;
                    referenceOrientation = Quaternion.LookRotation(Vector3.ProjectOnPlane(up, vessel.obt_velocity.normalized), up);
                    break;

                case ReferenceAttitude.REF_SURFACE_NORTH:
                    referenceOrientation = Quaternion.LookRotation(vessel.north, vessel.up);
                    break;

                case ReferenceAttitude.REF_TARGET:
                    fwd = (activeTarget.GetTransform().position - vessel.GetTransform().position).normalized;
                    up = Vector3.Cross(fwd, vessel.orbit.GetOrbitNormal());
                    Vector3.OrthoNormalize(ref fwd, ref up);
                    referenceOrientation = Quaternion.LookRotation(fwd, up);
                    break;

                case ReferenceAttitude.REF_TARGET_RELATIVE_VEL:
                    fwd = (vessel.obt_velocity - activeTarget.GetObtVelocity()).normalized;
                    up = Vector3.Cross(fwd, vessel.orbit.GetOrbitNormal());
                    Vector3.OrthoNormalize(ref fwd, ref up);
                    referenceOrientation = Quaternion.LookRotation(fwd, up);
                    break;

                case ReferenceAttitude.REF_TARGET_ORIENTATION:
                    if ((activeTarget is Vessel) || (activeTarget is ModuleDockingNode))
                    {
                        referenceOrientation = Quaternion.LookRotation(activeTarget.GetTransform().forward, activeTarget.GetTransform().up);
                    }
                    else
                    {
                        referenceOrientation = Quaternion.LookRotation(activeTarget.GetTransform().up, activeTarget.GetTransform().right);
                    }
                    break;

                case ReferenceAttitude.REF_MANEUVER_NODE:
                    fwd = node.GetBurnVector(vessel.orbit);
                    up = Vector3.Cross(fwd, vessel.orbit.GetOrbitNormal());
                    Vector3.OrthoNormalize(ref fwd, ref up);
                    referenceOrientation = Quaternion.LookRotation(fwd, up);
                    break;

                case ReferenceAttitude.REF_SUN:
                    Orbit baseOrbit = FindSolarOrbit(vessel.orbit);
                    fwd = (Planetarium.fetch.Sun.transform.position - vessel.CoM).normalized;
                    up = baseOrbit.GetOrbitNormal();
                    Vector3.OrthoNormalize(ref fwd, ref up);
                    referenceOrientation = Quaternion.LookRotation(fwd, up);
                    break;
            }

            return referenceOrientation;
        }

        // Normalize the angle to the range (-180, 180], and return the absolute value of the
        // result.
        static float NormalizeAngle(float angle)
        {
            angle = Mathf.Abs(angle);

            return (angle > 180.0f) ? -(angle - 360.0f) : angle;
        }

        float currentAttitudeXVel = 0.0f;
        float currentAttitudeYVel = 0.0f;
        float currentAttitudeZVel = 0.0f;
        /// <summary>
        /// Attitude pilot update method.
        /// </summary>
        private void UpdateHeading()
        {
            Quaternion referenceRotation = GetReferenceOrientation(activeReference);

            if (!lockOrientation)
            {
                Vector3 forward = Vector3.forward;
                Vector3 up = Quaternion.Inverse(referenceRotation) * (-vessel.GetTransform().forward);
                Vector3.OrthoNormalize(ref forward, ref up);

                orientation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(90, 0, 0);
            }

            // Where we do want to point?
            Quaternion requestedAttitude = referenceRotation * orientation;
            Vector3 requestedEuler = requestedAttitude.eulerAngles;

            // Where do we point now?
            Quaternion currentOrientation = vessel.Autopilot.SAS.lockedRotation;
            Vector3 currentEuler = currentOrientation.eulerAngles;

            float xError = NormalizeAngle(requestedEuler.x - currentEuler.x);
            float yError = NormalizeAngle(requestedEuler.y - currentEuler.y);
            float zError = NormalizeAngle(requestedEuler.z - currentEuler.z);
            if (xError > 0.500f || yError > 0.500f || zError > 1.000f)
            {
                float smoothTime = 0.35f;
                // Roll tends to overshoot.
                float rollMaxV = (zError > 1.0f) ? 60.0f : 30.0f;

                float newX = Mathf.SmoothDampAngle(currentEuler.x, requestedEuler.x, ref currentAttitudeXVel, smoothTime, 60.0f, Time.fixedDeltaTime);
                float newY = Mathf.SmoothDampAngle(currentEuler.y, requestedEuler.y, ref currentAttitudeYVel, smoothTime, 60.0f, Time.fixedDeltaTime);
                float newZ = Mathf.SmoothDampAngle(currentEuler.z, requestedEuler.z, ref currentAttitudeZVel, smoothTime, rollMaxV, Time.fixedDeltaTime);

                requestedAttitude = Quaternion.Euler(newX, newY, newZ);
            }
            else
            {
                currentAttitudeXVel = currentAttitudeYVel = currentAttitudeZVel = 0.0f;
            }

            vessel.Autopilot.SAS.LockRotation(requestedAttitude);
        }

        /// <summary>
        /// Construct the attitude pilot state machine.
        /// </summary>
        private void InitAttitudeFSM()
        {
            KFSMState idleState = new KFSMState("Attitude-Idle");
            idleState.updateMode = KFSMUpdateMode.FIXEDUPDATE;

            KFSMState holdAttitudeState = new KFSMState("Attitude-Hold");
            holdAttitudeState.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            holdAttitudeState.OnEnter = (KFSMState fromState) =>
            {
                currentAttitudeXVel = currentAttitudeYVel = currentAttitudeZVel = 0.0f;
            };
            holdAttitudeState.OnFixedUpdate = () =>
            {
                // We need to check attitudePilotEngaged because the state machine
                // event doesn't seem to fire before the state's update fires.
                // Ditto with ValidReference - if the player canceled the maneuver
                // node, we may trigger an NRE.
                if (attitudePilotEngaged && ValidReference(activeReference) && SetMode())
                {
                    UpdateHeading();
                }
            };

            KFSMEvent engageEvent = new KFSMEvent("AttitudeEv-Engage");
            engageEvent.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            engageEvent.OnCheckCondition = (KFSMState currentState) =>
            {
                if (attitudePilotEngaged == true)
                {
                    if (!ValidReference(activeReference))
                    {
                        Utility.LogWarning(this, "Not engaging pilot - {0} is not currently a valid reference vector.", activeReference);
                        attitudePilotEngaged = false;

                        return false;
                    }

                    // Other checks here?

                    vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);

                    return true;
                }
                else
                {
                    return false;
                }
            };
            engageEvent.GoToStateOnEvent = holdAttitudeState;
            //
            idleState.AddEvent(engageEvent);

            KFSMEvent cancelEvent = new KFSMEvent("AttitudeEv-Cancel");
            cancelEvent.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            cancelEvent.OnCheckCondition = (KFSMState currentState) =>
            {
                bool stopThisPilot = (attitudePilotEngaged == false || vessel.Autopilot.Enabled == false || (!ValidReference(activeReference)));
                if (stopThisPilot)
                {
                    Utility.LogWarning(this, "Attitude Pilot canceling - attitudePilot = {0}, sas = {2}, validRef = {1}", attitudePilotEngaged, ValidReference(activeReference), vessel.Autopilot.Enabled);
                    DisengageAutopilots();
                }

                return stopThisPilot;
            };
            cancelEvent.GoToStateOnEvent = idleState;
            //
            holdAttitudeState.AddEvent(cancelEvent);

            attitudePilot.AddState(idleState);
            attitudePilot.AddState(holdAttitudeState);

            attitudePilot.StartFSM(idleState);
        }

        private double startDeltaV = 0.0;
        float currentThrottleVel = 0.0f;
        private void Maneuver()
        {
            double remainingDeltaV = node.GetBurnVector(vessel.orbit).magnitude;
            if (remainingDeltaV < 0.15)
            {
                maneuverPilotEngaged = false;

                vessel.patchedConicSolver.maneuverNodes.Clear();

                FlightInputHandler.state.mainThrottle = 0.0f;
            }
            else
            {
                float currentThrottle = vessel.ctrlState.mainThrottle;

                float goalThrottle = 1.0f;

                //Utility.LogMessage(this, "Updating throttle:");

                // Are we way off-axis?
                float headingErrorDot = Mathf.Clamp01(Vector3.Dot(vessel.GetTransform().up, node.GetBurnVector(vessel.orbit).normalized) + 0.01f);
                if (headingErrorDot < 1.0f)
                {
                    float constraint = headingErrorDot * headingErrorDot;
                    goalThrottle = Mathf.Min(constraint, goalThrottle);
                    //Utility.LogMessage(this, "Constraint due to heading error: {0:0.00} because headingErrorDot = {1:0.00}", constraint, headingErrorDot);
                }

                float remainingDvPercent = (float)(remainingDeltaV / startDeltaV);
                if (remainingDvPercent < 0.1f)
                {
                    float constraint = remainingDvPercent * 10.0f;
                    goalThrottle = Mathf.Min(constraint, goalThrottle);
                    //Utility.LogMessage(this, "Constraint due to dV percent: {0:0.00} because remaining dV % = {1:0.00}", constraint, remainingDvPercent);
                }

                float newThrottle = Mathf.SmoothDamp(currentThrottle, goalThrottle, ref currentThrottleVel, 0.15f);
                //Utility.LogMessage(this, "Adjusting throttle from {0:0.00} to {1:0.00}", currentThrottle, newThrottle);
                FlightInputHandler.state.mainThrottle = newThrottle;
            }
        }

        // Store the burn start time.  Initialize to the maneuver node time, then use stage Isp
        // and thrust to refince the value.
        private double burnStartUT = 0.0;

        // The stock delta-V code doesn't like being hammered frequently.  So, pace the queries
        // using a 5 second interval.  Realistically, unless the player is messing around with
        // starting / stopping engines (changing Isp or max thrust), the burn time should be
        // fairly invariant once it's computed.
        private double lastBurnStartCheck = 0.0;

        /// <summary>
        /// Initialize the Maneuver pilot finite state machine.
        /// </summary>
        private void InitManeuverFSM()
        {
            KFSMState idleState = new KFSMState("Maneuver-Idle");
            idleState.updateMode = KFSMUpdateMode.FIXEDUPDATE;

            KFSMState coastState = new KFSMState("Maneuver-Coast");
            coastState.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            coastState.OnEnter = (KFSMState fromState) =>
            {
                burnStartUT = node.UT;
                lastBurnStartCheck = 0.0;
            };
            coastState.OnFixedUpdate = () =>
            {
                if (maneuverPilotEngaged && node != null && Planetarium.GetUniversalTime() - lastBurnStartCheck > 5.0 * TimeWarp.CurrentRate)
                {
                    VesselDeltaV vdV = vessel.VesselDeltaV;
                    if (vdV.IsReady)
                    {
                        List<DeltaVStageInfo> stageInfo = vdV.OperatingStageInfo;
                        if (stageInfo.Count > 0)
                        {
                            float currentMaxThrust = stageInfo[0].thrustActual;

                            if (currentMaxThrust > 0.0f)
                            {
                                double currentIsp = stageInfo[0].ispActual;
                                double deltaV = node.DeltaV.magnitude;
                                double burnTime = currentIsp * (1.0 - Math.Exp(-deltaV / currentIsp / PhysicsGlobals.GravitationalAcceleration)) / (currentMaxThrust / (vessel.totalMass * PhysicsGlobals.GravitationalAcceleration));

                                burnStartUT = node.UT - 0.5 * burnTime;

                                lastBurnStartCheck = Planetarium.GetUniversalTime();
                            }
                        }
                    }

                }
            };

            KFSMState flyState = new KFSMState("Maneuver-Fly");
            flyState.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            flyState.OnEnter = (KFSMState fromState) =>
            {
                startDeltaV = Math.Max(node.DeltaV.magnitude, 0.01);
                currentThrottleVel = 0.0f;
            };
            flyState.OnFixedUpdate = () =>
            {
                if (maneuverPilotEngaged && node != null)
                {
                    Maneuver();
                }
            };

            KFSMEvent startEvent = new KFSMEvent("ManeuverEv-Start");
            startEvent.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            startEvent.OnCheckCondition = (KFSMState currentState) =>
            {
                if (maneuverPilotEngaged && node != null)
                {
                    Utility.LogMessage(this, "Maneuver Pilot starting");
                    return true;
                }

                return false;
            };
            startEvent.GoToStateOnEvent = coastState;
            //
            idleState.AddEvent(startEvent);

            KFSMEvent cancelEvent = new KFSMEvent("ManeuverEv-Cancel");
            cancelEvent.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            cancelEvent.OnCheckCondition = (KFSMState currentState) =>
            {
                bool stopThisPilot = (attitudePilotEngaged == false || maneuverPilotEngaged == false || vessel.Autopilot.Enabled == false || node == null);
                if (stopThisPilot)
                {
                    maneuverPilotEngaged = false;
                    FlightInputHandler.state.mainThrottle = 0.0f;
                    Utility.LogMessage(this, "Maneuver Pilot canceling");
                }

                return stopThisPilot;
            };
            cancelEvent.GoToStateOnEvent = idleState;
            //
            coastState.AddEvent(cancelEvent);
            flyState.AddEvent(cancelEvent);

            KFSMEvent flyEvent = new KFSMEvent("ManeuverEv-Fly");
            flyEvent.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            flyEvent.OnCheckCondition = (KFSMState currentState) =>
            {
                if (maneuverPilotEngaged && node != null && Planetarium.GetUniversalTime() > burnStartUT)
                {
                    Utility.LogMessage(this, "Time to maneuver");
                    return true;
                }

                return false;
            };
            flyEvent.GoToStateOnEvent = flyState;
            //
            coastState.AddEvent(flyEvent);

            maneuverPilot.AddState(idleState);
            maneuverPilot.AddState(coastState);
            maneuverPilot.AddState(flyState);

            maneuverPilot.StartFSM(idleState);
        }

        /// <summary>
        /// Initialize the Ascent Autopilot FSM
        /// </summary>
        private void InitAscentFSM()
        {
            KFSMState idleState = new KFSMState("Ascent-Idle");
            idleState.updateMode = KFSMUpdateMode.FIXEDUPDATE;

            KFSMState clearTowerState = new KFSMState("Ascent-ClearLaunchTower");
            clearTowerState.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            clearTowerState.OnFixedUpdate = () =>
            {
                FlightInputHandler.state.mainThrottle = 1.0f;
            };

            KFSMState verticalAscentState = new KFSMState("Ascent-VerticalAscent");
            verticalAscentState.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            verticalAscentState.OnEnter = (KFSMState fromState) =>
            {
                relativeHPR = new Vector3(this.heading, 89.0f, this.roll);
                orientation = Quaternion.AngleAxis(relativeHPR.x, Vector3.up) * Quaternion.AngleAxis(-relativeHPR.y, Vector3.right) * Quaternion.AngleAxis(-relativeHPR.z, Vector3.forward) * Quaternion.Euler(90, 0, 0);
                FlightInputHandler.state.mainThrottle = 1.0f;
            };

            KFSMState pitchManeuverState = new KFSMState("Ascent-PitchManeuver");
            pitchManeuverState.updateMode = KFSMUpdateMode.FIXEDUPDATE;

            KFSMState gravityTurnState = new KFSMState("Ascent-GravityTurn");
            gravityTurnState.updateMode = KFSMUpdateMode.FIXEDUPDATE;

            KFSMState coastToAtmState = new KFSMState("Ascent-CoastToAtmosphere");
            coastToAtmState.updateMode = KFSMUpdateMode.FIXEDUPDATE;

            KFSMEvent startEvent = new KFSMEvent("AscentEv-Start");
            startEvent.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            startEvent.OnCheckCondition = (KFSMState currentState) =>
            {
                if (ascentPilotEngaged)
                {
                    Utility.LogMessage(this, "Ascent Pilot engaged - waiting to clear the tower");
                    return true;
                }

                return false;
            };
            startEvent.GoToStateOnEvent = clearTowerState;
            //
            idleState.AddEvent(startEvent);

            KFSMEvent vertAscentEvent = new KFSMEvent("AscentEv-StartVertAscent");
            vertAscentEvent.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            vertAscentEvent.OnCheckCondition = (KFSMState currentState) =>
            {
                if (vessel.altitude - vessel.terrainAltitude > 100.0)
                {
                    Utility.LogMessage(this, "Tower Cleared - Starting Vertical Ascent");
                    return true;
                }
                return false;
            };
            vertAscentEvent.GoToStateOnEvent = verticalAscentState;
            //
            clearTowerState.AddEvent(vertAscentEvent);

            KFSMEvent startPitchMnvrEvent = new KFSMEvent("AscentEv-StartPitchMnvr");
            startPitchMnvrEvent.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            startPitchMnvrEvent.OnCheckCondition = (KFSMState currentState) =>
            {
                if (vessel.altitude - vessel.terrainAltitude > 1000.0)
                {
                    Utility.LogMessage(this, "Starting Pitch Maneuver");
                    return true;
                }
                return false;
            };
            startPitchMnvrEvent.GoToStateOnEvent = pitchManeuverState;
            //
            verticalAscentState.AddEvent(startPitchMnvrEvent);

            KFSMEvent cancelEvent = new KFSMEvent("AscentEv-Cancel");
            cancelEvent.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            cancelEvent.OnCheckCondition = (KFSMState currentState) =>
            {
                bool stopThisPilot = (attitudePilotEngaged == false || ascentPilotEngaged == false || vessel.Autopilot.Enabled == false);
                if (stopThisPilot)
                {
                    Utility.LogWarning(this, "Ascent Pilot canceling - attitudePilot = {0}, ascentPilot = {1}, sas = {2}", attitudePilotEngaged, ascentPilotEngaged, vessel.Autopilot.Enabled);
                    ascentPilotEngaged = false;
                    FlightInputHandler.state.mainThrottle = 0.0f;
                }

                return stopThisPilot;
            };
            cancelEvent.GoToStateOnEvent = idleState;
            //
            clearTowerState.AddEvent(cancelEvent);
            verticalAscentState.AddEvent(cancelEvent);
            pitchManeuverState.AddEvent(cancelEvent);
            gravityTurnState.AddEvent(cancelEvent);
            coastToAtmState.AddEvent(cancelEvent);

            ascentPilot.AddState(idleState);
            ascentPilot.AddState(clearTowerState);
            ascentPilot.AddState(verticalAscentState);
            ascentPilot.AddState(pitchManeuverState);
            ascentPilot.AddState(gravityTurnState);
            ascentPilot.AddState(coastToAtmState);

            ascentPilot.StartFSM(idleState);
        }

        #endregion

        #region Game Events

        public void Awake()
        {
            // "constructor"

            relativeHPR = Vector3.zero;
            // Pick something that might be innocuous.
            activeReference = ReferenceAttitude.REF_ORBIT_PROGRADE;

            InitAttitudeFSM();
            InitAscentFSM();
            InitManeuverFSM();
        }

        //public void Start()
        //{
        //Utility.LogMessage(this, "Start()");
        // Scene should be initialized.
        //}

        public void FixedUpdate()
        {
            // Updating.  Refresh what we know.
            node = (vessel.patchedConicSolver != null && vessel.patchedConicSolver.maneuverNodes.Count > 0) ? vessel.patchedConicSolver.maneuverNodes[0] : null;
            activeTarget = FlightGlobals.fetch.VesselTarget;

            attitudePilot.FixedUpdateFSM();
            maneuverPilot.FixedUpdateFSM();
            ascentPilot.FixedUpdateFSM();
        }

        public void OnDestroy()
        {
            // Tear down.
            DisengageAutopilots();
        }

        #endregion
    }
}
