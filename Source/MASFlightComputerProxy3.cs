/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2019 MOARdV
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 * 
 ****************************************************************************/
using KSP.UI;
using KSP.UI.Screens;
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    // ΔV - put this somewhere where I can find it easily to copy/paste

    /// <summary>
    /// The flight computer proxy provides the interface between the flight
    /// computer module and the variable / Lua environment.
    /// 
    /// While it is a wrapper for MASFlightComputer, not all
    /// values are plumbed through to the flight computer (for instance, the
    /// action group control and state are all handled in this class).
    /// </summary>
    /// <LuaName>fc</LuaName>
    /// <mdDoc>
    /// The `fc` group contains the core interface between KSP, Avionics
    /// Systems, and props in an IVA.  It consists of many 'information' functions
    /// that can be used to get information as well as numerous 'action' functions
    /// that are used to do things.
    /// 
    /// Due to the number of methods in the `fc` group, this document has been split
    /// across three pages:
    ///
    /// * [[MASFlightComputerProxy]] (Abort - Lights),
    /// * [[MASFlightComputerProxy2]] (Maneuver Node - Reaction Wheel), and
    /// * [[MASFlightComputerProxy3]] (Resources - Vessel Info).
    /// 
    /// **NOTE 1:** If a function listed below includes an entry for 'Supported Mod(s)',
    /// then that function will automatically use one of the mods listed to
    /// generate the data.  In some cases, it is possible that the function does not work without
    /// one of the required mods.  Those instances are noted in the function's description.
    /// 
    /// **NOTE 2:** Many descriptions make use of mathetmatical short-hand to describe
    /// a range of values.  This short-hand consists of using square brackets `[` and `]`
    /// to denote "inclusive range", while parentheses `(` and `)` indicate exclusive range.
    /// 
    /// For example, if a parameter says "an integer between [0, `fc.ExperimentCount()`)", it
    /// means that the parameter must be an integer greater than or equal to 0, but less
    /// than `fc.ExperimentCount()`.
    /// 
    /// For another example, if a parameter says "a number in the range [0, 1]", it means that
    /// the number must be at least zero, and it must not be larger than 1.
    /// </mdDoc>
    internal partial class MASFlightComputerProxy
    {
        private int electricChargeIndex = -1;

        /// <summary>
        /// The resource methods report the availability of various resources aboard the
        /// vessel.  They are grouped into three types.
        /// 
        /// 'Power' methods `PowerCurrent()`, etc,
        /// report the state of the resource identified in the MAS config file.  By default,
        /// this is `ElectricCharge`, but mods may use a different resource for power, instead.
        /// By using the 'Power' methods, IVA makers do not have to worry about adapting their
        /// IVA configurations for use with modded configurations, as long as the player has
        /// correctly configured MAS to use the alternative power name.
        /// 
        /// 'Propellant' methods track all of the active fuel types being used by ModuleEngines
        /// and ModuleEnginesFX.  Instead of reporting the current and maximum amounts in units,
        /// like the 'Resource' methods do, these methods report amounts in kilograms.  Using
        /// the propellant mass allows these methods to track alternate fuel types (such as mods
        /// using LHyd + Oxidizer), with the downside being mixed engine configurations, such as
        /// solid rockets + liquid-fueled engines) may be less helpful
        /// 
        /// 'Rcs' methods work similarly to the 'Propellant' methods, but they track resource types
        /// consumed by ModuleRCS and ModuleRCSFX.
        /// 
        /// 'Resource' methods that take a numeric parameter are ordinal resource listers.  The
        /// numeric parameter is a number from 0 to fc.ResourceCount() - 1.  This allows the
        /// IVA maker to display an alphabetized list of resources (on an MFD, for instance).
        /// 
        /// 'Resource' methods that take a string parameter return the named resource.  The name
        /// must match the `name` field of a `RESOURCE_DEFINITION` config node, or 0 will be returned.
        /// </summary>
        #region Resources

        /// <summary>
        /// Returns the amount of power this MASFlightComputer requires.
        /// </summary>
        /// <returns>The current power draw of the flight computer, in EC/sec.</returns>
        public double GetPowerDraw()
        {
            return fc.GetPowerDraw();
        }

        /// <summary>
        /// Increase or decrease the amount of power the MASFlightComputer requires.
        /// 
        /// This feature can be used to simulate subcomponents of the IVA being switched on
        /// or off.  The minimum power draw of the MASFlightComputer is defined by its `rate` field
        /// in the configuration file.  ChangePowerDraw can not be used to reduce the total
        /// power demand below `rate`.
        /// 
        /// **NOTE:** Additional power draw above `rate` is not persistent.  This function is best used by creating
        /// a TRIGGER_EVENT in the appropriate prop that uses `fc.ChangePowerDraw()` with
        /// a positive value as the `event`, and `fc.ChangePowerDraw()` with a
        /// negative values as the `exitEvent`.
        /// </summary>
        /// <param name="rateChange">The amount of additional EC/sec required to operate the IVA (positive to increase, negative to decrease).</param>
        /// <returns>The current power draw of the flight computer, in EC/sec.</returns>
        public double ChangePowerDraw(double rateChange)
        {
            return fc.ChangePowerDraw((float)rateChange);
        }

        /// <summary>
        /// Returns the current level of available power for the designated
        /// "Power" resource; by default, this is ElectricCharge.
        /// </summary>
        /// <returns>Current units of power.</returns>
        public double PowerCurrent()
        {
            if (electricChargeIndex == -1)
            {
                electricChargeIndex = vc.GetResourceIndex(MASConfig.ElectricCharge);
            }
            return vc.ResourceCurrentDirect(electricChargeIndex);
        }

        /// <summary>
        /// Returns the rate of change in available power (units/sec) for the
        /// designated "Power" resource; by default, this is ElectricCharge.
        /// </summary>
        /// <returns></returns>
        public double PowerDelta()
        {
            if (electricChargeIndex == -1)
            {
                electricChargeIndex = vc.GetResourceIndex(MASConfig.ElectricCharge);
            }
            return vc.ResourceDeltaDirect(electricChargeIndex);
        }

        /// <summary>
        /// Returns the maximum capacity of the resource defined as "power" in
        /// the config.  By default, this is ElectricCharge.
        /// </summary>
        /// <returns></returns>
        public double PowerMax()
        {
            if (electricChargeIndex == -1)
            {
                electricChargeIndex = vc.GetResourceIndex(MASConfig.ElectricCharge);
            }
            return vc.ResourceMaxDirect(electricChargeIndex);
        }

        /// <summary>
        /// Returns the current percentage of maximum capacity of the resource
        /// designated as "power" - in a stock installation, this would be
        /// ElectricCharge.
        /// </summary>
        /// <returns></returns>
        public double PowerPercent()
        {
            if (electricChargeIndex == -1)
            {
                electricChargeIndex = vc.GetResourceIndex(MASConfig.ElectricCharge);
            }
            return vc.ResourcePercentDirect(electricChargeIndex);
        }

        /// <summary>
        /// Reports whether the vessel's power percentage falls between the two listed bounds.
        /// The bounds do not need to be in numerical order.
        /// 
        /// If there is no power onboard, returns 0.  Doing so makes this
        /// function useful for alerts, for example.
        /// </summary>
        /// <param name="firstBound">The first boundary percentage, between 0 and 1.</param>
        /// <param name="secondBound">The second boundary percentage, between 0 and 1.</param>
        /// <returns>1 if the power percentage is between the listed bounds.</returns>
        public double PowerThreshold(double firstBound, double secondBound)
        {
            if (electricChargeIndex == -1)
            {
                electricChargeIndex = vc.GetResourceIndex(MASConfig.ElectricCharge);
            }

            double vesselMax = vc.ResourceMaxDirect(electricChargeIndex);
            if (vesselMax > 0.0)
            {
                double min = Math.Min(firstBound, secondBound);
                double max = Math.Max(firstBound, secondBound);
                double percent = vc.ResourcePercentDirect(electricChargeIndex);

                if (percent >= min && percent <= max)
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Reports the total quantity in units (U) of resources currently being consumed by all active engines on the vessel.
        /// </summary>
        /// <returns>The current quantity of the active propellants in the vessel, in U.</returns>
        public double PropellantCurrent()
        {
            return vc.enginePropellant.currentQuantity;
        }

        /// <summary>
        /// Reports the propellant consumption rate in U/s for all active engines on the vessel.
        /// </summary>
        /// <returns>The current propellant consumption rate, in U/s.</returns>
        public double PropellantDelta()
        {
            return vc.enginePropellant.deltaPerSecond;
        }

        /// <summary>
        /// Returns the current density of the propellant.  Note that because different propellants for different
        /// engines may be consumed at different rates, the density is only valid for the current propellant quantity.
        /// It should not be used to determine maximum propellant mass, unless you know in advance that all active
        /// propellants load are consumed at the same rate.
        /// </summary>
        /// <returns>Propellant density in kg/U.</returns>
        public double PropellantDensity()
        {
            return vc.enginePropellant.density * 1000.0;
        }

        /// <summary>
        /// Reports the current mass of the propellant, including any locked resource tanks.
        /// </summary>
        /// <returns>The current propellant mass, in kg.</returns>
        public double PropellantMass()
        {
            return (vc.enginePropellant.currentQuantity + vc.enginePropellant.reserveQuantity) * vc.enginePropellant.density * 1000.0;
        }

        /// <summary>
        /// Reports the maximum amount of propellant, in units (U), that may be carried aboard the vessel.
        /// </summary>
        /// <returns>The maximum propellant capacity, in U.</returns>
        public double PropellantMax()
        {
            return vc.enginePropellant.maxQuantity;
        }

        /// <summary>
        /// Reports the current percentage of propellant aboard the vessel.
        /// </summary>
        /// <returns>The percentage of maximum propellant capacity that contains propellant, between 0 and 1.</returns>
        public double PropellantPercent()
        {
            return (vc.enginePropellant.maxQuantity > 0.0f) ? ((vc.enginePropellant.currentQuantity + vc.enginePropellant.reserveQuantity) / vc.enginePropellant.maxQuantity) : 0.0;
        }

        /// <summary>
        /// Reports the amount of propellant, in U, that is currently locked (unavailable to engines).
        /// </summary>
        /// <returns>The locked propellant amount, in U.</returns>
        public double PropellantReserve()
        {
            return vc.enginePropellant.reserveQuantity;
        }

        /// <summary>
        /// Reports the number of propellants currently in use.
        /// </summary>
        /// <returns></returns>
        public double PropellantStageCount()
        {
            return vc.PropellantStageCount();
        }

        /// <summary>
        /// Reports the current amount of propellant, in U, available to active engines on the current stage.
        /// </summary>
        /// <returns>The current quantity of propellant accessible by the current stage, in U.</returns>
        public double PropellantStageCurrent()
        {
            return vc.enginePropellant.currentStage;
        }

        /// <summary>
        /// Reports the maximum amount of propellant available, in U, to the active engines on the
        /// current stage.  Note that locked propellant tanks are *not* reported by this function.
        /// </summary>
        /// <returns>The maximum quantity of propellant accessible by the current stage, in U.</returns>
        public double PropellantStageMax()
        {
            return vc.enginePropellant.maxStage;
        }

        /// <summary>
        /// Returns the display (localized) name of the active propellant indexed by `index`.  This call is equivalent
        /// to `fc.ResourceDisplayName(fc.PropellantStageResourceId(index))`.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.PropellantStageCount()` - 1, inclusive.</param>
        /// <returns>The name of the propellant, or an empty string for invalid indices.</returns>
        public string PropellantStageDisplayName(double index)
        {
            return vc.PropellantStageDisplayName((int)index);
        }

        /// <summary>
        /// Returns the name of the active propellant indexed by `index`.  This call is equivalent
        /// to `fc.ResourceName(fc.PropellantStageResourceId(index))`.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.PropellantStageCount()` - 1, inclusive.</param>
        /// <returns>The name of the propellant, or an empty string for invalid indices.</returns>
        public string PropellantStageName(double index)
        {
            return vc.PropellantStageName((int)index);
        }

        /// <summary>
        /// Reports the percentage of propellant remaining on the current stage for the active engines.
        /// </summary>
        /// <returns>The percentage of maximum stage propellant capacity that contains propellant, between 0 and 1.</returns>
        public double PropellantStagePercent()
        {
            return (vc.enginePropellant.maxStage > 0.0f) ? (vc.enginePropellant.currentStage / vc.enginePropellant.maxStage) : 0.0;
        }

        /// <summary>
        /// Returns the resourceId of the active propellant indexed by `index`.  This value can be used
        /// in the various Resource methods in this category.
        /// </summary>
        /// <param name="index">A number between 0 and `fc.PropellantStageCount()` - 1, inclusive.</param>
        /// <returns>The resourceId of the propellant, or -1 for invalid indices.</returns>
        public double PropellantStageResourceId(double index)
        {
            return vc.PropellantResourceId((int)index);
        }

        /// <summary>
        /// Reports whether the current stage propellant percentage falls between the two listed bounds.
        /// The bounds do not need to be in numerical order.
        /// 
        /// If there is no propellant on the current stage, returns 0.  Doing so makes this
        /// function useful for alerts, for example.
        /// </summary>
        /// <param name="firstBound">The first boundary percentage, between 0 and 1.</param>
        /// <param name="secondBound">The second boundary percentage, between 0 and 1.</param>
        /// <returns>1 if current stage propellant is between the listed bounds.</returns>
        public double PropellantStageThreshold(double firstBound, double secondBound)
        {
            if (vc.enginePropellant.maxStage > 0.0f)
            {
                double min = Math.Min(firstBound, secondBound);
                double max = Math.Max(firstBound, secondBound);
                double percent = vc.enginePropellant.currentStage / vc.enginePropellant.maxStage;

                if (percent >= min && percent <= max)
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Reports whether the vessel's available propellant percentage falls between the two listed bounds.
        /// The bounds do not need to be in numerical order.
        /// 
        /// If there is no propellant or active engines, returns 0.  Doing so makes this
        /// function useful for alerts, for example.
        /// 
        /// Reserve propellant is not included in this computation.
        /// </summary>
        /// <param name="firstBound">The first boundary percentage, between 0 and 1.</param>
        /// <param name="secondBound">The second boundary percentage, between 0 and 1.</param>
        /// <returns>1 if propellant is between the listed bounds.</returns>
        public double PropellantThreshold(double firstBound, double secondBound)
        {
            if (vc.enginePropellant.maxQuantity > 0.0f)
            {
                double min = Math.Min(firstBound, secondBound);
                double max = Math.Max(firstBound, secondBound);
                double percent = vc.enginePropellant.currentQuantity / vc.enginePropellant.maxQuantity;

                if (percent >= min && percent <= max)
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Tracks the current units of all resources consumed by installed RCS thrusters.
        /// </summary>
        /// <returns>Total RCS propellant quantity in units.</returns>
        public double RcsCurrent()
        {
            return vc.rcsPropellant.currentQuantity;
        }

        /// <summary>
        /// Tracks the current resource consumption rate by installed RCS thrusters.
        /// </summary>
        /// <returns>RCS propellant consumption rate in units/s.</returns>
        public double RcsDelta()
        {
            return vc.rcsPropellant.deltaPerSecond;
        }

        /// <summary>
        /// Reports the current density of propellant for all RCS thrusters.
        /// </summary>
        /// <returns>RCS propellant consumption rate in kg/U.</returns>
        public double RcsDensity()
        {
            return vc.rcsPropellant.density * 1000.0;
        }

        /// <summary>
        /// Reports the current mass of RCS propellant accessible by the current stage.
        /// </summary>
        /// <returns>Current RCS propellant mass, in kg.</returns>
        public double RcsMass()
        {
            return vc.rcsPropellant.currentStage * vc.rcsPropellant.density * 1000.0;
        }

        /// <summary>
        /// Tracks the total units that can be carried in all RCS propellant tanks.
        /// </summary>
        /// <returns>Maximum propellant capacity in units.</returns>
        public double RcsMax()
        {
            return vc.rcsPropellant.maxQuantity;
        }

        /// <summary>
        /// Tracks the percentage of total RCS propellant currently onboard.
        /// </summary>
        /// <returns>Current RCS propellant supply, between 0 and 1.</returns>
        public double RcsPercent()
        {
            return (vc.rcsPropellant.maxQuantity > 0.0f) ? ((vc.rcsPropellant.currentQuantity + vc.rcsPropellant.reserveQuantity) / vc.rcsPropellant.maxQuantity) : 0.0;
        }

        /// <summary>
        /// Returns the number of units of RCS propellant that is currently locked and unavailable.
        /// </summary>
        /// <returns>Reserve RCS propellant, in U.</returns>
        public double RcsReserve()
        {
            return vc.rcsPropellant.reserveQuantity;
        }

        /// <summary>
        /// Reports the current amount of RCS propellant available to the active stage.
        /// </summary>
        /// <returns>Available RCS propellant, in units.</returns>
        public double RcsStageCurrent()
        {
            return vc.rcsPropellant.currentStage;
        }

        /// <summary>
        /// Reports the maximum amount of RCS propellant storage accessible by the current stage.
        /// </summary>
        /// <returns>Maximum stage RCS propellant mass, in units.</returns>
        public double RcsStageMax()
        {
            return vc.rcsPropellant.maxStage;
        }

        /// <summary>
        /// Reports the percentage of RCS propellant mass available to the current stage.
        /// </summary>
        /// <returns>Current stage percentage, between 0 and 1.</returns>
        public double RcsStagePercent()
        {
            return (vc.rcsPropellant.maxStage > 0.0f) ? (vc.rcsPropellant.currentStage / vc.rcsPropellant.maxStage) : 0.0;
        }

        /// <summary>
        /// Reports whether the current stage RCS propellant percentage falls between the two listed bounds.
        /// The bounds do not need to be in numerical order.
        /// 
        /// If there is no RCS propellant on the current stage, returns 0.  Doing so makes this
        /// function useful for alerts, for example.
        /// </summary>
        /// <param name="firstBound">The first boundary percentage, between 0 and 1.</param>
        /// <param name="secondBound">The second boundary percentage, between 0 and 1.</param>
        /// <returns>1 if current stage RCS propellant is between the listed bounds.</returns>
        public double RcsStageThreshold(double firstBound, double secondBound)
        {
            if (vc.rcsPropellant.maxStage > 0.0f)
            {
                double min = Math.Min(firstBound, secondBound);
                double max = Math.Max(firstBound, secondBound);
                double percent = vc.rcsPropellant.currentStage / vc.rcsPropellant.maxStage;

                if (percent >= min && percent <= max)
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Reports whether the vessel's RCS propellant percentage falls between the two listed bounds.
        /// The bounds do not need to be in numerical order.
        /// 
        /// If there is no RCS propellant, returns 0.  Doing so makes this
        /// function useful for alerts, for example.
        /// 
        /// This function does not count reserve quantities.
        /// </summary>
        /// <param name="firstBound">The first boundary percentage, between 0 and 1.</param>
        /// <param name="secondBound">The second boundary percentage, between 0 and 1.</param>
        /// <returns>1 if RCS propellant is between the listed bounds.</returns>
        public double RcsThreshold(double firstBound, double secondBound)
        {
            if (vc.rcsPropellant.maxQuantity > 0.0f)
            {
                double min = Math.Min(firstBound, secondBound);
                double max = Math.Max(firstBound, secondBound);
                double percent = vc.rcsPropellant.currentQuantity / vc.rcsPropellant.maxQuantity;

                if (percent >= min && percent <= max)
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the total number of resources found on this vessel.
        /// </summary>
        /// <returns></returns>
        public double ResourceCount()
        {
            return vc.ResourceCount();
        }

        /// <summary>
        /// Returns the current available amount of the selected resource.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns></returns>
        public double ResourceCurrent(object resourceId)
        {
            return vc.ResourceCurrent(resourceId);
        }

        /// <summary>
        /// Returns the instantaneous change-per-second of the selected resource,
        /// or zero if the resource is invalid.
        /// 
        /// A positive number means the resource is being consumed (burning fuel,
        /// for instance).
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns></returns>
        public double ResourceDelta(object resourceId)
        {
            return vc.ResourceDelta(resourceId);
        }

        /// <summary>
        /// Returns the density of the selected resource, or zero if it is invalid.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns>Density in kg / unit</returns>
        public double ResourceDensity(object resourceId)
        {
            return vc.ResourceDensity(resourceId) * 1000.0;
        }

        /// <summary>
        /// Returns the display (localized) name of the selected resource, or an empty string if it doesn't
        /// exist.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns></returns>
        public string ResourceDisplayName(object resourceId)
        {
            return vc.ResourceDisplayName(resourceId);
        }

        /// <summary>
        /// Returns 1 if resourceId is valid (there is a resource with that
        /// id on the craft).
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns></returns>
        public double ResourceExists(object resourceId)
        {
            return vc.ResourceExists(resourceId);
        }

        /// <summary>
        /// Returns 1 if the identified resource is currently marked as a propellant.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns>1 if the resource is currently a propellant, 0 otherwise.</returns>
        public double ResourceIsPropellant(object resourceId)
        {
            return (vc.ResourceIsPropellant(resourceId)) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if the selected resource is locked (can not transfer resources) anywhere
        /// on the vessel.  Returns 0 if the part is unlocked, or if it is not present.
        /// 
        /// Use -1 to query if any resource is locked.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.  Use -1 to query if any resource is locked.</param>
        /// <returns></returns>
        public double ResourceLocked(object resourceId)
        {
            if (resourceId is double)
            {
                if ((double)resourceId == -1.0)
                {
                    return vc.AnyResourceLocked();
                }
            }

            return (vc.ResourceLocked(resourceId)) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the current mass of the selected resource in kg.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns></returns>
        public double ResourceMass(object resourceId)
        {
            return vc.ResourceMass(resourceId) * 1000.0;
        }

        /// <summary>
        /// Returns the maximum mass of the selected resource.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns></returns>
        public double ResourceMassMax(object resourceId)
        {
            return vc.ResourceMassMax(resourceId);
        }

        /// <summary>
        /// Returns the maximum quantity of the selected resource.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns></returns>
        public double ResourceMax(object resourceId)
        {
            return vc.ResourceMax(resourceId);
        }

        /// <summary>
        /// Returns the name of the selected resource, or an empty string if it doesn't
        /// exist.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns></returns>
        public string ResourceName(object resourceId)
        {
            return vc.ResourceName(resourceId);
        }

        /// <summary>
        /// Returns the amount of the selected resource remaining as a percentage in
        /// the range [0, 1].
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns></returns>
        public double ResourcePercent(object resourceId)
        {
            return vc.ResourcePercent(resourceId);
        }

        /// <summary>
        /// Returns the amount of the selected resource that is marked as unavailable for use (locked).
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns>The amount of the selected resource that is locked, or 0.</returns>
        public double ResourceReserve(object resourceId)
        {
            return vc.ResourceReserve(resourceId);
        }

        /// <summary>
        /// Returns the current amount of the selected resource in the current stage.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns></returns>
        public double ResourceStageCurrent(object resourceId)
        {
            return vc.ResourceStageCurrent(resourceId);
        }

        /// <summary>
        /// Returns the current mass of the selected resource in the current stage.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns></returns>
        public double ResourceStageMass(object resourceId)
        {
            return vc.ResourceStageMass(resourceId);
        }

        /// <summary>
        /// Returns the maximum mass of the selected resource in the current stage.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns></returns>
        public double ResourceStageMassMax(object resourceId)
        {
            return vc.ResourceStageMassMax(resourceId);
        }

        /// <summary>
        /// Returns the max amount of the selected resource in the current stage.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns></returns>
        public double ResourceStageMax(object resourceId)
        {
            return vc.ResourceStageMax(resourceId);
        }

        /// <summary>
        /// Returns the max amount of the selected resource in the current stage.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns></returns>
        public double ResourceStagePercent(object resourceId)
        {
            double stageMax = vc.ResourceStageMax(resourceId);
            if (stageMax > 0.0)
            {
                return vc.ResourceStageCurrent(resourceId) / stageMax;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Reports whether the named resource's current stage percentage falls between the two listed bounds.
        /// The bounds do not need to be in numerical order.
        /// 
        /// If there is no such resource on the current stage, returns 0.  Doing so makes this
        /// function useful for alerts, for example.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <param name="firstBound">The first boundary percentage, between 0 and 1.</param>
        /// <param name="secondBound">The second boundary percentage, between 0 and 1.</param>
        /// <returns>1 if current stage resource percentage is between the listed bounds.</returns>
        public double ResourceStageThreshold(object resourceId, double firstBound, double secondBound)
        {
            double stageMax = vc.ResourceStageMax(resourceId);
            if (stageMax > 0.0f)
            {
                double min = Math.Min(firstBound, secondBound);
                double max = Math.Max(firstBound, secondBound);
                double percent = vc.ResourceStageCurrent(resourceId) / stageMax;

                if (percent >= min && percent <= max)
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Reports whether the vessel's total resource percentage falls between the two listed bounds.
        /// The bounds do not need to be in numerical order.
        /// 
        /// If there is no resource capacity onboard, returns 0.  Doing so makes this
        /// function useful for alerts, for example.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <param name="firstBound">The first boundary percentage, between 0 and 1.</param>
        /// <param name="secondBound">The second boundary percentage, between 0 and 1.</param>
        /// <returns>1 if the resource percentage is between the listed bounds.</returns>
        public double ResourceThreshold(object resourceId, double firstBound, double secondBound)
        {
            double vesselMax = vc.ResourceMax(resourceId);
            if (vesselMax > 0.0f)
            {
                double min = Math.Min(firstBound, secondBound);
                double max = Math.Max(firstBound, secondBound);
                double percent = vc.ResourcePercent(resourceId);

                if (percent >= min && percent <= max)
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Sets all resources aboard the vessel as available.
        /// </summary>
        /// <returns>1 if any resources were unlocked, 0 if all resources were already available.</returns>
        public double SetAllResourcesUnlocked()
        {
            return vc.UnlockAllResources();
        }

        /// <summary>
        /// Controls whether `resourceId` can be consumed.
        /// 
        /// When `lockResource` is true, all remaining resources of
        /// that type on the vessel are locked (unavailable).  When
        /// `lockResource` is false, resources may be consumed.
        /// 
        /// Note that this will toggle *all* resource containers on the vessel.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <param name="lockResource">When true, prevents that resource from being consumed.  When false, allows that resource to be consumed.</param>
        /// <returns>1 if the resource is present on the vessel, 0 otherwise.</returns>
        public double SetResourceLock(object resourceId, bool lockResource)
        {
            return vc.LockResource(resourceId, lockResource);
        }

        /// <summary>
        /// Toggles the lock / unlock state of each resource container for `resourceId`.
        /// Locked resources are unlocked, unlocked resources are locked.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns>1 if the resource is present on the vessel, 0 otherwise.</returns>
        public double ToggleResourceLock(object resourceId)
        {
            return vc.ToggleResource(resourceId);
        }

        /// <summary>
        /// Returns 1 when there is at least 0.0001 units of power available
        /// to the craft.  By default, 'power' is the ElectricCharge resource,
        /// but users may change that in the MAS config file.
        /// </summary>
        /// <returns>1 if there is ElectricCharge, 0 otherwise.</returns>
        public double VesselPowered()
        {
            if (electricChargeIndex == -1)
            {
                // We have to poll it here because the value may not be initialized
                // when we're in Start().
                electricChargeIndex = vc.GetResourceIndex(MASConfig.ElectricCharge);
            }
            return (vc.ResourceCurrentDirect(electricChargeIndex) > 0.0001) ? 1.0 : 0.0;
        }
        #endregion

        /// <summary>
        /// The Resource Converter category allows an IVA creator to register to track specific resources
        /// converters (ModuleResourceConverter) that are of interest to the IVA.  By default, the
        /// MASFlightComputer installs a Resource Converter tracker for "ElectricCharge" (or whatever
        /// override resource is configured in the persistent file) as `id` number 0.  This tracker
        /// is used to provide information for the Fuel Cell functions (FuelCellCount(), etc).
        /// 
        /// Any number of trackers may be installed.  The only requirement is that each must be given a
        /// unique `id` that is a positive integer (1 or higher).  When assigning a ModuleResourceConverter
        /// that has multiple outputs to a group, MAS will place it in the group with the highest `id`.
        /// 
        /// For example, say a ModuleResourceConverter took "Water" as an input, and it generated "LqdHydrogen"
        /// and "Oxidizer" for output.  If Resource Converter group 1 tracked "LqdHydrogen" output and group 2
        /// tracked "Oxidizer", then this resource converter would be assigned to group 2, since it is the
        /// highest priority resource of the two listed.
        /// 
        /// If an IVA tries to register more than one resource type to the same `id`, only the first
        /// one found will be registered.  For instance, if a MASFlightComputer script attempts to
        /// call `fc.TrackResourceConverter(1, "LqdHydrogen")` and then calls `fc.TrackResourceConverter(1, "Oxidizer")`,
        /// then group 1 will track "LqdHydrogen".  MAS will return a -1 on the second call to indicate that
        /// the requested group id is already in use with a different resource.
        /// </summary>
        #region Resource Converter
        /// <summary>
        /// Returns 1 if at least one resource converter in the group selected by `id` is enabled; 0 otherwise.
        /// </summary>
        /// <param name="id">The id number of the resource converter group to query.  Must be an integer 0 or larger.</param>
        /// <returns>1 if any selected resource converter is switched on; 0 otherwise.</returns>
        public double GetResourceConverterActive(double id)
        {
            int idNum = (int)id;

            var rc = vc.resourceConverterList.Find(x => x.id == idNum);
            if (rc != null)
            {
                return (rc.converterActive) ? 1.0 : 0.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the number of resource converters that generate the output selected by the
        /// resource converter group `id`.
        /// </summary>
        /// <param name="id">The id number of the resource converter group to query.  Must be an integer 0 or larger.</param>
        /// <returns></returns>
        public double ResourceConverterCount(double id)
        {
            int idNum = (int)id;

            var rc = vc.resourceConverterList.Find(x => x.id == idNum);
            if (rc != null)
            {
                return rc.moduleConverter.Length;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the current output of installed fuel cells.
        /// </summary>
        /// <param name="id">The id number of the resource converter group to query.  Must be an integer 0 or larger.</param>
        /// <returns>Units of the resource generated per second.</returns>
        public double ResourceConverterOutput(double id)
        {
            int idNum = (int)id;

            var rc = vc.resourceConverterList.Find(x => x.id == idNum);
            if (rc != null)
            {
                return rc.netOutput;
            }

            return 0.0;
        }

        /// <summary>
        /// Sets the resource converter group selected by `id` on or off.
        /// </summary>
        /// <param name="id">The id number of the resource converter group to query.  Must be an integer 0 or larger.</param>
        /// <returns>1 if resource converters are now active, 0 if they're off or they could not be toggled.</returns>
        public double SetResourceConverterActive(double id, bool newState)
        {
            int idNum = (int)id;

            var rc = vc.resourceConverterList.Find(x => x.id == idNum);
            if (rc != null)
            {
                bool state = rc.converterActive;
                bool anyChanged = false;
                for (int i = rc.moduleConverter.Length - 1; i >= 0; --i)
                {
                    if (!rc.moduleConverter[i].AlwaysActive && state != newState)
                    {
                        anyChanged = true;
                        if (newState)
                        {
                            rc.moduleConverter[i].StartResourceConverter();
                        }
                        else
                        {
                            rc.moduleConverter[i].StopResourceConverter();
                        }
                    }
                }

                return (state && anyChanged) ? 1.0 : 0.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Toggles the resource converter group selected by `id` on or off.
        /// </summary>
        /// <param name="id">The id number of the resource converter group to query.  Must be an integer 0 or larger.</param>
        /// <returns>1 if resource converters are now active, 0 if they're off or they could not be toggled.</returns>
        public double ToggleResourceConverterActive(double id)
        {
            int idNum = (int)id;

            var rc = vc.resourceConverterList.Find(x => x.id == idNum);
            if (rc != null)
            {
                bool state = !rc.converterActive;
                bool anyChanged = false;
                for (int i = rc.moduleConverter.Length - 1; i >= 0; --i)
                {
                    if (!rc.moduleConverter[i].AlwaysActive)
                    {
                        anyChanged = true;
                        if (state)
                        {
                            rc.moduleConverter[i].StartResourceConverter();
                        }
                        else
                        {
                            rc.moduleConverter[i].StopResourceConverter();
                        }
                    }
                }

                return (state && anyChanged) ? 1.0 : 0.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Registers a family of Resource Converters with MAS.  A resource converter is defined by two components:
        /// the `id` and the `resourceName`.
        /// 
        /// The `id` defines the priority, with larger numbers indicating
        /// a higher priority.  When a specific ModuleResourceConverter has more than one output,
        /// MAS will add the module to the highest-priority tracked resource converter.
        /// 
        /// For example, if a particular resource converter outputs "LiquidFuel" and "Oxidizer", and
        /// "LiquidFuel" is registered as `id = 1`, and "Oxidizer" is registered as `id = 2`, then MAS
        /// will treat that resource converter as `id = 2` with an "Oxidizer" output.
        /// 
        /// If more than one call to `TrackResourceConverter` uses the same `id` with different `resourceName`
        /// values, only the first such call applies.  Additional calls using a different `resourceName` will
        /// have no effect, and `TrackResourceConverter()` will return -1.
        /// 
        /// `id` must be a positive number.  `id = 0` is reserved for "ElectricCharge", which corresponds with
        /// the Fuel Cell methods.  If a call to `TrackResourceConverter` uses "ElectricCharge" with an `id`
        /// greater than zero, then the Fuel Cell methods will behave as if no fuel cells
        /// are installed.
        /// 
        /// The `resourceName` must be one of the `name` fields for a `RESOURCE_DEFINITION`, such as
        /// "ElectricCharge" in GameData/Squad/Resources/ResourcesGeneric.cfg.  If an invalid `resourceName`
        /// is provided (for instance, if it is for a resource included in a mod that is not installed),
        /// there are no errors - MAS behaves as if there are no relevant resource converters.
        /// 
        /// </summary>
        /// <param name="id">The id number to assign to this resource.  Must be an integer 1 or larger.</param>
        /// <param name="resourceName">The name of the resource (from the `name` field of a RESOURCE_DEFINITION).</param>
        /// <returns>1 if the converter was registered (or was already registered with the same id), -1 if a resource converter was registered using that `id` with a different `resourceName`,
        /// and 0 if an invalid `id` was provided.</returns>
        public double TrackResourceConverter(double id, string resourceName)
        {
            int idNum = (int)id;
            if (idNum < 1)
            {
                return 0.0;
            }
            resourceName = resourceName.Trim();

            MASVesselComputer.GeneralPurposeResourceConverter rc = vc.resourceConverterList.Find(x => x.id == idNum);
            if (rc == null)
            {
                rc = new MASVesselComputer.GeneralPurposeResourceConverter();
                rc.id = idNum;
                rc.outputResource = resourceName;

                int prevIdx = vc.resourceConverterList.FindLastIndex(x => x.id < idNum);
                if (prevIdx == -1 || prevIdx == vc.resourceConverterList.Count - 1)
                {
                    vc.resourceConverterList.Add(rc);
                }
                else
                {
                    vc.resourceConverterList.Insert(prevIdx + 1, rc);
                }
                // Force rescanning the resource converter list now that we've added something new.
                vc.modulesInvalidated = true;
                return 1.0;
            }
            else
            {
                if (rc.outputResource == resourceName)
                {
                    return 1.0;
                }
                else
                {
                    return -1.0;
                }
            }
        }
        #endregion

        /// <summary>
        /// The SAS section provides methods to control and query the state of
        /// a vessel's SAS stability system.
        /// </summary>
        #region SAS
        /// <summary>
        /// Returns whether the controls are configured for precision mode.
        /// </summary>
        /// <returns>1 if the controls are in precision mode, 0 if they are not.</returns>
        public double GetPrecisionMode()
        {
            return (FlightInputHandler.fetch.precisionMode) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if SAS is on, 0 otherwise.
        /// </summary>
        /// <returns></returns>
        public double GetSAS()
        {
            return (vessel.ActionGroups[KSPActionGroup.SAS]) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns a number representing the SAS mode:
        ///
        /// * 0 = StabilityAssist
        /// * 1 = Prograde
        /// * 2 = Retrograde
        /// * 3 = Normal
        /// * 4 = Anti-Normal
        /// * 5 = Radial In
        /// * 6 = Radial Out
        /// * 7 = Target
        /// * 8 = Anti-Target
        /// * 9 = Maneuver Node
        /// </summary>
        /// <returns>A number between 0 and 9, inclusive.</returns>
        public double GetSASMode()
        {
            double mode;
            switch (autopilotMode)
            {
                case VesselAutopilot.AutopilotMode.StabilityAssist:
                    mode = 0.0;
                    break;
                case VesselAutopilot.AutopilotMode.Prograde:
                    mode = 1.0;
                    break;
                case VesselAutopilot.AutopilotMode.Retrograde:
                    mode = 2.0;
                    break;
                case VesselAutopilot.AutopilotMode.Normal:
                    mode = 3.0;
                    break;
                case VesselAutopilot.AutopilotMode.Antinormal:
                    mode = 4.0;
                    break;
                case VesselAutopilot.AutopilotMode.RadialIn:
                    mode = 6.0; // RadialIn and RadialOut appear to be backwards?!?
                    break;
                case VesselAutopilot.AutopilotMode.RadialOut:
                    mode = 5.0; // RadialIn and RadialOut appear to be backwards?!?
                    break;
                case VesselAutopilot.AutopilotMode.Target:
                    mode = 7.0;
                    break;
                case VesselAutopilot.AutopilotMode.AntiTarget:
                    mode = 8.0;
                    break;
                case VesselAutopilot.AutopilotMode.Maneuver:
                    mode = 9.0;
                    break;
                default:
                    mode = 0.0;
                    break;
            }

            return mode;
        }

        /// <summary>
        /// Return the current speed display mode: 1 for orbit, 0 for surface,
        /// and -1 for target.
        /// </summary>
        /// <returns></returns>
        public double GetSASSpeedMode()
        {
            var mode = FlightGlobals.speedDisplayMode;

            if (mode == FlightGlobals.SpeedDisplayModes.Orbit)
            {
                return 1.0;
            }
            else if (mode == FlightGlobals.SpeedDisplayModes.Target)
            {
                return -1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if the SAS action group has actions assigned to it.
        /// </summary>
        /// <returns></returns>
        public double SASHasActions()
        {
            return (vc.GroupHasActions(KSPActionGroup.SAS)) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Activates or deactivates precision control mode.
        /// </summary>
        /// <param name="state">'true' to enable precision control, 'false' to disable it.</param>
        /// <returns>1 if precision mode is now on, 0 if it is now off.</returns>
        public double SetPrecisionMode(bool state)
        {
            if (state != FlightInputHandler.fetch.precisionMode)
            {
                FlightInputHandler.fetch.precisionMode = state;

                var gauges = UnityEngine.Object.FindObjectOfType<KSP.UI.Screens.Flight.LinearControlGauges>();
                if (gauges != null)
                {
                    for (int i = gauges.inputGaugeImages.Count - 1; i >= 0; --i)
                    {
                        gauges.inputGaugeImages[i].color = (state) ? XKCDColors.BrightCyan : XKCDColors.Orange;
                    }
                }
            }

            return (state) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Set the SAS state to on or off per the parameter.
        /// </summary>
        /// <param name="active"></param>
        /// <returns>1 if the SAS action group is on, 0 otherwise.</returns>
        public double SetSAS(bool active)
        {
            vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, active);
            return (active) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Set the SAS mode.  Note that while you can set this mode when SAS is off, KSP
        /// sets it back to Stability Assist when SAS is switched on.  Valid modes are:
        /// 
        /// * 0 = StabilityAssist
        /// * 1 = Prograde
        /// * 2 = Retrograde
        /// * 3 = Normal
        /// * 4 = Anti-Normal
        /// * 5 = Radial In
        /// * 6 = Radial Out
        /// * 7 = Target
        /// * 8 = Anti-Target
        /// * 9 = Maneuver Node
        /// </summary>
        /// <param name="mode">One of the modes listed above.  If an invalid value is provided, Stability Assist is set.</param>
        /// <returns>1 if the mode was set, 0 if an invalid mode was specified</returns>
        public double SetSASMode(double mode)
        {
            int iMode = (int)mode;
            double returnVal = 1.0;
            switch (iMode)
            {
                case 0:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.StabilityAssist);
                    break;
                case 1:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.Prograde);
                    break;
                case 2:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.Retrograde);
                    break;
                case 3:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.Normal);
                    break;
                case 4:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.Antinormal);
                    break;
                case 5:
                    // RadialIn and RadialOut appear to be backwards?!?
                    TrySetSASMode(VesselAutopilot.AutopilotMode.RadialOut);
                    break;
                case 6:
                    // RadialIn and RadialOut appear to be backwards?!?
                    TrySetSASMode(VesselAutopilot.AutopilotMode.RadialIn);
                    break;
                case 7:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.Target);
                    break;
                case 8:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.AntiTarget);
                    break;
                case 9:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.Maneuver);
                    break;
                default:
                    TrySetSASMode(VesselAutopilot.AutopilotMode.StabilityAssist);
                    returnVal = 0.0;
                    break;
            }

            return returnVal;
        }

        /// <summary>
        /// Toggle precision control mode
        /// </summary>
        /// <returns>1 if precision mode is now on, 0 if it is now off.</returns>
        public double TogglePrecisionMode()
        {
            bool state = !FlightInputHandler.fetch.precisionMode;

            FlightInputHandler.fetch.precisionMode = state;

            var gauges = UnityEngine.Object.FindObjectOfType<KSP.UI.Screens.Flight.LinearControlGauges>();
            if (gauges != null)
            {
                for (int i = gauges.inputGaugeImages.Count - 1; i >= 0; --i)
                {
                    gauges.inputGaugeImages[i].color = (state) ? XKCDColors.BrightCyan : XKCDColors.Orange;
                }
            }

            return (state) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Toggles SAS on-to-off or vice-versa
        /// </summary>
        /// <returns>1 if SAS is now on, 0 if it is now off.</returns>
        public double ToggleSAS()
        {
            vessel.ActionGroups.ToggleGroup(KSPActionGroup.SAS);
            return (vessel.ActionGroups[KSPActionGroup.SAS]) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Toggles the SAS speed mode.
        /// </summary>
        /// <returns>The new speed mode (see `fc.GetSASSpeedMode()`).</returns>
        public double ToggleSASSpeedMode()
        {
            FlightGlobals.CycleSpeedModes();
            return GetSASSpeedMode();
        }

        [MoonSharpHidden]
        private void TrySetSASMode(VesselAutopilot.AutopilotMode mode)
        {
            if (vessel.Autopilot.CanSetMode(mode))
            {
                fc.ap.DisengageAutopilots();

                vessel.Autopilot.SetMode(mode);

                if (SASbtns == null)
                {
                    SASbtns = UnityEngine.Object.FindObjectOfType<VesselAutopilotUI>().modeButtons;
                }
                // set our mode, note it takes the mode as an int, generally top to bottom, left to right, as seen on the screen. Maneuver node being the exception, it is 9
                SASbtns[(int)mode].SetState(true);
            }
        }
        #endregion


        /// <summary>
        /// The Science category provides interaction with science experiments, categories of science experiments,
        /// and data transmitters.
        /// 
        /// The `Experiment` functions interact with individual experiments on the vessel, such as a Crew Report in the
        /// Command Pod.  The `ScienceType` functions interact with categories of experiments.
        /// 
        /// Data Transmitters are any transmitter on the vessel capable of transmitting science.  Not every transmitter
        /// can send science, however (such as the default transmitter found in a Command Pod), so the transmitters
        /// that MAS reports will not be all of the transmitters on the vessel.
        /// 
        /// Note that the functions that run an experiment always trigger the KSP experiment dialog, even if the UI overlays
        /// are switched off.  The dialog's buttons do not intercept the button presses, so it won't have any side-effects other
        /// than possibly confusion when he player enables the UI later.
        /// </summary>
        #region Science
        // For a bootstrap to interpreting science values, see https://github.com/KerboKatz/AutomatedScienceSampler/blob/master/source/AutomatedScienceSampler/DefaultActivator.cs

        /// <summary>
        /// Tell the science container identified by `scienceContainerId` to fill itself
        /// with as many experiments as possible.
        /// </summary>
        /// <param name="scienceContainerId">An integer between [0, `fc.ScienceContainerCount()`).</param>
        /// <returns>1 if a valid science container was selected, 0 otherwise.</returns>
        public double CollectExperiments(double scienceContainerId)
        {
            int idx = (int)scienceContainerId;
            if (idx >= 0 && idx < vc.scienceContainer.Length)
            {
                vc.scienceContainer[idx].CollectAllEvent();
                return 1.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Indicates whether the selected transmitter is available for transmitting science.
        /// 
        /// **TODO:** This function only checks if the transmitter is busy.  It should also check
        /// transmission ranges, once I find out how to do that.
        /// </summary>
        /// <param name="transmitterId">An integer in the range [0, `fc.DataTransmitterCount()`).</param>
        /// <returns>1 if the selected transmitter is available to send data, 0 if it is not, or an invalid `transmitterId` was provided.</returns>
        public double DataTransmitterAvailable(double transmitterId)
        {
            int idx = (int)transmitterId;
            if (idx >= 0 && idx < vc.moduleTransmitter.Length)
            {
                // Note the reversed values - converting from IsBusy to !IsBusy.
                return vc.moduleTransmitter[idx].IsBusy() ? 0.0 : 1.0;
            }
            return 0.0;
        }

        /// <summary>
        /// Returns the number of data transmitters aboard the vessel that are capable of transmitting science.
        /// </summary>
        /// <returns>An integer 0 or larger.</returns>
        public double DataTransmitterCount()
        {
            return vc.moduleTransmitter.Length;
        }

        /// <summary>
        /// Dump all of the data stored in the selected science container.
        /// </summary>
        /// <param name="scienceContainerId">An integer between [0, `fc.ScienceContainerCount()`).</param>
        /// <returns>1 if data was dumped, 0 if the container was empty or an invalid container was selected.</returns>
        public double DumpScienceContainer(double scienceContainerId)
        {
            int idx = (int)scienceContainerId;
            if (idx >= 0 && idx < vc.scienceContainer.Length)
            {
                ModuleScienceContainer msc = vc.scienceContainer[idx];
                if (msc.GetStoredDataCount() > 0)
                {
                    ScienceData[] data = msc.GetData();
                    for (int i = data.Length - 1; i >= 0; --i)
                    {
                        msc.DumpData(data[i]);
                    }
                    return 1.0;
                }
            }
            return 0.0;
        }

        /// <summary>
        /// Checks to see if the vessel currently has data for the selected experiment in the
        /// current circumstances (situation, biome, etc).
        /// </summary>
        /// <param name="experimentId">An integer between 0 and `fc.ExperimentTotal()` - 1, inclusive.</param>
        /// <returns>1 if the vessel already has equivalent data stored, 0 if it does not, or the experimentId is invalid.</returns>
        public double DuplicateExperiment(double experimentId)
        {
            int id = (int)experimentId;
            if (id >= 0 && id < vc.moduleScienceExperiment.Length)
            {
                var experiment = vc.moduleScienceExperiment[id];

                // XXX: This is grossly ineffecient, since I create a ScienceData object
                // every time this is called.  I need to reverse engineer the subjectId
                // algorithm, or figure out what helper function creates it.
                ScienceData sd = vc.GenerateScienceData(experiment.experiment, experiment.xmitDataScalar);

                var st = Array.Find(vc.scienceType, x => x.type.id == experiment.experiment.id);

                // It looks like some modules don't have a valid experiment.id field at startup,
                // so we have to do a null check here.  If we see null, we will signal to the vessel
                // computer to check again on the science type data at the next FixedUpdate.
                if (st != null)
                {
                    var exp = st.experiments;

                    // Iterate over the science experiments to see if one already contains this data.
                    for (int i = exp.Count - 1; i >= 0; --i)
                    {
                        if (exp[i].Deployed)
                        {
                            ScienceData[] data = exp[i].GetData();
                            int duplicateIdx = Array.FindIndex(data, s => s.subjectID == sd.subjectID);
                            if (duplicateIdx >= 0)
                            {
                                return 1.0;
                            }
                        }
                    }

                    for (int i = vc.scienceContainer.Length - 1; i >= 0; --i)
                    {
                        ScienceData[] data = vc.scienceContainer[i].GetData();
                        int duplicateIdx = Array.FindIndex(data, s => s.subjectID == sd.subjectID);
                        if (duplicateIdx >= 0)
                        {
                            return 1.0;
                        }
                    }

                }
                else
                {
                    //Utility.LogMessage(this, "st is null for {0} - triggering update:", experiment.experiment.id);
                    //foreach (var ss in vc.scienceType)
                    //{
                    //    Utility.LogMessage(this, "...{0}", ss.type.id);
                    //}
                    vc.scienceInvalidated = true;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Checks to see if the vessel already has data for the selected science type in the current circumstances
        /// (situation, biome).
        /// </summary>
        /// <param name="scienceTypeId">An integer in the range [0, `fc.ScienceTypeTotal()`).</param>
        /// <returns>1 if the vessel already has equivalent data stored, 0 if it does not, or the scienceTypeId is invalid.</returns>
        public double DuplicateScienceType(double scienceTypeId)
        {
            int id = (int)scienceTypeId;
            if (id >= 0 && id < vc.scienceType.Length)
            {
                var exp = vc.scienceType[id].experiments;

                // XXX: This is grossly ineffecient, since I create a ScienceData object
                // every time this is called.  I need to reverse engineer the subjectId
                // algorithm, or figure out what helper function creates it.
                ScienceData sd = vc.GenerateScienceData(exp[0].experiment, exp[0].xmitDataScalar);

                // Iterate over the science experiments to see if one already contains this data.
                for (int i = exp.Count - 1; i >= 0; --i)
                {
                    if (exp[i].Deployed)
                    {
                        ScienceData[] data = exp[i].GetData();
                        int duplicateIdx = Array.FindIndex(data, s => s.subjectID == sd.subjectID);
                        if (duplicateIdx >= 0)
                        {
                            return 1.0;
                        }
                    }
                }

                for (int i = vc.scienceContainer.Length - 1; i >= 0; --i)
                {
                    ScienceData[] data = vc.scienceContainer[i].GetData();
                    int duplicateIdx = Array.FindIndex(data, s => s.subjectID == sd.subjectID);
                    if (duplicateIdx >= 0)
                    {
                        return 1.0;
                    }
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns a count of the number of experiments of the specified science type that
        /// are available.  If an invalid science type is selected, returns 0.
        /// </summary>
        /// <param name="scienceTypeId">An integer in the range [0, `fc.ScienceTypeTotal()`).</param>
        /// <returns>The number of valid experiments available for the selected science type.</returns>
        public double ExperimentAvailableCount(double scienceTypeId)
        {
            int id = (int)scienceTypeId;
            int available = 0;
            if (id >= 0 && id < vc.scienceType.Length)
            {
                var exp = vc.scienceType[id].experiments;
                for (int i = exp.Count - 1; i >= 0; --i)
                {
                    if (!exp[i].Deployed)
                    {
                        ++available;
                    }
                }
            }

            return available;
        }

        /// <summary>
        /// Returns the name of the biome where the experiment `experimentId` was conducted.
        ///  
        /// If `experimentId` does not refer to a valid experiment, or if the selected experiment
        /// has not been run, or a biome does not apply (such as when in orbit), an empty string is returned.
        /// </summary>
        /// <param name="experimentId">An integer between 0 and `fc.ExperimentTotal()` - 1, inclusive.</param>
        /// <returns>The name of the biome, or an empty string.</returns>
        public string ExperimentBiome(double experimentId)
        {
            int id = (int)experimentId;
            if (id >= 0 && id < vc.moduleScienceExperiment.Length)
            {
                ModuleScienceExperiment mse = vc.moduleScienceExperiment[id];
                if (mse.Deployed)
                {
                    ScienceData[] data = mse.GetData();
                    if (data.Length > 0)
                    {
                        MASVesselComputer.ExperimentData ed = vc.GetExperimentData(data[0].subjectID, mse.experiment);
                        return ed.biomeDisplayName;
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns a count of the total number of experiments of the specified science type.
        /// If an invalid science type is selected, returns 0.
        /// </summary>
        /// <param name="scienceTypeId">An integer in the range [0, `fc.ScienceTypeTotal()`).</param>
        /// <returns>The total number of experiments for the selected science type.</returns>
        public double ExperimentCount(double scienceTypeId)
        {
            int id = (int)scienceTypeId;
            if (id >= 0 && id < vc.scienceType.Length)
            {
                return vc.scienceType[id].experiments.Count;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the size of the data for experiment `experimentId`.
        /// 
        /// If `experimentId` is an invalid experiment id, or the experiment has not run,
        /// returns 0.
        /// </summary>
        /// <param name="experimentId">An integer between 0 and `fc.ExperimentTotal()` - 1, inclusive.</param>
        /// <returns>The data size of the experiment, in Mits, or 0.</returns>
        public double ExperimentDataSize(double experimentId)
        {
            int id = (int)experimentId;
            if (id >= 0 && id < vc.moduleScienceExperiment.Length)
            {
                ModuleScienceExperiment mse = vc.moduleScienceExperiment[id];
                if (mse.Deployed)
                {
                    ScienceData[] data = mse.GetData();
                    if (data.Length > 0)
                    {
                        return data[0].dataAmount;
                    }
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the `experimentId` value for the selected experiment for the science type ID.  This
        /// value may then be used as the parameter for science fields that require `experimentId`.  If
        /// an invalid `scienceTypeId` or `experimentIndex` is used, this function returns -1.
        /// </summary>
        /// <param name="scienceTypeId">An integer in the range [0, `fc.ScienceTypeTotal()`).</param>
        /// <param name="experimentIndex">An integer in the range [0, `fc.ExperimentCount(scienceTypeId)`).</param>
        /// <returns>The experimentId for the selected experiment, or -1 if an invalid id or index was provided.</returns>
        public double ExperimentId(double scienceTypeId, double experimentIndex)
        {
            int id = (int)scienceTypeId;
            int expIdx = (int)experimentIndex;
            if (id >= 0 && id < vc.scienceType.Length && expIdx >= 0 && expIdx < vc.scienceType[id].experiments.Count)
            {
                int hashCode = vc.scienceType[id].experiments[expIdx].GetHashCode();
                return Array.FindIndex(vc.moduleScienceExperiment, x => x.GetHashCode() == hashCode);
            }

            return -1.0;
        }

        /// <summary>
        /// Returns the results of the selected experiment.
        /// 
        /// Note that this string may be lengthy - using `fc.ScrollingMarquee()` is recommended.
        /// </summary>
        /// <param name="experimentId">An integer between 0 and `fc.ExperimentTotal()` - 1, inclusive.</param>
        /// <returns>The results of the experiment, or an empty string.</returns>
        public string ExperimentResults(double experimentId)
        {
            int id = (int)experimentId;
            if (id >= 0 && id < vc.moduleScienceExperiment.Length)
            {
                ModuleScienceExperiment mse = vc.moduleScienceExperiment[id];
                if (mse.Deployed)
                {
                    ScienceData[] data = mse.GetData();
                    if (data.Length > 0)
                    {
                        MASVesselComputer.ExperimentData ed = vc.GetExperimentData(data[0].subjectID, mse.experiment);
                        return ed.experimentResults;
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the science value of the experiment selected by `experimentId`.
        /// 
        /// If `experimentId` is not a valid experiment, or the experiment has not been run,
        /// returns 0.  It will also return 0 if the stored experiment has no science value,
        /// of course.
        /// </summary>
        /// <param name="experimentId">An integer between 0 and `fc.ExperimentTotal()` - 1, inclusive.</param>
        /// <returns>The science value of the experiment, or 0.</returns>
        public double ExperimentScienceValue(double experimentId)
        {
            int id = (int)experimentId;
            if (id >= 0 && id < vc.moduleScienceExperiment.Length)
            {
                ModuleScienceExperiment mse = vc.moduleScienceExperiment[id];
                if (mse.Deployed)
                {
                    ScienceData[] data = mse.GetData();
                    if (data.Length > 0)
                    {
                        ScienceExperiment experiment = mse.experiment;
                        MASVesselComputer.ExperimentData ed = vc.GetExperimentData(data[0].subjectID, experiment);

                        float scienceValue = ResearchAndDevelopment.GetScienceValue(experiment.baseValue * experiment.dataScale, ed.subject);

                        return scienceValue * HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;
                    }
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the name of the situation where the experiment `experimentId` was conducted.
        ///  
        /// If `experimentId` does not refer to a valid experiment, or if the selected experiment
        /// has not been run, or a situaion does not apply, an empty string is returned.
        /// </summary>
        /// <param name="experimentId">An integer between 0 and `fc.ExperimentTotal()` - 1, inclusive.</param>
        /// <returns>The name of the biome, or an empty string.</returns>
        public string ExperimentSituation(double experimentId)
        {
            int id = (int)experimentId;
            if (id >= 0 && id < vc.moduleScienceExperiment.Length)
            {
                ModuleScienceExperiment mse = vc.moduleScienceExperiment[id];
                if (mse.Deployed)
                {
                    ScienceData[] data = mse.GetData();
                    if (data.Length > 0)
                    {
                        MASVesselComputer.ExperimentData ed = vc.GetExperimentData(data[0].subjectID, mse.experiment);
                        return ed.situation.displayDescription();
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the status of the selected `experimentId`.
        /// 
        /// * +1: Experiment has run, results are available.
        /// *  0: Experiment has not run, or invalid `experimentId`
        /// * -1: Experiment has run, results have been transmitted.
        /// </summary>
        /// <param name="experimentId">An integer between 0 and `fc.ExperimentTotal()` - 1, inclusive.</param>
        /// <returns>-1, 0, or +1.</returns>
        public double ExperimentStatus(double experimentId)
        {
            int id = (int)experimentId;
            if (id >= 0 && id < vc.moduleScienceExperiment.Length)
            {
                ModuleScienceExperiment mse = vc.moduleScienceExperiment[id];
                //Utility.LogMessage(this, "status {2}: dataIsCollectable {0}; Deployed {1}; rerunnable {3}; resettable {4}",
                //    mse.dataIsCollectable,
                //    mse.Deployed,
                //    mse.experiment.experimentTitle,
                //    mse.rerunnable,
                //    mse.resettable
                //    );

                if (mse.Deployed)
                {
                    if (mse.GetData().Length > 0)
                    {
                        return 1.0;
                    }
                    else
                    {
                        return -1.0;
                    }
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the data transmission scalar for the experiment `experimentId`.
        /// </summary>
        /// <param name="experimentId">An integer between 0 and `fc.ExperimentTotal()` - 1, inclusive.</param>
        /// <returns>The data transmission scalar, or 0 if an invalid `experimentid` is supplied.</returns>
        public double ExperimentTransmissionScalar(double experimentId)
        {
            int id = (int)experimentId;
            if (id >= 0 && id < vc.moduleScienceExperiment.Length)
            {
                ModuleScienceExperiment mse = vc.moduleScienceExperiment[id];

                return mse.xmitDataScalar;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the type of experiment the experiment `experimentId` contains.
        /// 
        /// Returns an empty string if `experimentId` is invalid.
        /// </summary>
        /// <param name="experimentId">An integer between 0 and `fc.ExperimentTotal()` - 1, inclusive.</param>
        /// <returns>The name of the experiment type, or an empty string.</returns>
        public string ExperimentType(double experimentId)
        {
            int id = (int)experimentId;
            if (id >= 0 && id < vc.moduleScienceExperiment.Length)
            {
                ModuleScienceExperiment mse = vc.moduleScienceExperiment[id];

                return mse.experiment.experimentTitle;
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the total number of science experiments aboard the vessel.
        /// </summary>
        /// <returns>An integer 0 or larger.</returns>
        public double ExperimentTotal()
        {
            return vc.moduleScienceExperiment.Length;
        }

        /// <summary>
        /// Resets the selected experiment.  If the experiment has not been run, or it requires
        /// a scientist to clean it first, this function has no effect.
        /// </summary>
        /// <param name="experimentId">An integer between 0 and `fc.ExperimentTotal()` - 1, inclusive.</param>
        /// <returns>1 if the experiment was reset, 0 otherwise.</returns>
        public double ResetExperiment(double experimentId)
        {
            int id = (int)experimentId;
            if (id >= 0 && id < vc.moduleScienceExperiment.Length)
            {
                ModuleScienceExperiment mse = vc.moduleScienceExperiment[id];
                if (mse.Deployed && mse.resettable && !mse.Inoperable)
                {
                    mse.ResetExperiment();
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Trigger the stock "Review Science" dialog for the given experiment.
        /// 
        /// If the experiment has not been run, or `experimentId` selects an
        /// invalid experiment, nothing happens.
        /// </summary>
        /// <param name="experimentId">An integer between 0 and `fc.ExperimentTotal()` - 1, inclusive.</param>
        /// <returns>1 if the experiment review dialog was launched, 0 otherwise.</returns>
        public double ReviewExperiment(double experimentId)
        {
            int id = (int)experimentId;
            if (id >= 0 && id < vc.moduleScienceExperiment.Length)
            {
                ModuleScienceExperiment mse = vc.moduleScienceExperiment[id];
                if (mse.Deployed)
                {
                    mse.ReviewDataEvent();
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Trigger the stock "Review Science" dialog for the given container.
        /// </summary>
        /// <param name="scienceContainerId">An integer between [0, `fc.ScienceContainerCount()`).</param>
        /// <returns>1 if the container review dialog was launched, 0 otherwise.</returns>
        public double ReviewScienceContainer(double scienceContainerId)
        {
            int idx = (int)scienceContainerId;
            if (idx >= 0 && idx < vc.scienceContainer.Length)
            {
                vc.scienceContainer[idx].ReviewDataEvent();
                return 1.0;
            }
            return 0.0;
        }

        /// <summary>
        /// Run the first available experiment of the type selected by `scienceTypeId`.  If no experiments
        /// are available, then nothing happens.
        /// </summary>
        /// <param name="scienceTypeId">An integer in the range [0, `fc.ScienceTypeTotal()`).</param>
        /// <returns>1 if an experiment was run, 0 otherwise.</returns>
        public double RunAvailableExperiment(double scienceTypeId)
        {
            int id = (int)scienceTypeId;
            if (id >= 0 && id < vc.scienceType.Length)
            {
                var exp = vc.scienceType[id].experiments;
                for (int i = exp.Count - 1; i >= 0; --i)
                {
                    if (!exp[i].Deployed && vc.CanRunExperiment(exp[i]))
                    {
                        ScienceData sd = vc.GenerateScienceData(exp[i].experiment, exp[i].xmitDataScalar);
                        // XXX: There isn't an AddData method in ModuleScienceExperiment.
                        exp[i].ReturnData(sd);
                        return 1.0;
                    }
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Run the selected experiment.
        /// 
        /// If the experiment has already been run, this function has no effect.
        /// 
        /// Note that the science dialog is displayed after running an experiment.
        /// </summary>
        /// <param name="experimentId">An integer between 0 and `fc.ExperimentTotal()` - 1, inclusive.</param>
        /// <returns>1 if the experiment was run, 0 if it was not.</returns>
        public double RunExperiment(double experimentId)
        {
            int id = (int)experimentId;
            if (id >= 0 && id < vc.moduleScienceExperiment.Length)
            {
                ModuleScienceExperiment mse = vc.moduleScienceExperiment[id];

                if (!mse.Deployed && vc.CanRunExperiment(mse))
                {
                    ScienceData sd = vc.GenerateScienceData(mse.experiment, mse.xmitDataScalar);
                    // XXX: There isn't an AddData method in ModuleScienceExperiment.
                    mse.ReturnData(sd);
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the number of experiments that may be stored in the container, or 0 if an invalid ID is provided.
        /// 
        /// A capacity of 0 indicates unlimited storage capacity.
        /// </summary>
        /// <param name="scienceContainerId">An integer between [0, `fc.ScienceContainerCount()`).</param>
        /// <returns>The storage capacity, or 0.</returns>
        public double ScienceContainerCapacity(double scienceContainerId)
        {
            int idx = (int)scienceContainerId;
            if (idx >= 0 && idx < vc.scienceContainer.Length)
            {
                return vc.scienceContainer[idx].capacity;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the number of science containers (ModuleScienceContainer) on the vessel.
        /// </summary>
        /// <returns>The number of science containers.</returns>
        public double ScienceContainerCount()
        {
            return vc.scienceContainer.Length;
        }

        /// <summary>
        /// Returns the number of experiments stored in the container, or 0 if an invalid ID is provided.
        /// </summary>
        /// <param name="scienceContainerId">An integer between [0, `fc.ScienceContainerCount()`).</param>
        /// <returns>The number of stored experiments, or 0.</returns>
        public double ScienceContainerDataCount(double scienceContainerId)
        {
            int idx = (int)scienceContainerId;
            if (idx >= 0 && idx < vc.scienceContainer.Length)
            {
                return vc.scienceContainer[idx].GetScienceCount();
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the size of all the data stored in the science container.
        /// 
        /// If `scienceContainerId` is an invalid science container id, or there is
        /// no data in the container, it returns 0.
        /// </summary>
        /// <param name="scienceContainerId">An integer between [0, `fc.ScienceContainerCount()`).</param>
        /// <returns>The data size of all experiments stored in the container, in Mits, or 0.</returns>
        public double ScienceContainerDataSize(double scienceContainerId)
        {
            int idx = (int)scienceContainerId;
            if (idx >= 0 && idx < vc.scienceContainer.Length)
            {
                ScienceData[] sd = vc.scienceContainer[idx].GetData();
                float data = 0.0f;
                for (int i = sd.Length - 1; i >= 0; --i)
                {
                    data += sd[i].dataAmount;
                }

                return data;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the name of the part that the container is installed on, or an empty string if an invalid ID is provided.
        /// </summary>
        /// <param name="scienceContainerId">An integer between [0, `fc.ScienceContainerCount()`).</param>
        /// <returns>The part name, or an empty string.</returns>
        public string ScienceContainerName(double scienceContainerId)
        {
            int idx = (int)scienceContainerId;
            if (idx >= 0 && idx < vc.scienceContainer.Length)
            {
                return vc.scienceContainer[idx].part.partInfo.title;
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the type of experiment recorded as category `scienceTypeId`.
        /// 
        /// Returns an empty string if `scienceTypeId` does not refer to a valid category.
        /// </summary>
        /// <param name="scienceTypeId">An integer between 0 and `fc.ScienceTypeTotal()` - 1.</param>
        /// <returns>The name of the experiment type, or an empty string.</returns>
        public string ScienceType(double scienceTypeId)
        {
            int id = (int)scienceTypeId;
            if (id >= 0 && id < vc.scienceType.Length)
            {
                return vc.scienceType[id].type.experimentTitle;
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns a number usable as the `scienceTypeId` parameter in science functions
        /// based on the `scienceTypeName` parameter.  If an invalid name is supplied, or
        /// there are no experiments of the named type, returns -1.
        /// 
        /// Note that `scienceTypeName` is the "id" field for the corresponding EXPERIMENT_DEFINITION
        /// from ScienceDefs.cfg (or another science definition config file).
        /// </summary>
        /// <param name="scienceTypeName">The id of the science.</param>
        /// <returns>An integer in the range [0, `fc.ScienceTypeTotal()`), or -1.</returns>
        public double ScienceTypeId(string scienceTypeName)
        {
            return Array.FindIndex(vc.scienceType, x => x.type.id == scienceTypeName);
        }

        /// <summary>
        /// Returns the total number of categories of science experiments aboard the vessel.
        /// </summary>
        /// <returns>An integer 0 or larger.</returns>
        public double ScienceTypeTotal()
        {
            return vc.scienceType.Length;
        }

        /// <summary>
        /// Transmit all of the contents of the selected science container using the transmitter
        /// identified by `transitterId`.
        /// 
        /// Does nothing if there is no data in the container, or if an invalid or busy
        /// transmitter is selected.
        /// </summary>
        /// <param name="transmitterId">An integer in the range [0, `fc.DataTransmitterCount()`).</param>
        /// <param name="scienceContainerId">An integer between [0, `fc.ScienceContainerCount()`).</param>
        /// <returns>1 if the container's data was sent, 0 if it could not be sent or an invalid ID was provided.</returns>
        public double TransmitScienceContainer(double transmitterId, double scienceContainerId)
        {
            int xmitId = (int)transmitterId;
            int idx = (int)scienceContainerId;
            if (xmitId >= 0 && xmitId < vc.moduleTransmitter.Length && idx >= 0 && idx < vc.scienceContainer.Length)
            {
                ModuleDataTransmitter mdt = vc.moduleTransmitter[xmitId];
                ModuleScienceContainer msc = vc.scienceContainer[idx];
                if (mdt.IsBusy() == false && msc.GetStoredDataCount() > 0)
                {
                    List<ScienceData> sd = new List<ScienceData>();
                    sd.AddRange(msc.GetData());
                    int numSciences = sd.Count;
                    if (numSciences > 0)
                    {
                        mdt.TransmitData(sd);
                        for (int i = 0; i < numSciences; ++i)
                        {
                            msc.DumpData(sd[i]);
                        }

                        return 1.0;
                    }
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Transmit the experiment selected by `experimentId` using the transmitter
        /// `transmitterId`.
        /// 
        /// Does nothing if there is no data in the experiment, or if an invalid or busy
        /// transmitter is selected.
        /// </summary>
        /// <param name="transmitterId">An integer in the range [0, `fc.DataTransmitterCount()`).</param>
        /// <param name="experimentId">An integer between 0 and `fc.ExperimentTotal()` - 1, inclusive.</param>
        /// <returns>1 if the experiment was sent, 0 if it could not be sent or an invalid ID was provided.</returns>
        public double TransmitExperiment(double transmitterId, double experimentId)
        {
            int xmitId = (int)transmitterId;
            int expId = (int)experimentId;
            if (xmitId >= 0 && xmitId < vc.moduleTransmitter.Length && expId >= 0 && expId < vc.moduleScienceExperiment.Length)
            {
                ModuleDataTransmitter mdt = vc.moduleTransmitter[xmitId];
                ModuleScienceExperiment mse = vc.moduleScienceExperiment[expId];
                if (mdt.IsBusy() == false && mse.Deployed)
                {
                    List<ScienceData> sd = new List<ScienceData>();
                    sd.AddRange(mse.GetData());
                    int numSciences = sd.Count;
                    if (numSciences > 0)
                    {
                        mdt.TransmitData(sd);
                        for (int i = 0; i < numSciences; ++i)
                        {
                            mse.DumpData(sd[i]);
                        }

                        return 1.0;
                    }
                }
            }
            return 0.0;
        }
        #endregion

        /// <summary>
        /// Variables related to the vessels speed, velocity, and accelerations are grouped
        /// in this category.
        /// </summary>
        #region Speed, Velocity, and Acceleration

        /// <summary>
        /// Returns the current acceleration of the vessel from engines, in m/s^2.
        /// </summary>
        /// <returns>Engine acceleration in m/s^2.</returns>
        public double AccelEngines()
        {
            return vc.currentThrust / vessel.totalMass;
        }

        /// <summary>
        /// Returns the net acceleration on the vessel from all forces in m/s^2.
        /// </summary>
        /// <returns>Net acceleration in m/s^2.</returns>
        public double Acceleration()
        {
            return vessel.acceleration.magnitude;
        }

        /// <summary>
        /// Returns the forward (towards the vessel nose) component of the net
        /// acceleration on the vessel in m/s^2.  Negative values represent a
        /// rearward acceleration.
        /// </summary>
        /// <returns>Forward acceleration in m/s^2.</returns>
        public double AccelForward()
        {
            return Vector3d.Dot(vessel.acceleration, vc.forward);
        }

        /// <summary>
        /// Returns the rightward component of the net
        /// acceleration on the vessel in m/s^2.  Negative values represent a
        /// leftward acceleration.
        /// </summary>
        /// <returns>Right acceleration in m/s^2.</returns>
        public double AccelRight()
        {
            return Vector3d.Dot(vessel.acceleration, vc.right);
        }

        /// <summary>
        /// Returns the surface forward-relative component of the net
        /// acceleration on the vessel in m/s^2.
        /// </summary>
        /// <returns>Surface forward acceleration in m/s^2.</returns>
        public double AccelSurfaceForward()
        {
            return Vector3d.Dot(vessel.acceleration, vc.surfaceForward);
        }

        /// <summary>
        /// Returns the surface prograde-relative component of the net
        /// acceleration on the vessel in m/s^2.
        /// </summary>
        /// <returns>Surface prograde acceleration in m/s^2.</returns>
        public double AccelSurfacePrograde()
        {
            return Vector3d.Dot(vessel.acceleration, vc.surfacePrograde);
        }

        /// <summary>
        /// Returns the surface right-relative component of the net
        /// acceleration on the vessel in m/s^2.
        /// </summary>
        /// <returns>Surface right acceleration in m/s^2.</returns>
        public double AccelSurfaceRight()
        {
            return Vector3d.Dot(vessel.acceleration, vc.surfaceRight);
        }

        /// <summary>
        /// Returns the top (towards the 'top' of an aircraft, or the typical 'up' direction of kerbals in pods) component of the net
        /// acceleration on the vessel in m/s^2.  Negative values represent a
        /// vessel-downward acceleration.
        /// </summary>
        /// <returns>Top acceleration in m/s^2.</returns>
        public double AccelTop()
        {
            return Vector3d.Dot(vessel.acceleration, vc.top);
        }

        /// <summary>
        /// Returns the surface-relative up component of the net
        /// acceleration on the vessel in m/s^2.  Negative values represent
        /// acceleration towards the surface.
        /// </summary>
        /// <returns>Top acceleration in m/s^2.</returns>
        public double AccelUp()
        {
            return Vector3d.Dot(vessel.acceleration, vc.up);
        }

        /// <summary>
        /// Returns the rate at which the vessel's distance to the ground
        /// is changing.  This is the vertical speed as measured from vessel
        /// to surface, as opposed to measuring from a fixed altitude.  When
        /// over an ocean, sea level is used as the ground height (in other
        /// words, `fc.AltitudeTerrain(false)`).
        /// 
        /// Because terrain may be rough, this value may be noisy.  It is
        /// smoothed using exponential smoothing, so the rate is not
        /// instantaneously precise.
        /// </summary>
        /// <returns>Rate of change of terrain altitude in m/s.</returns>
        public double AltitudeTerrainRate()
        {
            return vc.altitudeTerrainRate;
        }

        /// <summary>
        /// Returns the approach speed (the rate of closure directly towards
        /// the target).  Returns 0 if there's no target or all relative
        /// movement is perpendicular to the approach direction.
        /// </summary>
        /// <returns>Approach speed in m/s.  Returns 0 if there is no target.</returns>
        public double ApproachSpeed()
        {
            if (vc.activeTarget != null)
            {
                return Vector3d.Dot(vc.targetRelativeVelocity, vc.targetDirection);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the speed selected by the speed mode (surface, orbit, or target)
        /// in m/s.
        /// This value is equivalent to the speed displayed over the NavBall in the UI.
        /// </summary>
        /// <returns>Current speed in m/s.</returns>
        public double CurrentSpeedModeSpeed()
        {
            switch (FlightGlobals.speedDisplayMode)
            {
                case FlightGlobals.SpeedDisplayModes.Orbit:
                    return vessel.obt_speed;
                case FlightGlobals.SpeedDisplayModes.Surface:
                    return vessel.srfSpeed;
                case FlightGlobals.SpeedDisplayModes.Target:
                    return vc.targetSpeed;
                default:
                    return 0.0;
            }
        }

        /// <summary>
        /// Compute equivalent airspeed based on current surface speed and atmospheric density.
        /// 
        /// https://en.wikipedia.org/wiki/Equivalent_airspeed
        /// </summary>
        /// <returns>EAS in m/s.</returns>
        public double EquivalentAirspeed()
        {
            double densityRatio = vessel.atmDensity / 1.225;
            return vessel.srfSpeed * Math.Sqrt(densityRatio);
        }

        /// <summary>
        /// Returns the magnitude of g-forces currently affecting the craft, in gees.
        /// </summary>
        /// <returns>Current instantaneous force in Gs.</returns>
        public double GForce()
        {
            return vessel.geeForce_immediate;
        }

        /// <summary>
        /// Returns the magnitude of g-forces perpendicular to the front of the craft
        /// aligned with the normal seating configuration of the crew.  For aircraft and
        /// most spacecraft, positive values indicates forces towards the feet of the crew,
        /// while negative values indicate forces towards the heads of the crew.
        /// 
        /// Excessive positive values could cause blackouts, while excessive negative values
        /// may cause redouts.
        /// </summary>
        /// <returns>The number of Gs towards the head or feet of the crew.</returns>
        public double GForceVertical()
        {
            // acceleration is in m/s.
            return -Vector3d.Dot(vessel.acceleration - vessel.graviticAcceleration, vc.top) / PhysicsGlobals.GravitationalAcceleration;
        }

        /// <summary>
        /// Measure of the surface speed of the vessel after removing the
        /// vertical component, in m/s.
        /// </summary>
        /// <returns>Horizontal surface speed in m/s.</returns>
        public double HorizontalSpeed()
        {
            double speedHorizontal;
            if (Math.Abs(vessel.verticalSpeed) < Math.Abs(vessel.srfSpeed))
            {
                speedHorizontal = Math.Sqrt(vessel.srfSpeed * vessel.srfSpeed - vessel.verticalSpeed * vessel.verticalSpeed);
            }
            else
            {
                speedHorizontal = 0.0;
            }

            return speedHorizontal;
        }

        /// <summary>
        /// Returns the indicated airspeed in m/s, based on current surface speed, atmospheric density, and Mach number.
        /// </summary>
        /// <returns>IAS in m/s.</returns>
        public double IndicatedAirspeed()
        {
            // We compute this because this formula is basically what FAR uses; Vessel.indicatedAirSpeed
            // gives drastically different results while in motion.
            double densityRatio = vessel.atmDensity / 1.225;
            double pressureRatio = Utility.StagnationPressure(vc.mainBody.atmosphereAdiabaticIndex, vessel.mach);
            return vessel.srfSpeed * Math.Sqrt(densityRatio) * pressureRatio;
        }

        /// <summary>
        /// Returns the vessel's current Mach number (multiple of the speed of sound).
        /// This number only makes sense in an atmosphere.
        /// </summary>
        /// <returns>Vessel speed as a factor of the speed of sound.</returns>
        public double MachNumber()
        {
            return vessel.mach;
        }

        /// <summary>
        /// Return the orbital speed of the vessel in m/s
        /// </summary>
        /// <returns>Orbital speed in m/s.</returns>
        public double OrbitSpeed()
        {
            return vessel.obt_speed;
        }

        /// <summary>
        /// Returns +1 if the KSP automatic speed display is set to "Orbit",
        /// +0 if it's "Surface", and -1 if it's "Target".  This mode affects
        /// SAS behaviors, so it's useful to know.
        /// </summary>
        /// <returns>1 for "Orbit" mode, 0 for "Surface" mode, and -1 for "Target" mode.</returns>
        public double SpeedDisplayMode()
        {
            var displayMode = FlightGlobals.speedDisplayMode;
            if (displayMode == FlightGlobals.SpeedDisplayModes.Orbit)
            {
                return 1.0;
            }
            else if (displayMode == FlightGlobals.SpeedDisplayModes.Surface)
            {
                return 0.0;
            }
            else
            {
                return -1.0;
            }
        }

        /// <summary>
        /// Returns the component of surface velocity relative to the nose of
        /// the craft, in m/s.  If the vessel is near vertical, the 'forward'
        /// vector is treated as the vector that faces 'down' in a horizontal
        /// cockpit configuration.
        /// </summary>
        /// <returns>The vessel's velocity fore/aft velocity in m/s.</returns>
        public double SurfaceForwardSpeed()
        {
            // TODO: the following dot returns a negative number, but lateral speed is right.
            // What did I get turned around?
            return -Vector3.Dot(vc.surfacePrograde, vc.surfaceForward) * vessel.srfSpeed;
        }

        /// <summary>
        /// Returns the lateral (right/left) component of surface velocity in
        /// m/s.  This value could become zero at extreme roll orientations.
        /// Positive values are to the right, negative to the left.
        /// </summary>
        /// <returns>The vessel's left/right velocity in m/s.  Right is positive; left is negative.</returns>
        public double SurfaceLateralSpeed()
        {
            return Vector3.Dot(vc.surfacePrograde, vc.surfaceRight) * vessel.srfSpeed;
        }

        /// <summary>
        /// Return the surface-relative speed of the vessel in m/s.
        /// </summary>
        /// <returns>Surface speed in m/s.</returns>
        public double SurfaceSpeed()
        {
            return vessel.srfSpeed;
        }

        /// <summary>
        /// Target-relative speed in m/s.  0 if no target.
        /// </summary>
        /// <returns>Speed relative to the target in m/s.  0 if there is no target.</returns>
        public double TargetSpeed()
        {
            return vc.targetSpeed;
        }

        /// <summary>
        /// Returns the vertical speed of the vessel in m/s.
        /// </summary>
        /// <returns>Surface-relative vertical speed in m/s.</returns>
        public double VerticalSpeed()
        {
            return vessel.verticalSpeed;
        }
        #endregion

        /// <summary>
        /// Controls for staging a vessel, and controlling the stage lock, and information
        /// related to both staging and stage locks are all in the Staging category.  Launch
        /// clamp info is also grouped under Staging.
        /// </summary>
        #region Staging
        /// <summary>
        /// Returns the current stage.  Before launch or after undocking, this number may
        /// be larger than the total number of stages on the vessel.
        /// </summary>
        /// <returns>A whole number 0 or larger.</returns>
        public double CurrentStage()
        {
            return StageManager.CurrentStage;
        }

        /// <summary>
        /// Returns the number of launch clamps currently attached to the vessel.
        /// </summary>
        /// <returns>A whole number 0 or larger.</returns>
        public double GetLaunchClampCount()
        {
            return vc.moduleLaunchClamp.Length;
        }

        /// <summary>
        /// Returns 1 if staging is locked, 0 otherwise.
        /// </summary>
        /// <returns>1 if staging is locked, 0 if staging is unlocked.</returns>
        public double GetStageLocked()
        {
            return (InputLockManager.IsLocked(ControlTypes.STAGING)) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Release all launch clamps holding the vessel.
        /// </summary>
        /// <returns>1 if any launch clamps released, 0 otherwise.</returns>
        public double ReleaseLaunchClamps()
        {
            for (int i = vc.moduleLaunchClamp.Length - 1; i >= 0; --i)
            {
                vc.moduleLaunchClamp[i].Release();
            }

            return (vc.moduleLaunchClamp.Length > 0) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Sets stage locking to the specified setting (true or false).
        /// </summary>
        /// <param name="locked">`true` to lock staging, `false` to unlock staging.</param>
        /// <returns>1 if staging is locked, 0 otherwise.</returns>
        public double SetStageLocked(bool locked)
        {
            if (locked)
            {
                InputLockManager.SetControlLock(ControlTypes.STAGING, "manualStageLock");
                return 1.0;
            }
            else
            {
                InputLockManager.RemoveControlLock("manualStageLock");
                return 0.0;
            }
        }

        /// <summary>
        /// Activate the next stage.
        /// </summary>
        /// <returns>1 if the vessel staged; 0 otherwise.</returns>
        public double Stage()
        {
            if (StageManager.CanSeparate && InputLockManager.IsUnlocked(ControlTypes.STAGING))
            {
                StageManager.ActivateNextStage();
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Can the vessel stage?
        /// </summary>
        /// <returns>1 if the vessel can stage, and staging is unlocked; 0 otherwise.</returns>
        public double StageReady()
        {
            return (StageManager.CanSeparate && InputLockManager.IsUnlocked(ControlTypes.STAGING)) ? 1.0 : 0.0;
        }

        /// <summary>
        /// The staging manager sets the 'current stage' to 1 greater than the number of stages
        /// on the vessel when a new vessel spawns (either at the launch pad, or after undocking).
        /// Doing so allows the vessel to sit on the launch pad without the last stage firing.
        /// 
        /// However, since the same thing happens after undocking, it is possible that the vessel's
        /// staging system is off-by-one, meaning that an engine on the last stage won't be available
        /// without staging, first.
        /// 
        /// This function returns 1 if the staging manager Current Stage is the same as the vessel's
        /// Last Stage, meaning that `fc.CurrentStage()` shows a valid stage on the vessel.  It returns
        /// 0 prior to launch, or immediately after undocking, when the current stage is larger than the
        /// number of stages on the vessel.
        /// </summary>
        /// <returns>1 if CurrentStage refers to a stage on the vessel, 0 if it does not.</returns>
        public double StageValid()
        {
            return (StageManager.CurrentStage > StageManager.LastStage) ? 0.0 : 1.0;
        }

        /// <summary>
        /// Toggle the stage lock on or off.  Returns the new state.
        /// </summary>
        /// <returns>1 if staging is now locked; 0 if staging is now unlocked.</returns>
        public double ToggleStageLocked()
        {
            bool state = !InputLockManager.IsLocked(ControlTypes.STAGING);
            SetStageLocked(state);
            return (state) ? 1.0 : 0.0;
        }
        #endregion

        /// <summary>
        /// Information related to the survivability of the current pod are grouped in the Survival
        /// category.
        /// </summary>
        #region Survival

        /// <summary>
        /// Reports the maximum impact speed of the current part, in meters per second.
        /// </summary>
        /// <returns>Max impact speed of the command pod, in m/s.</returns>
        public double MaxImpactSpeed()
        {
            return (fc != null && fc.part != null) ? fc.part.crashTolerance : 0.0;
        }

        /// <summary>
        /// Returns the time (in seconds) until a suicide burn (maximum thrust burn) must
        /// start to avoid lithobraking.  If the orbit does not impact the surface, or it
        /// is too late to avoid impact, returns 0.
        /// 
        /// If Kerbal Engineer Redux is installed, MAS will use the KER prediction of time-of-impact.
        /// If KER is not installed, MAS uses its own estimate of time-to-impact.
        /// </summary>
        /// <seealso>MechJeb, Kerbal Engineer Redux</seealso>
        /// <returns>Time in seconds until the burn must start, or 0.</returns>
        public double SuicideBurnTime()
        {
            double timeToImpact = 0.0;
            if (MASIKerbalEngineer.keFound)
            {
                timeToImpact = keProxy.LandingTime();
            }
            else if (mjProxy.LandingComputerActive() > 0.0)
            {
                timeToImpact = mjProxy.LandingTime();
            }
            else
            {
                timeToImpact = vc.timeToImpact;
            }

            if (timeToImpact > 0.0)
            {
                double sine = Mathf.Max(0.0f, Vector3.Dot(-(vessel.srf_velocity.normalized), vc.up));
                double g = FlightGlobals.getGeeForceAtPosition(vessel.CoM).magnitude;
                double T = vc.currentLimitedMaxThrust / vessel.totalMass;

                double effectiveDecel = 0.5 * (-2 * g * sine + Math.Sqrt((2 * g * sine) * (2 * g * sine) + 4 * (T * T - g * g)));
                if (!double.IsNaN(effectiveDecel))
                {
                    double decelTime = vessel.srfSpeed / effectiveDecel;

                    return Math.Max(0.0, timeToImpact - decelTime * 0.5);
                }
            }

            return 0.0;
        }

        #endregion

        /// <summary>
        /// The Target and Rendezvous section provides functions and methods related to
        /// targets and rendezvous operations with a target.  These methods include raw
        /// distance and velocities as well as target name and classifiers (is it a vessel,
        /// a celestial body, etc).
        /// </summary>
        #region Target and Rendezvous
        /// <summary>
        /// Clears any targets being tracked.
        /// </summary>
        /// <returns>1 if the target was cleared, 0 otherwise.</returns>
        public double ClearTarget()
        {
            if (vc.targetValid)
            {
                FlightGlobals.fetch.SetVesselTarget((ITargetable)null);
                return 1.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Removes the specified vesselType from the target tracking filter.  vesselType
        /// must be one of:
        /// 
        /// * 1 - Ship
        /// * 2 - Plane
        /// * 3 - Probe
        /// * 4 - Lander
        /// * 5 - Station
        /// * 6 - Relay
        /// * 7 - Rover
        /// * 8 - Base
        /// * 9 - EVA
        /// * 10 - Flag
        /// * 11 - Debris
        /// * 12 - Space Object
        /// * 13 - Unknown
        /// 
        /// Returns 1 if the provided vesselType was previously set, or 0 if it was not set
        /// or an invalid vesselType was supplied.
        /// </summary>
        /// <param name="vesselType">An integer value between 1 and 13, inclusive.</param>
        /// <returns>1 if the vesselType was cleared, 0 if it was not.</returns>
        public double ClearTargetFilter(double vesselType)
        {
            int vBitIndex = (int)(vesselType);
            if (vBitIndex > 0 && vBitIndex <= 13)
            {
                return (fc.ClearTargetFilter(vBitIndex)) ? 1.0 : 0.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Indicates whether the specified vesselType is set in the target selection filter.  vesselType
        /// must be one of:
        /// 
        /// * 1 - Ship
        /// * 2 - Plane
        /// * 3 - Probe
        /// * 4 - Lander
        /// * 5 - Station
        /// * 6 - Relay
        /// * 7 - Rover
        /// * 8 - Base
        /// * 9 - EVA
        /// * 10 - Flag
        /// * 11 - Debris
        /// * 12 - Space Object
        /// * 13 - Unknown
        /// 
        /// Returns 1 if the provided vesselType is a target that will be selected, or 0 if it will not be selected
        /// or an invalid vesselType was supplied.
        /// </summary>
        /// <param name="vesselType">An integer value between 1 and 13, inclusive.</param>
        /// <returns>1 if the vesselType was cleared, 0 if it was not.</returns>
        public double GetTargetFilter(double vesselType)
        {
            int vBitIndex = (int)(vesselType);
            if (vBitIndex > 0 && vBitIndex <= 13)
            {
                return (fc.GetTargetFilter(vBitIndex)) ? 1.0 : 0.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Adds the specified vesselType to the target tracking filter.  vesselType
        /// must be one of:
        /// 
        /// * 1 - Ship
        /// * 2 - Plane
        /// * 3 - Probe
        /// * 4 - Lander
        /// * 5 - Station
        /// * 6 - Relay
        /// * 7 - Rover
        /// * 8 - Base
        /// * 9 - EVA
        /// * 10 - Flag
        /// * 11 - Debris
        /// * 12 - Space Object
        /// * 13 - Unknown
        /// 
        /// Returns 1 if the provided vesselType was not previously set, or 0 if it was already set
        /// or an invalid vesselType was supplied.
        /// </summary>
        /// <param name="vesselType">An integer value between 1 and 13, inclusive.</param>
        /// <returns>1 if the vesselType was set, 0 if it was not.</returns>
        public double SetTargetFilter(double vesselType)
        {
            int vBitIndex = (int)(vesselType);
            if (vBitIndex > 0 && vBitIndex <= 13)
            {
                return (fc.SetTargetFilter(vBitIndex)) ? 1.0 : 0.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Set the active target to the vessel selected by id.  If the id
        /// is invalid, the current target is cleared.  The id parameter
        /// must be greater than or equal to 0, and less than fc.TargetVesselCount()
        /// to be valid.
        /// </summary>
        /// <param name="id">The id of the vessel to target.</param>
        /// <returns>1 if the target was successfully set, 0 otherwise.</returns>
        public double SetTargetVessel(double id)
        {
            UpdateNeighboringVessels();

            int index = (int)id;
            if (index >= 0 && index < neighboringVessels.Length)
            {
                FlightGlobals.fetch.SetVesselTarget(neighboringVessels[index]);
                return 1.0;
            }
            else
            {
                ClearTarget();
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the altitude of the target, or 0 if there is no target.
        /// </summary>
        /// <returns>Target altitude in meters.</returns>
        public double TargetAltitude()
        {
            if (vc.activeTarget != null)
            {
                return vc.targetOrbit.altitude;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the raw angle between the target and the nose of the vessel,
        /// or 0 if there is no target.
        /// </summary>
        /// <returns>Returns 0 if the target is directly in front of the vessel, or
        /// if there is no target; returns a number up to 180 in all other cases.  Value is in degrees.</returns>
        public double TargetAngle()
        {
            if (vc.targetType > 0)
            {
                return Vector3.Angle(vc.forward, vc.targetDirection);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the geometric angle of the target's position when projected onto the vessel's
        /// X/Y plane.
        /// 
        /// Combined with `fc.TargetAxialDistance()`, this angle can be used to determine where the
        /// target is relative to the vessel's nose.  The angle and distance can also be used to
        /// compute `fc.TargetDistanceX()` and `fc.TargetDistanceY()`.
        /// 
        /// An angle of 0 indicates the target lies directly along the +X axis.  An angle of
        /// 90 indicates it lies on the +Y axis.
        /// </summary>
        /// <returns>Axial angle in degrees, or 0 if there is no target.</returns>
        public double TargetAxialAngle()
        {
            if (vc.targetType > 0)
            {
                double displacementX = Vector3.Dot(vc.targetDisplacement, vc.referenceTransform.right);
                double displacementY = -Vector3.Dot(vc.targetDisplacement, vc.referenceTransform.forward);

                return Math.Atan2(displacementY, displacementX);
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the axial displacement of the target relative to the nose of the vessel.
        /// 
        /// The axial displacement
        /// indicates how far away the target is from a line extending directly from the front of the vessel
        /// (or from the current reference part / docking port).
        /// </summary>
        /// <returns>Axial displacement in meters, or 0 if there is no target.</returns>
        public double TargetAxialDistance()
        {
            if (vc.targetType > 0)
            {
                double displacementX = Vector3.Dot(vc.targetDisplacement, vc.referenceTransform.right);
                double displacementY = Vector3.Dot(vc.targetDisplacement, vc.referenceTransform.forward);

                return Math.Sqrt(displacementX * displacementX + displacementY * displacementY);
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the relative axial velocity of the target relative to the nose of the vessel.
        /// </summary>
        /// <returns>Axial velocity in m/s, or 0 if there is no target.</returns>
        public double TargetAxialVelocity()
        {
            if (vc.targetType > 0)
            {
                double velocityX = Vector3.Dot(vc.targetRelativeVelocity, vc.referenceTransform.right);
                double velocityY = -Vector3.Dot(vc.targetRelativeVelocity, vc.referenceTransform.forward);

                return Math.Sqrt(velocityX * velocityX + velocityY * velocityY);
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the target's apoapsis.
        /// </summary>
        /// <returns>Target's Ap in meters, or 0 if there is no target.</returns>
        public double TargetApoapsis()
        {
            if (vc.targetType > 0)
            {
                return vc.targetOrbit.ApA;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the name of the body that the target orbits, or an empty string if
        /// there is no target.
        /// </summary>
        /// <returns></returns>
        public string TargetBodyName()
        {
            if (vc.targetType > 0)
            {
                return vc.targetOrbit.referenceBody.bodyName;
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Returns the distance of the closest approach to the target during the
        /// next orbit.  If the target is a celestial body, the closest approach
        /// distance reports the predicted periapsis, with a value of 0 indicating
        /// lithobraking (impact).
        /// </summary>
        /// <returns>Closest approach distance in meters, or 0 if there is no target.</returns>
        public double TargetClosestApproachDistance()
        {
            return vc.targetClosestDistance;
        }

        /// <summary>
        /// Returns the relative speed of the target at closest approach.  If there is no
        /// target, returns 0.
        /// </summary>
        /// <returns>Speed at closest approach in m/s, or 0 if there is no target.</returns>
        public double TargetClosestApproachSpeed()
        {
            if (vc.targetType > 0)
            {
                return vc.targetClosestSpeed;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the time until the closest approach to the target.
        /// </summary>
        /// <returns>Time to closest approach in seconds, or 0 if there is no target.</returns>
        public double TargetClosestApproachTime()
        {
            if (vc.targetType > 0)
            {
                return vc.targetClosestUT - vc.universalTime;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the distance to the current target in meters, or 0 if there is no target.
        /// </summary>
        /// <returns>Target distance in meters, or 0 if there is no target.</returns>
        public double TargetDistance()
        {
            return vc.targetDisplacement.magnitude;
        }

        /// <summary>
        /// Returns the displacement between the target vessel and the reference
        /// transform on the horizontal (reference-transform relative) plane in
        /// meters, with target to the right = +X and left = -X.
        /// </summary>
        /// <returns>Distance in meters.  Positive means the target is to the right,
        /// negative means to the left.</returns>
        public double TargetDistanceX()
        {
            return Vector3.Dot(vc.targetDisplacement, vc.referenceTransform.right);
        }

        /// <summary>
        /// Returns the displacement between the target vessel and the reference
        /// transform on the vertical (rt-relative) plane in meters, with target
        /// up = +Y and down = -Y.
        /// </summary>
        /// <returns>Distance in meters.  Positive means the target is above the
        /// craft, negative means below.</returns>
        public double TargetDistanceY()
        {
            // The sign is reversed because it appears that the forward vector actually
            // points down, not up, which also means not having to flip the sign for the
            // Z axis.
            return -Vector3.Dot(vc.targetDisplacement, vc.referenceTransform.forward);
        }

        /// <summary>
        /// Returns the displacement between the target vessel and the reference
        /// transform on the Z (fore/aft) axis in meters, with target ahead = +Z
        /// and behind = -Z
        /// </summary>
        /// <returns>Distance in meters.  Positive indicates a target in front
        /// of the craft, negative indicates behind.</returns>
        public double TargetDistanceZ()
        {
            return Vector3.Dot(vc.targetDisplacement, vc.referenceTransform.up);
        }

        /// <summary>
        /// Returns the eccentricity of the target's orbit, or 0 if there is no
        /// target.
        /// </summary>
        /// <returns>Returns the target orbit's eccentricity.</returns>
        public double TargetEccentricity()
        {
            if (vc.targetType > 0)
            {
                return vc.targetOrbit.eccentricity;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the heading of the target's prograde surface speed, or 0 if there is no target, or the target does not have a meaningful surface heading.
        /// 
        /// **NOTE:** At present, only vessel targets return valid headings.  All others return 0.
        /// </summary>
        /// <returns>Heading, or 0.</returns>
        public double TargetHeadingPrograde()
        {
            // This VV gives me orbital velocity that matches with Vessel.obt_velocity.
            //    Vector3d getVel = vc.Vessel.orbit.GetVel();
            // Now I need to figure out how to get surface velocity from that information.

            if (vc.targetType > 0 && vc.targetOrbit.referenceBody == vc.orbit.referenceBody)
            {
                if (vc.targetType == MASVesselComputer.TargetType.Vessel || vc.targetType == MASVesselComputer.TargetType.DockingPort)
                {
                    Vessel targetVessel;
                    if (vc.targetType == MASVesselComputer.TargetType.Vessel)
                    {
                        targetVessel = vc.activeTarget as Vessel;
                    }
                    else // Docking port
                    {
                        targetVessel = (vc.activeTarget as ModuleDockingNode).vessel;
                    }

                    Vector3 surfaceProgradeProjected = Vector3.ProjectOnPlane(targetVessel.srf_vel_direction, targetVessel.up);
                    double progradeHeading = Vector3.Angle(surfaceProgradeProjected, targetVessel.north);
                    if (Vector3.Dot(surfaceProgradeProjected, targetVessel.east) < 0.0)
                    {
                        progradeHeading = 360.0 - progradeHeading;
                    }

                    return progradeHeading;
                }
                else
                {
                    return 0.0;
                }
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the orbital inclination of the target, or 0 if there is no target.
        /// </summary>
        /// <returns>Target orbital inclination in degrees, or 0 if there is no target.</returns>
        public double TargetInclination()
        {
            if (vc.targetType > 0)
            {
                return vc.targetOrbit.inclination;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if the target is a vessel (vessel or Docking Port); 0 otherwise.
        /// </summary>
        /// <returns>1 for vessel or docking port targets, 0 otherwise.</returns>
        public double TargetIsVessel()
        {
            return (vc.targetType == MASVesselComputer.TargetType.Vessel || vc.targetType == MASVesselComputer.TargetType.DockingPort) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns whether the latitude / longitude of the target are
        /// currently valid.
        /// </summary>
        /// <returns>1 for vessel, docking port, or waypoint targets, 0 otherwise.</returns>
        public double TargetLatLonValid()
        {
            return (vc.targetType == MASVesselComputer.TargetType.CelestialBody || vc.targetType == MASVesselComputer.TargetType.Vessel || vc.targetType == MASVesselComputer.TargetType.DockingPort || vc.targetType == MASVesselComputer.TargetType.PositionTarget) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the target latitude for targets that have valid latitudes.
        /// </summary>
        /// <returns>Latitude in degrees.  Positive values are north of the
        /// equator, and negative values are south.</returns>
        public double TargetLatitude()
        {
            switch (vc.targetType)
            {
                case MASVesselComputer.TargetType.Vessel:
                    return vc.activeTarget.GetVessel().latitude;
                case MASVesselComputer.TargetType.DockingPort:
                    return vc.activeTarget.GetVessel().latitude;
                case MASVesselComputer.TargetType.PositionTarget:
                case MASVesselComputer.TargetType.CelestialBody:
                    // TODO: Is there a better way to do this?  Can I use GetVessel?
                    return vessel.mainBody.GetLatitude(vc.activeTarget.GetTransform().position);
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the target longitude for targets that have valid longitudes.
        /// </summary>
        /// <returns>Longitude in degrees.  Negative values are west of the prime
        /// meridian, and positive values are east of it.</returns>
        public double TargetLongitude()
        {
            switch (vc.targetType)
            {
                case MASVesselComputer.TargetType.Vessel:
                    return Utility.NormalizeLongitude(vc.activeTarget.GetVessel().longitude);
                case MASVesselComputer.TargetType.DockingPort:
                    return Utility.NormalizeLongitude(vc.activeTarget.GetVessel().longitude);
                case MASVesselComputer.TargetType.PositionTarget:
                case MASVesselComputer.TargetType.CelestialBody:
                    // TODO: Is there a better way to do this?
                    return vessel.mainBody.GetLongitude(vc.activeTarget.GetTransform().position);
            }

            return 0.0;
        }

        /// <summary>
        /// Get the name of the current target, or an empty string if there
        /// is no target.
        /// </summary>
        /// <returns>The name of the current target, or "" if there is no target.</returns>
        public string TargetName()
        {
            return vc.targetName;
        }

        /// <summary>
        /// Sets the target to the next moon of the body that vessel currently orbits.  If there
        /// are no moons orbiting the current body, nothing happens.
        /// 
        /// If the vessel is currently targeting anything other than a moon of the current body,
        /// that target is cleared and the first moon is selected, instead.
        /// 
        /// Moon order is based on the order that the moons appear in the CelestialBody's list of
        /// worlds.
        /// 
        /// If the vessel is currently orbiting the Sun, this method will target planets.
        /// </summary>
        /// <returns>Returns 1 if a moon was targeted.  0 otherwise.</returns>
        public double TargetNextMoon()
        {
            if (vc.mainBody.orbitingBodies != null)
            {
                int numMoons = vc.mainBody.orbitingBodies.Count;

                if (numMoons > 0)
                {
                    int moonIndex = -1;

                    if (vc.targetType == MASVesselComputer.TargetType.CelestialBody)
                    {
                        CelestialBody targetWorld = vc.activeTarget as CelestialBody;
                        moonIndex = vc.mainBody.orbitingBodies.FindIndex(t => (t == targetWorld));
                    }

                    if (moonIndex >= 0)
                    {
                        moonIndex = (moonIndex + 1) % numMoons;
                    }
                    else
                    {
                        moonIndex = 0;
                    }

                    FlightGlobals.fetch.SetVesselTarget(vc.mainBody.orbitingBodies[moonIndex]);

                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Sets the target to the nearest vessel in same SoI as the current vessel.
        /// 
        /// If the vessel is alreadying targeting a vessel in the same SoI, the next closest one will
        /// be targeted, instead.  If the current target is the closest vessel, the most distant one
        /// is selected.
        /// </summary>
        /// <returns>1 if a vessel was targeted, 0 otherwise.</returns>
        public double TargetNextVessel()
        {
            UpdateNeighboringVessels();

            int numVessels = neighboringVessels.Length;
            if (numVessels > 0)
            {
                if (vc.targetType != MASVesselComputer.TargetType.Vessel && vc.targetType != MASVesselComputer.TargetType.DockingPort)
                {
                    // Simple case: We're not currently targeting a vessel.
                    FlightGlobals.fetch.SetVesselTarget(neighboringVessels[0]);
                }
                else
                {
                    Vessel targetVessel;
                    if (vc.targetType == MASVesselComputer.TargetType.Vessel)
                    {
                        targetVessel = vc.activeTarget as Vessel;
                    }
                    else // Docking port
                    {
                        targetVessel = (vc.activeTarget as ModuleDockingNode).vessel;
                    }

                    int vesselIdx = Array.FindIndex(neighboringVessels, v => v.id == targetVessel.id);
                    int selectedIdx = 0;
                    if (vesselIdx == 0)
                    {
                        selectedIdx = neighboringVessels.Length - 1;
                    }
                    else
                    {
                        selectedIdx = vesselIdx - 1;
                    }

                    FlightGlobals.fetch.SetVesselTarget(neighboringVessels[selectedIdx]);
                }
                return 1.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the orbital speed of the current target, in m/s.  If there is no target, returns 0.
        /// </summary>
        /// <returns>Current orbital speed of the target, or 0.</returns>
        public double TargetOrbitSpeed()
        {
            if (vc.targetType > 0)
            {
                return vc.targetOrbit.orbitalSpeed;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Returns the target's periapsis.
        /// </summary>
        /// <returns>Target's Pe in meters, or 0 if there is no target.</returns>
        public double TargetPeriapsis()
        {
            if (vc.targetType > 0)
            {
                return vc.targetOrbit.PeA;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the relative inclination between the vessel and the target.
        /// </summary>
        /// <returns>Inclination in degrees.  Returns 0 if there is no target, or the
        /// target orbits a different celestial body.</returns>
        public double TargetRelativeInclination()
        {
            if (vc.targetType > 0 && vc.targetOrbit.referenceBody == vc.orbit.referenceBody)
            {
                return Vector3.Angle(vc.orbit.GetOrbitNormal(), vc.targetOrbit.GetOrbitNormal());
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if there is a target, and it is in the same SoI as the
        /// vessel (for example: both orbiting Kerbin, or both orbiting the Mun, but not
        /// one orbiting Kerbin, and the other orbiting the Mun).
        /// </summary>
        /// <returns>1 if the target is in the same SoI; 0 if not, or if there is no target.</returns>
        public double TargetSameSoI()
        {
            if (vc.activeTarget != null)
            {
                return (vc.targetOrbit.referenceBody == vc.orbit.referenceBody) ? 1.0 : 0.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the target's situation, based on the KSP variable:
        /// 
        /// * -1 - INVALID (no target)
        /// * 0 - LANDED
        /// * 1 - SPLASHED
        /// * 2 - PRELAUNCH
        /// * 3 - FLYING
        /// * 4 - SUB_ORBITAL
        /// * 5 - ORBITING
        /// * 6 - ESCAPING
        /// * 7 - DOCKED
        /// 
        /// For some Celestial Body target types, the situation is always
        /// 5 (ORBITING).
        /// </summary>
        /// <returns>A number between -1 and 7 (inclusive).</returns>
        public double TargetSituation()
        {
            switch (vc.targetType)
            {
                case MASVesselComputer.TargetType.None:
                    return -1.0;
                case MASVesselComputer.TargetType.Vessel:
                    return ConvertVesselSituation((vc.activeTarget as Vessel).situation);
                case MASVesselComputer.TargetType.DockingPort:
                    return ConvertVesselSituation((vc.activeTarget as ModuleDockingNode).vessel.situation);
                case MASVesselComputer.TargetType.CelestialBody:
                    return 5.0;
                case MASVesselComputer.TargetType.PositionTarget:
                    return 0.0;
                case MASVesselComputer.TargetType.Asteroid:
                    return 5.0;
                default:
                    return -1.0;
            }
        }

        /// <summary>
        /// Returns the semi-major axis of the target's orbit.
        /// </summary>
        /// <returns>SMA in meters, or 0 if there is no target.</returns>
        public double TargetSMA()
        {
            if (vc.activeTarget != null)
            {
                return vc.targetOrbit.semiMajorAxis;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the time until the target's next apoapsis.
        /// </summary>
        /// <returns>Time to Ap in seconds, or 0 if there's no target.</returns>
        public double TargetTimeToAp()
        {
            if (vc.targetType > 0)
            {
                return vc.targetOrbit.timeToAp;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the time until the target's next periapsis.
        /// </summary>
        /// <returns>Time to Pe in seconds, or 0 if there's no target.</returns>
        public double TargetTimeToPe()
        {
            if (vc.targetType > 0)
            {
                return vc.targetOrbit.timeToPe;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns a number identifying the target type.  Valid results are:
        /// 
        /// * 0: No target
        /// * 1: Target is a Vessel
        /// * 2: Target is a Docking Port
        /// * 3: Target is a Celestial Body
        /// * 4: Target is a Waypoint
        /// * 5: Target is an asteroid
        /// </summary>
        /// <returns>A number between 0 and 5 (inclusive)</returns>
        public double TargetType()
        {
            switch (vc.targetType)
            {
                case MASVesselComputer.TargetType.None:
                    return 0.0;
                case MASVesselComputer.TargetType.Vessel:
                    return 1.0;
                case MASVesselComputer.TargetType.DockingPort:
                    return 2.0;
                case MASVesselComputer.TargetType.CelestialBody:
                    return 3.0;
                case MASVesselComputer.TargetType.PositionTarget:
                    return 4.0;
                case MASVesselComputer.TargetType.Asteroid:
                    return 5.0;
                default:
                    return 0.0;
            }
        }

        /// <summary>
        /// Returns a number representing the target vessel type (eg, 1 = Ship, etc).
        /// 
        /// * 0 - Invalid (not one of the below types)
        /// * 1 - Ship
        /// * 2 - Plane
        /// * 3 - Probe
        /// * 4 - Lander
        /// * 5 - Station
        /// * 6 - Relay
        /// * 7 - Rover
        /// * 8 - Base
        /// * 9 - EVA
        /// * 10 - Flag
        /// * 11 - Debris
        /// * 12 - Space Object
        /// * 13 - Unknown
        /// * 14 - Celestial Body
        /// </summary>
        /// <returns>A value between 1 and 13 inclusive for a vessel-type target, or 14 for a Celestial Body, or 0 for no target, or another target type.</returns>
        public double TargetTypeId()
        {
            if (vc.targetType == MASVesselComputer.TargetType.Vessel || vc.targetType == MASVesselComputer.TargetType.DockingPort)
            {
                VesselType type = (vc.targetType == MASVesselComputer.TargetType.DockingPort) ? (vc.activeTarget as ModuleDockingNode).vessel.vesselType : (vc.activeTarget as Vessel).vesselType;
                switch (type)
                {
                    case global::VesselType.Ship:
                        return 1.0;
                    case global::VesselType.Plane:
                        return 2.0;
                    case global:: VesselType.Probe:
                        return 3.0;
                    case global::VesselType.Lander:
                        return 4.0;
                    case global::VesselType.Station:
                        return 5.0;
                    case global::VesselType.Relay:
                        return 6.0;
                    case global::VesselType.Rover:
                        return 7.0;
                    case global::VesselType.Base:
                        return 8.0;
                    case global::VesselType.EVA:
                        return 9.0;
                    case global::VesselType.Flag:
                        return 10.0;
                    case global::VesselType.Debris:
                        return 11.0;
                    case global::VesselType.SpaceObject:
                        return 12.0;
                    case global::VesselType.Unknown:
                        return 13.0;
                }
            }
            else if (vc.targetType == MASVesselComputer.TargetType.CelestialBody)
            {
                return 14.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the target's velocity relative to the left-right axis of the vessel.
        /// </summary>
        /// <returns>Velocity in m/s.  Positive means the vessel is moving 'right' relative
        /// to the target, and negative means 'left'.</returns>
        public double TargetVelocityX()
        {
            return Vector3.Dot(vc.targetRelativeVelocity, vc.referenceTransform.right);
        }

        /// <summary>
        /// Returns the target's velocity relative to the top-bottom axis of the
        /// vessel (the top / bottom of the vessel from the typical inline IVA's
        /// perspective).
        /// </summary>
        /// <returns>Velocity in m/s.  Positive means the vessel is moving 'up'
        /// relative to the target, negative means relative 'down'.</returns>
        public double TargetVelocityY()
        {
            return -Vector3.Dot(vc.targetRelativeVelocity, vc.referenceTransform.forward);
        }

        /// <summary>
        /// Returns the target's velocity relative to the forward-aft axis of
        /// the vessel (the nose of an aircraft, the 'top' of a vertically-launched
        /// craft).
        /// </summary>
        /// <returns>Velocity in m/s.  Positive means approaching, negative means departing.</returns>
        public double TargetVelocityZ()
        {
            return Vector3.Dot(vc.targetRelativeVelocity, vc.referenceTransform.up);
        }

        /// <summary>
        /// Returns the number of other non-debris vessels in the current SoI.  This count
        /// includes landed vessels as well as vessels in flight, but it does not count the
        /// current vessel.
        /// </summary>
        /// <returns>The number of other non-debris vessels, or 0 if there are none.</returns>
        public double TargetVesselCount()
        {
            UpdateNeighboringVessels();

            return neighboringVessels.Length;
        }

        /// <summary>
        /// Returns the distance to the non-debris target selected by id.  Distance
        /// is in meters.  The id parameter must be between 0 and fc.TargetVesselCount() - 1.
        /// </summary>
        /// <param name="id">The id number of the desired vessel.</param>
        /// <returns>Distance to the target in meters, or 0.</returns>
        public double TargetVesselDistance(double id)
        {
            UpdateNeighboringVessels();

            int index = (int)id;
            if (index >= 0 && index < neighboringVessels.Length)
            {
                return (neighboringVessels[index].GetTransform().position - vessel.GetTransform().position).magnitude;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the name of the non-debris vessel selected by id.  The id parameter
        /// must be between 0 and fc.TargetVesselCount() - 1.
        /// </summary>
        /// <param name="id">The id number of the desired vessel.</param>
        /// <returns>The name of the selected vessel, or an empty string if no valid vessel was selected.</returns>
        public string TargetVesselName(double id)
        {
            UpdateNeighboringVessels();

            int index = (int)id;
            if (index >= 0 && index < neighboringVessels.Length)
            {
                return neighboringVessels[index].GetName();
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Returns the name of the target vessel type (eg, "Ship", "Plane", "Station", etc).
        /// </summary>
        /// <returns>Name of the target vessel type, or an empty string if there is no target, or the target is not a vessel.</returns>
        public string TargetVesselType()
        {
            if (vc.targetType == MASVesselComputer.TargetType.Vessel)
            {
                return Utility.typeDict[(vc.activeTarget as Vessel).vesselType];
            }
            else if (vc.targetType == MASVesselComputer.TargetType.DockingPort)
            {
                return Utility.typeDict[(vc.activeTarget as ModuleDockingNode).vessel.vesselType];
            }

            return string.Empty;
        }

        /// <summary>
        /// Toggles the specified vesselType in the target tracking filter.  vesselType
        /// must be one of:
        /// 
        /// * 1 - Ship
        /// * 2 - Plane
        /// * 3 - Probe
        /// * 4 - Lander
        /// * 5 - Station
        /// * 6 - Relay
        /// * 7 - Rover
        /// * 8 - Base
        /// * 9 - EVA
        /// * 10 - Flag
        /// * 11 - Debris
        /// * 12 - Space Object
        /// * 13 - Unknown
        /// 
        /// Returns 1 if the provided vesselType was previously set, or 0 if it was not set
        /// or an invalid vesselType was supplied.
        /// </summary>
        /// <param name="vesselType">An integer value between 1 and 13, inclusive.</param>
        /// <returns>1 if the vesselType was cleared, 0 if it was not.</returns>
        public double ToggleTargetFilter(double vesselType)
        {
            int vBitIndex = (int)(vesselType);
            if (vBitIndex > 0 && vBitIndex <= 13)
            {
                fc.ToggleTargetFilter(vBitIndex);
                return 1.0;
            }

            return 0.0;
        }
        #endregion

        /// <summary>
        /// The Thermal section contains temperature monitoring values.
        /// </summary>
        #region Thermal

        /// <summary>
        /// Returns the static temperature of the atmosphere (or vacuum of space) outside the craft.
        /// 
        /// Static temperature does not account for compression heating caused by the vessel's passage through an
        /// atmosphere.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Ambient temperature in Kelvin or Celsius.</returns>
        public double AmbientTemperature(bool useKelvin)
        {
            if (vessel.atmosphericTemperature > 0.0)
            {
                return vessel.atmosphericTemperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the current actual temperature outside the vessel.
        /// 
        /// In an atmosphere at high speeds, this temperature represents the compression heating as the
        /// vessel travels through the atmosphere.  In a vacuum, this temperature is identical to `fc.AmbientTemperature(useKelvin)`.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>External temperature in Kelvin or Celsius.</returns>
        public double ExternalTemperature(bool useKelvin)
        {
            if (vessel.externalTemperature > 0.0)
            {
                return vessel.externalTemperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the direction of temperature change of the hottest engine.
        /// </summary>
        /// <returns>-1 if the temperature is cooling, +1 if it is increasing, +0 if it is stable or no heat shields are installed.</returns>
        public double HottestEngineTemperatureSign()
        {
            return vc.hottestEngineSign;
        }

        /// <summary>
        /// Returns the current temperature of the hottest engine, where hottest engine
        /// is defined as "closest to its maximum temperature".
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Current temperature of the hottest engine in Kelvin or Celsius.</returns>
        public double HottestEngineTemperature(bool useKelvin)
        {
            if (vc.hottestEngineTemperature > 0.0)
            {
                return vc.hottestEngineTemperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the maximum temperature of the hottest engine, where hottest engine
        /// is defined as "closest to its maximum temperature".
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Current temperature of the hottest engine in Kelvin or Celsius.</returns>
        public double HottestEngineMaxTemperature(bool useKelvin)
        {
            if (vc.hottestEngineMaxTemperature > 0.0)
            {
                return vc.hottestEngineMaxTemperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the maximum temperature of the hottest heat shield.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Heat shield maximum temperature in Kelvin or Celsius, or 0 if no heatshields are installed.</returns>
        public double HeatShieldMaxTemperature(bool useKelvin)
        {
            if (vc.hottestAblatorMax > 0.0)
            {
                return vc.hottestAblatorMax + ((useKelvin) ? 0.0 : KelvinToCelsius);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the current temperature of the hottest heat shield.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Heat shield temperature in Kelvin or Celsius, or 0 if no heatshields are installed.</returns>
        public double HeatShieldTemperature(bool useKelvin)
        {
            if (vc.hottestAblator > 0.0)
            {
                return vc.hottestAblator + ((useKelvin) ? 0.0 : KelvinToCelsius);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the direction of temperature change of the hottest heat shield.
        /// </summary>
        /// <returns>-1 if the temperature is cooling, +1 if it is increasing, +0 if it is stable or no heat shields are installed.</returns>
        public double HeatShieldTemperatureSign()
        {
            return vc.hottestAblatorSign;
        }

        /// <summary>
        /// Returns the maximum temperature of the current hottest part.
        /// </summary>
        /// <param name="useKelvin">When true, returns the temperature in units of Kelvin; when false, Celsius is used.</param>
        /// <returns>The current hottest part's maximum temperature, in degrees Celsius or Kelvin.</returns>
        public double HottestPartMaxTemperature(bool useKelvin)
        {
            return vc.hottestPartMax + ((useKelvin) ? 0.0 : KelvinToCelsius);
        }

        /// <summary>
        /// Returns the direction of temperature change of the hottest part.
        /// </summary>
        /// <returns>-1 if the temperature is cooling, +1 if it is increasing, +0 if it is stable.</returns>
        public double HottestPartSign()
        {
            return vc.hottestPartSign;
        }

        /// <summary>
        /// Returns the hottest part on the vessel (the part closest to its thermal limit).
        /// </summary>
        /// <param name="useKelvin">When true, returns the temperature in units of Kelvin; when false, Celsius is used.</param>
        /// <returns>The current hottest part's temperature, in degrees Celsius or Kelvin.</returns>
        public double HottestPartTemperature(bool useKelvin)
        {
            return vc.hottestPart + ((useKelvin) ? 0.0 : KelvinToCelsius);
        }

        /// <summary>
        /// Returns the maximum interior temperature of the current IVA pod.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Maximum temperature of the interior of the current IVA pod in Kelvin or Celsius.</returns>
        public double InternalMaxTemperature(bool useKelvin)
        {
            if (fc.part.maxTemp > 0.0)
            {
                return fc.part.maxTemp + ((useKelvin) ? 0.0 : KelvinToCelsius);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the interior temperature of the current IVA pod.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Current temperature of the interior of the current IVA pod in Kelvin or Celsius.</returns>
        public double InternalTemperature(bool useKelvin)
        {
            if (fc.part.temperature > 0.0)
            {
                return fc.part.temperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the direction of the temperature change in the pod interior.
        /// </summary>
        /// <returns>-1 if the temperature is cooling, +1 if it is increasing, +0 if it is stable.</returns>
        public double InternalTemperatureSign()
        {
            return Math.Sign(fc.part.thermalInternalFlux);
        }

        /// <summary>
        /// Returns the maximum skin temperature of the current IVA pod.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Maximum temperature of the skin of the current IVA pod in Kelvin or Celsius.</returns>
        public double PodMaxTemperature(bool useKelvin)
        {
            if (fc.part.skinMaxTemp > 0.0)
            {
                return fc.part.skinMaxTemp + ((useKelvin) ? 0.0 : KelvinToCelsius);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the skin temperature of the current IVA pod.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Current temperature of the skin of the current IVA pod in Kelvin or Celsius.</returns>
        public double PodTemperature(bool useKelvin)
        {
            if (fc.part.skinTemperature > 0.0)
            {
                return fc.part.skinTemperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the direction of the temperature change on the pod skin.
        /// </summary>
        /// <returns>-1 if the temperature is cooling, +1 if it is increasing, +0 if it is stable.</returns>
        public double PodTemperatureSign()
        {
            return Math.Sign(fc.part.thermalSkinFlux);
        }

        /// <summary>
        /// Returns 1 if there is at least one radiator active on the vessel.
        /// </summary>
        /// <returns>1 if any radiators are active, or 0 if no radiators are active or no radiators
        /// are installed.</returns>
        public double RadiatorActive()
        {
            return (vc.radiatorActive) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the number of radiators installed on the craft, regardless of their status
        /// (enabled / disabled / damaged).
        /// </summary>
        /// <returns></returns>
        public double RadiatorCount()
        {
            return vc.moduleRadiator.Length;
        }

        /// <summary>
        /// Returns 1 if any deployable radiators are damaged.
        /// </summary>
        /// <returns></returns>
        public double RadiatorDamaged()
        {
            return (vc.radiatorDamaged) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if at least one radiator may be deployed.
        /// </summary>
        /// <returns>1 if any radiators may be deployed, or 0 if no radiators may be deployed
        /// or no radiators are installed.</returns>
        public double RadiatorDeployable()
        {
            return (vc.radiatorDeployable) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns 1 if there is at least one radiator inactive on the vessel.
        /// </summary>
        /// <returns>1 if any radiators are inactive, or 0 if no radiators are inactive or no radiators
        /// are installed.</returns>
        public double RadiatorInactive()
        {
            return (vc.radiatorInactive) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns -1 if a deployable radiator is retracting, +1 if a deployable radiator is extending, or 0 if
        /// no deployable radiators are moving.
        /// </summary>
        /// <returns>-1, 0, or +1.</returns>
        public double RadiatorMoving()
        {
            return vc.radiatorMoving;
        }

        /// <summary>
        /// Returns a number representing the average position of undamaged deployable radiators.
        /// 
        /// * 0 - No radiators, no undamaged radiators, or all undamaged radiators are retracted.
        /// * 1 - All deployable radiators extended.
        /// 
        /// If the radiators are moving, a number between 0 and 1 is returned.
        /// </summary>
        /// <returns>A number between 0 and 1 as described in the summary.</returns>
        public double RadiatorPosition()
        {
            float numRadiators = 0.0f;
            float lerpPosition = 0.0f;
            for (int i = vc.moduleDeployableRadiator.Length - 1; i >= 0; --i)
            {
                if (vc.moduleDeployableRadiator[i].useAnimation && vc.moduleDeployableRadiator[i].deployState != ModuleDeployablePart.DeployState.BROKEN)
                {
                    numRadiators += 1.0f;

                    lerpPosition += vc.moduleDeployableRadiator[i].GetScalar;
                }
            }

            if (numRadiators > 1.0f)
            {
                return lerpPosition / numRadiators;
            }
            else if (numRadiators == 1.0f)
            {
                return lerpPosition;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns 1 if at least one radiator on the vessel may be retracted or
        /// undeployed.
        /// </summary>
        /// <returns>1 if a deployable radiator may be retracted, or 0 if none may be
        /// retracted or no deployable radiators are installed.</returns>
        public double RadiatorRetractable()
        {
            return (vc.radiatorRetractable) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns current radiator utilization as a percentage of maximum of active
        /// radiators.
        /// </summary>
        /// <returns>Current utilization, in the range of 0 to 1.  If no active radiators are installed,
        /// or none are active, returns 0.</returns>
        public double RadiatorUtilization()
        {
            return (vc.maxEnergyTransfer > 0.0) ? (vc.currentEnergyTransfer / vc.maxEnergyTransfer) : 0.0;
        }

        /// <summary>
        /// Deploys deployable radiators, or retracts retractable radiators.
        /// </summary>
        /// <param name="deploy">'true' to deploy radiators, 'false' to undeploy radiators.</param>
        /// <returns>1 if any radiators are now deploying or retracting.</returns>
        public double SetRadiator(bool deploy)
        {
            if (vc.radiatorDeployable && deploy)
            {
                for (int i = vc.moduleDeployableRadiator.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleDeployableRadiator[i].useAnimation && vc.moduleDeployableRadiator[i].deployState == ModuleDeployablePart.DeployState.RETRACTED)
                    {
                        vc.moduleDeployableRadiator[i].Extend();
                    }
                }
                return 1.0;
            }
            else if (vc.radiatorRetractable && !deploy)
            {
                for (int i = vc.moduleDeployableRadiator.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleDeployableRadiator[i].useAnimation && vc.moduleDeployableRadiator[i].retractable && vc.moduleDeployableRadiator[i].deployState == ModuleDeployablePart.DeployState.EXTENDED)
                    {
                        vc.moduleDeployableRadiator[i].Retract();
                    }
                }
                return 1.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the skin temperature of the current IVA pod.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Current temperature of the interior of the current IVA pod in Kelvin or Celsius.</returns>
        public double SkinTemperature(bool useKelvin)
        {
            if (fc.part.skinTemperature > 0.0)
            {
                return fc.part.skinTemperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Deploys deployable radiators, or retracts retractable radiators.
        /// </summary>
        /// <returns>1 if any deployable radiators are installed.  0 otherwise.</returns>
        public double ToggleRadiator()
        {
            if (vc.radiatorDeployable)
            {
                for (int i = vc.moduleDeployableRadiator.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleDeployableRadiator[i].useAnimation && vc.moduleDeployableRadiator[i].deployState == ModuleDeployablePart.DeployState.RETRACTED)
                    {
                        vc.moduleDeployableRadiator[i].Extend();
                    }
                }
            }
            else if (vc.radiatorRetractable)
            {
                for (int i = vc.moduleDeployableRadiator.Length - 1; i >= 0; --i)
                {
                    if (vc.moduleDeployableRadiator[i].useAnimation && vc.moduleDeployableRadiator[i].retractable && vc.moduleDeployableRadiator[i].deployState == ModuleDeployablePart.DeployState.EXTENDED)
                    {
                        vc.moduleDeployableRadiator[i].Retract();
                    }
                }
            }

            return (vc.moduleDeployableRadiator.Length > 0) ? 1.0 : 0.0;
        }
        #endregion

        /// <summary>
        /// The Time section provides access to the various timers in MAS (and KSP).
        /// </summary>
        #region Time
        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns the hour of the day (0-5.999... using the Kerbin clock, 0-23.999... using the
        /// Earth clock).  Fraction of the hour is retained.
        /// </summary>
        /// <param name="time">Time in seconds (eg, `fc.UT()`).</param>
        /// <returns>The hour of the day, accounting for Kerbin time vs. Earth time.</returns>
        public double HourOfDay(double time)
        {
            return TimeOfDay(time) / 3600.0;
        }

        /// <summary>
        /// Fetch the current MET (Mission Elapsed Time) for the vessel in
        /// seconds.
        /// </summary>
        /// <returns>Mission time, in seconds.</returns>
        public double MET()
        {
            return vessel.missionTime;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Given a standard time in seconds, return the minutes of the hour (a
        /// number from 0 to 60).  Fractions of a minute are retained and negative
        /// values are converted to positive.
        /// </summary>
        /// <param name="time">Time in seconds (eg, `fc.MET()`).</param>
        /// <returns>A number representing the minutes in the hour in the range [0, 60).</returns>
        public double MinutesOfHour(double time)
        {
            return (Math.Abs(time) / 60.0) % 60.0;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Given a standard time in seconds, return the seconds of the minute (the
        /// number from 0 to 60).  Fractions of a second are retained and negative
        /// values are converted to positive.
        /// </summary>
        /// <param name="time">Time in seconds (eg, `fc.MET()`).</param>
        /// <returns>A number representing the seconds in the minute in the range [0, 60).</returns>
        public double SecondsOfMinute(double time)
        {
            return Math.Abs(time) % 60.0;
        }

        /// <summary>
        /// Similar to `fc.HourOfDay()`, but returning the answer in seconds instead
        /// of hours.
        /// 
        /// When used with `fc.UT()`, for instance, it returns the number of seconds since midnight UT.
        /// </summary>
        /// <returns>Number of seconds since the latest day began.</returns>
        public double TimeOfDay(double time)
        {
            return time % ((double)KSPUtil.dateTimeFormatter.Day);
        }

        /// <summary>
        /// Given an altitude in meters, return the number of seconds until the vessel
        /// next crosses that altitude.  If the vessel is on a hyperbolic orbit, or
        /// if the orbit never crosses the given altitude, return 0.0.
        /// </summary>
        /// <param name="altitude">Altitude above the datum, in meters.</param>
        /// <returns>Time in seconds until the altitude is crossed, or 0 if the orbit does not cross that altitude.</returns>
        public double TimeToAltitude(double altitude)
        {
            if (vc.orbit.ApA >= altitude && vc.orbit.PeA <= altitude && vc.orbit.eccentricity < 1.0)
            {
                return Utility.NextTimeToRadius(vc.orbit, altitude + vc.orbit.referenceBody.Radius);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// For non-hyperbolic orbits, returns time to the next equatorial
        /// Ascending Node.
        /// </summary>
        /// <returns>Time to AN, seconds, or 0 if the orbit is hyperbolic.</returns>
        public double TimeToANEq()
        {
            if (vc.orbit.eccentricity < 1.0)
            {
                Vector3d ANVector = vc.orbit.GetANVector();
                ANVector.Normalize();
                double taAN = vc.orbit.GetTrueAnomalyOfZupVector(ANVector);
                double timeAN = vc.orbit.GetUTforTrueAnomaly(taAN, vc.orbit.period) - vc.universalTime;

                return Utility.NormalizeOrbitTime(timeAN, vc.orbit);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the time until the next ascending node with the current target,
        /// provided the target is orbiting the same body as the vessel (and the target
        /// exists).
        /// </summary>
        /// <returns>Time in seconds to the next ascending node, in seconds, or 0.</returns>
        public double TimeToANTarget()
        {
            if (vc.orbit.eccentricity < 1.0 && vc.targetType != MASVesselComputer.TargetType.None && vc.orbit.referenceBody == vc.activeTarget.GetOrbit().referenceBody)
            {
                Vector3d vesselNormal = vc.orbit.GetOrbitNormal();
                Vector3d targetNormal = vc.activeTarget.GetOrbit().GetOrbitNormal();
                Vector3d cross = Vector3d.Cross(vesselNormal, targetNormal);
                double taAN = vc.orbit.GetTrueAnomalyOfZupVector(-cross);
                double timeAN = vc.orbit.GetUTforTrueAnomaly(taAN, vc.orbit.period) - vc.universalTime;

                return Utility.NormalizeOrbitTime(timeAN, vc.orbit);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Fetch the time to the next apoapsis.  If the orbit is hyperbolic,
        /// or the vessel is not flying, return 0.
        /// </summary>
        /// <returns>Time until Ap in seconds, or 0 if the time would be invalid.</returns>
        public double TimeToAp()
        {
            if (vesselSituationConverted > 2 && vc.orbit.eccentricity < 1.0)
            {
                return vc.orbit.timeToAp;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Fetch the time until the vessel's orbit next enters or exits the
        /// body's atmosphere.  If there is no atmosphere, or the orbit does not
        /// cross that threshold, return 0.
        /// </summary>
        /// <returns>Time until the atmosphere boundary is crossed, in seconds; 0 for invalid times.</returns>
        public double TimeToAtmosphere()
        {
            if (vc.mainBody.atmosphere)
            {
                return TimeToAltitude(vc.mainBody.atmosphereDepth);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the time to the equatorial descending node, in seconds.
        /// </summary>
        /// <returns>Time in seconds to the next descending node, or 0 if the orbit is hyperbolic.</returns>
        public double TimeToDNEq()
        {
            if (vc.orbit.eccentricity < 1.0)
            {
                Vector3d DNVector = vc.orbit.GetANVector();
                DNVector.Normalize();
                double taDN = vc.orbit.GetTrueAnomalyOfZupVector(-DNVector);
                double timeDN = vc.orbit.GetUTforTrueAnomaly(taDN, vc.orbit.period) - vc.universalTime;

                return Utility.NormalizeOrbitTime(timeDN, vc.orbit);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the time until the next descending node with the current target,
        /// provided the target is orbiting the same body as the vessel (and the target
        /// exists).
        /// </summary>
        /// <returns>Time in seconds to the next descending node, in seconds, or 0.</returns>
        public double TimeToDNTarget()
        {
            if (vc.orbit.eccentricity < 1.0 && vc.targetType != MASVesselComputer.TargetType.None && vc.orbit.referenceBody == vc.activeTarget.GetOrbit().referenceBody)
            {
                Vector3d vesselNormal = vc.orbit.GetOrbitNormal();
                Vector3d targetNormal = vc.activeTarget.GetOrbit().GetOrbitNormal();
                Vector3d cross = Vector3d.Cross(vesselNormal, targetNormal);
                double taDN = vc.orbit.GetTrueAnomalyOfZupVector(cross);
                double timeDN = vc.orbit.GetUTforTrueAnomaly(taDN, vc.orbit.period) - vc.universalTime;

                return Utility.NormalizeOrbitTime(timeDN, vc.orbit);
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Alias for `fc.LandingTime()`
        /// </summary>
        /// <returns>Time in seconds until landing; 0 for invalid times.</returns>
        public double TimeToLanding()
        {
            return LandingTime();
        }

        /// <summary>
        /// Fetch the time to the next periapsis.  If the vessel is not
        /// flying, the value will be zero.  If the vessel is on a hyperbolic
        /// orbit, and it has passed the periapsis already, the value will
        /// be negative.
        /// </summary>
        /// <returns>Time until the next Pe in seconds, or 0 if the time would
        /// be invalid.  May return a negative number in hyperbolic orbits.</returns>
        public double TimeToPe()
        {
            if (vesselSituationConverted > 2)
            {
                return vc.orbit.timeToPe;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the number of seconds until the vessel's orbit transitions to
        /// another sphere of influence (leaving the current one and entering another).
        /// </summary>
        /// <returns>Time until transition in seconds; 0 if the orbit does not cross a
        /// Sphere of Influence.</returns>
        public double TimeToSoI()
        {
            if (vc.orbit.patchEndTransition == Orbit.PatchTransitionType.ESCAPE || vc.orbit.patchEndTransition == Orbit.PatchTransitionType.ENCOUNTER)
            {
                return vc.orbit.UTsoi - vc.universalTime;
            }

            return 0.0;
        }

        /// <summary>
        /// Fetch the current UT (universal time) in seconds.
        /// </summary>
        /// <returns>Universal Time, in seconds.</returns>
        public double UT()
        {
            return vc.universalTime;
        }

        /// <summary>
        /// Returns the current time warp multiplier.
        /// </summary>
        /// <returns>1 for normal speed, larger values for various warps.</returns>
        public double WarpRate()
        {
            return TimeWarp.CurrentRate;
        }
        #endregion

        /// <summary>
        /// The Trim section provides control over aircraft trim settings.
        /// </summary>
        #region Trim

        /// <summary>
        /// Returns the current pitch trim.
        /// </summary>
        /// <returns>Current pitch trim, in the range [-1, 1].</returns>
        public double GetPitchTrim()
        {
            return FlightInputHandler.state.pitchTrim;
        }

        /// <summary>
        /// Returns the current roll trim.
        /// </summary>
        /// <returns>Current roll trim, in the range [-1, 1].</returns>
        public double GetRollTrim()
        {
            return FlightInputHandler.state.rollTrim;
        }

        /// <summary>
        /// Returns the current yaw trim.
        /// </summary>
        /// <returns>Current yaw trim, in the range [-1, 1].</returns>
        public double GetYawTrim()
        {
            return FlightInputHandler.state.yawTrim;
        }

        /// <summary>
        /// Returns the current pitch trim.
        /// </summary>
        /// <param name="pitchTrim">The new pitch trim, in the range [-1, 1]</param>
        /// <returns>Updated pitch trim, in the range [-1, 1].</returns>
        public double SetPitchTrim(double pitchTrim)
        {
            FlightInputHandler.state.pitchTrim = Mathf.Clamp((float)pitchTrim, -1.0f, 1.0f);
            return FlightInputHandler.state.pitchTrim;
        }

        /// <summary>
        /// Returns the current roll trim.
        /// </summary>
        /// <param name="rollTrim">The new roll trim, in the range [-1, 1]</param>
        /// <returns>Updated roll trim, in the range [-1, 1].</returns>
        public double SetRollTrim(double rollTrim)
        {
            FlightInputHandler.state.rollTrim = Mathf.Clamp((float)rollTrim, -1.0f, 1.0f);
            return FlightInputHandler.state.rollTrim;
        }

        /// <summary>
        /// Returns the current yaw trim.
        /// </summary>
        /// <param name="yawTrim">The new yaw trim, in the range [-1, 1]</param>
        /// <returns>Updated yaw trim, in the range [-1, 1].</returns>
        public double SetYawTrim(double yawTrim)
        {
            FlightInputHandler.state.yawTrim = Mathf.Clamp((float)yawTrim, -1.0f, 1.0f);
            return FlightInputHandler.state.yawTrim;
        }

        /// <summary>
        /// Update all trim settings at once.
        /// </summary>
        /// <param name="yawTrim">The new yaw trim, in the range [-1, 1]</param>
        /// <param name="pitchTrim">The new pitch trim, in the range [-1, 1]</param>
        /// <param name="rollTrim">The new roll trim, in the range [-1, 1]</param>
        /// <returns>Always returns 1.</returns>
        public double SetTrim(double yawTrim, double pitchTrim, double rollTrim)
        {
            FlightInputHandler.state.yawTrim = Mathf.Clamp((float)yawTrim, -1.0f, 1.0f);
            FlightInputHandler.state.pitchTrim = Mathf.Clamp((float)pitchTrim, -1.0f, 1.0f);
            FlightInputHandler.state.rollTrim = Mathf.Clamp((float)rollTrim, -1.0f, 1.0f);

            return 1.0;
        }
        #endregion

        /// <summary>
        /// Variables that have not been assigned to a different category are
        /// dumped in this region until I figured out where to put them.
        /// </summary>
        #region Unassigned Region

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns 1 if `condition` is true, 0 if it is false.
        /// </summary>
        /// <param name="condition">The condition to test</param>
        /// <returns>1 if `condition` is true, 0 if it is false.</returns>
        public double BoolToNumber(bool condition)
        {
            return (condition) ? 1.0 : 0.0;
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Remaps `value` from the range [`bound1`, `bound2`] to the range
        /// [`map1`, `map2`].
        /// 
        /// The order of the bound and map parameters will be interpreted
        /// correctly.  For instance, `fc.Remap(var, 1, 0, 0, 1)` will
        /// have the same effect as `1 - var`.
        /// </summary>
        /// <param name="value">An input number</param>
        /// <param name="bound1">One of the two bounds of the source range.</param>
        /// <param name="bound2">The other bound of the source range.</param>
        /// <param name="map1">The first value of the destination range.</param>
        /// <param name="map2">The second value of the destination range.</param>
        /// <returns></returns>
        public double Remap(double value, double bound1, double bound2, double map1, double map2)
        {
            return value.Remap(bound1, bound2, map1, map2);
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Returns `trueValue` when `condition` is true, otherwise returns `falseValue`.
        /// 
        /// `trueValue` and `falseValue` may be numbers or strings.  They do not *have* to
        /// be the same type.
        /// 
        /// `condition` may be either a boolean value or a number.  If it a a number, then
        /// `trueValue` is returned if `condition` is greater than zero.  `falseValue` is
        /// returned if `condition` is equal to or less than zero.
        /// </summary>
        /// <param name="condition">The condition that selects the value.</param>
        /// <param name="trueValue">The value returned when `condition` is true.</param>
        /// <param name="falseValue">The value returned when `condition` is false.</param>
        /// <returns>One of `trueValue` or `falseValue`.</returns>
        public object Select(bool condition, object trueValue, object falseValue)
        {
            if (condition)
            {
                return trueValue;
            }
            else
            {
                return falseValue;
            }
        }

        // Add signatures that allow us to avoid paying for boxing / unboxing doubles in the
        // trueValue and falseValue cases.
        [MASProxy(Dependent = true)]
        public object Select(double condition, object trueValue, object falseValue)
        {
            if (condition > 0.0)
            {
                return trueValue;
            }
            else
            {
                return falseValue;
            }
        }

        [MASProxy(Dependent = true)]
        public double Select(double condition, double trueValue, double falseValue)
        {
            if (condition > 0.0)
            {
                return trueValue;
            }
            else
            {
                return falseValue;
            }
        }

        [MASProxy(Dependent = true)]
        public double Select(bool condition, double trueValue, double falseValue)
        {
            if (condition)
            {
                return trueValue;
            }
            else
            {
                return falseValue;
            }
        }

        [MASProxy(Dependent = true)]
        /// <summary>
        /// Select one of three values, depending on the value of `condition`.
        /// 
        /// If `condition` is less than zero, `negativeValue` is returned.  If `condition` is
        /// exactly zero, `zeroValue` is returned.  If `condition` is positive, `positiveValue`
        /// is returned.
        /// 
        /// `negativeValue`, `zeroValue`, and `positiveValue` may be numbers or strings.  They do not *have* to
        /// be all numbers or all strings.
        /// 
        /// Remember that numeric precision may may it difficult for equations to return exactly
        /// zero.
        /// </summary>
        /// <param name="condition">A numeric value that selects the response.</param>
        /// <param name="negativeValue">The value returned when `condition` is less than zero.</param>
        /// <param name="zeroValue">The value returned when `condition` is exactly zero.</param>
        /// <param name="positiveValue">The value returned when `condition` is greater than zero.</param>
        /// <returns>One of `negativeValue`, `zeroValue`, or `positiveValue`.</returns>
        public object Select(double condition, object negativeValue, object zeroValue, object positiveValue)
        {
            if (condition < 0.0)
            {
                return negativeValue;
            }
            else if (condition > 0.0)
            {
                return positiveValue;
            }
            else
            {
                return zeroValue;
            }
        }

        /// <summary>
        /// Send a softkey event to the named monitor.  A softkey is a numeric integer code that
        /// may be interpreted by the active page on that monitor, or it may be forwarded to
        /// the components of that page (such as an `RPM_MODULE` node).
        /// </summary>
        /// <param name="monitorName">The name of the monitor.</param>
        /// <param name="softkeyNumber">The softkey code to send.</param>
        /// <returns>1 if the code was processed, 0 otherwise.</returns>
        public double SendSoftkey(string monitorName, double softkeyNumber)
        {
            return (fc.HandleSoftkey(monitorName, (int)softkeyNumber)) ? 1.0 : 0.0;
        }
        #endregion

        /// <summary>
        /// The Vessel Info group contains non-flight information about the vessel (such
        /// as vessel name, type, etc.).
        /// </summary>
        #region Vessel Info

        /// <summary>
        /// Returns the text entered into the vessel description in the Editor.
        /// 
        /// Line breaks are preserved, so this text may display as multiple lines.
        /// Any Action Group Memos (lines that begin with "AG") are removed.
        /// </summary>
        public string VesselDescription()
        {
            return fc.vesselDescription;
        }

        /// <summary>
        /// Returns the name of the vessel.
        /// </summary>
        /// <returns></returns>
        public string VesselName()
        {
            return vessel.vesselName;
        }

        /// <summary>
        /// Returns a string naming the type of vessel.
        /// </summary>
        /// <returns></returns>
        public string VesselType()
        {
            return Utility.typeDict[vessel.vesselType];
        }

        /// <summary>
        /// Returns a number representing the vessel type (eg, 1 = Ship, etc).
        /// 
        /// * 0 - Invalid (not one of the below types)
        /// * 1 - Ship
        /// * 2 - Plane
        /// * 3 - Probe
        /// * 4 - Lander
        /// * 5 - Station
        /// * 6 - Relay
        /// * 7 - Rover
        /// * 8 - Base
        /// * 9 - EVA
        /// * 10 - Flag
        /// * 11 - Debris
        /// * 12 - Space Object
        /// * 13 - Unknown
        /// * 14 - Celestial Body
        /// </summary>
        /// <returns>A value between 1 and 13 inclusive for a vessel, or 14 for a Celestial Body, or 0 another target type.</returns>
        public double VesselTypeId()
        {
            switch (vessel.vesselType)
            {
                case global::VesselType.Ship:
                    return 1.0;
                case global::VesselType.Plane:
                    return 2.0;
                case global:: VesselType.Probe:
                    return 3.0;
                case global::VesselType.Lander:
                    return 4.0;
                case global::VesselType.Station:
                    return 5.0;
                case global::VesselType.Relay:
                    return 6.0;
                case global::VesselType.Rover:
                    return 7.0;
                case global::VesselType.Base:
                    return 8.0;
                case global::VesselType.EVA:
                    return 9.0;
                case global::VesselType.Flag:
                    return 10.0;
                case global::VesselType.Debris:
                    return 11.0;
                case global::VesselType.SpaceObject:
                    return 12.0;
                case global::VesselType.Unknown:
                    return 13.0;
            }

            return 0.0;
        }
        #endregion
    }
}
