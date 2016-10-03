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
    ///
    /// One of the two parselet interfaces used by the Pratt parser. An
    /// InfixParselet is associated with a token that appears in the middle of the
    /// expression it parses. Its parse() method will be called after the left-hand
    /// side has been parsed, and it in turn is responsible for parsing everything
    /// that comes after the token. This is also used for postfix expressions, in
    /// which case it simply doesn't consume any more tokens in its parse() call.
    /// </summary>
    interface InfixParselet
    {
        Expression parse(Parser parser, Expression left, Token token);
        int getPrecedence();
    }
}
