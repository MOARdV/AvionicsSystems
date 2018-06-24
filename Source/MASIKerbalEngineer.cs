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
using System.Reflection;

namespace AvionicsSystems
{
    /// <summary>
    /// MASIKerbalEngineer is the MAS interface to the Kerbal Engineer Redux mod.  It
    /// is a MoonSharp proxy class.
    /// 
    /// </summary>
    /// <LuaName>ke</LuaName>
    /// <mdDoc>
    /// MASIKerbalEngineer is the AvionicsSystems interface with Kerbal Engineer Redux.
    /// </mdDoc>
    internal class MASIKerbalEngineer
    {
        private bool requestUpdates = false;

        internal static readonly bool keFound;

        //--- SimulationProcessor
        private static readonly Func<bool> keSimulationReady;
        private static readonly Func<object> keLastStage;
        private static readonly Action keSimulationRequestUpdate;

        //--- ImpactProcessor
        private static readonly Action keImpactRequestUpdate;
        private static readonly Func<bool> keImpactReady;
        private static readonly Func<double> keImpactLatitude;
        private static readonly Func<double> keImpactLongitude;
        private static readonly Func<double> keImpactAltitude;
        private static readonly Func<double> keImpactTime;

        //--- Stage accessors
        private static readonly Func<object, double> keGetDeltaV;
        private static readonly Func<object, double> keGetTotalDeltaV;

        [MoonSharpHidden]
        internal void Update()
        {
            if (requestUpdates)
            {
                keSimulationRequestUpdate();
                keImpactRequestUpdate();
            }
        }

        /// <summary>
        /// The KER Simulation category reports information from Kerbal Enginner Redux.
        /// </summary>
        #region KER Simulation

        /// <summary>
        /// Returns the total delta-V available to the vessel, in m/s.
        /// </summary>
        /// <returns></returns>
        public double DeltaV()
        {
            if (keFound)
            {
                requestUpdates = true;
                if (keSimulationReady())
                {
                    object lastStage = keLastStage();
                    if (lastStage != null)
                    {
                        return keGetTotalDeltaV(lastStage);
                    }
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the projected altitude of landing, in meters.
        /// </summary>
        /// <returns></returns>
        public double LandingAltitude()
        {
            if (keFound)
            {
                requestUpdates = true;
                if (keImpactReady())
                {
                    return keImpactAltitude();
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the projected latitude of landing, in meters.
        /// </summary>
        /// <returns></returns>
        public double LandingLatitude()
        {
            if (keFound)
            {
                requestUpdates = true;
                if (keImpactReady())
                {
                    return keImpactLatitude();
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the projected longitude of landing, in meters.
        /// </summary>
        /// <returns></returns>
        public double LandingLongitude()
        {
            if (keFound)
            {
                requestUpdates = true;
                if (keImpactReady())
                {
                    return keImpactLongitude();
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the projected time of landing, in seconds.
        /// </summary>
        /// <returns></returns>
        public double LandingTime()
        {
            if (keFound)
            {
                requestUpdates = true;
                if (keImpactReady())
                {
                    return keImpactTime();
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the total delta-V available to the vessel, in m/s.
        /// </summary>
        /// <returns></returns>
        public double StageDeltaV()
        {
            if (keFound)
            {
                requestUpdates = true;
                if (keSimulationReady())
                {
                    object lastStage = keLastStage();
                    if (lastStage != null)
                    {
                        return keGetDeltaV(lastStage);
                    }
                }
            }

            return 0.0;
        }
        #endregion

        static MASIKerbalEngineer()
        {
            Type keSimProcessor_t = Utility.GetExportedType("KerbalEngineer", "KerbalEngineer.Flight.Readouts.Vessel.SimulationProcessor");
            Type keImpactProcessor_t = Utility.GetExportedType("KerbalEngineer", "KerbalEngineer.Flight.Readouts.Surface.ImpactProcessor");
            Type keStage_t = Utility.GetExportedType("KerbalEngineer", "KerbalEngineer.VesselSimulator.Stage");

            if (keSimProcessor_t == null || keImpactProcessor_t == null || keStage_t == null)
            {
                return;
            }

            //--- SimulationProcessor
            MethodInfo RequestUpdate_t = keSimProcessor_t.GetMethod("RequestUpdate", BindingFlags.Static | BindingFlags.Public);
            if (RequestUpdate_t == null)
            {
                Utility.LogStaticError("Failed to find 'RequestUpdate' in KER");
                return;
            }
            keSimulationRequestUpdate = (Action)Delegate.CreateDelegate(typeof(Action), RequestUpdate_t);

            PropertyInfo LastStage_t = keSimProcessor_t.GetProperty("LastStage", BindingFlags.Static | BindingFlags.Public);
            if (LastStage_t == null)
            {
                Utility.LogStaticError("Failed to find LastStage in KER");
                return;
            }
            MethodInfo getInfo_t = LastStage_t.GetGetMethod();
            if (getInfo_t == null)
            {
                Utility.LogStaticError("Failed to find 'LastStage' get method in KER");
                return;
            }
            keLastStage = (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), getInfo_t);

            PropertyInfo ShowDetails_t = keSimProcessor_t.GetProperty("ShowDetails", BindingFlags.Static | BindingFlags.Public);
            if (ShowDetails_t == null)
            {
                Utility.LogStaticError("Failed to find ShowDetails");
                return;
            }
            getInfo_t = ShowDetails_t.GetGetMethod();
            if (getInfo_t == null)
            {
                Utility.LogStaticError("Failed to find 'ShowDetails' get method in KER");
                return;
            }
            keSimulationReady = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getInfo_t);

            //--- ImpactProcessor
            RequestUpdate_t = keImpactProcessor_t.GetMethod("RequestUpdate", BindingFlags.Static | BindingFlags.Public);
            if (RequestUpdate_t == null)
            {
                Utility.LogStaticError("Failed to find 'RequestUpdate' in KER");
                return;
            }
            keImpactRequestUpdate = (Action)Delegate.CreateDelegate(typeof(Action), RequestUpdate_t);

            ShowDetails_t = keImpactProcessor_t.GetProperty("ShowDetails", BindingFlags.Static | BindingFlags.Public);
            if (ShowDetails_t == null)
            {
                Utility.LogStaticError("Failed to find ShowDetails");
                return;
            }
            getInfo_t = ShowDetails_t.GetGetMethod();
            if (getInfo_t == null)
            {
                Utility.LogStaticError("Failed to find 'ShowDetails' get method in KER");
                return;
            }
            keImpactReady = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getInfo_t);

            ShowDetails_t = keImpactProcessor_t.GetProperty("Latitude", BindingFlags.Static | BindingFlags.Public);
            if (ShowDetails_t == null)
            {
                Utility.LogStaticError("Failed to find Latitude");
                return;
            }
            getInfo_t = ShowDetails_t.GetGetMethod();
            if (getInfo_t == null)
            {
                Utility.LogStaticError("Failed to find 'Latitude' get method in KER");
                return;
            }
            keImpactLatitude = (Func<double>)Delegate.CreateDelegate(typeof(Func<double>), getInfo_t);

            ShowDetails_t = keImpactProcessor_t.GetProperty("Longitude", BindingFlags.Static | BindingFlags.Public);
            if (ShowDetails_t == null)
            {
                Utility.LogStaticError("Failed to find Longitude");
                return;
            }
            getInfo_t = ShowDetails_t.GetGetMethod();
            if (getInfo_t == null)
            {
                Utility.LogStaticError("Failed to find 'Longitude' get method in KER");
                return;
            }
            keImpactLongitude = (Func<double>)Delegate.CreateDelegate(typeof(Func<double>), getInfo_t);

            ShowDetails_t = keImpactProcessor_t.GetProperty("Altitude", BindingFlags.Static | BindingFlags.Public);
            if (ShowDetails_t == null)
            {
                Utility.LogStaticError("Failed to find Altitude");
                return;
            }
            getInfo_t = ShowDetails_t.GetGetMethod();
            if (getInfo_t == null)
            {
                Utility.LogStaticError("Failed to find 'Altitude' get method in KER");
                return;
            }
            keImpactAltitude = (Func<double>)Delegate.CreateDelegate(typeof(Func<double>), getInfo_t);

            ShowDetails_t = keImpactProcessor_t.GetProperty("Time", BindingFlags.Static | BindingFlags.Public);
            if (ShowDetails_t == null)
            {
                Utility.LogStaticError("Failed to find Time");
                return;
            }
            getInfo_t = ShowDetails_t.GetGetMethod();
            if (getInfo_t == null)
            {
                Utility.LogStaticError("Failed to find 'Time' get method in KER");
                return;
            }
            keImpactTime = (Func<double>)Delegate.CreateDelegate(typeof(Func<double>), getInfo_t);

            //--- Stage accessors
            FieldInfo DeltaV_t = keStage_t.GetField("deltaV", BindingFlags.Instance | BindingFlags.Public);
            if (DeltaV_t == null)
            {
                Utility.LogStaticError("Didn't find deltaV");
                return;
            }
            keGetDeltaV = DynamicMethodFactory.CreateGetField<object, double>(DeltaV_t);

            FieldInfo TotalDeltaV_t = keStage_t.GetField("totalDeltaV", BindingFlags.Instance | BindingFlags.Public);
            if (TotalDeltaV_t == null)
            {
                Utility.LogStaticError("Didn't find totalDeltaV");
                return;
            }
            keGetTotalDeltaV = DynamicMethodFactory.CreateGetField<object, double>(TotalDeltaV_t);

            keFound = true;
        }
    }
}
