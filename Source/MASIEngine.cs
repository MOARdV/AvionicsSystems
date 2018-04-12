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
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetPropellerCount()</param>
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
        /// Returns the core (non-afterburning) throttle position for the selected jet engine.
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetPropellerCount()</param>
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
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetPropellerCount()</param>
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
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetPropellerCount()</param>
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
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetPropellerCount()</param>
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
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetPropellerCount()</param>
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
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetPropellerCount()</param>
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
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetPropellerCount()</param>
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
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetPropellerCount()</param>
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
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetPropellerCount()</param>
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
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetPropellerCount()</param>
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
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetPropellerCount()</param>
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
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetPropellerCount()</param>
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
        /// To set engines individually, set `engineId` to any number between 1 and engine.GetPropellerCount() (inclusive).
        /// To set all engines at once, set engineId to 0.
        /// </summary>
        /// <param name="engineId">The id of the engine to change, between 1 and engine.GetPropellerCount(), or 0 to set all engines at the same time.</param>
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
        /// To set engines individually, set `engineId` to any number between 1 and engine.GetPropellerCount() (inclusive).
        /// To set all engines at once, set engineId to 0.
        /// </summary>
        /// <param name="engineId">The id of the engine to change, between 1 and engine.GetPropellerCount(), or 0 to set all engines at the same time.</param>
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
        /// To set engines individually, set `engineId` to any number between 1 and engine.GetPropellerCount() (inclusive).
        /// To set all engines at once, set engineId to 0.
        /// </summary>
        /// <param name="engineId">The id of the engine to check, between 1 and engine.GetPropellerCount(), or 0 to set all engines at the same time.</param>
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
        /// Toggles installed thrust reversers (deploys them if they are not deployed,
        /// retracts them if they are deployed).
        /// </summary>
        /// <returns>1 if reversers were toggle, 0 if there are no thrust reversers.</returns>
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
