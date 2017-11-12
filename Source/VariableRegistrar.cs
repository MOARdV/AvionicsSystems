/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2017 MOARdV
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
//using System.Text;

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
        private List<string> variableName = new List<string>();
        private List<Action<double>> variableAction = new List<Action<double>>();

        internal VariableRegistrar(MASFlightComputer comp, InternalProp internalProp)
        {
            this.comp = comp;
            this.internalProp = internalProp;
        }

        /// <summary>
        /// Register a numeric variable callback.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="action">The action to trigger.</param>
        internal void RegisterNumericVariable(string name, Action<double> action)
        {
            comp.RegisterNumericVariable(name, internalProp, action);
            variableName.Add(name);
            variableAction.Add(action);
        }

        /// <summary>
        /// Iterate over all registered variables and unregister them.
        /// </summary>
        /// <param name="comp"></param>
        /// <param name="internalProp"></param>
        internal void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            if (comp != this.comp)
            {
                Utility.LogWarning(this, "ReleaseResource: comp and this.comp don't match");
            }

            if (internalProp != this.internalProp)
            {
                Utility.LogWarning(this, "ReleaseResource: internalProp and this.internalProp don't match");
            }

            for (int i = variableName.Count - 1; i >= 0; --i)
            {
                comp.UnregisterNumericVariable(variableName[i], internalProp, variableAction[i]);
            }

            this.comp = null;
            this.internalProp = null;
            variableName.Clear();
            variableAction.Clear();
        }
    }
}
