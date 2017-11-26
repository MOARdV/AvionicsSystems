/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016 - 2017 MOARdV
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
    /// MASActionTextureShift implements a simple fire-and-forget texture shift
    /// for a specified transform.  This allows multiple props to use a texture
    /// atlas in some places, reducing the need to make multiple textures.
    /// </summary>
    internal class MASActionTextureShift : IMASSubComponent
    {
        private string name = "anonymous";
        private string variableName = string.Empty;
        private Material localMaterial = null;
        private Vector2[] startUV;
        private Vector2[] endUV;
        private MASFlightComputer.Variable range1, range2;
        private readonly bool blend;
        private readonly bool rangeMode;
        private bool currentState = false;
        private float currentBlend = 0.0f;
        private string[] layer;

        internal MASActionTextureShift(ConfigNode config, InternalProp prop, MASFlightComputer comp)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "anonymous";
            }

            string transform = string.Empty;
            if (!config.TryGetValue("transform", ref transform))
            {
                throw new ArgumentException("Missing 'transform' in TEXTURE_SHIFT " + name);
            }

            string layers = "_MainTex";
            config.TryGetValue("layers", ref layers);

            Vector2 rawStartUV = Vector2.zero;
            Vector2 rawEndUV = Vector2.zero;
            if (!config.TryGetValue("startUV", ref rawStartUV))
            {
                throw new ArgumentException("Missing or invalid 'startUV' in TEXTURE_SHIFT " + name);
            }

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
                    throw new ArgumentException("Incorrect number of values in 'range' in TEXTURE_SHIFT " + name);
                }
                range1 = comp.GetVariable(ranges[0], prop);
                range2 = comp.GetVariable(ranges[1], prop);
                rangeMode = true;

                blend = false;
                config.TryGetValue("blend", ref blend);

                // TODO: Support rate-limited changes
            }
            else
            {
                blend = false;
                rangeMode = false;
            }

            // Final validations
            if (rangeMode || !string.IsNullOrEmpty(variableName))
            {
                if (string.IsNullOrEmpty(variableName))
                {
                    throw new ArgumentException("Invalid or missing 'variable' in TEXTURE_SHIFT " + name);
                }
                else if (!config.TryGetValue("endUV", ref rawEndUV))
                {
                    throw new ArgumentException("Invalid or missing 'endUV' in TEXTURE_SHIFT " + name);
                }
            }

            Transform t = prop.FindModelTransform(transform);
            Renderer r = t.GetComponent<Renderer>();
            localMaterial = r.material;

            layer = layers.Split();
            int layerLength = layer.Length;
            startUV = new Vector2[layerLength];
            endUV = new Vector2[layerLength];
            for (int i = 0; i < layerLength; ++i)
            {
                Vector2 baseOffset = localMaterial.GetTextureOffset(layer[i]);
                startUV[i] = rawStartUV + baseOffset;
                endUV[i] = rawEndUV + baseOffset;
                layer[i] = layer[i].Trim();
                localMaterial.SetTextureOffset(layer[i], startUV[i]);
            }

            if (string.IsNullOrEmpty(variableName))
            {
                Utility.LogMessage(this, "TEXTURE_SHIFT {0} configured as static texture shift, with no variable defined", name);
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

                    int layerLength = layer.Length;
                    for (int i = 0; i < layerLength; ++i)
                    {
                        Vector2 newUV = Vector2.Lerp(startUV[i], endUV[i], currentBlend);
                        localMaterial.SetTextureOffset(layer[i], newUV);
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

                    int layerLength = layer.Length;
                    if (currentState)
                    {
                        for (int i = 0; i < layerLength; ++i)
                        {
                            localMaterial.SetTextureOffset(layer[i], endUV[i]);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < layerLength; ++i)
                        {
                            localMaterial.SetTextureOffset(layer[i], startUV[i]);
                        }
                    }
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
            }
            UnityEngine.Object.Destroy(localMaterial);
        }
    }
}
