/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2021 Sovetskysoyuz
 * based on code by MoarDV
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
    /// MASIAtmosphereAutopilot is the interface with the Atmosphere Autopilot mod.
    /// </summary>
    /// <LuaName>atmosphereAutopilot</LuaName>
    /// <mdDoc>
    /// The MASIAtmosphereAutopilot class provides an interface between the Atmosphere Autopilot mod
    /// and Avionics Systems.  It provides two informational variables and one
    /// action.
    /// </mdDoc>
    internal class MASIAtmosphereAutopilot
    {
        static private bool aaFound = false;

		// basic mod control functions
        static private Type aaAPI_t;
		// control over which controller is active
        static private Type aaTopModuleAPI_t;
        // static private MethodInfo txMethod_t;
        // static private MethodInfo rxMethod_t;
        // static private MethodInfo chatterMethod_t;
		// replace these with MethodInfo for the requisite AA methods
		static private MethodInfo activateAutopilot_t;

        // internal Func<bool> chattererTx;
        // internal Func<bool> chattererRx;
        // internal Action chattererStartTalking;
		// Figure out what these need to be replaced with
		internal Action aaActivateAutopilot;

        [MoonSharpHidden]
        public MASIAtmosphereAutopilot()
        {
            InitAtmosphereAutopilotMethods();
        }

        ~MASIAtmosphereAutopilot()
        {
        }

        private void InitAtmosphereAutopilotMethods()
        {
            if (!aaFound)
            {
                return;
            }

            object atmosphereAutopilot = UnityEngine.Object.FindObjectOfType(aaAPI_t);
			object topModuleManager = UnityEngine.Object.FindObjectOfType(aaTopModuleAPI_t);

            // chattererTx = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), chatterer, txMethod_t);
            // chattererRx = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), chatterer, rxMethod_t);
            // chattererStartTalking = (Action)Delegate.CreateDelegate(typeof(Action), chatterer, chatterMethod_t);
			// replace these with whatever functions wound up being defined above.
			
			aaActivateAutopilot = (Action)Delegate.CreateDelegate(typeof(Action), topModuleManager, activateAutopilot_t);
        }

        /// <summary>
        /// The AtmosphereAutopilot category provides the interface with the AtmosphereAutopilot mod (when installed).
        /// </summary>
        #region AtmosphereAutopilot

        [MASProxyAttribute(Immutable = true)]
        /// <summary>
        /// Reports whether the AtmosphereAutopilot mod is installed.
        /// </summary>
        /// <returns>1 if AtmosphereAutopilot is installed, 0 otherwise</returns>
        public double Available()
        {
            return (aaFound) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Reports on whether or not Mission Control is communicating with the capsule.
        /// </summary>
        /// <returns>1 if ground control is talking, 0 otherwise.</returns>
        // public double Receiving()
        // {
            // if (chattererFound)
            // {
                // return (chattererRx()) ? 1.0 : 0.0;
            // }
            // else
            // {
                // return 0.0;
            // }
        // }

        /// <summary>
        /// If the comm channel is idle, start a chatter sequence.  If there is
        /// already an exchange active, do nothing.
        /// </summary>
        /// <returns>1 if Chatterer starts transmitting; 0 otherwise.</returns>
        // public double StartTalking()
        // {
            // if (chattererFound)
            // {
                // if (!chattererTx() && !chattererRx())
                // {
                    // chattererStartTalking();
                    // return 1.0;
                // }
            // }

            // return 0.0;
        // }
		
		public double AAActivateAutopilot()
		{
			if(aaFound)
			{
				aaActivateAutopilot();
				return 1.0;
			}
			
			return 0.0;
		}

        /// <summary>
        /// Reports whether or not the vessel is transmitting to Mission Control.
        /// </summary>
        /// <returns>1 if the vessel is transmitting, 0 otherwise.</returns>
        // public double Transmitting()
        // {
            // if (chattererFound)
            // {
                // return (chattererTx()) ? 1.0 : 0.0;
            // }
            // else
            // {
                // return 0.0;
            // }
        // }
        #endregion

        [MoonSharpHidden]
        internal void UpdateVessel()
        {
            InitAtmosphereAutopilotMethods();
        }

        #region Reflection Configuration
        static MASIAtmosphereAutopilot()
        {
            aaFound = false;
            aaAPI_t = Utility.GetExportedType("AtmosphereAutopilot", "AtmosphereAutopilot.AtmosphereAutopilot");
			aaTopModuleAPI_t = Utility.GetExportedType("AtmosphereAutopilot", "AtmosphereAutopilot.TopModuleManager");
            if(aaAPI_t != null)
            {
                // txMethod_t = chattererAPI_t.GetMethod("VesselIsTransmitting", BindingFlags.Instance | BindingFlags.Public);
                // if (txMethod_t == null)
                // {
                    // throw new NotImplementedException("txMethod_t");
                // }

                // rxMethod_t = chattererAPI_t.GetMethod("VesselIsReceiving", BindingFlags.Instance | BindingFlags.Public);
                // if (rxMethod_t == null)
                // {
                    // throw new NotImplementedException("rxMethod_t");
                // }

                // chatterMethod_t = chattererAPI_t.GetMethod("InitiateChatter", BindingFlags.Instance | BindingFlags.Public);
                // if (chatterMethod_t == null)
                // {
                    // throw new NotImplementedException("chatterMethod_t");
                // }

                aaFound = true;
            }
			if(aaTopModuleAPI_t != null)
			{
				activateAutopilot_t = aaTopModuleAPI_t.GetMethod("activateAutopilot", BindingFlags.Instance | BindingFlags.Public);
				if (activateAutopilot_t == null)
				{
					throw new NotImplementedException("activateAutopilot_t");
				}
			}
        }
    #endregion
    }
}
