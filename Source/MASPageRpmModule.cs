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
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// The MASPageRpmModule provides an interface to legacy RPM modules, allowing
    /// them to be used in a MAS IVA without also using RPM.
    /// </summary>
    internal class MASPageRpmModule : IMASMonitorComponent
    {
        private string name = "anonymous";

        private VariableRegistrar variableRegistrar;
        private GameObject imageObject;
        private MeshRenderer meshRenderer;
        private Material imageMaterial;
        private RenderTexture displayTexture;
        private RenderTexture renderTexture;

        private object rpmModule;
        private DynamicMethod<object, RenderTexture, float> renderMethod;
        private DynamicMethod<object, bool, int> pageActiveMethod;
        private DynamicMethod<object, int> buttonClickMethod;

        private MASFlightComputer.Variable range1, range2;
        private readonly bool rangeMode;
        private bool currentState;
        private bool pageEnabled;
        private bool coroutineActive;
        private MASFlightComputer comp;

        internal MASPageRpmModule(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            variableRegistrar = new VariableRegistrar(comp, prop);

            this.comp = comp;
            if (!config.TryGetValue("name", ref name))
            {
                name = "anonymous";
            }

            Vector2 position = Vector2.zero;
            if (!config.TryGetValue("position", ref position))
            {
                position = Vector2.zero;
            }

            Vector2 size = Vector2.zero;
            if (!config.TryGetValue("size", ref size))
            {
                size = monitor.screenSize;
            }

            string variableName = string.Empty;
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

            displayTexture = new RenderTexture(((int)size.x) >> MASConfig.CameraTextureScale, ((int)size.y) >> MASConfig.CameraTextureScale, 24, RenderTextureFormat.ARGB32);
            displayTexture.Create();

            string moduleNameString = string.Empty;
            if (!config.TryGetValue("moduleName", ref moduleNameString))
            {
                throw new ArgumentException("Unable to find 'moduleName' in RPM_MODULE " + name);
            }

            bool moduleFound = false;

            int numModules = prop.internalModules.Count;
            int moduleIndex;
            for (moduleIndex = 0; moduleIndex < numModules; ++moduleIndex)
            {
                if (prop.internalModules[moduleIndex].ClassName == moduleNameString)
                {
                    moduleFound = true;
                    break;
                }
            }

            if (moduleFound)
            {
                rpmModule = prop.internalModules[moduleIndex];
                Type moduleType = prop.internalModules[moduleIndex].GetType();

                string renderMethodName = string.Empty;
                if (config.TryGetValue("renderMethod", ref renderMethodName))
                {
                    MethodInfo method = moduleType.GetMethod(renderMethodName);
                    if (method != null && method.GetParameters().Length == 2 && method.GetParameters()[0].ParameterType == typeof(RenderTexture) && method.GetParameters()[1].ParameterType == typeof(float))
                    {
                        renderMethod = DynamicMethodFactory.CreateFunc<object, RenderTexture, float>(method);
                    }

                    if (renderMethod != null)
                    {
                        Vector2 renderSize = Vector2.zero;
                        if (!config.TryGetValue("renderSize", ref renderSize))
                        {
                            renderSize = size;
                        }

                        if (renderSize != size)
                        {
                            renderTexture = new RenderTexture((int)renderSize.x, (int)renderSize.y, 24, RenderTextureFormat.ARGB32);
                            renderTexture.Create();
                        }
                    }
                    else
                    {
                        throw new ArgumentException("Unable to initialize 'renderMethod' " + renderMethodName + " in RPM_MODULE " + name);
                    }
                }

                string pageActiveMethodName = string.Empty;
                if (config.TryGetValue("pageActiveMethod", ref pageActiveMethodName))
                {
                    MethodInfo method = moduleType.GetMethod(pageActiveMethodName);
                    if (method != null && method.GetParameters().Length == 2 && method.GetParameters()[0].ParameterType == typeof(bool) && method.GetParameters()[1].ParameterType == typeof(int))
                    {
                        pageActiveMethod = DynamicMethodFactory.CreateFunc<object, bool, int>(method);
                    }
                    else
                    {
                        throw new ArgumentException("Unable to initialize 'pageActiveMethod' " + pageActiveMethodName + " in RPM_MODULE " + name);
                    }
                }

                string buttonClickMethodName = string.Empty;
                if (config.TryGetValue("buttonClickMethod", ref buttonClickMethodName))
                {
                    MethodInfo method = moduleType.GetMethod(buttonClickMethodName);
                    if (method != null && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(int))
                    {
                        buttonClickMethod = DynamicMethodFactory.CreateFunc<object, int>(method);
                    }
                }
            }
            else
            {
                string textureName = string.Empty;
                if (config.TryGetValue("texture", ref textureName))
                {
                    Texture missingTexture = GameDatabase.Instance.GetTexture(textureName, false);
                    if (missingTexture != null)
                    {
                        Graphics.Blit(missingTexture, displayTexture);
                    }
                }
            }

            // Set up our surface.
            imageObject = new GameObject();
            imageObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
            imageObject.layer = pageRoot.gameObject.layer;
            imageObject.transform.parent = pageRoot;
            imageObject.transform.position = pageRoot.position;
            imageObject.transform.Translate(monitor.screenSize.x * -0.5f + position.x + size.x * 0.5f, monitor.screenSize.y * 0.5f - position.y - size.y * 0.5f, depth);
            // add renderer stuff
            MeshFilter meshFilter = imageObject.AddComponent<MeshFilter>();
            meshRenderer = imageObject.AddComponent<MeshRenderer>();
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
                {
                    new Vector3(-0.5f * size.x, 0.5f * size.y, depth),
                    new Vector3(0.5f * size.x, 0.5f * size.y, depth),
                    new Vector3(-0.5f * size.x, -0.5f * size.y, depth),
                    new Vector3(0.5f * size.x, -0.5f * size.y, depth),
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

            imageMaterial = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));
            imageMaterial.mainTexture = displayTexture;
            meshRenderer.material = imageMaterial;
            EnableRender(false);

            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the mesh if we're in variable mode
                imageObject.SetActive(false);
                //comp.RegisterNumericVariable(variableName, prop, VariableCallback);
                variableRegistrar.RegisterNumericVariable(variableName, VariableCallback);
            }
            else
            {
                currentState = true;
                imageObject.SetActive(true);
            }

            if (renderMethod != null)
            {
                comp.StartCoroutine(QueryModule());
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

                if (pageActiveMethod != null)
                {
                    pageActiveMethod(rpmModule, pageEnabled && currentState, 0);
                }

                if (pageEnabled && currentState && !coroutineActive)
                {
                    if (comp)
                    {
                        comp.StartCoroutine(QueryModule());
                    }
                }
            }
        }

        /// <summary>
        /// Coroutine whose purpose is to update plugin-renderable data.
        /// </summary>
        /// <returns></returns>
        private IEnumerator QueryModule()
        {
            coroutineActive = true;

            while (pageEnabled && currentState)
            {
                yield return MASConfig.waitForFixedUpdate;

                try
                {
                    if (renderMethod != null)
                    {
                        RenderTexture renderTo;
                        if (renderTexture != null)
                        {
                            renderTo = renderTexture;
                        }
                        else
                        {
                            renderTo = displayTexture;
                        }

                        RenderTexture backupRenderTexture = null;
                        // TODO: Can I make the RenderTexture backup / GL stuff
                        // optional?
                        backupRenderTexture = RenderTexture.active;
                        RenderTexture.active = renderTo;

                        renderTo.DiscardContents();

                        GL.PushMatrix();
                        GL.LoadPixelMatrix(0, renderTo.width, renderTo.height, 0);

                        GL.Clear(true, true, Color.black);

                        renderMethod(rpmModule, renderTo, 1.0f);

                        GL.PopMatrix();

                        if (backupRenderTexture != null)
                        {
                            RenderTexture.active = backupRenderTexture;
                        }

                        if (renderTexture != null)
                        {
                            Graphics.Blit(renderTexture, displayTexture);
                        }
                    }
                }
                catch
                {
                    // We may start querying before the methods in question are ready.
                }

            }

            coroutineActive = false;
        }

        /// <summary>
        /// Enable / disable renderer components without disabling game objects.
        /// </summary>
        /// <param name="enable"></param>
        public void EnableRender(bool enable)
        {
            meshRenderer.enabled = enable;
        }

        /// <summary>
        /// Enables / disables overall page rendering.
        /// </summary>
        /// <param name="enable"></param>
        public void EnablePage(bool enable)
        {
            pageEnabled = enable;

            if (pageActiveMethod != null)
            {
                pageActiveMethod(rpmModule, pageEnabled && currentState, 0);
            }

            if (pageEnabled && currentState && !coroutineActive)
            {
                if (comp)
                {
                    comp.StartCoroutine(QueryModule());
                }
            }
            //if (activeCamera != null)
            //{
            //    if (enable)
            //    {
            //        activeCamera.renderCallback += ReadCamera;
            //    }
            //    else
            //    {
            //        activeCamera.renderCallback -= ReadCamera;
            //    }
            //}
        }

        /// <summary>
        /// Handle a softkey event.
        /// </summary>
        /// <param name="keyId">The numeric ID of the key to handle.</param>
        /// <returns>true if the component handled the key, false otherwise.</returns>
        public bool HandleSoftkey(int keyId)
        {
            if (buttonClickMethod != null)
            {
                buttonClickMethod(rpmModule, keyId);
                return true;
            }
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
            renderMethod = null;
            pageActiveMethod = null;
            buttonClickMethod = null;
            pageEnabled = false;
            this.comp = null;

            UnityEngine.GameObject.Destroy(imageObject);
            imageObject = null;
            UnityEngine.GameObject.Destroy(imageMaterial);
            imageMaterial = null;
            if (displayTexture != null)
            {
                if (displayTexture.IsCreated())
                {
                    displayTexture.Release();
                }
                UnityEngine.GameObject.Destroy(displayTexture);
                displayTexture = null;
            }
            if (renderTexture != null)
            {
                if (renderTexture.IsCreated())
                {
                    renderTexture.Release();
                }
                UnityEngine.GameObject.Destroy(renderTexture);
                renderTexture = null;
            }
            //if (!string.IsNullOrEmpty(variableName))
            //{
            //    comp.UnregisterNumericVariable(variableName, internalProp, VariableCallback);
            //}

            variableRegistrar.ReleaseResources(comp, internalProp);
        }
    }
}
