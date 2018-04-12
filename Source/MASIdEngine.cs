/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2018 MOARdV
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
using System.Reflection;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// MASIdEngine is used to identify unique engine parts, allowing for
    /// fine-tuned control of the vessel.
    /// 
    /// It also encompasses the reflection that allows MAS to talk to the
    /// Advanced Jet Engine mode (AJE).
    /// </summary>
    class MASIdEngine : MASIdGeneric
    {
        //--- Reflection info for Advanced Jet Engine
        private static readonly bool ajeInstalled = false;
        internal static readonly Type ajePropellerAPI_t;
        internal static readonly Type ajeJetAPI_t;
        // All fields are floats

        // Read-only fields:
        private static readonly Func<object, float> getPropRPM;
        private static readonly Func<object, float> getPropPitch;
        private static readonly Func<object, float> getPropThrust;
        private static readonly Func<object, float> getManifoldPressure;
        private static readonly Func<object, float> getChargeAirTemp;
        private static readonly Func<object, float> getNetExhaustThrust;
        private static readonly Func<object, float> getNetMeredithEffect;
        private static readonly Func<object, float> getBrakeShaftPower;

        private static readonly Func<object, float> getCoreThrottle;
        private static readonly Func<object, float> getAfterburnerThrottle;
        // maxEngineTemp
        private static readonly Func<object, double> getMaxJetTemp;
        // engineTempString: {0}K / {1}K, where {0} is current, {1} is max
        private static readonly Func<object, string> getCurrentJetTemp;

        // Read-write fields:
        private static readonly Func<object, float> getBoost;
        private static readonly Action<object, float> setBoost;
        private static readonly Func<object, float> getRpmLever;
        private static readonly Action<object, float> setRpmLever;
        private static readonly Func<object, float> getMixture;
        private static readonly Action<object, float> setMixture;

        //--- Tracked fields
        private PartModule ajePropellerModule;
        private PartModule ajeJetModule;
        //private ModuleEngines engineModule;

        /// <summary>
        /// Scan through the current part to find a trackable engine, but
        /// only if the partId is not zero.
        /// </summary>
        public void Start()
        {
            if (HighLogic.LoadedSceneIsFlight && partId > 0)
            {
                if (ajeInstalled)
                {

                    ajePropellerModule = part.Modules["ModuleEnginesAJEPropeller"];
                    ajeJetModule = part.Modules["ModuleEnginesAJEJet"];
                    if (ajePropellerModule == null && ajeJetModule == null)
                    {
                        Utility.LogErrorMessage(this, "Didn't find any supported AJE engine");
                    }
                }

                // If no tracked modules are installed, we don't want this part to register
                // with the vessel computer.
                if (ajePropellerModule == null && ajeJetModule == null)
                {
                    partId = 0;
                }
                //if (ajeModule == null) // not yet
                //{
                //    engineModule = part.FindModuleImplementing<ModuleEngines>();
                //    Utility.LogMessage(this, "Found ModulesEngine");
                //}
            }
        }

        #region AJE Jet
        internal float GetAfterburnerThrottle()
        {
            if (ajeJetModule != null)
            {
                return getAfterburnerThrottle(ajeJetModule) * 0.01f;
            }

            return 0.0f;
        }

        internal float GetCoreThrottle()
        {
            if (ajeJetModule != null)
            {
                return getCoreThrottle(ajeJetModule) * 0.01f;
            }

            return 0.0f;
        }

        internal double GetCurrentJetTemperature()
        {
            // returns K
            if (ajeJetModule != null)
            {
                // This is something of a hack - the numeric value is not exposed,
                // so I have to parse it out of a string.  If the string's format
                // changes, this will break.
                string currentString = getCurrentJetTemp(ajeJetModule);
                string[] token = currentString.Split('K');
                if(token.Length > 0)
                {
                    double temperature;
                    if (double.TryParse(token[0], out temperature))
                    {
                        return temperature;
                    }
                }
            }

            return 0.0;
        }

        internal double GetMaximumJetTemperature()
        {
            // returns K
            if (ajeJetModule != null)
            {
                return getMaxJetTemp(ajeJetModule);
            }

            return 0.0;
        }
        #endregion

        #region AJE Propellers
        internal float GetPropellerBoost()
        {
            if (ajePropellerModule != null)
            {
                return getBoost(ajePropellerModule);
            }

            return 0.0f;
        }

        internal float GetPropellerBrakeShaftPower()
        {
            if (ajePropellerModule != null)
            {
                // TODO: BHP units need to be queried - ajeModule.useHP determines if this
                // field is in horsepower or SI units (PS).
                return getBrakeShaftPower(ajePropellerModule);
            }

            return 0.0f;
        }

        internal float GetPropellerChargeAirTemp()
        {
            if (ajePropellerModule != null)
            {
                return getChargeAirTemp(ajePropellerModule);
            }

            return 0.0f;
        }

        internal float GetPropellerManifoldPressure()
        {
            if (ajePropellerModule != null)
            {
                // TODO: Manifold Press units to be queried - ajeModule.useInHg determines
                // if this field is in inches Hg or SI units (ata)
                return getManifoldPressure(ajePropellerModule);
            }

            return 0.0f;
        }

        internal float GetPropellerMixture()
        {
            if (ajePropellerModule != null)
            {
                return getMixture(ajePropellerModule);
            }

            return 0.0f;
        }

        internal float GetPropellerNetExhaustThrust()
        {
            if (ajePropellerModule != null)
            {
                return getNetExhaustThrust(ajePropellerModule);
            }

            return 0.0f;
        }

        internal float GetPropellerNetMeredithEffect()
        {
            if (ajePropellerModule != null)
            {
                return getNetMeredithEffect(ajePropellerModule);
            }

            return 0.0f;
        }

        internal float GetPropellerPitch()
        {
            if (ajePropellerModule != null)
            {
                return getPropPitch(ajePropellerModule);
            }

            return 0.0f;
        }

        internal float GetPropellerRPM()
        {
            if (ajePropellerModule != null)
            {
                return getPropRPM(ajePropellerModule);
            }

            return 0.0f;
        }

        internal float GetPropellerRPMLever()
        {
            if (ajePropellerModule != null)
            {
                return getRpmLever(ajePropellerModule);
            }

            return 0.0f;
        }

        internal float GetPropellerThrust()
        {
            if (ajePropellerModule != null)
            {
                return getPropThrust(ajePropellerModule);
            }

            return 0.0f;
        }

        internal bool SetPropellerBoost(float newBoost)
        {
            if (ajePropellerModule != null)
            {
                setBoost(ajePropellerModule, Mathf.Clamp01(newBoost));
                return true;
            }

            return false;
        }

        internal bool SetPropellerMixture(float newMixture)
        {
            if (ajePropellerModule != null)
            {
                setMixture(ajePropellerModule, Mathf.Clamp01(newMixture));
                return true;
            }

            return false;
        }

        internal bool SetPropellerRPM(float newRpm)
        {
            if (ajePropellerModule != null)
            {
                setRpmLever(ajePropellerModule, Mathf.Clamp01(newRpm));
                return true;
            }

            return false;
        }
        #endregion

        /// <summary>
        /// Static constructor - go find which plugins are installed.
        /// </summary>
        static MASIdEngine()
        {
            ajePropellerAPI_t = Utility.GetExportedType("AJE", "AJE.ModuleEnginesAJEPropeller");
            ajeJetAPI_t = Utility.GetExportedType("AJE", "AJE.ModuleEnginesAJEJet");

            if (ajePropellerAPI_t != null && ajeJetAPI_t != null)
            {
                FieldInfo propRPM_t = ajePropellerAPI_t.GetField("propRPM", BindingFlags.Instance | BindingFlags.Public);
                if (propRPM_t == null)
                {
                    Utility.LogErrorMessage("propRPM_t is null");
                    return;
                }
                getPropRPM = DynamicMethodFactory.CreateGetField<object, float>(propRPM_t);

                FieldInfo propPitch_t = ajePropellerAPI_t.GetField("propPitch", BindingFlags.Instance | BindingFlags.Public);
                if (propPitch_t == null)
                {
                    Utility.LogErrorMessage("propPitch_t is null");
                    return;
                }
                getPropPitch = DynamicMethodFactory.CreateGetField<object, float>(propPitch_t);

                FieldInfo propThrust_t = ajePropellerAPI_t.GetField("propThrust", BindingFlags.Instance | BindingFlags.Public);
                if (propThrust_t == null)
                {
                    Utility.LogErrorMessage("propThrust_t is null");
                    return;
                }
                getPropThrust = DynamicMethodFactory.CreateGetField<object, float>(propThrust_t);

                FieldInfo manifoldPressure_t = ajePropellerAPI_t.GetField("manifoldPressure", BindingFlags.Instance | BindingFlags.Public);
                if (manifoldPressure_t == null)
                {
                    Utility.LogErrorMessage("manifoldPressure_t is null");
                    return;
                }
                getManifoldPressure = DynamicMethodFactory.CreateGetField<object, float>(manifoldPressure_t);

                FieldInfo chargeAirTemp_t = ajePropellerAPI_t.GetField("chargeAirTemp", BindingFlags.Instance | BindingFlags.Public);
                if (chargeAirTemp_t == null)
                {
                    Utility.LogErrorMessage("chargeAirTemp_t is null");
                    return;
                }
                getChargeAirTemp = DynamicMethodFactory.CreateGetField<object, float>(chargeAirTemp_t);

                FieldInfo netExhaustThrust_t = ajePropellerAPI_t.GetField("netExhaustThrust", BindingFlags.Instance | BindingFlags.Public);
                if (netExhaustThrust_t == null)
                {
                    Utility.LogErrorMessage("netExhaustThrust_t is null");
                    return;
                }
                getNetExhaustThrust = DynamicMethodFactory.CreateGetField<object, float>(netExhaustThrust_t);

                FieldInfo netMeredithEffect_t = ajePropellerAPI_t.GetField("netMeredithEffect", BindingFlags.Instance | BindingFlags.Public);
                if (netMeredithEffect_t == null)
                {
                    Utility.LogErrorMessage("netMeredithEffect_t is null");
                    return;
                }
                getNetMeredithEffect = DynamicMethodFactory.CreateGetField<object, float>(netMeredithEffect_t);

                FieldInfo brakeShaftPower_t = ajePropellerAPI_t.GetField("brakeShaftPower", BindingFlags.Instance | BindingFlags.Public);
                if (brakeShaftPower_t == null)
                {
                    Utility.LogErrorMessage("brakeShaftPower_t is null");
                    return;
                }
                getBrakeShaftPower = DynamicMethodFactory.CreateGetField<object, float>(brakeShaftPower_t);


                FieldInfo boost_t = ajePropellerAPI_t.GetField("boost", BindingFlags.Instance | BindingFlags.Public);
                if (boost_t == null)
                {
                    Utility.LogErrorMessage("boost_t is null");
                    return;
                }
                getBoost = DynamicMethodFactory.CreateGetField<object, float>(boost_t);
                setBoost = DynamicMethodFactory.CreateSetField<object, float>(boost_t);

                FieldInfo rpmLever_t = ajePropellerAPI_t.GetField("rpmLever", BindingFlags.Instance | BindingFlags.Public);
                if (rpmLever_t == null)
                {
                    Utility.LogErrorMessage("rpmLever_t is null");
                    return;
                }
                getRpmLever = DynamicMethodFactory.CreateGetField<object, float>(rpmLever_t);
                setRpmLever = DynamicMethodFactory.CreateSetField<object, float>(rpmLever_t);

                FieldInfo mixture_t = ajePropellerAPI_t.GetField("mixture", BindingFlags.Instance | BindingFlags.Public);
                if (mixture_t == null)
                {
                    Utility.LogErrorMessage("mixture_t is null");
                    return;
                }
                getMixture = DynamicMethodFactory.CreateGetField<object, float>(mixture_t);
                setMixture = DynamicMethodFactory.CreateSetField<object, float>(mixture_t);

                FieldInfo coreThrottle_t = ajeJetAPI_t.GetField("actualCoreThrottle", BindingFlags.Instance | BindingFlags.Public);
                if (coreThrottle_t == null)
                {
                    Utility.LogErrorMessage("coreThrottle_t is null");
                    return;
                }
                getCoreThrottle = DynamicMethodFactory.CreateGetField<object, float>(coreThrottle_t);

                FieldInfo afterburnerThrottle_t = ajeJetAPI_t.GetField("actualABThrottle", BindingFlags.Instance | BindingFlags.Public);
                if (afterburnerThrottle_t == null)
                {
                    Utility.LogErrorMessage("afterburnerThrottle_t is null");
                    return;
                }
                getAfterburnerThrottle = DynamicMethodFactory.CreateGetField<object, float>(afterburnerThrottle_t);

                FieldInfo maxEngineTemp_t = ajeJetAPI_t.GetField("maxEngineTemp", BindingFlags.Instance | BindingFlags.Public);
                if (maxEngineTemp_t == null)
                {
                    Utility.LogErrorMessage("maxEngineTemp_t is null");
                    return;
                }
                getMaxJetTemp = DynamicMethodFactory.CreateGetField<object, double>(maxEngineTemp_t);

                FieldInfo currentEngineTemp_t = ajeJetAPI_t.GetField("engineTempString", BindingFlags.Instance | BindingFlags.Public);
                if (currentEngineTemp_t == null)
                {
                    Utility.LogErrorMessage("currentEngineTemp_t is null");
                    return;
                }
                getCurrentJetTemp = DynamicMethodFactory.CreateGetField<object, string>(currentEngineTemp_t);

                ajeInstalled = true;
            }
        }
    }
}
