/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2017 MOARdV
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
    /// Implements a line string-based ellipe renderer.
    /// </summary>
    class MASPageEllipse : IMASMonitorComponent
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

        private readonly int numVertices;
        private float radiusX, radiusY;
        private float startAngle, endAngle;

        private bool usesTexture;
        private float inverseTextureWidth = 1.0f;

        private VariableRegistrar registeredVariables;

        internal MASPageEllipse(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            registeredVariables = new VariableRegistrar(comp, prop);

            if (!config.TryGetValue("name", ref name))
            {
                name = "anonymous";
            }

            string startColorString = string.Empty;
            if (!config.TryGetValue("startColor", ref startColorString))
            {
                throw new ArgumentException("Unable to find 'startColor' in ELLIPSE " + name);
            }
            string endColorString = string.Empty;
            if (!config.TryGetValue("endColor", ref endColorString))
            {
                endColorString = string.Empty;
            }

            string startWidthString = string.Empty;
            if (!config.TryGetValue("startWidth", ref startWidthString))
            {
                throw new ArgumentException("Unable to find 'startWidth' in ELLIPSE " + name);
            }
            string endWidthString = string.Empty;
            if (!config.TryGetValue("endWidth", ref endWidthString))
            {
                endWidthString = string.Empty;
            }

            numVertices = 0;
            if (!config.TryGetValue("vertexCount", ref numVertices))
            {
                throw new ArgumentException("Unable to find 'vertexCount' in ELLIPSE " + name);
            }
            else if (numVertices < 3)
            {
                throw new ArgumentException("'vertexCount' must be at least 3 in ELLIPSE " + name);
            }

            Vector2 position = Vector2.zero;
            if (!config.TryGetValue("position", ref position))
            {
                throw new ArgumentException("Unable to find 'position' in ELLIPSE " + name);
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
                string[] ranges = range.Split(',');
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in ELLIPSE " + name);
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
            lineMaterial = new Material(Shader.Find("Particles/Additive"));
            lineRenderer = lineOrigin.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.material = lineMaterial;
            lineRenderer.SetColors(startColor, endColor);
            lineRenderer.SetWidth(startWidth, endWidth);

            ++numVertices;
            lineRenderer.SetVertexCount(numVertices);
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

            string startAngleName = string.Empty;
            if (config.TryGetValue("startAngle", ref startAngleName))
            {
                registeredVariables.RegisterNumericVariable(startAngleName, (double newValue) =>
                {
                    startAngle = (float)newValue;
                    RecalculateVertices();
                });
            }
            else
            {
                startAngle = 0.0f;
            }

            string endAngleName = string.Empty;
            if (config.TryGetValue("endAngle", ref endAngleName))
            {
                if (string.IsNullOrEmpty(startAngleName))
                {
                    throw new ArgumentException("Missing 'startAngle', but found 'endAngle' in ELLIPSE " + name);
                }
                else
                {
                    registeredVariables.RegisterNumericVariable(endAngleName, (double newValue) =>
                    {
                        endAngle = (float)newValue;
                        RecalculateVertices();
                    });
                }
            }
            else if (!string.IsNullOrEmpty(startAngleName))
            {
                throw new ArgumentException("Found 'startAngle', but missing 'endAngle' in ELLIPSE " + name);
            }
            else
            {
                endAngle = 360.0f;
            }

            string radiusXName = string.Empty;
            if (!config.TryGetValue("radiusX", ref radiusXName))
            {
                throw new ArgumentException("Unable to find 'radiusX' in ELLIPSE " + name);
            }
            string radiusYName = string.Empty;
            if (!config.TryGetValue("radiusY", ref radiusYName))
            {
                Action<double> newRadius = (double newValue) =>
                {
                    radiusX = (float)newValue;
                    radiusY = radiusX;
                    RecalculateVertices();
                };
                registeredVariables.RegisterNumericVariable(radiusXName, newRadius);
            }
            else
            {
                Action<double> newRadiusX = (double newValue) =>
                {
                    radiusX = (float)newValue;
                    RecalculateVertices();
                };
                registeredVariables.RegisterNumericVariable(radiusXName, newRadiusX);
                Action<double> newRadiusY = (double newValue) =>
                {
                    radiusY = (float)newValue;
                    RecalculateVertices();
                };
                registeredVariables.RegisterNumericVariable(radiusYName, newRadiusY);
            }

            EnableRender(false);

            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the lines if we're in variable mode
                lineOrigin.SetActive(false);
                registeredVariables.RegisterNumericVariable(variableName, VariableCallback);
            }
            else
            {
                lineOrigin.SetActive(true);
            }

            if (!string.IsNullOrEmpty(rotationVariableName))
            {
                registeredVariables.RegisterNumericVariable(rotationVariableName, RotationCallback);
            }

            if (string.IsNullOrEmpty(endColorString))
            {
                Color32 namedColor;
                if (comp.TryGetNamedColor(startColorString, out namedColor))
                {
                    startColor = namedColor;
                    lineRenderer.SetColors(startColor, startColor);
                }
                else
                {
                    string[] startColors = Utility.SplitVariableList(startColorString);
                    if (startColors.Length < 3 || startColors.Length > 4)
                    {
                        throw new ArgumentException("startColor does not contain 3 or 4 values in ELLIPSE " + name);
                    }

                    Action<double> startColorR = (double newValue) =>
                    {
                        startColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.SetColors(startColor, startColor);
                    };
                    registeredVariables.RegisterNumericVariable(startColors[0], startColorR);

                    Action<double> startColorG = (double newValue) =>
                    {
                        startColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.SetColors(startColor, startColor);
                    };
                    registeredVariables.RegisterNumericVariable(startColors[1], startColorG);

                    Action<double> startColorB = (double newValue) =>
                    {
                        startColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.SetColors(startColor, startColor);
                    };
                    registeredVariables.RegisterNumericVariable(startColors[2], startColorB);

                    if (startColors.Length == 4)
                    {
                        Action<double> startColorA = (double newValue) =>
                        {
                            startColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            lineRenderer.SetColors(startColor, startColor);
                        };
                        registeredVariables.RegisterNumericVariable(startColors[3], startColorA);
                    }
                }
            }
            else
            {
                Color32 namedColor;
                if (comp.TryGetNamedColor(startColorString, out namedColor))
                {
                    startColor = namedColor;
                    lineRenderer.SetColors(startColor, endColor);
                }
                else
                {
                    string[] startColors = Utility.SplitVariableList(startColorString);
                    if (startColors.Length < 3 || startColors.Length > 4)
                    {
                        throw new ArgumentException("startColor does not contain 3 or 4 values in ELLIPSE " + name);
                    }

                    Action<double> startColorR = (double newValue) =>
                    {
                        startColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.SetColors(startColor, endColor);
                    };
                    registeredVariables.RegisterNumericVariable(startColors[0], startColorR);

                    Action<double> startColorG = (double newValue) =>
                    {
                        startColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.SetColors(startColor, endColor);
                    };
                    registeredVariables.RegisterNumericVariable(startColors[1], startColorG);

                    Action<double> startColorB = (double newValue) =>
                    {
                        startColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.SetColors(startColor, endColor);
                    };
                    registeredVariables.RegisterNumericVariable(startColors[2], startColorB);

                    if (startColors.Length == 4)
                    {
                        Action<double> startColorA = (double newValue) =>
                        {
                            startColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            lineRenderer.SetColors(startColor, endColor);
                        };
                        registeredVariables.RegisterNumericVariable(startColors[3], startColorA);
                    }
                }

                if (comp.TryGetNamedColor(endColorString, out namedColor))
                {
                    endColor = namedColor;
                    lineRenderer.SetColors(startColor, endColor);
                }
                else
                {
                    string[] endColors = Utility.SplitVariableList(endColorString);
                    if (endColors.Length < 3 || endColors.Length > 4)
                    {
                        throw new ArgumentException("endColor does not contain 3 or 4 values in ELLIPSE " + name);
                    }

                    Action<double> endColorR = (double newValue) =>
                    {
                        endColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.SetColors(startColor, endColor);
                    };
                    registeredVariables.RegisterNumericVariable(endColors[0], endColorR);

                    Action<double> endColorG = (double newValue) =>
                    {
                        endColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.SetColors(startColor, endColor);
                    };
                    registeredVariables.RegisterNumericVariable(endColors[1], endColorG);

                    Action<double> endColorB = (double newValue) =>
                    {
                        endColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.SetColors(startColor, endColor);
                    };
                    registeredVariables.RegisterNumericVariable(endColors[2], endColorB);

                    if (endColors.Length == 4)
                    {
                        Action<double> endColorA = (double newValue) =>
                        {
                            endColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            lineRenderer.SetColors(startColor, endColor);
                        };
                        registeredVariables.RegisterNumericVariable(endColors[3], endColorA);
                    }
                }
            }

            if (string.IsNullOrEmpty(endWidthString))
            {
                // Monowidth line
                Action<double> startWidthAction = (double newValue) =>
                {
                    startWidth = (float)newValue;
                    lineRenderer.SetWidth(startWidth, startWidth);
                };
                registeredVariables.RegisterNumericVariable(startWidthString, startWidthAction);
            }
            else
            {
                Action<double> startWidthAction = (double newValue) =>
                {
                    startWidth = (float)newValue;
                    lineRenderer.SetWidth(startWidth, endWidth);
                };
                registeredVariables.RegisterNumericVariable(startWidthString, startWidthAction);

                Action<double> endWidthAction = (double newValue) =>
                {
                    endWidth = (float)newValue;
                    lineRenderer.SetWidth(startWidth, endWidth);
                };
                registeredVariables.RegisterNumericVariable(endWidthString, endWidthAction);
            }
        }

        /// <summary>
        /// Recalculate the vertices of the ellipse.
        /// </summary>
        private void RecalculateVertices()
        {
            float theta = startAngle * Mathf.Deg2Rad;
            float arcLength = (endAngle - startAngle) * Mathf.Deg2Rad;
            arcLength = Mathf.Clamp(arcLength, -2.0f * Mathf.PI, 2.0f * Mathf.PI);
            float radiansPerVertex = arcLength / (float)(numVertices - 1);
            for (int i = 0; i < numVertices; ++i)
            {
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);
                theta += radiansPerVertex;

                vertices[i] = Vector3.zero;
                vertices[i].x = radiusX * cosTheta;
                vertices[i].y = radiusY * sinTheta;
            }

            lineRenderer.SetPositions(vertices);

            if (usesTexture)
            {
                RecalculateTextureScale();
            }
        }

        /// <summary>
        /// Recalculate the texture scale based on the new line length.  This attempts to
        /// minimize the stretching of the texture as the length changes.
        /// </summary>
        private void RecalculateTextureScale()
        {
            float netLength = Mathf.PI * (3.0f * (radiusX + radiusY) - Mathf.Sqrt((3.0f * radiusX + radiusY) * (3.0f * radiusY + radiusX)));
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
        /// Enable / disable renderer components without disabling game objects.
        /// </summary>
        /// <param name="enable"></param>
        public void EnableRender(bool enable)
        {
            lineRenderer.enabled = enable;
        }

        /// <summary>
        /// Enables / disables overall page rendering.
        /// </summary>
        /// <param name="enable"></param>
        public void EnablePage(bool enable)
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

            registeredVariables.ReleaseResources(comp, internalProp);
        }
    }
}
