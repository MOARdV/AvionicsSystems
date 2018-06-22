/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2018 MOARdV
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
    class MASPageRollingDigit : IMASMonitorComponent
    {
        private GameObject meshObject;
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;

        private Font font;
        private bool dynamic;
        private int fontSize;
        private float characterScalar = 1.0f;
        private int fixedAdvance = 0;
        private readonly float fixedLineSpacing;
        private readonly float yMaxLimit;
        private readonly float yMinLimit;

        private Vector3 imageOrigin = Vector3.zero;
        private Vector2 position = Vector2.zero;

        readonly int numDigits;
        readonly int numRolling;
        readonly FontStyle style;
        readonly int ascent;

        readonly static string charactersUsed = "0123456789 -";
        readonly float valueScalar;
        readonly float upperLimit;
        readonly float lowerLimit;
        readonly string digitsFormat;
        readonly int scrollingVerticesOffset;
        int digits = int.MaxValue;
        string digitsString;
        float fraction = float.MaxValue;
        bool digitsChanged;
        bool fractionChanged;

        Vector3[] vertices = new Vector3[0];
        Color32[] colors32 = new Color32[0];
        Vector4[] tangents = new Vector4[0];
        Vector2[] uv = new Vector2[0];
        int[] triangles = new int[0];

        internal MASPageRollingDigit(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
            : base(config, prop, comp)
        {
            string localFonts = string.Empty;
            if (!config.TryGetValue("font", ref localFonts))
            {
                localFonts = string.Empty;
            }

            string styleStr = string.Empty;
            style = FontStyle.Normal;
            if (config.TryGetValue("style", ref styleStr))
            {
                style = MdVTextMesh.FontStyle(styleStr);
            }
            else
            {
                style = monitor.defaultStyle;
            }

            Vector2 fontDimensions = Vector2.zero;
            if (!config.TryGetValue("fontSize", ref fontDimensions) || fontDimensions.x < 0.0f || fontDimensions.y < 0.0f)
            {
                fontDimensions = monitor.fontSize;
            }

            Color32 textColor;
            string textColorStr = string.Empty;
            if (!config.TryGetValue("textColor", ref textColorStr) || string.IsNullOrEmpty(textColorStr))
            {
                textColor = monitor.textColor_;
            }
            else
            {
                textColor = Utility.ParseColor32(textColorStr, comp);
            }

            // Position is based on default font size
            Vector2 fontScale = monitor.fontSize;

            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            if (string.IsNullOrEmpty(localFonts))
            {
                font = monitor.defaultFont;
            }
            else
            {
                font = MASLoader.GetFont(localFonts.Trim());
            }

            // Set up our text.
            imageOrigin = pageRoot.position + new Vector3(monitor.screenSize.x * -0.5f, monitor.screenSize.y * 0.5f, depth);

            meshObject = new GameObject();
            meshObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
            meshObject.layer = pageRoot.gameObject.layer;
            meshObject.transform.parent = pageRoot;
            meshObject.transform.position = imageOrigin;

            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshRenderer.material = new Material(MASLoader.shaders["MOARdV/TextMonitor"]);
            meshRenderer.material.mainTexture = font.material.mainTexture;

            string positionString = string.Empty;
            if (config.TryGetValue("position", ref positionString))
            {
                string[] positions = Utility.SplitVariableList(positionString);
                if (positions.Length != 2)
                {
                    throw new ArgumentException("position does not contain 2 values in ROLLING_DIGIT " + name);
                }

                variableRegistrar.RegisterNumericVariable(positions[0], (double newValue) =>
                {
                    position.x = (float)newValue * fontScale.x;
                    meshObject.transform.position = imageOrigin + new Vector3(position.x, -position.y, 0.0f);
                });

                variableRegistrar.RegisterNumericVariable(positions[1], (double newValue) =>
                {
                    position.y = (float)newValue * fontScale.y;
                    meshObject.transform.position = imageOrigin + new Vector3(position.x, -position.y, 0.0f);
                });
            }

            int maxDigits = 0;
            if (!config.TryGetValue("maxDigits", ref maxDigits))
            {
                throw new ArgumentException("'maxDigits' missing in ROLLING_DIGIT " + name);
            }

            if (!config.TryGetValue("numRolling", ref numRolling))
            {
                throw new ArgumentException("'numRolling' missing in ROLLING_DIGIT " + name);
            }
            if (numRolling < 1)
            {
                throw new ArgumentException("numRolling must be greater than zero in ROLLING_DIGIT " + name);
            }
            valueScalar = Mathf.Pow(10.0f, numRolling);
            numDigits = maxDigits - numRolling;
            if (numDigits < 0)
            {
                throw new ArgumentException("'numRolling' must be less than 'maxDigits' in ROLLING_DIGIT " + name);
            }
            scrollingVerticesOffset = 4 * numDigits;

            upperLimit = Mathf.Pow(10.0f, maxDigits) - 1.0f;
            lowerLimit = -Mathf.Pow(10.0f, maxDigits-1) + 1.0f;

            bool padZero = false;
            if (!config.TryGetValue("padZero", ref padZero))
            {
                padZero = false;
            }

            int numVertices = 4 * numDigits + 12 * numRolling;
            int numIndices = 6 * numDigits + 18 * numRolling;
            vertices = new Vector3[numVertices];
            colors32 = new Color32[numVertices];
            tangents = new Vector4[numVertices];
            uv = new Vector2[numVertices];
            triangles = new int[numIndices];
            // These values are invariant:
            Vector4 tangent = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
            for (int i = 0; i < numVertices; ++i)
            {
                colors32[i] = textColor;
                tangents[i] = tangent;
            }
            for (int i = 0; i < numIndices / 6; ++i)
            {
                triangles[i * 6 + 0] = i * 4 + 0;
                triangles[i * 6 + 1] = i * 4 + 3;
                triangles[i * 6 + 2] = i * 4 + 2;
                triangles[i * 6 + 3] = i * 4 + 0;
                triangles[i * 6 + 4] = i * 4 + 1;
                triangles[i * 6 + 5] = i * 4 + 3;
            }

            if (numDigits > 0)
            {
                StringBuilder sb = StringBuilderCache.Acquire();
                sb.AppendFormat("{{0,{0}:0", numDigits);
                if (padZero)
                {
                    sb.Append('0', numDigits - 1);
                }
                sb.Append("}");
                digitsFormat = sb.ToStringAndRelease();
            }

            string valueString = string.Empty;
            if (!config.TryGetValue("value", ref valueString))
            {
                throw new ArgumentException("'value' missing in ROLLING_DIGIT " + name);
            }
            variableRegistrar.RegisterNumericVariable(valueString, ValueCallback);

            RenderPage(false);

            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the mesh if we're in variable mode
                meshObject.SetActive(false);
                variableRegistrar.RegisterNumericVariable(variableName, VariableCallback);
            }
            else
            {
                currentState = true;
            }

            dynamic = font.dynamic;

            float characterScalar;
            if (dynamic)
            {
                characterScalar = fontDimensions.y / (float)font.lineHeight;
                this.fontSize = font.fontSize;
            }
            else
            {
                // Unfortunately, there doesn't seem to be a way to set the font metrics when
                // creating a bitmap font, so I have to play games here by fetching the values
                // I stored in the character info.
                CharacterInfo ci = font.characterInfo[0];
                characterScalar = fontDimensions.y / (float)ci.glyphHeight;
                this.fontSize = ci.glyphHeight;
            }

            this.fixedAdvance = (int)fontDimensions.x;
            this.fixedLineSpacing = Mathf.Floor(fontDimensions.y);
            this.characterScalar = characterScalar;
            ascent = (dynamic) ? font.ascent : (int)(0.8125f * fixedLineSpacing);
            yMaxLimit = 0.5f * fixedLineSpacing;
            yMinLimit = -1.5f * fixedLineSpacing;

            Font.textureRebuilt += FontRebuiltCallback;
            font.RequestCharactersInTexture(charactersUsed, fontSize, style);

            digitsChanged = true;
            fractionChanged = true;
        }

        /// <summary>
        /// Handle a changed node visibility changed value.
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue)
        {
            if (EvaluateVariable(newValue))
            {
                meshObject.SetActive(currentState);
            }
        }

        /// <summary>
        /// Process a changed numeric value for the displayed value.
        /// </summary>
        /// <param name="newValue">The new displayed value.</param>
        private void ValueCallback(double newValue)
        {
            float scaledValue = Mathf.Clamp((float)newValue, lowerLimit, upperLimit) / valueScalar;
            // Add 1/2 to round the value up, so it flips to the next digit when the fraction
            // passes 1/2.
            int newDigits = (int)Mathf.Floor(scaledValue + 0.05f);
            float floorScaled = Mathf.Floor(scaledValue);
            float newFraction = (scaledValue - floorScaled) * 10.0f;

            if (newDigits != digits)
            {
                digits = newDigits;
                digitsChanged = true;
                digitsString = string.Format(digitsFormat, newDigits);
            }
            if (!Mathf.Approximately(newFraction, fraction))
            {
                fraction = newFraction;
                fractionChanged = true;
            }
        }

        /// <summary>
        /// Callback to tell us when a Font had to rebuild its texture atlas.
        /// When that happens, we have to regenerate our text.
        /// </summary>
        /// <param name="whichFont"></param>
        /// <param name="newTexture"></param>
        private void FontRebuiltCallback(Font whichFont)
        {
            if (whichFont == font)
            {
                digitsChanged = true;
                fractionChanged = true;
                meshRenderer.material.mainTexture = whichFont.material.mainTexture;
            }
        }

        /// <summary>
        /// Update the vertex / UV data for a given glyph.
        /// </summary>
        private void PlaceGlyph(char ch, float xPos, float yPos, int startIndex)
        {
            CharacterInfo charInfo;
            bool fetched;
            if (dynamic)
            {
                fetched = font.GetCharacterInfo(ch, out charInfo, 0, style);
            }
            else
            {
                fetched = font.GetCharacterInfo(ch, out charInfo);
            }
            if (fetched)
            {
                if (charInfo.minX != charInfo.maxX && charInfo.minY != charInfo.maxY)
                {
                    float minX, maxX;
                    // Some characters have a large advance (Inconsolata-Go filled triangle (arrowhead)
                    // and delta characters, for instance).  Instead of letting them overwrite
                    // neighboring characters, force them to fit the fixedAdvance space.
                    // This also affects wide characters in variable-advance fonts.
                    if ((int)(charInfo.advance * characterScalar) > fixedAdvance)
                    {
                        minX = 0.0f;
                        // don't need to multiply by character size, since fixedAdvance accounts for
                        // that.
                        maxX = fixedAdvance;
                    }
                    else if ((int)(charInfo.advance * characterScalar) < fixedAdvance)
                    {
                        // Characters that have smaller advance than our fixed-size setting
                        // need to be pushed towards the center so they don't look out of place.
                        int nudge = (fixedAdvance - (int)(charInfo.advance * characterScalar)) / 2;

                        minX = (nudge + charInfo.minX * characterScalar);
                        maxX = (nudge + charInfo.maxX * characterScalar);
                    }
                    else
                    {
                        minX = charInfo.minX * characterScalar;
                        maxX = charInfo.maxX * characterScalar;
                    }
                    minX += (float)xPos;
                    maxX += (float)xPos;

                    float minY;
                    float maxY;
                    // Excessively tall characters need tweaked to fit
                    maxY = Math.Min(charInfo.maxY, ascent) * characterScalar;

                    if ((ascent - charInfo.minY) * characterScalar > fixedLineSpacing)
                    {
                        // Push the bottom of the character upwards so it's not
                        // hanging over the next line.
                        minY = ascent * characterScalar - fixedLineSpacing;
                    }
                    else
                    {
                        minY = charInfo.minY * characterScalar;
                    }

                    minY += yPos;
                    maxY += yPos;

                    // For now: clamp.  Actually, I think this is sufficient.
                    maxY = Mathf.Clamp(maxY, yMinLimit, yMaxLimit);
                    minY = Mathf.Clamp(minY, yMinLimit, yMaxLimit);

                    vertices[startIndex + 0] = new Vector3(minX, maxY, 0.0f);
                    uv[startIndex + 0] = charInfo.uvTopLeft;

                    vertices[startIndex + 1] = new Vector3(maxX, maxY, 0.0f);
                    uv[startIndex + 1] = charInfo.uvTopRight;

                    vertices[startIndex + 2] = new Vector3(minX, minY, 0.0f);
                    uv[startIndex + 2] = charInfo.uvBottomLeft;

                    vertices[startIndex + 3] = new Vector3(maxX, minY, 0.0f);
                    uv[startIndex + 3] = charInfo.uvBottomRight;
                }
            }
            else
            {
                // Didn't fetch it?  What?  Make it a degenerate tri.
                vertices[startIndex + 0] = Vector3.zero;
                vertices[startIndex + 1] = Vector3.zero;
                vertices[startIndex + 2] = Vector3.zero;
                vertices[startIndex + 3] = Vector3.zero;
            }
        }

        /// <summary>
        /// Update the fixed (non-scrolling) digits.
        /// </summary>
        private void UpdateDigits()
        {
            float xPos = 0.0f;
            float yPos = Mathf.Floor(-ascent * characterScalar);

            for (int i = 0; i < numDigits; ++i)
            {
                PlaceGlyph(digitsString[i], xPos, yPos, i * 4);
                xPos += fixedAdvance;
            }
        }

        /// <summary>
        /// Update the scrolling digits.
        /// </summary>
        private void UpdateFraction()
        {
            int primeDigit = (int)(fraction);
            float offset = fraction % 1.0f;
            // Adjust which digits we display, so the 'next' value doesn't appear and disappear too soon.
            if (fraction > 0.5f)
            {
                primeDigit = (primeDigit + 1) % 10;
                offset -= 1.0f;
            }

            int nextDigit = (primeDigit + 1) % 10;
            int prevDigit = (primeDigit + 9) % 10;
            float xPos = fixedAdvance * numDigits;
            float yPos = Mathf.Floor(-ascent * characterScalar);

            yPos -= offset * fixedLineSpacing;

            PlaceGlyph(charactersUsed[primeDigit], xPos, yPos, scrollingVerticesOffset);
            PlaceGlyph(charactersUsed[nextDigit], xPos, yPos + fixedLineSpacing, scrollingVerticesOffset + 4);
            PlaceGlyph(charactersUsed[prevDigit], xPos, yPos - fixedLineSpacing, scrollingVerticesOffset + 8);

            for (int i = 1; i < numRolling; ++i)
            {
                xPos += fixedAdvance;
                PlaceGlyph('0', xPos, yPos, scrollingVerticesOffset + 12 * i);
                PlaceGlyph('0', xPos, yPos + fixedLineSpacing, scrollingVerticesOffset + 4 + 12 * i);
                PlaceGlyph('0', xPos, yPos - fixedLineSpacing, scrollingVerticesOffset + 8 + 12 * i);
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

            if (enable)
            {
                if (digitsChanged)
                {
                    UpdateDigits();
                }
                if (fractionChanged)
                {
                    UpdateFraction();
                }

                if (digitsChanged || fractionChanged)
                {
                    meshFilter.mesh.Clear();
                    meshFilter.mesh.vertices = vertices;
                    meshFilter.mesh.colors32 = colors32;
                    meshFilter.mesh.tangents = tangents;
                    meshFilter.mesh.uv = uv;
                    meshFilter.mesh.triangles = triangles;
                    meshFilter.mesh.RecalculateNormals();
                    meshFilter.mesh.UploadMeshData(false);
                }

                digitsChanged = false;
                fractionChanged = false;
            }
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            Font.textureRebuilt -= FontRebuiltCallback;

            UnityEngine.GameObject.Destroy(meshObject);
            meshObject = null;

            variableRegistrar.ReleaseResources();
        }
    }
}
