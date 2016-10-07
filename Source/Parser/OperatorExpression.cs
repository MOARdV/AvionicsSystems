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
    class OperatorExpression : Expression
    {
        public OperatorExpression(Expression left, Parser.LuaToken oper, Expression right)
        {
            mLeft = left;
            mOperator = oper;
            mRight = right;

            switch(mOperator)
            {
                case Parser.LuaToken.PLUS:
                case Parser.LuaToken.MINUS:
                case Parser.LuaToken.MULTIPLY:
                case Parser.LuaToken.DIVIDE:
                    returnType = typeof(double);
                    break;
                case Parser.LuaToken.CONCAT:
                    returnType = typeof(string);
                    break;
                case Parser.LuaToken.LESS_THAN:
                case Parser.LuaToken.GREATER_THAN:
                case Parser.LuaToken.AND:
                case Parser.LuaToken.OR:
                    returnType = typeof(bool);
                    break;
                default:
                    returnType = typeof(void);
                    break;
            }

            StringBuilder sb = Utility.GetStringBuilder();
            print(sb);
            canonicalName = sb.ToString();
        }

        public string CanonicalName() { return canonicalName; }

        public ExpressionIs ExpressionType() { return ExpressionIs.Operator; }

        public void print(StringBuilder builder)
        {
            builder.Append("(");
            mLeft.print(builder);
            builder.Append(" ").Append(mOperator.punctuator()).Append(" ");
            mRight.print(builder);
            builder.Append(")");
        }

        public Expression LeftOperand() { return mLeft; }
        public Expression RightOperand() { return mRight; }

        public Type ReturnType()
        {
            return returnType;
        }

        public Parser.LuaToken Operator() { return mOperator; }
        private Expression mLeft;
        private Parser.LuaToken mOperator;
        private Expression mRight;
        private readonly Type returnType;
        private string canonicalName;
    }

    class DotOperatorExpression : Expression
    {
        public DotOperatorExpression(Expression left, Parser.LuaToken oper, Expression right)
        {
            mLeft = left;
            mOperator = oper;
            mRight = right;

            StringBuilder sb = Utility.GetStringBuilder();
            print(sb);
            canonicalName = sb.ToString();
        }

        public string CanonicalName() { return canonicalName; }

        public ExpressionIs ExpressionType() { return ExpressionIs.DotOperator; }

        public void print(StringBuilder builder)
        {
            builder.Append("(");
            mLeft.print(builder);
            builder.Append(mOperator.punctuator());
            mRight.print(builder);
            builder.Append(")");
        }

        public Type ReturnType()
        {
            // What is the right return type?
            return typeof(object);
        }

        public Expression TableName() { return mLeft; }
        public Expression MethodName() { return mRight; }

        private Expression mLeft;
        private Parser.LuaToken mOperator;
        private Expression mRight;
        private string canonicalName;
    }
}
