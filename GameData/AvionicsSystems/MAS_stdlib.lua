-- MAS_stdlib
--
-- Standard library of useful functions for Avionics Systems

mas = {}

-- Trigger the supplied /action/ method and toggle the persistent variable
-- /variable/.  This method allows a switch to rotate even if the 'get'
-- method for the action never changes - for instance, if solar panels are not
-- installed, GetSolarPanelState will always return 0, so a switch based on
-- that function will never move.
mas.ActionAndVariable = function(action, variable)
	action()
	return fc.TogglePersistent(variable)
end

-- Returns 1 if /value/ is between /bound1/ and /bound2/ (inclusive).
-- Bounds may be in any order.
mas.Between = function(value, bound1, bound2)
	if bound1 < bound2 then
		return mas.BoolToNumber(value >= bound1 and value <= bound2)
	else
		return mas.BoolToNumber(value >= bound2 and value <= bound1)
	end
end

-- Converts a logical condition to a 1 or 0 value for use in variables.
-- Returns 1 if boolValue is true, 0 otherwise.
mas.BoolToNumber = function(boolValue)
	if boolValue == true then
		return 1
	else
		return 0
	end
end

-- Clamp /value/ to the range /bound1/ to /bound2/.  Return the clamped value.
-- Bounds may be in any order.
mas.Clamp = function(value, bound1, bound2)

	if bound1 < bound2 then
		return math.max(math.min(value, bound2), bound1)
	else
		return math.max(math.min(value, bound1), bound2)
	end
end

-- TODO: Move all of these to C# / native fc for performance

-- Given a value that is treated as seconds, convert the number to the
-- hour of the day - that is [0, 6) on Kerbin or [0, 23) on Earth
mas.Hours = function(value)
	return (value / 3600) % fc.HoursPerDay()
end

-- Given a value that is treated as a seconds, convert the number into the
-- minutes of an hour (that is, (value/60) % 60) - ranges [0, 60).
mas.Minutes = function(value)
	return (value/60) % 60
end

-- Given an input such as MET or UT that is in seconds, returns the seconds 
-- of the minute for the supplied time (that is, the input value modulo 60).
-- Ranges [0, 60).
mas.Seconds = function(value)
	return value % 60
end
