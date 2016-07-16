/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016 MOARdV
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
    /// The navball component renders a NavBall for a monitor.
    /// </summary>
    internal class MASPageNavBall : IMASSubComponent
    {
        private string name = "(anonymous)";
        private string text = string.Empty;

        private GameObject imageObject;
        private GameObject navballModel;
        private RenderTexture navballRenTex;
        private Camera navballCamera;
        private Material imageMaterial;
        private Material navballMaterial;
        private string variableName;
        private MASFlightComputer.Variable range1, range2;
        private readonly bool rangeMode;
        private bool currentState;
        private MASFlightComputer comp;

        internal MASPageNavBall(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            this.comp = comp;
            if (!config.TryGetValue("name", ref name))
            {
                name = "(anonymous)";
            }

            string modelName = string.Empty;
            if (!config.TryGetValue("model", ref modelName))
            {
                throw new ArgumentException("Unable to find 'model' in NAVBALL " + name);
            }
            navballModel = GameDatabase.Instance.GetModel(modelName);
            if (navballModel == null)
            {
                throw new ArgumentException("Unable to find 'model' " + modelName + " for NAVBALL " + name);
            }
            float navballExtents;
            try
            {
                Vector3 extents =navballModel.GetComponent<MeshFilter>().mesh.bounds.extents;
                navballExtents = Mathf.Max(extents.x, extents.y) * 1.01f;
            }
            catch
            {
                navballExtents = 1.0f;
            }
            //Utility.LogMessage(this, "navballExtents -> {0}", navballExtents);

            string textureName = string.Empty;
            if (!config.TryGetValue("texture", ref textureName))
            {
                throw new ArgumentException("Unable to find 'texture' in NAVBALL " + name);
            }
            Texture2D navballTexture = GameDatabase.Instance.GetTexture(textureName, false);
            if (navballTexture == null)
            {
                throw new ArgumentException("Unable to find 'texture' " + textureName + " for NAVBALL " + name);
            }

            Vector2 position = Vector2.zero;
            if (!config.TryGetValue("position", ref position))
            {
                position = monitor.screenSize * 0.5f;
            }

            Vector2 size = Vector2.zero;
            if (!config.TryGetValue("size", ref size))
            {
                throw new ArgumentException("Unable to find 'size' in NAVBALL " + name);
            }
            size = size * 0.5f;

            float opacity = 1.0f;
            if(!config.TryGetValue("opacity", ref opacity))
            {
                opacity = 1.0f;
            }
            else
            {
                opacity = Mathf.Clamp01(opacity);
            }

            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            string range = string.Empty;
            if (config.TryGetValue("range", ref range))
            {
                string[] ranges = range.Split(',');
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in TEXT " + name);
                }
                range1 = comp.GetVariable(ranges[0], prop);
                range2 = comp.GetVariable(ranges[1], prop);

                rangeMode = true;
            }
            else
            {
                rangeMode = false;
            }

            // Set up our navball renderer
            Shader displayShader = Shader.Find("KSP/Alpha/Unlit Transparent");
            navballRenTex = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            navballRenTex.Create();
            navballRenTex.DiscardContents();

            // Set up our display surface.
            imageObject = new GameObject();
            imageObject.name = pageRoot.gameObject.name + "-MASPageNavBall-" + name + "-" + depth.ToString();
            imageObject.layer = pageRoot.gameObject.layer;
            imageObject.transform.parent = pageRoot;
            imageObject.transform.position = pageRoot.position;
            imageObject.transform.Translate(monitor.screenSize.x * -0.5f + position.x, monitor.screenSize.y * 0.5f - position.y, depth);
            // add renderer stuff
            MeshFilter meshFilter = imageObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = imageObject.AddComponent<MeshRenderer>();
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
                {
                    new Vector3(-size.x, size.y, depth),
                    new Vector3(size.x, size.y, depth),
                    new Vector3(-size.x, -size.y, depth),
                    new Vector3(size.x, -size.y, depth),
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
            mesh.Optimize();
            mesh.UploadMeshData(true);
            meshFilter.mesh = mesh;
            imageMaterial = new Material(displayShader);
            imageMaterial.mainTexture = navballRenTex;
            meshRenderer.material = imageMaterial;

            navballCamera = imageObject.AddComponent<Camera>();
            navballCamera.enabled = true;
            navballCamera.orthographic = true;
            navballCamera.aspect = 1.0f;
            navballCamera.eventMask = 0;
            navballCamera.farClipPlane = 13.0f;
            navballCamera.orthographicSize = navballExtents;
            navballCamera.cullingMask = 1 << 29;
            // TODO: Different shader... clearing to a=0 hides the navball
            navballCamera.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);
            //navballCamera.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            navballCamera.clearFlags = CameraClearFlags.SolidColor;
            navballCamera.transparencySortMode = TransparencySortMode.Orthographic;
            navballCamera.transform.LookAt(navballCamera.transform.position + new Vector3(0.0f, 0.0f, 1.0f), Vector3.up);
            navballCamera.targetTexture = navballRenTex;
            Camera.onPreRender += CameraPrerender;

            navballModel.layer = 29;
            navballModel.transform.parent = imageObject.transform;
            // TODO: this isn't working when the camera is shifted.
            navballModel.transform.Translate(new Vector3(0.0f, 0.0f, 2.4f));
            Renderer navballRenderer = null;
            navballMaterial = navballModel.GetComponentCached<Renderer>(ref navballRenderer).material;
            navballMaterial.shader = displayShader;
            navballMaterial.mainTexture = navballTexture;
            navballMaterial.SetFloat("_Opacity", opacity);
            navballRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            navballModel.SetActive(true);

            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the mesh if we're in variable mode
                imageObject.SetActive(false);
                comp.RegisterNumericVariable(variableName, prop, VariableCallback);
            }
            else
            {
                imageObject.SetActive(true);
            }
        }

        private void CameraPrerender(Camera whichCamera)
        {
            if(whichCamera == navballCamera)
            {
                if (!navballRenTex.IsCreated())
                {
                    navballRenTex.Create();
                    navballCamera.targetTexture = navballRenTex;
                    imageMaterial.mainTexture = navballRenTex;
                }
                // Apply navball gimbal
                navballModel.transform.rotation = comp.vc.navBallRotation;
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
            }
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
            Camera.onPreRender -= CameraPrerender;
            UnityEngine.GameObject.Destroy(imageObject);
            imageObject = null;
            UnityEngine.GameObject.Destroy(imageMaterial);
            imageMaterial = null;
            UnityEngine.GameObject.Destroy(navballModel);
            navballModel = null;
            UnityEngine.GameObject.Destroy(navballMaterial);
            navballMaterial = null;
            if (!string.IsNullOrEmpty(variableName))
            {
                comp.UnregisterNumericVariable(variableName, internalProp, VariableCallback);
            }
            this.comp = null;
        }
    }
}
