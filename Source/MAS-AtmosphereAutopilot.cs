//#define DEBUG_REGISTERS
//#define ATMOSPHERE_AUTOPILOT
/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2023 TommyAtkins
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

using UnityEngine;
using AtmosphereAutopilot;
using System;
using static AtmosphereAutopilot.VectorArray;
using MoonSharp.Interpreter;
using System.Diagnostics;

namespace AvionicsSystems
{
    class MASAAException : Exception
    {
        public MASAAException(string message)
        {
        }
    }
    internal class MASIAtmosphereAutopilot
    {
        internal Vessel vessel = null;
        internal MASVesselComputer vc = null;

        #region AAData
        private AtmosphereAutopilot.AtmosphereAutopilot aa = AtmosphereAutopilot.AtmosphereAutopilot.Instance;

        private TopModuleManager masterAP = null;
        private StandardFlyByWire fbwAP = null;
        private MASDirector dcAP = null;
        private CruiseController ccAP = null;
        private ProgradeThrustController speedAP = null;

        private bool CoordTurn = false;
        private bool RocketMode = false;

        private bool PseudoFLC = true;
        private double FLCMargin = double.NaN;
        private double MaxClimbAngle = double.NaN;
        private float HeadingSetPoint = -1f;
        private float AltitudeSetPoint = -1f;
        private float VertSpeedSetPoint = float.NaN;
        private float VertAngleSetPoint = float.NaN;
        private float LongitudeSetPoint = float.NaN;
        private float LatitudeSetPoint = float.NaN;
        ///private GeoCoordinates WaypointSetPoint = null; 

        private Vector3 DirectionSetPoint = Vector3.zero;

        private float SpeedSetPoint = -1f;
        #endregion

        public bool Available()
        {
            return AtmosphereAutopilot.AtmosphereAutopilot.Instance != null;
        }
        private AutopilotModule GetAPModule(Type type)
        {
            var apModules = aa.getVesselModules(aa.ActiveVessel);

            if (apModules.ContainsKey(type))
                return apModules[type];
            else
                throw new MASAAException("Unable to get autopilot from AtmosphereAutopilot.");
        }
        private void GetMasterAP()
        {
            masterAP = GetAPModule(typeof(TopModuleManager)) as TopModuleManager;
        }
        #region Craft settings
        private bool GetModerateAoA()
        {
            PitchAngularVelocityController pvc = GetAPModule(typeof(PitchAngularVelocityController)) as PitchAngularVelocityController;
            return pvc.moderate_aoa;
        }
        private void SetModerateAoA(bool value)
        {
            PitchAngularVelocityController pvc = GetAPModule(typeof(PitchAngularVelocityController)) as PitchAngularVelocityController;
            pvc.moderate_aoa = value;
        }
        private float GetMaxAoA()
        {
            PitchAngularVelocityController pvc = GetAPModule(typeof(PitchAngularVelocityController)) as PitchAngularVelocityController;
            return pvc.max_aoa;
        }
        private void SetMaxAoA(float value)
        {
            PitchAngularVelocityController pvc = GetAPModule(typeof(PitchAngularVelocityController)) as PitchAngularVelocityController;
            pvc.max_aoa = value;
        }
        private bool GetModerateG()
        {
            PitchAngularVelocityController pvc = GetAPModule(typeof(PitchAngularVelocityController)) as PitchAngularVelocityController;
            return pvc.moderate_g;
        }
        private void SetModerateG(bool value)
        {
            PitchAngularVelocityController pvc = GetAPModule(typeof(PitchAngularVelocityController)) as PitchAngularVelocityController;
            pvc.moderate_g = value;
        }
        private float GetMaxG()
        {
            PitchAngularVelocityController pvc = GetAPModule(typeof(PitchAngularVelocityController)) as PitchAngularVelocityController;
            return pvc.max_g_force;
        }
        private void SetMaxG(float value)
        {
            PitchAngularVelocityController pvc = GetAPModule(typeof(PitchAngularVelocityController)) as PitchAngularVelocityController;
            pvc.max_g_force = value;
        }
        private bool GetModerateSideSlip()
        {
            YawAngularVelocityController yvc = GetAPModule(typeof(YawAngularVelocityController)) as YawAngularVelocityController;
            return yvc.moderate_aoa;
        }
        private void SetModerateSideSlip(bool value)
        {
            YawAngularVelocityController yvc = GetAPModule(typeof(YawAngularVelocityController)) as YawAngularVelocityController;
            yvc.moderate_aoa = value;
        }
        private float GetMaxSideSlip()
        {
            YawAngularVelocityController yvc = GetAPModule(typeof(YawAngularVelocityController)) as YawAngularVelocityController;
            return yvc.max_aoa;
        }
        private void SetMaxSideSlip(float value)
        {
            YawAngularVelocityController yvc = GetAPModule(typeof(YawAngularVelocityController)) as YawAngularVelocityController;
            yvc.max_aoa = value;
        }
        private bool GetModerateSideG()
        {
            YawAngularVelocityController yvc = GetAPModule(typeof(YawAngularVelocityController)) as YawAngularVelocityController;
            return yvc.moderate_g;
        }
        private void SetModerateSideG(bool value)
        {
            YawAngularVelocityController yvc = GetAPModule(typeof(YawAngularVelocityController)) as YawAngularVelocityController;
            yvc.moderate_g = value;
        }
        private float GetMaxSideG()
        {
            YawAngularVelocityController yvc = GetAPModule(typeof(YawAngularVelocityController)) as YawAngularVelocityController;
            return yvc.max_g_force;
        }
        private void SetMaxSideG(float value)
        {
            YawAngularVelocityController yvc = GetAPModule(typeof(YawAngularVelocityController)) as YawAngularVelocityController;
            yvc.max_g_force = value;
        }
        private float GetPitchRateLimit()
        {
            PitchAngularVelocityController pvc = GetAPModule(typeof(PitchAngularVelocityController)) as PitchAngularVelocityController;
            return pvc.max_v_construction;
        }
        private void SetPitchRateLimit(float value)
        {
            PitchAngularVelocityController pvc = GetAPModule(typeof(PitchAngularVelocityController)) as PitchAngularVelocityController;
            pvc.max_v_construction = value;
        }
        private float GetYawRateLimit()
        {
            YawAngularVelocityController yvc = GetAPModule(typeof(YawAngularVelocityController)) as YawAngularVelocityController;
            return yvc.max_v_construction;
        }
        private void SetYawRateLimit(float value)
        {
            YawAngularVelocityController yvc = GetAPModule(typeof(YawAngularVelocityController)) as YawAngularVelocityController;
            yvc.max_v_construction = value;
        }
        private float GetRollRateLimit()
        {
            RollAngularVelocityController rvc = GetAPModule(typeof(RollAngularVelocityController)) as RollAngularVelocityController;
            return rvc.max_v_construction;
        }
        private void SetRollRateLimit(float value)
        {
            RollAngularVelocityController rvc = GetAPModule(typeof(RollAngularVelocityController)) as RollAngularVelocityController;
            rvc.max_v_construction = value;
        }
        private bool GetWingLeveler()
        {
            RollAngularVelocityController rvc = GetAPModule(typeof(RollAngularVelocityController)) as RollAngularVelocityController;
            return rvc.wing_leveler;
        }
        private void SetWingLeveler(bool value)
        {
            RollAngularVelocityController rvc = GetAPModule(typeof(RollAngularVelocityController)) as RollAngularVelocityController;
            rvc.wing_leveler = value;
        }
        #endregion
        #region FBW
        private double SetFBWActive(bool value)
        {
            if (value)
            {
                GetMasterAP();
                fbwAP = masterAP.activateAutopilot(typeof(StandardFlyByWire)) as StandardFlyByWire;
                dcAP = null;
                ccAP = null;
                if (SpeedSetPoint < 0f)
                {
                    ResetSpeedControl();
                }
                fbwAP.coord_turn = CoordTurn;
                fbwAP.rocket_mode = RocketMode;
                return 1.0;
            }
            else
            {
                if (fbwAP != null)
                    masterAP.Active = false;
                fbwAP = null;
                return 1.0;
            }
        }
        public double ToggleFBWActive()
        {
            return SetFBWActive(fbwAP == null);
        }

        public bool GetFBWActive()
        {
            if (fbwAP != null)
            {
                return fbwAP.Active;
            }
            else
            {
                return false;
            }
        }

        private void SetCoordTurn(bool value)
        {
            CoordTurn = value;
            if (fbwAP != null)
            {
                fbwAP.coord_turn = CoordTurn;
            }
        }
        private void SetRocketMode(bool value)
        {
            RocketMode = value;
            if (fbwAP != null)
            {
                fbwAP.rocket_mode = RocketMode;
            }
        }
        #endregion
        #region Director
        private void SetDirectorActive(bool value)
        {
            if (value)
            {
                GetMasterAP();
                fbwAP = null;
                dcAP = masterAP.activateAutopilot(typeof(MASDirector)) as MASDirector;
                ccAP = null;

                if (DirectionSetPoint.sqrMagnitude < 0.9)
                    //DirectionSetPoint = shared.Vessel.ReferenceTransform.forward;
                    DirectionSetPoint = aa.ActiveVessel.ReferenceTransform.forward;

                dcAP.target_direction = DirectionSetPoint;
                if (SpeedSetPoint < 0f)
                {
                    ResetSpeedControl();
                }
            }
            else
            {
                if (dcAP != null)
                    masterAP.Active = false;
                dcAP = null;
            }
        }
        private void SetDirectionSetPoint(Vector3 value)
        {
            DirectionSetPoint = value.normalized;
            if (dcAP != null)
            {
                dcAP.target_direction = DirectionSetPoint;
            }
        }
        private double GetDirectorStrength()
        {
            DirectorController dc = GetAPModule(typeof(DirectorController)) as DirectorController;
            return dc.strength;
        }
        private void SetDirectorStrength(float value)
        {
            DirectorController dc = GetAPModule(typeof(DirectorController)) as DirectorController;
            dc.strength = value;
        }
        #endregion
        #region Cruise
        private void SetCruiseMode(double mode)
        {
            if (mode == 2.0 && !float.IsNaN(LongitudeSetPoint) && !float.IsNaN(LatitudeSetPoint))
            {
                ccAP.current_mode = CruiseController.CruiseMode.Waypoint;
                ccAP.desired_latitude.Value = LatitudeSetPoint;
                ccAP.desired_longitude.Value = LongitudeSetPoint;
            }
            else if (mode == 1.0 && HeadingSetPoint >= 0f)
            {
                ccAP.current_mode = CruiseController.CruiseMode.CourseHold;
                ccAP.desired_course.Value = HeadingSetPoint;
            }
            else if (mode == 0.0)
            {
                ccAP.current_mode = CruiseController.CruiseMode.LevelFlight;
                ccAP.circle_axis = Vector3d.Cross(aa.ActiveVessel.srf_velocity, aa.ActiveVessel.GetWorldPos3D() - aa.ActiveVessel.mainBody.position).normalized;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(mode), "must be between 0 and 2 inclusive!");
            }
        }

        public double SwitchHNAVMode(double mode)
        {
            try
            {
                SetCruiseMode(mode);
                return 1.0;
            }
            catch (ArgumentOutOfRangeException e)
            {
                Utility.LogError(this, e.ToString());
                return 0.0;
            }
        }

        public bool GetCruiseActive()
        {
            if (ccAP != null)
            {
                return ccAP.Active;
            }
            else
            {
                return false;
            }
        }

        private double SetCruiseActive(bool value)
        {
            if (value)
            {
                if (ccAP == null)
                {
                    GetMasterAP();
                    fbwAP = null;
                    dcAP = null;
                    ccAP = masterAP.activateAutopilot(typeof(CruiseController)) as CruiseController;
                    CruiseController.use_keys = false;

                    if (double.IsNaN(FLCMargin))
                        FLCMargin = ccAP.flc_margin;
                    else
                        ccAP.flc_margin = FLCMargin;
                    if (double.IsNaN(MaxClimbAngle))
                        MaxClimbAngle = ccAP.max_climb_angle;
                    else
                        ccAP.max_climb_angle = MaxClimbAngle;

                    SetCruiseMode(0.0);

                    if (AltitudeSetPoint >= 0f)
                    {
                        ccAP.height_mode = CruiseController.HeightMode.Altitude;
                        ccAP.desired_altitude.Value = AltitudeSetPoint;
                    }
                    else
                    {
                        ccAP.height_mode = CruiseController.HeightMode.Altitude;
                        AltitudeSetPoint = 1000f;
                        ccAP.desired_altitude.Value = AltitudeSetPoint;
                    }
                    ccAP.vertical_control = true;
                    ccAP.pseudo_flc = PseudoFLC;
                    if (SpeedSetPoint < 0f)
                    {
                        ResetSpeedControl();
                    }
                }

                return 1.0;
            }
            else
            {
                if (ccAP != null)
                    masterAP.Active = false;
                ccAP = null;

                return 1.0;
            }
        }
        public double ToggleCruiseActive()
        {
            return SetCruiseActive(ccAP == null);
        }

        private void SetPseudoFLC(bool value)
        {
            PseudoFLC = value;
            if (ccAP != null)
            {
                ccAP.pseudo_flc = PseudoFLC;
            }
        }
        public double SetHeadingSetPoint(double value)
        {
            HeadingSetPoint = (float)value;
            if (ccAP != null)
            {
                //LongitudeSetPoint = float.NaN;
                //LatitudeSetPoint = float.NaN;
                //SetCruiseMode(1.0);
                ccAP.desired_course.Value = HeadingSetPoint;
                return 1.0;
            }
            else 
            { 
                return 0.0; 
            }
           
        }

        public double GetHeadingSetPoint()
        {
            return HeadingSetPoint;
        }

        /*private void SetWaypointSetPoint(GeoCoordinates value)
        {
            WaypointSetPoint = value;
            if (ccAP != null)
            {
                HeadingSetPoint = -1f;
                SetCruiseMode();
            }
        }*/
        public double SetLongitudeSetPoint(double value)
        {
            LongitudeSetPoint = (float)value;
            if (ccAP != null)
            {
                //HeadingSetPoint = -1f;
                //SetCruiseMode(2.0);
                ccAP.desired_longitude.Value = LongitudeSetPoint;
                return 1.0;
            }
            else
            {
                return 0.0; 
            }
        }
        public double SetLatitudeSetPoint(double value)
        {
            LatitudeSetPoint = (float)value;
            if (ccAP != null)
            {
                //HeadingSetPoint = -1f;
                //SetCruiseMode(2.0);
                ccAP.desired_latitude.Value = LatitudeSetPoint;
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        public double GetLongitudeSetPoint()
        {
            if (float.IsNaN(LongitudeSetPoint))
            {
                return 0.0;
            }
            else
            {
                return LongitudeSetPoint;
            }

        }

        public double GetLatitudeSetPoint()
        {
            if (float.IsNaN(LatitudeSetPoint))
            {
                return 0.0;
            }
            else
            {
                return LatitudeSetPoint;
            }

        }

        private void SetVerticalMode(double mode)
        {
            if (mode == 2.0 && !float.IsNaN(VertAngleSetPoint))
            {
                ccAP.height_mode = CruiseController.HeightMode.FlightPathAngle;
                ccAP.desired_vertsetpoint.Value = VertAngleSetPoint;
            }
            else if (mode == 1.0 && VertSpeedSetPoint >= 0f)
            {
                ccAP.height_mode = CruiseController.HeightMode.VerticalSpeed;
                ccAP.desired_vertsetpoint.Value = VertSpeedSetPoint;
            }
            else if (mode == 0.0)
            {
                ccAP.height_mode = CruiseController.HeightMode.Altitude;
                ccAP.desired_altitude.Value = AltitudeSetPoint;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(mode), "must be between 0 and 2 inclusive! Value is: " + mode.ToString());
            }
        }

        public double SwitchVNAVMode(double mode)
        {
            try
            {
                SetVerticalMode(mode);
                return 1.0;
            }
            catch (ArgumentOutOfRangeException e)
            {
                Utility.LogError(this, e.ToString());
                return 0.0;
            }
        }

        public double SetAltitudeSetPoint(double value)
        {
            AltitudeSetPoint = (float)value;
            if (ccAP != null)
            {
                if (AltitudeSetPoint >= 0f)
                {
                    //ccAP.height_mode = CruiseController.HeightMode.Altitude;
                    ccAP.desired_altitude.Value = AltitudeSetPoint;
                    return 1.0;
                }
                else
                {
                    ccAP.height_mode = CruiseController.HeightMode.VerticalSpeed;
                    ccAP.desired_vertsetpoint.Value = 0f;
                    return 0.0;
                }
            }
            else
            {
                return 0.0;
            }
            
        }

        public double GetAltitudeSetPoint()
        {
            return (double)AltitudeSetPoint;
        }

        public double SetVertSpeedSetPoint(double value)
        {
            VertSpeedSetPoint = (float)value;
            if (ccAP != null)
            {
                //ccAP.height_mode = CruiseController.HeightMode.VerticalSpeed;
                ccAP.desired_vertsetpoint.Value = VertSpeedSetPoint;
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        public double SetVertAngleSetPoint(double value)
        {
            VertAngleSetPoint = (float)value;
            if (ccAP != null)
            {
                //ccAP.height_mode = CruiseController.HeightMode.FlightPathAngle;
                ccAP.desired_vertsetpoint.Value = VertAngleSetPoint;
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        public double GetVertSpeedSetPoint()
        {
            if (float.IsNaN(VertSpeedSetPoint))
            {
                return 0.0;
            }
            else
            {
                return VertSpeedSetPoint;
            }

        }

        public double GetVertAngleSetPoint()
        {
            if (float.IsNaN(VertAngleSetPoint))
            {
                return 0.0;
            }
            else
            {
                return VertAngleSetPoint;
            }

        }

        private void SetFLCMargin(float value)
        {
            FLCMargin = value;
            if (ccAP != null)
                ccAP.flc_margin = FLCMargin;
        }
        private void SetMaxClimbAngle(float value)
        {
            MaxClimbAngle = value;
            if (ccAP != null)
                ccAP.max_climb_angle = MaxClimbAngle;
        }
        #endregion
        #region Speed control
        private double SetThrustActive(bool value)
        {
            if (speedAP == null)
            {
                speedAP = aa.getVesselModules(aa.ActiveVessel)[typeof(ProgradeThrustController)] as ProgradeThrustController;
            }
            speedAP.spd_control_enabled = value;
            if (speedAP.spd_control_enabled)
            {
                if (SpeedSetPoint >= 0f)
                    speedAP.setpoint = new SpeedSetpoint(SpeedType.MetersPerSecond, SpeedSetPoint, aa.ActiveVessel);
                else
                    SpeedSetPoint = speedAP.setpoint.mps();
                return 1.0;
            }
            else
            {
                return 1.0;
            }
        }
        public double ToggleThrustActive()
        {
            return SetThrustActive(speedAP == null || !speedAP.spd_control_enabled);
        }
        public bool GetThrustActive()
        {
            if (speedAP != null)
            {
                return speedAP.Active;
            }
            else
            {
                return false;
            }
        }

        private void ResetSpeedControl()
        {
            if (speedAP != null && speedAP.spd_control_enabled)
            {
                speedAP.setpoint = new SpeedSetpoint(SpeedType.MetersPerSecond, SpeedSetPoint, aa.ActiveVessel);
            }
        }
        public double SetSpeedSetPoint(double value)
        {
            SpeedSetPoint = (float)value;
            
            if (speedAP != null)
            {
                //Utility.LogMessage(this, "Need to set speed from {0} {1} to {2}", speedAP.setpoint.value, speedAP.setpoint.type.ToString(), SpeedSetPoint);
                //speedAP.setpoint = new SpeedSetpoint(SpeedType.MetersPerSecond, Mathf.Max(SpeedSetPoint, 0f), aa.ActiveVessel);
                speedAP.setpoint.value = SpeedSetPoint;
                //Utility.LogMessage(this, "setting speed to {0}", speedAP.setpoint.value);
                //Utility.LogMessage(this, "Units are {0}", speedAP.setpoint.type.ToString());
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }
        #endregion

        /// <summary>
        /// Method called during FixedUpdate to update queryable variables that
        /// are used by multiple methods.
        /// </summary>
        [MoonSharpHidden]
        internal void Update()
        {
            try
            {
                if (masterAP != null && masterAP.Active && fbwAP != null && fbwAP.Active)
                {
                    CoordTurn = fbwAP.Coord_turn;
                    RocketMode = fbwAP.RocketMode;
                }
                else
                {
                    fbwAP = null;
                    CoordTurn = false;
                    RocketMode = false;
                }

                if (masterAP != null && masterAP.Active && ccAP != null && ccAP.Active)
                {
                    if (ccAP.Active)
                    {
                        PseudoFLC = ccAP.pseudo_flc;
                        FLCMargin = ccAP.flc_margin;
                        MaxClimbAngle = ccAP.max_climb_angle;
                        HeadingSetPoint = ccAP.desired_course;
                        AltitudeSetPoint = ccAP.desired_altitude;
                        if (ccAP.height_mode == CruiseController.HeightMode.FlightPathAngle)
                        {
                            VertAngleSetPoint = ccAP.desired_vertsetpoint;
                        }
                        else
                        {
                            VertSpeedSetPoint = ccAP.desired_vertsetpoint;
                        }
                        LongitudeSetPoint = ccAP.desired_longitude.Value;
                        LatitudeSetPoint = ccAP.desired_latitude.Value;
                        ///private GeoCoordinates WaypointSetPoint = null;
                    }
                    else
                    {
                        ccAP = null;
                    }
                }
                else
                {
                    ccAP = null;
                    PseudoFLC = true;
                    FLCMargin = double.NaN;
                    MaxClimbAngle = double.NaN;
                    HeadingSetPoint = -1f;
                    AltitudeSetPoint = -1f;
                    VertSpeedSetPoint = float.NaN;
                    VertAngleSetPoint = float.NaN;
                    LongitudeSetPoint = float.NaN;
                    LatitudeSetPoint = float.NaN;
                    ///private GeoCoordinates WaypointSetPoint = null; 
                }

                DirectionSetPoint = Vector3.zero;

                if (masterAP != null && masterAP.Active && speedAP != null && speedAP.Active)
                {
                    SpeedSetPoint = speedAP.setpoint.convert(SpeedType.MetersPerSecond);
                    //Utility.LogMessage(this, "SpeedSetPoint set to {0} on update", SpeedSetPoint);
                }
                else
                {
                    speedAP = null;
                    SpeedSetPoint = -1f;
                }
                
            }
            catch (Exception e)
            {
                var st = new StackTrace(e, true);
                // Get the top stack frame
                var frame = st.GetFrame(0);
                // Get the line number from the stack frame
                var line = frame.GetFileLineNumber();
                Utility.LogError(this, "MASIAtmosphereAutopilot.Update exception: " + e.ToString() + line.ToString());
                throw new MASAAException("Error in update: " + e.Source + e.TargetSite + e.Data + e.StackTrace);
            }
        }
    }
}