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
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Text;

namespace AvionicsSystems
{
    internal partial class MASVesselComputer : VesselModule
    {
        // Tracks per-module data.

        private bool modulesInvalidated = true;

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
        private List<ModuleEngines> enginesList = new List<ModuleEngines>(8);
        internal ModuleEngines[] moduleEngines = new ModuleEngines[0];
        private float[] invMaxISP = new float[0];
        internal float currentThrust; // current net thrust, kN
        internal float currentLimitedThrust; // Max thrust, accounting for throttle limits, kN
        internal float currentMaxThrust; // Max possible thrust at current altitude, kN
        internal float maxRatedThrust; // Max possible thrust, kN
        internal float maxEngineFuelFlow; // max fuel flow, g/s
        internal float currentEngineFuelFlow; // current fuel flow, g/s
        internal float currentIsp;
        internal float maxIsp;
        internal float hottestEngineTemperature;
        internal float hottestEngineMaxTemperature;
        internal int currentEngineCount;
        internal int activeEngineCount;
        internal bool anyEnginesFlameout;
        //internal bool anyEnginesOverheating;
        internal bool anyEnginesEnabled;
        private bool UpdateEngines()
        {
            this.currentThrust = 0.0f;
            this.maxRatedThrust = 0.0f;
            currentLimitedThrust = 0.0f;
            currentMaxThrust = 0.0f;
            hottestEngineTemperature = 0.0f;
            hottestEngineMaxTemperature = 0.0f;
            maxEngineFuelFlow = 0.0f;
            currentEngineFuelFlow = 0.0f;
            anyEnginesFlameout = false;
            anyEnginesEnabled = false;

            float hottestEngine = float.MaxValue;
            float maxIspContribution = 0.0f;
            float averageIspContribution = 0.0f;

            List<Part> visitedParts = new List<Part>(vessel.parts.Count);

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

                //anyEnginesOverheating |= (thatPart.skinTemperature / thatPart.skinMaxTemp > 0.9) || (thatPart.temperature / thatPart.maxTemp > 0.9);
                anyEnginesEnabled |= me.allowShutdown && me.getIgnitionState;
                anyEnginesFlameout |= (me.isActiveAndEnabled && me.flameout);

                if (me.EngineIgnited && me.isEnabled && me.isOperational)
                {
                    float currentThrust = me.finalThrust;
                    this.currentThrust += currentThrust;
                    this.maxRatedThrust += me.GetMaxThrust();
                    float rawMaxThrust = me.GetMaxThrust() * me.realIsp * invMaxISP[i];
                    currentMaxThrust += rawMaxThrust;
                    float maxThrust = rawMaxThrust * me.thrustPercentage * 0.01f;
                    currentLimitedThrust += maxThrust;
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
                }

                if (thatPart.skinMaxTemp - thatPart.skinTemperature < hottestEngine)
                {
                    hottestEngineTemperature = (float)thatPart.skinTemperature;
                    hottestEngineMaxTemperature = (float)thatPart.skinMaxTemp;
                    hottestEngine = hottestEngineMaxTemperature - hottestEngineTemperature;
                }
                if (thatPart.maxTemp - thatPart.temperature < hottestEngine)
                {
                    hottestEngineTemperature = (float)thatPart.temperature;
                    hottestEngineMaxTemperature = (float)thatPart.maxTemp;
                    hottestEngine = hottestEngineMaxTemperature - hottestEngineTemperature;
                }
            }

            if (averageIspContribution > 0.0f)
            {
                currentIsp = currentLimitedThrust / averageIspContribution;
            }
            else
            {
                currentIsp = 0.0f;
            }

            if (maxIspContribution > 0.0f)
            {
                maxIsp = currentLimitedThrust / maxIspContribution;
            }
            else
            {
                maxIsp = 0.0f;
            }

            return requestReset;
        }
        internal void ToggleEnginesEnabled()
        {
            bool newState = !anyEnginesEnabled;
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
        }
        #endregion

        #region Gimbal
        private List<ModuleGimbal> gimbalsList = new List<ModuleGimbal>(8);
        internal ModuleGimbal[] moduleGimbals = new ModuleGimbal[0];
        internal bool anyGimbalsLocked = false;
        void UpdateGimbals()
        {
            anyGimbalsLocked = false;
            for (int i = moduleGimbals.Length - 1; i >= 0; --i)
            {
                if (moduleGimbals[i].gimbalLock)
                {
                    anyGimbalsLocked |= moduleGimbals[i].gimbalLock;
                    break;
                }
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
        internal List<float> alternatorOutputList = new List<float>();
        internal float[] alternatorOutput = new float[0];
        private List<ModuleResourceConverter> fuelCellList = new List<ModuleResourceConverter>();
        internal ModuleResourceConverter[] moduleFuelCell = new ModuleResourceConverter[0];
        internal List<float> fuelCellOutputList = new List<float>();
        internal float[] fuelCellOutput = new float[0];
        private List<ModuleGenerator> generatorList = new List<ModuleGenerator>();
        internal ModuleGenerator[] moduleGenerator = new ModuleGenerator[0];
        internal List<float> generatorOutputList = new List<float>();
        internal float[] generatorOutput = new float[0];
        private List<ModuleDeployableSolarPanel> solarPanelList = new List<ModuleDeployableSolarPanel>();
        internal ModuleDeployableSolarPanel[] moduleSolarPanel = new ModuleDeployableSolarPanel[0];
        internal bool fuelCellActive;
        internal bool generatorActive;
        internal bool solarPanelsDeployable;
        internal bool solarPanelsRetractable;
        internal bool solarPanelsMoving;
        internal int solarPanelPosition;
        internal float netAlternatorOutput;
        internal float netFuelCellOutput;
        internal float netGeneratorOutput;
        internal float netSolarOutput;
        private void UpdatePower()
        {
            netAlternatorOutput = 0.0f;
            netFuelCellOutput = 0.0f;
            netGeneratorOutput = 0.0f;
            netSolarOutput = 0.0f;

            fuelCellActive = false;
            generatorActive = false;
            solarPanelsDeployable = false;
            solarPanelsRetractable = false;
            solarPanelsMoving = false;

            solarPanelPosition = -1;

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

            for (int i = moduleFuelCell.Length - 1; i >= 0; --i)
            {
                fuelCellActive |= (moduleFuelCell[i].IsActivated && !moduleFuelCell[i].AlwaysActive);

                if (moduleFuelCell[i].IsActivated)
                {
                    netFuelCellOutput += (float)moduleFuelCell[i].lastTimeFactor * fuelCellOutput[i];
                }
            }

            for (int i = moduleAlternator.Length - 1; i >= 0; --i)
            {
                // I assume there's only one ElectricCharge output in a given ModuleAlternator
                netAlternatorOutput += alternatorOutput[i] * moduleAlternator[i].outputRate;
            }

            for (int i = moduleSolarPanel.Length - 1; i >= 0; --i)
            {
                netSolarOutput += moduleSolarPanel[i].flowRate;
                solarPanelsRetractable |= (moduleSolarPanel[i].useAnimation && moduleSolarPanel[i].retractable && moduleSolarPanel[i].panelState == ModuleDeployableSolarPanel.panelStates.EXTENDED);
                solarPanelsDeployable |= (moduleSolarPanel[i].useAnimation && moduleSolarPanel[i].panelState == ModuleDeployableSolarPanel.panelStates.RETRACTED);
                solarPanelsMoving |= (moduleSolarPanel[i].useAnimation && (moduleSolarPanel[i].panelState == ModuleDeployableSolarPanel.panelStates.RETRACTING || moduleSolarPanel[i].panelState == ModuleDeployableSolarPanel.panelStates.EXTENDING));
                /* ModuleDeployableSolarPanel.panelStates = 
                        RETRACTED = 0,
                        EXTENDED = 1,
                        RETRACTING = 2,
                        EXTENDING = 3,
                        BROKEN = 4,
                 * REMAP TO:
                 * BROKEN = 0
                 * RETRACTED = 1
                 * RETRACTING = 2
                 * EXTENDING = 3
                 * EXTENDED = 4
                 */
                if (solarPanelPosition < 1)
                {
                    if (moduleSolarPanel[i].useAnimation)
                    {
                        switch (moduleSolarPanel[i].panelState)
                        {
                            case ModuleDeployableSolarPanel.panelStates.BROKEN:
                                solarPanelPosition = 0;
                                break;
                            case ModuleDeployableSolarPanel.panelStates.RETRACTED:
                                solarPanelPosition = 1;
                                break;
                            case ModuleDeployableSolarPanel.panelStates.RETRACTING:
                                solarPanelPosition = 2;
                                break;
                            case ModuleDeployableSolarPanel.panelStates.EXTENDING:
                                solarPanelPosition = 3;
                                break;
                            case ModuleDeployableSolarPanel.panelStates.EXTENDED:
                                solarPanelPosition = 4;
                                break;
                        }
                    }
                }
            }
            // If there are no panels, or no deployable panels, set it to RETRACTED
            if (solarPanelPosition < 0)
            {
                solarPanelPosition = 1;
            }
        }
        #endregion

        #region Radar
        private List<MASRadar> radarList = new List<MASRadar>();
        internal MASRadar[] moduleRadar = new MASRadar[0];
        internal bool radarActive;
        private void UpdateRadars()
        {
            radarActive = false;
            for (int i = moduleRadar.Length - 1; i >= 0; --i)
            {
                radarActive |= moduleRadar[i].radarEnabled;
            }
        }
        #endregion

        #region RCS
        private List<ModuleRCS> rcsList = new List<ModuleRCS>();
        internal ModuleRCS[] moduleRcs = new ModuleRCS[0];
        internal bool anyRcsDisabled = false;
        internal float rcsWeightedThrustLimit;
        private void UpdateRcs()
        {
            anyRcsDisabled = false;
            float netThrust = 0.0f;
            rcsWeightedThrustLimit = 0.0f;
            for (int i = moduleRcs.Length - 1; i >= 0; --i)
            {
                if (moduleRcs[i].rcsEnabled == false)
                {
                    anyRcsDisabled = true;
                }
                else
                {
                    netThrust += moduleRcs[i].thrusterPower;
                    rcsWeightedThrustLimit += moduleRcs[i].thrusterPower * moduleRcs[i].thrustPercentage;
                }
            }

            if (netThrust > 0.0f)
            {
                rcsWeightedThrustLimit = rcsWeightedThrustLimit / (netThrust * 100.0f);
            }
        }
        #endregion

        #region Reaction Wheels
        private List<ModuleReactionWheel> reactionWheelList = new List<ModuleReactionWheel>();
        internal ModuleReactionWheel[] moduleReactionWheel = new ModuleReactionWheel[0];
        private void UpdateReactionWheels()
        {
            /*
            for(int i=moduleReactionWheel.Length-1; i>=0; --i)
            {
                // wheelState == Disabled, unit is disabled.
                // wheelState == Active and inputSum == 0, unit is idle
                // wheelState == Active and inputSum > 0, unit is torquing.
                // inputVector provides current torque demand as ( pitch, roll, yaw )
                //Utility.LogMessage(this, "Reac[{0}]: inputSum = {1:0.00}, inputVector = {2:0.00}, {3:0.00}. {4:0.00}",
                //    i,
                //    moduleReactionWheel[i].inputSum,
                //    moduleReactionWheel[i].inputVector.x,
                //    moduleReactionWheel[i].inputVector.y,
                //    moduleReactionWheel[i].inputVector.z);
            }
             */
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
            }

            for (int i = sourceList.Count - 1; i >= 0; --i)
            {
                destArray[i] = sourceList[i];
            }
            sourceList.Clear();
        }

        /// <summary>
        /// Iterate over all of everything to update tracked modules.
        /// </summary>
        private void RebuildModules()
        {
            for (int agIndex = hasActionGroup.Length - 1; agIndex >= 0; --agIndex)
            {
                hasActionGroup[agIndex] = false;
            }

            // Update the lists of modules
            for (int partIdx = vessel.parts.Count - 1; partIdx >= 0; --partIdx)
            {
                PartModuleList Modules = vessel.parts[partIdx].Modules;
                for (int moduleIdx = Modules.Count - 1; moduleIdx >= 0; --moduleIdx)
                {
                    PartModule module = Modules[moduleIdx];
                    if (module.isEnabled)
                    {
                        if (module is ModuleEngines)
                        {
                            enginesList.Add(module as ModuleEngines);
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
                            for (int i = 0; i < alternator.outputResources.Count; ++i)
                            {
                                if (alternator.outputResources[i].name == MASLoader.ElectricCharge)
                                {
                                    alternatorList.Add(alternator);
                                    alternatorOutputList.Add((float)alternator.outputResources[i].rate);
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
                            for (int i = 0; i < generator.outputList.Count; ++i)
                            {
                                if (generator.outputList[i].name == MASLoader.ElectricCharge)
                                {
                                    generatorList.Add(generator);
                                    generatorOutputList.Add((float)generator.outputList[i].rate);
                                    break;
                                }
                            }
                        }
                        else if (module is ModuleResourceConverter)
                        {
                            ModuleResourceConverter gen = module as ModuleResourceConverter;
                            ConversionRecipe recipe = gen.Recipe;
                            for (int i = 0; i < recipe.Outputs.Count; ++i)
                            {
                                if (recipe.Outputs[i].ResourceName == "ElectricCharge")
                                {
                                    fuelCellList.Add(gen);
                                    fuelCellOutputList.Add((float)recipe.Outputs[i].Ratio);
                                    break;
                                }
                            }
                        }
                        else if (module is MASRadar)
                        {
                            radarList.Add(module as MASRadar);
                        }
                        else if (module is ModuleRCS)
                        {
                            rcsList.Add(module as ModuleRCS);
                        }
                        else if (module is ModuleReactionWheel)
                        {
                            reactionWheelList.Add(module as ModuleReactionWheel);
                        }
                        else if (MASIRealChute.realChuteFound && module.GetType() == MASIRealChute.rcAPI_t)
                        {
                            realchuteList.Add(module);
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

                // While we're here, update resources
                List<PartResource> list = vessel.parts[partIdx].Resources.list;

                if (list != null)
                {
                    for (int resourceIdx = list.Count - 1; resourceIdx >= 0; --resourceIdx)
                    {
                        AddResource(list[resourceIdx]);
                    }
                }
            }

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

            TransferModules<PartModule>(realchuteList, ref moduleRealChute);
            TransferModules<ModuleParachute>(parachuteList, ref moduleParachute);
            TransferModules<ModuleGimbal>(gimbalsList, ref moduleGimbals);
            TransferModules<ModuleAlternator>(alternatorList, ref moduleAlternator);
            TransferModules<float>(alternatorOutputList, ref alternatorOutput);
            TransferModules<ModuleGenerator>(generatorList, ref moduleGenerator);
            TransferModules<float>(generatorOutputList, ref generatorOutput);
            TransferModules<ModuleDeployableSolarPanel>(solarPanelList, ref moduleSolarPanel);
            TransferModules<ModuleResourceConverter>(fuelCellList, ref moduleFuelCell);
            TransferModules<float>(fuelCellOutputList, ref fuelCellOutput);
            TransferModules<MASRadar>(radarList, ref moduleRadar);
            TransferModules<ModuleRCS>(rcsList, ref moduleRcs);
            TransferModules<ModuleReactionWheel>(reactionWheelList, ref moduleReactionWheel);
        }

        /// <summary>
        /// Update per-part data that may change per fixed update.
        /// </summary>
        private void UpdatePartData()
        {
            for (int partIdx = vessel.parts.Count - 1; partIdx >= 0; --partIdx)
            {
                List<PartResource> list = vessel.parts[partIdx].Resources.list;

                if (list != null)
                {
                    for (int resourceIdx = list.Count - 1; resourceIdx >= 0; --resourceIdx)
                    {
                        AddResource(list[resourceIdx]);
                    }
                }
            }
        }

        /// <summary>
        /// Update per-module data after refreshing the module lists, if needed.
        /// </summary>
        private void UpdateModuleData()
        {
            if (modulesInvalidated)
            {
                RebuildModules();

                modulesInvalidated = false;
            }
            else
            {
                // We *still* have to iterate over the parts - but just for resource counting.
                UpdatePartData();
            }

            bool requestReset = false;
            UpdateDockingNodeState();
            requestReset |= UpdateEngines();
            UpdateGimbals();
            UpdatePower();
            UpdateRadars();
            UpdateRcs();
            UpdateReactionWheels();

            if (requestReset)
            {
                InvalidateModules();
            }
        }
        #endregion
    }
}
