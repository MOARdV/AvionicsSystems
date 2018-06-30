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
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// MASPageViewport generates a simple viewport object to mask out part of what's
    /// rendered beneath it.
    /// </summary>
    class MASPageViewport : IMASMonitorComponent
    {
        private GameObject imageObject;
        private Material imageMaterial;
        private Color materialColor = Color.black;
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private readonly Vector2 screenSize;

        internal MASPageViewport(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
            : base(config, prop, comp)
        {
            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            screenSize = monitor.screenSize;

            // Set up our surface.
            imageObject = new GameObject();
            imageObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
            imageObject.layer = pageRoot.gameObject.layer;
            imageObject.transform.parent = pageRoot;
            imageObject.transform.position = pageRoot.position;
            imageObject.transform.Translate(monitor.screenSize.x * -0.5f, monitor.screenSize.y * -0.5f, depth);

            // add renderer stuff
            MeshFilter meshFilter = imageObject.AddComponent<MeshFilter>();
            meshRenderer = imageObject.AddComponent<MeshRenderer>();
            mesh = new Mesh();
            mesh.vertices = new[]
                {
                    // LEFT
                    new Vector3(-1.0f, screenSize.y + 1.0f, 0.0f),
                    new Vector3(64.0f, screenSize.y + 1.0f, 0.0f),
                    new Vector3(-1.0f, -1.0f, 0.0f),
                    new Vector3(64.0f, -1.0f, 0.0f),

                    // RIGHT
                    new Vector3(394.0f, screenSize.y + 1.0f, 0.0f),
                    new Vector3(screenSize.x + 1.0f, screenSize.y + 1.0f, 0.0f),
                    new Vector3(394.0f, -1.0f, 0.0f),
                    new Vector3(screenSize.x + 1.0f, -1.0f, 0.0f),

                    // BOTTOM - note that Y increases from the bottom
                    new Vector3(-1.0f, screenSize.y - 32.0f, 0.0f),
                    new Vector3(screenSize.x + 1.0f, screenSize.y - 32.0f, 0.0f),
                    new Vector3(-1.0f, -1.0f, 0.0f),
                    new Vector3(screenSize.x + 1.0f, -1.0f, 0.0f),

                    // TOP - note that Y increases from the bottom
                    new Vector3(-1.0f, screenSize.y + 1.0f, 0.0f),
                    new Vector3(screenSize.x + 1.0f, screenSize.y + 1.0f, 0.0f),
                    new Vector3(-1.0f, screenSize.y - 256.0f, 0.0f),
                    new Vector3(screenSize.x + 1.0f, screenSize.y - 256.0f, 0.0f),
                };
            mesh.uv = new[]
                {
                    new Vector2(0.0f, 1.0f),
                    Vector2.one,
                    Vector2.zero,
                    new Vector2(1.0f, 0.0f),

                    new Vector2(0.0f, 1.0f),
                    Vector2.one,
                    Vector2.zero,
                    new Vector2(1.0f, 0.0f),

                    new Vector2(0.0f, 1.0f),
                    Vector2.one,
                    Vector2.zero,
                    new Vector2(1.0f, 0.0f),

                    new Vector2(0.0f, 1.0f),
                    Vector2.one,
                    Vector2.zero,
                    new Vector2(1.0f, 0.0f),
                };
            mesh.triangles = new[] 
                {
                    0, 1, 2,
                    1, 3, 2,

                    4, 5, 6,
                    5, 7, 6,

                    8, 9,10,
                    9,11,10,

                   12,13,14,
                   13,15,14
                };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            meshFilter.mesh = mesh;

            imageMaterial = new Material(MASLoader.shaders["MOARdV/Monitor"]);
            imageMaterial.color = materialColor;
            meshRenderer.material = imageMaterial;
            RenderPage(false);

            string upperLeftCornerName = string.Empty;
            if (!config.TryGetValue("upperLeftCorner", ref upperLeftCornerName))
            {
                throw new ArgumentException("Unable to find 'upperLeftCorner' in VIEWPORT " + name);
            }

            string[] cornerVars = Utility.SplitVariableList(upperLeftCornerName);
            if (cornerVars.Length != 2)
            {
                throw new ArgumentException("Incorrect number of entries in 'upperLeftCorner' in VIEWPORT " + name);
            }
            variableRegistrar.RegisterVariableChangeCallback(cornerVars[0], UpdateLeft);
            variableRegistrar.RegisterVariableChangeCallback(cornerVars[1], UpdateTop);

            string lowerRightCornerName = string.Empty;
            if (!config.TryGetValue("lowerRightCorner", ref lowerRightCornerName))
            {
                throw new ArgumentException("Unable to find 'lowerRightCorner' in VIEWPORT " + name);
            }
            cornerVars = Utility.SplitVariableList(lowerRightCornerName);
            if (cornerVars.Length != 2)
            {
                throw new ArgumentException("Incorrect number of entries in 'lowerRightCorner' in VIEWPORT " + name);
            }
            variableRegistrar.RegisterVariableChangeCallback(cornerVars[0], UpdateRight);
            variableRegistrar.RegisterVariableChangeCallback(cornerVars[1], UpdateBottom);

            string colorString = string.Empty;
            if (config.TryGetValue("color", ref colorString))
            {
                string[] startColors = Utility.SplitVariableList(colorString);
                if (startColors.Length < 3 || startColors.Length > 4)
                {
                    throw new ArgumentException("color does not contain 3 or 4 values in VIEWPORT " + name);
                }

                variableRegistrar.RegisterVariableChangeCallback(startColors[0], (double newValue) =>
                {
                    materialColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                    imageMaterial.color = materialColor;
                });

                variableRegistrar.RegisterVariableChangeCallback(startColors[1], (double newValue) =>
                {
                    materialColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                    imageMaterial.color = materialColor;
                });

                variableRegistrar.RegisterVariableChangeCallback(startColors[2], (double newValue) =>
                {
                    materialColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                    imageMaterial.color = materialColor;
                });

                if (startColors.Length == 4)
                {
                    variableRegistrar.RegisterVariableChangeCallback(startColors[3], (double newValue) =>
                    {
                        materialColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        imageMaterial.color = materialColor;
                    });
                }
            }


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
        /// Update the left side of the viewport
        /// </summary>
        /// <param name="newValue"></param>
        private void UpdateLeft(double newValue)
        {
            float newX = Mathf.Max(0.0f, (float)newValue);
            Vector3[] vertices = mesh.vertices;

            vertices[1].x = newX;
            vertices[3].x = newX;
            mesh.vertices = vertices;

            mesh.UploadMeshData(false);
        }

        /// <summary>
        /// Update the right side of the viewport.
        /// </summary>
        /// <param name="newValue"></param>
        private void UpdateRight(double newValue)
        {
            float newX = Mathf.Min(screenSize.x, (float)newValue);
            Vector3[] vertices = mesh.vertices;

            vertices[4].x = newX;
            vertices[6].x = newX;
            mesh.vertices = vertices;

            mesh.UploadMeshData(false);
        }

        /// <summary>
        /// Update the top side of the viewport
        /// </summary>
        /// <param name="newValue"></param>
        private void UpdateTop(double newValue)
        {
            float newY = screenSize.y - Mathf.Min(screenSize.y, (float)newValue);
            Vector3[] vertices = mesh.vertices;

            vertices[14].y = newY;
            vertices[15].y = newY;
            mesh.vertices = vertices;

            mesh.UploadMeshData(false);
        }

        /// <summary>
        /// Update the bottom side of the viewport
        /// </summary>
        /// <param name="newValue"></param>
        private void UpdateBottom(double newValue)
        {
            float newY = screenSize.y - Mathf.Max(0.0f, (float)newValue);
            Vector3[] vertices = mesh.vertices;

            vertices[8].y = newY;
            vertices[9].y = newY;
            mesh.vertices = vertices;

            mesh.UploadMeshData(false);
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
            variableRegistrar.ReleaseResources();

            UnityEngine.GameObject.Destroy(imageObject);
            imageObject = null;
            UnityEngine.GameObject.Destroy(imageMaterial);
            imageMaterial = null;
        }
    }
}
