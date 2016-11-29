//#define SHOW_EXTRA_DEBUG_STUFF
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
    /// MASClimateControl simulates managing a pressurized volume occupied by Kerbals.
    /// It does the following: adds body heat to the cabin, along with heat from
    /// electronics.  Adds/removes heat from the cabin based on the difference between
    /// cabin temperature and the part's internal temperature.  Adds/removes heat from
    /// the cabin based on a goal temperature and the engagement of the cabin heater.
    /// 
    /// Heat added to the cabin from the part is removed from the part, and vice
    /// versa (net system energy is conserved).  In addition, when the climate control
    /// system is removing heat from the cabin, it is applied to the part to simulate
    /// heat exchangers.
    /// 
    /// The module makes these assumptions:
    /// 
    /// * Shirtsleeve environment with sea-level air pressure.
    /// 
    /// * A desired temperature somewhere near 300K (275K-325K, preferably).
    /// 
    /// * Perfect efficiency converting EC to heat (and perfect efficiency in
    /// cooling, too).
    /// 
    /// * 1EC/s = 1kW (for converting EC to heating/cooling effects).
    /// 
    /// --- TODO List:
    /// 
    /// * Allow volume / area to be specified, instead of inferred.  Actually, volume
    /// is already - divide the desired cabin volume by CrewCapacity, and
    /// set volumePerKerbal to that result.  Area, however, is hard-coded.
    /// Or should the volume be specified as an absolute number, instead of per-Kerbal?
    /// 
    /// * Consequences for exceeding survivable temperatures.  Including specifying
    /// those temperatures (which gets messy - space suits provide a buffer.
    /// 
    /// * Seeing if there's a way to implement a mode that is compatible with analytic
    /// mode during high warp.
    /// </summary>
    class MASClimateControl : PartModule
    {
        /// <summary>
        /// Goal temperature of the cabin in K.  Defaults to 293K (~20C / 68F).
        /// </summary>
        [KSPField(guiName = "Temp Goal (K)", guiActive = true, guiFormat = "0.0")]
        public float cabinTempGoal = 293.0f;

        /// <summary>
        /// Kerbal heat output in kW.  A human adult ranges ~0.09kW - 0.12kW.
        /// Default is 0.06 (yes, that's probably high for their size).
        /// This value will be the dominant contributor to the thermal load
        /// in the pod.
        /// </summary>
        [KSPField]
        public float kerbalHeatGeneration = 0.060f;

        /// <summary>
        /// Heat contributed by equipment in the pod, in kW.  For simplicity,
        /// use ModuleCommand's energy draw.  If that's 0, use some small value
        /// like 0.02 (which is the default if this field is omitted).
        /// </summary>
        [KSPField]
        public float equipmentHeatGeneration = 0.020f;

        /// <summary>
        /// Pod heater system maximum energy consumption in EC/s.  This must
        /// be higher than kerbalHeatGeneration * CrewCapacity + equipmentHeatGeneration, 
        /// or it won't be able to keep up.  Set to 0.5 if omitted.
        /// </summary>
        [KSPField]
        public float podHeaterOutput = 0.5f;

        /// <summary>
        /// Records the amount of energy actually requested from the heater.
        /// </summary>
        internal float podHeaterDraw = 0.0f;

        /// <summary>
        /// Cabin temperature readout
        /// </summary>
        [KSPField(guiName = "Cabin Temp (K)", guiActive = true, guiFormat = "0.0")]
        public float cabinTemperature = 0.0f;

        /// <summary>
        /// Heater on/off readout
        /// </summary>
        [KSPField(guiName = "Climate Control", isPersistant = true, guiActive = true)]
        [UI_Toggle(disabledText = "Off", enabledText = "On")]
        public bool enableHeater = false;

#if SHOW_EXTRA_DEBUG_STUFF
        // Debug / peek-under-the-hood numbers - may remove them.
        [KSPField(guiName = "Part Temp (K)", guiActive = true, guiFormat = "0.0")]
        public float D_PartTemperature = 0.0f;
        [KSPField(guiName = "Heater/Cooler (kW)", guiActive = true, guiFormat = "0.000")]
        public float D_HeaterSetting = 0.0f;
        [KSPField(guiName = "Cabin Heat (kW)", guiActive = true, guiFormat = "0.000000")]
        public float D_CabinHeatLoad = 0.0f;
#endif

        /// <summary>
        /// Keep track of how much energy is in the cabin's atmosphere, in kJ.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float cabinEnergy = -1.0f;

        /// <summary>
        /// How much cabin volume is there per Kerbal, in m^3?  Defaults to 1.0m^3.
        /// </summary>
        [KSPField]
        public float volumePerKerbal = 1.0f;

        /// <summary>
        /// Thermal mass of the atmosphere in the cabin, so we can update
        /// temperatures.  Derived value.
        /// </summary>
        private float cabinThermalMass;

        /// <summary>
        /// Approximate surface area of the cabin.  Derived value.
        /// </summary>
        private float cabinInteriorArea;

        /// <summary>
        /// The heat transfer coefficient controls how much heat leaks into
        /// the cabin from the pod, and vice versa.  It is in units of
        /// W/m^2 * K.  Some example values - mild steel is 7.9.  Cast iron
        /// is 5.7.  BTW, these are both way too high for an insulated cabin.
        /// 
        /// From what I read, R-60 insulation (US rating) is roughly R-340 using
        /// SI values, and R (SI) = 1 / heatTransferCoefficient, so R-60 insulation
        /// would provide a value around 0.003.  That feels a little bit too low,
        /// so I set the default to 0.005.  Even so, Kerbal body heat will
        /// dominate the heat values most of the time - a value of 0.005 will
        /// typically translate to a couple of Watts for most launch and orbital
        /// conditions.
        /// </summary>
        [KSPField]
        public float heatTransferCoefficient = 0.005f;

        /// <summary>
        /// Track the base energy rate in kW.  This is the energy continuously added to the
        /// cabin from Kerbal waste heat and electronics heat.  Derived value.
        /// </summary>
        private float baseEnergy = 0.0f;

        /// <summary>
        /// Energy curve tracking air conditioner / heater contribution.  Provides the number
        /// of EC/s the heater/cooler will consume (which correlates to the number of kW the
        /// unit is adding or subtracting).  This curve is centered on CoreTempGoal, allowing
        /// a +/- 2.5* band bracketing that value where the equipment operates at reduced
        /// power (it otherwise runs at full power outside that band).  Derived value.
        /// </summary>
        private FloatCurve poweredEnergyCurve = null;

        /// <summary>
        /// FixedUpdate - determine if the heating / cooling system is active.  If it is,
        /// consume power.  If we're cooling, dump excess heat to the "heat exchanger"
        /// on the pod's skin.
        /// </summary>
        public void FixedUpdate()
        {
            // Disable at high warp.  I haven't wrapped my head around how this would work in
            // analytics mode.
            if (HighLogic.LoadedSceneIsFlight && TimeWarp.CurrentRate <= PhysicsGlobals.ThermalMaxIntegrationWarp)
            {
                // Convective heat transfer = 
                // Q* = h * A * (Ta - Tb)
                //  - Q* = heat transfer (Watts)
                //  - h  = heat transfer coefficient (W / m^2 * K)
                //  - A  = area (m^2)
                //  - (Ta - Tb) = temperatureDifference of solid - fluid
                //
                // Q* is what we want to solve, in W.
                // A is a simplistic estimate based on the crew capacity and volume/Kerbal.
                // h is based on values I got from the Internet, which is, of course, authorative :)
                //   (actually, the first pass was from http://www.engineeringtoolbox.com/overall-heat-transfer-coefficients-d_284.html
                //   using the air - steel - air value of 7.9 W / m^2; that was way high).

                double temperatureDifference = part.temperature - cabinTemperature;
                double Q = heatTransferCoefficient * cabinInteriorArea * temperatureDifference;

                // Convert to kW
                Q *= 0.001;

                double energyToPart = -Q;

                // Q heat transfered from the cabin walls + heat energy from Kerbals + equipment
                float cabinHeatLoad = (float)Q + baseEnergy;
                // Convert from kW to kJ using fixedDeltaTime.
                double energyToCabin = cabinHeatLoad * TimeWarp.fixedDeltaTime;

                // Update cabin temperature based on last update.
                cabinTemperature = cabinEnergy / cabinThermalMass;
#if SHOW_EXTRA_DEBUG_STUFF
                D_CabinHeatLoad = cabinHeatLoad;
                D_HeaterSetting = 0.0f;
                D_PartTemperature = (float)part.temperature;
#endif

                // If the heater's enabled, see how much heat the climate control hardware
                // needs to add or remove.
                if (enableHeater)
                {
                    float heaterEnergy = poweredEnergyCurve.Evaluate(cabinTemperature);

                    double requiredEC = Math.Abs(heaterEnergy) * TimeWarp.fixedDeltaTime;
                    if (requiredEC > 0.0)
                    {
                        double powerDrawn = part.RequestResource("ElectricCharge", requiredEC);
                        podHeaterDraw = (float)(powerDrawn / TimeWarp.fixedDeltaTime);

                        if (heaterEnergy < 0.0f)
                        {
                            energyToCabin -= powerDrawn;

                            // Dump the heat from cooling into the part.
                            energyToPart += powerDrawn;
                        }
                        else
                        {
                            energyToCabin += powerDrawn;
                        }
#if SHOW_EXTRA_DEBUG_STUFF
                        D_HeaterSetting = Math.Sign(heaterEnergy) * (float)powerDrawn / TimeWarp.fixedDeltaTime;
#endif
                    }
                    else
                    {
                        podHeaterDraw = 0.0f;
                    }
                }
                else
                {
                    podHeaterDraw = 0.0f;
                }

                // Transfer energy.  kW to the part,
                part.AddThermalFlux(energyToPart);
                // kJ to our cabin energy.
                cabinEnergy += (float)energyToCabin;
            }
        }

        /// <summary>
        /// Unregister the callback.
        /// </summary>
        public void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                GameEvents.onVesselCrewWasModified.Remove(onVesselCrewChanged);
            }
        }

        /// <summary>
        /// Set up.
        /// </summary>
        public void Start()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                GameEvents.onVesselCrewWasModified.Add(onVesselCrewChanged);

                Keyframe[] energyCurve = new Keyframe[6];

                // Cabin heating / cooling equipment settings.
                // below CoreTempGoal, add heat
                energyCurve[0] = new Keyframe(0.0f, podHeaterOutput, 0.0f, 0.0f);
                energyCurve[1] = new Keyframe(cabinTempGoal - 2.5f, podHeaterOutput, 0.0f, 0.0f);
                // Near CoreTempGoal, shut off.
                energyCurve[2] = new Keyframe(cabinTempGoal - 0.25f, 0, 0.0f, 0.0f);
                energyCurve[3] = new Keyframe(cabinTempGoal + 0.25f, 0, 0.0f, 0.0f);
                // Above CoreTempGoal, remove heat.
                energyCurve[4] = new Keyframe(cabinTempGoal + 2.5f, -podHeaterOutput, 0.0f, 0.0f);
                energyCurve[5] = new Keyframe(Math.Max(cabinTempGoal * 2.0f, 1000.0f), -podHeaterOutput, 0.0f, 0.0f);
                poweredEnergyCurve = new FloatCurve(energyCurve);

                UpdateBaseEnergy();

                float cabinVolume = volumePerKerbal * part.CrewCapacity;

                // specific heat @1ATM, 300K.  Accurate enough for most reasonable temperature values.
                // density @1ATM.
                // specific heat kJ/kg K       m^3     kg/m^3 density
                cabinThermalMass = 1.005f * cabinVolume * 1.225f;

                // Simplistic estimate of cabin area:
                // treat the cabinVolume as a cube, and compute the surface
                // area of the cube.  That allows a larger area than the ideal
                // sphere.
                cabinInteriorArea = 6.0f * Mathf.Pow(cabinVolume, 2.0f / 3.0f);

                if (cabinEnergy <= 0.0f)
                {
                    // Initialize cabin to part.temperature if we didn't already
                    // have a value.
                    cabinEnergy = (float)(part.temperature * cabinThermalMass);
                }
            }
        }

        /// <summary>
        /// Callback so we can see if the crew count in the pod changed, since that
        /// affects heat loads.
        /// </summary>
        /// <param name="v"></param>
        private void onVesselCrewChanged(Vessel v)
        {
            if (v == vessel)
            {
                UpdateBaseEnergy();
            }
        }

        /// <summary>
        /// Update the energy curves used by the climate control module.  This
        /// method is called at Start, and any time the crew count changes.
        /// </summary>
        private void UpdateBaseEnergy()
        {
            baseEnergy = part.protoModuleCrew.Count * kerbalHeatGeneration + equipmentHeatGeneration;
        }
    }
}
