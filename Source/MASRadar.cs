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
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// Import / adaptation of the JSIRadar module I wrote for RasterPropMonitor.
    /// </summary>
    public class MASRadar : PartModule
    {
        /// <summary>
        /// Maximum radar range (in km).
        /// </summary>
        [KSPField]
        public float maxRange = 200.0f;
        private double maxRangeMeters;
        private double maxRangeMetersSquared;

        /// <summary>
        /// What is the maximum angle off of the centerline that the radar can
        /// scan, in degrees
        /// </summary>
        [KSPField]
        public float scanAngle = 30.0f;

        /// <summary>
        /// What resource do we use?
        /// </summary>
        [KSPField]
        public string resourceName = "ElectricCharge";
        private int resourceId;
        internal bool hasPower = true;

        /// <summary>
        /// How many units/second of the resource do we use?
        /// </summary>
        [KSPField]
        public float resourceAmount = 0.0f;

        /// <summary>
        /// Optional: transform to use instead of the root transform or the
        /// docking port on the part (if there's a docking port).
        /// </summary>
        [KSPField]
        public string radarTransform = string.Empty;

        /// <summary>
        /// Will the radar select a docking port on a targeted vessel once we
        /// are close?
        /// </summary>
        [KSPField]
        public bool targetDockingPorts = false;

        [UI_Toggle(disabledText = "Standby", enabledText = "Active")]
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Radar: ", isPersistant = true)]
        public bool radarEnabled = false;

        [UI_Toggle(disabledText = "Ignore", enabledText = "Target")]
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Debris: ", isPersistant = true)]
        public bool targetDebris = false;

        private Transform scanTransform;
        // Because the docking port's basis isn't the same as the part's, we
        // have to look at the forward vector instead of the up vector.
        private bool scanTransformIsDockingNode;

        private string nodeType = string.Empty;

        // Piecewise scanning data:
        int currentScanTarget = 0;
        double currentScanDistance = 0.0;
        double currentScanDistanceSquared = 0.0;

        /// <summary>
        /// Initialize the radar system.
        /// </summary>
        public void Start()
        {
            if (!string.IsNullOrEmpty(resourceName))
            {
                try
                {
                    PartResourceDefinition def = PartResourceLibrary.Instance.resourceDefinitions[resourceName];
                    resourceId = def.id;
                }
                catch (Exception)
                {
                    Utility.LogErrorMessage(this, "Unable to find a resource ID for \"{0}\".  Disabling resource consumption.", resourceName);
                    resourceAmount = 0.0f;
                }
            }
            else
            {
                resourceAmount = 0.0f;
            }

            if (!HighLogic.LoadedSceneIsFlight)
            {
                return;
            }

            scanAngle = Mathf.Clamp(scanAngle, 0.01f, 180.0f);

            maxRangeMeters = maxRange * 1000.0;
            maxRangeMetersSquared = maxRangeMeters * maxRangeMeters;

            if (!string.IsNullOrEmpty(radarTransform))
            {
                scanTransformIsDockingNode = false;
                try
                {
                    Transform[] transforms = part.FindModelTransforms(radarTransform);
                    scanTransform = transforms[0];
                }
                catch (Exception e)
                {
                    Utility.ComplainLoudly("MASRadar cannot find the radar transform");
                    Utility.LogErrorMessage(this, "Unable to find the named transform {0}", radarTransform);
                    Utility.LogErrorMessage(this, "Exception: {0}", e);
                    scanTransform = part.transform;
                }
            }
            else
            {
                scanTransform = part.transform;
                scanTransformIsDockingNode = false;
                try
                {
                    ModuleDockingNode dockingNode = part.FindModuleImplementing<ModuleDockingNode>();
                    if (dockingNode != null)
                    {
                        scanTransform = dockingNode.nodeTransform;
                        nodeType = dockingNode.nodeType;
                        scanTransformIsDockingNode = true;
                    }
                }
                catch (Exception e)
                {
                    Utility.LogErrorMessage(this, "Setting dockingNode transform.");
                    Utility.LogErrorMessage(this, "Exception: {0}", e);
                }
            }

        }

        /// <summary>
        /// Do stuff, or not, depending on whether we're supposed to.
        /// </summary>
        public void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsEditor || !vessel.isActiveVessel)
            {
                return;
            }

            if (radarEnabled)
            {
                hasPower = true;
                // Resources check
                if (resourceAmount > 0.0f)
                {
                    float requested = resourceAmount * TimeWarp.fixedDeltaTime;
                    float supplied = part.RequestResource(resourceId, requested);
                    if (supplied < requested * 0.5f)
                    {
                        hasPower = false;
                        return;
                    }
                }


                FlightGlobals fg = FlightGlobals.fetch;
                ITargetable target = fg.VesselTarget;
                // Can't test only null: we check only one vessel per FixedUpdate,
                // so we must iterate over all of the vessels in the vessels list
                // before we can punt.
                if (target == null || currentScanTarget < fg.vessels.Count)
                {
                    // Scan
                    StepScanForTarget(fg);
                }
                else if (targetDockingPorts && (target is Vessel))
                {
                    Vessel targetVessel = target as Vessel;

                    if (!targetVessel.packed && targetVessel.loaded)
                    {
                        // Attempt to refine our target.
                        ModuleDockingNode closestNode = null;
                        float closestDistance = float.MaxValue;
                        List<ModuleDockingNode> docks = targetVessel.FindPartModulesImplementing<ModuleDockingNode>();
                        if (docks != null)
                        {
                            for (int i = docks.Count - 1; i >= 0; --i)
                            {
                                ModuleDockingNode dock = docks[i];
                                if (dock.state == "Ready" && (string.IsNullOrEmpty(nodeType) || nodeType == dock.nodeType))
                                {
                                    Vector3 vectorToTarget = (dock.part.transform.position - scanTransform.position);
                                    if (vectorToTarget.sqrMagnitude < closestDistance)
                                    {
                                        closestDistance = vectorToTarget.sqrMagnitude;
                                        closestNode = dock;
                                    }
                                }
                            }
                        }

                        if (closestNode != null)
                        {
                            fg.SetVesselTarget(closestNode);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Scan for a target.  We do this piecewise, instead of iterating over
        /// the *entire* vessels list every FixedUpdate.
        /// </summary>
        private void StepScanForTarget(FlightGlobals fg)
        {
            try
            {
                List<Vessel> vessels = fg.vessels;

                // If we've looped over the list, reset our counter.
                if (currentScanTarget >= vessels.Count)
                {
                    currentScanTarget = 0;
                }

                // If we've reset our counter, reset our tracking variables
                if (currentScanTarget == 0)
                {
                    currentScanDistance = maxRangeMeters;
                    currentScanDistanceSquared = maxRangeMetersSquared;
                }

                // See if this one is a candidate.
                Vessel candidate = vessels[currentScanTarget];

                VesselType vesselType = candidate.vesselType;
                if (candidate.id != vessel.id && !(vesselType == VesselType.EVA || vesselType == VesselType.Flag || vesselType == VesselType.Unknown) && (vesselType != VesselType.Debris || targetDebris))
                {
                    Vector3d distance = (candidate.GetTransform().position - scanTransform.position);
                    double manhattanDistance = Math.Max(Math.Abs(distance.x), Math.Max(Math.Abs(distance.y), Math.Abs(distance.z)));
                    if (manhattanDistance < currentScanDistance)
                    {
                        // Within Manhattan distance.  Check for real distance (squared, so we're not wasting cycles on a square root operation).
                        double distSq = distance.sqrMagnitude;
                        if (distSq < currentScanDistanceSquared)
                        {
                            float angle = Vector3.Angle(distance.normalized, (scanTransformIsDockingNode) ? scanTransform.forward : scanTransform.up);
                            if (angle < scanAngle)
                            {
                                currentScanDistanceSquared = distSq;
                                currentScanDistance = Math.Sqrt(distSq);
                                fg.SetVesselTarget(candidate);
                            }
                        }
                    }
                }
            }
            catch
            {

            }
            // Done with this iteration.  Increment the counter.
            ++currentScanTarget;
        }

        /// <summary>
        /// Return the info string viewable in the Editor.
        /// </summary>
        /// <returns></returns>
        public override string GetInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(string.Format("Max Range: {0:0.0}km\nUp to {1:0.0}° off-axis", maxRange, scanAngle));
            if (resourceAmount > 0.0f)
            {
                sb.Append(string.Format("\nConsumes {0:0.000} {1}/sec", resourceAmount, resourceName));
            }
            if (targetDockingPorts)
            {
                sb.Append("\nWill select nearest docking port on target vessel.");
            }
            return sb.ToString();
        }

        [KSPAction("Turn Radar Off")]
        public void RadarOffAction(KSPActionParam param)
        {
            radarEnabled = false;
        }

        [KSPAction("Turn Radar On")]
        public void RadarOnAction(KSPActionParam param)
        {
            radarEnabled = true;
        }

        [KSPAction("Toggle Radar")]
        public void ToggleRadarAction(KSPActionParam param)
        {
            radarEnabled = !radarEnabled;
        }
    }
}
