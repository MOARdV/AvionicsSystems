-- MAS_ASET_Props.lua
--
-- MOARdV's Avionics Systems
-- One-off Lua functions for MAS ASET props
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

-- Function to control both RCS rotation and RCS translation from a single prop.
-- parameter 'mode': If 0, activate both modes.  If negative, set to translate only.
-- If positive, set to rotate only.
function RCSModeSelect(mode)

	if mode < 0 then

		fc.SetRCSRotate(false)
		fc.SetRCSTranslate(true)

	elseif mode > 0 then

		fc.SetRCSRotate(true)
		fc.SetRCSTranslate(false)

	else

		fc.SetRCSRotate(true)
		fc.SetRCSTranslate(true)

	end

	return 1
end

-- Function to return a specific electric output rate, depending on the mode.
-- Used by MAS_DigitalIndicator_Elec_Output and MAS_ASET_Elec_Output.
--
-- mode 0: Net output
-- mode 1: Alternator
-- mode 2: Fuel Cell
-- mode 3: Solar
-- mode 4: Generator
function SelectElectricOutput(mode)

	if mode == 1 then
		return fc.AlternatorOutput()
	elseif mode == 2 then
		return fc.FuelCellOutput()
	elseif mode == 3 then
		return fc.SolarPanelOutput()
	elseif mode == 4 then
		return fc.GeneratorOutput()
	else
		return fc.AlternatorOutput() + fc.FuelCellOutput() + fc.SolarPanelOutput() + fc.GeneratorOutput()
	end
end

-- Function used to map the 4-position rotary IMP switch to an on/off + mode
function Set4posIMP(direction)

	if direction > 0 then
		local enabled = fc.GetPersistentAsNumber("MAS_IMP_On")
		if enabled > 0 then
			fc.AddPersistentClamped("MAS_IMP_Mode", 1, -1, 1)
		else
			fc.SetPersistent("MAS_IMP_On", 1)
			fc.SetPersistent("MAS_IMP_Mode", -1)
		end
	else
		local mode = fc.GetPersistentAsNumber("MAS_IMP_Mode")
		if mode > -1 then
			fc.AddPersistentClamped("MAS_IMP_Mode", -1, -1, 1)
		else
			fc.SetPersistent("MAS_IMP_On", 0)
		end
	end

	return 1
end

-- Function used to map 4-position rotary X-pointer mode switch to on/off + mode
function Set4posXPtr(direction)

	if direction > 0 then
		local enabled = fc.GetPersistentAsNumber("MAS_Xpointer_Power")
		if enabled > 0 then
			fc.AddPersistentClamped("MAS_Xpointer_Mode", 1, -1, 1)
		else
			fc.SetPersistent("MAS_Xpointer_Power", 1)
			fc.SetPersistent("MAS_Xpointer_Mode", -1)
		end
	else
		local mode = fc.GetPersistentAsNumber("MAS_Xpointer_Mode")
		if mode > -1 then
			fc.AddPersistentClamped("MAS_Xpointer_Mode", -1, -1, 1)
		else
			fc.SetPersistent("MAS_Xpointer_Power", 0)
		end
	end

	return 1
end

-- Used on the portable timer prop
function GetPortableTimerValue(mode)
	if mode == 0 then
		return fc.MET()
	elseif mode == 2 then
		return fc.TimeToAp()
	elseif mode == 3 then
		return fc.TimeToPe()
	elseif mode == 4 then
		return fc.ManeuverNodeTime()
	elseif mode == 6 then
		return kac.TimeToAlarm()
	else
		return 0
	end
end

-- Initialize the overloaded page buttons for the ALCOR 40x20 MFD
function MAS_Alcor_40x20_Init(propid)

	fc.InitializePersistent(propid .. "-B", "ALCOR_MFD40x20_Target")
	fc.InitializePersistent(propid .. "-C", "ALCOR_MFD40x20_Nav1")
	fc.InitializePersistent(propid .. "-D", "ALCOR_MFD40x20_MechJeb")
	fc.InitializePersistent(propid .. "-E", "ALCOR_MFD40x20_Graphs1")
	fc.InitializePersistent(propid .. "-G", "ALCOR_MFD40x20_RsrcStage")
	fc.InitializePersistent(propid .. "-R1", "ALCOR_MFD40x20_Flight")
	fc.InitializePersistent(propid .. "-R2", "ALCOR_MFD40x20_Orbit")
	fc.InitializePersistent(propid .. "-R3", "ALCOR_MFD40x20_Dock")
	
	return 1
end

-- Initialize the overloaded page buttons for the ALCOR 60x30 MFD
function MAS_Alcor_60x30_Init(propid)

	fc.InitializePersistent(propid .. "-R1", "ALCOR_MFD60x30_ResourceTotal")
	fc.InitializePersistent(propid .. "-R7", "ALCOR_MFD60x30_Orbit")
	fc.InitializePersistent(propid .. "-R8", "ALCOR_MFD60x30_SCANsat")
	
	return 1
end

function FMSEditNode(mode, FMSBuffer, progradeDV, normalDV, radialDV, nodeTime)
	-- local success
	
	if mode < 0 then 
		return 0
	elseif mode == 0 then
		return fc.AddManeuverNode(progradeDV, normalDV, radialDV, fc.UT() + FMSBuffer - fc.TimeOfDay(fc.UT())) and fc.LogMessage("Editing Node Time")
	elseif mode == 1 then
		return fc.AddManeuverNode(FMSBuffer, normalDV, radialDV, fc.UT() + nodeTime) and fc.LogMessage("Editing Prograde DV")
	elseif mode == 2 then
		return fc.AddManeuverNode(progradeDV, FMSBuffer, radialDV, fc.UT() + nodeTime) and fc.LogMessage("Editing Normal DV")
	elseif mode == 3 then
		return fc.AddManeuverNode(progradeDV, normalDV, FMSBuffer, fc.UT() + nodeTime) and fc.LogMessage("Editing Radial DV")
	else
		return not fc.LogMessage("Invalid Mode")
	end
	
	return not fc.LogMessage("No Conditions Satisfied")
end

function AAEditParams(mode, FMSBuffer)
	if mode < 0 then
		return 0
	elseif mode == 0 then
		return aa.SetHeadingSetPoint(FMSBuffer)
	elseif mode == 1 then
		return aa.SetLatitudeSetPoint(FMSBuffer)
	elseif mode == 2 then
		return aa.SetLongitudeSetPoint(FMSBuffer)
	elseif mode == 3 then
		return aa.SetAltitudeSetPoint(FMSBuffer)
	elseif mode == 4 then
		return aa.SetVertSpeedSetPoint(FMSBuffer)
	elseif mode == 5 then
		return aa.SetVertAngleSetPoint(FMSBuffer)
	else
		return not fc.LogMessage("Invalid Mode")
	end
	
	return not fc.LogMessage("No Conditions Satisfied")

end

function AACopyParams(mode)
	if mode < 0 then
		return 0
	elseif mode == 0 then
		return aa.GetHeadingSetPoint()
	elseif mode == 1 then
		return aa.GetLatitudeSetPoint()
	elseif mode == 2 then
		return aa.GetLongitudeSetPoint()
	elseif mode == 3 then
		return aa.GetAltitudeSetPoint()
	elseif mode == 4 then
		return aa.GetVertSpeedSetPoint()
	elseif mode == 5 then
		return aa.GetVertAngleSetPoint()
	else
		return not fc.LogMessage("Invalid Mode")
	end
	
	return not fc.LogMessage("No Conditions Satisfied")
end

function AAUpdateHNAV(mode)
	if mode < 0 then
		return not fc.LogMessage("Invalid Mode")
	elseif mode >= 0 and mode <= 2 then
		return aa.SwitchHNAVMode(mode)
	else
		return not fc.LogMessage("Invalid Mode")
	end
end

function AAUpdateVNAV(mode)
	if mode < 0 then
		return not fc.LogMessage("Invalid Mode")
	elseif mode >= 0 and mode <= 2 then
		return aa.SwitchVNAVMode(mode)
	else
		return not fc.LogMessage("Invalid Mode")
	end
end

local KACThreshold = {
	5,
	15,
	30,
	60,
	120,
	300
}

function SetKACAlarm(modeSelA, modeSelB, threshold)

	if modeSelA == 0 then
		kac.DeleteAlarm(fc.GetPersistent("MAS_Clock_AlarmId"))
		local newAlarmID = kac.CreateTypeAlarm("Raw", "Alarm for " .. fc.VesselName(), fc.UT() - fc.TimeOfDay(fc.UT()) + fc.GetPersistentAsNumber("STS_FMSCompBuffer") - KACThreshold[threshold])
		fc.SetPersistent("MAS_Clock_AlarmId", newAlarmID)
		return 1
	elseif modeSelA == 1 and fc.TimeToSoI() then
		kac.DeleteAlarm(fc.GetPersistent("MAS_Clock_AlarmId"))
		local newAlarmID = kac.CreateTypeAlarm("SOIChange", "SOI Transition to " .. fc.NextBodyName() .. " for " .. fc.VesselName(), fc.UT() + fc.TimeToNextSoI() - KACThreshold[threshold])
		fc.SetPersistent("MAS_Clock_AlarmId", newAlarmID)
		return 1
	elseif modeSelA == 2 and fc.TargetClosestApproachTime() then
		kac.DeleteAlarm(fc.GetPersistent("MAS_Clock_AlarmId"))
		local newAlarmID = kac.CreateTypeAlarm("Closest", fc.VesselName() .. " closest approach to " .. fc.TargetName(), fc.UT() + fc.TargetClosestApproachTime() - KACThreshold[threshold])
		fc.SetPersistent("MAS_Clock_AlarmId", newAlarmID)
		return 1
	elseif modeSelA == 3 and fc.BodyAtmosphereTop(fc.CurrentBodyIndex()) and fc.BodyAtmosphereTop(fc.CurrentBodyIndex()) > fc.Periapsis() then
		kac.DeleteAlarm(fc.GetPersistent("MAS_Clock_AlarmId"))
		local newAlarmID = kac.CreateTypeAlarm("Closest", fc.VesselName() .. " altitude limit " .. fc.BodyAtmosphereTop(fc.CurrentBodyIndex()) .. " m at " .. fc.BodyName(fc.CurrentBodyIndex()), fc.UT() + fc.TimeToAtmosphere() - KACThreshold[threshold])
		fc.SetPersistent("MAS_Clock_AlarmId", newAlarmID)
		return 1
	elseif modeSelA == 4 and fc.ManeuverNodeTime() < 0 then
		kac.DeleteAlarm(fc.GetPersistent("MAS_Clock_AlarmId"))
		local newAlarmID = kac.CreateTypeAlarm("Maneuver", fc.VesselName() .. " maneuver", fc.UT() - fc.ManeuverNodeTime() - 0.5 * fc.ManeuverNodeBurnTime() - KACThreshold[threshold])
		fc.SetPersistent("MAS_Clock_AlarmId", newAlarmID)
		return 1
	elseif modeSelA == 5 then
		if modeSelB == 0 and fc.TimeToAp() > 0 then
			kac.DeleteAlarm(fc.GetPersistent("MAS_Clock_AlarmId"))
			local newAlarmID = kac.CreateTypeAlarm("Apoapsis", fc.VesselName() .. " apoapsis", fc.UT() + fc.TimeToAp() - KACThreshold[threshold])
			fc.SetPersistent("MAS_Clock_AlarmId", newAlarmID)
			return 1
		elseif modeSelB == 1 and fc.TimeToPe() > 0 then
			kac.DeleteAlarm(fc.GetPersistent("MAS_Clock_AlarmId"))
			local newAlarmID = kac.CreateTypeAlarm("Periapsis", fc.VesselName() .. " periapsis", fc.UT() + fc.TimeToPe() - KACThreshold[threshold])
			fc.SetPersistent("MAS_Clock_AlarmId", newAlarmID)
			return 1
		elseif modeSelB == 2 and fc.TimeToANEq() > 0 then
			kac.DeleteAlarm(fc.GetPersistent("MAS_Clock_AlarmId"))
			local newAlarmID = kac.CreateTypeAlarm("AscendingNode", fc.VesselName() .. " ascending node", fc.UT() + fc.TimeToANEq() - KACThreshold[threshold])
			fc.SetPersistent("MAS_Clock_AlarmId", newAlarmID)
			return 1
		elseif modeSelB == 3 and fc.TimeToDNEq() > 0 then
			kac.DeleteAlarm(fc.GetPersistent("MAS_Clock_AlarmId"))
			local newAlarmID = kac.CreateTypeAlarm("DescendingNode", fc.VesselName() .. " descending node", fc.UT() + fc.TimeToDNEq() - KACThreshold[threshold])
			fc.SetPersistent("MAS_Clock_AlarmId", newAlarmID)
			return 1
		elseif modeSelB == 4 and transfer.TimeUntilPhaseAngle() > 0 then
			kac.DeleteAlarm(fc.GetPersistent("MAS_Clock_AlarmId"))
			local newAlarmID = kac.CreateTypeAlarm("Transfer", fc.VesselName() .. " transfer window from " .. fc.BodyName(fc.CurrentBodyIndex()) .. " to " .. fc.TargetBodyName(), fc.UT() + transfer.TimeUntilPhaseAngle() - KACThreshold[threshold])
			fc.SetPersistent("MAS_Clock_AlarmId", newAlarmID)
			return 1
		elseif modeSelB == 5 then
			kac.DeleteAlarm(fc.GetPersistent("MAS_Clock_AlarmId"))
			local newAlarmID = kac.CreateTypeAlarm("Crew", fc.VesselName() .. " crew alarm", fc.UT() - fc.TimeOfDay(fc.UT()) + fc.GetPersistentAsNumber("STS_FMSCompBuffer") - KACThreshold[threshold])
			fc.SetPersistent("MAS_Clock_AlarmId", newAlarmID)
			return 1
		else
			fc.LogMessage("Invalid Mode B")
			fc.SetPersistent("KAC_Error", 1)
			return 0
		end
	else
		fc.LogMessage("Invalid Mode A")
		fc.SetPersistent("KAC_Error", 1)
		return 0
	end
	
	fc.LogMessage("No Conditions Satisfied")
	fc.SetPersistent("KAC_Error", 1)
	return 0
	
end

function CircularizeAltitudeTest(altitude)
	
	if not fc.VesselFlying()	 then
		return 0
	elseif fc.Eccentricity() < 1 then
		return transfer.CircularizeAltitude(altitude)
	else
		return transfer.CircularizeAltitudeHypVis(altitude)
	end
	
	return not fc.LogMessage("No Conditions Satisfied")
end