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
    public class MASMonitor : InternalModule
    {
        [KSPField]
        public string screenTransform = string.Empty;

        [KSPField]
        public string layer = "_Emissive";

        [KSPField]
        public Vector2 screenSize = Vector2.zero;
        private int screenWidth, screenHeight;

        [KSPField]
        public Vector2 fontSize = Vector2.zero;

        [KSPField]
        public string font = string.Empty;

        [KSPField]
        public string textColor;
        internal Color32 textColor_;

        [KSPField]
        public string backgroundColor;
        private Color32 backgroundColor_;

        [KSPField]
        public string monitorID;
        private MASFlightComputer.Variable pageSelector;

        private RenderTexture screen;
        private GameObject screenSpace;
        private Camera screenCamera;
        private Dictionary<string, MASPage> page = new Dictionary<string, MASPage>();
        private MASPage currentPage;
        internal Font defaultFont;

        private bool initialized = false;

        internal static readonly float maxDepth = 1.0f - depthDelta;
        internal static readonly float minDepth = 0.5f;
        internal static readonly float depthDelta = 0.015625f;
        internal static readonly int drawingLayer = 30; // Pick a layer KSP isn't using.

        /// <summary>
        /// Startup, initialize, configure, etc.
        /// </summary>
        public void Start()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                try
                {
                    MASFlightComputer comp = MASFlightComputer.Instance(internalProp.part);

                    if (string.IsNullOrEmpty(screenTransform))
                    {
                        throw new ArgumentException("Missing 'transform' in MASMonitor");
                    }

                    if (string.IsNullOrEmpty(layer))
                    {
                        throw new ArgumentException("Missing 'layer' in MASMonitor");
                    }

                    if (string.IsNullOrEmpty(font))
                    {
                        throw new ArgumentException("Missing 'font' in MASMonitor");
                    }

                    if (string.IsNullOrEmpty(textColor))
                    {
                        throw new ArgumentException("Missing 'textColor' in MASMonitor");
                    }
                    else
                    {
                        textColor_ = Utility.ParseColor32(textColor, comp);
                    }

                    if (string.IsNullOrEmpty(backgroundColor))
                    {
                        throw new ArgumentException("Missing 'backgroundColor' in MASMonitor");
                    }
                    else
                    {
                        backgroundColor_ = Utility.ParseColor32(backgroundColor, comp);
                    }

                    if (screenSize.x <= 0.0f || screenSize.y <= 0.0f)
                    {
                        throw new ArgumentException("Invalid 'screenSize' in MASMonitor");
                    }

                    if (fontSize.x <= 0.0f || fontSize.y <= 0.0f)
                    {
                        throw new ArgumentException("Invalid 'fontSize' in MASMonitor");
                    }

                    screenWidth = (int)screenSize.x;
                    screenHeight = (int)screenSize.y;

                    screenSpace = new GameObject();
                    screenSpace.name = "MASMonitor-" + screenSpace.GetInstanceID();
                    screenSpace.layer = drawingLayer;
                    screenSpace.transform.position = Vector3.zero;
                    screenSpace.SetActive(true);

                    screen = new RenderTexture(screenWidth, screenHeight, 24, RenderTextureFormat.ARGB32);
                    if (!screen.IsCreated())
                    {
                        screen.Create();
                    }

                    screenCamera = screenSpace.AddComponent<Camera>();
                    screenCamera.enabled = true; // Enable = "auto-draw"
                    screenCamera.orthographic = true;
                    screenCamera.aspect = screenSize.x / screenSize.y;
                    screenCamera.eventMask = 0;
                    screenCamera.farClipPlane = 1.0f + depthDelta;
                    screenCamera.nearClipPlane = depthDelta;
                    screenCamera.orthographicSize = screenSize.y * 0.5f;
                    screenCamera.cullingMask = 1 << drawingLayer;
                    screenCamera.transparencySortMode = TransparencySortMode.Orthographic;
                    screenCamera.transform.position = Vector3.zero;
                    screenCamera.transform.LookAt(new Vector3(0.0f, 0.0f, maxDepth), Vector3.up);
                    screenCamera.backgroundColor = backgroundColor_;
                    screenCamera.clearFlags = CameraClearFlags.SolidColor;
                    screenCamera.targetTexture = screen;

                    Material screenMat = internalProp.FindModelTransform(screenTransform).GetComponent<Renderer>().material;
                    string[] layers = layer.Split();
                    for (int i = layers.Length - 1; i >= 0; --i)
                    {
                        screenMat.SetTexture(layers[i].Trim(), screen);
                    }

                    defaultFont = MASLoader.GetFont(font.Trim());

                    ConfigNode moduleConfig = Utility.GetPropModuleConfigNode(internalProp.propName, moduleID);
                    if (moduleConfig == null)
                    {
                        throw new ArgumentNullException("No ConfigNode found for MASMonitor in " + internalProp.propName + "!");
                    }

                    string[] pages = moduleConfig.GetValues("page");
                    int numPages = pages.Length;
                    for (int i = 0; i < numPages; ++i)
                    {
                        pages[i] = pages[i].Trim();
                        ConfigNode pageConfig = Utility.GetPageConfigNode(pages[i]);
                        if (pageConfig == null)
                        {
                            throw new ArgumentException("No ConfigNode found for page " + pages[i] + " in MASMonitor in " + internalProp.propName + "!");
                        }

                        // Parse the page node
                        MASPage newPage = new MASPage(pageConfig, internalProp, comp, this, screenSpace.transform);
                        if (i == 0)
                        {
                            // Select the default page as the current page
                            currentPage = newPage;
                        }

                        newPage.EnablePage(false);

                        page.Add(pages[i], newPage);
                        Utility.LogMessage(this, "Page = {0}", pages[i]);
                    }
                    //HackWalkTransforms(screenSpace.transform, 0);
                    string variableName = "fc.GetPersistent(\"" + monitorID.Trim() +"\")";
                    pageSelector = comp.RegisterOnVariableChange(variableName, internalProp, PageChanged);
                    // See if we have a saved page to restore.
                    if (!string.IsNullOrEmpty(pageSelector.String()) && page.ContainsKey(pageSelector.String()))
                    {
                        currentPage = page[pageSelector.String()];
                    }
                    currentPage.EnablePage(true);
                    initialized = true;
                    Utility.LogMessage(this, "Configuration complete in prop #{0} ({1}) with {2} pages", internalProp.propID, internalProp.propName, numPages);
                }
                catch (Exception e)
                {
                    Utility.ComplainLoudly("MASMonitor configuration failed.");
                    Utility.LogErrorMessage(this, "Failed to configure prop #{0} ({1})", internalProp.propID, internalProp.propName);
                    Utility.LogErrorMessage(this, e.ToString());
                }
            }
        }

        private static void HackWalkTransforms(Transform transform, int p)
        {
            StringBuilder sb = new StringBuilder(p + 3);
            for (int i = 0; i < p; ++i)
            {
                sb.Append(" ");
            }
            sb.Append("+");
            sb.Append(transform.name);
            Utility.LogMessage(transform, "{0} @ ({1:0.0}, {2:0.0}, {3:0.000}) face ({4:0.0}, {5:0.0}, {6:0.0})", sb.ToString(), transform.position.x, transform.position.y, transform.position.z, transform.eulerAngles.x, transform.eulerAngles.y, transform.eulerAngles.z);
            for (int i = 0; i < transform.childCount; ++i)
            {
                HackWalkTransforms(transform.GetChild(i), p + 1);
            }
        }

        /// <summary>
        /// Tear things down
        /// </summary>
        public void OnDestroy()
        {
            if (screen != null)
            {
                screen.Release();
                screen = null;
            }

            if (initialized)
            {
                MASFlightComputer comp = MASFlightComputer.Instance(internalProp.part);

                comp.UnregisterOnVariableChange(pageSelector.name, internalProp, PageChanged);

                foreach (var value in page.Values)
                {
                    value.ReleaseResources(comp, internalProp);
                }
                page.Clear();

                GameObject.DestroyObject(screenSpace);
                screenSpace = null;
                screenCamera = null;
            }
        }

        /// <summary>
        /// Just in case we lose context.
        /// </summary>
        public override void OnUpdate()
        {
            if (initialized)
            {
                if (!screen.IsCreated())
                {
                    screen.Create();
                    screenCamera.targetTexture = screen;
                }
            }
        }

        /// <summary>
        /// Callback fired when our variable changes.
        /// </summary>
        private void PageChanged()
        {
            try
            {
                MASPage newPage = page[pageSelector.String()];

                currentPage.EnablePage(false);
                currentPage = newPage;
                currentPage.EnablePage(true);
            }
            catch
            {
                Utility.LogErrorMessage(this, "Unable to switch to page '" + pageSelector.String() + "'");
            }
        }
    }
}
