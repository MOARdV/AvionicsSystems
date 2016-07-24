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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// LineGraph renderer.
    /// </summary>
    internal class MASPageLineGraph : IMASSubComponent
    {
        private string name = "(anonymous)";
        private GameObject graphObject;
        private GameObject borderObject;
        private Material graphMaterial;
        private Material borderMaterial;
        private MASFlightComputer comp;
        private LineRenderer lineRenderer;
        private readonly float verticalSpan;
        private readonly float sampleRate;
        private readonly MASFlightComputer.Variable sourceValue;
        private readonly MASFlightComputer.Variable sourceRange1, sourceRange2;
        private readonly string variableName;
        private MASFlightComputer.Variable range1, range2;
        private readonly bool rangeMode;
        private bool currentState;

        private int currentSample;
        private int maxSamples;
        private Vector3[] graphPoints;

        internal MASPageLineGraph(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            this.comp = comp;
            if (!config.TryGetValue("name", ref name))
            {
                name = "(anonymous)";
            }

            Vector2 position = Vector2.zero;
            if (!config.TryGetValue("position", ref position))
            {
                throw new ArgumentException("Unable to find 'position' in LINE_GRAPH " + name);
            }

            Vector2 size = Vector2.zero;
            if (!config.TryGetValue("size", ref size))
            {
                throw new ArgumentException("Unable to find 'size' in LINE_GRAPH " + name);
            }
            verticalSpan = size.y;

            if (!config.TryGetValue("sampleRate", ref sampleRate))
            {
                throw new ArgumentException("Unable to find 'sampleRate' in LINE_GRAPH " + name);
            }
            maxSamples = (int)(size.x / sampleRate);
            graphPoints = new Vector3[maxSamples];

            string sourceName = string.Empty;
            if (!config.TryGetValue("source", ref sourceName))
            {
                throw new ArgumentException("Unable to find 'source' in LINE_GRAPH " + name);
            }
            sourceValue = comp.GetVariable(sourceName, prop);

            string sourceRange = string.Empty;
            if (!config.TryGetValue("sourceRange", ref sourceRange))
            {
                throw new ArgumentException("Unable to find 'sourceRange' in LINE_GRAPH " + name);
            }
            string[] ranges = sourceRange.Split(',');
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Incorrect number of values in 'sourceRange' in LINE_GRAPH " + name);
            }
            sourceRange1 = comp.GetVariable(ranges[0], prop);
            sourceRange2 = comp.GetVariable(ranges[1], prop);

            string sourceColorString = string.Empty;
            if (!config.TryGetValue("sourceColor", ref sourceColorString))
            {
                throw new ArgumentException("Unable to find 'sourceColor' in LINE_GRAPH " + name);
            }
            Color sourceColor = Utility.ParseColor32(sourceColorString, comp);

            float borderWidth = 0.0f;
            if (!config.TryGetValue("borderWidth", ref borderWidth))
            {
                borderWidth = 0.0f;
            }
            else
            {
                borderWidth = Math.Max(1.0f, borderWidth);
            }
            string borderColorName = string.Empty;
            if (!config.TryGetValue("borderColor", ref borderColorName))
            {
                borderColorName = string.Empty;
            }
            if (string.IsNullOrEmpty(borderColorName) == (borderWidth > 0.0f))
            {
                throw new ArgumentException("Only one of 'borderColor' and 'borderWidth' are defined in LINE_GRAPH " + name);
            }

            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            string range = string.Empty;
            if (config.TryGetValue("range", ref range))
            {
                ranges = range.Split(',');
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in LINE_GRAPH " + name);
                }
                range1 = comp.GetVariable(ranges[0], prop);
                range2 = comp.GetVariable(ranges[1], prop);

                rangeMode = true;
            }
            else
            {
                rangeMode = false;
            }

            // Set up our display surface.
            if (borderWidth > 0.0f)
            {
                borderObject = new GameObject();
                borderObject.name = pageRoot.gameObject.name + "-MASPageLineGraphBorder-" + name + "-" + depth.ToString();
                borderObject.layer = pageRoot.gameObject.layer;
                borderObject.transform.parent = pageRoot;
                borderObject.transform.position = pageRoot.position;
                borderObject.transform.Translate(monitor.screenSize.x * -0.5f + position.x, monitor.screenSize.y * 0.5f - position.y - size.y, depth);

                Color borderColor = Utility.ParseColor32(borderColorName, comp);
                borderMaterial = new Material(Shader.Find("Particles/Additive"));
                lineRenderer = borderObject.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = false;
                lineRenderer.material = borderMaterial;
                lineRenderer.SetColors(borderColor, borderColor);
                lineRenderer.SetWidth(borderWidth, borderWidth);

                float halfWidth = borderWidth * 0.5f;
                Vector3[] borderPoints = new Vector3[]
                {
                    new Vector3(-halfWidth, halfWidth, 0.0f),
                    new Vector3(size.x + halfWidth, halfWidth, 0.0f),
                    new Vector3(size.x + halfWidth, size.y + halfWidth, 0.0f),
                    new Vector3(-halfWidth, size.y+halfWidth, 0.0f),
                    new Vector3(-halfWidth, halfWidth, 0.0f)
                };
                SetPositions(lineRenderer, borderPoints.Length, borderPoints);
            }

            graphObject = new GameObject();
            graphObject.name = pageRoot.gameObject.name + "-MASPageLineGraph-" + name + "-" + depth.ToString();
            graphObject.layer = pageRoot.gameObject.layer;
            graphObject.transform.parent = pageRoot;
            graphObject.transform.position = pageRoot.position;
            graphObject.transform.Translate(monitor.screenSize.x * -0.5f + position.x, monitor.screenSize.y * 0.5f - position.y - size.y, depth);
            // add renderer stuff
            //graphMaterial = new Material(MASLoader.shaders["MOARdV/TextMonitor"]);
            graphMaterial = new Material(Shader.Find("Particles/Additive"));
            lineRenderer = graphObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.material = graphMaterial;
            lineRenderer.SetColors(sourceColor, sourceColor);
            lineRenderer.SetWidth(2.5f, 2.5f);

            for (int i = 0; i < maxSamples; ++i)
            {
                graphPoints[i] = Vector3.zero;
            }

            currentSample = 0;

            // Urk... SetPositions isn't available in the KSP 1.1.3 version of Unity.  Will have to redo this later.
            //SetPositions(lineRenderer, currentSample, graphPoints);

            comp.StartCoroutine(SampleData());

            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the mesh if we're in variable mode
                graphObject.SetActive(false);
                comp.RegisterNumericVariable(variableName, prop, VariableCallback);
            }
            else
            {
                graphObject.SetActive(true);
            }
        }

        /// <summary>
        /// Temporary worker method to set positions for the LineRenderer.
        /// Unity docs say there's a SetPositions method for that class, but
        /// it is not in Unity 5.2.4f; hopefully KSP 1.2's new Unity flavor
        /// will include it.
        /// </summary>
        /// <param name="lineRenderer"></param>
        /// <param name="numVertices"></param>
        /// <param name="graphPoints"></param>
        private static void SetPositions(LineRenderer lineRenderer, int numVertices, Vector3[] graphPoints)
        {
            lineRenderer.SetVertexCount(numVertices);
            for (int i = 0; i < numVertices; ++i)
            {
                lineRenderer.SetPosition(i, graphPoints[i]);
            }
        }

        /// <summary>
        /// Coroutine that samples data.
        /// </summary>
        /// <returns></returns>
        private IEnumerator SampleData()
        {
            while (lineRenderer != null)
            {
                float newSample = verticalSpan * Mathf.InverseLerp((float)sourceRange1.SafeValue(), (float)sourceRange2.SafeValue(), (float)sourceValue.SafeValue());
                newSample = Mathf.Round(newSample);

                if (currentSample < maxSamples)
                {
                    graphPoints[currentSample] = new Vector3(currentSample * sampleRate, newSample, 0.0f);
                    currentSample++;
                }
                else
                {
                    for (int i = 1; i < maxSamples; ++i)
                    {
                        graphPoints[i - 1].y = graphPoints[i].y;
                    }
                    graphPoints[maxSamples - 1] = new Vector3((maxSamples - 1) * sampleRate, newSample, 0.0f);
                }
                SetPositions(lineRenderer, currentSample, graphPoints);

                yield return new WaitForSeconds(sampleRate);
            }
        }

        /// <summary>
        /// Handle a changed value
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue)
        {
            if (rangeMode)
            {
                newValue = (newValue.Between(range1.SafeValue(), range2.SafeValue())) ? 1.0 : 0.0;
            }

            bool newState = (newValue > 0.0);

            if (newState != currentState)
            {
                currentState = newState;
                graphObject.SetActive(currentState);
            }
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
            this.comp = null;
            lineRenderer = null;
            UnityEngine.Object.Destroy(graphMaterial);
            graphMaterial = null;
            UnityEngine.Object.Destroy(borderMaterial);
            borderMaterial = null;
            UnityEngine.Object.Destroy(graphObject);
            graphObject = null;
            UnityEngine.Object.Destroy(borderObject);
            borderObject = null;

            //comp.UnregisterNumericVariable(inputName, internalProp, InputCallback);
            if (!string.IsNullOrEmpty(variableName))
            {
                comp.UnregisterNumericVariable(variableName, internalProp, VariableCallback);
            }
        }
    }
}
