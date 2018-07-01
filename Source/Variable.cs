/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2018 MOARdV
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
    /// <summary>
    /// The Variable is a wrapper class to manage a single variable (as
    /// defined by a Lua script, a lambda expression, or a constant value).
    /// It allows the MAS Flight Computer to track and update a single
    /// instance of a given variable, so heavily-used variables do not
    /// cause excessive performance penalties by being queried dozens or
    /// hundreds of times per Fixed Update.
    /// </summary>
    public abstract class Variable
    {
        /// <summary>
        /// Variable name
        /// </summary>
        public readonly string name;
        /// <summary>
        /// Whether the variable can change (otherwise it is a constant)
        /// </summary>
        public bool mutable
        {
            get
            {
                return variableType != VariableType.Constant;
            }
        }

        /// <summary>
        /// Can the results be cached, or must they be evaluated each time
        /// it is called?
        /// </summary>
        public readonly bool cacheable;
        /// <summary>
        /// List of numeric callback subscribers.
        /// </summary>
        private event Action<double> numericCallbacks;
        /// <summary>
        /// The type of variable (constant, lambda/delegate, Lua)
        /// </summary>
        internal readonly VariableType variableType = VariableType.Unknown;
        /// <summary>
        /// Flag to indicate a variable this variable depends on has changed.  Used only for
        /// Dependent variables.
        /// </summary>
        internal bool triggerUpdate = true;

        /// <summary>
        /// What type of variable does this represent?
        /// 
        /// NB: Probably can remove the mutable boolean - Constant is the only situation where mutable
        /// is going to be false.
        /// </summary>
        public enum VariableType
        {
            /// <summary>
            /// Invalid type
            /// </summary>
            Unknown,
            /// <summary>
            /// Lua script.  Expects cacheable = true, mutable = true.
            /// </summary>
            LuaScript,
            /// <summary>
            /// Constant boolean, numeric, or string value.  Expects cacheable = true, mutable = false.
            /// </summary>
            Constant,
            /// <summary>
            /// Any lambda expression whose results are non-deterministic (does not depend on inputs).
            /// Expects cacheable = true or false, mutable = true.
            /// </summary>
            Func,
            /// <summary>
            /// A lambda expression whose results are always wholly dependent on the input(s), and thus
            /// does not automatically need to be evaluated on every FixedUpdate.  Expects cacheable = true
            /// or false, mutable = true.
            /// </summary>
            Dependent,
        };

        /// <summary>
        /// Initialize the readonly values.
        /// </summary>
        /// <param name="name">Canonical name of the variable</param>
        /// <param name="cacheable">Whether the variable may be cached.</param>
        /// <param name="variableType">Type of variable.</param>
        public Variable(string name, bool cacheable, VariableType variableType)
        {
            this.name = name;

            this.cacheable = cacheable;
            this.variableType = variableType;
        }

        /// <summary>
        /// Return the object conditioned as a boolean.
        /// </summary>
        /// <returns></returns>
        public abstract bool AsBool();

        /// <summary>
        /// Return the numeric value of this variable (NaN and Inf are silently treated
        /// as 0.0).
        /// </summary>
        /// <returns></returns>
        public abstract double AsDouble();

        /// <summary>
        /// Return the value as a DynValue for Lua processing.
        /// </summary>
        /// <returns></returns>
        public abstract DynValue AsDynValue();

        /// <summary>
        /// Return the raw object for customized processing.
        /// </summary>
        /// <returns></returns>
        public abstract object AsObject();

        /// <summary>
        /// Return the value as a string.
        /// </summary>
        /// <returns></returns>
        public abstract string AsString();

        /// <summary>
        /// Evaluate() conditionally updates the variable by calling the evaluator.
        /// </summary>
        /// <param name="invokeCallbacks">Whether numeric callbacks should be triggered after evaluation.
        /// This value is set to false in the accessor function of non-cacheable variables.</param>
        internal abstract void Evaluate(bool invokeCallbacks);

        /// <summary>
        /// Add a given callback to our callbacks list, provided our value can change.
        /// </summary>
        /// <param name="callback"></param>
        public void RegisterNumericCallback(Action<double> callback)
        {
            if (mutable)
            {
                bool reevaluate = (numericCallbacks == null);

                numericCallbacks += callback;

                if (reevaluate)
                {
                    triggerUpdate = true;
                    Evaluate(true);
                }
            }
        }

        /// <summary>
        /// Remove a given callback from our callbacks list, provided our value can change.
        /// </summary>
        /// <param name="callback"></param>
        public void UnregisterNumericCallback(Action<double> callback)
        {
            if (mutable)
            {
                numericCallbacks -= callback;
            }
        }

        /// <summary>
        /// Only a Dependent variable uses this method.  When a depedenent variable is created,
        /// this callback is registered with the numeric callback of the source variable(s) so
        /// that the dependent variable is notified when a source value has changed, thus triggering
        /// its own update (and those of any down-stream dependent variables).
        /// </summary>
        /// <param name="unused"></param>
        public void TriggerUpdate(double unused)
        {
            triggerUpdate = true;
        }

        /// <summary>
        /// Invoke any registered callbacks.
        /// </summary>
        /// <param name="value"></param>
        internal protected void TriggerNumericCallbacks(double value)
        {
            if (numericCallbacks != null)
            {
                numericCallbacks.Invoke(value);
            }
        }
    }

    /// <summary>
    /// Variable representing a numeric value.
    /// </summary>
    public class BooleanVariable : Variable
    {
        private Func<bool> evaluator;
        bool boolValue;

        /// <summary>
        /// Construct a constant-value double.
        /// </summary>
        /// <param name="constantValue"></param>
        public BooleanVariable(bool constantValue)
            : base(string.Format("{0}", constantValue), true, VariableType.Constant)
        {
            this.boolValue = constantValue;
        }

        /// <summary>
        /// Construct a dynamic native evaluator.
        /// </summary>
        /// <param name="name">Name (canonical) of the variable.</param>
        /// <param name="evaluator">The function used to evaluate the variable.</param>
        /// <param name="cacheable">Whether the variable is cacheable (if false, the variable must be evaluated every time it's called).</param>
        /// <param name="mutable">Whether the variable will ever change value.</param>
        /// <param name="variableType">The type of variable.</param>
        public BooleanVariable(string name, Func<bool> evaluator, bool cacheable, bool mutable, VariableType variableType)
            : base(name, (mutable) ? cacheable : true, (mutable) ? variableType : VariableType.Constant)
        {
            this.evaluator = evaluator;

            Evaluate(false);
        }

        /// <summary>
        /// Return the raw object for customized processing.
        /// </summary>
        /// <returns></returns>
        public override object AsObject()
        {
            if (!cacheable)
            {
                Evaluate(false);
            }
            return boolValue;
        }

        /// <summary>
        /// Return the object conditioned as a boolean.
        /// </summary>
        /// <returns></returns>
        public override bool AsBool()
        {
            if (!cacheable)
            {
                Evaluate(false);
            }
            return boolValue;
        }

        /// <summary>
        /// Return the value as a DynValue for Lua processing.
        /// </summary>
        /// <returns></returns>
        public override DynValue AsDynValue()
        {
            if (!cacheable)
            {
                Evaluate(false);
            }
            return DynValue.NewBoolean(boolValue);
        }

        /// <summary>
        /// Return the numeric value of this variable (NaN and Inf are silently treated
        /// as 0.0).
        /// </summary>
        /// <returns></returns>
        public override double AsDouble()
        {
            if (!cacheable)
            {
                Evaluate(false);
            }
            return (boolValue) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Return the value as a string.
        /// </summary>
        /// <returns></returns>
        public override string AsString()
        {
            if (!cacheable)
            {
                Evaluate(false);
            }
            return boolValue.ToString();
        }

        /// <summary>
        /// Evaluate() conditionally updates the variable by calling the evaluator.
        /// </summary>
        internal override void Evaluate(bool invokeCallbacks)
        {
            if (variableType == VariableType.Dependent && triggerUpdate == false)
            {
                return; // early
            }

            triggerUpdate = false;

            bool newValue = evaluator();

            if (newValue != boolValue)
            {
                if (invokeCallbacks)
                {
                    TriggerNumericCallbacks(newValue ? 1.0 : 0.0);
                }
                boolValue = newValue;
            }
        }
    }

    /// <summary>
    /// Variable representing a numeric value.
    /// </summary>
    public class DoubleVariable : Variable
    {
        Func<double> evaluator;
        double doubleValue = float.MaxValue;

        /// <summary>
        /// Construct a constant-value double.
        /// </summary>
        /// <param name="constantValue"></param>
        public DoubleVariable(double constantValue)
            : base(string.Format("{0:R}", constantValue), true, VariableType.Constant)
        {
            this.doubleValue = constantValue;
        }

        /// <summary>
        /// Construct a dynamic native evaluator.
        /// </summary>
        /// <param name="name">Name (canonical) of the variable.</param>
        /// <param name="evaluator">The function used to evaluate the variable.</param>
        /// <param name="cacheable">Whether the variable is cacheable (if false, the variable must be evaluated every time it's called).</param>
        /// <param name="mutable">Whether the variable will ever change value.</param>
        /// <param name="variableType">The type of variable.</param>
        public DoubleVariable(string name, Func<double> evaluator, bool cacheable, bool mutable, VariableType variableType)
            : base(name, (mutable) ? cacheable : true, (mutable) ? variableType : VariableType.Constant)
        {
            this.evaluator = evaluator;

            Evaluate(false);
        }

        /// <summary>
        /// Return the raw object for customized processing.
        /// </summary>
        /// <returns></returns>
        public override object AsObject()
        {
            if (!cacheable)
            {
                Evaluate(false);
            }
            return doubleValue;
        }

        /// <summary>
        /// Return the object conditioned as a boolean.
        /// </summary>
        /// <returns></returns>
        public override bool AsBool()
        {
            if (!cacheable)
            {
                Evaluate(false);
            }
            return (doubleValue != 0.0);
        }

        /// <summary>
        /// Return the value as a DynValue for Lua processing.
        /// </summary>
        /// <returns></returns>
        public override DynValue AsDynValue()
        {
            if (!cacheable)
            {
                Evaluate(false);
            }
            return DynValue.NewNumber(doubleValue);
        }

        /// <summary>
        /// Return the numeric value of this variable (NaN and Inf are silently treated
        /// as 0.0).
        /// </summary>
        /// <returns></returns>
        public override double AsDouble()
        {
            if (!cacheable)
            {
                Evaluate(false);
            }
            return doubleValue;
        }

        /// <summary>
        /// Return the value as a string.
        /// </summary>
        /// <returns></returns>
        public override string AsString()
        {
            if (!cacheable)
            {
                Evaluate(false);
            }
            return string.Format("{0:R}", doubleValue);
        }

        /// <summary>
        /// Evaluate() conditionally updates the variable by calling the evaluator.
        /// </summary>
        internal override void Evaluate(bool invokeCallbacks)
        {
            if (variableType == VariableType.Dependent && triggerUpdate == false)
            {
                return; // early
            }

            triggerUpdate = false;

            double newValue = evaluator();

            // There is actually inadequate precision in a 32 bit float
            // to detect fixed update time deltas by the time 96 hours
            // have passed on the UT clock, so we leave the values as
            // double precision and compare the delta to the Mathf.Epsilon.
            if (Math.Abs(newValue - doubleValue) > Mathf.Epsilon)
            {
                if (invokeCallbacks)
                {
                    TriggerNumericCallbacks(newValue);
                }
                doubleValue = newValue;
            }
        }
    }

    /// <summary>
    /// Variable representing an indeterminate value type.
    /// </summary>
    public class GenericVariable : Variable
    {
        /// <summary>
        /// Classifier to identify the type of variable this represents, so AsDynValue can evaluate
        /// at call-time the correct DynValue to return for mutable variables.
        /// </summary>
        public enum EvaluationType
        {
            Boolean,
            Double,
            String,
            Nil
        };

        /// <summary>
        /// The delegate that is invoked to evaluate this variable.
        /// </summary>
        private Func<object> evaluator;
        /// <summary>
        /// Result of previous evaluation as a boxed value.
        /// </summary>
        private object rawObject;
        /// <summary>
        /// String version of rawObject.
        /// </summary>
        private string stringValue;
        /// <summary>
        /// Safe numeric version of rawObject.  Do I really need doubleValue *and* safeValue?
        /// </summary>
        private double doubleValue;
        /// <summary>
        /// Lua DynValue version of the result.
        /// </summary>
        private DynValue luaValue;
        /// <summary>
        /// The value type of the variable, for dynamic value conversion to DynValue.
        /// </summary>
        public EvaluationType valueType { private set; get; }

        /// <summary>
        /// Construct a dynamic native evaluator.
        /// </summary>
        /// <param name="name">Name (canonical) of the variable.</param>
        /// <param name="evaluator">The function used to evaluate the variable.</param>
        /// <param name="cacheable">Whether the variable is cacheable (if false, the variable must be evaluated every time it's called).</param>
        /// <param name="mutable">Whether the variable will ever change value.</param>
        /// <param name="variableType">The type of variable.</param>
        public GenericVariable(string name, Func<object> evaluator, bool cacheable, bool mutable, VariableType variableType)
            : base(name, (mutable) ? cacheable : true, (mutable) ? variableType : VariableType.Constant)
        {
            this.evaluator = evaluator;
            this.valueType = EvaluationType.Nil;

            ProcessObject(false);

            if (!mutable)
            {
                switch (valueType)
                {
                    case EvaluationType.Boolean:
                        luaValue = DynValue.NewBoolean(doubleValue != 0.0);
                        break;
                    case EvaluationType.Double:
                        luaValue = DynValue.NewNumber(doubleValue);
                        break;
                    case EvaluationType.String:
                        luaValue = DynValue.NewString(stringValue);
                        break;
                    case EvaluationType.Nil:
                        luaValue = DynValue.NewNil();
                        break;
                }
            }
        }

        /// <summary>
        /// Return the raw object for customized processing.
        /// </summary>
        /// <returns></returns>
        public override object AsObject()
        {
            if (!cacheable)
            {
                ProcessObject(false);
            }
            return rawObject;
        }

        /// <summary>
        /// Return the object conditioned as a boolean.
        /// </summary>
        /// <returns></returns>
        public override bool AsBool()
        {
            if (!cacheable)
            {
                ProcessObject(false);
            }
            return (doubleValue != 0.0);
        }

        /// <summary>
        /// Return the value as a DynValue for Lua processing.
        /// </summary>
        /// <returns></returns>
        public override DynValue AsDynValue()
        {
            if (variableType == VariableType.Constant)
            {
                return luaValue;
            }
            else
            {
                switch (valueType)
                {
                    case EvaluationType.Boolean:
                        return DynValue.NewBoolean(doubleValue != 0.0);
                    case EvaluationType.Double:
                        return DynValue.NewNumber(doubleValue);
                    case EvaluationType.String:
                        return DynValue.NewString(stringValue);
                }

                // ValueType.Nil and fallthrough:
                return DynValue.NewNil();
            }
        }

        /// <summary>
        /// Return the numeric value of this variable (NaN and Inf are silently treated
        /// as 0.0).
        /// </summary>
        /// <returns></returns>
        public override double AsDouble()
        {
            if (!cacheable)
            {
                ProcessObject(false);
            }
            return doubleValue;
        }

        /// <summary>
        /// Return the value as a string.
        /// </summary>
        /// <returns></returns>
        public override string AsString()
        {
            if (!cacheable)
            {
                ProcessObject(false);
            }
            return stringValue;
        }

        /// <summary>
        /// Process and classify the raw object that comes from the native
        /// evaluator.
        /// </summary>
        private void ProcessObject(bool invokeCallbacks)
        {
            object value = evaluator();
            if (rawObject == null || !value.Equals(rawObject))
            {
                double oldSafeValue = doubleValue;
                rawObject = value;

                if (value is double)
                {
                    doubleValue = (double)value;
                    if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))
                    {
                        doubleValue = 0.0;
                    }
                    stringValue = string.Format("{0:R}", doubleValue);
                    valueType = EvaluationType.Double;
                }
                else if (value is string)
                {
                    stringValue = value as string;
                    doubleValue = 0.0;
                    valueType = EvaluationType.String;
                    // Since strings never change doubleValue, we force them
                    // to trigger callbacks here.
                    if (invokeCallbacks)
                    {
                        TriggerNumericCallbacks(0.0);
                    }
                }
                else if (value is bool)
                {
                    bool bValue = (bool)value;
                    doubleValue = (bValue) ? 1.0 : 0.0;
                    stringValue = bValue.ToString();
                    valueType = EvaluationType.Boolean;
                }
                else if (value == null)
                {
                    doubleValue = 0.0;
                    stringValue = name;
                    valueType = EvaluationType.Nil;
                }
                else
                {
                    // TODO ...?
                    throw new NotImplementedException("ProcessObject found an unexpected return type " + value.GetType() + " for " + name);
                }

                // There is actually inadequate precision in a 32 bit float
                // to detect fixed update time deltas by the time 96 hours
                // have passed on the UT clock, so we leave the values as
                // double precision and compare the delta to the Mathf.Epsilon.
                if (Math.Abs(oldSafeValue - doubleValue) > Mathf.Epsilon)
                {
                    TriggerNumericCallbacks(doubleValue);
                }
            }
        }

        internal override void Evaluate(bool invokeCallbacks)
        {
            if (variableType == VariableType.Dependent && triggerUpdate == false)
            {
                return; // early
            }

            triggerUpdate = false;

            ProcessObject(invokeCallbacks);
        }
    }

    /// <summary>
    /// Variable representing a string.
    /// </summary>
    public class StringVariable : Variable
    {
        Func<string> evaluator;
        string stringValue;

        public StringVariable(string stringValue)
            : base(stringValue, true, VariableType.Constant)
        {
            this.stringValue = stringValue;
        }

        /// <summary>
        /// Construct a dynamic native evaluator.
        /// </summary>
        /// <param name="name">Name (canonical) of the variable.</param>
        /// <param name="evaluator">The function used to evaluate the variable.</param>
        /// <param name="cacheable">Whether the variable is cacheable (if false, the variable must be evaluated every time it's called).</param>
        /// <param name="mutable">Whether the variable will ever change value.</param>
        /// <param name="variableType">The type of variable.</param>
        public StringVariable(string name, Func<string> evaluator, bool cacheable, bool mutable, VariableType variableType)
            : base(name, (mutable) ? cacheable : true, (mutable) ? variableType : VariableType.Constant)
        {
            this.evaluator = evaluator;

            Evaluate(false);
        }

        /// <summary>
        /// Return the raw object for customized processing.
        /// </summary>
        /// <returns></returns>
        public override object AsObject()
        {
            if (!cacheable)
            {
                Evaluate(false);
            }
            return stringValue;
        }

        /// <summary>
        /// Return the object conditioned as a boolean.
        /// </summary>
        /// <returns></returns>
        public override bool AsBool()
        {
            return false;
        }

        /// <summary>
        /// Return the value as a DynValue for Lua processing.
        /// </summary>
        /// <returns></returns>
        public override DynValue AsDynValue()
        {
            return DynValue.NewString(stringValue);
        }

        /// <summary>
        /// Return the numeric value of this variable (NaN and Inf are silently treated
        /// as 0.0).
        /// </summary>
        /// <returns></returns>
        public override double AsDouble()
        {
            return 0.0;
        }

        /// <summary>
        /// Return the value as a string.
        /// </summary>
        /// <returns></returns>
        public override string AsString()
        {
            if (!cacheable)
            {
                Evaluate(false);
            }
            return stringValue;
        }

        /// <summary>
        /// Evaluate() conditionally updates the variable by calling the evaluator.
        /// </summary>
        internal override void Evaluate(bool invokeCallbacks)
        {
            if (variableType == VariableType.Dependent && triggerUpdate == false)
            {
                return; // early
            }

            triggerUpdate = false;

            string newValue = evaluator();

            stringValue = newValue;

            if (invokeCallbacks)
            {
                // Note - this is primarily for Dependent variables who
                // don't care about safeValue.
                TriggerNumericCallbacks(0.0);
            }
        }
    }
}
