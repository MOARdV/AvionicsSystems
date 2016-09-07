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
    internal class MASActionColorShift : IMASSubComponent
    {
        private string name = "(anonymous)";
        private string variableName;
        private Material[] localMaterial = new Material[0];
        private readonly int colorIndex;
        private MASFlightComputer.Variable range1, range2;
        private readonly bool blend;
        private readonly bool rangeMode;
        private readonly bool useFlash;
        private bool flashOn = true;
        private bool currentState = false;
        private float currentBlend = 0.0f;
        private readonly float flashRate = 0.0f;
        private Color32 activeColor = XKCDColors.Black;
        private Color32 passiveColor;

        internal MASActionColorShift(ConfigNode config, InternalProp prop, MASFlightComputer comp)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "(anonymous)";
            }

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
                    Utility.LogErrorMessage(this, "Can't find transform {0} in COLOR_SHIFT {1}", transforms[i].Trim(), name);
                    throw e;
                }
            }

            // activeColor, passiveColor
            string passiveColorStr = string.Empty;
            if (!config.TryGetValue("passiveColor", ref passiveColorStr))
            {
                throw new ArgumentException("Invalid or missing 'passiveColor' in COLOR_SHIFT " + name);
            }

            passiveColor = Utility.ParseColor32(passiveColorStr, comp);

            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            string range = string.Empty;
            if (config.TryGetValue("range", ref range))
            {
                string[] ranges = range.Split(',');
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in COLOR_SHIFT " + name);
                }
                range1 = comp.GetVariable(ranges[0], prop);
                range2 = comp.GetVariable(ranges[1], prop);
                rangeMode = true;

                blend = false;
                config.TryGetValue("blend", ref blend);

                if (blend == false && config.TryGetValue("flashRate", ref flashRate) && flashRate > 0.0f)
                {
                    useFlash = true;
                    comp.RegisterFlashCallback(flashRate, FlashToggle);
                }
            }
            else
            {
                blend = false;
                rangeMode = false;

                if (config.TryGetValue("flashRate", ref flashRate) && flashRate > 0.0f)
                {
                    useFlash = true;
                    comp.RegisterFlashCallback(flashRate, FlashToggle);
                }
            }

            // Final validations
            if (rangeMode || useFlash || !string.IsNullOrEmpty(variableName))
            {
                string activeColorStr = string.Empty;
                if (string.IsNullOrEmpty(variableName))
                {
                    throw new ArgumentException("Invalid or missing 'variable' in COLOR_SHIFT " + name);
                }
                else if (config.TryGetValue("activeColor", ref activeColorStr))
                {
                    activeColor = Utility.ParseColor32(activeColorStr, comp);
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

            if (string.IsNullOrEmpty(variableName))
            {
                Utility.LogMessage(this, "COLOR_SHIFT {0} configured as static color shift, with no variable defined", name);
            }
            else
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
                    for (int i = localMaterial.Length - 1; i >= 0; --i)
                    {
                        localMaterial[i].SetColor(colorIndex, newColor);
                    }
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
                    for (int i = localMaterial.Length - 1; i >= 0; --i)
                    {
                        localMaterial[i].SetColor(colorIndex, (currentState && flashOn) ? activeColor : passiveColor);
                    }
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
            for (int i = localMaterial.Length - 1; i >= 0; --i)
            {
                UnityEngine.Object.Destroy(localMaterial[i]);
            }
        }
    }
}
