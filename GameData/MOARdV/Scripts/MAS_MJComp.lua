-- MAS_MJComp.lua
--
-- MOARdV's Avionics Systems
-- Lua scripts for an onboard computer data entry system similar to a Soyuz TM PRVI / BRVI (ПРВИ / БРВИ)
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

function MAS_MJComp_NumKey(key, autoId)
	fc.TogglePersistent(autoId)

	if (fc.GetPersistentAsNumber("MAS_MJComp_On") * mechjeb.Available()) > 0 then
		fc.AppendPersistent("MAS_MJComp_Buffer", key, 6)
	end
end

function MAS_MJComp_AddKey(amount, autoId)
	fc.TogglePersistent(autoId)

	if (fc.GetPersistentAsNumber("MAS_MJComp_On") * mechjeb.Available()) > 0 then
		fc.AddPersistentClamped("MAS_MJComp_Buffer", amount, 0, 999999)
	end
end

function MAS_MJComp_Clear(autoId)
	fc.TogglePersistent(autoId)
	if (fc.GetPersistentAsNumber("MAS_MJComp_On") * mechjeb.Available()) > 0 then
		fc.SetPersistent("MAS_MJComp_Buffer", 0)
	end
end

-- Don't have a "delete last character" function yet.
function MAS_MJComp_Delete(autoId)
	fc.TogglePersistent(autoId)
	if (fc.GetPersistentAsNumber("MAS_MJComp_On") * mechjeb.Available()) > 0 then
		fc.SetPersistent("MAS_MJComp_Buffer", 0)
	end
end

function MAS_MJComp_Enter()

	fc.SetPersistent("MAS_MJComp_Exec", 1)

	if mechjeb.Available() == 0 then
		fc.SetPersistent("MAS_MJComp_Status", 0)
		return
	end

	local MJComp_mode = fc.GetPersistentAsNumber("MAS_MJComp_Mode")
	local inFlight = (fc.VesselFlying() > 0)

	-- mode 0 = OFF
	-- mode 1 = LAUNCH ALT
	-- mode 2 = LAUNCH INC
	-- mode 3 = ALT
	-- mode 4 = AP
	-- mode 5 = PE

	if MJComp_mode == 0 then
		fc.SetPersistent("MAS_MJComp_Status", 0)
		return
	end

	if fc.GetPersistentAsNumber("MAS_MJComp_WriteEnable") == 0 then
		-- READ
		if MJComp_mode == 1 then
			fc.SetPersistent("MAS_MJComp_Buffer", math.floor(mechjeb.GetDesiredLaunchAltitude() * 0.001 + 0.5))
			fc.SetPersistent("MAS_MJComp_Status", 1)
		elseif MJComp_mode == 2 then
			fc.SetPersistent("MAS_MJComp_Buffer", mechjeb.GetDesiredLaunchInclination())
			fc.SetPersistent("MAS_MJComp_Status", 1)
		elseif MJComp_mode == 3 then
			if inFlight == true then
				fc.SetPersistent("MAS_MJComp_Buffer", math.floor(fc.Altitude() * 0.001 + 0.5))
				fc.SetPersistent("MAS_MJComp_Status", 1)
			else
				fc.SetPersistent("MAS_MJComp_Status", 2)
			end
		elseif MJComp_mode == 4 then
			if inFlight == true then
				fc.SetPersistent("MAS_MJComp_Buffer", math.floor(fc.Apoapsis() * 0.001 + 0.5))
				fc.SetPersistent("MAS_MJComp_Status", 1)
			else
				fc.SetPersistent("MAS_MJComp_Status", 2)
			end
		elseif MJComp_mode == 5 then
			if inFlight == true then
				fc.SetPersistent("MAS_MJComp_Buffer", math.floor(fc.Periapsis() * 0.001 + 0.5))
				fc.SetPersistent("MAS_MJComp_Status", 1)
			else
				fc.SetPersistent("MAS_MJComp_Status", 2)
			end
		end
	else
		-- WRITE
		if MJComp_mode == 1 then
			mechjeb.SetDesiredLaunchAltitude(fc.GetPersistentAsNumber("MAS_MJComp_Buffer") * 1000)
			fc.SetPersistent("MAS_MJComp_Status", 1)
		elseif MJComp_mode == 2 then
			local inc = fc.GetPersistentAsNumber("MAS_MJComp_Buffer")
			if inc < 360 then
				mechjeb.SetDesiredLaunchInclination(inc)
				fc.SetPersistent("MAS_MJComp_Status", 1)
			else
				fc.SetPersistent("MAS_MJComp_Status", 2)
			end
		elseif MJComp_mode == 3 then
			if inFlight == true then
				local alt = fc.GetPersistentAsNumber("MAS_MJComp_Buffer") * 1000
				if alt >= fc.Periapsis() and alt <= fc.Apoapsis() then
					mechjeb.CircularizeAt(alt)
					fc.SetPersistent("MAS_MJComp_Status", 1)
				else
					fc.SetPersistent("MAS_MJComp_Status", 2)
				end
			else
				fc.SetPersistent("MAS_MJComp_Status", 2)
			end
		elseif MJComp_mode == 4 then
			if inFlight == true then
				local alt = fc.GetPersistentAsNumber("MAS_MJComp_Buffer") * 1000
				if alt >= fc.Periapsis() then
					mechjeb.ChangeApoapsis(alt)
					fc.SetPersistent("MAS_MJComp_Status", 1)
				else
					fc.SetPersistent("MAS_MJComp_Status", 2)
				end
			else
				fc.SetPersistent("MAS_MJComp_Status", 2)
			end
		elseif MJComp_mode == 5 then
			if inFlight == true then
				local alt = fc.GetPersistentAsNumber("MAS_MJComp_Buffer") * 1000
				if alt <= fc.Apoapsis() then
					mechjeb.ChangePeriapsis(alt)
					fc.SetPersistent("MAS_MJComp_Status", 1)
				else
					fc.SetPersistent("MAS_MJComp_Status", 2)
				end
			else
				fc.SetPersistent("MAS_MJComp_Status", 2)
			end
		end
	end

end
