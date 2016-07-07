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
    /// <summary>
    /// MASActionRotation rotates part of a prop around a specified axis given
    /// in Euler angles.
    /// </summary>
    class MASActionRotation : IMASAction
    {
        private string name = "(anonymous)";
        private string variableName;
        private MASFlightComputer.Variable range1, range2;
        private Quaternion startRotation, endRotation;
        private MASFlightComputer comp;
        private Transform transform;
        private readonly bool blend;
        private readonly bool rangeMode;
        private readonly bool modulo;
        private readonly bool rateLimited;
        private readonly float moduloValue;
        private readonly float speed;
        private bool coroutineActive = false;
        private bool currentState = false;
        private float currentBlend = 0.0f;
        private float goalBlend = 0.0f;

        internal MASActionRotation(ConfigNode config, InternalProp prop, MASFlightComputer comp)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "(anonymous)";
            }

            string transform = string.Empty;
            if (!config.TryGetValue("transform", ref transform))
            {
                throw new ArgumentException("Missing 'transform' in ROTATION " + name);
            }

            this.transform = prop.FindModelTransform(transform);

            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            Vector3 startRotation = Vector3.zero;
            if (!config.TryGetValue("startRotation", ref startRotation))
            {
                throw new ArgumentException("Missing 'startRotation' in ROTATION " + name);
            }

            Vector3 endRotation = Vector3.zero;
            bool hasRotationEnd = config.TryGetValue("endRotation", ref endRotation);
            if (!hasRotationEnd)
            {
                endRotation = Vector3.zero;
            }

            string range = string.Empty;
            bool useLongPath = false;
            if (config.TryGetValue("range", ref range))
            {
                string[] ranges = range.Split(',');
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in ROTATION " + name);
                }
                range1 = comp.GetVariable(ranges[0]);
                range2 = comp.GetVariable(ranges[1]);
                rangeMode = true;

                blend = false;
                config.TryGetValue("blend", ref blend);

                if (blend)
                {
                    config.TryGetValue("longPath", ref useLongPath);

                    float modulo = 0.0f;
                    if (config.TryGetValue("modulo", ref modulo) && modulo > 0.0f)
                    {
                        this.modulo = true;
                        this.moduloValue = modulo;
                    }
                    else
                    {
                        this.modulo = false;
                    }

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
            if (rangeMode || hasRotationEnd)
            {
                if (string.IsNullOrEmpty(variableName))
                {
                    throw new ArgumentException("Invalid or missing 'variable' in ROTATION " + name);
                }
            }
            else if (!string.IsNullOrEmpty(variableName))
            {
                if (!hasRotationEnd)
                {
                    throw new ArgumentException("Missing 'endRotation' in ROTATION " + name);
                }
            }

            // Make everything a known value before the callback fires.
            this.startRotation = this.transform.localRotation * Quaternion.Euler(startRotation);
            if (blend)
            {
                // This looks a little odd.  Here's why:
                // The Quaternion.Slerp takes the shortest path between two
                // rotations.  That's fine as long as the intended path is < 180
                // degrees.  However, for something that has a longer intended travel,
                // say 270 degrees, it's a problem: Slerp will take the shorter,
                // 90 degree path.
                //
                // In RasterPropMonitor, Mihara got around this problem by using
                // a linear interpolation between the Euler angles, and a conversion
                // to a quaternion.  That's a lot of extra math.  What I'm doing
                // instead is to store 7/16 of the rotation, so I don't lose the intended
                // path. I allow the slerp to be unbounded, and I scale the blended range
                // by 16/7 to make sure that a blend of 1.0 has the same rotation.
                //
                // I use less than 1/2 because if I use exactly 1/2, it does not work
                // for the case of rotation 0 to 360 degrees: regardless of if I use
                // a start of 0 and end of 360, or the other way around, the midpoint
                // is 180.  Since the quaternions for 0 and 360 are the same, there's
                // no sense of which way the rotation should go.  Tracing less than half
                // the circle leaves a sense of the intended direction of the rotation.
                if (useLongPath)
                {
                    this.endRotation = this.transform.localRotation * Quaternion.Euler(Vector3.Lerp(startRotation, endRotation, 0.4375f));
                }
                else
                {
                    this.endRotation = Quaternion.Slerp(this.startRotation, this.transform.localRotation * Quaternion.Euler(endRotation), 0.4375f);
                }
            }
            else
            {
                this.endRotation = this.transform.localRotation * Quaternion.Euler(endRotation);
            }

            this.transform.localRotation = this.startRotation;

            if (string.IsNullOrEmpty(variableName))
            {
                Utility.LogMessage(this, "ROTATION {0} configured as static rotation, with no variable defined", name);
            }
            else
            {
                comp.RegisterNumericVariable(variableName, VariableCallback);
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
                // Limit modulo?
                bool wrapAround = modulo && (difference > 0.5f);
                if (wrapAround)
                {
                    maxDelta = Mathf.Min(maxDelta, 1.0f - difference);
                    maxDelta = -maxDelta;
                }

                if (newBlend < currentBlend)
                {
                    newBlend = currentBlend - maxDelta;
                }
                else
                {
                    newBlend = currentBlend + maxDelta;
                }

                if (wrapAround)
                {
                    if (newBlend < 0.0f)
                    {
                        newBlend += 1.0f;
                    }
                    else if (newBlend > 1.0f)
                    {
                        newBlend -= 1.0f;
                    }
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

                if (modulo)
                {
                    float lowValue = (float)range1.SafeValue();
                    float highValue = (float)range2.SafeValue();
                    if (highValue < lowValue)
                    {
                        float tmp = lowValue;
                        lowValue = highValue;
                        highValue = tmp;
                    }

                    newBlend = Mathf.InverseLerp(lowValue, highValue, (float)newValue);
                    float range = highValue - lowValue;
                    if (range > 0.0f)
                    {
                        float wasBlend = newBlend;
                        float modDivRange = moduloValue / range;
                        newBlend = (newBlend % (modDivRange)) / modDivRange;
                    }
                }
                else
                {
                    newBlend = Mathf.InverseLerp((float)range1.SafeValue(), (float)range2.SafeValue(), (float)newValue);
                }

                if (!Mathf.Approximately(newBlend, currentBlend))
                {
                    if (rateLimited)
                    {
                        goalBlend = newBlend;

                        newBlend = RateLimitBlend(newBlend);
                    }

                    currentBlend = newBlend;

                    Quaternion newRotation = Quaternion.SlerpUnclamped(startRotation, endRotation, currentBlend * 2.285714286f);
                    transform.localRotation = newRotation;
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
                    transform.localRotation = (currentState) ? endRotation : startRotation;
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
                yield return new WaitForFixedUpdate();

                currentBlend = RateLimitBlend(goalBlend);

                Quaternion newRotation = Quaternion.SlerpUnclamped(startRotation, endRotation, currentBlend * 2.285714286f);
                transform.localRotation = newRotation;
            }
            coroutineActive = false;
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
        /// Return if the action is persistent
        /// </summary>
        /// <returns></returns>
        public bool Persistent()
        {
            return true;
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
            this.comp = null;
            transform = null;
        }
    }
}
