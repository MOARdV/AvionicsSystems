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
function MAS_kOS_Stby(propId, timerName, currentTime)

	local ut = fc.UT()
	
	if currentTime < ut then
		fc.SetPersistent(timerName, ut + 5)
		fc.LogMessage("Setting Timer")
	else
		fc.SetPersistent(propId, "MAS_kOS_Standby")
		fc.SetPersistent(timerName, 0)
		fc.LogMessage("Clearing Timer")
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
