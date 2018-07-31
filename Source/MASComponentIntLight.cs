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
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// The MASComponentIntLight component provides a means to interact with
    /// lights in the IVA.
    /// </summary>
    internal class MASComponentIntLight : IMASSubComponent
    {
        private bool currentState;
        private Light[] controlledLights;
        private Color lightColor;

        internal MASComponentIntLight(ConfigNode config, InternalProp prop, MASFlightComputer comp)
            : base(config, prop, comp)
        {
            string lightName = string.Empty;
            if (!config.TryGetValue("lightName", ref lightName))
            {
                throw new ArgumentException("Missing 'lightName' in INT_LIGHT " + name);
            }

            Light[] availableLights = prop.part.internalModel.FindModelComponents<Light>();
            if (availableLights != null && availableLights.Length > 0)
            {
                List<Light> lights = new List<Light>(availableLights);
                for (int i = lights.Count - 1; i >= 0; --i)
                {
                    if (lights[i].name != lightName)
                    {
                        lights.RemoveAt(i);
                    }
                }
                if (lights.Count > 0)
                {
                    controlledLights = lights.ToArray();
                }
            }

            if (controlledLights == null)
            {
                Utility.LogError(this, "No lights named '{0}' found in internalModel '{1}'", lightName, prop.part.internalModel.internalName);
                return;
            }

            string variableName = string.Empty;
            if (!config.TryGetValue("variable", ref variableName) || string.IsNullOrEmpty(variableName))
            {
                throw new ArgumentException("Invalid or missing 'variable' in INT_LIGHT " + name);
            }

            string colorString = string.Empty;
            if (config.TryGetValue("color", ref colorString))
            {
                Color32 color32;
                if (comp.TryGetNamedColor(colorString, out color32))
                {
                    lightColor = color32;
                    UpdateColor();
                }
                else
                {
                    string[] colors = Utility.SplitVariableList(colorString);
                    if (colors.Length < 3 || colors.Length > 4)
                    {
                        throw new ArgumentException("'lightColor' does not contain 3 or 4 values in INT_LIGHT " + name);
                    }

                    variableRegistrar.RegisterVariableChangeCallback(colors[0], (double newValue) =>
                    {
                        lightColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        UpdateColor();
                    });

                    variableRegistrar.RegisterVariableChangeCallback(colors[1], (double newValue) =>
                    {
                        lightColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        UpdateColor();
                    });

                    variableRegistrar.RegisterVariableChangeCallback(colors[2], (double newValue) =>
                    {
                        lightColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        UpdateColor();
                    });

                    if (colors.Length == 4)
                    {
                        variableRegistrar.RegisterVariableChangeCallback(colors[3], (double newValue) =>
                        {
                            lightColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            UpdateColor();
                        });
                    }
                }
            }

            string intensityString = string.Empty;
            if (config.TryGetValue("intensity", ref intensityString))
            {
                variableRegistrar.RegisterVariableChangeCallback(intensityString, (double newValue) =>
                {
                    float intensity = Mathf.Clamp((float)newValue, 0.0f, 8.0f);
                    for (int i = controlledLights.Length - 1; i >= 0; --i)
                    {
                        controlledLights[i].intensity = intensity;
                    }
                });
            }

            currentState = false;
            for (int i = 0; i < controlledLights.Length; ++i)
            {
                controlledLights[i].enabled = currentState;
            }

            variableRegistrar.RegisterVariableChangeCallback(variableName, VariableCallback);
        }

        /// <summary>
        /// Update the color for the lights.
        /// </summary>
        private void UpdateColor()
        {
            for (int i = controlledLights.Length - 1; i >= 0; --i)
            {
                controlledLights[i].color = lightColor;
            }
        }

        /// <summary>
        /// Variable callback used to update the animation when it is playing.
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue)
        {
            bool newState = (newValue > 0.0);

            if (newState != currentState)
            {
                currentState = newState;
                for (int i = 0; i < controlledLights.Length; ++i)
                {
                    controlledLights[i].enabled = currentState;
                }
            }
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp prop)
        {
            variableRegistrar.ReleaseResources();
        }
    }
}
