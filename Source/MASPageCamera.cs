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
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    internal class MASPageCamera : IMASMonitorComponent
    {
        private GameObject imageObject;
        private Material imageMaterial;
        private MeshRenderer meshRenderer;
        private RenderTexture cameraTexture;
        private Texture missingCameraTexture;
        private MASFlightComputer.Variable range1, range2;
        private readonly bool rangeMode;
        private MASFlightComputer.Variable cameraSelector;
        private MASCamera activeCamera = null;
        private MASFlightComputer comp;
        private bool currentState;
        private bool pageEnabled = false;
        private bool coroutineActive;
        private Stopwatch renderStopwatch = new Stopwatch();
        private long renderFrames = 0;

        internal MASPageCamera(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
            : base(config, prop, comp)
        {
            this.comp = comp;

            Vector2 position = Vector2.zero;
            if (!config.TryGetValue("position", ref position))
            {
                throw new ArgumentException("Unable to find 'position' in CAMERA " + name);
            }

            Vector2 size = Vector2.zero;
            if (!config.TryGetValue("size", ref size))
            {
                throw new ArgumentException("Unable to find 'size' in CAMERA " + name);
            }
            float aspectRatio = size.x / size.y;

            cameraTexture = new RenderTexture(((int)size.x) >> MASConfig.CameraTextureScale, ((int)size.y) >> MASConfig.CameraTextureScale, 24, RenderTextureFormat.ARGB32);

            string cameraName = string.Empty;
            if (config.TryGetValue("camera", ref cameraName))
            {
                cameraName = cameraName.Trim();
            }
            else
            {
                throw new ArgumentException("Unable to find 'cameraName' in CAMERA " + name);
            }

            string missingTextureName = string.Empty;
            if (config.TryGetValue("missingTexture", ref missingTextureName))
            {
                missingCameraTexture = GameDatabase.Instance.GetTexture(missingTextureName, false);
            }

            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            string range = string.Empty;
            if (config.TryGetValue("range", ref range))
            {
                string[] ranges = Utility.SplitVariableList(range);
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in CAMERA " + name);
                }
                range1 = comp.GetVariable(ranges[0], prop);
                range2 = comp.GetVariable(ranges[1], prop);

                rangeMode = true;
            }
            else
            {
                rangeMode = false;
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
            imageMaterial.mainTexture = cameraTexture;
            meshRenderer.material = imageMaterial;
            RenderPage(false);

            if (!string.IsNullOrEmpty(variableName))
            {
                currentState = false;
                // Disable the mesh if we're in variable mode
                imageObject.SetActive(false);
                variableRegistrar.RegisterNumericVariable(variableName, VariableCallback);
            }
            else
            {
                currentState = true;
                imageObject.SetActive(true);
            }

            if (!cameraTexture.IsCreated())
            {
                cameraTexture.Create();
            }
            cameraTexture.DiscardContents();
            ApplyMissingCamera();

            cameraSelector = comp.RegisterOnVariableChange(cameraName, prop, CameraSelectCallback);
            CameraSelectCallback();
        }

        /// <summary>
        /// Apply the "missing camera" effect.
        /// </summary>
        private void ApplyMissingCamera()
        {
            if (missingCameraTexture != null)
            {
                Graphics.Blit(missingCameraTexture, cameraTexture);
            }
            else
            {
                RenderTexture backup = RenderTexture.active;
                RenderTexture.active = cameraTexture;
                GL.Clear(true, true, new Color(0.016f, 0.016f, 0.031f));
                RenderTexture.active = backup;
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
                imageObject.SetActive(currentState);

                if (currentState == false)
                {
                    if (cameraTexture.IsCreated())
                    {
                        cameraTexture.Release();
                    }
                }
            }
        }

        /// <summary>
        /// Coroutine whose purpose is to re-attempt to select a camera when CameraSelectCallback returns null
        /// </summary>
        /// <returns></returns>
        private IEnumerator CameraSelectCoroutine()
        {
            coroutineActive = true;

            while (this.comp != null && activeCamera == null)
            {
                yield return MASConfig.waitForFixedUpdate;

                CameraSelectCallback();
            }

            coroutineActive = false;
        }

        /// <summary>
        /// Callback used to select active cameras
        /// </summary>
        private void CameraSelectCallback()
        {
            try
            {
                // This can return null at startup when the VC hasn't had a chance
                // to run.  Unfortunately, that means we have to call this callback
                // every time the camera should be enabled.
                MASCamera newCamera = comp.vc.FindCameraModule(cameraSelector.String());
                if (activeCamera != null)
                {
                    activeCamera.renderCallback -= ReadCamera;
                }
                activeCamera = newCamera;
                if (activeCamera != null)
                {
                    if (pageEnabled)
                    {
                        activeCamera.UpdateFlightComputer(comp);
                        activeCamera.renderCallback += ReadCamera;
                    }
                }
                else if (!coroutineActive)
                {
                    comp.StartCoroutine(CameraSelectCoroutine());
                }

                if (activeCamera == null)
                {
                    ApplyMissingCamera();
                }
            }
            catch
            {
                if (activeCamera != null)
                {
                    activeCamera.renderCallback -= ReadCamera;
                }

                activeCamera = null;
            }
        }

        /// <summary>
        /// Callback to process the rentex sent by the camera.
        /// </summary>
        /// <param name="rentex"></param>
        private void ReadCamera(RenderTexture rentex, Material postProcShader)
        {
            if (rentex == null)
            {
                activeCamera = null;
                return;
            }

            cameraTexture.DiscardContents();
            renderStopwatch.Start();
            if (postProcShader == null)
            {
                Graphics.Blit(rentex, cameraTexture);
            }
            else
            {
                Graphics.Blit(rentex, cameraTexture, postProcShader);
            }
            renderStopwatch.Stop();
            ++renderFrames;
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
        /// Called with `true` when the page is active on the monitor, called with
        /// `false` when the page is no longer active.
        /// </summary>
        /// <param name="enable">true when the page is actively displayed, false when the page
        /// is no longer displayed.</param>
        public override void SetPageActive(bool enable)
        {
            pageEnabled = enable;
            if (activeCamera != null)
            {
                if (enable)
                {
                    activeCamera.UpdateFlightComputer(comp);
                    activeCamera.renderCallback += ReadCamera;
                }
                else
                {
                    activeCamera.renderCallback -= ReadCamera;
                }
            }
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            this.comp = null;

            if (activeCamera != null)
            {
                activeCamera.renderCallback -= ReadCamera;
            }
            activeCamera = null;

            if (cameraTexture.IsCreated())
            {
                cameraTexture.Release();
            }
            UnityEngine.GameObject.Destroy(cameraTexture);
            cameraTexture = null;

            UnityEngine.GameObject.Destroy(imageObject);
            imageObject = null;
            UnityEngine.GameObject.Destroy(imageMaterial);
            imageMaterial = null;

            variableRegistrar.ReleaseResources();

            if (!string.IsNullOrEmpty(cameraSelector.name))
            {
                comp.UnregisterOnVariableChange(cameraSelector.name, internalProp, CameraSelectCallback);
            }

            if (renderFrames > 0)
            {
                double msPerFrame = 1000.0 * (double)(renderStopwatch.ElapsedTicks) / (double)(renderFrames * Stopwatch.Frequency);
                Utility.LogMessage(this, "Camera page {0}: {1} frames rendered, {2}/frame",
                    name, renderFrames, msPerFrame);
            }
        }
    }
}
