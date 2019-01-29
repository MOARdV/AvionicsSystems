//#define TIME_REBUILD
//#define TIME_UPDATES
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
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    internal partial class MASVesselComputer : MonoBehaviour
    {
        // Tracks per-module data.

        internal bool modulesInvalidated = true;

        #region Action Groups
        //---Action Groups
        private bool[] hasActionGroup = new bool[17];

        /// <summary>
        /// Returns true if there is at least one action in this action group.
        /// </summary>
        /// <param name="ag"></param>
        /// <returns></returns>
        internal bool GroupHasActions(KSPActionGroup ag)
        {
            int index = -1;
            for (int ui = (int)ag; ui != 0; ui >>= 1)
            {
                ++index;
            }

            if (index < 0 || index > hasActionGroup.Length)
            {
                // Should never happen!
                throw new ArgumentOutOfRangeException("MASVesselComputer.GroupHasActions() called with invalid action group!");
            }

            return hasActionGroup[index];
        }

        /// <summary>
        /// Sets the indicated action group to true (meaning there is an action)
        /// </summary>
        /// <param name="ag"></param>
        private void SetActionGroup(KSPActionGroup ag)
        {
            int index = 0;
            for (int ui = (int)ag; ui != 0; ui >>= 1)
            {
                if ((ui & 0x1) != 0)
                {
                    hasActionGroup[index] = true;
                }
                ++index;
            }
        }
        #endregion

        #region Air Brakes
        private List<ModuleAeroSurface> airBrakeList = new List<ModuleAeroSurface>();
        internal ModuleAeroSurface[] moduleAirBrake = new ModuleAeroSurface[0];
        #endregion

        #region Aircraft Engines
        private List<MASThrustReverser> thrustReverserList = new List<MASThrustReverser>();
        internal MASThrustReverser[] moduleThrustReverser = new MASThrustReverser[0];
        private List<ModuleResourceIntake> resourceIntakeList = new List<ModuleResourceIntake>();
        internal ModuleResourceIntake[] moduleResourceIntake = new ModuleResourceIntake[0];
        #endregion

        #region Brakes
        private List<ModuleWheels.ModuleWheelBrakes> brakesList = new List<ModuleWheels.ModuleWheelBrakes>();
        internal ModuleWheels.ModuleWheelBrakes[] moduleBrakes = new ModuleWheels.ModuleWheelBrakes[0];
        #endregion

        #region Cameras
        private List<MASCamera> cameraList = new List<MASCamera>(4);
        internal MASCamera[] moduleCamera = new MASCamera[0];
        private int dockCamCount = 0;
        private int camCount = 0;
        private bool camerasReset = true;
        private void UpdateCamera()
        {
            if (camerasReset)
            {
                camerasReset = false;
                for (int i = moduleCamera.Length - 1; i >= 0; --i)
                {
                    if (string.IsNullOrEmpty(moduleCamera[i].cameraName))
                    {
                        if (moduleCamera[i].isDockingPortCamera)
                        {
                            ++dockCamCount;
                            moduleCamera[i].cameraName = string.Format("Dock {0}", dockCamCount);
                        }
                        else
                        {
                            ++camCount;
                            moduleCamera[i].cameraName = string.Format("Camera {0}", camCount);
                        }
                    }
                    else
                    {
                        MASCamera[] dupeNames = Array.FindAll(moduleCamera, x => x.cameraName == moduleCamera[i].cameraName);

                        if (dupeNames.Length > 1)
                        {
                            for (int dupeIndex = 0; dupeIndex < dupeNames.Length; ++dupeIndex)
                            {
                                dupeNames[dupeIndex].cameraName = string.Format("{0} {1}", dupeNames[dupeIndex].cameraName, dupeIndex + 1);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Search the list of camera modules to find the named camera.
        /// </summary>
        /// <param name="cameraName">The name to search for</param>
        /// <returns>The camera module, or null if it was not found.</returns>
        internal MASCamera FindCameraModule(string cameraName)
        {
            for (int i = moduleCamera.Length - 1; i >= 0; --i)
            {
                if (moduleCamera[i].cameraName == cameraName)
                {
                    return moduleCamera[i];
                }
            }

            return null;
        }
        #endregion

        #region Cargo Bay
        private List<ModuleCargoBay> cargoBayList = new List<ModuleCargoBay>(2);
        internal ModuleCargoBay[] moduleCargoBay = new ModuleCargoBay[0];
        internal float cargoBayDirection = 0.0f;
        internal float cargoBayPosition = 0.0f;
        void UpdateCargoBay()
        {
            if (moduleCargoBay.Length > 0)
            {
                float newPosition = 0.0f;
                float numBays = 0.0f;
                for (int i = moduleCargoBay.Length - 1; i >= 0; --i)
                {
                    ModuleCargoBay me = moduleCargoBay[i];
                    PartModule deployer = me.part.Modules[me.DeployModuleIndex];
                    /*if (deployer is ModuleServiceModule)
                    {
                        ModuleServiceModule msm = deployer as ModuleServiceModule;
                        if (msm.IsDeployed)
                        {
                            cargoBayDeployed = true;
                        }
                        else
                        {
                            cargoBayRetracted = true;
                        }
                    }
                    else*/
                    if (deployer is ModuleAnimateGeneric)
                    {
                        ModuleAnimateGeneric mag = deployer as ModuleAnimateGeneric;
                        newPosition += Mathf.InverseLerp(me.closedPosition, Mathf.Abs(1.0f - me.closedPosition), mag.animTime);
                    }
                }

                if (numBays > 1.0f)
                {
                    newPosition /= numBays;
                }

                cargoBayDirection = newPosition - cargoBayPosition;
                if (cargoBayDirection < 0.0f)
                {
                    cargoBayDirection = -1.0f;
                }
                else if (cargoBayDirection > 0.0f)
                {
                    cargoBayDirection = 1.0f;
                }
                cargoBayPosition = newPosition;
            }
        }
        #endregion

        #region Communications
        private List<ModuleDeployableAntenna> antennaList = new List<ModuleDeployableAntenna>(8);
        internal ModuleDeployableAntenna[] moduleAntenna = new ModuleDeployableAntenna[0];
        internal bool antennaDeployable;
        internal bool antennaRetractable;
        internal int antennaMoving;
        internal bool antennaDamaged;
        private void UpdateAntenna()
        {
            // TODO: What about detecting if dynamic pressure is low enough to deploy antennae?
            antennaDeployable = false;
            antennaRetractable = false;
            antennaMoving = 0;
            antennaDamaged = false;

            for (int i = moduleAntenna.Length - 1; i >= 0; --i)
            {
                if (moduleAntenna[i].useAnimation)
                {
                    antennaRetractable |= (moduleAntenna[i].retractable && moduleAntenna[i].deployState == ModuleDeployablePart.DeployState.EXTENDED);
                    antennaDeployable |= (moduleAntenna[i].deployState == ModuleDeployablePart.DeployState.RETRACTED);
                    if (moduleAntenna[i].deployState == ModuleDeployablePart.DeployState.RETRACTING)
                    {
                        antennaMoving = -1;
                    }
                    else if (moduleAntenna[i].deployState == ModuleDeployablePart.DeployState.EXTENDING)
                    {
                        antennaMoving = 1;
                    }
                    antennaDamaged |= (moduleAntenna[i].deployState == ModuleDeployablePart.DeployState.BROKEN);
                }
            }
        }

        #endregion

        #region Docking
        // Unlike some of the other sections here, the Dock section focuses on
        // a single ModuleDockingNode that the vessel computer designates as
        // the "main" docking node.  We do this because we do NOT want an
        // undock event, for instance, to disassemble an entire space
        // station, and we never want to affect more than one docking port with
        // such transactions.  Code substantially imported from what I wrote for
        // RasterPropMonitor.
        internal ModuleDockingNode dockingNode;
        internal DockingNodeState dockingNodeState = DockingNodeState.UNKNOWN;
        internal enum DockingNodeState
        {
            UNKNOWN,
            DOCKED,
            PREATTACHED,
            READY
        };
        private void UpdateDockingNode(Part referencePart)
        {
            // Our candidate for docking node.  Called from UpdateReferenceTransform()
            ModuleDockingNode dockingNode = null;

            if (referencePart != null)
            {
                // See if the reference part is a docking port.
                if (referenceTransformType == ReferenceType.DockingPort)
                {
                    // If it's a docking port, this should be all we need to do.
                    dockingNode = referencePart.FindModuleImplementing<ModuleDockingNode>();
                }
                else
                {
                    uint shipFlightNumber = 0;
                    if (referenceTransformType == ReferenceType.Self)
                    {
                        // If the reference transform is the current IVA, we need
                        // to look for another part that has a docking node and the
                        // same ID as our part.
                        shipFlightNumber = referencePart.launchID;
                    }
                    else
                    {
                        if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA)
                        {
                            Kerbal refKerbal = CameraManager.Instance.IVACameraActiveKerbal;
                            if (refKerbal != null)
                            {
                                shipFlightNumber = refKerbal.InPart.launchID;
                            }
                        }
                    }

                    if (shipFlightNumber > 0)
                    {
                        List<Part> vesselParts = vessel.Parts;
                        for (int i = vesselParts.Count - 1; i >= 0; --i)
                        {
                            Part p = vesselParts[i];
                            if (p.launchID == shipFlightNumber)
                            {
                                dockingNode = p.FindModuleImplementing<ModuleDockingNode>();
                                if (dockingNode != null)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            this.dockingNode = dockingNode;
        }

        private void UpdateDockingNodeState()
        {
            if (dockingNode != null)
            {
                switch (dockingNode.state)
                {
                    case "PreAttached":
                        dockingNodeState = DockingNodeState.PREATTACHED;
                        break;
                    case "Docked (docker)":
                        dockingNodeState = DockingNodeState.DOCKED;
                        break;
                    case "Docked (dockee)":
                        dockingNodeState = DockingNodeState.DOCKED;
                        break;
                    case "Ready":
                        dockingNodeState = DockingNodeState.READY;
                        break;
                    default:
                        dockingNodeState = DockingNodeState.UNKNOWN;
                        break;
                }
            }
            else
            {
                dockingNodeState = DockingNodeState.UNKNOWN;
            }
        }
        #endregion

        #region Engines
        //---Engines
        private List<MASIdEngine> idEnginesList = new List<MASIdEngine>();
        internal MASIdEngine[] moduleIdEngines = new MASIdEngine[0];
        private List<ModuleEngines> enginesList = new List<ModuleEngines>(8);
        internal ModuleEngines[] moduleEngines = new ModuleEngines[0];
        private List<MultiModeEngine> multiModeEngineList = new List<MultiModeEngine>();
        internal MultiModeEngine[] multiModeEngines = new MultiModeEngine[0];
        private List<MASIdEngineGroup> engineGroupList = new List<MASIdEngineGroup>();
        internal MASIdEngineGroup[] engineGroup = new MASIdEngineGroup[0];
        internal PartModule moduleGraviticEngine = null;
        private float[] invMaxISP = new float[0];
        internal float currentThrust; // current net thrust, kN
        internal float currentLimitedMaxThrust; // Max thrust, accounting for throttle limits, kN
        internal float currentMaxThrust; // Max possible thrust at current altitude, kN
        internal float maxRatedThrust; // Max possible thrust, kN
        internal float maxEngineFuelFlow; // max fuel flow, g/s
        internal float currentEngineFuelFlow; // current fuel flow, g/s
        internal float currentIsp;
        internal float maxIsp;
        internal float hottestEngineTemperature;
        internal float hottestEngineMaxTemperature;
        internal float hottestEngineSign;
        internal int currentEngineCount;
        internal int activeEngineCount;
        internal float throttleLimit;
        internal bool anyEnginesFlameout;
        internal bool anyEnginesEnabled;
        private List<Part> visitedParts = new List<Part>();
        private bool UpdateEngines()
        {
            this.currentThrust = 0.0f;
            this.maxRatedThrust = 0.0f;
            currentLimitedMaxThrust = 0.0f;
            currentMaxThrust = 0.0f;
            hottestEngineMaxTemperature = 0.0f;
            hottestEngineSign = 0.0f;
            maxEngineFuelFlow = 0.0f;
            currentEngineFuelFlow = 0.0f;
            throttleLimit = 0.0f;
            float throttleCount = 0.0f;
            anyEnginesFlameout = false;
            anyEnginesEnabled = false;
            activeEngineCount = 0;

            float hottestEngine = float.MaxValue;
            float currentHottestEngine = 0.0f;
            float maxIspContribution = 0.0f;
            float averageIspContribution = 0.0f;

            visitedParts.Clear();

            bool requestReset = false;
            for (int i = moduleEngines.Length - 1; i >= 0; --i)
            {
                ModuleEngines me = moduleEngines[i];
                requestReset |= (!me.isEnabled);

                Part thatPart = me.part;
                if (thatPart.inverseStage == StageManager.CurrentStage)
                {
                    if (!visitedParts.Contains(thatPart))
                    {
                        currentEngineCount++;
                        if (me.getIgnitionState)
                        {
                            activeEngineCount++;
                        }
                        visitedParts.Add(thatPart);
                    }
                }

                anyEnginesEnabled |= me.allowShutdown && me.getIgnitionState;
                anyEnginesFlameout |= (me.isActiveAndEnabled && me.flameout);

                if (me.EngineIgnited && me.isEnabled)
                {
                    throttleLimit += me.thrustPercentage;
                    throttleCount += 1.0f;

                    float currentThrust = me.finalThrust;
                    this.currentThrust += currentThrust;
                    this.maxRatedThrust += me.GetMaxThrust();
                    float rawMaxThrust = me.GetMaxThrust() * me.realIsp * invMaxISP[i];
                    currentMaxThrust += rawMaxThrust;
                    float maxThrust = rawMaxThrust * me.thrustPercentage * 0.01f;
                    currentLimitedMaxThrust += maxThrust;
                    float realIsp = me.realIsp;

                    if (realIsp > 0.0f)
                    {
                        averageIspContribution += maxThrust / realIsp;

                        // Compute specific fuel consumption and
                        // multiply by thrust to get grams/sec fuel flow
                        float specificFuelConsumption = 101972f / realIsp;
                        maxEngineFuelFlow += specificFuelConsumption * rawMaxThrust;
                        currentEngineFuelFlow += specificFuelConsumption * currentThrust;
                    }
                    if (invMaxISP[i] > 0.0f)
                    {
                        maxIspContribution += maxThrust * invMaxISP[i];
                    }

                    List<PartResourceDefinition> propellants = me.GetConsumedResources();
                    for (int res = propellants.Count - 1; res >= 0; --res)
                    {
                        MarkActiveEnginePropellant(propellants[res].id);
                    }
                }

                if (thatPart.skinMaxTemp - thatPart.skinTemperature < hottestEngine)
                {
                    currentHottestEngine = (float)thatPart.skinTemperature;
                    hottestEngineMaxTemperature = (float)thatPart.skinMaxTemp;
                    hottestEngine = hottestEngineMaxTemperature - currentHottestEngine;
                }
                if (thatPart.maxTemp - thatPart.temperature < hottestEngine)
                {
                    currentHottestEngine = (float)thatPart.temperature;
                    hottestEngineMaxTemperature = (float)thatPart.maxTemp;
                    hottestEngine = hottestEngineMaxTemperature - currentHottestEngine;
                }
            }

            hottestEngineSign = Mathf.Sign(currentHottestEngine - hottestEngineTemperature);
            hottestEngineTemperature = currentHottestEngine;

            if (throttleCount > 0.0f)
            {
                throttleLimit /= (throttleCount * 100.0f);
            }

            if (averageIspContribution > 0.0f)
            {
                currentIsp = currentLimitedMaxThrust / averageIspContribution;
            }
            else
            {
                currentIsp = 0.0f;
            }

            if (maxIspContribution > 0.0f)
            {
                maxIsp = currentLimitedMaxThrust / maxIspContribution;
            }
            else
            {
                maxIsp = 0.0f;
            }

            return requestReset;
        }
        internal bool SetEnginesEnabled(bool newState)
        {
            for (int i = moduleEngines.Length - 1; i >= 0; --i)
            {
                Part thatPart = moduleEngines[i].part;

                if (thatPart.inverseStage == StageManager.CurrentStage || !newState)
                {
                    if (moduleEngines[i].EngineIgnited != newState)
                    {
                        if (newState && moduleEngines[i].allowRestart)
                        {
                            moduleEngines[i].Activate();
                        }
                        else if (moduleEngines[i].allowShutdown)
                        {
                            moduleEngines[i].Shutdown();
                        }
                    }
                }
            }

            return newState;
        }

        internal bool SetThrottleLimit(float newLimit)
        {
            bool anyUpdated = false;
            for (int i = moduleEngines.Length - 1; i >= 0; --i)
            {
                Part thatPart = moduleEngines[i].part;

                if (thatPart.inverseStage == StageManager.CurrentStage)
                {
                    if (moduleEngines[i].EngineIgnited)
                    {
                        moduleEngines[i].thrustPercentage = newLimit;
                        anyUpdated = true;
                    }
                }
            }

            return anyUpdated;
        }
        #endregion

        #region Launch Clamp
        private List<LaunchClamp> launchClampList = new List<LaunchClamp>();
        internal LaunchClamp[] moduleLaunchClamp = new LaunchClamp[0];
        #endregion

        #region Gimbal
        private List<ModuleGimbal> gimbalsList = new List<ModuleGimbal>(8);
        internal ModuleGimbal[] moduleGimbals = new ModuleGimbal[0];
        internal bool anyGimbalsLocked = false;
        internal bool anyGimbalsRoll = false;
        internal bool anyGimbalsPitch = false;
        internal bool anyGimbalsYaw = false;
        internal bool anyGimbalsActive = false;
        internal bool activeEnginesGimbal = false;
        internal float gimbalDeflection = 0.0f;
        internal Vector2 gimbalAxisDeflection = Vector2.zero;
        internal float gimbalLimit = 0.0f;
        void UpdateGimbals()
        {
            gimbalLimit = 0.0f;
            gimbalDeflection = 0.0f;
            anyGimbalsLocked = false;
            anyGimbalsActive = false;
            anyGimbalsRoll = false;
            anyGimbalsPitch = false;
            anyGimbalsYaw = false;
            Vector2 localDeflection = Vector2.zero;
            gimbalAxisDeflection = Vector2.zero;
            float gimbalCount = 0.0f;

            for (int i = moduleGimbals.Length - 1; i >= 0; --i)
            {
                if (moduleGimbals[i].gimbalLock)
                {
                    anyGimbalsLocked = true;
                }
                else if (moduleGimbals[i].gimbalActive)
                {
                    anyGimbalsActive = true;

                    anyGimbalsRoll |= moduleGimbals[i].enableRoll;
                    anyGimbalsPitch |= moduleGimbals[i].enablePitch;
                    anyGimbalsYaw |= moduleGimbals[i].enableYaw;

                    gimbalCount += 1.0f;

                    gimbalLimit += moduleGimbals[i].gimbalLimiter;

                    localDeflection.x = moduleGimbals[i].actuationLocal.x;
                    // all gimbal range values are positive scalars.
                    if (localDeflection.x < 0.0f)
                    {
                        localDeflection.x /= moduleGimbals[i].gimbalRangeXN;
                    }
                    else
                    {
                        localDeflection.x /= moduleGimbals[i].gimbalRangeXP;
                    }

                    localDeflection.y = moduleGimbals[i].actuationLocal.y;
                    if (localDeflection.y < 0.0f)
                    {
                        localDeflection.y /= moduleGimbals[i].gimbalRangeYN;
                    }
                    else
                    {
                        localDeflection.y /= moduleGimbals[i].gimbalRangeYP;
                    }

                    gimbalAxisDeflection += localDeflection;
                    gimbalDeflection += localDeflection.magnitude;
                }
            }

            if (gimbalCount > 0.0f)
            {
                float invGimbalCount = 1.0f / gimbalCount;

                gimbalDeflection *= invGimbalCount;
                // Must convert from [0, 100] to [0, 1], so multiply by 0.01.
                gimbalLimit *= invGimbalCount * 0.01f;
                gimbalAxisDeflection.x *= invGimbalCount;
                gimbalAxisDeflection.y *= invGimbalCount;
            }
        }
        #endregion

        #region Parachutes
        private List<PartModule> realchuteList = new List<PartModule>(8);
        internal PartModule[] moduleRealChute = new PartModule[0];
        private List<ModuleParachute> parachuteList = new List<ModuleParachute>(8);
        internal ModuleParachute[] moduleParachute = new ModuleParachute[0];
        #endregion

        #region Power Production
        private List<ModuleAlternator> alternatorList = new List<ModuleAlternator>();
        internal ModuleAlternator[] moduleAlternator = new ModuleAlternator[0];
        private List<ModuleGenerator> generatorList = new List<ModuleGenerator>();
        internal ModuleGenerator[] moduleGenerator = new ModuleGenerator[0];
        internal List<float> generatorOutputList = new List<float>();
        internal float[] generatorOutput = new float[0];
        private List<ModuleDeployableSolarPanel> solarPanelList = new List<ModuleDeployableSolarPanel>();
        internal ModuleDeployableSolarPanel[] moduleSolarPanel = new ModuleDeployableSolarPanel[0];
        internal bool generatorActive;
        internal bool solarPanelsDeployable;
        internal float solarPanelsEfficiency;
        internal bool solarPanelsRetractable;
        internal int solarPanelsMoving;
        internal bool solarPanelsDamaged;
        internal float netAlternatorOutput;
        internal float netGeneratorOutput;
        internal float netSolarOutput;
        private void UpdatePower()
        {
            // TODO: What about detecting if dynamic pressure is low enough to deploy solar panels?
            netAlternatorOutput = 0.0f;
            netGeneratorOutput = 0.0f;
            netSolarOutput = 0.0f;
            solarPanelsEfficiency = 0.0f;

            generatorActive = false;
            solarPanelsDeployable = false;
            solarPanelsRetractable = false;
            solarPanelsMoving = 0;
            solarPanelsDamaged = false;

            for (int i = moduleGenerator.Length - 1; i >= 0; --i)
            {
                generatorActive |= (moduleGenerator[i].generatorIsActive && !moduleGenerator[i].isAlwaysActive);

                if (moduleGenerator[i].generatorIsActive)
                {
                    float output = moduleGenerator[i].efficiency * generatorOutput[i];
                    if (moduleGenerator[i].isThrottleControlled)
                    {
                        output *= moduleGenerator[i].throttle;
                    }
                    netGeneratorOutput += output;
                }
            }

            for (int i = moduleAlternator.Length - 1; i >= 0; --i)
            {
                // I assume there's only one ElectricCharge output in a given ModuleAlternator
                netAlternatorOutput += moduleAlternator[i].outputRate;
            }

            for (int i = moduleSolarPanel.Length - 1; i >= 0; --i)
            {
                netSolarOutput += moduleSolarPanel[i].flowRate;
                solarPanelsEfficiency += moduleSolarPanel[i].flowRate / moduleSolarPanel[i].chargeRate;

                if (moduleSolarPanel[i].useAnimation)
                {
                    solarPanelsRetractable |= (moduleSolarPanel[i].retractable && moduleSolarPanel[i].deployState == ModuleDeployablePart.DeployState.EXTENDED);
                    solarPanelsDeployable |= (moduleSolarPanel[i].deployState == ModuleDeployablePart.DeployState.RETRACTED);
                    if (moduleSolarPanel[i].deployState == ModuleDeployablePart.DeployState.RETRACTING)
                    {
                        solarPanelsMoving = -1;
                    }
                    else if (moduleSolarPanel[i].deployState == ModuleDeployablePart.DeployState.EXTENDING)
                    {
                        solarPanelsMoving = 1;
                    }
                    solarPanelsDamaged |= (moduleSolarPanel[i].deployState == ModuleDeployablePart.DeployState.BROKEN);
                }
            }
            if (solarPanelsEfficiency > 0.0f)
            {
                solarPanelsEfficiency /= (float)moduleSolarPanel.Length;
            }
        }
        #endregion

        #region Procedural Fairings
        private List<ModuleProceduralFairing> proceduralFairingList = new List<ModuleProceduralFairing>();
        internal ModuleProceduralFairing[] moduleProceduralFairing = new ModuleProceduralFairing[0];
        internal bool fairingsCanDeploy = false;
        private void UpdateProceduralFairing()
        {
            fairingsCanDeploy = false;

            for (int i = moduleProceduralFairing.Length - 1; i >= 0; --i)
            {
                if (moduleProceduralFairing[i].CanMove)
                {
                    fairingsCanDeploy = true;
                }
                //Utility.LogMessage(this, "fairing {0}: CanMove {1}",
                //    i, moduleProceduralFairing[i].CanMove);
            }
        }
        #endregion

        #region Radar
        private List<MASRadar> radarList = new List<MASRadar>();
        internal MASRadar[] moduleRadar = new MASRadar[0];
        #endregion

        #region RCS
        private List<ModuleRCS> rcsList = new List<ModuleRCS>(8);
        internal ModuleRCS[] moduleRcs = new ModuleRCS[0];
        internal bool anyRcsDisabled = false;
        internal bool anyRcsFiring = false;
        internal bool anyRcsRotate = false;
        internal bool anyRcsTranslate = false;
        internal float rcsWeightedThrustLimit;
        internal float rcsActiveThrustPercent;
        private void UpdateRcs()
        {
            anyRcsDisabled = false;
            anyRcsFiring = false;
            anyRcsRotate = false;
            anyRcsTranslate = false;
            float netThrust = 0.0f;
            rcsWeightedThrustLimit = 0.0f;
            rcsActiveThrustPercent = 0.0f;
            float numActiveThrusters = 0.0f;

            for (int i = moduleRcs.Length - 1; i >= 0; --i)
            {
                if (moduleRcs[i].rcsEnabled == false)
                {
                    anyRcsDisabled = true;
                }
                else
                {
                    if (moduleRcs[i].enableX || moduleRcs[i].enableY || moduleRcs[i].enableZ)
                    {
                        anyRcsTranslate = true;
                    }
                    if (moduleRcs[i].enableRoll || moduleRcs[i].enableYaw || moduleRcs[i].enablePitch)
                    {
                        anyRcsRotate = true;
                    }
                    if (moduleRcs[i].rcs_active)
                    {
                        for (int q = 0; q < moduleRcs[i].thrustForces.Length; ++q)
                        {
                            if (moduleRcs[i].thrustForces[q] > 0.0f)
                            {
                                rcsActiveThrustPercent += moduleRcs[i].thrustForces[q] / moduleRcs[i].thrusterPower;
                                numActiveThrusters += 1.0f;
                                anyRcsFiring = true;
                            }
                        }
                    }
                    netThrust += moduleRcs[i].thrusterPower;
                    rcsWeightedThrustLimit += moduleRcs[i].thrusterPower * moduleRcs[i].thrustPercentage;

                    List<PartResourceDefinition> propellants = moduleRcs[i].GetConsumedResources();
                    for (int res = propellants.Count - 1; res >= 0; --res)
                    {
                        MarkActiveRcsPropellant(propellants[res].id);
                    }
                }
            }

            if (numActiveThrusters > 0.0f)
            {
                rcsActiveThrustPercent /= numActiveThrusters;
            }

            if (netThrust > 0.0f)
            {
                rcsWeightedThrustLimit = rcsWeightedThrustLimit / (netThrust * 100.0f);
            }
        }
        #endregion

        #region Reaction Wheels
        private List<ModuleReactionWheel> reactionWheelList = new List<ModuleReactionWheel>(4);
        internal ModuleReactionWheel[] moduleReactionWheel = new ModuleReactionWheel[0];
        internal float reactionWheelNetTorque = 0.0f;
        internal float reactionWheelPitch = 0.0f;
        internal float reactionWheelRoll = 0.0f;
        internal float reactionWheelYaw = 0.0f;
        internal float reactionWheelAuthority = 0.0f;
        internal bool reactionWheelActive = false;
        internal bool reactionWheelDamaged = false;
        private void UpdateReactionWheels()
        {
            // wheelState == Disabled, unit is disabled.
            // wheelState == Active and inputSum == 0, unit is idle
            // wheelState == Active and inputSum > 0, unit is torquing.
            // inputVector provides current torque demand as ( pitch, roll, yaw )
            reactionWheelNetTorque = 0.0f;
            reactionWheelPitch = 0.0f;
            reactionWheelRoll = 0.0f;
            reactionWheelYaw = 0.0f;
            reactionWheelAuthority = 0.0f;
            reactionWheelActive = false;
            reactionWheelDamaged = false;

            float activeWheels = 0.0f;
            float activePitch = 0.0f;
            float activeRoll = 0.0f;
            float activeYaw = 0.0f;

            for (int i = moduleReactionWheel.Length - 1; i >= 0; --i)
            {
                if (moduleReactionWheel[i].wheelState == ModuleReactionWheel.WheelState.Active)
                {
                    reactionWheelAuthority += moduleReactionWheel[i].authorityLimiter;

                    if (moduleReactionWheel[i].inputVector.sqrMagnitude > 0.0f)
                    {
                        float partMaxTorque = 0.0f;
                        if (moduleReactionWheel[i].PitchTorque > 0.0f)
                        {
                            float torque = moduleReactionWheel[i].inputVector.x / moduleReactionWheel[i].PitchTorque;
                            if (Mathf.Abs(torque) > 0.0f)
                            {
                                reactionWheelPitch += torque;
                                activePitch += 1.0f;
                                partMaxTorque = Mathf.Max(partMaxTorque, Mathf.Abs(torque));
                            }
                        }
                        if (moduleReactionWheel[i].RollTorque > 0.0f)
                        {
                            float torque = moduleReactionWheel[i].inputVector.y / moduleReactionWheel[i].RollTorque;
                            if (Mathf.Abs(torque) > 0.0f)
                            {
                                reactionWheelRoll += torque;
                                activeRoll += 1.0f;
                                partMaxTorque = Mathf.Max(partMaxTorque, Mathf.Abs(torque));
                            }
                        }
                        if (moduleReactionWheel[i].YawTorque > 0.0f)
                        {
                            float torque = moduleReactionWheel[i].inputVector.z / moduleReactionWheel[i].YawTorque;
                            if (Mathf.Abs(torque) > 0.0f)
                            {
                                reactionWheelYaw += torque;
                                activeYaw += 1.0f;
                                partMaxTorque = Mathf.Max(partMaxTorque, Mathf.Abs(torque));
                            }
                        }

                        reactionWheelActive = true;
                        reactionWheelNetTorque += partMaxTorque;
                        activeWheels += 1.0f;
                    }
                }
                else if (moduleReactionWheel[i].wheelState == ModuleReactionWheel.WheelState.Broken)
                {
                    reactionWheelDamaged = true;
                }
            }

            if (reactionWheelAuthority > 0.0f)
            {
                reactionWheelAuthority = reactionWheelAuthority / (100.0f * moduleReactionWheel.Length);
            }

            if (activeWheels > 1.0f)
            {
                reactionWheelNetTorque /= activeWheels;
            }
            if (activePitch > 1.0f)
            {
                reactionWheelPitch /= activePitch;
            }
            if (activeRoll > 1.0f)
            {
                reactionWheelRoll /= activeRoll;
            }
            if (activeYaw > 1.0f)
            {
                reactionWheelYaw /= activeYaw;
            }
        }
        #endregion

        #region Resource Converters
        internal class GeneralPurposeResourceConverter
        {
            internal List<ModuleResourceConverter> converterList = new List<ModuleResourceConverter>();
            internal ModuleResourceConverter[] moduleConverter = new ModuleResourceConverter[0];
            internal List<float> outputRatioList = new List<float>();
            internal float[] outputRatio = new float[0];
            internal string outputResource = string.Empty;
            internal int id;
            internal float netOutput = 0.0f;
            internal bool converterActive = false;
        }
        internal List<GeneralPurposeResourceConverter> resourceConverterList = new List<GeneralPurposeResourceConverter>();
        private void UpdateResourceConverter()
        {
            for (int rsrc = resourceConverterList.Count - 1; rsrc >= 0; --rsrc)
            {
                GeneralPurposeResourceConverter rc = resourceConverterList[rsrc];
                rc.converterActive = false;
                rc.netOutput = 0.0f;

                for (int i = rc.moduleConverter.Length - 1; i >= 0; --i)
                {
                    rc.converterActive |= (rc.moduleConverter[i].IsActivated && !rc.moduleConverter[i].AlwaysActive);

                    if (rc.moduleConverter[i].IsActivated)
                    {
                        rc.netOutput += (float)rc.moduleConverter[i].lastTimeFactor * rc.outputRatio[i];
                    }
                }
            }
        }
        #endregion

        #region Science
        // For a bootstrap to interpreting science values, see https://github.com/KerboKatz/AutomatedScienceSampler/blob/master/source/AutomatedScienceSampler/DefaultActivator.cs
        private List<ModuleScienceExperiment> scienceExperimentList = new List<ModuleScienceExperiment>();
        internal ModuleScienceExperiment[] moduleScienceExperiment = new ModuleScienceExperiment[0];
        private List<ModuleScienceContainer> scienceContainerList = new List<ModuleScienceContainer>();
        internal ModuleScienceContainer[] scienceContainer = new ModuleScienceContainer[0];

        internal class ScienceType
        {
            internal ScienceExperiment type;
            internal List<ModuleScienceExperiment> experiments = new List<ModuleScienceExperiment>();
        };
        private List<ScienceType> scienceTypeList = new List<ScienceType>();
        internal ScienceType[] scienceType = new ScienceType[0];
        private void RebuildScienceTypes()
        {
            for (int i = moduleScienceExperiment.Length - 1; i >= 0; --i)
            {
                int idx = scienceTypeList.FindIndex(x => x.type.id == moduleScienceExperiment[i].experiment.id);
                if (idx == -1)
                {
                    ScienceType st = new ScienceType();
                    st.type = moduleScienceExperiment[i].experiment;
                    st.experiments.Add(moduleScienceExperiment[i]);
                    scienceTypeList.Add(st);
                }
                else
                {
                    scienceTypeList[idx].experiments.Add(moduleScienceExperiment[i]);
                }
            }

            TransferModules<ScienceType>(scienceTypeList, ref scienceType);
        }

        internal class ExperimentData
        {
            internal CelestialBody body;
            internal ExperimentSituations situation;
            internal ScienceSubject subject;
            internal string biomeDisplayName;
        };
        private Dictionary<string, ExperimentData> processedExperimentData = new Dictionary<string, ExperimentData>();
        internal ExperimentData GetExperimentData(string subjectID, ScienceExperiment experiment)
        {
            ExperimentData ed;
            if (!processedExperimentData.TryGetValue(subjectID, out ed))
            {
                ed = new ExperimentData();

                string bodyName;
                string biome;
                ScienceUtil.GetExperimentFieldsFromScienceID(subjectID, out bodyName, out ed.situation, out biome);
                ed.body = FlightGlobals.GetBodyByName(bodyName);
                ed.biomeDisplayName = ScienceUtil.GetBiomedisplayName(ed.body, biome);
                ed.subject = ResearchAndDevelopment.GetExperimentSubject(experiment, ed.situation, ed.body, biome, ed.biomeDisplayName);

                processedExperimentData.Add(subjectID, ed);
            }

            return ed;
        }

        #endregion

        #region Thermal Management
        private List<ModuleActiveRadiator> radiatorList = new List<ModuleActiveRadiator>();
        internal ModuleActiveRadiator[] moduleRadiator = new ModuleActiveRadiator[0];
        private List<ModuleAblator> ablatorList = new List<ModuleAblator>();
        internal ModuleAblator[] moduleAblator = new ModuleAblator[0];
        private List<ModuleDeployableRadiator> deployableRadiatorList = new List<ModuleDeployableRadiator>();
        internal ModuleDeployableRadiator[] moduleDeployableRadiator = new ModuleDeployableRadiator[0];
        internal double currentEnergyTransfer;
        internal double maxEnergyTransfer;
        internal bool radiatorActive;
        internal bool radiatorInactive;
        internal bool radiatorDeployable;
        internal bool radiatorRetractable;
        internal int radiatorMoving;
        internal bool radiatorDamaged;
        internal float hottestAblator;
        internal float hottestAblatorMax;
        internal float hottestAblatorSign = 0.0f;
        private void UpdateRadiators()
        {
            radiatorActive = false;
            radiatorInactive = false;
            radiatorDeployable = false;
            radiatorRetractable = false;
            radiatorMoving = 0;
            radiatorDamaged = false;
            maxEnergyTransfer = 0.0;
            currentEnergyTransfer = 0.0;

            if (moduleAblator.Length == 0)
            {
                hottestAblator = 0.0f;
                hottestAblatorMax = 0.0f;
                hottestAblatorSign = 0.0f;
            }
            else
            {
                float currentHottestAblatorDiff = float.MaxValue;
                float currentMaxAblator = 0.0f;
                float currentAblator = 0.0f;
                for (int i = moduleAblator.Length - 1; i >= 0; --i)
                {
                    Part ablatorPart = moduleAblator[i].part;
                    if ((ablatorPart.skinMaxTemp - ablatorPart.skinTemperature) < currentHottestAblatorDiff)
                    {
                        currentHottestAblatorDiff = (float)(ablatorPart.skinMaxTemp - ablatorPart.skinTemperature);
                        currentMaxAblator = (float)ablatorPart.skinMaxTemp;
                        currentAblator = (float)ablatorPart.skinTemperature;
                    }
                }

                if (currentHottestAblatorDiff < float.MaxValue)
                {
                    hottestAblatorSign = Math.Sign(currentAblator - hottestAblator);
                    hottestAblator = currentAblator;
                    hottestAblatorMax = currentMaxAblator;
                }
                else
                {
                    hottestAblator = 0.0f;
                    hottestAblatorMax = 0.0f;
                    hottestAblatorSign = 0.0f;
                }
            }

            string tempString;
            for (int i = moduleRadiator.Length - 1; i >= 0; --i)
            {
                if (moduleRadiator[i].IsCooling)
                {
                    radiatorActive = true;
                    maxEnergyTransfer += moduleRadiator[i].maxEnergyTransfer;
                    float xv;
                    // Hack: I can't coax this information out through another
                    // public field.
                    tempString = moduleRadiator[i].status.Substring(0, moduleRadiator[i].status.Length - 1);
                    if (float.TryParse(tempString, out xv))
                    {
                        currentEnergyTransfer += xv * 0.01 * moduleRadiator[i].maxEnergyTransfer;
                    }
                }
                else
                {
                    radiatorInactive = true;
                }
            }

            for (int i = moduleDeployableRadiator.Length - 1; i >= 0; --i)
            {
                if (moduleDeployableRadiator[i].useAnimation)
                {
                    radiatorRetractable |= (moduleDeployableRadiator[i].retractable && moduleDeployableRadiator[i].deployState == ModuleDeployablePart.DeployState.EXTENDED);
                    radiatorDeployable |= (moduleDeployableRadiator[i].deployState == ModuleDeployablePart.DeployState.RETRACTED);
                    if (moduleDeployableRadiator[i].deployState == ModuleDeployablePart.DeployState.RETRACTING)
                    {
                        radiatorMoving = -1;
                    }
                    else if (moduleDeployableRadiator[i].deployState == ModuleDeployablePart.DeployState.EXTENDING)
                    {
                        radiatorMoving = 1;
                    }
                    radiatorDamaged |= (moduleDeployableRadiator[i].deployState == ModuleDeployablePart.DeployState.BROKEN);
                }
            }
        }
        #endregion

        #region Wheels
        private List<ModuleWheels.ModuleWheelDamage> wheelDamageList = new List<ModuleWheels.ModuleWheelDamage>();
        internal ModuleWheels.ModuleWheelDamage[] moduleWheelDamage = new ModuleWheels.ModuleWheelDamage[0];
        private List<ModuleWheels.ModuleWheelDeployment> wheelDeploymentList = new List<ModuleWheels.ModuleWheelDeployment>();
        internal ModuleWheels.ModuleWheelDeployment[] moduleWheelDeployment = new ModuleWheels.ModuleWheelDeployment[0];
        private List<ModuleWheelBase> wheelBaseList = new List<ModuleWheelBase>();
        internal ModuleWheelBase[] moduleWheelBase = new ModuleWheelBase[0];
        internal float wheelPosition = 0.0f;
        internal float wheelDirection = 0.0f;
        private void UpdateGear()
        {
            if (moduleWheelDeployment.Length > 0)
            {
                float newPosition = 0.0f;
                float numWheels = 0.0f;
                for (int i = moduleWheelDeployment.Length - 1; i >= 0; --i)
                {
                    if (!moduleWheelDamage[i].isDamaged)
                    {
                        numWheels += 1.0f;

                        newPosition += Mathf.InverseLerp(moduleWheelDeployment[i].retractedPosition, moduleWheelDeployment[i].deployedPosition, moduleWheelDeployment[i].position);
                    }
                }

                if (numWheels > 1.0f)
                {
                    newPosition /= numWheels;
                }

                wheelDirection = newPosition - wheelPosition;
                if (wheelDirection < 0.0f)
                {
                    wheelDirection = -1.0f;
                }
                else if (wheelDirection > 0.0f)
                {
                    wheelDirection = 1.0f;
                }
                wheelPosition = newPosition;
            }
        }
        #endregion

        #region Modules Management
        /// <summary>
        /// Mark modules as potentially invalid to force reiterating over the
        /// part and module lists.
        /// </summary>
        private void InvalidateModules()
        {
            modulesInvalidated = true;
            camerasReset = true;
        }

        /// <summary>
        /// Helper method to transfer a list to an array without creating a new
        /// array (if the existing array is the same size as the list needs).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sourceList"></param>
        /// <param name="destArray"></param>
        static void TransferModules<T>(List<T> sourceList, ref T[] destArray)
        {
            if (sourceList.Count != destArray.Length)
            {
                destArray = new T[sourceList.Count];

                // Assume the list can't change without changing size -
                // I shouldn't be able to add a module and remove a module
                // of the same type in a single transaction, right?
                // For whatever reason, this copy is faster than List.CopyTo()
                for (int i = sourceList.Count - 1; i >= 0; --i)
                {
                    destArray[i] = sourceList[i];
                }
            }
            sourceList.Clear();
        }

#if TIME_REBUILD
        private Stopwatch rebuildStopwatch = new Stopwatch();
#endif

        /// <summary>
        /// Iterate over all of everything to update tracked modules.
        /// </summary>
        private void RebuildModules()
        {
            // Merkur:
            // LOAD TIME:
            // [LOG 09:41:38.560] [MASVesselComputer] Scan parts in 0.172 ms, transfer modules in 0.776 ms
            // STAGING:
            // [LOG 09:42:11.132] [MASVesselComputer] Scan parts in 0.044 ms, transfer modules in 0.044 ms

#if TIME_REBUILD
            rebuildStopwatch.Reset();
            rebuildStopwatch.Start();
#endif

            activeResources.Clear();
            for (int agIndex = hasActionGroup.Length - 1; agIndex >= 0; --agIndex)
            {
                hasActionGroup[agIndex] = false;
            }

            activeEnginesGimbal = false;
            moduleGraviticEngine = null;

            // Update the lists of modules
            for (int partIdx = vessel.parts.Count - 1; partIdx >= 0; --partIdx)
            {
                PartResourceList resources = vessel.parts[partIdx].Resources;
                UpdateResourceList(resources);

                PartModuleList Modules = vessel.parts[partIdx].Modules;
                for (int moduleIdx = Modules.Count - 1; moduleIdx >= 0; --moduleIdx)
                {
                    PartModule module = Modules[moduleIdx];
                    if (module.isEnabled)
                    {
                        if (module is ModuleEngines)
                        {
                            ModuleEngines engine = module as ModuleEngines;
                            enginesList.Add(engine);

                            // This is something we only need to worry about when rebulding the list.
                            if (activeEnginesGimbal == false && engine.EngineIgnited && engine.isOperational && Modules.GetModule<ModuleGimbal>() != null)
                            {
                                activeEnginesGimbal = true;
                            }
                            // Crazy Mode controllers are a special-case engine.
                            if (MASIVTOL.wbiWBIGraviticEngine_t != null && MASIVTOL.wbiWBIGraviticEngine_t.IsAssignableFrom(module.GetType()))
                            {
                                if (MASIVTOL.wbiCrazyModeIsActive(module))
                                {
                                    moduleGraviticEngine = module;
                                }
                            }
                        }
                        else if (module is ModuleAblator)
                        {
                            ablatorList.Add(module as ModuleAblator);
                        }
                        else if (module is ModuleGimbal)
                        {
                            gimbalsList.Add(module as ModuleGimbal);
                        }
                        else if (module is ModuleParachute)
                        {
                            parachuteList.Add(module as ModuleParachute);
                        }
                        else if (module is ModuleAlternator)
                        {
                            ModuleAlternator alternator = module as ModuleAlternator;
                            for (int i = alternator.resHandler.outputResources.Count - 1; i >= 0; --i)
                            {
                                if (alternator.resHandler.outputResources[i].name == MASConfig.ElectricCharge)
                                {
                                    alternatorList.Add(alternator);
                                    break;
                                }
                            }
                        }
                        else if (module is ModuleDeployableSolarPanel)
                        {
                            solarPanelList.Add(module as ModuleDeployableSolarPanel);
                        }
                        else if (module is ModuleGenerator)
                        {
                            ModuleGenerator generator = module as ModuleGenerator;
                            for (int i = generator.resHandler.outputResources.Count - 1; i >= 0; --i)
                            {
                                if (generator.resHandler.outputResources[i].name == MASConfig.ElectricCharge)
                                {
                                    generatorList.Add(generator);
                                    generatorOutputList.Add((float)generator.resHandler.outputResources[i].rate);
                                    break;
                                }
                            }
                        }
                        else if (module is ModuleResourceConverter)
                        {
                            ModuleResourceConverter gen = module as ModuleResourceConverter;
                            List<ResourceRatio> outputs = gen.Recipe.Outputs;
                            int outputCount = outputs.Count;
                            for (int rsrc = resourceConverterList.Count - 1; rsrc >= 0; --rsrc)
                            {
                                int rrIdx = outputs.FindIndex(x => x.ResourceName == resourceConverterList[rsrc].outputResource);
                                if (rrIdx > -1)
                                {
                                    resourceConverterList[rsrc].converterList.Add(gen);
                                    resourceConverterList[rsrc].outputRatioList.Add((float)outputs[rrIdx].Ratio);
                                }
                            }
                        }
                        else if (module is ModuleDeployableAntenna)
                        {
                            antennaList.Add(module as ModuleDeployableAntenna);
                        }
                        else if (module is MASRadar)
                        {
                            radarList.Add(module as MASRadar);
                        }
                        else if (module is ModuleDeployableRadiator)
                        {
                            deployableRadiatorList.Add(module as ModuleDeployableRadiator);
                        }
                        else if (module is ModuleActiveRadiator)
                        {
                            radiatorList.Add(module as ModuleActiveRadiator);
                        }
                        else if (module is ModuleRCS)
                        {
                            rcsList.Add(module as ModuleRCS);
                        }
                        else if (module is ModuleReactionWheel)
                        {
                            reactionWheelList.Add(module as ModuleReactionWheel);
                        }
                        else if (MASIParachute.realChuteFound && module.GetType() == MASIParachute.rcAPI_t)
                        {
                            realchuteList.Add(module);
                        }
                        else if (module is MASCamera)
                        {
                            // Do not add broken cameras to the control list.
                            if ((module as MASCamera).IsDamaged() == false)
                            {
                                cameraList.Add(module as MASCamera);
                            }
                        }
                        else if (module is ModuleProceduralFairing)
                        {
                            proceduralFairingList.Add(module as ModuleProceduralFairing);
                        }
                        else if (module is MASThrustReverser)
                        {
                            thrustReverserList.Add(module as MASThrustReverser);
                        }
                        else if (module is MASIdEngine)
                        {
                            MASIdEngine idE = module as MASIdEngine;
                            if (idE.partId > 0 && idEnginesList.FindIndex(x => x.partId == idE.partId) == -1)
                            {
                                idEnginesList.Add(idE);
                            }
                        }
                        else if (module is ModuleWheelBase)
                        {
                            wheelBaseList.Add(module as ModuleWheelBase);
                        }
                        else if (module is ModuleWheels.ModuleWheelDamage)
                        {
                            wheelDamageList.Add(module as ModuleWheels.ModuleWheelDamage);
                        }
                        else if (module is ModuleWheels.ModuleWheelDeployment)
                        {
                            wheelDeploymentList.Add(module as ModuleWheels.ModuleWheelDeployment);
                        }
                        else if (module is ModuleCargoBay)
                        {
                            ModuleCargoBay cb = module as ModuleCargoBay;
                            if (Modules[cb.DeployModuleIndex] is ModuleAnimateGeneric)
                            {
                                // ModuleCargoBay has been used with one of 3 deploy modules:
                                // 1) ModuleProceduralFairing - we don't count these, since they're already covered by fairing controls.
                                // 2) ModuleServiceModule - KSP 1.4.x, used so far only in the expansion.  Doesn't appear to have a way
                                //    to control the service module deployment outside of staging.
                                // 3) ModuleAnimateGeneric - we can control these.
                                cargoBayList.Add(cb);
                            }
                        }
                        else if (module is MultiModeEngine)
                        {
                            multiModeEngineList.Add(module as MultiModeEngine);
                        }
                        else if (module is MASIdEngineGroup)
                        {
                            var group = module as MASIdEngineGroup;
                            if (group.partId != 0 && group.engine != null)
                            {
                                engineGroupList.Add(group);
                            }
                        }
                        else if (module is ModuleWheels.ModuleWheelBrakes)
                        {
                            brakesList.Add(module as ModuleWheels.ModuleWheelBrakes);
                        }
                        else if (module is ModuleResourceIntake)
                        {
                            resourceIntakeList.Add(module as ModuleResourceIntake);
                        }
                        else if (module is LaunchClamp)
                        {
                            launchClampList.Add(module as LaunchClamp);
                        }
                        else if (module is ModuleScienceExperiment)
                        {
                            scienceExperimentList.Add(module as ModuleScienceExperiment);
                        }
                        else if (module is ModuleScienceContainer)
                        {
                            scienceContainerList.Add(module as ModuleScienceContainer);
                        }
                        else if (module is ModuleAeroSurface)
                        {
                            airBrakeList.Add(module as ModuleAeroSurface);
                        }

                        foreach (BaseAction ba in module.Actions)
                        {
                            if (ba.actionGroup != KSPActionGroup.None)
                            {
                                SetActionGroup(ba.actionGroup);
                            }
                        }
                    }
                }

                // While we're here, update active resources
                if (vessel.parts[partIdx].inverseStage >= vessel.currentStage)
                {
                    activeResources.UnionWith(vessel.parts[partIdx].crossfeedPartSet.GetParts());
                }
            }
#if TIME_REBUILD
            TimeSpan scanTime = rebuildStopwatch.Elapsed;
#endif

            // Rebuild the part set.
            if (partSet == null)
            {
                partSet = new PartSet(activeResources);
            }
            else
            {
                partSet.RebuildParts(activeResources);
            }

            idEnginesList.Sort((a, b) => { return a.partId - b.partId; });

            // Transfer the modules to an array, since the array is cheaper to
            // iterate over, and we're going to be iterating over it a lot.
            TransferModules<ModuleEngines>(enginesList, ref moduleEngines);
            if (invMaxISP.Length != moduleEngines.Length)
            {
                invMaxISP = new float[moduleEngines.Length];
            }
            for (int i = moduleEngines.Length - 1; i >= 0; --i)
            {
                // MOARdV TODO: This ignores the velocity ISP curve of jets.
                float maxIsp, minIsp;
                moduleEngines[i].atmosphereCurve.FindMinMaxValue(out minIsp, out maxIsp);
                invMaxISP[i] = 1.0f / maxIsp;
            }

            TransferModules<ModuleAeroSurface>(airBrakeList, ref moduleAirBrake);
            TransferModules<ModuleAlternator>(alternatorList, ref moduleAlternator);
            TransferModules<ModuleAblator>(ablatorList, ref moduleAblator);
            TransferModules<ModuleCargoBay>(cargoBayList, ref moduleCargoBay);
            TransferModules<ModuleDeployableAntenna>(antennaList, ref moduleAntenna);
            TransferModules<ModuleDeployableRadiator>(deployableRadiatorList, ref moduleDeployableRadiator);
            TransferModules<MASIdEngineGroup>(engineGroupList, ref engineGroup);
            TransferModules<MASIdEngine>(idEnginesList, ref moduleIdEngines);
            TransferModules<ModuleGenerator>(generatorList, ref moduleGenerator);
            TransferModules<float>(generatorOutputList, ref generatorOutput);
            TransferModules<ModuleGimbal>(gimbalsList, ref moduleGimbals);
            TransferModules<MultiModeEngine>(multiModeEngineList, ref multiModeEngines);
            TransferModules<ModuleParachute>(parachuteList, ref moduleParachute);
            TransferModules<MASRadar>(radarList, ref moduleRadar);
            TransferModules<ModuleActiveRadiator>(radiatorList, ref moduleRadiator);
            TransferModules<ModuleRCS>(rcsList, ref moduleRcs);
            TransferModules<PartModule>(realchuteList, ref moduleRealChute);
            TransferModules<ModuleReactionWheel>(reactionWheelList, ref moduleReactionWheel);
            TransferModules<ModuleResourceIntake>(resourceIntakeList, ref moduleResourceIntake);
            TransferModules<ModuleDeployableSolarPanel>(solarPanelList, ref moduleSolarPanel);
            TransferModules<MASCamera>(cameraList, ref moduleCamera);
            TransferModules<ModuleProceduralFairing>(proceduralFairingList, ref moduleProceduralFairing);
            TransferModules<MASThrustReverser>(thrustReverserList, ref moduleThrustReverser);
            TransferModules<ModuleWheels.ModuleWheelDamage>(wheelDamageList, ref moduleWheelDamage);
            TransferModules<ModuleWheels.ModuleWheelDeployment>(wheelDeploymentList, ref moduleWheelDeployment);
            TransferModules<ModuleWheelBase>(wheelBaseList, ref moduleWheelBase);
            TransferModules<ModuleWheels.ModuleWheelBrakes>(brakesList, ref moduleBrakes);
            TransferModules<ModuleScienceExperiment>(scienceExperimentList, ref moduleScienceExperiment);
            TransferModules<ModuleScienceContainer>(scienceContainerList, ref scienceContainer);
            TransferModules<LaunchClamp>(launchClampList, ref moduleLaunchClamp);

            for (int i = resourceConverterList.Count - 1; i >= 0; --i)
            {
                TransferModules<ModuleResourceConverter>(resourceConverterList[i].converterList, ref resourceConverterList[i].moduleConverter);
                TransferModules<float>(resourceConverterList[i].outputRatioList, ref resourceConverterList[i].outputRatio);
            }
            RebuildScienceTypes();

#if TIME_REBUILD
            TimeSpan transferTime = rebuildStopwatch.Elapsed - scanTime;

            Utility.LogMessage(this, "Scan parts in {0:0.000} ms, transfer modules in {1:0.000} ms",
                ((double)scanTime.Ticks) / ((double)TimeSpan.TicksPerMillisecond),
                ((double)transferTime.Ticks) / ((double)TimeSpan.TicksPerMillisecond));
#endif
            UpdateDockingNode(vessel.GetReferenceTransformPart());
        }

#if TIME_UPDATES
        private Stopwatch updateStopwatch = new Stopwatch();
#endif
        /// <summary>
        /// Update per-module data after refreshing the module lists, if needed.
        /// </summary>
        private void UpdateModuleData()
        {
            if (modulesInvalidated)
            {
                InitRebuildPartResources();

                RebuildModules();

                modulesInvalidated = false;
            }

#if TIME_UPDATES
            updateStopwatch.Reset();
            updateStopwatch.Start();
#endif
            bool requestReset = false;
            UpdateAntenna();
            UpdateCamera();
            UpdateCargoBay();
            UpdateDockingNodeState();
            requestReset |= UpdateEngines();
            UpdateGimbals();
            UpdatePower();
            UpdateProceduralFairing();
            UpdateRadiators();
            UpdateGear();
            UpdateRcs();
            UpdateReactionWheels();
            UpdateResourceConverter();

            if (requestReset)
            {
                InvalidateModules();
            }
#if TIME_UPDATES
            TimeSpan updateTime = updateStopwatch.Elapsed;

            Utility.LogMessage(this, "UpdateModuleData in {0:0.000} ms",
                ((double)updateTime.Ticks) / ((double)TimeSpan.TicksPerMillisecond));
#endif
        }
        #endregion
    }
}
