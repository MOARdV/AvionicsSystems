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
    /// MASIChatterer is the interface with the Chatterer mod.
    /// </summary>
    /// <LuaName>chatterer</LuaName>
    /// <mdDoc>
    /// The MASIChatterer class provides an interface between the Chatterer mod
    /// and Avionics Systems.  It provides two informational variables and one
    /// action.
    /// </mdDoc>
    internal class MASIChatterer
    {
        static private bool chattererFound = false;

        static private Type chattererAPI_t;
        static private MethodInfo txMethod_t;
        static private MethodInfo rxMethod_t;
        static private MethodInfo chatterMethod_t;

        internal Func<bool> chattererTx;
        internal Func<bool> chattererRx;
        internal Action chattererStartTalking;

        [MoonSharpHidden]
        public MASIChatterer()
        {
            InitChattererMethods();
        }

        ~MASIChatterer()
        {
        }

        private void InitChattererMethods()
        {
            if (!chattererFound)
            {
                return;
            }

            object chatterer = UnityEngine.Object.FindObjectOfType(chattererAPI_t);

            chattererTx = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), chatterer, txMethod_t);
            chattererRx = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), chatterer, rxMethod_t);
            chattererStartTalking = (Action)Delegate.CreateDelegate(typeof(Action), chatterer, chatterMethod_t);
        }

        /// <summary>
        /// The Chatterer category provides the interface with the Chatterer mod (when installed).
        /// </summary>
        #region Chatterer

        [MASProxyAttribute(Immutable = true)]
        /// <summary>
        /// Reports whether the Chatterer mod is installed.
        /// </summary>
        /// <returns>1 if Chatterer is installed, 0 otherwise</returns>
        public double Available()
        {
            return (chattererFound) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Reports on whether or not Mission Control is communicating with the capsule.
        /// </summary>
        /// <returns>1 if ground control is talking, 0 otherwise.</returns>
        public double Receiving()
        {
            if (chattererFound)
            {
                return (chattererRx()) ? 1.0 : 0.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// If the comm channel is idle, start a chatter sequence.  If there is
        /// already an exchange active, do nothing.
        /// </summary>
        /// <returns>1 if Chatterer starts transmitting; 0 otherwise.</returns>
        public double StartTalking()
        {
            if (chattererFound)
            {
                if (!chattererTx() && !chattererRx())
                {
                    chattererStartTalking();
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Reports whether or not the vessel is transmitting to Mission Control.
        /// </summary>
        /// <returns>1 if the vessel is transmitting, 0 otherwise.</returns>
        public double Transmitting()
        {
            if (chattererFound)
            {
                return (chattererTx()) ? 1.0 : 0.0;
            }
            else
            {
                return 0.0;
            }
        }
        #endregion

        [MoonSharpHidden]
        internal void UpdateVessel()
        {
            InitChattererMethods();
        }

        #region Reflection Configuration
        static MASIChatterer()
        {
            chattererFound = false;
            chattererAPI_t = Utility.GetExportedType("Chatterer", "Chatterer.chatterer");
            if(chattererAPI_t != null)
            {
                txMethod_t = chattererAPI_t.GetMethod("VesselIsTransmitting", BindingFlags.Instance | BindingFlags.Public);
                if (txMethod_t == null)
                {
                    throw new NotImplementedException("txMethod_t");
                }

                rxMethod_t = chattererAPI_t.GetMethod("VesselIsReceiving", BindingFlags.Instance | BindingFlags.Public);
                if (rxMethod_t == null)
                {
                    throw new NotImplementedException("rxMethod_t");
                }

                chatterMethod_t = chattererAPI_t.GetMethod("InitiateChatter", BindingFlags.Instance | BindingFlags.Public);
                if (chatterMethod_t == null)
                {
                    throw new NotImplementedException("chatterMethod_t");
                }

                chattererFound = true;
            }
        }
    #endregion
    }
}
