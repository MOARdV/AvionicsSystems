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
    internal class MASPageVerticalStrip : IMASMonitorComponent
    {
        private GameObject imageObject;
        private Material imageMaterial;
        private MeshRenderer meshRenderer;
        private readonly float textureOffset;
        private readonly float texelWidth;
        private float inputRange1, inputRange2;
        private float displayRange1, displayRange2;

        internal MASPageVerticalStrip(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
            : base(config, prop, comp)
        {
            string textureName = string.Empty;
            if (!config.TryGetValue("texture", ref textureName))
            {
                throw new ArgumentException("Unable to find 'texture' in VERTICAL_STRIP " + name);
            }
            Texture2D mainTexture = GameDatabase.Instance.GetTexture(textureName, false);
            if (mainTexture == null)
            {
                throw new ArgumentException("Unable to find 'texture' " + textureName + " for VERTICAL_STRIP " + name);
            }
            bool wrapMode = false;
            if (!config.TryGetValue("wrap", ref wrapMode))
            {
                wrapMode = false;
            }
            mainTexture.wrapMode = (wrapMode) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

            Vector2 position = Vector2.zero;
            if (!config.TryGetValue("position", ref position))
            {
                throw new ArgumentException("Unable to find 'position' in VERTICAL_STRIP " + name);
            }

            Vector2 size = Vector2.zero;
            if (!config.TryGetValue("size", ref size))
            {
                throw new ArgumentException("Unable to find 'size' in VERTICAL_STRIP " + name);
            }

            string variableName = string.Empty;
            string inputName = string.Empty;
            if (!config.TryGetValue("input", ref inputName))
            {
                throw new ArgumentException("Unable to find 'input' in VERTICAL_STRIP " + name);
            }

            string inputRange = string.Empty;
            if (!config.TryGetValue("inputRange", ref inputRange))
            {
                throw new ArgumentException("Unable to find 'inputRange' in VERTICAL_STRIP " + name);
            }
            string[] ranges = Utility.SplitVariableList(inputRange);
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Incorrect number of values in 'inputRange' in VERTICAL_STRIP " + name);
            }
            variableRegistrar.RegisterVariableChangeCallback(ranges[0], (double newValue) => inputRange1 = (float)newValue);
            variableRegistrar.RegisterVariableChangeCallback(ranges[1], (double newValue) => inputRange2 = (float)newValue);

            string displayRange = string.Empty;
            if (!config.TryGetValue("displayRange", ref displayRange))
            {
                throw new ArgumentException("Unable to find 'displayRange' in VERTICAL_STRIP " + name);
            }
            ranges = Utility.SplitVariableList(displayRange);
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Incorrect number of values in 'displayRange' in VERTICAL_STRIP " + name);
            }
            variableRegistrar.RegisterVariableChangeCallback(ranges[0], (double newValue) => displayRange1 = (float)newValue);
            variableRegistrar.RegisterVariableChangeCallback(ranges[1], (double newValue) => displayRange2 = (float)newValue);

            float displayHeight = 0.0f;
            if (!config.TryGetValue("displayHeight", ref displayHeight))
            {
                throw new ArgumentException("Unable to find 'displayHeight' in VERTICAL_STRIP " + name);
            }
            texelWidth = mainTexture.texelSize.y;
            float textureSpan = displayHeight * texelWidth;
            textureOffset = textureSpan * 0.5f;

            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            // Set up our display surface.
            imageObject = new GameObject();
            imageObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
            imageObject.layer = pageRoot.gameObject.layer;
            imageObject.transform.parent = pageRoot;
            imageObject.transform.position = pageRoot.position;
            imageObject.transform.Translate(monitor.screenSize.x * -0.5f + position.x, monitor.screenSize.y * 0.5f - position.y, depth);
            // add renderer stuff
            MeshFilter meshFilter = imageObject.AddComponent<MeshFilter>();
            meshRenderer = imageObject.AddComponent<MeshRenderer>();
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
                {
                    new Vector3(0.0f, 0.0f, 0.0f),
                    new Vector3(size.x, 0.0f, 0.0f),
                    new Vector3(0.0f, -size.y, 0.0f),
                    new Vector3(size.x, -size.y, 0.0f),
                };
            mesh.uv = new[]
                {
                    new Vector2(0.0f, 1.0f),
                    Vector2.one,
                    Vector2.zero,
                    new Vector2(1.0f, 0.0f),
                };
            mesh.triangles = new[] 
                {
                    0, 1, 2,
                    1, 3, 2
                };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            meshFilter.mesh = mesh;
            imageMaterial = new Material(MASLoader.shaders["MOARdV/Monitor"]);
            imageMaterial.mainTexture = mainTexture;
            imageMaterial.mainTextureScale = new Vector2(1.00f, textureSpan);
            meshRenderer.material = imageMaterial;
            RenderPage(false);

            variableRegistrar.RegisterVariableChangeCallback(inputName, InputCallback);
            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the mesh if we're in variable mode
                imageObject.SetActive(false);
                variableRegistrar.RegisterVariableChangeCallback(variableName, VariableCallback);
            }
            else
            {
                imageObject.SetActive(true);
            }
        }

        /// <summary>
        /// Update the texture offset.  We do this be inverse-lerping the
        /// input variable and lerping it into the scaled output variable.
        /// </summary>
        /// <param name="newValue"></param>
        private void InputCallback(double newValue)
        {
            float iLerp = Mathf.InverseLerp(inputRange1, inputRange2, (float)newValue);
            float newCenter = Mathf.Lerp(displayRange1 * texelWidth, displayRange2 * texelWidth, iLerp);
            // Since we invert the vertical coordinates to place y=0 at the top, we need to flip the offset here,
            // too.
            imageMaterial.mainTextureOffset = new Vector2(0.0f, 1.0f - (newCenter + textureOffset));
        }

        /// <summary>
        /// Handle a changed value
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue)
        {
            if (EvaluateVariable(newValue))
            {
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

            variableRegistrar.ReleaseResources();
        }
    }
}

