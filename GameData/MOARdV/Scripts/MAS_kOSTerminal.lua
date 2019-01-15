-- MAS_kOSTerminal.lua
--
-- MOARdV's Avionics Systems
-- Lua scripts for ALCOR kOS terminal MFD
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

------------------------------------------------------------------------------
--
function MAS_kOS_Stby(propId, timerName, currentTime, stbyPage)

	local ut = fc.UT()
	
	if currentTime < ut then
		fc.SetPersistent(timerName, ut + 5)
		--fc.LogMessage("Setting Timer")
	else
		fc.SetPersistent(propId, stbyPage)
		fc.SetPersistent(timerName, 0)
		--fc.LogMessage("Clearing Timer")
	end
	
end

------------------------------------------------------------------------------
--
function MAS_kOS_ActionGroup_Menu_Select(propId, activeRow)
	if activeRow >= 0 and activeRow <= 8 then
		fc.ToggleActionGroup(activeRow + 1)
	elseif activeRow == 9 then
		fc.ToggleActionGroup(0)
	end
end

------------------------------------------------------------------------------
--
function MAS_kOS_PilotToggle(headingStore, pitchStore, rollStore)
	
	if fc.GetAttitudePilotActive() < 1 then
		if fc.VesselFlying() > 0 then
			-- Setups...
			local heading = math.floor(fc.Heading())
			fc.SetPersistent(headingStore, heading)
			local pitch = math.floor(fc.Pitch())
			fc.SetPersistent(pitchStore, pitch)
			local roll = math.floor(fc.Roll())
			fc.SetPersistent(rollStore, roll)
			
			fc.SetHeading(5, heading, pitch, roll)
		end
	else
		fc.ToggleAttitudePilot()
	end
	
end

------------------------------------------------------------------------------
--
function MAS_kOS_Pilot_AdjustSetting(direction, row, headingStore, pitchStore, rollStore)
	
	if fc.GetAttitudePilotActive() > 0 then
		-- Setups...
		local heading = fc.GetPersistentAsNumber(headingStore)
		local pitch = fc.GetPersistentAsNumber(pitchStore)
		local roll = fc.GetPersistentAsNumber(rollStore)
		
		if row == 1 then
			heading = fc.AddPersistentWrapped(headingStore, direction, 0, 360)
		elseif row == 2 then
			pitch = fc.Clamp(fc.NormalizePitch(pitch + direction), -90, 90)
			fc.SetPersistent(pitchStore, pitch)
		elseif row == 3 then
			roll = fc.AddPersistentWrapped(rollStore, direction, -180, 180)
		end
		
		--fc.LogMessage("Adjust Heading " .. heading .. ", " .. pitch..", " .. roll)
		fc.SetHeading(5, heading, pitch, roll)
	end
	
end

------------------------------------------------------------------------------
--
function MAS_kOS_Pilot_EnterDigit(buffer, value)
	local currentValue = fc.GetPersistentAsNumber(buffer)
	local sign = 1
	if currentValue < 0 then
		sign = -1
	end
	
	if math.abs(currentValue) < 100 then
		currentValue = currentValue * 10 + sign * value
		fc.SetPersistent(buffer, currentValue)
	end
end

------------------------------------------------------------------------------
--
function MAS_kOS_Pilot_UpdateHPR(bufferValue, headingMode, headingStore, pitchStore, rollStore)
	
	if MAS_kOS_Pilot_BufferError(bufferValue, headingMode) < 1 and fc.VesselFlying() > 0 then
		local heading = fc.GetPersistentAsNumber(headingStore)
		local pitch = fc.GetPersistentAsNumber(pitchStore)
		local roll = fc.GetPersistentAsNumber(rollStore)

		if headingMode == 1 then
			heading = bufferValue
			fc.SetPersistent(headingStore, heading)
		elseif headingMode == 2 then
			pitch = bufferValue
			fc.SetPersistent(pitchStore, pitch)
		else
			roll = bufferValue
			fc.SetPersistent(rollStore, roll)
		end
		
		fc.SetHeading(5, heading, pitch, roll)
	end
end

------------------------------------------------------------------------------
--
function MAS_kOS_Pilot_BufferError(bufferValue, headingMode)
	
	if headingMode == 0 then
		if math.abs(bufferValue) > 360 then
			return 1
		end
	elseif headingMode == 1 then
		-- Heading
		if bufferValue > 360 or bufferValue < 0 then
			return 1
		end
	elseif headingMode == 2 then
		-- Pitch
		if bufferValue > 90 or bufferValue < -90 then
			return 1
		end
	else
		-- Roll
		if bufferValue > 180 or bufferValue < -180 then
			return 1
		end
	end
	
	return 0
end

------------------------------------------------------------------------------
--
function MAS_kOS_Resource_AdjustAlarm(amount, activePanel, resourceBase)
	if activePanel > 0 and activePanel < 10 then
		fc.AddPersistentClamped(resourceBase .. activePanel, amount, 0, 1)
	end
end

------------------------------------------------------------------------------
--
function MAS_kOS_Resource_ChangeResource(direction, activePanel, resourceBase)
	if activePanel > 0 and activePanel < 10 then
		fc.AddPersistentWrapped(resourceBase .. activePanel, direction, 0, fc.ResourceCount())
	end
end
