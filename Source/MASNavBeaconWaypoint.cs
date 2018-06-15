/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2018 MOARdV
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
    /// <summary>
    /// This is a little helper object that is used to add or remove MAS Navigation Beacons
    /// from the stock custom waypoints manager.  It only does something in Flight or the
    /// Tracking Station (the two scenes where ScenarioCustomWaypoints is instantiated).
    /// </summary>
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    class MASNavBeaconWaypoint : MonoBehaviour
    {
        /// <summary>
        /// Figure out if we're supposed to be doing something.
        /// </summary>
        public void Awake()
        {
            if (!(HighLogic.LoadedScene == GameScenes.FLIGHT || HighLogic.LoadedScene == GameScenes.TRACKSTATION))
            {
                // If we're not in the right scene, shut down.
                enabled = false;
            }
        }

        /// <summary>
        /// Wait for the custom waypoints to be ready so we can take care of initialization.
        /// </summary>
        public void FixedUpdate()
        {
            if (ScenarioCustomWaypoints.Instance != null)
            {
                InitializeNavAids();
                // We've done our job.  Shut down.
                enabled = false;
            }
        }

        /// <summary>
        /// This method is called when a vessel computer awakens since the waypoint manager doesn't
        /// appear to be initialized before the Flight scene.  This method ensures that all of the
        /// MAS NAVAIDS are registered in the waypoint manager.
        /// </summary>
        static internal void InitializeNavAids()
        {
            FinePrint.WaypointManager waypointManager = FinePrint.WaypointManager.Instance();
            List<FinePrint.Waypoint> knownWaypoints = (waypointManager) ? waypointManager.Waypoints : null;

            int numNavAids = MASLoader.navaids.Count;

            if (MASConfig.navigation.enableNavBeacons)
            {
                bool anyAdded = false;
                ConfigNode master = new ConfigNode("CUSTOM_WAYPOINTS");
                for (int i = 0; i < numNavAids; ++i)
                {
                    // Make sure all navigation beacons are updated and added to the waypoint manager.
                    if (MASLoader.navaids[i].maximumRange == -1.0)
                    {
                        MASLoader.navaids[i].UpdateHorizonDistance();
                    }

                    string waypointName = MASLoader.navaids[i].waypointName;

                    FinePrint.Waypoint wp = (knownWaypoints == null) ? null : knownWaypoints.Find(x => x.name == waypointName);
                    if (MASConfig.ResetWaypoints && wp != null)
                    {
                        ScenarioCustomWaypoints.RemoveWaypoint(wp);
                        wp = null;
                    }

                    if (wp == null)
                    {
                        FinePrint.Waypoint newwp = MASLoader.navaids[i].ToWaypoint(i);

                        // Note: this is round-about, but it appears to be the way to register
                        // waypoints to show up in Waypoint Manager.  If I simply add the
                        // waypoint directly using FinePrint.WaypointManager, it's present there, but
                        // not in the Waypoint Manager mod's GUI list.  So this is a simple
                        // way to get compatibility.

                        ConfigNode child = new ConfigNode("WAYPOINT");
                        child.AddValue("latitude", newwp.latitude);
                        child.AddValue("longitude", newwp.longitude);
                        child.AddValue("altitude", newwp.altitude);
                        child.AddValue("celestialName", newwp.celestialName);
                        child.AddValue("name", newwp.name);
                        child.AddValue("id", newwp.id);
                        child.AddValue("index", newwp.index);
                        child.AddValue("navigationId", newwp.navigationId.ToString());

                        master.AddNode(child);
                        anyAdded = true;
                        //FinePrint.WaypointManager.AddWaypoint(newwp);
                    }
                }
                if (anyAdded)
                {
                    ScenarioCustomWaypoints.Instance.OnLoad(master);
                }
            }
            else if (knownWaypoints != null && knownWaypoints.Count > 0)
            {
                for (int i = 0; i < numNavAids; ++i)
                {
                    // If nav beacons are disabled, remove them from the database
                    string waypointName = MASLoader.navaids[i].waypointName;
                    FinePrint.Waypoint wp = knownWaypoints.Find(x => x.name == waypointName);

                    if (wp != null)
                    {
                        ScenarioCustomWaypoints.RemoveWaypoint(wp);
                        knownWaypoints.Remove(wp);
                    }
                }
            }

            if (MASConfig.EnableCommNetWaypoints)
            {
                CelestialBody kerbin = Planetarium.fetch.Home;

                int index = 0;
                bool anyAdded = false;
                ConfigNode master = new ConfigNode("CUSTOM_WAYPOINTS");
                foreach (var keyValue in MASLoader.deepSpaceNetwork)
                {
                    string waypointName = keyValue.Key;
                    FinePrint.Waypoint wp = (knownWaypoints == null) ? null : knownWaypoints.Find(x => x.name == waypointName);
                    if (MASConfig.ResetWaypoints && wp != null)
                    {
                        ScenarioCustomWaypoints.RemoveWaypoint(wp);
                        wp = null;
                    }
                    ConfigNode child = new ConfigNode("WAYPOINT");
                    if (wp == null)
                    {
                        Guid g = Guid.NewGuid();

                        // Waypoint altitude keeps reporting a 0 when I query it.
                        // I've tried using altitude above datum (TerrainAltitude) and
                        // AGL (10 meters, here), and I keep seeing a 0 for altitude.
                        // It looks like altitude is not actually stored in ScenarioCustomWaypoints,
                        // so I may have to derive that value myself.
                        //double altitude = kerbin.TerrainAltitude(keyValue.Value.x, keyValue.Value.y, false);

                        child.AddValue("latitude", keyValue.Value.x);
                        child.AddValue("longitude", keyValue.Value.y);
                        child.AddValue("altitude", 10.0);
                        //child.AddValue("altitude", altitude); // This isn't working?
                        child.AddValue("celestialName", kerbin.name);
                        child.AddValue("name", waypointName);
                        child.AddValue("id", "vessel");
                        child.AddValue("index", ++index);
                        child.AddValue("navigationId", g.ToString());
                        anyAdded = true;
                    }

                    master.AddNode(child);
                }
                if (anyAdded)
                {
                    ScenarioCustomWaypoints.Instance.OnLoad(master);
                }
            }
            else if (knownWaypoints != null && knownWaypoints.Count > 0)
            {
                foreach (var keyValue in MASLoader.deepSpaceNetwork)
                {
                    string waypointName = keyValue.Key;
                    FinePrint.Waypoint wp = knownWaypoints.Find(x => x.name == waypointName);

                    if (wp != null)
                    {
                        ScenarioCustomWaypoints.RemoveWaypoint(wp);
                        knownWaypoints.Remove(wp);
                    }
                }
            }

            MASConfig.ResetWaypoints = false;
        }
    }
}
