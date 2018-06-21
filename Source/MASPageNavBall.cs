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
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// The navball component renders a NavBall for a monitor.
    /// </summary>
    internal class MASPageNavBall : IMASMonitorComponent
    {
        private GameObject imageObject;
        private GameObject cameraObject;
        private GameObject navballModel;
        private GameObject[] markers = new GameObject[12];
        private Material[] markerMaterial = new Material[12];
        private MeshRenderer[] meshRenderer = new MeshRenderer[12];
        private RenderTexture navballRenTex;
        private Camera navballCamera;
        private Material imageMaterial;
        private Material navballMaterial;
        private MeshRenderer rentexRenderer;
        private Renderer navballRenderer;
        private MASFlightComputer.Variable range1, range2;
        private readonly bool rangeMode;
        private bool currentState;
        private MASFlightComputer comp;
        private readonly float navballExtents;
        private readonly float iconDepth;
        private readonly float iconAlphaScalar;
        private static readonly int navballLayer = 29;
        private static int colorIdx = Shader.PropertyToID("_Color");
        private readonly Color[] activeMarkerColor = new Color[12];

        enum MarkerId
        {
            Prograde,
            Retrograde,
            RadialOut,
            RadialIn,
            NormalPlus,
            NormalMinus,
            ManeuverPlus,
            ManeuverMinus,
            TargetPlus,
            TargetMinus,
            DockingAlignment,
            Waypoint
        }

        internal MASPageNavBall(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth):base(config, prop, comp)
        {
            this.comp = comp;

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
            try
            {
                Vector3 extents = navballModel.GetComponent<MeshFilter>().mesh.bounds.extents;
                navballExtents = Mathf.Max(extents.x, extents.y) * 1.01f;
            }
            catch
            {
                navballExtents = 1.0f;
            }
            iconDepth = 1.4f - navballExtents - 0.01f;
            iconAlphaScalar = 0.6f / navballExtents;

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
            if (!config.TryGetValue("opacity", ref opacity))
            {
                opacity = 1.0f;
            }
            else
            {
                opacity = Mathf.Clamp01(opacity);
            }

            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            string range = string.Empty;
            if (config.TryGetValue("range", ref range))
            {
                string[] ranges = Utility.SplitVariableList(range);
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in NAVBALL " + name);
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
            Shader displayShader = MASLoader.shaders["MOARdV/Monitor"];
            navballRenTex = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            navballRenTex.Create();
            navballRenTex.DiscardContents();

            // Set up our display surface.
            imageObject = new GameObject();
            imageObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
            imageObject.layer = pageRoot.gameObject.layer;
            imageObject.transform.parent = pageRoot;
            imageObject.transform.position = pageRoot.position;
            imageObject.transform.Translate(monitor.screenSize.x * -0.5f + position.x, monitor.screenSize.y * 0.5f - position.y, depth);
            // add renderer stuff
            MeshFilter meshFilter = imageObject.AddComponent<MeshFilter>();
            rentexRenderer = imageObject.AddComponent<MeshRenderer>();
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
                {
                    new Vector3(-size.x, size.y, 0.0f),
                    new Vector3(size.x, size.y, 0.0f),
                    new Vector3(-size.x, -size.y, 0.0f),
                    new Vector3(size.x, -size.y, 0.0f),
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
            imageMaterial = new Material(displayShader);
            imageMaterial.mainTexture = navballRenTex;
            rentexRenderer.material = imageMaterial;

            //cameraObject
            cameraObject = new GameObject();
            cameraObject.name = pageRoot.gameObject.name + "-MASPageNavBallCamera-" + name + "-" + depth.ToString();
            cameraObject.layer = pageRoot.gameObject.layer;
            cameraObject.transform.parent = pageRoot;
            cameraObject.transform.position = pageRoot.position;
            navballCamera = cameraObject.AddComponent<Camera>();
            navballCamera.enabled = true;
            navballCamera.orthographic = true;
            navballCamera.aspect = 1.0f;
            navballCamera.eventMask = 0;
            navballCamera.farClipPlane = 13.0f;
            navballCamera.orthographicSize = navballExtents;
            navballCamera.cullingMask = 1 << navballLayer;
            // TODO: Different shader... clearing to a=0 hides the navball
            navballCamera.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);
            //navballCamera.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            navballCamera.clearFlags = CameraClearFlags.SolidColor;
            navballCamera.transparencySortMode = TransparencySortMode.Orthographic;
            navballCamera.targetTexture = navballRenTex;
            Camera.onPreCull += CameraPrerender;
            Camera.onPostRender += CameraPostrender;

            navballModel.layer = navballLayer;
            navballModel.transform.parent = cameraObject.transform;
            // TODO: this isn't working when the camera is shifted.  Camera needs
            // to be on a separate GO than the display.
            navballModel.transform.Translate(new Vector3(0.0f, 0.0f, 2.4f));
            navballRenderer = null;
            navballModel.GetComponentCached<Renderer>(ref navballRenderer);
            navballMaterial = new Material(displayShader);
            navballMaterial.shader = displayShader;
            navballMaterial.mainTexture = navballTexture;
            navballMaterial.SetFloat("_Opacity", opacity);
            navballRenderer.material = navballMaterial;
            navballRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            navballRenderer.enabled = false;
            navballModel.SetActive(true);
            navballCamera.transform.LookAt(navballModel.transform, Vector3.up);

            float iconScale = 1.0f;
            if (!config.TryGetValue("iconScale", ref iconScale))
            {
                iconScale = 1.0f;
            }
            InitMarkers(cameraObject.transform, iconScale);
            RenderPage(false);

            // Following icons are not currently supported:
            markers[7].SetActive(false); // Maneuver minus (why did I want this?)
            markers[10].SetActive(false); // Docking port alignment
            markers[11].SetActive(false); // Waypoint

            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the mesh if we're in variable mode
                imageObject.SetActive(false);
                cameraObject.SetActive(false);
                variableRegistrar.RegisterNumericVariable(variableName, VariableCallback);
            }
            else
            {
                imageObject.SetActive(true);
                cameraObject.SetActive(true);
            }
        }

        /// <summary>
        /// Convert the Z-value of a direction vector into an alpha value for fading icons
        /// </summary>
        /// <param name="zValue"></param>
        /// <returns></returns>
        private float GetIconAlpha(float zValue)
        {
            // Current iconAlphaScalar = 0.6f / navballExtents
            return Mathf.Clamp01(zValue * iconAlphaScalar + 0.4f);
        }

        /// <summary>
        /// Update a direction marker and its antipode in one action.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="scaledDirection"></param>
        private void UpdateVectorPair(int index, Vector3 scaledDirection)
        {
            meshRenderer[index].enabled = true;
            markers[index].transform.localPosition = new Vector3(scaledDirection.x, scaledDirection.y, iconDepth);
            activeMarkerColor[index].a = GetIconAlpha(scaledDirection.z);
            markerMaterial[index].SetColor(colorIdx, activeMarkerColor[index]);

            meshRenderer[index + 1].enabled = true;
            markers[index + 1].transform.localPosition = new Vector3(-scaledDirection.x, -scaledDirection.y, iconDepth);
            activeMarkerColor[index + 1].a = GetIconAlpha(-scaledDirection.z);
            markerMaterial[index + 1].SetColor(colorIdx, activeMarkerColor[index + 1]);
        }

        /// <summary>
        /// Update a single direction marker
        /// </summary>
        /// <param name="index"></param>
        /// <param name="scaledDirection"></param>
        private void UpdateSingleVector(int index, Vector3 scaledDirection)
        {
            meshRenderer[index].enabled = true;
            markers[index].transform.localPosition = new Vector3(scaledDirection.x, scaledDirection.y, iconDepth);
            activeMarkerColor[index].a = GetIconAlpha(scaledDirection.z);
            markerMaterial[index].SetColor(colorIdx, activeMarkerColor[index]);
        }

        /// <summary>
        /// Callback called before culling - we use this to update marker positions
        /// and determine which markers (and the navball) should be rendered.
        /// </summary>
        /// <param name="whichCamera"></param>
        private void CameraPrerender(Camera whichCamera)
        {
            if (whichCamera == navballCamera)
            {
                navballRenderer.enabled = true;
                if (!navballRenTex.IsCreated())
                {
                    navballRenTex.Create();
                    navballCamera.targetTexture = navballRenTex;
                    imageMaterial.mainTexture = navballRenTex;
                }

                // Apply navball gimbal
                navballModel.transform.rotation = comp.vc.navBallRelativeGimbal;

                Quaternion attitudeGimbal = comp.vc.navBallAttitudeGimbal;
                var speedMode = FlightGlobals.speedDisplayMode;
                if (speedMode == FlightGlobals.SpeedDisplayModes.Orbit)
                {
                    UpdateVectorPair(0, (attitudeGimbal * comp.vc.prograde) * navballExtents);

                    markers[2].SetActive(true);

                    markers[3].SetActive(true);
                    UpdateVectorPair(2, (attitudeGimbal * comp.vc.radialOut) * navballExtents);

                    markers[4].SetActive(true);
                    markers[5].SetActive(true);
                    UpdateVectorPair(4, (attitudeGimbal * comp.vc.normal) * navballExtents);
                }
                else if (speedMode == FlightGlobals.SpeedDisplayModes.Surface)
                {
                    UpdateVectorPair(0, (attitudeGimbal * comp.vessel.srf_velocity.normalized) * navballExtents);

                    for (int i = 2; i < 6; ++i)
                    {
                        markers[i].SetActive(false);
                    }
                }
                else
                {
                    UpdateVectorPair(0, (attitudeGimbal * FlightGlobals.ship_tgtVelocity.normalized) * navballExtents);

                    for (int i = 2; i < 6; ++i)
                    {
                        markers[i].SetActive(false);
                    }
                }

                // Maneuver +/-
                if (comp.vc.maneuverNodeValid)
                {
                    markers[6].SetActive(true);
                    //markers[7].SetActive(false);
                    UpdateSingleVector(6, (attitudeGimbal * comp.vc.maneuverNodeVector.normalized) * navballExtents);
                }
                else
                {
                    markers[6].SetActive(false);
                    //markers[7].SetActive(false);
                }

                // Target +/-
                if (comp.vc.targetValid)
                {
                    markers[8].SetActive(true);
                    markers[9].SetActive(true);
                    UpdateVectorPair(8, (attitudeGimbal * comp.vc.targetDirection) * navballExtents);

                    // Docking Port
                    //if (comp.vc.targetType == MASVesselComputer.TargetType.DockingPort)
                    //{
                    //    // TODO:
                    //    markers[10].SetActive(false);
                    //}
                    //else
                    //{
                    //    markers[10].SetActive(false);
                    //}
                }
                else
                {
                    markers[8].SetActive(false);
                    markers[9].SetActive(false);
                    //markers[10].SetActive(false);
                }

                // Waypoint
                // TODO: This requires some additional effort, since the waypoint icon may change
                //markers[11].SetActive(false);

            }
        }

        /// <summary>
        /// Switch off our renderers once the navball camera is done.
        /// </summary>
        /// <param name="whichCamera"></param>
        private void CameraPostrender(Camera whichCamera)
        {
            if (whichCamera == navballCamera)
            {
                navballRenderer.enabled = false;
                for (int i = 0; i < 12; ++i)
                {
                    meshRenderer[i].enabled = false;
                }
            }
        }


        /// <summary>
        /// UV offsets within the squad maneuver icon texture.
        /// </summary>
        private static readonly Vector2[] markerUV =
        {
            new Vector2(0.0f / 3.0f, 2.0f / 3.0f), // Prograde
            new Vector2(1.0f / 3.0f, 2.0f / 3.0f), // Retrograde
            new Vector2(1.0f / 3.0f, 1.0f / 3.0f), // RadialOut
            new Vector2(0.0f / 3.0f, 1.0f / 3.0f), // RadialIn
            new Vector2(0.0f / 3.0f, 0.0f / 3.0f), // NormalPlus
            new Vector2(1.0f / 3.0f, 0.0f / 3.0f), // NormalMinus
            new Vector2(2.0f / 3.0f, 0.0f / 3.0f), // ManeuverPlus
            new Vector2(1.0f / 3.0f, 2.0f / 3.0f), // ManeuverMinus
            new Vector2(2.0f / 3.0f, 2.0f / 3.0f), // TargetPlus
            new Vector2(2.0f / 3.0f, 1.0f / 3.0f), // TargetMinus
            new Vector2(0.0f / 3.0f, 2.0f / 3.0f), // DockingAlignment
            new Vector2(0.0f / 3.0f, 2.0f / 3.0f), // Waypoint
        };

        /// <summary>
        /// Default colors for markers.  May be overridden with the config file.
        /// </summary>
        private static readonly Color32[] markerColor =
        {
            new Color32(255, 203, 0, 255), // Prograde
            new Color32(255, 203, 0, 255), // Retrograde
            new Color32(0, 155, 255, 255), // RadialOut
            new Color32(0, 155, 255, 255), // RadialIn
            new Color32(156, 0, 206, 255), // NormalPlus
            new Color32(156, 0, 206, 255), // NormalMinus
            new Color32(0, 102, 249, 255), // ManeuverPlus
            new Color32(0, 102, 249, 255), // ManeuverMinus
            new Color32(255, 0, 255, 255), // TargetPlus
            new Color32(255, 0, 255, 255), // TargetMinus
            XKCDColors.Red, // DockingAlignment
            XKCDColors.Red, // Waypoint
        };

        /// <summary>
        /// Method to automate all the work needed to create a new planar game object
        /// </summary>
        /// <param name="rootTransform"></param>
        /// <param name="displayShader"></param>
        /// <param name="maneuverTexture"></param>
        /// <param name="markerIdx"></param>
        /// <returns></returns>
        private GameObject MakeMarker(Transform rootTransform, Shader displayShader, Texture maneuverTexture, int markerIdx, float iconScale)
        {
            GameObject newMarker = new GameObject();
            newMarker.layer = navballLayer;
            newMarker.transform.parent = rootTransform;
            newMarker.transform.localPosition = new Vector3(0.0f, 0.0f, iconDepth);

            Material markerMaterial = new Material(displayShader);
            markerMaterial.mainTexture = maneuverTexture;
            markerMaterial.SetColor(colorIdx, markerColor[markerIdx]);

            MeshFilter meshFilter = newMarker.AddComponent<MeshFilter>();
            meshRenderer[markerIdx] = newMarker.AddComponent<MeshRenderer>();
            meshRenderer[markerIdx].material = markerMaterial;
            this.markerMaterial[markerIdx] = markerMaterial;

            Vector2 uv0 = markerUV[markerIdx];
            Vector2 uv1 = uv0 + new Vector2(1.0f / 3.0f, 1.0f / 3.0f);
            // Half-extents based on centered position
            // relative to the navball
            float markerExtents = navballExtents * 0.18f * iconScale;
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
                {
                    new Vector3(-markerExtents, markerExtents, 0.0f),
                    new Vector3(markerExtents, markerExtents, 0.0f),
                    new Vector3(-markerExtents, -markerExtents, 0.0f),
                    new Vector3(markerExtents, -markerExtents, 0.0f),
                };
            mesh.colors32 = new Color32[]
                {
                    XKCDColors.White,
                    XKCDColors.White,
                    XKCDColors.White,
                    XKCDColors.White,
                };
            mesh.uv = new[]
                {
                    new Vector2(uv0.x, uv1.y),
                    uv1,
                    uv0,
                    new Vector2(uv1.x, uv0.y),
                };
            mesh.triangles = new[] 
                {
                    0, 3, 2,
                    0, 1, 3
                };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            meshFilter.mesh = mesh;

            newMarker.SetActive(true);

            return newMarker;
        }

        /// <summary>
        /// Iterate over all of the markers and initiate them (basic position, color, etc).
        /// </summary>
        /// <param name="rootTransform"></param>
        private void InitMarkers(Transform rootTransform, float iconScale)
        {
            Shader displayShader = MASLoader.shaders["MOARdV/TextMonitor"];
            Texture2D maneuverTexture = GameDatabase.Instance.GetTexture("Squad/Props/IVANavBall/ManeuverNode_vectors", false);

            int markerCount = markers.Length;
            for (int markerId = 0; markerId < markerCount; ++markerId)
            {
                markers[markerId] = MakeMarker(rootTransform, displayShader, maneuverTexture, markerId, iconScale);
                markers[markerId].transform.localPosition = new Vector3(0.0f, 0.0f, iconDepth);
                activeMarkerColor[markerId] = markerColor[markerId];
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
                cameraObject.SetActive(currentState);
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
            rentexRenderer.enabled = enable;
        }

        /// <summary>
        /// Called with `true` when the page is active on the monitor, called with
        /// `false` when the page is no longer active.
        /// </summary>
        /// <param name="enable">true when the page is actively displayed, false when the page
        /// is no longer displayed.</param>
        public override void SetPageActive(bool enable)
        {
            navballCamera.enabled = enable;
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            Camera.onPreCull -= CameraPrerender;
            Camera.onPostRender -= CameraPostrender;
            UnityEngine.GameObject.Destroy(imageObject);
            imageObject = null;
            UnityEngine.Object.Destroy(imageMaterial);
            imageMaterial = null;
            UnityEngine.GameObject.Destroy(navballModel);
            navballModel = null;
            UnityEngine.Object.Destroy(navballMaterial);
            navballMaterial = null;
            UnityEngine.GameObject.Destroy(cameraObject);
            cameraObject = null;
            variableRegistrar.ReleaseResources();
            this.comp = null;
        }
    }
}
