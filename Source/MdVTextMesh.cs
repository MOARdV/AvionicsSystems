/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2018 MOARdV
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
        static internal MASStringFormatter formatter = new MASStringFormatter();

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

        private FontStyle fontStyle_ = UnityEngine.FontStyle.Normal;
        public FontStyle fontStyle
        {
            get
            {
                return fontStyle_;
            }
            set
            {
                if (value != fontStyle_)
                {
                    invalidated = true;
                    fontStyle_ = value;
                }
            }
        }

        private Font font;
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private TextRow[] textRow = new TextRow[0];
        private Color32 color;
        private int fontSize;
        private float characterScalar = 1.0f;
        private float lineSpacing = 1.0f;
        private int fixedAdvance = 0;
        private int fixedLineSpacing = 0;
        private bool boundedText = false;
        private bool richText;
        private bool invalidated;
        private bool colorInvalidated;
        private bool dynamic;
        private VariableRegistrar variableRegistrar;
        private bool configured = false;

        // To avoid piles of garbage creation, keep the local arrays here so
        // we do not allocate them every update.  Intentionally set their sizes
        // to zero to trigger a reallocation on first use.
        Vector3[] vertices = new Vector3[0];
        Color32[] colors32 = new Color32[0];
        Vector4[] tangents = new Vector4[0];
        Vector2[] uv = new Vector2[0];
        int[] triangles = new int[0];

        // We use only one tangent value ever.
        private readonly Vector4 tangent = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);

        internal static readonly string[] VariableListSeparator = { "$&$", "$#$" };

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
        public void SetFont(Font font, int fontSize, float characterScalar)
        {
            this.font = font;
            this.fontSize = fontSize;
            this.characterScalar = characterScalar;
            this.dynamic = font.dynamic;
            boundedText = false;
            meshRenderer.material.mainTexture = font.material.mainTexture;
            invalidated = true;
        }

        /// <summary>
        /// Set the font and define the bounding box for characters (in pixels)
        /// </summary>
        /// <param name="font"></param>
        /// <param name="fontDimensions"></param>
        public void SetFont(Font font, Vector2 fontDimensions)
        {
            this.dynamic = font.dynamic;
            float characterScalar;
            if (this.dynamic)
            {
                // lineHeight isn't always valid, apparently.  The Digital-7 fonts
                // included in MAS have a lineHeight 17, even though the ascent is
                // 23.  So, add this little hack to try to handle that case.
                characterScalar = fontDimensions.y / (float)Math.Max(font.lineHeight, font.fontSize);
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

            this.font = font;
            this.fixedAdvance = (int)fontDimensions.x;
            this.fixedLineSpacing = (int)fontDimensions.y;
            this.characterScalar = characterScalar;
            boundedText = true;
            meshRenderer.material.mainTexture = font.material.mainTexture;
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
            variableRegistrar = new VariableRegistrar(comp, internalProp);

            configured = false;
            // Do some up-front processing:

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
                // If there are no '$&$' or '$#$' tokens, then the text has no variables,
                // and it doesn't change.
                if (immutable || !(text.Contains(VariableListSeparator[0]) || text.Contains(VariableListSeparator[1])))
                {
                    string[] textRows = text.Split(Utility.LineSeparator, StringSplitOptions.None);
                    textRow = new TextRow[textRows.Length];

                    for (int i = 0; i < textRows.Length; ++i)
                    {
                        TextRow tr = new TextRow();

                        if (textRows[i].Contains(VariableListSeparator[0]) || textRows[i].Contains(VariableListSeparator[1]))
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
                    // preprocessing - split into rows
                    string[] textRows = text.Split(Utility.LineSeparator, StringSplitOptions.None);
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
                            tr.variable = new Variable[variables.Length];
                            tr.evals = new object[variables.Length];
                            tr.callback = (double dontCare) => { invalidated = true; tr.rowInvalidated = true; };
                            for (int var = 0; var < tr.variable.Length; ++var)
                            {
                                try
                                {
                                    tr.variable[var] = variableRegistrar.RegisterVariableChangeCallback(variables[var], tr.callback);
                                }
                                catch (Exception e)
                                {
                                    Utility.LogError(this, "Variable {0} threw an exception", variables[var]);
                                    throw e;
                                }
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
                tr.variable = new Variable[variables.Length];
                tr.evals = new object[variables.Length];
                tr.callback = (double dontCare) => { invalidated = true; tr.rowInvalidated = true; };
                for (int var = 0; var < tr.variable.Length; ++var)
                {
                    tr.variable[var] = variableRegistrar.RegisterVariableChangeCallback(variables[var], tr.callback);
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
            Font.textureRebuilt += FontRebuiltCallback;
            CreateComponents();
        }

        /// <summary>
        /// Make sure we don't leave our callback lingering.
        /// </summary>
        public void OnDestroy()
        {
            Font.textureRebuilt -= FontRebuiltCallback;

            for (int i = textRow.Length - 1; i >= 0; --i)
            {
                if (textRow[i].variable != null)
                {
                    for (int var = 0; var < textRow[i].variable.Length; ++var)
                    {
                        textRow[i].variable[var] = null;
                    }
                    textRow[i].variable = null;
                    textRow[i].callback = null;
                }
            }
            variableRegistrar.ReleaseResources();

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
                Utility.LogError(this, "Trapped exception:");
                Utility.LogError(this, e.ToString());
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
                invalidated = true;
                meshRenderer.material.mainTexture = whichFont.material.mainTexture;
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
                    Utility.LogError(this, "Exception caught creating MdVTextMesh components:");
                    Utility.LogError(this, e.ToString());
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
            // Picked an arbitrary value for non-dynamic ascent.
            int ascent = (dynamic) ? font.ascent : (int)(0.8125f * fixedLineSpacing);
            // Smallest value of minY to avoid descenders overlapping the next line of text.
            int minimumY = ascent - (int)((float)fixedLineSpacing / characterScalar);
            //size = something.

            // Determine text length
            int maxVerts = 0;
            int numTextRows = textRow.Length;
            float widthScaling = 1.0f;
            for (int line = 0; line < numTextRows; ++line)
            {
                bold = false;
                italic = false;
                widthScaling = 1.0f;
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
                        else if (tagText[0] == 'n')
                        {
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
                        else if (tagText == "sup")
                        {
                            // RPM "superscript" tag.  We'll consume it, but not do anything with it.
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/sup")
                        {
                            // RPM "superscript" tag.  We'll consume it, but not do anything with it.
                            charIndex += nextBracket + 1;
                        }
                        else // Else we didn't recognise anything so it's not a tag.
                        {
                            break;
                        }
                    }

                    if (charIndex < stringLength)
                    {
                        if (dynamic)
                        {
                            FontStyle style = GetFontStyle(bold, italic);
                            font.RequestCharactersInTexture(escapedBracket ? "[" : textRow[line].formattedData[charIndex].ToString(), fontSize, style);
                            CharacterInfo charInfo;
                            if (font.GetCharacterInfo(escapedBracket ? '[' : textRow[line].formattedData[charIndex], out charInfo, 0, style))
                            {
                                textRow[line].textLength += fixedAdvance;
                                maxVerts += 4;
                            }
                        }
                        else
                        {
                            font.RequestCharactersInTexture(escapedBracket ? "[" : textRow[line].formattedData[charIndex].ToString());
                            CharacterInfo charInfo;
                            if (font.GetCharacterInfo(escapedBracket ? '[' : textRow[line].formattedData[charIndex], out charInfo))
                            {
                                textRow[line].textLength += fixedAdvance;
                                maxVerts += 4;
                            }
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
            int yPos = (int)(-ascent * characterScalar);
            int xAnchor = 0;

            //Utility.LogMessage(this, "Font {0}: ascent = {1}, fontSize = {2}, lineHeight = {3}",
            //    font.fontNames[0], ascent, font.fontSize, font.lineHeight);
            for (int line = 0; line < numTextRows; ++line)
            {
                bold = false;
                italic = false;
                widthScaling = 1.0f;
                int xPos = xAnchor;

                Color32 fontColor = color;
                float xOffset = 0.0f;
                float yOffset = 0.0f;

                int stringLength = textRow[line].formattedData.Length;
                for (int charIndex = 0; charIndex < stringLength; charIndex++)
                {
                    bool lastWasNewline = false;
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
                        else if (tagText[0] == 'n')
                        {
                            if (!lastWasNewline)
                            {
                                xPos -= fixedAdvance;
                            }
                            float newlineAdvance = 1.0f;
                            if (tagText.Length > 1)
                            {
                                if (!float.TryParse(tagText.Substring(1), out newlineAdvance))
                                {
                                    newlineAdvance = 1.0f;
                                }
                            }
                            yPos -= (int)(fixedLineSpacing * newlineAdvance);
                            lastWasNewline = true;
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
                        else if (tagText == "sup")
                        {
                            // RPM "superscript" tag.  We'll consume it, but not do anything with it.
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/sup")
                        {
                            // RPM "superscript" tag.  We'll consume it, but not do anything with it.
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
                        bool fetched;
                        if (dynamic)
                        {
                            fetched = font.GetCharacterInfo(escapedBracket ? '[' : textRow[line].formattedData[charIndex], out charInfo, 0, style);
                        }
                        else
                        {
                            fetched = font.GetCharacterInfo(escapedBracket ? '[' : textRow[line].formattedData[charIndex], out charInfo);
                        }
                        if (fetched)
                        {
                            if (charInfo.minX != charInfo.maxX && charInfo.minY != charInfo.maxY)
                            {
                                // Reminders for next time I need to tweak things:
                                // charInfo .glyphWidth and .glyphHeight describe the footprint of the character.
                                // charInfo.bearing is the X displacement from the origin to the start of the glyph.
                                // charInfo.advance is the advance to the next character.  Ideally, a fixed font will
                                // have .advance * characterSize == fixedAdvance.
                                // font.ascent is the distance from the baseline to the top of the glyphs.
                                // if (font.ascent - charInfo.minY) * characterSize > fixedLineSpacing, the descender
                                // will hang over to the next line.  This seems to be a problem more with variable-advance
                                // fonts (Arial, for instance) than with convenient fixed-width fonts (Inconsolata Go).

                                //if (charInfo.glyphWidth > 2 && (charInfo.glyphHeight * characterSize) > fixedLineSpacing)
                                //{
                                //    Utility.LogMessage(this, "{0}: glyph width = {1}, advance = {2}, x span {3} - {4}, height {5}, y span {6} - {7}",
                                //       textRow[line].formattedData[charIndex], charInfo.glyphWidth, charInfo.advance,
                                //       charInfo.minX, charInfo.maxX,
                                //       charInfo.glyphHeight,
                                //       charInfo.minY, charInfo.maxY);
                                //}

                                float minX, maxX;
                                // Some characters have a large advance (Inconsolata-Go filled triangle (arrowhead)
                                // and delta characters, for instance).  Instead of letting them overwrite
                                // neighboring characters, force them to fit the fixedAdvance space.
                                // This also affects wide characters in variable-advance fonts.
                                if ((int)(charInfo.advance * characterScalar) > fixedAdvance)
                                {
                                    // Proportion the character to its advance
                                    float scaledCharacter = fixedAdvance / (float)charInfo.advance;
                                    minX = Mathf.Max(0.0f, charInfo.minX * scaledCharacter) * widthScaling;
                                    maxX = Mathf.Min(fixedAdvance, charInfo.maxX * scaledCharacter) * widthScaling;
                                }
                                else if ((int)(charInfo.advance * characterScalar) < fixedAdvance)
                                {
                                    // Characters that have smaller advance than our fixed-size setting
                                    // need to be pushed towards the center so they don't look out of place.
                                    int nudge = (fixedAdvance - (int)(charInfo.advance * characterScalar)) / 2;

                                    minX = (nudge + charInfo.minX * characterScalar) * widthScaling;
                                    maxX = (nudge + charInfo.maxX * characterScalar) * widthScaling;
                                }
                                else
                                {
                                    minX = charInfo.minX * characterScalar * widthScaling;
                                    maxX = charInfo.maxX * characterScalar * widthScaling;
                                }
                                minX += (float)xPos + xOffset;
                                maxX += (float)xPos + xOffset;

                                float minY;
                                float maxY;

                                // Excessively tall characters need tweaked to fit
                                maxY = Math.Min(charInfo.maxY, ascent) * characterScalar;
                                minY = Math.Max(charInfo.minY, minimumY) * characterScalar;

                                minY += yPos + yOffset;
                                maxY += yPos + yOffset;

                                triangles[charWritten * 6 + 0] = arrayIndex + 0;
                                triangles[charWritten * 6 + 1] = arrayIndex + 3;
                                triangles[charWritten * 6 + 2] = arrayIndex + 2;
                                triangles[charWritten * 6 + 3] = arrayIndex + 0;
                                triangles[charWritten * 6 + 4] = arrayIndex + 1;
                                triangles[charWritten * 6 + 5] = arrayIndex + 3;

                                vertices[arrayIndex] = new Vector3(minX, maxY, 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = tangent;
                                uv[arrayIndex] = charInfo.uvTopLeft;

                                ++arrayIndex;

                                vertices[arrayIndex] = new Vector3(maxX, maxY, 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = tangent;
                                uv[arrayIndex] = charInfo.uvTopRight;

                                ++arrayIndex;

                                vertices[arrayIndex] = new Vector3(minX, minY, 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = tangent;
                                uv[arrayIndex] = charInfo.uvBottomLeft;

                                ++arrayIndex;

                                vertices[arrayIndex] = new Vector3(maxX, minY, 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = tangent;
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
            // Picked an arbitrary value for non-dynamic ascent.
            int ascent = (dynamic) ? font.ascent : (int)(0.8125f * fixedLineSpacing);
            //size = something.

            // Determine text length
            int maxVerts = 0;
            int numTextRows = textRow.Length;
            float widthScaling = 1.0f;
            for (int line = 0; line < numTextRows; ++line)
            {
                bold = false;
                italic = false;
                widthScaling = 1.0f;
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
                        else if (tagText == "sup")
                        {
                            // RPM "superscript" tag.  We'll consume it, but not do anything with it.
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/sup")
                        {
                            // RPM "superscript" tag.  We'll consume it, but not do anything with it.
                            charIndex += nextBracket + 1;
                        }
                        else // Else we didn't recognise anything so it's not a tag.
                        {
                            break;
                        }
                    }

                    if (charIndex < stringLength)
                    {
                        if (dynamic)
                        {
                            FontStyle style = GetFontStyle(bold, italic);
                            font.RequestCharactersInTexture(escapedBracket ? "[" : textRow[line].formattedData[charIndex].ToString(), fontSize, style);
                            CharacterInfo charInfo;
                            if (font.GetCharacterInfo(escapedBracket ? '[' : textRow[line].formattedData[charIndex], out charInfo, 0, style))
                            {
                                textRow[line].textLength += charInfo.advance;
                                maxVerts += 4;
                            }
                        }
                        else
                        {
                            font.RequestCharactersInTexture(escapedBracket ? "[" : textRow[line].formattedData[charIndex].ToString());
                            CharacterInfo charInfo;
                            if (font.GetCharacterInfo(escapedBracket ? '[' : textRow[line].formattedData[charIndex], out charInfo))
                            {
                                textRow[line].textLength += charInfo.advance;
                                maxVerts += 4;
                            }
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
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) - ascent;
                    break;
                case TextAnchor.LowerLeft:
                    //xAnchor = 0;
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) - ascent;
                    break;
                case TextAnchor.LowerRight:
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) - ascent;
                    break;
                case TextAnchor.MiddleCenter:
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) / 2 - ascent;
                    break;
                case TextAnchor.MiddleLeft:
                    //xAnchor = 0;
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) / 2 - ascent;
                    break;
                case TextAnchor.MiddleRight:
                    yPos = (int)(lineSpacing * font.lineHeight * numTextRows) / 2 - ascent;
                    break;
                case TextAnchor.UpperCenter:
                    yPos = -ascent;
                    break;
                case TextAnchor.UpperLeft:
                    //xAnchor = 0;
                    yPos = -ascent;
                    break;
                case TextAnchor.UpperRight:
                    yPos = -ascent;
                    break;
            }

            int lineAdvance = (int)(lineSpacing * font.lineHeight);
            for (int line = 0; line < numTextRows; ++line)
            {
                bold = false;
                italic = false;
                widthScaling = 1.0f;
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
                        else if (tagText == "sup")
                        {
                            // RPM "superscript" tag.  We'll consume it, but not do anything with it.
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/sup")
                        {
                            // RPM "superscript" tag.  We'll consume it, but not do anything with it.
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
                        bool fetched;
                        if (dynamic)
                        {
                            fetched = font.GetCharacterInfo(escapedBracket ? '[' : textRow[line].formattedData[charIndex], out charInfo, 0, style);
                        }
                        else
                        {
                            fetched = font.GetCharacterInfo(escapedBracket ? '[' : textRow[line].formattedData[charIndex], out charInfo);
                        }
                        if (fetched)
                        {
                            if (charInfo.minX != charInfo.maxX && charInfo.minY != charInfo.maxY)
                            {
                                triangles[charWritten * 6 + 0] = arrayIndex + 0;
                                triangles[charWritten * 6 + 1] = arrayIndex + 3;
                                triangles[charWritten * 6 + 2] = arrayIndex + 2;
                                triangles[charWritten * 6 + 3] = arrayIndex + 0;
                                triangles[charWritten * 6 + 4] = arrayIndex + 1;
                                triangles[charWritten * 6 + 5] = arrayIndex + 3;

                                vertices[arrayIndex] = new Vector3(characterScalar * ((float)(xPos + (charInfo.minX) * widthScaling) + xOffset), characterScalar * ((float)(yPos + charInfo.maxY) + yOffset), 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = tangent;
                                uv[arrayIndex] = charInfo.uvTopLeft;

                                ++arrayIndex;

                                vertices[arrayIndex] = new Vector3(characterScalar * ((float)(xPos + (charInfo.maxX) * widthScaling) + xOffset), characterScalar * ((float)(yPos + charInfo.maxY) + yOffset), 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = tangent;
                                uv[arrayIndex] = charInfo.uvTopRight;

                                ++arrayIndex;

                                vertices[arrayIndex] = new Vector3(characterScalar * ((float)(xPos + (charInfo.minX) * widthScaling) + xOffset), characterScalar * ((float)(yPos + charInfo.minY) + yOffset), 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = tangent;
                                uv[arrayIndex] = charInfo.uvBottomLeft;

                                ++arrayIndex;

                                vertices[arrayIndex] = new Vector3(characterScalar * ((float)(xPos + (charInfo.maxX) * widthScaling) + xOffset), characterScalar * ((float)(yPos + charInfo.minY) + yOffset), 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = tangent;
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

                        vertices[arrayIndex] = new Vector3(characterScalar * (float)(xPos + charInfo.minX), characterScalar * (float)(yPos + charInfo.maxY), 0.0f);
                        colors32[arrayIndex] = color;
                        tangents[arrayIndex] = tangent;
                        uv[arrayIndex] = charInfo.uvTopLeft;

                        ++arrayIndex;

                        vertices[arrayIndex] = new Vector3(characterScalar * (float)(xPos + charInfo.maxX), characterScalar * (float)(yPos + charInfo.maxY), 0.0f);
                        colors32[arrayIndex] = color;
                        tangents[arrayIndex] = tangent;
                        uv[arrayIndex] = charInfo.uvTopRight;

                        ++arrayIndex;

                        vertices[arrayIndex] = new Vector3(characterScalar * (float)(xPos + charInfo.minX), characterScalar * (float)(yPos + charInfo.minY), 0.0f);
                        colors32[arrayIndex] = color;
                        tangents[arrayIndex] = tangent;
                        uv[arrayIndex] = charInfo.uvBottomLeft;

                        ++arrayIndex;

                        vertices[arrayIndex] = new Vector3(characterScalar * (float)(xPos + charInfo.maxX), characterScalar * (float)(yPos + charInfo.minY), 0.0f);
                        colors32[arrayIndex] = color;
                        tangents[arrayIndex] = tangent;
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
        public FontStyle GetFontStyle(bool bold, bool italic)
        {
            if (bold)
            {
                return (italic) ? UnityEngine.FontStyle.BoldAndItalic : UnityEngine.FontStyle.Bold;
            }
            else if (italic)
            {
                return UnityEngine.FontStyle.Italic;
            }
            else
            {
                return fontStyle_;
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
        internal class TextRow
        {
            internal string formatString;
            internal string formattedData;
            internal Variable[] variable;
            internal object[] evals;
            internal Action<double> callback;
            internal int textLength;
            internal bool rowInvalidated;

            internal void EvaluateVariables()
            {
                if (rowInvalidated)
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
                            evals[i] = variable[i].AsObject();
                        }

                        formattedData = string.Format(formatter, formatString, evals);
                    }
                    rowInvalidated = false;
                }
            }
        }

        /// <summary>
        /// Convert a FontStyle string to an enumeraction, falling back to FontStyle.Normal on error.
        /// </summary>
        /// <param name="styleStr"></param>
        /// <returns></returns>
        internal static FontStyle FontStyle(string styleStr)
        {
            if (!string.IsNullOrEmpty(styleStr))
            {
                if (styleStr == UnityEngine.FontStyle.Bold.ToString())
                {
                    return UnityEngine.FontStyle.Bold;
                }
                else if (styleStr == UnityEngine.FontStyle.Italic.ToString())
                {
                    return UnityEngine.FontStyle.Italic;
                }
                else if (styleStr == UnityEngine.FontStyle.BoldAndItalic.ToString())
                {
                    return UnityEngine.FontStyle.BoldAndItalic;
                }
            }
            return UnityEngine.FontStyle.Normal;
        }
    }
}
