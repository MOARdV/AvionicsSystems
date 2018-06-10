// MOARdV Comment:
// Code substantially adopted from java code at https://github.com/munificent/bantam

using System;
using System.Collections.Generic;
using System.Text;
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

namespace AvionicsSystems.CodeGen
{
    class CallParselet : InfixParselet
    {
        public CallParselet(int precedence)
        {
            mPrecedence = precedence;
        }

        public Expression parse(Parser parser, Expression left, Token token)
        {
            // Parse the comma-separated arguments until we hit, ")".
            List<Expression> args = new List<Expression>();

            // There may be no arguments at all.
            if (!parser.match(Parser.LuaToken.RIGHT_PAREN))
            {
                do
                {
                    args.Add(parser.parseExpression());
                } while (parser.match(Parser.LuaToken.COMMA));
                parser.consume(Parser.LuaToken.RIGHT_PAREN);
            }

            return new CallExpression(left, args);
        }

        public int getPrecedence()
        {
            return mPrecedence;
        }
        
        private readonly int mPrecedence;
    }
}
