/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2017-2019 MOARdV
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
        private Color startColor = Color.white, endColor = Color.white;

        private float startWidth = 1.0f, endWidth = 1.0f;

        private Vector3[] vertices;
        private GameObject lineOrigin;
        private Material lineMaterial;
        private LineRenderer lineRenderer;
        private Vector2 position = Vector2.zero;
        private Vector3 layerOrigin = Vector3.zero;

        private bool usesTexture;
        private float inverseTextureWidth = 1.0f;

        internal MASPageLineString(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
            : base(config, prop, comp)
        {
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


            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            string rotationVariableName = string.Empty;
            config.TryGetValue("rotation", ref rotationVariableName);

            bool loop = false;
            config.TryGetValue("loop", ref loop);

            lineOrigin = new GameObject();
            lineOrigin.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
            lineOrigin.layer = pageRoot.gameObject.layer;
            lineOrigin.transform.parent = pageRoot;
            lineOrigin.transform.position = pageRoot.position;
            lineOrigin.transform.Translate(monitor.screenSize.x * -0.5f + position.x, monitor.screenSize.y * 0.5f - position.y, depth);

            layerOrigin = pageRoot.position + new Vector3(monitor.screenSize.x * -0.5f, monitor.screenSize.y * 0.5f, depth);

            string positionString = string.Empty;
            if (!config.TryGetValue("position", ref positionString))
            {
                position = Vector2.zero;
                throw new ArgumentException("Unable to find 'position' in LINE_STRING " + name);
            }
            else
            {
                string[] pos = Utility.SplitVariableList(positionString);
                if (pos.Length != 2)
                {
                    throw new ArgumentException("Invalid number of values for 'position' in LINE_STRING " + name);
                }

                variableRegistrar.RegisterVariableChangeCallback(pos[0], (double newValue) =>
                {
                    position.x = (float)newValue;
                    lineOrigin.transform.position = layerOrigin + new Vector3(position.x, -position.y, 0.0f);
                });

                variableRegistrar.RegisterVariableChangeCallback(pos[1], (double newValue) =>
                {
                    position.y = (float)newValue;
                    lineOrigin.transform.position = layerOrigin + new Vector3(position.x, -position.y, 0.0f);
                });
            }

            // add renderer stuff
            lineMaterial = new Material(MASLoader.shaders["MOARdV/Monitor"]);
            lineRenderer = lineOrigin.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.material = lineMaterial;
            lineRenderer.startColor = startColor;
            lineRenderer.endColor = endColor;
            lineRenderer.startWidth = startWidth;
            lineRenderer.endWidth = endWidth;
            lineRenderer.loop = loop;

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
                variableRegistrar.RegisterVariableChangeCallback(vtx[0], vertexX);

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
                variableRegistrar.RegisterVariableChangeCallback(vtx[1], vertexY);
            }
            lineRenderer.SetPositions(vertices);
            RenderPage(false);

            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the lines if we're in variable mode
                lineOrigin.SetActive(false);
                variableRegistrar.RegisterVariableChangeCallback(variableName, VariableCallback);
            }
            else
            {
                lineOrigin.SetActive(true);
            }

            if (!string.IsNullOrEmpty(rotationVariableName))
            {
                variableRegistrar.RegisterVariableChangeCallback(rotationVariableName, RotationCallback);
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
                    variableRegistrar.RegisterVariableChangeCallback(startColors[0], startColorR);

                    Action<double> startColorG = (double newValue) =>
                    {
                        startColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.startColor = startColor;
                        lineRenderer.endColor = startColor;
                    };
                    variableRegistrar.RegisterVariableChangeCallback(startColors[1], startColorG);

                    Action<double> startColorB = (double newValue) =>
                    {
                        startColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.startColor = startColor;
                        lineRenderer.endColor = startColor;
                    };
                    variableRegistrar.RegisterVariableChangeCallback(startColors[2], startColorB);

                    if (startColors.Length == 4)
                    {
                        Action<double> startColorA = (double newValue) =>
                        {
                            startColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            lineRenderer.startColor = startColor;
                            lineRenderer.endColor = startColor;
                        };
                        variableRegistrar.RegisterVariableChangeCallback(startColors[3], startColorA);
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
                    variableRegistrar.RegisterVariableChangeCallback(startColors[0], startColorR);

                    Action<double> startColorG = (double newValue) =>
                    {
                        startColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.startColor = startColor;
                    };
                    variableRegistrar.RegisterVariableChangeCallback(startColors[1], startColorG);

                    Action<double> startColorB = (double newValue) =>
                    {
                        startColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.startColor = startColor;
                    };
                    variableRegistrar.RegisterVariableChangeCallback(startColors[2], startColorB);

                    if (startColors.Length == 4)
                    {
                        Action<double> startColorA = (double newValue) =>
                        {
                            startColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            lineRenderer.startColor = startColor;
                        };
                        variableRegistrar.RegisterVariableChangeCallback(startColors[3], startColorA);
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
                    variableRegistrar.RegisterVariableChangeCallback(endColors[0], endColorR);

                    Action<double> endColorG = (double newValue) =>
                    {
                        endColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.endColor = endColor;
                    };
                    variableRegistrar.RegisterVariableChangeCallback(endColors[1], endColorG);

                    Action<double> endColorB = (double newValue) =>
                    {
                        endColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.endColor = endColor;
                    };
                    variableRegistrar.RegisterVariableChangeCallback(endColors[2], endColorB);

                    if (endColors.Length == 4)
                    {
                        Action<double> endColorA = (double newValue) =>
                        {
                            endColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            lineRenderer.endColor = endColor;
                        };
                        variableRegistrar.RegisterVariableChangeCallback(endColors[3], endColorA);
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
                variableRegistrar.RegisterVariableChangeCallback(startWidthString, startWidthAction);
            }
            else
            {
                Action<double> startWidthAction = (double newValue) =>
                {
                    startWidth = (float)newValue;
                    lineRenderer.startWidth = startWidth;
                };
                variableRegistrar.RegisterVariableChangeCallback(startWidthString, startWidthAction);

                Action<double> endWidthAction = (double newValue) =>
                {
                    endWidth = (float)newValue;
                    lineRenderer.endWidth = endWidth;
                };
                variableRegistrar.RegisterVariableChangeCallback(endWidthString, endWidthAction);
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
            if (EvaluateVariable(newValue))
            {
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
        public override void RenderPage(bool enable)
        {
            lineRenderer.enabled = enable;
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            lineRenderer = null;
            UnityEngine.Object.Destroy(lineMaterial);
            lineMaterial = null;
            UnityEngine.Object.Destroy(lineOrigin);
            lineOrigin = null;

            variableRegistrar.ReleaseResources();
        }
    }
}
