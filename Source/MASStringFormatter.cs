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
        /// vals contains an array of integer values: s, m, h, d, y
        /// Originally, the KSP formatter provided this service.
        /// </summary>
        private static int[] vals = new int[5];

        private static object[] formatData = new object[8];

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
            if (formatSpecification.StartsWith("LAT") || formatSpecification.StartsWith("LON"))
            {
                return FormatLatLon(formatSpecification, value);
            }
            if (formatSpecification.StartsWith("BAR"))
            {
                return FormatBar(formatSpecification, value);
            }

            return DefaultFormat(formatSpecification, arg, formatProvider);
        }

        /// <summary>
        /// Generates a textual ("ASCII") bar graph scaled based on the provided value.
        /// </summary>
        /// <param name="formatSpecification"></param>
        /// <param name="value">The value to use as the scale, ranging [0, 1]/</param>
        /// <returns>The bar graph as specified.</returns>
        private static string FormatBar(string formatSpecification, double valueIn)
        {
            float value = Mathf.Clamp01((float)valueIn);

            string[] s = formatSpecification.Split(',');
            if (s.Length != 3)
            {
                return formatSpecification;
            }

            if (s[1].Length < 2)
            {
                Utility.LogStaticError("Invalid second parameter.  Must be two characters minimum length. \"{1}\"", value, formatSpecification);
                return formatSpecification;
            }

            int numChars = 0;
            if (!int.TryParse(s[2], out numChars) || numChars < 1)
            {
                Utility.LogStaticError("Invalid third parameter.  Must be an integer. \"{1}\"", value, formatSpecification);
                return formatSpecification;
            }

            int fillLength = (int)Math.Floor((float)numChars * value + 0.5f);

            StringBuilder sb = StringBuilderCache.Acquire();
            sb.Append(s[1][0], fillLength);
            sb.Append(s[1][1], numChars - fillLength);

            return sb.ToStringAndRelease();
        }

        /// <summary>
        /// Convert a value into either a latitude or longitude display
        /// </summary>
        /// <param name="formatSpecification"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private static string FormatLatLon(string formatSpecification, double value)
        {
            bool latitude = (formatSpecification[1] == 'A');
            bool zeroPad = (formatSpecification.Length >= 4 && formatSpecification[3] == '0');
            bool negative = (value < 0.0);

            float degrees;
            if (latitude == true)
            {
                degrees = Mathf.Clamp(Mathf.Abs((float)value), 0.0f, 90.0f);
            }
            else
            {
                degrees = Mathf.Clamp(Mathf.Abs((float)value), 0.0f, 180.0f);
            }

            float minutes = (degrees - Mathf.Floor(degrees)) * 60.0f;
            degrees = Mathf.Floor(degrees);
            float seconds = (minutes - Mathf.Floor(minutes)) * 60.0f;
            minutes = Mathf.Floor(minutes);

            string result;
            if (zeroPad)
            {
                if (latitude)
                {
                    result = string.Format("{0:00}° {1:00}' {2:00}\" {3}",
                        degrees, minutes, seconds, (negative) ? 'S' : 'N');
                }
                else
                {
                    result = string.Format("{0:000}° {1:00}' {2:00}\" {3}",
                        degrees, minutes, seconds, (negative) ? 'W' : 'E');
                }
            }
            else
            {
                if (latitude)
                {
                    result = string.Format("{0:#0}° {1:00}' {2:00}\" {3}",
                        degrees, minutes, seconds, (negative) ? 'S' : 'N');
                }
                else
                {
                    result = string.Format("{0:##0}° {1:00}' {2:00}\" {3}",
                        degrees, minutes, seconds, (negative) ? 'W' : 'E');
                }
            }

            return result;
        }

        /// <summary>
        /// Format a numeric value following KSP date/time standards.  For KDT,
        /// we add one to the days column to mimic KSP behavior.
        /// </summary>
        /// <param name="formatSpecification"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private static string FormatMET(string formatSpecification, double value)
        {
            double hoursPerDay = KSPUtil.dateTimeFormatter.Day / 3600.0;
            double daysPerYear = KSPUtil.dateTimeFormatter.Year / KSPUtil.dateTimeFormatter.Day;

            // seconds...
            double timeBalance = (double.IsInfinity(value) || double.IsNaN(value)) ? 0.0 : Math.Abs(value);
            vals[0] = (int)(timeBalance % 60.0);
            // minutes...
            timeBalance /= 60.0;
            vals[1] = (int)(timeBalance % 60.0);
            // hours...
            timeBalance /= 60.0;
            vals[2] = (int)(timeBalance % hoursPerDay);
            // days...
            timeBalance /= hoursPerDay;
            vals[3] = (int)(timeBalance % daysPerYear);
            // years...
            vals[4] = (int)(timeBalance / daysPerYear);

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

            StringBuilder sb = StringBuilderCache.Acquire();
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
                                formatData[parameterCount] = (vals[4] + (calendarAdjust ? 1 : 0));
                                break;
                            case 'D':
                                formatData[parameterCount] = (int)(vals[3] + daysPerYear * vals[4]);
                                break;
                            case 'd':
                                formatData[parameterCount] = (vals[3] + (calendarAdjust ? 1 : 0));
                                break;
                            case 'H':
                                formatData[parameterCount] = (int)(vals[2] + hoursPerDay * vals[3] + daysPerYear * vals[4]);
                                break;
                            case 'h':
                                formatData[parameterCount] = vals[2];
                                break;
                            case 'M':
                                formatData[parameterCount] = (int)(vals[1] + 60 * vals[2] + hoursPerDay * vals[3] + daysPerYear * vals[4]);
                                break;
                            case 'm':
                                formatData[parameterCount] = vals[1];
                                break;
                            case 'S':
                                formatData[parameterCount] = Math.Floor(Math.Abs(value));
                                break;
                            case 's':
                                formatData[parameterCount] = vals[0];
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

            string result = string.Format(sb.ToStringAndRelease(), formatData);

            return result;
        }

        private static readonly string siPrefixes = " kMGTPEZY";

        /// <summary>
        /// Append SI prefix for any variable >= 1000
        /// </summary>
        /// <param name="formatSpecification"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private static string FormatSI(string formatSpecification, double value)
        {
            int siChar = 0;
            if (double.IsInfinity(value) || double.IsNaN(value))
            {
                value = 0.0;
            }
            else if (value >= 1000.0 || value <= -1000.0)
            {
                siChar = (int)(Math.Log10(Math.Abs(value))) / 3;
                siChar = Math.Min(siChar, 8);

                value /= Math.Pow(10.0, (siChar * 3));
            }

            StringBuilder sb = StringBuilderCache.Acquire();
            sb.Append("{0:");
            sb.Append(formatSpecification.Substring(3));
            sb.Append('}');
            sb.Append(siPrefixes[siChar]);

            return string.Format(sb.ToStringAndRelease(), value);
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
