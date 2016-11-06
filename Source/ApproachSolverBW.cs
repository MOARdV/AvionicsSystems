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
        /// <param name="sourceOrbit"></param>
        /// <param name="targetOrbit"></param>
        /// <param name="startUT"></param>
        /// <param name="endUT"></param>
        /// <param name="recursionDepth"></param>
        /// <param name="targetClosestDistance"></param>
        /// <param name="targetClosestUT"></param>
        static private void OneStep(Orbit sourceOrbit, Orbit targetOrbit, double startUT, double endUT, int recursionDepth, ref double targetClosestDistance, ref double targetClosestUT)
        {
            if (recursionDepth > MaxRecursions)
            {
                return;
            }

            double deltaT = (endUT - startUT) / (double)NumSubdivisions;

            double closestDistSq = targetClosestDistance * targetClosestDistance;
            for (double t = startUT; t <= endUT; t += deltaT)
            {
                Vector3d vesselPos = sourceOrbit.getPositionAtUT(t);
                Vector3d targetPos = targetOrbit.getPositionAtUT(t);

                double distSq = (vesselPos - targetPos).sqrMagnitude;
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    targetClosestUT = t;
                }
            }

            targetClosestDistance = Math.Sqrt(closestDistSq);

            if (deltaT < 0.5)
            {
                // If our timesteps are less than a half second, I think
                // this is an accurate enough estimate.
                return;
            }

            OneStep(sourceOrbit, targetOrbit, Math.Max(targetClosestUT - deltaT, startUT), Math.Min(targetClosestUT + deltaT, endUT), recursionDepth + 1, ref targetClosestDistance, ref targetClosestUT);
        }

        /// <summary>
        /// Compare the two orbits to see if they're fairly close to one another.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private static bool OrbitsSimilar(Orbit a, Orbit b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            if (a.referenceBody != b.referenceBody)
            {
                return false;
            }

            if (Math.Abs(a.inclination - b.inclination) > 0.01)
            {
                //Utility.LogMessage(a, "inclination different");
                return false;
            }
            if (Math.Abs(a.eccentricity - b.eccentricity) > 0.01)
            {
                //Utility.LogMessage(a, "eccentricity different");
                return false;
            }
            if (Math.Abs(a.LAN - b.LAN) > 0.01)
            {
                //Utility.LogMessage(a, "LAN different");
                return false;
            }
            if (Math.Abs(a.argumentOfPeriapsis - b.argumentOfPeriapsis) > 0.01)
            {
                //Utility.LogMessage(a, "argumentOfPeriapsis different");
                return false;
            }
            if (Math.Abs(a.semiMajorAxis - b.semiMajorAxis) > 100.0)
            {
                //Utility.LogMessage(a, "SMA different");
                return false;
            }
            // Orbit.meanAnomalyAtEpoch changes every update - presumably, the
            // epoch is shifting over time to avoid numeric precision problems?
            // So, instead of comparing mA at the epoch, we compute the current
            // mean anomaly.
            double ma1 = a.meanAnomalyAtEpoch + a.meanMotion * (Planetarium.GetUniversalTime() - a.epoch);
            double ma2 = b.meanAnomalyAtEpoch + b.meanMotion * (Planetarium.GetUniversalTime() - b.epoch);
            if (Math.Abs(ma1 - ma2) > 0.01)
            {
                //Utility.LogMessage(a, "meanAnomalyAtEpoch has shifted ... mA 1 = {0:0.00}, mA 2 = {1:0.00}", ma1, ma2);

                return false;
            }

            return true;
        }

        private void bw_FindClosestApproach(object sender, DoWorkEventArgs e)
        {
            if (startOrbit.eccentricity >= 1.0 || targetOrbit.eccentricity >= 1.0)
            {
                // Hyperbolic orbit is involved.  Don't check multiple orbits.
                // This is a fairly quick search.

                double startTime = resultsStartTime;
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
                OneStep(startOrbit, targetOrbit, startTime, endTime, 0, ref targetClosestDistance, ref targetClosestTime);

                this.targetClosestDistance = targetClosestDistance;
                this.targetClosestUT = targetClosestTime;
                this.resultsValidUntil = targetClosestTime;
            }
            else
            {
                //Utility.LogMessage(this, "... Closed orbits involved - using multi-orbit scan");

                double targetClosestDistance = float.MaxValue;
                double targetClosestTime = float.MaxValue;

                double startTime = resultsStartTime;
                double endTime = startTime + startOrbit.period;

                for (int i = 0; i < NumOrbitsLookAhead; ++i)
                {
                    OneStep(startOrbit, targetOrbit, startTime, endTime, 0, ref targetClosestDistance, ref targetClosestTime);
                    startTime += startOrbit.period;
                    endTime += startOrbit.period;
                }

                this.targetClosestDistance = targetClosestDistance;
                this.targetClosestUT = targetClosestTime;
                this.resultsValidUntil = Math.Min(resultsStartTime + startOrbit.period, targetClosestTime);
                //Utility.LogMessage(this, "close = {0:0} @ {1:0}, with resultsValid until {2:0}",
                //    targetClosestDistance, targetClosestTime - Planetarium.GetUniversalTime(), this.resultsValidUntil - Planetarium.GetUniversalTime());
            }
        }

        /// <summary>
        /// Work completed ... signal that results are valid.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bw_TaskComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            resultsComputing = false;
            resultsReady = true;
        }

        internal void ResetComputation()
        {
            resultsReady = false;
        }

        internal bool resultsComputing { get; private set; }
        internal bool resultsReady { get; private set; }
        internal double targetClosestDistance { get; private set; }
        internal double targetClosestUT { get; private set; }

        private double resultsValidUntil;
        private double resultsStartTime;
        private Orbit startOrbit;
        private Orbit targetOrbit;
        private BackgroundWorker worker;

        /// <summary>
        /// Iterate over our closest approach estimator.  Someday, I may figure out how to spin this into a thread
        /// instead, so it's less costly.
        /// </summary>
        /// <param name="vesselOrbit"></param>
        /// <param name="targetOrbit"></param>
        /// <param name="now"></param>
        /// <param name="targetClosestDistance"></param>
        /// <param name="targetClosestUT"></param>
        internal void IterateApproachSolver(Orbit vesselOrbit, Orbit targetOrbit, double now)
        {
            // Ignore requests while we're busy.
            if (resultsComputing)
            {
                //Utility.LogMessage(this, "IterateApproachSolver(): I'm busy.  Check back later.");
                return;
            }

            Orbit startOrbit = SelectClosestOrbit(vesselOrbit, targetOrbit.referenceBody);

            bool needToRecompute = false;
            if (!OrbitsSimilar(targetOrbit, this.targetOrbit))
            {
                //Utility.LogMessage(this, "IterateApproachSolver(): Target Orbit has changed - going to work.");
                needToRecompute = true;
            }
            if (!OrbitsSimilar(startOrbit, this.startOrbit))
            {
                //Utility.LogMessage(this, "IterateApproachSolver(): Vessel Orbit has changed - going to work.");
                needToRecompute = true;
            }
            if (now > resultsValidUntil)
            {
                //Utility.LogMessage(this, "IterateApproachSolver(): Results have expired - going to work.");
                needToRecompute = true;
            }

            if (needToRecompute)
            {
                //this.resultsReady = false;
                this.resultsComputing = true;

                //this.targetClosestDistance = float.MaxValue;
                //this.targetClosestUT = float.MaxValue;

                this.targetOrbit = new Orbit(targetOrbit);
                this.targetOrbit.Init(); // needed?

                this.startOrbit = new Orbit(startOrbit);
                this.startOrbit.Init(); // needed?

                resultsStartTime = now;

                worker = new BackgroundWorker();
                worker.DoWork += bw_FindClosestApproach;
                worker.RunWorkerCompleted += bw_TaskComplete;

                worker.RunWorkerAsync();
            }
            else
            {
                worker = null;
            }
        }
    }
}
