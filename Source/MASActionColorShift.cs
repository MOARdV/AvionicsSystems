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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    internal class MASActionColorShift : IMASSubComponent
    {
        private Material[] localMaterial = new Material[0];
        private readonly int colorIndex;
        private readonly bool blend;
        private readonly bool useFlash;
        private bool flashOn = true;
        private bool currentState = false;
        private float currentBlend = 0.0f;
        private readonly float flashRate = 0.0f;
        private Color activeColor = XKCDColors.Black;
        private Color passiveColor;

        internal MASActionColorShift(ConfigNode config, InternalProp prop, MASFlightComputer comp)
            : base(config, prop, comp)
        {
            string transform = string.Empty;
            if (!config.TryGetValue("transform", ref transform))
            {
                throw new ArgumentException("Missing 'transform' in COLOR_SHIFT " + name);
            }
            string[] transforms = transform.Split(',');

            string colorName = "_EmissiveColor";
            config.TryGetValue("colorName", ref colorName);
            colorIndex = Shader.PropertyToID(colorName.Trim());

            localMaterial = new Material[transforms.Length];
            for (int i = transforms.Length - 1; i >= 0; --i)
            {
                try
                {
                    Transform t = prop.FindModelTransform(transforms[i].Trim());
                    Renderer r = t.GetComponent<Renderer>();
                    localMaterial[i] = r.material;
                }
                catch (Exception e)
                {
                    Utility.LogError(this, "Can't find transform {0} in COLOR_SHIFT {1}", transforms[i].Trim(), name);
                    throw e;
                }
            }

            // activeColor, passiveColor
            string passiveColorStr = string.Empty;
            if (!config.TryGetValue("passiveColor", ref passiveColorStr))
            {
                throw new ArgumentException("Invalid or missing 'passiveColor' in COLOR_SHIFT " + name);
            }
            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            blend = false;
            config.TryGetValue("blend", ref blend);

            if (blend == false && config.TryGetValue("flashRate", ref flashRate) && flashRate > 0.0f)
            {
                useFlash = true;
                comp.RegisterFlashCallback(flashRate, FlashToggle);
            }

            Color32 namedColor;
            if (comp.TryGetNamedColor(passiveColorStr, out namedColor))
            {
                passiveColor = namedColor;
            }
            else
            {
                string[] startColors = Utility.SplitVariableList(passiveColorStr);
                if (startColors.Length < 3 || startColors.Length > 4)
                {
                    throw new ArgumentException("passiveColor does not contain 3 or 4 values in COLOR_SHIFT " + name);
                }

                variableRegistrar.RegisterVariableChangeCallback(startColors[0], (double newValue) =>
                {
                    passiveColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                    if (blend)
                    {
                        UpdateBlendColor();
                    }
                    else
                    {
                        UpdateBooleanColor();
                    }
                });

                variableRegistrar.RegisterVariableChangeCallback(startColors[1], (double newValue) =>
                {
                    passiveColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                    if (blend)
                    {
                        UpdateBlendColor();
                    }
                    else
                    {
                        UpdateBooleanColor();
                    }
                });

                variableRegistrar.RegisterVariableChangeCallback(startColors[2], (double newValue) =>
                {
                    passiveColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                    if (blend)
                    {
                        UpdateBlendColor();
                    }
                    else
                    {
                        UpdateBooleanColor();
                    }
                });

                if (startColors.Length == 4)
                {
                    variableRegistrar.RegisterVariableChangeCallback(startColors[3], (double newValue) =>
                    {
                        passiveColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        if (blend)
                        {
                            UpdateBlendColor();
                        }
                        else
                        {
                            UpdateBooleanColor();
                        }
                    });
                }
            }

            // Final validations
            if (blend || useFlash || !string.IsNullOrEmpty(variableName))
            {
                string activeColorStr = string.Empty;
                if (string.IsNullOrEmpty(variableName))
                {
                    throw new ArgumentException("Invalid or missing 'variable' in COLOR_SHIFT " + name);
                }
                else if (config.TryGetValue("activeColor", ref activeColorStr))
                {
                    if (comp.TryGetNamedColor(activeColorStr, out namedColor))
                    {
                        activeColor = namedColor;
                    }
                    else
                    {
                        string[] startColors = Utility.SplitVariableList(activeColorStr);
                        if (startColors.Length < 3 || startColors.Length > 4)
                        {
                            throw new ArgumentException("activeColor does not contain 3 or 4 values in COLOR_SHIFT " + name);
                        }

                        variableRegistrar.RegisterVariableChangeCallback(startColors[0], (double newValue) =>
                        {
                            activeColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            if (blend)
                            {
                                UpdateBlendColor();
                            }
                            else
                            {
                                UpdateBooleanColor();
                            }
                        });

                        variableRegistrar.RegisterVariableChangeCallback(startColors[1], (double newValue) =>
                        {
                            activeColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            if (blend)
                            {
                                UpdateBlendColor();
                            }
                            else
                            {
                                UpdateBooleanColor();
                            }
                        });

                        variableRegistrar.RegisterVariableChangeCallback(startColors[2], (double newValue) =>
                        {
                            activeColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            if (blend)
                            {
                                UpdateBlendColor();
                            }
                            else
                            {
                                UpdateBooleanColor();
                            }
                        });

                        if (startColors.Length == 4)
                        {
                            variableRegistrar.RegisterVariableChangeCallback(startColors[3], (double newValue) =>
                            {
                                activeColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                                if (blend)
                                {
                                    UpdateBlendColor();
                                }
                                else
                                {
                                    UpdateBooleanColor();
                                }
                            });
                        }
                    }
                }
                else
                {
                    throw new ArgumentException("Invalid or missing 'activeColor' in COLOR_SHIFT " + name);
                }
            }

            // Make everything a known value before the callback fires.
            for (int i = localMaterial.Length - 1; i >= 0; --i)
            {
                localMaterial[i].SetColor(colorIndex, passiveColor);
            }

            if (!string.IsNullOrEmpty(variableName))
            {
                variableRegistrar.RegisterVariableChangeCallback(variableName, VariableCallback);
            }
        }

        /// <summary>
        /// Update blend-mode colors
        /// </summary>
        private void UpdateBlendColor()
        {
            Color newColor = Color.Lerp(passiveColor, activeColor, currentBlend);
            for (int i = localMaterial.Length - 1; i >= 0; --i)
            {
                localMaterial[i].SetColor(colorIndex, newColor);
            }
        }

        /// <summary>
        /// Update boolean-mode colors.
        /// </summary>
        private void UpdateBooleanColor()
        {
            for (int i = localMaterial.Length - 1; i >= 0; --i)
            {
                localMaterial[i].SetColor(colorIndex, (currentState && flashOn) ? activeColor : passiveColor);
            }
        }

        /// <summary>
        /// Handle a changed value
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue)
        {
            if (blend)
            {
                float newBlend = Mathf.Clamp01((float)newValue);

                if (!Mathf.Approximately(newBlend, currentBlend))
                {
                    currentBlend = newBlend;
                    UpdateBlendColor();
                }
            }
            else
            {
                bool newState = (newValue > 0.0);

                if (newState != currentState)
                {
                    currentState = newState;
                    UpdateBooleanColor();
                }
            }
        }

        /// <summary>
        /// Callback for toggling flash
        /// </summary>
        /// <param name="newFlashState"></param>
        private void FlashToggle(bool newFlashState)
        {
            flashOn = newFlashState;
            if (currentState)
            {
                for (int i = localMaterial.Length - 1; i >= 0; --i)
                {
                    localMaterial[i].SetColor(colorIndex, (flashOn) ? activeColor : passiveColor);
                }
            }
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            variableRegistrar.ReleaseResources(); ;

            if (useFlash)
            {
                comp.UnregisterFlashCallback(flashRate, FlashToggle);
            }

            for (int i = localMaterial.Length - 1; i >= 0; --i)
            {
                UnityEngine.Object.Destroy(localMaterial[i]);
            }
        }
    }
}
