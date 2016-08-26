//#define VERBOSE_LEXER
//#define VERBOSE_PARSING
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
        // 48 = boolean value (true / false)

        private static bool NativeNamespace(string name)
        {
            switch (name)
            {
                case "fc":
                    return true;
                case "chatterer":
                    return true;
                case "far":
                    return true;
                case "realchute":
                    return true;
                case "mechjeb":
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Do the tokens hold the form 'str' + '.' + 'str' + '(' + * + ')'?
        /// </summary>
        /// <param name="tokens"></param>
        /// <returns></returns>
        private static bool NativeMethod(Token[] tokens, int firstToken, int lastToken)
        {
            // Note: tokens length is not verified - that's the responsibility
            // of the caller
            if (tokens[firstToken + 0].Type == TokenType.Identifier &&
                tokens[firstToken + 1].Type == TokenType.Symbol && tokens[firstToken + 1].Id == 1 &&
                tokens[firstToken + 2].Type == TokenType.Identifier &&
                tokens[firstToken + 3].Type == TokenType.Symbol && tokens[firstToken + 3].Id == 16 &&
                tokens[lastToken].Type == TokenType.Symbol && tokens[lastToken].Id == 17 &&
                NativeNamespace(tokens[0].Text))
            {
                // Must do additional parsing here, in case the tokens are in the form 'fc.blah() * fc.blah2()'
                int depth = 0;
                for (int idx = firstToken + 4; idx < lastToken; ++idx)
                {
                    if (tokens[idx].Type == TokenType.Symbol && tokens[idx].Id == 16)
                    {
                        ++depth;
                    }
                    else if (tokens[idx].Type == TokenType.Symbol && tokens[idx].Id == 17)
                    {
                        --depth;
                    }

                    if (depth < 0)
                    {
                        return false;
                    }
                }
                return true;
            }

            return false;
        }

        enum VariableParameterType
        {
            UNSUPPORTED,
            CONST_BOOL,
            CONST_DOUBLE,
            CONST_STRING,
            VARIABLE
        };

        struct VariableParameter
        {
            // Type is the native type of the value for constants, or the
            // return type of the method.
            internal VariableParameterType vpType;
            internal Type valueType;
            internal bool booleanValue;
            internal double numericValue;
            internal string stringValue;
            internal Variable variableValue;
        };

        static private Dictionary<string, Type> typeMap = new Dictionary<string, Type>
        {
            {"fc", typeof(MASFlightComputerProxy)},
            {"chatterer", typeof(MASIChatterer)},
            {"far", typeof(MASIFAR)},
            {"mechjeb", typeof(MASIMechJeb)},
            {"realchute", typeof(MASIRealChute)},
        };

        /// <summary>
        /// Reassemble the name into its 'canonical' form.
        /// </summary>
        /// <param name="tokens"></param>
        /// <param name="firstIndex"></param>
        /// <param name="lastIndex"></param>
        /// <returns></returns>
        private static string MakeCanonicalName(Token[] tokens, int firstIndex, int lastIndex)
        {
            StringBuilder sb = Utility.GetStringBuilder();
            for (int index = firstIndex; index <= lastIndex; ++index)
            {
                sb.Append(tokens[index].Text);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Parse the parameter list and come up with a proposed arrangement.
        /// </summary>
        /// <param name="tokens"></param>
        /// <param name="firstIndex">First index of the parameter list (after the opening paren)</param>
        /// <param name="lastIndex">Last index of the parameter list (before the closing paren)</param>
        /// <returns></returns>
        private VariableParameter[] TryParseParameters(Token[] tokens, int firstIndex, int lastIndex)
        {
            if (firstIndex > lastIndex)
            {
                return new VariableParameter[0];
            }
            else
            {
                int numParameters = 1;
                // Count the parameters
                for (int index = firstIndex; index < lastIndex; ++index)
                {
                    if (tokens[index].Type == TokenType.Symbol && tokens[index].Id == 18)
                    {
                        ++numParameters;
                    }
                }
#if VERBOSE_PARSING
                Utility.LogMessage(this, "TryParseParameters - found {0} parameters", numParameters);
#endif
                VariableParameter[] newParms = new VariableParameter[numParameters];
                int currentParam = 0;
                int numTokens;
                for (int index = firstIndex; index <= lastIndex; ++index)
                {
                    if (tokens[index].Type == TokenType.Symbol && tokens[index].Id == 18)
                    {
                        numTokens = index - firstIndex;
#if VERBOSE_PARSING
                        Utility.LogMessage(this, "index = {0}, firstIndex = {1}, numTokens = {2}", index, firstIndex, numTokens);
#endif
                        if (numTokens == 1)
                        {
                            if (tokens[firstIndex].Type == TokenType.Number)
                            {
#if VERBOSE_PARSING
                                Utility.LogMessage(this, " Parameter {0} is number", currentParam);
#endif

                                newParms[currentParam].numericValue = (double)tokens[firstIndex].Value;
                                newParms[currentParam].valueType = typeof(double);
                                newParms[currentParam].vpType = VariableParameterType.CONST_DOUBLE;
                            }
                            else if (tokens[firstIndex].Type == TokenType.QuotedString)
                            {
#if VERBOSE_PARSING
                                Utility.LogMessage(this, " Parameter {0} is string", currentParam);
#endif

                                newParms[currentParam].stringValue = tokens[firstIndex].Value.ToString();
                                newParms[currentParam].stringValue = newParms[currentParam].stringValue.Substring(1, newParms[currentParam].stringValue.Length - 2);
                                newParms[currentParam].valueType = typeof(string);
                                newParms[currentParam].vpType = VariableParameterType.CONST_STRING;
                            }
                            else if (tokens[firstIndex].Type == TokenType.Keyword && tokens[firstIndex].Id == 48)
                            {
#if VERBOSE_PARSING
                                Utility.LogMessage(this, " Parameter {0} is bool", currentParam);
#endif

                                newParms[currentParam].booleanValue = (bool)tokens[firstIndex].Value;
                                newParms[currentParam].valueType = typeof(bool);
                                newParms[currentParam].vpType = VariableParameterType.CONST_BOOL;
                            }
                            else
                            {
                                Utility.LogErrorMessage(this, " Parameter {0} is an unexpected value", currentParam);
                            }
                        }
                        else
                        {
                            // Variable
                            string canonName = MakeCanonicalName(tokens, firstIndex, index - 1);
                            Variable v = GetVariable(canonName, null);
#if VERBOSE_PARSING
                            Utility.LogMessage(this, "Looked for child variable {0}: {1}", canonName, (v==null) ? "failed" : "succeeded");
#endif
                            if (v == null)
                            {
                                newParms = new VariableParameter[1];
                                newParms[0].vpType = VariableParameterType.UNSUPPORTED;
                                return newParms;
                            }
                            else
                            {
                                newParms[currentParam].variableValue = v;
                                newParms[currentParam].valueType = v.RawValue().GetType();
                                newParms[currentParam].vpType = VariableParameterType.VARIABLE;
                            }
                        }
                        ++currentParam;
                        firstIndex = index + 1;
                    }
                }
                numTokens = lastIndex - firstIndex + 1;
#if VERBOSE_PARSING
                Utility.LogMessage(this, "lastIndex = {0}, firstIndex = {1}, numTokens = {2}", lastIndex, firstIndex, numTokens);
#endif
                if (numTokens == 1)
                {
                    if (tokens[firstIndex].Type == TokenType.Number)
                    {
#if VERBOSE_PARSING
                        Utility.LogMessage(this, " Parameter {0} is number", currentParam);
#endif

                        newParms[currentParam].numericValue = (double)tokens[firstIndex].Value;
                        newParms[currentParam].valueType = typeof(double);
                        newParms[currentParam].vpType = VariableParameterType.CONST_DOUBLE;
                    }
                    else if (tokens[firstIndex].Type == TokenType.QuotedString)
                    {
#if VERBOSE_PARSING
                        Utility.LogMessage(this, " Parameter {0} is string", currentParam);
#endif

                        newParms[currentParam].stringValue = tokens[firstIndex].Value.ToString();
                        newParms[currentParam].stringValue = newParms[currentParam].stringValue.Substring(1, newParms[currentParam].stringValue.Length - 2);
                        newParms[currentParam].valueType = typeof(string);
                        newParms[currentParam].vpType = VariableParameterType.CONST_STRING;
                    }
                    else if (tokens[firstIndex].Type == TokenType.Keyword && tokens[firstIndex].Id == 48)
                    {
#if VERBOSE_PARSING
                        Utility.LogMessage(this, " Parameter {0} is bool", currentParam);
#endif

                        newParms[currentParam].booleanValue = (bool)tokens[firstIndex].Value;
                        newParms[currentParam].valueType = typeof(bool);
                        newParms[currentParam].vpType = VariableParameterType.CONST_BOOL;
                    }
                }
                else
                {
                    // Variable
                    string canonName = MakeCanonicalName(tokens, firstIndex, lastIndex);
                    Variable v = GetVariable(canonName, null);
#if VERBOSE_PARSING
                    Utility.LogMessage(this, "Looked for child variable {0}: {1}", canonName, (v == null) ? "failed" : "succeeded");
#endif
                    if (v == null)
                    {
                        newParms = new VariableParameter[1];
                        newParms[0].vpType = VariableParameterType.UNSUPPORTED;
                        return newParms;
                    }
                    else
                    {
                        newParms[currentParam].variableValue = v;
                        newParms[currentParam].valueType = v.RawValue().GetType();
                        newParms[currentParam].vpType = VariableParameterType.VARIABLE;
                    }
                }
                return newParms;
            }
        }

        /// <summary>
        /// Find a method with the supplied name within the supplied class (Type)
        /// with a parameter list that matches the types in the VariableParameter array.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private static MethodInfo FindMethod(string name, Type type, VariableParameter[] parameters)
        {
            int numParams = parameters.Length;
            MethodInfo[] methods = type.GetMethods();
            for (int i = methods.Length - 1; i >= 0; --i)
            {
                if (methods[i].Name == name)
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
                                if (methodParams[index].ParameterType != parameters[index].valueType)
                                {
                                    match = false;
                                    break;
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
        /// See if we can parse the tokens sufficiently to create a delegate
        /// that allows us to bypass the MoonSharp interpreter.  Returns
        /// null on failure.
        /// </summary>
        /// <param name="tokens"></param>
        /// <param name="lastToken"></param>
        /// <returns></returns>
        private Variable TryCreateNativeVariable(string variableName, Token[] tokens, int lastToken)
        {
            Variable newVar = null;
            if (NativeMethod(tokens, 0, lastToken))
            {
#if VERBOSE_PARSING
                Utility.LogMessage(this, "Native candidate...");
#endif
                // This looks promising...  Can we figure out how to construct a variable?
                VariableParameter[] parameters = TryParseParameters(tokens, 4, lastToken - 1);

                if (parameters.Length == 1 && parameters[0].vpType == VariableParameterType.UNSUPPORTED)
                {
#if VERBOSE_PARSING
                    Utility.LogMessage(this, "... Unsupported component in parameter list");
#endif
                    return null;
                }
                else
                {
                    MethodInfo method = FindMethod(tokens[2].Text, typeMap[tokens[0].Text], parameters);
                    if (method == null)
                    {
                        Utility.LogErrorMessage(this, "... Did not find matching method for '{0}'", tokens[2].Text);
                    }
                    else
                    {
#if VERBOSE_PARSING
                        Utility.LogMessage(this, "... Found matching method for '{0}'", tokens[2].Text);
#endif
                        object objRef = null;
                        switch (tokens[0].Text)
                        {
                            case "fc":
                                objRef = fcProxy;
                                break;
                            case "far":
                                objRef = farProxy;
                                break;
                            case "chatterer":
                                objRef = chattererProxy;
                                break;
                            case "mechjeb":
                                objRef = mjProxy;
                                break;
                            case "realchute":
                                objRef = realChuteProxy;
                                break;
                        }
                        if (objRef != null)
                        {
                            if (parameters.Length == 0)
                            {
                                // No parameters.
                                DynamicMethod<object> dm = DynamicMethodFactory.CreateFunc<object>(method);
                                if (dm != null)
                                {
                                    newVar = new Variable(variableName, () => { return dm(objRef); });
#if VERBOSE_PARSING
                                    Utility.LogMessage(this, "Added native variable \"{0}\"", variableName);
#endif
                                }
                            }
                            else
                            {
                                if (parameters.Length == 1)
                                {
                                    if (parameters[0].vpType == VariableParameterType.CONST_BOOL)
                                    {
                                        DynamicMethod<object, bool> dm = DynamicMethodFactory.CreateFunc<object, bool>(method);
                                        bool bValue = parameters[0].booleanValue;
                                        newVar = new Variable(variableName, () => { return dm(objRef, bValue); });
#if VERBOSE_PARSING
                                        Utility.LogMessage(this, "Added native bool variable \"{0}\"", variableName);
#endif
                                    }
                                    else if (parameters[0].vpType == VariableParameterType.CONST_DOUBLE)
                                    {
                                        DynamicMethod<object, double> dm = DynamicMethodFactory.CreateFunc<object, double>(method);
                                        double dValue = parameters[0].numericValue;
                                        newVar = new Variable(variableName, () => { return dm(objRef, dValue); });
#if VERBOSE_PARSING
                                        Utility.LogMessage(this, "Added native double variable \"{0}\"", variableName);
#endif
                                    }
                                    else if (parameters[0].vpType == VariableParameterType.CONST_STRING)
                                    {
                                        DynamicMethod<object, string> dm = DynamicMethodFactory.CreateFunc<object, string>(method);
                                        string sValue = parameters[0].stringValue;
                                        newVar = new Variable(variableName, () => { return dm(objRef, sValue); });
#if VERBOSE_PARSING
                                        Utility.LogMessage(this, "Added native string variable \"{0}\"", variableName);
#endif
                                    }
                                    else if (parameters[0].vpType == VariableParameterType.VARIABLE)
                                    {
                                        if (parameters[0].valueType == typeof(bool))
                                        {
                                            DynamicMethod<object, bool> dm = DynamicMethodFactory.CreateFunc<object, bool>(method);
                                            Variable v = parameters[0].variableValue;
                                            newVar = new Variable(variableName, () =>
                                            {
                                                bool bValue = (bool)v.RawValue();
                                                return dm(objRef, bValue);
                                            });
#if VERBOSE_PARSING
                                            Utility.LogMessage(this, "Added variable bool variable \"{0}\"", variableName);
#endif
                                        }
                                        else if (parameters[0].valueType == typeof(double))
                                        {
                                            DynamicMethod<object, double> dm = DynamicMethodFactory.CreateFunc<object, double>(method);
                                            Variable v = parameters[0].variableValue;
                                            newVar = new Variable(variableName, () =>
                                            {
                                                return dm(objRef, v.SafeValue());
                                            });
#if VERBOSE_PARSING
                                            Utility.LogMessage(this, "Added variable double variable \"{0}\"", variableName);
#endif
                                        }
                                        else if (parameters[0].valueType == typeof(string))
                                        {
                                            DynamicMethod<object, string> dm = DynamicMethodFactory.CreateFunc<object, string>(method);
                                            Variable v = parameters[0].variableValue;
                                            newVar = new Variable(variableName, () =>
                                            {
                                                return dm(objRef, v.String());
                                            });
#if VERBOSE_PARSING
                                            Utility.LogMessage(this, "Added variable string variable \"{0}\"", variableName);
#endif
                                        }
                                    }
                                    else
                                    {
                                        Utility.LogErrorMessage(this, "Found unsupported parameter type {0} for variable {1}", parameters[0].vpType, variableName);
                                    }
                                }
                                else
                                {
                                    Utility.LogErrorMessage(this, "Found unsupported {0} parameter method for {1}", parameters.Length, variableName);
                                }
                            }
                        }
                    }
                }
            }

            return newVar;
        }

        /// <summary>
        /// Get the named Variable (for direct access).
        /// 
        /// So, that's the really short summary, but it does not tell you what
        /// I am really doing.
        /// 
        /// The MoonSharp interpreter, version 1.6.0, seems to be able to
        /// process about 90-ish variables per millisecond using the test IVA
        /// I created (Yarbrough Mk1-1A2, 228 mutable variables).  That's really
        /// poor performance - around 2.5ms per FixedUpdate, which fires every
        /// 20ms at default frequency.  In other words, about 1/8 of the FixedUpdate
        /// time budget, just for the MAS variables.  No bueno.
        /// 
        /// So, I use a Lexer object (see Lexer.cs) to parse the variable.  I
        /// then apply some processing to the parsed variable to simplify
        /// constant numbers and constant strings into immutable values.  I also
        /// look for variables that are calling MAS directly (eg, 'fc.Something()')
        /// so I can create a delegate that allows me to call them directly
        /// without the cost of a round trip through Lua.
        /// 
        /// That optimization by itself, where I can call both void methods and
        /// single-parameter constant methods (eg 'fc.GetPersistentAsNumber("SomePersistent"),
        /// moves the refresh rate above 120 updates/ms.  That's better.  The fewer
        /// calls into Lua / MoonSharp, the better.  It also accounted for about half of the
        /// total number of calls.
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
                bool inQuote = false;
                int startPosition = 0;
                StringBuilder sb = null;// new StringBuilder();
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
                            // TODO: Do I care about anything here beyond the first 4 parameters?
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
                            sb = Utility.GetStringBuilder();
                            sb.Append(tok.Text);
                        }
                        else if (tok.Type != TokenType.WhiteSpace)
                        {
                            if (tok.Type == TokenType.Identifier)
                            {
                                if (tok.Text == "true")
                                {
                                    tokenList.Add(new Token(TokenType.Keyword, true, "true", 48, 0, 0, 0, 0, 0, 0));
                                }
                                else if (tok.Text == "false")
                                {
                                    tokenList.Add(new Token(TokenType.Keyword, false, "false", 48, 0, 0, 0, 0, 0, 0));
                                }
                                else if (tok.Text == "and")
                                {
                                    tokenList.Add(new Token(TokenType.Identifier, " and ", " and ", 0, 0, 0, 0, 0, 0, 0));
                                }
                                else if (tok.Text == "or")
                                {
                                    tokenList.Add(new Token(TokenType.Identifier, " or ", " or ", 0, 0, 0, 0, 0, 0, 0));
                                }
                                else
                                {
                                    tokenList.Add(tok);
                                }
                            }
                            else if (tok.Type == TokenType.Symbol)
                            {
                                if (tok.Id == 18)
                                {
                                    tokenList.Add(new Token(TokenType.Symbol, ", ", ", ", tok.Id, 0, 0, 0, 0, 0, 0));
                                }
                                else if (tok.Id == 32)
                                {
                                    tokenList.Add(new Token(TokenType.Symbol, " * ", " * ", tok.Id, 0, 0, 0, 0, 0, 0));
                                }
                                else if (tok.Id == 33)
                                {
                                    tokenList.Add(new Token(TokenType.Symbol, " / ", " / ", tok.Id, 0, 0, 0, 0, 0, 0));
                                }
                                else if (tok.Id == 34)
                                {
                                    tokenList.Add(new Token(TokenType.Symbol, " + ", " + ", tok.Id, 0, 0, 0, 0, 0, 0));
                                }
                                else
                                {
                                    tokenList.Add(tok);
                                }
                            }
                            else if (tok.Type == TokenType.Decimal)
                            {
                                tokenList.Add(new Token(TokenType.Number, (double)(Decimal)tok.Value, tok.Text, 0, 0, 0, 0, 0, 0, 0));
                            }
                            else
                            {
                                tokenList.Add(tok);
                            }
                        }
                    }
                }
                if (tokenList.Count > 0)
                {
                    // Second pass - convert '-' + number to '-number'.
                    for (int i = tokenList.Count - 1; i >= 1; --i)
                    {
                        // '-' preceding digit - convert?
                        // TODO: This shouldn't actually happen automatically.  The dash may be a subtraction symbol.
                        if (tokenList[i].Type == TokenType.Number && tokenList[i - 1].Type == TokenType.Symbol && tokenList[i - 1].Id == 3)
                        {
                            Token dash = tokenList[i - 1];
                            Token num = tokenList[i];
                            double newValue = (double)num.Value;
                            newValue = -newValue;
                            string newString = newValue.ToString();
                            tokenList[i - 1] = new Token(TokenType.Number, newValue, newString, 0, dash.StartPosition, num.EndPosition, dash.LineBegin, num.LineNumber, num.EndLineBegin, num.EndLineNumber);
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

            string canonicalName = string.Empty;
            if (canonicalVariableName.ContainsKey(variableName))
            {
                canonicalName = canonicalVariableName[variableName];
            }
            else
            {
                canonicalName = MakeCanonicalName(tokens, 0, tokenCount - 1);
                canonicalVariableName[variableName] = canonicalName;
            }

            Variable v = null;
            if (variables.ContainsKey(canonicalName))
            {
                v = variables[canonicalName];
            }
            else
            {
                if (tokenCount == 1)
                {
                    if (tokens[0].Type == TokenType.Number)
                    {
#if VERBOSE_PARSING
                        Utility.LogMessage(this, "Adding parsed numeric constant \"{0}\"", tokens[0].Text);
#endif
                        v = new Variable((double)tokens[0].Value);
                        constantVariableCount++;
                    }
                    else if (tokens[0].Type == TokenType.QuotedString)
                    {
#if VERBOSE_PARSING
                        Utility.LogMessage(this, "Adding parsed string constant {0}", tokens[0].Text);
#endif
                        v = new Variable(tokens[0].Text);
                        constantVariableCount++;
                    }
                }
                else if (tokenCount >= 5)
                {
#if VERBOSE_PARSING
                    Utility.LogMessage(this, "Trying native variable path for '{0}'", variableName);
#endif
                    v = TryCreateNativeVariable(canonicalName, tokens, tokenCount - 1);
                    if (v != null)
                    {
                        ++nativeVariableCount;
                    }
                }

                if (v == null)
                {
                    // If we couldn't find a way to optimize the value, fall
                    // back to interpreted Lua script.
                    v = new Variable(canonicalName, script);
                    luaVariableCount++;
                }
                variables.Add(canonicalName, v);
                if (v.mutable)
                {
                    mutableVariablesList.Add(v);
                    mutableVariablesChanged = true;
                }
                Utility.LogMessage(this, "Adding new variable '{0}'", canonicalName);
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
                                this.rawObject = this.doubleValue;
                            }
                            else if (type == DataType.String)
                            {
                                this.doubleValue = double.NaN;
                                this.safeValue = 0.0;
                                this.stringValue = luaValue.String;
                                this.rawObject = this.stringValue;
                            }
                            else if (type == DataType.Boolean)
                            {
                                bool boolValue = luaValue.Boolean;
                                this.doubleValue = (boolValue) ? 1.0 : 0.0;
                                this.safeValue = doubleValue;
                                this.stringValue = value.ToString();
                                this.rawObject = boolValue;
                            }
                            else
                            {
                                this.doubleValue = double.NaN;
                                this.safeValue = 0.0;
                                this.stringValue = luaValue.CastToString();
                                this.rawObject = luaValue.ToObject();
                            }

                            this.valid = true;
                            this.variableType = VariableType.LuaScript;
                        }
                    }
                    else
                    {
                        this.doubleValue = double.NaN;
                        this.stringValue = name;
                        this.valid = false;
                        this.variableType = VariableType.Constant;
                    }
                }
            }

            /// <summary>
            /// Return the raw object for customized processing.
            /// </summary>
            /// <returns></returns>
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
                }
                else if (value is string)
                {
                    stringValue = value as string;
                    doubleValue = double.NaN;
                    safeValue = 0.0;
                }
                else if (value is bool)
                {
                    bool bValue = (bool)value;
                    safeValue = (bValue) ? 1.0 : 0.0;
                    doubleValue = double.NaN;
                    stringValue = bValue.ToString();
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
                    // TODO: Refactor this similar to the native func code path
                    // and allow for booleans.
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
