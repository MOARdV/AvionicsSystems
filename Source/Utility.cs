//#define VALIDATE_ANGLE_NORMALIZER
/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016 - 2018 MOARdV
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
using UnityEngine;

namespace AvionicsSystems
{
    internal static class Utility
    {
        internal static readonly double Deg2Rad = Math.PI / 180.0;
        internal static readonly double Rad2Deg = 180.0 / Math.PI;

        internal static readonly string[] NewLine = { Environment.NewLine };

        internal static Dictionary<VesselType, string> typeDict = new Dictionary<VesselType, string>
        {
            {VesselType.Debris, "Debris"},
            {VesselType.SpaceObject, "SpaceObject"},
            {VesselType.Unknown, "Unknown"},
            {VesselType.Probe, "Probe"},
            {VesselType.Relay, "Relay"},
            {VesselType.Rover, "Rover"},
            {VesselType.Lander, "Lander"},
            {VesselType.Ship, "Ship"},
            {VesselType.Plane, "Plane"},
            {VesselType.Station, "Station"},
            {VesselType.Base, "Base"},
            {VesselType.EVA, "EVA"},
            {VesselType.Flag, "Flag"},
        };

        #region Message Logging

        private static StringBuilder logSb = new StringBuilder();

        /// <summary>
        /// Log a message.  Logged regardless of the MAS Settings debug flag.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="values"></param>
        internal static void LogInfo(string format, params object[] values)
        {
            logSb.Remove(0, logSb.Length);
            logSb.Append("[AvionicsSystems] ").AppendFormat(format, values);
            UnityEngine.Debug.Log(logSb.ToString());
        }

        /// <summary>
        /// Log a message.  Logged regardless of the MAS Settings debug flag.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="values"></param>
        internal static void LogInfo(object who, string format, params object[] values)
        {
            logSb.Remove(0, logSb.Length);
            logSb.Append("[").Append(who.GetType().Name).Append("] ").AppendFormat(format, values);

            UnityEngine.Debug.Log(logSb.ToString());
        }

        /// <summary>
        /// Log a message.  Suppressed by the MAS Settings debug flag.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="values"></param>
        internal static void LogMessage(string format, params object[] values)
        {
            if (MASConfig.VerboseLogging)
            {
                logSb.Remove(0, logSb.Length);
                logSb.Append("[AvionicsSystems] ").AppendFormat(format, values);
                UnityEngine.Debug.Log(logSb.ToString());
            }
        }

        /// <summary>
        /// Log a message associated with an object.  Suppressed by the MAS Settings debug flag.
        /// </summary>
        /// <param name="who"></param>
        /// <param name="format"></param>
        /// <param name="values"></param>
        internal static void LogMessage(object who, string format, params object[] values)
        {
            if (MASConfig.VerboseLogging)
            {
                logSb.Remove(0, logSb.Length);
                logSb.Append("[").Append(who.GetType().Name).Append("] ").AppendFormat(format, values);

                UnityEngine.Debug.Log(logSb.ToString());
            }
        }

        /// <summary>
        /// Log a message.  Suppressed by the MAS Settings debug flag.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="values"></param>
        internal static void LogWarning(string format, params object[] values)
        {
            logSb.Remove(0, logSb.Length);
            logSb.Append("[AvionicsSystems] ").AppendFormat(format, values);
            UnityEngine.Debug.LogWarning(logSb.ToString());
        }

        /// <summary>
        /// Log a message associated with an object.  Suppressed by the MAS Settings debug flag.
        /// </summary>
        /// <param name="who"></param>
        /// <param name="format"></param>
        /// <param name="values"></param>
        internal static void LogWarning(object who, string format, params object[] values)
        {
            logSb.Remove(0, logSb.Length);
            logSb.Append("[").Append(who.GetType().Name).Append("] ").AppendFormat(format, values);

            UnityEngine.Debug.LogWarning(logSb.ToString());
        }

        /// <summary>
        /// Log an error
        /// </summary>
        /// <param name="format"></param>
        /// <param name="values"></param>
        internal static void LogErrorMessage(string format, params object[] values)
        {
            logSb.Remove(0, logSb.Length);
            logSb.Append("[AvionicsSystems] ").AppendFormat(format, values);
            UnityEngine.Debug.LogError(logSb.ToString());
        }

        /// <summary>
        /// Log an error associated with an object
        /// </summary>
        /// <param name="who"></param>
        /// <param name="format"></param>
        /// <param name="values"></param>
        internal static void LogErrorMessage(object who, string format, params object[] values)
        {
            logSb.Remove(0, logSb.Length);
            logSb.Append("[").Append(who.GetType().Name).Append("] ").AppendFormat(format, values);

            UnityEngine.Debug.LogError(logSb.ToString());
        }
        #endregion

        #region Numeric Utilities
        /// <summary>
        /// Returns true if the value falls between the two extents (order independent)
        /// </summary>
        /// <param name="value"></param>
        /// <param name="extent1"></param>
        /// <param name="extent2"></param>
        /// <returns></returns>
        internal static bool Between(this double value, double extent1, double extent2)
        {
            if (extent1 < extent2)
            {
                return (value >= extent1 && value <= extent2);
            }
            else
            {
                return (value <= extent1 && value >= extent2);
            }
        }

        /// <summary>
        /// Clamp a double between two extents
        /// </summary>
        /// <param name="value"></param>
        /// <param name="extent1"></param>
        /// <param name="extent2"></param>
        /// <returns></returns>
        internal static double Clamp(this double value, double extent1, double extent2)
        {
            if (extent1 < extent2)
            {
                value = Math.Max(Math.Min(value, extent2), extent1);
            }
            else
            {
                value = Math.Max(Math.Min(value, extent1), extent2);
            }

            return value;
        }

        /// <summary>
        /// Constrain an angle to the range [0, 360).
        /// </summary>
        /// <param name="angle">An input angle in degrees</param>
        /// <returns>The equivalent angle constrained to the range of 0 to 360 degrees.</returns>
        internal static double NormalizeAngle(double angle)
        {
#if VALIDATE_ANGLE_NORMALIZER
            double angle2 = angle;
            if (angle2 < 0.0)
            {
                while (angle2 < 0.0)
                {
                    angle2 += 360.0;
                }
            }
            else if (angle2 >= 360.0)
            {
                while (angle2 >= 360.0)
                {
                    angle2 -= 360.0;
                }
            }
#endif
            if (angle < 0.0)
            {
                angle = 360.0 + (angle % 360.0);
#if VALIDATE_ANGLE_NORMALIZER
                if (!Mathf.Approximately((float)angle, (float)angle2))
                {
                    Utility.LogMessage(angle, "Got {0,6:0.00}, expect {1,6:0.00} (mod-)", angle, angle2);
                }
#endif
            }
            else if (angle >= 360.0)
            {
                angle = angle % 360.0;
#if VALIDATE_ANGLE_NORMALIZER
                if (!Mathf.Approximately((float)angle, (float)angle2))
                {
                    Utility.LogMessage(angle, "Got {0,6:0.00}, expect {1,6:0.00} (mod+)", angle, angle2);
                }
#endif
            }
#if VALIDATE_ANGLE_NORMALIZER
            else
            {
                if (!Mathf.Approximately((float)angle, (float)angle2))
                {
                    Utility.LogMessage(angle, "Got {0,6:0.00}, expect {1,6:0.00} (direct)", angle, angle2);
                }
            }
#endif
            return angle;
        }

        /// <summary>
        /// Constrain longitude to the range [-180, 180).  KSP does not
        /// appear to normalize the value in Vessel consistently.
        /// </summary>
        /// <param name="angle">Longitudinal angle to normalize.</param>
        /// <returns>Normalized value in the range [-180, +180)</returns>
        internal static double NormalizeLongitude(double longitude)
        {
            return NormalizeAngle(longitude + 180.0) - 180.0;
        }

        /// <summary>
        /// Normalize a time by limiting it to the range of [0, orbit.period).
        /// 
        /// Assumes this is not an absolute time, so events in the past are moved
        /// forward to the next occurrence.
        /// </summary>
        /// <param name="time">Relative time to normalize.</param>
        /// <param name="o">Orbit to normalize to</param>
        /// <returns>Number in the range [0, o.period).</returns>
        internal static double NormalizeOrbitTime(double time, Orbit o)
        {
            if (time < 0.0)
            {
                return o.period - (-time % o.period);
            }
            else if (time < o.period)
            {
                return time;
            }
            else
            {
                return time % o.period;
            }
        }

        /// <summary>
        /// Remap the source variable from [sourceRange1, sourceRange2] into
        /// the range [destinationRange1, destinationRange2].  Convert to
        /// float as well, since we don't need maximum precision.
        /// </summary>
        /// <param name="sourceVariable"></param>
        /// <param name="sourceRange1"></param>
        /// <param name="sourceRange2"></param>
        /// <param name="destinationRange1"></param>
        /// <param name="destinationRange2"></param>
        /// <returns></returns>
        internal static float Remap(this double sourceVariable, double sourceRange1, double sourceRange2, double destinationRange1, double destinationRange2)
        {
            float iLerp = Mathf.InverseLerp((float)sourceRange1, (float)sourceRange2, (float)sourceVariable);
            return Mathf.Lerp((float)destinationRange1, (float)destinationRange2, iLerp);
        }
        #endregion

        #region Orbital Utilities

        /// <summary>
        /// Returns the orbital basis vectors for an orbit at a given time.
        /// </summary>
        /// <param name="orbitalVelocity">The orbital velocity (Orbit.getOrbitalVelocityAtUT())</param>
        /// <param name="relativePosition">The relative position (Orbit.getRelativePositionAtUT())</param>
        /// <param name="prograde">The prograde vector for the orbit at time `ut`.</param>
        /// <param name="radial">The radial vector for the orbit at time `ut`.</param>
        /// <param name="normal">The normal vector for the orbit at time `ut`.</param>
        internal static void GetOrbitalBasisVectors(Vector3d orbitalVelocity, Vector3d relativePosition, out Vector3d prograde, out Vector3d radial, out Vector3d normal)
        {
            prograde = orbitalVelocity.xzy.normalized;
            Vector3d upAtUt = relativePosition.xzy.normalized;

            normal = Vector3d.Cross(prograde, upAtUt);
            radial = Vector3d.Cross(normal, prograde);
        }

        /// <summary>
        /// Convenience function to properly order the components for a maneuver node.
        /// </summary>
        /// <param name="progradedV"></param>
        /// <param name="radialdV"></param>
        /// <param name="normaldV"></param>
        /// <returns></returns>
        internal static Vector3d ManeuverNode(double progradedV, double normaldV, double radialdV)
        {
            return new Vector3d(radialdV, normaldV, progradedV);
        }

        /// <summary>
        /// Returns the time in seconds until we cross the given radius
        /// </summary>
        /// <param name="orbit"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        internal static double NextTimeToRadius(Orbit orbit, double radius)
        {
            // How do I do this?  Like so:
            // TrueAnomalyAtRadius returns a TA between 0 and PI, representing
            // when the orbit crosses that altitude while ascending from Pe (0) to Ap (PI).
            double taAtRadius = orbit.TrueAnomalyAtRadius(radius);
            // GetUTForTrueAnomaly gives us a time for when that will occur.  I don't know
            // what parameter 2 is really supposed to do (wrapAfterSeconds), because after
            // subtracting vc.UT, I see values sometimes 2 orbits in the past.  Which is why
            // we have to normalize it here to the next time we cross that TA.
            double timeToTa1 = NormalizeOrbitTime(orbit.GetUTforTrueAnomaly(taAtRadius, orbit.period) - Planetarium.GetUniversalTime(), orbit);

            // Now, what about the other time we cross that altitude (in the range of -PI to 0)?
            // Easy.  The orbit is symmetrical around 0, so the other TA is -taAtAltitude.
            // I *could* use TrueAnomalyAtRadius and normalize the result, but I don't know the
            // complexity of that function, and there's an easy way to compute it: since
            // the TA is symmetrical, the time from the Pe to TA1 is the same as the time
            // from TA2 to Pe.

            // First, find the time-to-Pe that occurs before the time to TA1:
            double relevantPe = orbit.timeToPe;
            if (relevantPe > timeToTa1)
            {
                // If we've passed the Pe, but we haven't reached TA1, we
                // need to find the previous Pe
                relevantPe -= orbit.period;
            }

            // Then, we subtract the interval from TA1 to the Pe from the time
            // until the Pe (that is, T(Pe) - (T(TA1) - T(Pe)), rearranging terms:
            double timeToTa2 = NormalizeOrbitTime(2.0 * relevantPe - timeToTa1, orbit);

            // Whichever occurs first is the one we care about:
            return Math.Min(timeToTa1, timeToTa2);
        }

        /// <summary>
        /// Returns the velocity required for a circular orbit at a given radius (*not* altitude).
        /// </summary>
        /// <param name="GM">CelestialBody.gravParameter</param>
        /// <param name="radius">radius in meters</param>
        /// <returns>Required velocity in m/s.</returns>
        internal static double OrbitalVelocity(double GM, double radius)
        {
            return Math.Sqrt(GM / radius);
        }

        #endregion

        /// <summary>
        /// Put something on-screen when an error occurs.
        /// </summary>
        /// <param name="message"></param>
        internal static void ComplainLoudly(string message)
        {
            string formattedMessage = String.Format("[AvionicsSystems] INITIALIZATION ERROR: {0}", message);
            UnityEngine.Debug.LogError(formattedMessage);
            ScreenMessages.PostScreenMessage("#MAS_Initialization_Error", 120, ScreenMessageStyle.UPPER_CENTER);
        }

        private static StringBuilder strb = new StringBuilder();
        /// <summary>
        /// Instead of everyone instantiating one of these, just keep it
        /// available here for temporary use.
        /// </summary>
        /// <returns>Global string builder, cleared and ready for use.</returns>
        internal static StringBuilder GetStringBuilder()
        {
            strb.Remove(0, strb.Length);
            return strb;
        }

        /// <summary>
        /// Helper method to construct a name for a game object.
        /// </summary>
        /// <param name="typeName"></param>
        /// <param name="objectName"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        internal static string ComposeObjectName(string typeName, string objectName, int id)
        {
            StringBuilder sb = StringBuilderCache.Acquire();

            sb.Append(typeName).Append("-").Append(objectName).AppendFormat("-{0}", id);

            return sb.ToStringAndRelease();
        }

        /// <summary>
        /// Helper method to construct a name for a game object.
        /// </summary>
        /// <param name="goName"></param>
        /// <param name="typeName"></param>
        /// <param name="objectName"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        internal static string ComposeObjectName(string goName, string typeName, string objectName, int id)
        {
            StringBuilder sb = StringBuilderCache.Acquire();

            sb.Append(goName).Append("-").Append(typeName).Append("-").Append(objectName).AppendFormat("-{0}", id);

            return sb.ToStringAndRelease();
        }

        private static void DumpConfigNode(ConfigNode node, int depth)
        {
            strb.Remove(0, strb.Length);
            if (depth > 0)
            {
                strb.Append(' ', depth);
            }
            strb.Append('+');
            strb.Append(' ');
            strb.Append(node.name);
            if (!node.HasData)
            {
                strb.Append(" - has no data");
            }
            LogMessage(strb.ToString());
            if (!node.HasData)
            {
                return;
            }

            var vals = node.values;
            if (vals.Count == 0)
            {
                strb.Remove(0, strb.Length);
                if (depth > 0)
                {
                    strb.Append(' ', depth);
                }
                strb.Append("- No values");
                LogMessage(strb.ToString());
            }
            for (int i = 0; i < vals.Count; ++i)
            {
                strb.Remove(0, strb.Length);
                if (depth > 0)
                {
                    strb.Append(' ', depth);
                }
                strb.Append('-');
                strb.Append(' ');
                strb.Append(vals[i].name);
                strb.Append(" = ");
                strb.Append(vals[i].value);
                LogMessage(strb.ToString());
            }

            var nodes = node.nodes;
            if (nodes.Count == 0)
            {
                strb.Remove(0, strb.Length);
                if (depth > 0)
                {
                    strb.Append(' ', depth);
                }
                strb.Append("- No child ConfigNode");
                LogMessage(strb.ToString());
            }
            for (int i = 0; i < nodes.Count; ++i)
            {
                DumpConfigNode(nodes[i], depth + 1);
            }
        }

        /// <summary>
        /// Debug utility to dump config node and its children to the log.
        /// </summary>
        /// <param name="node"></param>
        internal static void DebugDumpConfigNode(ConfigNode node)
        {
            DumpConfigNode(node, 0);
        }

        /// <summary>
        /// Look up the ConfigNode for the named MAS_PAGE.
        /// </summary>
        /// <param name="pageName">Name of the requested page configuration.</param>
        /// <returns>The ConfigNode, or null if it wasn't found.</returns>
        internal static ConfigNode GetPageConfigNode(string pageName)
        {
            ConfigNode[] asPageNodes = GameDatabase.Instance.GetConfigNodes("MAS_PAGE");

            for (int nodeIdx = asPageNodes.Length - 1; nodeIdx >= 0; --nodeIdx)
            {
                string nodeName = string.Empty;
                if (asPageNodes[nodeIdx].TryGetValue("name", ref nodeName) && nodeName == pageName)
                {
                    return asPageNodes[nodeIdx];
                }
            }

            return null;
        }

        /// <summary>
        /// Find the ConfigNode corresponding to a particular module in a part.
        /// </summary>
        /// <param name="part">The part to search</param>
        /// <param name="moduleName">Name of the module</param>
        /// <param name="index">The index of the part module (0 for the first one, etc)</param>
        /// <returns></returns>
        internal static ConfigNode GetPartModuleConfigNode(Part part, string moduleName, int index)
        {
            ConfigNode[] moduleConfigs = part.partInfo.partConfig.GetNodes("MODULE");
            int numSeen = 0;
            for (int i = 0; i < moduleConfigs.Length; ++i)
            {
                if (moduleConfigs[i].GetValue("name") == moduleName)
                {
                    if (numSeen == index)
                    {
                        return moduleConfigs[i];
                    }
                    ++numSeen;
                }
            }

            return null;
        }

        /// <summary>
        /// Find the ConfigNode corresponding to a particular module.
        /// </summary>
        /// <param name="propName">Name of the prop</param>
        /// <param name="moduleName">The name of the module</param>
        /// <returns></returns>
        internal static ConfigNode GetPropModuleConfigNode(string propName, string moduleName)
        {
            ConfigNode[] dbNodes = GameDatabase.Instance.GetConfigNodes("PROP");

            for (int nodeIdx = dbNodes.Length - 1; nodeIdx >= 0; --nodeIdx)
            {
                if (dbNodes[nodeIdx].GetValue("name") == propName)
                {
                    ConfigNode[] moduleNodes = dbNodes[nodeIdx].GetNodes("MODULE");
                    for (int i = moduleNodes.Length - 1; i >= 0; --i)
                    {
                        if (moduleNodes[i].GetValue("name") == moduleName)
                        {
                            return moduleNodes[i];
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Search through loaded assemblies to find the specified Type that's
        /// in the specified assembly.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <param name="fullTypeName"></param>
        /// <returns></returns>
        internal static Type GetExportedType(string assemblyName, string fullTypeName)
        {
            int assyCount = AssemblyLoader.loadedAssemblies.Count;
            for (int assyIndex = 0; assyIndex < assyCount; ++assyIndex)
            {
                AssemblyLoader.LoadedAssembly assy = AssemblyLoader.loadedAssemblies[assyIndex];
                if (assy.name == assemblyName)
                {
                    Type[] exportedTypes = assy.assembly.GetExportedTypes();
                    int typeCount = exportedTypes.Length;
                    for (int typeIndex = 0; typeIndex < typeCount; ++typeIndex)
                    {
                        if (exportedTypes[typeIndex].FullName == fullTypeName)
                        {
                            return exportedTypes[typeIndex];
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Convert a string to a Color32; supports RasterPropMonitor COLOR_
        /// names.
        /// </summary>
        /// <param name="colorString">String to convert</param>
        /// <param name="comp">Reference to the ASFlightComputer</param>
        /// <returns></returns>
        internal static UnityEngine.Color32 ParseColor32(string colorString, MASFlightComputer comp)
        {
            colorString = colorString.Trim();

            if (colorString.StartsWith("COLOR_"))
            {
                // Using a RasterPropMonitor named color.
                return comp.GetNamedColor(colorString);
            }
            else
            {
                return ConfigNode.ParseColor32(colorString);
            }
        }

        /// <summary>
        /// Splits an arbitrary comma-delimited string of variables which may include Lua functions that
        /// themselves contain commas.
        /// Returns an array of strings, or a zero-length array if the string could not be
        /// split.
        /// </summary>
        /// <param name="variableListString">The string to parse</param>
        /// <returns>Array of strings split at the commas</returns>
        internal static string[] SplitVariableList(string variableListString)
        {
            // Instead of having to create a specialized parser to handle an arbitrary
            // comma-delimited string, we wrap the provided list in parenthesis to convince
            // the parser that it's a multi-parameter function.
            StringBuilder sb = Utility.GetStringBuilder();
            sb.Append("fn(");
            sb.Append(variableListString);
            sb.Append(")");
            CodeGen.Parser.CompilerResult result = CodeGen.Parser.TryParse(sb.ToString());
            if (result.type == CodeGen.Parser.ResultType.EXPRESSION_TREE)
            {
                CodeGen.CallExpression callExpression = result.expressionTree as CodeGen.CallExpression;

                int numArgs = callExpression.NumArgs();

                string[] returnString = new string[numArgs];
                for (int i = 0; i < numArgs; ++i)
                {
                    returnString[i] = callExpression.Arg(i).CanonicalName();
                }
                return returnString;
            }

            return new string[0];
        }

        /// <summary>
        /// Computes Stagnation Pressure using gamma (ratio of specific heats)
        /// and Mach number.
        /// Per https://en.wikipedia.org/wiki/Stagnation_pressure
        /// </summary>
        /// <param name="gamma">Ratio of specific heats (CelestialBody.atmosphereAdiabaticIndex)</param>
        /// <param name="M">Mach number (Vessel.mach)</param>
        /// <returns></returns>
        internal static double StagnationPressure(double gamma, double M)
        {
            double term = 1.0 + 0.5 * (gamma - 1.0) * M * M;
            return Math.Pow(term, gamma / (gamma - 1.0));
        }
    }
}
