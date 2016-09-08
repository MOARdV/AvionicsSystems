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
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// The MdVTextMesh is derived from JSITextMesh in RasterPropMonitor.  It
    /// has been refactored in a number of ways to improve its performance and
    /// reduce garbage production (particularly for frequently-changing
    /// variables).
    /// 
    /// As the sole author of the original code, I have granted myself
    /// permission to port it here and apply a different license to it.
    /// </summary>
    public class MdVTextMesh : MonoBehaviour
    {
        static private MASStringFormatter formatter = new MASStringFormatter();

        private TextAlignment alignment_ = TextAlignment.Left;
        public TextAlignment alignment
        {
            get
            {
                return alignment_;
            }
            set
            {
                if (value != alignment_)
                {
                    invalidated = true;
                    alignment_ = value;
                }
            }
        }

        private TextAnchor anchor_ = TextAnchor.UpperLeft;
        public TextAnchor anchor
        {
            get
            {
                return anchor_;
            }
            set
            {
                if (value != anchor_)
                {
                    invalidated = true;
                    anchor_ = value;
                }
            }
        }

        public Material material
        {
            get
            {
                return meshRenderer.material;
            }
            set
            {
                meshRenderer.material = value;
            }
        }

        private MASFlightComputer comp;
        private Font font;
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private TextRow[] textRow;
        private Color32 color;
        private int fontSize;
        private float characterSize = 1.0f;
        private float lineSpacing = 1.0f;
        private int fixedAdvance = 0;
        private int fixedLineSpacing = 0;
        private bool boundedText = false;
        private bool richText;
        private bool invalidated;
        private bool colorInvalidated;
        private InternalProp internalProp;
        private bool configured = false;

        // To avoid piles of garbage creation, keep the local arrays here so
        // we do not allocate them every update.  Intentionally set their sizes
        // to zero to trigger a reallocation on first use.
        Vector3[] vertices = new Vector3[0];
        Color32[] colors32 = new Color32[0];
        Vector4[] tangents = new Vector4[0];
        Vector2[] uv = new Vector2[0];
        int[] triangles = new int[0];

        private static readonly string[] VariableListSeparator = { "$&$" };
        //private static readonly string[] MangledLineSeparator = { "$$$" };
        private static readonly string[] LineSeparator = { Environment.NewLine };

        /// <summary>
        /// Set / change the characterSize field
        /// </summary>
        /// <param name="characterSize"></param>
        public void SetCharacterSize(float characterSize)
        {
            this.characterSize = characterSize;
            invalidated = true;
        }

        /// <summary>
        /// Set the default text color
        /// </summary>
        /// <param name="color"></param>
        public void SetColor(Color32 color)
        {
            this.color = color;
            colorInvalidated = true;
        }

        /// <summary>
        /// Enable / disable the renderer.
        /// </summary>
        /// <param name="enabled"></param>
        public void SetRenderEnabled(bool enabled)
        {
            this.meshRenderer.enabled = enabled;
        }

        /// <summary>
        /// Set the font and fontSize for auto-scaled text
        /// </summary>
        /// <param name="font"></param>
        /// <param name="fontSize"></param>
        public void SetFont(Font font, int fontSize)
        {
            this.font = font;
            this.fontSize = fontSize;
            boundedText = false;
            meshRenderer.material.mainTexture = MASLoader.GetFontTexture(font);
            invalidated = true;
        }

        /// <summary>
        /// Set the font and define the bounding box for characters (in pixels)
        /// </summary>
        /// <param name="font"></param>
        /// <param name="fontDimensions"></param>
        public void SetFont(Font font, Vector2 fontDimensions)
        {
            this.font = font;
            this.fontSize = font.fontSize;
            this.fixedAdvance = (int)fontDimensions.x;
            this.fixedLineSpacing = (int)fontDimensions.y;
            boundedText = true;
            meshRenderer.material.mainTexture = MASLoader.GetFontTexture(font);
            invalidated = true;
        }

        /// <summary>
        /// Set line-to-line spacing.  1.0 = default
        /// </summary>
        /// <param name="lineSpacing"></param>
        public void SetLineSpacing(float lineSpacing)
        {
            this.lineSpacing = lineSpacing;
            invalidated = true;
        }

        /// <summary>
        /// Update the text.  This method will unmangle .cfg-safe text, parse
        /// into multiple lines, and take care of all of the pre-processing
        /// needed.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="immutable"></param>
        /// <param name="preserveWhitespace"></param>
        /// <param name="comp"></param>
        public void SetText(string text, bool immutable, bool preserveWhitespace, MASFlightComputer comp, InternalProp internalProp)
        {
            configured = false;
            // Do some up-front processing:
            this.internalProp = internalProp;

            // If there's no text, disable this object.
            if (string.IsNullOrEmpty(text))
            {
                meshRenderer.gameObject.SetActive(false);
            }
            else
            {
                meshRenderer.gameObject.SetActive(true);

                text = UnmangleText(text);
                richText = text.Contains("[");

                // If the text was initialized with immutable = true, then it
                // was configured as a one-shot text that should not be updated.
                // If there are no '$&$' tokens, then the text has no variables,
                // and it doesn't change.
                if (immutable || !text.Contains(VariableListSeparator[0]))
                {
                    // This is a one-shot text.  We will evaluate it here once
                    //if (text.Contains(VariableListSeparator[0]))
                    //{
                    //    text = EvaluateImmutableVariables(text, comp);
                    //}

                    // Remove any variable evaluators
                    //string[] staticText = text.Split(VariableListSeparator, StringSplitOptions.RemoveEmptyEntries);
                    //if (staticText.Length == 0)
                    //{
                    //    // Something went wrong.
                    //    Utility.LogErrorMessage(this, "Splitting static text - got no variables");
                    //    meshRenderer.gameObject.SetActive(false);
                    //    return;
                    //}

                    string[] textRows = text.Split(LineSeparator, StringSplitOptions.None);
                    textRow = new TextRow[textRows.Length];

                    for (int i = 0; i < textRows.Length; ++i)
                    {
                        TextRow tr = new TextRow();

                        if (textRows[i].Contains(VariableListSeparator[0]))
                        {
                            tr.formatString = tr.formattedData = EvaluateImmutableVariables(textRows[i], comp, internalProp);
                        }
                        else
                        {
                            tr.formatString = tr.formattedData = (preserveWhitespace) ? textRows[i] : textRows[i].Trim();
                        }
                        tr.rowInvalidated = false;
                        textRow[i] = tr;
                    }
                }
                else
                {
                    this.comp = comp;

                    // preprocessing - split into rows
                    string[] textRows = text.Split(LineSeparator, StringSplitOptions.None);
                    textRow = new TextRow[textRows.Length];

                    for (int i = 0; i < textRows.Length; ++i)
                    {
                        string[] rowText = textRows[i].Split(VariableListSeparator, StringSplitOptions.RemoveEmptyEntries);

                        TextRow tr = new TextRow();
                        if (rowText.Length == 0)
                        {
                            tr.formatString = string.Empty;
                        }
                        else
                        {
                            tr.formatString = (preserveWhitespace) ? rowText[0] : rowText[0].Trim();
                        }
                        if (rowText.Length > 1)
                        {
                            string[] variables = rowText[1].Split(';');
                            tr.variable = new MASFlightComputer.Variable[variables.Length];
                            tr.evals = new object[variables.Length];
                            tr.callback = () => { invalidated = true; tr.rowInvalidated = true; };
                            for (int var = 0; var < tr.variable.Length; ++var)
                            {
                                tr.variable[var] = comp.RegisterOnVariableChange(variables[var], internalProp, tr.callback);
                            }
                            tr.rowInvalidated = true;
                            tr.EvaluateVariables();
                        }
                        else
                        {
                            tr.formattedData = tr.formatString;
                        }
                        tr.rowInvalidated = false;
                        textRow[i] = tr;
                    }
                }

                if (boundedText)
                {
                    GenerateRichBoundedText();
                }
                else if (richText)
                {
                    GenerateRichText();
                }
                else
                {
                    GenerateText();
                }
                invalidated = false;
                configured = true;
            }
            // HACK: Text positions are wrong on MASMonitor props if I don't
            // regenerate the text post-configuration.
            // I should figure out why at some point.
            invalidated = true;
        }

        /// <summary>
        /// This method is used to evaluate text flagged as immutable one time
        /// at initialization.
        /// </summary>
        /// <param name="text">Text to evaluate</param>
        /// <param name="comp">The owning flight computer</param>
        /// <returns></returns>
        private string EvaluateImmutableVariables(string text, MASFlightComputer comp, InternalProp internalProp)
        {
            string[] staticText = text.Split(VariableListSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (staticText.Length == 1)
            {
                return text;
            }
            else
            {
                string[] variables = staticText[1].Split(';');
                TextRow tr = new TextRow();
                tr.formatString = staticText[0];
                tr.variable = new MASFlightComputer.Variable[variables.Length];
                tr.evals = new object[variables.Length];
                for (int var = 0; var < tr.variable.Length; ++var)
                {
                    tr.variable[var] = comp.GetVariable(variables[var], internalProp);
                }
                tr.rowInvalidated = true;
                tr.EvaluateVariables();
                for (int var = 0; var < tr.variable.Length; ++var)
                {
                    tr.variable[var] = null;
                }

                return tr.formattedData;
            }
        }

        /// <summary>
        /// Set up the JSITextMesh components if they haven't been set up yet.
        /// </summary>
        public void Awake()
        {
            MASLoader.textureRebuilt += FontRebuiltCallback;
            CreateComponents();
        }

        /// <summary>
        /// Make sure we don't leave our callback lingering.
        /// </summary>
        public void OnDestroy()
        {
            MASLoader.textureRebuilt -= FontRebuiltCallback;

            for (int i = textRow.Length - 1; i >= 0; --i)
            {
                if (textRow[i].variable != null)
                {
                    for (int var = 0; var < textRow[i].variable.Length; ++var)
                    {
                        comp.UnregisterOnVariableChange(textRow[i].variable[var].name, internalProp, textRow[i].callback);
                        textRow[i].variable[var] = null;
                    }
                    textRow[i].variable = null;
                    textRow[i].callback = null;
                }
            }
            internalProp = null;
            comp = null;

            Destroy(meshFilter);
            meshFilter = null;

            Destroy(meshRenderer.material);

            Destroy(meshRenderer);
            meshRenderer = null;
        }

        /// <summary>
        /// Does our text need updated?  Do it here
        /// </summary>
        public void Update()
        {
            if (configured == false)
            {
                return;
            }

            try
            {
                if (invalidated)
                {
                    if (textRow == null)
                    {
                        // Not initialized yet?
                        meshRenderer.gameObject.SetActive(false);
                        return;
                    }

                    for (int i = textRow.Length - 1; i >= 0; --i)
                    {
                        textRow[i].EvaluateVariables();
                    }

                    if (boundedText)
                    {
                        GenerateRichBoundedText();
                    }
                    else if (richText)
                    {
                        GenerateRichText();
                    }
                    else
                    {
                        GenerateText();
                    }

                    invalidated = false;
                    colorInvalidated = false;
                }
                else if (colorInvalidated)
                {
                    if (richText)
                    {
                        GenerateRichText();
                    }
                    else
                    {
                        int colorsLength = colors32.Length;
                        if (colorsLength > 0)
                        {
                            for (int i = colorsLength - 1; i >= 0; --i)
                            {
                                colors32[i] = color;
                            }
                            meshFilter.mesh.colors32 = colors32;
                            meshFilter.mesh.UploadMeshData(false);
                        }
                    }
                    colorInvalidated = false;
                }
            }
            catch (Exception e)
            {
                Utility.LogErrorMessage(this, "Trapped exception:");
                Utility.LogErrorMessage(this, e.ToString());
            }
        }

        /// <summary>
        /// Callback to tell us when a Font had to rebuild its texture atlas.
        /// When that happens, we have to regenerate our text.
        /// </summary>
        /// <param name="whichFont"></param>
        /// <param name="newTexture"></param>
        private void FontRebuiltCallback(Font whichFont, Texture2D newTexture)
        {
            if (whichFont == font)
            {
                invalidated = true;
                meshRenderer.material.mainTexture = newTexture;
            }
        }

        /// <summary>
        /// Set up rendering components.
        /// </summary>
        private void CreateComponents()
        {
            if (meshRenderer == null)
            {
                try
                {
                    meshFilter = gameObject.AddComponent<MeshFilter>();
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();
                    meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    meshRenderer.receiveShadows = true;
                    meshRenderer.material = new Material(MASLoader.shaders["MOARdV/TextMesh"]);
                }
                catch (Exception e)
                {
                    Utility.LogErrorMessage(this, "Exception caught creating MdVTextMesh components:");
                    Utility.LogErrorMessage(this, e.ToString());
                }
            }
        }

        #region Mesh Regeneration
        /// <summary>
        /// Generate a forced fixed-size text, enabling the various rich character
        /// features.  Intended for use with MASMonitor.
        /// </summary>
        private void GenerateRichBoundedText()
        {
            // State tracking
            bool bold = false;
            bool italic = false;
            //size = something.

            // Determine text length
            int maxVerts = 0;
            int numTextRows = textRow.Length;
            float widthScaling = 1.0f;
            for (int line = 0; line < numTextRows; ++line)
            {
                textRow[line].textLength = 0;

                int stringLength = textRow[line].formattedData.Length;
                for (int charIndex = 0; charIndex < stringLength; charIndex++)
                {
                    bool escapedBracket = false;
                    // We will continue parsing bracket pairs until we're out of bracket pairs,
                    // since all of them -- except the escaped bracket tag --
                    // consume characters and change state without actually generating any output.
                    while (charIndex < stringLength && textRow[line].formattedData[charIndex] == '[')
                    {
                        // If there's no closing bracket, we stop parsing and go on to printing.
                        int nextBracket = textRow[line].formattedData.IndexOf(']', charIndex) - charIndex;
                        if (nextBracket < 1)
                            break;
                        // Much easier to parse it this way, although I suppose more expensive.
                        string tagText = textRow[line].formattedData.Substring(charIndex + 1, nextBracket - 1).Trim();
                        if ((tagText.Length == 9 || tagText.Length == 7) && tagText[0] == '#')
                        {
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText.Length > 2 && tagText[0] == '@')
                        {
                            // Valid nudge tags are [@x<number>] or [@y<number>] so the conditions for them is that
                            // the next symbol is @ and there are at least three, one designating the axis.
                            float coord;
                            if (float.TryParse(tagText.Substring(2), out coord))
                            {
                                // Only consume the symbols if they parse.
                                charIndex += nextBracket + 1;
                            }
                            else //If it didn't parse, skip over it.
                            {
                                break;
                            }
                        }
                        else if (tagText == "[")
                        {
                            // We got a "[[]" which means an escaped opening bracket.
                            escapedBracket = true;
                            charIndex += nextBracket;
                            break;
                        }
                        else if (tagText == "b")
                        {
                            bold = true;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "i")
                        {
                            italic = true;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/b")
                        {
                            bold = false;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/i")
                        {
                            italic = false;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "hw")
                        {
                            widthScaling = 0.5f;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "dw")
                        {
                            widthScaling = 2.0f;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/hw")
                        {
                            widthScaling = 1.0f;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/dw")
                        {
                            widthScaling = 1.0f;
                            charIndex += nextBracket + 1;
                        }
                        else // Else we didn't recognise anything so it's not a tag.
                        {
                            break;
                        }
                    }

                    if (charIndex < stringLength)
                    {
                        FontStyle style = GetFontStyle(bold, italic);
                        font.RequestCharactersInTexture(escapedBracket ? "[" : textRow[line].formattedData[charIndex].ToString(), fontSize, style);
                        CharacterInfo charInfo;
                        if (font.GetCharacterInfo(textRow[line].formattedData[charIndex], out charInfo, 0, style))
                        {
                            textRow[line].textLength += fixedAdvance;
                            maxVerts += 4;
                        }
                    }
                }
            }

            if (maxVerts == 0)
            {
                meshRenderer.gameObject.SetActive(false);
                return;
            }

            meshRenderer.gameObject.SetActive(true);

            if (vertices.Length < maxVerts)
            {
                vertices = new Vector3[maxVerts];
                colors32 = new Color32[maxVerts];
                tangents = new Vector4[maxVerts];
                uv = new Vector2[maxVerts];

                int triLength = maxVerts + maxVerts / 2;
                triangles = new int[triLength];
            }

            int charWritten = 0;
            int arrayIndex = 0;
            int yPos = -fixedLineSpacing;
            int xAnchor = 0;

            for (int line = 0; line < numTextRows; ++line)
            {
                int xPos = xAnchor;

                Color32 fontColor = color;
                float xOffset = 0.0f;
                float yOffset = 0.0f;

                int stringLength = textRow[line].formattedData.Length;
                for (int charIndex = 0; charIndex < stringLength; charIndex++)
                {
                    bool escapedBracket = false;
                    // We will continue parsing bracket pairs until we're out of bracket pairs,
                    // since all of them -- except the escaped bracket tag --
                    // consume characters and change state without actually generating any output.
                    while (charIndex < stringLength && textRow[line].formattedData[charIndex] == '[')
                    {
                        // If there's no closing bracket, we stop parsing and go on to printing.
                        int nextBracket = textRow[line].formattedData.IndexOf(']', charIndex) - charIndex;
                        if (nextBracket < 1)
                            break;
                        // Much easier to parse it this way, although I suppose more expensive.
                        string tagText = textRow[line].formattedData.Substring(charIndex + 1, nextBracket - 1).Trim();
                        if ((tagText.Length == 9 || tagText.Length == 7) && tagText[0] == '#')
                        {
                            // Valid color tags are [#rrggbbaa] or [#rrggbb].
                            fontColor = XKCDColors.ColorTranslator.FromHtml(tagText);
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText.Length > 2 && tagText[0] == '@')
                        {
                            // Valid nudge tags are [@x<number>] or [@y<number>] so the conditions for them is that
                            // the next symbol is @ and there are at least three, one designating the axis.
                            float coord;
                            if (float.TryParse(tagText.Substring(2), out coord))
                            {
                                switch (tagText[1])
                                {
                                    case 'X':
                                    case 'x':
                                        xOffset = coord;
                                        break;
                                    case 'Y':
                                    case 'y':
                                        yOffset = -coord;
                                        break;
                                }

                                // Only consume the symbols if they parse.
                                charIndex += nextBracket + 1;
                            }
                            else //If it didn't parse, skip over it.
                            {
                                break;
                            }
                        }
                        else if (tagText == "[")
                        {
                            // We got a "[[]" which means an escaped opening bracket.
                            escapedBracket = true;
                            charIndex += nextBracket;
                            break;
                        }
                        else if (tagText == "b")
                        {
                            bold = true;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "i")
                        {
                            italic = true;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/b")
                        {
                            bold = false;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/i")
                        {
                            italic = false;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "hw")
                        {
                            widthScaling = 0.5f;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "dw")
                        {
                            widthScaling = 2.0f;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/hw")
                        {
                            widthScaling = 1.0f;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/dw")
                        {
                            widthScaling = 1.0f;
                            charIndex += nextBracket + 1;
                        }
                        else // Else we didn't recognise anything so it's not a tag.
                        {
                            break;
                        }
                    }

                    if (charIndex < stringLength)
                    {
                        FontStyle style = GetFontStyle(bold, italic);
                        CharacterInfo charInfo;
                        if (font.GetCharacterInfo(escapedBracket ? '[' : textRow[line].formattedData[charIndex], out charInfo, 0, style))
                        {
                            if (charInfo.minX != charInfo.maxX && charInfo.minY != charInfo.maxY)
                            {
                                // TODO: Tune this
                                int minX = Math.Max(charInfo.minX, 0);
                                int maxX = Math.Min(charInfo.maxX, fixedAdvance);
                                int minY = charInfo.minY;// Math.Max(charInfo.minY, 0);
                                int maxY = charInfo.maxY;// Math.Min(charInfo.maxY, (int)characterBound.y);
                                triangles[charWritten * 6 + 0] = arrayIndex + 0;
                                triangles[charWritten * 6 + 1] = arrayIndex + 3;
                                triangles[charWritten * 6 + 2] = arrayIndex + 2;
                                triangles[charWritten * 6 + 3] = arrayIndex + 0;
                                triangles[charWritten * 6 + 4] = arrayIndex + 1;
                                triangles[charWritten * 6 + 5] = arrayIndex + 3;

                                // TODO: make this work correctly by centering the
                                // characters and clamping to the width.
                                vertices[arrayIndex] = new Vector3(characterSize * ((float)(xPos + (minX * widthScaling)) + xOffset), characterSize * ((float)(yPos + maxY) + yOffset), 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                                uv[arrayIndex] = charInfo.uvTopLeft;

                                ++arrayIndex;

                                vertices[arrayIndex] = new Vector3(characterSize * ((float)(xPos + (maxX * widthScaling)) + xOffset), characterSize * ((float)(yPos + maxY) + yOffset), 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                                uv[arrayIndex] = charInfo.uvTopRight;

                                ++arrayIndex;

                                vertices[arrayIndex] = new Vector3(characterSize * ((float)(xPos + (minX * widthScaling)) + xOffset), characterSize * ((float)(yPos + minY) + yOffset), 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                                uv[arrayIndex] = charInfo.uvBottomLeft;

                                ++arrayIndex;

                                vertices[arrayIndex] = new Vector3(characterSize * ((float)(xPos + (maxX * widthScaling)) + xOffset), characterSize * ((float)(yPos + minY) + yOffset), 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                                uv[arrayIndex] = charInfo.uvBottomRight;

                                ++arrayIndex;
                                ++charWritten;
                            }
                            xPos += (int)(fixedAdvance * widthScaling);
                        }
                    }
                }

                yPos -= fixedLineSpacing;
            }

            int triangleLength = triangles.Length;
            for (int i = charWritten * 6; i < triangleLength; ++i)
            {
                triangles[i] = 0;
            }
            meshFilter.mesh.Clear();
            meshFilter.mesh.vertices = vertices;
            meshFilter.mesh.colors32 = colors32;
            meshFilter.mesh.tangents = tangents;
            meshFilter.mesh.uv = uv;
            meshFilter.mesh.triangles = triangles;
            meshFilter.mesh.RecalculateNormals();
            meshFilter.mesh.Optimize();
            // Can't hide mesh with (true), or we can't edit colors later.
            meshFilter.mesh.UploadMeshData(false);
        }

        /// <summary>
        /// Convert a text using control sequences ([b], [i], [#rrggbb(aa)], [size]).
        /// </summary>
        private void GenerateRichText()
        {
            // State tracking
            bool bold = false;
            bool italic = false;
            //size = something.

            // Determine text length
            int maxVerts = 0;
            int numTextRows = textRow.Length;
            float widthScaling = 1.0f;
            for (int line = 0; line < numTextRows; ++line)
            {
                textRow[line].textLength = 0;

                int stringLength = textRow[line].formattedData.Length;
                for (int charIndex = 0; charIndex < stringLength; charIndex++)
                {
                    bool escapedBracket = false;
                    // We will continue parsing bracket pairs until we're out of bracket pairs,
                    // since all of them -- except the escaped bracket tag --
                    // consume characters and change state without actually generating any output.
                    while (charIndex < stringLength && textRow[line].formattedData[charIndex] == '[')
                    {
                        // If there's no closing bracket, we stop parsing and go on to printing.
                        int nextBracket = textRow[line].formattedData.IndexOf(']', charIndex) - charIndex;
                        if (nextBracket < 1)
                            break;
                        // Much easier to parse it this way, although I suppose more expensive.
                        string tagText = textRow[line].formattedData.Substring(charIndex + 1, nextBracket - 1).Trim();
                        if ((tagText.Length == 9 || tagText.Length == 7) && tagText[0] == '#')
                        {
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText.Length > 2 && tagText[0] == '@')
                        {
                            // Valid nudge tags are [@x<number>] or [@y<number>] so the conditions for them is that
                            // the next symbol is @ and there are at least three, one designating the axis.
                            float coord;
                            if (float.TryParse(tagText.Substring(2), out coord))
                            {
                                // Only consume the symbols if they parse.
                                charIndex += nextBracket + 1;
                            }
                            else //If it didn't parse, skip over it.
                            {
                                break;
                            }
                        }
                        else if (tagText == "[")
                        {
                            // We got a "[[]" which means an escaped opening bracket.
                            escapedBracket = true;
                            charIndex += nextBracket;
                            break;
                        }
                        else if (tagText == "b")
                        {
                            bold = true;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "i")
                        {
                            italic = true;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/b")
                        {
                            bold = false;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/i")
                        {
                            italic = false;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "hw")
                        {
                            widthScaling = 0.5f;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "dw")
                        {
                            widthScaling = 2.0f;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/hw")
                        {
                            widthScaling = 1.0f;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/dw")
                        {
                            widthScaling = 1.0f;
                            charIndex += nextBracket + 1;
                        }
                        else // Else we didn't recognise anything so it's not a tag.
                        {
                            break;
                        }
                    }

                    if (charIndex < stringLength)
                    {
                        FontStyle style = GetFontStyle(bold, italic);
                        font.RequestCharactersInTexture(escapedBracket ? "[" : textRow[line].formattedData[charIndex].ToString(), fontSize, style);
                        CharacterInfo charInfo;
                        if (font.GetCharacterInfo(textRow[line].formattedData[charIndex], out charInfo, 0, style))
                        {
                            textRow[line].textLength += charInfo.advance;
                            maxVerts += 4;
                        }
                    }
                }
            }

            if (maxVerts == 0)
            {
                meshRenderer.gameObject.SetActive(false);
                return;
            }

            meshRenderer.gameObject.SetActive(true);

            if (vertices.Length < maxVerts)
            {
                vertices = new Vector3[maxVerts];
                colors32 = new Color32[maxVerts];
                tangents = new Vector4[maxVerts];
                uv = new Vector2[maxVerts];

                int triLength = maxVerts + maxVerts / 2;
                triangles = new int[triLength];
            }

            int charWritten = 0;
            int arrayIndex = 0;
            int yPos = 0;
            int xAnchor = 0;
            switch (anchor_)
            {
                case TextAnchor.LowerCenter:
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) - font.ascent;
                    break;
                case TextAnchor.LowerLeft:
                    //xAnchor = 0;
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) - font.ascent;
                    break;
                case TextAnchor.LowerRight:
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) - font.ascent;
                    break;
                case TextAnchor.MiddleCenter:
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) / 2 - font.ascent;
                    break;
                case TextAnchor.MiddleLeft:
                    //xAnchor = 0;
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) / 2 - font.ascent;
                    break;
                case TextAnchor.MiddleRight:
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) / 2 - font.ascent;
                    break;
                case TextAnchor.UpperCenter:
                    yPos = -font.ascent;
                    break;
                case TextAnchor.UpperLeft:
                    //xAnchor = 0;
                    yPos = -font.ascent;
                    break;
                case TextAnchor.UpperRight:
                    yPos = -font.ascent;
                    break;
            }

            int lineAdvance = (int)(lineSpacing * font.lineHeight);
            for (int line = 0; line < numTextRows; ++line)
            {
                int xPos = 0;
                if (alignment_ == TextAlignment.Center)
                {
                    xPos = -(textRow[line].textLength) / 2;
                }
                else if (alignment_ == TextAlignment.Right)
                {
                    xPos = -textRow[line].textLength;
                }
                xPos += xAnchor;

                Color32 fontColor = color;
                float xOffset = 0.0f;
                float yOffset = 0.0f;

                int stringLength = textRow[line].formattedData.Length;
                for (int charIndex = 0; charIndex < stringLength; charIndex++)
                {
                    bool escapedBracket = false;
                    // We will continue parsing bracket pairs until we're out of bracket pairs,
                    // since all of them -- except the escaped bracket tag --
                    // consume characters and change state without actually generating any output.
                    while (charIndex < stringLength && textRow[line].formattedData[charIndex] == '[')
                    {
                        // If there's no closing bracket, we stop parsing and go on to printing.
                        int nextBracket = textRow[line].formattedData.IndexOf(']', charIndex) - charIndex;
                        if (nextBracket < 1)
                            break;
                        // Much easier to parse it this way, although I suppose more expensive.
                        string tagText = textRow[line].formattedData.Substring(charIndex + 1, nextBracket - 1).Trim();
                        if ((tagText.Length == 9 || tagText.Length == 7) && tagText[0] == '#')
                        {
                            // Valid color tags are [#rrggbbaa] or [#rrggbb].
                            fontColor = XKCDColors.ColorTranslator.FromHtml(tagText);
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText.Length > 2 && tagText[0] == '@')
                        {
                            // Valid nudge tags are [@x<number>] or [@y<number>] so the conditions for them is that
                            // the next symbol is @ and there are at least three, one designating the axis.
                            float coord;
                            if (float.TryParse(tagText.Substring(2), out coord))
                            {
                                switch (tagText[1])
                                {
                                    case 'X':
                                    case 'x':
                                        xOffset = coord;
                                        break;
                                    case 'Y':
                                    case 'y':
                                        yOffset = -coord;
                                        break;
                                }

                                // Only consume the symbols if they parse.
                                charIndex += nextBracket + 1;
                            }
                            else //If it didn't parse, skip over it.
                            {
                                break;
                            }
                        }
                        else if (tagText == "[")
                        {
                            // We got a "[[]" which means an escaped opening bracket.
                            escapedBracket = true;
                            charIndex += nextBracket;
                            break;
                        }
                        else if (tagText == "b")
                        {
                            bold = true;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "i")
                        {
                            italic = true;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/b")
                        {
                            bold = false;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/i")
                        {
                            italic = false;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "hw")
                        {
                            widthScaling = 0.5f;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "dw")
                        {
                            widthScaling = 2.0f;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/hw")
                        {
                            widthScaling = 1.0f;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/dw")
                        {
                            widthScaling = 1.0f;
                            charIndex += nextBracket + 1;
                        }
                        else // Else we didn't recognise anything so it's not a tag.
                        {
                            break;
                        }
                    }

                    if (charIndex < stringLength)
                    {
                        FontStyle style = GetFontStyle(bold, italic);
                        CharacterInfo charInfo;
                        if (font.GetCharacterInfo(escapedBracket ? '[' : textRow[line].formattedData[charIndex], out charInfo, 0, style))
                        {
                            if (charInfo.minX != charInfo.maxX && charInfo.minY != charInfo.maxY)
                            {
                                triangles[charWritten * 6 + 0] = arrayIndex + 0;
                                triangles[charWritten * 6 + 1] = arrayIndex + 3;
                                triangles[charWritten * 6 + 2] = arrayIndex + 2;
                                triangles[charWritten * 6 + 3] = arrayIndex + 0;
                                triangles[charWritten * 6 + 4] = arrayIndex + 1;
                                triangles[charWritten * 6 + 5] = arrayIndex + 3;

                                vertices[arrayIndex] = new Vector3(characterSize * ((float)(xPos + (charInfo.minX) * widthScaling) + xOffset), characterSize * ((float)(yPos + charInfo.maxY) + yOffset), 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                                uv[arrayIndex] = charInfo.uvTopLeft;

                                ++arrayIndex;

                                vertices[arrayIndex] = new Vector3(characterSize * ((float)(xPos + (charInfo.maxX) * widthScaling) + xOffset), characterSize * ((float)(yPos + charInfo.maxY) + yOffset), 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                                uv[arrayIndex] = charInfo.uvTopRight;

                                ++arrayIndex;

                                vertices[arrayIndex] = new Vector3(characterSize * ((float)(xPos + (charInfo.minX) * widthScaling) + xOffset), characterSize * ((float)(yPos + charInfo.minY) + yOffset), 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                                uv[arrayIndex] = charInfo.uvBottomLeft;

                                ++arrayIndex;

                                vertices[arrayIndex] = new Vector3(characterSize * ((float)(xPos + (charInfo.maxX) * widthScaling) + xOffset), characterSize * ((float)(yPos + charInfo.minY) + yOffset), 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                                uv[arrayIndex] = charInfo.uvBottomRight;

                                ++arrayIndex;
                                ++charWritten;
                            }
                            xPos += (int)(charInfo.advance * widthScaling);
                        }
                    }
                }

                yPos -= lineAdvance;
            }

            int triangleLength = triangles.Length;
            for (int i = charWritten * 6; i < triangleLength; ++i)
            {
                triangles[i] = 0;
            }
            meshFilter.mesh.Clear();
            meshFilter.mesh.vertices = vertices;
            meshFilter.mesh.colors32 = colors32;
            meshFilter.mesh.tangents = tangents;
            meshFilter.mesh.uv = uv;
            meshFilter.mesh.triangles = triangles;
            meshFilter.mesh.RecalculateNormals();
            meshFilter.mesh.Optimize();
            // Can't hide mesh with (true), or we can't edit colors later.
            meshFilter.mesh.UploadMeshData(false);
        }

        /// <summary>
        /// Convert a simple text string into displayable quads with no
        /// additional processing (untagged text).
        /// </summary>
        private void GenerateText()
        {
            int maxVerts = 0;
            int numTextRows = textRow.Length;
            for (int line = 0; line < numTextRows; ++line)
            {
                textRow[line].textLength = 0;
                font.RequestCharactersInTexture(textRow[line].formattedData, fontSize);
                maxVerts += Font.GetMaxVertsForString(textRow[line].formattedData);

                int chCount = textRow[line].formattedData.Length;
                for (int ch = 0; ch < chCount; ++ch)
                {
                    CharacterInfo charInfo;
                    if (font.GetCharacterInfo(textRow[line].formattedData[ch], out charInfo))
                    {
                        textRow[line].textLength += charInfo.advance;
                    }
                }
            }

            if (maxVerts == 0)
            {
                // No renderable characters?
                meshRenderer.gameObject.SetActive(false);
                return;
            }

            meshRenderer.gameObject.SetActive(true);

            if (vertices.Length < maxVerts)
            {
                vertices = new Vector3[maxVerts];
                colors32 = new Color32[maxVerts];
                tangents = new Vector4[maxVerts];
                uv = new Vector2[maxVerts];

                int triLength = maxVerts + maxVerts / 2;
                triangles = new int[triLength];
            }

            int charWritten = 0;
            int arrayIndex = 0;
            int yPos = 0;
            int xAnchor = 0;
            switch (anchor_)
            {
                case TextAnchor.LowerCenter:
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) - font.ascent;
                    break;
                case TextAnchor.LowerLeft:
                    //xAnchor = 0;
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) - font.ascent;
                    break;
                case TextAnchor.LowerRight:
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) - font.ascent;
                    break;
                case TextAnchor.MiddleCenter:
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) / 2 - font.ascent;
                    break;
                case TextAnchor.MiddleLeft:
                    //xAnchor = 0;
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) / 2 - font.ascent;
                    break;
                case TextAnchor.MiddleRight:
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) / 2 - font.ascent;
                    break;
                case TextAnchor.UpperCenter:
                    yPos = -font.ascent;
                    break;
                case TextAnchor.UpperLeft:
                    //xAnchor = 0;
                    yPos = -font.ascent;
                    break;
                case TextAnchor.UpperRight:
                    yPos = -font.ascent;
                    break;
            }

            int lineAdvance = (int)(lineSpacing * font.lineHeight);
            for (int line = 0; line < numTextRows; ++line)
            {
                int xPos = 0;
                if (alignment_ == TextAlignment.Center)
                {
                    xPos = -(textRow[line].textLength) / 2;
                }
                else if (alignment_ == TextAlignment.Right)
                {
                    xPos = -textRow[line].textLength;
                }
                xPos += xAnchor;

                int stringLength = textRow[line].formattedData.Length;
                for (int ch = 0; ch < stringLength; ++ch)
                {
                    CharacterInfo charInfo;
                    if (font.GetCharacterInfo(textRow[line].formattedData[ch], out charInfo))
                    {
                        triangles[charWritten * 6 + 0] = arrayIndex + 0;
                        triangles[charWritten * 6 + 1] = arrayIndex + 3;
                        triangles[charWritten * 6 + 2] = arrayIndex + 2;
                        triangles[charWritten * 6 + 3] = arrayIndex + 0;
                        triangles[charWritten * 6 + 4] = arrayIndex + 1;
                        triangles[charWritten * 6 + 5] = arrayIndex + 3;

                        vertices[arrayIndex] = new Vector3(characterSize * (float)(xPos + charInfo.minX), characterSize * (float)(yPos + charInfo.maxY), 0.0f);
                        colors32[arrayIndex] = color;
                        tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                        uv[arrayIndex] = charInfo.uvTopLeft;

                        ++arrayIndex;

                        vertices[arrayIndex] = new Vector3(characterSize * (float)(xPos + charInfo.maxX), characterSize * (float)(yPos + charInfo.maxY), 0.0f);
                        colors32[arrayIndex] = color;
                        tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                        uv[arrayIndex] = charInfo.uvTopRight;

                        ++arrayIndex;

                        vertices[arrayIndex] = new Vector3(characterSize * (float)(xPos + charInfo.minX), characterSize * (float)(yPos + charInfo.minY), 0.0f);
                        colors32[arrayIndex] = color;
                        tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                        uv[arrayIndex] = charInfo.uvBottomLeft;

                        ++arrayIndex;

                        vertices[arrayIndex] = new Vector3(characterSize * (float)(xPos + charInfo.maxX), characterSize * (float)(yPos + charInfo.minY), 0.0f);
                        colors32[arrayIndex] = color;
                        tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                        uv[arrayIndex] = charInfo.uvBottomRight;

                        ++arrayIndex;

                        xPos += charInfo.advance;
                        ++charWritten;
                    }
                }

                yPos -= lineAdvance;
            }

            int triangleLength = triangles.Length;
            for (int i = charWritten * 6; i < triangleLength; ++i)
            {
                triangles[i] = 0;
            }
            meshFilter.mesh.Clear();
            meshFilter.mesh.vertices = vertices;
            meshFilter.mesh.colors32 = colors32;
            meshFilter.mesh.tangents = tangents;
            meshFilter.mesh.uv = uv;
            meshFilter.mesh.triangles = triangles;
            meshFilter.mesh.RecalculateNormals();
            meshFilter.mesh.Optimize();
            // Can't hide mesh with (true), or we can't edit colors later.
            meshFilter.mesh.UploadMeshData(false);
        }
        #endregion

        /// <summary>
        /// Convert the booleans for bold and italic text into a FontStyle.
        /// </summary>
        /// <param name="bold">Is the style bold?</param>
        /// <param name="italic">Is the style italic?</param>
        /// <returns></returns>
        public static FontStyle GetFontStyle(bool bold, bool italic)
        {
            if (bold)
            {
                return (italic) ? FontStyle.BoldAndItalic : FontStyle.Bold;
            }
            else if (italic)
            {
                return FontStyle.Italic;
            }
            else
            {
                return FontStyle.Normal;
            }
        }

        /// <summary>
        /// Replace the .cfg-safe values with processable values
        /// </summary>
        /// <param name="mangledText"></param>
        /// <returns></returns>
        public static string UnmangleText(string mangledText)
        {
            return mangledText.Replace("<=", "{").Replace("=>", "}").Replace("$$$", Environment.NewLine);
        }

        /// <summary>
        /// TextRow encapsulates one row of text.  It handles parsing variables
        /// and updating text (as stored in formattedData).
        /// </summary>
        private class TextRow
        {
            internal string formatString;
            internal string formattedData;
            internal MASFlightComputer.Variable[] variable;
            internal object[] evals;
            internal Action callback;
            internal int textLength;
            internal bool rowInvalidated;

            internal void EvaluateVariables()
            {
                if (rowInvalidated) // TODO: Use callbacks to set rowInvalidated
                {
                    if (variable == null || variable.Length == 0)
                    {
                        // Not sure why this path was hit - we should only be
                        // in this code if rowInvalidated is true, and that
                        // only happens when a variable changed.
                        formattedData = formatString;
                    }
                    else
                    {
                        for (int i = 0; i < variable.Length; ++i)
                        {
                            evals[i] = variable[i].RawValue();
                        }

                        formattedData = string.Format(formatter, formatString, evals);
                    }
                    rowInvalidated = false;
                }
            }
        }
    }
}
