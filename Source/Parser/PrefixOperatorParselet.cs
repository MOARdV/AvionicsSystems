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
    /// <summary>
    /// Generic prefix parselet for an unary arithmetic operator. Parses prefix
    /// unary "-", "+", "~", and "!" expressions.
    /// </summary>
    class PrefixOperatorParselet : PrefixParselet
    {
        public PrefixOperatorParselet(int precedence)
        {
            mPrecedence = precedence;
        }

        public Expression parse(Parser parser, Token token)
        {
            // To handle right-associative operators like "^", we allow a slightly
            // lower precedence when parsing the right-hand side. This will let a
            // parselet with the same precedence appear on the right, which will then
            // take *this* parselet's result as its left-hand argument.
            Expression right = parser.parseExpression(mPrecedence);

            return new PrefixExpression(token.getType(), right);
        }

        public int getPrecedence()
        {
            return mPrecedence;
        }

        private int mPrecedence;
    }
}
