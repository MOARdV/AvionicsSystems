//#define PLENTIFUL_LOGGING
#if PLENTIFUL_LOGGING
//#define EXCESSIVE_LOGGING
#endif
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
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// This class is a convenience object that allows me to automate which proxy
    /// objects have been instantiated, instead of having to manually maintain
    /// static dictionaries.
    /// </summary>
    public class MASRegisteredTable
    {
        public Type type
        {
            get
            {
                return proxy.GetType();
            }
        }
        public object proxy { get; private set; }
        internal MASRegisteredTable(object who)
        {
            proxy = who;
        }
    }

    public partial class MASFlightComputer : PartModule
    {
        /// <summary>
        /// Dictionary mapping variable names to Variable objects.
        /// </summary>
        private Dictionary<string, Variable> variables = new Dictionary<string, Variable>();

        /// <summary>
        /// Converts unformatted strings to canonical formats.
        /// </summary>
        private Dictionary<string, string> canonicalVariableName = new Dictionary<string, string>();

        /// <summary>
        /// List of variables that change (as opposed to constants).
        /// </summary>
        private List<Variable> mutableVariablesList = new List<Variable>();

        /// <summary>
        /// Because a List is more expensive to traverse than an array, and
        /// because we are going to iterate over the mutable variables
        /// A LOT, we keep arrays that we really iterate over.  To further
        /// track what's going on, we split the list into 'native' variables
        /// that are delegates and 'lua' variables that are Lua scripts.
        /// </summary>
        private Variable[] nativeVariables = new Variable[0];

        /// <summary>
        /// The array of Lua variables (see description for nativeVariables).
        /// </summary>
        private Variable[] luaVariables = new Variable[0];

        /// <summary>
        /// Array of dependent variables.
        /// </summary>
        private Variable[] dependentVariables = new Variable[0];

        private int luaVariableCount = 0;
        private int nativeVariableCount = 0;
        private int dependentVariableCount = 0;
        private int constantVariableCount = 0;

        private Dictionary<string, MASRegisteredTable> registeredTables = new Dictionary<string, MASRegisteredTable>();

        /// <summary>
        /// Given a table (object) name, a method name, and an array of
        /// parameters, use reflection to find the object and Method that
        /// corresponds to that table.method pair.
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="methodName"></param>
        /// <param name="parameters"></param>
        /// <param name="tableRef"></param>
        /// <param name="methodInfo"></param>
        private void GetMASIMethod(string tableName, string methodName, Type[] parameters, out object tableRef, out MethodInfo methodInfo)
        {
            methodInfo = null;
            tableRef = null;
            var tableInfo = registeredTables[tableName];
            methodInfo = FindMethod(tableName, methodName, tableInfo.type, parameters);

            if (methodInfo != null)
            {
                tableRef = tableInfo.proxy;
            }
        }

        /// <summary>
        /// Find a method with the supplied name within the supplied class (Type)
        /// with a parameter list that matches the types in the VariableParameter array.
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="methodName"></param>
        /// <param name="type"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private MethodInfo FindMethod(string tableName, string methodName, Type type, Type[] parameters)
        {
            int numParams = parameters.Length;
            MethodInfo[] methods = type.GetMethods();
            for (int i = methods.Length - 1; i >= 0; --i)
            {
                if (methods[i].Name == methodName)
                {
                    ParameterInfo[] methodParams = methods[i].GetParameters();
                    if (methodParams.Length == numParams)
                    {
                        if (numParams == 0)
                        {
                            return methods[i];
                        }
                        else
                        {
                            bool match = true;
                            for (int index = 0; index < numParams; ++index)
                            {
                                if (!(methodParams[index].ParameterType == typeof(object) || parameters[index] == typeof(object) || methodParams[index].ParameterType == parameters[index]))
                                {
                                    match = false;
                                    Utility.LogError(this, "Processing {0}.{1}(): Did not find a match for parameter {2} (expecting {3}, but got {4}).",
                                        tableName, methodName,
                                        index + 1,
                                        methodParams[index].ParameterType, parameters[index]);
                                    throw new ArgumentException(string.Format("Parameter type mismatch in {0}.{1}(): Did not find a match for parameter {2} (expecting {3}, but got {4}).",
                                        tableName, methodName,
                                        index + 1,
                                        methodParams[index].ParameterType, parameters[index]));
                                }
                            }

                            if (match)
                            {
                                return methods[i];
                            }
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get the named Variable (for direct access).
        /// 
        /// So, that's the really short summary, but it does not tell you what
        /// I am doing.
        /// 
        /// The MoonSharp interpreter, version 1.6.0, processes about 60-90
        /// variables per millisecond using the various test IVAs I've created.
        /// That's really poor performance - around 2.5ms per FixedUpdate, which
        /// fires every 20ms at default frequency.  In other words, a big chunk
        /// of the FixedUpdate time budget, just for MAS variables.  No bueno.
        /// 
        /// So, I use a Lexer object (see Parser/Lexer.cs) and the Bantam Pratt
        /// parser to transform the variable to an expression tree.  The expression
        /// tree allows me to break the variable into easy-to-digest components,
        /// each of which is shoved into a delegate (or directly into a Variable
        /// for constant values).  Doing so greatly expands the number of Variable
        /// objects I create, but it also allows me to pull more processing away from
        /// the slow Lua interpreter and into quicker delegates.
        /// 
        /// A simple initial implementation where I transfered constants and simple
        /// single-parameter methods (eg 'fc.GetPersistentAsNumber("SomePersistent"),
        /// moved the refresh rate above 120 updates/ms.  That's better.  The fewer
        /// calls into Lua / MoonSharp, the better.  The simple implementation also
        /// accounted for about half of the total number of variables.
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="prop"></param>
        /// <returns></returns>
        internal Variable GetVariable(string variableName, InternalProp prop)
        {
            variableName = ConditionVariableName(variableName, prop);
            if (variableName.Length < 1)
            {
                Utility.ComplainLoudly("GetVariable with empty variableName");
                throw new ArgumentException("[MASFlightComputer] Trying to GetVariable with empty variableName");
            }

            Variable v = null;

            // Find out if we've already parsed a variable with this name.  If we have,
            // and we've already generated a Variable based on it, don't run it through
            // the parser again (which takes some time and creates a few temporary allocations).
            // Performance doesn't actually seem affected in my installation, though, so
            // maybe parsing these text snippets isn't that costly.
            string canonicalName;
            if (canonicalVariableName.TryGetValue(variableName, out canonicalName))
            {
                variables.TryGetValue(canonicalName, out v);
            }

            if (v == null)
            {
                CodeGen.Parser.CompilerResult result = CodeGen.Parser.TryParse(variableName);

                // Because variables may be added when parsing an expression tree, we need to
                // make sure we check the canonical variable name map here.
                if (!canonicalVariableName.ContainsKey(variableName))
                {
                    canonicalVariableName[variableName] = result.canonicalName;
                }

                if (!variables.TryGetValue(result.canonicalName, out v))
                {
#if PLENTIFUL_LOGGING
                Utility.LogMessage(this, "*  *  *");
                Utility.LogMessage(this, "Generating variable from {0}", result.canonicalName);
#endif
                    if (result.type == CodeGen.Parser.ResultType.NUMERIC_CONSTANT)
                    {
#if EXCESSIVE_LOGGING
                    Utility.LogMessage(this, "- NUMERIC_CONSTANT");
#endif
                        v = new Variable(result.numericConstant);
                        ++constantVariableCount;
                    }
                    else if (result.type == CodeGen.Parser.ResultType.STRING_CONSTANT)
                    {
#if EXCESSIVE_LOGGING
                    Utility.LogMessage(this, "- STRING_CONSTANT");
#endif
                        v = new Variable(result.stringConstant);
                        ++constantVariableCount;
                    }
                    else if (result.type == CodeGen.Parser.ResultType.EXPRESSION_TREE)
                    {
#if EXCESSIVE_LOGGING

                    Utility.LogMessage(this, "- EXPRESSION_TREE");
#endif
                        v = GenerateVariable(result.expressionTree);
                    }

                    if (v == null)
                    {
#if PLENTIFUL_LOGGING
                    Utility.LogMessage(this, "- fall back to Lua scripting");
#endif
                        // If we couldn't find a way to optimize the value, fall
                        // back to interpreted Lua script.
                        v = new Variable(result.canonicalName, script);
                        if (v.valid == false)
                        {
                            throw new ArgumentException(string.Format("Unable to process variable {0}", result.canonicalName));
                        }

                        ++luaVariableCount;
                        Utility.LogMessage(this, "luaVariableCount increased in GetVariable -- this may be buggy -- for {0}", result.canonicalName);
                    }
                    if (!variables.ContainsKey(result.canonicalName))
                    {
                        variables.Add(result.canonicalName, v);
                        if (v.mutable)
                        {
                            mutableVariablesList.Add(v);
                            mutableVariablesChanged = true;
                        }
                        else if (v.variableType == Variable.VariableType.Unknown)
                        {
                            Utility.LogError(this, "There was an error processing variable {0}", variableName);
                        }
#if PLENTIFUL_LOGGING
                    Utility.LogMessage(this, "Adding new variable '{0}'", result.canonicalName);
#endif
                    }
                }
            }

            return v;
        }

        /// <summary>
        /// Transform an expression into a delegate.
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        private Variable GenerateVariable(CodeGen.Expression expression)
        {
            string canonical = expression.CanonicalName();
            Variable v = null;
            if (variables.TryGetValue(canonical, out v))
            {
                return v;
            }

            switch (expression.ExpressionType())
            {
                case CodeGen.ExpressionIs.ConstantNumber:
#if EXCESSIVE_LOGGING
                    Utility.LogMessage(this, "-- GenerateVariable(): NumberExpression");
#endif
                    v = new Variable((expression as CodeGen.NumberExpression).getNumber());
                    break;
                case CodeGen.ExpressionIs.ConstantString:
#if EXCESSIVE_LOGGING
                    Utility.LogMessage(this, "-- GenerateVariable(): StringExpression");
#endif
                    v = new Variable((expression as CodeGen.StringExpression).getString());
                    break;
                case CodeGen.ExpressionIs.Operator:
#if EXCESSIVE_LOGGING
                    Utility.LogMessage(this, "-- GenerateVariable(): OperatorExpression");
#endif
                    v = GenerateOperatorVariable(expression as CodeGen.OperatorExpression);
                    break;
                case CodeGen.ExpressionIs.PrefixOperator:
#if EXCESSIVE_LOGGING
                    Utility.LogMessage(this, "-- GenerateVariable(): PrefixExpression");
#endif
                    v = GeneratePrefixVariable(expression as CodeGen.PrefixExpression);
                    break;
                case CodeGen.ExpressionIs.Call:
#if EXCESSIVE_LOGGING
                    Utility.LogMessage(this, "-- GenerateVariable(): CallExpression");
#endif
                    v = GenerateCallVariable(expression as CodeGen.CallExpression);
                    break;
                case CodeGen.ExpressionIs.Name:
#if EXCESSIVE_LOGGING
                    Utility.LogMessage(this, "-- GenerateVariable(): NameExpression");
#endif
                    v = GenerateNameVariable(expression as CodeGen.NameExpression);
                    break;
                default:
#if PLENTIFUL_LOGGING
                    Utility.LogErrorMessage(this, "!! GenerateVariable(): Unhandled expression type {0}", expression.GetType());
#endif
                    //v = new Variable(canonical, script);
                    break;
            }

            if (v != null && !variables.ContainsKey(canonical))
            {
                variables.Add(canonical, v);
                if (v.mutable)
                {
                    mutableVariablesList.Add(v);
                    mutableVariablesChanged = true;
                }

                if (v.variableType == Variable.VariableType.Constant)
                {
                    ++constantVariableCount;
                }
                else if (v.variableType == Variable.VariableType.Func)
                {
                    ++nativeVariableCount;
                }
                else if (v.variableType == Variable.VariableType.Dependent)
                {
                    ++dependentVariableCount;
                }
                else if (v.variableType == Variable.VariableType.LuaScript || v.variableType == Variable.VariableType.LuaClosure)
                {
                    ++luaVariableCount;
                }
                else
                {
                    Utility.LogError(this, "There was an error processing variable {0}", canonical);
                }
            }
            if (v == null)
            {
                Utility.LogWarning(this, "CAUTION: Failed to generate variable for {0} - check its name?", canonical);
                Utility.LogMessage(this, "Additional info: expression was type {0}", expression.GetType());
            }
            return v;
        }

        /// <summary>
        /// Generate a variable from a name expression.
        /// </summary>
        /// <param name="nameExpression"></param>
        /// <returns></returns>
        private Variable GenerateNameVariable(CodeGen.NameExpression nameExpression)
        {
            if (nameExpression.getName() == "true")
            {
                return new Variable(true);
            }
            else if (nameExpression.getName() == "false")
            {
                return new Variable(false);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Take a binary operator (+, -, *, /, <, >, etc) and transform it into
        /// a variable.
        /// </summary>
        /// <param name="operatorExpression"></param>
        /// <returns></returns>
        private Variable GenerateOperatorVariable(CodeGen.OperatorExpression operatorExpression)
        {
            Variable lhs = GenerateVariable(operatorExpression.LeftOperand());
            Variable rhs = GenerateVariable(operatorExpression.RightOperand());
#if EXCESSIVE_LOGGING
            Utility.LogMessage(this, "--- GenerateOperatorVariable(): operator {0}", operatorExpression.Operator());
#endif
#if PLENTIFUL_LOGGING
            if (lhs == null)
            {
                if (rhs == null)
                {
                    Utility.LogMessage(this, "!!! GenerateOperatorVariable(): Failed to find both operands {0} and {1}",
                        operatorExpression.LeftOperand().CanonicalName(), operatorExpression.RightOperand().CanonicalName());
                }
                else
                {
                    Utility.LogMessage(this, "!!! GenerateOperatorVariable(): Failed to find left operand {0}",
                        operatorExpression.LeftOperand().CanonicalName());
                }

                return null;
            }
            else if (rhs == null)
            {
                Utility.LogMessage(this, "!!! GenerateOperatorVariable(): Failed to find right operand {0}",
                    operatorExpression.RightOperand().CanonicalName());
                return null;
            }
#endif
            Variable v = null;
            switch (operatorExpression.Operator())
            {
                case CodeGen.Parser.LuaToken.PLUS:
                    v = new Variable(operatorExpression.CanonicalName(), () => lhs.SafeValue() + rhs.SafeValue(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.numericCallbacks += v.TriggerUpdate;
                    rhs.numericCallbacks += v.TriggerUpdate;
                    break;
                case CodeGen.Parser.LuaToken.MINUS:
                    v = new Variable(operatorExpression.CanonicalName(), () => lhs.SafeValue() - rhs.SafeValue(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.numericCallbacks += v.TriggerUpdate;
                    rhs.numericCallbacks += v.TriggerUpdate;
                    break;
                case CodeGen.Parser.LuaToken.MULTIPLY:
                    v = new Variable(operatorExpression.CanonicalName(), () => lhs.SafeValue() * rhs.SafeValue(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.numericCallbacks += v.TriggerUpdate;
                    rhs.numericCallbacks += v.TriggerUpdate;
                    break;
                case CodeGen.Parser.LuaToken.DIVIDE:
                    v = new Variable(operatorExpression.CanonicalName(), () => lhs.SafeValue() / rhs.SafeValue(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.numericCallbacks += v.TriggerUpdate;
                    rhs.numericCallbacks += v.TriggerUpdate;
                    break;
                case CodeGen.Parser.LuaToken.MODULO:
                    v = new Variable(operatorExpression.CanonicalName(), () => lhs.SafeValue() % rhs.SafeValue(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.numericCallbacks += v.TriggerUpdate;
                    rhs.numericCallbacks += v.TriggerUpdate;
                    break;
                case CodeGen.Parser.LuaToken.EXPONENT:
                    v = new Variable(operatorExpression.CanonicalName(), () => Math.Pow(lhs.SafeValue(), rhs.SafeValue()), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.numericCallbacks += v.TriggerUpdate;
                    rhs.numericCallbacks += v.TriggerUpdate;
                    break;
                case CodeGen.Parser.LuaToken.LESS_THAN:
                    v = new Variable(operatorExpression.CanonicalName(), () => lhs.SafeValue() < rhs.SafeValue(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.numericCallbacks += v.TriggerUpdate;
                    rhs.numericCallbacks += v.TriggerUpdate;
                    break;
                case CodeGen.Parser.LuaToken.GREATER_THAN:
                    v = new Variable(operatorExpression.CanonicalName(), () => lhs.SafeValue() > rhs.SafeValue(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.numericCallbacks += v.TriggerUpdate;
                    rhs.numericCallbacks += v.TriggerUpdate;
                    break;
                case CodeGen.Parser.LuaToken.EQUALITY:
                    v = new Variable(operatorExpression.CanonicalName(), () => lhs.SafeValue() == rhs.SafeValue(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.numericCallbacks += v.TriggerUpdate;
                    rhs.numericCallbacks += v.TriggerUpdate;
                    break;
                case CodeGen.Parser.LuaToken.INEQUALITY:
                    v = new Variable(operatorExpression.CanonicalName(), () => lhs.SafeValue() != rhs.SafeValue(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.numericCallbacks += v.TriggerUpdate;
                    rhs.numericCallbacks += v.TriggerUpdate;
                    break;
                case CodeGen.Parser.LuaToken.LESS_EQUAL:
                    v = new Variable(operatorExpression.CanonicalName(), () => lhs.SafeValue() <= rhs.SafeValue(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.numericCallbacks += v.TriggerUpdate;
                    rhs.numericCallbacks += v.TriggerUpdate;
                    break;
                case CodeGen.Parser.LuaToken.GREATER_EQUAL:
                    v = new Variable(operatorExpression.CanonicalName(), () => lhs.SafeValue() >= rhs.SafeValue(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.numericCallbacks += v.TriggerUpdate;
                    rhs.numericCallbacks += v.TriggerUpdate;
                    break;
                case CodeGen.Parser.LuaToken.AND:
                    v = new Variable(operatorExpression.CanonicalName(), () => lhs.BoolValue() && rhs.BoolValue(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.numericCallbacks += v.TriggerUpdate;
                    rhs.numericCallbacks += v.TriggerUpdate;
                    break;
                case CodeGen.Parser.LuaToken.OR:
                    v = new Variable(operatorExpression.CanonicalName(), () => lhs.BoolValue() || rhs.BoolValue(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.numericCallbacks += v.TriggerUpdate;
                    rhs.numericCallbacks += v.TriggerUpdate;
                    break;
                default:
#if PLENTIFUL_LOGGING
                    Utility.LogErrorMessage(this, "!!! GenerateOperatorVariable(): unsupported operator {0}", operatorExpression.Operator());
#endif
                    break;
            }
            return v;
        }

        /// <summary>
        /// Transform a prefix expression of the form (-number) into a numeric
        /// constant.
        /// </summary>
        /// <param name="prefixExpression"></param>
        /// <returns></returns>
        private Variable GeneratePrefixVariable(CodeGen.PrefixExpression prefixExpression)
        {
            CodeGen.Expression right = prefixExpression.getRight();

            if (prefixExpression.getOperator() == CodeGen.Parser.LuaToken.MINUS)
            {
                if (right is CodeGen.NumberExpression)
                {
                    double numericConstant = -(right as CodeGen.NumberExpression).getNumber();
                    return new Variable(numericConstant);
                }
                else
                {
                    Variable v = GenerateVariable(right);
                    if (v != null)
                    {
                        Variable newVar = new Variable(prefixExpression.CanonicalName(), () => -v.SafeValue(), true, true, Variable.VariableType.Dependent);
                        v.numericCallbacks += newVar.TriggerUpdate;
                        return newVar;
                    }
                }
            }
            else if (prefixExpression.getOperator() == CodeGen.Parser.LuaToken.NOT)
            {
                Variable v = GenerateVariable(right);
                if (v != null)
                {
                    Variable newVar = new Variable(prefixExpression.CanonicalName(), () => !v.BoolValue(), true, true, Variable.VariableType.Dependent);
                    v.numericCallbacks += newVar.TriggerUpdate;
                    return newVar;
                }

            }


            return null;
        }

        /// <summary>
        /// Given a CallExpression, convert it into either a native delegate or a Lua
        /// script function.
        /// </summary>
        /// <param name="callExpression"></param>
        /// <returns></returns>
        private Variable GenerateCallVariable(CodeGen.CallExpression callExpression)
        {
            string canonical = callExpression.CanonicalName();

            int numArgs = callExpression.NumArgs();
            Variable[] parms = new Variable[numArgs];
            Type[] parameters = new Type[numArgs];
#if EXCESSIVE_LOGGING
                Utility.LogMessage(this, "--- GenerateCallVariable(): {0} parameters", numArgs);
#endif
            for (int i = 0; i < numArgs; ++i)
            {
                CodeGen.Expression exp = callExpression.Arg(i);
#if EXCESSIVE_LOGGING
                    Utility.LogMessage(this, "--- GenerateCallVariable(): Parameter {0} is {1} (a {2})", i, "???"/*sb.ToString()*/, exp.ExpressionType());
#endif
                parms[i] = GenerateVariable(exp);
                if (parms[i] == null)
                {
#if PLENTIFUL_LOGGING
                        Utility.LogErrorMessage(this, "!!! GenerateCallVariable(): Unable to generate variable for parameter {0}, punting", i, exp.CanonicalName());
#endif
                    return null;
                }
                else
                {
                    parameters[i] = parms[i].RawValue().GetType();
#if EXCESSIVE_LOGGING
                        Utility.LogMessage(this, "--- GenerateCallVariable(): parameter[{0}] is {1}", i, parameters[i]);
#endif
                }
            }

            // Assume this is a MAS function.
            if (callExpression.Function().ExpressionType() == CodeGen.ExpressionIs.DotOperator)
            {
                object tableInstance;
                MethodInfo method;
                EvaluateDotOperator(callExpression.Function() as CodeGen.DotOperatorExpression, parameters, out tableInstance, out method);

                if (tableInstance != null)
                {
                    bool cacheable = true;
                    bool dependent = false;
                    bool mutable = true;
                    bool persistent = false;

                    object[] attrs = method.GetCustomAttributes(typeof(MASProxyAttribute), true);
                    if (attrs.Length > 0)
                    {
                        for (int i = 0; i < attrs.Length; ++i)
                        {
                            MASProxyAttribute attr = attrs[i] as MASProxyAttribute;
                            cacheable = !attr.Uncacheable;
                            mutable = !attr.Immutable;
                            dependent = attr.Dependent;
                            persistent = attr.Persistent;
                        }
                    }
                    ParameterInfo[] methodParams = method.GetParameters();
                    if (numArgs == 0)
                    {
                        Func<object, object> dm = DynamicMethodFactory.CreateFunc<object, object>(method);
#if EXCESSIVE_LOGGING
                        Utility.LogMessage(this, "--- GenerateCallVariable(): Creating variable for {0}, {1} parameters", canonical, numArgs);
#endif
                        return new Variable(canonical, () => dm(tableInstance), cacheable, mutable, Variable.VariableType.Func);
                    }
                    else if (numArgs == 1)
                    {
                        if (methodParams[0].ParameterType == typeof(double))
                        {
#if EXCESSIVE_LOGGING
                            Utility.LogMessage(this, "--- GenerateCallVariable(): Creating variable for {0}, with 1 parameter of type {1}", canonical, methodParams[0].ParameterType);
#endif
                            Func<object, double, object> dm = DynamicMethodFactory.CreateDynFunc<object, double, object>(method);
                            Variable newVar = new Variable(canonical, () => dm(tableInstance, parms[0].SafeValue()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                            if (dependent)
                            {
                                parms[0].numericCallbacks += newVar.TriggerUpdate;
                            }
                            return newVar;
                        }
                        else if (methodParams[0].ParameterType == typeof(string))
                        {
#if EXCESSIVE_LOGGING
                            Utility.LogMessage(this, "--- GenerateCallVariable(): Creating variable for {0}, with 1 parameter of type {1}", canonical, methodParams[0].ParameterType);
#endif
                            Func<object, string, object> dm = DynamicMethodFactory.CreateDynFunc<object, string, object>(method);
                            if (persistent)
                            {
                                dependent = false;
                                if (parms[0].variableType == Variable.VariableType.Constant)
                                {
                                    dependent = true;
                                }
                            }
                            Variable newVar = new Variable(canonical, () => dm(tableInstance, parms[0].String()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                            if (persistent && dependent)
                            {
                                RegisterPersistentNotice(parms[0].name, newVar.TriggerUpdate);
                            }
                            return newVar;
                        }
                        else if (methodParams[0].ParameterType == typeof(bool))
                        {
#if EXCESSIVE_LOGGING
                            Utility.LogMessage(this, "--- GenerateCallVariable(): Creating variable for {0}, with 1 parameter of type {1}", canonical, methodParams[0].ParameterType);
#endif
                            Func<object, bool, object> dm = DynamicMethodFactory.CreateDynFunc<object, bool, object>(method);
                            Variable newVar = new Variable(canonical, () => dm(tableInstance, parms[0].BoolValue()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                            if (dependent)
                            {
                                parms[0].numericCallbacks += newVar.TriggerUpdate;
                            }
                            return newVar;
                        }
                        else if (methodParams[0].ParameterType == typeof(object))
                        {
#if EXCESSIVE_LOGGING
                            Utility.LogMessage(this, "--- GenerateCallVariable(): Creating variable for {0}, with 1 parameter of type {1}", canonical, methodParams[0].ParameterType);
#endif
                            Func<object, object, object> dm = DynamicMethodFactory.CreateDynFunc<object, object, object>(method);
                            return new Variable(canonical, () => dm(tableInstance, parms[0].RawValue()), cacheable, mutable, Variable.VariableType.Func);
                        }
                        else
                        {
                            Utility.LogWarning(this, "!!! GenerateCallVariable(): Don't know how to create variable for {0}, with parameter {1}.  Falling back to Lua.", canonical, methodParams[0].ParameterType);
                        }
                    }
                    else if (numArgs == 2)
                    {
                        if (methodParams[0].ParameterType == typeof(double) && methodParams[1].ParameterType == typeof(double))
                        {
                            Func<object, double, double, object> dm = DynamicMethodFactory.CreateFunc<object, double, double, object>(method);
                            Variable newVar = new Variable(canonical, () => dm(tableInstance, parms[0].SafeValue(), parms[1].SafeValue()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                            if (dependent)
                            {
                                parms[0].numericCallbacks += newVar.TriggerUpdate;
                                parms[1].numericCallbacks += newVar.TriggerUpdate;
                            }
                            return newVar;
                        }
                        else if (methodParams[0].ParameterType == typeof(bool) && methodParams[1].ParameterType == typeof(double))
                        {
                            Func<object, bool, double, object> dm = DynamicMethodFactory.CreateFunc<object, bool, double, object>(method);
                            Variable newVar = new Variable(canonical, () => dm(tableInstance, parms[0].BoolValue(), parms[1].SafeValue()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                            if (dependent)
                            {
                                parms[0].numericCallbacks += newVar.TriggerUpdate;
                                parms[1].numericCallbacks += newVar.TriggerUpdate;
                            }
                            return newVar;
                        }
                        else if (methodParams[0].ParameterType == typeof(double) && methodParams[1].ParameterType == typeof(bool))
                        {
                            Func<object, double, bool, object> dm = DynamicMethodFactory.CreateFunc<object, double, bool, object>(method);
                            Variable newVar = new Variable(canonical, () => dm(tableInstance, parms[0].SafeValue(), parms[1].BoolValue()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                            if (dependent)
                            {
                                parms[0].numericCallbacks += newVar.TriggerUpdate;
                                parms[1].numericCallbacks += newVar.TriggerUpdate;
                            }
                            return newVar;
                        }
                        else
                        {
                            Utility.LogWarning(this, "!!! GenerateCallVariable(): Don't know how to create variable for {0}, with parameters {1} and {2}.  Falling back to Lua.", canonical, methodParams[0].ParameterType, methodParams[1].ParameterType);
                        }
                    }
                    else if (numArgs == 3)
                    {
                        if (methodParams[0].ParameterType == typeof(double) && methodParams[1].ParameterType == typeof(double) && methodParams[2].ParameterType == typeof(double))
                        {
                            Func<object, double, double, double, object> dm = DynamicMethodFactory.CreateFunc<object, double, double, double, object>(method);
                            Variable newVar = new Variable(canonical, () => dm(tableInstance, parms[0].SafeValue(), parms[1].SafeValue(), parms[2].SafeValue()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                            if (dependent)
                            {
                                parms[0].numericCallbacks += newVar.TriggerUpdate;
                                parms[1].numericCallbacks += newVar.TriggerUpdate;
                                parms[2].numericCallbacks += newVar.TriggerUpdate;
                            }
                            return newVar;
                        }
                        else if (methodParams[0].ParameterType == typeof(string) && methodParams[1].ParameterType == typeof(double) && methodParams[2].ParameterType == typeof(double))
                        {
                            Func<object, string, double, double, object> dm = DynamicMethodFactory.CreateFunc<object, string, double, double, object>(method);
                            return new Variable(canonical, () => dm(tableInstance, parms[0].String(), parms[1].SafeValue(), parms[2].SafeValue()), cacheable, mutable, Variable.VariableType.Func);
                        }
                        else if (methodParams[0].ParameterType == typeof(object) && methodParams[1].ParameterType == typeof(double) && methodParams[2].ParameterType == typeof(double))
                        {
                            Func<object, object, double, double, object> dm = DynamicMethodFactory.CreateFunc<object, object, double, double, object>(method);
                            return new Variable(canonical, () => dm(tableInstance, parms[0].RawValue(), parms[1].SafeValue(), parms[2].SafeValue()), cacheable, mutable, Variable.VariableType.Func);
                        }
                        else if (methodParams[0].ParameterType == typeof(bool) && methodParams[1].ParameterType == typeof(object) && methodParams[2].ParameterType == typeof(object))
                        {
                            Func<object, bool, object, object, object> dm = DynamicMethodFactory.CreateFunc<object, bool, object, object, object>(method);
                            return new Variable(canonical, () => dm(tableInstance, parms[0].BoolValue(), parms[1].RawValue(), parms[2].RawValue()), cacheable, mutable, Variable.VariableType.Func);
                        }
                        else
                        {
                            Utility.LogWarning(this, "!!! GenerateCallVariable(): Don't know how to create variable for {0}, with parameters {1}, {2}, and {3}.  Falling back to Lua.", canonical, methodParams[0].ParameterType, methodParams[1].ParameterType, methodParams[2].ParameterType);
                        }
                    }
                    else if (numArgs >= 4)
                    {
                        // Support any arbitrary number of arguments.
                        DynamicMethodDelegate dm = DynamicMethodFactory.CreateFunc(method);

                        Variable newVar = new Variable(canonical, () =>
                            {
                                object[] paramList = new object[numArgs];
                                for (int i = 0; i < numArgs; ++i)
                                {
                                    paramList[i] = parms[i].RawValue();
                                }
                                return dm(tableInstance, paramList);
                            }
                            , cacheable, mutable, Variable.VariableType.Func);

                        if (dependent)
                        {
                            for (int i = 0; i < numArgs; ++i)
                            {
                                parms[i].numericCallbacks += newVar.TriggerUpdate;
                            }
                        }
                        return newVar;
                    }
                    else
                    {
                        Utility.LogWarning(this, "!!! GenerateCallVariable(): Don't know how to create variable for {0} with {1} parameters.  Falling back to Lua.", canonical, numArgs);
                    }
                }
#if PLENTIFUL_LOGGING
                else
                {
                    Utility.LogMessage(this, "!!! GenerateCallVariable(): Did not find method for {0}", canonical);
                }
#endif
            }
            else
            {
                // I think only a Lua script will hit here.
                Variable v = null;

                // If the function is a Name (as opposed to the dot operator above), we assume
                // that it's supposed to be a Lua script.  We attempt to fetch the global
                // associated with the function's name.  If that's a DataType.Function, then
                // we can evaluate it as a closure.
                if (callExpression.Function().ExpressionType() == CodeGen.ExpressionIs.Name)
                {
                    try
                    {
                        DynValue closure = script.Globals.Get(callExpression.Function().CanonicalName());

                        if (closure.Type == DataType.Function)
                        {
                            if (parms.Length == 0)
                            {
                                return new Variable(canonical, () =>
                                {
                                    return script.Call(closure).ToObject();
                                }, true, true, Variable.VariableType.LuaClosure);
                            }
                            else
                            {
                                // Is this the best way to do this?  Or should I write it as fixed-length arrays per-parameter length instead?
                                return new Variable(canonical, () =>
                                {
                                    DynValue[] callParams = new DynValue[parms.Length];
                                    for (int i = 0; i < parms.Length; ++i)
                                    {
                                        callParams[i] = parms[i].AsDynValue();
                                    }
                                    return script.Call(closure, callParams).ToObject();
                                }, true, true, Variable.VariableType.LuaClosure);
                            }
                        }
                    }
                    catch
                    {
                        // No-op.  Soak the exception and fall back below.
                    }
                }

                // Fall back to evaluating the text as a Lua snippet every FixedUpdate.
                // NOTE: It's possible that Lua stdlib methods (eg, math.sin) are used.  Right now,
                // I fall back to here to evaluate them.  I suppose I could add Lua table evaluation
                // to the DotOperator path, and that may allow a more efficient evaluation, since
                // I'd be able to call the method inside the table directly.
                v = new Variable(canonical, script);
                if (v.valid)
                {
                    Utility.LogMessage(this, "Did not evaluate {0} - fell back to script evaluation.", canonical);
#if EXCESSIVE_LOGGING
                    Utility.LogMessage(this, "--- GenerateCallVariable(): Created Lua variable for {0}", canonical);
#endif
                    return v;
                }

#if EXCESSIVE_LOGGING
                Utility.LogMessage(this, "--- GenerateCallVariable(): Not able to find method for {0}", canonical);
#endif
            }

            return null;
        }

        /// <summary>
        /// Dot Operator evaluation for the case of a table.method pair - does not handle
        /// convention Lua tables.  It's only for the case of a reflected object.method,
        /// such as fc.Pitch().
        /// </summary>
        /// <param name="dotOperatorExpression"></param>
        /// <param name="parameters"></param>
        /// <param name="tableInstance"></param>
        /// <param name="method"></param>
        private void EvaluateDotOperator(CodeGen.DotOperatorExpression dotOperatorExpression, Type[] parameters, out object tableInstance, out MethodInfo method)
        {
            tableInstance = null;
            method = null;

            CodeGen.NameExpression tableName = dotOperatorExpression.TableName() as CodeGen.NameExpression;
            CodeGen.NameExpression methodName = dotOperatorExpression.MethodName() as CodeGen.NameExpression;

            if (tableName != null && methodName != null)
            {
                if (registeredTables.ContainsKey(tableName.getName()))
                {
                    GetMASIMethod(tableName.getName(), methodName.getName(), parameters, out tableInstance, out method);
                }
                else
                {
                    Utility.LogWarning(this, "No table named \"{0}\" is registered in the variables map.  Something may need updated.", tableName.getName());
                }
            }
        }

        //--------------------------------------------------------------------
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
            /// <summary>
            /// Variable name
            /// </summary>
            public readonly string name;
            /// <summary>
            /// Whether the variable can change (otherwise it is a constant)
            /// </summary>
            public readonly bool mutable;
            /// <summary>
            /// Is this variable even valid?
            /// </summary>
            public readonly bool valid;
            /// <summary>
            /// Can the results be cached, or must they be evaluated each time
            /// it is called?
            /// </summary>
            public readonly bool cacheable;
            internal event Action<double> numericCallbacks;
            internal event Action changeCallbacks;
            private Func<object> nativeEvaluator;
            // Lua script that is invoked:
            private DynValue luaEvaluator;
            private object rawObject;
            private string stringValue;
            private double doubleValue;
            private double safeValue;
            // Lua-native results
            private DynValue luaValue;
            internal readonly VariableType variableType = VariableType.Unknown;
            private bool triggerUpdate;

            /// <summary>
            /// How do we evaluate this variable?
            /// </summary>
            public enum VariableType
            {
                /// <summary>
                /// Invalid type
                /// </summary>
                Unknown,
                /// <summary>
                /// Complex Lua script
                /// </summary>
                LuaScript,
                /// <summary>
                /// Simple Lua closure
                /// </summary>
                LuaClosure,
                /// <summary>
                /// Constant numeric or string value
                /// </summary>
                Constant,
                /// <summary>
                /// A lambda expression whose results are non-deterministic (does not depend on inputs)
                /// </summary>
                Func,
                /// <summary>
                /// A lambda expression whose results are always wholly dependent on the input(s), and thus
                /// does not need to be evaluated on every FixedUpdate.
                /// </summary>
                Dependent,
            };

            /// <summary>
            /// Construct a constant boolean Variable.
            /// </summary>
            /// <param name="value"></param>
            public Variable(bool value)
            {
                this.name = string.Format("{0}", value);

                this.valid = true;
                this.stringValue = this.name;
                this.doubleValue = value ? 1.0 : 0.0;
                this.safeValue = this.doubleValue;
                this.rawObject = value;
                this.variableType = VariableType.Constant;
                this.cacheable = true;
                this.mutable = false;
                this.luaValue = DynValue.NewBoolean(value);
            }

            /// <summary>
            /// Construct a constant numeric Variable.
            /// </summary>
            /// <param name="value"></param>
            public Variable(double value)
            {
                this.name = string.Format("{0:R}", value);

                this.valid = true;
                this.stringValue = this.name;
                this.doubleValue = value;
                this.safeValue = value;
                this.rawObject = value;
                this.variableType = VariableType.Constant;
                this.cacheable = true;
                this.mutable = false;
                this.luaValue = DynValue.NewNumber(value);
            }

            /// <summary>
            /// Construct a constant string Variable.
            /// </summary>
            /// <param name="value"></param>
            public Variable(string value)
            {
                this.name = value;

                this.valid = true;
                this.stringValue = value;
                this.doubleValue = double.NaN;
                this.safeValue = 0.0;
                this.rawObject = value;
                this.variableType = VariableType.Constant;
                this.cacheable = true;
                this.mutable = false;
                this.luaValue = DynValue.NewString(value);
            }

            /// <summary>
            /// Construct a dynamic native evaluator.
            /// </summary>
            /// <param name="name"></param>
            /// <param name="nativeEvaluator"></param>
            /// <param name="cacheable"></param>
            public Variable(string name, Func<object> nativeEvaluator, bool cacheable, bool mutable, VariableType variableType)
            {
                this.name = name;

                this.nativeEvaluator = nativeEvaluator;

                this.valid = true;
                this.cacheable = (mutable) ? cacheable : true;
                this.mutable = mutable;
                this.variableType = (mutable) ? variableType : VariableType.Constant;

                ProcessObject(nativeEvaluator());
            }

            /// <summary>
            /// Construct a dynamic Lua Variable.
            /// </summary>
            /// <param name="name"></param>
            /// <param name="script"></param>
            public Variable(string name, Script script)
            {
                this.name = name;

                try
                {
                    // TODO: MoonSharp "hardwiring" - does it help performance?
                    luaEvaluator = script.LoadString("return " + name);
                    this.luaValue = script.Call(luaEvaluator);
                    this.valid = true;
                }
                catch (Exception e)
                {
                    Utility.ComplainLoudly("Error creating variable " + name);
                    Utility.LogError(this, "Unknown variable '{0}':", name);
                    Utility.LogError(this, e.ToString());
                    this.luaValue = null;
                    this.valid = false;
                }

                if (this.valid)
                {
                    this.variableType = VariableType.LuaScript;
                    ProcessObject(this.luaValue.ToObject());
                }
                this.cacheable = true;
                this.mutable = true;
            }

            /// <summary>
            /// Return the raw object for customized processing.
            /// </summary>
            /// <returns></returns>
            public object RawValue()
            {
                if (!cacheable)
                {
                    // Only permitted for native objects
                    ProcessObject(nativeEvaluator());
                }
                return rawObject;
            }

            /// <summary>
            /// Return the object conditioned as a boolean.
            /// </summary>
            /// <returns></returns>
            public bool BoolValue()
            {
                if (!cacheable)
                {
                    // Only permitted for native objects
                    ProcessObject(nativeEvaluator());
                }
                return (safeValue != 0.0);
            }

            public DynValue AsDynValue()
            {
                return luaValue;
            }

            /// <summary>
            /// Return the raw double value of this variable (including NaN and Inf).
            /// </summary>
            /// <returns></returns>
            public double Value()
            {
                if (!cacheable)
                {
                    // Only permitted for native objects
                    ProcessObject(nativeEvaluator());
                }
                return doubleValue;
            }

            /// <summary>
            /// Return the safe value of this variable (NaN and Inf are silently treated
            /// as 0.0).
            /// </summary>
            /// <returns></returns>
            public double SafeValue()
            {
                if (!cacheable)
                {
                    // Only permitted for native objects
                    ProcessObject(nativeEvaluator());
                }
                return safeValue;
            }

            /// <summary>
            /// Return the value as a string.
            /// </summary>
            /// <returns></returns>
            public string String()
            {
                if (!cacheable)
                {
                    // Only permitted for native objects
                    ProcessObject(nativeEvaluator());
                }
                return stringValue;
            }

            /// <summary>
            /// Process and classify the raw object that comes from the native
            /// evaluator.
            /// </summary>
            /// <param name="value"></param>
            private void ProcessObject(object value)
            {
                if (rawObject == null || !value.Equals(rawObject))
                {
                    double oldSafeValue = safeValue;
                    rawObject = value;

                    if (value is double)
                    {
                        doubleValue = (double)value;
                        safeValue = doubleValue;
                        stringValue = doubleValue.ToString();
                        luaValue = DynValue.NewNumber(doubleValue);
                    }
                    else if (value is string)
                    {
                        stringValue = value as string;
                        doubleValue = double.NaN;
                        safeValue = 0.0;
                        luaValue = DynValue.NewString(stringValue);
                        // Note - this is primarily for Dependent variables who
                        // don't care about safeValue.
                        if (numericCallbacks != null)
                        {
                            numericCallbacks.Invoke(safeValue);
                        }
                    }
                    else if (value is bool)
                    {
                        bool bValue = (bool)value;
                        safeValue = (bValue) ? 1.0 : 0.0;
                        doubleValue = double.NaN;
                        stringValue = bValue.ToString();
                        luaValue = DynValue.NewBoolean(bValue);
                    }
                    else if (value == null)
                    {
                        safeValue = 0.0;
                        doubleValue = double.NaN;
                        stringValue = name;
                        luaValue = DynValue.NewNil();
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
                    //if (!Mathf.Approximately((float)safeValue, (float)oldSafeValue))
                    if (Math.Abs(oldSafeValue - safeValue) > Mathf.Epsilon)
                    {
                        if (numericCallbacks != null)
                        {
                            numericCallbacks.Invoke(safeValue);
                        }
                    }

                    if (changeCallbacks != null)
                    {
                        changeCallbacks.Invoke();
                    }
                }
            }

            /// <summary>
            /// Evaluate updates the variable in question by calling the code
            /// snippet using the supplied Lua script.
            /// </summary>
            /// <param name="script"></param>
            internal void Evaluate(Script script)
            {
                switch (variableType)
                {
                    case VariableType.LuaScript:
                        try
                        {
                            luaValue = script.Call(luaEvaluator);
                        }
                        catch
                        {
                            luaValue = DynValue.NewNil();
                        }

                        ProcessObject(luaValue.ToObject());
                        break;
                    case VariableType.Func:
                    case VariableType.LuaClosure:
                        ProcessObject(nativeEvaluator());
                        break;
                    case VariableType.Dependent:
                        if (triggerUpdate)
                        {
                            triggerUpdate = false;
                            ProcessObject(nativeEvaluator());
                        }
                        break;
                    default:
                        Utility.LogError(this, "Attempting to evaluate {0}, which is listed as {1} - this should not happen.", name, variableType);
                        break;
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
        }
    }
}
