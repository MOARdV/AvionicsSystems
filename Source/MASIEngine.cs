/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2017 MOARdV
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
//using MoonSharp.Interpreter;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;

namespace AvionicsSystems
{
    /// <summary>
    /// MASIEngine is the interface with aircraft engines and features, including
    /// aircraft engine mods.
    /// </summary>
    /// <LuaName>engine</LuaName>
    /// <mdDoc>
    /// The MASIEngine class provides an interface between Avionics Systems
    /// and aircraft engine related features.
    /// </mdDoc>
    internal class MASIEngine
    {
        internal MASVesselComputer vc;
        
        /// <summary>
        /// The number of thrust reverser modules found on the vessel.
        /// </summary>
        /// <returns>Number of thrust reverser modules.</returns>
        public double ThrustReverserCount()
        {
            return vc.moduleThrustReverser.Length;
        }

        /// <summary>
        /// Returns the normalized thrust reverser position, or 0 if there are none.
        /// </summary>
        /// <returns>Thrust reverser position in the range [0, 1], or 0.</returns>
        public double ThrustReverserPosition()
        {
            float position = 0.0f;
            int numReverserers = vc.moduleThrustReverser.Length;
            for (int i = 0; i < numReverserers; ++i)
            {
                position += vc.moduleThrustReverser[i].Position();
            }
            if (numReverserers > 1)
            {
                position /= (float)(numReverserers);
            }
                
            return position;
        }

        /// <summary>
        /// Toggles installed thrust reversers (deploys them if they are not deployed,
        /// retracts them if they are deployed).
        /// </summary>
        /// <returns>1 if reversers were toggle, 0 if there are no thrust reversers.</returns>
        public double ToggleThrustReverser()
        {
            int numReverserers = vc.moduleThrustReverser.Length;
            for (int i = 0; i < numReverserers; ++i)
            {
                vc.moduleThrustReverser[i].ToggleReverser();
            }

            return (numReverserers > 0) ? 1.0 : 0.0;
        }
    }
}
