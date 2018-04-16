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
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace AvionicsSystems
{
    /// <summary>
    /// MASIParachute is the interface with the RealChute mod and
    /// stock parachutes.
    /// </summary>
    /// <LuaName>parachute</LuaName>
    /// <mdDoc>
    /// The MASIParachute component allows Avionics Systems to interact with the
    /// RealChute mod.  In addition, it allows some control with the stock
    /// parachute systems regardless of whether RealChute is installed.
    /// </mdDoc>
    internal class MASIParachute
    {
        internal static bool realChuteFound;
        internal static readonly Type rcAPI_t;

        private static readonly Func<object, bool> getArmed;
        private static readonly FieldInfo safeState_t;

        private static readonly MethodInfo armChute_t;
        private static readonly MethodInfo cutChute_t;
        private static readonly MethodInfo deployChute_t;
        private static readonly MethodInfo disarmChute_t;
        private static readonly MethodInfo getAnyDeployed_t;

        // From RealChute:
        private enum SafeState
        {
            SAFE,
            RISKY,
            DANGEROUS
        }

        internal Vessel vessel;
        internal MASVesselComputer vc;

        internal Func<bool>[] getAnyDeployed = new Func<bool>[0];
        internal Action[] armParachute = new Action[0];
        internal Action[] cutRealChute = new Action[0];
        internal Action[] deployRealChute = new Action[0];
        internal Action[] disarmParachute = new Action[0];

        private bool allSafe;
        private bool allDangerous;
        private bool anyArmed;
        private bool anyDeployed;

        [MoonSharpHidden]
        public MASIParachute(Vessel vessel)
        {
            this.vessel = vessel;
            anyArmed = false;
        }

        ~MASIParachute()
        {
            vessel = null;
            vc = null;
        }

        [MASProxyAttribute(Immutable = true)]
        /// <summary>
        /// Returns 1 if RealChute is installed and available on this craft, 0 if it
        /// is not available.
        /// </summary>
        /// <returns></returns>
        public double RealChuteAvailable()
        {
            return (realChuteFound) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Cut all deployed parachutes (RealChute as well as stock).
        /// </summary>
        /// <returns>The number of parachutes receiving the instruction.</returns>
        public double CutParachute()
        {
            int chuteCount = 0;
            for (int i = cutRealChute.Length - 1; i >= 0; --i)
            {
                cutRealChute[i]();
                ++chuteCount;
            }

            for (int i = vc.moduleParachute.Length - 1; i >= 0; --i)
            {
                if (vc.moduleParachute[i].deploymentState == ModuleParachute.deploymentStates.DEPLOYED || vc.moduleParachute[i].deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED)
                {
                    vc.moduleParachute[i].CutParachute();
                    ++chuteCount;
                }
            }

            return (double)chuteCount;
        }

        /// <summary>
        /// Deploy all parachutes (RealChute as well as stock).
        /// </summary>
        /// <returns>The number of parachutes receiving the deploy command.</returns>
        public double DeployParachute()
        {
            int chuteCount = 0;
            for (int i = deployRealChute.Length - 1; i >= 0; --i)
            {
                deployRealChute[i]();
                ++chuteCount;
            }

            for (int i = vc.moduleParachute.Length - 1; i >= 0; --i)
            {
                if (vc.moduleParachute[i].deploymentState == ModuleParachute.deploymentStates.STOWED)
                {
                    vc.moduleParachute[i].Deploy();
                    ++chuteCount;
                }
            }

            return (double)chuteCount;
        }

        /// <summary>
        /// Returns 1 if it is safe to deploy all parachutes, 0 if it is safe for
        /// some parachutes, or -1 if it is dangerous for all parachutes.  Returns
        /// 1 if there are no parachutes.
        /// </summary>
        /// <returns></returns>
        public double DeploymentSafe()
        {
            if (allSafe)
            {
                return 1.0;
            }
            else if (allDangerous)
            {
                return -1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if at least one RealChute parachute is armed, 0
        /// otherwise.
        /// </summary>
        /// <returns></returns>
        public double GetParachuteArmed()
        {
            return (anyArmed) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 is at least one RealChute parachute is armed or deployed,
        /// or if any stock parachutes are deployed; 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double GetParachuteArmedOrDeployed()
        {
            return (anyArmed || anyDeployed) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if at least one RealChute or stock parachute is deployed;
        /// 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double GetParachuteDeployed()
        {
            return (anyDeployed) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Toggles the armed state of any RealChute parachutes.
        /// </summary>
        /// <returns>1 if parachutes are armed, 0 otherwise</returns>
        public double ToggleParachuteArmed()
        {
            bool armed = false;

            if (anyArmed)
            {
                for (int i = disarmParachute.Length - 1; i >= 0; --i)
                {
                    disarmParachute[i]();
                }
                for (int i = vc.moduleParachute.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleParachute[i].deploymentState == ModuleParachute.deploymentStates.ACTIVE)
                    {
                        vc.moduleParachute[i].Disarm();
                    }
                }
            }
            else
            {
                armed = (armParachute.Length > 0);
                for (int i = armParachute.Length - 1; i >= 0; --i)
                {
                    armParachute[i]();
                }
                for (int i = vc.moduleParachute.Length - 1; i >= 0; --i)
                {
                    // If the stock 'chute is stowed, and it is configured as
                    // automateSafeDeploy == 0 (deploy when safe), tell it to deploy.
                    // It won't deploy until it's safe, so it's similar to RealChute's
                    // armed state.
                    if (vc.moduleParachute[i].deploymentState == ModuleParachute.deploymentStates.STOWED && vc.moduleParachute[i].automateSafeDeploy == 0)
                    {
                        vc.moduleParachute[i].Deploy();
                        armed = true;
                    }
                }
            }

            return (armed) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Method called during FixedUpdate to update queryable variables that
        /// are used by multiple methods.
        /// </summary>
        [MoonSharpHidden]
        internal void Update()
        {
            int newLength = vc.moduleRealChute.Length;
            if (newLength != armParachute.Length)
            {
                // The module count has changed: we need to rebuild all of our delegates.
                getAnyDeployed = new Func<bool>[newLength];
                armParachute = new Action[newLength];
                cutRealChute = new Action[newLength];
                deployRealChute = new Action[newLength];
                disarmParachute = new Action[newLength];

                for (int i = 0; i < newLength; ++i)
                {
                    getAnyDeployed[i] = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), vc.moduleRealChute[i], getAnyDeployed_t);
                    armParachute[i] = (Action)Delegate.CreateDelegate(typeof(Action), vc.moduleRealChute[i], armChute_t);
                    cutRealChute[i] = (Action)Delegate.CreateDelegate(typeof(Action), vc.moduleRealChute[i], cutChute_t);
                    deployRealChute[i] = (Action)Delegate.CreateDelegate(typeof(Action), vc.moduleRealChute[i], deployChute_t);
                    disarmParachute[i] = (Action)Delegate.CreateDelegate(typeof(Action), vc.moduleRealChute[i], disarmChute_t);
                }
            }

            anyArmed = false;
            anyDeployed = false;
            allSafe = true;
            allDangerous = true;
            for (int i = 0; i < newLength; ++i)
            {
                if (getArmed(vc.moduleRealChute[i]))
                {
                    anyArmed = true;
                }
                if (getAnyDeployed[i]())
                {
                    anyDeployed = true;
                }

                object safetyState_o = safeState_t.GetValue(vc.moduleRealChute[i]);
                int safetyState = (int)safetyState_o;
                if (safetyState != (int)SafeState.SAFE)
                {
                    allSafe = false;
                }
                if (safetyState != (int)SafeState.DANGEROUS)
                {
                    allDangerous = false;
                }
            }

            if (!anyDeployed || (allSafe && allDangerous))
            {
                for (int i = vc.moduleParachute.Length - 1; i >= 0; --i)
                {
                    //automateSafeDeploy = 0: Deploy when safe; 1: Deploy when risky; 2: Deploy immediate
                    //Utility.LogMessage(this, "chute {0}: {1} / {2} / {3}", i, vc.moduleParachute[i].deploymentState, vc.moduleParachute[i].deploymentSafeState, vc.moduleParachute[i].automateSafeDeploy);
                    if (vc.moduleParachute[i].deploymentState == ModuleParachute.deploymentStates.DEPLOYED ||
                        vc.moduleParachute[i].deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED)
                    {
                        anyDeployed = true;
                    }
                    else if (vc.moduleParachute[i].deploymentState == ModuleParachute.deploymentStates.ACTIVE)
                    {
                        anyArmed = true;
                    }

                    if ((vc.moduleParachute[i].deploymentSafeState == ModuleParachute.deploymentSafeStates.RISKY) || (vc.moduleParachute[i].deploymentSafeState == ModuleParachute.deploymentSafeStates.UNSAFE))
                    {
                        allSafe = false;
                    }
                    if (vc.moduleParachute[i].deploymentSafeState != ModuleParachute.deploymentSafeStates.RISKY)
                    {
                        allDangerous = false;
                    }
                }
            }
        }

        #region Reflection Configuration
        static MASIParachute()
        {
            realChuteFound = false;
            rcAPI_t = Utility.GetExportedType("RealChute", "RealChute.RealChuteModule");
            if (rcAPI_t != null)
            {
                PropertyInfo rcAnyDeployed = rcAPI_t.GetProperty("AnyDeployed", BindingFlags.Instance | BindingFlags.Public);
                if (rcAnyDeployed == null)
                {
                    Utility.LogErrorMessage("rcAnyDeployed is null");
                    return;
                }
                getAnyDeployed_t = rcAnyDeployed.GetGetMethod();
                if (getAnyDeployed_t == null)
                {
                    Utility.LogErrorMessage("getAnyDeployed_t is null");
                    return;
                }

                armChute_t = rcAPI_t.GetMethod("GUIArm", BindingFlags.Instance | BindingFlags.Public);
                if (armChute_t == null)
                {
                    Utility.LogErrorMessage("armChute_t is null");
                    return;
                }

                disarmChute_t = rcAPI_t.GetMethod("GUIDisarm", BindingFlags.Instance | BindingFlags.Public);
                if (disarmChute_t == null)
                {
                    Utility.LogErrorMessage("disarmChute_t is null");
                    return;
                }

                deployChute_t = rcAPI_t.GetMethod("GUIDeploy", BindingFlags.Instance | BindingFlags.Public);
                if (deployChute_t == null)
                {
                    Utility.LogErrorMessage("deployChute_t is null");
                    return;
                }

                cutChute_t = rcAPI_t.GetMethod("GUICut", BindingFlags.Instance | BindingFlags.Public);
                if (cutChute_t == null)
                {
                    Utility.LogErrorMessage("cutChute_t is null");
                    return;
                }

                FieldInfo armed_t = rcAPI_t.GetField("armed", BindingFlags.Instance | BindingFlags.Public);
                if (armed_t == null)
                {
                    Utility.LogErrorMessage("armed_t is null");
                    return;
                }
                getArmed = DynamicMethodFactory.CreateGetField<object, bool>(armed_t);

                safeState_t = rcAPI_t.GetField("safeState", BindingFlags.Instance | BindingFlags.Public);
                if (safeState_t == null)
                {
                    Utility.LogErrorMessage("safeState_t is null");
                    return;
                }

                realChuteFound = true;
            }
        }
        #endregion
    }
}
