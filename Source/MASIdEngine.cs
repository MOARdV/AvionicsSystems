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
    /// </summary>
    class MASIdEngine : MASIdGeneric
    {
        //--- Reflection info for Advanced Jet Engine
        private static readonly bool ajeInstalled = false;
        internal static readonly Type ajePropellerAPI_t;
        // All fields are floats
        private static readonly Func<object, float> getPropRPM;
        private static readonly FieldInfo propPitch_t;
        private static readonly FieldInfo propThrust_t;
        private static readonly FieldInfo manifoldPressure_t;
        private static readonly FieldInfo chargeAirTemp_t;
        private static readonly FieldInfo netExhaustThrust_t;
        private static readonly FieldInfo netMeredithEffect_t;
        private static readonly FieldInfo brakeShaftPower_t;
        // read-write fields:
        private static readonly FieldInfo boost_t;
        private static readonly Func<object, float> getRpmLever;
        private static readonly Action<object, float> setRpmLever;
        //private static readonly FieldInfo rpmLever_t;
        private static readonly FieldInfo mixture_t;

        //--- Tracked fields
        private PartModule ajeModule;
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

                    ajeModule = part.Modules["ModuleEnginesAJEPropeller"];
                    if (ajeModule == null)
                    {
                        Utility.LogErrorMessage(this, "Didn't find any AJE engine");
                    }
                    else
                    {
                        Utility.LogMessage(this, "Found ModuleEnginesAJEPropeller");
                    }
                }

                // If no tracked modules are installed, we don't want this part to register
                // with the vessel computer.
                if (ajeModule == null)
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

        #region AJE Propellers
        internal float GetPropellerRPM()
        {
            if (ajeModule != null)
            {
                return getPropRPM(ajeModule);
            }

            return 0.0f;
        }

        internal float GetPropellerRPMLever()
        {
            if (ajeModule != null)
            {
                return getRpmLever(ajeModule);
            }

            return 0.0f;
        }

        internal bool SetPropellerRPMLever(float newRpm)
        {
            if (ajeModule != null)
            {
                setRpmLever(ajeModule, Mathf.Clamp01(newRpm));
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

            if (ajePropellerAPI_t != null)
            {
                Utility.LogMessage(ajePropellerAPI_t, "MASIdEngine found AJE");

                FieldInfo propRPM_t = ajePropellerAPI_t.GetField("propRPM", BindingFlags.Instance | BindingFlags.Public);
                if (propRPM_t == null)
                {
                    Utility.LogErrorMessage("propRPM_t is null");
                    return;
                }
                getPropRPM = DynamicMethodFactory.CreateGetField<object, float>(propRPM_t);

                propPitch_t = ajePropellerAPI_t.GetField("propPitch", BindingFlags.Instance | BindingFlags.Public);
                if (propPitch_t == null)
                {
                    Utility.LogErrorMessage("propPitch_t is null");
                    return;
                }

                propThrust_t = ajePropellerAPI_t.GetField("propThrust", BindingFlags.Instance | BindingFlags.Public);
                if (propThrust_t == null)
                {
                    Utility.LogErrorMessage("propThrust_t is null");
                    return;
                }

                manifoldPressure_t = ajePropellerAPI_t.GetField("manifoldPressure", BindingFlags.Instance | BindingFlags.Public);
                if (manifoldPressure_t == null)
                {
                    Utility.LogErrorMessage("manifoldPressure_t is null");
                    return;
                }

                chargeAirTemp_t = ajePropellerAPI_t.GetField("chargeAirTemp", BindingFlags.Instance | BindingFlags.Public);
                if (chargeAirTemp_t == null)
                {
                    Utility.LogErrorMessage("chargeAirTemp_t is null");
                    return;
                }

                netExhaustThrust_t = ajePropellerAPI_t.GetField("netExhaustThrust", BindingFlags.Instance | BindingFlags.Public);
                if (netExhaustThrust_t == null)
                {
                    Utility.LogErrorMessage("netExhaustThrust_t is null");
                    return;
                }

                netMeredithEffect_t = ajePropellerAPI_t.GetField("netMeredithEffect", BindingFlags.Instance | BindingFlags.Public);
                if (netMeredithEffect_t == null)
                {
                    Utility.LogErrorMessage("netMeredithEffect_t is null");
                    return;
                }

                brakeShaftPower_t = ajePropellerAPI_t.GetField("brakeShaftPower", BindingFlags.Instance | BindingFlags.Public);
                if (brakeShaftPower_t == null)
                {
                    Utility.LogErrorMessage("brakeShaftPower_t is null");
                    return;
                }

                boost_t = ajePropellerAPI_t.GetField("boost", BindingFlags.Instance | BindingFlags.Public);
                if (boost_t == null)
                {
                    Utility.LogErrorMessage("boost_t is null");
                    return;
                }

                FieldInfo rpmLever_t = ajePropellerAPI_t.GetField("rpmLever", BindingFlags.Instance | BindingFlags.Public);
                if (rpmLever_t == null)
                {
                    Utility.LogErrorMessage("rpmLever_t is null");
                    return;
                }
                getRpmLever = DynamicMethodFactory.CreateGetField<object, float>(rpmLever_t);
                setRpmLever = DynamicMethodFactory.CreateSetField<object, float>(rpmLever_t);

                mixture_t = ajePropellerAPI_t.GetField("mixture", BindingFlags.Instance | BindingFlags.Public);
                if (mixture_t == null)
                {
                    Utility.LogErrorMessage("mixture_t is null");
                    return;
                }

                ajeInstalled = true;
            }
        }
    }
}
