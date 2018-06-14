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
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace AvionicsSystems
{
    /// <summary>
    /// MASIVTOL is the MAS interface to the VTOL Manager found in
    /// the WildBlueTools mod.  It is a MoonSharp proxy class.
    /// </summary>
    /// <LuaName>vtol</LuaName>
    /// <mdDoc>
    /// MASIVTOL is the AvionicsSystems interface with the Wild Blue Industries VTOL manager.
    /// </mdDoc>
    internal class MASIVTOL
    {
        internal static readonly bool wbivtolInstalled;

        internal static readonly Type wbiVtolManager_t;
        internal static readonly Type wbiWBIGraviticEngine_t;

        // Initialization
        private static readonly Action<object, Vessel> wbiFindControllers;

        // Air Park control
        private static readonly Func<object, bool> wbiAirParkAvailable;
        private static readonly Action<object> wbiTogglePark;
        private static readonly Func<object, bool> wbiGetParkActive;

        // Crazy Mode control
        internal static readonly Func<object, bool> wbiCrazyModeIsActive; // This method is used to select the command crazy-mode engine.
        private static readonly Func<object, bool> wbiCrazyModeUnlocked;
        private static readonly Func<object, object> wbiGetWarpDirection;
        private static readonly Action<object, int> wbiSetWarpDirection;

        // Hover mode control
        private static readonly Func<object, bool> wbiHoverControllerAvailable;
        private static readonly Func<object, bool> wbiGetHoverActive;
        private static readonly Func<object, bool> wbiEnginesAreActive;
        private static readonly Action<object> wbiStartEngines;
        private static readonly Action<object> wbiStopEngines;
        private static readonly Action<object> wbiToggleHover;
        private static readonly Func<object, float> wbiGetVerticalSpeed;
        private static readonly Action<object, float> wbiDecreaseVerticalSpeed;
        private static readonly Action<object, float> wbiIncreaseVerticalSpeed;
        private static readonly Action<object> wbiKillVerticalSpeed;

        // Rotation control
        private static readonly Func<object, bool> wbiRotationControllerAvailable;
        private static readonly Func<object, bool> wbiCanRotateMax;
        private static readonly Func<object, bool> wbiCanRotateMin;
        private static readonly Func<object, float> wbiGetMaxRotation;
        private static readonly Func<object, float> wbiGetMinRotation;
        private static readonly Func<object, float> wbiGetCurrentRotation;
        private static readonly Action<object> wbiRotateMax;
        private static readonly Action<object> wbiRotateMin;
        private static readonly Action<object> wbiRotateNeutral;
        private static readonly Action<object, float> wbiDecreaseRotationAngle;
        private static readonly Action<object, float> wbiIncreaseRotationAngle;

        // Thrust mode
        private static readonly Func<object, bool> wbiThrustControllerAvailable;
        private static readonly Func<object, object> wbiGetThrustMode;
        private static readonly Action<object> wbiSetForwardThrust;
        private static readonly Action<object> wbiSetReverseThrust;
        private static readonly Action<object> wbiSetVTOLThrust;

        private object vtolManager;
        private Vessel vessel;
        private MASVesselComputer vc;

        [MoonSharpHidden]
        internal void UpdateVessel(Vessel vessel, MASVesselComputer vc)
        {
            if (vessel != this.vessel)
            {
                this.vessel = vessel;

                if (vtolManager != null)
                {
                    wbiFindControllers(vtolManager, this.vessel);
                }
            }
            this.vc = vc;
        }

        private object GetVtolManager()
        {
            if (vtolManager == null && wbivtolInstalled)
            {
                vtolManager = UnityEngine.Object.FindObjectOfType(wbiVtolManager_t);
                if (vtolManager != null)
                {
                    wbiFindControllers(vtolManager, vessel);
                }
            }

            return vtolManager;
        }

        /// <summary>
        /// The WBI VTOL Manager has several features that it controls.  The VTOL
        /// Capabilities category provides a way to query the availability of those
        /// features on a given craft.
        /// </summary>
        #region VTOL Capabilities

        /// <summary>
        /// Indicates whether the Wild Blue Industries VTOL manager mod is available.
        /// </summary>
        /// <returns></returns>
        public double Available()
        {
            return (wbivtolInstalled) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if the current vessel supports Air Park mode.
        /// </summary>
        /// <returns></returns>
        public double HasAirPark()
        {
            object manager = GetVtolManager();
            if (manager != null)
            {
                return (wbiAirParkAvailable(manager)) ? 1.0 : 0.0;
            }
            return 0.0;
        }

        /// <summary>
        /// Returns 1 if the current vessel has WBI Crazy Mode components.
        /// </summary>
        /// <returns></returns>
        public double HasCrazyMode()
        {
            return (vc.moduleGraviticEngine != null && wbiCrazyModeUnlocked(vc.moduleGraviticEngine)) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if the current vessel has WBI Hover controller components.
        /// </summary>
        /// <returns></returns>
        public double HasHover()
        {
            object manager = GetVtolManager();
            if (manager != null)
            {
                return (wbiHoverControllerAvailable(manager)) ? 1.0 : 0.0;
            }
            return 0.0;
        }

        /// <summary>
        /// Returns 1 if at least one WBI Rotation Controller is installed.
        /// </summary>
        /// <returns></returns>
        public double HasRotationController()
        {
            object manager = GetVtolManager();
            if (manager != null)
            {
                return (wbiRotationControllerAvailable(manager)) ? 1.0 : 0.0;
            }
            return 0.0;
        }

        /// <summary>
        /// Returns 1 if the current vessel has WBI Thrust Vector components.
        /// </summary>
        /// <returns></returns>
        public double HasThrustVector()
        {
            object manager = GetVtolManager();
            if (manager != null)
            {
                return (wbiThrustControllerAvailable(manager)) ? 1.0 : 0.0;
            }
            return 0.0;
        }

        #endregion

        /// <summary>
        /// The VTOL Air Park category provides the interface to toggle and query the Air Park mode
        /// in vessels equipped with that capability.
        /// </summary>
        #region VTOL Air Park

        /// <summary>
        /// Returns 1 if the Air Park feature is active.  Returns 0 if it is inactive or unavailable.
        /// </summary>
        /// <returns></returns>
        public double GetParked()
        {
            object manager = GetVtolManager();
            if (manager != null && wbiAirParkAvailable(manager))
            {
                return wbiGetParkActive(manager) ? 1.0 : 0.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Toggle the Air Park feature.  Returns 1 if Air Park is now active, returns 0
        /// otherwise (including if Air Park is unavailable).
        /// </summary>
        /// <returns></returns>
        public double ToggleAirPark()
        {
            object manager = GetVtolManager();
            if (manager != null && wbiAirParkAvailable(manager))
            {
                wbiTogglePark(manager);
                return wbiGetParkActive(manager) ? 1.0 : 0.0;
            }
            return 0.0;
        }

        #endregion

        /// <summary>
        /// The VTOL Crazy Mode category provides the interface with the WBI Flying Saucer mod's
        /// Crazy Mode warp features.
        /// </summary>
        #region VTOL Crazy Mode

        /// <summary>
        /// Get the Crazy Mode Warp Direction as specified below:
        /// 
        /// * 0: Stop
        /// * 1: Forward
        /// * 2: Back
        /// * 3: Left
        /// * 4: Right
        /// * 5: Up
        /// * 6: Down
        /// </summary>
        /// <returns>The current crazy mode warp direction, or 0 if crazy mode is unavailable.</returns>
        public double GetWarpDirection()
        {
            if (vc.moduleGraviticEngine != null && wbiCrazyModeUnlocked(vc.moduleGraviticEngine))
            {
                object direction = wbiGetWarpDirection(vc.moduleGraviticEngine);
                return (double)(int)direction;
            }

            return 0.0;
        }

        /// <summary>
        /// Set the Crazy Mode Warp Direction as specified below:
        /// 
        /// * 0: Stop
        /// * 1: Forward
        /// * 2: Back
        /// * 3: Left
        /// * 4: Right
        /// * 5: Up
        /// * 6: Down
        /// </summary>
        /// <param name="direction">One of the values from the description.</param>
        /// <returns>1 if the mode was set, 0 if it could not be set.</returns>
        public double SetWarpDirection(double direction)
        {
            if (vc.moduleGraviticEngine != null && wbiCrazyModeUnlocked(vc.moduleGraviticEngine) && wbiSetWarpDirection != null)
            {
                int dir = (int)direction;
                if (dir >= 0 && dir <= 6)
                {
                    wbiSetWarpDirection(vc.moduleGraviticEngine, dir);
                    return 1.0;
                }
            }

            return 0.0;
        }
        #endregion

        /// <summary>
        /// The VTOL Hover category provides the interface for controlling engines that function as
        /// VTOL thrusters.  It allows MAS to query the current commanded vertical speed, as well
        /// as change it.  It also allows VTOL engines to be switched on or off separately from
        /// the main `fc.ToggleEnginesEnabled()` command.
        /// </summary>
        #region VTOL Hover

        /// <summary>
        /// Increase (positive amount) or decrease (negative amount) the
        /// commanded vertical speed in m/s.
        /// </summary>
        /// <param name="amount">The change in vertical speed in m/s.  Positive is up, negative is down.</param>
        /// <returns>1 if a change was commanded, 0 otherwise.</returns>
        public double ChangeVerticalSpeed(double amount)
        {
            object manager = GetVtolManager();
            if (manager != null)
            {
                if (!wbiHoverControllerAvailable(manager))
                {
                    return 0.0;
                }

                if (amount > 0.0)
                {
                    wbiIncreaseVerticalSpeed(manager, (float)amount);
                }
                else if (amount < 0.0)
                {
                    wbiDecreaseVerticalSpeed(manager, -(float)amount);
                }

                return 1.0;
            }
            return 0.0;
        }

        /// <summary>
        /// Returns 1 if the engines managed by the VTOL manager are active, or 0 if they are
        /// shut down, or if there are no managed engines.
        /// </summary>
        /// <returns>1 if engines are active, otherwise 0.</returns>
        public double GetEnginesActive()
        {
            object manager = GetVtolManager();
            if (manager != null)
            {
                return wbiEnginesAreActive(manager) ? 1.0 : 0.0;
            }
            return 0.0;
        }

        /// <summary>
        /// Returns 1 if the VTOL manager reports that this vessel is in hover mode.  Returns 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double GetHover()
        {
            object manager = GetVtolManager();
            if (manager != null)
            {
                return wbiGetHoverActive(manager) ? 1.0 : 0.0;
            }
            return 0.0;
        }

        /// <summary>
        /// Returns the commanded vertical speed in m/s.
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
        public double GetVerticalSpeed()
        {
            object manager = GetVtolManager();
            if (manager != null)
            {
                return wbiGetVerticalSpeed(manager);
            }
            return 0.0;
        }

        /// <summary>
        /// Set the commanded vertical speed to zero.
        /// </summary>
        /// <returns></returns>
        public double KillVerticalSpeed()
        {
            object manager = GetVtolManager();
            if (manager != null)
            {
                if (!wbiHoverControllerAvailable(manager))
                {
                    return 0.0;
                }
                wbiKillVerticalSpeed(manager);
                return 1.0;
            }
            return 0.0;
        }

        /// <summary>
        /// Toggles engines managed by the VTOL manager.
        /// 
        /// Note that these engines are also reported and controlled through the
        /// standard `fc` engine interface, so it is possible to switch them on
        /// and off through both.
        /// </summary>
        /// <returns>1 if engines toggled, 0 if there is no manager.</returns>
        public double ToggleEngines()
        {
            object manager = GetVtolManager();
            if (manager != null)
            {
                bool currentState = wbiEnginesAreActive(manager);
                if (currentState == true)
                {
                    wbiStopEngines(manager);
                }
                else
                {
                    wbiStartEngines(manager);
                }
                return 1.0;
            }
            return 0.0;
        }

        /// <summary>
        /// Toggles VTOL hover mode.
        /// </summary>
        /// <returns></returns>
        public double ToggleHover()
        {
            object manager = GetVtolManager();
            if (manager != null)
            {
                if (!wbiHoverControllerAvailable(manager))
                {
                    return 0.0;
                }
                wbiToggleHover(manager);
                return 1.0;
            }
            return 0.0;
        }

        #endregion

        /// <summary>
        /// The VTOL Rotation Controller category provides the interface for controlling engines
        /// that support configurable positions, such as tilt-rotor engines.
        /// </summary>
        #region VTOL Rotation Controller

        /// <summary>
        /// Instructs the rotation controller to adjust the position of the rotatable engines by
        /// the specified number of degrees.
        /// </summary>
        /// <param name="changeDegrees">The number of degrees to rotate the engines.  Positive rotates towards the up position,
        /// negative rotates towards the down position.</param>
        /// <returns>1 if a change was commanded, 0 otherwise.</returns>
        public double ChangeRotation(double changeDegrees)
        {
            object manager = GetVtolManager();
            if (manager != null)
            {
                if (changeDegrees > 0.0)
                {
                    wbiIncreaseRotationAngle(manager, (float)changeDegrees);
                    return 1.0;
                }
                else if (changeDegrees < 0.0)
                {
                    wbiDecreaseRotationAngle(manager, -(float)changeDegrees);
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Instructs the rotation controller to set engines to either full-up, full-down,
        /// or neutral, depending on `position`.
        /// 
        /// If `position` is zero, the engines are moved to their neutral positions.  If `position` is
        /// greater than zero, the engines are moved to their full-up position, as long as the engines
        /// support that rotation.  If `position` is negative and the engines support a full-down
        /// position, the engines will be moved to their full-down position.
        /// </summary>
        /// <param name="position">Either 0 (for neutral), a positive value (for full-up), or a negative value (for full-down).</param>
        /// <returns>1 if a rotation was successfully commanded, 0 otherwise.</returns>
        public double SetRotation(double position)
        {
            object manager = GetVtolManager();
            if (manager != null)
            {
                if (position > 0.0)
                {
                    if (wbiCanRotateMax(manager))
                    {
                        wbiRotateMax(manager);
                        return 0.0;
                    }
                }
                else if (position < 0.0)
                {
                    if (wbiCanRotateMin(manager))
                    {
                        wbiRotateMin(manager);
                        return 0.0;
                    }
                }
                else
                {
                    wbiRotateNeutral(manager);
                    return 1.0;
                }
            }
            return 0.0;
        }
        #endregion

        /// <summary>
        /// The VTOL Thrust Vector category provides methods to query and set the thrust
        /// mode of engines that may switch between VTOL and Forward thrust modes, as welll
        /// as potentially Reverse thrust.
        /// </summary>
        #region VTOL Thrust Vector

        /// <summary>
        /// Returns the current thrust mode for the VTOL engines.  If none are
        /// available, returns 1 (forward).
        /// 
        /// Valid return values:
        /// * -1: Reverse Thrust
        /// * 0: VTOL Thrust
        /// * 1: Forward Thrust
        /// </summary>
        /// <returns></returns>
        public double GetThrustMode()
        {
            object manager = GetVtolManager();
            if (manager != null)
            {
                object mode = wbiGetThrustMode(manager);

                switch ((int)mode)
                {
                    case 0:
                        return 1.0;
                    case 1:
                        return -1.0;
                    case 2:
                        return 0.0;
                }
            }
            return 1.0;
        }

        /// <summary>
        /// Set the VTOL manager thrust mode.  Valid settings are:
        /// * -1: Reverse Thrust
        /// * 0: VTOL Thrust
        /// * 1: Forward Thrust
        /// </summary>
        /// <param name="mode"></param>
        /// <returns>1 if the mode was changed, 0 if it was not</returns>
        public double SetThrustMode(double mode)
        {
            object manager = GetVtolManager();
            if (manager != null)
            {
                if (!wbiThrustControllerAvailable(manager))
                {
                    return 0.0;
                }

                object currentModeO = wbiGetThrustMode(manager);
                int currentMode = (int)currentModeO;
                int newMode = (int)mode;

                // currentMode is WBIThrustModes, which maps 0 = Forward, 1 = Reverse, 2 = VTOL
                // MAS thrust mode is 1 = Forward, -1 = Reverse, 0 = VTOL
                if (newMode == 1 && currentMode != 0)
                {
                    wbiSetForwardThrust(manager);
                    return 1.0;
                }
                else if (newMode == 0 && currentMode != 2)
                {
                    wbiSetVTOLThrust(manager);
                    return 1.0;
                }
                else if (newMode == -1 && currentMode != 1)
                {
                    wbiSetReverseThrust(manager);
                    return 1.0;
                }
            }
            return 0.0;
        }
        #endregion

        [MoonSharpHidden]
        static MASIVTOL()
        {
            wbiVtolManager_t = Utility.GetExportedType("KerbalActuators", "KerbalActuators.WBIVTOLManager");

            if (wbiVtolManager_t != null)
            {
                // Init
                MethodInfo FindControllers_t = wbiVtolManager_t.GetMethod("FindControllers", BindingFlags.Instance | BindingFlags.Public);
                if (FindControllers_t == null)
                {
                    Utility.LogStaticError("Didn't find FindControllers");
                    return;
                }
                wbiFindControllers = DynamicMethodFactory.CreateAction<object, Vessel>(FindControllers_t);

                // Capabilities
                MethodInfo AirParkControllerActive_t = wbiVtolManager_t.GetMethod("AirParkControllerActive", BindingFlags.Instance | BindingFlags.Public);
                if (AirParkControllerActive_t != null)
                {
                    wbiAirParkAvailable = DynamicMethodFactory.CreateFunc<object, bool>(AirParkControllerActive_t);
                }
                else
                {
                    Utility.LogStaticError("Didn't find AirParkControllerActive");
                    return;
                }
                MethodInfo HoverControllerActive_t = wbiVtolManager_t.GetMethod("HoverControllerActive", BindingFlags.Instance | BindingFlags.Public);
                if (HoverControllerActive_t != null)
                {
                    wbiHoverControllerAvailable = DynamicMethodFactory.CreateFunc<object, bool>(HoverControllerActive_t);
                }
                else
                {
                    Utility.LogStaticError("Didn't find HoverControllerActive");
                    return;
                }
                MethodInfo HasRotationControllers_t = wbiVtolManager_t.GetMethod("HasRotationControllers", BindingFlags.Instance | BindingFlags.Public);
                if (HasRotationControllers_t != null)
                {
                    wbiRotationControllerAvailable = DynamicMethodFactory.CreateFunc<object, bool>(HasRotationControllers_t);
                }
                else
                {
                    Utility.LogStaticError("Didn't find HasRotationControllers");
                    return;
                }
                MethodInfo ThrustVectorControllerActive_t = wbiVtolManager_t.GetMethod("ThrustVectorControllerActive", BindingFlags.Instance | BindingFlags.Public);
                if (ThrustVectorControllerActive_t != null)
                {
                    wbiThrustControllerAvailable = DynamicMethodFactory.CreateFunc<object, bool>(ThrustVectorControllerActive_t);
                }
                else
                {
                    Utility.LogStaticError("Didn't find ThrustVectorControllerActive");
                    return;
                }

                // Air Park
                MethodInfo IsParked_t = wbiVtolManager_t.GetMethod("IsParked", BindingFlags.Instance | BindingFlags.Public);
                if (IsParked_t == null)
                {
                    Utility.LogStaticError("Didn't find IsParked");
                    return;
                }
                wbiGetParkActive = DynamicMethodFactory.CreateFunc<object, bool>(IsParked_t);

                MethodInfo TogglePark_t = wbiVtolManager_t.GetMethod("TogglePark", BindingFlags.Instance | BindingFlags.Public);
                if (TogglePark_t == null)
                {
                    Utility.LogStaticError("Didn't find TogglePark");
                    return;
                }
                wbiTogglePark = DynamicMethodFactory.CreateAction<object>(TogglePark_t);

                // Hover
                MethodInfo EnginesAreActive_t = wbiVtolManager_t.GetMethod("EnginesAreActive", BindingFlags.Instance | BindingFlags.Public);
                if (EnginesAreActive_t == null)
                {
                    Utility.LogStaticError("Didn't find EnginesAreActive");
                    return;
                }
                wbiEnginesAreActive = DynamicMethodFactory.CreateFunc<object, bool>(EnginesAreActive_t);

                MethodInfo StartEngines_t = wbiVtolManager_t.GetMethod("StartEngines", BindingFlags.Instance | BindingFlags.Public);
                if (StartEngines_t == null)
                {
                    Utility.LogStaticError("Didn't find StartEngines");
                    return;
                }
                wbiStartEngines = DynamicMethodFactory.CreateAction<object>(StartEngines_t);

                MethodInfo StopEngines_t = wbiVtolManager_t.GetMethod("StopEngines", BindingFlags.Instance | BindingFlags.Public);
                if (StopEngines_t == null)
                {
                    Utility.LogStaticError("Didn't find StopEngines");
                    return;
                }
                wbiStopEngines = DynamicMethodFactory.CreateAction<object>(StopEngines_t);

                FieldInfo HoverActive_t = wbiVtolManager_t.GetField("hoverActive", BindingFlags.Instance | BindingFlags.Public);
                if (HoverActive_t == null)
                {
                    Utility.LogStaticError("Didn't find hoverActive");
                    return;
                }
                wbiGetHoverActive = DynamicMethodFactory.CreateGetField<object, bool>(HoverActive_t);

                MethodInfo ToggleHover_t = wbiVtolManager_t.GetMethod("ToggleHover", BindingFlags.Instance | BindingFlags.Public);
                if (ToggleHover_t == null)
                {
                    Utility.LogStaticError("Didn't find ToggleHover");
                    return;
                }
                wbiToggleHover = DynamicMethodFactory.CreateAction<object>(ToggleHover_t);

                // Rotation
                MethodInfo CanRotateMax_t = wbiVtolManager_t.GetMethod("CanRotateMax", BindingFlags.Instance | BindingFlags.Public);
                if (CanRotateMax_t == null)
                {
                    Utility.LogStaticError("Didn't find CanRotateMax");
                    return;
                }
                wbiCanRotateMax = DynamicMethodFactory.CreateFunc<object, bool>(CanRotateMax_t);

                MethodInfo CanRotateMin_t = wbiVtolManager_t.GetMethod("CanRotateMin", BindingFlags.Instance | BindingFlags.Public);
                if (CanRotateMin_t == null)
                {
                    Utility.LogStaticError("Didn't find CanRotateMin");
                    return;
                }
                wbiCanRotateMin = DynamicMethodFactory.CreateFunc<object, bool>(CanRotateMin_t);

                MethodInfo RotateToMax_t = wbiVtolManager_t.GetMethod("RotateToMax", BindingFlags.Instance | BindingFlags.Public);
                if (RotateToMax_t == null)
                {
                    Utility.LogStaticError("Didn't find RotateToMax");
                    return;
                }
                wbiRotateMax = DynamicMethodFactory.CreateAction<object>(RotateToMax_t);

                MethodInfo RotateToMin_t = wbiVtolManager_t.GetMethod("RotateToMin", BindingFlags.Instance | BindingFlags.Public);
                if (RotateToMin_t == null)
                {
                    Utility.LogStaticError("Didn't find RotateToMin");
                    return;
                }
                wbiRotateMin = DynamicMethodFactory.CreateAction<object>(RotateToMin_t);

                MethodInfo RotateToNeutral_t = wbiVtolManager_t.GetMethod("RotateToNeutral", BindingFlags.Instance | BindingFlags.Public);
                if (ToggleHover_t == null)
                {
                    Utility.LogStaticError("Didn't find RotateToNeutral");
                    return;
                }
                wbiRotateNeutral = DynamicMethodFactory.CreateAction<object>(RotateToNeutral_t);

                MethodInfo IncreaseRotationAngle_t = wbiVtolManager_t.GetMethod("IncreaseRotationAngle", BindingFlags.Instance | BindingFlags.Public);
                if (IncreaseRotationAngle_t == null)
                {
                    Utility.LogStaticError("Didn't find IncreaseRotationAngle");
                    return;
                }
                wbiIncreaseRotationAngle = DynamicMethodFactory.CreateAction<object, float>(IncreaseRotationAngle_t);

                MethodInfo DecreaseRotationAngle_t = wbiVtolManager_t.GetMethod("DecreaseRotationAngle", BindingFlags.Instance | BindingFlags.Public);
                if (DecreaseRotationAngle_t == null)
                {
                    Utility.LogStaticError("Didn't find DecreaseRotationAngle");
                    return;
                }
                wbiDecreaseRotationAngle = DynamicMethodFactory.CreateAction<object, float>(DecreaseRotationAngle_t);

                MethodInfo GetMaxRotation_t = wbiVtolManager_t.GetMethod("GetMaxRotation", BindingFlags.Instance | BindingFlags.Public);
                if (GetMaxRotation_t == null)
                {
                    Utility.LogStaticError("Didn't find GetMaxRotation");
                    return;
                }
                wbiGetMaxRotation = DynamicMethodFactory.CreateFunc<object, float>(GetMaxRotation_t);

                MethodInfo GetMinRotation_t = wbiVtolManager_t.GetMethod("GetMinRotation", BindingFlags.Instance | BindingFlags.Public);
                if (GetMinRotation_t == null)
                {
                    Utility.LogStaticError("Didn't find GetMinRotation");
                    return;
                }
                wbiGetMinRotation = DynamicMethodFactory.CreateFunc<object, float>(GetMinRotation_t);

                MethodInfo GetCurrentRotation_t = wbiVtolManager_t.GetMethod("GetCurrentRotation", BindingFlags.Instance | BindingFlags.Public);
                if (GetCurrentRotation_t == null)
                {
                    Utility.LogStaticError("Didn't find GetCurrentRotation");
                    return;
                }
                wbiGetCurrentRotation = DynamicMethodFactory.CreateFunc<object, float>(GetCurrentRotation_t);

                // VSpd
                MethodInfo KillVerticalSpeed_t = wbiVtolManager_t.GetMethod("KillVerticalSpeed", BindingFlags.Instance | BindingFlags.Public);
                if (KillVerticalSpeed_t == null)
                {
                    Utility.LogStaticError("Didn't find KillVerticalSpeed");
                    return;
                }
                wbiKillVerticalSpeed = DynamicMethodFactory.CreateAction<object>(KillVerticalSpeed_t);

                MethodInfo IncreaseVerticalSpeed_t = wbiVtolManager_t.GetMethod("IncreaseVerticalSpeed", BindingFlags.Instance | BindingFlags.Public);
                if (IncreaseVerticalSpeed_t == null)
                {
                    Utility.LogStaticError("Didn't find IncreaseVerticalSpeed");
                    return;
                }
                wbiIncreaseVerticalSpeed = DynamicMethodFactory.CreateAction<object, float>(IncreaseVerticalSpeed_t);

                MethodInfo DecreaseVerticalSpeed_t = wbiVtolManager_t.GetMethod("DecreaseVerticalSpeed", BindingFlags.Instance | BindingFlags.Public);
                if (DecreaseVerticalSpeed_t == null)
                {
                    Utility.LogStaticError("Didn't find DecreaseVerticalSpeed");
                    return;
                }
                wbiDecreaseVerticalSpeed = DynamicMethodFactory.CreateAction<object, float>(DecreaseVerticalSpeed_t);

                // Thrust mode
                FieldInfo ThrustMode_t = wbiVtolManager_t.GetField("thrustMode", BindingFlags.Instance | BindingFlags.Public);
                if (ThrustMode_t == null)
                {
                    Utility.LogStaticError("Didn't find thrustMode");
                    return;
                }
                wbiGetThrustMode = DynamicMethodFactory.CreateGetField<object, object>(ThrustMode_t);

                MethodInfo SetForwardThrust_t = wbiVtolManager_t.GetMethod("SetForwardThrust", BindingFlags.Instance | BindingFlags.Public);
                if (SetForwardThrust_t == null)
                {
                    Utility.LogStaticError("Didn't find SetForwardThrust");
                    return;
                }
                wbiSetForwardThrust = DynamicMethodFactory.CreateAction<object>(SetForwardThrust_t);

                MethodInfo SetReverseThrust_t = wbiVtolManager_t.GetMethod("SetReverseThrust", BindingFlags.Instance | BindingFlags.Public);
                if (SetReverseThrust_t == null)
                {
                    Utility.LogStaticError("Didn't find SetReverseThrust");
                    return;
                }
                wbiSetReverseThrust = DynamicMethodFactory.CreateAction<object>(SetReverseThrust_t);

                MethodInfo SetVTOLThrust_t = wbiVtolManager_t.GetMethod("SetVTOLThrust", BindingFlags.Instance | BindingFlags.Public);
                if (SetVTOLThrust_t == null)
                {
                    Utility.LogStaticError("Didn't find SetVTOLThrust");
                    return;
                }
                wbiSetVTOLThrust = DynamicMethodFactory.CreateAction<object>(SetVTOLThrust_t);

                FieldInfo VerticalSpeed_t = wbiVtolManager_t.GetField("verticalSpeed", BindingFlags.Instance | BindingFlags.Public);
                if (VerticalSpeed_t == null)
                {
                    Utility.LogStaticError("Didn't find verticalSpeed");
                    return;
                }
                wbiGetVerticalSpeed = DynamicMethodFactory.CreateGetField<object, float>(VerticalSpeed_t);

                wbiWBIGraviticEngine_t = Utility.GetExportedType("FlyingSaucers", "WildBlueIndustries.WBIGraviticEngine");
                if (wbiWBIGraviticEngine_t != null)
                {
                    MethodInfo IsCrazyModeUnlocked_t = wbiWBIGraviticEngine_t.GetMethod("IsCrazyModeUnlocked", BindingFlags.Instance | BindingFlags.Public);
                    if (IsCrazyModeUnlocked_t == null)
                    {
                        Utility.LogStaticError("Didn't find IsCrazyModeUnlocked");
                        return;
                    }
                    wbiCrazyModeUnlocked = DynamicMethodFactory.CreateFunc<object, bool>(IsCrazyModeUnlocked_t);

                    MethodInfo IsActive_t = wbiWBIGraviticEngine_t.GetMethod("IsActive", BindingFlags.Instance | BindingFlags.Public);
                    if (IsActive_t == null)
                    {
                        Utility.LogStaticError("Didn't find IsActive");
                        return;
                    }
                    wbiCrazyModeIsActive = DynamicMethodFactory.CreateFunc<object, bool>(IsActive_t);

                    MethodInfo GetWarpDirection_t = wbiWBIGraviticEngine_t.GetMethod("GetWarpDirection", BindingFlags.Instance | BindingFlags.Public);
                    if (GetWarpDirection_t == null)
                    {
                        Utility.LogStaticError("Didn't find GetWarpDirection");
                        return;
                    }
                    wbiGetWarpDirection = DynamicMethodFactory.CreateFunc<object, object>(GetWarpDirection_t);

                    MethodInfo SetWarpDirection_t = wbiWBIGraviticEngine_t.GetMethod("SetWarpDirection", BindingFlags.Instance | BindingFlags.Public);
                    if (SetWarpDirection_t == null)
                    {
                        Utility.LogStaticError("Didn't find SetWarpDirection");
                        return;
                    }
                    wbiSetWarpDirection = DynamicMethodFactory.CreateAction<object, int>(SetWarpDirection_t);
                }

                wbivtolInstalled = true;
            }
        }
    }
}
