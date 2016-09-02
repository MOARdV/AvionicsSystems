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
        internal class ApproachSolver
        {
            private static readonly int NumSubdivisions = 16;
            private static readonly int MaxRecursions = 16;

            /// <summary>
            /// Iterate through the next several patches on the orbit to find the
            /// first one that shares the same reference body as the supplied
            /// parameter.
            /// </summary>
            /// <param name="startOrbit"></param>
            /// <param name="referenceBody"></param>
            /// <returns></returns>
            private Orbit SelectClosestOrbit(Orbit startOrbit, CelestialBody referenceBody)
            {
                Orbit checkorbit = startOrbit;
                int orbitcount = 0;

                while (checkorbit.nextPatch != null && checkorbit.patchEndTransition != Orbit.PatchTransitionType.FINAL && orbitcount < 3)
                {
                    checkorbit = checkorbit.nextPatch;
                    orbitcount++;
                    if (checkorbit.referenceBody == referenceBody)
                    {
                        return checkorbit;
                    }

                }

                return startOrbit;
            }

            private void OneStep(Orbit vesselOrbit, Orbit targetOrbit, double startUT, double endUT, int recursionDepth, ref double targetClosestDistance, ref double targetClosestUT)
            {
                if (recursionDepth > MaxRecursions)
                {
                    return;
                }

                double deltaT = (endUT - startUT) / (double)NumSubdivisions;

                double closestDistSq = targetClosestDistance * targetClosestDistance;
                for (double t = startUT; t <= endUT; t += deltaT)
                {
                    Vector3d vesselPos = vesselOrbit.getPositionAtUT(t);
                    Vector3d targetPos = targetOrbit.getPositionAtUT(t);

                    double distSq = (vesselPos - targetPos).sqrMagnitude;
                    if (distSq < closestDistSq)
                    {
                        closestDistSq = distSq;
                        targetClosestUT = t;
                    }
                }

                targetClosestDistance = Math.Sqrt(closestDistSq);

                if (deltaT < 0.5)
                {
                    // If our timesteps are less than a half second, I think
                    // this is an accurate enough estimate.
                    return;
                }

                OneStep(vesselOrbit, targetOrbit, targetClosestUT - deltaT, targetClosestUT + deltaT, recursionDepth + 1, ref targetClosestDistance, ref targetClosestUT);
            }

            /// <summary>
            /// Iterate over our closest approach estimator.  Someday, I may figure out how to spin this into a thread
            /// instead, so it's less costly.
            /// </summary>
            /// <param name="vesselOrbit"></param>
            /// <param name="targetOrbit"></param>
            /// <param name="now"></param>
            /// <param name="targetClosestDistance"></param>
            /// <param name="targetClosestUT"></param>
            internal void IterateApproachSolver(Orbit vesselOrbit, Orbit targetOrbit, double now, out double targetClosestDistance, out double targetClosestUT)
            {
                targetClosestDistance = float.MaxValue;
                targetClosestUT = float.MaxValue;

                vesselOrbit = SelectClosestOrbit(vesselOrbit, targetOrbit.referenceBody);
                double minPeriod = Math.Max(vesselOrbit.period, targetOrbit.period);

                OneStep(vesselOrbit, targetOrbit, now, now + minPeriod, 0, ref targetClosestDistance, ref targetClosestUT);
            }
        };

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
        private bool refreshReferenceTransform = false;

        /// <summary>
        /// Type of object that the reference transform is attached to.
        /// </summary>
        internal ReferenceType referenceTransformType;

        /// <summary>
        /// Local copy of the current orbit.  This is updated per fixed-update
        /// so we're not querying an indeterminate-cost property of Vessel.
        /// </summary>
        internal Orbit orbit;

        /// <summary>
        /// A copy of the module's vessel ID, in case vessel is null'd before OnDestroy fires.
        /// </summary>
        private Guid vesselId;

        /// <summary>
        /// Whether the vessel needs MASVC support (has at least one crew).
        /// </summary>
        internal bool vesselActive;

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

                if (refreshReferenceTransform)
                {
                    // GetReferenceTransformPart() seems to be pointing at the
                    // previous part when the callback fires, so I use this hack
                    // to manually recompute it here.
                    UpdateReferenceTransform(referenceTransform);
                    refreshReferenceTransform = false;
                }
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

            GameEvents.OnCameraChange.Add(onCameraChange);
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

            vesselActive = (vessel.GetCrewCount() > 0) && HighLogic.LoadedSceneIsFlight;

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

            GameEvents.OnCameraChange.Remove(onCameraChange);
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
                refreshReferenceTransform = true;

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
        #endregion

        #region Vessel Data

        internal double altitudeASL;
        internal double altitudeTerrain;
        internal double altitudeTerrainRate;
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
            double previousAltitudeTerrain = Math.Min(altitudeTerrain, altitudeASL);
            altitudeASL = vessel.altitude;
            altitudeTerrain = vessel.altitude - vessel.terrainAltitude;

            // Apply exponential smoothing - terrain rate is very noisy.
            const float alpha = 0.0625f;
            altitudeTerrainRate = altitudeTerrainRate * (1.0 - alpha) + ((Math.Min(altitudeTerrain, altitudeASL) - previousAltitudeTerrain) / TimeWarp.fixedDeltaTime) * alpha;

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
        // Surface-relative vectors.
        internal Vector3 up; // local world "up"
        internal Vector3 surfaceRight; // vessel right projected onto the plane described by "up"
        internal Vector3 surfaceForward; // vessel forward projected onto the plane described by "up"; special handling when 'forward' is near 'up'.

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

            // We base our surface vector off of UP and RIGHT, unless roll is extreme.
            if (Mathf.Abs(Vector3.Dot(right, up)) > 0.995f)
            {
                surfaceRight = Vector3.Cross(forward, up);
                surfaceForward = Vector3.Cross(up, surfaceRight);
            }
            else
            {
                surfaceForward = Vector3.Cross(up, right);
                surfaceRight = Vector3.Cross(surfaceForward, up);
            }

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
        internal Orbit nodeOrbit;
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

                if (node != null)
                {
                    nodeOrbit = node.nextPatch;
                }
            }
            else
            {
                node = null;
                nodeOrbit = null;
            }
            nodeDV = -1.0;
            maneuverVector = Vector3d.zero;
        }
        #endregion

        #region Target
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
        internal ApproachSolver approachSolver = new ApproachSolver();
        internal Vector3 targetDisplacement;
        internal Vector3 targetDirection;
        internal Vector3d targetRelativeVelocity;
        internal TargetType targetType;
        internal string targetName;
        internal Transform targetDockingTransform; // Docking node transform - valid only for docking port targets.
        internal Orbit targetOrbit;
        internal double targetClosestUT;
        internal double targetClosestDistance;
        internal bool targetValid
        {
            get
            {
                return (activeTarget != null);
            }
        }
        private double targetCmpSpeed = -1.0;
        internal double targetSpeed
        {
            get
            {
                if (targetCmpSpeed < 0.0)
                {
                    targetCmpSpeed = targetRelativeVelocity.magnitude;
                }
                return targetCmpSpeed;
            }
        }
        void UpdateTarget()
        {
            activeTarget = FlightGlobals.fetch.VesselTarget;
            if (activeTarget != null)
            {
                targetDisplacement = activeTarget.GetTransform().position - vessel.GetTransform().position;
                targetDirection = targetDisplacement.normalized;

                targetRelativeVelocity = vessel.obt_velocity - activeTarget.GetObtVelocity();
                targetCmpSpeed = -1.0;
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

                targetName = activeTarget.GetName();
                targetOrbit = activeTarget.GetOrbit();
                approachSolver.IterateApproachSolver(orbit, targetOrbit, universalTime, out targetClosestDistance, out targetClosestUT);
            }
            else
            {
                targetCmpSpeed = 0.0;
                targetType = TargetType.None;
                targetDisplacement = Vector3.zero;
                targetRelativeVelocity = Vector3d.zero;
                targetDirection = forward;
                targetDockingTransform = null;
                targetName = string.Empty;
                targetOrbit = null;
                targetClosestUT = universalTime;
                targetClosestDistance = 0.0;
            }
        }
        #endregion

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
            // Actually, it seems like GetReferenceTransformPart() hasn't
            // updated yet!
            Part referencePart = vessel.GetReferenceTransformPart();
            if (referencePart != null)
            {
                PartModuleList referenceModules = referencePart.Modules;
                if (referenceModules != null)
                {
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

            UpdateDockingNode(referencePart);
        }
        #endregion

        #region GameEvent Callbacks
        /// <summary>
        /// The player changed camera modes.  If we're going from 'outside' to
        /// 'inside', we can figure out which part we're in, and thus whether
        /// there are docks available.  To do that, we have to reprocess the
        /// reference transform.
        /// </summary>
        /// <param name="data"></param>
        private void onCameraChange(CameraManager.CameraMode data)
        {
            UpdateReferenceTransform(referenceTransform);
        }

        private void onVesselChange(Vessel who)
        {
            if (who.id == vesselId)
            {
                vesselActive = (vessel.GetCrewCount() > 0) && HighLogic.LoadedSceneIsFlight;
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
                vesselActive = (vessel.GetCrewCount() > 0) && HighLogic.LoadedSceneIsFlight;
                InvalidateModules();
            }
        }

        private void onVesselDestroy(Vessel who)
        {
            if (who.id == vesselId)
            {
                vesselActive = (vessel.GetCrewCount() > 0) && HighLogic.LoadedSceneIsFlight;
            }
        }

        private void onVesselCreate(Vessel who)
        {
            if (who.id == vesselId)
            {
                vesselActive = (vessel.GetCrewCount() > 0) && HighLogic.LoadedSceneIsFlight;
            }
        }

        private void onVesselCrewWasModified(Vessel who)
        {
            if (who.id == vessel.id)
            {
                vesselActive = (vessel.GetCrewCount() > 0) && HighLogic.LoadedSceneIsFlight;
            }
        }

        private void onVesselReferenceTransformSwitch(Transform fromXform, Transform toXform)
        {
            UpdateReferenceTransform(toXform);
            refreshReferenceTransform = true;
            //Utility.LogMessage(this, "onVesselReferenceTransformSwitch from {0} to {1}; fromMatch = {2}", 
            //    (fromXform == null) ? "(null)" : fromXform.name,
            //    (toXform == null) ? "(null)" : toXform.name,
            //    fromXform == referenceTransform);
        }
        #endregion
    }
}
