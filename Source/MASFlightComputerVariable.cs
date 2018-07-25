//#define PLENTIFUL_LOGGING
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
            bool mismatch = false;
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
                            mismatch = true;
                            for (int index = 0; index < numParams; ++index)
                            {
                                if (!(methodParams[index].ParameterType == typeof(object) || parameters[index] == typeof(object) || methodParams[index].ParameterType == parameters[index]))
                                {
                                    match = false;
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

            if (mismatch)
            {
                // If we found the right method name, but the parameters were incompatible, we generate an error
                // here and hope falling back to Lua scripts will resolve it.
                Utility.LogError(this, "Searching for {0}.{1}(): Found a matching name, but not the right parameter types", tableName, methodName);
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
        private Variable GetVariable(string variableName, InternalProp prop)
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
                        v = new DoubleVariable(result.numericConstant);
                        ++constantVariableCount;
                    }
                    else if (result.type == CodeGen.Parser.ResultType.STRING_CONSTANT)
                    {
                        v = new StringVariable(result.stringConstant);
                        ++constantVariableCount;
                    }
                    else if (result.type == CodeGen.Parser.ResultType.EXPRESSION_TREE)
                    {
                        v = GenerateVariable(result.expressionTree);
                    }

                    if (v == null)
                    {
#if PLENTIFUL_LOGGING
                    Utility.LogMessage(this, "- fall back to Lua scripting");
#endif
                        // If we couldn't find a way to evaluate the value above, fall
                        // back to interpreted Lua script.
                        DynValue luaEvaluator = script.LoadString("return " + result.canonicalName);
                        v = new GenericVariable(result.canonicalName, () => script.Call(luaEvaluator).ToObject(), true, true, Variable.VariableType.LuaScript);

                        ++luaVariableCount;
                        Utility.LogWarning(this, "Generated out-of-band Lua variable for {0}", result.canonicalName);
                        Utility.LogWarning(this, "This could be an incorrect variable name, or something that could be optimized.");
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
                    Utility.LogMessage(this, "Adding new GenericVariable '{0}'", result.canonicalName);
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
                    v = new DoubleVariable((expression as CodeGen.NumberExpression).getNumber());
                    break;
                case CodeGen.ExpressionIs.ConstantString:
                    v = new StringVariable((expression as CodeGen.StringExpression).getString());
                    break;
                case CodeGen.ExpressionIs.Operator:
                    v = GenerateOperatorVariable(expression as CodeGen.OperatorExpression);
                    break;
                case CodeGen.ExpressionIs.PrefixOperator:
                    v = GeneratePrefixVariable(expression as CodeGen.PrefixExpression);
                    break;
                case CodeGen.ExpressionIs.Call:
                    v = GenerateCallVariable(expression as CodeGen.CallExpression);
                    break;
                case CodeGen.ExpressionIs.Name:
                    v = GenerateNameVariable(expression as CodeGen.NameExpression);
                    break;
                default:
#if PLENTIFUL_LOGGING
                    Utility.LogErrorMessage(this, "!! GenerateVariable(): Unhandled expression type {0}", expression.GetType());
#endif
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
                else if (v.variableType == Variable.VariableType.LuaScript)
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
                Utility.LogWarning(this, "Additional info: expression was type {0}", expression.GetType());
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
                return new BooleanVariable(true);
            }
            else if (nameExpression.getName() == "false")
            {
                return new BooleanVariable(false);
            }
            else
            {
                Utility.LogWarning(this, "Encountered unhandled NameExpression {0}", nameExpression.getName());
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
                    v = new DoubleVariable(operatorExpression.CanonicalName(), () => lhs.AsDouble() + rhs.AsDouble(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.RegisterNumericCallback(v.TriggerUpdate);
                    rhs.RegisterNumericCallback(v.TriggerUpdate);
                    break;
                case CodeGen.Parser.LuaToken.MINUS:
                    v = new DoubleVariable(operatorExpression.CanonicalName(), () => lhs.AsDouble() - rhs.AsDouble(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.RegisterNumericCallback(v.TriggerUpdate);
                    rhs.RegisterNumericCallback(v.TriggerUpdate);
                    break;
                case CodeGen.Parser.LuaToken.MULTIPLY:
                    v = new DoubleVariable(operatorExpression.CanonicalName(), () => lhs.AsDouble() * rhs.AsDouble(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.RegisterNumericCallback(v.TriggerUpdate);
                    rhs.RegisterNumericCallback(v.TriggerUpdate);
                    break;
                case CodeGen.Parser.LuaToken.DIVIDE:
                    v = new DoubleVariable(operatorExpression.CanonicalName(), () => lhs.AsDouble() / rhs.AsDouble(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.RegisterNumericCallback(v.TriggerUpdate);
                    rhs.RegisterNumericCallback(v.TriggerUpdate);
                    break;
                case CodeGen.Parser.LuaToken.MODULO:
                    v = new DoubleVariable(operatorExpression.CanonicalName(), () => lhs.AsDouble() % rhs.AsDouble(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.RegisterNumericCallback(v.TriggerUpdate);
                    rhs.RegisterNumericCallback(v.TriggerUpdate);
                    break;
                case CodeGen.Parser.LuaToken.EXPONENT:
                    v = new DoubleVariable(operatorExpression.CanonicalName(), () => Math.Pow(lhs.AsDouble(), rhs.AsDouble()), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.RegisterNumericCallback(v.TriggerUpdate);
                    rhs.RegisterNumericCallback(v.TriggerUpdate);
                    break;
                case CodeGen.Parser.LuaToken.LESS_THAN:
                    v = new BooleanVariable(operatorExpression.CanonicalName(), () => lhs.AsDouble() < rhs.AsDouble(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.RegisterNumericCallback(v.TriggerUpdate);
                    rhs.RegisterNumericCallback(v.TriggerUpdate);
                    break;
                case CodeGen.Parser.LuaToken.GREATER_THAN:
                    v = new BooleanVariable(operatorExpression.CanonicalName(), () => lhs.AsDouble() > rhs.AsDouble(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.RegisterNumericCallback(v.TriggerUpdate);
                    rhs.RegisterNumericCallback(v.TriggerUpdate);
                    break;
                case CodeGen.Parser.LuaToken.EQUALITY:
                    v = new BooleanVariable(operatorExpression.CanonicalName(), () => lhs.AsDouble() == rhs.AsDouble(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.RegisterNumericCallback(v.TriggerUpdate);
                    rhs.RegisterNumericCallback(v.TriggerUpdate);
                    break;
                case CodeGen.Parser.LuaToken.INEQUALITY:
                    v = new BooleanVariable(operatorExpression.CanonicalName(), () => lhs.AsDouble() != rhs.AsDouble(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.RegisterNumericCallback(v.TriggerUpdate);
                    rhs.RegisterNumericCallback(v.TriggerUpdate);
                    break;
                case CodeGen.Parser.LuaToken.LESS_EQUAL:
                    v = new BooleanVariable(operatorExpression.CanonicalName(), () => lhs.AsDouble() <= rhs.AsDouble(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.RegisterNumericCallback(v.TriggerUpdate);
                    rhs.RegisterNumericCallback(v.TriggerUpdate);
                    break;
                case CodeGen.Parser.LuaToken.GREATER_EQUAL:
                    v = new BooleanVariable(operatorExpression.CanonicalName(), () => lhs.AsDouble() >= rhs.AsDouble(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.RegisterNumericCallback(v.TriggerUpdate);
                    rhs.RegisterNumericCallback(v.TriggerUpdate);
                    break;
                case CodeGen.Parser.LuaToken.AND:
                    v = new BooleanVariable(operatorExpression.CanonicalName(), () => lhs.AsBool() && rhs.AsBool(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.RegisterNumericCallback(v.TriggerUpdate);
                    rhs.RegisterNumericCallback(v.TriggerUpdate);
                    break;
                case CodeGen.Parser.LuaToken.OR:
                    v = new BooleanVariable(operatorExpression.CanonicalName(), () => lhs.AsBool() || rhs.AsBool(), lhs.cacheable && rhs.cacheable, lhs.mutable || rhs.mutable, Variable.VariableType.Dependent);
                    lhs.RegisterNumericCallback(v.TriggerUpdate);
                    rhs.RegisterNumericCallback(v.TriggerUpdate);
                    break;
                default:
                    Utility.LogWarning(this, "Encountered unhandled OperatorExpression operator {0}", operatorExpression.Operator());
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
                    return new DoubleVariable(numericConstant);
                }
                else
                {
                    Variable v = GenerateVariable(right);
                    if (v != null)
                    {
                        Variable newVar = new DoubleVariable(prefixExpression.CanonicalName(), () => -v.AsDouble(), true, true, Variable.VariableType.Dependent);
                        v.RegisterNumericCallback(newVar.TriggerUpdate);
                        return newVar;
                    }
                }
            }
            else if (prefixExpression.getOperator() == CodeGen.Parser.LuaToken.NOT)
            {
                Variable v = GenerateVariable(right);
                if (v != null)
                {
                    Variable newVar = new BooleanVariable(prefixExpression.CanonicalName(), () => !v.AsBool(), true, true, Variable.VariableType.Dependent);
                    v.RegisterNumericCallback(newVar.TriggerUpdate);
                    return newVar;
                }

            }

            return null;
        }

        /// <summary>
        /// Generate the appropriate Variable for a 0-parameter method.
        /// </summary>
        /// <returns></returns>
        private Variable Generate0ParmCallVariable(string canonical, object tableInstance, MethodInfo method, bool cacheable, bool mutable, Type methodReturn)
        {
            if (methodReturn == typeof(string))
            {
                Func<object, string> dm = DynamicMethodFactory.CreateFunc<object, string>(method);
                if (!mutable)
                {
                    return new StringVariable(dm(tableInstance));
                }
                else
                {
                    return new StringVariable(canonical, () => dm(tableInstance), cacheable, mutable, Variable.VariableType.Func);
                }
            }
            else if (methodReturn == typeof(double))
            {
                Func<object, double> dm = DynamicMethodFactory.CreateFunc<object, double>(method);
                if (!mutable)
                {
                    return new DoubleVariable(dm(tableInstance));
                }
                else
                {
                    return new DoubleVariable(canonical, () => dm(tableInstance), cacheable, mutable, Variable.VariableType.Func);
                }
            }
            else if (methodReturn == typeof(bool))
            {
                Func<object, bool> dm = DynamicMethodFactory.CreateFunc<object, bool>(method);
                if (!mutable)
                {
                    return new BooleanVariable(dm(tableInstance));
                }
                else
                {
                    return new BooleanVariable(canonical, () => dm(tableInstance), cacheable, mutable, Variable.VariableType.Func);
                }
            }
            else
            {
                Func<object, object> dm = DynamicMethodFactory.CreateFunc<object, object>(method);
                return new GenericVariable(canonical, () => dm(tableInstance), cacheable, mutable, Variable.VariableType.Func);
            }
        }

        /// <summary>
        /// Generate the appropriate Variable for a 1-parameter method.
        /// </summary>
        /// <returns></returns>
        private Variable Generate1ParmCallVariable(string canonical, object tableInstance, MethodInfo method, bool cacheable, bool mutable, bool persistent, bool dependent, ParameterInfo[] methodParams, Variable[] parms, Type methodReturn)
        {
            if (mutable == false)
            {
                Utility.LogWarning(this, "Variable {0} has 'mutable' set to false.  This is probably in error.", canonical);
            }

            if (methodParams[0].ParameterType == typeof(double))
            {
                Variable newVar;
                if (methodReturn == typeof(double))
                {
                    Func<object, double, double> dm = DynamicMethodFactory.CreateDynFunc<object, double, double>(method);
                    newVar = new DoubleVariable(canonical, () => dm(tableInstance, parms[0].AsDouble()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                }
                else if (methodReturn == typeof(string))
                {
                    Func<object, double, string> dm = DynamicMethodFactory.CreateDynFunc<object, double, string>(method);
                    newVar = new StringVariable(canonical, () => dm(tableInstance, parms[0].AsDouble()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                }
                else
                {
                    Func<object, double, object> dm = DynamicMethodFactory.CreateDynFunc<object, double, object>(method);
                    newVar = new GenericVariable(canonical, () => dm(tableInstance, parms[0].AsDouble()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                }

                if (dependent)
                {
                    parms[0].RegisterNumericCallback(newVar.TriggerUpdate);
                }
                return newVar;
            }
            else if (methodParams[0].ParameterType == typeof(string))
            {
                if (persistent)
                {
                    dependent = false;
                    if (parms[0].variableType == Variable.VariableType.Constant)
                    {
                        dependent = true;
                    }
                }

                Variable newVar;
                if (methodReturn == typeof(double))
                {
                    Func<object, string, double> dm = DynamicMethodFactory.CreateDynFunc<object, string, double>(method);
                    newVar = new DoubleVariable(canonical, () => dm(tableInstance, parms[0].AsString()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                }
                else
                {
                    Func<object, string, object> dm = DynamicMethodFactory.CreateDynFunc<object, string, object>(method);
                    newVar = new GenericVariable(canonical, () => dm(tableInstance, parms[0].AsString()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                }
                if (dependent)
                {
                    if (persistent)
                    {
                        RegisterPersistentNotice(parms[0].name, newVar.TriggerUpdate);
                    }
                    else
                    {
                        parms[0].RegisterNumericCallback(newVar.TriggerUpdate);
                    }
                }
                return newVar;
            }
            else if (methodParams[0].ParameterType == typeof(bool))
            {
                Variable newVar;
                if (methodReturn == typeof(double))
                {
                    Func<object, bool, double> dm = DynamicMethodFactory.CreateDynFunc<object, bool, double>(method);
                    newVar = new DoubleVariable(canonical, () => dm(tableInstance, parms[0].AsBool()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                }
                else
                {
                    Func<object, bool, object> dm = DynamicMethodFactory.CreateDynFunc<object, bool, object>(method);
                    newVar = new GenericVariable(canonical, () => dm(tableInstance, parms[0].AsBool()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                }
                if (dependent)
                {
                    parms[0].RegisterNumericCallback(newVar.TriggerUpdate);
                }
                return newVar;
            }
            else if (methodParams[0].ParameterType == typeof(object))
            {
                if (methodReturn == typeof(double))
                {
                    Func<object, object, double> dm = DynamicMethodFactory.CreateDynFunc<object, object, double>(method);
                    return new DoubleVariable(canonical, () => dm(tableInstance, parms[0].AsObject()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                }
                else if (methodReturn == typeof(string))
                {
                    Func<object, object, string> dm = DynamicMethodFactory.CreateDynFunc<object, object, string>(method);
                    return new StringVariable(canonical, () => dm(tableInstance, parms[0].AsObject()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                }
                else
                {
                    Func<object, object, object> dm = DynamicMethodFactory.CreateDynFunc<object, object, object>(method);
                    return new GenericVariable(canonical, () => dm(tableInstance, parms[0].AsObject()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                }
            }
            else
            {
                Utility.LogWarning(this, "Generate1ParmCallVariable(): Don't know how to optimize variable for {0}, with parameter {1}.  Falling back to Lua.", canonical, methodParams[0].ParameterType);
                return null;
            }
        }

        /// <summary>
        /// Generate the appropriate Variable for a 2-parameter method.
        /// </summary>
        /// <returns></returns>
        private Variable Generate2ParmCallVariable(string canonical, object tableInstance, MethodInfo method, bool cacheable, bool mutable, bool persistent, bool dependent, ParameterInfo[] methodParams, Variable[] parms, Type methodReturn)
        {
            if (mutable == false)
            {
                Utility.LogWarning(this, "Variable {0} has 'mutable' set to false.  This is probably in error.", canonical);
            }

            if (methodParams[0].ParameterType == typeof(double) && methodParams[1].ParameterType == typeof(double))
            {
                Variable newVar;
                if (methodReturn == typeof(double))
                {
                    Func<object, double, double, double> dm = DynamicMethodFactory.CreateFunc<object, double, double, double>(method);
                    newVar = new DoubleVariable(canonical, () => dm(tableInstance, parms[0].AsDouble(), parms[1].AsDouble()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                }
                else
                {
                    Func<object, double, double, object> dm = DynamicMethodFactory.CreateFunc<object, double, double, object>(method);
                    newVar = new GenericVariable(canonical, () => dm(tableInstance, parms[0].AsDouble(), parms[1].AsDouble()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                }
                if (dependent)
                {
                    parms[0].RegisterNumericCallback(newVar.TriggerUpdate);
                    parms[1].RegisterNumericCallback(newVar.TriggerUpdate);
                }
                return newVar;
            }
            else if (methodParams[0].ParameterType == typeof(bool) && methodParams[1].ParameterType == typeof(double))
            {
                if (methodReturn != typeof(object))
                {
                    Utility.LogWarning(this, "(bool, double) -> {0} could be optimized for {1}", methodReturn.ToString(), canonical);
                }
                Func<object, bool, double, object> dm = DynamicMethodFactory.CreateFunc<object, bool, double, object>(method);
                Variable newVar = new GenericVariable(canonical, () => dm(tableInstance, parms[0].AsBool(), parms[1].AsDouble()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                if (dependent)
                {
                    parms[0].RegisterNumericCallback(newVar.TriggerUpdate);
                    parms[1].RegisterNumericCallback(newVar.TriggerUpdate);
                }
                return newVar;
            }
            else if (methodParams[0].ParameterType == typeof(double) && methodParams[1].ParameterType == typeof(bool))
            {
                if (methodReturn != typeof(object))
                {
                    Utility.LogWarning(this, "(double, bool) -> {0} could be optimized for {1}", methodReturn.ToString(), canonical);
                }
                Func<object, double, bool, object> dm = DynamicMethodFactory.CreateFunc<object, double, bool, object>(method);
                Variable newVar = new GenericVariable(canonical, () => dm(tableInstance, parms[0].AsDouble(), parms[1].AsBool()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                if (dependent)
                {
                    parms[0].RegisterNumericCallback(newVar.TriggerUpdate);
                    parms[1].RegisterNumericCallback(newVar.TriggerUpdate);
                }
                return newVar;
            }
            else if (methodParams[0].ParameterType == typeof(object) && methodParams[1].ParameterType == typeof(double))
            {
                Variable newVar;
                if (methodReturn == typeof(double))
                {
                    Func<object, object, double, double> dm = DynamicMethodFactory.CreateFunc<object, object, double, double>(method);
                    newVar = new DoubleVariable(canonical, () => dm(tableInstance, parms[0].AsObject(), parms[1].AsDouble()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                }
                else
                {
                    Func<object, object, double, object> dm = DynamicMethodFactory.CreateFunc<object, object, double, object>(method);
                    newVar = new GenericVariable(canonical, () => dm(tableInstance, parms[0].AsObject(), parms[1].AsDouble()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                }
                if (dependent)
                {
                    parms[0].RegisterNumericCallback(newVar.TriggerUpdate);
                    parms[1].RegisterNumericCallback(newVar.TriggerUpdate);
                }
                return newVar;
            }
            else
            {
                Utility.LogWarning(this, "Generate2ParmCallVariable(): Don't know how to optimize variable for {0}, with parameters {1} and {2}.  Falling back to Lua.", canonical, methodParams[0].ParameterType, methodParams[1].ParameterType);
            }

            return null;
        }

        /// <summary>
        /// Generate the appropriate Variable for a 3-parameter method.
        /// </summary>
        /// <returns></returns>
        private Variable Generate3ParmCallVariable(string canonical, object tableInstance, MethodInfo method, bool cacheable, bool mutable, bool persistent, bool dependent, ParameterInfo[] methodParams, Variable[] parms, Type methodReturn)
        {
            if (mutable == false)
            {
                Utility.LogWarning(this, "Variable {0} has 'mutable' set to false.  This is probably in error.", canonical);
            }

            if (methodParams[0].ParameterType == typeof(double) && methodParams[1].ParameterType == typeof(double) && methodParams[2].ParameterType == typeof(double))
            {
                Variable newVar;
                if (methodReturn == typeof(double))
                {
                    Func<object, double, double, double, double> dm = DynamicMethodFactory.CreateFunc<object, double, double, double, double>(method);
                    newVar = new DoubleVariable(canonical, () => dm(tableInstance, parms[0].AsDouble(), parms[1].AsDouble(), parms[2].AsDouble()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                }
                else if (methodReturn == typeof(string))
                {
                    Func<object, double, double, double, string> dm = DynamicMethodFactory.CreateFunc<object, double, double, double, string>(method);
                    newVar = new StringVariable(canonical, () => dm(tableInstance, parms[0].AsDouble(), parms[1].AsDouble(), parms[2].AsDouble()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                }
                else
                {
                    if (methodReturn != typeof(object))
                    {
                        Utility.LogWarning(this, "(double, double, double) -> {0} could be optimized for {1}", methodReturn.ToString(), canonical);
                    }
                    Func<object, double, double, double, object> dm = DynamicMethodFactory.CreateFunc<object, double, double, double, object>(method);
                    newVar = new GenericVariable(canonical, () => dm(tableInstance, parms[0].AsDouble(), parms[1].AsDouble(), parms[2].AsDouble()), cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                }

                if (dependent)
                {
                    parms[0].RegisterNumericCallback(newVar.TriggerUpdate);
                    parms[1].RegisterNumericCallback(newVar.TriggerUpdate);
                    parms[2].RegisterNumericCallback(newVar.TriggerUpdate);
                }

                return newVar;
            }
            else if (methodParams[0].ParameterType == typeof(string) && methodParams[1].ParameterType == typeof(double) && methodParams[2].ParameterType == typeof(double))
            {
                Variable newVar;
                if (method.ReturnType == typeof(string))
                {
                    Func<object, string, double, double, string> dm = DynamicMethodFactory.CreateFunc<object, string, double, double, string>(method);
                    newVar = new StringVariable(canonical, () => dm(tableInstance, parms[0].AsString(), parms[1].AsDouble(), parms[2].AsDouble()), cacheable, mutable, Variable.VariableType.Func);
                }
                else
                {
                    if (methodReturn != typeof(object))
                    {
                        Utility.LogWarning(this, "(string, double, double) -> {0} could be optimized for {1}", methodReturn.ToString(), canonical);
                    }
                    Func<object, string, double, double, object> dm = DynamicMethodFactory.CreateFunc<object, string, double, double, object>(method);
                    newVar = new GenericVariable(canonical, () => dm(tableInstance, parms[0].AsString(), parms[1].AsDouble(), parms[2].AsDouble()), cacheable, mutable, Variable.VariableType.Func);
                }
                return newVar;
            }
            else if (methodParams[0].ParameterType == typeof(object) && methodParams[1].ParameterType == typeof(double) && methodParams[2].ParameterType == typeof(double))
            {
                Variable newVar;
                if (method.ReturnType == typeof(double))
                {
                    Func<object, object, double, double, double> dm = DynamicMethodFactory.CreateFunc<object, object, double, double, double>(method);
                    newVar = new DoubleVariable(canonical, () => dm(tableInstance, parms[0].AsObject(), parms[1].AsDouble(), parms[2].AsDouble()), cacheable, mutable, Variable.VariableType.Func);
                }
                else if (method.ReturnType == typeof(string))
                {
                    Func<object, object, double, double, string> dm = DynamicMethodFactory.CreateFunc<object, object, double, double, string>(method);
                    newVar = new StringVariable(canonical, () => dm(tableInstance, parms[0].AsObject(), parms[1].AsDouble(), parms[2].AsDouble()), cacheable, mutable, Variable.VariableType.Func);
                }
                else
                {
                    if (methodReturn != typeof(object))
                    {
                        Utility.LogWarning(this, "(object, double, double) -> {0} could be optimized for {1}", methodReturn.ToString(), canonical);
                    }
                    Func<object, object, double, double, object> dm = DynamicMethodFactory.CreateFunc<object, object, double, double, object>(method);
                    newVar = new GenericVariable(canonical, () => dm(tableInstance, parms[0].AsObject(), parms[1].AsDouble(), parms[2].AsDouble()), cacheable, mutable, Variable.VariableType.Func);
                }
                return newVar;
            }
            else if (methodParams[0].ParameterType == typeof(bool) && methodParams[1].ParameterType == typeof(object) && methodParams[2].ParameterType == typeof(object))
            {
                if (methodReturn != typeof(object))
                {
                    Utility.LogWarning(this, "(bool, double, double) -> {0} could be optimized for {1}", methodReturn.ToString(), canonical);
                }
                Func<object, bool, object, object, object> dm = DynamicMethodFactory.CreateFunc<object, bool, object, object, object>(method);
                return new GenericVariable(canonical, () => dm(tableInstance, parms[0].AsBool(), parms[1].AsObject(), parms[2].AsObject()), cacheable, mutable, Variable.VariableType.Func);
            }
            else
            {
                Utility.LogWarning(this, "Generate3ParmCallVariable(): Don't know how to optimize variable for {0}, with parameters {1}, {2}, and {3}.  Falling back to Lua.", canonical, methodParams[0].ParameterType, methodParams[1].ParameterType, methodParams[2].ParameterType);
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
            for (int i = 0; i < numArgs; ++i)
            {
                CodeGen.Expression exp = callExpression.Arg(i);
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
                    parameters[i] = parms[i].AsObject().GetType();
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
                        return Generate0ParmCallVariable(canonical, tableInstance, method, cacheable, mutable, method.ReturnType);
                    }
                    else if (numArgs == 1)
                    {
                        return Generate1ParmCallVariable(canonical, tableInstance, method, cacheable, mutable, persistent, dependent, methodParams, parms, method.ReturnType);
                    }
                    else if (numArgs == 2)
                    {
                        return Generate2ParmCallVariable(canonical, tableInstance, method, cacheable, mutable, persistent, dependent, methodParams, parms, method.ReturnType);
                    }
                    else if (numArgs == 3)
                    {
                        return Generate3ParmCallVariable(canonical, tableInstance, method, cacheable, mutable, persistent, dependent, methodParams, parms, method.ReturnType);
                    }
                    else
                    {
                        // Support any arbitrary number of arguments.

                        // Create the array here, so it's not a temporary allocation every time this variable is
                        // evaluated.
                        object[] paramList = new object[numArgs];

                        Variable newVar;
                        if (method.ReturnType == typeof(double))
                        {
                            DynamicMethodDelegate<double> dm = DynamicMethodFactory.CreateFunc<double>(method);
                            newVar = new DoubleVariable(canonical, () =>
                            {
                                for (int i = 0; i < numArgs; ++i)
                                {
                                    paramList[i] = parms[i].AsObject();
                                }
                                return dm(tableInstance, paramList);
                            }
                                , cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                        }
                        else if (method.ReturnType == typeof(string))
                        {
                            DynamicMethodDelegate<string> dm = DynamicMethodFactory.CreateFunc<string>(method);
                            newVar = new StringVariable(canonical, () =>
                            {
                                for (int i = 0; i < numArgs; ++i)
                                {
                                    paramList[i] = parms[i].AsObject();
                                }
                                return dm(tableInstance, paramList);
                            }
                                , cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                        }
                        else
                        {
                            if (method.ReturnType != typeof(object))
                            {
                                Utility.LogMessage(this, "(Multi-parameter) -> {0} could be optimized for {1}", method.ReturnType.ToString(), canonical);
                            }

                            DynamicMethodDelegate<object> dm = DynamicMethodFactory.CreateFunc<object>(method);
                            newVar = new GenericVariable(canonical, () =>
                            {
                                for (int i = 0; i < numArgs; ++i)
                                {
                                    paramList[i] = parms[i].AsObject();
                                }
                                return dm(tableInstance, paramList);
                            }
                            , cacheable, mutable, (dependent) ? Variable.VariableType.Dependent : Variable.VariableType.Func);
                        }
                        if (dependent)
                        {
                            for (int i = 0; i < numArgs; ++i)
                            {
                                parms[i].RegisterNumericCallback(newVar.TriggerUpdate);
                            }
                        }
                        return newVar;
                    }
                }

                return TryEvaluateLuaTable(callExpression.Function() as CodeGen.DotOperatorExpression, parms);
            }
            else
            {
                // I think only a Lua script will hit here.

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
                            return MakeLuaVariable(canonical, parms, closure);
                        }
                    }
                    catch
                    {
                        // No-op.  Soak the exception and fall back below.
                    }
                }

                // Fall back to evaluating the text as a Lua snippet every FixedUpdate.
                DynValue luaEvaluator = script.LoadString("return " + name);
                Variable v = new GenericVariable(canonical, () => script.Call(luaEvaluator).ToObject(), true, true, Variable.VariableType.LuaScript);

                Utility.LogMessage(this, "Did not evaluate {0} - fell back to script evaluation.", canonical);
                return v;
            }
        }

        /// <summary>
        /// Create a lambda to wrap a DynValue closure (function).
        /// </summary>
        /// <param name="canonical"></param>
        /// <param name="parms"></param>
        /// <param name="closure"></param>
        /// <returns></returns>
        private Variable MakeLuaVariable(string canonical, Variable[] parms, DynValue closure)
        {
            Variable luaVariable = null;
            if (parms.Length == 0)
            {
                DynValue returnValue = script.Call(closure);
                if (returnValue.Type == DataType.Number)
                {
                    luaVariable = new DoubleVariable(canonical, () =>
                    {
                        return script.Call(closure).Number;
                    }, true, true, Variable.VariableType.LuaScript);
                }
                else if (returnValue.Type == DataType.String)
                {
                    luaVariable = new StringVariable(canonical, () =>
                    {
                        return script.Call(closure).String;
                    }, true, true, Variable.VariableType.LuaScript);
                }
                else
                {
                    luaVariable = new GenericVariable(canonical, () =>
                    {
                        return script.Call(closure).ToObject();
                    }, true, true, Variable.VariableType.LuaScript);
                }
            }
            else
            {
                DynValue[] callParams = new DynValue[parms.Length];
                // Do an early call to determine the return type.
                for (int i = 0; i < parms.Length; ++i)
                {
                    callParams[i] = parms[i].AsDynValue();
                }
                DynValue returnValue = script.Call(closure, callParams);

                if (returnValue.Type == DataType.Number)
                {
                    luaVariable = new DoubleVariable(canonical, () =>
                    {
                        for (int i = 0; i < parms.Length; ++i)
                        {
                            callParams[i] = parms[i].AsDynValue();
                        }
                        return script.Call(closure, callParams).Number;
                    }, true, true, Variable.VariableType.LuaScript);
                }
                else if (returnValue.Type == DataType.String)
                {
                    luaVariable = new StringVariable(canonical, () =>
                    {
                        for (int i = 0; i < parms.Length; ++i)
                        {
                            callParams[i] = parms[i].AsDynValue();
                        }
                        return script.Call(closure, callParams).String;
                    }, true, true, Variable.VariableType.LuaScript);
                }
                else
                {
                    luaVariable = new GenericVariable(canonical, () =>
                    {
                        for (int i = 0; i < parms.Length; ++i)
                        {
                            callParams[i] = parms[i].AsDynValue();
                        }
                        return script.Call(closure, callParams).ToObject();
                    }, true, true, Variable.VariableType.LuaScript);
                }
            }
            return luaVariable;
        }

        /// <summary>
        /// Attempt to evaluate a dot expression as a Lua table.function pair
        /// </summary>
        /// <param name="dotOperatorExpression">The dot operator we are evaluating.</param>
        /// <param name="parameters">The parameter list</param>
        /// <returns>A Variable on success, null otherwise.</returns>
        private Variable TryEvaluateLuaTable(CodeGen.DotOperatorExpression dotOperatorExpression, Variable[] parameters)
        {
            try
            {
                CodeGen.NameExpression tableName = dotOperatorExpression.TableName() as CodeGen.NameExpression;
                CodeGen.NameExpression methodName = dotOperatorExpression.MethodName() as CodeGen.NameExpression;

                DynValue tableEntry = script.Globals.Get(tableName.getName());
                if (tableEntry.Type == DataType.Table)
                {
                    Table table = tableEntry.Table;
                    DynValue methodEntry = table.Get(methodName.getName());

                    if (methodEntry.Type == DataType.Function || methodEntry.Type == DataType.ClrFunction)
                    {
                        Variable v = MakeLuaVariable(dotOperatorExpression.CanonicalName(), parameters, methodEntry);
                        if (v != null)
                        {
                            return v;
                        }
                        else
                        {
                            Utility.LogWarning(this, "Did not create {0}.{1} Lua variable", tableName.getName(), methodName.getName());
                        }
                    }
                    else
                    {
                        Utility.LogWarning(this, "Found Lua table \"{0}\", but no method \"{1}\".", tableName.getName(), methodName.getName());
                    }
                }
                else
                {
                    Utility.LogWarning(this, "No table named \"{0}\" is registered in the variables map, and it is not a Lua global table.  Something may need updated.", tableName.getName());
                }
            }
            catch
            {
                Utility.LogError(this, "TryEvaluateLuaTable threw an exception - falling back");
            }

            return null;
        }

        /// <summary>
        /// Dot Operator evaluation for the case of a table.method pair - does not handle
        /// conventional Lua tables.  It's only for the case of a reflected object.method,
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
            }
        }

    }
}
