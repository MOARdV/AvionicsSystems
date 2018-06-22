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

namespace AvionicsSystems
{
    internal abstract class IMASMonitorComponent : IMASSubComponent
    {
        internal MASFlightComputer.Variable range1, range2;
        internal readonly bool rangeMode;
        internal bool currentState;

        internal IMASMonitorComponent(ConfigNode config, InternalProp prop, MASFlightComputer comp)
            : base(config, prop, comp)
        {

            string range = string.Empty;
            if (config.TryGetValue("range", ref range))
            {
                string[] ranges = Utility.SplitVariableList(range);
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in "+ config.name +" " + name);
                }
                range1 = comp.GetVariable(ranges[0], prop);
                range2 = comp.GetVariable(ranges[1], prop);

                rangeMode = true;
            }
            else
            {
                rangeMode = false;
            }
        }

        /// <summary>
        /// Evaluate the variable to determine what the new state of the component should be.
        /// </summary>
        /// <param name="newValue">Value from the variable callback</param>
        /// <returns>Whether currentMode has changed.</returns>
        internal bool EvaluateVariable(double newValue)
        {
            if (rangeMode)
            {
                newValue = (newValue.Between(range1.SafeValue(), range2.SafeValue())) ? 1.0 : 0.0;
            }

            bool newState = (newValue > 0.0);
            
            if (newState != currentState)
            {
                currentState = newState;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Called with `true` prior to the page rendering.  Called with
        /// `false` after the page completes rendering.
        /// </summary>
        /// <param name="enable">true indicates that the page is about to
        /// be rendered.  false indicates that the page has completed rendering.</param>
        public abstract void RenderPage(bool enable);

        /// <summary>
        /// Called with `true` when the page is active on the monitor, called with
        /// `false` when the page is no longer active.
        /// 
        /// Note that it is not needed to switch of the game objects in a given monitor
        /// page as long as they're attached to the MASPage's pageRoot game object.  This
        /// callback is intended to handle other required processing (such as starting / stopping
        /// a coroutine).
        /// </summary>
        /// <param name="enable">true when the page is actively displayed, false when the page
        /// is no longer displayed.</param>
        virtual public void SetPageActive(bool enable)
        {

        }

        /// <summary>
        /// Handle a softkey event.
        /// </summary>
        /// <param name="keyId">The numeric ID of the key to handle.</param>
        /// <returns>true if the component handled the key, false otherwise.</returns>
        virtual public bool HandleSoftkey(int keyId)
        {
            return false;
        }
    }

    internal abstract class IMASSubComponent
    {
        internal readonly string name;
        internal VariableRegistrar variableRegistrar;

        /// <summary>
        /// Configure the common fields.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="prop"></param>
        /// <param name="comp"></param>
        internal IMASSubComponent(ConfigNode config, InternalProp prop, MASFlightComputer comp)
        {
            variableRegistrar = new VariableRegistrar(comp, prop);
            if (!config.TryGetValue("name", ref name))
            {
                name = "anonymous";
            }
        }

        /// <summary>
        /// Optional name reported for this subcomponent.
        /// </summary>
        /// <returns>Supplied name or "anonymous"</returns>
        public string Name()
        {
            return name;
        }

        /// <summary>
        /// Release any resources obtained during the lifetime of this object.
        /// </summary>
        public abstract void ReleaseResources(MASFlightComputer comp, InternalProp prop);
    }
}
