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
        /// Have we initialized?
        /// </summary>
        private bool initialized = false;

        /// <summary>
        /// Dictionary of known RasterPropComputer-compatible named colors.
        /// </summary>
        private Dictionary<string, Color32> namedColors = new Dictionary<string, Color32>();

        /// <summary>
        /// Dictionary of loaded Action Lua wrappers.
        /// </summary>
        private Dictionary<string, Action> actions = new Dictionary<string, Action>();

        #region Internal Interface
        /// <summary>
        /// Return the ASFlightComputer attached to the given part, or null if
        /// none is found.
        /// </summary>
        /// <param name="part">The part where the flight computer should live</param>
        /// <returns>ASFlightComputer or null</returns>
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
            if (prop == null)
            {
                return initialName;
            }
            else
            {
                return initialName.Trim();
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
            variableName = ConditionVariableName(variableName, prop);
            if (variableName.Length < 1)
            {
                throw new ArgumentException("RegisterNumericVariable called with empty variableName");
            }

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
            if (variables.ContainsKey(variableName))
            {
                variables[variableName].numericCallbacks -= callback;
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
            variableName = ConditionVariableName(variableName, prop);
            if (variableName.Length < 1)
            {
                throw new ArgumentException("RegisterOnVariableChange called with empty variableName");
            }

            Utility.LogMessage(this, "RegisterOnVariableChange {0}", variableName);
            Variable v = GetVariable(variableName, null);

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
            if (variables.ContainsKey(variableName))
            {
                variables[variableName].changeCallbacks -= callback;
            }
        }

        /// <summary>
        /// Get the named Variable (for direct access)
        /// </summary>
        /// <param name="variableName"></param>
        /// <returns></returns>
        internal Variable GetVariable(string variableName, InternalProp prop)
        {
            variableName = ConditionVariableName(variableName, prop);
            if (variableName.Length < 1)
            {
                throw new ArgumentException("Trying to GetVariable with empty variableName");
            }

            Variable v = null;
            if (variables.ContainsKey(variableName))
            {
                v = variables[variableName];
            }
            else
            {
                v = new Variable(variableName, script);
                variables.Add(variableName, v);
                if (v.mutable)
                {
                    mutableVariables.Add(v);
                }
                Utility.LogMessage(this, "Adding new variable '{0}'", variableName);
            }

            return v;
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

            throw new ArgumentException("Unknown named color '" + namedColor + "'.");
        }

        /// <summary>
        /// Returns or generates an Action that encapsulates the supplied actionName,
        /// which is simply the Lua function(s) that are to be executed.
        /// </summary>
        /// <param name="actionName"></param>
        /// <returns></returns>
        internal Action GetAction(string actionName, InternalProp prop)
        {
            actionName = ConditionVariableName(actionName, prop);

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
            if (initialized) // Change this to "is active vessel"
            {
                int count = mutableVariables.Count;
                for (int i = 0; i < count; ++i)
                {
                    mutableVariables[i].Evaluate(script);
                }
            }
        }

        /// <summary>
        /// Shut down, unregister, etc.
        /// </summary>
        public void OnDestroy()
        {
            script = null;
            fcProxy = null;
            if (initialized)
            {
                Utility.LogMessage(this, "OnDestroy for {0}", flightComputerId);
                GameEvents.onVesselWasModified.Remove(onVesselChanged);
                GameEvents.onVesselChange.Remove(onVesselChanged);
                GameEvents.onVesselCrewWasModified.Remove(onVesselChanged);
            }
        }

        /// <summary>
        /// Start up this module.
        /// </summary>
        public void Start()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
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

                // Global State (set up links to the proxy).  Note that we
                // don't use the proxy system included in MoonSharp, since it
                // creates a proxy object for every single script.Call(), which
                // means plenty of garbage...
                fcProxy = new MASFlightComputerProxy(this);
                UserData.RegisterType<MASFlightComputerProxy>();
                script.Globals["fc"] = fcProxy;
                fcProxy.vc = MASVesselComputer.Instance(parentVesselId);
                fcProxy.vessel = vessel;

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
                    Utility.LogErrorMessage(this, "Exception caught loading user scripts:");
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
            if (who.id == vessel.id)
            {
                // TODO: Do something different if parentVesselID != vessel.id?
                parentVesselId = vessel.id;
                fcProxy.vc = MASVesselComputer.Instance(parentVesselId);
                fcProxy.vessel = vessel;
                UpdateLocalCrew();
            }
        }
        #endregion
    }
}
