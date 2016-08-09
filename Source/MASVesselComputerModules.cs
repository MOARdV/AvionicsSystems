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

        #region Engines
        //---Engines
        private List<ModuleEngines> enginesList = new List<ModuleEngines>(8);
        private ModuleEngines[] moduleEngines = new ModuleEngines[0];
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
            requestReset |= UpdateEngines();
            UpdateGimbals();

            if (requestReset)
            {
                InvalidateModules();
            }
        }
        #endregion
    }
}
