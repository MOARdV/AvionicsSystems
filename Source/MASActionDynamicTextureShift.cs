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
using UnityEngine;

namespace AvionicsSystems
{
    class MASActionDynamicTextureShift : IMASSubComponent
    {
        private string name = "(anonymous)";
        private string variableName = string.Empty;
        private Material localMaterial = null;
        private MASFlightComputer.Variable variable;
        //private MASFlightComputer.Variable range1, range2;
        private Vector2 currentUV = Vector2.zero;
        //private readonly bool blend;
        //private readonly bool rangeMode;
        //private bool currentState = false;
        //private float currentBlend = 0.0f;
        private Vector2[] startUV;
        private string[] layer;

        internal MASActionDynamicTextureShift(ConfigNode config, InternalProp prop, MASFlightComputer comp)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "(anonymous)";
            }

            string transform = string.Empty;
            if (!config.TryGetValue("transform", ref transform))
            {
                throw new ArgumentException("Missing 'transform' in DYNAMIC_TEXTURE_SHIFT " + name);
            }

            string layers = "_MainTex";
            config.TryGetValue("layers", ref layers);

            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }
            else
            {
                throw new ArgumentException("Invalid or missing 'variable' in DYNAMIC_TEXTURE_SHIFT " + name);
            }

            Transform t = prop.FindModelTransform(transform);
            Renderer r = t.GetComponent<Renderer>();
            localMaterial = r.material;

            layer = layers.Split();
            int layerLength = layer.Length;
            startUV = new Vector2[layerLength];
            for (int i = 0; i < layerLength; ++i)
            {
                startUV[i] = localMaterial.GetTextureOffset(layer[i]);
            }

            variable = comp.RegisterOnVariableChange(variableName, prop, VariableCallback);
            VariableCallback(); // Must explicitly call to initialize.
        }

        /// <summary>
        /// Handle a changed value
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback()
        {
            Vector2 newVector = variable.RawValue().ToObject<MASVector2>();

            if (newVector != currentUV)
            {
                currentUV = newVector;

                int layerLength = layer.Length;
                for (int i = 0; i < layerLength; ++i)
                {
                    localMaterial.SetTextureOffset(layer[i], currentUV + startUV[i]);
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
            variable = null;
            if (!string.IsNullOrEmpty(variableName))
            {
                comp.UnregisterOnVariableChange(variableName, internalProp, VariableCallback);
            }
            UnityEngine.Object.Destroy(localMaterial);
        }
    }
}
