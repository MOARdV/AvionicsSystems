/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2017-2018 MOARdV
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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    class MASPageGroundTrack : IMASMonitorComponent
    {
        const int numObjects = 6;

        private string name = "anonymous";

        private GameObject[] lineOrigin = new GameObject[numObjects];
        private Material[] lineMaterial = new Material[numObjects];
        private LineRenderer[] lineRenderer = new LineRenderer[numObjects];

        // this is messy...
        private Vector3[] vesselVertex1;
        private Vector3[] vesselVertex2;
        private Vector3[] targetVertex1;
        private Vector3[] targetVertex2;
        private Vector3[] maneuverVertex1;
        private Vector3[] maneuverVertex2;
        private Vector3d[] positions;
        private Color vesselColor;
        private Color targetColor;
        private Color maneuverColor;

        private VariableRegistrar variableRegistrar;
        private readonly int vertexCount;
        private readonly Vector2 size;
        private bool currentState;
        private bool pageActive = false;
        private readonly bool rangeMode;
        private MASFlightComputer.Variable range1, range2;
        private bool coroutineEnabled = true;
        private bool updateVessel = false;
        private bool updateTarget = false;
        private bool validTarget = false;
        private bool updateManeuver = false;
        private bool validManeuver = false;
        private float startLongitude = -180.0f;
        private float startLongitudeNormalized;
        private MASFlightComputer comp;

        internal MASPageGroundTrack(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            variableRegistrar = new VariableRegistrar(comp, prop);
            this.comp = comp;

            if (!config.TryGetValue("name", ref name))
            {
                name = "anonymous";
            }

            Vector2 position = Vector2.zero;
            if (!config.TryGetValue("position", ref position))
            {
                throw new ArgumentException("Unable to find 'position' in GROUND_TRACK " + name);
            }

            if (!config.TryGetValue("vertexCount", ref vertexCount))
            {
                throw new ArgumentException("Unable to find 'vertexCount' in GROUND_TRACK " + name);
            }
            if (vertexCount < 2)
            {
                throw new ArgumentException("'vertexCount' needs to be at least 2 in GROUND_TRACK " + name);
            }
            vesselVertex1 = new Vector3[vertexCount];
            vesselVertex2 = new Vector3[vertexCount];
            targetVertex1 = new Vector3[vertexCount];
            targetVertex2 = new Vector3[vertexCount];
            maneuverVertex1 = new Vector3[vertexCount];
            maneuverVertex2 = new Vector3[vertexCount];
            positions = new Vector3d[vertexCount];

            float width = 0.0f;
            if (!config.TryGetValue("size", ref width))
            {
                throw new ArgumentException("Unable to find 'size' in GROUND_TRACK " + name);
            }
            size.x = width; size.y = width * 0.5f;

            float lineWidth = 1.0f;
            if (!config.TryGetValue("lineWidth", ref lineWidth))
            {
                throw new ArgumentException("Unable to find 'lineWidth' in GROUND_TRACK " + name);
            }

            for (int i = 0; i < numObjects; ++i)
            {
                lineOrigin[i] = new GameObject();
                lineOrigin[i].transform.parent = pageRoot;
                lineOrigin[i].transform.position = pageRoot.position;
                lineOrigin[i].transform.Translate(monitor.screenSize.x * -0.5f + position.x, monitor.screenSize.y * 0.5f - position.y, depth);
                lineOrigin[i].name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name + "-" + i.ToString(), (int)(-depth / MASMonitor.depthDelta));
                lineOrigin[i].layer = pageRoot.gameObject.layer;

                lineMaterial[i] = new Material(MASLoader.shaders["MOARdV/Monitor"]);
                lineRenderer[i] = lineOrigin[i].AddComponent<LineRenderer>();
                lineRenderer[i].useWorldSpace = false;
                lineRenderer[i].material = lineMaterial[i];
                lineRenderer[i].startWidth = lineWidth;
                lineRenderer[i].endWidth = lineWidth;
                lineRenderer[i].positionCount = vertexCount;
                lineRenderer[i].enabled = false;
            }
            lineRenderer[0].SetPositions(vesselVertex1);
            lineRenderer[1].SetPositions(vesselVertex2);
            lineRenderer[2].SetPositions(targetVertex1);
            lineRenderer[3].SetPositions(targetVertex2);
            lineRenderer[4].SetPositions(maneuverVertex1);
            lineRenderer[5].SetPositions(maneuverVertex2);

            string vesselColorString = string.Empty;
            if (config.TryGetValue("vesselColor", ref vesselColorString))
            {
                Color32 color;
                if (comp.TryGetNamedColor(vesselColorString, out color))
                {
                    vesselColor = color;
                    lineRenderer[0].startColor = vesselColor;
                    lineRenderer[0].endColor = vesselColor;
                    lineRenderer[1].startColor = vesselColor;
                    lineRenderer[1].endColor = vesselColor;
                }
                else
                {
                    string[] vesselColors = Utility.SplitVariableList(vesselColorString);
                    if (vesselColors.Length < 3 || vesselColors.Length > 4)
                    {
                        throw new ArgumentException("vesselColor does not contain 3 or 4 values in GROUND_TRACK " + name);
                    }

                    variableRegistrar.RegisterNumericVariable(vesselColors[0], (double newValue) =>
                    {
                        vesselColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer[0].startColor = vesselColor;
                        lineRenderer[0].endColor = vesselColor;
                        lineRenderer[1].startColor = vesselColor;
                        lineRenderer[1].endColor = vesselColor;
                    });
                    variableRegistrar.RegisterNumericVariable(vesselColors[1], (double newValue) =>
                    {
                        vesselColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer[0].startColor = vesselColor;
                        lineRenderer[0].endColor = vesselColor;
                        lineRenderer[1].startColor = vesselColor;
                        lineRenderer[1].endColor = vesselColor;
                    });
                    variableRegistrar.RegisterNumericVariable(vesselColors[2], (double newValue) =>
                    {
                        vesselColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer[0].startColor = vesselColor;
                        lineRenderer[0].endColor = vesselColor;
                        lineRenderer[1].startColor = vesselColor;
                        lineRenderer[1].endColor = vesselColor;
                    });

                    if (vesselColors.Length == 4)
                    {
                        variableRegistrar.RegisterNumericVariable(vesselColors[3], (double newValue) =>
                        {
                            vesselColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            lineRenderer[0].startColor = vesselColor;
                            lineRenderer[0].endColor = vesselColor;
                            lineRenderer[1].startColor = vesselColor;
                            lineRenderer[1].endColor = vesselColor;
                        });
                    }
                }

                updateVessel = true;
            }
            else
            {
                lineOrigin[0].SetActive(false);
                lineOrigin[1].SetActive(false);
                updateVessel = false;
            }

            string maneuverColorString = string.Empty;
            if (config.TryGetValue("maneuverColor", ref maneuverColorString))
            {
                Color32 color;
                if (comp.TryGetNamedColor(maneuverColorString, out color))
                {
                    maneuverColor = color;
                    lineRenderer[4].startColor = maneuverColor;
                    lineRenderer[4].endColor = maneuverColor;
                    lineRenderer[5].startColor = maneuverColor;
                    lineRenderer[5].endColor = maneuverColor;
                }
                else
                {
                    string[] maneuverColors = Utility.SplitVariableList(maneuverColorString);
                    if (maneuverColors.Length < 3 || maneuverColors.Length > 4)
                    {
                        throw new ArgumentException("maneuverColor does not contain 3 or 4 values in GROUND_TRACK " + name);
                    }

                    variableRegistrar.RegisterNumericVariable(maneuverColors[0], (double newValue) =>
                    {
                        maneuverColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer[4].startColor = maneuverColor;
                        lineRenderer[4].endColor = maneuverColor;
                        lineRenderer[5].startColor = maneuverColor;
                        lineRenderer[5].endColor = maneuverColor;
                    });
                    variableRegistrar.RegisterNumericVariable(maneuverColors[1], (double newValue) =>
                    {
                        maneuverColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer[4].startColor = maneuverColor;
                        lineRenderer[4].endColor = maneuverColor;
                        lineRenderer[5].startColor = maneuverColor;
                        lineRenderer[5].endColor = maneuverColor;
                    });
                    variableRegistrar.RegisterNumericVariable(maneuverColors[2], (double newValue) =>
                    {
                        maneuverColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer[4].startColor = maneuverColor;
                        lineRenderer[4].endColor = maneuverColor;
                        lineRenderer[5].startColor = maneuverColor;
                        lineRenderer[5].endColor = maneuverColor;
                    });

                    if (maneuverColors.Length == 4)
                    {
                        variableRegistrar.RegisterNumericVariable(maneuverColors[3], (double newValue) =>
                        {
                            maneuverColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            lineRenderer[4].startColor = maneuverColor;
                            lineRenderer[4].endColor = maneuverColor;
                            lineRenderer[5].startColor = maneuverColor;
                            lineRenderer[5].endColor = maneuverColor;
                        });
                    }
                }

                updateManeuver = true;
            }
            else
            {
                lineOrigin[4].SetActive(false);
                lineOrigin[5].SetActive(false);
                updateManeuver = false;
            }

            string targetColorString = string.Empty;
            if (config.TryGetValue("targetColor", ref targetColorString))
            {
                Color32 color;
                if (comp.TryGetNamedColor(targetColorString, out color))
                {
                    targetColor = color;
                    lineRenderer[2].startColor = targetColor;
                    lineRenderer[2].endColor = targetColor;
                    lineRenderer[3].startColor = targetColor;
                    lineRenderer[3].endColor = targetColor;
                }
                else
                {
                    string[] targetColors = Utility.SplitVariableList(targetColorString);
                    if (targetColors.Length < 3 || targetColors.Length > 4)
                    {
                        throw new ArgumentException("targetColor does not contain 3 or 4 values in GROUND_TRACK " + name);
                    }

                    variableRegistrar.RegisterNumericVariable(targetColors[0], (double newValue) =>
                    {
                        targetColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer[2].startColor = targetColor;
                        lineRenderer[2].endColor = targetColor;
                        lineRenderer[3].startColor = targetColor;
                        lineRenderer[3].endColor = targetColor;
                    });
                    variableRegistrar.RegisterNumericVariable(targetColors[1], (double newValue) =>
                    {
                        targetColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer[2].startColor = targetColor;
                        lineRenderer[2].endColor = targetColor;
                        lineRenderer[3].startColor = targetColor;
                        lineRenderer[3].endColor = targetColor;
                    });
                    variableRegistrar.RegisterNumericVariable(targetColors[2], (double newValue) =>
                    {
                        targetColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer[2].startColor = targetColor;
                        lineRenderer[2].endColor = targetColor;
                        lineRenderer[3].startColor = targetColor;
                        lineRenderer[3].endColor = targetColor;
                    });

                    if (targetColors.Length == 4)
                    {
                        variableRegistrar.RegisterNumericVariable(targetColors[3], (double newValue) =>
                        {
                            targetColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            lineRenderer[2].startColor = targetColor;
                            lineRenderer[2].endColor = targetColor;
                            lineRenderer[3].startColor = targetColor;
                            lineRenderer[3].endColor = targetColor;
                        });
                    }
                }

                updateTarget = true;
            }
            else
            {
                lineOrigin[2].SetActive(false);
                lineOrigin[3].SetActive(false);
                updateTarget = false;
            }

            string startLongitudeString = string.Empty;
            if (!config.TryGetValue("startLongitude", ref startLongitudeString))
            {
                startLongitude = -180.0f;
                startLongitudeNormalized = startLongitude / 360.0f;
            }
            else
            {
                variableRegistrar.RegisterNumericVariable(startLongitudeString, (double newValue) =>
                {
                    float longitude = (float)Utility.NormalizeLongitude(newValue);
                    if (!Mathf.Approximately(longitude, startLongitude))
                    {
                        startLongitude = longitude;
                        startLongitudeNormalized = startLongitude / 360.0f;
                    }
                });
            }

            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                string range = string.Empty;
                if (config.TryGetValue("range", ref range))
                {
                    string[] ranges = Utility.SplitVariableList(range);
                    if (ranges.Length != 2)
                    {
                        throw new ArgumentException("Incorrect number of values in 'range' in GROUND_TRACK " + name);
                    }
                    range1 = comp.GetVariable(ranges[0], prop);
                    range2 = comp.GetVariable(ranges[1], prop);

                    rangeMode = true;
                }
                else
                {
                    rangeMode = false;
                }

                // Disable the mesh if we're in variable mode
                SetAllActive(false);
                variableRegistrar.RegisterNumericVariable(variableName, (double newValue) =>
                {
                    if (rangeMode)
                    {
                        newValue = (newValue.Between(range1.SafeValue(), range2.SafeValue())) ? 1.0 : 0.0;
                    }

                    bool newState = (newValue > 0.0);

                    if (newState != currentState)
                    {
                        currentState = newState;
                        SetAllActive(currentState);
                    }
                });
            }
            else
            {
                currentState = true;
                SetAllActive(true);
            }

            comp.StartCoroutine(LineUpdateCoroutine());
        }

        /// <summary>
        /// Update the positions array to contain one orbit (or the portion of
        /// the orbit prior to lithobraking).
        /// </summary>
        /// <param name="orbit"></param>
        void updateOrbitPositions(Orbit orbit)
        {
            double dT = orbit.period / (double)(vertexCount - 1);
            double T = orbit.StartUT;
            double netDT = orbit.StartUT - Planetarium.GetUniversalTime();
            if (orbit.inclination > 90.0 || orbit.inclination < -90.0)
            {
                // I want the positions to be in ascending longitudinal order in the array, if possible.
                T += orbit.period;
                dT = -dT;
                netDT += orbit.period;
            }
            double rotationPerSecond = (orbit.referenceBody.rotates) ? (360.0 / orbit.referenceBody.rotationPeriod) : 0.0;
            bool lithobrake = false;

            for (int i = 0; i < vertexCount; ++i)
            {
                if (lithobrake)
                {
                    positions[i] = positions[i - 1];
                }
                else
                {
                    Vector3d pos = orbit.getPositionAtUT(T);
                    // The following does not account for planetary rotation...
                    orbit.referenceBody.GetLatLonAlt(pos, out positions[i].x, out positions[i].y, out positions[i].z);
                    // ... so account for planetary rotation.  Although this won't handle
                    // retrograde rotation, unless the period is negative.
                    positions[i].y = Utility.NormalizeLongitude(positions[i].y - rotationPerSecond * netDT);

                    if (i > 0)
                    {
                        if (positions[i].z < 0.0)
                        {
                            lithobrake = true;
                            // Approximate the point of impact
                            float iLerp = Mathf.InverseLerp((float)positions[i - 1].z, (float)positions[i].z, 0.0f);
                            positions[i].x = Mathf.Lerp((float)positions[i - 1].x, (float)positions[i].x, iLerp);
                            positions[i].y = Mathf.Lerp((float)positions[i - 1].y, (float)positions[i].y, iLerp);
                            positions[i].z = 0.0f;
                        }
                    }

                    T += dT;
                    netDT += dT;
                }
            }

            // We now have the lat/lon of each sample of the orbit.
        }

        /// <summary>
        /// Convert the given lat/lon positions into screen coordinates in the
        /// two vertex arrays supplied.
        /// </summary>
        /// <param name="vert1">Primary vertex array</param>
        /// <param name="vert2">Seconary (wrap-around) vertex array</param>
        private void convertPositions(Vector3[] vert1, Vector3[] vert2)
        {
            try
            {
                float offset = 0.0f;
                int transition = -1;
                float transitionLatitude = 0.0f;
                for (int i = 0; i < vertexCount; ++i)
                {
                    // Remaps latitude to the correct y offset.
                    vert1[i].y = Utility.Remap(positions[i].x, 90.0, -90.0, 0.0, -size.y);

                    vert1[i].x = size.x * ((float)(positions[i].y) / 360.0f - startLongitudeNormalized) + offset;

                    if (i == 0)
                    {
                        if (vert1[0].x < 0.0f)
                        {
                            offset = size.x;
                            vert1[0].x += offset;
                        }
                        else if (vert1[0].x >= size.x)
                        {
                            offset = -size.x;
                            vert1[0].x += offset;
                        }
                    }
                    else if ((vert1[i - 1].x - vert1[i].x) > size.x * 0.5f)
                    {
                        vert1[i].x += size.x;
                    }

                    if (transition == -1)
                    {
                        if (vert1[i].x >= size.x)
                        {
                            if (i == 0)
                            {
                                // Try to trap Invalid index exceptions.  This seems to be the only
                                // condition where it could happen.
                                Utility.LogErrorMessage(this, "vert1[0].x overflowed: {0:0} >= {1:0}",
                                    vert1[i].x, size.x);
                                Utility.LogMessage(this, " ({0:0.000} - {1:0.000}) = {2:0.000}", (float)(positions[i].y) / 360.0f, startLongitudeNormalized, (float)(positions[i].y) / 360.0f - startLongitudeNormalized);
                            }
                            transition = i;
                            vert2[i].x = vert1[i].x - size.x;
                            float iLerpLat = Mathf.InverseLerp(vert1[i - 1].x, vert1[i].x, size.x);
                            transitionLatitude = Mathf.Lerp(vert1[i - 1].y, vert1[i].y, iLerpLat);
                            vert2[i].y = vert1[i].y;
                            vert1[i].y = transitionLatitude;

                            // Squish the previous wrap-around vertices to
                            // the transition latitude.
                            for (int j = 0; j < i; ++j)
                            {
                                vert2[j].y = transitionLatitude;
                            }
                        }
                        else
                        {
                            vert2[i].x = 0.0f;
                            vert2[i].y = 0.0f;
                        }
                    }
                    else
                    {
                        vert2[i].x = vert1[i].x - size.x;
                        vert2[i].y = vert1[i].y;

                        vert1[i].y = transitionLatitude;
                    }
                }

                if (transition >= 0)
                {
                    for (int i = transition; i < vertexCount; ++i)
                    {
                        vert1[i].x = size.x;
                    }
                }
            }
            catch (Exception e)
            {
                Utility.LogErrorMessage(this, "convertPositions threw an exception:");
                Utility.LogErrorMessage(this, "{0}", e);
            }
        }

        /// <summary>
        /// Coroutine to defer line updates so they're all processed in one location,
        /// since they may be expensive.
        /// </summary>
        /// <returns></returns>
        private IEnumerator LineUpdateCoroutine()
        {
            while (coroutineEnabled)
            {
                if (currentState && pageActive)
                {
                    if (updateVessel)
                    {
                        updateOrbitPositions(comp.vessel.orbit);
                        convertPositions(vesselVertex1, vesselVertex2);

                        lineRenderer[0].SetPositions(vesselVertex1);
                        lineRenderer[1].SetPositions(vesselVertex2);
                    }

                    if (updateTarget)
                    {
                        if ((comp.vc.targetType == MASVesselComputer.TargetType.Vessel || comp.vc.targetType == MASVesselComputer.TargetType.DockingPort) && comp.vc.targetOrbit != null && comp.vc.targetOrbit.referenceBody == comp.vessel.mainBody)
                        {
                            updateOrbitPositions(comp.vc.targetOrbit);
                            convertPositions(targetVertex1, targetVertex2);

                            lineRenderer[2].SetPositions(targetVertex1);
                            lineRenderer[3].SetPositions(targetVertex2);
                            validTarget = true;
                        }
                        else
                        {
                            validTarget = false;
                        }
                    }

                    if (updateManeuver)
                    {
                        if (comp.vc.nodeOrbit != null)
                        {
                            updateOrbitPositions(comp.vc.nodeOrbit);
                            convertPositions(maneuverVertex1, maneuverVertex2);

                            lineRenderer[4].SetPositions(maneuverVertex1);
                            lineRenderer[5].SetPositions(maneuverVertex2);
                            validManeuver = true;
                        }
                        else
                        {
                            validManeuver = false;
                        }
                    }
                }

                yield return MASConfig.waitForFixedUpdate;
            }
        }

        /// <summary>
        /// Enable / disable rendering of everything
        /// </summary>
        /// <param name="newState"></param>
        private void SetAllActive(bool newState)
        {
            if (updateVessel)
            {
                lineOrigin[0].SetActive(newState);
                lineOrigin[1].SetActive(newState);
            }
            if (updateTarget)
            {
                lineOrigin[2].SetActive(newState);
                lineOrigin[3].SetActive(newState);
            }
            if (updateManeuver)
            {
                lineOrigin[4].SetActive(newState);
                lineOrigin[5].SetActive(newState);
            }
        }

        /// <summary>
        /// Called with `true` prior to the page rendering.  Called with
        /// `false` after the page completes rendering.
        /// </summary>
        /// <param name="enable">true indicates that the page is about to
        /// be rendered.  false indicates that the page has completed rendering.</param>
        public void RenderPage(bool enable)
        {
            if (updateVessel)
            {
                lineRenderer[0].enabled = enable;
                lineRenderer[1].enabled = enable;
            }
            if (updateTarget)
            {
                lineRenderer[2].enabled = enable && validTarget;
                lineRenderer[3].enabled = enable && validTarget;
            }
            if (updateManeuver)
            {
                lineRenderer[4].enabled = enable && validManeuver;
                lineRenderer[5].enabled = enable && validManeuver;
            }
        }

        /// <summary>
        /// Called with `true` when the page is active on the monitor, called with
        /// `false` when the page is no longer active.
        /// </summary>
        /// <param name="enable">true when the page is actively displayed, false when the page
        /// is no longer displayed.</param>
        public void SetPageActive(bool enable)
        {
            pageActive = enable;
        }

        /// <summary>
        /// Handle a softkey event.
        /// </summary>
        /// <param name="keyId">The numeric ID of the key to handle.</param>
        /// <returns>true if the component handled the key, false otherwise.</returns>
        public bool HandleSoftkey(int keyId)
        {
            return false;
        }

        /// <summary>
        ///  Return the name of the action.
        /// </summary>
        /// <returns></returns>
        public string Name()
        {
            return name;
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            coroutineEnabled = false;
            updateVessel = false;
            updateTarget = false;
            updateManeuver = false;
            variableRegistrar.ReleaseResources(comp, internalProp);

            for (int i = lineOrigin.Length - 1; i >= 0; --i)
            {
                lineRenderer[i] = null;
                UnityEngine.Object.Destroy(lineMaterial[i]);
                lineMaterial[i] = null;
                UnityEngine.Object.Destroy(lineOrigin[i]);
                lineOrigin[i] = null;
            }

            this.comp = null;
        }
    }
}
