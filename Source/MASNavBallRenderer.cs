/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2019 MOARdV
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
    /// The MASNavBallRenderer is a class that renders the navball(s) used in
    /// MFDs.  Since there are multiple NAVBALL instances in a given MFD config
    /// and multiple MFDs per IVA, having every single NAVBALL instance render
    /// the exact same thing into separate render textures is a particularly
    /// wasteful use of resources.
    /// 
    /// Instead, this class manages all of the unique instances required to meet
    /// configuration specifications:
    /// * modelName
    /// * textureName
    /// * opacity setting
    /// * icon scaling
    /// 
    /// All NAVBALL instances using the same settings will share a single render
    /// texture managed by a MASNavBallRenderer.NavBallInstance.
    /// 
    /// The MASNavBallRenderer is invoked automatically on the Vessel as a
    /// Monobehaviour, which allows us to catch the OnDestroy method to iterate
    /// over all of the navball instances to release resources.
    /// 
    /// @todo: Add a method to NavBallInstance that can switch the camera on or
    /// off by tracking the number of subscribers.  No sense rendering if no one
    /// is looking.
    /// </summary>
    internal class MASNavBallRenderer : MonoBehaviour
    {
        private Dictionary<string, NavBallInstance> navballInstance = new Dictionary<string, NavBallInstance>();

        /// <summary>
        /// Fetch the NavBallInstance on the vessel that meets the criteria.
        /// </summary>
        /// <param name="comp">The active MASFlightComputer.</param>
        /// <param name="name">Name of the NAVBALL node, for error reporting.</param>
        /// <param name="modelName">Name of the model to use for rendering the NavBall.</param>
        /// <param name="textureName">Name of the texture to apply to the NavBall.</param>
        /// <param name="opacity">Opacity of the NavBall.</param>
        /// <param name="iconScale">Relative scaling of the icons on the NavBall.</param>
        /// <returns>The instance to use.</returns>
        static internal NavBallInstance GetInstance(MASFlightComputer comp, string name, string modelName, string textureName, float opacity, float iconScale)
        {
            MASNavBallRenderer nbr = comp.vessel.gameObject.AddOrGetComponent<MASNavBallRenderer>();

            return nbr.Get(comp, name, modelName, textureName, opacity, iconScale);
        }

        internal NavBallInstance Get(MASFlightComputer comp, string name, string model, string texture, float opacity, float iconScale)
        {
            StringBuilder sb = StringBuilderCache.Acquire();
            sb.Append(model).Append(':').Append(texture).Append(':').Append(opacity.ToString("R")).Append(':').Append(iconScale.ToString("R"));
            string id = sb.ToStringAndRelease();
            Utility.LogMessage(this, "Searching cache for {0}", id);

            NavBallInstance nbi;
            if (!navballInstance.TryGetValue(id, out nbi))
            {
                Utility.LogMessage(this, "Creating NavBallInstance \"{0}\"", id);
                nbi = new NavBallInstance(comp, name, model, texture, opacity, iconScale);
                navballInstance.Add(id, nbi);
            }

            return nbi;
        }

        public void OnDestroy()
        {
            //Utility.LogMessage(this, "OnDestroy() - {0} nbis", navballInstance.Count);
            foreach (NavBallInstance nbi in navballInstance.Values)
            {
                nbi.OnDestroy();
            }
        }

        internal class NavBallInstance
        {
            /// <summary>
            /// Fetch the texture that displays the rendered NavBall.
            /// </summary>
            internal Texture mainTexture { get { return navballRenTex; } }

            internal NavBallInstance(MASFlightComputer comp, string name, string modelName, string textureName, float opacity, float iconScale)
            {
                this.comp = comp;
                //Utility.LogMessage(this, "--- creating {0}:{1}:{2:R}:{3:R}", modelName, textureName, opacity, iconScale);

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

                Texture2D navballTexture = GameDatabase.Instance.GetTexture(textureName, false);
                if (navballTexture == null)
                {
                    throw new ArgumentException("Unable to find 'texture' " + textureName + " for NAVBALL " + name);
                }

                // Set up our navball renderer
                Shader displayShader = MASLoader.shaders["MOARdV/Monitor"];
                navballRenTex = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
                navballRenTex.Create();
                navballRenTex.DiscardContents();

                Transform parent = comp.vessel.gameObject.transform;
                cameraObject = new GameObject();
                cameraObject.name = parent.gameObject.name + "-MASPageNavBallCamera-" + name;
                cameraObject.layer = navballLayer;// MASMonitor.drawingLayer;
                cameraObject.transform.parent = parent;
                cameraObject.transform.position = parent.position;
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

                InitMarkers(cameraObject.transform, iconScale);

                // Following icons are not currently supported:
                markers[7].SetActive(false); // Maneuver minus (why did I want this?)
                markers[10].SetActive(false); // Docking port alignment
                markers[11].SetActive(false); // Waypoint
            }

            internal void OnDestroy()
            {
                //Utility.LogMessage(this, "--- OnDestroy");
                Camera.onPreCull -= CameraPrerender;
                Camera.onPostRender -= CameraPostrender;

                UnityEngine.GameObject.Destroy(navballModel);
                navballModel = null;
                UnityEngine.Object.Destroy(navballMaterial);
                navballMaterial = null;
                UnityEngine.GameObject.Destroy(cameraObject);
                cameraObject = null;

                // All of the other objects?
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
                    //Utility.LogMessage(this, "CameraPrerender");
                    navballRenderer.enabled = true;
                    if (!navballRenTex.IsCreated())
                    {
                        navballRenTex.Create();
                        navballCamera.targetTexture = navballRenTex;
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
                    //Utility.LogMessage(this, "CameraPostrender");
                    navballRenderer.enabled = false;
                    for (int i = 0; i < 12; ++i)
                    {
                        meshRenderer[i].enabled = false;
                    }
                }
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

            private GameObject cameraObject;
            private GameObject navballModel;
            private GameObject[] markers = new GameObject[12];
            private Material[] markerMaterial = new Material[12];
            private MeshRenderer[] meshRenderer = new MeshRenderer[12];
            private RenderTexture navballRenTex;
            private Camera navballCamera;
            private Material navballMaterial;
            private Renderer navballRenderer;

            private MASFlightComputer comp;
            private readonly float navballExtents;
            private readonly float iconDepth;
            private readonly float iconAlphaScalar;
            private static readonly int navballLayer = 28;
            private static int colorIdx = Shader.PropertyToID("_Color");
            private readonly Color[] activeMarkerColor = new Color[12];
        }
    }
}
