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
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    public partial class MASFlightComputer : PartModule
    {
        /// <summary>
        /// Dictionary mapping variable names to Variable objects.
        /// </summary>
        private Dictionary<string, Variable> variables = new Dictionary<string, Variable>();

        /// <summary>
        /// List of variables that change (as opposed to constants).
        /// </summary>
        private List<Variable> mutableVariablesList = new List<Variable>();

        /// <summary>
        /// Because a List is more expensive to traverse than an array, and
        /// because we are going to iterate over the mutable variables
        /// *A*LOT*, we keep an array that we really iterate over.
        /// </summary>
        private Variable[] mutableVariables = new Variable[0];

        /// <summary>
        /// The Variable is a wrapper class to manage a single variable (as
        /// defined by a Lua script or a constant value).  It allows the MAS
        /// Flight Computer to track and update a single instance of a given
        /// variable, so heavily-used variables do not cause excessive
        /// performance penalties by being queried dozens or hundreds of
        /// times.
        /// </summary>
        public class Variable
        {
            public readonly string name;
            public readonly bool mutable;
            //TODO: public readonly bool cacheable;
            public readonly bool valid;
            internal event Action<double> numericCallbacks;
            internal event Action changeCallbacks;
            private DynValue evaluator;
            private DynValue value;
            private string stringValue;
            private double doubleValue;
            private double safeValue;
            private bool isString;

            public Variable(string name, Script script)
            {
                this.name = name;

                double value;
                if (double.TryParse(name, out value))
                {
                    this.mutable = false;
                    this.valid = true;
                    this.stringValue = value.ToString();
                    this.doubleValue = value;
                    this.safeValue = value;
                    this.value = DynValue.NewNumber(value);
                    this.isString = false;
                }
                else
                {
                    this.value = null;
                    try
                    {
                        evaluator = script.LoadString("return " + name);
                        this.value = script.Call(evaluator);
                        //this.mutable = true;
                        this.valid = true;
                    }
                    catch (Exception e)
                    {
                        Utility.LogErrorMessage(this, "Unknown variable '{0}':", name);
                        Utility.LogErrorMessage(this, e.ToString());
                        this.value = null;
                        this.valid = false;
                    }

                    if (this.value != null)
                    {
                        if (this.value.IsNil() || this.value.IsVoid())
                        {
                            // Not a valid evaluable
                            this.valid = false;
                            this.mutable = false;
                        }
                        else
                        {
                            this.stringValue = this.value.CastToString();
                            this.doubleValue = this.value.CastToNumber() ?? double.NaN;
                            // TODO: Find a way to convey mutability
                            this.mutable = true;
                            this.valid = true;
                        }
                    }
                    else
                    {
                        this.doubleValue = double.NaN;
                        this.stringValue = name;
                        this.mutable = false;
                        this.valid = false;
                        this.isString = true;
                    }

                    if (double.IsNaN(this.doubleValue) || double.IsInfinity(this.doubleValue))
                    {
                        this.safeValue = 0.0;
                        this.isString = true;
                    }
                    else
                    {
                        this.safeValue = doubleValue;
                        this.isString = false;
                    }
                }
            }

            public bool IsString()
            {
                return this.isString;
            }

            /// <summary>
            /// Return the raw DynValue for specialized processing.
            /// </summary>
            /// <returns></returns>
            public DynValue RawValue()
            {
                return value;
            }

            /// <summary>
            /// Return the raw double value of this variable (including NaN and Inf).
            /// </summary>
            /// <returns></returns>
            public double Value()
            {
                return doubleValue;
            }

            /// <summary>
            /// Return the safe value of this variable (NaN and Inf are silently treated
            /// as 0.0).
            /// </summary>
            /// <returns></returns>
            public double SafeValue()
            {
                return safeValue;
            }

            /// <summary>
            /// Return the value as a string.
            /// </summary>
            /// <returns></returns>
            public string String()
            {
                return stringValue;
            }

            /// <summary>
            /// Evaluate updates the variable in question by calling the code
            /// snippet using the supplied Lua script.
            /// </summary>
            /// <param name="script"></param>
            internal void Evaluate(Script script)
            {
                DynValue oldDynValue = value;
                double oldValue = safeValue;
                //string oldString = stringValue;
                try
                {
                    value = script.Call(evaluator);
                    stringValue = value.CastToString();
                    doubleValue = value.CastToNumber() ?? double.NaN;
                }
                catch
                {
                    this.doubleValue = double.NaN;
                    this.stringValue = name;
                }

                safeValue = (double.IsInfinity(doubleValue) || double.IsNaN(doubleValue)) ? 0.0 : doubleValue;

                DataType type = value.Type;
                if (type == DataType.Number)
                {
                    if (!Mathf.Approximately((float)oldValue, (float)safeValue))
                    {
                        try
                        {
                            numericCallbacks.Invoke(safeValue);
                        }
                        catch { }
                        try
                        {
                            changeCallbacks.Invoke();
                        }
                        catch { }
                    }
                }
                else if (type == DataType.String)
                {
                    if (oldDynValue.String != stringValue)
                    {
                        try
                        {
                            changeCallbacks.Invoke();
                        }
                        catch { }
                    }
                }
                else if (!oldDynValue.Equals(value))
                {
                    try
                    {
                        changeCallbacks.Invoke();
                    }
                    catch { }
                }
            }
        }
    }
}
