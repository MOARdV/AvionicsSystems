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
    internal class MASPageHorizon : IMASMonitorComponent
    {
        private string name = "(anonymous)";
        private GameObject imageObject;
        private Material imageMaterial;
        private readonly string variableName;
        private readonly string pitchName;
        private readonly string rollName;
        private readonly float textureOffset;
        private readonly Vector2 texelSize;
        private MASFlightComputer.Variable range1, range2;
        private readonly MASFlightComputer.Variable pitchRange1, pitchRange2;
        private readonly MASFlightComputer.Variable displayPitchRange1, displayPitchRange2;
        private readonly MASFlightComputer.Variable rollRange1, rollRange2;
        private readonly MASFlightComputer.Variable displayRollRange1, displayRollRange2;
        private readonly bool rangeMode;
        private bool currentState;
        private float lastRoll = 0.0f;
        private float oldPitchCenter = -1.0f;

        internal MASPageHorizon(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "(anonymous)";
            }

            string textureName = string.Empty;
            if (!config.TryGetValue("texture", ref textureName))
            {
                throw new ArgumentException("Unable to find 'texture' in HORIZON " + name);
            }
            Texture2D mainTexture = GameDatabase.Instance.GetTexture(textureName, false);
            if (mainTexture == null)
            {
                throw new ArgumentException("Unable to find 'texture' " + textureName + " for HORIZON " + name);
            }
            mainTexture.wrapMode = TextureWrapMode.Clamp;

            Vector2 position = Vector2.zero;
            if (!config.TryGetValue("position", ref position))
            {
                throw new ArgumentException("Unable to find 'position' in HORIZON " + name);
            }

            Vector2 size = Vector2.zero;
            if (!config.TryGetValue("size", ref size))
            {
                throw new ArgumentException("Unable to find 'size' in HORIZON " + name);
            }

            if (!config.TryGetValue("pitch", ref pitchName))
            {
                throw new ArgumentException("Unable to find 'pitch' in HORIZON " + name);
            }

            string pitchRange = string.Empty;
            if (!config.TryGetValue("pitchRange", ref pitchRange))
            {
                throw new ArgumentException("Unable to find 'pitchRange' in HORIZON " + name);
            }
            string[] ranges = pitchRange.Split(',');
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Incorrect number of values in 'pitchRange' in HORIZON " + name);
            }
            pitchRange1 = comp.GetVariable(ranges[0], prop);
            pitchRange2 = comp.GetVariable(ranges[1], prop);
            string displayPitchRange = string.Empty;
            if (!config.TryGetValue("displayPitchRange", ref displayPitchRange))
            {
                throw new ArgumentException("Unable to find 'displayPitchRange' in HORIZON " + name);
            }
            ranges = displayPitchRange.Split(',');
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Incorrect number of values in 'displayPitchRange' in HORIZON " + name);
            }
            displayPitchRange1 = comp.GetVariable(ranges[0], prop);
            displayPitchRange2 = comp.GetVariable(ranges[1], prop);

            if (!config.TryGetValue("roll", ref rollName))
            {
                throw new ArgumentException("Unable to find 'roll' in HORIZON " + name);
            }

            string rollRange = string.Empty;
            if (!config.TryGetValue("rollRange", ref rollRange))
            {
                throw new ArgumentException("Unable to find 'rollRange' in HORIZON " + name);
            }
            ranges = rollRange.Split(',');
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Incorrect number of values in 'rollRange' in HORIZON " + name);
            }
            rollRange1 = comp.GetVariable(ranges[0], prop);
            rollRange2 = comp.GetVariable(ranges[1], prop);
            string displayRollRange = string.Empty;
            if (!config.TryGetValue("displayRollRange", ref displayRollRange))
            {
                throw new ArgumentException("Unable to find 'displayRollRange' in HORIZON " + name);
            }
            ranges = displayRollRange.Split(',');
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Incorrect number of values in 'displayRollRange' in HORIZON " + name);
            }
            displayRollRange1 = comp.GetVariable(ranges[0], prop);
            displayRollRange2 = comp.GetVariable(ranges[1], prop);

            texelSize = mainTexture.texelSize;

            // Infer the scaling of the source image to the display size, fixing
            // the width as 100% of the texel width of the source.
            float texelsPerPixel = (float)mainTexture.width / size.x;
            // Get the inverse aspect ratio of the display
            float aspectRatio = size.y / size.x;
            float texelsHeight = aspectRatio * size.y * texelsPerPixel;
            Vector2 textureScale = new Vector2(1.0f, texelsHeight * texelSize.y);
            textureOffset = 0.5f * texelsHeight * texelSize.y;

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
                    throw new ArgumentException("Incorrect number of values in 'range' in HORIZON " + name);
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
            imageObject = new GameObject();
            imageObject.name = pageRoot.gameObject.name + "-MASPageHorizon-" + name + "-" + depth.ToString();
            imageObject.layer = pageRoot.gameObject.layer;
            imageObject.transform.parent = pageRoot;
            imageObject.transform.position = pageRoot.position;
            imageObject.transform.Translate(position.x, -position.y, depth);

            // Determine the extents of the horizon in clip coordinates for
            // feeding the shader.
            Vector4 clipCoords = new Vector4(
                position.x - size.x * 0.5f, position.y - size.y * 0.5f,
                position.x + size.x * 0.5f, position.y + size.y * 0.5f
                );

            clipCoords.x /= monitor.screenSize.x * 0.5f;
            clipCoords.y /= monitor.screenSize.y * 0.5f;
            clipCoords.z /= monitor.screenSize.x * 0.5f;
            clipCoords.w /= monitor.screenSize.y * 0.5f;

            // add renderer stuff
            MeshFilter meshFilter = imageObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = imageObject.AddComponent<MeshRenderer>();
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
                {
                    new Vector3(-size.x*0.75f, size.y*0.75f, depth),
                    new Vector3(size.x*0.75f, size.y*0.75f, depth),
                    new Vector3(-size.x*0.75f, -size.y*0.75f, depth),
                    new Vector3(size.x*0.75f, -size.y*0.75f, depth),
                };
            mesh.uv = new[]
                {
                    new Vector2(-0.5f, 1.5f),
                    new Vector2(1.5f, 1.5f),
                    new Vector2(-0.5f, -0.5f),
                    new Vector2(1.5f, -0.5f),
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
            Shader imageShader = MASLoader.shaders["MOARdV/Monitor"];
            imageMaterial = new Material(imageShader);
            imageMaterial.mainTexture = mainTexture;
            imageMaterial.mainTextureScale = textureScale;
            meshRenderer.material = imageMaterial;
            imageMaterial.SetVector("_ClipCoords", clipCoords);

            comp.RegisterNumericVariable(pitchName, prop, PitchCallback);
            comp.RegisterNumericVariable(rollName, prop, RollCallback);
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

        /// <summary>
        /// Update the texture roll.
        /// </summary>
        /// <param name="newValue"></param>
        private void RollCallback(double newValue)
        {
            float iLerp = Mathf.InverseLerp((float)rollRange1.SafeValue(), (float)rollRange2.SafeValue(), (float)newValue);
            float newRoll = Mathf.Lerp((float)displayRollRange1.SafeValue(), (float)displayRollRange2.SafeValue(), iLerp);
            
            if (!Mathf.Approximately(newRoll, lastRoll))
            {
                imageObject.transform.Rotate(Vector3.forward, newRoll - lastRoll);
                lastRoll = newRoll;
            }
        }

        /// <summary>
        /// Update the texture offset.  We do this be inverse-lerping the
        /// input variable and lerping it into the scaled output variable.
        /// </summary>
        /// <param name="newValue"></param>
        private void PitchCallback(double newValue)
        {
            float iLerp = Mathf.InverseLerp((float)pitchRange1.SafeValue(), (float)pitchRange2.SafeValue(), (float)newValue);
            float newCenter = Mathf.Lerp((float)displayPitchRange1.SafeValue() * texelSize.y, (float)displayPitchRange2.SafeValue() * texelSize.y, iLerp);

            if (!Mathf.Approximately(newCenter, oldPitchCenter))
            {
                imageMaterial.mainTextureOffset = new Vector2(0.0f, 1.0f - (newCenter + textureOffset));
                oldPitchCenter = newCenter;
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
        /// Enable / disable renderer components without disabling game objects.
        /// </summary>
        /// <param name="enable"></param>
        public void EnableRender(bool enable)
        {

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

            comp.UnregisterNumericVariable(pitchName, internalProp, PitchCallback);
            comp.UnregisterNumericVariable(rollName, internalProp, RollCallback);
            if (!string.IsNullOrEmpty(variableName))
            {
                comp.UnregisterNumericVariable(variableName, internalProp, VariableCallback);
            }
        }
    }
}
