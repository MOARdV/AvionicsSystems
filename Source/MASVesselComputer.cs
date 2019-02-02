/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2019 MOARdV
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
    internal partial class MASVesselComputer : MonoBehaviour
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
        /// Our current reference transform.
        /// </summary>
        private Transform _referenceTransform;
        internal Transform referenceTransform
        {
            get
            {
                if (_referenceTransform == null)
                {
                    UpdateReferenceTransform(vessel.GetReferenceTransformPart(), true);
                }
                return _referenceTransform;
            }
        }

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
        /// The vessel this module represents.
        /// </summary>
        private Vessel vessel;

        /// <summary>
        /// A copy of the module's vessel ID, in case vessel is null'd before OnDestroy fires.
        /// </summary>
        private Guid vesselId;

        /// <summary>
        /// Whether the vessel needs MASVC support (has at least one crew).
        /// </summary>
        internal bool vesselCrewed;

        /// <summary>
        /// Whether the vessel is actually loaded and active.
        /// </summary>
        internal bool vesselActive;

        /// <summary>
        /// Boolean used to detect double-clicks during IVA, which would cause
        /// the vessel to lose target track.
        /// </summary>
        private bool doubleClickDetected = false;

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

        /// <summary>
        /// Current UT.
        /// </summary>
        internal double universalTime;

        /// <summary>
        /// Wrapper method for all of the subcategories of data that are
        /// refreshed per-FixedUpdate.
        /// </summary>
        private void RefreshData()
        {
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

        private void UpdateThermals()
        {
            double difference = float.MaxValue;

            double currentHottest = 0.0;
            for (int partIdx = vessel.parts.Count - 1; partIdx >= 0; --partIdx)
            {
                Part part = vessel.parts[partIdx];
                double maxTemp = part.maxTemp;
                double currentTemp = part.temperature;
                if (maxTemp - currentTemp < difference)
                {
                    _hottestPartMax = maxTemp;
                    currentHottest = currentTemp;
                    difference = maxTemp - currentTemp;
                }
                maxTemp = part.skinMaxTemp;
                currentTemp = part.skinTemperature;
                if (maxTemp - currentTemp < difference)
                {
                    _hottestPartMax = maxTemp;
                    currentHottest = currentTemp;
                    difference = maxTemp - currentTemp;
                }
            }
            _hottestPartSign = Math.Sign(_hottestPart - currentHottest);
            _hottestPart = currentHottest;
        }
        private double _hottestPart;
        private double _hottestPartMax;
        private float _hottestPartSign;
        internal double hottestPart
        {
            get
            {
                if (_hottestPartMax == -1.0)
                {
                    UpdateThermals();
                }
                return _hottestPart;
            }
        }
        internal double hottestPartSign
        {
            get
            {
                if (_hottestPartMax == -1.0)
                {
                    UpdateThermals();
                }
                return _hottestPartSign;
            }
        }
        internal double hottestPartMax
        {
            get
            {
                if (_hottestPartMax == -1.0)
                {
                    UpdateThermals();
                }
                return _hottestPartMax;
            }
        }

        #region Monobehaviour
        /// <summary>
        /// Update per-Vessel fields.
        /// </summary>
        private void FixedUpdate()
        {
            // Compute delta-t from Planetarium time so if the vessel goes
            // inactive for a while, it won't resume with an inaccurate
            // time ... or is that even worth the extra effort?
            if (vesselCrewed && vesselActive)
            {
                // TODO: Can I make these two update by callback?
                mainBody = vessel.mainBody;
                orbit = vessel.orbit;
                _hottestPartMax = -1.0;

                universalTime = Planetarium.GetUniversalTime();

                // GetReferenceTransformPart() seems to be pointing at the
                // previous part when the callback fires, so I use this hack
                // to manually recompute it here.
                UpdateReferenceTransform(vessel.GetReferenceTransformPart(), false);

                // If there was a mouse double-click event, and we think there's
                // a target, and KSP says there isn't a target, the user likely
                // double-clicked in the IVA and accidentally cleared the active
                // target.  Let's fix that for them.
                //
                // However, there seems to be a one-update delay in the change
                // registering:
                // 1) LateUpdate sees a double-click.
                // 2) FixedUpdate shows a VesselTarget in FlightGlobals.
                // 3) LateUpdate does not see a double-click.
                // 4) FixedUpdate shows the target is cleared.
                //
                // I could do a countdown timer instead of a boolean, I suppose.
                if (doubleClickDetected)
                {
                    if (activeTarget == null)
                    {
                        //Utility.LogMessage(this, "doubleClick corrector: no target expected (active = {0}, FG = {1})",
                        //    activeTarget != null, FlightGlobals.fetch.VesselTarget != null);
                        doubleClickDetected = false;
                    }
                    else if (activeTarget != null && FlightGlobals.fetch.VesselTarget == null)
                    {
                        FlightGlobals.fetch.SetVesselTarget(activeTarget);
                        //Utility.LogMessage(this, "doubleClick corrector: resetting");
                        doubleClickDetected = false;
                    }
                    //else
                    //{
                    //    Utility.LogMessage(this, "doubleClick corrector: no-op (active = {0}, FG = {1})",
                    //        activeTarget != null, FlightGlobals.fetch.VesselTarget != null);
                    //}
                }

                RefreshData();

                //Utility.LogMessage(this, "FixedUpdate for {0}", vessel.id);
            }
        }

        /// <summary>
        /// We use the LateUpdate() (why? - RPM did it here, but I don't know if
        /// it *needs* to be here) to look for double-click events, which happen
        /// too easily when playing in IVA.  The double-click clears targets, which
        /// can be a problem.
        /// </summary>
        public void LateUpdate()
        {
            if (vesselActive)
            {
                doubleClickDetected |= Mouse.Left.GetDoubleClick();
                //Utility.LogMessage(this, "LateUpdate: doubleClick = {0} ({1})", doubleClickDetected, Mouse.Left.GetDoubleClick());
            }
            else
            {
                doubleClickDetected = false;
            }
        }

        /// <summary>
        /// Initialize some fields to safe values (or expected unchanging values).
        /// The vessel fields of VesselComputer doesn't have good values yet, so this
        /// step is only good for non-specific initial values.
        /// </summary>
        public void Awake()
        {
            if (HighLogic.LoadedSceneIsFlight == false)
            {
                Utility.LogWarning(this, "Someone is creating a vessel computer outside of flight!");
            }

            vessel = gameObject.GetComponent<Vessel>();
            if (vessel == null)
            {
                throw new ArgumentNullException("[MASVesselComputer] Awake(): Could not find the vessel!");
            }
            //Utility.LogMessage(this, "Awake() for {0}", vessel.id);

            navBall = UnityEngine.Object.FindObjectOfType<KSP.UI.Screens.Flight.NavBall>();
            if (navBall == null)
            {
                Utility.LogError(this, "navBall was null!");
            }
            LinearAtmosphereGauge linearAtmosGauge = UnityEngine.Object.FindObjectOfType<KSP.UI.Screens.Flight.LinearAtmosphereGauge>();
            if (linearAtmosGauge == null)
            {
                Utility.LogError(this, "linearAtmosGauge was null!");
                atmosphereDepthGauge = new KSP.UI.Screens.LinearGauge();
            }
            else
            {
                atmosphereDepthGauge = linearAtmosGauge.gauge;
            }

            mainBody = vessel.mainBody;
            vesselId = vessel.id;
            orbit = vessel.orbit;

            universalTime = Planetarium.GetUniversalTime();

            InitResourceData();

            UpdateReferenceTransform(vessel.GetReferenceTransformPart(), true);
            vesselCrewed = (vessel.GetCrewCount() > 0);
            vesselActive = ActiveVessel(vessel);
            if (vesselCrewed)
            {
                RefreshData();
            }

            GameEvents.OnCameraChange.Add(onCameraChange);
            GameEvents.onStageActivate.Add(onStageActivate);
            GameEvents.onVesselChange.Add(onVesselChange);
            GameEvents.onVesselSOIChanged.Add(onVesselSOIChanged);
            GameEvents.onVesselWasModified.Add(onVesselWasModified);

            // onDominantBodyChange
        }

        /// <summary>
        /// This vessel is being scrapped.  Release modules.
        /// </summary>
        private void OnDestroy()
        {
            //Utility.LogMessage(this, "OnDestroy for {0}", vesselId);

            GameEvents.OnCameraChange.Remove(onCameraChange);
            GameEvents.onStageActivate.Remove(onStageActivate);
            GameEvents.onVesselChange.Remove(onVesselChange);
            GameEvents.onVesselSOIChanged.Remove(onVesselSOIChanged);
            GameEvents.onVesselWasModified.Remove(onVesselWasModified);

            TeardownResourceData();

            vesselId = Guid.Empty;
            orbit = null;
            atmosphereDepthGauge = null;
            mainBody = null;
            navBall = null;
            activeTarget = null;
        }
        #endregion

        #region Vessel Data

        /// <summary>
        /// Helper method to determine if the vessel is the active IVA vessel,
        /// since we don't want to burn cycles on vessels whose IVA doesn't exist.
        /// </summary>
        /// <param name="vessel"></param>
        /// <returns></returns>
        private static bool ActiveVessel(Vessel vessel)
        {
            // This does not account for the stock overlays.  However, that
            // required iterating over the cameras list to find
            // "InternalSpaceOverlay Host".  At least, in 1.1.3.
            return vessel.isActiveVessel && (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA || CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal);
        }

        // Time in seconds until impact.  0 if there is no impact.
        private double timeToImpact_;
        internal double timeToImpact
        {
            get
            {
                if (timeToImpact_ < 0.0)
                {
                    RefreshLandingEstimate();
                }
                return timeToImpact_;
            }
        }
        private double landingAltitude_;
        internal double landingAltitude
        {
            get
            {
                if (timeToImpact_ < 0.0)
                {
                    RefreshLandingEstimate();
                }
                return landingAltitude_;
            }
        }
        private double landingLongitude_;
        internal double landingLongitude
        {
            get
            {
                if (timeToImpact_ < 0.0)
                {
                    RefreshLandingEstimate();
                }
                return landingLongitude_;
            }
        }
        private double landingLatitude_;
        internal double landingLatitude
        {
            get
            {
                if (timeToImpact_ < 0.0)
                {
                    RefreshLandingEstimate();
                }
                return landingLatitude_;
            }
        }

        private void RefreshLandingEstimate()
        {
            if (orbit.PeA < 0.0 && orbit.eccentricity < 1.0 && !(vessel.Landed || vessel.Splashed))
            {
                // Initial estimate:
                landingAltitude_ = 0.0;

                timeToImpact_ = Utility.NextTimeToRadius(orbit, landingAltitude_ + orbit.referenceBody.Radius);
                Vector3d pos = orbit.getPositionAtUT(timeToImpact_ + Planetarium.GetUniversalTime());

                Vector2d latlon = orbit.referenceBody.GetLatitudeAndLongitude(pos);
                landingAltitude_ = orbit.referenceBody.TerrainAltitude(latlon.x, latlon.y);
                landingLatitude_ = latlon.x;
                landingLongitude_ = latlon.y;

                landingAltitude_ = Math.Min(orbit.ApA, Math.Max(orbit.PeA, Math.Max(landingAltitude_, 0.0)));

                double lastImpact = timeToImpact_;

                //Utility.LogMessage(this, "RefreshLandingEstimate():");
                for (int i = 0; i < 6; ++i)
                {
                    timeToImpact_ = Utility.NextTimeToRadius(orbit, landingAltitude_ + orbit.referenceBody.Radius);

                    pos = orbit.getPositionAtUT(timeToImpact_ + Planetarium.GetUniversalTime());
                    latlon = orbit.referenceBody.GetLatitudeAndLongitude(pos);
                    landingAltitude_ = orbit.referenceBody.TerrainAltitude(latlon.x, latlon.y);
                    landingLatitude_ = latlon.x;
                    landingLongitude_ = latlon.y;

                    landingAltitude_ = Math.Min(orbit.ApA, Math.Max(orbit.PeA, Math.Max(landingAltitude_, 0.0)));

                    //Utility.LogMessage(this, "[{2}]: {0:0}m in {1:0}s", landingAltitude_, timeToImpact_, i);

                    if (Math.Abs(timeToImpact_ - lastImpact) < 2.0)
                    {
                        break;
                    }
                    else
                    {
                        lastImpact = timeToImpact_;
                    }
                }
            }
            else
            {
                timeToImpact_ = 0.0;
                landingAltitude_ = 0.0;
                landingLongitude_ = 0.0;
                landingLatitude_ = 0.0;
            }
        }

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
            timeToImpact_ = -1.0;
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

        internal float progradeHeading;

        private float lastHeading;
        internal double headingRate = 0.0;

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

        /// <summary>
        /// Do a ray-cast to determine slope beneath the vessel.
        /// </summary>
        /// <returns>Slope, or 0 if it can not be computed.</returns>
        internal double GetSlopeAngle()
        {
            RaycastHit sfc;
            if (Physics.Raycast(vessel.CoM, -up, out sfc, (float)altitudeASL + 1000.0f, 1 << 15))
            {
                return Vector3.Angle(up, sfc.normal);
            }
            else
            {
                return 0.0;
            }
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

            double headingChange = Utility.NormalizeLongitude(surfaceAttitude.y - lastHeading);
            lastHeading = surfaceAttitude.y;
            headingRate = 0.875 * headingRate + 0.125 * headingChange / TimeWarp.fixedDeltaTime;

            up = vessel.upAxis;
            prograde = vessel.obt_velocity.normalized;
            surfacePrograde = vessel.srf_vel_direction;
            radialOut = Vector3.ProjectOnPlane(up, prograde).normalized;
            normal = -Vector3.Cross(radialOut, prograde).normalized;
            // TODO: does Vector3.OrthoNormalize do anything for me here?

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

            Vector3 surfaceProgradeProjected = Vector3.ProjectOnPlane(surfacePrograde, up);
            progradeHeading = Vector3.Angle(surfaceProgradeProjected, vessel.north);
            if (Vector3.Dot(surfaceProgradeProjected, vessel.east) < 0.0)
            {
                progradeHeading = 360.0f - progradeHeading;
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
        private double nodeTotalDV = 0.0;
        private void RefreshNodeValues()
        {
            // Per KSP API wiki http://docuwiki-kspapi.rhcloud.com/#/classes/ManeuverNode:
            // The x-component of DeltaV represents the delta-V in the radial-plus direction.
            // The y-component of DeltaV  represents the delta-V in the normal-plus direction.
            // The z-component of DeltaV represents the delta-V in the prograde direction.
            // However... it is not returned in the basis of the orbit at the time of the
            // maneuver.  It needs transformed into the right basis.
            maneuverVector = node.GetBurnVector(orbit);
            nodeDV = maneuverVector.magnitude;
            nodeTotalDV = node.DeltaV.magnitude;

            // Swizzle these into the right order.
            Vector3d mnvrVel = orbit.getOrbitalVelocityAtUT(node.UT).xzy;
            Vector3d mnvrPos = orbit.getRelativePositionAtUT(node.UT).xzy;

            Vector3d mnvrPrograde = mnvrVel.normalized; // Prograde vector at maneuver time
            Vector3d mnvrNml = Vector3d.Cross(mnvrVel, mnvrPos).normalized;
            Vector3d mnvrRadial = Vector3d.Cross(mnvrNml, mnvrPrograde);

            maneuverNodeComponentVector.x = Vector3d.Dot(maneuverVector, mnvrPrograde);
            maneuverNodeComponentVector.y = Vector3d.Dot(maneuverVector, mnvrNml);
            maneuverNodeComponentVector.z = Vector3d.Dot(maneuverVector, mnvrRadial);
        }

        private Vector3d maneuverNodeComponentVector = Vector3d.zero;
        internal Vector3d maneuverNodeComponent
        {
            get
            {
                if (nodeDV < 0.0)
                {
                    if (node != null && orbit != null)
                    {
                        RefreshNodeValues();
                    }
                    else
                    {
                        nodeDV = 0.0;
                    }
                }

                return maneuverNodeComponentVector;
            }
        }
        internal double maneuverNodeDeltaV
        {
            get
            {
                if (nodeDV < 0.0)
                {
                    if (node != null && orbit != null)
                    {
                        RefreshNodeValues();
                    }
                    else
                    {
                        nodeDV = 0.0;
                    }
                }

                return nodeDV;
            }
        }
        internal double maneuverNodeTotalDeltaV
        {
            get
            {
                if (nodeDV < 0.0)
                {
                    if (node != null && orbit != null)
                    {
                        RefreshNodeValues();
                    }
                    else
                    {
                        nodeDV = 0.0;
                    }
                }

                return nodeTotalDV;
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
                        RefreshNodeValues();
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

        internal double NodeBurnTime()
        {
            if (maneuverNodeValid && currentIsp > 0.0 && currentMaxThrust > 0.0)
            {
                return currentIsp * (1.0f - Math.Exp(-maneuverNodeDeltaV / currentIsp / PhysicsGlobals.GravitationalAcceleration)) / (currentMaxThrust / (vessel.totalMass * PhysicsGlobals.GravitationalAcceleration));
            }
            else
            {
                return 0.0;
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
                else
                {
                    nodeOrbit = null;
                }
            }
            else
            {
                node = null;
                nodeOrbit = null;
            }
            nodeDV = -1.0;
            nodeTotalDV = 0.0;
            maneuverVector = Vector3d.zero;
            maneuverNodeComponentVector = Vector3d.zero;
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
        internal Vector3 targetDisplacement = Vector3.zero;
        internal Vector3 targetDirection = Vector3.zero;
        internal Vector3d targetRelativeVelocity = Vector3.zero;
        internal TargetType targetType = TargetType.None;
        internal string targetName = string.Empty;
        internal Transform targetDockingTransform; // Docking node transform - valid only for docking port targets.
        internal ModuleDockingNode[] targetDockingPorts = new ModuleDockingNode[0];
        internal Orbit targetOrbit;
        internal double targetClosestUT
        {
            get
            {
                if (activeTarget != null && !approachSolver.resultsReady)
                {
                    if (targetType == MASVesselComputer.TargetType.CelestialBody)
                    {
                        approachSolver.SolveBodyIntercept(orbit, activeTarget as CelestialBody);
                    }
                    else
                    {
                        approachSolver.SolveOrbitIntercept(orbit, targetOrbit);
                    }
                }
                return approachSolver.resultsReady ? approachSolver.targetClosestUT : 0.0;
            }
        }
        internal double targetClosestSpeed
        {
            get
            {
                if (activeTarget != null && !approachSolver.resultsReady)
                {
                    if (targetType == MASVesselComputer.TargetType.CelestialBody)
                    {
                        approachSolver.SolveBodyIntercept(orbit, activeTarget as CelestialBody);
                    }
                    else
                    {
                        approachSolver.SolveOrbitIntercept(orbit, targetOrbit);
                    }
                }
                return approachSolver.resultsReady ? approachSolver.targetClosestSpeed : 0.0;
            }
        }
        internal double targetClosestDistance
        {
            get
            {
                if (activeTarget != null && !approachSolver.resultsReady)
                {
                    if (targetType == MASVesselComputer.TargetType.CelestialBody)
                    {
                        approachSolver.SolveBodyIntercept(orbit, activeTarget as CelestialBody);
                    }
                    else
                    {
                        approachSolver.SolveOrbitIntercept(orbit, targetOrbit);
                    }
                }
                if (approachSolver.resultsReady)
                {
                    if (targetType == TargetType.CelestialBody)
                    {
                        // If we are targeting a body, account for the radius of the planet when describing closest approach.
                        // That is, targetClosestDistance is effectively PeA.
                        return Math.Max(0.0, approachSolver.targetClosestDistance - (activeTarget as CelestialBody).Radius);
                    }
                    else
                    {
                        return approachSolver.targetClosestDistance;
                    }
                }
                else
                {
                    return 0.0;
                }
            }
        }
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
        private void UpdateTargetDockingPorts()
        {
            Vessel targetVessel = (targetType == TargetType.Vessel) ? (activeTarget as Vessel) : (activeTarget as ModuleDockingNode).vessel;
            if (!targetVessel.packed && targetVessel.loaded)
            {
                List<ModuleDockingNode> potentialDocks = targetVessel.FindPartModulesImplementing<ModuleDockingNode>();
                List<ModuleDockingNode> validDocks = new List<ModuleDockingNode>();

                if (dockingNode != null)
                {
                    for (int i = potentialDocks.Count - 1; i >= 0; --i)
                    {
                        ModuleDockingNode otherDock = potentialDocks[i];
                        // Only lock on to an available dock of the same type that is either ungendered or the opposite gender.
                        if (otherDock.state == "Ready" && (string.IsNullOrEmpty(dockingNode.nodeType) || dockingNode.nodeType == otherDock.nodeType) && (dockingNode.gendered == false || dockingNode.genderFemale != otherDock.genderFemale))
                        {
                            validDocks.Add(otherDock);
                        }
                    }
                }
                else
                {
                    for (int i = potentialDocks.Count - 1; i >= 0; --i)
                    {
                        ModuleDockingNode otherDock = potentialDocks[i];
                        // Only lock on to an available dock of the same type that is either ungendered or the opposite gender.
                        if (otherDock.state == "Ready")
                        {
                            validDocks.Add(otherDock);
                        }
                    }
                }
                if (targetDockingPorts.Length != validDocks.Count)
                {
                    targetDockingPorts = validDocks.ToArray();
                }
                else
                {
                    for (int i = targetDockingPorts.Length - 1; i >= 0; --i)
                    {
                        targetDockingPorts[i] = validDocks[i];
                    }
                }
            }

            else if ((targetVessel.packed || !targetVessel.loaded) && targetDockingPorts.Length > 0)
            {
                targetDockingPorts = new ModuleDockingNode[0];
            }
        }
        private void UpdateTarget()
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
                    targetType = ((activeTarget as Vessel).vesselType == VesselType.SpaceObject) ? TargetType.Asteroid : TargetType.Vessel;
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
                    Utility.LogError(this, "UpdateTarget() - unable to classify target {0}", activeTarget.GetType().Name);
                    targetType = TargetType.None;
                }

                if (targetType == TargetType.Vessel || targetType == TargetType.DockingPort)
                {
                    UpdateTargetDockingPorts();
                }

                targetName = activeTarget.GetName();
                targetOrbit = activeTarget.GetOrbit();
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
                if (targetDockingPorts.Length > 0)
                {
                    targetDockingPorts = new ModuleDockingNode[0];
                }
            }
            approachSolver.ResetComputation();
        }
        #endregion

        private bool aeroDataValid = false;
        private double dragForce;
        private double gravForce;
        private double liftForce;
        private double liftUpForce;
        private double terminalVelocity;

        private void UpdateAeroForces()
        {
            if (aeroDataValid)
            {
                return;
            }

            aeroDataValid = true;

            gravForce = 1000.0 * vessel.GetTotalMass() * FlightGlobals.getGeeForceAtPosition(vessel.CoM).magnitude; // force of gravity

            // Short-circuit these computations if there's no atmosphere.
            if (vessel.atmDensity == 0.0)
            {
                liftForce = 0.0;
                dragForce = 0.0;
                terminalVelocity = 0.0;
                liftUpForce = 0.0;

                return;
            }

            // Code substantially from NathanKell's AeroGUI mod,
            // https://github.com/NathanKell/AeroGUI/blob/ccfd5e2e40fdf13e6ce66517ceb1db418689a5f0/AeroGUI/AeroGUI.cs#L301

            Vector3d vLift = Vector3d.zero; // the sum of lift from all parts
            Vector3d vDrag = Vector3d.zero; // the sum of drag from all parts
            double areaDrag = 0.0;

            for (int i = vessel.Parts.Count - 1; i >= 0; --i)
            {
                Part p = vessel.Parts[i];

                // get part drag (but not wing/surface drag)
                vDrag += -p.dragVectorDir * p.dragScalar;
                if (!p.hasLiftModule)
                {
                    Vector3 bodyLift = p.transform.rotation * (p.bodyLiftScalar * p.DragCubes.LiftForce);
                    bodyLift = Vector3.ProjectOnPlane(bodyLift, -p.dragVectorDir);
                    vLift += bodyLift;
                }

                ModuleLiftingSurface wing = p.FindModuleImplementing<ModuleLiftingSurface>();
                if (wing != null)
                {
                    vLift += wing.liftForce;
                    vDrag += wing.dragForce;
                }

                areaDrag += p.DragCubes.AreaDrag * PhysicsGlobals.DragCubeMultiplier * PhysicsGlobals.DragMultiplier;
            }

            Vector3d force = vLift + vDrag; // sum of all forces on the craft
            Vector3d nVel = vessel.srf_velocity.normalized;
            Vector3d liftDir = -Vector3d.Cross(vessel.transform.right, nVel); // we need the "lift" direction, which
            // is "up" from our current velocity vector and roll angle.

            // Now we can compute the dots.
            liftForce = Vector3d.Dot(force, liftDir); // just the force in the 'lift' direction

            dragForce = Vector3d.Dot(force, -nVel); // drag force, = pDrag + lift-induced drag

            liftUpForce = Vector3d.Dot(force, up);

            terminalVelocity = Math.Sqrt(2.0 * gravForce / (areaDrag * vessel.atmDensity));
        }

        internal double DragForce()
        {
            if (!aeroDataValid)
            {
                UpdateAeroForces();
            }

            return dragForce;
        }
        internal double GravForce()
        {
            if (!aeroDataValid)
            {
                UpdateAeroForces();
            }

            return gravForce;
        }
        internal double LiftForce()
        {
            if (!aeroDataValid)
            {
                UpdateAeroForces();
            }

            return liftForce;
        }
        internal double LiftUpForce()
        {
            if (!aeroDataValid)
            {
                UpdateAeroForces();
            }

            return liftUpForce;
        }
        internal double TerminalVelocity()
        {
            if (!aeroDataValid)
            {
                UpdateAeroForces();
            }

            return terminalVelocity;
        }

        internal double surfaceAccelerationFromGravity;
        private void UpdateMisc()
        {
            // Convert to m/2^s
            surfaceAccelerationFromGravity = orbit.referenceBody.GeeASL * PhysicsGlobals.GravitationalAcceleration;
            aeroDataValid = false;
        }

        private void UpdateReferenceTransform(Part referencePart, bool forceEvaluate)
        {
            if (referencePart == null)
            {
                Utility.LogWarning(this, "UpdateReferenceTransform(): referencePart is null?");
                return;
            }

            Transform newRefXform = (referencePart == null) ? null : referencePart.GetReferenceTransform();
            if (_referenceTransform == newRefXform && !forceEvaluate)
            {
                return;
            }

            _referenceTransform = newRefXform;
            referenceTransformType = ReferenceType.Unknown;

            if (referencePart.Modules.GetModule<ModuleCommand>() != null)
            {
                referenceTransformType = ReferenceType.RemoteCommand;
                if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA)
                {
                    Kerbal refKerbal = CameraManager.Instance.IVACameraActiveKerbal;
                    if (refKerbal != null && refKerbal.InPart == referencePart)
                    {
                        referenceTransformType = ReferenceType.Self;
                    }
                }
            }
            else if (referencePart.Modules.GetModule<ModuleDockingNode>() != null)
            {
                referenceTransformType = ReferenceType.DockingPort;
            }
            else if (referencePart.Modules.GetModule<ModuleGrappleNode>() != null)
            {
                referenceTransformType = ReferenceType.Claw;
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
        /// <param name="newMode"></param>
        private void onCameraChange(CameraManager.CameraMode newMode)
        {
            vesselActive = ActiveVessel(vessel);
            UpdateReferenceTransform(vessel.GetReferenceTransformPart(), vesselActive);
        }

        /// <summary>
        /// We staged - time to refresh our resource tracking.
        /// </summary>
        /// <param name="stage"></param>
        private void onStageActivate(int stage)
        {
            InvalidateModules();
        }

        private void onVesselChange(Vessel who)
        {
            if (who.id == vesselId)
            {
                vesselCrewed = (vessel.GetCrewCount() > 0) && HighLogic.LoadedSceneIsFlight;
                vesselActive = ActiveVessel(vessel);
                InvalidateModules();
            }
        }

        private void onVesselSOIChanged(GameEvents.HostedFromToAction<Vessel, CelestialBody> what)
        {
            if (what.host.id == vesselId)
            {
                Utility.LogMessage(this, "onVesselSOIChanged: to {0}", what.to.bodyName);
                mainBody = what.to;
            }
        }

        private void onVesselWasModified(Vessel who)
        {
            if (who.id == vesselId)
            {
                vesselCrewed = (vessel.GetCrewCount() > 0) && HighLogic.LoadedSceneIsFlight;
                vesselActive = ActiveVessel(vessel);
                InvalidateModules();
            }
        }

        private void onVesselDestroy(Vessel who)
        {
            if (who.id == vesselId)
            {
                vesselCrewed = (vessel.GetCrewCount() > 0) && HighLogic.LoadedSceneIsFlight;
                vesselActive = ActiveVessel(vessel);
            }
        }

        private void onVesselCreate(Vessel who)
        {
            if (who.id == vesselId)
            {
                vesselCrewed = (vessel.GetCrewCount() > 0) && HighLogic.LoadedSceneIsFlight;
                vesselActive = ActiveVessel(vessel);
            }
        }

        private void onVesselCrewWasModified(Vessel who)
        {
            if (who.id == vessel.id)
            {
                vesselCrewed = (vessel.GetCrewCount() > 0) && HighLogic.LoadedSceneIsFlight;
                vesselActive = ActiveVessel(vessel);
            }
        }
        #endregion
    }
}
