/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2019 MOARdV
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
    /// The navball component displays a NavBall for a monitor.
    /// </summary>
    internal class MASPageNavBall : IMASMonitorComponent
    {
        private GameObject imageObject;
        private Material imageMaterial;
        private MeshRenderer rentexRenderer;
        private Vector2 position = Vector2.zero;
        private Vector3 componentOrigin = Vector3.zero;

        private MASFlightComputer comp;

        internal MASPageNavBall(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
            : base(config, prop, comp)
        {
            this.comp = comp;

            string modelName = string.Empty;
            if (!config.TryGetValue("model", ref modelName))
            {
                throw new ArgumentException("Unable to find 'model' in NAVBALL " + name);
            }

            string textureName = string.Empty;
            if (!config.TryGetValue("texture", ref textureName))
            {
                throw new ArgumentException("Unable to find 'texture' in NAVBALL " + name);
            }

            Vector2 size = Vector2.zero;
            if (!config.TryGetValue("size", ref size))
            {
                throw new ArgumentException("Unable to find 'size' in NAVBALL " + name);
            }
            size = size * 0.5f;

            float opacity = 1.0f;
            if (!config.TryGetValue("opacity", ref opacity))
            {
                opacity = 1.0f;
            }
            else
            {
                opacity = Mathf.Clamp01(opacity);
            }

            float iconScale = 1.0f;
            if (!config.TryGetValue("iconScale", ref iconScale))
            {
                iconScale = 1.0f;
            }

            MASNavBallRenderer.NavBallInstance nbi = MASNavBallRenderer.GetInstance(comp, name, modelName, textureName, opacity, iconScale);

            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            componentOrigin = pageRoot.position + new Vector3(monitor.screenSize.x * -0.5f, monitor.screenSize.y * 0.5f, depth);

            // Set up our display surface.
            imageObject = new GameObject();
            imageObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
            imageObject.layer = pageRoot.gameObject.layer;
            imageObject.transform.parent = pageRoot;
            imageObject.transform.position = pageRoot.position;
            imageObject.transform.Translate(monitor.screenSize.x * -0.5f + position.x, monitor.screenSize.y * 0.5f - position.y, depth);
            // add renderer stuff
            MeshFilter meshFilter = imageObject.AddComponent<MeshFilter>();
            rentexRenderer = imageObject.AddComponent<MeshRenderer>();
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
                {
                    new Vector3(-size.x, size.y, 0.0f),
                    new Vector3(size.x, size.y, 0.0f),
                    new Vector3(-size.x, -size.y, 0.0f),
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

            Shader displayShader = MASLoader.shaders["MOARdV/Monitor"];
            imageMaterial = new Material(displayShader);
            imageMaterial.mainTexture = nbi.mainTexture;
            rentexRenderer.material = imageMaterial;

            string positionString = string.Empty;
            if (!config.TryGetValue("position", ref positionString))
            {
                throw new ArgumentException("Unable to find 'position' in NAVBALL " + name);
            }
            else
            {
                string[] pos = Utility.SplitVariableList(positionString);
                if (pos.Length != 2)
                {
                    throw new ArgumentException("Invalid number of values for 'position' in NAVBALL " + name);
                }

                variableRegistrar.RegisterVariableChangeCallback(pos[0], (double newValue) =>
                {
                    position.x = (float)newValue;
                    imageObject.transform.position = componentOrigin + new Vector3(position.x, -position.y, 0.0f);
                });

                variableRegistrar.RegisterVariableChangeCallback(pos[1], (double newValue) =>
                {
                    position.y = (float)newValue;
                    imageObject.transform.position = componentOrigin + new Vector3(position.x, -position.y, 0.0f);
                });
            }

            RenderPage(false);

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
            rentexRenderer.enabled = enable;
        }

        /// <summary>
        /// Called with `true` when the page is active on the monitor, called with
        /// `false` when the page is no longer active.
        /// </summary>
        /// <param name="enable">true when the page is actively displayed, false when the page
        /// is no longer displayed.</param>
        public override void SetPageActive(bool enable)
        {
            // TODO: Keep the NavBallInstance around, and add a callback system
            // that can disable its camera when no one is looking at it.
            //navballCamera.enabled = enable;
            variableRegistrar.EnableCallbacks(enable);
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            UnityEngine.GameObject.Destroy(imageObject);
            imageObject = null;
            UnityEngine.Object.Destroy(imageMaterial);
            imageMaterial = null;
            variableRegistrar.ReleaseResources();
            this.comp = null;
        }
    }
}
