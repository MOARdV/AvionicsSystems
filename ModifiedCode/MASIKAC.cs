/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2017 MOARdV
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
        private string[] alarmIDs = new string[0];

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
                        alarmIDs = new string[vesselAlarmCount];
                    }

                    if (vesselAlarmCount > 0)
                    {
                        for (int i = 0; i < alarmCount; ++i)
                        {
                            if (alarms[i].VesselID == id && alarms[i].AlarmTime > UT)
                            {
                                --vesselAlarmCount;
                                alarmIDs[vesselAlarmCount] = alarms[i].ID;
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
                    alarmIDs = new string[0];
                }
            }
        }

        /// <summary>
        /// The functions for interacting with the Kerbal Alarm Clock are listed in this category.
        /// </summary>
        #region Kerbal Alarm

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
        /// Scans the list of alarms assigned to this vessel to see if the alarm identified by `alarmID`
        /// exists.  Returns 1 if it is found, 0 otherwise.
        /// </summary>
        /// <param name="alarmID">The ID of the alarm, typically from the return value of `fc.CreateAlarm()`.</param>
        /// <returns>1 if the alarm exists, 0 otherwise.</returns>
        public double AlarmExists(string alarmID)
        {
            if (alarmIDs.Length > 0)
            {
                int idx = alarmIDs.IndexOf(alarmID);

                return (idx == -1) ? 0.0 : 1.0;
            }

            return 0.0;
        }

        [MASProxyAttribute(Immutable = true)]
        /// <summary>
        /// Returns 1 if Kerbal Alarm Clock is installed and available on this craft, 0 if it
        /// is not available.
        /// </summary>
        /// <returns></returns>
        public double Available()
        {
            return (KACWrapper.InstanceExists) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Create a Raw alarm at the time specified by `UT`, using the name `name`.  This alarm is
        /// assigned to the current vessel ID.
        /// </summary>
        /// <param name="name">The short name to apply to the alarm.</param>
        /// <param name="UT">The UT when the alarm should fire.</param>
        /// <returns>The alarm ID (a string), or an empty string if the method failed.</returns>
        public string CreateAlarm(string name, double UT)
        {
            if (KACWrapper.InstanceExists)
            {
                string alarmID = KACWrapper.KAC.CreateAlarm(KACWrapper.KACAPI.AlarmTypeEnum.Raw, name, UT);

                if (string.IsNullOrEmpty(alarmID))
                {
                    return string.Empty;
                }
                else
                {
                    var newAlarm = KACWrapper.KAC.Alarms.Find(x => x.ID == alarmID);
                    if (newAlarm != null)
                    {
                        newAlarm.VesselID = vessel.id.ToString();
                    }

                    return alarmID;
                }

            }

            return string.Empty;
        }

        /// <summary>
        /// Create an alarm of specified at the time specified by `UT`, using the name `name`.  This alarm is
        /// assigned to the current vessel ID.
        /// </summary>
        /// <param name="alarmType">The type of alarm to create."</param>
        /// <param name="name">The short name to apply to the alarm.</param>
        /// <param name="UT">The UT when the alarm should fire.</param>
        /// <returns>The alarm ID (a string), or an empty string if the method failed.</returns>
        public string CreateTypeAlarm(string alarmTypeStr, string name, double UT)
        {
            if (KACWrapper.InstanceExists)
            {
                KACWrapper.KACAPI.AlarmTypeEnum alarmType = (KACWrapper.KACAPI.AlarmTypeEnum) Enum.Parse(typeof(KACWrapper.KACAPI.AlarmTypeEnum), alarmTypeStr);
                
                string alarmID = KACWrapper.KAC.CreateAlarm(alarmType, name, UT);

                if (string.IsNullOrEmpty(alarmID))
                {
                    return string.Empty;
                }
                else
                {
                    var newAlarm = KACWrapper.KAC.Alarms.Find(x => x.ID == alarmID);
                    if (newAlarm != null)
                    {
                        newAlarm.VesselID = vessel.id.ToString();
                    }

                    return alarmID;
                }

            }

            return string.Empty;
        }

        /// <summary>
        /// Attempts to remove the alarm identified by `alarmID`.  Normally, `alarmID` is the return
        /// value from `fc.CreateAlarm()`.
        /// </summary>
        /// <param name="alarmID">The ID of the alarm to remove</param>
        /// <returns>1 if the alarm was removed, 0 if it was not, or it did not exist, or the Kerbal Alarm Clock is not available.</returns>
        public double DeleteAlarm(string alarmID)
        {
            if (alarms.Length > 0)
            {
                return (KACWrapper.KAC.DeleteAlarm(alarmID)) ? 1.0 : 0.0;
            }
            return 0.0;
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
        #endregion

        #region Reflection Configuration
        static MASIKAC()
        {
            KACWrapper.InitKACWrapper();
        }
        #endregion
    }
}
