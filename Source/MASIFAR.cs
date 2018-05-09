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

        private static readonly Func<Vessel, double> VesselAoA;
        private static readonly Func<Vessel, double> VesselCoeffBallistic;
        private static readonly Func<Vessel, double> VesselCoeffLift;
        private static readonly Func<Vessel, double> VesselCoeffDrag;
        private static readonly Action<Vessel> VesselDecreaseFlapDeflection;
        private static readonly Func<Vessel, double> VesselDynPress;
        private static readonly Func<Vessel, int> VesselFlapSetting;
        private static readonly Action<Vessel> VesselIncreaseFlapDeflection;
        private static readonly Func<Vessel, object> VesselFlightInfo;
        private static readonly Func<Vessel, double> VesselRefArea;
        private static readonly Action<Vessel, bool> VesselSetSpoilers;
        private static readonly Func<Vessel, double> VesselSideSlip;
        private static readonly Func<Vessel, double> VesselSpecFuelConsumption;
        private static readonly Func<Vessel, bool> VesselSpoilerSetting;
        private static readonly Func<Vessel, double> VesselStallFrac;
        private static readonly Func<Vessel, double> VesselTerminalVelocity;
        
        private static readonly Func<object, object> GetInfoParameters;
        private static readonly Func<object, double> GetDragForce;
        private static readonly Func<object, double> GetLiftForce;

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

        private static object GetVesselFlightInfo(Vessel v)
        {
            if (farFound)
            {
                object flightGUI = VesselFlightInfo(v);
                if (flightGUI != null)
                {
                    return GetInfoParameters(flightGUI);
                }
            }

            return null;
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
        /// Returns the vessel's angle of attack.
        /// </summary>
        /// <returns>Angle of attack in degrees.</returns>
        public double AngleOfAttack()
        {
            if (farFound)
            {
                return VesselAoA(vessel);
            }
            else
            {
                return 0.0;
            }
        }

        [MASProxyAttribute(Immutable = true)]
        /// <summary>
        /// Returns 1 if FAR is installed and available on this craft, 0 if it
        /// is not available.
        /// </summary>
        /// <returns></returns>
        public double Available()
        {
            return (farFound) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Return the ballistic coefficient for this vessel.
        /// </summary>
        /// <returns></returns>
        public double CoeffBallistic()
        {
            if (farFound)
            {
                return VesselCoeffBallistic(vessel);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Return the coefficient of drag for this vessel.
        /// </summary>
        /// <returns></returns>
        public double CoeffDrag()
        {
            if (farFound)
            {
                return VesselCoeffDrag(vessel);
            }
            else
            {
                return 0.0;
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
        /// Reduce flap setting one step, unless flaps are already at 0.
        /// </summary>
        /// <returns>1 if flap settings were decreased, 0 otherwise.</returns>
        public double DecreaseFlapSetting()
        {
            if (farFound && flapSetting > 0)
            {
                VesselDecreaseFlapDeflection(vessel);
                return 1.0;
            }
            return 0.0;
        }

        /// <summary>
        /// Return the total force applied to the vessel due to drag.
        /// </summary>
        /// <returns>Drag force in kN.</returns>
        public double DragForce()
        {
            object flightInfo = GetVesselFlightInfo(vessel);
            if (flightInfo != null)
            {
                return GetDragForce(flightInfo);
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the current dynamic pressure in kPa.
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
        /// Returns the current flap setting for the vessel.
        /// </summary>
        /// <returns>0 (no flaps) through 3 (maximum flaps).</returns>
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
        /// Increase flap setting one step, unless flaps are already at 3.
        /// </summary>
        /// <returns>1 if flap settings were increased, 0 otherwise.</returns>
        public double IncreaseFlapSetting()
        {
            if (farFound && flapSetting < 3)
            {
                VesselIncreaseFlapDeflection(vessel);
                return 1.0;
            }
            return 0.0;
        }

        /// <summary>
        /// Return the total force applied to the vessel due to lift.
        /// </summary>
        /// <returns>Lift force in kN.</returns>
        public double LiftForce()
        {
            object flightInfo = GetVesselFlightInfo(vessel);
            if (flightInfo != null)
            {
                return GetLiftForce(flightInfo);
            }

            return 0.0;
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
        /// Deploy or retract spoilers.
        /// </summary>
        /// <param name="newState">true to deploy spoilers, false to retract spoilers.</param>
        /// <returns>1 if the spoiler state changed, 0 otherwise.</returns>
        public double SetSpoilers(bool newState)
        {
            if (farFound && newState != spoilerSetting)
            {
                VesselSetSpoilers(vessel, newState);
                return 1.0;
            }
            return 0.0;
        }

        /// <summary>
        /// Returns the sideslip of the vessel.
        /// </summary>
        /// <returns>Sideslip in degrees.</returns>
        public double Sideslip()
        {
            if (farFound)
            {
                return VesselSideSlip(vessel);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the thrust specific fuel consumption of the vessel.
        /// </summary>
        /// <returns></returns>
        public double SpecFuelConsumption()
        {
            if (farFound)
            {
                return VesselSpecFuelConsumption(vessel);
            }
            else
            {
                return 0.0;
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

        /// <summary>
        /// Returns an estimate of the terminal velocity for the current vessel.
        /// </summary>
        /// <returns>Terminal velocity in m/s.</returns>
        public double TerminalVelocity()
        {
            if (farFound)
            {
                return VesselTerminalVelocity(vessel);
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
                MethodInfo coeffBallistic_t = farAPI_t.GetMethod("VesselBallisticCoeff", BindingFlags.Static | BindingFlags.Public);
                if (coeffBallistic_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'VesselBallisticCoeff' in FAR");
                    return;
                }
                VesselCoeffBallistic = (Func<Vessel, double>)Delegate.CreateDelegate(typeof(Func<Vessel, double>), coeffBallistic_t);

                MethodInfo coeffDrag_t = farAPI_t.GetMethod("VesselDragCoeff", BindingFlags.Static | BindingFlags.Public);
                if (coeffDrag_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'VesselDragCoeff' in FAR");
                    return;
                }
                VesselCoeffDrag = (Func<Vessel, double>)Delegate.CreateDelegate(typeof(Func<Vessel, double>), coeffDrag_t);

                MethodInfo coeffLift_t = farAPI_t.GetMethod("VesselLiftCoeff", BindingFlags.Static | BindingFlags.Public);
                if (coeffLift_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'VesselLiftCoeff' in FAR");
                    return;
                }
                VesselCoeffLift = (Func<Vessel, double>)Delegate.CreateDelegate(typeof(Func<Vessel, double>), coeffLift_t);

                MethodInfo aoA_t = farAPI_t.GetMethod("VesselAoA", BindingFlags.Static | BindingFlags.Public);
                if (aoA_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'VesselAoA' in FAR");
                    return;
                }
                VesselAoA = (Func<Vessel, double>)Delegate.CreateDelegate(typeof(Func<Vessel, double>), aoA_t);

                MethodInfo dynPress_t = farAPI_t.GetMethod("VesselDynPres", BindingFlags.Static | BindingFlags.Public);
                if (dynPress_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'VesselDynPres' in FAR");
                    return;
                }
                VesselDynPress = (Func<Vessel, double>)Delegate.CreateDelegate(typeof(Func<Vessel, double>), dynPress_t);

                MethodInfo refArea_t = farAPI_t.GetMethod("VesselRefArea", BindingFlags.Static | BindingFlags.Public);
                if (refArea_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'VesselRefArea' in FAR");
                    return;
                }
                VesselRefArea = (Func<Vessel, double>)Delegate.CreateDelegate(typeof(Func<Vessel, double>), refArea_t);

                MethodInfo stallFrac_t = farAPI_t.GetMethod("VesselStallFrac", BindingFlags.Static | BindingFlags.Public);
                if (stallFrac_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'VesselStallFrac' in FAR");
                    return;
                }
                VesselStallFrac = (Func<Vessel, double>)Delegate.CreateDelegate(typeof(Func<Vessel, double>), stallFrac_t);

                MethodInfo termVel_t = farAPI_t.GetMethod("VesselTermVelEst", BindingFlags.Static | BindingFlags.Public);
                if (termVel_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'VesselTermVelEst' in FAR");
                    return;
                }
                VesselTerminalVelocity = (Func<Vessel, double>)Delegate.CreateDelegate(typeof(Func<Vessel, double>), termVel_t);

                MethodInfo flapSetting_t = farAPI_t.GetMethod("VesselFlapSetting", BindingFlags.Static | BindingFlags.Public);
                if (flapSetting_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'VesselFlapSetting' in FAR");
                    return;
                }
                VesselFlapSetting = (Func<Vessel, int>)Delegate.CreateDelegate(typeof(Func<Vessel, int>), flapSetting_t);

                MethodInfo decreaseFlapSetting_t = farAPI_t.GetMethod("VesselDecreaseFlapDeflection", BindingFlags.Static | BindingFlags.Public);
                if (decreaseFlapSetting_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'VesselDecreaseFlapDeflection' in FAR");
                    return;
                }
                VesselDecreaseFlapDeflection = (Action<Vessel>)Delegate.CreateDelegate(typeof(Action<Vessel>), decreaseFlapSetting_t);

                MethodInfo increaseFlapSetting_t = farAPI_t.GetMethod("VesselIncreaseFlapDeflection", BindingFlags.Static | BindingFlags.Public);
                if (increaseFlapSetting_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'VesselIncreaseFlapDeflection' in FAR");
                    return;
                }
                VesselIncreaseFlapDeflection = (Action<Vessel>)Delegate.CreateDelegate(typeof(Action<Vessel>), increaseFlapSetting_t);

                MethodInfo spoilerSetting_t = farAPI_t.GetMethod("VesselSpoilerSetting", BindingFlags.Static | BindingFlags.Public);
                if (spoilerSetting_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'VesselSpoilerSetting' in FAR");
                    return;
                }
                VesselSpoilerSetting = (Func<Vessel, bool>)Delegate.CreateDelegate(typeof(Func<Vessel, bool>), spoilerSetting_t);

                MethodInfo setSpoiler_t = farAPI_t.GetMethod("VesselSetSpoilers", BindingFlags.Static | BindingFlags.Public);
                if (setSpoiler_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'VesselSetSpoilers' in FAR");
                    return;
                }
                VesselSetSpoilers = (Action<Vessel, bool>)Delegate.CreateDelegate(typeof(Action<Vessel, bool>), setSpoiler_t);

                MethodInfo sideslip_t = farAPI_t.GetMethod("VesselSideslip", BindingFlags.Static | BindingFlags.Public);
                if (sideslip_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'VesselSideslip' in FAR");
                    return;
                }
                VesselSideSlip = (Func<Vessel, double>)Delegate.CreateDelegate(typeof(Func<Vessel, double>), sideslip_t);

                MethodInfo specFuelConsumption_t = farAPI_t.GetMethod("VesselTSFC", BindingFlags.Static | BindingFlags.Public);
                if (specFuelConsumption_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'VesselTSFC' in FAR");
                    return;
                }
                VesselSpecFuelConsumption = (Func<Vessel, double>)Delegate.CreateDelegate(typeof(Func<Vessel, double>), specFuelConsumption_t);

                MethodInfo flightInfo_t = farAPI_t.GetMethod("VesselFlightInfo", BindingFlags.Static | BindingFlags.Public);
                if (flightInfo_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'VesselFlightInfo' in FAR");
                    return;
                }
                VesselFlightInfo = (Func<Vessel, object>)Delegate.CreateDelegate(typeof(Func<Vessel, object>), flightInfo_t);

                Type flightGUI_t = Utility.GetExportedType("FerramAerospaceResearch", "FerramAerospaceResearch.FARGUI.FARFlightGUI.FlightGUI");
                if (flightGUI_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'FlightGUI' in FAR");
                    return;
                }
                PropertyInfo infoParam_t = flightGUI_t.GetProperty("InfoParameters", BindingFlags.Instance | BindingFlags.Public);
                if (infoParam_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'InfoParameters' in FAR");
                    return;
                }
                MethodInfo getInfo_t = infoParam_t.GetGetMethod();
                if (getInfo_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'InfoParameters' get method in FAR");
                    return;
                }
                GetInfoParameters = (Func<object, object>)Delegate.CreateDelegate(typeof(Func<object, object>), getInfo_t);

                Type FlightInfo_t = Utility.GetExportedType("FerramAerospaceResearch", "FerramAerospaceResearch.FARGUI.FARFlightGUI.VesselFlightInfo");
                if (FlightInfo_t == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'VesselFlightInfo' in FAR");
                    return;
                }
                FieldInfo DragForceFieldInfo = FlightInfo_t.GetField("dragForce");
                if (DragForceFieldInfo == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'dragForce' field in FAR");
                    return;
                }
                GetDragForce = DynamicMethodFactory.CreateGetField<object, double>(DragForceFieldInfo);
                FieldInfo LiftForceFieldInfo = FlightInfo_t.GetField("liftForce");
                if (LiftForceFieldInfo == null)
                {
                    Utility.LogStaticErrorMessage("Failed to find 'liftForce' field in FAR");
                    return;
                }
                GetLiftForce = DynamicMethodFactory.CreateGetField<object, double>(LiftForceFieldInfo);

                farFound = true;
            }
        }
        #endregion
    }
}
