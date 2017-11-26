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
    internal class MASActionIntLight : IMASSubComponent
    {
        private string name = "anonymous";
        private string variableName = string.Empty;
        private MASFlightComputer.Variable range1, range2;
        private readonly bool rangeMode;
        private bool currentState;
        private Light[] controlledLights;

        internal MASActionIntLight(ConfigNode config, InternalProp prop, MASFlightComputer comp)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "anonymous";
            }

            string lightName = string.Empty;
            if (!config.TryGetValue("lightName", ref lightName))
            {
                throw new ArgumentException("Missing 'lightName' in INT_LIGHT " + name);
            }

            if (!config.TryGetValue("variable", ref variableName) || string.IsNullOrEmpty(variableName))
            {
                throw new ArgumentException("Invalid or missing 'variable' in INT_LIGHT " + name);
            }

            Light[] availableLights = prop.part.internalModel.FindModelComponents<Light>();
            if (availableLights != null && availableLights.Length > 0)
            {
                List<Light> lights = new List<Light>(availableLights);
                for (int i = lights.Count - 1; i >= 0; --i)
                {
                    if (lights[i].name != lightName)
                    {
                        lights.RemoveAt(i);
                    }
                }
                if (lights.Count > 0)
                {
                    controlledLights = lights.ToArray();
                }
            }

            if (controlledLights == null)
            {
                Utility.LogErrorMessage(this, "No lights named '{0}' found in internalModel '{1}'", lightName, prop.part.internalModel.internalName);
                return;
            }

            variableName = variableName.Trim();

            string range = string.Empty;
            if (config.TryGetValue("range", ref range))
            {
                string[] ranges = range.Split(',');
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in INT_LIGHT " + name);
                }
                range1 = comp.GetVariable(ranges[0], prop);
                range2 = comp.GetVariable(ranges[1], prop);
                rangeMode = true;
            }
            else
            {
                rangeMode = false;
            }

            currentState = false;
            for (int i = 0; i < controlledLights.Length; ++i)
            {
                controlledLights[i].enabled = currentState;
            }

            comp.RegisterNumericVariable(variableName, prop, VariableCallback);
        }

        /// <summary>
        /// Variable callback used to update the animation when it is playing.
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
                for (int i = 0; i < controlledLights.Length; ++i)
                {
                    controlledLights[i].enabled = currentState;
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
        public void ReleaseResources(MASFlightComputer comp, InternalProp prop)
        {
            comp.UnregisterNumericVariable(variableName, prop, VariableCallback);
        }
    }
}
