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
        private Material monitorMaterial;
        private MeshRenderer meshRenderer;
        private RenderTexture cameraTexture;
        private Texture missingCameraTexture;
        private Variable cameraSelector;
        private MASCamera activeCamera = null;
        private MASFlightComputer comp;
        private bool pageEnabled = false;
        private bool coroutineActive;
        private Stopwatch renderStopwatch = new Stopwatch();
        private long renderFrames = 0;

        private int rentexWidth, rentexHeight;
        private string[] propertyValue = new string[0];
        private int[] propertyId = new int[0];

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

            rentexWidth = ((int)size.x) >> MASConfig.CameraTextureScale;
            rentexHeight = ((int)size.y) >> MASConfig.CameraTextureScale;
            cameraTexture = new RenderTexture(rentexWidth, rentexHeight, 24, RenderTextureFormat.ARGB32);

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

            string shader = string.Empty;
            if (config.TryGetValue("shader", ref shader))
            {
                Shader ppShader;
                if (MASLoader.shaders.TryGetValue(shader, out ppShader))
                {
                    monitorMaterial = new Material(ppShader);
                }
            }
            if (monitorMaterial != null)
            {
                string textureName = string.Empty;
                if (config.TryGetValue("texture", ref textureName))
                {
                    Texture auxTexture = GameDatabase.Instance.GetTexture(textureName, false);
                    if (auxTexture == null)
                    {
                        throw new ArgumentException("Unable to find 'texture' " + textureName + " for CAMERA " + name);
                    }
                    monitorMaterial.SetTexture("_AuxTex", auxTexture);
                }

                string concatProperties = string.Empty;
                if (config.TryGetValue("properties", ref concatProperties))
                {
                    string[] propertiesList = concatProperties.Split(';');
                    int listLength = propertiesList.Length;
                    if (listLength > 0)
                    {
                        propertyId = new int[listLength];
                        propertyValue = new string[listLength];

                        for (int i = 0; i < listLength; ++i)
                        {
                            string[] pair = propertiesList[i].Split(':');
                            if (pair.Length != 2)
                            {
                                throw new ArgumentOutOfRangeException("Incorrect number of parameters for property: requires 2, found " + pair.Length + " in property " + propertiesList[i] + " for CAMERA " + name);
                            }
                            propertyId[i] = Shader.PropertyToID(pair[0].Trim());
                            propertyValue[i] = pair[1].Trim();
                        }
                        for (int i = 0; i < propertyValue.Length; ++i)
                        {
                            int id = propertyId[i];
                            variableRegistrar.RegisterVariableChangeCallback(propertyValue[i], (double newValue) => monitorMaterial.SetFloat(id, (float)newValue));
                        }
                    }
                }
            }

            imageMaterial = new Material(MASLoader.shaders["MOARdV/Monitor"]);
            imageMaterial.mainTexture = cameraTexture;
            meshRenderer.material = imageMaterial;
            RenderPage(false);

            if (!string.IsNullOrEmpty(variableName))
            {
                currentState = false;
                // Disable the mesh if we're in variable mode
                imageObject.SetActive(false);
                variableRegistrar.RegisterVariableChangeCallback(variableName, VariableCallback);
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

            cameraSelector = variableRegistrar.RegisterVariableChangeCallback(cameraName, CameraSelectCallback, false);
            CameraSelectCallback(0.0);
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
            if (EvaluateVariable(newValue))
            {
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

                CameraSelectCallback(0.0);
            }

            coroutineActive = false;
        }

        /// <summary>
        /// Callback used to select active cameras
        /// </summary>
        private void CameraSelectCallback(double dontCare)
        {
            try
            {
                // This can return null at startup when the VC hasn't had a chance
                // to run.  Unfortunately, that means we have to call this callback
                // every time the camera should be enabled.
                MASCamera newCamera = comp.vc.FindCameraModule(cameraSelector.AsString());
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
        /// <param name="rentex">The source texture.</param>
        /// <param name="postProcShader">The post-processing shader applied by the current camera's active mode.</param>
        private void ReadCamera(RenderTexture rentex, Material postProcShader)
        {
            if (rentex == null)
            {
                activeCamera = null;
                return;
            }

            cameraTexture.DiscardContents();
            renderStopwatch.Start();
            if (monitorMaterial == null)
            {
                if (postProcShader == null)
                {
                    Graphics.Blit(rentex, cameraTexture);
                }
                else
                {
                    Graphics.Blit(rentex, cameraTexture, postProcShader);
                }
            }
            else
            {
                if (postProcShader == null)
                {
                    Graphics.Blit(rentex, cameraTexture, monitorMaterial);
                }
                else
                {
                    RenderTexture tmp = RenderTexture.GetTemporary(rentexWidth, rentexHeight, 24);
                    Graphics.Blit(rentex, tmp, postProcShader);
                    Graphics.Blit(tmp, cameraTexture, monitorMaterial);
                    tmp.Release();
                }
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
            variableRegistrar.EnableCallbacks(enable);
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

            //if (renderFrames > 0)
            //{
            //    double msPerFrame = 1000.0 * (double)(renderStopwatch.ElapsedTicks) / (double)(renderFrames * Stopwatch.Frequency);
            //    Utility.LogMessage(this, "Camera page {0}: {1} frames rendered, {2:0.0}ms/frame",
            //        name, renderFrames, msPerFrame);
            //}
        }
    }
}
