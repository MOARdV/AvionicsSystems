//#define USE_OLD_SOLVER
/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2022 MOARdV
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

namespace AvionicsSystems
{
    internal class ApproachSolver
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
#if USE_OLD_SOLVER
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
#endif

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

        /// <summary>
        /// Find the first orbit / orbit patch that is valid at the specified validUT.
        /// </summary>
        /// <param name="startingOrbit"></param>
        /// <param name="validUT"></param>
        /// <returns></returns>
#if !USE_OLD_SOLVER
        private static Orbit FindOrbit(Orbit startingOrbit, double validUT)
        {
            if ((startingOrbit.StartUT <= validUT && startingOrbit.EndUT >= validUT) || startingOrbit.patchEndTransition == Orbit.PatchTransitionType.FINAL)
            {
                return startingOrbit;
            }

            Orbit o = startingOrbit;
            while (o.patchEndTransition != Orbit.PatchTransitionType.FINAL)
            {
                if (o.nextPatch == null)
                {
                    // Not sure this is actually feasible.
                    return o;
                }
                o = o.nextPatch;

                if ((o.StartUT <= validUT && o.EndUT >= validUT) || o.patchEndTransition == Orbit.PatchTransitionType.FINAL)
                {
                    return o;
                }
            }

            Utility.LogStaticError("... Wait!  Exception because o.nextPath {0} and endTransition is {1}",
                (o.nextPatch != null) ? "!null" : "null", o.patchEndTransition);

            throw new ArgumentNullException("FindOrbit failed... no valid orbits existed during validUT");
        }
#endif

        /// <summary>
        /// Reset the "I'm ready" flag.
        /// </summary>
        internal void ResetComputation()
        {
            resultsReady = false;
        }

        internal bool resultsReady { get; private set; }
        internal double targetClosestDistance { get; private set; }
        internal double targetClosestUT { get; private set; }
        internal double targetClosestSpeed { get; private set; }

        /// <summary>
        /// Find the closest approach to a given body.
        /// 
        /// We do this by looking for every orbit patch that lists the targetBody
        /// as its referenceBody, and then looking for the lowest periapsis amongst
        /// those patches.  We short-circuit the search at the first patch whose
        /// Pe is below the datum (or sea-level).
        /// 
        /// If no patches orbit the target body, we fall back to the conventional
        /// orbital segments search.
        /// </summary>
        /// <param name="vesselOrbit">The starting orbit (either the vessel.orbit, or the first patch of a maneuver).</param>
        /// <param name="targetBody">The body we are intercepting.</param>
        internal void SolveBodyIntercept(Orbit vesselOrbit, CelestialBody targetBody)
        {
            targetClosestDistance = double.MaxValue;

            double targetRadius = targetBody.Radius;
            bool resultsFound = false;

            if (vesselOrbit.referenceBody == targetBody)
            {
                targetClosestDistance = vesselOrbit.PeR;
                targetClosestUT = vesselOrbit.StartUT + vesselOrbit.timeToPe;
                targetClosestSpeed = vesselOrbit.getOrbitalSpeedAt(targetClosestUT);

                if (targetClosestDistance < targetRadius)
                {
                    resultsReady = true;
                    return; // Early return: the first segment we tested impacts the surface.
                }

                resultsFound = true;
            }

            Orbit checkorbit = vesselOrbit;

            while (checkorbit.nextPatch != null && checkorbit.patchEndTransition != Orbit.PatchTransitionType.FINAL)
            {
                checkorbit = checkorbit.nextPatch;
                if (checkorbit.referenceBody == targetBody)
                {
                    if (checkorbit.PeR < targetClosestDistance)
                    {
                        targetClosestDistance = checkorbit.PeR;
                        targetClosestUT = checkorbit.StartUT + checkorbit.timeToPe;
                        targetClosestSpeed = checkorbit.getOrbitalSpeedAt(targetClosestUT);

                        if (checkorbit.PeR < targetRadius)
                        {
                            resultsReady = true;
                            return; // Early return: the first segment we tested impacts the surface.
                        }

                        resultsFound = true;
                    }
                }
            }

            if (!resultsFound)
            {
                // None of the orbits orbit the target body.  Fall back to the standard
                // solver.
                SolveOrbitIntercept(vesselOrbit, targetBody.orbit);
            }
        }

        /// <summary>
        /// Find the closest approach between two objects in orbit(s).
        /// </summary>
        /// <param name="vesselOrbit"></param>
        /// <param name="targetOrbit"></param>
        internal void SolveOrbitIntercept(Orbit vesselOrbit, Orbit targetOrbit)
        {
#if USE_OLD_SOLVER
            try
            {
                double now = Planetarium.GetUniversalTime();

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
                    Vector3d relativeVelocity = targetOrbit.getOrbitalVelocityAtUT(targetClosestTime) - startOrbit.getOrbitalVelocityAtUT(targetClosestTime);
                    this.targetClosestSpeed = relativeVelocity.magnitude;
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

                    Vector3d relativeVelocity = targetOrbit.getOrbitalVelocityAtUT(this.targetClosestUT) - startOrbit.getOrbitalVelocityAtUT(this.targetClosestUT);
                    this.targetClosestSpeed = relativeVelocity.magnitude;

                    //this.targetClosestDistance = closestDistance;
                    //this.targetClosestUT = closestTime;
                }
                this.resultsReady = true;
            }
            catch (Exception e)
            {
                Utility.LogInfo("ApproachSolver threw {0}", e);
            }
#else
            // Set up initial conditions
            Orbit vessel = vesselOrbit;
            Orbit target1;
            Orbit target2;

            double now;
            double then;

            double closestDistance = float.MaxValue;
            double closestUT = 0.0;

            while (vessel.patchEndTransition != Orbit.PatchTransitionType.FINAL)
            {
                now = vessel.StartUT;
                then = vessel.EndUT;
                
                if (double.IsInfinity(then))
                {
                    // If we get bogus values for 'then', it means we're probably not solving this -
                    // maybe the orbit intercepts another body.
                    targetClosestUT = vesselOrbit.StartUT;
                    targetClosestDistance = (vesselOrbit.getPositionAtUT(targetClosestUT) - targetOrbit.getPositionAtUT(targetClosestUT)).magnitude;
                    targetClosestSpeed = (vesselOrbit.GetFrameVelAtUT(vesselOrbit.StartUT) - targetOrbit.GetFrameVelAtUT(vesselOrbit.StartUT)).magnitude;

                    return;
                }

                target1 = FindOrbit(targetOrbit, vessel.StartUT);
                target2 = FindOrbit(targetOrbit, vessel.EndUT);

                //Utility.LogMessage(this, "vessel : {0:0} to {1:0}, transitions = {3} / {2}", vessel.StartUT, vessel.EndUT, vessel.patchEndTransition, vessel.patchStartTransition);
                //Utility.LogMessage(this, "target1: {0:0} to {1:0}, transitions = {3} / {2}", target1.StartUT, target1.EndUT, target1.patchEndTransition, target1.patchStartTransition);
                //Utility.LogMessage(this, "target2: {0:0} to {1:0}, transitions = {3} / {2}", target2.StartUT, target2.EndUT, target2.patchEndTransition, target2.patchStartTransition);

                while (target1 != target2 && !Double.IsInfinity(vessel.EndUT))
                {

                    FindClosest(vessel, target1, Math.Max(now, target1.StartUT), target1.EndUT, 0, ref closestDistance, ref closestUT);

                    target1 = target1.nextPatch;
                }

                if (!Double.IsInfinity(then))
                {
                    FindClosest(vessel, target1, now, then, 0, ref closestDistance, ref closestUT);
                }


                vessel = vessel.nextPatch;
            }

            // Final transition.
            now = vessel.StartUT;
            then = vessel.EndUT;
            if (double.IsInfinity(then))
            {
                // If we get bogus values for 'then', it means we're probably not solving this -
                // maybe the orbit intercepts another body.
                targetClosestUT = vesselOrbit.StartUT;
                targetClosestDistance = (vesselOrbit.getPositionAtUT(targetClosestUT) - targetOrbit.getPositionAtUT(targetClosestUT)).magnitude;
                targetClosestSpeed = (vesselOrbit.GetFrameVelAtUT(vesselOrbit.StartUT) - targetOrbit.GetFrameVelAtUT(vesselOrbit.StartUT)).magnitude;

                return;
            }

            // Don't bother searching ahead with hyperbolic orbits
            int orbitsToCheck = (vessel.eccentricity < 1.0) ? NumOrbitsLookAhead : 1;

            for (int i = 0; i < orbitsToCheck; ++i)
            {
                target1 = FindOrbit(targetOrbit, now);
                target2 = FindOrbit(target1, then);

                while (target1 != target2 && !Double.IsInfinity(vessel.EndUT))
                {

                    FindClosest(vessel, target1, Math.Max(now, target1.StartUT), target1.EndUT, 0, ref closestDistance, ref closestUT);
 
                    target1 = target1.nextPatch;
                }

                if (!Double.IsInfinity(then) && !Double.IsInfinity(now))
                {
                   FindClosest(vessel, target1, now, then, 0, ref closestDistance, ref closestUT);
                }
                now = then;
                then += vessel.period;
            }

            resultsReady = true;

            if (closestUT > 0.0)
            {
                targetClosestUT = closestUT;
                targetClosestDistance = closestDistance;
                targetClosestSpeed = (vesselOrbit.GetFrameVelAtUT(closestUT) - targetOrbit.GetFrameVelAtUT(closestUT)).magnitude;
            }
            else
            {
                // Did not solve.  Use "now" as closest approach.
                targetClosestUT = vesselOrbit.StartUT;
                targetClosestDistance = (vesselOrbit.getPositionAtUT(targetClosestUT) - targetOrbit.getPositionAtUT(targetClosestUT)).magnitude;
                targetClosestSpeed = (vesselOrbit.GetFrameVelAtUT(vesselOrbit.StartUT) - targetOrbit.GetFrameVelAtUT(vesselOrbit.StartUT)).magnitude;
            }
#endif
        }
    }
}
