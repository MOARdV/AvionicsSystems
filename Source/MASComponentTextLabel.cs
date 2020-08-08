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
    class MASComponentTextLabel : IMASSubComponent
    {
        private Color passiveColor = XKCDColors.White;
        private Color activeColor = XKCDColors.White;
        private readonly bool blend;
        private bool currentState = false;
        private bool flashOn = true;
        private float currentBlend = 0.0f;
        private float flashRate = 0.0f;
        private float fontSize = 1.0f;
        private MdVTextMesh textObj;
        private MASFlightComputer comp;
        private EmissiveMode emissiveMode = EmissiveMode.always;
        private readonly int emissiveFactorIndex = Shader.PropertyToID("_EmissiveFactor");
        enum EmissiveMode
        {
            always,
            never,
            active,
            passive,
            flash
        };

        internal MASComponentTextLabel(ConfigNode config, InternalProp prop, MASFlightComputer comp)
            : base(config, prop, comp)
        {
            string transform = string.Empty;
            if (!config.TryGetValue("transform", ref transform))
            {
                throw new ArgumentException("Missing 'transform' in TEXT_LABEL " + name);
            }

            string fontName = string.Empty;
            if (!config.TryGetValue("font", ref fontName))
            {
                throw new ArgumentException("Invalid or missing 'font' in TEXT_LABEL " + name);
            }

            string styleStr = string.Empty;
            FontStyle style = FontStyle.Normal;
            if (config.TryGetValue("style", ref styleStr))
            {
                style = MdVTextMesh.FontStyle(styleStr);
            }

            string text = string.Empty;
            if (!config.TryGetValue("text", ref text))
            {
                throw new ArgumentException("Invalid or missing 'text' in TEXT_LABEL " + name);
            }

            if (!config.TryGetValue("fontSize", ref fontSize))
            {
                throw new ArgumentException("Invalid or missing 'fontSize' in TEXT_LABEL " + name);
            }

            Vector2 transformOffset = Vector2.zero;
            if (!config.TryGetValue("transformOffset", ref transformOffset))
            {
                transformOffset = Vector2.zero;
            }

            Transform textObjTransform = prop.FindModelTransform(transform);
            if (textObjTransform == null)
            {
                throw new ArgumentException("Unable to find transform '" + transform + "' in TEXT_LABEL " + name);
            }
            Vector3 localScale = prop.transform.localScale;

            Transform offsetTransform = new GameObject().transform;
            offsetTransform.gameObject.name = Utility.ComposeObjectName(this.GetType().Name, name, prop.propID);
            offsetTransform.gameObject.layer = textObjTransform.gameObject.layer;
            offsetTransform.SetParent(textObjTransform, false);
            offsetTransform.Translate(transformOffset.x * localScale.x, transformOffset.y * localScale.y, 0.0f);

            textObj = offsetTransform.gameObject.AddComponent<MdVTextMesh>();

            Font font = MASLoader.GetFont(fontName.Trim());
            if (font == null)
            {
                throw new ArgumentNullException("Unable to load font " + fontName + " in TEXT_LABEL " + name);
            }

            float lineSpacing = 1.0f;
            if (!config.TryGetValue("lineSpacing", ref lineSpacing))
            {
                lineSpacing = 1.0f;
            }

            float sizeScalar = 32.0f / (float)font.fontSize;
            textObj.SetFont(font, font.fontSize, fontSize * 0.00005f * sizeScalar);
            textObj.SetLineSpacing(lineSpacing);
            textObj.fontStyle = style;

            string passiveColorStr = string.Empty;
            if (!config.TryGetValue("passiveColor", ref passiveColorStr))
            {
                throw new ArgumentException("Invalid or missing 'passiveColor' in TEXT_LABEL " + name);
            }
            else
            {
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
                        throw new ArgumentException("passiveColor does not contain 3 or 4 values in TEXT_LABEL " + name);
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
            }
            // Final validations
            bool usesMulticolor = false;
            string activeColorStr = string.Empty;
            if (config.TryGetValue("activeColor", ref activeColorStr))
            {
                usesMulticolor = true;

                Color32 namedColor;
                if (comp.TryGetNamedColor(activeColorStr, out namedColor))
                {
                    activeColor = namedColor;
                }
                else
                {
                    string[] startColors = Utility.SplitVariableList(activeColorStr);
                    if (startColors.Length < 3 || startColors.Length > 4)
                    {
                        throw new ArgumentException("activeColor does not contain 3 or 4 values in TEXT_LABEL " + name);
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

            string anchor = string.Empty;
            if (!config.TryGetValue("anchor", ref anchor))
            {
                anchor = string.Empty;
            }

            if (!string.IsNullOrEmpty(anchor))
            {
                if (anchor == TextAnchor.LowerCenter.ToString())
                {
                    textObj.anchor = TextAnchor.LowerCenter;
                }
                else if (anchor == TextAnchor.LowerLeft.ToString())
                {
                    textObj.anchor = TextAnchor.LowerLeft;
                }
                else if (anchor == TextAnchor.LowerRight.ToString())
                {
                    textObj.anchor = TextAnchor.LowerRight;
                }
                else if (anchor == TextAnchor.MiddleCenter.ToString())
                {
                    textObj.anchor = TextAnchor.MiddleCenter;
                }
                else if (anchor == TextAnchor.MiddleLeft.ToString())
                {
                    textObj.anchor = TextAnchor.MiddleLeft;
                }
                else if (anchor == TextAnchor.MiddleRight.ToString())
                {
                    textObj.anchor = TextAnchor.MiddleRight;
                }
                else if (anchor == TextAnchor.UpperCenter.ToString())
                {
                    textObj.anchor = TextAnchor.UpperCenter;
                }
                else if (anchor == TextAnchor.UpperLeft.ToString())
                {
                    textObj.anchor = TextAnchor.UpperLeft;
                }
                else if (anchor == TextAnchor.UpperRight.ToString())
                {
                    textObj.anchor = TextAnchor.UpperRight;
                }
                else
                {
                    Utility.LogError(this, "Unrecognized anchor '{0}' in config for {1} ({2})", anchor, prop.propID, prop.propName);
                }
            }

            string alignment = string.Empty;
            if (!config.TryGetValue("alignment", ref alignment))
            {
                alignment = string.Empty;
            }
            if (!string.IsNullOrEmpty(alignment))
            {
                if (alignment == TextAlignment.Center.ToString())
                {
                    textObj.alignment = TextAlignment.Center;
                }
                else if (alignment == TextAlignment.Left.ToString())
                {
                    textObj.alignment = TextAlignment.Left;
                }
                else if (alignment == TextAlignment.Right.ToString())
                {
                    textObj.alignment = TextAlignment.Right;
                }
                else
                {
                    Utility.LogError(this, "Unrecognized alignment '{0}' in config for {1} ({2})", alignment, prop.propID, prop.propName);
                }
            }

            string emissive = string.Empty;
            config.TryGetValue("emissive", ref emissive);

            if (string.IsNullOrEmpty(emissive))
            {
                if (usesMulticolor)
                {
                    emissiveMode = EmissiveMode.active;
                }
                else
                {
                    emissiveMode = EmissiveMode.always;
                }
            }
            else if (emissive.ToLower() == EmissiveMode.always.ToString())
            {
                emissiveMode = EmissiveMode.always;
            }
            else if (emissive.ToLower() == EmissiveMode.never.ToString())
            {
                emissiveMode = EmissiveMode.never;
            }
            else if (emissive.ToLower() == EmissiveMode.active.ToString())
            {
                emissiveMode = EmissiveMode.active;
            }
            else if (emissive.ToLower() == EmissiveMode.passive.ToString())
            {
                emissiveMode = EmissiveMode.passive;
            }
            else if (emissive.ToLower() == EmissiveMode.flash.ToString())
            {
                emissiveMode = EmissiveMode.flash;
            }
            else
            {
                Utility.LogError(this, "Unrecognized emissive mode '{0}' in config for {1} ({2})", emissive, prop.propID, prop.propName);
                emissiveMode = EmissiveMode.always;
            }

            string variableName = string.Empty;
            if (!config.TryGetValue("variable", ref variableName) || string.IsNullOrEmpty(variableName))
            {
                if (usesMulticolor)
                {
                    throw new ArgumentException("Invalid or missing 'variable' in TEXT_LABEL " + name);
                }
            }
            else if (!usesMulticolor)
            {
                throw new ArgumentException("Invalid or missing 'activeColor' in TEXT_LABEL " + name);
            }

            config.TryGetValue("blend", ref blend);

            if (emissiveMode == EmissiveMode.flash)
            {
                if (blend == false && config.TryGetValue("flashRate", ref flashRate) && flashRate > 0.0f)
                {
                    this.comp = comp;
                    comp.RegisterFlashCallback(flashRate, FlashToggle);
                }
                else
                {
                    emissiveMode = EmissiveMode.active;
                }
            }

            bool immutable = false;
            if (!config.TryGetValue("oneshot", ref immutable))
            {
                immutable = false;
            }

            textObj.SetColor(passiveColor);
            textObj.SetText(text, immutable, false, comp, prop);

            UpdateShader();

            if (!string.IsNullOrEmpty(variableName))
            {
                variableRegistrar.RegisterVariableChangeCallback(variableName, VariableCallback);
            }
        }

        /// <summary>
        /// Update a blended color.
        /// </summary>
        private void UpdateBlendColor()
        {
            Color32 newColor = Color32.Lerp(passiveColor, activeColor, currentBlend);
            textObj.SetColor(newColor);

            textObj.material.SetFloat(emissiveFactorIndex, currentBlend);
        }

        /// <summary>
        /// Update a boolean-mode color.
        /// </summary>
        private void UpdateBooleanColor()
        {
            if (currentState && flashOn)
            {
                textObj.SetColor(activeColor);
            }
            else
            {
                textObj.SetColor(passiveColor);
            }

            UpdateShader();
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
                textObj.SetColor((flashOn) ? activeColor : passiveColor);
                UpdateShader();
            }
        }

        /// <summary>
        /// Update the shader parameters
        /// </summary>
        private void UpdateShader()
        {
            float emissiveValue;
            if (emissiveMode == EmissiveMode.always)
            {
                emissiveValue = 1.0f;
            }
            else if (emissiveMode == EmissiveMode.never)
            {
                emissiveValue = 0.0f;
            }
            else if (emissiveMode == EmissiveMode.flash)
            {
                emissiveValue = (currentState && flashOn) ? 1.0f : 0.0f;
            }
            else if (currentState ^ (emissiveMode == EmissiveMode.passive))
            {
                emissiveValue = 1.0f;
            }
            else
            {
                emissiveValue = 0.0f;
            }

            textObj.material.SetFloat(emissiveFactorIndex, emissiveValue);
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            variableRegistrar.ReleaseResources();

            this.comp = null;
        }
    }
}
