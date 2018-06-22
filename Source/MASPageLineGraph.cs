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
    internal class MASPageLineGraph : IMASMonitorComponent
    {
        private GameObject graphObject;
        private GameObject borderObject;
        private Material graphMaterial;
        private Material borderMaterial;
        private LineRenderer lineRenderer;
        private LineRenderer borderRenderer;
        private Color borderColor = Color.white;
        private Color sourceColor = Color.white;
        private readonly float verticalSpan;
        private readonly float sampleRate;
        private readonly MASFlightComputer.Variable sourceValue;
        private readonly MASFlightComputer.Variable sourceRange1, sourceRange2;

        private int currentSample;
        private int maxSamples;
        private Vector3[] graphPoints;

        internal MASPageLineGraph(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
            : base(config, prop, comp)
        {
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
            string[] ranges = Utility.SplitVariableList(sourceRange);
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Incorrect number of values in 'sourceRange' in LINE_GRAPH " + name);
            }
            sourceRange1 = comp.GetVariable(ranges[0], prop);
            sourceRange2 = comp.GetVariable(ranges[1], prop);

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

            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            // Set up our display surface.
            if (borderWidth > 0.0f)
            {
                borderObject = new GameObject();
                borderObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name + "-border", (int)(-depth / MASMonitor.depthDelta));
                borderObject.layer = pageRoot.gameObject.layer;
                borderObject.transform.parent = pageRoot;
                borderObject.transform.position = pageRoot.position;
                borderObject.transform.Translate(monitor.screenSize.x * -0.5f + position.x, monitor.screenSize.y * 0.5f - position.y - size.y, depth);

                borderMaterial = new Material(MASLoader.shaders["MOARdV/Monitor"]);
                borderRenderer = borderObject.AddComponent<LineRenderer>();
                borderRenderer.useWorldSpace = false;
                borderRenderer.material = borderMaterial;
                borderRenderer.startColor = borderColor;
                borderRenderer.endColor = borderColor;
                borderRenderer.startWidth = borderWidth;
                borderRenderer.endWidth = borderWidth;
                borderRenderer.positionCount = 4;
                borderRenderer.loop = true;

                Color32 namedColor;
                if (comp.TryGetNamedColor(borderColorName, out namedColor))
                {
                    borderColor = namedColor;
                    lineRenderer.startColor = borderColor;
                    lineRenderer.endColor = borderColor;
                }
                else
                {
                    string[] startColors = Utility.SplitVariableList(borderColorName);
                    if (startColors.Length < 3 || startColors.Length > 4)
                    {
                        throw new ArgumentException("borderColor does not contain 3 or 4 values in LINE_GRAPH " + name);
                    }

                    variableRegistrar.RegisterNumericVariable(startColors[0], (double newValue) =>
                    {
                        borderColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        borderRenderer.startColor = borderColor;
                        borderRenderer.endColor = borderColor;
                    });

                    variableRegistrar.RegisterNumericVariable(startColors[1], (double newValue) =>
                    {
                        borderColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        borderRenderer.startColor = borderColor;
                        borderRenderer.endColor = borderColor;
                    });

                    variableRegistrar.RegisterNumericVariable(startColors[2], (double newValue) =>
                    {
                        borderColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        borderRenderer.startColor = borderColor;
                        borderRenderer.endColor = borderColor;
                    });

                    if (startColors.Length == 4)
                    {
                        variableRegistrar.RegisterNumericVariable(startColors[3], (double newValue) =>
                        {
                            borderColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            borderRenderer.startColor = borderColor;
                            borderRenderer.endColor = borderColor;
                        });
                    }
                }

                float halfWidth = borderWidth * 0.5f;
                Vector3[] borderPoints = new Vector3[]
                {
                    new Vector3(-halfWidth, -halfWidth, 0.0f),
                    new Vector3(size.x + halfWidth, -halfWidth, 0.0f),
                    new Vector3(size.x + halfWidth, size.y + halfWidth, 0.0f),
                    new Vector3(-halfWidth, size.y+halfWidth, 0.0f)
                };
                borderRenderer.SetPositions(borderPoints);
            }

            graphObject = new GameObject();
            graphObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
            graphObject.layer = pageRoot.gameObject.layer;
            graphObject.transform.parent = pageRoot;
            graphObject.transform.position = pageRoot.position;
            graphObject.transform.Translate(monitor.screenSize.x * -0.5f + position.x, monitor.screenSize.y * 0.5f - position.y - size.y, depth);
            // add renderer stuff
            graphMaterial = new Material(MASLoader.shaders["MOARdV/Monitor"]);
            lineRenderer = graphObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.material = graphMaterial;
            lineRenderer.startColor = sourceColor;
            lineRenderer.endColor = sourceColor;
            lineRenderer.startWidth = 2.5f;
            lineRenderer.endWidth = 2.5f;
            lineRenderer.positionCount = maxSamples;
            lineRenderer.loop = false;
            RenderPage(false);

            for (int i = 0; i < maxSamples; ++i)
            {
                graphPoints[i] = Vector3.zero;
            }

            string sourceColorName = string.Empty;
            if (config.TryGetValue("sourceColor", ref sourceColorName))
            {
                Color32 namedColor;
                if (comp.TryGetNamedColor(sourceColorName, out namedColor))
                {
                    sourceColor = namedColor;
                    lineRenderer.startColor = sourceColor;
                    lineRenderer.endColor = sourceColor;
                }
                else
                {
                    string[] sourceColors = Utility.SplitVariableList(sourceColorName);
                    if (sourceColors.Length < 3 || sourceColors.Length > 4)
                    {
                        throw new ArgumentException("sourceColor does not contain 3 or 4 values in LINE_GRAPH " + name);
                    }

                    variableRegistrar.RegisterNumericVariable(sourceColors[0], (double newValue) =>
                    {
                        sourceColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.startColor = sourceColor;
                        lineRenderer.endColor = sourceColor;
                    });

                    variableRegistrar.RegisterNumericVariable(sourceColors[1], (double newValue) =>
                    {
                        sourceColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.startColor = sourceColor;
                        lineRenderer.endColor = sourceColor;
                    });

                    variableRegistrar.RegisterNumericVariable(sourceColors[2], (double newValue) =>
                    {
                        sourceColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.startColor = sourceColor;
                        lineRenderer.endColor = sourceColor;
                    });

                    if (sourceColors.Length == 4)
                    {
                        variableRegistrar.RegisterNumericVariable(sourceColors[3], (double newValue) =>
                        {
                            sourceColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            lineRenderer.startColor = sourceColor;
                            lineRenderer.endColor = sourceColor;
                        });
                    }
                }
            }

            currentSample = 0;

            comp.StartCoroutine(SampleData());

            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the mesh if we're in variable mode
                if (borderObject != null)
                {
                    borderObject.SetActive(false);
                }
                graphObject.SetActive(false);
                variableRegistrar.RegisterNumericVariable(variableName, VariableCallback);
            }
            else
            {
                currentState = true;
                if (borderObject != null)
                {
                    borderObject.SetActive(true);
                }
                graphObject.SetActive(true);
            }
        }

        private WaitForSeconds waitToSample;

        /// <summary>
        /// Coroutine that samples data.
        /// </summary>
        /// <returns></returns>
        private IEnumerator SampleData()
        {
            waitToSample = new WaitForSeconds(sampleRate);
            while (lineRenderer != null)
            {
                float newSample = verticalSpan * Mathf.InverseLerp((float)sourceRange1.SafeValue(), (float)sourceRange2.SafeValue(), (float)sourceValue.SafeValue());
                newSample = Mathf.Round(newSample);

                if (currentSample < maxSamples)
                {
                    graphPoints[currentSample] = new Vector3(currentSample * sampleRate, newSample, 0.0f);
                    currentSample++;
                    lineRenderer.positionCount = currentSample;
                }
                else
                {
                    for (int i = 1; i < maxSamples; ++i)
                    {
                        graphPoints[i - 1].y = graphPoints[i].y;
                    }
                    graphPoints[maxSamples - 1] = new Vector3((maxSamples - 1) * sampleRate, newSample, 0.0f);
                }
                lineRenderer.SetPositions(graphPoints);

                yield return waitToSample;
            }
        }

        /// <summary>
        /// Handle a changed value
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue)
        {
            if (EvaluateVariable(newValue))
            {
                if (borderObject != null)
                {
                    borderObject.SetActive(currentState);
                }
                graphObject.SetActive(currentState);
            }
        }

        /// <summary>
        /// Called with `true` prior to the page rendering.  Called with
        /// `false` after the page completes rendering.
        /// </summary>
        /// <param name="enable">true indicates that the page is about to
        /// be rendered.  false indicates that the page has completed rendering.</param>
        public override void RenderPage(bool enable)
        {
            borderRenderer.enabled = enable;
            lineRenderer.enabled = enable;
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            lineRenderer = null;
            UnityEngine.Object.Destroy(graphMaterial);
            graphMaterial = null;
            UnityEngine.Object.Destroy(borderMaterial);
            borderMaterial = null;
            UnityEngine.Object.Destroy(graphObject);
            graphObject = null;
            UnityEngine.Object.Destroy(borderObject);
            borderObject = null;

            variableRegistrar.ReleaseResources(); ;
        }
    }
}
