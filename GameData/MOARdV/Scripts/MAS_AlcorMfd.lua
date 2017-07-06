-- MAS_AlcorMfd.lua
--
-- MOARdV's Avionics Systems
-- Lua scripts for ALCOR Multi-Function Displays
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

-- Initialization script for the ALCOR MFD.  This script may set up multiple variables if needed.
function MAS_AlcorMfdInit(propId)
	
	if fc.GetPersistentExists(propId .. "-Acapt") < 1 then
		fc.SetPersistent(propId .. "-A", "MAS_ALCOR_MFD_A_Launch")
		fc.SetPersistent(propId .. "-Acapt", "LAUNCH")
	end
end

-- table (array) indices as follows:
--  1 = R1
--  2 = R2
--  3 = R3
--  4 = R4
--  5 = R5
--  6 = R6
--  7 = R7
--  8 = A
--  9 = B
-- 10 = C
-- 11 = D
-- 12 = E
-- 13 = F
-- 14 = G

------------------------------------------------------------------------------
-- local selectors
local function MAS_AlcorPage7(propId, direction)
	
	fc.AddPersistentWrapped(propId .. "-CameraIndex", direction, 0, fc.CameraCount())

end

local function MAS_AlcorPageA(propId, direction)
	local newPage = fc.AddPersistentWrapped(propId .. "-LaunchLand", direction, 0, 2)
	if newPage == 0 then
		-- Set the page
		fc.SetPersistent(propId, "MAS_ALCOR_MFD_A_Launch")
		-- Remember which page was set, so we can set it again next time
		fc.SetPersistent(propId .. "-A", "MAS_ALCOR_MFD_A_Launch")
		-- Set the caption for the MFD top-row menu
		fc.SetPersistent(propId .. "-Acapt", "LAUNCH")
	elseif newPage == 1 then
		fc.SetPersistent(propId, "MAS_ALCOR_MFD_A_Land")
		fc.SetPersistent(propId .. "-A", "MAS_ALCOR_MFD_A_Land")
		fc.SetPersistent(propId .. "-Acapt", " LAND ")
	end
end

local pageSelector =
{
	nil,
	nil,
	nil,
	nil,
	nil,
	nil,
	MAS_AlcorPage7,
	MAS_AlcorPageA,
	nil,
	nil,
	nil,
	nil,
	nil,
	nil,
}

-- Router function that handles the Next/Prev buttons
function MAS_AlcorMFD_NextPrev(propId, direction)
	
	local btnName = propId .. "-btn"
	local currentPage = fc.GetPersistentAsNumber(btnName)

	-- If currentPage is 0, then there's no 'next' action.
	if currentPage > 0 then
		local handler = pageSelector[currentPage]
		if handler ~= nil then
			handler(propId, direction)
		end
	end
	
end

------------------------------------------------------------------------------
local function MAS_AlcorLeftRightPage7(propId, direction)
	
	local cameraId = fc.GetPersistentAsNumber(propId .. "-CameraIndex")
	
	return fc.AddPan(cameraId, direction * 0.5)
end

local pageLeftRight =
{
	nil,
	nil,
	nil,
	nil,
	nil,
	nil,
	MAS_AlcorLeftRightPage7,
	nil,
	nil,
	nil,
	nil,
	nil,
	nil,
	nil,
}

-- Router function that handles the Left/Right buttons
function MAS_AlcorMFD_LeftRight(propId, direction)
	local btnName = propId .. "-btn"
	local currentPage = fc.GetPersistentAsNumber(btnName)
	local handler = pageLeftRight[currentPage]
	
	if handler ~= nil then
		return handler(propId, direction)
	else
		return 0
	end
end


------------------------------------------------------------------------------
local function MAS_AlcorUpDownPage7(propId, direction)
	
	local cameraId = fc.GetPersistentAsNumber(propId .. "-CameraIndex")
	
	return fc.AddTilt(cameraId, direction)
end

local pageUpDown =
{
	nil,
	nil,
	nil,
	nil,
	nil,
	nil,
	MAS_AlcorUpDownPage7,
	nil,
	nil,
	nil,
	nil,
	nil,
	nil,
	nil,
}

-- Router function that handles the Up/Down buttons
function MAS_AlcorMFD_UpDown(propId, direction)
	local btnName = propId .. "-btn"
	local currentPage = fc.GetPersistentAsNumber(btnName)
	local handler = pageUpDown[currentPage]
	
	if handler ~= nil then
		return handler(propId, direction)
	else
		return 0
	end
end


------------------------------------------------------------------------------
local function MAS_AlcorEnterPage7(propId)
	
	local cameraId = fc.GetPersistentAsNumber(propId .. "-CameraIndex")
	
	return fc.AddFoV(cameraId, -1)
end

local pageEnter =
{
	nil,
	nil,
	nil,
	nil,
	nil,
	nil,
	MAS_AlcorEnterPage7,
	nil,
	nil,
	nil,
	nil,
	nil,
	nil,
	nil,
}

-- Router function that handles the Enter button
function MAS_AlcorMFD_Enter(propId)
	local btnName = propId .. "-btn"
	local currentPage = fc.GetPersistentAsNumber(btnName)
	local handler = pageEnter[currentPage]
	
	if handler ~= nil then
		return handler(propId)
	else
		return 0
	end
end

------------------------------------------------------------------------------
local function MAS_AlcorEscPage7(propId)
	
	local cameraId = fc.GetPersistentAsNumber(propId .. "-CameraIndex")
	
	return fc.AddFoV(cameraId, 1)
end

local pageEsc =
{
	nil,
	nil,
	nil,
	nil,
	nil,
	nil,
	MAS_AlcorEscPage7,
	nil,
	nil,
	nil,
	nil,
	nil,
	nil,
	nil,
}

-- Router function that handles the Esc button
function MAS_AlcorMFD_Esc(propId)
	local btnName = propId .. "-btn"
	local currentPage = fc.GetPersistentAsNumber(btnName)
	local handler = pageEsc[currentPage]
	
	if handler ~= nil then
		return handler(propId)
	else
		return 0
	end
end

------------------------------------------------------------------------------
local function MAS_AlcorHomePage7(propId)
	
	local cameraId = fc.GetPersistentAsNumber(propId .. "-CameraIndex")
	
	-- Set it to a large number at let the camera clamp it
	fc.SetFoV(cameraId, 180)

	-- Zero these out.
	-- TODO: Smoothly blend these values, instead of an insta-snap.
	fc.SetPan(cameraId, 0)
	fc.SetTilt(cameraId, 0)
	
	return 0
end

local pageHome =
{
	nil,
	nil,
	nil,
	nil,
	nil,
	nil,
	MAS_AlcorHomePage7,
	nil,
	nil,
	nil,
	nil,
	nil,
	nil,
	nil,
}

-- Router function that handles the Home button
function MAS_AlcorMFD_Home(propId)
	local btnName = propId .. "-btn"
	local currentPage = fc.GetPersistentAsNumber(btnName)
	local handler = pageHome[currentPage]
	
	if handler ~= nil then
		return handler(propId)
	else
		return 0
	end
end
