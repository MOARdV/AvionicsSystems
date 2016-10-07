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

// Parts of this code are translated from the Bantam Java-based Pratt parser
// found at https://github.com/munificent/bantam
// C#-specific changes, and expansion of the base design to support Lua all
// by MOARdV.
/*
 Bantam uses the MIT License:

Copyright (c) 2011 Robert Nystrom

Permission is hereby granted, free of charge, to
any person obtaining a copy of this software and
associated documentation files (the "Software"),
to deal in the Software without restriction,
including without limitation the rights to use,
copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software,
and to permit persons to whom the Software is
furnished to do so, subject to the following
conditions:

The above copyright notice and this permission
notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT
WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO
THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO
EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR
OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/


using System;
using System.Collections.Generic;
using System.Text;

namespace AvionicsSystems.CodeGen
{
    /// <summary>
    /// The Parser class converts a text string into a Delegate whenever the
    /// text can be parsed successfully.  The actual parser is based on a Pratt Parser
    /// implemented in Java at https://github.com/munificent/bantam
    /// with appropriate changes for C#.
    /// 
    /// The Lexer is documented separately.
    /// 
    /// The processing of the Parser's expression tree is original code by MOARdV.
    /// </summary>
    internal class Parser
    {
        internal struct CompilerResult
        {
            internal ResultType type;
            internal Func<object> func;
            internal Expression expressionTree;
            internal string source;
            internal string canonicalName;
            internal double numericConstant;
            internal string stringConstant;
        }

        internal enum ResultType
        {
            ERROR,
            EXPRESSION_TREE,
            NUMERIC_CONSTANT,
            STRING_CONSTANT
        };

        /// <summary>
        /// First stage interface.  MOARdV code to wrap the lexer / parser
        /// steps and provide a neatly-wrapped result.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        internal static CompilerResult TryParse(string source)
        {
            CompilerResult result;
            result.type = ResultType.ERROR;
            result.source = source;
            result.expressionTree = null;
            result.canonicalName = string.Empty;
            result.func = null;
            result.numericConstant = 0.0;
            result.stringConstant = string.Empty;

            try
            {
                Parser parser = new Parser(source);

                Expression exp = parser.parseExpression();

                result.canonicalName = exp.CanonicalName();

                if (exp is StringExpression)
                {
                    result.stringConstant = (exp as StringExpression).getString();
                    result.type = ResultType.STRING_CONSTANT;
                }
                else if (exp is NumberExpression)
                {
                    result.numericConstant = (exp as NumberExpression).getNumber();
                    result.type = ResultType.NUMERIC_CONSTANT;
                }
                else if (exp is PrefixExpression)
                {
                    PrefixExpression p = exp as PrefixExpression;
                    Expression right = p.getRight();

                    if (p.getOperator() == LuaToken.MINUS && right is NumberExpression)
                    {
                        result.numericConstant = -(right as NumberExpression).getNumber();
                        result.type = ResultType.NUMERIC_CONSTANT;
                    }
                }

                if (result.type == ResultType.ERROR)
                {
                    result.expressionTree = exp;
                    result.type = ResultType.EXPRESSION_TREE;
                }

                return result;
            }
            catch (Exception e)
            {
                Utility.LogErrorMessage("TryParse(" + source + ") threw an exception:");
                Utility.LogErrorMessage(e.ToString());
                result.canonicalName = source;
            }

            return result;
        }

        private static Dictionary<string, int> LexerTokens = new Dictionary<string, int>
        {
            {".", 1},  // dot operator -> identify table -dot- method
            {"\"", 2}, // quotation mark -> reassemble strings
            {"~", 3},  // tilde -> not operator
            {"..", 4}, // dot-dot -> concatenation operator
            {"(", 16}, // open paren -> function parameter list entry
            {")", 17}, // close paren -> function parameter list exit
            {",", 18}, // comma operator -> function list separator
            {"*", 32}, // multiplication operator
            {"/", 33}, // division operator
            {"+", 34}, // addition operator
            {"-", 35}, // dash -> might be 'minus', might be 'unary negation'
            {"<", 48}, // less than
            {">", 49}, // greater than

            // Text doesn't actually parse to symbols :(
            {"and", 64}, // logical AND
            {"or", 65}, // logical OR
        };

        internal enum LuaToken
        {
            DOT,

            PLUS,
            MINUS,
            MULTIPLY,
            DIVIDE,
            CONCAT,

            NUMBER,
            STRING,

            LEFT_PAREN,
            RIGHT_PAREN,
            COMMA,
            ASSIGN,
            TILDE,

            LESS_THAN,
            GREATER_THAN,

            AND,
            OR,

            //FALSE,
            //TRUE,

            NAME,

            EOF
        };

        List<Token> tokenList = new List<Token>();
        int mTokens = 0;
        List<Token> mRead = new List<Token>();
        Dictionary<LuaToken, PrefixParselet> mPrefixParselets = new Dictionary<LuaToken, PrefixParselet>();
        Dictionary<LuaToken, InfixParselet> mInfixParselets = new Dictionary<LuaToken, InfixParselet>();

        internal static readonly int ASSIGNMENT = 1;
        internal static readonly int CONDITIONAL = 2;
        internal static readonly int LOGICAL = 3;
        internal static readonly int COMPARISON = 4;
        internal static readonly int SUM = 5;
        internal static readonly int PRODUCT = 6;
        internal static readonly int EXPONENT = 7;
        internal static readonly int PREFIX = 8;
        internal static readonly int POSTFIX = 9;
        internal static readonly int CALL = 10;

        private Parser(string source)
        {
            LexerSettings settings = new LexerSettings();
            settings.Symbols = LexerTokens;
            Lexer lex = new Lexer(source.Trim(), settings);

            bool inQuote = false;
            int startPosition = 0;
            StringBuilder sb = null;
            foreach (var tok in lex)
            {
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
                        sb = new StringBuilder();
                        sb.Append(tok.Text);
                    }
                    else if (tok.Type == TokenType.Decimal)
                    {
                        tokenList.Add(new Token(TokenType.Number, (double)(Decimal)tok.Value, tok.Text, 0, 0, 0, 0, 0, 0, 0));
                    }
                    else if (tok.Type == TokenType.Identifier)
                    {
                        if (tok.Text == "and")
                        {
                            tokenList.Add(new Token(TokenType.Symbol, tok.Text, tok.Text, 64, 0, 0, 0, 0, 0, 0));
                        }
                        else if (tok.Text == "or")
                        {
                            tokenList.Add(new Token(TokenType.Symbol, tok.Text, tok.Text, 65, 0, 0, 0, 0, 0, 0));
                        }
                        else
                        {
                            tokenList.Add(tok);
                        }
                    }
                    else if (tok.Type != TokenType.WhiteSpace)
                    {
                        tokenList.Add(tok);
                    }
                }
            }

            // Now have tokens ready for parsing.
            // This could be done externally.  It also could be hardcoded.

            register(LuaToken.NUMBER, new NumberParselet());
            register(LuaToken.NAME, new NameParselet());
            register(LuaToken.STRING, new StringParselet());
            register(LuaToken.LEFT_PAREN, new CallParselet());
            register(LuaToken.LEFT_PAREN, new GroupParselet());

            register(LuaToken.PLUS, new PrefixOperatorParselet(PREFIX));
            register(LuaToken.MINUS, new PrefixOperatorParselet(PREFIX));
            register(LuaToken.TILDE, new PrefixOperatorParselet(PREFIX));

            register(LuaToken.PLUS, new BinaryOperatorParselet(SUM, false));
            register(LuaToken.MINUS, new BinaryOperatorParselet(SUM, false));
            register(LuaToken.CONCAT, new BinaryOperatorParselet(SUM, false));
            register(LuaToken.MULTIPLY, new BinaryOperatorParselet(PRODUCT, false));
            register(LuaToken.DIVIDE, new BinaryOperatorParselet(PRODUCT, false));
            register(LuaToken.DOT, new BinaryOperatorParselet(CALL, false));
            //register(LuaToken.DOT, new BinaryOperatorParselet(POSTFIX, false));
            register(LuaToken.LESS_THAN, new BinaryOperatorParselet(COMPARISON, false));
            register(LuaToken.GREATER_THAN, new BinaryOperatorParselet(COMPARISON, false));
            register(LuaToken.AND, new BinaryOperatorParselet(LOGICAL, false));
            register(LuaToken.OR, new BinaryOperatorParselet(LOGICAL, false));
        }

        internal Expression parseExpression(int precedence)
        {
            Token token = consume();
            PrefixParselet prefix = null;

            var type = token.getType();
            if (mPrefixParselets.ContainsKey(type))
            {
                prefix = mPrefixParselets[type];
            }

            if (prefix == null)
            {
                throw new Exception("Could not parse \"" + token.Text + "\".");
            }

            Expression left = prefix.parse(this, token);

            while (precedence < getPrecedence())
            {
                token = consume();

                InfixParselet infix = mInfixParselets[token.getType()];
                left = infix.parse(this, left, token);
            }

            return left;

        }

        internal void register(LuaToken token, PrefixParselet parselet)
        {
            mPrefixParselets[token] = parselet;
        }

        internal void register(LuaToken token, InfixParselet parselet)
        {
            mInfixParselets[token] = parselet;
        }

        internal Expression parseExpression()
        {
            return parseExpression(0);
        }

        internal bool match(LuaToken expected)
        {
            Token token = lookAhead(0);
            if (token.getType() != expected)
            {
                return false;
            }

            consume();
            return true;
        }

        internal Token consume(LuaToken expected)
        {
            Token token = lookAhead(0);
            if (token.getType() != expected)
            {
                throw new Exception("Expected token " + expected + " and found " + token.getType() + " " + token.Text);
            }

            return consume();
        }

        internal Token consume()
        {
            // Make sure we've read the token.
            Token toReturn = lookAhead(0);

            mRead.RemoveAt(0);
            return toReturn;
        }

        private Token lookAhead(int distance)
        {
            // Read in as many as needed.
            while (distance >= mRead.Count && mTokens < tokenList.Count)
            {
                mRead.Add(tokenList[mTokens]);
                mTokens++;
            }

            // Get the queued token.
            if (mRead.Count > 0)
            {
                return mRead[distance];
            }
            else
            {
                return null;
            }
        }

        private int getPrecedence()
        {
            var type = lookAhead(0).getType();
            if (mInfixParselets.ContainsKey(type))
            {
                InfixParselet parser = mInfixParselets[type];
                return parser.getPrecedence();
            }
            else
            {
                return 0;
            }
        }

    }

    internal static class Extensions
    {
        private static Parser.LuaToken Symbol(int id)
        {
            switch (id)
            {
                case 1:
                    return Parser.LuaToken.DOT;
                case 3:
                    return Parser.LuaToken.TILDE;
                case 4:
                    return Parser.LuaToken.CONCAT;
                case 16:
                    return Parser.LuaToken.LEFT_PAREN;
                case 17:
                    return Parser.LuaToken.RIGHT_PAREN;
                case 18:
                    return Parser.LuaToken.COMMA;
                case 32:
                    return Parser.LuaToken.MULTIPLY;
                case 33:
                    return Parser.LuaToken.DIVIDE;
                case 34:
                    return Parser.LuaToken.PLUS;
                case 35:
                    return Parser.LuaToken.MINUS;
                case 48:
                    return Parser.LuaToken.LESS_THAN;
                case 49:
                    return Parser.LuaToken.GREATER_THAN;
                case 64:
                    return Parser.LuaToken.AND;
                case 65:
                    return Parser.LuaToken.OR;
            }
            throw new Exception("Unhandled symbol id " + id.ToString());
        }

        internal static Parser.LuaToken getType(this Token token)
        {
            if (token == null)
            {
                return Parser.LuaToken.EOF;
            }

            switch (token.Type)
            {
                case TokenType.Number:
                    return Parser.LuaToken.NUMBER;
                case TokenType.Identifier:
                    return Parser.LuaToken.NAME;
                case TokenType.QuotedString:
                    return Parser.LuaToken.STRING;
                case TokenType.Symbol:
                    return Symbol(token.Id);
            }

            throw new Exception("Unhandled token type " + token.Type.ToString());
        }

        internal static Token Next(this List<Token>.Enumerator enumerator)
        {
            if (enumerator.MoveNext())
            {
                return enumerator.Current;
            }
            else
            {
                throw new IndexOutOfRangeException("Tried to read past end of enumerator");
            }
        }

        internal static string punctuator(this Parser.LuaToken token)
        {
            switch (token)
            {
                case Parser.LuaToken.LEFT_PAREN:
                    return "(";
                case Parser.LuaToken.RIGHT_PAREN:
                    return ")";
                case Parser.LuaToken.COMMA:
                    return ",";
                case Parser.LuaToken.ASSIGN:
                    return "=";
                case Parser.LuaToken.PLUS:
                    return "+";
                case Parser.LuaToken.MINUS:
                    return "-";
                case Parser.LuaToken.MULTIPLY:
                    return "*";
                case Parser.LuaToken.DIVIDE:
                    return "/";
                case Parser.LuaToken.TILDE:
                    return "~";
                case Parser.LuaToken.DOT:
                    return ".";
                case Parser.LuaToken.CONCAT:
                    return "..";
                case Parser.LuaToken.GREATER_THAN:
                    return ">";
                case Parser.LuaToken.LESS_THAN:
                    return "<";
                case Parser.LuaToken.AND:
                    return "and";
                case Parser.LuaToken.OR:
                    return "or";
            }

            throw new Exception("Un-punctuable token " + token.ToString());
        }
    }
}
