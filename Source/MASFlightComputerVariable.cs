//#define USE_LEXER
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

            Variable v = null;
            if (variables.ContainsKey(variableName))
            {
                v = variables[variableName];
            }
            else
            {
                v = new Variable(variableName, script);
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
            private readonly VariableType variableType = VariableType.Unknown;

            /// <summary>
            /// How do we evaluate this variable?
            /// </summary>
            public enum VariableType
            {
                Unknown,
                LuaScript,
                Constant,
            };

            private static Dictionary<string, int> LexerTokens = new Dictionary<string, int>
            {
                {"(", 1},
                {")", 2},
                {"-", 3},
                {".", 4},
                {"\"", 5}
            };

            public Variable(string name, Script script)
            {
                this.name = name;

                double value;
#if USE_LEXER
                // Lexer experiment
                Token[] tokens = new Token[0];
                try
                {
                    LexerSettings settings = new LexerSettings();
                    settings.Symbols = LexerTokens;
                    Lexer lex = new Lexer(name.Trim(), settings);
                    List<Token> tokenList = new List<Token>();

                    Utility.LogMessage(this, "Parsing string \"{0}\":", name);
                    // Reassemble strings.
                    // Convert '-' + number back to '-number'. <- as a second pass?
                    // 
                    bool inQuote = false;
                    int startPosition = 0;
                    StringBuilder sb = new StringBuilder();
                    foreach (var tok in lex)
                    {
                        Utility.LogMessage(this, " -{0} / {2} = {1}", tok.Type, tok.Value, tok.Id);
                        if (inQuote)
                        {
                            sb.Append(tok.Text);
                            if (tok.Type == TokenType.Symbol && tok.Id == 5)
                            {
                                tokenList.Add(new Token(TokenType.QuotedString, sb.ToString(), sb.ToString(), 0, startPosition, tok.EndPosition, tok.LineBegin, tok.LineNumber, tok.EndLineBegin, tok.EndLineNumber));
                                inQuote = false;
                            }
                        }
                        else
                        {
                            if (tok.Type == TokenType.Symbol && tok.Id == 5)
                            {
                                inQuote = true;
                                startPosition = tok.StartPosition;
                                sb.Remove(0, sb.Length);
                                sb.Append(tok.Text);
                            }
                            else if(tok.Type != TokenType.WhiteSpace)
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
                        Utility.LogMessage(this, "Parsed:");
                        for (int i = 0; i < tokens.Length; ++i)
                        {
                            Utility.LogMessage(this, " -{0} / {2} = {1}", tokens[i].Type, tokens[i].Value, tokens[i].Id);
                        }
                    }
                }
                catch (Exception e)
                {
                    Utility.LogErrorMessage(this, "Oops - {0}", e);
                }

                if (tokens.Length == 0)
                {
                    throw new ArgumentNullException("Parsing variable " + name + " went badly");
                }
                StringBuilder newname = new StringBuilder();
                for (int i = 0; i < tokens.Length; ++i)
                {
                    if (tokens[i].Type == TokenType.WhiteSpace)
                    {
                        newname.Append(' ');
                    }
                    else 
                    {
                        if (i > 1 && tokens[i].Type == TokenType.Identifier && tokens[i - 1].Type == TokenType.Identifier)
                        {
                            newname.Append(' ');
                        }
                        newname.Append(tokens[i].Text);
                    }
                }
                name = newname.ToString();
                this.name = name;
                Utility.LogMessage(this, "Reassembled name = {0}", name);
                // See if we can process this token list, or if we punt to Lua.

                if (tokens[0].Type == TokenType.Decimal)
                {
                    //double.TryParse(tokens[0].Text, out value);
                    this.valid = true;
                    value = (double)(Decimal)tokens[0].Value;
                    Utility.LogMessage(this, "Found constant number \"{0}\", evaluated to {1}.", tokens[0].Text, value);
                    this.stringValue = tokens[0].Text;
                    this.doubleValue = value;
                    this.safeValue = value;
                    this.value = DynValue.NewNumber(value);
                    this.isString = false;
                    this.variableType = VariableType.Constant;
                }
                else
#endif
                    if (double.TryParse(name, out value))
                    {
                        this.valid = true;
                        this.stringValue = value.ToString();
                        this.doubleValue = value;
                        this.safeValue = value;
                        this.value = DynValue.NewNumber(value);
                        this.isString = false;
                        this.variableType = VariableType.Constant;
                    }
                    else
                    {
                        // TODO: Can I find a way to parse or analyze the evaluator
                        // and set up a direct call (bypassing Lua) for very simply
                        // queries?
                        // TODO: MoonSharp "hardwiring" - does it help performance?
                        this.value = null;
                        try
                        {
                            evaluator = script.LoadString("return " + name);
                            this.value = script.Call(evaluator);
                            this.valid = true;
                        }
                        catch (Exception e)
                        {
                            Utility.ComplainLoudly("Error creating variable " + name);
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
                                this.doubleValue = double.NaN;
                                this.stringValue = name;
                                this.valid = false;
                                this.isString = true;
                                this.variableType = VariableType.LuaScript;
                            }
                            else
                            {
                                this.stringValue = this.value.CastToString();
                                this.doubleValue = this.value.CastToNumber() ?? double.NaN;
                                this.valid = true;

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
                if (variableType == VariableType.LuaScript)
                {
                    DynValue oldDynValue = value;
                    double oldValue = safeValue;
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
}
