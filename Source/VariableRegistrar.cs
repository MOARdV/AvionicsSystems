/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2017-2018 MOARdV
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

namespace AvionicsSystems
{
    /// <summary>
    /// This class tracks multiple variables registered for callbacks, and cleans them up on demand.
    /// Intended for situations where a variable number of variable callbacks may be generated.
    /// </summary>
    internal class VariableRegistrar
    {
        private MASFlightComputer comp;
        private InternalProp internalProp;
        private List<Variable> variable = new List<Variable>();
        private List<Action<double>> variableAction = new List<Action<double>>();
        private bool isEnabled;

        internal VariableRegistrar(MASFlightComputer comp, InternalProp internalProp)
        {
            this.comp = comp;
            this.internalProp = internalProp;
            isEnabled = true;
        }

        /// <summary>
        /// Register a variable-changed callback.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="action">The action to trigger.</param>
        /// <param name="initializeNow">Trigger the callback before this method returns (okay for normal use,
        /// may be a problem if the Variable that's returned needs to be initialized first.</param>
        /// <returns>The Variable created (or null if it failed for some reason).</returns>
        internal Variable RegisterVariableChangeCallback(string name, Action<double> action, bool initializeNow = true)
        {
            name = name.Trim();
            Variable v = comp.RegisterVariableChangeCallback(name, internalProp, action, initializeNow);
            if (v != null)
            {
                variableAction.Add(action);
                variable.Add(v);
            }
            return v;
        }

        /// <summary>
        /// Enable or disable callback registration for the tracked variables.  Unregistering when a given
        /// MASMonitor component is not visible, for instance, can have a big impact on performance.
        /// </summary>
        /// <param name="enable"></param>
        internal void EnableCallbacks(bool enable)
        {
            if (isEnabled == enable)
            {
                Utility.LogMessage(this, "EnableCallbacks({0}) - I think I'm already in that mode (this is not a bug).", enable);
            }
            isEnabled = enable;

            if (enable)
            {
                for (int i = variable.Count - 1; i >= 0; --i)
                {
                    variable[i].RegisterNumericCallback(variableAction[i]);
                    // In case the value has updated since last time we were active:
                    variableAction[i](variable[i].AsDouble());
                }
            }
            else
            {
                for (int i = variable.Count - 1; i >= 0; --i)
                {
                    variable[i].UnregisterNumericCallback(variableAction[i]);
                }
            }
        }

        /// <summary>
        /// Iterate over all registered variables and unregister them.
        /// </summary>
        internal void ReleaseResources()
        {
            if (isEnabled)
            {
                for (int i = variable.Count - 1; i >= 0; --i)
                {
                    variable[i].UnregisterNumericCallback(variableAction[i]);
                }
            }

            this.comp = null;
            this.internalProp = null;
            variable.Clear();
            variableAction.Clear();
        }
    }
}
