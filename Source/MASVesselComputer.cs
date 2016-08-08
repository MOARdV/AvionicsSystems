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
using KSP.UI.Screens.Flight;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// The MASVesselComputer encompasses all of the per-vessel data tracking
    /// used in Avionics Systems.  As such, it's entirely concerned with keeping
    /// tabs on data, but not much else.
    /// </summary>
    internal partial class MASVesselComputer : VesselModule
    {
        internal enum ReferenceType
        {
            Unknown,
            Self,
            RemoteCommand,
            DockingPort,
            Claw
        };

        /// <summary>
        /// We use this dictionary to quickly fetch the vessel module for a
        /// given vessel, so we don't have to repeatedly call GetComponent<Vessel>().
        /// </summary>
        private static Dictionary<Guid, MASVesselComputer> knownModules = new Dictionary<Guid, MASVesselComputer>();

        /// <summary>
        /// The Vessel that we're attached to.  This is expected not to change.
        /// </summary>
        private Vessel vessel;

        /// <summary>
        /// Our current reference transform.
        /// </summary>
        internal Transform referenceTransform;

        /// <summary>
        /// Type of object that the reference transform is attached to.
        /// </summary>
        internal ReferenceType referenceTransformType;

        /// <summary>
        /// Local copy of the current orbit.  This is updated per fixed-update
        /// so we're not querying an indeterminate-cost property of Vessel.
        /// </summary>
        private Orbit orbit;

        /// <summary>
        /// A copy of the module's vessel ID, in case vessel is null'd before OnDestroy fires.
        /// </summary>
        private Guid vesselId;

        /// <summary>
        /// Whether the vessel needs MASVC support (has at least one crew).
        /// </summary>
        private bool vesselActive;

        /// <summary>
        /// A reference of the linear gauge used for atmospheric depth.
        /// </summary>
        private KSP.UI.Screens.LinearGauge atmosphereDepthGauge;

        /// <summary>
        /// A reference of the navBall to easily deduce flight information.
        /// </summary>
        private NavBall navBall;

        /// <summary>
        /// What world / star are we orbiting?
        /// </summary>
        internal CelestialBody mainBody;

        internal double universalTime;

        /// <summary>
        /// Returns the MASVesselComputer attached to the specified computer.
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        internal static MASVesselComputer Instance(Vessel v)
        {
            if (v != null)
            {
                return Instance(v.id);
            }
            else
            {
                Utility.LogErrorMessage("ASVesselComputer.Instance called with null vessel");
                return null;
            }
        }

        /// <summary>
        /// Return the vessel computer associated with the specified id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        internal static MASVesselComputer Instance(Guid id)
        {
            if (knownModules.ContainsKey(id))
            {
                return knownModules[id];
            }
            else
            {
                Utility.LogErrorMessage("ASVesselComputer.Instance called with unrecognized vessel id {0}", id);
                return null;
            }
        }

        #region Monobehaviour
        /// <summary>
        /// Update per-Vessel fields.
        /// </summary>
        private void FixedUpdate()
        {
            if (vesselActive)
            {
                orbit = vessel.orbit;
                universalTime = Planetarium.GetUniversalTime();

                // First step:
                PrepareResourceData();

                UpdateModuleData();
                UpdateAttitude();
                UpdateAltitudes();
                UpdateManeuverNode();
                UpdateTarget();
                UpdateMisc();
                // Last step:
                ProcessResourceData();
                //Utility.LogMessage(this, "FixedUpdate for {0}", vessel.id);
            }
        }

        /// <summary>
        /// Startup: see if we're attached to a vessel, and if that vessel is
        /// one we should update (has crew).  Since the latter can change, we
        /// will idle this object (ignore updates) if the crew count is 0.
        /// </summary>
        public override void OnAwake()
        {
            vessel = GetComponent<Vessel>();

            if (vessel == null)
            {
                // This happens when the vessel module is instantiated outside of flight.
                //Utility.LogErrorMessage(this, "OnAwake: Failed to get a valid vessel");
                return;
            }

            navBall = UnityEngine.Object.FindObjectOfType<KSP.UI.Screens.Flight.NavBall>();

            LinearAtmosphereGauge linearAtmosGauge = UnityEngine.Object.FindObjectOfType<KSP.UI.Screens.Flight.LinearAtmosphereGauge>();
            atmosphereDepthGauge = linearAtmosGauge.gauge;

            mainBody = vessel.mainBody;

            vesselId = vessel.id;
            orbit = vessel.orbit;

            GameEvents.onVesselChange.Add(onVesselChange);
            GameEvents.onVesselSOIChanged.Add(onVesselSOIChanged);
            GameEvents.onVesselWasModified.Add(onVesselWasModified);
            GameEvents.onVesselReferenceTransformSwitch.Add(onVesselReferenceTransformSwitch);

            if (knownModules.ContainsKey(vesselId))
            {
                Utility.LogErrorMessage(this, "OnAwake called on a instance that's already in the database");
            }

            knownModules[vesselId] = this;

            InitResourceData();

            vesselActive = (vessel.GetCrewCount() > 0);

            // TODO: Optimize this better - does any of this really need done if there's no crew?
            // First step:
            //PrepareResourceData();

            //UpdateModuleData();
            //UpdateAttitude();
            //UpdateAltitudes();
            //UpdateManeuverNode();
            //UpdateTarget();
            //UpdateMisc();
            //// Last step:
            //ProcessResourceData();

            Utility.LogMessage(this, "OnAwake for {0}", vesselId);
        }

        /// <summary>
        /// This vessel is being scrapped.  Release modules.
        /// </summary>
        private void OnDestroy()
        {
            if (vesselId == Guid.Empty)
            {
                return; // early - we never configured.
            }

            Utility.LogMessage(this, "OnDestroy for {0}", vesselId);

            GameEvents.onVesselChange.Remove(onVesselChange);
            GameEvents.onVesselSOIChanged.Remove(onVesselSOIChanged);
            GameEvents.onVesselWasModified.Remove(onVesselWasModified);
            GameEvents.onVesselReferenceTransformSwitch.Remove(onVesselReferenceTransformSwitch);

            TeardownResourceData();
            knownModules.Remove(vesselId);

            vesselId = Guid.Empty;
            vessel = null;
            orbit = null;
            atmosphereDepthGauge = null;
            mainBody = null;
            navBall = null;
            activeTarget = null;
        }

        /// <summary>
        /// Load persistent variable data from the persistent files, and
        /// distribute that data to the MASFlightComputer modules.
        /// </summary>
        /// <param name="node"></param>
        public override void OnLoad(ConfigNode node)
        {
            if (vesselActive)
            {
                base.OnLoad(node);
                Utility.LogMessage(this, "OnLoad for {0}", vessel.id);

                List<MASFlightComputer> knownFc = new List<MASFlightComputer>();
                for (int partIdx = vessel.parts.Count - 1; partIdx >= 0; --partIdx)
                {
                    MASFlightComputer fc = MASFlightComputer.Instance(vessel.parts[partIdx]);
                    if (fc != null)
                    {
                        knownFc.Add(fc);
                    }
                }

                ConfigNode[] persistentNodes = node.GetNodes();
                Utility.LogMessage(this, "Found {0} child nodes and {1} fc", persistentNodes.Length, knownFc.Count);

                // Yes, this is a horribly inefficient nested loop.  Except that
                // it should be uncommon to have more than a small number of pods
                // in most configurations.
                for (int nodeIdx = persistentNodes.Length - 1; nodeIdx >= 0; --nodeIdx)
                {
                    for (int fcIdx = knownFc.Count - 1; fcIdx >= 0; --fcIdx)
                    {
                        if (knownFc[fcIdx] != null)
                        {
                            if (knownFc[fcIdx].LoadPersistents(persistentNodes[nodeIdx]))
                            {
                                knownFc[fcIdx] = null;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Save this vessel's ASFlightComputer persistent vars.
        /// </summary>
        /// <param name="node"></param>
        public override void OnSave(ConfigNode node)
        {
            if (vesselActive)
            {
                base.OnSave(node);
                Utility.LogMessage(this, "OnSave for {0}", vessel.id);

                for (int partIdx = vessel.parts.Count - 1; partIdx >= 0; --partIdx)
                {
                    MASFlightComputer fc = MASFlightComputer.Instance(vessel.parts[partIdx]);
                    if (fc != null)
                    {
                        ConfigNode saveNode = fc.SavePersistents();
                        if (saveNode != null)
                        {
                            node.AddNode(saveNode);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Initialize our state.
        /// </summary>
        private void Start()
        {
            if (vesselActive)
            {
                Utility.LogMessage(this, "Start for {0}", vessel.id);

                // All this tells me is the current state of the nodes.  I
                // don't care about that - I want "is there anything in
                // that node"?
                //ConfigNode node = new ConfigNode("dummy");
                //vessel.ActionGroups.Save(node);
                UpdateReferenceTransform(vessel.ReferenceTransform);

                // First step:
                PrepareResourceData();

                UpdateModuleData();
                UpdateAttitude();
                UpdateAltitudes();
                UpdateManeuverNode();
                UpdateTarget();
                UpdateMisc();
                // Last step:
                ProcessResourceData();
            }
        }

        /// <summary>
        /// ???
        /// </summary>
        //private void Update()
        //{
        //    if (vesselActive)
        //    {
        //        Utility.LogMessage(this, "Update for {0}", vessel.id);
        //    }
        //}

        #endregion

        #region Vessel Data
        internal double altitudeASL;
        internal double altitudeTerrain;
        private double altitudeBottom_;
        internal double altitudeBottom
        {
            get
            {
                // This is expensive to compute, so we
                // don't until we're close to the ground,
                // and never until it's requested.
                if (altitudeBottom_ < 0.0)
                {
                    altitudeBottom_ = Math.Min(altitudeASL, altitudeTerrain);

                    // Precision isn't *that* important ... until we get close.
                    if (altitudeBottom_ < 500.0)
                    {
                        double lowestPoint = altitudeASL;

                        for (int i = vessel.parts.Count - 1; i >= 0; --i)
                        {
                            if (vessel.parts[i].collider != null)
                            {
                                Vector3d bottomPoint = vessel.parts[i].collider.ClosestPointOnBounds(mainBody.position);
                                double partBottomAlt = mainBody.GetAltitude(bottomPoint);
                                lowestPoint = Math.Min(lowestPoint, partBottomAlt);
                            }
                        }
                        lowestPoint -= altitudeASL;
                        altitudeBottom_ += lowestPoint;

                        altitudeBottom_ = Math.Max(0.0, altitudeBottom_);
                    }
                }

                return altitudeBottom_;
            }
        }
        internal double atmosphereDepth
        {
            get
            {
                return Mathf.Clamp01(atmosphereDepthGauge.Value);
            }
        }
        internal double apoapsis;
        internal double periapsis;
        void UpdateAltitudes()
        {
            altitudeASL = vessel.altitude;
            altitudeTerrain = vessel.altitude - vessel.terrainAltitude;
            altitudeBottom_ = -1.0;
            apoapsis = orbit.ApA;
            periapsis = orbit.PeA;
        }

        #region Attitudes
        private Vector3 surfaceAttitude;
        internal float heading
        {
            get
            {
                return surfaceAttitude.y;
            }
        }
        internal float pitch
        {
            get
            {
                return surfaceAttitude.x;
            }
        }
        internal float roll
        {
            get
            {
                return surfaceAttitude.z;
            }
        }
        private readonly Quaternion navballYRotate = Quaternion.Euler(0.0f, 180.0f, 0.0f);
        private Quaternion navballRelativeGimbal;
        internal Quaternion navBallRelativeGimbal
        {
            get
            {
                return navballRelativeGimbal;
            }
        }
        private Quaternion navballAttitudeGimbal;
        internal Quaternion navBallAttitudeGimbal
        {
            get
            {
                return navballAttitudeGimbal;
            }
        }
        internal Vector3 up; // local world "up"
        internal Vector3 prograde;
        internal Vector3 surfacePrograde;
        internal Vector3 radialOut;
        internal Vector3 normal;

        // Vessel-relative right, forward, and "top" unit vectors.
        internal Vector3 right;
        internal Vector3 forward;
        internal Vector3 top;


        /// <summary>
        /// Because the gimbal is reflected for presentation, we need to
        /// mirror the value here so the gimbal is correct.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        static Quaternion MirrorXAxis(Quaternion input)
        {
            return new Quaternion(input.x, -input.y, -input.z, input.w);
        }
        void UpdateAttitude()
        {
            Quaternion relativeGimbal = navBall.relativeGymbal;
            // We have to do all sorts of voodoo to get the navball
            // gimbal rotated so the rendered navball behaves the same
            // as navballs.
            navballRelativeGimbal = navballYRotate * MirrorXAxis(relativeGimbal);
            navballAttitudeGimbal = navBall.attitudeGymbal;
            surfaceAttitude = Quaternion.Inverse(relativeGimbal).eulerAngles;
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

            up = vessel.upAxis;
            prograde = vessel.obt_velocity.normalized;
            surfacePrograde = vessel.srf_velocity.normalized;
            radialOut = Vector3.ProjectOnPlane(up, prograde).normalized;
            normal = -Vector3.Cross(radialOut, prograde).normalized;

            right = vessel.GetTransform().right;
            forward = vessel.GetTransform().up;
            top = vessel.GetTransform().forward;

            // TODO: Am I computing normal wrong?
            // TODO: orbit.GetOrbitNormal() appears to return a vector in the opposite
            // direction when in the inertial frame of reference, but it's perpendicular
            // in the rotating reference frame.  Is there a way to always get the inertial
            // frame?  Or can I get away with working in whatever framework KSP is working
            // in?
            //Orbit.SolveClosestApproach();
            //orbit.GetTimeToPeriapsis();
            //orbit.timeToAp;
            //orbit.timeToPe;
            //orbit.getRelativePositionAtUT();
            // Trajectory object?
            //Utility.LogMessage(this, "orb Ap {0:0} / Pe {1:0}, end type {2}", orbit.ApA, orbit.PeA, orbit.patchEndTransition.ToString());
        }
        internal double GetRelativePitch(Vector3 direction)
        {
            // Project the direction vector onto the plane YZ plane
            Vector3 projectedVector = Vector3.ProjectOnPlane(direction, right);
            projectedVector.Normalize();

            // Dot the projected vector with the 'top' direction so we can find
            // the relative pitch.
            float dotPitch = Vector3.Dot(projectedVector, top);
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
        internal double GetRelativeYaw(Vector3 direction)
        {
            // Project the direction vector onto the plane XZ plane
            Vector3 projectedVector = Vector3.ProjectOnPlane(direction, top);
            projectedVector.Normalize();

            // Determine the lateral displacement by dotting the vector with
            // the 'right' vector...
            float dotLateral = Vector3.Dot(projectedVector, right);
            // And the forward/back displacement by dotting with the forward vector.
            float dotLongitudinal = Vector3.Dot(projectedVector, forward);

            // Taking arc tangent of x/y lets us treat the front of the vessel
            // as the 0 degree location.
            float yaw = Mathf.Atan2(dotLateral, dotLongitudinal);
            yaw *= Mathf.Rad2Deg;

            return yaw;
        }
        #endregion

        #region Maneuver
        private ManeuverNode node;
        private double nodeDV = -1.0;
        internal double maneuverNodeDeltaV
        {
            get
            {
                if (nodeDV < 0.0)
                {
                    if (node != null && orbit != null)
                    {
                        maneuverVector = node.GetBurnVector(orbit);
                        nodeDV = maneuverVector.magnitude;
                    }
                    else
                    {
                        nodeDV = 0.0;
                    }
                }

                return nodeDV;
            }
        }
        private Vector3d maneuverVector;
        internal Vector3d maneuverNodeVector
        {
            get
            {
                if (nodeDV < 0.0)
                {
                    if (node != null && orbit != null)
                    {
                        maneuverVector = node.GetBurnVector(orbit);
                        nodeDV = maneuverVector.magnitude;
                    }
                    else
                    {
                        nodeDV = 0.0;
                    }
                }

                return maneuverVector;
            }
        }
        internal bool maneuverNodeValid
        {
            get
            {
                return node != null;
            }
        }
        internal double maneuverNodeTime
        {
            get
            {
                if (node != null)
                {
                    return (universalTime - node.UT);
                }
                else
                {
                    return 0.0;
                }
            }
        }
        void UpdateManeuverNode()
        {
            if (vessel.patchedConicSolver != null)
            {
                node = vessel.patchedConicSolver.maneuverNodes.Count > 0 ? vessel.patchedConicSolver.maneuverNodes[0] : null;
            }
            else
            {
                node = null;
            }
            nodeDV = -1.0;
            maneuverVector = Vector3d.zero;
        }
        #endregion

        public enum TargetType
        {
            None,
            Vessel,
            DockingPort,
            CelestialBody,
            PositionTarget,
            Asteroid,
        };
        internal ITargetable activeTarget = null;
        internal Vector3d targetDisplacement;
        internal Vector3 targetDirection;
        internal Vector3d targetRelativeVelocity;
        internal TargetType targetType;
        internal Transform targetDockingTransform; // Docking node transform - valid only for docking port targets.
        internal bool targetValid
        {
            get
            {
                return (activeTarget != null);
            }
        }
        void UpdateTarget()
        {
            activeTarget = FlightGlobals.fetch.VesselTarget;
            if (activeTarget != null)
            {
                targetDisplacement = vessel.GetTransform().position - activeTarget.GetTransform().position;
                targetDirection = targetDisplacement.normalized;

                targetRelativeVelocity = vessel.obt_velocity - activeTarget.GetObtVelocity();
                targetDockingTransform = null;

                if (activeTarget is Vessel)
                {
                    targetType = TargetType.Vessel;
                }
                else if (activeTarget is CelestialBody)
                {
                    targetType = TargetType.CelestialBody;
                }
                else if (activeTarget is ModuleDockingNode)
                {
                    targetType = TargetType.DockingPort;
                    targetDockingTransform = (activeTarget as ModuleDockingNode).GetTransform();
                }
                else if (activeTarget is PositionTarget)
                {
                    targetType = TargetType.PositionTarget;
                }
                else
                {
                    Utility.LogErrorMessage(this, "UpdateTarget() - unable to classify target {0}", activeTarget.GetType().Name);
                    targetType = TargetType.None;
                }
            }
            else
            {
                targetType = TargetType.None;
                targetDisplacement = Vector3d.zero;
                targetRelativeVelocity = Vector3d.zero;
                targetDirection = forward;
                targetDockingTransform = null;
            }
        }

        internal double surfaceAccelerationFromGravity;
        private void UpdateMisc()
        {
            // Convert to m/2^s
            surfaceAccelerationFromGravity = orbit.referenceBody.GeeASL * 9.81;
        }

        private void UpdateReferenceTransform(Transform newRefXform)
        {
            referenceTransform = newRefXform;
            referenceTransformType = ReferenceType.Unknown;

            // TODO: Can I infer this from newRefXform?  And is it more
            // efficient to do than this call?
            Part referencePart = vessel.GetReferenceTransformPart();
            PartModuleList referenceModules = referencePart.Modules;
            for (int i = referenceModules.Count - 1; i >= 0; --i)
            {
                PartModule rpm = referenceModules[i];
                if (rpm is ModuleDockingNode)
                {
                    referenceTransformType = ReferenceType.DockingPort;
                    break;
                }
                else if (rpm is ModuleGrappleNode)
                {
                    referenceTransformType = ReferenceType.Claw;
                    break;
                }
                else if (rpm is ModuleCommand)
                {
                    referenceTransformType = ReferenceType.RemoteCommand;
                    break;
                }
            }

            if (referenceTransformType == ReferenceType.RemoteCommand)
            {
                // See if it's actually the current IVA command pod.
                if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA)
                {
                    Kerbal refKerbal = CameraManager.Instance.IVACameraActiveKerbal;
                    if (refKerbal != null && refKerbal.InPart == referencePart)
                    {
                        referenceTransformType = ReferenceType.Self;
                    }
                }
            }
        }
        #endregion

        #region GameEvent Callbacks
        private void onVesselChange(Vessel who)
        {
            if (who.id == vesselId)
            {
                vesselActive = (vessel.GetCrewCount() > 0);
                InvalidateModules();
            }
        }

        private void onVesselSOIChanged(GameEvents.HostedFromToAction<Vessel, CelestialBody> what)
        {
            if (what.host.id == vesselId)
            {
                mainBody = what.to;
            }
        }

        private void onVesselWasModified(Vessel who)
        {
            if (who.id == vesselId)
            {
                vesselActive = (vessel.GetCrewCount() > 0);
                InvalidateModules();
            }
        }

        private void onVesselDestroy(Vessel who)
        {
            if (who.id == vesselId)
            {
                vesselActive = (vessel.GetCrewCount() > 0);
            }
        }

        private void onVesselCreate(Vessel who)
        {
            if (who.id == vesselId)
            {
                vesselActive = (vessel.GetCrewCount() > 0);
            }
        }

        private void onVesselCrewWasModified(Vessel who)
        {
            if (who.id == vessel.id)
            {
                vesselActive = (vessel.GetCrewCount() > 0);
            }
        }

        private void onVesselReferenceTransformSwitch(Transform fromXform, Transform toXform)
        {
            UpdateReferenceTransform(toXform);
            //Utility.LogMessage(this, "onVesselReferenceTransformSwitch from {0} to {1}; fromMatch = {2}", 
            //    (fromXform == null) ? "(null)" : fromXform.name,
            //    (toXform == null) ? "(null)" : toXform.name,
            //    fromXform == referenceTransform);
        }
        #endregion
    }
}
