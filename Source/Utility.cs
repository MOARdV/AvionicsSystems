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

namespace AvionicsSystems
{
    internal static class Utility
    {
        internal static readonly string[] NewLine = { Environment.NewLine };

        /// <summary>
        /// Log a message
        /// </summary>
        /// <param name="format"></param>
        /// <param name="values"></param>
        internal static void LogMessage(string format, params object[] values)
        {
            UnityEngine.Debug.Log(String.Format("[AvionicsSystems] " + format, values));
        }

        /// <summary>
        /// Log a message associated with an object.
        /// </summary>
        /// <param name="who"></param>
        /// <param name="format"></param>
        /// <param name="values"></param>
        internal static void LogMessage(object who, string format, params object[] values)
        {
            UnityEngine.Debug.Log(String.Format("[" + who.GetType().Name + "] " + format, values));
        }

        /// <summary>
        /// Log an error
        /// </summary>
        /// <param name="format"></param>
        /// <param name="values"></param>
        internal static void LogErrorMessage(string format, params object[] values)
        {
            UnityEngine.Debug.LogError(String.Format("[AvionicsSystems] " + format, values));
        }

        /// <summary>
        /// Log an error associated with an object
        /// </summary>
        /// <param name="who"></param>
        /// <param name="format"></param>
        /// <param name="values"></param>
        internal static void LogErrorMessage(object who, string format, params object[] values)
        {
            UnityEngine.Debug.LogError(String.Format("[" + who.GetType().Name + "] " + format, values));
        }

        /// <summary>
        /// Create an IASAction-based object from a ConfigNode
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        internal static IMASAction CreateAction(ConfigNode config, InternalProp prop, MASFlightComputer comp)
        {
            LogMessage(config, "Node {0} being parsed", config.name);

            if (config.name == "ANIMATION_PLAYER")
            {
                return new MASActionAnimationPlayer(config, prop, comp);
            }
            else if (config.name == "AUDIO_PLAYER")
            {
                return new MASActionAudioPlayer(config, prop, comp);
            }
            else if (config.name == "COLOR_SHIFT")
            {
                return new MASActionColorShift(config, prop, comp);
            }
            else if (config.name == "COLLIDER_EVENT")
            {
                return new MASActionColliderEvent(config, prop, comp);
            }
            else if (config.name == "INT_LIGHT")
            {
                return new MASActionIntLight(config, prop, comp);
            }
            else if (config.name == "MODEL_SCALE")
            {
                return new MASActionModelScale(config, prop, comp);
            }
            else if (config.name == "ROTATION")
            {
                return new MASActionRotation(config, prop, comp);
            }
            else if (config.name == "TEXT_LABEL")
            {
                return new MASActionTextLabel(config, prop, comp);
            }
            else if (config.name == "TEXTURE_SHIFT")
            {
                return new MASActionTextureShift(config, prop, comp);
            }
            else
            {
                LogMessage(config, "Unrecognized ASComponent child node {0} found", config.name);
                return null;
            }
        }

        /// <summary>
        /// Look up the ConfigNode for the named AS_PAGE.
        /// </summary>
        /// <param name="pageName">Name of the requested page configuration.</param>
        /// <returns>The ConfigNode, or null if it wasn't found.</returns>
        internal static ConfigNode GetPageConfigNode(string pageName)
        {
            ConfigNode[] asPageNodes = GameDatabase.Instance.GetConfigNodes("AS_PAGE");

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
        /// Find the ConfigNode corresponding to a particular module.
        /// </summary>
        /// <param name="propName">Name of the prop</param>
        /// <param name="moduleID">ID (index) of the node</param>
        /// <returns></returns>
        internal static ConfigNode GetPropModuleConfigNode(string propName, int moduleID)
        {
            ConfigNode[] dbNodes = GameDatabase.Instance.GetConfigNodes("PROP");

            for (int nodeIdx = dbNodes.Length - 1; nodeIdx >= 0; --nodeIdx)
            {
                if (dbNodes[nodeIdx].GetValue("name") == propName)
                {
                    ConfigNode[] moduleNodes = dbNodes[nodeIdx].GetNodes("MODULE");
                    if (moduleNodes.Length > moduleID)
                    {
                        return moduleNodes[moduleID];
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Returns true if the value falls between the two extents (order ignored)
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
    }
}
