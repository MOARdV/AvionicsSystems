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
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

// TODO:
// 
// Research Attributes - can I come up with attributes for methods to automate
// which ones can be cached and which ones are variable?
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
        /// Ship's description string copied from the editor.  At flight time, we
        /// will parse this to formulate the action group fields (equivalent to
        /// AGMEMO in RPM).
        /// </summary>
        [KSPField(isPersistant = true)]
        public string shipDescription = string.Empty;
        internal string[] agMemoOff = { "AG0", "AG1", "AG2", "AG3", "AG4", "AG5", "AG6", "AG7", "AG8", "AG9" };
        internal string[] agMemoOn = { "AG0", "AG1", "AG2", "AG3", "AG4", "AG5", "AG6", "AG7", "AG8", "AG9" };

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

        [KSPField]
        public string powerOnVariable = string.Empty;
        internal bool powerOnValid = true;

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
        /// Instance of the aircraft engines proxy class.
        /// </summary>
        private MASIEngine engineProxy;

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
        /// Instance of the parachute proxy class.
        /// </summary>
        private MASIParachute parachuteProxy;

        /// <summary>
        /// Instance of the Transfer proxy class.
        /// </summary>
        private MASITransfer transferProxy;

        /// <summary>
        /// Have we initialized?
        /// </summary>
        internal bool initialized { get; private set; }

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
        /// Dictionary of named monitors, for routing softkeys.
        /// </summary>
        private Dictionary<string, MASMonitor> monitors = new Dictionary<string, MASMonitor>();

        /// <summary>
        /// Reference to the current vessel computer.
        /// </summary>
        internal MASVesselComputer vc;

        private int electricChargeIndex = -1;

        internal static readonly string vesselIdLabel = "__vesselId";
        internal static readonly string vesselFilterLabel = "__vesselFilter";

        internal int vesselFilterValue = 0;
        internal List<VesselType> activeVesselFilter = new List<VesselType>();
        private readonly Dictionary<int, VesselType> vesselBitRemap = new Dictionary<int, VesselType>
        {
            { 1, VesselType.Ship},
            { 2, VesselType.Plane},
            { 3, VesselType.Probe},
            { 4, VesselType.Lander},
            { 5, VesselType.Station},
            { 6, VesselType.Relay},
            { 7, VesselType.Rover},
            { 8, VesselType.Base},
            { 9, VesselType.EVA},
            { 10, VesselType.Flag},
            { 11, VesselType.Debris},
            { 12, VesselType.SpaceObject},
            { 13, VesselType.Unknown}
        };
        private readonly Dictionary<VesselType, int> bitVesselRemap = new Dictionary<VesselType, int>
        {
            { VesselType.Ship, 1},
            { VesselType.Plane, 2},
            { VesselType.Probe, 3},
            { VesselType.Lander, 4},
            { VesselType.Station, 5},
            { VesselType.Relay, 6},
            { VesselType.Rover, 7},
            { VesselType.Base, 8},
            { VesselType.EVA, 9},
            { VesselType.Flag, 10},
            { VesselType.Debris, 11},
            { VesselType.SpaceObject, 12},
            { VesselType.Unknown, 13}
        };

        internal ProtoCrewMember[] localCrew = new ProtoCrewMember[0];
        private kerbalExpressionSystem[] localCrewMedical = new kerbalExpressionSystem[0];


        private Stopwatch nativeStopwatch = new Stopwatch();
        private Stopwatch luaStopwatch = new Stopwatch();
        long samplecount = 0;
        long nativeVariablesCount = 0;
        long luaVariablesCount = 0;

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
            try
            {
                Variable v = GetVariable(variableName, prop);

                if (v.mutable)
                {
                    v.numericCallbacks += callback;
                }

                callback(v.SafeValue());
            }
            catch (Exception e)
            {
                throw new ArgumentException("Error parsing variable \"" + variableName + "\"", e);
            }
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
            else
            {
                Utility.LogErrorMessage(this, "UnregisterNumericVariable: Did not find {0}", variableName);
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
        /// Register a monitor so it will receive softkey events.  If the monitor's name
        /// already exists, this is a no-op.
        /// </summary>
        /// <param name="monitorName">Name of the monitor</param>
        /// <param name="monitor"></param>
        internal void RegisterMonitor(string monitorName, InternalProp prop, MASMonitor monitor)
        {
            monitorName = ConditionVariableName(monitorName, prop);

            if (!monitors.ContainsKey(monitorName))
            {
                monitors[monitorName] = monitor;
            }
        }

        /// <summary>
        /// Unregister a monitor so it will no longer receive softkey events.
        /// </summary>
        /// <param name="monitorName"></param>
        /// <param name="monitor"></param>
        internal void UnregisterMonitor(string monitorName, InternalProp prop, MASMonitor monitor)
        {
            monitorName = ConditionVariableName(monitorName, prop);

            if (monitors.ContainsKey(monitorName))
            {
                monitors.Remove(monitorName);
            }
        }

        /// <summary>
        /// Handle a softkey event by forwarding it to the named monitor (if it exists).
        /// </summary>
        /// <param name="monitorName"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        internal bool HandleSoftkey(string monitorName, int key)
        {
            MASMonitor monitor;
            if (monitors.TryGetValue(monitorName, out monitor))
            {
                return monitor.HandleSoftkey(key);
            }

            return false;
        }

        /// <summary>
        /// Convert an RPM-compatible named color to a Color32.  Returns true
        /// if successful.
        /// </summary>
        /// <param name="namedColor">Color to convert</param>
        /// <returns>True if the namedColor was a COLOR_ field, false otherwise.</returns>
        internal bool TryGetNamedColor(string namedColor, out Color32 color)
        {
            namedColor = namedColor.Trim();
            if (namedColor.StartsWith("COLOR_"))
            {
                if (namedColors.TryGetValue(namedColor, out color))
                {
                    return true;
                }
                else if (MASLoader.namedColors.TryGetValue(namedColor, out color))
                {
                    namedColors.Add(namedColor, color);
                    return true;
                }
            }

            color = Color.black;
            return false;
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
            Color32 colorValue;
            if (namedColors.TryGetValue(namedColor, out colorValue))
            {
                return colorValue;
            }
            else
            {
                if (MASLoader.namedColors.TryGetValue(namedColor, out colorValue))
                {
                    namedColors.Add(namedColor, colorValue);
                    return colorValue;
                }
            }

            Utility.ComplainLoudly("GetNamedColor with unknown named color");
            throw new ArgumentException("[MASFlightComputer] Unknown named color '" + namedColor + "'.");
        }

        /// <summary>
        /// Returns or generates an Action that encapsulates the supplied actionName,
        /// which is simply the Lua function(s) that are to be executed.
        /// </summary>
        /// <param name="actionName">The action (Lua code snippet) to execute.</param>
        /// <returns>The Action.</returns>
        internal Action GetAction(string actionName, InternalProp prop)
        {
            actionName = ConditionVariableName(actionName, prop);

            Action action;
            if (actions.TryGetValue(actionName, out action))
            {
                return action;
            }
            else
            {
                try
                {
                    DynValue dv = script.LoadString(actionName);

                    action = () =>
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

                    //Utility.LogMessage(this, "Adding new Action '{0}'", actionName);
                    actions.Add(actionName, action);
                    return action;
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Fetch the kerbalExpressionSystem for a local crew seat.
        /// </summary>
        /// <param name="crewSeat"></param>
        /// <returns></returns>
        internal kerbalExpressionSystem GetLocalKES(int crewSeat)
        {
            if (crewSeat >= 0 && crewSeat < localCrew.Length && localCrew[crewSeat] != null)
            {
                localCrew[crewSeat].KerbalRef.GetComponentCached<kerbalExpressionSystem>(ref localCrewMedical[crewSeat]);

                return localCrewMedical[crewSeat];
            }

            return null;
        }
        #endregion

        #region Target Tracking
        internal bool ClearTargetFilter(int bitIndex)
        {
            int bit = 1 << bitIndex;
            int oldValue = vesselFilterValue;
            vesselFilterValue &= ~bit;

            if (oldValue != vesselFilterValue)
            {
                activeVesselFilter.Remove(vesselBitRemap[bitIndex]);
                SetPersistent(vesselFilterLabel, vesselFilterValue.ToString("X"));
                return true;
            }
            else
            {
                return false;
            }
        }

        internal bool GetTargetFilter(int bitIndex)
        {
            int bit = 1 << bitIndex;
            int andResult = bit & vesselFilterValue;

            return (andResult != 0);
        }

        internal bool SetTargetFilter(int bitIndex)
        {
            int bit = 1 << bitIndex;
            int oldValue = vesselFilterValue;
            vesselFilterValue |= bit;

            if (oldValue != vesselFilterValue)
            {
                activeVesselFilter.Add(vesselBitRemap[bitIndex]);
                SetPersistent(vesselFilterLabel, vesselFilterValue.ToString("X"));
                return true;
            }
            else
            {
                return false;
            }
        }

        internal void ToggleTargetFilter(int bitIndex)
        {
            int bit = 1 << bitIndex;
            if ((vesselFilterValue & bit) != 0)
            {
                activeVesselFilter.Remove(vesselBitRemap[bitIndex]);
            }
            else
            {
                activeVesselFilter.Add(vesselBitRemap[bitIndex]);
            }
            vesselFilterValue ^= bit;
            SetPersistent(vesselFilterLabel, vesselFilterValue.ToString("X"));
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
                if (initialized && vc.vesselCrewed && vc.vesselActive)
                {
                    // Realistically, this block of code won't be triggered very
                    // often.  Once per scene change per pod.
                    if (mutableVariablesChanged)
                    {
                        nativeVariables = new Variable[nativeVariableCount];
                        luaVariables = new Variable[luaVariableCount];
                        int nativeIdx = 0, luaIdx = 0;
                        foreach (Variable var in mutableVariablesList)
                        {
                            if (var.variableType == Variable.VariableType.LuaScript || var.variableType == Variable.VariableType.LuaClosure)
                            {
                                luaVariables[luaIdx] = var;
                                ++luaIdx;
                            }
                            else if (var.variableType == Variable.VariableType.Func)
                            {
                                nativeVariables[nativeIdx] = var;
                                ++nativeIdx;
                            }
                            else
                            {
                                throw new ArgumentException(string.Format("Unexpected variable type {0} for variable {1} in mutableVariablesList", var.variableType, var.name));
                            }
                        }
                        Utility.LogMessage(this, "Resizing variables lists to N:{0} L:{1}", nativeVariableCount, luaVariableCount);
                        mutableVariablesChanged = false;
                    }

                    fcProxy.Update();
                    farProxy.Update();
                    kacProxy.Update();
                    mjProxy.Update();
                    navProxy.Update();
                    parachuteProxy.Update();
                    transferProxy.Update();
                    UpdateRadios();

                    // Precompute the disruption chances.
                    if (electricChargeIndex == -1)
                    {
                        // We have to poll it here because the value may not be initialized
                        // when we're in Start().
                        electricChargeIndex = vc.GetResourceIndex(MASConfig.ElectricCharge);
                    }

                    isPowered = (!requiresPower || vc.ResourceCurrentDirect(electricChargeIndex) > 0.0001) && powerOnValid;
                    if (vessel.geeForce_immediate > gLimit)
                    {
                        disruptionChance = baseDisruptionChance * Mathf.Sqrt((float)vessel.geeForce_immediate - gLimit);
                    }
                    else
                    {
                        disruptionChance = 0.0f;
                    }

                    // TODO: Add a heuristic to adjust the loop so not all variables
                    // update every fixed update if the average update time is too high.
                    // Need to decide if it's going to be an absolute time (max # ms/update)
                    // or a relative time.
                    // NOTE: 128 Lua "variables" average about 1.7ms/update!  And there's
                    // a LOT of garbage collection going on.
                    // 229 variables -> 2.6-2.7ms/update, so 70-80 variables per ms on
                    // a reasonable mid-upper range CPU (3.6GHz).
                    nativeStopwatch.Start();
                    int count = nativeVariables.Length;
                    for (int i = 0; i < count; ++i)
                    {
                        try
                        {
                            nativeVariables[i].Evaluate(script);
                        }
                        catch (Exception e)
                        {
                            Utility.LogErrorMessage(this, "FixedUpdate exception on variable {0}:", nativeVariables[i].name);
                            Utility.LogErrorMessage(this, e.ToString());
                            //throw e;
                        }
                    }
                    nativeVariablesCount += count;
                    nativeStopwatch.Stop();

                    luaStopwatch.Start();
                    // Update some Lua variables - user configurable, so lower-
                    // spec machines aren't as badly affected.
                    int startLuaIdx, endLuaIdx;
                    if (MASConfig.LuaUpdatePriority == 1)
                    {
                        startLuaIdx = 0;
                        endLuaIdx = luaVariables.Length;
                    }
                    else
                    {
                        long modulo = samplecount % MASConfig.LuaUpdatePriority;
                        int span = luaVariables.Length / MASConfig.LuaUpdatePriority;
                        startLuaIdx = (int)modulo * span;

                        if (modulo == MASConfig.LuaUpdatePriority - 1)
                        {
                            endLuaIdx = luaVariables.Length;
                        }
                        else
                        {
                            endLuaIdx = startLuaIdx + span;
                        }
                    }
                    count = endLuaIdx - startLuaIdx;
                    for (int i = startLuaIdx; i < endLuaIdx; ++i)
                    {
                        try
                        {
                            luaVariables[i].Evaluate(script);
                        }
                        catch (Exception e)
                        {
                            Utility.LogErrorMessage(this, "FixedUpdate exception on variable {0}", luaVariables[i].name);
                            luaStopwatch.Stop();
                            throw e;
                        }
                    }
                    luaVariablesCount += count;
                    luaStopwatch.Stop();
                    ++samplecount;
                }
                else if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch.shipDescriptionField != null)
                {
                    string newDescr = EditorLogic.fetch.shipDescriptionField.text.Replace(Utility.EditorNewLine, "$$$");
                    Utility.LogMessage(this, "newDescr is {0}", newDescr);
                    if (newDescr != shipDescription)
                    {
                        shipDescription = newDescr;
                    }
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
            engineProxy = null;
            mjProxy = null;
            navProxy = null;
            farProxy = null;
            kacProxy = null;
            parachuteProxy = null;
            transferProxy = null;
            if (initialized)
            {
                Utility.LogMessage(this, "OnDestroy for {0}", flightComputerId);
                GameEvents.onVesselWasModified.Remove(onVesselChanged);
                GameEvents.onVesselChange.Remove(onVesselChanged);
                GameEvents.onVesselCrewWasModified.Remove(onVesselChanged);

                if (!string.IsNullOrEmpty(powerOnVariable))
                {
                    UnregisterNumericVariable(powerOnVariable, null, UpdatePowerOnVariable);
                }

                Utility.LogInfo(this, "{3} variables created: {0} constant variables, {1} native variables, and {2} Lua variables",
                    constantVariableCount, nativeVariableCount, luaVariableCount, variables.Count);
                if (samplecount > 0)
                {
                    double msPerFixedUpdate = 1000.0 * (double)(nativeStopwatch.ElapsedTicks) / (double)(samplecount * Stopwatch.Frequency);
                    double samplesPerMs = (double)nativeVariablesCount / (1000.0 * (double)(nativeStopwatch.ElapsedTicks) / (double)(Stopwatch.Frequency));
                    Utility.LogInfo(this, "FixedUpdate native average = {0:0.00}ms/FixedUpdate or {1:0.0} variables/ms", msPerFixedUpdate, samplesPerMs);

                    msPerFixedUpdate = 1000.0 * (double)(luaStopwatch.ElapsedTicks) / (double)(samplecount * Stopwatch.Frequency);
                    samplesPerMs = (double)luaVariablesCount / (1000.0 * (double)(luaStopwatch.ElapsedTicks) / (double)(Stopwatch.Frequency));
                    Utility.LogInfo(this, "FixedUpdate Lua    average = {0:0.00}ms/FixedUpdate or {1:0.0} variables/ms", msPerFixedUpdate, samplesPerMs);
                }
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
                    registeredTables.Add("chatterer", new MASRegisteredTable(chattererProxy));

                    engineProxy = new MASIEngine();
                    UserData.RegisterType<MASIEngine>();
                    script.Globals["engine"] = engineProxy;
                    registeredTables.Add("engine", new MASRegisteredTable(engineProxy));

                    farProxy = new MASIFAR(vessel);
                    UserData.RegisterType<MASIFAR>();
                    script.Globals["far"] = farProxy;
                    registeredTables.Add("far", new MASRegisteredTable(farProxy));

                    kacProxy = new MASIKAC(vessel);
                    UserData.RegisterType<MASIKAC>();
                    script.Globals["kac"] = kacProxy;
                    registeredTables.Add("kac", new MASRegisteredTable(kacProxy));

                    mjProxy = new MASIMechJeb();
                    UserData.RegisterType<MASIMechJeb>();
                    script.Globals["mechjeb"] = mjProxy;
                    registeredTables.Add("mechjeb", new MASRegisteredTable(mjProxy));

                    navProxy = new MASINavigation(vessel, this);
                    UserData.RegisterType<MASINavigation>();
                    script.Globals["nav"] = navProxy;
                    registeredTables.Add("nav", new MASRegisteredTable(navProxy));

                    parachuteProxy = new MASIParachute(vessel);
                    UserData.RegisterType<MASIParachute>();
                    script.Globals["parachute"] = parachuteProxy;
                    registeredTables.Add("parachute", new MASRegisteredTable(parachuteProxy));

                    transferProxy = new MASITransfer(vessel);
                    UserData.RegisterType<MASITransfer>();
                    script.Globals["transfer"] = transferProxy;
                    registeredTables.Add("transfer", new MASRegisteredTable(transferProxy));

                    fcProxy = new MASFlightComputerProxy(this, farProxy, mjProxy);
                    UserData.RegisterType<MASFlightComputerProxy>();
                    script.Globals["fc"] = fcProxy;
                    registeredTables.Add("fc", new MASRegisteredTable(fcProxy));
                }
                catch (Exception e)
                {
                    Utility.LogErrorMessage(this, "Proxy object configuration failed:");
                    Utility.LogErrorMessage(this, e.ToString());
                    Utility.ComplainLoudly("Initialization Failed.  Please check KSP.log");
                }
                vc = MASVesselComputer.Instance(vessel);

                if (!MASPersistent.PersistentsLoaded)
                {
                    throw new ArgumentNullException("MASPersistent.PersistentsLoaded has not loaded!");
                }
                persistentVars = MASPersistent.RestoreDictionary(fcId, persistentVars);
                navRadioFrequency = MASPersistent.RestoreNavRadio(fcId, navRadioFrequency);
                foreach (var radio in navRadioFrequency)
                {
                    ReloadRadio(radio.Key, radio.Value);
                }

                // Always make sure we set the vessel ID in the persistent table
                // based on what it currently is.
                SetPersistent(vesselIdLabel, parentVesselId.ToString());

                object activeFilters;
                if (persistentVars.TryGetValue(vesselFilterLabel, out activeFilters) && (activeFilters is string))
                {
                    vesselFilterValue = int.Parse((activeFilters as string), System.Globalization.NumberStyles.HexNumber);
                }
                else
                {
                    // Initial values
                    vesselFilterValue = 0;
                    vesselFilterValue |= 1 << bitVesselRemap[VesselType.Probe];
                    vesselFilterValue |= 1 << bitVesselRemap[VesselType.Relay];
                    vesselFilterValue |= 1 << bitVesselRemap[VesselType.Rover];
                    vesselFilterValue |= 1 << bitVesselRemap[VesselType.Lander];
                    vesselFilterValue |= 1 << bitVesselRemap[VesselType.Ship];
                    vesselFilterValue |= 1 << bitVesselRemap[VesselType.Plane];
                    vesselFilterValue |= 1 << bitVesselRemap[VesselType.Station];
                    vesselFilterValue |= 1 << bitVesselRemap[VesselType.Base];
                    vesselFilterValue |= 1 << bitVesselRemap[VesselType.EVA];
                    vesselFilterValue |= 1 << bitVesselRemap[VesselType.Flag];

                    SetPersistent(vesselFilterLabel, vesselFilterValue.ToString("X"));
                }

                activeVesselFilter.Clear();
                for (int i = 1; i < 14; ++i)
                {
                    if ((vesselFilterValue & (1 << i)) != 0)
                    {
                        activeVesselFilter.Add(vesselBitRemap[i]);
                    }
                }

                // TODO: Don't need to set vessel for all of these guys if I just now init'd them.
                fcProxy.vc = vc;
                fcProxy.vessel = vessel;
                engineProxy.vc = vc;

                //farProxy.vessel = vessel;
                //kacProxy.vessel = vessel;
                mjProxy.UpdateVessel(vessel, vc);
                parachuteProxy.vc = vc;
                transferProxy.vc = vc;
                //realChuteProxy.vessel = vessel;

                // Add User scripts
                try
                {
                    for (int i = MASLoader.userScripts.Count - 1; i >= 0; --i)
                    {
                        script.DoString(MASLoader.userScripts[i]);
                    }

                    Utility.LogInfo(this, "{1}: Loaded {0} user scripts", MASLoader.userScripts.Count, flightComputerId);
                }
                catch (MoonSharp.Interpreter.SyntaxErrorException e)
                {
                    Utility.ComplainLoudly("User Script Loading error");
                    Utility.LogErrorMessage(this, " - {0}", e.DecoratedMessage);
                    Utility.LogErrorMessage(this, e.ToString());
                }
                catch (Exception e)
                {
                    Utility.ComplainLoudly("User Script Loading error");
                    Utility.LogErrorMessage(this, e.ToString());
                }

                // Parse action group labels:
                if (!string.IsNullOrEmpty(shipDescription))
                {
                    string[] rows = shipDescription.Replace("$$$", Environment.NewLine).Split(Utility.LineSeparator, StringSplitOptions.RemoveEmptyEntries);
                    for(int i=0; i<rows.Length; ++i)
                    {
                        if (rows[i].StartsWith("AG"))
                        {
                            string[] row = rows[i].Split('=');
                            int groupID;
                            if (int.TryParse(row[0].Substring(2), out groupID))
                            {
                                if (groupID >=0 && groupID <=9)
                                {
                                    if (row.Length == 2)
                                    {
                                        string[] memo = row[1].Split('|');
                                        if (memo.Length == 1)
                                        {
                                            agMemoOn[groupID] = memo[0].Trim();
                                            agMemoOff[groupID] = agMemoOn[groupID];
                                        }
                                        else if (memo.Length == 2)
                                        {
                                            agMemoOn[groupID] = memo[0].Trim();
                                            agMemoOff[groupID] = memo[1].Trim();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Initialize persistent vars ... note that save game values have
                // already been restored, so check if the persistent already exists,
                // first.
                // Also restore named color per-part overrides
                try
                {
                    ConfigNode myNode = Utility.GetPartModuleConfigNode(part, typeof(MASFlightComputer).Name, 0);
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

                audioObject.name = "MASFlightComputerAudio-" + flightComputerId;
                audioSource = audioObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0.0f;

                UpdateLocalCrew();

                GameEvents.onVesselWasModified.Add(onVesselChanged);
                GameEvents.onVesselChange.Add(onVesselChanged);
                GameEvents.onVesselCrewWasModified.Add(onVesselChanged);

                if (!string.IsNullOrEmpty(powerOnVariable))
                {
                    RegisterNumericVariable(powerOnVariable, null, UpdatePowerOnVariable);
                }

                initialized = true;
            }
        }

        /// <summary>
        /// Display name to show in the VAB when part description is expanded.
        /// </summary>
        /// <returns></returns>
        public override string GetModuleDisplayName()
        {
            return "#MAS_FlightComputer_Module_DisplayName";
        }

        /// <summary>
        /// Text to show in the VAB when part description is expanded.
        /// </summary>
        /// <returns></returns>
        public override string GetInfo()
        {
            return "#MAS_FlightComputer_GetInfo";
        }

        #endregion

        #region Private Methods
        private void UpdatePowerOnVariable(double newValue)
        {
            powerOnValid = (newValue > 0.0);
        }

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
            else if (localCrew.Length > 0)
            {
                localCrew = new ProtoCrewMember[0];
                localCrewMedical = new kerbalExpressionSystem[0];
            }
        }
        #endregion

        #region Audio Player
        GameObject audioObject = new GameObject();
        AudioSource audioSource;

        /// <summary>
        /// Select and play an audio clip.
        /// 
        /// If stopCurrent is true, any current audio is stopped, first.  If stopCurrent is
        /// false and audio is playing, the new clip does not play.
        /// </summary>
        /// <param name="clipName">URI of the clip to load & play.</param>
        /// <param name="volume">Volume (clamped to [0, 1]) for playback.</param>
        /// <param name="stopCurrent">Whether current audio should be stopped.</param>
        /// <returns>true if the clip was loaded and is playing, false if the clip was not played.</returns>
        internal bool PlayAudio(string clipName, float volume, bool stopCurrent)
        {
            AudioClip clip = GameDatabase.Instance.GetAudioClip(clipName);
            if (clip == null)
            {
                return false;
            }

            if (stopCurrent)
            {
                audioSource.Stop();
            }
            else if (audioSource.isPlaying)
            {
                return false;
            }

            audioSource.clip = clip;
            audioSource.volume = GameSettings.SHIP_VOLUME * volume;
            audioSource.Play();
            return true;
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
                if (vessel.id != parentVesselId)
                {
                    parentVesselId = vessel.id;
                    SetPersistent(vesselIdLabel, parentVesselId.ToString());
                }
                vc = MASVesselComputer.Instance(vessel);
                fcProxy.vc = vc;
                fcProxy.vessel = vessel;
                chattererProxy.UpdateVessel();
                engineProxy.vc = vc;
                farProxy.vessel = vessel;
                kacProxy.vessel = vessel;
                mjProxy.UpdateVessel(vessel, vc);
                navProxy.UpdateVessel(vessel);
                parachuteProxy.vc = vc;
                parachuteProxy.vessel = vessel;
                transferProxy.vc = vc;
                transferProxy.vessel = vessel;
            }
            UpdateLocalCrew();
        }
        #endregion
    }
}
