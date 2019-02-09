-- MAS IFMS MFD scripting

local nextTerminalId = 1
local terminals = { }

local nextMFDId = 1
local mfds = { }

---------------------------------------
function IFMS_MCU_Init(propId)

	return 1
end

---------------------------------------
function IFMS_MFD_Init(propId)

	fc.InitializePersistent(propId .. "-LaunchPage", "MAS_IFMS_MFD_Launch0")
	fc.InitializePersistent(propId .. "-FlightPage", "MAS_IFMS_MFD_Flight0")
	fc.InitializePersistent(propId .. "-ManeuverPage", "MAS_IFMS_MFD_Maneuver0")

	if mechjeb.Available() > 0 then
		fc.InitializePersistent("IFMS_Launch_Alt", math.floor(mechjeb.GetDesiredLaunchAltitude() * 0.001))
		fc.InitializePersistent("IFMS_Launch_Inc", mechjeb.GetDesiredLaunchInclination())
	end

	mfds[nextMFDId] = propId
	nextMFDId = nextMFDId + 1

	return 1
end

---------------------------------------
function IFMS_Term_Init(propId)

	fc.InitializePersistent(propId .. "-ProgramPage", "MAS_IFMS_Term_Program0")

	if mechjeb.Available() > 0 then
		fc.InitializePersistent("IFMS_Launch_Alt", math.floor(mechjeb.GetDesiredLaunchAltitude() * 0.001))
		fc.InitializePersistent("IFMS_Launch_Inc", mechjeb.GetDesiredLaunchInclination())
	end

	terminals[nextTerminalId] = propId
	nextTerminalId = nextTerminalId + 1

	return 1
end

---------------------------------------
-- IFMS Terminal Launch Parameters Actions -----------------------------------

function IFMS_SetLaunchAltitude(altitude)

	if altitude < fc.BodySoI(fc.CurrentBodyIndex()) and altitude > 0 then
		--todo: fc.SetLaunchAltitude(altitude)
		mechjeb.SetDesiredLaunchAltitude(altitude)
		fc.SetPersistent("IFMS_Launch_Alt", altitude * 0.001)

		fc.SetPersistent("MAS_IFMS_Error", 0)
		fc.SetPersistent("MAS_IFMS_Launch_Buffer", 0)
		fc.SetPersistent("IFMS_Launch_Alt_OK", fc.UT() + 2)
	else
		fc.SetPersistent("MAS_IFMS_Error", fc.UT() + 4)
	end


	return 1
end

---------------------------------------
function IFMS_SetLaunchInclination(inclination)

	if inclination < 360 and inclination >= 0 then
		--todo: fc.SetLaunchInclination(inclination)
		mechjeb.SetDesiredLaunchInclination(inclination)
		fc.SetPersistent("IFMS_Launch_Inc", inclination)

		fc.SetPersistent("MAS_IFMS_Error", 0)
		fc.SetPersistent("MAS_IFMS_Launch_Buffer", 0)
		fc.SetPersistent("IFMS_Launch_Inc_OK", fc.UT() + 2)
	else
		fc.SetPersistent("MAS_IFMS_Error", fc.UT() + 4)
	end


	return 1
end

---------------------------------------
function IFMS_ToggleLaunchPilot()
	-- TODO: Selector to choose between MAS and MechJeb (when MAS can do this)
	mechjeb.ToggleAscentAutopilot()

	return 1
end

---------------------------------------
-- IFMS Reset Button ---------------------------------------------------------

function IFMS_Reset()

	fc.SetPersistent("MAS_IFMS_Reset", 1)
	fc.SetPersistent("IFMS_MechJeb_Select", 0)
	fc.SetPersistent("MAS_IFMS_Launch_Buffer", 0)
	fc.SetPersistent("MAS_IFMS_Plan_Buffer", 0)
	mechjeb.Reset()
	fc.SetAttitudePilotActive(false)
	fc.SetManeuverPilotActive(false)

	for i = 1, nextTerminalId - 1 do
		fc.LogMessage("Terminal " .. i .. " is " .. terminals[i])
		fc.SetPersistent(terminals[i], "MAS_IFMS_Term_Standby")
	end

	for i = 1, nextMFDId - 1 do
		fc.LogMessage("MFD " .. i .. " is " .. mfds[i])
		fc.SetPersistent(mfds[i], "MAS_IFMS_MFD_Standby")
	end

	return 1
end

---------------------------------------
-- IFMS Terminal Maneuver Planner Actions ------------------------------------

function IFMS_ChangeApoapsis(altitude)

	if altitude >= fc.Periapsis() and transfer.ChangeApoapsis(altitude) > 0 then
		fc.SetPersistent("MAS_IFMS_Plan_Buffer", 0)
		fc.SetPersistent("IFMS_Mnvr_Ap_OK", fc.UT() + 2)
	else
		fc.SetPersistent("MAS_IFMS_Error", fc.UT() + 4)
	end

	return 1
end

---------------------------------------
function IFMS_CircularizeOrbit(altitude)

	if altitude >= fc.Periapsis() and altitude <= fc.Apoapsis() and transfer.CircularizeAltitude(altitude) > 0 then
		fc.SetPersistent("MAS_IFMS_Plan_Buffer", 0)
		fc.SetPersistent("IFMS_Mnvr_Circ_OK", fc.UT() + 2)
	else
		fc.SetPersistent("MAS_IFMS_Error", fc.UT() + 4)
	end

	return 1
end

---------------------------------------
function IFMS_ChangePeriapsis(altitude)

	if altitude <= fc.Apoapsis() and transfer.ChangePeriapsis(altitude) > 0 then
		fc.SetPersistent("MAS_IFMS_Plan_Buffer", 0)
		fc.SetPersistent("IFMS_Mnvr_Pe_OK", fc.UT() + 2)
	else
		fc.SetPersistent("MAS_IFMS_Error", fc.UT() + 4)
	end

	return 1
end

---------------------------------------
function IFMS_PlotTransfer()

	--if  then
	--else
		fc.SetPersistent("MAS_IFMS_Error", fc.UT() + 4)
	--end

	return 1
end

---------------------------------------
function IFMS_MatchVelocity()

	--if  then
	--else
		fc.SetPersistent("MAS_IFMS_Error", fc.UT() + 4)
	--end

	return 1
end

---------------------------------------
function IFMS_ClearNode()
	if fc.ManeuverNodeExists() > 0 then
		fc.ClearManeuverNode()
	else
		fc.SetPersistent("MAS_IFMS_Error", fc.UT() + 4)
	end

	return 1
end

---------------------------------------
function IFMS_ToggleManeuverPilot()
	-- TODO: Selector to choose between MAS and MechJeb (when MAS can do this)
	--if  then
		fc.ToggleManeuverPilot()
	--else
	--	mechjeb.ToggleManeuverNodeExecutor()
	--end

	return 1
end