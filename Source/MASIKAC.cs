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
using System.Text;

namespace AvionicsSystems
{
    /// <summary>
    /// The MASIKAC class interfaces with the KACWrapper, which is imported
    /// verbatim from KAC's source.
    /// </summary>
    /// <LuaName>kac</LuaName>
    /// <mdDoc>
    /// The MASIKAC object encapsulates the interface with Kerbal Alarm Clock.
    /// </mdDoc>
    internal class MASIKAC
    {
        internal Vessel vessel;
        private double[] alarms;

        [MoonSharpHidden]
        public MASIKAC(Vessel vessel)
        {
            this.vessel = vessel;
            this.alarms = new double[0];
        }

        ~MASIKAC()
        {
            vessel = null;
        }

        [MoonSharpHidden]
        internal void Update()
        {
            if (KACWrapper.InstanceExists)
            {
                KACWrapper.KACAPI.KACAlarmList alarms = KACWrapper.KAC.Alarms;
                int alarmCount = alarms.Count;
                if (alarmCount > 0)
                {
                    double UT = Planetarium.GetUniversalTime();
                    int vesselAlarmCount = 0;
                    string id = vessel.id.ToString();
                    for (int i = 0; i < alarmCount; ++i)
                    {
                        if (alarms[i].VesselID == id && alarms[i].AlarmTime > UT)
                        {
                            ++vesselAlarmCount;
                        }
                    }

                    if (this.alarms.Length != vesselAlarmCount)
                    {
                        this.alarms = new double[vesselAlarmCount];
                    }

                    if (vesselAlarmCount > 0)
                    {
                        for (int i = 0; i < alarmCount; ++i)
                        {
                            if (alarms[i].VesselID == id && alarms[i].AlarmTime > UT)
                            {
                                --vesselAlarmCount;
                                this.alarms[vesselAlarmCount] = alarms[i].AlarmTime - UT;
                            }
                        }

                        // Sort the array so the next alarm is in [0].
                        Array.Sort<double>(this.alarms);
                    }
                }
                else if (this.alarms.Length > 0)
                {
                    this.alarms = new double[0];
                }
            }
        }

        /// <summary>
        /// Returns the number of future alarms scheduled for this vessel.
        /// Alarms that are in the past, or for other vessels, are not
        /// counted.  If Kerbal Alarm Clock is not installed, this value
        /// is zero.
        /// </summary>
        /// <returns>Count of alarms for this vessel; 0 or more.</returns>
        public double AlarmCount()
        {
            return alarms.Length;
        }

        /// <summary>
        /// Returns the time to the next alarm scheduled in Kerbal Alarm Clock
        /// for this vessel.  If no alarm is scheduled, or all alarms occurred in
        /// the past, this value is 0.
        /// </summary>
        /// <returns>Time to the next alarm for the current vessel, in seconds; 0 if there are no alarms.</returns>
        public double TimeToAlarm()
        {
            if (alarms.Length > 0)
            {
                return alarms[0];
            }
            else
            {
                return 0.0;
            }
        }

        #region Reflection Configuration
        static MASIKAC()
        {
            KACWrapper.InitKACWrapper();
        }
        #endregion
    }
}
