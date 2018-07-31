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
    /// MASComponentTextureShift implements a simple fire-and-forget texture shift
    /// for a specified transform.  This allows multiple props to use a texture
    /// atlas in some places, reducing the need to make multiple textures.
    /// </summary>
    internal class MASComponentTextureShift : IMASSubComponent
    {
        private Material localMaterial = null;
        private Vector2[] baseUV;
        private Vector2 startUV;
        private Vector2 endUV;
        private readonly bool blend;
        private bool currentState = false;
        private float currentBlend = 0.0f;
        private string[] layer;

        internal MASComponentTextureShift(ConfigNode config, InternalProp prop, MASFlightComputer comp)
            : base(config, prop, comp)
        {
            string transform = string.Empty;
            if (!config.TryGetValue("transform", ref transform))
            {
                throw new ArgumentException("Missing 'transform' in TEXTURE_SHIFT " + name);
            }

            string layers = "_MainTex";
            config.TryGetValue("layers", ref layers);

            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            config.TryGetValue("blend", ref blend);

            Transform t = prop.FindModelTransform(transform);
            Renderer r = t.GetComponent<Renderer>();
            localMaterial = r.material;

            layer = layers.Split();
            int layerLength = layer.Length;
            baseUV = new Vector2[layerLength];
            for (int i = 0; i < layerLength; ++i)
            {
                layer[i] = layer[i].Trim();
                baseUV[i] = localMaterial.GetTextureOffset(layer[i]);
            }

            string startUVstring = string.Empty;
            if (!config.TryGetValue("startUV", ref startUVstring))
            {
                throw new ArgumentException("Missing or invalid 'startUV' in TEXTURE_SHIFT " + name);
            }
            else
            {
                string[] uvs = Utility.SplitVariableList(startUVstring);
                if (uvs.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'startUV' in TEXTURE_SHIFT " + name);
                }

                variableRegistrar.RegisterVariableChangeCallback(uvs[0], (double newValue) =>
                {
                    startUV.x = (float)newValue;
                    UpdateUVs();
                });

                variableRegistrar.RegisterVariableChangeCallback(uvs[1], (double newValue) =>
                {
                    startUV.y = (float)newValue;
                    UpdateUVs();
                });
            }

            // Final validations
            if (!string.IsNullOrEmpty(variableName))
            {
                string endUVstring = string.Empty;
                if (string.IsNullOrEmpty(variableName))
                {
                    throw new ArgumentException("Invalid or missing 'variable' in TEXTURE_SHIFT " + name);
                }
                else if (!config.TryGetValue("endUV", ref endUVstring))
                {
                    throw new ArgumentException("Invalid or missing 'endUV' in TEXTURE_SHIFT " + name);
                }
                else
                {
                    string[] uvs = Utility.SplitVariableList(endUVstring);
                    if (uvs.Length != 2)
                    {
                        throw new ArgumentException("Incorrect number of values in 'endUV' in TEXTURE_SHIFT " + name);
                    }

                    variableRegistrar.RegisterVariableChangeCallback(uvs[0], (double newValue) =>
                    {
                        endUV.x = (float)newValue;
                        UpdateUVs();
                    });

                    variableRegistrar.RegisterVariableChangeCallback(uvs[1], (double newValue) =>
                    {
                        endUV.y = (float)newValue;
                        UpdateUVs();
                    });
                }
            }

            if (!string.IsNullOrEmpty(variableName))
            {
                variableRegistrar.RegisterVariableChangeCallback(variableName, VariableCallback);
            }
        }

        /// <summary>
        /// Refresh the UVs
        /// </summary>
        private void UpdateUVs()
        {
            int layerLength = layer.Length;
            if (blend)
            {
                for (int i = 0; i < layerLength; ++i)
                {
                    Vector2 newUV = Vector2.Lerp(baseUV[i] + startUV, baseUV[i] + endUV, currentBlend);
                    localMaterial.SetTextureOffset(layer[i], newUV);
                }
            }
            else if (currentState)
            {
                for (int i = 0; i < layerLength; ++i)
                {
                    localMaterial.SetTextureOffset(layer[i], baseUV[i] + endUV);
                }
            }
            else
            {
                for (int i = 0; i < layerLength; ++i)
                {
                    localMaterial.SetTextureOffset(layer[i], baseUV[i] + startUV);
                }
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

                    UpdateUVs();
                }
            }
            else
            {
                bool newState = (newValue > 0.0);

                if (newState != currentState)
                {
                    currentState = newState;

                    UpdateUVs();
                }
            }
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            variableRegistrar.ReleaseResources();

            UnityEngine.Object.Destroy(localMaterial);
        }
    }
}
