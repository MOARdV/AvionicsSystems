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
    class CallExpression : Expression
    {
        public CallExpression(Expression function, List<Expression> args)
        {
            mFunction = function;
            mArgs = args;

            StringBuilder sb = Utility.GetStringBuilder();
            print(sb);
            canonicalName = sb.ToString();
        }

        public string CanonicalName() { return canonicalName; }

        public ExpressionIs ExpressionType() { return ExpressionIs.Call; }

        public void print(StringBuilder builder)
        {
            mFunction.print(builder);
            builder.Append("(");
            for (int i = 0; i < mArgs.Count; i++)
            {
                mArgs[i].print(builder);
                if (i < mArgs.Count - 1)
                {
                    builder.Append(", ");
                }
            }
            builder.Append(")");
        }

        public Type ReturnType()
        {
            return typeof(object);
        }

        internal Expression Function() { return mFunction; }
        internal int NumArgs() { return mArgs.Count; }
        internal Expression Arg(int idx) { return mArgs[idx]; }

        private Expression mFunction;
        private List<Expression> mArgs;
        private string canonicalName;
    }
}
