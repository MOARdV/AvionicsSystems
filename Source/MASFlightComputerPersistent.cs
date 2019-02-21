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
using System;
using System.Collections.Generic;
using System.Text;

// TODO: Global Persistent methods
namespace AvionicsSystems
{
    // This file implements the Persistent Variables interfaces.
    public partial class MASFlightComputer : PartModule
    {
        /// <summary>
        /// Persistent variables are stored as objects, although that adds the
        /// overhead of boxing and unboxing numeric values.
        /// </summary>
        private Dictionary<string, object> persistentVars = new Dictionary<string, object>();

        /// <summary>
        /// Callback-based system to notify the GetPersistent variables that a variable changed.
        /// </summary>
        private Dictionary<string, PersistentChangedNotification> persistentNotices = new Dictionary<string, PersistentChangedNotification>();

        /// <summary>
        /// Registers a callback with one of the persistent strings, allowing the Get queries
        /// to be callback-based (Dependent variables) instead of polling.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <param name="callback"></param>
        private void RegisterPersistentNotice(string persistentName, Action<double> callback)
        {
            PersistentChangedNotification notice;
            if (!persistentNotices.TryGetValue(persistentName, out notice))
            {
                notice = new PersistentChangedNotification();
                persistentNotices[persistentName] = notice;
            }
            notice.numericCallbacks += callback;
        }

        /// <summary>
        /// Used to notify registered persistent value watchers that a persistent has
        /// changed.
        /// </summary>
        /// <param name="persistentName"></param>
        private void UpdatePersistent(string persistentName)
        {
            PersistentChangedNotification notice;
            if (!persistentNotices.TryGetValue(persistentName, out notice))
            {
                notice = new PersistentChangedNotification();
                persistentNotices[persistentName] = notice;
            }

            notice.TriggerCallbacks();
        }

        #region Persistent Access
        /// <summary>
        /// Add a quantity to a persistent.  If the persistent doesn't exist,
        /// it is initialized to zero.  If the persistent can not be converted
        /// to a numeric value, the name is returned.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        internal object AddPersistent(string persistentName, double amount)
        {
            double v;
            object o;
            if (persistentVars.TryGetValue(persistentName, out o))
            {
                if (o is double)
                {
                    v = (double)o;
                }
                else
                {
                    if (!double.TryParse(o as string, out v))
                    {
                        return persistentName;
                    }
                }
            }
            else
            {
                v = 0.0;
            }

            v += amount;
            persistentVars[persistentName] = v;
            if (amount != 0.0)
            {
                UpdatePersistent(persistentName);
            }
            return v;
        }

        /// <summary>
        /// Add an amount to a persistent variable, but clamp it to the specified range.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <param name="amount"></param>
        /// <param name="minValue"></param>
        /// <param name="maxValue"></param>
        /// <returns></returns>
        internal object AddPersistentClamped(string persistentName, double amount, double minValue, double maxValue)
        {
            double v;
            object o;
            if (persistentVars.TryGetValue(persistentName, out o))
            {
                if (o is double)
                {
                    v = (double)o;
                }
                else
                {
                    if (!double.TryParse(o as string, out v))
                    {
                        if (!string.IsNullOrEmpty(o as string))
                        {
                            return persistentName;
                        }
                        else
                        {
                            v = 0.0;
                        }
                    }
                }
            }
            else
            {
                v = 0.0;
            }

            if (minValue > maxValue)
            {
                double tmp = minValue;
                minValue = maxValue;
                maxValue = tmp;
            }

            double oldV = v;
            v += amount;
            v = Math.Max(minValue, Math.Min(maxValue, v));
            persistentVars[persistentName] = v;
            if (Math.Abs(oldV - v) > 0.0)
            {
                UpdatePersistent(persistentName);
            }
            return v;
        }

        /// <summary>
        /// Add an amount to a persistent variable, wrapping it between the two extents.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <param name="amount"></param>
        /// <param name="extent1"></param>
        /// <param name="extent2"></param>
        /// <returns></returns>
        internal object AddPersistentWrapped(string persistentName, double amount, double extent1, double extent2)
        {
            double v;
            object o;
            if (persistentVars.TryGetValue(persistentName, out o))
            {
                if (o is double)
                {
                    v = (double)o;
                }
                else
                {
                    if (!double.TryParse(o as string, out v))
                    {
                        return persistentName;
                    }
                }
            }
            else
            {
                v = 0.0;
            }

            double minValue, maxValue;
            if (extent1 < extent2)
            {
                minValue = extent1;
                maxValue = extent2;
            }
            else if (extent2 > extent1)
            {
                minValue = extent2;
                maxValue = extent1;
            }
            else
            {
                return v;
            }

            double oldV = v;
            v += amount;
            double range = maxValue - minValue;
            while (v < minValue)
            {
                v += range;
            }

            while (v >= maxValue)
            {
                v -= range;
            }

            persistentVars[persistentName] = v;
            if (Math.Abs(oldV - v) > 0.0)
            {
                UpdatePersistent(persistentName);
            }
            return v;
        }

        /// <summary>
        /// Treat the persistent as a string, and append the string specified.
        /// maxLength indicates the maximum number of characters the string
        /// will allow.  Anything after that is truncated.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <param name="addon"></param>
        /// <param name="maxLength"></param>
        /// <returns></returns>
        internal object AppendPersistent(string persistentName, string addon, int maxLength)
        {
            object pvo;
            if (persistentVars.TryGetValue(persistentName, out pvo))
            {
                // Can this be more efficient?
                string persistBuffer = pvo.ToString() + addon;
                if (persistBuffer.Length > maxLength)
                {
                    persistBuffer = persistBuffer.Substring(0, maxLength);
                }
                persistentVars[persistentName] = persistBuffer;
                if (pvo.ToString() != persistBuffer)
                {
                    UpdatePersistent(persistentName);
                }
                return persistBuffer;
            }
            else
            {
                persistentVars[persistentName] = addon;
                UpdatePersistent(persistentName);

                return addon;
            }
        }

        /// <summary>
        /// Treat the persistent as a number, and append the digit specified.
        /// maxLength indicates the maximum number of characters the number
        /// will allow when treated as a string.  Anything after that is truncated.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <param name="digit"></param>
        /// <param name="maxLength"></param>
        /// <returns></returns>
        internal double AppendPersistentNumeric(string persistentName, int digit, int maxLength)
        {
            object val;
            if (persistentVars.TryGetValue(persistentName, out val))
            {
                double result;
                if (val is double)
                {
                    result = (double)val;
                }
                else if (!double.TryParse(val as string, out result))
                {
                    result = 0.0;
                }

                if (result < Math.Pow(10.0, (double)(maxLength - 1)))
                {
                    result *= 10.0;
                    result += (double)digit;

                    persistentVars[persistentName] = result;
                    UpdatePersistent(persistentName);
                }

                return result;
            }
            else if (digit != 0)
            {
                persistentVars[persistentName] = (double)digit;
                UpdatePersistent(persistentName);

                return (double)digit;
            }

            return 0.0;
        }

        internal double ClearBits(string persistentName, int bits)
        {
            int persistentInt = 0;
            bits = ~bits;

            object val;
            if (persistentVars.TryGetValue(persistentName, out val))
            {
                if (val is double)
                {
                    persistentInt = (int)(double)val;
                }
                else
                {
                    double result;
                    if (double.TryParse(val as string, out result))
                    {
                        persistentInt = (int)result;
                    }
                }
            }

            int newPersistentInt = persistentInt & bits;
            if (newPersistentInt != persistentInt)
            {
                persistentVars[persistentName] = (double)newPersistentInt;
                UpdatePersistent(persistentName);
            }

            return (double)newPersistentInt;
        }

        /// <summary>
        /// Returns the value of the named persistent, or the name if it wasn't
        /// found.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <returns></returns>
        internal object GetPersistent(string persistentName)
        {
            object o;
            if (persistentVars.TryGetValue(persistentName, out o))
            {
                return o;
            }
            else
            {
                return persistentName;
            }
        }

        /// <summary>
        /// Indicates if the named persistent is already in the database.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <returns></returns>
        internal bool GetPersistentExists(string persistentName)
        {
            return persistentVars.ContainsKey(persistentName);
        }

        /// <summary>
        /// Try to force the named persistent to a numeric value, returning 0
        /// if the persistent doesn't exist, or it can't be converted.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <returns></returns>
        internal double GetPersistentAsNumber(string persistentName)
        {
            object val;
            if (persistentVars.TryGetValue(persistentName, out val))
            {
                if (val is double)
                {
                    return (double)val;
                }
                else
                {
                    double result;
                    if (double.TryParse(val as string, out result))
                    {
                        return result;
                    }
                }
            }

            return 0.0;
        }

        internal double InitializePersistent(string persistentName, object value)
        {
            if (persistentVars.ContainsKey(persistentName))
            {
                return 0.0;
            }
            else
            {
                persistentVars[persistentName] = value;
                UpdatePersistent(persistentName);
                return 1.0;
            }
        }

        internal double SetBits(string persistentName, int bits)
        {
            int persistentInt = 0;

            object val;
            if (persistentVars.TryGetValue(persistentName, out val))
            {
                if (val is double)
                {
                    persistentInt = (int)(double)val;
                }
                else
                {
                    double result;
                    if (double.TryParse(val as string, out result))
                    {
                        persistentInt = (int)result;
                    }
                }
            }

            int newPersistentInt = persistentInt | bits;
            if (newPersistentInt != persistentInt)
            {
                persistentVars[persistentName] = (double)newPersistentInt;
                UpdatePersistent(persistentName);
            }

            return (double)newPersistentInt;
        }

        /// <summary>
        /// Unconditionally set the named persistent to the specified value.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal object SetPersistent(string persistentName, object value)
        {
            if (value == null)
            {
                Utility.LogError(this, "Trying to set {0} to null - check Lua scripts", persistentName);
                value = "null";
            }
            persistentVars[persistentName] = value;
            UpdatePersistent(persistentName);
            return value;
        }

        /// <summary>
        /// Set a persistent to a value, but limit the amount that the persistent may change
        /// in a given period of time.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <param name="value"></param>
        /// <param name="maxRatePerSecond"></param>
        /// <returns></returns>
        internal double SetPersistentBlended(string persistentName, double value, double maxRatePerSecond)
        {
            double result;
            object val;
            if (persistentVars.TryGetValue(persistentName, out val))
            {
                if (val is double)
                {
                    result = (double)val;
                }
                else if (!double.TryParse(val as string, out result))
                {
                    result = value;
                }
            }
            else
            {
                result = value;
                persistentVars[persistentName] = result;
                UpdatePersistent(persistentName);
                // Early return: Initializing the value
                return result;
            }

            if (maxRatePerSecond > 0.0)
            {
                double delta = Math.Abs(value - result);
                delta = Math.Min(delta, maxRatePerSecond * TimeWarp.fixedDeltaTime);

                if (value < result)
                {
                    result -= delta;
                }
                else
                {
                    result += delta;
                }
            }
            else
            {
                result = value;
            }

            if (result != value)
            {
                persistentVars[persistentName] = result;
                UpdatePersistent(persistentName);
            }

            return result;
        }

        /// <summary>
        /// Treat the persistent as a boolean value (0 or 1) and toggle it.
        /// Treat it like it was 0 if it wasn't found (thus setting it to 1).
        /// If it is a string, try to convert it to a numeric before toggling.
        /// If it can't be converted to a number, treat it as 0.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <returns></returns>
        internal double TogglePersistent(string persistentName)
        {
            object o;
            if (persistentVars.TryGetValue(persistentName, out o))
            {
                if (o is double)
                {
                    double v = (double)o;
                    double newVal = (v > 0.0) ? 0.0 : 1.0;
                    persistentVars[persistentName] = newVal;
                    UpdatePersistent(persistentName);
                    return newVal;
                }
                else
                {
                    double v;
                    if (double.TryParse(o as string, out v))
                    {
                        double newVal = (v > 0.0) ? 0.0 : 1.0;
                        persistentVars[persistentName] = newVal;
                        UpdatePersistent(persistentName);
                        return newVal;
                    }
                }
            }

            persistentVars[persistentName] = 1.0;
            UpdatePersistent(persistentName);
            return 1.0;
        }
        #endregion
    }

    /// <summary>
    /// Helper class to trigger persistent variable changes, so persistents don't have to
    /// be polled.
    /// </summary>
    internal class PersistentChangedNotification
    {
        internal event Action<double> numericCallbacks;

        internal void TriggerCallbacks()
        {
            if (numericCallbacks != null)
            {
                numericCallbacks.Invoke(0.0);
            }
        }
    }
}
