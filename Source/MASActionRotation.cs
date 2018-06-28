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
    /// <summary>
    /// MASActionRotation rotates part of a prop around a specified axis given
    /// in Euler angles.
    /// </summary>
    class MASActionRotation : IMASSubComponent
    {
        private MASFlightComputer.Variable range1, range2;
        // Beginning and ending rotation points for all rotations
        private Quaternion startRotation, endRotation;
        // midpoint rotation for 180* < rotation < 360*, and midpoint1
        // for 360* rotations.
        private Quaternion midRotation;
        // 2nd midpoint for 360* rotations.
        private Quaternion mid2Rotation;
        private MASFlightComputer comp;
        private Transform transform;
        private readonly bool useLongPath;
        private readonly bool useVeryLongPath;
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

        internal MASActionRotation(ConfigNode config, InternalProp prop, MASFlightComputer comp):base(config, prop, comp)
        {
            string transform = string.Empty;
            if (!config.TryGetValue("transform", ref transform))
            {
                throw new ArgumentException("Missing 'transform' in ROTATION " + name);
            }

            this.transform = prop.FindModelTransform(transform);

            string variableName = string.Empty;
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
            useLongPath = false;
            useVeryLongPath = false;
            if (config.TryGetValue("range", ref range))
            {
                string[] ranges = Utility.SplitVariableList(range);
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in ROTATION " + name);
                }
                range1 = comp.GetVariable(ranges[0], prop);
                range2 = comp.GetVariable(ranges[1], prop);
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

            // Determine the rotations.
            this.startRotation = this.transform.localRotation * Quaternion.Euler(startRotation);
            if (useLongPath)
            {
                // Due to the nature of Quaternions, any SLerp we do will always
                // select the shortest route between two Quaternions.  This is great for
                // rotations that are < 180 degrees, but it's a problem if the rotation is
                // intended to be > 180 degrees.
                // As long as the total rotation is < 360, we can get away with a single
                // midpoint Quaternion and interpolate the result between the two
                // piece-wise rotations.  However, for a full 360* rotation, we have to have
                // a third point so we can encode a sense of direction.
                // And, if the configuration requested long path, but our read of the rotation
                // says it's less than 180 degrees (and thus a candidate for short path), we
                // quietly 'downgrade' to the slightly cheaper to compute short path.
                Quaternion mid = Quaternion.Euler(Vector3.Lerp(startRotation, endRotation, 0.5f));
                float midAngle = Quaternion.Angle(Quaternion.Euler(startRotation), mid);
                if (midAngle >= 179.0f)
                {
                    useVeryLongPath = true;
                    this.midRotation = this.transform.localRotation * Quaternion.Euler(Vector3.Lerp(startRotation, endRotation, 1.0f / 3.0f));
                    this.mid2Rotation = this.transform.localRotation * Quaternion.Euler(Vector3.Lerp(startRotation, endRotation, 2.0f / 3.0f));
                }
                else if (midAngle >= 89.0f)
                {
                    this.midRotation = this.transform.localRotation * mid;
                }
                else
                {
                    useLongPath = false;
                }
            }

            this.endRotation = this.transform.localRotation * Quaternion.Euler(endRotation);

            // Make everything a known value before the callback fires.
            this.transform.localRotation = this.startRotation;

            if (string.IsNullOrEmpty(variableName))
            {
                Utility.LogMessage(this, "ROTATION {0} configured as static rotation, with no variable defined", name);
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
        /// Apply the updated blend to the rotation.
        /// </summary>
        /// <param name="blend"></param>
        private Quaternion UpdateRotation(float blend)
        {
            Quaternion newRotation;
            if (useVeryLongPath)
            {
                blend *= 3.0f;
                if (blend <= 1.0f)
                {
                    newRotation = Quaternion.Slerp(startRotation, midRotation, blend);
                }
                else if (blend <= 2.0f)
                {
                    newRotation = Quaternion.Slerp(midRotation, mid2Rotation, blend - 1.0f);
                }
                else
                {
                    newRotation = Quaternion.Slerp(mid2Rotation, endRotation, blend - 2.0f);
                }
            }
            else if (useLongPath)
            {
                if (blend > 0.5f)
                {
                    newRotation = Quaternion.Slerp(midRotation, endRotation, blend * 2.0f - 1.0f);
                }
                else
                {
                    newRotation = Quaternion.Slerp(startRotation, midRotation, blend * 2.0f);
                }
            }
            else
            {
                newRotation = Quaternion.Slerp(startRotation, endRotation, blend);
            }

            return newRotation;
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
                    float lowValue = (float)range1.DoubleValue();
                    float highValue = (float)range2.DoubleValue();
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
                    newBlend = Mathf.InverseLerp((float)range1.DoubleValue(), (float)range2.DoubleValue(), (float)newValue);
                }

                if (!Mathf.Approximately(newBlend, currentBlend))
                {
                    if (rateLimited)
                    {
                        goalBlend = newBlend;

                        newBlend = RateLimitBlend(newBlend);
                    }

                    currentBlend = newBlend;

                    transform.localRotation = UpdateRotation(currentBlend);
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
                yield return MASConfig.waitForFixedUpdate;

                currentBlend = RateLimitBlend(goalBlend);

                transform.localRotation = UpdateRotation(currentBlend);
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
