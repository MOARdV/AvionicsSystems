-- MOARdV.lua
--
-- Lua functions for FlightSystemsRedux
--
-- Author: MOARdV

function MOARdVAppendDigitAndClick(persistentVar, addon, toggleVar)

	fc.AppendPersistent(persistentVar, addon, 6)

	fc.TogglePersistent(toggleVar)

end

function MOARdVAddDigitAndClick(persistentVar, addon, toggleVar)

	fc.AddPersistentClamped(persistentVar, addon, 0, 999999)

	fc.TogglePersistent(toggleVar)

end

function MOARdVResetAndClick(persistentVar, toggleVar)

	fc.SetPersistent(persistentVar, "")

	fc.TogglePersistent(toggleVar)

end

-- Returns a texture shift that depends on the safetyState of the parachutes.
-- (0.0, 0.4) = green
-- (0.0, 0.6) = yellow
-- (0.0, 0.2) = red
function  MOARdVParachuteSafetyTexture()

	local safetyState = realchute.DeploymentSafe()
	if safetyState > 0 then
		return fc.Vector2(0, 0.4)
	elseif safetyState < 0 then
		return fc.Vector2(0, 0.2)
	else
		return fc.Vector2(0, 0.6)
	end
end

-- Returns 180 - the angle to the target if it is a trackable vessel,
-- 180 otherwise.
function MOARdVRadarAngle()

	local targetType = fc.TargetType()

	if targetType > 0 and targetType < 3 then
		return 180 - fc.TargetAngle()
	end

	return 180
end

-- Returns 1 if the backlight has been switched on and there is a valid
-- target.
function MOARdVRadarSignal()

	if fc.Conditioned(fc.GetPersistentAsNumber("Backlight")) > 0 then
		local targetType = fc.TargetType()

		if targetType > 0 and targetType < 3 then
			return 1
		end
	end

	return 0
end

-- Returns a pseudo "fuel tank pressure" variable based on remaining
-- fuel percentage.
function MOARdVFuelPressure()
	local fuelPercent = fc.ResourceStagePercent("LiquidFuel")

	return fuelPercent / (fuelPercent + 0.1)
end

-- Returns a pseudo "monopropellant tank pressure" variable based on remaining
-- monopropellant percentage.
function MOARdVMonopropPressure()
	local monopropPercent = fc.ResourcePercent("MonoPropellant")

	return monopropPercent / (monopropPercent + 0.1)
end

function MOARdVStage(persistentValue)
	fc.TogglePersistent(persistentValue)
	fc.Stage()
end

local TargetType =
{
	"No Target",
	"Vessel",
	"Vessel/Dock",
	"Celestial Body",
	"Location",
	"Asteroid"
}

function MOARdV_TargetType()
	return TargetType[fc.TargetType() + 1]
end

function MOARdVTimeSelect()
	local selectedTime = 0
	local selector = fc.GetPersistentAsNumber("MOARdV_TimeMode")

	if selector == 0 then
		-- MET
		selectedTime = fc.MET()
	elseif selector == 1 then
		-- ATMO
		selectedTime = fc.TimeToAtmosphere()
	elseif selector == 2 then
		-- SoI
		selectedTime = fc.TimeToSoI()
	elseif selector == 3 then
		-- KAC
		selectedTime = kac.TimeToAlarm()
	else
		--selector == 4
		-- LANDING
		selectedTime = fc.TimeToLanding()
	end
	-- Clamp the time to 999:59:59
	return math.min(3599999, selectedTime)
end
