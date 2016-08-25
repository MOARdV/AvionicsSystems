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
    /// MASIMechJeb is the interface to MechJeb.
    /// </summary>
    /// <LuaName>mechjeb</LuaName>
    /// <mdDoc>MASIMechJeb provides the interface for controlling and querying
    /// MechJeb from Avionics Systems.</mdDoc>
    internal class MASIMechJeb
    {
        private static readonly bool mjFound;

        //--- Methods found in MechJebCore
        private static readonly Type mjCore_t;

        //--- Methods found in ComputerModule
        private static readonly DynamicMethodBool<object> ModuleEnabled;
        private static readonly FieldInfo ModuleUsers;
        private static readonly FieldInfo Target;

        //--- Methods found in EditableDoubleMult
        private static readonly DynamicMethod<object, double> setEditableDoubleMult;
        private static readonly DynamicMethodDouble<object> getEditableDoubleMult;

        //--- Methods found in OrbitalManeuverCalculator
        private static readonly DynamicMethodVec3d<Orbit, double, double> DeltaVToChangeApoapsis;
        private static readonly DynamicMethodVec3d<Orbit, double, double> DeltaVToChangePeriapsis;
        private static readonly DynamicMethodVec3d<Orbit, double> DeltaVToCircularize;

        //--- Methods found in VesselExtensions
        private static readonly Type mjVesselExtensions_t;
        private static readonly DynamicMethod<Vessel> GetMasterMechJeb;
        private static readonly DynamicMethod<object, string> GetComputerModule;
        private static readonly DynamicMethod<Vessel, Orbit, Vector3d, double> PlaceManeuverNode;

        //--- Methods found in MechJebModuleAscentAutopilot
        private static readonly FieldInfo desiredOrbitAltitude_t;
        private static readonly FieldInfo desiredOrbitInclination_t;

        //--- Methods found in ModuleAscentGuidance
        private static readonly FieldInfo desiredOrbitInclinationAG_t;

        //--- Methods found in ModuleLandingAutopilot
        private static readonly DynamicMethod<object, object> LandAtPositionTarget;
        private static readonly DynamicMethod<object, object> LandUntargeted;
        private static readonly DynamicMethod<object> StopLanding;

        //--- Methods found in ModuleNodeExecutor
        private static readonly DynamicMethod<object> AbortNode;
        private static readonly DynamicMethod<object, object> ExecuteOneNode;

        //--- Methods found in ModuleSmartASS
        private static readonly FieldInfo saTarget_t;
        private static readonly DynamicMethod<object, bool> Engage;
        internal static readonly string[] modeNames;

        //--- Methods found in ModuleTargetController
        private static readonly DynamicMethodBool<object> PositionTargetExists;
        private static readonly FieldInfo TargetLatitude;
        private static readonly FieldInfo TargetLongitude;
        private static readonly DynamicMethod<object> TargetOrbit;

        //--- Methods found in UserPool
        private static readonly DynamicMethod<object, object> AddUser;
        private static readonly DynamicMethod<object, object> RemoveUser;

        //--- Instance variables
        internal bool mjAvailable;

        Vessel vessel;
        Orbit vesselOrbit;

        bool landingPredictionEnabled;
        double landingAltitude;
        double landingLatitude;
        double landingLongitude;

        object masterMechJeb;
        object ascentAutopilot;
        object ascentGuidance;
        object landingAutopilot;
        object landingGuidance;
        object landingPrediction;
        object maneuverPlanner;
        object nodeExecutor;
        object rendezvousAutopilot;
        object rendezvousAutopilotWindow;
        object smartAss;

        private SATarget saTarget;

        #region MechJeb Enum Imports
        public enum SATarget
        {
            OFF = 0,
            KILLROT = 1,
            NODE = 2,
            SURFACE = 3,
            PROGRADE = 4,
            RETROGRADE = 5,
            NORMAL_PLUS = 6,
            NORMAL_MINUS = 7,
            RADIAL_PLUS = 8,
            RADIAL_MINUS = 9,
            RELATIVE_PLUS = 10,
            RELATIVE_MINUS = 11,
            TARGET_PLUS = 12,
            TARGET_MINUS = 13,
            PARALLEL_PLUS = 14,
            PARALLEL_MINUS = 15,
            ADVANCED = 16,
            AUTO = 17,
            SURFACE_PROGRADE = 18,
            SURFACE_RETROGRADE = 19,
            HORIZONTAL_PLUS = 20,
            HORIZONTAL_MINUS = 21,
            VERTICAL_PLUS = 22,
        }
        static private Dictionary<int, SATarget> saTargetMap = new Dictionary<int, SATarget>
        {
            { 0, SATarget.OFF },
            { 1, SATarget.KILLROT },
            { 2,SATarget.NODE },
            { 3,SATarget.SURFACE },
            { 4,SATarget.PROGRADE },
            { 5,SATarget.RETROGRADE },
            { 6,SATarget.NORMAL_PLUS },
            { 7,SATarget.NORMAL_MINUS },
            { 8,SATarget.RADIAL_PLUS },
            { 9,SATarget.RADIAL_MINUS },
            {10,SATarget.RELATIVE_PLUS },
            {11,SATarget.RELATIVE_MINUS },
            {12,SATarget.TARGET_PLUS },
            {13,SATarget.TARGET_MINUS },
            {14,SATarget.PARALLEL_PLUS },
            {15,SATarget.PARALLEL_MINUS },
            {16,SATarget.ADVANCED },
            {17,SATarget.AUTO },
            {18,SATarget.SURFACE_PROGRADE },
            {19,SATarget.SURFACE_RETROGRADE },
            {20,SATarget.HORIZONTAL_PLUS },
            {21,SATarget.HORIZONTAL_MINUS },
            {22,SATarget.VERTICAL_PLUS }
        };
        #endregion

        [MoonSharpHidden]
        public MASIMechJeb(Vessel vessel)
        {
            if (mjFound)
            {
                UpdateVessel(vessel);
            }
            else
            {
                mjAvailable = false;
            }
        }

        ~MASIMechJeb()
        {
            vesselOrbit = null;
        }

        /// <summary>
        /// The General category provides some non-specific queries and
        /// actions (such as whether
        /// MechJeb functionality is available on a given craft).
        /// </summary>
        #region General
        /// <summary>
        /// Returns 1 if any of the MechJeb autopilots MAS can control are active.
        /// </summary>
        /// <returns></returns>
        public double AutopilotActive()
        {
            if (mjAvailable && (ModuleEnabled(ascentAutopilot) || ModuleEnabled(landingAutopilot) || ModuleEnabled(nodeExecutor) || ModuleEnabled(rendezvousAutopilot)))
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if MechJeb is installed and available on this craft, 0 if it
        /// is not available.
        /// </summary>
        /// <returns></returns>
        public double Available()
        {
            return (mjAvailable) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if any managed autopilots are engaged, or if SmartASS is not OFF.
        /// </summary>
        /// <returns></returns>
        public double ComputerActive()
        {
            if (mjAvailable && (saTarget != SATarget.OFF || ModuleEnabled(ascentAutopilot) || ModuleEnabled(landingAutopilot) || ModuleEnabled(nodeExecutor) || ModuleEnabled(rendezvousAutopilot)))
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Resets / disables all auto pilots as well as SmartASS.
        /// </summary>
        public void Reset()
        {
            if (mjAvailable)
            {
                if (saTarget != SATarget.OFF)
                {
                    saTarget_t.SetValue(smartAss, 0);

                    Engage(smartAss, true);
                }

                if (ModuleEnabled(ascentAutopilot))
                {
                    RemoveUser(ModuleUsers.GetValue(ascentAutopilot), ascentGuidance);
                }

                if (ModuleEnabled(landingAutopilot))
                {
                    StopLanding(landingAutopilot);
                }

                if (ModuleEnabled(nodeExecutor))
                {
                    AbortNode(nodeExecutor);
                }

                if (ModuleEnabled(rendezvousAutopilot))
                {
                    RemoveUser(ModuleUsers.GetValue(rendezvousAutopilot), rendezvousAutopilotWindow);
                }
            }
        }
        #endregion

        /// <summary>
        /// The Ascent Autopilot and Guidance methods provide an interface to MechJeb's
        /// Ascent Autopilot.
        /// </summary>
        #region Ascent Autopilot and Guidance

        /// <summary>
        /// Returns 1 if the MJ Ascent Autopilot is enabled; 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double AscentAutopilotActive()
        {
            if (mjAvailable && ModuleEnabled(ascentAutopilot))
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the MJ Ascent Autopilot's targeted launch altitude in meters.
        /// </summary>
        /// <returns></returns>
        public double GetDesiredLaunchAltitude()
        {
            double desiredAltitude = 0.0;
            if (mjAvailable)
            {
                object desiredAlt = desiredOrbitAltitude_t.GetValue(ascentAutopilot);
                if (desiredAlt != null)
                {
                    desiredAltitude = getEditableDoubleMult(desiredAlt);
                }
            }

            return desiredAltitude;
        }

        /// <summary>
        /// Returns the MJ Ascent Autopilot's targeted orbital inclination in degrees.
        /// </summary>
        /// <returns></returns>
        public double GetDesiredLaunchInclination()
        {
            double desiredInclination = 0.0;
            if (mjAvailable)
            {
                object desiredInc = desiredOrbitInclination_t.GetValue(ascentAutopilot);
                desiredInclination = (double)desiredInc;
            }

            return desiredInclination;
        }

        /// <summary>
        /// Set the ascent guidance desired altitude.  Altitude is in meters.
        /// </summary>
        /// <param name="altitude"></param>
        public void SetDesiredLaunchAltitude(double altitude)
        {
            if (mjAvailable)
            {
                object desiredAlt = desiredOrbitAltitude_t.GetValue(ascentAutopilot);
                if (desiredAlt != null)
                {
                    setEditableDoubleMult(desiredAlt, altitude);
                }
            }
        }

        /// <summary>
        /// Set the desired launch inclination in the ascent guidance.
        /// </summary>
        /// <param name="inclination"></param>
        public void SetDesiredLaunchInclination(double inclination)
        {
            if (mjAvailable)
            {
                desiredOrbitInclination_t.SetValue(ascentAutopilot, inclination);
                // Just writing it to the autopilot doesn't update the ascent guidance gui...
                object desiredInc = desiredOrbitInclinationAG_t.GetValue(ascentGuidance);
                if (desiredInc != null)
                {
                    setEditableDoubleMult(desiredInc, inclination);
                }
            }
        }

        /// <summary>
        /// Toggles the Ascent Autopilot on or off.
        /// </summary>
        public void ToggleAscentAutopilot()
        {
            if (mjAvailable)
            {
                object users = ModuleUsers.GetValue(ascentAutopilot);

                if (ModuleEnabled(ascentAutopilot))
                {
                    RemoveUser(users, ascentGuidance);
                }
                else
                {
                    AddUser(users, ascentGuidance);
                }
            }
        }
        #endregion

        /// <summary>
        /// The Landing Autopilot and Computer methods provide an interface to MechJeb's
        /// Landing Autopilot.  It can also independently trigger the Landing Computer to
        /// provide landing predictions.
        /// </summary>
        #region Landing Autopilot and Computer
        /// <summary>
        /// Returns 1 if the MechJeb landing autopilot is engaged.
        /// </summary>
        /// <returns></returns>
        public double LandingAutopilotActive()
        {
            if (mjAvailable && ModuleEnabled(landingAutopilot))
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if the MechJeb landing prediction computer is engaged.
        /// </summary>
        /// <returns></returns>
        public double LandingComputerActive()
        {
            if (mjAvailable && landingPredictionEnabled)
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// When the landing prediction computer is engaged, returns the
        /// predicted altitude of the landing site.  Returns 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double LandingAltitude()
        {
            if (mjAvailable && landingPredictionEnabled)
            {
                return landingAltitude;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// When the landing prediction computer is engaged, returns the
        /// predicted latitude of the landing site.  Returns 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double LandingLatitude()
        {
            if (mjAvailable && landingPredictionEnabled)
            {
                return landingLatitude;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// When the landing prediction computer is engaged, returns the
        /// predicted longitude of the landing site.  Returns 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double LandingLongitude()
        {
            if (mjAvailable && landingPredictionEnabled)
            {
                return landingLongitude;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Toggles the MechJeb landing autopilot on/off.
        /// </summary>
        public void ToggleLandingAutopilot()
        {
            if (mjAvailable)
            {
                if (ModuleEnabled(landingAutopilot))
                {
                    StopLanding(landingAutopilot);
                }
                else
                {
                    object target = Target.GetValue(masterMechJeb);
                    if (PositionTargetExists(target))
                    {
                        LandAtPositionTarget(landingAutopilot, landingGuidance);
                    }
                    else
                    {
                        LandUntargeted(landingAutopilot, landingGuidance);
                    }
                }
            }
        }

        /// <summary>
        /// Toggles the MechJeb landing prediction computer on/off independently
        /// of the landing autopilot.
        /// </summary>
        public void ToggleLandingComputer()
        {
            if (mjAvailable)
            {
                object users = ModuleUsers.GetValue(landingPrediction);

                if (ModuleEnabled(landingPrediction))
                {
                    RemoveUser(users, landingGuidance);
                }
                else
                {
                    AddUser(users, landingGuidance);
                }
            }
        }
        #endregion

        /// <summary>
        /// TODO
        /// </summary>
        #region Maneuver Planner and Node Executor
        /// <summary>
        /// Change apoapsis to the specified altitude in meters.  Command is
        /// ignored if Ap &lt; Pe or the orbit is hyperbolic and the vessel is
        /// already past the Pe.
        /// </summary>
        /// <param name="newAp"></param>
        public void ChangeApoapsis(double newAp)
        {
            if (mjAvailable && newAp >= vesselOrbit.PeA && vessel.patchedConicSolver != null)
            {
                double nextPeriapsisTime = vesselOrbit.timeToPe;
                if (nextPeriapsisTime > 0.0)
                {
                    vessel.patchedConicSolver.maneuverNodes.Clear();

                    nextPeriapsisTime += Planetarium.GetUniversalTime();
                    Vector3d dV = DeltaVToChangeApoapsis(vesselOrbit, nextPeriapsisTime, vesselOrbit.referenceBody.Radius + newAp);

                    PlaceManeuverNode(vessel, vesselOrbit, dV, nextPeriapsisTime);
                }
            }
        }

        /// <summary>
        /// Change Periapsis to the new altitude in meters.  Command is ignored
        /// if Pe &gt; Ap.
        /// </summary>
        /// <param name="newPe"></param>
        public void ChangePeriapsis(double newPe)
        {
            if (mjAvailable && vesselOrbit.eccentricity < 1.0 && newPe <= vesselOrbit.ApA && vessel.patchedConicSolver != null)
            {
                double nextApoapsisTime = vesselOrbit.timeToAp;
                if (nextApoapsisTime > 0.0)
                {
                    vessel.patchedConicSolver.maneuverNodes.Clear();

                    nextApoapsisTime += Planetarium.GetUniversalTime();
                    Vector3d dV = DeltaVToChangePeriapsis(vesselOrbit, nextApoapsisTime, vesselOrbit.referenceBody.Radius + newPe);

                    PlaceManeuverNode(vessel, vesselOrbit, dV, nextApoapsisTime);
                }
            }
        }

        /// <summary>
        /// Circularize at the specified altitude, in meters.  Command is
        /// ignored if an invalid altitude is supplied.
        /// </summary>
        /// <param name="newAlt"></param>
        public void CircularizeAt(double newAlt)
        {
            if (mjAvailable && newAlt >= vesselOrbit.PeA && newAlt <= vesselOrbit.ApA && vessel.patchedConicSolver != null)
            {
                vessel.patchedConicSolver.maneuverNodes.Clear();

                double tA = vesselOrbit.TrueAnomalyAtRadius(newAlt + vesselOrbit.referenceBody.Radius);
                double nextAltTime = vesselOrbit.GetUTforTrueAnomaly(tA, vesselOrbit.period);

                Vector3d dV = DeltaVToCircularize(vesselOrbit, nextAltTime);
                PlaceManeuverNode(vessel, vesselOrbit, dV, nextAltTime);
            }
        }

        /// <summary>
        /// Returns 1 if the maneuver node executor is active, 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double ManeuverNodeExecutorActive()
        {
            if (mjAvailable && ModuleEnabled(nodeExecutor))
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Enables / disables Maneuver Node Executor
        /// </summary>
        public void ToggleManeuverNodeExecutor()
        {
            if (mjAvailable)
            {
                if (ModuleEnabled(nodeExecutor))
                {
                    AbortNode(nodeExecutor);
                }
                else
                {
                    ExecuteOneNode(nodeExecutor, maneuverPlanner);
                }
            }
        }
        #endregion

        /// <summary>
        /// TODO
        /// </summary>
        #region Rendezvous Autopilot
        /// <summary>
        /// Returns 1 if the rendezvous autopilot is engaged.
        /// </summary>
        /// <returns></returns>
        public double RendezvousAutopilotActive()
        {
            if (mjAvailable && ModuleEnabled(rendezvousAutopilot))
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Engages / disengages the MechJeb Rendezvous Autopilot
        /// </summary>
        public void ToggleRendezvousAutopilot()
        {
            if (mjAvailable)
            {
                object users = ModuleUsers.GetValue(rendezvousAutopilot);
                if (ModuleEnabled(rendezvousAutopilot))
                {
                    RemoveUser(users, rendezvousAutopilotWindow);
                }
                else
                {
                    AddUser(users, rendezvousAutopilotWindow);
                }
            }
        }
        #endregion

        /// <summary>
        /// TODO
        /// </summary>
        #region SmartASS
        /// <summary>
        /// Returns the number of the currently active SASS mode, or zero if MechJeb
        /// is unavailable.
        /// 
        /// * OFF = 0,
        /// * KILLROT = 1,
        /// * NODE = 2,
        /// * SURFACE = 3,
        /// * PROGRADE = 4,
        /// * RETROGRADE = 5,
        /// * NORMAL_PLUS = 6,
        /// * NORMAL_MINUS = 7,
        /// * RADIAL_PLUS = 8,
        /// * RADIAL_MINUS = 9,
        /// * RELATIVE_PLUS = 10,
        /// * RELATIVE_MINUS = 11,
        /// * TARGET_PLUS = 12,
        /// * TARGET_MINUS = 13,
        /// * PARALLEL_PLUS = 14,
        /// * PARALLEL_MINUS = 15,
        /// * ADVANCED = 16,
        /// * AUTO = 17,
        /// * SURFACE_PROGRADE = 18,
        /// * SURFACE_RETROGRADE = 19,
        /// * HORIZONTAL_PLUS = 20,
        /// * HORIZONTAL_MINUS = 21,
        /// * VERTICAL_PLUS = 22,
        /// </summary>
        /// <returns></returns>
        public double GetSASSMode()
        {
            if (mjAvailable)
            {
                return (double)(int)saTarget;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if the current SASS mode matches the listed value.
        /// 
        /// * OFF = 0,
        /// * KILLROT = 1,
        /// * NODE = 2,
        /// * SURFACE = 3,
        /// * PROGRADE = 4,
        /// * RETROGRADE = 5,
        /// * NORMAL_PLUS = 6,
        /// * NORMAL_MINUS = 7,
        /// * RADIAL_PLUS = 8,
        /// * RADIAL_MINUS = 9,
        /// * RELATIVE_PLUS = 10,
        /// * RELATIVE_MINUS = 11,
        /// * TARGET_PLUS = 12,
        /// * TARGET_MINUS = 13,
        /// * PARALLEL_PLUS = 14,
        /// * PARALLEL_MINUS = 15,
        /// * ADVANCED = 16,
        /// * AUTO = 17,
        /// * SURFACE_PROGRADE = 18,
        /// * SURFACE_RETROGRADE = 19,
        /// * HORIZONTAL_PLUS = 20,
        /// * HORIZONTAL_MINUS = 21,
        /// * VERTICAL_PLUS = 22,
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        public double GetSASSModeActive(double mode)
        {
            int mode_i = (int)mode;
            if (mjAvailable && saTargetMap.ContainsKey(mode_i) && saTarget == saTargetMap[mode_i])
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns true if SmartASS is off
        /// </summary>
        /// <returns></returns>
        public double SASSOff()
        {
            if (mjAvailable && saTarget == SATarget.OFF)
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Set the SmartASS pilot to the specified mode.  Some modes may not
        /// be 'settable', such as `AUTO` or `ADVANCED`.
        /// 
        /// * OFF = 0,
        /// * KILLROT = 1,
        /// * NODE = 2,
        /// * SURFACE = 3,
        /// * PROGRADE = 4,
        /// * RETROGRADE = 5,
        /// * NORMAL_PLUS = 6,
        /// * NORMAL_MINUS = 7,
        /// * RADIAL_PLUS = 8,
        /// * RADIAL_MINUS = 9,
        /// * RELATIVE_PLUS = 10,
        /// * RELATIVE_MINUS = 11,
        /// * TARGET_PLUS = 12,
        /// * TARGET_MINUS = 13,
        /// * PARALLEL_PLUS = 14,
        /// * PARALLEL_MINUS = 15,
        /// * ADVANCED = 16,
        /// * AUTO = 17,
        /// * SURFACE_PROGRADE = 18,
        /// * SURFACE_RETROGRADE = 19,
        /// * HORIZONTAL_PLUS = 20,
        /// * HORIZONTAL_MINUS = 21,
        /// * VERTICAL_PLUS = 22,
        /// </summary>
        /// <param name="mode"></param>
        public void SetSASSMode(double mode)
        {
            int mode_i = (int)mode;
            if (mjAvailable && saTargetMap.ContainsKey(mode_i))
            {
                saTarget_t.SetValue(smartAss, mode_i);

                Engage(smartAss, true);
            }
        }
        #endregion

        /// <summary>
        /// Do all of the internal processing we need to do per FixedUpdate.
        /// </summary>
        [MoonSharpHidden]
        internal void Update()
        {
            if (mjAvailable)
            {
                object activeSATarget = saTarget_t.GetValue(smartAss);
                saTarget = saTargetMap[(int)activeSATarget];

                landingPredictionEnabled = ModuleEnabled(landingPrediction);

                if (landingPredictionEnabled)
                {
                    landingAltitude = 0.0;
                    landingLatitude = 0.0;
                    landingLongitude = 0.0;
                }
                else
                {
                    landingAltitude = 0.0;
                    landingLatitude = 0.0;
                    landingLongitude = 0.0;
                }
            }
        }

        /// <summary>
        /// Vessel changed - we need to re-establish the master MechJeb and our
        /// delegates.
        /// </summary>
        /// <param name="vessel"></param>
        [MoonSharpHidden]
        internal void UpdateVessel(Vessel vessel)
        {
            if (mjFound)
            {
                this.vessel = vessel;
                this.vesselOrbit = vessel.GetOrbit();
                try
                {
                    masterMechJeb = GetMasterMechJeb(vessel);

                    if (masterMechJeb != null)
                    {
                        smartAss = GetComputerModule(masterMechJeb, "MechJebModuleSmartASS");
                        if (smartAss == null)
                        {
                            throw new Exception("MASIMechJeb: Failed to get SmartASS MJ module");
                        }

                        ascentAutopilot = GetComputerModule(masterMechJeb, "MechJebModuleAscentAutopilot");
                        if (ascentAutopilot == null)
                        {
                            throw new Exception("MASIMechJeb: Failed to get Ascent Autopilot MJ module");
                        }

                        ascentGuidance = GetComputerModule(masterMechJeb, "MechJebModuleAscentGuidance");
                        if (ascentGuidance == null)
                        {
                            throw new Exception("MASIMechJeb: Failed to get Ascent Guidance MJ module");
                        }

                        landingAutopilot = GetComputerModule(masterMechJeb, "MechJebModuleLandingAutopilot");
                        if (ascentGuidance == null)
                        {
                            throw new Exception("MASIMechJeb: Failed to get Landing Autopilot MJ module");
                        }

                        landingGuidance = GetComputerModule(masterMechJeb, "MechJebModuleLandingGuidance");
                        if (landingGuidance == null)
                        {
                            throw new Exception("MASIMechJeb: Failed to get Landing Guidance MJ module");
                        }

                        landingPrediction = GetComputerModule(masterMechJeb, "MechJebModuleLandingPredictions");
                        if (landingPrediction == null)
                        {
                            throw new Exception("MASIMechJeb: Failed to get Landing Prediction MJ module");
                        }

                        maneuverPlanner = GetComputerModule(masterMechJeb, "MechJebModuleManeuverPlanner");
                        if (maneuverPlanner == null)
                        {
                            throw new Exception("MASIMechJeb: Failed to get Maneuver Planner MJ module");
                        }

                        nodeExecutor = GetComputerModule(masterMechJeb, "MechJebModuleNodeExecutor");
                        if (nodeExecutor == null)
                        {
                            throw new Exception("MASIMechJeb: Failed to get Node Executor MJ module");
                        }

                        rendezvousAutopilot = GetComputerModule(masterMechJeb, "MechJebModuleRendezvousAutopilot");
                        if (rendezvousAutopilot == null)
                        {
                            throw new Exception("MASIMechJeb: Failed to get Rendezvous Autopilot MJ module");
                        }

                        rendezvousAutopilotWindow = GetComputerModule(masterMechJeb, "MechJebModuleRendezvousAutopilotWindow");
                        if (rendezvousAutopilotWindow == null)
                        {
                            throw new Exception("MASIMechJeb: Failed to get Rendezvous Autopilot Window MJ module");
                        }
                    }

                    mjAvailable = (masterMechJeb != null);
                }
                catch (Exception e)
                {
                    Utility.LogErrorMessage(this, "mechjeb.UpdateVessel threw exception: {0}", e);
                    mjAvailable = false;
                }
            }
        }

        #region Reflection Configuration
        static MASIMechJeb()
        {
            // Spaghetti code: I wanted to use readonly qualifiers on the static
            // variables, but that requires me to do all of this in the static
            // constructor.
            mjFound = false;
            try
            {
                mjCore_t = Utility.GetExportedType("MechJeb2", "MuMech.MechJebCore");
                if (mjCore_t == null)
                {
                    return;
                }
                mjVesselExtensions_t = Utility.GetExportedType("MechJeb2", "MuMech.VesselExtensions");
                if (mjVesselExtensions_t == null)
                {
                    return;
                }
                Type mjComputerModule_t = Utility.GetExportedType("MechJeb2", "MuMech.ComputerModule");
                if (mjComputerModule_t == null)
                {
                    return;
                }
                Type mjEditableDoubleMult_t = Utility.GetExportedType("MechJeb2", "MuMech.EditableDoubleMult");
                if (mjEditableDoubleMult_t == null)
                {
                    return;
                }
                Type mjModuleSmartass_t = Utility.GetExportedType("MechJeb2", "MuMech.MechJebModuleSmartASS");
                if (mjModuleSmartass_t == null)
                {
                    return;
                }
                Type mjModuleAscentAP_t = Utility.GetExportedType("MechJeb2", "MuMech.MechJebModuleAscentAutopilot");
                if (mjModuleAscentAP_t == null)
                {
                    return;
                }
                Type mjModuleAscentGuid_t = Utility.GetExportedType("MechJeb2", "MuMech.MechJebModuleAscentGuidance");
                if (mjModuleAscentGuid_t == null)
                {
                    return;
                }
                Type mjLandingAutopilot_t = Utility.GetExportedType("MechJeb2", "MuMech.MechJebModuleLandingAutopilot");
                if (mjLandingAutopilot_t == null)
                {
                    return;
                }
                Type mjNodeExecutor_t = Utility.GetExportedType("MechJeb2", "MuMech.MechJebModuleNodeExecutor");
                if (mjNodeExecutor_t == null)
                {
                    throw new ArgumentNullException("mjNodeExecutor_t");
                }
                Type mjOrbitalManeuverCalculator_t = Utility.GetExportedType("MechJeb2", "MuMech.OrbitalManeuverCalculator");
                if (mjOrbitalManeuverCalculator_t == null)
                {
                    throw new ArgumentNullException("mjOrbitalManeuverCalculator_t");
                }
                Type mjModuleTargetController_t = Utility.GetExportedType("MechJeb2", "MuMech.MechJebModuleTargetController");
                if (mjModuleTargetController_t == null)
                {
                    throw new ArgumentNullException("mjModuleTargetController_t");
                }
                Type mjUserPool_t = Utility.GetExportedType("MechJeb2", "MuMech.UserPool");
                if (mjUserPool_t == null)
                {
                    throw new ArgumentNullException("mjUserPool_t");
                }

                //--- MechJebCore
                MethodInfo GetComputerModule_t = mjCore_t.GetMethod("GetComputerModule", new Type[] { typeof(string) });
                if (GetComputerModule_t == null)
                {
                    return;
                }
                GetComputerModule = DynamicMethodFactory.CreateFunc<object, string>(GetComputerModule_t);
                if (GetComputerModule == null)
                {
                    return;
                }
                Target = mjCore_t.GetField("target", BindingFlags.Instance | BindingFlags.Public);
                if (Target == null)
                {
                    throw new ArgumentNullException("Target");
                }

                //--- ComputerModule
                PropertyInfo mjModuleEnabledProperty = mjComputerModule_t.GetProperty("enabled", BindingFlags.Instance | BindingFlags.Public);
                MethodInfo mjModuleEnabled = null;
                if (mjModuleEnabledProperty != null)
                {
                    mjModuleEnabled = mjModuleEnabledProperty.GetGetMethod();
                }
                if (mjModuleEnabled == null)
                {
                    return;
                }
                ModuleEnabled = DynamicMethodFactory.CreateFuncBool<object>(mjModuleEnabled);
                ModuleUsers = mjComputerModule_t.GetField("users", BindingFlags.Instance | BindingFlags.Public);
                if (ModuleUsers == null)
                {
                    throw new ArgumentNullException("ModuleUsers");
                }

                //--- EditableDoubleMult
                PropertyInfo edmVal = mjEditableDoubleMult_t.GetProperty("val");
                if (edmVal == null)
                {
                    throw new ArgumentNullException("edmVal");
                }
                // getEditableDoubleMult
                MethodInfo mjGetEDM = edmVal.GetGetMethod();
                if (mjGetEDM != null)
                {
                    getEditableDoubleMult = DynamicMethodFactory.CreateFuncDouble<object>(mjGetEDM);
                }
                // setEditableDoubleMult
                MethodInfo mjSetEDM = edmVal.GetSetMethod();
                if (mjSetEDM != null)
                {
                    setEditableDoubleMult = DynamicMethodFactory.CreateFunc<object, double>(mjSetEDM);
                }

                //--- ModuleAscentAutoPilot
                desiredOrbitAltitude_t = mjModuleAscentAP_t.GetField("desiredOrbitAltitude");
                if (desiredOrbitAltitude_t == null)
                {
                    return;
                }
                desiredOrbitInclination_t = mjModuleAscentAP_t.GetField("desiredInclination");
                if (desiredOrbitInclination_t == null)
                {
                    return;
                }

                //--- ModuleAscentGuidance
                desiredOrbitInclinationAG_t = mjModuleAscentGuid_t.GetField("desiredInclination");
                if (desiredOrbitInclinationAG_t == null)
                {
                    return;
                }

                //--- ModuleLandingAutopilot
                MethodInfo mjLandAtPositionTarget = mjLandingAutopilot_t.GetMethod("LandAtPositionTarget", BindingFlags.Instance | BindingFlags.Public);
                if (mjLandAtPositionTarget == null)
                {
                    throw new NotImplementedException("mjLandAtPositionTarget");
                }
                LandAtPositionTarget = DynamicMethodFactory.CreateFunc<object, object>(mjLandAtPositionTarget);
                MethodInfo mjLandUntargeted = mjLandingAutopilot_t.GetMethod("LandUntargeted", BindingFlags.Instance | BindingFlags.Public);
                if (mjLandUntargeted == null)
                {
                    throw new NotImplementedException("mjLandUntargeted");
                }
                LandUntargeted = DynamicMethodFactory.CreateFunc<object, object>(mjLandUntargeted);
                MethodInfo mjStopLanding = mjLandingAutopilot_t.GetMethod("StopLanding", BindingFlags.Instance | BindingFlags.Public);
                if (mjStopLanding == null)
                {
                    throw new NotImplementedException("mjStopLanding");
                }
                StopLanding = DynamicMethodFactory.CreateFunc<object>(mjStopLanding);

                //--- ModuleNodeExecutor
                MethodInfo mjExecuteOneNode = mjNodeExecutor_t.GetMethod("ExecuteOneNode", BindingFlags.Instance | BindingFlags.Public);
                if (mjExecuteOneNode == null)
                {
                    throw new NotImplementedException("mjExecuteOneNode");
                }
                ExecuteOneNode = DynamicMethodFactory.CreateFunc<object, object>(mjExecuteOneNode);
                MethodInfo mjAbortNode = mjNodeExecutor_t.GetMethod("Abort", BindingFlags.Instance | BindingFlags.Public);
                if (mjAbortNode == null)
                {
                    throw new NotImplementedException("mjAbortNode");
                }
                AbortNode = DynamicMethodFactory.CreateFunc<object>(mjAbortNode);

                //--- ModuleSmartASS
                saTarget_t = mjModuleSmartass_t.GetField("target", BindingFlags.Instance | BindingFlags.Public);
                if (saTarget_t == null)
                {
                    return;
                }
                FieldInfo modeTexts_t = mjModuleSmartass_t.GetField("ModeTexts", BindingFlags.Static | BindingFlags.Public);
                modeNames = (string[])modeTexts_t.GetValue(null);
                MethodInfo mjSmartassEngage = mjModuleSmartass_t.GetMethod("Engage", BindingFlags.Instance | BindingFlags.Public);
                if (mjSmartassEngage == null)
                {
                    throw new NotImplementedException("mjSmartassEngage");
                }
                Engage = DynamicMethodFactory.CreateFunc<object, bool>(mjSmartassEngage);

                //--- ModuleTargetController
                TargetLongitude = mjModuleTargetController_t.GetField("targetLongitude", BindingFlags.Instance | BindingFlags.Public);
                if (TargetLongitude == null)
                {
                    throw new NotImplementedException("TargetLongitude");
                }
                TargetLatitude = mjModuleTargetController_t.GetField("targetLatitude", BindingFlags.Instance | BindingFlags.Public);
                if (TargetLatitude == null)
                {
                    throw new NotImplementedException("TargetLatitude");
                }
                PropertyInfo mjPositionTargetExists = mjModuleTargetController_t.GetProperty("PositionTargetExists", BindingFlags.Instance | BindingFlags.Public);
                MethodInfo mjGetPositionTargetExists = null;
                if (mjPositionTargetExists != null)
                {
                    mjGetPositionTargetExists = mjPositionTargetExists.GetGetMethod();
                }
                if (mjGetPositionTargetExists == null)
                {
                    throw new NotImplementedException("mjGetPositionTargetExists");
                }
                PositionTargetExists = DynamicMethodFactory.CreateFuncBool<object>(mjGetPositionTargetExists);
                //PropertyInfo mjNormalTargetExists = mjModuleTargetController_t.GetProperty("NormalTargetExists", BindingFlags.Instance | BindingFlags.Public);
                //MethodInfo mjGetNormalTargetExists = null;
                //if (mjNormalTargetExists != null)
                //{
                //    mjGetNormalTargetExists = mjNormalTargetExists.GetGetMethod();
                //}
                //if (mjGetNormalTargetExists == null)
                //{
                //    throw new NotImplementedException("mjGetNormalTargetExists");
                //}
                //getNormalTargetExists = DynamicMethodDelegateFactory.CreateFuncBool(mjGetNormalTargetExists);
                PropertyInfo mjTargetOrbit = mjModuleTargetController_t.GetProperty("TargetOrbit", BindingFlags.Instance | BindingFlags.Public); ;
                MethodInfo mjGetTargetOrbit = null;
                if (mjTargetOrbit != null)
                {
                    mjGetTargetOrbit = mjTargetOrbit.GetGetMethod();
                }
                if (mjGetTargetOrbit == null)
                {
                    throw new NotImplementedException("mjGetTargetOrbit");
                }
                TargetOrbit = DynamicMethodFactory.CreateFunc<object>(mjGetTargetOrbit);

                //--- OrbitalManeuverCalculator
                MethodInfo deltaVToChangePeriapsis = mjOrbitalManeuverCalculator_t.GetMethod("DeltaVToChangePeriapsis", BindingFlags.Static | BindingFlags.Public);
                if (deltaVToChangePeriapsis == null)
                {
                    throw new ArgumentNullException("deltaVToChangePeriapsis");
                }
                DeltaVToChangePeriapsis = DynamicMethodFactory.CreateFuncVec3d<Orbit, double, double>(deltaVToChangePeriapsis);

                MethodInfo deltaVToChangeApoapsis = mjOrbitalManeuverCalculator_t.GetMethod("DeltaVToChangeApoapsis", BindingFlags.Static | BindingFlags.Public);
                if (deltaVToChangeApoapsis == null)
                {
                    throw new ArgumentNullException("deltaVToChangeApoapsis");
                }
                DeltaVToChangeApoapsis = DynamicMethodFactory.CreateFuncVec3d<Orbit, double, double>(deltaVToChangeApoapsis);

                MethodInfo deltaVToChangeCircularize = mjOrbitalManeuverCalculator_t.GetMethod("DeltaVToCircularize", BindingFlags.Static | BindingFlags.Public);
                if (deltaVToChangeCircularize == null)
                {
                    throw new ArgumentNullException("deltaVToChangeCircularize");
                }
                DeltaVToCircularize = DynamicMethodFactory.CreateFuncVec3d<Orbit, double>(deltaVToChangeCircularize);

                //--- UserPool
                MethodInfo mjAddUser = mjUserPool_t.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
                if (mjAddUser == null)
                {
                    throw new NotImplementedException("mjAddUser");
                }
                AddUser = DynamicMethodFactory.CreateFunc<object, object>(mjAddUser);
                MethodInfo mjRemoveUser = mjUserPool_t.GetMethod("Remove", BindingFlags.Instance | BindingFlags.Public);
                if (mjRemoveUser == null)
                {
                    throw new NotImplementedException("mjRemoveUser");
                }
                RemoveUser = DynamicMethodFactory.CreateFunc<object, object>(mjRemoveUser);

                //--- VesselExtensions
                MethodInfo GetMasterMechJeb_t = mjVesselExtensions_t.GetMethod("GetMasterMechJeb", BindingFlags.Static | BindingFlags.Public);
                if (GetMasterMechJeb_t == null)
                {
                    return;
                }
                GetMasterMechJeb = DynamicMethodFactory.CreateFunc<Vessel>(GetMasterMechJeb_t);
                if (GetMasterMechJeb == null)
                {
                    return;
                }
                MethodInfo mjPlaceManeuverNode = mjVesselExtensions_t.GetMethod("PlaceManeuverNode", BindingFlags.Static | BindingFlags.Public);
                if (mjPlaceManeuverNode == null)
                {
                    throw new NotImplementedException("mjPlaceManeuverNode");
                }
                PlaceManeuverNode = DynamicMethodFactory.CreateFunc<Vessel, Orbit, Vector3d, double>(mjPlaceManeuverNode);

                mjFound = true;
            }
            catch (Exception e)
            {
                Utility.LogErrorMessage("MJ static ctor exception {0}", e);
            }
        }
        #endregion
    }
}
