/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2018 MOARdV
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
    /// computer module and the Lua environment.  It is a thin wrapper over
    /// the flight computer that prevents in-Lua access to some elements.
    /// 
    /// Note that this class must be stateless - it can not maintain variables
    /// between calls because there is no guarantee it'll exist next time it's
    /// called.
    /// 
    /// Also note that, while it is a wrapper for ASFlightComputer, not all
    /// values are plumbed through to the flight computer (for instance, the
    /// action group control and state are all handled in this class).
    /// </summary>
    /// <LuaName>fc</LuaName>
    /// <mdDoc>
    /// The `fc` group contains the core interface between KSP, Avionics
    /// Systems, and props in an IVA.  It consists of many 'variable' functions
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
    /// *NOTE:* If a variable listed below includes an entry for 'Required Mod(s)',
    /// then the mod listed (or any of the mods, if more than one) must be installed
    /// for that particular feature to work.
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
        /// Reports the total mass, in kg, of resources consumed by all currently-active engines on the vessel.
        /// </summary>
        /// <returns>The current mass of the active propellants in the vessel, in kg.</returns>
        public double PropellantCurrent()
        {
            return vc.enginePropellant.currentQuantity;
        }

        /// <summary>
        /// Reports the propellant consumption rate in kg/s for all active engines on the vessel.
        /// </summary>
        /// <returns>The current propellant consumption rate, in kg/s.</returns>
        public double PropellantDelta()
        {
            return vc.enginePropellant.deltaPerSecond;
        }

        /// <summary>
        /// Reports the maximum amount of propellant, in kg, that may be carried aboard the vessel.
        /// </summary>
        /// <returns>The maximum propellant capacity, in kg.</returns>
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
            return (vc.enginePropellant.maxQuantity > 0.0f) ? (vc.enginePropellant.currentQuantity / vc.enginePropellant.maxQuantity) : 0.0;
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
        /// Reports the current amount of propellant available, in kg, to active engines on the current stage.
        /// </summary>
        /// <returns>The current mass of propellant accessible by the current stage, in kg.</returns>
        public double PropellantStageCurrent()
        {
            return vc.enginePropellant.currentStage;
        }

        /// <summary>
        /// Reports the maximum amount of propellant available, in kg, to the active engiens on the
        /// current stage.
        /// </summary>
        /// <returns>The maximum mass of propellant accessibly by the current stage, in kg.</returns>
        public double PropellantStageMax()
        {
            return vc.enginePropellant.maxStage;
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
        /// Reports whether the vessel's propellant percentage falls between the two listed bounds.
        /// The bounds do not need to be in numerical order.
        /// 
        /// If there is no propellant or active engines, returns 0.  Doing so makes this
        /// function useful for alerts, for example.
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
        /// Tracks the current total mass of all resources consumed by installed RCS thrusters.
        /// </summary>
        /// <returns>Total RCS propellant mass in kg.</returns>
        public double RcsCurrent()
        {
            return vc.rcsPropellant.currentQuantity;
        }

        /// <summary>
        /// Tracks the current resource consumption rate by installed RCS thrusters.
        /// </summary>
        /// <returns>RCS propellant consumption rate in kg/s.</returns>
        public double RcsDelta()
        {
            return vc.rcsPropellant.deltaPerSecond;
        }

        /// <summary>
        /// Tracks the total mass that can be carried in all RCS propellant tanks.
        /// </summary>
        /// <returns>Maximum propellant capacity in kg.</returns>
        public double RcsMax()
        {
            return vc.rcsPropellant.maxQuantity;
        }

        /// <summary>
        /// Tracks the percentage of total RCS propellant mass currently onboard.
        /// </summary>
        /// <returns>Current RCS propellant supply, between 0 and 1.</returns>
        public double RcsPercent()
        {
            return (vc.rcsPropellant.maxQuantity > 0.0f) ? (vc.rcsPropellant.currentQuantity / vc.rcsPropellant.maxQuantity) : 0.0;
        }

        /// <summary>
        /// Reports the current amount of RCS propellant available to the active stage.
        /// </summary>
        /// <returns>Available RCS propellant, in kg.</returns>
        public double RcsStageCurrent()
        {
            return vc.rcsPropellant.currentStage;
        }

        /// <summary>
        /// Reports the maximum amount of RCS propellant storage accessible by the current stage.
        /// </summary>
        /// <returns>Maximum stage RCS propellant mass, in kg.</returns>
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
        /// Returns the current amount of the selected resource.
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
        /// Returns the current amount of the selected resource in the current stage.
        /// </summary>
        /// <param name="resourceId">A number between 0 and `fc.ResourceCount()`-1 or the name of a resource.</param>
        /// <returns></returns>
        public double ResourceStageCurrent(object resourceId)
        {
            return vc.ResourceCurrent(resourceId);
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
        /// Set the SAS state to on or off per the parameter.
        /// </summary>
        /// <param name="active"></param>
        public void SetSAS(bool active)
        {
            vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, active);
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

        /// <summary>
        /// Internal method to set SAS mode and update the UI.
        /// TODO: Radial Out / Radial In may be backwards (either in the display,
        /// or in the enums).
        /// </summary>
        /// <param name="mode">Mode to set</param>
        private void TrySetSASMode(VesselAutopilot.AutopilotMode mode)
        {
            if (vessel.Autopilot.CanSetMode(mode))
            {
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
        /// Returns the magnitude g-forces currently affecting the craft, in gees.
        /// </summary>
        /// <returns>Current instantaneous force in Gs.</returns>
        public double GForce()
        {
            return vessel.geeForce_immediate;
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
            return Vector3.Dot(vc.surfacePrograde, vc.surfaceForward);
        }

        /// <summary>
        /// Returns the lateral (right/left) component of surface velocity in
        /// m/s.  This value could become zero at extreme roll orientations.
        /// Positive values are to the right, negative to the left.
        /// </summary>
        /// <returns>The vessel's left/right velocity in m/s.  Right is positive; left is negative.</returns>
        public double SurfaceLateralSpeed()
        {
            return Vector3.Dot(vc.surfacePrograde, vc.surfaceRight);
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
        /// related to both staging and stage locks are all in the Staging category.
        /// </summary>
        #region Staging
        /// <summary>
        /// Returns the current stage.
        /// </summary>
        /// <returns>A whole number 0 or larger.</returns>
        public double CurrentStage()
        {
            return StageManager.CurrentStage;
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
        /// Sets stage locking to the specified setting (true or false).
        /// </summary>
        /// <param name="locked">`true` to lock staging, `false` to unlock staging.</param>
        public void SetStageLocked(bool locked)
        {
            if (locked)
            {
                InputLockManager.SetControlLock(ControlTypes.STAGING, "manualStageLock");
            }
            else
            {
                InputLockManager.RemoveControlLock("manualStageLock");
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
        /// Returns the number of available docking ports found on the target vessel when the following
        /// conditions are met:
        /// 
        /// 1) There are docking ports on the target vessel compatible with the designated
        /// docking port on the current vessel.  The designated docking port is either the
        /// only docking port on the current vessel, or it is one of the docking ports selected
        /// arbitrarily.
        /// 
        /// 2) There are no docking ports on the current vessel.  In this case, all docking
        /// ports on the target vessel are counted.
        /// 
        /// Note that if the target is unloaded, this method will return 0.  If the target is
        /// not a vessel, it also returns 0.
        /// </summary>
        /// <returns>Number of available compatible docking ports, or total available docking ports, or 0.</returns>
        public double TargetAvailableDockingPorts()
        {
            Vessel targetVessel = null;
            if (vc.targetType == MASVesselComputer.TargetType.Vessel)
            {
                targetVessel = vc.activeTarget as Vessel;
            }
            else if (vc.targetType == MASVesselComputer.TargetType.DockingPort)
            {
                targetVessel = (vc.activeTarget as ModuleDockingNode).vessel;
            }

            if (targetVessel != null)
            {
                List<ModuleDockingNode> docks = targetVessel.FindPartModulesImplementing<ModuleDockingNode>();
                if (docks == null)
                {
                    // Can it return null?
                    return 0.0;
                }

                int numAvailable = 0;
                if (vc.dockingNode != null)
                {
                    for (int i = docks.Count - 1; i >= 0; --i)
                    {
                        ModuleDockingNode otherDock = docks[i];
                        // Only lock on to an available dock of the same type that is either ungendered or the opposite gender.
                        if (otherDock.state == "Ready" && (string.IsNullOrEmpty(vc.dockingNode.nodeType) || vc.dockingNode.nodeType == otherDock.nodeType) && (vc.dockingNode.gendered == false || vc.dockingNode.genderFemale != otherDock.genderFemale))
                        {
                            ++numAvailable;
                        }
                    }
                }
                else
                {
                    for (int i = docks.Count - 1; i >= 0; --i)
                    {
                        ModuleDockingNode otherDock = docks[i];
                        // Only lock on to an available dock of the same type that is either ungendered or the opposite gender.
                        if (otherDock.state == "Ready")
                        {
                            ++numAvailable;
                        }
                    }
                }

                return numAvailable;
            }

            return 0.0;
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
        /// Returns the current atmosphere / ambient temperature outside the craft.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Ambient temperature in Kelvin or Celsius.</returns>
        public double AmbientTemperature(bool useKelvin)
        {
            return vessel.atmosphericTemperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
        }

        /// <summary>
        /// Returns the current temperature outside the vessel.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>External temperature in Kelvin or Celsius.</returns>
        public double ExternalTemperature(bool useKelvin)
        {
            return vessel.externalTemperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
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
            return vc.hottestEngineTemperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
        }

        /// <summary>
        /// Returns the maximum temperature of the hottest engine, where hottest engine
        /// is defined as "closest to its maximum temperature".
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Current temperature of the hottest engine in Kelvin or Celsius.</returns>
        public double HottestEngineMaxTemperature(bool useKelvin)
        {
            return vc.hottestEngineMaxTemperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
        }

        /// <summary>
        /// Returns the maximum temperature of the hottest heat shield.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Heat shield maximum temperature in Kelvin or Celsius, or 0 if no heatshields are installed.</returns>
        public double HeatShieldMaxTemperature(bool useKelvin)
        {
            return vc.hottestAblatorMax + ((useKelvin) ? 0.0 : KelvinToCelsius);
        }

        /// <summary>
        /// Returns the current temperature of the hottest heat shield.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Heat shield temperature in Kelvin or Celsius, or 0 if no heatshields are installed.</returns>
        public double HeatShieldTemperature(bool useKelvin)
        {
            return vc.hottestAblator + ((useKelvin) ? 0.0 : KelvinToCelsius);
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
        /// Returns the maximum interior temperature of the current IVA pod.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Maximum temperature of the interior of the current IVA pod in Kelvin or Celsius.</returns>
        public double InternalMaxTemperature(bool useKelvin)
        {
            return fc.part.maxTemp + ((useKelvin) ? 0.0 : KelvinToCelsius);
        }

        /// <summary>
        /// Returns the interior temperature of the current IVA pod.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Current temperature of the interior of the current IVA pod in Kelvin or Celsius.</returns>
        public double InternalTemperature(bool useKelvin)
        {
            return fc.part.temperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
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
            return fc.part.skinMaxTemp + ((useKelvin) ? 0.0 : KelvinToCelsius);
        }

        /// <summary>
        /// Returns the skin temperature of the current IVA pod.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Current temperature of the skin of the current IVA pod in Kelvin or Celsius.</returns>
        public double PodTemperature(bool useKelvin)
        {
            return fc.part.skinTemperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
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
        /// Returns 1 if the deployable radiators are damaged.
        /// </summary>
        /// <returns></returns>
        public double RadiatorDamaged()
        {
            return (vc.radiatorPosition == 0) ? 1.0 : 0.0;
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
        /// Returns 1 if at least one radiator on the vessel is deploying or
        /// retracting.
        /// </summary>
        /// <returns>1 if a deployable radiator is moving, or 0 if none are moving.
        /// </returns>
        public double RadiatorMoving()
        {
            return (vc.radiatorMoving) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns a number representing deployable radiator position:
        /// 
        /// * 0 = Broken
        /// * 1 = Retracted
        /// * 2 = Retracting
        /// * 3 = Extending
        /// * 4 = Extended
        /// 
        /// If there are multiple radiators, the first non-broken radiator's state
        /// is reported; if all radiators are broken, the state will be 0.
        /// </summary>
        /// <returns>Radiator Position (a number between 0 and 4); 1 if no radiators are installed.</returns>
        public double RadiatorPosition()
        {
            return vc.radiatorPosition;
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
        /// Returns the skin temperature of the current IVA pod.
        /// </summary>
        /// <param name="useKelvin">If true, the temperature is returned in Kelvin; if false, the temperature is in Celsius.</param>
        /// <returns>Current temperature of the interior of the current IVA pod in Kelvin or Celsius.</returns>
        public double SkinTemperature(bool useKelvin)
        {
            return fc.part.skinTemperature + ((useKelvin) ? 0.0 : KelvinToCelsius);
        }

        /// <summary>
        /// Deploys deployable radiators, or retracts retractable radiators.
        /// </summary>
        public void ToggleRadiator()
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
        }
        #endregion

        /// <summary>
        /// The Time section provides access to the various timers in MAS (and KSP).
        /// </summary>
        #region Time
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
        /// Returns the time until the vessel lands.  If MechJeb is available and the
        /// landing prediction module is enabled, MechJeb's results are used.
        /// 
        /// If the MechJeb is not available, the fallback estimator assumes the surface
        /// is at sea level and there is no atmosphere, so this result will be inaccurate 
        /// when the point of impact is at a higher altitude, or there is an atmosphere.
        /// 
        /// If the orbit does not intercept the
        /// surface, 0 is returned.
        /// </summary>
        /// <seealso>MechJeb</seealso>
        /// <returns>Time in seconds until landing; 0 for invalid times.</returns>
        public double TimeToLanding()
        {
            if (mjProxy.LandingComputerActive() > 0.0 && mjProxy.LandingTime() > 0.0)
            {
                return mjProxy.LandingTime();
            }
            else
            {
                return timeToImpact;
            }
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
        /// Variables that have not been assigned to a different category are
        /// dumped in this region until I figured out where to put them.
        /// </summary>
        #region Unassigned Region

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

        /// <summary>
        /// Returns `trueValue` when `condition` is true, otherwise returns `falseValue`.
        /// 
        /// `trueValue` and `falseValue` may be numbers or strings.  They do not *have* to
        /// both be numbers or both be strings.
        /// 
        /// `condition` must be a boolean value.  For example, `fc.GetActionGroup(0)` returns
        /// a number.  To make it a boolean, you would need to use `fc.GetActionGroup(0) == 1`.
        /// </summary>
        /// <param name="condition">The condition that selects the value.</param>
        /// <param name="trueValue">The value returned when `condition` is true.</param>
        /// <param name="falseValue">The value returned when `condition` is false.</param>
        /// <returns></returns>
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
