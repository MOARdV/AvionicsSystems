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
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

// TODO:
// 
// Research Attributes - can I come up with attributes for methods to automate
// which ones can be cached and which ones are variable?
//
// Store propID as a global, updated by each caller.
namespace AvionicsSystems
{
    /// <summary>
    /// The MASFlightComputer manages the components attached to props inside
    /// the part this computer is attached to.  It is a central clearing-house
    /// for the data requsted by the props, and it manages data that persists
    /// across game sessions.
    /// </summary>
    public partial class MASFlightComputer : PartModule
    {
        /// <summary>
        /// ID that is stored automatically on-save by KSP.  This value is the
        /// string version of fcId, so on load we can restore the Guid.
        /// </summary>
        [KSPField(isPersistant = true)]
        public string flightComputerId = string.Empty;

        /// <summary>
        /// The maximum g-loading the command pod can sustain without disrupting
        /// power.
        /// </summary>
        [KSPField]
        public float gLimit = float.MaxValue;

        /// <summary>
        /// The chance that power is disrupted at (gLimit + 1) Gs.  This number
        /// scales with G forces.  A 1 represents 100% chance of a power disruption.
        /// </summary>
        [KSPField]
        public float baseDisruptionChance = 0.0f;
        internal float disruptionChance = 0.0f;

        /// <summary>
        /// Does the command pod's instruments require power to function?
        /// </summary>
        [KSPField]
        public bool requiresPower = false;
        internal bool isPowered = true;

        /// <summary>
        /// Our module ID (so each FC can be distinguished in a save file).
        /// </summary>
        private Guid fcId = Guid.Empty;

        /// <summary>
        /// ID of our parent vessel (for some sanity checking)
        /// </summary>
        private Guid parentVesselId = Guid.Empty;

        /// <summary>
        /// This flight computer's Lua context
        /// </summary>
        private Script script;

        /// <summary>
        /// Instance of the flight computer proxy used in the Lua context.
        /// </summary>
        private MASFlightComputerProxy fcProxy;

        /// <summary>
        /// Instance of the Chatterer proxy class.
        /// </summary>
        private MASIChatterer chattererProxy;

        /// <summary>
        /// Instance of the FAR proxy class.
        /// </summary>
        private MASIFAR farProxy;

        /// <summary>
        /// Instance of the Kerbal Alarm Clock proxy class.
        /// </summary>
        private MASIKAC kacProxy;

        /// <summary>
        /// Instance of the MechJeb proxy class.
        /// </summary>
        private MASIMechJeb mjProxy;

        /// <summary>
        /// Instance of the Navigation proxy class.
        /// </summary>
        private MASINavigation navProxy;

        /// <summary>
        /// Instance of the RealChute proxy class.
        /// </summary>
        private MASIRealChute realChuteProxy;

        /// <summary>
        /// Have we initialized?
        /// </summary>
        private bool initialized = false;

        /// <summary>
        /// Has there been an addition or subtraction to the mutable variables list?
        /// </summary>
        private bool mutableVariablesChanged = false;

        /// <summary>
        /// Dictionary of known RasterPropComputer-compatible named colors.
        /// </summary>
        private Dictionary<string, Color32> namedColors = new Dictionary<string, Color32>();

        /// <summary>
        /// Dictionary of loaded Action Lua wrappers.
        /// </summary>
        private Dictionary<string, Action> actions = new Dictionary<string, Action>();

        /// <summary>
        /// Reference to the current vessel computer.
        /// </summary>
        internal MASVesselComputer vc;

        internal ProtoCrewMember[] localCrew = new ProtoCrewMember[0];
        internal kerbalExpressionSystem[] localCrewMedical = new kerbalExpressionSystem[0];


        private Stopwatch stopwatch = new Stopwatch();
        long samplecount = 0;

        #region Internal Interface
        /// <summary>
        /// Return the MASFlightComputer attached to the given part, or null if
        /// none is found.
        /// </summary>
        /// <param name="part">The part where the flight computer should live</param>
        /// <returns>MASFlightComputer or null</returns>
        static internal MASFlightComputer Instance(Part part)
        {
            for (int i = part.Modules.Count - 1; i >= 0; --i)
            {
                if (part.Modules[i].ClassName == typeof(MASFlightComputer).Name)
                {
                    return part.Modules[i] as MASFlightComputer;
                }
            }

            return null;
        }

        /// <summary>
        /// Convert a name to a 
        /// </summary>
        /// <param name="initialName"></param>
        /// <param name="prop">Prop that is associated with the variable request; 
        /// null indicates "Do not condition this variable"</param>
        /// <returns></returns>
        private string ConditionVariableName(string initialName, InternalProp prop)
        {
            initialName = initialName.Trim();
            double numeric;
            if (double.TryParse(initialName, out numeric))
            {
                // If the variable is a numeric constant, we want to
                // canonicalize it so we don't create multiple variables that
                // all have the same value, eg "0" and "0.0" and "0.00"
                initialName = string.Format("{0:R}", numeric);
            }

            if (prop == null)
            {
                return initialName;
            }
            else
            {
                if (initialName.Contains("%AUTOID%"))
                {
                    string replacementString = string.Format("PROP-{0}-{1}", prop.propName, prop.propID);
                    initialName = initialName.Replace("%AUTOID%", replacementString);
                }
                if (initialName.Contains("%PROPID%"))
                {
                    initialName = initialName.Replace("%PROPID%", prop.propID.ToString());
                }
                return initialName;
            }
        }

        /// <summary>
        /// Register an object to receive on-changed callback notification for
        /// a variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="callback"></param>
        internal void RegisterNumericVariable(string variableName, InternalProp prop, Action<double> callback)
        {
            Variable v = GetVariable(variableName, prop);

            if (v.mutable)
            {
                v.numericCallbacks += callback;
            }

            callback(v.SafeValue());
        }

        /// <summary>
        /// Unregister an object from receiving callback notifications.
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="callback"></param>
        internal void UnregisterNumericVariable(string variableName, InternalProp prop, Action<double> callback)
        {
            variableName = ConditionVariableName(variableName, prop);
            if (canonicalVariableName.ContainsKey(variableName))
            {
                variables[canonicalVariableName[variableName]].numericCallbacks -= callback;
            }
        }

        /// <summary>
        /// Register a callback to notify the recipient that the variable has changed.
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        internal Variable RegisterOnVariableChange(string variableName, InternalProp prop, Action callback)
        {
            Variable v = GetVariable(variableName, prop);

            if (v.mutable)
            {
                v.changeCallbacks += callback;
            }

            return v;
        }

        /// <summary>
        /// Unregister an on-change callback.
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="callback"></param>
        internal void UnregisterOnVariableChange(string variableName, InternalProp prop, Action callback)
        {
            variableName = ConditionVariableName(variableName, prop);
            if (canonicalVariableName.ContainsKey(variableName))
            {
                variables[canonicalVariableName[variableName]].changeCallbacks -= callback;
            }
        }

        /// <summary>
        /// Converts an RPM-compatible named color (COLOR_*) to a Color32.  If
        /// a part-local override exists, it will be chosen; otherwise, a check
        /// of the global table is made.  If the named color is not found, an
        /// exception is thrown.
        /// </summary>
        /// <param name="namedColor"></param>
        /// <returns></returns>
        internal Color32 GetNamedColor(string namedColor)
        {
            if (namedColors.ContainsKey(namedColor))
            {
                return namedColors[namedColor];
            }
            else
            {
                if (MASLoader.namedColors.ContainsKey(namedColor))
                {
                    Color32 newColor = MASLoader.namedColors[namedColor];
                    namedColors.Add(namedColor, newColor);
                    return newColor;
                }
            }

            Utility.ComplainLoudly("GetNamedColor with unknown named color");
            throw new ArgumentException("[MASFlightComputer] Unknown named color '" + namedColor + "'.");
        }

        /// <summary>
        /// Returns or generates an Action that encapsulates the supplied actionName,
        /// which is simply the Lua function(s) that are to be executed.
        /// </summary>
        /// <param name="actionName"></param>
        /// <returns></returns>
        internal Action GetAction(string actionName, InternalProp prop)
        {
            // TODO: Lexer / parsing on this.
            actionName = ConditionVariableName(actionName, prop);

            //var result = CodeGen.Parser.TryParse(actionName);
            //if (result.type != CodeGen.Parser.ResultType.ERROR)
            //{
            //    Utility.LogMessage(this, "IN : {0}", actionName);
            //    Utility.LogMessage(this, "OUT: {0}", result.canonicalSource);
            //}

            if (actions.ContainsKey(actionName))
            {
                return actions[actionName];
            }
            else
            {
                try
                {
                    DynValue dv = script.LoadString(actionName);

                    Action a = () =>
                    {
                        try
                        {
                            script.Call(dv);
                        }
                        catch (Exception e)
                        {
                            Utility.ComplainLoudly("Action " + actionName + " triggered an exception");
                            Utility.LogErrorMessage(this, "Action {0} triggered exception:", actionName);
                            Utility.LogErrorMessage(this, e.ToString());
                        }
                    };

                    Utility.LogMessage(this, "Adding new Action '{0}'", actionName);
                    actions.Add(actionName, a);
                    return a;
                }
                catch
                {
                    return null;
                }
            }
        }
        #endregion

        #region Monobehaviour
        /// <summary>
        /// Process updates to tracked variables.
        /// </summary>
        public void FixedUpdate()
        {
            try
            {
                if (vc.vesselActive && initialized)
                {
                    // Realistically, this block of code won't be triggered very
                    // often.
                    if (mutableVariablesChanged)
                    {
                        mutableVariables = mutableVariablesList.ToArray();
                        mutableVariablesChanged = false;
                    }

                    fcProxy.Update();
                    farProxy.Update();
                    kacProxy.Update();
                    mjProxy.Update();
                    navProxy.Update();
                    realChuteProxy.Update();

                    // Precompute the disruption effects.
                    // TODO: Don't do the string lookup every FixedUpdate...
                    isPowered = (!requiresPower || vc.ResourceCurrent(MASLoader.ElectricCharge) > 0.0001);

                    if (vessel.geeForce_immediate > gLimit)
                    {
                        disruptionChance = baseDisruptionChance * Mathf.Sqrt((float)vessel.geeForce_immediate - gLimit);
                    }
                    else
                    {
                        disruptionChance = 0.0f;
                    }

                    // Crew medical seems to get nulled somewhere after the
                    // crew callback, so it appears I need to repeatedly poll
                    // it.
                    int numSeats = localCrew.Length;
                    if (numSeats > 0)
                    {
                        for (int i = 0; i < numSeats; i++)
                        {
                            if (localCrew[i] != null)
                            {
                                kerbalExpressionSystem kES = localCrewMedical[i];
                                localCrew[i].KerbalRef.GetComponentCached<kerbalExpressionSystem>(ref kES);
                                localCrewMedical[i] = kES;
                            }
                            else
                            {
                                localCrewMedical[i] = null;
                            }
                        }
                    }

                    // TODO: Add a heuristic to adjust the loop so not all variables
                    // update every fixed update if the average update time is too high.
                    // Need to decide if it's going to be an absolute time (max # ms/update)
                    // or a relative time.
                    // NOTE: 128 "variables" average about 1.7ms/update!  And there's
                    // a LOT of garbage collection going on.
                    // 229 variables -> 2.6-2.7ms/update, so 70-80 variables per ms on
                    // a reasonable mid-upper range CPU (3.6GHz).
                    stopwatch.Start();
                    int count = mutableVariables.Length;
                    for (int i = 0; i < count; ++i)
                    {
                        try
                        {
                            mutableVariables[i].Evaluate(script);
                        }
                        catch (Exception e)
                        {
                            Utility.LogErrorMessage(this, "FixedUpdate exception on variable {0}", mutableVariables[i].name);
                            throw e;
                        }
                    }
                    stopwatch.Stop();
                    ++samplecount;
                }
            }
            catch (Exception e)
            {
                Utility.LogErrorMessage(this, "MASFlightComputer.FixedUpdate exception: {0}", e);
            }
        }

        /// <summary>
        /// Shut down, unregister, etc.
        /// </summary>
        public void OnDestroy()
        {
            script = null;
            fcProxy = null;
            chattererProxy = null;
            mjProxy = null;
            navProxy = null;
            farProxy = null;
            kacProxy = null;
            realChuteProxy = null;
            if (initialized)
            {
                Utility.LogMessage(this, "OnDestroy for {0}", flightComputerId);
                GameEvents.onVesselWasModified.Remove(onVesselChanged);
                GameEvents.onVesselChange.Remove(onVesselChanged);
                GameEvents.onVesselCrewWasModified.Remove(onVesselChanged);

                Utility.LogMessage(this, "{3} variables created: {0} constant variables, {1} native variables, and {2} Lua variables",
                    constantVariableCount, nativeVariableCount, luaVariableCount, variables.Count);
                double msPerFixedUpdate = 1000.0 * (double)(stopwatch.ElapsedTicks) / (double)(samplecount * Stopwatch.Frequency);
                Utility.LogMessage(this, "MoonSharp time average = {0:0.00}ms/FixedUpdate or {1:0.0} variables/ms", msPerFixedUpdate, (double)mutableVariables.Length / msPerFixedUpdate);
            }
        }

        /// <summary>
        /// Start up this module.
        /// </summary>
        public void Start()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                Vessel vessel = this.vessel;

                if (string.IsNullOrEmpty(flightComputerId))
                {
                    fcId = Guid.NewGuid();
                    flightComputerId = fcId.ToString();

                    Utility.LogMessage(this, "Creating new flight computer {0}", flightComputerId);
                }
                else
                {
                    fcId = new Guid(flightComputerId);

                    Utility.LogMessage(this, "Restoring flight computer {0}", flightComputerId);
                }

                parentVesselId = vessel.id;

                // TODO: Review the CoreModule settings - are they tight enough?
                script = new Script(CoreModules.Preset_HardSandbox);

                UserData.DefaultAccessMode = InteropAccessMode.Preoptimized;

                // Global State (set up links to the proxy).  Note that we
                // don't use the proxy system included in MoonSharp, since it
                // creates a proxy object for every single script.Call(), which
                // means plenty of garbage...
                try
                {
                    chattererProxy = new MASIChatterer();
                    UserData.RegisterType<MASIChatterer>();
                    script.Globals["chatterer"] = chattererProxy;

                    farProxy = new MASIFAR(vessel);
                    UserData.RegisterType<MASIFAR>();
                    script.Globals["far"] = farProxy;

                    kacProxy = new MASIKAC(vessel);
                    UserData.RegisterType<MASIKAC>();
                    script.Globals["kac"] = kacProxy;

                    mjProxy = new MASIMechJeb();
                    UserData.RegisterType<MASIMechJeb>();
                    script.Globals["mechjeb"] = mjProxy;

                    navProxy = new MASINavigation(vessel);
                    UserData.RegisterType<MASINavigation>();
                    script.Globals["nav"] = navProxy;

                    realChuteProxy = new MASIRealChute(vessel);
                    UserData.RegisterType<MASIRealChute>();
                    script.Globals["realchute"] = realChuteProxy;

                    fcProxy = new MASFlightComputerProxy(this, farProxy, mjProxy);
                    UserData.RegisterType<MASFlightComputerProxy>();
                    script.Globals["fc"] = fcProxy;

                    UserData.RegisterType<MASVector2>();
                }
                catch (Exception e)
                {
                    Utility.LogErrorMessage(this, "Proxy object configuration failed:");
                    Utility.LogErrorMessage(this, e.ToString());
                    Utility.ComplainLoudly("Initialization Failed.  Please check KSP.log");
                }
                vc = MASVesselComputer.Instance(vessel);
                vc.RestorePersistentData(this);

                // TODO: Don't need to set vessel for all of these guys if I just now init'd them.
                fcProxy.vc = vc;
                fcProxy.vessel = vessel;
                //farProxy.vessel = vessel;
                //kacProxy.vessel = vessel;
                mjProxy.UpdateVessel(vessel, vc);
                realChuteProxy.vc = vc;
                //realChuteProxy.vessel = vessel;


                // TODO: Add MAS script

                // Add User scripts
                try
                {
                    for (int i = MASLoader.userScripts.Count - 1; i >= 0; --i)
                    {
                        script.DoString(MASLoader.userScripts[i]);
                    }

                    Utility.LogMessage(this, "{1}: Loaded {0} user scripts", MASLoader.userScripts.Count, flightComputerId);
                }
                catch (Exception e)
                {
                    Utility.ComplainLoudly("User Script Loading error");
                    Utility.LogErrorMessage(this, e.ToString());
                }

                // Initialize persistent vars ... note that save game values have
                // already been restored, so check if the persistent already exists,
                // first.
                // Also restore named color per-part overrides
                try
                {
                    ConfigNode myNode = Utility.GetPartModuleConfigNode(part, typeof(MASFlightComputer).Name);
                    ConfigNode persistentSeed = myNode.GetNode("PERSISTENT_VARIABLES");
                    if (persistentSeed != null)
                    {
                        var values = persistentSeed.values;
                        int valueCount = values.Count;
                        for (int i = 0; i < valueCount; ++i)
                        {
                            var persistentVal = values[i];

                            if (!persistentVars.ContainsKey(persistentVal.name))
                            {
                                double doubleVal;
                                if (double.TryParse(persistentVal.value, out doubleVal))
                                {
                                    persistentVars[persistentVal.name] = doubleVal;
                                }
                                else
                                {
                                    persistentVars[persistentVal.name] = persistentVal.value;
                                }
                            }
                        }
                    }

                    ConfigNode overrideColorNodes = myNode.GetNode("RPM_COLOROVERRIDE");
                    ConfigNode[] colorConfig = overrideColorNodes.GetNodes("COLORDEFINITION");
                    for (int defIdx = 0; defIdx < colorConfig.Length; ++defIdx)
                    {
                        if (colorConfig[defIdx].HasValue("name") && colorConfig[defIdx].HasValue("color"))
                        {
                            string name = "COLOR_" + (colorConfig[defIdx].GetValue("name").Trim());
                            Color32 color = ConfigNode.ParseColor32(colorConfig[defIdx].GetValue("color").Trim());
                            namedColors[name] = color;
                        }
                    }
                }
                catch
                {

                }

                UpdateLocalCrew();

                GameEvents.onVesselWasModified.Add(onVesselChanged);
                GameEvents.onVesselChange.Add(onVesselChanged);
                GameEvents.onVesselCrewWasModified.Add(onVesselChanged);

                initialized = true;
            }
        }
        #endregion

        #region Private Methods
        private void UpdateLocalCrew()
        {
            // part.internalModel may be null if the craft is loaded but isn't the active/IVA craft
            if (part.internalModel != null)
            {
                int seatCount = part.internalModel.seats.Count;
                if (seatCount != localCrew.Length)
                {
                    // This can happen when an internalModel is loaded when
                    // it wasn't previously, which appears to occur on docking
                    // for instance.
                    localCrew = new ProtoCrewMember[seatCount];
                    localCrewMedical = new kerbalExpressionSystem[seatCount];
                }

                // Note that we set localCrewMedical to null because the
                // crewMedical ends up being going null sometime between
                // when the crew changed callback fires and when we start
                // checking variables.  Thus, we still have to poll the
                // crew medical.
                for (int i = 0; i < seatCount; i++)
                {
                    localCrew[i] = part.internalModel.seats[i].crew;
                    localCrewMedical[i] = null;
                }
            }
            else if(localCrew.Length > 0)
            {
                localCrew = new ProtoCrewMember[0];
                localCrewMedical = new kerbalExpressionSystem[0];
            }
        }
        #endregion

        #region GameEvent Callbacks
        /// <summary>
        /// General-purpose callback to make sure we refresh our data when
        /// something changes.
        /// </summary>
        /// <param name="who">The Vessel being changed</param>
        private void onVesselChanged(Vessel who)
        {
            if (who.id == this.vessel.id)
            {
                // TODO: Do something different if parentVesselID != vessel.id?
                Vessel vessel = this.vessel;
                parentVesselId = vessel.id;
                vc = MASVesselComputer.Instance(vessel);
                fcProxy.vc = vc;
                fcProxy.vessel = vessel;
                chattererProxy.UpdateVessel();
                farProxy.vessel = vessel;
                kacProxy.vessel = vessel;
                mjProxy.UpdateVessel(vessel, vc);
                navProxy.UpdateVessel(vessel);
                realChuteProxy.vc = vc;
                realChuteProxy.vessel = vessel;
                UpdateLocalCrew();
            }
        }
        #endregion
    }
}
