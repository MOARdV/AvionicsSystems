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
    /// <summary>
    /// MODEL_SCALE is used to change the a transform's localScale.  The values
    /// of the two endpoints are added to the model's initial localScale, so
    /// a value of 0,0,0 will use the original scale untouched.
    /// </summary>
    class MASActionModelScale : IMASSubComponent
    {
        private string name = "(anonymous)";
        private string variableName = string.Empty;
        private Vector3 startScale = Vector3.zero;
        private Vector3 endScale = Vector3.zero;
        private Transform transform;
        private MASFlightComputer.Variable range1, range2;
        private readonly bool blend;
        private readonly bool rangeMode;
        private bool currentState = false;
        private float currentBlend = 0.0f;

        internal MASActionModelScale(ConfigNode config, InternalProp prop, MASFlightComputer comp)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "(anonymous)";
            }

            string transform = string.Empty;
            if (!config.TryGetValue("transform", ref transform))
            {
                throw new ArgumentException("Missing 'transform' in MODEL_SCALE " + name);
            }

            this.transform = prop.FindModelTransform(transform.Trim());
            if (this.transform == null)
            {
                throw new ArgumentException("Unable to find 'transform' " + transform + " for MODEL_SCALE " + name);
            }
            Vector3 initialScale = this.transform.localScale;

            if (!config.TryGetValue("variable", ref variableName) || string.IsNullOrEmpty(variableName))
            {
                throw new ArgumentException("Invalid or missing 'variable' in MODEL_SCALE " + name);
            }

            if (!config.TryGetValue("startScale", ref startScale))
            {
                throw new ArgumentException("Invalid or missing 'startScale' in MODEL_SCALE " + name);
            }
            else
            {
                startScale = startScale + initialScale;
            }

            if (!config.TryGetValue("endScale", ref endScale))
            {
                throw new ArgumentException("Invalid or missing 'endScale' in MODEL_SCALE " + name);
            }
            else
            {
                endScale = endScale + initialScale;
            }

            string range = string.Empty;
            if (config.TryGetValue("range", ref range))
            {
                string[] ranges = range.Split(',');
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in TEXTURE_SHIFT " + name);
                }
                range1 = comp.GetVariable(ranges[0]);
                range2 = comp.GetVariable(ranges[1]);
                rangeMode = true;

                blend = false;
                config.TryGetValue("blend", ref blend);
            }
            else
            {
                blend = false;
                rangeMode = false;
            }

            comp.RegisterNumericVariable(variableName, VariableCallback);
        }

        /// <summary>
        /// Handle a changed value
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue)
        {
            if (blend)
            {
                float newBlend = Mathf.Lerp((float)range1.SafeValue(), (float)range2.SafeValue(), (float)newValue);

                if (!Mathf.Approximately(newBlend, currentBlend))
                {
                    currentBlend = newBlend;

                    Vector3 newScale = Vector3.Lerp(startScale, endScale, currentBlend);
                    transform.localScale = newScale;
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

                    if (currentState)
                    {
                        transform.localScale = endScale;
                    }
                    else
                    {
                        transform.localScale = startScale;
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
        public void ReleaseResources(MASFlightComputer comp)
        {
            if (!string.IsNullOrEmpty(variableName))
            {
                comp.UnregisterNumericVariable(variableName, VariableCallback);
            }
            transform = null;
        }
    }
}
