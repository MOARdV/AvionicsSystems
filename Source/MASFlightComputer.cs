//#define MEASURE_FC_FIXEDUPDATE
//#define LIST_LUA_VARIABLES
/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2019 MOARdV
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using UnityEngine;

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
        internal string vesselDescription = string.Empty;

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
        /// How much power does the MASFlightComputer draw on its own?  Ignored if `requiresPower` is false.
        /// </summary>
        [KSPField]
        public float rate = 0.0f;

        [KSPField]
        public string powerOnVariable = string.Empty;
        internal bool powerOnValid = true;

        [KSPField]
        public float maxRot = 80.0f;

        [KSPField]
        public float minPitch = -80.0f;

        [KSPField]
        public float maxPitch = 45.0f;

        [KSPField]
        public string startupScript = string.Empty;

        [KSPField]
        public string onEnterIVA = string.Empty;
        private DynValue enterIvaScript = null;

        [KSPField]
        public string onExitIVA = string.Empty;
        private DynValue exitIvaScript = null;

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
        /// Instance of the Kerbal Engineer proxy class;
        /// </summary>
        private MASIKerbalEngineer keProxy;

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
        /// Instance of the VTOL manager proxy class.
        /// </summary>
        private MASIVTOL vtolProxy;

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
        /// Set to true when any of the throttle keys are pressed.
        /// </summary>
        internal bool anyThrottleKeysPressed;

        /// <summary>
        /// Custom MAS Action Group dictionary.
        /// </summary>
        internal Dictionary<int, MASActionGroup> masActionGroup = new Dictionary<int, MASActionGroup>();

        /// <summary>
        /// Reference to the current vessel computer.
        /// </summary>
        internal MASVesselComputer vc;

        /// <summary>
        /// Reference to the current vessel autopilot.
        /// </summary>
        internal MASAutoPilot ap;

        /// <summary>
        /// Additional EC required by subcomponents via `fc.IncreasePowerDraw(rate)`.  Ignored if requiresPower is false.
        /// </summary>
        private float additionalEC = 0.0f;

        /// <summary>
        /// Resource ID of the electric charge.
        /// </summary>
        private int resourceId = -1;

        /// <summary>
        /// Reference to the current IVA Kerbal.
        /// </summary>
        internal Kerbal currentKerbal;

        /// <summary>
        /// Is the current Kerbal suffering the effects of GLOC?
        /// </summary>
        internal bool currentKerbalBlackedOut;

        /// <summary>
        /// Direct index to the vessel resource list.
        /// </summary>
        private int electricChargeIndex = -1;

        /// <summary>
        /// The ModuleCommand on this IVA.
        /// </summary>
        private ModuleCommand commandModule = null;
        private int activeControlPoint = 0;

        internal ModuleColorChanger colorChangerModule = null;

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
        private Stopwatch dependentStopwatch = new Stopwatch();
        long samplecount = 0;
        long nativeEvaluationCount = 0;
        long luaEvaluationCount = 0;
        long dependentEvaluationCount = 0;

        private static bool dpaiChecked = false;
        private static Func<object, string> GetDpaiName = null;

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
        /// Register to receive on-changed callback notification for
        /// a variable.
        /// </summary>
        /// <param name="variableName">Un-massaged name of the variable.</param>
        /// <param name="prop">The prop associated with this variable.</param>
        /// <param name="callback">The callback to invoke when this variable changes.</param>
        /// <param name="initializeNow">Should the callback be invoked immediately?</param>
        /// <returns>The variable created, or null.</returns>
        internal Variable RegisterVariableChangeCallback(string variableName, InternalProp prop, Action<double> callback, bool initializeNow = true)
        {
            Variable v = null;
            try
            {
                v = GetVariable(variableName, prop);

                v.RegisterNumericCallback(callback);

                if (initializeNow)
                {
                    callback(v.AsDouble());
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException("Error parsing variable \"" + variableName + "\"", e);
            }

            return v;
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
        /// Function to handle a touchscreen (COLLIDER_ADVANCED) event.
        /// </summary>
        /// <param name="monitorName">The monitor that will receive the event.</param>
        /// <param name="hitCoordinate">The x and y coordinates of the event, as processed by the COLLIDER_ADVANCED</param>
        internal void HandleTouchEvent(string monitorName, Vector2 hitCoordinate, EventType eventType)
        {
            MASMonitor monitor;
            if (monitors.TryGetValue(monitorName, out monitor))
            {
                monitor.HandleTouchEvent(hitCoordinate, eventType);
            }
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
                            Utility.LogError(this, "Action {0} triggered exception:", actionName);
                            Utility.LogError(this, e.ToString());
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
        /// Creates a Lua function that uses 'actionName' as its body, then generates an Action that takes
        /// a double for its parameter that is used to call the newly-created function.
        /// </summary>
        /// <param name="actionName"></param>
        /// <param name="componentName"></param>
        /// <param name="prop"></param>
        /// <returns></returns>
        internal Action<double> GetDragAction(string actionName, string componentName, InternalProp prop)
        {
            string preppedActionName = actionName.Replace("%DRAG%", "dragDelta");
            string propName = ConditionVariableName(string.Format("%AUTOID%_{0}", componentName), prop);
            propName = propName.Replace('-', '_').Replace(' ', '_');

            StringBuilder sb = StringBuilderCache.Acquire();
            sb.AppendFormat("function {0}_drag(dragDelta)", propName).AppendLine().AppendFormat("  {0}", preppedActionName).AppendLine().AppendLine("end");
            preppedActionName = ConditionVariableName(sb.ToStringAndRelease(), prop);

            // Compile the script.
            script.DoString(preppedActionName);
            // Get the function.
            DynValue closure = script.Globals.Get(string.Format("{0}_drag", propName));
            if (closure.Type == DataType.Function)
            {
                return (double newValue) =>
                  {
                      DynValue parm = DynValue.NewNumber(newValue);
                      DynValue result = script.Call(closure, parm);
                  };
            }
            else
            {
                Utility.LogError(this, "Failed to compile \"{0}\" into a Lua function", actionName);
                return null;
            }
        }

        /// <summary>
        /// Returns an action that the COLLIDER_ADVANCED object can use to pass click information to a monitor.
        /// </summary>
        /// <param name="monitorID">ID of the monitor.</param>
        /// <param name="prop"></param>
        /// <returns></returns>
        internal Action<Vector2, EventType> GetHitAction(string monitorID, InternalProp prop, Action<string, Vector2, EventType> hitAction)
        {
            string conditionedId = ConditionVariableName(monitorID, prop);

            Action<Vector2, EventType> ev = (xy, eventType) => hitAction(conditionedId, xy, eventType);
            return ev;
        }

        internal Action<Vector2> GetColliderAction(string actionName, int hitboxID, string actionType, InternalProp prop)
        {
            actionName = ConditionVariableName(actionName, prop);

            string propName = ConditionVariableName(string.Format("%AUTOID%_{0}", hitboxID), prop);
            propName = propName.Replace('-', '_').Replace(' ', '_');

            Action<Vector2> act = null;
            if (actionName.Contains("%X%") || actionName.Contains("%Y%"))
            {
                // Case where the location within the hitbox matters.

                actionName = actionName.Replace("%X%", "x").Replace("%Y%", "y");
                StringBuilder sb = StringBuilderCache.Acquire();
                sb.AppendFormat("function {0}_{1}(x, y)", propName, actionType).AppendLine().AppendFormat("  {0}", actionName).AppendLine().AppendLine("end");
                string preppedActionName = ConditionVariableName(sb.ToStringAndRelease(), prop);

                // Compile the script.
                script.DoString(preppedActionName);
                // Get the function.
                DynValue closure = script.Globals.Get(string.Format("{0}_{1}", propName, actionType));
                if (closure.Type == DataType.Function)
                {
                    return (loc) =>
                    {
                        DynValue xIn = DynValue.NewNumber(loc.x);
                        DynValue yIn = DynValue.NewNumber(loc.y);
                        try
                        {
                            script.Call(closure, xIn, yIn);
                        }
                        catch (Exception e)
                        {
                            Utility.ComplainLoudly("Action " + actionName + " triggered an exception");
                            Utility.LogError(this, "Action {0} triggered exception:", actionName);
                            Utility.LogError(this, e.ToString());
                        }
                    };
                }
                else
                {
                    Utility.LogError(this, "Failed to compile \"{0}\" into a Lua function", actionName);
                    return null;
                }
            }
            else
            {
                // Simple case - doesn't use X or Y parameter.
                try
                {
                    DynValue dv = script.LoadString(actionName);

                    act = (loc) =>
                    {
                        try
                        {
                            script.Call(dv);
                        }
                        catch (Exception e)
                        {
                            Utility.ComplainLoudly("Action " + actionName + " triggered an exception");
                            Utility.LogError(this, "Action {0} triggered exception:", actionName);
                            Utility.LogError(this, e.ToString());
                        }
                    };
                }
                catch
                {
                    return null;
                }
            }

            return act;
        }

        /// <summary>
        /// Write a Lua script to transform x, y, z normalized Collider coordinates into an x, y value
        /// that is sent to the monitor.
        /// </summary>
        /// <param name="xTransformation"></param>
        /// <param name="yTransformation"></param>
        /// <param name="prop"></param>
        /// <returns></returns>
        internal Func<float, float, float, Vector2> GetColliderTransformation(string xTransformation, string yTransformation, string componentName, InternalProp prop)
        {
            string propName = ConditionVariableName(string.Format("%AUTOID%_{0}", componentName), prop);
            propName = propName.Replace('-', '_').Replace(' ', '_');

            string conditionedX = ConditionVariableName(xTransformation, prop).Replace("%X%", "x").Replace("%Y%", "y").Replace("%Z%", "z");
            string conditionedY = ConditionVariableName(yTransformation, prop).Replace("%X%", "x").Replace("%Y%", "y").Replace("%Z%", "z");
            StringBuilder sb = StringBuilderCache.Acquire();

            sb.AppendFormat("function {0}_transform(x, y, z)", propName).AppendLine().AppendFormat("  local x1 = {0}", conditionedX).AppendFormat("  local y1 = {0}", conditionedY).AppendLine().AppendLine("  return x1, y1").AppendLine("end");

            string preppedFunction = sb.ToStringAndRelease();
            script.DoString(preppedFunction);

            // Get the function.
            DynValue closure = script.Globals.Get(string.Format("{0}_transform", propName));

            Func<float, float, float, Vector2> f = (x, y, z) =>
                {
                    DynValue xIn = DynValue.NewNumber(x);
                    DynValue yIn = DynValue.NewNumber(y);
                    DynValue zIn = DynValue.NewNumber(z);
                    DynValue result = script.Call(closure, xIn, yIn, zIn);
                    if (result.Type == DataType.Tuple)
                    {
                        DynValue[] multiret = result.Tuple;
                        double x1 = multiret[0].Number;
                        double y1 = multiret[1].Number;

                        return new Vector2((float)x1, (float)y1);
                    }
                    else
                    {
                        Utility.LogError(this, "Called script - result = {0}, not DataType.Tuple", result.Type);

                        return new Vector2(x, y);
                    }
                };
            return f;
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

        internal string GetDockingPortName(Part dockingNodePart)
        {
            if (GetDpaiName == null)
            {
                return dockingNodePart.partInfo.title;
            }
            else
            {
                PartModule namedDockModule = dockingNodePart.Modules["ModuleDockingNodeNamed"];

                return (namedDockModule != null) ? GetDpaiName(namedDockModule) : dockingNodePart.partInfo.title;
            }
        }

        internal float GetPowerDraw()
        {
            return (requiresPower) ? (additionalEC + rate) : 0.0f;
        }

        internal float ChangePowerDraw(float amount)
        {
            if (requiresPower)
            {
                additionalEC = Mathf.Max(0.0f, amount + additionalEC);
                return additionalEC + rate;
            }
            else
            {
                return 0.0f;
            }
        }

        internal int GetCurrentControlPoint()
        {
            return activeControlPoint;
        }

        internal string GetControlPointName(int controlPoint)
        {
            if (controlPoint == -1)
            {
                controlPoint = activeControlPoint;
            }

            if (commandModule != null && controlPoint >= 0 && controlPoint < commandModule.controlPoints.Count)
            {
                return commandModule.controlPoints.At(controlPoint).displayName;
            }
            return string.Empty;
        }

        internal int GetNumControlPoints()
        {
            if (commandModule != null)
            {
                return commandModule.controlPoints.Count;
            }
            return 0;
        }

        internal float SetCurrentControlPoint(int newControlPoint)
        {
            if (commandModule != null && newControlPoint >= 0 && newControlPoint < commandModule.controlPoints.Count)
            {
                commandModule.SetControlPoint(commandModule.controlPoints.At(newControlPoint).name);
                return 1.0f;
            }
            return 0.0f;
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
#if MEASURE_FC_FIXEDUPDATE
        Stopwatch fixedupdateTimer = new Stopwatch();
#endif
        /// <summary>
        /// Process updates to tracked variables.
        /// </summary>
        public void FixedUpdate()
        {
            try
            {
                if (initialized && vc.vesselCrewed && vc.vesselActive)
                {
#if MEASURE_FC_FIXEDUPDATE
                    fixedupdateTimer.Reset();
                    fixedupdateTimer.Start();
#endif
                    // Realistically, this block of code won't be triggered very
                    // often.  Once per scene change per pod.
                    if (mutableVariablesChanged)
                    {
                        nativeVariables = new Variable[nativeVariableCount];
                        luaVariables = new Variable[luaVariableCount];
                        dependentVariables = new Variable[dependentVariableCount];
                        int nativeIdx = 0, luaIdx = 0, dependentIdx = 0;
                        foreach (Variable var in mutableVariablesList)
                        {
                            if (var.variableType == Variable.VariableType.LuaScript)
                            {
                                luaVariables[luaIdx] = var;
                                ++luaIdx;
                            }
                            else if (var.variableType == Variable.VariableType.Func)
                            {
                                nativeVariables[nativeIdx] = var;
                                ++nativeIdx;
                            }
                            else if (var.variableType == Variable.VariableType.Dependent)
                            {
                                dependentVariables[dependentIdx] = var;
                                ++dependentIdx;
                            }
                            else
                            {
                                throw new ArgumentException(string.Format("Unexpected variable type {0} for variable {1} in mutableVariablesList", var.variableType, var.name));
                            }
                        }
                        Utility.LogMessage(this, "Resizing variables lists to N:{0} L:{1} D:{2}", nativeVariableCount, luaVariableCount, dependentVariableCount);
                        mutableVariablesChanged = false;
                    }

                    fcProxy.Update();
                    farProxy.Update();
                    kacProxy.Update();
                    keProxy.Update();
                    mjProxy.Update();
                    navProxy.Update();
                    parachuteProxy.Update();
                    transferProxy.Update();
                    UpdateRadios();
#if MEASURE_FC_FIXEDUPDATE
                    TimeSpan updatesTime = fixedupdateTimer.Elapsed;
#endif

                    anyThrottleKeysPressed = GameSettings.THROTTLE_CUTOFF.GetKey() || GameSettings.THROTTLE_FULL.GetKey() || GameSettings.THROTTLE_UP.GetKey() || GameSettings.THROTTLE_DOWN.GetKey();

                    // Precompute the disruption chances.
                    if (electricChargeIndex == -1)
                    {
                        // We have to poll it here because the value may not be initialized
                        // when we're in Start().
                        electricChargeIndex = vc.GetResourceIndex(MASConfig.ElectricCharge);
                        try
                        {
                            PartResourceDefinition def = PartResourceLibrary.Instance.resourceDefinitions[MASConfig.ElectricCharge];
                            resourceId = def.id;
                        }
                        catch
                        {
                            Utility.LogError(this, "Unable to resolve resource '{0}', flight computer can not require power.", MASConfig.ElectricCharge);
                            requiresPower = false;
                        }
                    }

                    isPowered = (!requiresPower || vc.ResourceCurrentDirect(electricChargeIndex) > 0.0001) && powerOnValid;

                    if (requiresPower && (rate + additionalEC) > 0.0f)
                    {
                        double requested = (rate + additionalEC) * TimeWarp.fixedDeltaTime;
                        double supplied = part.RequestResource(resourceId, requested);
                        if (supplied < requested * 0.5)
                        {
                            isPowered = false;
                        }
                    }

                    currentKerbalBlackedOut = false;
                    currentKerbal = FindCurrentKerbal();
                    if (currentKerbal != null && currentKerbal.protoCrewMember.outDueToG)
                    {
                        // Always black-out instruments from blackouts.
                        disruptionChance = 1.1f;
                        currentKerbalBlackedOut = true;
                    }
                    else if (vessel.geeForce_immediate > gLimit)
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
                            nativeVariables[i].Evaluate(true);
                        }
                        catch (Exception e)
                        {
                            Utility.LogError(this, "FixedUpdate exception on variable {0}:", nativeVariables[i].name);
                            Utility.LogError(this, e.ToString());
                            //throw e;
                        }
                    }
                    nativeEvaluationCount += count;
                    nativeStopwatch.Stop();
#if MEASURE_FC_FIXEDUPDATE
                    TimeSpan nativeTime = fixedupdateTimer.Elapsed;
#endif

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
                            luaVariables[i].Evaluate(true);
                        }
                        catch (Exception e)
                        {
                            Utility.LogError(this, "FixedUpdate exception on variable {0}", luaVariables[i].name);
                            luaStopwatch.Stop();
                            throw e;
                        }
                    }
                    luaEvaluationCount += count;
                    luaStopwatch.Stop();
#if MEASURE_FC_FIXEDUPDATE
                    TimeSpan luaTime = fixedupdateTimer.Elapsed;
#endif

                    dependentStopwatch.Start();
                    count = dependentVariables.Length;
                    for (int i = 0; i < count; ++i)
                    {
                        try
                        {
                            dependentVariables[i].Evaluate(true);
                        }
                        catch (Exception e)
                        {
                            Utility.LogError(this, "FixedUpdate exception on variable {0}:", dependentVariables[i].name);
                            Utility.LogError(this, e.ToString());
                            //throw e;
                        }
                    }
                    dependentEvaluationCount += count;
                    dependentStopwatch.Stop();
                    ++samplecount;
#if MEASURE_FC_FIXEDUPDATE
                    TimeSpan finalTime = fixedupdateTimer.Elapsed;
                    if (finalTime.Ticks > (TimeSpan.TicksPerMillisecond / 2))
                    {
                        double ticksPerMillisecond = (double)TimeSpan.TicksPerMillisecond;
                        Utility.LogMessage(this, "FixedUpdate proxiestes {0,7:0.000} ms",
                            ((double)updatesTime.Ticks) / ticksPerMillisecond);
                        Utility.LogMessage(this, "FixedUpdate native var {0,7:0.000} ms",
                            ((double)(nativeTime.Ticks - updatesTime.Ticks)) / ticksPerMillisecond);
                        Utility.LogMessage(this, "FixedUpdate lua var    {0,7:0.000} ms",
                            ((double)(luaTime.Ticks - nativeTime.Ticks)) / ticksPerMillisecond);
                        Utility.LogMessage(this, "FixedUpdate dep var    {0,7:0.000} ms",
                            ((double)(finalTime.Ticks - luaTime.Ticks)) / ticksPerMillisecond);
                        Utility.LogMessage(this, "FixedUpdate net        {0,7:0.000} ms",
                            ((double)finalTime.Ticks) / ticksPerMillisecond);
                    }
#endif
                }
                else if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch.shipDescriptionField != null)
                {
                    string newDescr = EditorLogic.fetch.shipDescriptionField.text.Replace(Utility.EditorNewLine, "$$$");
                    if (newDescr != shipDescription)
                    {
                        shipDescription = newDescr;
                    }
                }
            }
            catch (Exception e)
            {
                Utility.LogError(this, "MASFlightComputer.FixedUpdate exception: {0}", e);
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
            keProxy = null;
            parachuteProxy = null;
            transferProxy = null;
            vtolProxy = null;
            if (initialized)
            {
                Utility.LogMessage(this, "OnDestroy for {0}", flightComputerId);
                StopAllCoroutines();
                GameEvents.onVesselWasModified.Remove(onVesselChanged);
                GameEvents.onVesselChange.Remove(onVesselChanged);
                GameEvents.onVesselCrewWasModified.Remove(onVesselChanged);
                GameEvents.OnCameraChange.Remove(onCameraChange);
                GameEvents.OnIVACameraKerbalChange.Remove(OnIVACameraKerbalChange);
                GameEvents.OnControlPointChanged.Remove(OnControlPointChanged);

                Utility.LogInfo(this, "{3} variables created: {0} constant variables, {1} delegate variables, {2} Lua variables, and {4} expression variables",
                    constantVariableCount, nativeVariableCount, luaVariableCount, variables.Count, dependentVariableCount);
                if (samplecount > 0)
                {
                    double msPerFixedUpdate = 1000.0 * (double)(nativeStopwatch.ElapsedTicks) / (double)(samplecount * Stopwatch.Frequency);
                    double samplesPerMs = (double)nativeEvaluationCount / (1000.0 * (double)(nativeStopwatch.ElapsedTicks) / (double)(Stopwatch.Frequency));
                    Utility.LogInfo(this, "FixedUpdate Delegate average = {0:0.00}ms/FixedUpdate or {1:0.0} variables/ms", msPerFixedUpdate, samplesPerMs);

                    msPerFixedUpdate = 1000.0 * (double)(luaStopwatch.ElapsedTicks) / (double)(samplecount * Stopwatch.Frequency);
                    samplesPerMs = (double)luaEvaluationCount / (1000.0 * (double)(luaStopwatch.ElapsedTicks) / (double)(Stopwatch.Frequency));
                    Utility.LogInfo(this, "FixedUpdate Lua      average = {0:0.00}ms/FixedUpdate or {1:0.0} variables/ms", msPerFixedUpdate, samplesPerMs);

                    msPerFixedUpdate = 1000.0 * (double)(dependentStopwatch.ElapsedTicks) / (double)(samplecount * Stopwatch.Frequency);
                    samplesPerMs = (double)dependentEvaluationCount / (1000.0 * (double)(dependentStopwatch.ElapsedTicks) / (double)(Stopwatch.Frequency));
                    Utility.LogInfo(this, "FixedUpdate Expr     average = {0:0.00}ms/FixedUpdate or {1:0.0} variables/ms", msPerFixedUpdate, samplesPerMs);
                }
#if LIST_LUA_VARIABLES
                // Lua variables are costly to evaluate - ideally, we minimize the number created for use as variables.
                Utility.LogMessage(this, "{0} Lua variables were created:", luaVariables.Length);
                for(int i=0; i<luaVariables.Length; ++i)
                {
                    Utility.LogMessage(this, "  [{0,2}]: {1}", i, luaVariables[i].name);
                }
#endif
            }
        }

        /// <summary>
        /// Start up this module.
        /// </summary>
        public void Start()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                additionalEC = 0.0f;
                rate = Mathf.Max(0.0f, rate);
                commandModule = part.FindModuleImplementing<ModuleCommand>();
                UpdateControlPoint(commandModule.ActiveControlPointName);
                colorChangerModule = part.FindModuleImplementing<ModuleColorChanger>();
                if (colorChangerModule != null && colorChangerModule.toggleInFlight == false && colorChangerModule.toggleAction == false)
                {
                    // Not a controllable light
                    colorChangerModule = null;
                }

                if (dependentLuaMethods.Count == 0)
                {
                    // Maybe a bit kludgy -- I want to list the Lua table methods that I know
                    // are dependent variables, so they can be treated as such instead of polled
                    // every FixedUpdate.  But I want a fast search, so I use a List, sort it,
                    // and BSearch it.
                    dependentLuaMethods.Add("math.abs");
                    dependentLuaMethods.Add("math.acos");
                    dependentLuaMethods.Add("math.asin");
                    dependentLuaMethods.Add("math.atan");
                    dependentLuaMethods.Add("math.atan2");
                    dependentLuaMethods.Add("math.ceil");
                    dependentLuaMethods.Add("math.cos");
                    dependentLuaMethods.Add("math.cosh");
                    dependentLuaMethods.Add("math.deg");
                    dependentLuaMethods.Add("math.exp");
                    dependentLuaMethods.Add("math.floor");
                    dependentLuaMethods.Add("math.fmod");
                    dependentLuaMethods.Add("math.frexp");
                    dependentLuaMethods.Add("math.ldexp");
                    dependentLuaMethods.Add("math.log");
                    dependentLuaMethods.Add("math.log10");
                    dependentLuaMethods.Add("math.max");
                    dependentLuaMethods.Add("math.min");
                    dependentLuaMethods.Add("math.modf");
                    dependentLuaMethods.Add("math.pow");
                    dependentLuaMethods.Add("math.rad");
                    dependentLuaMethods.Add("math.sin");
                    dependentLuaMethods.Add("math.sinh");
                    dependentLuaMethods.Add("math.sqrt");
                    dependentLuaMethods.Add("math.tan");
                    dependentLuaMethods.Add("math.tanh");
                    dependentLuaMethods.Add("string.format");
                    dependentLuaMethods.Add("string.len");
                    dependentLuaMethods.Add("string.lower");
                    dependentLuaMethods.Add("string.rep");
                    dependentLuaMethods.Add("string.reverse");
                    dependentLuaMethods.Add("string.upper");

                    dependentLuaMethods.Sort();
                }

                if (dpaiChecked == false)
                {
                    dpaiChecked = true;

                    Type dpaiMDNNType = Utility.GetExportedType("ModuleDockingNodeNamed", "NavyFish.ModuleDockingNodeNamed");
                    if (dpaiMDNNType != null)
                    {
                        Utility.LogMessage(this, "Found DPAI");
                        FieldInfo portName = dpaiMDNNType.GetField("portName", BindingFlags.Instance | BindingFlags.Public);
                        GetDpaiName = DynamicMethodFactory.CreateGetField<object, string>(portName);
                    }
                }

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
                // Add the LoadMethods to allow dynamic script compilation.
                script = new Script(CoreModules.Preset_HardSandbox | CoreModules.LoadMethods);

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

                    keProxy = new MASIKerbalEngineer();
                    UserData.RegisterType<MASIKerbalEngineer>();
                    script.Globals["ke"] = keProxy;
                    registeredTables.Add("ke", new MASRegisteredTable(keProxy));

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

                    vtolProxy = new MASIVTOL();
                    UserData.RegisterType<MASIVTOL>();
                    script.Globals["vtol"] = vtolProxy;
                    registeredTables.Add("vtol", new MASRegisteredTable(vtolProxy));

                    fcProxy = new MASFlightComputerProxy(this, farProxy, keProxy, mjProxy);
                    UserData.RegisterType<MASFlightComputerProxy>();
                    script.Globals["fc"] = fcProxy;
                    registeredTables.Add("fc", new MASRegisteredTable(fcProxy));
                }
                catch (Exception e)
                {
                    Utility.LogError(this, "Proxy object configuration failed:");
                    Utility.LogError(this, e.ToString());
                    Utility.ComplainLoudly("Initialization Failed.  Please check KSP.log");
                }

                vc = MASPersistent.FetchVesselComputer(vessel);
                ap = MASAutoPilot.Get(vessel);
                // Initialize the resourceConverterList with ElectricCharge at index 0
                if (vc.resourceConverterList.Count > 0)
                {
                    vc.resourceConverterList.Clear();
                }
                var rc = new MASVesselComputer.GeneralPurposeResourceConverter();
                rc.id = 0;
                rc.outputResource = MASConfig.ElectricCharge;
                vc.resourceConverterList.Add(rc);
                // The resourceConverterList changes - trigger a rebuild
                vc.modulesInvalidated = true;

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
                mjProxy.UpdateVessel(vessel, vc);
                parachuteProxy.vc = vc;
                transferProxy.vc = vc;
                vtolProxy.UpdateVessel(vessel, vc);

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
                    Utility.LogError(this, " - {0}", e.DecoratedMessage);
                    Utility.LogError(this, e.ToString());
                }
                catch (Exception e)
                {
                    Utility.ComplainLoudly("User Script Loading error");
                    Utility.LogError(this, e.ToString());
                }

                // Parse action group labels:
                if (!string.IsNullOrEmpty(shipDescription))
                {
                    string[] rows = shipDescription.Replace("$$$", Environment.NewLine).Split(Utility.LineSeparator, StringSplitOptions.RemoveEmptyEntries);
                    StringBuilder sb = StringBuilderCache.Acquire();
                    for (int i = 0; i < rows.Length; ++i)
                    {
                        if (rows[i].StartsWith("AG"))
                        {
                            string[] row = rows[i].Split('=');
                            int groupID;
                            if (int.TryParse(row[0].Substring(2), out groupID))
                            {
                                if (groupID >= 0 && groupID <= 9)
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
                        else if (sb.Length == 0)
                        {
                            sb.Append(rows[i].Trim());
                        }
                        else
                        {
                            sb.Append("$$$").Append(rows[i].Trim());
                        }
                    }
                    if (sb.Length > 0)
                    {
                        vesselDescription = sb.ToStringAndRelease();
                    }
                }

                // Initialize persistent vars ... note that save game values have
                // already been restored, so check if the persistent already exists,
                // first.
                // Also restore named color per-part overrides
                ConfigNode myNode = Utility.GetPartModuleConfigNode(part, typeof(MASFlightComputer).Name, 0);
                try
                {
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
                try
                {
                    ConfigNode[] actionGroupConfig = myNode.GetNodes("MAS_ACTION_GROUP");
                    for (int agIdx = 0; agIdx < actionGroupConfig.Length; ++agIdx)
                    {
                        MASActionGroup ag = new MASActionGroup(actionGroupConfig[agIdx]);
                        if (masActionGroup.ContainsKey(ag.actionGroupId))
                        {
                            Utility.LogError(this, "Found duplicate MAS_ACTION_GROUP id {0} ... duplicate AG was discarded", ag.actionGroupId);
                        }
                        else
                        {
                            ag.Rebuild(vessel.Parts);
                            masActionGroup.Add(ag.actionGroupId, ag);
                        }
                    }
                }
                catch
                {

                }

                audioObject.name = "MASFlightComputerAudio-" + flightComputerId;
                audioSource = audioObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0.0f;
                morseAudioObject.name = "MASFlightComputerMorseAudio-" + flightComputerId;
                morseAudioSource = audioObject.AddComponent<AudioSource>();
                morseAudioSource.spatialBlend = 0.0f;

                UpdateLocalCrew();

                GameEvents.onVesselWasModified.Add(onVesselChanged);
                GameEvents.onVesselChange.Add(onVesselChanged);
                GameEvents.onVesselCrewWasModified.Add(onVesselChanged);
                GameEvents.OnCameraChange.Add(onCameraChange);
                GameEvents.OnIVACameraKerbalChange.Add(OnIVACameraKerbalChange);
                GameEvents.OnControlPointChanged.Add(OnControlPointChanged);

                if (!string.IsNullOrEmpty(powerOnVariable))
                {
                    RegisterVariableChangeCallback(powerOnVariable, null, (double newValue) => powerOnValid = (newValue > 0.0));
                }

                // All the things are initialized ... Let's see if there's a startupScript
                if (!string.IsNullOrEmpty(startupScript))
                {
                    DynValue dv = script.LoadString(startupScript);

                    if (dv.IsNil() == false)
                    {
                        try
                        {
                            script.Call(dv);
                        }
                        catch (Exception e)
                        {
                            Utility.ComplainLoudly("MASFlightComputer startupScript triggered an exception");
                            Utility.LogError(this, "MASFlightComputer startupScript triggered an exception:");
                            Utility.LogError(this, e.ToString());
                        }
                    }
                }

                if (!string.IsNullOrEmpty(onEnterIVA))
                {
                    enterIvaScript = script.LoadString(onEnterIVA);
                    if (enterIvaScript.IsNil())
                    {
                        Utility.LogError(this, "Failed to process onEnterIVA script");
                    }
                }

                if (!string.IsNullOrEmpty(onExitIVA))
                {
                    exitIvaScript = script.LoadString(onExitIVA);
                    if (exitIvaScript.IsNil())
                    {
                        Utility.LogError(this, "Failed to process onExitIVA script");
                    }
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
        private void UpdateControlPoint(string cpName)
        {
            if (commandModule != null && commandModule.controlPoints.Count > 1)
            {
                for (int i = commandModule.controlPoints.Count - 1; i >= 0; --i)
                {
                    if (commandModule.controlPoints.At(i).name == cpName)
                    {
                        activeControlPoint = i;
                        return;
                    }
                }
            }
            else
            {
                activeControlPoint = 0;
            }
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
        GameObject morseAudioObject = new GameObject();
        AudioSource morseAudioSource;
        string morseSequence;
        float morseVolume;
        bool playingSequence;

        private IEnumerator MorsePlayerCoroutine()
        {
            while (morseSequence.Length > 0)
            {
                AudioClip clip;
                char first = morseSequence[0];
                if (first == ' ')
                {
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                else if (MASLoader.morseCode.TryGetValue(first, out clip))
                {
                    audioSource.clip = clip;
                    audioSource.volume = morseVolume;
                    audioSource.Play();
                    yield return new WaitForSecondsRealtime(clip.length + 0.05f);
                }

                morseSequence = morseSequence.Substring(1);
            }

            playingSequence = false;
            yield return null;
        }

        /// <summary>
        /// Play a morse code sequence.
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="volume"></param>
        /// <param name="stopCurrent"></param>
        /// <returns></returns>
        internal double PlayMorseSequence(string sequence, float volume, bool stopCurrent)
        {
            if (stopCurrent)
            {
                morseAudioSource.Stop();
            }
            else if (morseAudioSource.isPlaying)
            {
                return 0.0;
            }

            morseSequence = sequence.ToUpper();
            morseVolume = GameSettings.SHIP_VOLUME * volume;

            if (!playingSequence)
            {
                StartCoroutine(MorsePlayerCoroutine());
                playingSequence = true;
            }

            return 1.0;
        }

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

        /// <summary>
        /// Identify the current IVA Kerbal.  If the kerbal is not in the current part,
        /// return null.
        /// </summary>
        /// <returns></returns>
        internal Kerbal FindCurrentKerbal()
        {
            Kerbal activeKerbal = CameraManager.Instance.IVACameraActiveKerbal;
            if (activeKerbal.InPart == part)
            {
                return activeKerbal;
            }
            else
            {
                return null;
            }
        }

        #region GameEvent Callbacks
        /// <summary>
        /// Callback when the player changes camera modes.
        /// </summary>
        /// <param name="newMode"></param>
        private void onCameraChange(CameraManager.CameraMode newMode)
        {
            // newMode == Flight -> heading to external view.
            // newMode == Map -> heading to Map view
            // newMode == IVA -> heading to IVA view.
            if (newMode == CameraManager.CameraMode.IVA)
            {
                Kerbal activeKerbal = FindCurrentKerbal();
                if (activeKerbal != null)
                {
                    // There are situations where InternalCamera is null - like during staging
                    // from IVA when the IVA isn't in the new active vessel.
                    if (InternalCamera.Instance != null)
                    {
                        InternalCamera.Instance.maxRot = maxRot;
                        InternalCamera.Instance.minPitch = minPitch;
                        InternalCamera.Instance.maxPitch = maxPitch;
                    }
                }
                if (enterIvaScript != null)
                {
                    try
                    {
                        script.Call(enterIvaScript);
                    }
                    catch (Exception e)
                    {
                        Utility.ComplainLoudly("MASFlightComputer onEnterIVA triggered an exception");
                        Utility.LogError(this, "MASFlightComputer onEnterIVA triggered an exception:");
                        Utility.LogError(this, e.ToString());
                        enterIvaScript = null;
                    }
                }
            }
            else
            {
                // There are situations where InternalCamera is null - like during staging
                // from IVA when the IVA isn't in the new active vessel.
                if (InternalCamera.Instance != null)
                {
                    // Reset to defaults
                    InternalCamera.Instance.maxRot = 80.0f;
                    InternalCamera.Instance.minPitch = -80.0f;
                    InternalCamera.Instance.maxPitch = 45.0f;
                }
                if (exitIvaScript != null)
                {
                    try
                    {
                        script.Call(exitIvaScript);
                    }
                    catch (Exception e)
                    {
                        Utility.ComplainLoudly("MASFlightComputer onExitIVA triggered an exception");
                        Utility.LogError(this, "MASFlightComputer onExitIVA  triggered an exception:");
                        Utility.LogError(this, e.ToString());
                        exitIvaScript = null;
                    }
                }
            }
        }

        /// <summary>
        /// Callback when the player changes control points.
        /// </summary>
        private void OnControlPointChanged(Part who, ControlPoint where)
        {
            if (who == part && commandModule != null)
            {
                UpdateControlPoint(where.name);
            }
        }

        /// <summary>
        /// Callback when the player changes IVA cameras.  Note that the Kerbal passed in
        /// is *not* the current Kerbal.  It appears to be the *next* Kerbal, I think. So we
        /// are forced to call FindCurrentKerbal.
        /// </summary>
        /// <param name="newKerbal"></param>
        private void OnIVACameraKerbalChange(Kerbal dontCare)
        {
            Kerbal newKerbal = FindCurrentKerbal();
            if (newKerbal != null)
            {
                // There are situations where InternalCamera is null - like during staging
                // from IVA when the IVA isn't in the new active vessel.
                if (InternalCamera.Instance != null)
                {
                    InternalCamera.Instance.maxRot = maxRot;
                    InternalCamera.Instance.minPitch = minPitch;
                    InternalCamera.Instance.maxPitch = maxPitch;
                }
            }
        }

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

                vc = MASPersistent.FetchVesselComputer(vessel);
                ap = MASAutoPilot.Get(vessel);
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
                vtolProxy.UpdateVessel(vessel, vc);

                List<Part> parts = vessel.parts;
                foreach (var pair in masActionGroup)
                {
                    pair.Value.Rebuild(parts);
                }
            }
            UpdateLocalCrew();
        }
        #endregion
    }
}
