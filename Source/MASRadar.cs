/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2018 MOARdV
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
        /// are close?  Only applies to radars installed on a part that has a ModuleDockingPort.
        /// </summary>
        [KSPField]
        public bool targetDockingPorts = false;
        private ModuleDockingNode dock = null;

        /// <summary>
        /// Optional: provides the index number for another module on the same part that controls
        /// whether the radar can function or not.  This module needs to be a child of ModuleDeployablePart
        /// (ModuleDeployableAntenna, ModuleDeployableRadiator, ModuleDeployableSolarPanel), or ModuleAnimateGeneric.
        /// THis is a 0-based index.
        /// </summary>
        [KSPField]
        public int DeployFxModules = -1;
        private ModuleAnimateGeneric deployAnimator = null;
        private ModuleDeployablePart deployPart = null;

        [KSPField(guiActive = true, guiName = "#MAS_Radar_Status")]
        public string statusString;
        public enum RadarStatus
        {
            STANDBY,
            SCANNING,
            TRACKING,
            NOT_DEPLOYED,
            BROKEN,
            NO_POWER,
        };
        private RadarStatus status = RadarStatus.STANDBY;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "#MAS_Radar", isPersistant = true)]
        [UI_Toggle(disabledText = "#autoLOC_6001073", enabledText = "#autoLOC_6001074")]
        public bool radarEnabled = false;

        [UI_Toggle(disabledText = "#MAS_Radar_Debris_Ignore", enabledText = "#MAS_Radar_Debris_Target")]
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "#MAS_Radar_Debris", isPersistant = true)]
        public bool targetDebris = false;

        private Transform scanTransform;
        // Because the docking port's basis isn't the same as the part's, we
        // have to look at the forward vector instead of the up vector.
        private bool scanTransformIsDockingNode;

        private string nodeType = string.Empty;

        /// <summary>
        /// Initialize the radar system.
        /// </summary>
        public void Start()
        {
            if (resourceAmount > 0.0f && !string.IsNullOrEmpty(resourceName))
            {
                try
                {
                    PartResourceDefinition def = PartResourceLibrary.Instance.resourceDefinitions[resourceName];
                    resourceId = def.id;
                }
                catch (Exception)
                {
                    Utility.LogError(this, "Unable to find a resource ID for \"{0}\".  Disabling resource consumption.", resourceName);
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
                    Utility.LogError(this, "Unable to find the named transform {0}", radarTransform);
                    Utility.LogError(this, "Exception: {0}", e);
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
                        dock = dockingNode;
                        scanTransform = dockingNode.nodeTransform;
                        nodeType = dockingNode.nodeType;
                        scanTransformIsDockingNode = true;
                    }
                }
                catch (Exception e)
                {
                    Utility.LogError(this, "Setting dockingNode transform.");
                    Utility.LogError(this, "Exception: {0}", e);
                }
            }

            if (DeployFxModules >= 0)
            {
                try
                {
                    PartModule pm = part.Modules[DeployFxModules];
                    if (pm != null)
                    {
                        if (pm is ModuleDeployablePart)
                        {
                            deployPart = pm as ModuleDeployablePart;
                        }
                        else if (pm is ModuleAnimateGeneric)
                        {
                            deployAnimator = pm as ModuleAnimateGeneric;
                        }
                    }
                }
                catch (Exception e)
                {
                    Utility.LogError(this, "Grabbing deploy module.");
                    Utility.LogError(this, "Exception: {0}", e);
                }
            }
            targetDockingPorts = targetDockingPorts && scanTransformIsDockingNode;

            UpdateRadarStatus();
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
                bool radarDeployed = true;
                bool radarBroken = false;
                if (deployPart != null)
                {
                    radarDeployed = deployPart.useAnimation && deployPart.deployState == ModuleDeployablePart.DeployState.EXTENDED;
                    radarBroken = deployPart.deployState == ModuleDeployablePart.DeployState.BROKEN;
                }
                else if (deployAnimator != null)
                {
                    radarDeployed = deployAnimator.IsMoving() == false && deployAnimator.animTime == 1.0f;
                }

                if (!radarDeployed)
                {
                    // If the radar's not deployed, we're done.
                    status = (radarBroken) ? RadarStatus.BROKEN : RadarStatus.NOT_DEPLOYED;
                    UpdateRadarStatus();
                    return;
                }
                status = RadarStatus.SCANNING;

                // Resources check
                if (resourceAmount > 0.0f)
                {
                    double requested = resourceAmount * TimeWarp.fixedDeltaTime;
                    double supplied = part.RequestResource(resourceId, requested);
                    if (supplied < requested * 0.5f)
                    {
                        status = RadarStatus.NO_POWER;
                        UpdateRadarStatus();
                        return;
                    }
                }

                FlightGlobals fg = FlightGlobals.fetch;
                ITargetable target = fg.VesselTarget;
                if (target == null)
                {
                    // Scan
                    ScanForTarget(fg);
                }
                else if (targetDockingPorts && dock != null && (target is Vessel))
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
                                ModuleDockingNode otherDock = docks[i];
                                // Only lock on to an available dock of the same type that is either ungendered or the opposite gender.
                                if (otherDock.state == "Ready" && (string.IsNullOrEmpty(nodeType) || nodeType == otherDock.nodeType) && (dock.gendered == false || dock.genderFemale != otherDock.genderFemale))
                                {
                                    Vector3 vectorToTarget = (otherDock.part.transform.position - scanTransform.position);
                                    if (vectorToTarget.sqrMagnitude < closestDistance)
                                    {
                                        closestDistance = vectorToTarget.sqrMagnitude;
                                        closestNode = otherDock;
                                    }
                                }
                            }
                        }

                        if (closestNode != null)
                        {
                            fg.SetVesselTarget(closestNode);
                            status = RadarStatus.TRACKING;
                        }
                    }
                }
                else
                {
                    status = RadarStatus.TRACKING;
                }
            }
            else
            {
                status = RadarStatus.STANDBY;
            }

            UpdateRadarStatus();
        }

        /// <summary>
        /// Scan for a target.
        /// </summary>
        private void ScanForTarget(FlightGlobals fg)
        {
            try
            {
                List<Vessel> vessels = fg.vessels;

                double currentScanDistance = maxRangeMeters;
                double currentScanDistanceSquared = maxRangeMetersSquared;

                Vessel candidateTarget = null;
                for (int currentScanTarget = vessels.Count - 1; currentScanTarget >= 0; --currentScanTarget)
                {
                    // See if this one is a candidate.
                    Vessel candidate = vessels[currentScanTarget];

                    VesselType vesselType = candidate.vesselType;
                    if (candidate.id != vessel.id && candidate.mainBody == vessel.mainBody && !(vesselType == VesselType.EVA || vesselType == VesselType.Flag || vesselType == VesselType.Unknown) && (vesselType != VesselType.Debris || targetDebris))
                    {
                        Vector3d displacement = (candidate.GetTransform().position - scanTransform.position);
                        double cardinalDistance = Math.Max(Math.Abs(displacement.x), Math.Max(Math.Abs(displacement.y), Math.Abs(displacement.z)));
                        if (cardinalDistance < currentScanDistance)
                        {
                            // Within the current scanning distance on one axis.  Check for real distance (squared, so we're not wasting cycles on a square root operation).
                            double distSq = displacement.sqrMagnitude;
                            if (distSq < currentScanDistanceSquared)
                            {
                                // See if it's within our scanning angle.
                                float angle = Vector3.Angle(displacement.normalized, (scanTransformIsDockingNode) ? scanTransform.forward : scanTransform.up);
                                if (angle < scanAngle)
                                {
                                    currentScanDistanceSquared = distSq;
                                    currentScanDistance = Math.Sqrt(distSq);
                                    candidateTarget = candidate;
                                }
                            }
                        }
                    }
                }

                if (candidateTarget != null)
                {
                    status = RadarStatus.TRACKING;
                    fg.SetVesselTarget(candidateTarget);
                }
            }
            catch
            {

            }
        }

        /// <summary>
        /// Return the radar module's name for the Editor.
        /// </summary>
        /// <returns></returns>
        public override string GetModuleDisplayName()
        {
            return "#MAS_Radar_Module_DisplayName";
        }

        /// <summary>
        /// Return the info string viewable in the Editor.
        /// </summary>
        /// <returns></returns>
        public override string GetInfo()
        {
            // This string is constructed before Start() is called, so we need to pre-process some data here.
            string resourceUiName = string.Empty;
            if (resourceAmount > 0.0f && !string.IsNullOrEmpty(resourceName))
            {
                try
                {
                    PartResourceDefinition def = PartResourceLibrary.Instance.resourceDefinitions[resourceName];
                    resourceId = def.id;
                    resourceUiName = def.displayName;
                }
                catch (Exception)
                {
                    Utility.LogError(this, "Unable to find a resource ID for \"{0}\".  Disabling resource consumption.", resourceName);
                    resourceAmount = 0.0f;
                }
            }
            else
            {
                resourceAmount = 0.0f;
            }

            StringBuilder sb = StringBuilderCache.Acquire();
            sb.Append(KSP.Localization.Localizer.Format("#MAS_Radar_Info_1", string.Format("{0:0.0}", maxRange), string.Format("{0:0.0}", scanAngle)));
            if (resourceAmount > 0.0f)
            {
                sb.Append(KSP.Localization.Localizer.Format("#MAS_Radar_Info_2", string.Format("{0:0.000}", resourceAmount), resourceUiName));
            }
            if (targetDockingPorts)
            {
                sb.Append(KSP.Localization.Localizer.GetStringByTag("#MAS_Radar_Info_3"));
            }
            return sb.ToStringAndRelease();
        }

        /// <summary>
        /// Update the radar status string based on current radar status.
        /// </summary>
        private void UpdateRadarStatus()
        {
            switch (status)
            {
                case RadarStatus.STANDBY:
                    statusString = KSP.Localization.Localizer.GetStringByTag("#MAS_Radar_Status_Standby");
                    break;
                case RadarStatus.SCANNING:
                    statusString = KSP.Localization.Localizer.GetStringByTag("#MAS_Radar_Status_Scanning");
                    break;
                case RadarStatus.TRACKING:
                    statusString = KSP.Localization.Localizer.GetStringByTag("#MAS_Radar_Status_Tracking");
                    break;
                case RadarStatus.NOT_DEPLOYED:
                    statusString = KSP.Localization.Localizer.GetStringByTag("#MAS_Radar_Status_Not_Deployed");
                    break;
                case RadarStatus.BROKEN:
                    statusString = KSP.Localization.Localizer.GetStringByTag("#MAS_Radar_Status_Broken");
                    break;
                case RadarStatus.NO_POWER:
                    statusString = KSP.Localization.Localizer.GetStringByTag("#MAS_Radar_Status_No_Power");
                    break;
            }
        }

        [KSPAction("#MAS_Radar_Switch_Off")]
        public void RadarOffAction(KSPActionParam param)
        {
            radarEnabled = false;
        }

        [KSPAction("#MAS_Radar_Switch_On")]
        public void RadarOnAction(KSPActionParam param)
        {
            radarEnabled = true;
        }

        [KSPAction("#MAS_Radar_Toggle")]
        public void ToggleRadarAction(KSPActionParam param)
        {
            radarEnabled = !radarEnabled;
        }
    }
}
