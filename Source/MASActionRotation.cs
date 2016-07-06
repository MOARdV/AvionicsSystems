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
    class MASActionRotation : IMASAction
    {
        private string name = "(anonymous)";
        private string variableName;
        private MASFlightComputer.Variable range1, range2;
        private Quaternion startRotation, endRotation;
        private Transform transform;
        private readonly bool blend;
        private readonly bool rangeMode;
        private readonly bool modulo;
        private readonly float moduloValue;
        private bool currentState = false;
        private float currentBlend = 0.0f;

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
            if(!hasRotationEnd)
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

                if(blend)
                {
                    config.TryGetValue("longPath", ref useLongPath);
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
            else if(!string.IsNullOrEmpty(variableName))
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
                // I use less than 1/2 because if I use exactly 1/2, it is unclear
                // which way the rotation should go if it rotates from 0 to 360 degrees.
                // a start of 0 and end of 360 may be identical to a start of 360 and an
                // end of 0, even though the rotation goes "up" in the first case and
                // "down" in the second.
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

                    Quaternion newRotation = Quaternion.SlerpUnclamped(startRotation, endRotation, currentBlend*2.285714286f);
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
            transform = null;
        }
    }
}
