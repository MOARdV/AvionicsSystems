//#define SHORTCIRCUIT_SOLVER
/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016 - 2018 MOARdV
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
using System.ComponentModel;
using System.Text;

namespace AvionicsSystems
{
    internal class ApproachSolverBW
    {
        /// <summary>
        /// How many subdivisions do we want to use per iteration to find the
        /// local minimum?
        /// </summary>
        private static readonly int NumSubdivisions = 64;

        /// <summary>
        /// How many recursions do we want to allow before we punt?
        /// </summary>
        private static readonly int MaxRecursions = 16;

        /// <summary>
        /// How many orbits into the future do we want to look for the closest
        /// approach?
        /// </summary>
        private static readonly int NumOrbitsLookAhead = 6;

        /// <summary>
        /// Iterate through the next several patches on the orbit to find the
        /// first one that shares the same reference body as the supplied
        /// parameter.
        /// </summary>
        /// <param name="startOrbit">The orbit we're starting from.</param>
        /// <param name="referenceBody">The CelestialBody that we're heading for.</param>
        /// <returns></returns>
        static private Orbit SelectClosestOrbit(Orbit startOrbit, CelestialBody referenceBody)
        {
            Orbit checkorbit = startOrbit;
            int orbitcount = 0;

            while (checkorbit.nextPatch != null && checkorbit.patchEndTransition != Orbit.PatchTransitionType.FINAL && orbitcount < 3)
            {
                checkorbit = checkorbit.nextPatch;
                orbitcount++;
                if (checkorbit.referenceBody == referenceBody)
                {
                    return checkorbit;
                }
            }

            return startOrbit;
        }

        /// <summary>
        /// Iterate across one step of the search.
        /// </summary>
        static private void FindClosest(Orbit sourceOrbit, Orbit targetOrbit, double startUT, double endUT, int recursionDepth, ref double closestDistance, ref double closestUT)
        {
            double deltaT = (endUT - startUT) / (double)NumSubdivisions;
            double closestDistSq = closestDistance * closestDistance;
            double closestTime = (startUT + endUT) * 0.5;
            bool foundClosest = false;
            for (double t = startUT; t <= endUT; t += deltaT)
            {
                Vector3d vesselPos = sourceOrbit.getPositionAtUT(t);
                Vector3d targetPos = targetOrbit.getPositionAtUT(t);

                double distSq = (vesselPos - targetPos).sqrMagnitude;
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestTime = t;
                    foundClosest = true;
                }
            }

            if (foundClosest)
            {
                closestDistance = Math.Sqrt(closestDistSq);
                closestUT = closestTime;

                if (deltaT < 1.0)
                {
                    // If our timesteps are this small, I think
                    // this is an accurate enough estimate.
                    return;
                }
                if (recursionDepth == MaxRecursions)
                {
                    // Hit recursion limit.  Done.
                    return;
                }

                FindClosest(sourceOrbit, targetOrbit, Math.Max(closestTime - deltaT, startUT), Math.Min(closestTime + deltaT, endUT), recursionDepth + 1, ref closestDistance, ref closestUT);
            }

            // Did not improve on the previous iteration.  Don't recurse.
        }

        internal void ResetComputation()
        {
            resultsReady = false;
        }

        internal bool resultsReady { get; private set; }
        internal double targetClosestDistance { get; private set; }
        internal double targetClosestUT { get; private set; }

        internal void SolveApproach(Orbit vesselOrbit, CelestialBody targetBody, double now)
        {
            Orbit startOrbit = SelectClosestOrbit(vesselOrbit, targetBody);
            if (startOrbit.referenceBody == targetBody)
            {
                targetClosestDistance = startOrbit.PeR;
                targetClosestUT = startOrbit.timeToPe + startOrbit.StartUT;
                this.resultsReady = true;
            }
            else
            {
                SolveApproach(vesselOrbit, targetBody.orbit, now);
            }
        }

        internal void SolveApproach(Orbit vesselOrbit, Orbit targetOrbit, double now)
        {
#if SHORTCIRCUIT_SOLVER
            this.targetClosestUT = 1.0;
            this.targetClosestUT = 1.0;
            this.resultsReady = true;
#else

            try
            {
                Orbit startOrbit = SelectClosestOrbit(vesselOrbit, targetOrbit.referenceBody);

                if (startOrbit.eccentricity >= 1.0 || targetOrbit.eccentricity >= 1.0)
                {
                    // Hyperbolic orbit is involved.  Don't check multiple orbits.
                    // This is a fairly quick search.

                    double startTime = now;
                    startTime = Math.Max(startTime, startOrbit.StartUT);
                    startTime = Math.Max(startTime, targetOrbit.StartUT);

                    double endTime;
                    if (startOrbit.eccentricity >= 1.0)
                    {
                        endTime = startOrbit.EndUT;
                    }
                    else
                    {
                        endTime = startTime + startOrbit.period * 6.0;
                    }
                    if (targetOrbit.eccentricity >= 1.0)
                    {
                        endTime = Math.Min(endTime, targetOrbit.EndUT);
                    }
                    else
                    {
                        endTime = Math.Min(endTime, targetOrbit.period * 6.0);
                    }

                    double targetClosestDistance = float.MaxValue;
                    double targetClosestTime = float.MaxValue;
                    FindClosest(startOrbit, targetOrbit, startTime, endTime, 0, ref targetClosestDistance, ref targetClosestTime);

                    this.targetClosestDistance = targetClosestDistance;
                    this.targetClosestUT = targetClosestTime;
                }
                else
                {
                    double startTime = now;
                    double endTime = startTime + startOrbit.period;
                    targetClosestDistance = float.MaxValue;

                    for (int i = 0; i < NumOrbitsLookAhead; ++i)
                    {
                        double closestDistance = float.MaxValue;
                        double closestTime = float.MaxValue;

                        FindClosest(startOrbit, targetOrbit, startTime, endTime, 0, ref closestDistance, ref closestTime);
                        startTime += startOrbit.period;
                        endTime += startOrbit.period;

                        if (closestDistance < this.targetClosestDistance)
                        {
                            this.targetClosestDistance = closestDistance;
                            this.targetClosestUT = closestTime;
                        }
                    }

                    //this.targetClosestDistance = closestDistance;
                    //this.targetClosestUT = closestTime;
                }
                this.resultsReady = true;
            }
            catch (Exception e)
            {
                Utility.LogInfo("ApproachSolver threw {0}", e);
            }
#endif
        }
    }
}
