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
        private string name = "anonymous";

        private VariableRegistrar variableRegistrar;
        private GameObject imageObject;
        private Material imageMaterial;
        private MeshRenderer meshRenderer;
        private Color passiveColor, activeColor;
        private float currentBlend;
        private MASFlightComputer.Variable colorRange1, colorRange2;
        private readonly int colorField = Shader.PropertyToID("_Color");

        private MASFlightComputer.Variable range1, range2;
        private readonly bool rangeMode;
        private bool currentState;

        internal MASPageImage(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            variableRegistrar = new VariableRegistrar(comp, prop);

            if (!config.TryGetValue("name", ref name))
            {
                name = "anonymous";
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
                    throw new ArgumentException("Incorrect number of values in 'range' in IMAGE " + name);
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
            string rotationVariableName = string.Empty;
            if (config.TryGetValue("rotation", ref rotationVariableName))
            {
                config.TryGetValue("rotationOffset", ref rotationOffset);
            }

            // Set up our surface.
            imageObject = new GameObject();
            imageObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
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

            currentBlend = 0.0f;

            passiveColor = Color.white;
            activeColor = Color.white;

            string passiveColorName = string.Empty;
            if (config.TryGetValue("passiveColor", ref passiveColorName))
            {
                Color32 color32;
                if (comp.TryGetNamedColor(passiveColorName, out color32))
                {
                    passiveColor = color32;
                }
                else
                {
                    string[] startColors = Utility.SplitVariableList(passiveColorName);
                    if (startColors.Length < 3 || startColors.Length > 4)
                    {
                        throw new ArgumentException("'passiveColor' does not contain 3 or 4 values in IMAGE " + name);
                    }

                    variableRegistrar.RegisterNumericVariable(startColors[0], (double newValue) =>
                    {
                        passiveColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        UpdateColor();
                    });

                    variableRegistrar.RegisterNumericVariable(startColors[1], (double newValue) =>
                    {
                        passiveColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        UpdateColor();
                    });

                    variableRegistrar.RegisterNumericVariable(startColors[2], (double newValue) =>
                    {
                        passiveColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        UpdateColor();
                    });

                    if (startColors.Length == 4)
                    {
                        variableRegistrar.RegisterNumericVariable(startColors[3], (double newValue) =>
                        {
                            passiveColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            UpdateColor();
                        });
                    }
                }
            }

            string colorVariableName = string.Empty;
            if (config.TryGetValue("colorVariable", ref colorVariableName))
            {
                if (string.IsNullOrEmpty(passiveColorName))
                {
                    throw new ArgumentException("'colorVariable' found, but no 'passiveColor' in IMAGE " + name);
                }

                string activeColorName = string.Empty;
                if (!config.TryGetValue("activeColor", ref activeColorName))
                {
                    throw new ArgumentException("'colorVariable' found, but no 'activeColor' in IMAGE " + name);
                }

                string colorRangeString = string.Empty;
                if (config.TryGetValue("colorRange", ref colorRangeString))
                {
                    string[] colorRanges = Utility.SplitVariableList(colorRangeString);
                    if (colorRanges.Length != 2)
                    {
                        throw new ArgumentException("Expected 2 values for 'colorRange' in IMAGE " + name);
                    }

                    colorRange1 = comp.GetVariable(colorRanges[0], prop);
                    colorRange2 = comp.GetVariable(colorRanges[1], prop);

                    bool colorBlend = false;
                    if (config.TryGetValue("colorBlend", ref colorBlend) && colorBlend == true)
                    {
                        variableRegistrar.RegisterNumericVariable(colorVariableName, (double newValue) =>
                        {
                            float newBlend = Mathf.InverseLerp((float)colorRange1.SafeValue(), (float)colorRange2.SafeValue(), (float)newValue);

                            if (!Mathf.Approximately(newBlend, currentBlend))
                            {
                                currentBlend = newBlend;
                                UpdateColor();
                            }
                        });
                    }
                    else
                    {
                        variableRegistrar.RegisterNumericVariable(colorVariableName, (double newValue) =>
                        {
                            float newBlend = (newValue.Between(colorRange1.SafeValue(), colorRange2.SafeValue())) ? 1.0f : 0.0f;
                            if (newBlend != currentBlend)
                            {
                                currentBlend = newBlend;
                                UpdateColor();
                            }
                        });
                    }
                }
                else
                {
                    variableRegistrar.RegisterNumericVariable(colorVariableName, (double newValue) =>
                        {
                            float newBlend = (newValue > 0.0) ? 1.0f : 0.0f;
                            if (newBlend != currentBlend)
                            {
                                currentBlend = newBlend;
                                UpdateColor();
                            }
                        });
                }

                Color32 color32;
                if (comp.TryGetNamedColor(activeColorName, out color32))
                {
                    activeColor = color32;
                }
                else
                {
                    string[] activeColors = Utility.SplitVariableList(activeColorName);
                    if (activeColors.Length < 3 || activeColors.Length > 4)
                    {
                        throw new ArgumentException("'activeColor' does not contain 3 or 4 values in IMAGE " + name);
                    }

                    variableRegistrar.RegisterNumericVariable(activeColors[0], (double newValue) =>
                    {
                        activeColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        UpdateColor();
                    });

                    variableRegistrar.RegisterNumericVariable(activeColors[1], (double newValue) =>
                    {
                        activeColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        UpdateColor();
                    });

                    variableRegistrar.RegisterNumericVariable(activeColors[2], (double newValue) =>
                    {
                        activeColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        UpdateColor();
                    });

                    if (activeColors.Length == 4)
                    {
                        variableRegistrar.RegisterNumericVariable(activeColors[3], (double newValue) =>
                        {
                            activeColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            UpdateColor();
                        });
                    }
                }
            }

            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the mesh if we're in variable mode
                imageObject.SetActive(false);
                variableRegistrar.RegisterNumericVariable(variableName, VariableCallback);
            }
            else
            {
                imageObject.SetActive(true);
            }

            if (!string.IsNullOrEmpty(rotationVariableName))
            {
                variableRegistrar.RegisterNumericVariable(rotationVariableName, RotationCallback);
            }
        }

        /// <summary>
        /// Update the image's master color.
        /// </summary>
        private void UpdateColor()
        {
            Color newColor = Color.Lerp(passiveColor, activeColor, currentBlend);
            imageMaterial.SetColor(colorField, newColor);
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
            variableRegistrar.ReleaseResources(comp, internalProp);

            UnityEngine.GameObject.Destroy(imageObject);
            imageObject = null;
            UnityEngine.GameObject.Destroy(imageMaterial);
            imageMaterial = null;
        }
    }
}
