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
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    internal partial class MASVesselComputer : MonoBehaviour
    {
        /// <summary>
        /// Array of all resources found on the vessel, for numeric indexing.
        /// </summary>
        private int[] vesselActiveResource = new int[0];

        /// <summary>
        /// Array of all known resources.
        /// </summary>
        private ResourceData[] resources = new ResourceData[0];

        /// <summary>
        /// Structure to track active propellant use.  Stores in units of kg, not KSP units.
        /// </summary>
        internal ResourceData enginePropellant = new ResourceData();

        /// <summary>
        /// A listing of which resource IDs have been flagged as active propellants.
        /// </summary>
        private List<int> enginePropellantIds = new List<int>();

        /// <summary>
        /// Structure to track active RCS use.  Stores in units of kg, not KSP units.
        /// </summary>
        internal ResourceData rcsPropellant = new ResourceData();

        /// <summary>
        /// A listing of which resource IDs have been flagged as RCS resources.
        /// </summary>
        private HashSet<int> rcsPropellantIds = new HashSet<int>();

        /// <summary>
        /// HashSet to track active resources.
        /// </summary>
        private HashSet<Part> activeResources = new HashSet<Part>();

        /// <summary>
        /// PartSet tracking all parts connected to active stages for the sake
        /// of tracking stage current and stage max resources.
        /// </summary>
        private PartSet partSet = null;

        /// <summary>
        /// Used for binary searches
        /// </summary>
        private ResourceData dummyResource;

        /// <summary>
        /// Total mass of all current resources, in tonnes.
        /// </summary>
        internal float totalResourceMass;

        private ResourceNameComparer resourceNameComparer = new ResourceNameComparer();

        internal struct ResourceData
        {
            internal string name;
            internal string displayName;

            internal int id;
            internal float density;
            internal ResourceFlowMode flowMode;

            internal float currentQuantity;
            internal float maxQuantity;
            internal float previousQuantity; // for tracking delta
            internal float deltaPerSecond;

            internal float currentStage;
            internal float maxStage;

            internal bool resourceLocked;
            internal List<PartResource> partResources;
        }

        #region Resource Data Query
        /// <summary>
        /// Helper function: Find which resource this id selects.
        /// </summary>
        /// <param name="resourceId">Either the internal name of the resource (eg, "ElectricCharge") or a number corresponding to the active resources array.</param>
        /// <returns>Index, or a negative number if not found</returns>
        internal int GetResourceIndex(object resourceId)
        {
            int index = -1;
            if (resourceId is string)
            {
                dummyResource.name = resourceId as string;
                index = Array.BinarySearch<ResourceData>(resources, dummyResource, resourceNameComparer);
            }
            else if (resourceId is double)
            {
                index = (int)(double)(resourceId);
                if (index > -1 && index < vesselActiveResource.Length && vesselActiveResource[index] < int.MaxValue)
                {
                    index = vesselActiveResource[index];
                }
                else
                {
                    index = -1;
                }
            }

            return index;
        }

        /// <summary>
        /// Find the ResourceData structure that corresponds to the given resource ID.
        /// </summary>
        /// <param name="rsrcId"></param>
        /// <returns></returns>
        private ResourceData GetResourceData(int rsrcId)
        {
            return Array.Find(resources, x => x.id == rsrcId);
        }

        /// <summary>
        /// Returns the number of resources flagged as current propellants
        /// </summary>
        /// <returns></returns>
        internal double PropellantStageCount()
        {
            return enginePropellantIds.Count;
        }

        /// <summary>
        /// Maps the engine propellant index to the vessel index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        internal double PropellantResourceId(int index)
        {
            if (index >= 0 && index < enginePropellantIds.Count)
            {
                int resourceIndex = Array.FindIndex(resources, x => x.id == enginePropellantIds[index]);
                if (resourceIndex >= 0)
                {
                    return Array.FindIndex(vesselActiveResource, x => x == resourceIndex);
                }
            }
            return -1.0;
        }

        /// <summary>
        /// Returns the indexed propellant display name or an empty string.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        internal string PropellantStageDisplayName(int index)
        {
            if (index >= 0 && index < enginePropellantIds.Count)
            {
                try
                {
                    ResourceData rd = GetResourceData(enginePropellantIds[index]);
                    return rd.displayName;
                }
                catch { }
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the indexed propellant name or an empty string.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        internal string PropellantStageName(int index)
        {
            if (index >= 0 && index < enginePropellantIds.Count)
            {
                try
                {
                    ResourceData rd = GetResourceData(enginePropellantIds[index]);
                    return rd.name;
                }
                catch { }
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the number of resources on the vessel.
        /// </summary>
        /// <returns></returns>
        internal double ResourceCount()
        {
            int arrayLength = vesselActiveResource.Length;
            for (int i = 0; i < arrayLength; ++i)
            {
                if (vesselActiveResource[i] == int.MaxValue)
                {
                    return (double)i;
                }
            }

            return (double)arrayLength;
        }

        /// <summary>
        /// Return the current amount of the named resource, or zero if the
        /// resource does not exist.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceCurrent(object resourceId)
        {
            int index = GetResourceIndex(resourceId);
            if (index >= 0 && index < resources.Length)
            {
                return resources[index].currentQuantity;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Internal fast-access query - bypasses repeated GetResourceIndex
        /// lookups by using the index fetched previously from GetResourceIndex
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        internal double ResourceCurrentDirect(int index)
        {
            if (index >= 0 && index < resources.Length)
            {
                return resources[index].currentQuantity;
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the instantaneous change-per-second of the resource, or
        /// zero if the resource wasn't found.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceDelta(object resourceId)
        {
            int index = GetResourceIndex(resourceId);
            if (index >= 0 && index < resources.Length)
            {
                return resources[index].deltaPerSecond;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Internal fast-access query - bypasses repeated GetResourceIndex
        /// lookups by using the index fetched previously from GetResourceIndex
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        internal double ResourceDeltaDirect(int index)
        {
            if (index >= 0 && index < resources.Length)
            {
                return resources[index].deltaPerSecond;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the density of the named resource, or zero if it wasn't found.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceDensity(object resourceId)
        {
            int index = GetResourceIndex(resourceId);
            if (index >= 0 && index < resources.Length)
            {
                return resources[index].density;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if the named resource is found on this vessel, 0 otherwise.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceExists(object resourceId)
        {
            int index = GetResourceIndex(resourceId);
            if (index >= 0 && index < resources.Length)
            {
                int resourceIndex = Array.BinarySearch<int>(vesselActiveResource, index);
                if (resourceIndex >= 0)
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns whether the identified resource is a propellant or not.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal bool ResourceIsPropellant(object resourceId)
        {
            int index = GetResourceIndex(resourceId);

            if (index >= 0 && index < resources.Length)
            {
                return (enginePropellantIds.FindIndex(x => x == resources[index].id) >= 0);
            }

            return false;
        }

        /// <summary>
        /// Returns the mass of the current resource supply
        /// in (units).
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceMass(object resourceId)
        {
            int index = GetResourceIndex(resourceId);
            if (index >= 0 && index < resources.Length)
            {
                return resources[index].currentQuantity * resources[index].density;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the maximum mass of the resource in (units).
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceMassMax(object resourceId)
        {
            int index = GetResourceIndex(resourceId);
            if (index >= 0 && index < resources.Length)
            {
                return resources[index].maxQuantity * resources[index].density;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Return the maximum capacity of the resource, or zero if the resource
        /// doesn't exist.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceMax(object resourceId)
        {
            int index = GetResourceIndex(resourceId);
            if (index >= 0 && index < resources.Length)
            {
                return resources[index].maxQuantity;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Internal fast-access query - bypasses repeated GetResourceIndex
        /// lookups by using the index fetched previously from GetResourceIndex
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        internal double ResourceMaxDirect(int index)
        {
            if (index >= 0 && index < resources.Length)
            {
                return resources[index].maxQuantity;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the display name of the Nth active resource, or an empty string if
        /// the resource index is invalid.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal string ResourceDisplayName(object resourceId)
        {
            int index = GetResourceIndex(resourceId);
            if (index >= 0 && index < resources.Length)
            {
                return resources[index].displayName;
            }

            return string.Empty;
        }

        internal float LockResource(object resourceId, bool lockResource)
        {
            int index = GetResourceIndex(resourceId);
            if (index >= 0 && index < resources.Length)
            {
                List<PartResource> pr = resources[index].partResources;
                int resourceQty = pr.Count;
                if (resourceQty > 0)
                {
                    for (int i = 0; i < resourceQty; ++i)
                    {
                        pr[i].flowState = lockResource;
                    }

                    return 1.0f;
                }
            }

            return 0.0f;
        }

        internal bool ResourceLocked(object resourceId)
        {
            int index = GetResourceIndex(resourceId);
            if (index >= 0 && index < resources.Length)
            {
                return resources[index].resourceLocked;
            }

            return false;
        }

        internal float UnlockAllResources()
        {
            float returnV = 0.0f;
            for (int rsrcIdx = resources.Length - 1; rsrcIdx >= 0; --rsrcIdx)
            {
                if (resources[rsrcIdx].resourceLocked)
                {
                    List<PartResource> pr = resources[rsrcIdx].partResources;
                    for (int i = pr.Count - 1; i >= 0; --i)
                    {
                        if (!pr[i].flowState)
                        {
                            returnV = 1.0f;
                            pr[i].flowState = true;
                        }
                    }
                }
            }

            return returnV;
        }

        internal float AnyResourceLocked()
        {
            for (int i = resources.Length - 1; i >= 0; --i)
            {
                if (resources[i].resourceLocked)
                {
                    return 1.0f;
                }
            }

            return 0.0f;
        }

        /// <summary>
        /// Returns the name of the Nth active resource, or an empty string if
        /// the resource index is invalid.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal string ResourceName(object resourceId)
        {
            int index = GetResourceIndex(resourceId);
            if (index >= 0 && index < resources.Length)
            {
                return resources[index].name;
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the amount of the resource remaining as a percentage in the
        /// range [0, 1].
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourcePercent(object resourceId)
        {
            int index = GetResourceIndex(resourceId);
            if (index >= 0 && index < resources.Length)
            {
                return (resources[index].maxQuantity > 0.0) ? resources[index].currentQuantity / resources[index].maxQuantity : 0.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Internal fast-access query - bypasses repeated GetResourceIndex
        /// lookups by using the index fetched previously from GetResourceIndex
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        internal double ResourcePercentDirect(int index)
        {
            if (index >= 0 && index < resources.Length)
            {
                return (resources[index].maxQuantity > 0.0) ? resources[index].currentQuantity / resources[index].maxQuantity : 0.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the amount of the resource remaining in the current stage.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceStageCurrent(object resourceId)
        {
            int index = GetResourceIndex(resourceId);
            if (index >= 0 && index < resources.Length)
            {
                return resources[index].currentStage;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the current mass of the resource remaining in the current stage.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceStageMass(object resourceId)
        {
            int index = GetResourceIndex(resourceId);
            if (index >= 0 && index < resources.Length)
            {
                return resources[index].currentStage * resources[index].density;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the maximum mass of the resource in the current stage.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceStageMassMax(object resourceId)
        {
            int index = GetResourceIndex(resourceId);
            if (index >= 0 && index < resources.Length)
            {
                return resources[index].maxStage * resources[index].density;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the maximum amount of the resource in the current stage.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceStageMax(object resourceId)
        {
            int index = GetResourceIndex(resourceId);
            if (index >= 0 && index < resources.Length)
            {
                return resources[index].maxStage;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Add the resourceID of a resource being used as a propellant in
        /// a ModuleEngine.
        /// </summary>
        /// <param name="propellantId"></param>
        private void MarkActiveEnginePropellant(int propellantId)
        {
            if (!enginePropellantIds.Contains(propellantId))
            {
                enginePropellantIds.Add(propellantId);
            }
        }

        /// <summary>
        /// Add the resourceID of a resource being used as a propellant in
        /// a ModuleRCS.
        /// </summary>
        /// <param name="propellantId"></param>
        private void MarkActiveRcsPropellant(int propellantId)
        {
            if (!rcsPropellantIds.Contains(propellantId))
            {
                rcsPropellantIds.Add(propellantId);
            }
        }
        #endregion

        #region Resource Data Management

        /// <summary>
        /// Startup initialization of vessel resource tracking.
        /// </summary>
        private void InitResourceData()
        {
            int resourceCount = PartResourceLibrary.Instance.resourceDefinitions.Count;

            resources = new ResourceData[resourceCount];
            vesselActiveResource = new int[resourceCount];

            int index = 0;
            foreach (var thatResource in PartResourceLibrary.Instance.resourceDefinitions)
            {
                vesselActiveResource[index] = int.MaxValue;

                resources[index].name = thatResource.name;
                resources[index].displayName = thatResource.displayName;

                resources[index].id = thatResource.id;
                resources[index].density = thatResource.density;
                resources[index].flowMode = thatResource.resourceFlowMode;
                resources[index].currentQuantity = 0.0f;
                resources[index].maxQuantity = 0.0f;
                resources[index].previousQuantity = 0.0f;
                resources[index].deltaPerSecond = 0.0f;
                resources[index].currentStage = 0.0f;
                resources[index].maxStage = 0.0f;
                resources[index].resourceLocked = false;
                resources[index].partResources = new List<PartResource>();
                ++index;
            }

            // Alphabetize our list.
            // TODO: Should I sort on resource ID instead?  That would be
            // cheaper than a string search.
            Array.Sort(resources, resourceNameComparer);

            enginePropellant.name = "Engine Propellant";
            enginePropellant.displayName = "Engine Propellant";
            enginePropellant.density = 0.0f;
            enginePropellant.currentQuantity = 0.0f;
            enginePropellant.maxQuantity = 0.0f;
            enginePropellant.previousQuantity = 0.0f;
            enginePropellant.deltaPerSecond = 0.0f;
            enginePropellant.currentStage = 0.0f;
            enginePropellant.maxStage = 0.0f;
            // Balance of fields are "don't care".

            rcsPropellant.name = "RCS Propellant";
            rcsPropellant.displayName = "RCS Propellant";
            rcsPropellant.density = 0.0f;
            rcsPropellant.currentQuantity = 0.0f;
            rcsPropellant.maxQuantity = 0.0f;
            rcsPropellant.previousQuantity = 0.0f;
            rcsPropellant.deltaPerSecond = 0.0f;
            rcsPropellant.currentStage = 0.0f;
            rcsPropellant.maxStage = 0.0f;
            // Balance of fields are "don't care".
        }

        /// <summary>
        ///  Prepare to re-initialize the PartResource lists.
        /// </summary>
        private void InitRebuildPartResources()
        {
            for (int i = resources.Length - 1; i >= 0; --i)
            {
                resources[i].partResources.Clear();
            }
        }

        /// <summary>
        /// Add the resources to our tracked vessel-wide resource database
        /// </summary>
        /// <param name="resourceList">The part resource list to process.</param>
        private void UpdateResourceList(PartResourceList resourceList)
        {
            if (resourceList != null && resourceList.Count > 0)
            {
                for (int i = resourceList.Count - 1; i >= 0; --i)
                {
                    var pr = resourceList[i];
                    GetResourceData(pr.info.id).partResources.Add(pr);
                }
            }
        }

        /// <summary>
        /// Is this flow mode one that can move between stages?
        /// </summary>
        /// <param name="flowMode"></param>
        /// <returns></returns>
        private static bool IsFreeFlow(ResourceFlowMode flowMode)
        {
            return (flowMode == ResourceFlowMode.ALL_VESSEL || flowMode == ResourceFlowMode.STAGE_PRIORITY_FLOW);
        }

        /// <summary>
        /// Start of FixedUpdate: set everything up for the iteration of all
        /// the parts.
        /// </summary>
        private void PrepareResourceData()
        {
            totalResourceMass = 0.0f;

            for (int i = resources.Length - 1; i >= 0; --i)
            {
                vesselActiveResource[i] = int.MaxValue;

                double amount, maxAmount;
                vessel.GetConnectedResourceTotals(resources[i].id, out amount, out maxAmount);

                totalResourceMass += (float)amount * resources[i].density;

                resources[i].currentQuantity = (float)amount;
                resources[i].maxQuantity = (float)maxAmount;
                if (IsFreeFlow(resources[i].flowMode))
                {
                    resources[i].currentStage = (float)amount;
                    resources[i].maxStage = (float)maxAmount;
                }
                else
                {
                    resources[i].currentStage = 0.0f;
                    resources[i].maxStage = 0.0f;
                }
                resources[i].deltaPerSecond = 0.0f;
                if (maxAmount > 0.0)
                {
                    vesselActiveResource[i] = i;
                }

                int numPartResources = resources[i].partResources.Count;
                if (numPartResources > 0)
                {
                    resources[i].resourceLocked = false;
                    for (int rsrcIdx = 0; rsrcIdx < numPartResources; ++rsrcIdx)
                    {
                        if (resources[i].partResources[rsrcIdx].flowState == false)
                        {
                            resources[i].resourceLocked = true;
                            break;
                        }
                    }
                }
                else
                {
                    resources[i].resourceLocked = false;
                }
            }

            enginePropellantIds.Clear();
            rcsPropellantIds.Clear();
        }

        /// <summary>
        /// End of FixedUpdate: update the resource data.
        /// </summary>
        private void ProcessResourceData()
        {
            enginePropellant.currentStage = 0.0f;
            enginePropellant.maxStage = 0.0f;
            enginePropellant.currentQuantity = 0.0f;
            enginePropellant.maxQuantity = 0.0f;
            enginePropellant.density = 0.0f;

            rcsPropellant.currentStage = 0.0f;
            rcsPropellant.maxStage = 0.0f;
            rcsPropellant.currentQuantity = 0.0f;
            rcsPropellant.maxQuantity = 0.0f;
            rcsPropellant.density = 0.0f;

            float timeDelta = 1.0f / TimeWarp.fixedDeltaTime;
            for (int i = resources.Length - 1; i >= 0; --i)
            {
                if (resources[i].maxQuantity > 0.0f)
                {
                    // if maxStage > 0, then this resource has a flow rule that allows it to flow between
                    // stages, so we don't need to update maxStage and currentStage now.
                    // TODO: Does this manage blocked resource transfers (like over decouplers)?
                    // Maybe, instead of / in addition to marking engines, I should collect *all* parts
                    // on the currently active stage.
                    if (resources[i].maxStage == 0.0)
                    {
                        double amount, maxAmount;
                        partSet.GetConnectedResourceTotals(resources[i].id, out amount, out maxAmount, true);

                        resources[i].maxStage = (float)maxAmount;
                        resources[i].currentStage = (float)amount;
                    }

                    if (resources[i].previousQuantity > 0.0f)
                    {
                        resources[i].deltaPerSecond = timeDelta * (resources[i].previousQuantity - resources[i].currentQuantity);
                    }
                    else
                    {
                        resources[i].deltaPerSecond = 0.0f;
                    }

                    resources[i].previousQuantity = resources[i].currentQuantity;
                }

                if (enginePropellantIds.Contains(resources[i].id))
                {
                    enginePropellant.currentStage += resources[i].currentStage;
                    enginePropellant.maxStage += resources[i].maxStage;
                    enginePropellant.currentQuantity += resources[i].currentQuantity;
                    enginePropellant.maxQuantity += resources[i].maxQuantity;
                    enginePropellant.density += resources[i].currentStage * resources[i].density;
                }

                if (rcsPropellantIds.Contains(resources[i].id))
                {
                    rcsPropellant.currentStage += resources[i].currentStage;
                    rcsPropellant.maxStage += resources[i].maxStage;
                    rcsPropellant.currentQuantity += resources[i].currentQuantity;
                    rcsPropellant.maxQuantity += resources[i].maxQuantity;
                    rcsPropellant.density += resources[i].currentStage * resources[i].density;
                }
            }

            if (enginePropellant.previousQuantity > 0.0f)
            {
                enginePropellant.deltaPerSecond = timeDelta * (enginePropellant.previousQuantity - enginePropellant.currentQuantity);
            }
            else
            {
                enginePropellant.deltaPerSecond = 0.0f;
            }
            enginePropellant.previousQuantity = enginePropellant.currentQuantity;
            if (enginePropellant.currentStage > 0.0f)
            {
                enginePropellant.density /= enginePropellant.currentStage;
            }

            if (rcsPropellant.previousQuantity > 0.0f)
            {
                rcsPropellant.deltaPerSecond = timeDelta * (rcsPropellant.previousQuantity - rcsPropellant.currentQuantity);
            }
            else
            {
                rcsPropellant.deltaPerSecond = 0.0f;
            }
            rcsPropellant.previousQuantity = rcsPropellant.currentQuantity;
            if (rcsPropellant.currentStage > 0.0f)
            {
                rcsPropellant.density /= rcsPropellant.currentStage;
            }

            // sort the array of installed indices.
            Array.Sort<int>(this.vesselActiveResource);
        }

        /// <summary>
        /// All done with resource tracking.
        /// </summary>
        private void TeardownResourceData()
        {
            // TODO: May not need this at all.
        }
        #endregion

        /// <summary>
        /// Helper function to sort/find resources by alpha order of the name.
        /// </summary>
        private class ResourceNameComparer : IComparer<ResourceData>
        {
            public int Compare(ResourceData a, ResourceData b)
            {
                return string.Compare(a.name, b.name);
            }
        }
    }
}
