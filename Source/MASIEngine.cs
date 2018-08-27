/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2017-2018 MOARdV
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
using System.Collections.Generic;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// MASIEngine is the interface with aircraft engines and features, including
    /// aircraft engine mods.
    /// </summary>
    /// <LuaName>engine</LuaName>
    /// <mdDoc>
    /// The MASIEngine class provides an interface between Avionics Systems
    /// and aircraft engine related features.
    /// 
    /// Interaction with Advanced Jet Engine jet and propeller engines is done through this
    /// modules.
    /// </mdDoc>
    internal class MASIEngine
    {
        private List<int> uniqueIds = new List<int>();

        internal MASVesselComputer vc;

        /// <summary>
        /// The AJE Jet category provides an interface to engines using ModuleEnginesAJEJet from the
        /// Advanced Jet Engines mod.
        /// 
        /// In order to control the engines, those engines must have a properly-configured
        /// MASIdEngine module installed.  In particular, each engine must have a MAS Part ID
        /// other than zero, and each engine must have a unique MAS Part ID.
        /// 
        /// **Note:** There is a limited amount of information that the AJE mod exposes for jets in an accessible
        /// manner.
        /// </summary>
        #region AJE Jet
        /// <summary>
        /// Returns the afterburner throttle position for the selected jet engine.
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetEngineCount()</param>
        /// <returns>Afterburner throttle, between 0 and 1; if the selected engine is invalid, not a jet, or does not have an afterburner, returns 0.</returns>
        public double GetAfterburnerThrottle(double engineId)
        {
            int index = (int)(engineId) - 1;
            if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                return vc.moduleIdEngines[index].GetAfterburnerThrottle();
            }
            return 0.0;
        }

        /// <summary>
        /// Returns the current temperature of the selected jet.  If the selected engine is
        /// not a jet, returns 0.
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetEngineCount()</param>
        /// <param name="useKelvin">If true, returns units are in Kelvin.  If false, units are in Celsius.</param>
        /// <returns>Current temperature in Kelvin or Celsius, or 0.</returns>
        public double GetCurrentJetTemperature(double engineId, bool useKelvin)
        {
            int index = (int)(engineId) - 1;
            if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                double temperature = vc.moduleIdEngines[index].GetCurrentJetTemperature();
                if (!useKelvin && temperature > 0.0)
                {
                    temperature += MASFlightComputerProxy.KelvinToCelsius;
                }
                return temperature;
            }
            return 0.0;
        }

        /// <summary>
        /// Returns the maximum temperature of the selected jet.  If the selected engine is
        /// not a jet, returns 0.
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetEngineCount()</param>
        /// <param name="useKelvin">If true, returns units are in Kelvin.  If false, units are in Celsius.</param>
        /// <returns>Maximum temperature in Kelvin or Celsius, or 0.</returns>
        public double GetMaxJetTemperature(double engineId, bool useKelvin)
        {
            int index = (int)(engineId) - 1;
            if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                double temperature = vc.moduleIdEngines[index].GetMaximumJetTemperature();
                if (!useKelvin && temperature > 0.0)
                {
                    temperature += MASFlightComputerProxy.KelvinToCelsius;
                }
                return temperature;
            }
            return 0.0;
        }

        /// <summary>
        /// Returns the core (non-afterburning) throttle position for the selected jet engine.
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetEngineCount()</param>
        /// <returns>Core throttle, between 0 and 1; if the selected engine is invalid or not a jet, returns 0.</returns>
        public double GetCoreThrottle(double engineId)
        {
            int index = (int)(engineId) - 1;
            if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                return vc.moduleIdEngines[index].GetCoreThrottle();
            }
            return 0.0;
        }
        #endregion

        /// <summary>
        /// The AJE Propellers category provides an interface with engines using
        /// ModuleEnginesAJEPropeller from the Advanced Jet Engines mod.
        /// 
        /// In order to control the engines, those engines must have a properly-configured
        /// MASIdEngine module installed.  In particular, each engine must have a MAS Part ID
        /// other than zero, and each engine must have a unique MAS Part ID.
        /// 
        /// The "Set" functions may use an `engineId` of 0 to set all controlled engines
        /// simultaneously.
        /// 
        /// **Note 1:** AJE Propellers are complex devices, and not all engines support all of the
        /// features listed here.  MAS does not screen the engine for suitability, so some of these
        /// values may be meaningless for some engines.  Likewise, some functions will have no
        /// effect, such as adjusting the propeller RPM setting on a fixed-pitch properller.
        /// For IVA makers who plan on including AJE propeller support in
        /// their IVAs, it is important to make sure your players understand which engines
        /// are intended for use with the IVA.  Alternatively, provide craft files using
        /// the supported engines.
        /// 
        /// **Note 2:** Currently, the manifold pressure and brake shaft power values may be in
        /// either Imperial or SI values, depending on a config setting in ModuleEnginesAJEPropeller.
        /// A future MAS update may make these values agnostic of that config setting (either always Imperial, or
        /// always SI) - just let MOARdV know which system should be used for the values.
        /// </summary>
        #region AJE Propellers

        /// <summary>
        /// Returns the number of correctly configured AJE engines (both jet and propeller).
        /// </summary>
        /// <returns>The number of configured AJE engines.</returns>
        public double GetEngineCount()
        {
            return vc.moduleIdEngines.Length;
        }

        /// <summary>
        /// Get the current supercharger/turbocharger boost setting.
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetEngineCount()</param>
        /// <returns>Engine boost, between 0 and 1, or 0 for an invalid engineId.</returns>
        internal double GetPropellerBoost(double engineId)
        {
            int index = (int)(engineId) - 1;
            if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                return vc.moduleIdEngines[index].GetPropellerBoost();
            }
            return 0.0;
        }

        /// <summary>
        /// Get the current brake shaft power.  **NOTE:** units may be in HP or PS,
        /// depending on the engine's "useHP" field (which defaults to true).
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetEngineCount()</param>
        /// <returns>Brake shaft power or 0 for an invalid engineId.</returns>
        internal double GetPropellerBrakeShaftPower(double engineId)
        {
            int index = (int)(engineId) - 1;
            if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                return vc.moduleIdEngines[index].GetPropellerBrakeShaftPower();
            }
            return 0.0;
        }

        /// <summary>
        /// Get the charge air temperature.
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetEngineCount()</param>
        /// <param name="useKelvin">If true, returns units are in Kelvin.  If false, units are in Celsius.</param>
        /// <returns>Charge air temperature, or 0 for an invalid engineId.</returns>
        internal double GetPropellerChargeAirTemp(double engineId, bool useKelvin)
        {
            int index = (int)(engineId) - 1;
            if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                double temperature = vc.moduleIdEngines[index].GetPropellerChargeAirTemp();
                if (!useKelvin)
                {
                    temperature += MASFlightComputerProxy.KelvinToCelsius;
                }
                return temperature;
            }
            return 0.0;
        }

        /// <summary>
        /// Get the current manifold pressure.  **NOTE:** units may be in InHg or ata,
        /// depending on the engine's "useInHg" field (which defaults to true).
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetEngineCount()</param>
        /// <returns>Manifold pressure, or 0 for an invalid engineId.</returns>
        internal double GetPropellerManifoldPressure(double engineId)
        {
            int index = (int)(engineId) - 1;
            if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                return vc.moduleIdEngines[index].GetPropellerManifoldPressure();
            }
            return 0.0;
        }

        /// <summary>
        /// Get the current mixture setting for the engine, between 0 (full lean) and 1 (full rich)
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetEngineCount()</param>
        /// <returns>Mixture, between 0 and 1, or 0 for an invalid engineId.</returns>
        internal double GetPropellerMixture(double engineId)
        {
            int index = (int)(engineId) - 1;
            if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                return vc.moduleIdEngines[index].GetPropellerMixture();
            }
            return 0.0;
        }

        /// <summary>
        /// Get the net exhaust thrust of the engine, in kN.
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetEngineCount()</param>
        /// <returns>Exhaust thrust in kN, or 0 for an invalid engineId.</returns>
        internal double GetPropellerNetExhaustThrust(double engineId)
        {
            int index = (int)(engineId) - 1;
            if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                return vc.moduleIdEngines[index].GetPropellerNetExhaustThrust();
            }
            return 0.0;
        }

        /// <summary>
        /// Get the net Meredith effect of the engine in kN.
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetEngineCount()</param>
        /// <returns>Net Meredith effect, or 0 for an invalid engineId.</returns>
        internal double GetPropellerNetMeredithEffect(double engineId)
        {
            int index = (int)(engineId) - 1;
            if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                return vc.moduleIdEngines[index].GetPropellerNetMeredithEffect();
            }
            return 0.0;
        }

        /// <summary>
        /// Get the current pitch of the propeller in degrees.
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetEngineCount()</param>
        /// <returns>Pitch in degrees, or 0 for an invalid engineId.</returns>
        internal double GetPropellerPitch(double engineId)
        {
            int index = (int)(engineId) - 1;
            if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                return vc.moduleIdEngines[index].GetPropellerPitch();
            }
            return 0.0;
        }

        /// <summary>
        /// Returns the current RPM of the selected engine.  If an invalid engineId is
        /// provided, or the selected engine is not an AJE propeller engine, returns 0.
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetEngineCount()</param>
        /// <returns>Current propeller speed in RPM, or 0.</returns>
        public double GetPropellerRPM(double engineId)
        {
            int index = (int)(engineId) - 1;
            if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                return vc.moduleIdEngines[index].GetPropellerRPM();
            }
            return 0.0;
        }

        /// <summary>
        /// Returns the current RPM lever position for the selected engine.  The lever
        /// position ranges between 0 and 1.  Returns 0 for invalid engineIDs or engines
        /// that are not AJE propeller engines.
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetEngineCount()</param>
        /// <returns></returns>
        public double GetPropellerRPMLever(double engineId)
        {
            int index = (int)(engineId) - 1;
            if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                return vc.moduleIdEngines[index].GetPropellerRPMLever();
            }
            return 0.0;
        }

        /// <summary>
        /// Get the thrust of the engine, in kN.
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetEngineCount()</param>
        /// <returns>Engine thrust in kN, or 0 for an invalid engineId.</returns>
        internal double GetPropellerThrust(double engineId)
        {
            int index = (int)(engineId) - 1;
            if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                return vc.moduleIdEngines[index].GetPropellerThrust();
            }
            return 0.0;
        }

        /// <summary>
        /// Sets the turbo/super charger boost for the selected engine to the value in `newBoost`.
        /// Returns 1 if the position was updated, 0 otherwise.
        /// 
        /// To set engines individually, set `engineId` to any number between 1 and engine.GetEngineCount() (inclusive).
        /// To set all engines at once, set engineId to 0.
        /// </summary>
        /// <param name="engineId">The id of the engine to change, between 1 and engine.GetEngineCount(), or 0 to set all engines at the same time.</param>
        /// <param name="newBoost">The new boost setting , between 0 and 1 (inclusive).</param>
        /// <returns>1 if the setting was changed, 0 otherwise.</returns>
        public double SetPropellerBoost(double engineId, double newBoost)
        {
            int index = (int)(engineId) - 1;
            if (index == -1)
            {
                bool changed = false;
                for (int i = vc.moduleIdEngines.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleIdEngines[i].SetPropellerBoost((float)newBoost))
                    {
                        changed = true;
                    }
                }

                return changed ? 1.0 : 0.0;
            }
            else if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                return (vc.moduleIdEngines[index].SetPropellerBoost((float)newBoost)) ? 1.0 : 0.0;
            }
            return 0.0;
        }

        /// <summary>
        /// Sets the engine mixture for the selected engine to the value in `newMixture`.
        /// Returns 1 if the position was updated, 0 otherwise.
        /// 
        /// To set engines individually, set `engineId` to any number between 1 and engine.GetEngineCount() (inclusive).
        /// To set all engines at once, set engineId to 0.
        /// </summary>
        /// <param name="engineId">The id of the engine to change, between 1 and engine.GetEngineCount(), or 0 to set all engines at the same time.</param>
        /// <param name="newMixture">The new mixture setting, between 0 (full lean) and 1 (full rich).</param>
        /// <returns>1 if the setting was changed, 0 otherwise.</returns>
        public double SetPropellerMixture(double engineId, double newMixture)
        {
            int index = (int)(engineId) - 1;
            if (index == -1)
            {
                bool changed = false;
                for (int i = vc.moduleIdEngines.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleIdEngines[i].SetPropellerMixture((float)newMixture))
                    {
                        changed = true;
                    }
                }

                return changed ? 1.0 : 0.0;
            }
            else if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                return (vc.moduleIdEngines[index].SetPropellerMixture((float)newMixture)) ? 1.0 : 0.0;
            }
            return 0.0;
        }

        /// <summary>
        /// Sets the RPM lever position for the selected engine to the value in `newPosition`.
        /// Returns 1 if the position was updated, 0 otherwise.
        /// 
        /// To set engines individually, set `engineId` to any number between 1 and engine.GetEngineCount() (inclusive).
        /// To set all engines at once, set engineId to 0.
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetEngineCount(), or 0 to set all engines at the same time.</param>
        /// <param name="newPosition">The new lever position, between 0 and 1 (inclusive).</param>
        /// <returns>1 if the setting was changed, 0 otherwise.</returns>
        public double SetPropellerRPM(double engineId, double newPosition)
        {
            int index = (int)(engineId) - 1;
            if (index == -1)
            {
                bool changed = false;
                for (int i = vc.moduleIdEngines.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleIdEngines[i].SetPropellerRPM((float)newPosition))
                    {
                        changed = true;
                    }
                }

                return changed ? 1.0 : 0.0;
            }
            else if (index >= 0 && index < vc.moduleIdEngines.Length)
            {
                return (vc.moduleIdEngines[index].SetPropellerRPM((float)newPosition)) ? 1.0 : 0.0;
            }
            return 0.0;
        }
        #endregion

        /// <summary>
        /// The Engine Group Management category allows engines grouped together using MASIdEngineGroup to be
        /// toggled on or off separately from the main engine control interface, or be queried if they are
        /// on or off.
        /// 
        /// The functions that accept a 'groupId' parameter may be provided an integer in the range of 1-31 to
        /// affect only the engines in that group.  The parameter may also be 0, which will affect all engines
        /// that have a MASIdEngineGroup part module.
        /// </summary>
        #region Engine Group Management

        /// <summary>
        /// Returns the current fuel flow in grams/second for the selected group.
        /// </summary>
        /// <param name="groupId">A number from 1 to 31 (inclusive) to select a specific group, or 0 to select all groups.</param>
        /// <returns>Current fuel flow in g/s.</returns>
        public double CurrentFuelFlow(double groupId)
        {
            float fuelFlow = 0.0f;
            int id = (int)groupId;

            if (id == 0)
            {
                for (int i = 0; i < vc.engineGroup.Length; ++i)
                {
                    ModuleEngines me = vc.engineGroup[i].engine;

                    if (me.EngineIgnited)
                    {
                        float realIsp = me.realIsp;

                        if (realIsp > 0.0f)
                        {
                            // Compute specific fuel consumption and
                            // multiply by thrust to get grams/sec fuel flow
                            float specificFuelConsumption = 101972.0f / realIsp;
                            fuelFlow += specificFuelConsumption * me.finalThrust;
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < vc.engineGroup.Length; ++i)
                {
                    if (vc.engineGroup[i].partId == id)
                    {
                        ModuleEngines me = vc.engineGroup[i].engine;

                        if (me.EngineIgnited)
                        {
                            float realIsp = me.realIsp;

                            if (realIsp > 0.0f)
                            {
                                // Compute specific fuel consumption and
                                // multiply by thrust to get grams/sec fuel flow
                                float specificFuelConsumption = 101972.0f / realIsp;
                                fuelFlow += specificFuelConsumption * me.finalThrust;
                            }
                        }
                    }
                }
            }

            return fuelFlow;
        }

        /// <summary>
        /// Returns the current thrust in kiloNewtons for the selected group.
        /// </summary>
        /// <param name="groupId">A number from 1 to 31 (inclusive) to select a specific group, or 0 to select all groups.</param>
        /// <returns>Current thrust in kN.</returns>
        public double CurrentThrustkN(double groupId)
        {
            float thrust = 0.0f;
            int id = (int)groupId;

            if (id == 0)
            {
                for (int i = 0; i < vc.engineGroup.Length; ++i)
                {
                    ModuleEngines me = vc.engineGroup[i].engine;

                    if (me.EngineIgnited)
                    {
                        thrust += me.finalThrust;
                    }
                }
            }
            else
            {
                for (int i = 0; i < vc.engineGroup.Length; ++i)
                {
                    if (vc.engineGroup[i].partId == id)
                    {
                        ModuleEngines me = vc.engineGroup[i].engine;

                        if (me.EngineIgnited)
                        {
                            thrust += me.finalThrust;
                        }
                    }
                }
            }

            return thrust;
        }

        /// <summary>
        /// Returns 1 if the selected groupId has at least one member.
        /// </summary>
        /// <param name="groupId">A number from 1 to 31 (inclusive) to select a specific group, or 0 to select all groups.</param>
        /// <returns>0 if there are no engines belonging to the group, 1 if there is.</returns>
        public double EngineGroupValid(double groupId)
        {
            int id = (int)groupId;
            if (id == 0)
            {
                return (vc.engineGroup.Length > 0) ? 1.0 : 0.0;
            }
            else
            {
                for (int i = 0; i < vc.engineGroup.Length; ++i)
                {
                    if (vc.engineGroup[i].partId == id)
                    {
                        return 1.0;
                    }
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns 1 if any engine in the specified groupId is active.  If no engine is active,
        /// or if there are no engines in the specified groupId, returns 0.
        /// </summary>
        /// <param name="groupId">A number from 1 to 31 (inclusive) to select a specific group, or 0 to select all groups.</param>
        /// <returns>1 if any selected engine is active, 0 otherwise.</returns>
        public double GetEngineGroupActive(double groupId)
        {
            int id = (int)groupId;
            if (id == 0)
            {
                for (int i = 0; i < vc.engineGroup.Length; ++i)
                {
                    ModuleEngines me = vc.engineGroup[i].engine;
                    if (me.EngineIgnited && me.isEnabled && me.isOperational)
                    {
                        return 1.0;
                    }
                }
            }
            else
            {
                for (int i = 0; i < vc.engineGroup.Length; ++i)
                {
                    if (vc.engineGroup[i].partId == id)
                    {
                        ModuleEngines me = vc.engineGroup[i].engine;
                        if (me.EngineIgnited && me.isEnabled && me.isOperational)
                        {
                            return 1.0;
                        }
                    }
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the number of unique engine group IDs found on the vessel.
        /// </summary>
        /// <returns></returns>
        public double GetEngineGroupCount()
        {
            uniqueIds.Clear();
            for (int i = 0; i < vc.engineGroup.Length; ++i)
            {
                if (!uniqueIds.Contains(vc.engineGroup[i].partId))
                {
                    uniqueIds.Add(vc.engineGroup[i].partId);
                }
            }

            return uniqueIds.Count;
        }

        /// <summary>
        /// Returns the average of the throttle limit for the selected engine group,
        /// ranging from 0 (no thrust) to 1 (maximum thrust).
        /// </summary>
        /// <param name="groupId">A number from 1 to 31 (inclusive) to select a specific group, or 0 to select all groups.</param>
        /// <returns>A number from 0 (no thrust or no engines) to 1 (maximum thrust).</returns>
        public double GetThrottleLimit(double groupId)
        {
            float limit = 0.0f;
            float count = 0.0f;

            int id = (int)groupId;
            if (id == 0)
            {
                for (int i = 0; i < vc.engineGroup.Length; ++i)
                {
                    ModuleEngines me = vc.engineGroup[i].engine;

                    if (me.EngineIgnited)
                    {
                        limit += me.thrustPercentage;
                        // We use 100 because thrustPercentage is in the range [0, 100].  So, using
                        // 100 here gives us a free conversion to [0, 1].
                        count += 100.0f;
                    }
                }
            }
            else
            {
                for (int i = 0; i < vc.engineGroup.Length; ++i)
                {
                    if (vc.engineGroup[i].partId == id)
                    {
                        ModuleEngines me = vc.engineGroup[i].engine;

                        if (me.EngineIgnited)
                        {
                            limit += me.thrustPercentage;
                            count += 100.0f;
                        }
                    }
                }
            }

            return (count > 0.0f) ? (limit / count) : 0.0;
        }

        /// <summary>
        /// Returns the maximum thrust in kiloNewtons for the selected group.
        /// </summary>
        /// <param name="groupId">A number from 1 to 31 (inclusive) to select a specific group, or 0 to select all groups.</param>
        /// <returns>Max thrust in kN.</returns>
        public double MaxThrustkN(double groupId)
        {
            float thrust = 0.0f;
            int id = (int)groupId;

            if (id == 0)
            {
                for (int i = 0; i < vc.engineGroup.Length; ++i)
                {
                    ModuleEngines me = vc.engineGroup[i].engine;

                    if (me.EngineIgnited)
                    {
                        thrust += me.GetMaxThrust();
                    }
                }
            }
            else
            {
                for (int i = 0; i < vc.engineGroup.Length; ++i)
                {
                    if (vc.engineGroup[i].partId == id)
                    {
                        ModuleEngines me = vc.engineGroup[i].engine;

                        if (me.EngineIgnited)
                        {
                            thrust += me.GetMaxThrust();
                        }
                    }
                }
            }

            return thrust;
        }

        /// <summary>
        /// Toggles all of the engines in the selected group that can be toggled
        /// (activates them if they're deactivated, shuts them off if they're active).
        /// </summary>
        /// <param name="groupId">A number from 1 to 31 (inclusive) to select a specific group, or 0 to select all groups.</param>
        /// <param name="newState">When true, activates engines in the group.  When false, deactivates them.</param>
        /// <returns>1 if any engines were toggled, 0 otherwise.</returns>
        public double SetEnginesEnabled(double groupId, bool newState)
        {
            int id = (int)groupId;
            double anyChanged = 0.0;
            if (id == 0)
            {
                for (int i = 0; i < vc.engineGroup.Length; ++i)
                {
                    ModuleEngines me = vc.engineGroup[i].engine;
                    Part thatPart = me.part;

                    if (thatPart.inverseStage == StageManager.CurrentStage || !newState)
                    {
                        if (me.EngineIgnited != newState)
                        {
                            if (newState && me.allowRestart)
                            {
                                me.Activate();
                                anyChanged = 1.0;
                            }
                            else if (me.allowShutdown)
                            {
                                me.Shutdown();
                                anyChanged = 1.0;
                            }
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < vc.engineGroup.Length; ++i)
                {
                    if (vc.engineGroup[i].partId == id)
                    {
                        ModuleEngines me = vc.engineGroup[i].engine;
                        Part thatPart = me.part;

                        if (thatPart.inverseStage == StageManager.CurrentStage || !newState)
                        {
                            if (me.EngineIgnited != newState)
                            {
                                if (newState && me.allowRestart)
                                {
                                    me.Activate();
                                    anyChanged = 1.0;
                                }
                                else if (me.allowShutdown)
                                {
                                    me.Shutdown();
                                    anyChanged = 1.0;
                                }
                            }
                        }
                    }
                }
            }

            return anyChanged;
        }

        /// <summary>
        /// Set the throttle limit for all of the engines in the selected group.  May be set to any value between 0 and 1.  Values outside
        /// that range are clamped to [0, 1].
        /// </summary>
        /// <param name="groupId">A number from 1 to 31 (inclusive) to select a specific group, or 0 to select all groups.</param>
        /// <param name="newLimit">The new throttle limit, between 0 and 1 (inclusive).</param>
        /// <returns>1 if the throttle limit was updated, 0 otherwise.</returns>
        public double SetThrottleLimit(double groupId, double newLimit)
        {
            float limit = Mathf.Clamp01((float)newLimit) * 100.0f;

            int id = (int)groupId;
            if (id == 0)
            {
                for (int i = 0; i < vc.engineGroup.Length; ++i)
                {
                    ModuleEngines me = vc.engineGroup[i].engine;

                    if (me.EngineIgnited)
                    {
                        me.thrustPercentage = limit;
                    }
                }

                return (vc.engineGroup.Length > 0) ? 1.0 : 0.0;
            }
            else
            {
                bool updated = false;
                for (int i = 0; i < vc.engineGroup.Length; ++i)
                {
                    if (vc.engineGroup[i].partId == id)
                    {
                        ModuleEngines me = vc.engineGroup[i].engine;

                        if (me.EngineIgnited)
                        {
                            me.thrustPercentage = limit;
                        }
                    }
                }
                return (updated) ? 1.0 : 0.0;
            }
        }

        /// <summary>
        /// Toggles all of the engines in the selected group that can be toggled
        /// (activates them if they're deactivated, shuts them off if they're active).
        /// </summary>
        /// <param name="groupId">A number from 1 to 31 (inclusive) to select a specific group, or 0 to select all groups.</param>
        /// <returns>1 if any engines were toggled, 0 otherwise.</returns>
        public double ToggleEnginesEnabled(double groupId)
        {
            int id = (int)groupId;
            bool newState = (GetEngineGroupActive(groupId) == 0.0);
            double anyChanged = 0.0;
            if (id == 0)
            {
                for (int i = 0; i < vc.engineGroup.Length; ++i)
                {
                    ModuleEngines me = vc.engineGroup[i].engine;
                    Part thatPart = me.part;

                    if (thatPart.inverseStage == StageManager.CurrentStage || !newState)
                    {
                        if (me.EngineIgnited != newState)
                        {
                            if (newState && me.allowRestart)
                            {
                                me.Activate();
                                anyChanged = 1.0;
                            }
                            else if (me.allowShutdown)
                            {
                                me.Shutdown();
                                anyChanged = 1.0;
                            }
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < vc.engineGroup.Length; ++i)
                {
                    if (vc.engineGroup[i].partId == id)
                    {
                        ModuleEngines me = vc.engineGroup[i].engine;
                        Part thatPart = me.part;

                        if (thatPart.inverseStage == StageManager.CurrentStage || !newState)
                        {
                            if (me.EngineIgnited != newState)
                            {
                                if (newState && me.allowRestart)
                                {
                                    me.Activate();
                                    anyChanged = 1.0;
                                }
                                else if (me.allowShutdown)
                                {
                                    me.Shutdown();
                                    anyChanged = 1.0;
                                }
                            }
                        }
                    }
                }
            }

            return anyChanged;
        }
        #endregion

        /// <summary>
        /// The Thrust Reverser section controls thrust reversers attached to engines.
        /// </summary>
        #region Thrust Reverser

        /// <summary>
        /// The number of thrust reverser modules found on the vessel.
        /// </summary>
        /// <returns>Number of thrust reverser modules.</returns>
        public double ThrustReverserCount()
        {
            return vc.moduleThrustReverser.Length;
        }

        /// <summary>
        /// Returns the normalized thrust reverser position, or 0 if there are none.
        /// </summary>
        /// <returns>Thrust reverser position in the range [0, 1], or 0.</returns>
        public double ThrustReverserPosition()
        {
            float position = 0.0f;
            int numReverserers = vc.moduleThrustReverser.Length;
            for (int i = 0; i < numReverserers; ++i)
            {
                position += vc.moduleThrustReverser[i].Position();
            }
            if (numReverserers > 1)
            {
                position /= (float)(numReverserers);
            }

            return position;
        }

        /// <summary>
        /// Sets the state of thrust reversers.  Only affects thrust reversers that are not actively moving.
        /// </summary>
        /// <param name="active">If true, deploy the thrust reversers.  If false, stow the thrust reversers.</param>
        /// <returns>1 if any thrust reversers changed; 0 otherwise.</returns>
        public double SetThrustReverser(bool active)
        {
            bool anyChanged = false;
            int numReverserers = vc.moduleThrustReverser.Length;
            if (active)
            {
                for (int i = 0; i < numReverserers; ++i)
                {
                    if (vc.moduleThrustReverser[i].Position() < 0.005f)
                    {
                        vc.moduleThrustReverser[i].ToggleReverser();
                    }
                }
            }
            else
            {
                for (int i = 0; i < numReverserers; ++i)
                {
                    if (vc.moduleThrustReverser[i].Position() > 0.995f)
                    {
                        vc.moduleThrustReverser[i].ToggleReverser();
                    }
                }
            }

            return (anyChanged) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Toggles installed thrust reversers (deploys them if they are not deployed,
        /// retracts them if they are deployed).
        /// </summary>
        /// <returns>1 if reversers were toggled, 0 if there are no thrust reversers.</returns>
        public double ToggleThrustReverser()
        {
            int numReverserers = vc.moduleThrustReverser.Length;
            for (int i = 0; i < numReverserers; ++i)
            {
                vc.moduleThrustReverser[i].ToggleReverser();
            }

            return (numReverserers > 0) ? 1.0 : 0.0;
        }
        #endregion
    }
}
