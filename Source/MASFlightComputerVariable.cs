//#define VERBOSE_LEXER
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
using System.Reflection;
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

        private int luaVariableCount = 0;
        private int nativeVariableCount = 0;
        private int constantVariableCount = 0;
        private int skippedNativeVars = 0;

        private static Dictionary<string, int> LexerTokens = new Dictionary<string, int>
        {
            {".", 1},  // dot operator -> identify table -dot- method
            {"\"", 2}, // quotation mark -> reassemble strings
            {"-", 3},  // dash -> might be 'minus', might be 'unary negation'
            {"(", 16}, // open paren -> function parameter list entry
            {")", 17}, // close paren -> function parameter list exit
            {",", 18}, // comma operator -> function list separator
            {"*", 32}, // multiplication operator
            {"/", 33}, // division operator
            {"+", 34}, // addition operator
        };

        /// <summary>
        /// Do the tokens hold the form 'str' + '.' + 'str' + '(' + * + ')'?
        /// </summary>
        /// <param name="tokens"></param>
        /// <returns></returns>
        private static bool NativeMethod(Token[] tokens, int firstToken, int lastToken)
        {
            // Note: tokens length is not verified - that's the responsibility
            // of the caller
            return (tokens[firstToken + 0].Type == TokenType.Identifier &&
                tokens[firstToken + 1].Type == TokenType.Symbol && tokens[firstToken + 1].Id == 1 &&
                tokens[firstToken + 2].Type == TokenType.Identifier &&
                tokens[firstToken + 3].Type == TokenType.Symbol && tokens[firstToken + 3].Id == 16 &&
                tokens[lastToken].Type == TokenType.Symbol && tokens[lastToken].Id == 17);
        }

        /// <summary>
        /// Get the named Variable (for direct access)
        /// </summary>
        /// <param name="variableName"></param>
        /// <returns></returns>
        internal Variable GetVariable(string variableName, InternalProp prop)
        {
            variableName = ConditionVariableName(variableName, prop);
            if (variableName.Length < 1)
            {
                Utility.ComplainLoudly("GetVariable with empty variableName");
                throw new ArgumentException("[MASFlightComputer] Trying to GetVariable with empty variableName");
            }

            // Lexer experiment
            Token[] tokens = new Token[0];
            try
            {
                LexerSettings settings = new LexerSettings();
                settings.Symbols = LexerTokens;
                Lexer lex = new Lexer(variableName.Trim(), settings);
                List<Token> tokenList = new List<Token>();

#if VERBOSE_LEXER
                Utility.LogMessage(this, "Parsing string \"{0}\":", variableName);
#endif
                // Reassemble strings.
                // Convert '-' + number back to '-number'. <- as a second pass?
                // 
                bool inQuote = false;
                int startPosition = 0;
                StringBuilder sb = new StringBuilder();
                foreach (var tok in lex)
                {
#if VERBOSE_LEXER
                    Utility.LogMessage(this, " -{0} / {2} = {1}", tok.Type, tok.Value, tok.Id);
#endif
                    if (inQuote)
                    {
                        sb.Append(tok.Text);
                        // Close quote
                        if (tok.Type == TokenType.Symbol && tok.Id == 2)
                        {
                            tokenList.Add(new Token(TokenType.QuotedString, sb.ToString(), sb.ToString(), 0, startPosition, tok.EndPosition, tok.LineBegin, tok.LineNumber, tok.EndLineBegin, tok.EndLineNumber));
                            inQuote = false;
                        }
                    }
                    else
                    {
                        // Open quote
                        if (tok.Type == TokenType.Symbol && tok.Id == 2)
                        {
                            inQuote = true;
                            startPosition = tok.StartPosition;
                            sb.Remove(0, sb.Length);
                            sb.Append(tok.Text);
                        }
                        else if (tok.Type != TokenType.WhiteSpace)
                        {
                            tokenList.Add(tok);
                        }
                    }
                }
                if (tokenList.Count > 0)
                {
                    // Second pass - convert '-' + number to '-number'.
                    for (int i = tokenList.Count - 1; i >= 1; --i)
                    {
                        // '-' preciding digit - convert?
                        // TODO: This shouldn't actually happen automatically.
                        if (tokenList[i].Type == TokenType.Decimal && tokenList[i - 1].Type == TokenType.Symbol && tokenList[i - 1].Id == 3)
                        {
                            Token dash = tokenList[i - 1];
                            Token num = tokenList[i];
                            Decimal newValue = (Decimal)num.Value;
                            newValue = -newValue;
                            string newString = newValue.ToString();
                            tokenList[i - 1] = new Token(TokenType.Decimal, newValue, newString, 0, dash.StartPosition, num.EndPosition, dash.LineBegin, num.LineNumber, num.EndLineBegin, num.EndLineNumber);
                            tokenList.Remove(tokenList[i]);
                        }
                    }
                    tokens = tokenList.ToArray();
#if VERBOSE_LEXER
                    Utility.LogMessage(this, "Parsed:");
                    for (int i = 0; i < tokens.Length; ++i)
                    {
                        Utility.LogMessage(this, " -{0} / {2} = {1}", tokens[i].Type, tokens[i].Value, tokens[i].Id);
                    }
#endif
                }
            }
            catch (Exception e)
            {
                Utility.LogErrorMessage(this, "Oops - {0}", e);
            }

            int tokenCount = tokens.Length;
            if (tokenCount == 0)
            {
                throw new ArgumentNullException("Parsing variable " + variableName + " went badly");
            }

            // TODO: Convert variableName to canonicalName
            // TODO: Look up variableName -> canonicalName in dictionary
            // TODO: Check if canonicalName is in the variable dict.

            Variable v = null;
            if (variables.ContainsKey(variableName))
            {
                v = variables[variableName];
            }
            else
            {
                if (tokenCount == 1)
                {
                    if (tokens[0].Type == TokenType.Decimal)
                    {
                        Utility.LogMessage(this, "Adding parsed numeric constant \"{0}\"", tokens[0].Text);
                        v = new Variable((double)(Decimal)tokens[0].Value);
                        constantVariableCount++;
                    }
                    else if (tokens[0].Type == TokenType.QuotedString)
                    {
                        Utility.LogMessage(this, "Adding parsed string constant {0}", tokens[0].Text);
                        v = new Variable(tokens[0].Text);
                        constantVariableCount++;
                    }
                }
                else if (tokenCount == 5)
                {
                    // Try the very simple case of a direct call
                    if (NativeMethod(tokens, 0, 4))
                    {
                        object objRef = null;
                        MethodInfo method = null;
                        ++skippedNativeVars;
                        try
                        {
                            switch (tokens[0].Text)
                            {
                                case "chatterer":
                                    objRef = chattererProxy;
                                    method = typeof(MASIChatterer).GetMethod(tokens[2].Text);
                                    break;
                                case "far":
                                    objRef = farProxy;
                                    method = typeof(MASIFAR).GetMethod(tokens[2].Text);
                                    break;
                                case "fc":
                                    objRef = fcProxy;
                                    method = typeof(MASFlightComputerProxy).GetMethod(tokens[2].Text);
                                    break;
                                case "mechjeb":
                                    objRef = mjProxy;
                                    method = typeof(MASIMechJeb).GetMethod(tokens[2].Text);
                                    break;
                                case "realchute":
                                    objRef = realChuteProxy;
                                    method = typeof(MASIRealChute).GetMethod(tokens[2].Text);
                                    break;
                            }

                            if (method != null)
                            {
                                DynamicMethod<object> dm = DynamicMethodFactory.CreateFunc<object>(method);
                                if (dm != null)
                                {
                                    v = new Variable(variableName, () => { return dm(objRef); });
                                    ++nativeVariableCount;
                                    --skippedNativeVars;
                                    Utility.LogMessage(this, "Added native variable \"{0}\"", variableName);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Utility.LogErrorMessage(this, "Native parsing error for \"{0}\"", variableName);
                            Utility.LogErrorMessage(this, e.ToString());
                        }
                    }
                }

                if (v == null)
                {
                    // If we couldn't find a way to optimize the value, fall
                    // back to interpreted Lua script.
                    v = new Variable(variableName, script);
                    luaVariableCount++;
                }
                variables.Add(variableName, v);
                if (v.mutable)
                {
                    mutableVariablesList.Add(v);
                    mutableVariablesChanged = true;
                }
                Utility.LogMessage(this, "Adding new variable '{0}'", variableName);
            }

            return v;
        }

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
            public bool mutable
            {
                get
                {
                    return variableType != VariableType.Constant;
                }
            }
            public readonly bool valid;
            internal event Action<double> numericCallbacks;
            internal event Action changeCallbacks;
            private Func<object> nativeEvaluator;
            private DynValue luaEvaluator;
            private DynValue luaValue;
            private object rawObject;
            private string stringValue;
            private double doubleValue;
            private double safeValue;
            private bool isString;
            private readonly VariableType variableType = VariableType.Unknown;

            /// <summary>
            /// How do we evaluate this variable?
            /// </summary>
            public enum VariableType
            {
                Unknown,
                LuaScript,
                Constant,
                Func,
            };

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
                //this.luaValue = DynValue.NewNumber(value);
                this.isString = false;
                this.variableType = VariableType.Constant;
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
                //this.luaValue = DynValue.NewString(value);
                this.isString = true;
                this.variableType = VariableType.Constant;
            }

            /// <summary>
            /// Construct a dynamic native evaluator.
            /// </summary>
            /// <param name="name"></param>
            /// <param name="nativeEvaluator"></param>
            /// <param name="mutable"></param>
            public Variable(string name, Func<object> nativeEvaluator)
            {
                this.name = name;

                this.valid = true;
                this.nativeEvaluator = nativeEvaluator;
                this.rawObject = nativeEvaluator();
                this.variableType = VariableType.Func;

                ProcessObject(this.rawObject);
            }

            /// <summary>
            /// Construct a dynamic Lua Variable.
            /// </summary>
            /// <param name="name"></param>
            /// <param name="script"></param>
            public Variable(string name, Script script)
            {
                this.name = name;

                double value;
                if (double.TryParse(name, out value))
                {
                    this.valid = true;
                    this.stringValue = value.ToString();
                    this.doubleValue = value;
                    this.safeValue = value;
                    this.rawObject = value;
                    //this.luaValue = DynValue.NewNumber(value);
                    this.isString = false;
                    this.variableType = VariableType.Constant;
                }
                else
                {
                    // TODO: Can I find a way to parse or analyze the evaluator
                    // and set up a direct call (bypassing Lua) for very simple
                    // queries?
                    // TODO: MoonSharp "hardwiring" - does it help performance?
                    luaValue = null;
                    try
                    {
                        luaEvaluator = script.LoadString("return " + name);
                        luaValue = script.Call(luaEvaluator);
                        this.valid = true;
                    }
                    catch (Exception e)
                    {
                        Utility.ComplainLoudly("Error creating variable " + name);
                        Utility.LogErrorMessage(this, "Unknown variable '{0}':", name);
                        Utility.LogErrorMessage(this, e.ToString());
                        luaValue = null;
                        this.valid = false;
                    }

                    if (luaValue != null)
                    {
                        if (luaValue.IsNil() || luaValue.IsVoid())
                        {
                            // Not a valid evaluable
                            this.valid = false;
                            this.doubleValue = double.NaN;
                            this.stringValue = name;
                            this.rawObject = name;
                            this.valid = false;
                            this.isString = true;
                            this.variableType = VariableType.LuaScript;
                        }
                        else
                        {
                            DataType type = luaValue.Type;
                            if (type == DataType.Number)
                            {
                                this.doubleValue = luaValue.CastToNumber() ?? double.NaN;
                                this.safeValue = this.doubleValue;
                                this.stringValue = this.doubleValue.ToString();
                                this.isString = false;
                                this.rawObject = this.doubleValue;
                            }
                            else if(type == DataType.String)
                            {
                                this.doubleValue = double.NaN;
                                this.safeValue = 0.0;
                                this.stringValue = luaValue.String;
                                this.isString = true;
                                this.rawObject = this.stringValue;
                            }
                            else
                            {
                                this.doubleValue = double.NaN;
                                this.safeValue = 0.0;
                                this.stringValue = luaValue.CastToString();
                                this.isString = false;
                                this.rawObject = luaValue.ToObject();
                            }
                            /*
                            stringValue = luaValue.CastToString();
                            doubleValue = luaValue.CastToNumber() ?? double.NaN;
                            this.valid = true;

                            if (double.IsNaN(this.doubleValue) || double.IsInfinity(this.doubleValue))
                            {
                                this.safeValue = 0.0;
                                this.rawObject = stringValue;
                                this.isString = true;
                            }
                            else
                            {
                                this.safeValue = doubleValue;
                                this.rawObject = doubleValue;
                                this.isString = false;
                            }
                            */
                            this.valid = true;
                            this.variableType = VariableType.LuaScript;
                        }
                    }
                    else
                    {
                        this.doubleValue = double.NaN;
                        this.stringValue = name;
                        this.valid = false;
                        this.isString = true;
                        this.variableType = VariableType.Constant;
                    }
                }
            }

            /// <summary>
            /// Are the contents a string?
            /// </summary>
            /// <returns></returns>
            public bool IsString()
            {
                return this.isString;
            }

            /// <summary>
            /// Return the raw DynValue for specialized processing.
            /// 
            /// TODO: What about when Func can be put here?
            /// </summary>
            /// <returns></returns>
            //public DynValue RawValue()
            //{
            //    return luaValue;
            //}
            public object RawValue()
            {
                return rawObject;
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
            /// Process and classify the raw object that comes from the native
            /// evaluator.
            /// </summary>
            /// <param name="value"></param>
            private void ProcessObject(object value)
            {
                if (value is double)
                {
                    doubleValue = (double)value;
                    safeValue = doubleValue;
                    stringValue = doubleValue.ToString();
                    isString = false;
                }
                else if (value is string)
                {
                    stringValue = value as string;
                    doubleValue = double.NaN;
                    safeValue = 0.0;
                    isString = true;
                }
                else
                {
                    // TODO ...?
                    throw new NotImplementedException("ProcessObject found a non-double, non-string return type");
                }
            }

            /// <summary>
            /// Evaluate updates the variable in question by calling the code
            /// snippet using the supplied Lua script.
            /// </summary>
            /// <param name="script"></param>
            internal void Evaluate(Script script)
            {
                if (variableType == VariableType.LuaScript)
                {
                    DynValue oldDynValue = luaValue;
                    string oldString = stringValue;
                    double oldValue = safeValue;
                    try
                    {
                        luaValue = script.Call(luaEvaluator);
                        stringValue = luaValue.CastToString();
                        doubleValue = luaValue.CastToNumber() ?? double.NaN;
                    }
                    catch
                    {
                        this.doubleValue = double.NaN;
                        this.stringValue = name;
                    }

                    safeValue = (double.IsInfinity(doubleValue) || double.IsNaN(doubleValue)) ? 0.0 : doubleValue;

                    DataType type = luaValue.Type;
                    if (type == DataType.Number)
                    {
                        if (!Mathf.Approximately((float)oldValue, (float)safeValue))
                        {
                            rawObject = doubleValue;
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
                        if (oldString != stringValue)
                        {
                            rawObject = stringValue;
                            try
                            {
                                changeCallbacks.Invoke();
                            }
                            catch { }
                        }
                    }
                    else if (!oldDynValue.Equals(luaValue))
                    {
                        rawObject = luaValue.ToObject();
                        try
                        {
                            changeCallbacks.Invoke();
                        }
                        catch { }
                    }
                }
                else if (variableType == VariableType.Func)
                {
                    object value = nativeEvaluator();

                    if (!value.Equals(rawObject))
                    {
                        double oldSafeValue = safeValue;
                        rawObject = value;

                        ProcessObject(value);

                        if (!Mathf.Approximately((float)safeValue, (float)oldSafeValue))
                        {
                            try
                            {
                                numericCallbacks.Invoke(safeValue);
                            }
                            catch { }
                        }

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
}
