/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016 - 2017 MOARdV
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
    class MASPageHorizontalBar : IMASMonitorComponent
    {
        private string name = "anonymous";
        private GameObject imageObject;
        private GameObject borderObject;
        private Material imageMaterial;
        private Material borderMaterial;
        private LineRenderer lineRenderer;
        private MeshRenderer meshRenderer;
        private readonly string variableName;
        private readonly string sourceName;
        private MASFlightComputer.Variable range1, range2;
        private readonly MASFlightComputer.Variable sourceRange1, sourceRange2;
        private readonly bool rangeMode;
        private bool currentState;
        private float lastValue = -1.0f;
        private float barWidth;
        private Vector3[] vertices = new Vector3[4];
        private Vector2[] uv = new Vector2[4];
        private Mesh mesh;
        private HBarAnchor anchor;

        enum HBarAnchor
        {
            Left,
            Middle,
            Right
        };

        internal MASPageHorizontalBar(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "anonymous";
            }

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
                    throw new ArgumentException("Unable to find 'texture' " + textureName + " for HORIZONTAL_BAR " + name);
                }
                mainTexture.wrapMode = TextureWrapMode.Clamp;
            }

            Vector2 position = Vector2.zero;
            if (!config.TryGetValue("position", ref position))
            {
                throw new ArgumentException("Unable to find 'position' in HORIZONTAL_BAR " + name);
            }

            Vector2 size = Vector2.zero;
            if (!config.TryGetValue("size", ref size))
            {
                throw new ArgumentException("Unable to find 'size' in HORIZONTAL_BAR " + name);
            }
            barWidth = size.x;

            if (!config.TryGetValue("source", ref sourceName))
            {
                throw new ArgumentException("Unable to find 'input' in HORIZONTAL_BAR " + name);
            }

            string sourceRange = string.Empty;
            if (!config.TryGetValue("sourceRange", ref sourceRange))
            {
                throw new ArgumentException("Unable to find 'sourceRange' in HORIZONTAL_BAR " + name);
            }
            string[] ranges = sourceRange.Split(',');
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Incorrect number of values in 'sourceRange' in HORIZONTAL_BAR " + name);
            }
            sourceRange1 = comp.GetVariable(ranges[0], prop);
            sourceRange2 = comp.GetVariable(ranges[1], prop);

            Color sourceColor = XKCDColors.White;
            string sourceColorName = string.Empty;
            if (config.TryGetValue("sourceColor", ref sourceColorName))
            {
                sourceColor = Utility.ParseColor32(sourceColorName, comp);
            }

            string anchorName = string.Empty;
            if (config.TryGetValue("anchor", ref anchorName))
            {
                anchorName = anchorName.Trim();
                if (anchorName == HBarAnchor.Left.ToString())
                {
                    anchor = HBarAnchor.Left;
                }
                else if (anchorName == HBarAnchor.Right.ToString())
                {
                    anchor = HBarAnchor.Right;
                }
                else if (anchorName == HBarAnchor.Middle.ToString())
                {
                    anchor = HBarAnchor.Middle;
                }
                else
                {
                    throw new ArgumentException("Uncrecognized 'anchor' " + anchorName + " in HORIZONTAL_BAR " + name);
                }
            }
            else
            {
                anchor = HBarAnchor.Left;
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
                throw new ArgumentException("Only one of 'borderColor' and 'borderWidth' are defined in HORIZONTAL_BAR " + name);
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
                    throw new ArgumentException("Incorrect number of values in 'range' in HORIZONTAL_BAR " + name);
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
                borderObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name + "-border", (int)(-depth / MASMonitor.depthDelta));
                borderObject.layer = pageRoot.gameObject.layer;
                borderObject.transform.parent = pageRoot;
                borderObject.transform.position = pageRoot.position;
                borderObject.transform.Translate(monitor.screenSize.x * -0.5f + position.x, monitor.screenSize.y * 0.5f - position.y - size.y, depth);

                Color borderColor = Utility.ParseColor32(borderColorName, comp);
                borderMaterial = new Material(MASLoader.shaders["MOARdV/Monitor"]);
                lineRenderer = borderObject.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = false;
                lineRenderer.material = borderMaterial;
                lineRenderer.startColor = borderColor;
                lineRenderer.endColor = borderColor;
                lineRenderer.startWidth = borderWidth;
                lineRenderer.endWidth = borderWidth;

                float halfWidth = borderWidth * 0.5f - 0.5f;
                Vector3[] borderPoints = new Vector3[]
                {
                    new Vector3(-halfWidth, -halfWidth, 0.0f),
                    new Vector3(size.x + halfWidth, -halfWidth, 0.0f),
                    new Vector3(size.x + halfWidth, size.y + halfWidth, 0.0f),
                    new Vector3(-halfWidth, size.y + halfWidth, 0.0f),
                    new Vector3(-halfWidth, -halfWidth, 0.0f)
                };
                lineRenderer.SetVertexCount(5);
                lineRenderer.SetPositions(borderPoints);
            }
            imageObject = new GameObject();
            imageObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
            imageObject.layer = pageRoot.gameObject.layer;
            imageObject.transform.parent = pageRoot;
            imageObject.transform.position = pageRoot.position;
            imageObject.transform.Translate(monitor.screenSize.x * -0.5f + position.x, monitor.screenSize.y * 0.5f - position.y, depth);
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
            imageMaterial = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));
            if (mainTexture != null)
            {
                imageMaterial.mainTexture = mainTexture;
            }
            imageMaterial.SetColor("_Color", sourceColor);
            //imageMaterial.mainTextureScale = new Vector2(textureSpan, 1.0f);
            meshRenderer.material = imageMaterial;
            EnableRender(false);

            comp.RegisterNumericVariable(sourceName, prop, SourceCallback);
            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the mesh if we're in variable mode
                if (borderObject != null)
                {
                    borderObject.SetActive(false);
                }
                imageObject.SetActive(false);
                comp.RegisterNumericVariable(variableName, prop, VariableCallback);
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
            float iLerp = Mathf.InverseLerp((float)sourceRange1.SafeValue(), (float)sourceRange2.SafeValue(), (float)newValue);
            if (!Mathf.Approximately(lastValue, iLerp))
            {
                // Recompute x positions and uvs
                if (anchor == HBarAnchor.Left)
                {
                    vertices[1].x = iLerp * barWidth;
                    vertices[3].x = iLerp * barWidth;
                    uv[1].x = iLerp;
                    uv[3].x = iLerp;
                }
                else if (anchor == HBarAnchor.Middle)
                {
                    vertices[0].x = Math.Min(0.5f, iLerp) * barWidth;
                    vertices[2].x = Math.Min(0.5f, iLerp) * barWidth;
                    uv[0].x = Math.Min(0.5f, iLerp);
                    uv[2].x = Math.Min(0.5f, iLerp);

                    vertices[1].x = Math.Max(0.5f, iLerp) * barWidth;
                    vertices[3].x = Math.Max(0.5f, iLerp) * barWidth;
                    uv[1].x = Math.Max(0.5f, iLerp);
                    uv[3].x = Math.Max(0.5f, iLerp);
                }
                else // HBarAnchor.Right
                {
                    vertices[0].x = iLerp * barWidth;
                    vertices[2].x = iLerp * barWidth;
                    uv[0].x = iLerp;
                    uv[2].x = iLerp;
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
            if (rangeMode)
            {
                newValue = (newValue.Between(range1.SafeValue(), range2.SafeValue())) ? 1.0 : 0.0;
            }

            bool newState = (newValue > 0.0);

            if (newState != currentState)
            {
                currentState = newState;
                if (borderObject != null)
                {
                    borderObject.SetActive(currentState);
                }
                imageObject.SetActive(currentState);
            }
        }

        /// <summary>
        /// Enable / disable renderer components without disabling game objects.
        /// </summary>
        /// <param name="enable"></param>
        public void EnableRender(bool enable)
        {
            meshRenderer.enabled = enable;
            if (lineRenderer != null)
            {
                lineRenderer.enabled = enable;
            }
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
            UnityEngine.GameObject.Destroy(imageObject);
            imageObject = null;
            UnityEngine.GameObject.Destroy(imageMaterial);
            imageMaterial = null;
            UnityEngine.Object.Destroy(borderMaterial);
            borderMaterial = null;
            UnityEngine.Object.Destroy(borderObject);
            borderObject = null;

            comp.UnregisterNumericVariable(sourceName, internalProp, SourceCallback);
            if (!string.IsNullOrEmpty(variableName))
            {
                comp.UnregisterNumericVariable(variableName, internalProp, VariableCallback);
            }
        }
    }
}
