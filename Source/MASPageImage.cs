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
    /// <summary>
    /// MASPageImage renders an arbitrary image on the display.  Size and position
    /// are configurable.
    /// </summary>
    internal class MASPageImage : IMASMonitorComponent
    {
        private string name = "(anonymous)";

        private GameObject imageObject;
        private Material imageMaterial;
        private MeshRenderer meshRenderer;
        private string variableName;
        private MASFlightComputer.Variable range1, range2;
        private string rotationVariableName;
        private readonly bool rangeMode;
        private bool currentState;

        internal MASPageImage(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "(anonymous)";
            }

            string textureName = string.Empty;
            if (!config.TryGetValue("texture", ref textureName))
            {
                throw new ArgumentException("Unable to find 'texture' in IMAGE " + name);
            }
            if (textureName == "%FLAG%")
            {
                textureName = prop.part.flagURL;
            }
            Texture2D mainTexture = GameDatabase.Instance.GetTexture(textureName, false);
            if (mainTexture == null)
            {
                throw new ArgumentException("Unable to find 'texture' " + textureName + " for IMAGE " + name);
            }

            Vector2 position = Vector2.zero;
            if (!config.TryGetValue("position", ref position))
            {
                position = Vector2.zero;
            }

            Vector2 size = Vector2.zero;
            if (!config.TryGetValue("size", ref size))
            {
                size = new Vector2(mainTexture.width, mainTexture.height);
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

            Vector2 rotationOffset = Vector2.zero;
            if (config.TryGetValue("rotation", ref rotationVariableName))
            {
                config.TryGetValue("rotationOffset", ref rotationOffset);
            }

            // Set up our surface.
            imageObject = new GameObject();
            imageObject.name = pageRoot.gameObject.name + "-MASPageImage-" + name + "-" + depth.ToString();
            imageObject.layer = pageRoot.gameObject.layer;
            imageObject.transform.parent = pageRoot;
            imageObject.transform.position = pageRoot.position;
            imageObject.transform.Translate(monitor.screenSize.x * -0.5f + position.x + rotationOffset.x + size.x * 0.5f, monitor.screenSize.y * 0.5f - position.y - rotationOffset.y - size.y * 0.5f, depth);
            // add renderer stuff
            MeshFilter meshFilter = imageObject.AddComponent<MeshFilter>();
            meshRenderer = imageObject.AddComponent<MeshRenderer>();
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
                {
                    new Vector3(-0.5f * size.x - rotationOffset.x, 0.5f * size.y + rotationOffset.y, depth),
                    new Vector3(0.5f * size.x - rotationOffset.x, 0.5f * size.y+ rotationOffset.y, depth),
                    new Vector3(-0.5f * size.x - rotationOffset.x, -0.5f * size.y+ rotationOffset.y, depth),
                    new Vector3(0.5f * size.x - rotationOffset.x, -0.5f * size.y+ rotationOffset.y, depth),
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

            imageMaterial = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));
            imageMaterial.mainTexture = mainTexture;
            meshRenderer.material = imageMaterial;
            EnableRender(false);

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

            if (!string.IsNullOrEmpty(rotationVariableName))
            {
                comp.RegisterNumericVariable(rotationVariableName, prop, RotationCallback);
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
        /// Apply a rotation to the image.
        /// </summary>
        /// <param name="newValue"></param>
        private void RotationCallback(double newValue)
        {
            newValue = newValue % 360.0;
            imageObject.transform.localRotation = Quaternion.Euler(new Vector3(0.0f, 0.0f, (float)newValue));
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
            if (!string.IsNullOrEmpty(variableName))
            {
                comp.UnregisterNumericVariable(variableName, internalProp, VariableCallback);
            }
            if (!string.IsNullOrEmpty(rotationVariableName))
            {
                comp.UnregisterNumericVariable(rotationVariableName, internalProp, RotationCallback);
            }
        }
    }
}
