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
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    class MASPageVerticalBar : IMASMonitorComponent
    {
        private GameObject imageObject;
        private GameObject borderObject;
        private Material imageMaterial;
        private Color borderColor = Color.white;
        private Color sourceColor = Color.white;
        private Material borderMaterial;
        private LineRenderer lineRenderer;
        private MeshRenderer meshRenderer;
        private readonly MASFlightComputer.Variable sourceRange1, sourceRange2;
        private float lastValue = -1.0f;
        private float barHeight;
        private Vector3[] vertices = new Vector3[4];
        private Vector2[] uv = new Vector2[4];
        private Mesh mesh;
        private Vector2 position = Vector2.zero;
        private Vector3 imageOrigin = Vector3.zero;
        private VBarAnchor anchor;
        private readonly int colorField = Shader.PropertyToID("_Color");

        enum VBarAnchor
        {
            Top,
            Middle,
            Bottom
        };

        internal MASPageVerticalBar(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
            : base(config, prop, comp)
        {
            string textureName = string.Empty;
            if (!config.TryGetValue("texture", ref textureName))
            {
                textureName = string.Empty;
            }
            Texture2D mainTexture = null;
            if (!string.IsNullOrEmpty(textureName))
            {
                mainTexture = GameDatabase.Instance.GetTexture(textureName, false);
                if (mainTexture == null)
                {
                    throw new ArgumentException("Unable to find 'texture' " + textureName + " for VERTICAL_BAR " + name);
                }
                mainTexture.wrapMode = TextureWrapMode.Clamp;
            }

            Vector2 size = Vector2.zero;
            if (!config.TryGetValue("size", ref size))
            {
                throw new ArgumentException("Unable to find 'size' in VERTICAL_BAR " + name);
            }
            barHeight = size.y;

            string sourceName = string.Empty;
            if (!config.TryGetValue("source", ref sourceName))
            {
                throw new ArgumentException("Unable to find 'input' in VERTICAL_BAR " + name);
            }

            string sourceRange = string.Empty;
            if (!config.TryGetValue("sourceRange", ref sourceRange))
            {
                throw new ArgumentException("Unable to find 'sourceRange' in VERTICAL_BAR " + name);
            }
            string[] ranges = Utility.SplitVariableList(sourceRange);
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Incorrect number of values in 'sourceRange' in VERTICAL_BAR " + name);
            }
            sourceRange1 = comp.GetVariable(ranges[0], prop);
            sourceRange2 = comp.GetVariable(ranges[1], prop);

            string anchorName = string.Empty;
            if (config.TryGetValue("anchor", ref anchorName))
            {
                anchorName = anchorName.Trim();
                if (anchorName == VBarAnchor.Top.ToString())
                {
                    anchor = VBarAnchor.Top;
                }
                else if (anchorName == VBarAnchor.Bottom.ToString())
                {
                    anchor = VBarAnchor.Bottom;
                }
                else if (anchorName == VBarAnchor.Middle.ToString())
                {
                    anchor = VBarAnchor.Middle;
                }
                else
                {
                    throw new ArgumentException("Uncrecognized 'anchor' " + anchorName + " in VERTICAL_BAR " + name);
                }
            }
            else
            {
                anchor = VBarAnchor.Bottom;
            }

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
                throw new ArgumentException("Only one of 'borderColor' and 'borderWidth' are defined in VERTICAL_BAR " + name);
            }

            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            // Set up our display surface.
            imageOrigin = pageRoot.position + new Vector3(monitor.screenSize.x * -0.5f, monitor.screenSize.y * 0.5f, depth);
            if (borderWidth > 0.0f)
            {
                borderObject = new GameObject();
                borderObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name + "-border", (int)(-depth / MASMonitor.depthDelta));
                borderObject.layer = pageRoot.gameObject.layer;
                borderObject.transform.parent = pageRoot;
                borderObject.transform.position = imageOrigin + new Vector3(position.x, -(position.y + size.y), 0.0f);

                borderMaterial = new Material(MASLoader.shaders["MOARdV/Monitor"]);
                lineRenderer = borderObject.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = false;
                lineRenderer.material = borderMaterial;
                lineRenderer.startColor = borderColor;
                lineRenderer.endColor = borderColor;
                lineRenderer.startWidth = borderWidth;
                lineRenderer.endWidth = borderWidth;

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
                        throw new ArgumentException("borderColor does not contain 3 or 4 values in VERTICAL_BAR " + name);
                    }

                    variableRegistrar.RegisterNumericVariable(startColors[0], (double newValue) =>
                    {
                        borderColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.startColor = borderColor;
                        lineRenderer.endColor = borderColor;
                    });

                    variableRegistrar.RegisterNumericVariable(startColors[1], (double newValue) =>
                    {
                        borderColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.startColor = borderColor;
                        lineRenderer.endColor = borderColor;
                    });

                    variableRegistrar.RegisterNumericVariable(startColors[2], (double newValue) =>
                    {
                        borderColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        lineRenderer.startColor = borderColor;
                        lineRenderer.endColor = borderColor;
                    });

                    if (startColors.Length == 4)
                    {
                        variableRegistrar.RegisterNumericVariable(startColors[3], (double newValue) =>
                        {
                            borderColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            lineRenderer.startColor = borderColor;
                            lineRenderer.endColor = borderColor;
                        });
                    }
                }

                float halfWidth = borderWidth * 0.5f - 0.5f;
                Vector3[] borderPoints = new Vector3[]
                {
                    new Vector3(-halfWidth, -halfWidth, 0.0f),
                    new Vector3(size.x + halfWidth, -halfWidth, 0.0f),
                    new Vector3(size.x + halfWidth, size.y + halfWidth, 0.0f),
                    new Vector3(-halfWidth, size.y + halfWidth, 0.0f),
                    new Vector3(-halfWidth, -halfWidth, 0.0f)
                };
                lineRenderer.positionCount = 5;
                lineRenderer.SetPositions(borderPoints);
            }
            imageObject = new GameObject();
            imageObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
            imageObject.layer = pageRoot.gameObject.layer;
            imageObject.transform.parent = pageRoot;
            imageObject.transform.position = imageOrigin + new Vector3(position.x, -position.y, 0.0f);

            string positionString = string.Empty;
            if (!config.TryGetValue("position", ref positionString))
            {
                throw new ArgumentException("Unable to find 'position' in VERTICAL_BAR " + name);
            }
            else
            {
                string[] pos = Utility.SplitVariableList(positionString);
                if (pos.Length != 2)
                {
                    throw new ArgumentException("Invalid number of values for 'position' in VERTICAL_BAR " + name);
                }

                if (borderWidth > 0.0f)
                {
                    variableRegistrar.RegisterNumericVariable(pos[0], (double newValue) =>
                    {
                        position.x = (float)newValue;
                        borderObject.transform.position = imageOrigin + new Vector3(position.x, -(position.y + size.y), 0.0f);
                        imageObject.transform.position = imageOrigin + new Vector3(position.x, -position.y, 0.0f);
                    });

                    variableRegistrar.RegisterNumericVariable(pos[1], (double newValue) =>
                    {
                        position.y = (float)newValue;
                        borderObject.transform.position = imageOrigin + new Vector3(position.x, -(position.y + size.y), 0.0f);
                        imageObject.transform.position = imageOrigin + new Vector3(position.x, -position.y, 0.0f);
                    });
                }
                else
                {
                    variableRegistrar.RegisterNumericVariable(pos[0], (double newValue) =>
                    {
                        position.x = (float)newValue;
                        imageObject.transform.position = imageOrigin + new Vector3(position.x, -position.y, 0.0f);
                    });

                    variableRegistrar.RegisterNumericVariable(pos[1], (double newValue) =>
                    {
                        position.y = (float)newValue;
                        imageObject.transform.position = imageOrigin + new Vector3(position.x, -position.y, 0.0f);
                    });
                }
            }

            // add renderer stuff
            MeshFilter meshFilter = imageObject.AddComponent<MeshFilter>();
            meshRenderer = imageObject.AddComponent<MeshRenderer>();
            mesh = new Mesh();
            vertices[0] = new Vector3(0.0f, 0.0f, 0.0f);
            vertices[1] = new Vector3(size.x, 0.0f, 0.0f);
            vertices[2] = new Vector3(0.0f, -size.y, 0.0f);
            vertices[3] = new Vector3(size.x, -size.y, 0.0f);
            mesh.vertices = vertices;
            uv[0] = new Vector2(0.0f, 1.0f);
            uv[1] = Vector2.one;
            uv[2] = Vector2.zero;
            uv[3] = new Vector2(1.0f, 0.0f);
            mesh.uv = uv;
            mesh.triangles = new[] 
                {
                    0, 1, 2,
                    1, 3, 2
                };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            meshFilter.mesh = mesh;
            imageMaterial = new Material(MASLoader.shaders["MOARdV/Monitor"]);
            if (mainTexture != null)
            {
                imageMaterial.mainTexture = mainTexture;
            }
            imageMaterial.SetColor(colorField, sourceColor);
            meshRenderer.material = imageMaterial;
            RenderPage(false);

            string sourceColorName = string.Empty;
            if (config.TryGetValue("sourceColor", ref sourceColorName))
            {
                Color32 namedColor;
                if (comp.TryGetNamedColor(sourceColorName, out namedColor))
                {
                    sourceColor = namedColor;
                    imageMaterial.SetColor(colorField, sourceColor);
                }
                else
                {
                    string[] startColors = Utility.SplitVariableList(sourceColorName);
                    if (startColors.Length < 3 || startColors.Length > 4)
                    {
                        throw new ArgumentException("sourceColor does not contain 3 or 4 values in VERTICAL_BAR " + name);
                    }

                    variableRegistrar.RegisterNumericVariable(startColors[0], (double newValue) =>
                    {
                        sourceColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        imageMaterial.SetColor(colorField, sourceColor);
                    });

                    variableRegistrar.RegisterNumericVariable(startColors[1], (double newValue) =>
                    {
                        sourceColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        imageMaterial.SetColor(colorField, sourceColor);
                    });

                    variableRegistrar.RegisterNumericVariable(startColors[2], (double newValue) =>
                    {
                        sourceColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        imageMaterial.SetColor(colorField, sourceColor);
                    });

                    if (startColors.Length == 4)
                    {
                        variableRegistrar.RegisterNumericVariable(startColors[3], (double newValue) =>
                        {
                            sourceColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            imageMaterial.SetColor(colorField, sourceColor);
                        });
                    }
                }
            }

            variableRegistrar.RegisterNumericVariable(sourceName, SourceCallback);
            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the mesh if we're in variable mode
                imageObject.SetActive(false);
                if (borderObject != null)
                {
                    borderObject.SetActive(false);
                }
                variableRegistrar.RegisterNumericVariable(variableName, VariableCallback);
            }
            else
            {
                if (borderObject != null)
                {
                    borderObject.SetActive(true);
                }
                imageObject.SetActive(true);
            }
        }

        /// <summary>
        /// Update the texture offset.  We do this be inverse-lerping the
        /// input variable and lerping it into the scaled output variable.
        /// </summary>
        /// <param name="newValue"></param>
        private void SourceCallback(double newValue)
        {
            float iLerp = 1.0f - Mathf.InverseLerp((float)sourceRange1.DoubleValue(), (float)sourceRange2.DoubleValue(), (float)newValue);
            if (!Mathf.Approximately(lastValue, iLerp))
            {
                float posLerp = 1.0f - iLerp;
                // Recompute y positions and uvs
                if (anchor == VBarAnchor.Top)
                {
                    vertices[2].y = -posLerp * barHeight;
                    vertices[3].y = -posLerp * barHeight;
                    uv[2].y = iLerp;
                    uv[3].y = iLerp;
                }
                else if (anchor == VBarAnchor.Middle)
                {
                    vertices[0].y = Math.Min(0.5f, iLerp) * -barHeight;
                    vertices[1].y = Math.Min(0.5f, iLerp) * -barHeight;
                    uv[0].y = Math.Max(0.5f, posLerp);
                    uv[1].y = Math.Max(0.5f, posLerp);

                    vertices[2].y = Math.Max(0.5f, iLerp) * -barHeight;
                    vertices[3].y = Math.Max(0.5f, iLerp) * -barHeight;
                    uv[2].y = Math.Min(0.5f, posLerp);
                    uv[3].y = Math.Min(0.5f, posLerp);
                }
                else // VBarAnchor.Bottom
                {
                    vertices[0].y = -iLerp * barHeight;
                    vertices[1].y = -iLerp * barHeight;
                    uv[0].y = posLerp;
                    uv[1].y = posLerp;
                }

                mesh.vertices = vertices;
                mesh.uv = uv;
                mesh.UploadMeshData(false);
                lastValue = iLerp;
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
                imageObject.SetActive(currentState);
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
            meshRenderer.enabled = enable;
            if (lineRenderer != null)
            {
                lineRenderer.enabled = enable;
            }
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            UnityEngine.GameObject.Destroy(imageObject);
            imageObject = null;
            UnityEngine.GameObject.Destroy(imageMaterial);
            imageMaterial = null;
            UnityEngine.Object.Destroy(borderMaterial);
            borderMaterial = null;
            UnityEngine.Object.Destroy(borderObject);
            borderObject = null;

            variableRegistrar.ReleaseResources();
        }
    }
}
