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
        public string style = string.Empty;

        [KSPField]
        public string textColor;
        internal Color32 textColor_;

        [KSPField]
        public string backgroundColor;
        private Color32 backgroundColor_;

        [KSPField]
        public string monitorID;
        private Variable pageSelector;

        [KSPField]
        public string startupScript;

        private RenderTexture screen;
        private GameObject screenSpace;
        private Camera screenCamera;
        private Dictionary<string, MASPage> page = new Dictionary<string, MASPage>();
        private MASPage currentPage;
        internal Font defaultFont;
        internal FontStyle defaultStyle = FontStyle.Normal;

        private bool initialized = false;

        internal static readonly float maxDepth = 1.0f - depthDelta;
        internal static readonly float minDepth = 0.5f;
        internal static readonly float depthDelta = 1.0f / 256.0f;
        internal static readonly int drawingLayer = 30; // Pick a layer KSP isn't using.

        /// <summary>
        /// We need to disable our page when it's not our turn to draw, which
        /// also means we need to enable it again to draw.  But we don't want
        /// to disable the game objects, or they won't update when it's their
        /// turn in OnUpdate().  So we disable rendering instead.
        /// </summary>
        /// <param name="cam"></param>
        private void EnablePage(Camera cam)
        {
            if (cam.Equals(screenCamera))
            {
                currentPage.RenderPage(true);
            }
        }

        /// <summary>
        /// We need to disable our page when it's not our turn to draw, which
        /// also means we need to enable it again to draw.  But we don't want
        /// to disable the game objects, or they won't update when it's their
        /// turn in OnUpdate().  So we disable rendering instead.
        /// </summary>
        /// <param name="cam"></param>
        private void DisablePage(Camera cam)
        {
            if (cam.Equals(screenCamera))
            {
                currentPage.RenderPage(false);
            }
        }

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
                    screenSpace.name = Utility.ComposeObjectName(internalProp.propName, this.GetType().Name, screenSpace.GetInstanceID());
                    screenSpace.layer = drawingLayer;
                    screenSpace.transform.position = Vector3.zero;
                    screenSpace.SetActive(true);

                    screen = new RenderTexture(screenWidth, screenHeight, 24, RenderTextureFormat.ARGB32);
                    if (!screen.IsCreated())
                    {
                        screen.Create();
                        screen.DiscardContents();
                    }

                    Camera.onPreCull += EnablePage;
                    Camera.onPostRender += DisablePage;

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

                    if (!string.IsNullOrEmpty(style))
                    {
                        defaultStyle = MdVTextMesh.FontStyle(style.Trim());
                    }

                    ConfigNode moduleConfig = Utility.GetPropModuleConfigNode(internalProp.propName, ClassName);
                    if (moduleConfig == null)
                    {
                        throw new ArgumentNullException("No ConfigNode found for MASMonitor in " + internalProp.propName + "!");
                    }

                    // If an initialization script was supplied, call it.
                    if (!string.IsNullOrEmpty(startupScript))
                    {
                        Action startup = comp.GetAction(startupScript, internalProp);
                        startup();
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

                        newPage.SetPageActive(false);

                        page.Add(pages[i], newPage);
                        //Utility.LogMessage(this, "Page = {0}", pages[i]);
                    }
                    //HackWalkTransforms(screenSpace.transform, 0);
                    if (!string.IsNullOrEmpty(monitorID))
                    {
                        string variableName = "fc.GetPersistent(\"" + monitorID.Trim() + "\")";
                        pageSelector = comp.RegisterOnVariableChange(variableName, internalProp, PageChanged);
                        // See if we have a saved page to restore.
                        if (!string.IsNullOrEmpty(pageSelector.AsString()) && page.ContainsKey(pageSelector.AsString()))
                        {
                            currentPage = page[pageSelector.AsString()];
                        }
                        comp.RegisterMonitor(monitorID, internalProp, this);
                    }
                    currentPage.SetPageActive(true);
                    initialized = true;
                    Utility.LogMessage(this, "Configuration complete in prop #{0} ({1}) with {2} pages", internalProp.propID, internalProp.propName, numPages);
                }
                catch (Exception e)
                {
                    Utility.ComplainLoudly("MASMonitor configuration failed.");
                    Utility.LogError(this, "Failed to configure prop #{0} ({1})", internalProp.propID, internalProp.propName);
                    Utility.LogError(this, e.ToString());
                }
            }
        }

        //private static void HackWalkTransforms(Transform transform, int p)
        //{
        //    StringBuilder sb = new StringBuilder(p + 3);
        //    for (int i = 0; i < p; ++i)
        //    {
        //        sb.Append(" ");
        //    }
        //    sb.Append("+");
        //    sb.Append(transform.name);
        //    Utility.LogMessage(transform, "{0} @ ({1:0.0}, {2:0.0}, {3:0.000}) face ({4:0.0}, {5:0.0}, {6:0.0})", sb.ToString(), transform.position.x, transform.position.y, transform.position.z, transform.eulerAngles.x, transform.eulerAngles.y, transform.eulerAngles.z);
        //    for (int i = 0; i < transform.childCount; ++i)
        //    {
        //        HackWalkTransforms(transform.GetChild(i), p + 1);
        //    }
        //}

        /// <summary>
        /// Tear things down
        /// </summary>
        public void OnDestroy()
        {
            Camera.onPreCull -= EnablePage;
            Camera.onPostRender -= DisablePage;

            if (screenCamera != null)
            {
                screenCamera.targetTexture = null;
            }

            if (screen != null)
            {
                screen.Release();
                screen = null;
            }

            if (initialized)
            {
                MASFlightComputer comp = MASFlightComputer.Instance(internalProp.part);

                if (pageSelector != null)
                {
                    comp.UnregisterOnVariableChange(pageSelector.name, internalProp, PageChanged);
                }

                foreach (var value in page.Values)
                {
                    value.ReleaseResources(comp, internalProp);
                }
                page.Clear();

                comp.UnregisterMonitor(monitorID, internalProp, this);

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
        /// Call the startupScript, if it exists.
        /// </summary>
        /// <param name="comp">The MASFlightComputer for this prop.</param>
        /// <returns>true if a script exists, false otherwise.</returns>
        internal bool RunStartupScript(MASFlightComputer comp)
        {
            if (!string.IsNullOrEmpty(startupScript))
            {
                Action startup = comp.GetAction(startupScript, internalProp);
                startup();
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Method to handle softkeys.
        /// </summary>
        /// <param name="keyId">The keyId to process</param>
        /// <returns>True if the softkey was handled, false if it was ignored.</returns>
        internal bool HandleSoftkey(int keyId)
        {
            if (currentPage != null)
            {
                return currentPage.HandleSoftkey(keyId);
            }

            return false;
        }

        /// <summary>
        /// Callback fired when our variable changes.
        /// </summary>
        private void PageChanged()
        {
            try
            {
                MASPage newPage = page[pageSelector.AsString()];

                currentPage.SetPageActive(false);
                currentPage = newPage;
                currentPage.SetPageActive(true);
            }
            catch
            {
                Utility.LogError(this, "Unable to switch to page '" + pageSelector.AsString() + "'");
            }
        }
    }
}
