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
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace AvionicsSystems
{
    /// <summary>
    /// MASIFAR is the MAS interface to the Ferram Aerospace Research mod.  It
    /// is a MoonSharp proxy class.
    /// </summary>
    internal class MASIFAR
    {
        private static bool farFound;

        private static Func<Vessel, double> coeffLift;
        private static Func<Vessel, double> refArea;

        internal Vessel vessel;

        [MoonSharpHidden]
        public MASIFAR(Vessel vessel)
        {
            this.vessel = vessel;
        }

        ~MASIFAR()
        {
            vessel = null;
        }

        /// <summary>
        /// Return the coefficient of lift for this vessel.
        /// </summary>
        /// <returns></returns>
        public double CoeffLift()
        {
            if (farFound)
            {
                return coeffLift(vessel);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Return the RefArea for this vessel.
        /// </summary>
        /// <returns></returns>
        public double RefArea()
        {
            if (farFound)
            {
                return refArea(vessel);
            }
            else
            {
                return 0.0;
            }
        }

        #region Reflection Configuration
        static MASIFAR()
        {
            farFound = false;
            Type farAPI_t = Utility.GetExportedType("FerramAerospaceResearch", "FerramAerospaceResearch.FARAPI");
            if(farAPI_t != null)
            {
                MethodInfo coeffLift_t = farAPI_t.GetMethod("VesselLiftCoeff", BindingFlags.Static | BindingFlags.Public);
                if (coeffLift_t == null)
                {
                    Utility.LogErrorMessage("Failed to find 'VesselLiftCoeff' in FAR");
                    return;
                }
                coeffLift = (Func<Vessel, double>)Delegate.CreateDelegate(typeof(Func<Vessel, double>), coeffLift_t);

                MethodInfo refArea_t = farAPI_t.GetMethod("VesselRefArea", BindingFlags.Static | BindingFlags.Public);
                if (refArea_t == null)
                {
                    Utility.LogErrorMessage("Failed to find 'VesselRefArea' in FAR");
                    return;
                }
                refArea = (Func<Vessel, double>)Delegate.CreateDelegate(typeof(Func<Vessel, double>), refArea_t);

                farFound = true;
            }
        }
        #endregion
    }
}
