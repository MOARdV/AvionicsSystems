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
    class MASActionTranslation : IMASSubComponent
    {
        private MASFlightComputer.Variable range1, range2;
        private Vector3 startTranslation, endTranslation;
        private MASFlightComputer comp;
        private Transform transform;
        private readonly bool blend;
        private readonly bool rangeMode;
        private readonly bool rateLimited;
        private readonly float speed;
        private bool coroutineActive = false;
        private bool currentState = false;
        private float currentBlend = 0.0f;
        private float goalBlend = 0.0f;

        internal MASActionTranslation(ConfigNode config, InternalProp prop, MASFlightComputer comp):base(config, prop, comp)
        {
            string transform = string.Empty;
            if (!config.TryGetValue("transform", ref transform))
            {
                throw new ArgumentException("Missing 'transform' in TRANSLATION " + name);
            }

            this.transform = prop.FindModelTransform(transform);

            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            startTranslation = Vector3.zero;
            if (!config.TryGetValue("startTranslation", ref startTranslation))
            {
                throw new ArgumentException("Missing 'startTranslation' in TRANSLATION " + name);
            }

            endTranslation = Vector3.zero;
            bool hasTranslationEnd = config.TryGetValue("endTranslation", ref endTranslation);
            if (!hasTranslationEnd)
            {
                endTranslation = Vector3.zero;
            }

            string range = string.Empty;
            if (config.TryGetValue("range", ref range))
            {
                string[] ranges = Utility.SplitVariableList(range);
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in TRANSLATION " + name);
                }
                range1 = comp.GetVariable(ranges[0], prop);
                range2 = comp.GetVariable(ranges[1], prop);
                rangeMode = true;

                blend = false;
                config.TryGetValue("blend", ref blend);

                if (blend)
                {
                    float speed = 0.0f;
                    if (config.TryGetValue("speed", ref speed) && speed > 0.0f)
                    {
                        this.comp = comp;
                        this.rateLimited = true;
                        this.speed = speed;
                    }
                }
            }
            else
            {
                blend = false;
                rangeMode = false;
            }

            // Final validations
            if (rangeMode || hasTranslationEnd)
            {
                if (string.IsNullOrEmpty(variableName))
                {
                    throw new ArgumentException("Invalid or missing 'variable' in TRANSLATION " + name);
                }
            }
            else if (!string.IsNullOrEmpty(variableName))
            {
                if (!hasTranslationEnd)
                {
                    throw new ArgumentException("Missing 'endTranslation' in TRANSLATION " + name);
                }
            }

            // Make everything a known value before the callback fires.
            startTranslation = this.transform.localPosition + startTranslation;
            endTranslation = this.transform.localPosition + endTranslation;

            this.transform.localPosition = startTranslation;

            if (string.IsNullOrEmpty(variableName))
            {
                Utility.LogMessage(this, "TRANSLATION {0} configured as static translation, with no variable defined", name);
            }
            else
            {
                variableRegistrar.RegisterNumericVariable(variableName, VariableCallback);
            }
        }

        /// <summary>
        /// Apply rate limitations to the blend.
        /// </summary>
        /// <param name="newBlend">New intended blend position</param>
        /// <returns>Rate-limited blend position</returns>
        private float RateLimitBlend(float newBlend)
        {
            float difference = Mathf.Abs(newBlend - currentBlend);
            float maxDelta = TimeWarp.deltaTime * speed;

            if (difference > maxDelta)
            {
                if (newBlend < currentBlend)
                {
                    newBlend = currentBlend - maxDelta;
                }
                else
                {
                    newBlend = currentBlend + maxDelta;
                }

                if (!coroutineActive)
                {
                    comp.StartCoroutine(TimedRotationCoroutine());
                }
            }

            return newBlend;
        }

        /// <summary>
        /// Handle a changed value
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue)
        {
            if (blend)
            {
                float newBlend;

                newBlend = Mathf.InverseLerp((float)range1.DoubleValue(), (float)range2.DoubleValue(), (float)newValue);

                if (!Mathf.Approximately(newBlend, currentBlend))
                {
                    if (rateLimited)
                    {
                        goalBlend = newBlend;

                        newBlend = RateLimitBlend(newBlend);
                    }

                    currentBlend = newBlend;

                    Vector3 newPosition = Vector3.Lerp(startTranslation, endTranslation, currentBlend);
                    transform.localPosition = newPosition;
                }
            }
            else
            {
                if (rangeMode)
                {
                    newValue = (newValue.Between(range1.DoubleValue(), range2.DoubleValue())) ? 1.0 : 0.0;
                }

                bool newState = (newValue > 0.0);

                if (newState != currentState)
                {
                    currentState = newState;
                    transform.localPosition = (currentState) ? endTranslation : startTranslation;
                }
            }
        }

        /// <summary>
        /// Coroutine to manage rate-limited rotations (those that can't snap into
        /// position due to restraints in their configurations).
        /// </summary>
        /// <returns></returns>
        private IEnumerator TimedRotationCoroutine()
        {
            coroutineActive = true;
            while (!Mathf.Approximately(goalBlend, currentBlend))
            {
                yield return MASConfig.waitForFixedUpdate;

                currentBlend = RateLimitBlend(goalBlend);

                Vector3 newPosition = Vector3.Lerp(startTranslation, endTranslation, currentBlend);
                transform.localPosition = newPosition;
            }
            coroutineActive = false;
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp prop)
        {
            variableRegistrar.ReleaseResources();
            this.comp = null;
            transform = null;
        }
    }
}
