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
    /// The Parser class is used to convert a text string into a Delegate whenever the
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
                Utility.LogStaticError("TryParse(" + source + ") threw an exception:");
                Utility.LogStaticError(e.ToString());
                result.canonicalName = source;
            }

            return result;
        }

        internal enum LuaToken
        {
            DOT,

            PLUS,
            MINUS,
            MULTIPLY,
            DIVIDE,
            MODULO,
            CONCAT,
            EXPONENT,

            NUMBER,
            STRING,

            LEFT_PAREN,
            RIGHT_PAREN,
            COMMA,
            ASSIGN,

            LESS_THAN,
            GREATER_THAN,
            EQUALITY,
            INEQUALITY,
            LESS_EQUAL,
            GREATER_EQUAL,

            AND,
            OR,
            NOT,

            //FALSE,
            //TRUE,

            NAME,

            EOF
        };

        // +++ MOARdV updates
        internal struct LuaTokenData
        {
            internal readonly LuaToken token;
            internal readonly int id;
            internal readonly string symbol;

            internal LuaTokenData(LuaToken token, int id, string symbol)
            {
                this.token = token;
                this.id = id;
                this.symbol = symbol;
            }
        };

        internal static readonly LuaTokenData[] lexerTokens = new LuaTokenData[]
        {
            new LuaTokenData(LuaToken.DOT, 1, "."), // dot operator -> identify table -dot- method
            new LuaTokenData(LuaToken.STRING, 2, "\""), // quotation mark -> reassemble strings
            new LuaTokenData(LuaToken.CONCAT, 4, ".."), // dot-dot -> concatenation operator
            new LuaTokenData(LuaToken.LEFT_PAREN, 16, "("), // open paren -> function parameter list entry
            new LuaTokenData(LuaToken.RIGHT_PAREN, 17, ")"), // close paren -> function parameter list exit
            new LuaTokenData(LuaToken.COMMA, 18, ","), // comma operator -> function list separator
            new LuaTokenData(LuaToken.MULTIPLY, 32, "*"), // multiplication operator
            new LuaTokenData(LuaToken.DIVIDE, 33, "/"), // division operator
            new LuaTokenData(LuaToken.PLUS, 34, "+"), // addition operator
            new LuaTokenData(LuaToken.MINUS, 35, "-"), // dash -> might be 'minus', might be 'unary negation'
            new LuaTokenData(LuaToken.MODULO, 36, "%"), // modulo operator
            new LuaTokenData(LuaToken.EXPONENT, 37, "^"), // exponentation operator
            new LuaTokenData(LuaToken.LESS_THAN, 48, "<"), // less than
            new LuaTokenData(LuaToken.GREATER_THAN, 49, ">"), // greater than
            new LuaTokenData(LuaToken.EQUALITY, 50, "=="), // equality
            new LuaTokenData(LuaToken.INEQUALITY, 51, "~="), // inequality
            new LuaTokenData(LuaToken.LESS_EQUAL, 52, "<="), // less than / equal to
            new LuaTokenData(LuaToken.GREATER_EQUAL, 53, ">="), // greater than / equal to

            // Text doesn't actually parse to symbols :(
            new LuaTokenData(LuaToken.AND, 64, "and"), // logical AND
            new LuaTokenData(LuaToken.OR, 65, "or"), // logical OR
            new LuaTokenData(LuaToken.NOT, 66, "not"), // logical NOT
        };

        private static LexerSettings lexerSettings;

        private static readonly Dictionary<LuaToken, PrefixParselet> prefixParselets = new Dictionary<LuaToken, PrefixParselet>()
        {
            { LuaToken.NUMBER, new NumberParselet() },
            { LuaToken.NAME, new NameParselet() },
            { LuaToken.STRING, new StringParselet() },
            { LuaToken.LEFT_PAREN, new GroupParselet() },

            // NOT: priority 9
            { LuaToken.NOT, new PrefixOperatorParselet(9) },

            // PREFIX: priority 11
            { LuaToken.PLUS, new PrefixOperatorParselet(11) },
            { LuaToken.MINUS, new PrefixOperatorParselet(11) },
        };

        private static readonly Dictionary<LuaToken, InfixParselet> infixParselets = new Dictionary<LuaToken, InfixParselet>()
        {
            // priorities are listed lowest to highest (larger number = higher priority)
            // Based on Lua 5.2 reference manual for LOGICAL OR through EXPONENT, although
            // some of those are prefix operators, or otherwise not used here.
            
            // ASSIGNMENT: priority 1

            // CONDITIONAL: priority 2

            // LOGICAL OR: priority 3
            { LuaToken.OR, new BinaryOperatorParselet(3, false) },

            // LOGICAL AND: priority 4
            { LuaToken.AND, new BinaryOperatorParselet(4, false) },

            // COMPARISON: priority 5
            { LuaToken.LESS_THAN, new BinaryOperatorParselet(5, false) },
            { LuaToken.GREATER_THAN, new BinaryOperatorParselet(5, false) },
            { LuaToken.EQUALITY, new BinaryOperatorParselet(5, false) },
            { LuaToken.INEQUALITY, new BinaryOperatorParselet(5, false) },
            { LuaToken.LESS_EQUAL, new BinaryOperatorParselet(5, false) },
            { LuaToken.GREATER_EQUAL, new BinaryOperatorParselet(5, false) },

            // STRING CONCANTENATE: priority 6
            { LuaToken.CONCAT, new BinaryOperatorParselet(6, false) },
            
            // ADDITION: priority 7
            { LuaToken.PLUS, new BinaryOperatorParselet(7, false) },
            { LuaToken.MINUS, new BinaryOperatorParselet(7, false) },

            // PRODUCT: priority 8
            { LuaToken.MULTIPLY, new BinaryOperatorParselet(8, false) },
            { LuaToken.DIVIDE, new BinaryOperatorParselet(8, false) },
            { LuaToken.MODULO, new BinaryOperatorParselet(8, false) },

            // NOT: priority 9

            // EXPONENT: priority 10
            { LuaToken.EXPONENT, new BinaryOperatorParselet(10, false) },

            // PREFIX: priority 11

            // POSTFIX: priority 12

            // CALL: priority 13
            { LuaToken.LEFT_PAREN, new CallParselet(13) },
            { LuaToken.DOT, new BinaryOperatorParselet(13, false) },
            
        };
        // --- MOARdV updates

        List<Token> tokenList = new List<Token>();
        int mTokens = 0;
        List<Token> mRead = new List<Token>();

        private Parser(string source)
        {
            // MdV++
            // Initialize the static (reusable) lexer settings if we haven't already
            if (lexerSettings == null)
            {
                Dictionary<string, int> lexerTokenDict = new Dictionary<string, int>();
                foreach (LuaTokenData token in lexerTokens)
                {
                    lexerTokenDict.Add(token.symbol, token.id);
                }

                lexerSettings = new LexerSettings();
                lexerSettings.Symbols = lexerTokenDict;
            }
            Lexer lex = new Lexer(source.Trim(), lexerSettings);
            // MdV--

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
                        // The Lexer won't tokenize keywords as symbols, so we have to convert them
                        // here.
                        if (tok.Text == "and")
                        {
                            tokenList.Add(new Token(TokenType.Symbol, tok.Text, tok.Text, 64, 0, 0, 0, 0, 0, 0));
                        }
                        else if (tok.Text == "or")
                        {
                            tokenList.Add(new Token(TokenType.Symbol, tok.Text, tok.Text, 65, 0, 0, 0, 0, 0, 0));
                        }
                        else if (tok.Text == "not")
                        {
                            tokenList.Add(new Token(TokenType.Symbol, tok.Text, tok.Text, 66, 0, 0, 0, 0, 0, 0));
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
        }

        internal Expression parseExpression(int precedence)
        {
            Token token = consume();
            PrefixParselet prefix = null;

            var type = token.getType();
            if (!prefixParselets.TryGetValue(type, out prefix))
            {
                throw new Exception("Could not parse \"" + token.Text + "\".");
            }

            Expression left = prefix.parse(this, token);

            while (precedence < getPrecedence())
            {
                token = consume();

                InfixParselet infix = infixParselets[token.getType()];
                left = infix.parse(this, left, token);
            }

            return left;
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
            InfixParselet parser;
            if (infixParselets.TryGetValue(type, out parser))
            {
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
                case 36:
                    return Parser.LuaToken.MODULO;
                case 37:
                    return Parser.LuaToken.EXPONENT;
                case 48:
                    return Parser.LuaToken.LESS_THAN;
                case 49:
                    return Parser.LuaToken.GREATER_THAN;
                case 50:
                    return Parser.LuaToken.EQUALITY;
                case 51:
                    return Parser.LuaToken.INEQUALITY;
                case 52:
                    return Parser.LuaToken.LESS_EQUAL;
                case 53:
                    return Parser.LuaToken.GREATER_EQUAL;
                case 64:
                    return Parser.LuaToken.AND;
                case 65:
                    return Parser.LuaToken.OR;
                case 66:
                    return Parser.LuaToken.NOT;
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
                case Parser.LuaToken.MODULO:
                    return "%";
                case Parser.LuaToken.EXPONENT:
                    return "^";
                case Parser.LuaToken.DOT:
                    return ".";
                case Parser.LuaToken.CONCAT:
                    return "..";
                case Parser.LuaToken.GREATER_THAN:
                    return ">";
                case Parser.LuaToken.LESS_THAN:
                    return "<";
                case Parser.LuaToken.EQUALITY:
                    return "==";
                case Parser.LuaToken.INEQUALITY:
                    return "~=";
                case Parser.LuaToken.LESS_EQUAL:
                    return "<=";
                case Parser.LuaToken.GREATER_EQUAL:
                    return ">=";
                case Parser.LuaToken.AND:
                    return "and";
                case Parser.LuaToken.OR:
                    return "or";
                case Parser.LuaToken.NOT:
                    return "not";
            }

            throw new Exception("Un-punctuable token " + token.ToString());
        }
    }
}
