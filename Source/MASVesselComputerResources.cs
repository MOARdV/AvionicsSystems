/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016 MOARdV
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

namespace AvionicsSystems
{
    internal partial class MASVesselComputer : VesselModule
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

        private ResourceNameComparer resourceNameComparer = new ResourceNameComparer();

        internal struct ResourceData
        {
            internal string name;

            internal int id;
            internal float density;
            internal ResourceFlowMode flowMode;

            internal float currentQuantity;
            internal float maxQuantity;
            internal float previousQuantity; // for tracking delta
            internal float deltaPerSecond;

            internal float currentStage;
            internal float maxStage;
        }

        #region Resource Data Query
        /// <summary>
        /// Helper function: Find which resource has this name.
        /// </summary>
        /// <param name="resourceName">Internal name of the resource (eg, "ElectricCharge")</param>
        /// <returns>Index, or a negative number if not found</returns>
        private int GetResourceIndex(string resourceName)
        {
            // TODO: Cache the last queried index and skip the bsearch if the
            // dummyResource.name == resourceName?
            dummyResource.name = resourceName;
            return Array.BinarySearch<ResourceData>(resources, dummyResource, resourceNameComparer);
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
        /// Returns the current value of the nth resource found on the vessel,
        /// where the Nth resource is selected from the alphabetized list of
        /// resources.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceCurrent(int resourceId)
        {
            if (resourceId >= 0 && resourceId < resources.Length)
            {
                if (vesselActiveResource[resourceId] < int.MaxValue)
                {
                    return resources[vesselActiveResource[resourceId]].currentQuantity;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Return the current amount of the named resource, or zero if the
        /// resource does not exist.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        internal double ResourceCurrent(string resourceName)
        {
            int index = GetResourceIndex(resourceName);
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
        /// Returns the instantaneous change-per-second of the Nth resource, or
        /// zero if the index was invalid.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        internal double ResourceDelta(int resourceId)
        {
            if (resourceId >= 0 && resourceId < resources.Length)
            {
                if (vesselActiveResource[resourceId] < int.MaxValue)
                {
                    return resources[vesselActiveResource[resourceId]].deltaPerSecond;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the instantaneous change-per-second of the resource, or
        /// zero if the resource wasn't found.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        internal double ResourceDelta(string resourceName)
        {
            int index = GetResourceIndex(resourceName);
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
        /// Returns the density of the Nth resource, or zero if the index was invalid.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceDensity(int resourceId)
        {
            if (resourceId >= 0 && resourceId < resources.Length)
            {
                if (vesselActiveResource[resourceId] < int.MaxValue)
                {
                    return resources[vesselActiveResource[resourceId]].density;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the density of the named resource, or zero if it wasn't found.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        internal double ResourceDensity(string resourceName)
        {
            int index = GetResourceIndex(resourceName);
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
        /// Returns 1 if the resource id refers to a valid resource on the current
        /// vessel, 0 otherwise.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceExists(int resourceId)
        {
            if (resourceId >= 0 && resourceId < resources.Length && vesselActiveResource[resourceId] < int.MaxValue)
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns 1 if the named resource is found on this vessel, 0 otherwise.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        internal double ResourceExists(string resourceName)
        {
            int index = GetResourceIndex(resourceName);
            if (index >= 0)
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
        /// Returns the mass of the Nth resource
        /// in (units).
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceMass(int resourceId)
        {
            if (resourceId >= 0 && resourceId < resources.Length)
            {
                if (vesselActiveResource[resourceId] < int.MaxValue)
                {
                    return resources[vesselActiveResource[resourceId]].currentQuantity * resources[vesselActiveResource[resourceId]].density;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the mass of the current resource supply
        /// in (units).
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        internal double ResourceMass(string resourceName)
        {
            int index = GetResourceIndex(resourceName);
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
        /// Returns the maximum mass of the Nth resource in (units).
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceMassMax(int resourceId)
        {
            if (resourceId >= 0 && resourceId < resources.Length)
            {
                if (vesselActiveResource[resourceId] < int.MaxValue)
                {
                    return resources[vesselActiveResource[resourceId]].maxQuantity * resources[vesselActiveResource[resourceId]].density;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the maximum mass of the resource in (units).
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        internal double ResourceMassMax(string resourceName)
        {
            int index = GetResourceIndex(resourceName);
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
        /// Return the maximum capacity of the Nth resource, or zero if the resource
        /// doesn't exist.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceMax(int resourceId)
        {
            if (resourceId >= 0 && resourceId < resources.Length)
            {
                if (vesselActiveResource[resourceId] < int.MaxValue)
                {
                    return resources[vesselActiveResource[resourceId]].maxQuantity;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Return the maximum capacity of the resource, or zero if the resource
        /// doesn't exist.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        internal double ResourceMax(string resourceName)
        {
            int index = GetResourceIndex(resourceName);
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
        /// Returns the name of the Nth active resource, or an empty string if
        /// the resource index is invalid.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal string ResourceName(int resourceId)
        {
            if (resourceId >= 0 && resourceId < resources.Length)
            {
                if (vesselActiveResource[resourceId] < int.MaxValue)
                {
                    return resources[vesselActiveResource[resourceId]].name;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the amount of the Nth resource remaining as a percentage in the
        /// range [0, 1].
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourcePercent(int resourceId)
        {
            if (resourceId >= 0 && resourceId < resources.Length)
            {
                int index = vesselActiveResource[resourceId];
                if (index < int.MaxValue)
                {
                    return (resources[index].maxQuantity > 0.0) ? resources[index].currentQuantity / resources[index].maxQuantity : 0.0;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the amount of the resource remaining as a percentage in the
        /// range [0, 1].
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        internal double ResourcePercent(string resourceName)
        {
            int index = GetResourceIndex(resourceName);
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
        /// Returns the amount of the Nth resource remaining in the current stage.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceStageCurrent(int resourceId)
        {
            if (resourceId >= 0 && resourceId < resources.Length)
            {
                if (vesselActiveResource[resourceId] < int.MaxValue)
                {
                    return resources[vesselActiveResource[resourceId]].currentStage;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the amount of the resource remaining in the current stage.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        internal double ResourceStageCurrent(string resourceName)
        {
            int index = GetResourceIndex(resourceName);
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
        /// Returns the maximum amount of the Nth resource in the current stage.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        internal double ResourceStageMax(int resourceId)
        {
            if (resourceId >= 0 && resourceId < resources.Length)
            {
                if (vesselActiveResource[resourceId] < int.MaxValue)
                {
                    return resources[vesselActiveResource[resourceId]].maxStage;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Returns the maximum amount of the resource in the current stage.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        internal double ResourceStageMax(string resourceName)
        {
            int index = GetResourceIndex(resourceName);
            if (index >= 0 && index < resources.Length)
            {
                return resources[index].maxStage;
            }
            else
            {
                return 0.0;
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

                resources[index].id = thatResource.id;
                resources[index].density = thatResource.density;
                resources[index].flowMode = thatResource.resourceFlowMode;
                resources[index].currentQuantity = 0.0f;
                resources[index].maxQuantity = 0.0f;
                resources[index].previousQuantity = 0.0f;
                resources[index].deltaPerSecond = 0.0f;
                resources[index].currentStage = 0.0f;
                resources[index].maxStage = 0.0f;
                ++index;
            }

            // Alphabetize our list.
            // TODO: Should I sort on resource ID instead?  That would be
            // cheaper than a string search.
            Array.Sort(resources, resourceNameComparer);
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
            for (int i = resources.Length - 1; i >= 0; --i)
            {
                vesselActiveResource[i] = int.MaxValue;

                double amount, maxAmount;
                vessel.GetConnectedResourceTotals(resources[i].id, out amount, out maxAmount);

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
            }
        }

        /// <summary>
        /// End of FixedUpdate: update the resource data.
        /// </summary>
        private void ProcessResourceData()
        {
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
