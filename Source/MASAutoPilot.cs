//#define DEBUG_REGISTERS
//#define ASCENT_PILOT
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
        public static readonly MASAutoPilot.ReferenceAttitude[] referenceAttitudes =
        {
            MASAutoPilot.ReferenceAttitude.REF_INERTIAL,
            MASAutoPilot.ReferenceAttitude.REF_ORBIT_PROGRADE,
            MASAutoPilot.ReferenceAttitude.REF_ORBIT_HORIZONTAL,
            MASAutoPilot.ReferenceAttitude.REF_SURFACE_PROGRADE,
            MASAutoPilot.ReferenceAttitude.REF_SURFACE_HORIZONTAL,
            MASAutoPilot.ReferenceAttitude.REF_SURFACE_NORTH,
            MASAutoPilot.ReferenceAttitude.REF_TARGET,
            MASAutoPilot.ReferenceAttitude.REF_TARGET_RELATIVE_VEL,
            MASAutoPilot.ReferenceAttitude.REF_TARGET_ORIENTATION,
            MASAutoPilot.ReferenceAttitude.REF_MANEUVER_NODE,
            MASAutoPilot.ReferenceAttitude.REF_SUN,
            MASAutoPilot.ReferenceAttitude.REF_UP,
        };

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
            REF_UP,
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
        private float inclination, heading, roll;

        /// <summary>
        /// Is the MAS ascent pilot doing something?
        /// </summary>
        public bool ascentPilotEngaged { get; private set; }

        /// <summary>
        /// State machine to manage the ascent pilot module.
        /// </summary>
#if ASCENT_PILOT
        private KerbalFSM ascentPilot = new KerbalFSM();
#endif

        private KSP.UI.Screens.Flight.NavBall navBall;

        /// <summary>
        /// A representation of the maximum deflection from prograde that is allowed.
        /// 
        /// TODO: The full explanation for documenting once this becomes a usable field.
        /// </summary>
        public float Qalpha = 1.0f;

        /// <summary>
        /// Minimum allowable throttle.
        /// </summary>
        public float minThrottle = 0.10f;

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
        public Vector3 relativeHPR { get { return _relativeHPR; } }
        private Vector3 _relativeHPR = Vector3.zero;

        /// <summary>
        /// Reference to the UI buttons that display the current SAS mode, so we can keep
        /// them updated.
        /// </summary>
        private UIStateToggleButton[] SASbtns = null;

        /// <summary>
        /// Were we asked to hold heading, pitch, and roll (or even just roll) relative to the reference vector?
        /// </summary>
        private bool lockRoll = false;

        /// <summary>
        /// Is the current HPR 0, 0, 0, and is lockRoll false?
        /// </summary>
        private bool zeroOffset = true;

        /// <summary>
        /// Quaternion representing the desired orientation relative to the vector.
        /// </summary>
        private Quaternion orientation = Quaternion.identity;

#if DEBUG_REGISTERS
        private MASVesselComputer vc;
#endif

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
            currentAttitudeVel = 0.0f;
            currentThrottleVel = 0.0f;
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
            vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);

            inclination = Utility.NormalizeLongitude(inclination);
            roll = Utility.NormalizeLongitude(roll);

            this.apoapsis = apoapsis;
            this.periapsis = periapsis;
            this.inclination = (float)inclination;
            this.heading = 90.0f - this.inclination;
            this.roll = (float)roll;

            lockRoll = false;
            zeroOffset = false;

            activeReference = ReferenceAttitude.REF_UP;
            _relativeHPR = new Vector3(0.0f, 0.0f, 0.0f);
            orientation = HPRtoQuaternion(relativeHPR);

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

            lockRoll = true;
            zeroOffset = false;

            activeReference = reference;
            _relativeHPR = HPR;
            orientation = Quaternion.AngleAxis(relativeHPR.x, Vector3.up) * Quaternion.AngleAxis(-relativeHPR.y, Vector3.right) * Quaternion.AngleAxis(-relativeHPR.z, Vector3.forward) * Quaternion.Euler(90, 0, 0);
            vessel.Autopilot.SAS.LockRotation(vessel.ReferenceTransform.rotation);

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
        /// <param name="yaw">The yaw (heading) to maintain relative to the reference attitude.</param>
        /// <param name="pitch">The pitch to maintain relative to the reference attitude.</param>
        /// <returns>true if engaged, false otherwise.</returns>
        public bool EngageAttitudePilot(ReferenceAttitude reference, float yaw, float pitch)
        {
            if (!ValidReference(reference))
            {
                return false;
            }

            lockRoll = false;

            activeReference = reference;
            _relativeHPR.x = yaw;
            _relativeHPR.y = pitch;
            _relativeHPR.z = 0.0f;
            zeroOffset = Vector3.Dot(_relativeHPR, _relativeHPR) == 0.0f;

            orientation = Quaternion.identity; // Updated during FixedUpdate
            vessel.Autopilot.SAS.LockRotation(vessel.ReferenceTransform.rotation);

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

            lockRoll = false;
            zeroOffset = true;

            activeReference = ReferenceAttitude.REF_MANEUVER_NODE;
            _relativeHPR = Vector3.zero;
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
            if (lockRoll == false && zeroOffset)
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

                case ReferenceAttitude.REF_UP:
                    fwd = vessel.up;
                    up = vessel.north;
                    Vector3.OrthoNormalize(ref fwd, ref up);
                    referenceOrientation = Quaternion.LookRotation(fwd, up);
                    break;
            }

            return referenceOrientation;
        }

        /// <summary>
        /// Convert the HPR vector (heading(yaw) - pitch - roll) to a quaternion suitable for
        /// SAS orientation.  Applies the neccessary (but why, again?) 90 degree rotation on the
        /// x-axis.
        /// </summary>
        /// <param name="hpr">The HPR vector</param>
        /// <returns>The quaternion</returns>
        static Quaternion HPRtoQuaternion(Vector3 hpr)
        {
            return Quaternion.AngleAxis(hpr.x, Vector3.up) * Quaternion.AngleAxis(-hpr.y, Vector3.right) * Quaternion.AngleAxis(-hpr.z, Vector3.forward) * Quaternion.Euler(90, 0, 0);
        }

        float currentAttitudeVel = 0.0f;
        //float lastError = 0.0f;
        /// <summary>
        /// Attitude pilot update method.
        /// 
        /// This method is the core of the attitude controller.  It determines what the target heading is,
        /// and it compares that heading to the current vessel heading.  Based on that information,
        /// it uses a heuristic of some sort to determine what the updated heading sent to SAS should be.
        /// 
        /// The heuristic is the tricky part.  Here are the approaches I tried:
        /// 
        /// * Fixed Slerp between current and requested.  Works okay, but it tends to overshoot, since the
        ///   initial impulse imparts a lot of momentum.  Last setting I tried was to blend 95% current / 5% goal.
        /// * Variable Slerp between an upper and lower bound based on current error.  Also okay, but still
        ///   overshoots for the same reason.
        /// * Using Mathf.SmoothDampAngle() - this seems to give me the best overall control, since there are
        ///   plenty of tuning options.  Further experiments with it:
        /// * Don't mess with currentAttitudeVel once underway.  Set it to 0 when starting the alignment process,
        ///   and let SmoothDampAngle own the value afterwards.  Since all of the angles computed here are non-negative,
        ///   and currentAttitudeVel is negative (I'm converging to 0), I tried setting it to a positive value to see
        ///   if it would rebound more aggressively, and it stopped working, instead.
        /// * Fixed maxSpeed will either cause overshoot (if high) or a very slow convergence (if low).
        /// * Varying maxSpeed based on the current error works well until convergence.  Scaling maxSpeed as
        ///   1/2 of the error tends to be slow for large errors, but using maxSpeed * 1 seems to be a good option.
        /// * Near convergence, maxSpeed needs a floor.  1.5 is too small, 2.5 seems okay.  Need to try larger
        ///   values once I've nailed down smoothTime.
        /// * A fixed smoothTime seems to work okay using 0.35f, except recovery from overshoot is slow.
        /// * Using 0.35f for convergence, and 0.10f for divergence seems to improve the rebound behavior, but
        ///   once the rebound is complete, the convergence slows due to the relaxed value.
        /// * 0.10f overall seems to work.
        /// 
        /// Once on-axis (or close thereto, there seems to be occasional hiccups of the SAS system that cause the vessel
        /// to jump off-axis.  I'm not sure if it's because I'm using floats in the computations (and hitting a numeric
        /// precision error), or if the SAS system gets twitchy with continuous small updates.
        /// 
        /// * next test: lock to requested attitude once the error / velocity are small
        /// </summary>
        private void UpdateHeading()
        {
            // Where we do want to point?
            Quaternion referenceRotation = GetReferenceOrientation(activeReference);

            // First, let's get the quaternion that represents the desired heading *without*
            // a locked roll.
            Vector3 forward = Quaternion.AngleAxis(relativeHPR.x, Vector3.up) * Quaternion.AngleAxis(-relativeHPR.y, Vector3.right) * Vector3.forward;
            Vector3 up = Quaternion.Inverse(referenceRotation) * (-vessel.GetTransform().forward);
            Vector3.OrthoNormalize(ref forward, ref up);

            Quaternion unrolledRequestedAttitude = referenceRotation * Quaternion.LookRotation(forward, up) * Quaternion.Euler(90.0f, 0.0f, 0.0f);

            // Then, let's determine the 'real' attitude - which is the same as the
            // unrolled attitude if roll is unlocked.
            Quaternion requestedAttitude;
            if (lockRoll)
            {
                requestedAttitude = referenceRotation * orientation;
            }
            else
            {
                requestedAttitude = unrolledRequestedAttitude;
            }

            // Where do we currently point?
            Quaternion currentAttitude = vessel.ReferenceTransform.rotation;

            // Overall error is the angle between the current attitude and the requested attitude,
            // accounting for roll.
            float overallError = Quaternion.Angle(currentAttitude, requestedAttitude);

            // yaw/pitch error is the angle between the current heading and the target heading,
            // ignoring any roll errors.
            float yawPitchError = Quaternion.Angle(currentAttitude, unrolledRequestedAttitude);

            // rollError infers the roll error by storing the difference between overallError and yawPitchError.
            float rollError = overallError - yawPitchError;

            // +++ TUNING PARAMETERS
            // Controls the spring tension - smaller values will increase the rate of change of
            // the attitude.
            float smoothTime = 0.25f;

            // At what error / velocity do we lock to the requested attitude
            // instead of continue applying the damper.  Note that attitude
            // velocity is negative!
            float minErrorToLock = 1.5f;
            float minRollToLock = 7.5f;
            float maxAttVelToLock = -2.0f;

            // What is the minimum max speed we'll allow?
            float minMaxSpeed = 2.5f;
            // Letting a big ship spin is bad for stability...
            // I think the minMaxSpeed should be based on the moment of inertia and
            // the available torque forces (that's what MJ does).
            //float minMaxSpeed = Mathf.Max(2.5f, rollError);

            // Divergence from heading.  At what point do we try something more aggressive?
            //float netErrorRate = 0.0f;
            //if (yawPitchError > lastError)
            //{
            //    netErrorRate = (yawPitchError - lastError) / TimeWarp.fixedDeltaTime;
            //}
            //lastError = yawPitchError;
#if DEBUG_REGISTERS
            //vc.debugValue[3] = (double)netErrorRate;
#endif
            // TODO: Need a different guidance control system for launch.  The springs aren't
            // cutting it.

            // Determine what heading to apply to SAS.
            if (yawPitchError > minErrorToLock || rollError > minRollToLock || currentAttitudeVel < maxAttVelToLock)
            {
                float newError = Mathf.SmoothDampAngle(overallError, 0.0f, ref currentAttitudeVel, smoothTime, Mathf.Max(minMaxSpeed, overallError), TimeWarp.fixedDeltaTime);
                requestedAttitude = Quaternion.Slerp(requestedAttitude, currentAttitude, newError / overallError);
#if DEBUG_REGISTERS
                vc.debugValue[2] = "DAMP";
#endif
            }
#if DEBUG_REGISTERS
            else
            {
                vc.debugValue[2] = "LOCK";
            }

            vc.debugValue[1] = (double)overallError;
#endif

            vessel.Autopilot.SAS.LockRotation(requestedAttitude);
        }

        #region Attitude FSM Init
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
                currentAttitudeVel = 0.0f;
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
                        //Utility.LogWarning(this, "Not engaging pilot - {0} is not currently a valid reference vector.", activeReference);
                        attitudePilotEngaged = false;

                        return false;
                    }

                    // Other checks here?

                    vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                    Utility.LogMessage(this, "Attitude pilot engaged...");

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
                    //Utility.LogWarning(this, "Attitude Pilot canceling - attitudePilot = {0}, sas = {2}, validRef = {1}", attitudePilotEngaged, ValidReference(activeReference), vessel.Autopilot.Enabled);
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
        #endregion

        private double startDeltaV = 0.0;
        private float currentThrottleVel = 0.0f;
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

                float newThrottle = Mathf.SmoothDamp(currentThrottle, goalThrottle, ref currentThrottleVel, 0.20f);
                //Utility.LogMessage(this, "Adjusting throttle from {0:0.00} to {1:0.00}", currentThrottle, newThrottle);
                FlightInputHandler.state.mainThrottle = newThrottle;
            }
        }

        #region Maneuver FSM Init
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
                currentThrottleVel = 0.0f;
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
        #endregion

#if ASCENT_PILOT
        void FlyVerticalAscent()
        {
            // TODO: Steer prograde towards vertical
            //Utility.LogMessage(this, "Vertical ascent...");
            FlightInputHandler.state.mainThrottle = 1.0f;
        }

        float minTimeToAp = 44.0f;
        float maxTimeToAp = 45.0f;
        int activeStage = -1;
        void FlyGravityTurn()
        {
            if (vessel.staticPressurekPa < 1.0)
            {
                // Switch to orbital prograde.
                activeReference = ReferenceAttitude.REF_ORBIT_PROGRADE;
            }
            //Utility.LogMessage(this, "sspd {0:0}m/s, static {1:0.000}kPa, dyn {2:0.000}kPa",
            //    vessel.srfSpeed, vessel.staticPressurekPa, vessel.dynamicPressurekPa);
            float currentThrottle = FlightInputHandler.state.mainThrottle;
            float timeToAp = (float)vessel.orbit.timeToAp;

            // Heading adjust -- when do I start testing it?
            bool updateHPR = false;
            Vector3 hpr = relativeHPR;
            if (timeToAp >= 15.0f || activeReference == ReferenceAttitude.REF_ORBIT_PROGRADE)
            {
                // Check the sign -- do we not see a negative inclination during ascent?
                float currentInclination = (float)vessel.orbit.inclination;
                if (Vector3.Dot(vessel.obt_velocity, vessel.north) < 0.0f)
                {
                    //Utility.LogMessage(this, "negate inclination?");
                    currentInclination = -currentInclination;
                }
                if (Mathf.Abs(inclination - currentInclination) > 0.5f)
                {
                    updateHPR = true;
                    // Oversteer for corrective adjustment
                    //hpr.x = (inclination - currentInclination) * 1.5f;
                    hpr.x = (currentInclination - inclination) * 1.5f;
                    if (activeReference != ReferenceAttitude.REF_ORBIT_PROGRADE)
                    {
                        hpr.x = Mathf.Clamp(hpr.x, -5.0f, 5.0f);
                    }

                    //Utility.LogMessage(this, "Correcting yaw to {0:0.0} - current in = {1:0.00}, goal is {2:0.00}", hpr.x, currentInclination, inclination);
                }
            }
            // Under what conditions do I pitch up?

            if (!updateHPR && Mathf.Abs(hpr.x) > 0.0f)
            {
                updateHPR = true;
                hpr.x = 0.0f;
                Utility.LogMessage(this, "Zeroing yaw");
            }

            if (updateHPR)
            {
                _relativeHPR = hpr;
                orientation = HPRtoQuaternion(relativeHPR);
            }

            // Decide what we want to use for our throttle setting.  Try to keep Ap within 30s-45s in the future.
            // TODO: Reset to full throttle on flameout
            float goalThrottle = currentThrottle;
            if (timeToAp > maxTimeToAp)
            {
                // Absolute throttle based on time error.
                //goalThrottle = 1.0f - (timeToAp - maxTimeToAp) * 0.04f;
                // Relative throttle based on time error.
                goalThrottle = currentThrottle - (timeToAp - maxTimeToAp) * 0.01f * TimeWarp.fixedDeltaTime;
            }
            else if (timeToAp < minTimeToAp)
            {
                if (timeToAp < minTimeToAp - 10.0f)
                {
                    // Stop messing around.  Floor it.
                    goalThrottle = 1.0f;
                }
                else
                {
                    // Absolute throttle based on time error.
                    //goalThrottle = 1.0f;
                    // Relative throttle based on time error.  More aggressive throttle up than throttle down.
                    goalThrottle = currentThrottle - (timeToAp - minTimeToAp) * 0.05f * TimeWarp.fixedDeltaTime;
                }
            }

            goalThrottle = Mathf.Clamp(goalThrottle, minThrottle, 1.0f);

            //Utility.LogMessage(this, "accel: {0:0.00}", (vessel.acceleration - vessel.graviticAcceleration).magnitude);
            float newThrottle = Mathf.SmoothDamp(currentThrottle, goalThrottle, ref currentThrottleVel, 0.15f);
            if (activeStage != vessel.currentStage)
            {
                //Utility.LogMessage(this, "Staging");
                newThrottle = 1.0f;
                activeStage = vessel.currentStage;
            }
            //Utility.LogMessage(this, "Adjusting throttle from {0:0.00} to {1:0.00}", currentThrottle, newThrottle);
            FlightInputHandler.state.mainThrottle = newThrottle;
        }

        #region Ascent FSM Init
        double verticalAscentAltitude = 250.0;
        /// <summary>
        /// Initialize the Ascent Autopilot FSM
        /// </summary>
        private void InitAscentFSM()
        {
            KFSMState idleState = new KFSMState("Ascent-Idle");
            idleState.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            idleState.OnEnter = (KFSMState fromState) =>
            {
#if DEBUG_REGISTERS
                vc.debugValue[0] = "Idle";
#endif
            };

            KFSMState clearTowerState = new KFSMState("Ascent-ClearLaunchTower");
            clearTowerState.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            clearTowerState.OnEnter = (KFSMState fromState) =>
            {
#if DEBUG_REGISTERS
                vc.debugValue[0] = "Clear Tower";
#endif
                verticalAscentAltitude = 250.0 + vessel.altitude - vessel.terrainAltitude;
                Utility.LogMessage(this, "Waiting to clear tower");

                _relativeHPR = Vector3.zero;
                attitudePilotEngaged = true;
                lockRoll = false;
                zeroOffset = true;
                activeReference = ReferenceAttitude.REF_UP;

                FlightInputHandler.state.mainThrottle = 1.0f;
            };
            clearTowerState.OnLeave = (KFSMState toState) =>
            {
                Utility.LogMessage(this, "Clear tower: going to {0}", toState.name);
            };
            clearTowerState.OnFixedUpdate = () =>
            {
                //Utility.LogMessage(this, "Clear tower");
                FlightInputHandler.state.mainThrottle = 1.0f;
            };

            KFSMState verticalAscentState = new KFSMState("Ascent-VerticalAscent");
            verticalAscentState.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            verticalAscentState.OnEnter = (KFSMState fromState) =>
            {
#if DEBUG_REGISTERS
                vc.debugValue[0] = "Vertical Ascent";
#endif
                activeReference = ReferenceAttitude.REF_UP;
                lockRoll = true;
                zeroOffset = false;
                _relativeHPR = new Vector3(0.0f, 0.0f, this.heading);
                Utility.LogMessage(this, "Vertical Ascent: HPR = {0:0}, {1:0}, {2:0}", relativeHPR.x, relativeHPR.y, relativeHPR.z);
                orientation = HPRtoQuaternion(relativeHPR);
                FlightInputHandler.state.mainThrottle = 1.0f;
            };
            verticalAscentState.OnFixedUpdate = () =>
            {
                if (ascentPilotEngaged)
                {
                    FlyVerticalAscent();
                }
            };

            KFSMState pitchManeuverState = new KFSMState("Ascent-PitchManeuver");
            pitchManeuverState.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            pitchManeuverState.OnEnter = (KFSMState fromState) =>
            {
#if DEBUG_REGISTERS
                vc.debugValue[0] = "Pitch Maneuver";
#endif
                activeReference = ReferenceAttitude.REF_SURFACE_NORTH;
                lockRoll = true;
                zeroOffset = false;
                // What angle?
                _relativeHPR = new Vector3(this.heading, 75.0f, this.roll);
                Utility.LogMessage(this, "Pitch Maneuver: HPR = {0:0}, {1:0}, {2:0}", relativeHPR.x, relativeHPR.y, relativeHPR.z);
                orientation = HPRtoQuaternion(relativeHPR);
                FlightInputHandler.state.mainThrottle = 1.0f;
            };
            pitchManeuverState.OnFixedUpdate = () =>
            {
                //Utility.LogMessage(this, "Pitch maneuver...");
                FlightInputHandler.state.mainThrottle = 1.0f;
            };

            KFSMState gravityTurnState = new KFSMState("Ascent-GravityTurn");
            gravityTurnState.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            gravityTurnState.OnEnter = (KFSMState fromState) =>
            {
                // Lock to surface prograde
                activeReference = ReferenceAttitude.REF_SURFACE_PROGRADE;
                lockRoll = true;
                zeroOffset = false;

                _relativeHPR = new Vector3(0.0f, 0.0f, this.roll);
                Utility.LogMessage(this, "Gravity Turn: HPR = {0:0}, {1:0}, {2:0}", relativeHPR.x, relativeHPR.y, relativeHPR.z);
                orientation = HPRtoQuaternion(relativeHPR);
                FlightInputHandler.state.mainThrottle = 1.0f;
                currentThrottleVel = 0.0f;
                activeStage = vessel.currentStage;
            };
            gravityTurnState.OnFixedUpdate = () =>
            {
                Utility.LogMessage(this, "Gravity Turn...");
                FlyGravityTurn();
            };

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
                if (vessel.altitude - vessel.terrainAltitude > verticalAscentAltitude)
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
                // TODO: Refine the pitch maneuver altitude?  Static air pressure?  vertical speed?
                if ((vessel.altitude - vessel.terrainAltitude > 1000.0 + verticalAscentAltitude) && (vessel.verticalSpeed > 125.0))
                {
                    Utility.LogMessage(this, "Starting Pitch Maneuver");
                    verticalAscentAltitude = 1000.0 + vessel.altitude - vessel.terrainAltitude;
                    return true;
                }
                return false;
            };
            startPitchMnvrEvent.GoToStateOnEvent = pitchManeuverState;
            //
            verticalAscentState.AddEvent(startPitchMnvrEvent);

            KFSMEvent startGravityTurnEvent = new KFSMEvent("AscentEv-StartGravityTurn");
            startGravityTurnEvent.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            startGravityTurnEvent.OnCheckCondition = (KFSMState currentState) =>
            {
                Vector3 surfacePrograde = Vector3.ProjectOnPlane(vessel.srf_vel_direction, vessel.upAxis).normalized;
                Vector3 right = Vector3.Cross(surfacePrograde, vessel.upAxis);

                Vector3 fwdProj = Vector3.ProjectOnPlane(vessel.srf_vel_direction, right);
                Vector3 noseProj = Vector3.ProjectOnPlane(vessel.ReferenceTransform.up, right);

                float currentPitch = Vector3.Angle(surfacePrograde, fwdProj);
                float progradePitch = Vector3.Angle(surfacePrograde, noseProj);

                // Vertical speed also?
                // Require at least 1km in Pitch Maneuver before switching to gravity turn.
                if ((vessel.altitude - vessel.terrainAltitude > verticalAscentAltitude) && (progradePitch < currentPitch))
                {
                    Utility.LogMessage(this, "Starting Gravity Turn");
                    return true;
                }

                return false;
            };
            startGravityTurnEvent.GoToStateOnEvent = gravityTurnState;
            //
            pitchManeuverState.AddEvent(startGravityTurnEvent);

            KFSMEvent initCancelEvent = new KFSMEvent("AscentEv-InitialCancel");
            initCancelEvent.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            initCancelEvent.OnCheckCondition = (KFSMState currentState) =>
            {
                bool stopThisPilot = (ascentPilotEngaged == false);
                if (stopThisPilot)
                {
                    Utility.LogWarning(this, "Ascent Pilot canceling - ascentPilot = {0}", ascentPilotEngaged);
                    ascentPilotEngaged = false;
                    FlightInputHandler.state.mainThrottle = 0.0f;
#if DEBUG_REGISTERS
                    vc.debugValue[0] = "Cancel";
#endif
                }

                return stopThisPilot;
            };
            initCancelEvent.GoToStateOnEvent = idleState;
            //
            clearTowerState.AddEvent(initCancelEvent);

            KFSMEvent cancelEvent = new KFSMEvent("AscentEv-Cancel");
            cancelEvent.updateMode = KFSMUpdateMode.FIXEDUPDATE;
            cancelEvent.OnCheckCondition = (KFSMState currentState) =>
            {
                bool stopThisPilot = (attitudePilotEngaged == false || ascentPilotEngaged == false);
                if (stopThisPilot)
                {
                    Utility.LogWarning(this, "Ascent Pilot canceling - attitudePilot = {0}, ascentPilot = {1}", attitudePilotEngaged, ascentPilotEngaged);
                    ascentPilotEngaged = false;
                    FlightInputHandler.state.mainThrottle = 0.0f;
#if DEBUG_REGISTERS
                    vc.debugValue[0] = "Cancel";
#endif
                }

                return stopThisPilot;
            };
            cancelEvent.GoToStateOnEvent = idleState;
            //
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
#endif

        #endregion

        #region Game Events

        public void Awake()
        {
            // "constructor"
#if DEBUG_REGISTERS
            vc = MASPersistent.FetchVesselComputer(FlightGlobals.ActiveVessel);
            if (vc == null)
            {
                Utility.LogError(this, "Could not fetch the MASVesselComputer");
            }
#endif
            _relativeHPR = Vector3.zero;
            // Pick something that might be innocuous.
            activeReference = ReferenceAttitude.REF_ORBIT_PROGRADE;

            InitAttitudeFSM();
#if ASCENT_PILOT
            InitAscentFSM();
#endif
            InitManeuverFSM();
        }

        public void FixedUpdate()
        {
            // Updating.  Refresh what we know.
            node = (vessel.patchedConicSolver != null && vessel.patchedConicSolver.maneuverNodes.Count > 0) ? vessel.patchedConicSolver.maneuverNodes[0] : null;
            activeTarget = FlightGlobals.fetch.VesselTarget;

            attitudePilot.FixedUpdateFSM();
            maneuverPilot.FixedUpdateFSM();
#if ASCENT_PILOT
            ascentPilot.FixedUpdateFSM();
#endif
        }

        public void OnDestroy()
        {
            // Tear down.
            DisengageAutopilots();
        }

        #endregion
    }
}
