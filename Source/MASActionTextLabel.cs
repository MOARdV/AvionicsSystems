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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    class MASActionTextLabel : IMASSubComponent
    {
        private string name = "(anonymous)";
        private string variableName = string.Empty;
        private MASFlightComputer.Variable range1, range2;
        private Color32 passiveColor = XKCDColors.White;
        private Color32 activeColor = XKCDColors.White;
        private readonly bool blend;
        private readonly bool rangeMode;
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

        internal MASActionTextLabel(ConfigNode config, InternalProp prop, MASFlightComputer comp)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "(anonymous)";
            }

            string transform = string.Empty;
            if (!config.TryGetValue("transform", ref transform))
            {
                throw new ArgumentException("Missing 'transform' in TEXT_LABEL " + name);
            }

            string passiveColorStr = string.Empty;
            if (!config.TryGetValue("passiveColor", ref passiveColorStr))
            {
                throw new ArgumentException("Invalid or missing 'passiveColor' in TEXT_LABEL " + name);
            }

            passiveColor = Utility.ParseColor32(passiveColorStr, comp);

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
            Vector3 localScale = prop.transform.localScale;

            Transform offsetTransform = new GameObject().transform;
            offsetTransform.gameObject.name = "MASActionLabel-" + prop.propID + "-" + name;
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

            textObj.SetFont(font, font.fontSize);
            float sizeScalar = 32.0f / (float)font.fontSize;
            textObj.SetCharacterSize(fontSize * 0.00005f * sizeScalar);
            textObj.SetLineSpacing(lineSpacing);
            textObj.fontStyle = style;

            // Final validations
            bool usesMulticolor = false;
            string activeColorStr = string.Empty;
            if (config.TryGetValue("activeColor", ref activeColorStr))
            {
                usesMulticolor = true;
                activeColor = Utility.ParseColor32(activeColorStr, comp);
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
                    Utility.LogErrorMessage(this, "Unrecognized anchor '{0}' in config for {1} ({2})", anchor, prop.propID, prop.propName);
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
                    Utility.LogErrorMessage(this, "Unrecognized alignment '{0}' in config for {1} ({2})", alignment, prop.propID, prop.propName);
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
                Utility.LogErrorMessage(this, "Unrecognized emissive mode '{0}' in config for {1} ({2})", emissive, prop.propID, prop.propName);
                emissiveMode = EmissiveMode.always;
            }

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

            string range = string.Empty;
            if (config.TryGetValue("range", ref range))
            {
                string[] ranges = range.Split(',');
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in TEXT_LABEL " + name);
                }
                range1 = comp.GetVariable(ranges[0], prop);
                range2 = comp.GetVariable(ranges[1], prop);
                rangeMode = true;

                blend = false;
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
            }
            else
            {
                blend = false;
                rangeMode = false;

                if (emissiveMode == EmissiveMode.flash)
                {
                    if (config.TryGetValue("flashRate", ref flashRate) && flashRate > 0.0f)
                    {
                        this.comp = comp;
                        comp.RegisterFlashCallback(flashRate, FlashToggle);
                    }
                    else
                    {
                        emissiveMode = EmissiveMode.active;
                    }
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
                comp.RegisterNumericVariable(variableName, prop, VariableCallback);
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
                float newBlend = Mathf.InverseLerp((float)range1.SafeValue(), (float)range2.SafeValue(), (float)newValue);

                if (!Mathf.Approximately(newBlend, currentBlend))
                {
                    currentBlend = newBlend;

                    Color32 newColor = Color32.Lerp(passiveColor, activeColor, currentBlend);
                    textObj.SetColor(newColor);

                    textObj.material.SetFloat(emissiveFactorIndex, currentBlend);
                }
            }
            else
            {
                if (rangeMode)
                {
                    newValue = (newValue.Between(range1.SafeValue(), range2.SafeValue())) ? 1.0 : 0.0;
                }

                bool newState = (newValue > 0.0);

                if (newState != currentState)
                {
                    currentState = newState;

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
        ///  Return the name of the action.
        /// </summary>
        /// <returns></returns>
        public string Name()
        {
            return name;
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            if (!string.IsNullOrEmpty(variableName))
            {
                comp.UnregisterNumericVariable(variableName, internalProp, VariableCallback);
                if (flashRate > 0.0f)
                {
                    comp.UnregisterFlashCallback(flashRate, FlashToggle);
                }
            }
            this.comp = null;
        }
    }
}
