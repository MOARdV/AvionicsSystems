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
    /// A TRIGGER_EVENT fires an event when a tracked variable falls within a
    /// particular range (either > 0 for Boolean, or between specified range
    /// values).
    /// </summary>
    class MASActionTriggerEvent : IMASSubComponent
    {
        private string name = "(anonymous)";
        private string variableName;
        private MASFlightComputer.Variable range1, range2;
        private readonly bool rangeMode;
        private bool currentState = false;
        private bool autoRepeat = false;
        MASFlightComputer comp;
        Action triggerEvent;
        Action exitEvent = null;

        internal MASActionTriggerEvent(ConfigNode config, InternalProp prop, MASFlightComputer comp)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "(anonymous)";
            }

            if (!config.TryGetValue("variable", ref variableName) || string.IsNullOrEmpty(variableName))
            {
                throw new ArgumentException("Invalid or missing 'variable' in TRIGGER_EVENT " + name);
            }
            variableName = variableName.Trim();

            string range = string.Empty;
            if (config.TryGetValue("range", ref range))
            {
                string[] ranges = range.Split(',');
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in TRIGGER_EVENT " + name);
                }
                range1 = comp.GetVariable(ranges[0], prop);
                range2 = comp.GetVariable(ranges[1], prop);
                rangeMode = true;
            }
            else
            {
                rangeMode = false;
            }

            if (!bool.TryParse("autoRepeat", out autoRepeat))
            {
                autoRepeat = false;
            }

            string triggerEventName = string.Empty;
            config.TryGetValue("event", ref triggerEventName);
            if (string.IsNullOrEmpty(triggerEventName))
            {
                throw new ArgumentException("Invalid or missing 'event' in TRIGGER_EVENT " + name);
            }

            triggerEvent = comp.GetAction(triggerEventName, prop);
            if (triggerEvent == null)
            {
                throw new ArgumentException("Unable to create event '"+triggerEventName+"' in TRIGGER_EVENT " + name);
            }

            triggerEventName = string.Empty;
            if (config.TryGetValue("exitEvent", ref triggerEventName))
            {
                exitEvent = comp.GetAction(triggerEventName, prop);
                if (exitEvent == null)
                {
                    throw new ArgumentException("Unable to create event '" + triggerEventName + "' in TRIGGER_EVENT " + name);
                }
            }

            this.comp = comp;
            comp.RegisterNumericVariable(variableName, prop, VariableCallback);
        }

        /// <summary>
        /// Handle a changed value
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue)
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
                    triggerEvent();

                    if (autoRepeat)
                    {
                        comp.StartCoroutine(RepeatEvent());
                    }
                }
                else if(exitEvent != null)
                {
                    exitEvent();
                }
            }
        }

        /// <summary>
        /// Automatically repeate the triggerEvent for as long as the variable
        /// is in range.
        /// </summary>
        /// <returns></returns>
        private IEnumerator RepeatEvent()
        {
            yield return MASConfig.waitForFixedUpdate;

            while (currentState)
            {
                triggerEvent();
                yield return MASConfig.waitForFixedUpdate;
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
            this.comp = null;
            range1 = null;
            range2 = null;
        }
    }
}
