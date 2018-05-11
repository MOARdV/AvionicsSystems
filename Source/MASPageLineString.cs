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
    /// <summary>
    /// Line String renderer.
    /// </summary>
    class MASPageLineString : IMASMonitorComponent
    {
        private string name = "anonymous";

        private Color startColor = Color.white, endColor = Color.white;

        private float startWidth = 1.0f, endWidth = 1.0f;

        private Vector3[] vertices;
        private GameObject lineOrigin;
        private Material lineMaterial;
        private LineRenderer lineRenderer;

        private MASFlightComputer.Variable range1, range2;
        private readonly bool rangeMode;
        private bool currentState;

        private bool usesTexture;
        private float inverseTextureWidth = 1.0f;

        private VariableRegistrar variableRegistrar;

        internal MASPageLineString(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            variableRegistrar = new VariableRegistrar(comp, prop);

            if (!config.TryGetValue("name", ref name))
            {
                name = "anonymous";
            }

            string startColorString = string.Empty;
            if (!config.TryGetValue("startColor", ref startColorString))
            {
                throw new ArgumentException("Unable to find 'startColor' in LINE_STRING " + name);
            }
            string endColorString = string.Empty;
            if (!config.TryGetValue("endColor", ref endColorString))
            {
                endColorString = string.Empty;
            }

            string startWidthString = string.Empty;
            if (!config.TryGetValue("startWidth", ref startWidthString))
            {
                throw new ArgumentException("Unable to find 'startWidth' in LINE_STRING " + name);
            }
            string endWidthString = string.Empty;
            if (!config.TryGetValue("endWidth", ref endWidthString))
            {
                endWidthString = string.Empty;
            }

            string[] vertexStrings = config.GetValues("vertex");

            if (vertexStrings.Length < 2)
            {
                throw new ArgumentException("Insufficient number of 'vertex' entries in LINE_STRING " + name + " (must have at least 2)");
            }


            Vector2 position = Vector2.zero;
            if (!config.TryGetValue("position", ref position))
            {
                throw new ArgumentException("Unable to find 'position' in LINE_STRING " + name);
            }

            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            string rotationVariableName = string.Empty;
            config.TryGetValue("rotation", ref rotationVariableName);

            string range = string.Empty;
            if (config.TryGetValue("range", ref range))
            {
                string[] ranges = Utility.SplitVariableList(range);
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in LINE_STRING " + name);
                }
                range1 = comp.GetVariable(ranges[0], prop);
                range2 = comp.GetVariable(ranges[1], prop);

                rangeMode = true;
            }
            else
            {
                rangeMode = false;
            }

            lineOrigin = new GameObject();
            lineOrigin.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
            lineOrigin.layer = pageRoot.gameObject.layer;
            lineOrigin.transform.parent = pageRoot;
            lineOrigin.transform.position = pageRoot.position;
            lineOrigin.transform.Translate(monitor.screenSize.x * -0.5f + position.x, monitor.screenSize.y * 0.5f - position.y, depth);
            // add renderer stuff
            lineMaterial = new Material(MASLoader.shaders["MOARdV/Monitor"]);
            lineRenderer = lineOrigin.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.material = lineMaterial;
            lineRenderer.startColor = startColor;
            lineRenderer.endColor = endColor;
            lineRenderer.startWidth = startWidth;
            lineRenderer.endWidth = endWidth;

            int numVertices = vertexStrings.Length;
            lineRenderer.positionCount = numVertices;
            vertices = new Vector3[numVertices];

            string textureName = string.Empty;
            if (config.TryGetValue("texture", ref textureName))
            {
                Texture tex = GameDatabase.Instance.GetTexture(textureName, false);
                if (tex != null)
                {
                    lineMaterial.mainTexture = tex;
                    inverseTextureWidth = 1.0f / (float)tex.width;
                    usesTexture = true;
                }
            }

            for (int i = 0; i < numVertices; ++i)
            {
                // Need to make a copy of the value for the lambda capture,
                // otherwise we'll try using i = numVertices in the callbacks.
                int index = i;
                vertices[i] = Vector3.zero;

                string[] vtx = Utility.SplitVariableList(vertexStrings[i]);
                if (vtx.Length != 2)
                {
                    throw new ArgumentException("vertex " + (i + 1).ToString() + " does not contain two value in LINE_STRING " + name);
                }

                Action<double> vertexX = (double newValue) =>
                {
                    vertices[index].x = (float)newValue;
                    lineRenderer.SetPosition(index, vertices[index]);
                    if (usesTexture)
                    {
                        RecalculateTextureScale();
                    }
                };
                variableRegistrar.RegisterNumericVariable(vtx[0], vertexX);

                Action<double> vertexY = (double newValue) =>
                {
                    // Invert the value, since we stipulate +y is down on the monitor.
                    vertices[index].y = -(float)newValue;
                    lineRenderer.SetPosition(index, vertices[index]);
                    if (usesTexture)
                    {
                        RecalculateTextureScale();
                    }
                };
                variableRegistrar.RegisterNumericVariable(vtx[1], vertexY);
            }
            lineRenderer.SetPositions(vertices);
            RenderPage(false);

            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the lines if we're in variable mode
                lineOrigin.SetActive(false);
                variableRegistrar.RegisterNumericVariable(variableName, VariableCallback);
            }
            else
            {
                lineOrigin.SetActive(true);
            }

            if (!string.IsNullOrEmpty(rotationVariableName))
            {
                variableRegistrar.RegisterNumericVariable(rotationVariableName, RotationCallback);
            }

            if (string.IsNullOrEmpty(endColorString))
            {
                Color32 col;
                if (comp.TryGetNamedColor(startColorString, out col))
                {
                    startColor = col;
                    endColor = col;
                }
                else
                {
                    string[] startColors = Utility.SplitVariableList(startColorString);
                    if (startColors.Length < 3 || startColors.Length > 4)
                    {
                        throw new ArgumentException("startColor does not contain 3 or 4 values in LINE_STRING " + name);
                    }

                    Action<double> startColorR = (double newValue) =>
                    {
                        startColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.startColor = startColor;
                        lineRenderer.endColor = startColor;
                    };
                    variableRegistrar.RegisterNumericVariable(startColors[0], startColorR);

                    Action<double> startColorG = (double newValue) =>
                    {
                        startColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.startColor = startColor;
                        lineRenderer.endColor = startColor;
                    };
                    variableRegistrar.RegisterNumericVariable(startColors[1], startColorG);

                    Action<double> startColorB = (double newValue) =>
                    {
                        startColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.startColor = startColor;
                        lineRenderer.endColor = startColor;
                    };
                    variableRegistrar.RegisterNumericVariable(startColors[2], startColorB);

                    if (startColors.Length == 4)
                    {
                        Action<double> startColorA = (double newValue) =>
                        {
                            startColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            lineRenderer.startColor = startColor;
                            lineRenderer.endColor = startColor;
                        };
                        variableRegistrar.RegisterNumericVariable(startColors[3], startColorA);
                    }
                }

                lineRenderer.startColor = startColor;
                lineRenderer.endColor = startColor;
            }
            else
            {
                Color32 col;
                if (comp.TryGetNamedColor(startColorString, out col))
                {
                    startColor = col;
                }
                else
                {
                    string[] startColors = Utility.SplitVariableList(startColorString);
                    if (startColors.Length < 3 || startColors.Length > 4)
                    {
                        throw new ArgumentException("startColor does not contain 3 or 4 values in LINE_STRING " + name);
                    }

                    Action<double> startColorR = (double newValue) =>
                    {
                        startColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.startColor = startColor;
                    };
                    variableRegistrar.RegisterNumericVariable(startColors[0], startColorR);

                    Action<double> startColorG = (double newValue) =>
                    {
                        startColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.startColor = startColor;
                    };
                    variableRegistrar.RegisterNumericVariable(startColors[1], startColorG);

                    Action<double> startColorB = (double newValue) =>
                    {
                        startColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.startColor = startColor;
                    };
                    variableRegistrar.RegisterNumericVariable(startColors[2], startColorB);

                    if (startColors.Length == 4)
                    {
                        Action<double> startColorA = (double newValue) =>
                        {
                            startColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            lineRenderer.startColor = startColor;
                        };
                        variableRegistrar.RegisterNumericVariable(startColors[3], startColorA);
                    }
                }

                if (comp.TryGetNamedColor(endColorString, out col))
                {
                    endColor = col;
                }
                else
                {
                    string[] endColors = Utility.SplitVariableList(endColorString);
                    if (endColors.Length < 3 || endColors.Length > 4)
                    {
                        throw new ArgumentException("endColor does not contain 3 or 4 values in LINE_STRING " + name);
                    }

                    Action<double> endColorR = (double newValue) =>
                    {
                        endColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.endColor = endColor;
                    };
                    variableRegistrar.RegisterNumericVariable(endColors[0], endColorR);

                    Action<double> endColorG = (double newValue) =>
                    {
                        endColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.endColor = endColor;
                    };
                    variableRegistrar.RegisterNumericVariable(endColors[1], endColorG);

                    Action<double> endColorB = (double newValue) =>
                    {
                        endColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.endColor = endColor;
                    };
                    variableRegistrar.RegisterNumericVariable(endColors[2], endColorB);

                    if (endColors.Length == 4)
                    {
                        Action<double> endColorA = (double newValue) =>
                        {
                            endColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            lineRenderer.endColor = endColor;
                        };
                        variableRegistrar.RegisterNumericVariable(endColors[3], endColorA);
                    }
                }

                lineRenderer.startColor = startColor;
                lineRenderer.endColor = endColor;
            }

            if (string.IsNullOrEmpty(endWidthString))
            {
                // Monowidth line
                Action<double> startWidthAction = (double newValue) =>
                {
                    startWidth = (float)newValue;
                    lineRenderer.startWidth = startWidth;
                    lineRenderer.endWidth = startWidth;
                };
                variableRegistrar.RegisterNumericVariable(startWidthString, startWidthAction);
            }
            else
            {
                Action<double> startWidthAction = (double newValue) =>
                {
                    startWidth = (float)newValue;
                    lineRenderer.startWidth = startWidth;
                };
                variableRegistrar.RegisterNumericVariable(startWidthString, startWidthAction);

                Action<double> endWidthAction = (double newValue) =>
                {
                    endWidth = (float)newValue;
                    lineRenderer.endWidth = endWidth;
                };
                variableRegistrar.RegisterNumericVariable(endWidthString, endWidthAction);
            }
        }

        /// <summary>
        /// Recalculate the texture scale based on the new line length.  This attempts to
        /// minimize the stretching of the texture as the length changes.
        /// </summary>
        private void RecalculateTextureScale()
        {
            int numSegments = vertices.Length - 1;
            float netLength = 0.0f;
            for (int i = 0; i < numSegments; ++i)
            {
                netLength += Vector2.SqrMagnitude(vertices[i] - vertices[i + 1]);
            }
            netLength = Mathf.Sqrt(netLength);
            lineMaterial.SetTextureScale("_MainTex", new Vector2(netLength * inverseTextureWidth, 1.0f));
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
                lineOrigin.SetActive(currentState);
            }
        }

        /// <summary>
        /// Apply a rotation to the line string.
        /// </summary>
        /// <param name="newValue"></param>
        private void RotationCallback(double newValue)
        {
            newValue = newValue % 360.0;
            lineOrigin.transform.localRotation = Quaternion.Euler(new Vector3(0.0f, 0.0f, (float)newValue));
        }

        /// <summary>
        /// Called with `true` prior to the page rendering.  Called with
        /// `false` after the page completes rendering.
        /// </summary>
        /// <param name="enable">true indicates that the page is about to
        /// be rendered.  false indicates that the page has completed rendering.</param>
        public void RenderPage(bool enable)
        {
            lineRenderer.enabled = enable;
        }

        /// <summary>
        /// Called with `true` when the page is active on the monitor, called with
        /// `false` when the page is no longer active.
        /// </summary>
        /// <param name="enable">true when the page is actively displayed, false when the page
        /// is no longer displayed.</param>
        public void SetPageActive(bool enable)
        {

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
            lineRenderer = null;
            UnityEngine.Object.Destroy(lineMaterial);
            lineMaterial = null;
            UnityEngine.Object.Destroy(lineOrigin);
            lineOrigin = null;

            variableRegistrar.ReleaseResources(comp, internalProp);
        }
    }
}
