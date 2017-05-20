-- X-pointer.lua
--
-- MOARdV's Avionics Systems
-- Lua functions for the X-Pointer rendezvous / landing instrument
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

local xptrInitialized = false

-- Is the power on?
local xptrOn = false
-- What mode are we in? (0 = radar altimeter, 1 = target distance, 2 = target relative velocity)
local xptrMode = 0
-- xptrScale is 1 / (scale) - that is, if the scale shows on the x-pointer as
-- "x10", xptrScale = 0.1.  We do this because we multiply the needle values
-- by this number, instead of doing a division.  Multiplying is faster than
-- dividing.
local xptrScale = 1
-- Is the unit providing valid data?
local xptrEnable = false

-- Determine if the selected mode is currently valid
local function ValidateXPtrMode()

	local oldEnable = xptrEnable
	
	if xptrOn == false then
		xptrEnable = false
	elseif xptrMode == 0 then
		-- Landing mode (radar altimeter) enagages at 6500m.
		xptrEnable = (fc.AltitudeTerrain(false) <= 6500)
	else 
		-- for testing:
		xptrEnable = (fc.TargetIsVessel() > 0)
		-- for production:
		--xptrEnable = (fc.TargetType() == 2 and fc.ReferenceTransformType() == 3)
	end
	
	if oldEnable ~= xptrEnable then
		if xptrEnable == true then
			fc.SetPersistent("MAS_Xpointer_Enable", 1)
		else
			fc.SetPersistent("MAS_Xpointer_Enable", 0)
		end
	end
end

-- returns 1 if there is an error condition preventing the X-Pointer from
-- operating, 0 otherwise.  Also manages initialization.
function XPtr_Error()
	if xptrInitialized == false then
		xptrInitialized = true

		xptrMode = fc.GetPersistentAsNumber("MAS_Xpointer_Mode")

		local scale = fc.GetPersistentAsNumber("MAS_Xpointer_Scale")
		xptrScale = math.pow(10, scale)

		local enable = fc.GetPersistentAsNumber("MAS_Xpointer_Enable")
		if enable > 0 then
			xptrEnable = true
		else
			xptrEnable = false
		end

		local toggle = fc.GetPersistentAsNumber("MAS_Xpointer_Power")
		if toggle > 0 then
			xptrOn = true
			-- Validate happens below in this case
		else
			xptrOn = false
			ValidateXPtrMode()
		end
	end

	if xptrOn == true then
		ValidateXPtrMode()

		if xptrEnable == false then
			return fc.Conditioned(1)
		else
			return 0
		end
	else
		xptrEnable = false
		return 0
	end
end

-- Vertical Needle (the one that moves laterally) displacement
function XPtr_VerticalNeedle()
	if xptrEnable == true then
		local value
		if xptrMode == 0 then
			-- ground relative horiz velocity right
			value = fc.SurfaceLateralSpeed()
		elseif xptrMode == 1 then
			-- target distance x
			value = fc.TargetDistanceX()
		else --xptrMode == 2
			-- target relative vel x
			value = fc.TargetVelocityX()
		end

		return fc.Conditioned(value * xptrScale)
	else
		return 0
	end
end

-- Horizontal Needle (the one that moves vertically) displacement
function XPtr_HorizontalNeedle()
	if xptrEnable == true then
		local value
		if xptrMode == 0 then
			-- ground relative horiz velocity right
			value = fc.SurfaceForwardSpeed()
		elseif xptrMode == 1 then
			-- target distance x
			value = fc.TargetDistanceY()
		else --xptrMode == 2
			-- target relative vel x
			value = fc.TargetVelocityY()
		end

		return fc.Conditioned(value * xptrScale)
	else
		return 0
	end
end

-- Set the X-pointer scale to the new scale (10 ^ -scale)
function XPtr_NextScale(scale)

	if scale >= -1 and scale <= 1 then
		-- local scale = fc.AddPersistentWrapped("MAS_Xpointer_Scale", -1, -1, 2)
		xptrScale = math.pow(10, scale)
	end
	return 1
end

-- Set the X-pointer to the new mode if the mode is viable
function XPtr_SetMode(mode)

	if mode >=0 and mode <=2 then
		xptrMode = mode
		
		ValidateXPtrMode()
	end
	return 1
end

-- Toggle the power switch
function XPtr_TogglePower()
	local toggle = fc.TogglePersistent("MAS_Xpointer_Power")

	if toggle > 0 then
		xptrOn = true
	else
		xptrOn = false
		-- Call Validate to shut off enable flags
		ValidateXPtrMode()
	end
end
