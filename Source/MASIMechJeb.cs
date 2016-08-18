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
using System.Reflection;
using System.Text;

namespace AvionicsSystems
{
    internal class MASIMechJeb
    {
        private static readonly bool mjFound;

        //--- Methods found in MechJebCore
        private static readonly Type mjCore_t;

        //--- Methods found in ComputerModule
        private static readonly DynamicMethodBool<object> ModuleEnabled;

        //--- Methods found in EditableDoubleMult
        private static readonly DynamicMethod<object, double> setEditableDoubleMult;
        private static readonly DynamicMethodDouble<object> getEditableDoubleMult;

        //--- Methods found in VesselExtensions
        private static readonly Type mjVesselExtensions_t;
        private static readonly DynamicMethod<Vessel> GetMasterMechJeb;
        private static readonly DynamicMethod<object, string> GetComputerModule;

        //--- Methods found in MechJebModuleAscentAutopilot
        private static readonly FieldInfo desiredOrbitAltitude_t;

        //--- Methods found in MechJebModuleSmartASS
        private static readonly FieldInfo saTarget_t;
        internal static readonly string[] modeNames;

        //--- Instance variables
        bool mjAvailable;

        object masterMechJeb;
        object ascentAP;
        object smartAss;

        private SATarget saTarget;

        #region MechJeb Enum Imports
        public enum SATarget
        {
            OFF = 0,
            KILLROT = 1,
            NODE = 2,
            SURFACE = 3,
            PROGRADE = 4,
            RETROGRADE = 5,
            NORMAL_PLUS = 6,
            NORMAL_MINUS = 7,
            RADIAL_PLUS = 8,
            RADIAL_MINUS = 9,
            RELATIVE_PLUS = 10,
            RELATIVE_MINUS = 11,
            TARGET_PLUS = 12,
            TARGET_MINUS = 13,
            PARALLEL_PLUS = 14,
            PARALLEL_MINUS = 15,
            ADVANCED = 16,
            AUTO = 17,
            SURFACE_PROGRADE = 18,
            SURFACE_RETROGRADE = 19,
            HORIZONTAL_PLUS = 20,
            HORIZONTAL_MINUS = 21,
            VERTICAL_PLUS = 22,
        }
        static private Dictionary<int, SATarget> saTargetMap = new Dictionary<int, SATarget>
        {
            { 0, SATarget.OFF},
            { 1, SATarget.KILLROT },
            {2,SATarget.NODE },
            {3,SATarget.SURFACE },
            {4,SATarget.PROGRADE },
            {5,SATarget.RETROGRADE },
            {6,SATarget.NORMAL_PLUS },
            {7,SATarget.NORMAL_MINUS},
            {8,SATarget.RADIAL_PLUS},
            {9,SATarget.RADIAL_MINUS },
            {10,SATarget.RELATIVE_PLUS },
            {11,SATarget.RELATIVE_MINUS},
            {12,SATarget.TARGET_PLUS },
            {13,SATarget.TARGET_MINUS },
            {14,SATarget.PARALLEL_PLUS },
            {15,SATarget.PARALLEL_MINUS },
            {16,SATarget.ADVANCED},
            {17,SATarget.AUTO },
            {18,SATarget.SURFACE_PROGRADE},
            {19,SATarget.SURFACE_RETROGRADE},
            {20,SATarget.HORIZONTAL_PLUS },
            {21,SATarget.HORIZONTAL_MINUS},
            {22,SATarget.VERTICAL_PLUS }
        };
        #endregion

        [MoonSharpHidden]
        public MASIMechJeb(Vessel vessel)
        {
            if (mjFound)
            {
                UpdateVessel(vessel);
            }
            else
            {
                mjAvailable = false;
            }
        }

        ~MASIMechJeb()
        {
        }

        /// <summary>
        /// Returns 1 if any of the autopilots MAS can control are active.
        /// </summary>
        /// <returns></returns>
        public double AutopilotActive()
        {
            if (mjAvailable && (ModuleEnabled(ascentAP)))
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if MechJeb is installed and available on this craft.
        /// </summary>
        /// <returns></returns>
        public double Available()
        {
            return (mjAvailable) ? 1.0 : 0.0;
        }

        #region AscentAutopilot
        /// <summary>
        /// Returns the MJ Ascent Autopilot's targeted launch altitude in meters.
        /// </summary>
        /// <returns></returns>
        public double GetDesiredLaunchAltitude()
        {
            double desiredAltitude = 0.0;
            if (mjAvailable)
            {
                object desiredAlt = desiredOrbitAltitude_t.GetValue(ascentAP);
                if (desiredAlt != null)
                {
                    desiredAltitude = getEditableDoubleMult(desiredAlt);
                }
            }

            return desiredAltitude;
        }

        /// <summary>
        /// Returns the MJ Ascent Autopilot's targeted orbital inclination in degrees.
        /// </summary>
        /// <returns></returns>
        public double GetDesiredLaunchInclination()
        {
            return 0.0;
        }

        public void SetDesiredLaunchAltitude(double altitude)
        { 
        }
        public void SetDesiredLaunchInclination(double inclination)
        {
        }
        #endregion

        #region SmartASS
        /// <summary>
        /// Returns the number of the currently active SASS mode, or zero if MechJeb
        /// is unavailable.
        /// 
        /// OFF = 0,
        /// KILLROT = 1,
        /// NODE = 2,
        /// SURFACE = 3,
        /// PROGRADE = 4,
        /// RETROGRADE = 5,
        /// NORMAL_PLUS = 6,
        /// NORMAL_MINUS = 7,
        /// RADIAL_PLUS = 8,
        /// RADIAL_MINUS = 9,
        /// RELATIVE_PLUS = 10,
        /// RELATIVE_MINUS = 11,
        /// TARGET_PLUS = 12,
        /// TARGET_MINUS = 13,
        /// PARALLEL_PLUS = 14,
        /// PARALLEL_MINUS = 15,
        /// ADVANCED = 16,
        /// AUTO = 17,
        /// SURFACE_PROGRADE = 18,
        /// SURFACE_RETROGRADE = 19,
        /// HORIZONTAL_PLUS = 20,
        /// HORIZONTAL_MINUS = 21,
        /// VERTICAL_PLUS = 22,
        /// </summary>
        /// <returns></returns>
        public double GetSASSMode()
        {
            if (mjAvailable)
            {
                return (double)(int)saTarget;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if the current SASS mode matches the listed value.
        /// 
        /// OFF = 0,
        /// KILLROT = 1,
        /// NODE = 2,
        /// SURFACE = 3,
        /// PROGRADE = 4,
        /// RETROGRADE = 5,
        /// NORMAL_PLUS = 6,
        /// NORMAL_MINUS = 7,
        /// RADIAL_PLUS = 8,
        /// RADIAL_MINUS = 9,
        /// RELATIVE_PLUS = 10,
        /// RELATIVE_MINUS = 11,
        /// TARGET_PLUS = 12,
        /// TARGET_MINUS = 13,
        /// PARALLEL_PLUS = 14,
        /// PARALLEL_MINUS = 15,
        /// ADVANCED = 16,
        /// AUTO = 17,
        /// SURFACE_PROGRADE = 18,
        /// SURFACE_RETROGRADE = 19,
        /// HORIZONTAL_PLUS = 20,
        /// HORIZONTAL_MINUS = 21,
        /// VERTICAL_PLUS = 22,
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        public double GetSASSModeActive(double mode)
        {
            int mode_i = (int)mode;
            if(mjAvailable && saTargetMap.ContainsKey(mode_i) && saTarget == saTargetMap[mode_i])
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns true if SmartASS is off
        /// </summary>
        /// <returns></returns>
        public double SASSOff()
        {
            if (mjAvailable && saTarget == SATarget.OFF)
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Set the SmartASS pilot to the specified mode.
        /// </summary>
        /// <param name="mode"></param>
        public void SetSASSMode(double mode)
        {
            int mode_i = (int)mode;
            if (mjAvailable && saTargetMap.ContainsKey(mode_i))
            {
                saTarget_t.SetValue(smartAss, mode_i);

                //EnagageSmartASS(smartAss, true);
            }
        }
        #endregion

        /// <summary>
        /// Do all of the internal processing we need to do per FixedUpdate.
        /// </summary>
        [MoonSharpHidden]
        internal void Update()
        {
            if (mjAvailable)
            {
                object activeSATarget = saTarget_t.GetValue(smartAss);
                saTarget = saTargetMap[(int)activeSATarget];
            }
        }

        /// <summary>
        /// Vessel changed - we need to re-establish the master MechJeb and our
        /// delegates.
        /// </summary>
        /// <param name="vessel"></param>
        [MoonSharpHidden]
        internal void UpdateVessel(Vessel vessel)
        {
            if (mjFound)
            {
                try
                {
                    masterMechJeb = GetMasterMechJeb(vessel);

                    if (masterMechJeb != null)
                    {
                        smartAss = GetComputerModule(masterMechJeb, "MechJebModuleSmartASS");
                        if (smartAss == null)
                        {
                            throw new Exception("MASIMechJeb: Failed to get SmartASS MJ module");
                        }

                        ascentAP = GetComputerModule(masterMechJeb, "MechJebModuleAscentAutopilot");
                        if (ascentAP == null)
                        {
                            throw new Exception("MASIMechJeb: Failed to get Ascent Autopilot MJ module");
                        }
                    }

                    mjAvailable = (masterMechJeb != null);
                }
                catch (Exception e)
                {
                    Utility.LogErrorMessage(this, "mechjeb.UpdateVessel threw exception: {0}", e);
                    mjAvailable = false;
                }
            }
        }

        #region Reflection Configuration
        static MASIMechJeb()
        {
            mjFound = false;
            try
            {
                mjCore_t = Utility.GetExportedType("MechJeb2", "MuMech.MechJebCore");
                if (mjCore_t == null)
                {
                    return;
                }
                mjVesselExtensions_t = Utility.GetExportedType("MechJeb2", "MuMech.VesselExtensions");
                if (mjVesselExtensions_t == null)
                {
                    return;
                }
                Type mjComputerModule_t = Utility.GetExportedType("MechJeb2", "MuMech.ComputerModule");
                if (mjComputerModule_t == null)
                {
                    return;
                }
                Type mjEditableDoubleMult_t = Utility.GetExportedType("MechJeb2", "MuMech.EditableDoubleMult");
                if (mjEditableDoubleMult_t == null)
                {
                    return;
                }
                Type mjModuleSmartass_t = Utility.GetExportedType("MechJeb2", "MuMech.MechJebModuleSmartASS");
                if (mjModuleSmartass_t == null)
                {
                    return;
                }
                Type mjModuleAscentAP_t = Utility.GetExportedType("MechJeb2", "MuMech.MechJebModuleAscentAutopilot");
                if (mjModuleAscentAP_t == null)
                {
                    return;
                }

                //--- MechJebCore
                MethodInfo GetComputerModule_t = mjCore_t.GetMethod("GetComputerModule", new Type[] { typeof(string) });
                if (GetComputerModule_t == null)
                {
                    return;
                }
                GetComputerModule = DynamicMethodFactory.CreateFunc<object, string>(GetComputerModule_t);
                if (GetComputerModule == null)
                {
                    return;
                }

                //--- ComputerModule
                PropertyInfo mjModuleEnabledProperty = mjComputerModule_t.GetProperty("enabled", BindingFlags.Instance | BindingFlags.Public);
                MethodInfo mjModuleEnabled = null;
                if (mjModuleEnabledProperty != null)
                {
                    mjModuleEnabled = mjModuleEnabledProperty.GetGetMethod();
                }
                if (mjModuleEnabled == null)
                {
                    return;
                }
                ModuleEnabled = DynamicMethodFactory.CreateFuncBool<object>(mjModuleEnabled);

                //--- EditableDoubleMult
                PropertyInfo edmVal = mjEditableDoubleMult_t.GetProperty("val");
                if (edmVal == null)
                {
                    throw new NotImplementedException("edmVal");
                }
                // getEditableDoubleMult
                MethodInfo mjGetEDM = edmVal.GetGetMethod();
                if (mjGetEDM != null)
                {
                    getEditableDoubleMult = DynamicMethodFactory.CreateFuncDouble<object>(mjGetEDM);
                }
                // setEditableDoubleMult
                MethodInfo mjSetEDM = edmVal.GetSetMethod();
                if (mjSetEDM != null)
                {
                    setEditableDoubleMult = DynamicMethodFactory.CreateFunc<object, double>(mjSetEDM);
                }

                //--- ModuleAscentAutoPilot
                desiredOrbitAltitude_t = mjModuleAscentAP_t.GetField("desiredOrbitAltitude");
                if (desiredOrbitAltitude_t == null)
                {
                    return;
                }

                //--- ModuleSmartASS
                saTarget_t = mjModuleSmartass_t.GetField("target", BindingFlags.Instance | BindingFlags.Public);
                if (saTarget_t == null)
                {
                    return;
                }
                FieldInfo modeTexts_t = mjModuleSmartass_t.GetField("ModeTexts", BindingFlags.Static | BindingFlags.Public);
                modeNames = (string[])modeTexts_t.GetValue(null);

                //--- VesselExtensions
                MethodInfo GetMasterMechJeb_t = mjVesselExtensions_t.GetMethod("GetMasterMechJeb", BindingFlags.Static | BindingFlags.Public);
                if (GetMasterMechJeb_t == null)
                {
                    return;
                }
                GetMasterMechJeb = DynamicMethodFactory.CreateFunc<Vessel>(GetMasterMechJeb_t);
                if (GetMasterMechJeb == null)
                {
                    return;
                }

                mjFound = true;
            }
            catch (Exception e)
            {
                Utility.LogErrorMessage("MJ static ctor exception {0}", e);
            }
        }
        #endregion
    }
}
