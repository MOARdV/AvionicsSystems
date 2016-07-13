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
using System;
using System.Collections.Generic;
using System.Text;

namespace AvionicsSystems
{
    /// <summary>
    /// Provide formatting services for the custom formats introduced in
    /// RasterPropMonitor.  To ease the transition from RPM to MAS, this
    /// formatter is 100% backwards compatible, although it may provide
    /// some additional options.
    /// </summary>
    public class MASStringFormatter : IFormatProvider, ICustomFormatter
    {
        private static StringBuilder sb = new StringBuilder(8);

        /// <summary>
        /// Return a reference to ourselves if they're asking for a
        /// customer formatter.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public object GetFormat(Type t)
        {
            if (t == typeof(ICustomFormatter))
            {
                return this;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Provide formatting
        /// </summary>
        /// <param name="formatSpecification">Format parameters (if they exist)</param>
        /// <param name="arg">The object being formatted</param>
        /// <param name="formatProvider">The format provider object</param>
        /// <returns>Formatted text</returns>
        public string Format(string formatSpecification, object arg, IFormatProvider formatProvider)
        {
            if (formatSpecification == null || arg is string)
            {
                return DefaultFormat(formatSpecification, arg, formatProvider);
            }

            double value;
            try
            {
                value = Convert.ToDouble(arg);
            }
            catch
            {
                return DefaultFormat(formatSpecification, arg, formatProvider);
            }

            if (formatSpecification.StartsWith("SIP"))
            {
                return FormatSI(formatSpecification, value);
            }

            return DefaultFormat(formatSpecification, arg, formatProvider);
        }

        private static readonly string siPrefixes = " kMGTPEZY";

        /// <summary>
        /// Apply SI prefix formatting for any variable >= 1000
        /// </summary>
        /// <param name="formatSpecification"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private string FormatSI(string formatSpecification, double value)
        {
            int siChar = 0;
            if (value >= 1000.0 || value <=-1000.0)
            {
                bool isNegative = (value < 0.0);
                siChar = (int)(Math.Log10(Math.Abs(value))) / 3;
                siChar = Math.Min(siChar, 8);

                value /= Math.Pow(10.0, (siChar * 3));
            }

            int formatLength = formatSpecification.Length;
            bool foundDecimal = false;
            bool zeroPrefix = false;
            int units = 0;
            int frac = 0;
            char[] chars = formatSpecification.ToCharArray();

            if (chars[3] == '0')
            {
                zeroPrefix = true;
            }

            for (int i = 3; i < formatLength; ++i)
            {
                if (chars[i] == '.')
                {
                    foundDecimal = true;
                }
                else if (char.IsDigit(chars[i]))
                {
                    if (foundDecimal)
                    {
                        frac = frac * 10;
                        frac += (int)(chars[i]) - (int)('0');
                    }
                    else
                    {
                        units = units * 10;
                        units += (int)(chars[i]) - (int)('0');
                    }
                }
            }
            if (units > 1)
            {
                --units;
            }

            sb.Remove(0, sb.Length);
            sb.Append("{0:");
            if (zeroPrefix)
            {
                sb.Append('0', units);
            }
            else
            {
                sb.Append('#', units - 1);
                sb.Append('0');
            }
            if (foundDecimal)
            {
                sb.Append('.');
                sb.Append('0', frac);
            }
            sb.Append("}{1}");

            string formatString = sb.ToString();
            return string.Format(formatString, value, siPrefixes[siChar]);
        }

        /// <summary>
        /// If this is not a number, or there's no formatSpecification, then
        /// we punt on formatting.
        /// </summary>
        /// <param name="formatSpecification"></param>
        /// <param name="arg"></param>
        /// <param name="formatProvider"></param>
        /// <returns></returns>
        private static string DefaultFormat(string formatSpecification, object arg, IFormatProvider formatProvider)
        {
            return (arg is IFormattable) ? (arg as IFormattable).ToString(formatSpecification, formatProvider) : arg.ToString();
        }
    }
}
