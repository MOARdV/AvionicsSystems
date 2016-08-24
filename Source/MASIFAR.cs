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
    /// <summary>
    /// MASIFAR is the MAS interface to the Ferram Aerospace Research mod.  It
    /// is a MoonSharp proxy class.
    /// 
    /// </summary>
    /// <LuaName>far</LuaName>
    /// <mdDoc>
    /// MASIFAR is the AvionicsSystems interface with Ferram Aerospace Research (FAR).
    /// </mdDoc>
    internal class MASIFAR
    {
        internal static readonly bool farFound;

        private static readonly Func<Vessel, double> VesselCoeffLift;
        private static readonly Action<Vessel> VesselDecreaseFlapDeflection;
        private static readonly Func<Vessel, double> VesselDynPress;
        private static readonly Func<Vessel, int> VesselFlapSetting;
        private static readonly Action<Vessel> VesselIncreaseFlapDeflection;
        private static readonly Func<Vessel, double> VesselRefArea;
        private static readonly Action<Vessel, bool> VesselSetSpoilers;
        private static readonly Func<Vessel, bool> VesselSpoilerSetting;
        private static readonly Func<Vessel, double> VesselStallFrac;

        internal Vessel vessel;

        private int flapSetting;
        private bool spoilerSetting;

        [MoonSharpHidden]
        public MASIFAR(Vessel vessel)
        {
            this.vessel = vessel;
        }

        ~MASIFAR()
        {
            vessel = null;
        }

        [MoonSharpHidden]
        internal void Update()
        {
            if (farFound)
            {
                flapSetting = VesselFlapSetting(vessel);
                spoilerSetting = VesselSpoilerSetting(vessel);
            }
        }

        /// <summary>
        /// Return the coefficient of lift for this vessel.
        /// </summary>
        /// <returns></returns>
        public double CoeffLift()
        {
            if (farFound)
            {
                return VesselCoeffLift(vessel);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Reduce flap setting one step (if possible)
        /// </summary>
        public void DecreaseFlapSetting()
        {
            if (farFound && flapSetting > 0)
            {
                VesselDecreaseFlapDeflection(vessel);
            }
        }

        /// <summary>
        /// Return the current dynamic pressure in kPa.
        /// </summary>
        /// <returns></returns>
        public double DynamicPressure()
        {
            if (farFound)
            {
                return VesselDynPress(vessel);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the current flap setting for the vessel.  Valid values
        /// are from 0 to 3.
        /// </summary>
        /// <returns></returns>
        public double GetFlapSetting()
        {
            if (farFound)
            {
                return Math.Max(flapSetting, 0);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if spoilers are deployed, 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double GetSpoilerSetting()
        {
            if (farFound)
            {
                return (spoilerSetting) ? 1.0 : 0.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Increase flap setting one step (if possible)
        /// </summary>
        public void IncreaseFlapSetting()
        {
            if (farFound && flapSetting < 3)
            {
                VesselIncreaseFlapDeflection(vessel);
            }
        }

        /// <summary>
        /// Return the RefArea for this vessel.
        /// </summary>
        /// <returns></returns>
        public double RefArea()
        {
            if (farFound)
            {
                return VesselRefArea(vessel);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Set the spoilers to on/off (true/false)
        /// </summary>
        /// <param name="newState"></param>
        public void SetSpoilers(bool newState)
        {
            if (farFound && newState != spoilerSetting)
            {
                VesselSetSpoilers(vessel, newState);
            }
        }

        /// <summary>
        /// Returns the stall fraction for this vessel.
        /// </summary>
        /// <returns></returns>
        public double StallFraction()
        {
            if (farFound)
            {
                return VesselStallFrac(vessel);
            }
            else
            {
                return 0.0;
            }
        }

        #region Reflection Configuration
        static MASIFAR()
        {
            farFound = false;
            Type farAPI_t = Utility.GetExportedType("FerramAerospaceResearch", "FerramAerospaceResearch.FARAPI");
            if (farAPI_t != null)
            {
                MethodInfo coeffLift_t = farAPI_t.GetMethod("VesselLiftCoeff", BindingFlags.Static | BindingFlags.Public);
                if (coeffLift_t == null)
                {
                    Utility.LogErrorMessage("Failed to find 'VesselLiftCoeff' in FAR");
                    return;
                }
                VesselCoeffLift = (Func<Vessel, double>)Delegate.CreateDelegate(typeof(Func<Vessel, double>), coeffLift_t);

                MethodInfo dynPress_t = farAPI_t.GetMethod("VesselDynPres", BindingFlags.Static | BindingFlags.Public);
                if (dynPress_t == null)
                {
                    Utility.LogErrorMessage("Failed to find 'VesselDynPres' in FAR");
                    return;
                }
                VesselDynPress = (Func<Vessel, double>)Delegate.CreateDelegate(typeof(Func<Vessel, double>), dynPress_t);

                MethodInfo refArea_t = farAPI_t.GetMethod("VesselRefArea", BindingFlags.Static | BindingFlags.Public);
                if (refArea_t == null)
                {
                    Utility.LogErrorMessage("Failed to find 'VesselRefArea' in FAR");
                    return;
                }
                VesselRefArea = (Func<Vessel, double>)Delegate.CreateDelegate(typeof(Func<Vessel, double>), refArea_t);

                MethodInfo stallFrac_t = farAPI_t.GetMethod("VesselStallFrac", BindingFlags.Static | BindingFlags.Public);
                if (stallFrac_t == null)
                {
                    Utility.LogErrorMessage("Failed to find 'VesselStallFrac' in FAR");
                    return;
                }
                VesselStallFrac = (Func<Vessel, double>)Delegate.CreateDelegate(typeof(Func<Vessel, double>), stallFrac_t);

                MethodInfo flapSetting_t = farAPI_t.GetMethod("VesselFlapSetting", BindingFlags.Static | BindingFlags.Public);
                if (flapSetting_t == null)
                {
                    Utility.LogErrorMessage("Failed to find 'VesselFlapSetting' in FAR");
                    return;
                }
                VesselFlapSetting = (Func<Vessel, int>)Delegate.CreateDelegate(typeof(Func<Vessel, int>), flapSetting_t);

                MethodInfo decreaseFlapSetting_t = farAPI_t.GetMethod("VesselDecreaseFlapDeflection", BindingFlags.Static | BindingFlags.Public);
                if (decreaseFlapSetting_t == null)
                {
                    Utility.LogErrorMessage("Failed to find 'VesselDecreaseFlapDeflection' in FAR");
                    return;
                }
                VesselDecreaseFlapDeflection = (Action<Vessel>)Delegate.CreateDelegate(typeof(Action<Vessel>), decreaseFlapSetting_t);

                MethodInfo increaseFlapSetting_t = farAPI_t.GetMethod("VesselIncreaseFlapDeflection", BindingFlags.Static | BindingFlags.Public);
                if (increaseFlapSetting_t == null)
                {
                    Utility.LogErrorMessage("Failed to find 'VesselIncreaseFlapDeflection' in FAR");
                    return;
                }
                VesselIncreaseFlapDeflection = (Action<Vessel>)Delegate.CreateDelegate(typeof(Action<Vessel>), increaseFlapSetting_t);

                MethodInfo spoilerSetting_t = farAPI_t.GetMethod("VesselSpoilerSetting", BindingFlags.Static | BindingFlags.Public);
                if (spoilerSetting_t == null)
                {
                    Utility.LogErrorMessage("Failed to find 'VesselSpoilerSetting' in FAR");
                    return;
                }
                VesselSpoilerSetting = (Func<Vessel, bool>)Delegate.CreateDelegate(typeof(Func<Vessel, bool>), spoilerSetting_t);

                MethodInfo setSpoiler_t = farAPI_t.GetMethod("VesselSetSpoilers", BindingFlags.Static | BindingFlags.Public);
                if (setSpoiler_t == null)
                {
                    Utility.LogErrorMessage("Failed to find 'VesselSetSpoilers' in FAR");
                    return;
                }
                VesselSetSpoilers = (Action<Vessel, bool>)Delegate.CreateDelegate(typeof(Action<Vessel, bool>), setSpoiler_t);
                
                
                farFound = true;
            }
        }
        #endregion
    }
}
