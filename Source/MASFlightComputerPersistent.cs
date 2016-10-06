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
            if (persistentVars.ContainsKey(persistentName))
            {
                object o = persistentVars[persistentName];
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
            if (persistentVars.ContainsKey(persistentName))
            {
                object o = persistentVars[persistentName];
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

            v += amount;
            v = v.Clamp(minValue, maxValue);
            persistentVars[persistentName] = v;
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
            if (persistentVars.ContainsKey(persistentName))
            {
                object o = persistentVars[persistentName];
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
            else
            {
                minValue = extent2;
                maxValue = extent1;
            }

            v += amount;
            double range = maxValue - minValue;
            if (range > 0.0)
            {
                double iLerp = (v - minValue) / range;
                if (iLerp < 0.0)
                {
                    iLerp = 1.0 - (Math.Abs(iLerp) % 1.0);
                }
                else if (iLerp > 1.0)
                {
                    iLerp = iLerp % 1.0;
                }
                // The two extents are effectively aliases for one another.
                if (iLerp == 1.0)
                {
                    iLerp = 0.0;
                }
                v = minValue + iLerp * range;
            }

            persistentVars[persistentName] = v;
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
            if (persistentVars.ContainsKey(persistentName))
            {
                object pvo = persistentVars[persistentName];

                // Can this be more efficient?
                string persistBuffer = pvo.ToString() + addon;
                if (persistBuffer.Length > maxLength)
                {
                    persistBuffer = persistBuffer.Substring(0, maxLength);
                }
                persistentVars[persistentName] = persistBuffer;
                return persistBuffer;
            }
            else
            {
                persistentVars[persistentName] = addon;

                return addon;
            }
        }

        /// <summary>
        /// Returns the value of the named persistent, or the name if it wasn't
        /// found.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <returns></returns>
        internal object GetPersistent(string persistentName)
        {
            if (persistentVars.ContainsKey(persistentName))
            {
                return persistentVars[persistentName];
            }
            else
            {
                return persistentName;
            }
        }

        /// <summary>
        /// Try to force the named persistent to a numeric value, returning 0
        /// if the persistent doesn't exist, or it can't be converted.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <returns></returns>
        internal double GetPersistentAsNumber(string persistentName)
        {
            if (persistentVars.ContainsKey(persistentName))
            {
                object val = persistentVars[persistentName];
                if (val is double)
                {
                    return (double)val;
                }
                else
                {
                    double result;
                    if(double.TryParse(val as string, out result))
                    {
                        return result;
                    }
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Unconditionally set the named persistent to the specified value.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal object SetPersistent(string persistentName, object value)
        {
            persistentVars[persistentName] = value;
            return value;
        }

        /// <summary>
        /// Treat the persistent as a boolean value (0 or 1) and toggle it.
        /// Treat it like it was 0 if it wasn't found (thus setting it to 1).
        /// If it is a string, try to convert it to a numeric before toggling.
        /// If it can't be converted to a number, return the name as a string.
        /// </summary>
        /// <param name="persistentName"></param>
        /// <returns></returns>
        internal object TogglePersistent(string persistentName)
        {
            if (persistentVars.ContainsKey(persistentName))
            {
                object o = persistentVars[persistentName];

                if (o is double)
                {
                    double v = (double)o;
                    double newVal = (v > 0.0) ? 0.0 : 1.0;
                    persistentVars[persistentName] = newVal;
                    return newVal;
                }
                else
                {
                    double v;
                    if (double.TryParse(o as string, out v))
                    {
                        double newVal = (v > 0.0) ? 0.0 : 1.0;
                        persistentVars[persistentName] = newVal;
                        return newVal;
                    }
                    else
                    {
                        return persistentName;
                    }
                }
            }
            else
            {
                persistentVars[persistentName] = 1.0;
                return 1.0;
            }
        }
        #endregion

        #region Persistent Save/Load
        internal ConfigNode SavePersistents()
        {
            if (persistentVars.Count > 0)
            {
                Utility.LogMessage(this, "Saving persistents for {0}", flightComputerId);

                ConfigNode saveNode = new ConfigNode("MASFlightComputerPersistents");
                saveNode.AddValue("MASFlightComputerId", flightComputerId);

                foreach (var keyValPair in persistentVars)
                {
                    string value = string.Format("{0},{1}", keyValPair.Value.GetType().ToString(), keyValPair.Value.ToString());
                    saveNode.AddValue(keyValPair.Key, value);
                    //Utility.LogMessage(this, "{0} = {1}", keyValPair.Key, value);
                }

                return saveNode;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Load persistent variables from the supplied node.
        /// </summary>
        /// <param name="loadNode"></param>
        /// <returns>true if this was our node, false otherwise</returns>
        internal bool LoadPersistents(ConfigNode loadNode)
        {
            string id = string.Empty;
            if (!loadNode.TryGetValue("MASFlightComputerId", ref id) || id != flightComputerId)
            {
                Utility.LogErrorMessage(this, "looking for {0}, but got {1}", flightComputerId, id);

                return false;
            }

            //Utility.LogMessage(this, "Restoring persistents for {0}", flightComputerId);
            // NOTE: We stop at 1, not 0, because we expect ASFlightComputerPersistents
            // to be the first entry; it will be as long as no one hand-edited the file!
            for (int i = loadNode.CountValues - 1; i >= 1; --i)
            {
                ConfigNode.Value val = loadNode.values[i];

                string[] value = val.value.Split(',');
                if (value.Length > 2)
                {
                    string s = value[1].Trim();
                    for (int j = 2; j < value.Length; ++j)
                    {
                        s = s + ',' + value[i].Trim();
                    }
                    value[1] = s;
                }

                switch (value[0].Trim())
                {
                    case "System.String":
                        persistentVars[val.name.Trim()] = value[1].Trim();
                        break;
                    case "System.Double":
                        double dbl = 0;
                        if (Double.TryParse(value[1].Trim(), out dbl))
                        {
                            persistentVars[val.name.Trim()] = dbl;
                        }
                        else
                        {
                            Utility.LogErrorMessage(this, "Failed to parse {0} ({1}) as a double", val.name, value[1].Trim());
                        }
                        break;
                    default:
                        Utility.LogErrorMessage(this, "Found unknown persistent type {0}", value[0]);
                        break;
                }
                //Utility.LogMessage(this, "{0} = {1}", val.name.Trim(), persistentVars[val.name.Trim()]);
            }

            return true;
        }
        #endregion
    }
}
