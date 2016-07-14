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
using UnityEngine;
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
        /// <summary>
        /// Take advantage of KSP's existing date/time formatter.
        /// </summary>
        private static KSPUtil.DefaultDateTimeFormatter dateTimeFormatter = new KSPUtil.DefaultDateTimeFormatter();

        /// <summary>
        /// StringBuilder for concatenating strings.  Keep one persistent
        /// instance instead of creating one per format task.
        /// </summary>
        private static StringBuilder sb = new StringBuilder(8);

        private static object[] formatData = new object[8];

        private static readonly int EarthHoursPerDay = 24;
        private static readonly int KerbinHoursPerDay = 6;

        private static readonly int EarthDaysPerYear = 365;
        private static readonly int KerbinDaysPerYear = 426;

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
            if (formatSpecification.StartsWith("MET") || formatSpecification.StartsWith("KDT"))
            {
                return FormatMET(formatSpecification, value);
            }

            return DefaultFormat(formatSpecification, arg, formatProvider);
        }

        /// <summary>
        /// Format a numeric value following KSP date/time standards.  For KDT,
        /// we add one to the days column to mimic KSP behavior.
        /// </summary>
        /// <param name="formatSpecification"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private string FormatMET(string formatSpecification, double value)
        {
            // vals contains an array of integer values: s, m, h, d, y
            // All of them are negative if the input is negative.
            int[] vals;
            int daysPerYear;
            int hoursPerDay;
            if (GameSettings.KERBIN_TIME)
            {
                vals = dateTimeFormatter.GetKerbinDateFromUT(value);
                daysPerYear = KerbinDaysPerYear;
                hoursPerDay = KerbinHoursPerDay;
            }
            else
            {
                vals = dateTimeFormatter.GetEarthDateFromUT(value);
                daysPerYear = EarthDaysPerYear;
                hoursPerDay = EarthHoursPerDay;
            }

            char[] chars = formatSpecification.ToCharArray();

            bool calendarAdjust = (chars[1] == 'D');
            bool negativeValue = (value < 0.0);

            bool activeFormatter = false;
            bool didSomething = false;
            char activeChar = ' ';
            int charCount = 0;
            int parameterCount = 0;
            int numChars = chars.Length;
            int parameterLimit = formatData.Length;
            for (int i = 0; i < parameterLimit; ++i)
            {
                formatData[i] = null;
            }
            sb.Remove(0, sb.Length);
            for (int i = 3; i < numChars && parameterCount < parameterLimit; ++i)
            {
                didSomething = false;
                if (activeFormatter)
                {
                    if (chars[i] == activeChar)
                    {
                        ++charCount;
                        didSomething = true;
                    }
                    else
                    {
                        // Changed format or ended format.  Update SB and format array
                        activeFormatter = false;
                        sb.Append('{');
                        sb.Append(parameterCount);
                        sb.Append(':');
                        sb.Append('0', charCount);
                        sb.Append('}');

                        switch (activeChar)
                        {
                            case 'Y':
                            // Fall through
                            case 'y':
                                formatData[parameterCount] = Math.Abs(vals[4]);
                                break;
                            case 'D':
                                formatData[parameterCount] = Math.Abs(vals[3] + daysPerYear * vals[4]);
                                break;
                            case 'd':
                                formatData[parameterCount] = Math.Abs(vals[3] + (calendarAdjust ? 1 : 0));
                                break;
                            case 'H':
                                formatData[parameterCount] = Math.Abs(vals[2] + hoursPerDay * vals[3] + daysPerYear * vals[4]);
                                break;
                            case 'h':
                                formatData[parameterCount] = Math.Abs(vals[2]);
                                break;
                            case 'M':
                                formatData[parameterCount] = Math.Abs(vals[1] + 60 * vals[2] + hoursPerDay * vals[3] + daysPerYear * vals[4]);
                                break;
                            case 'm':
                                formatData[parameterCount] = Math.Abs(vals[1]);
                                break;
                            case 'S':
                                formatData[parameterCount] = Math.Floor(Math.Abs(value));
                                break;
                            case 's':
                                formatData[parameterCount] = Math.Abs(vals[0]);
                                break;
                            case 'f':
                                double fracV = Math.Abs(value) - Math.Floor(Math.Abs(value));
                                fracV = fracV * Math.Pow(10.0, charCount);
                                formatData[parameterCount] = Math.Floor(fracV);
                                break;
                        }
                        ++parameterCount;
                        charCount = 0;
                    }
                }

                if (!didSomething)
                {
                    switch (chars[i])
                    {
                        case 'Y':
                        case 'y':
                        case 'D':
                        case 'd':
                        case 'H':
                        case 'h':
                        case 'M':
                        case 'm':
                        case 'S':
                        case 's':
                        case 'f':
                            activeChar = chars[i];
                            activeFormatter = true;
                            charCount = 1;
                            break;
                        case '+':
                            sb.Append((negativeValue) ? '-' : '+');
                            break;
                        case '-':
                            if (negativeValue)
                            {
                                sb.Append('-');
                            }
                            break;
                        default:
                            sb.Append(chars[i]);
                            break;
                    }
                }
            }

            if (activeFormatter && parameterCount < parameterLimit)
            {
                activeFormatter = false;
                sb.Append('{');
                sb.Append(parameterCount);
                sb.Append(':');
                sb.Append('0', charCount);
                sb.Append('}');

                switch (activeChar)
                {
                    case 'Y':
                    // Fall through
                    case 'y':
                        formatData[parameterCount] = Math.Abs(vals[4]);
                        break;
                    case 'D':
                        formatData[parameterCount] = Math.Abs(vals[3] + daysPerYear * vals[4]);
                        break;
                    case 'd':
                        formatData[parameterCount] = Math.Abs(vals[3] + (calendarAdjust ? 1 : 0));
                        break;
                    case 'H':
                        formatData[parameterCount] = Math.Abs(vals[2] + hoursPerDay * vals[3] + daysPerYear * vals[4]);
                        break;
                    case 'h':
                        formatData[parameterCount] = Math.Abs(vals[2]);
                        break;
                    case 'M':
                        formatData[parameterCount] = Math.Abs(vals[1] + 60 * vals[2] + hoursPerDay * vals[3] + daysPerYear * vals[4]);
                        break;
                    case 'm':
                        formatData[parameterCount] = Math.Abs(vals[1]);
                        break;
                    case 'S':
                        formatData[parameterCount] = Math.Floor(Math.Abs(value));
                        break;
                    case 's':
                        formatData[parameterCount] = Math.Abs(vals[0]);
                        break;
                    case 'f': 
                        double fracV = Math.Abs(value) - Math.Floor(Math.Abs(value));
                        fracV = fracV * Math.Pow(10.0, charCount);
                        formatData[parameterCount] = Math.Floor(fracV);
                        break;
                }
                charCount = 0;
                ++parameterCount;
            }

            string result = string.Format(sb.ToString(), formatData);

            return result;
        }

        private static readonly string siPrefixes = " kMGTPEZY";

        /// <summary>
        /// Apply SI prefix formatting for any variable >= 1000
        /// </summary>
        /// <param name="formatSpecification"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private static string FormatSI(string formatSpecification, double value)
        {
            int siChar = 0;
            if (value >= 1000.0 || value <= -1000.0)
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
