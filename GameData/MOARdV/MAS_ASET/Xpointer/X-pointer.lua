-- X-pointer.lua
--
-- MOARdV's Avionics Systems
-- Lua functions for the X-Pointer rendezvous / landing instrument
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

local xptrScale = 1

-- Called every FixedUpdate while the x-pointer is switched on.
function XPtr_Validate(xptrEnable, xptrMode)

	local oldEnable = xptrEnable > 0
	local newEnable

	if xptrMode == 0 and fc.AltitudeBottom() <= 5000 then
		newEnable = true
	elseif fc.TargetIsVessel() > 0 then
		newEnable = true
	else
		newEnable = false
	end

	if oldEnable ~= newEnable then
		if newEnable == true then
			fc.SetPersistent("MAS_Xpointer_Enable", 1)
		else
			fc.SetPersistent("MAS_Xpointer_Enable", 0)
		end
	end
end

-- Vertical Needle (the one that moves laterally) displacement
function XPtr_VerticalNeedle(xptrMode)

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

	return value * xptrScale
end

-- Horizontal Needle (the one that moves vertically) displacement
function XPtr_HorizontalNeedle(xptrMode)

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

	return value * xptrScale
end

-- Set the X-pointer scale to the new scale (10 ^ -scale)
function XPtr_NextScale(scale)

	if scale >= -1 and scale <= 1 then
		xptrScale = math.pow(10, scale)
	end

	return 1
end
