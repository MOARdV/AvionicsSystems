/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2017-2019 MOARdV
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
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// The MASCameraMode represents the modes (resolution, post-processing shader) applied to
    /// a given camera.
    /// </summary>
    internal class MASCameraMode
    {
        public readonly string name;

        /// <summary>
        /// Post-processing shader to apply.
        /// </summary>
        public Material postProcShader;

        /// <summary>
        /// The resolution of the camera mode in pixels.  Cameras render a square
        /// image.  Valid values are 64 to 2048.  Values outside that range are
        /// clamped.  The value will be adjusted
        /// to a power-of-2 if needed.  Note that large values may cause
        /// problems with lower-end machines.  Defaults to 256.
        /// </summary>
        public readonly int cameraResolution;

        /// <summary>
        /// MAS variables that update shader properties.
        /// </summary>
        private string[] propertyValue = new string[0];

        /// <summary>
        /// Map property names to shader ID numbers.
        /// </summary>
        private int[] propertyId = new int[0];

        /// <summary>
        /// The registrar that manages subscribing / unsubscribing.
        /// </summary>
        private VariableRegistrar variableRegistrar = new VariableRegistrar(null, null);

        /// <summary>
        /// The flight computer we registered with.
        /// </summary>
        private MASFlightComputer comp;

        public MASCameraMode(ConfigNode node, string partName)
        {
            if (!node.TryGetValue("name", ref name))
            {
                //Utility.LogErrorMessage(this, "No 'name' defined for MASCamera MODE in {0}", partName);
                name = "(anonymous)";
            }

            string shader = string.Empty;
            if (!node.TryGetValue("shader", ref shader))
            {
                // If I simply blit the output of the camera, I have black sections in the image.  I suspect
                // they're regions where alpha = 0.  So, if the prop config doesn't select a shader, I use a
                // simple pass-through shader that drives alpha to 1.
                shader = "MOARdV/PassThrough";
            }
            else
            {
                string concatProperties = string.Empty;
                if (node.TryGetValue("properties", ref concatProperties))
                {
                    string[] propertiesList = concatProperties.Split(';');
                    int listLength = propertiesList.Length;
                    if (listLength > 0)
                    {
                        propertyId = new int[listLength];
                        propertyValue = new string[listLength];

                        for (int i = 0; i < listLength; ++i)
                        {
                            string[] pair = propertiesList[i].Split(':');
                            if (pair.Length != 2)
                            {
                                throw new ArgumentOutOfRangeException("Incorrect number of parameters for property: requires 2, found " + pair.Length + " in property " + propertiesList[i] + " for camera MODE " + name);
                            }
                            propertyId[i] = Shader.PropertyToID(pair[0].Trim());
                            propertyValue[i] = pair[1].Trim();
                        }
                    }
                }

            }

            if (!MASLoader.shaders.ContainsKey(shader))
            {
                Utility.LogError(this, "Invalid shader \"{0}\" in MASCamera MODE {1} in {2}.", shader, name, partName);
                throw new ArgumentException("MASCameraNode: Invalid post-processing shader name.");
            }

            postProcShader = new Material(MASLoader.shaders[shader]);

            string textureName = string.Empty;
            if (node.TryGetValue("texture", ref textureName))
            {
                Texture auxTexture = GameDatabase.Instance.GetTexture(textureName, false);
                if (auxTexture == null)
                {
                    throw new ArgumentException("Unable to find 'texture' " + textureName + " for CAMERA " + name);
                }
                postProcShader.SetTexture("_AuxTex", auxTexture);
            }

            if (!node.TryGetValue("cameraResolution", ref cameraResolution))
            {
                cameraResolution = 256;
            }
            cameraResolution >>= MASConfig.CameraTextureScale;

            Utility.LastPowerOf2(ref cameraResolution, 64, 2048);
        }

        /// <summary>
        /// Unsubscribe from the property callbacks for this shader mode.
        /// </summary>
        public void UnregisterShaderProperties()
        {
            variableRegistrar.ReleaseResources();
        }

        /// <summary>
        /// Callback used to tell the mode to refresh its shader properties.
        /// </summary>
        /// <param name="comp"></param>
        public void UpdateShaderProperties(MASFlightComputer comp)
        {
            if (this.comp == comp)
            {
                return;
            }

            UnregisterShaderProperties();

            this.comp = comp;
            variableRegistrar = new VariableRegistrar(comp, null);

            for (int i = 0; i < propertyValue.Length; ++i)
            {
                int id = propertyId[i];
                variableRegistrar.RegisterVariableChangeCallback(propertyValue[i], (double newValue) => postProcShader.SetFloat(id, (float)newValue));
            }
        }
    }

    /// <summary>
    /// The MASCamera represents a physical camera model, and it encapsulates the data specific
    /// to that model (field of view, pan/tilt movement limits, post-processing shaders that
    /// represent physical camera behavior).
    /// </summary>
    class MASCamera : PartModule
    {
        /// <summary>
        /// Defines the minimum and maximum field of view of the camera lens
        /// as measured across the vertical (Y) axis, in degrees.  Automatically
        /// clamps values between 1 and 179.
        /// </summary>
        [KSPField]
        public Vector2 fovRange = new Vector2(50.0f, 50.0f);

        /// <summary>
        /// Used internally to allow desired FoV to persist.  Should only
        /// be changed programmatically through AddFoV() and SetFoV() to
        /// manage the FoV limits.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float goalFov = 50.0f;

        /// <summary>
        /// Current FoV.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentFov = 50.0f;

        /// <summary>
        /// Max FoV rate of change in degrees/sec.  0 indicates instant.
        /// </summary>
        [KSPField]
        public float fovRate = 0.0f;

        /// <summary>
        /// Defines the minimum and maximum pan angle (left-right camera rotation)
        /// of the camera lens in degrees.  Automatically clamps between
        /// -180 and +180.  Negative values indicate to the left of the center
        /// position. Positive values indicate to the right.
        /// </summary>
        [KSPField]
        public Vector2 panRange = new Vector2(0.0f, 0.0f);

        /// <summary>
        /// Used internally to allow desired pan angle to persist.  Should only
        /// be changed programmatically through AddPan() and SetPan() to
        /// manage the pan limits.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float goalPan = 0.0f;

        /// <summary>
        /// Current pan position.  May differ from goalPan if panRate != 0
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentPan = 0.0f;

        /// <summary>
        /// Maximum rate (degrees/sec) for pan.  0 indicates instant.
        /// </summary>
        [KSPField]
        public float panRate = 0.0f;

        /// <summary>
        /// Defines the minimum and maximum tilt angle (up-down camera rotation)
        /// of the camera lens in degrees.  Automatically clamps between
        /// -180 and +180.  Negative values indicate (direction), positive
        /// values indicate (other direction).
        /// </summary>
        [KSPField]
        public Vector2 tiltRange = new Vector2(0.0f, 0.0f);

        /// <summary>
        /// Used internally to allow desired tilt angle to persist.  Should only
        /// be changed programmatically through AddTilt() and SetTilt() to
        /// manage the tilt limits.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float goalTilt = 0.0f;

        /// <summary>
        /// Current tilt position.  May differ from goalTilt if tiltRate != 0
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentTilt = 0.0f;

        /// <summary>
        /// Maximum rate (degrees/sec) for tilt.  0 indicates instant.
        /// </summary>
        [KSPField]
        public float tiltRate = 0.0f;

        /// <summary>
        /// Name of the transform that the camera lens is attached to.
        /// </summary>
        [KSPField]
        public string cameraTransformName = string.Empty;
        private Transform cameraTransform = null;
        private Quaternion cameraRotation = Quaternion.identity;

        /// <summary>
        /// Offset of the camera lens from its transform's position.
        /// </summary>
        [KSPField]
        public Vector3 translation = Vector3.zero;

        /// <summary>
        /// Euler Rotation of the camera lens from the transform's facing.
        /// </summary>
        [KSPField]
        public Vector3 rotation = Vector3.zero;

        /// <summary>
        /// Allows a camera to refresh less often than every Update().  Values
        /// larger than 1 indicate an update of every Nth Update() (for instance,
        /// 2 means "Render every other frame").
        /// </summary>
        [KSPField]
        public int refreshRate = 1;
        private int frameCount = 0;

        /// <summary>
        /// A unique name for the camera.  Note that cameras missing a name can not
        /// be selected in-flight, and if several cameras have the same name,
        /// only one of them will be selectable.
        /// </summary>
        [KSPField(isPersistant = true)]
        public string cameraName = string.Empty;
        private string newCameraName = string.Empty;

        [UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889")]
        [KSPField(guiActiveEditor = true, guiName = "#MAS_Camera_FoV_Marker_Label")]
        public bool showFov = false;
        const float rayLength = 10.0f;

        /// <summary>
        /// Used internally to keep track of which camera is active.
        /// </summary>
        [KSPField(isPersistant = true)]
        public int activeMode = 0;
        private MASCameraMode[] mode = new MASCameraMode[0];

        internal bool isDockingPortCamera = false;

        private static readonly string[] knownCameraNames = 
        {
            "GalaxyCamera",
            "Camera ScaledSpace",
            "Camera 01",
            "Camera 00",
            "FXCamera"
        };
        private Camera scaledSpace;
        private readonly Camera[] cameras = { null, null, null, null, null };
        private readonly GameObject[] cameraBody = { null, null, null, null, null };
        internal RenderTexture cameraRentex;
        internal event Action<RenderTexture, Material> renderCallback;
        private bool cameraLive;

        /*
         * Camera notes:
         * 
         * Culling masks:
         * GalaxyCamera = 4'0000 -> SkySphere
         * Camera ScaledSpace = 600 -> TransparentFX | Ignore Raycast
         * Camera 01 = 8A'8013 -> Default | TransparentFX | Water | Local Scenery | Editor_UI | Disconnected Parts | ScaledSpaceSun
         * Camera 02 = 8A'8013 -> Default | TransparentFX | Water | Local Scenery | Editor_UI | Disconnected Parts | ScaledSpaceSun
         * FXCamera = 2'0001 -> Default | Editor_UI
         */

        private MASDeployableCamera deploymentController;

        /// <summary>
        /// The camera mount we're attached to.
        /// </summary>
        private MASCameraMount mount;

        /// <summary>
        /// Is this object ready to use?
        /// </summary>
        /// <returns>true if a this object will function, false otherwise</returns>
        public bool IsValid()
        {
            return cameraTransform != null;
        }

        #region Setup - Teardown
        /// <summary>
        /// Configure everything.
        /// </summary>
        public void Start()
        {
            if (!(HighLogic.LoadedScene == GameScenes.EDITOR || HighLogic.LoadedScene == GameScenes.FLIGHT))
            {
                return;
            }

            if (knownCameraNames.Length != cameras.Length)
            {
                throw new NotImplementedException("MASCamera: Camera Names array has a different size than cameras array!");
            }
            if (cameraBody.Length != cameras.Length)
            {
                throw new NotImplementedException("MASCamera: Camera bodies array has a different size than cameras array!");
            }

            if (!string.IsNullOrEmpty(cameraTransformName))
            {
                Transform cameraParentTransform = part.FindModelTransform(cameraTransformName);
                cameraTransform = new GameObject().transform;
                cameraTransform.gameObject.name = "MASCamera-" + cameraParentTransform.gameObject.name;
                cameraTransform.parent = cameraParentTransform;
                cameraTransform.position = cameraParentTransform.position;
                cameraTransform.rotation = cameraParentTransform.rotation;

                // TODO: Still sort this out - ordering
                if (translation != Vector3.zero)
                {
                    cameraTransform.Translate(translation);
                }
                if (rotation != Vector3.zero)
                {
                    cameraTransform.Rotate(rotation);
                }
                //if (translation != Vector3.zero)
                //{
                //    cameraTransform.Translate(translation);
                //}
            }

            if (cameraTransform != null)
            {
                // Make everything in-order, and clamp the current values to a legal range.
                if (fovRange.y < fovRange.x)
                {
                    fovRange = new Vector2(fovRange.y, fovRange.x);
                }
                fovRange.x = Mathf.Clamp(fovRange.x, 1.0f, 179.0f);
                fovRange.y = Mathf.Clamp(fovRange.y, 1.0f, 179.0f);
                currentFov = Mathf.Clamp(currentFov, fovRange.x, fovRange.y);
                goalFov = Mathf.Clamp(goalFov, fovRange.x, fovRange.y);
                fovRate = Mathf.Abs(fovRate);

                if (panRange.y < panRange.x)
                {
                    panRange = new Vector2(panRange.y, panRange.x);
                }
                panRange.x = Mathf.Clamp(panRange.x, -180.0f, 180.0f);
                panRange.y = Mathf.Clamp(panRange.y, -180.0f, 180.0f);
                currentPan = Mathf.Clamp(currentPan, panRange.x, panRange.y);
                goalPan = Mathf.Clamp(goalPan, panRange.x, panRange.y);
                panRate = Mathf.Abs(panRate);
                if (panRange.y < 0.0f)
                {
                    Utility.LogWarning(this, "Camera {0} has a maximum panRange less than zero, so it is unable to point directly along the transform's axis.", cameraTransformName);
                }
                if (panRange.x > 0.0f)
                {
                    Utility.LogWarning(this, "Camera {0} has a minimum panRange greater than zero, so it is unable to point directly along the transform's axis.", cameraTransformName);
                }

                if (tiltRange.y < tiltRange.x)
                {
                    tiltRange = new Vector2(tiltRange.y, tiltRange.x);
                }
                tiltRange.x = Mathf.Clamp(tiltRange.x, -180.0f, 180.0f);
                tiltRange.y = Mathf.Clamp(tiltRange.y, -180.0f, 180.0f);
                currentTilt = Mathf.Clamp(currentTilt, tiltRange.x, tiltRange.y);
                goalTilt = Mathf.Clamp(goalTilt, tiltRange.x, tiltRange.y);
                tiltRate = Mathf.Abs(tiltRate);
                if (tiltRange.y < 0.0f)
                {
                    Utility.LogWarning(this, "Camera {0} has a maximum tiltRange less than zero, so it is unable to point directly along the transform's axis.", cameraTransformName);
                }
                if (tiltRange.x > 0.0f)
                {
                    Utility.LogWarning(this, "Camera {0} has a minimum tiltRange greater than zero, so it is unable to point directly along the transform's axis.", cameraTransformName);
                }

                if (HighLogic.LoadedSceneIsEditor)
                {
                    CreateFovRenderer();
                }
                cameraRotation = cameraTransform.rotation;
            }
            else if (string.IsNullOrEmpty(cameraTransformName))
            {
                Utility.LogError(this, "No 'cameraTransformName' provided in part.");
                Utility.ComplainLoudly("Missing 'cameraTransformName' in MASCamera");
                throw new NotImplementedException("MASCamera: Missing 'cameraTransformName' in module config node.");
            }
            else
            {
                Utility.LogError(this, "Unable to find transform \"{0}\" in part", cameraTransformName);
                Utility.ComplainLoudly("Unable to find camera transform in MASCamera part");
            }

            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                // Is this camera a docking port camera?
                if (part.FindModuleImplementing<ModuleDockingNode>() != null)
                {
                    isDockingPortCamera = true;
                }
                else
                {
                    isDockingPortCamera = false;
                }

                if (panRange == Vector2.zero && tiltRange == Vector2.zero)
                {
                    mount = part.FindModuleImplementing<MASCameraMount>();
                }

                deploymentController = part.FindModuleImplementing<MASDeployableCamera>();

                CreateFlightCameras();

                Camera.onPreCull += CameraPreCull;
            }

            refreshRate = Math.Max(refreshRate, 1);
        }

        /// <summary>
        /// Helper function to locate flight cameras.
        /// </summary>
        /// <param name="cameraName">Name of the camera we're looking for.</param>
        /// <returns>The named camera, or null if it was not found.</returns>
        private static Camera GetCameraByName(string cameraName)
        {
            return Array.Find(Camera.allCameras, x => x.name == cameraName);
        }

        /// <summary>
        /// Update parameters affected by a mode change.
        /// </summary>
        private void ApplyMode()
        {
            if (cameraRentex == null)
            {
                return;
            }
            try
            {
                if (cameraRentex.width != mode[activeMode].cameraResolution)
                {
                    cameraRentex.Release();
                    cameraRentex = new RenderTexture(mode[activeMode].cameraResolution, mode[activeMode].cameraResolution, 24);

                    for (int i = 0; i < cameras.Length; ++i)
                    {
                        if (cameras[i] != null)
                        {
                            cameras[i].targetTexture = cameraRentex;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Utility.LogError(this, "ApplyMode() threw an exception in {0}:", vessel.vesselName);
                Utility.LogError(this, e.ToString());
            }
        }

        /// <summary>
        /// Create a clone of one of the KSP standard cameras.
        /// </summary>
        /// <param name="index">The index in the array where the new Camera will reside.</param>
        private void ConstructCamera(int index)
        {
            Camera sourceCamera = GetCameraByName(knownCameraNames[index]);
            if (sourceCamera != null)
            {
                cameraBody[index] = new GameObject();
                cameraBody[index].name = string.Format("MASCamera-{0}-{1}", index, cameraBody[index].GetInstanceID());
                cameras[index] = cameraBody[index].AddComponent<Camera>();

                cameras[index].CopyFrom(sourceCamera);
                cameras[index].aspect = 1.0f;

                // These get stomped on at render time:
                cameras[index].fieldOfView = currentFov;
                cameras[index].transform.rotation = Quaternion.identity;

                // Minor hack to bring the near clip plane for the "up close"
                // cameras drastically closer to where the cameras notionally
                // are.  Experimentally, these two cameras have N/F of 0.4 / 300.0,
                // or 750:1 Far/Near ratio.  Changing this to 8192:1 brings the
                // near plane to 37cm or so, which hopefully is close enough to
                // see nearby details without creating z-fighting artifacts.
                if (index == 3 || index == 4)
                {
                    cameras[index].nearClipPlane = cameras[index].farClipPlane / 8192.0f;
                }
                // The ScaledSpace camera needs to move around scaled space.  Until / unless
                // I figure out how to locate my position in SS, I'll just copy the existing
                // camera's position.
                if (index == 1)
                {
                    scaledSpace = sourceCamera;
                }
                cameras[index].enabled = false;
            }
        }

        /// <summary>
        /// Create the cameras used during flight.
        /// </summary>
        private void CreateFlightCameras()
        {
            List<MASCamera> cameraModules = part.FindModulesImplementing<MASCamera>();
            int index = cameraModules.IndexOf(this);

            ConfigNode partConfigNode = Utility.GetPartModuleConfigNode(part, "MASCamera", index);
            if (partConfigNode == null)
            {
                Utility.LogError(this, "Unable to load part config node for MASCamera {0}.", part.partName);
                throw new NotImplementedException("MASCamera: Unable to load part config node for MASCamera.");
            }

            ConfigNode[] modeNodes = partConfigNode.GetNodes("MODE");
            if (modeNodes == null || modeNodes.Length == 0)
            {
                modeNodes = new ConfigNode[1];
                modeNodes[0] = new ConfigNode();
                modeNodes[0].AddValue("name", "Default");
            }

            mode = new MASCameraMode[modeNodes.Length];
            for (int i = 0; i < modeNodes.Length; ++i)
            {
                mode[i] = new MASCameraMode(modeNodes[i], part.partName);
            }

            for (int i = 0; i < cameras.Length; ++i)
            {
                ConstructCamera(i);
            }

            activeMode = Mathf.Clamp(activeMode, 0, mode.Length - 1);
        }

        /// <summary>
        /// Set up the FoV renderer when a part is attached.
        /// </summary>
        private void AttachPart()
        {
            Vector3 origin = cameraTransform.TransformPoint(Vector3.zero);
            Vector3 direction = cameraTransform.forward;
            if (minFovRenderer != null)
            {
                minFovRenderer.SetPosition(0, origin);
                minFovRenderer.SetPosition(1, origin + direction * rayLength);
            }
            if (maxFovRenderer != null)
            {
                maxFovRenderer.SetPosition(0, origin);
                maxFovRenderer.SetPosition(1, origin + direction * rayLength);
            }
        }

        /// <summary>
        /// Callback when the part's detached - don't show FoV ray in the editor.
        /// </summary>
        private void DetachPart()
        {
            showFov = false;
        }

        /// <summary>
        /// Tear down and release resources.
        /// </summary>
        public void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                Camera.onPreCull -= CameraPreCull;
            }

            if (minFovRenderer != null)
            {
                Destroy(minFovRenderer);
                minFovRenderer = null;
                part.OnEditorAttach -= AttachPart;
                part.OnEditorDetach -= DetachPart;
                part.OnEditorDestroy -= DetachPart;
            }
            if (maxFovRenderer != null)
            {
                Destroy(maxFovRenderer);
                maxFovRenderer = null;
                Destroy(maxFovPosition);
                maxFovPosition = null;
            }

            for (int i = 0; i < mode.Length; ++i)
            {
                mode[i].UnregisterShaderProperties();
                Destroy(mode[i].postProcShader);
                mode[i].postProcShader = null;
            }

            if (nameMenu != null)
            {
                InputLockManager.RemoveControlLock("MASCamera-UI");
                nameMenu.Dismiss();
                nameMenu = null;
            }

            if (renderCallback != null)
            {
                renderCallback.Invoke(null, null);
            }

            if (cameraRentex != null)
            {
                cameraRentex.Release();
                cameraRentex = null;
            }
        }
        #endregion

        #region Flight
        /// <summary>
        /// Change the current field of view by `deltaFoV` degrees, remaining within
        /// camera FoV limits.
        /// </summary>
        /// <param name="deltaFoV">The amount to add or subtract to FoV in degrees.</param>
        /// <returns>The adjusted field of view.</returns>
        public float AddFoV(float deltaFoV)
        {
            goalFov = Mathf.Clamp(goalFov + deltaFoV, fovRange.x, fovRange.y);

            return goalFov;
        }

        /// <summary>
        /// Change the current field of view by `deltaFoV` degrees, remaining within
        /// camera FoV limits.
        /// </summary>
        /// <param name="deltaPan">The amount to add or subtract to FoV in degrees.</param>
        /// <returns>The adjusted pan.</returns>
        public float AddPan(float deltaPan)
        {
            if (mount)
            {
                return mount.AddPan(deltaPan);
            }
            else
            {
                goalPan = Mathf.Clamp(goalPan + deltaPan, panRange.x, panRange.y);

                return goalPan;
            }
        }

        /// <summary>
        /// Change the current field of view by `deltaFoV` degrees, remaining within
        /// camera FoV limits.
        /// </summary>
        /// <param name="deltaTilt">The amount to add or subtract to FoV in degrees.</param>
        /// <returns>The adjusted tilt.</returns>
        public float AddTilt(float deltaTilt)
        {
            if (mount)
            {
                return mount.AddTilt(deltaTilt);
            }
            else
            {
                goalTilt = Mathf.Clamp(goalTilt + deltaTilt, tiltRange.x, tiltRange.y);

                return goalTilt;
            }
        }

        public bool GetDeployable()
        {
            return (deploymentController != null) ? (deploymentController.deployState != ModuleDeployablePart.DeployState.BROKEN) : false;
        }

        public bool IsDamaged()
        {
            return (deploymentController != null) ? (deploymentController.deployState == ModuleDeployablePart.DeployState.BROKEN) : false;
        }

        public bool IsDeployed()
        {
            return (deploymentController != null) ? (deploymentController.deployState == ModuleDeployablePart.DeployState.EXTENDED) : true;
        }

        public int IsMoving()
        {
            if (deploymentController != null)
            {
                if (deploymentController.deployState == ModuleDeployablePart.DeployState.EXTENDING)
                {
                    return 1;
                }
                else if (deploymentController.deployState == ModuleDeployablePart.DeployState.RETRACTING)
                {
                    return -1;
                }
            }

            return 0;
        }

        public bool ToggleDeployment()
        {
            if (deploymentController != null)
            {
                if (deploymentController.deployState == ModuleDeployablePart.DeployState.EXTENDED)
                {
                    deploymentController.Retract();
                    return true;
                }
                else if (deploymentController.deployState == ModuleDeployablePart.DeployState.RETRACTED)
                {
                    deploymentController.Extend();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the index of the currently-active camera mode.
        /// </summary>
        /// <returns></returns>
        public int GetMode()
        {
            return activeMode;
        }

        /// <summary>
        /// Return the total count of the camera modes.
        /// </summary>
        /// <returns></returns>
        public int GetModeCount()
        {
            return mode.Length;
        }

        /// <summary>
        /// Return the name of the selected camera mode
        /// </summary>
        /// <returns></returns>
        public string GetModeName(int selectedMode)
        {
            if (selectedMode >= 0 || selectedMode < mode.Length)
            {
                return mode[selectedMode].name;
            }
            else
            {
                return string.Empty;
            }
        }

        public double GetPan()
        {
            if (mount)
            {
                return mount.currentPan;
            }
            else
            {
                return currentPan;
            }
        }

        /// <summary>
        /// Return the pan range for the camera, or the camera's mount, as applicable.
        /// </summary>
        /// <returns></returns>
        public Vector2 GetPanRange()
        {
            if (mount)
            {
                return mount.panRange;
            }
            else
            {
                return panRange;
            }
        }

        public double GetTilt()
        {
            if (mount)
            {
                return mount.currentTilt;
            }
            else
            {
                return currentTilt;
            }
        }

        /// <summary>
        /// Return the tilt range for the camera, or the camera's mount, as applicable.
        /// </summary>
        /// <returns></returns>
        public Vector2 GetTiltRange()
        {
            if (mount)
            {
                return mount.tiltRange;
            }
            else
            {
                return tiltRange;
            }
        }

        /// <summary>
        /// Select the camera mode.
        /// </summary>
        /// <param name="newMode"></param>
        /// <returns></returns>
        public int SetMode(int newMode)
        {
            if (newMode >= 0 && newMode < mode.Length && newMode != activeMode)
            {
                activeMode = newMode;
                ApplyMode();
            }

            return activeMode;
        }

        /// <summary>
        /// Set the current field of view, remaining within camera FoV limits.
        /// </summary>
        /// <param name="fieldOfView">The new FoV in degrees.</param>
        /// <returns>The adjusted field of view.</returns>
        public float SetFoV(float fieldOfView)
        {
            goalFov = Mathf.Clamp(fieldOfView, fovRange.x, fovRange.y);

            return goalFov;
        }

        /// <summary>
        /// Set the current pan location within the pan limits.
        /// </summary>
        /// <param name="pan"></param>
        /// <returns>The adjusted pan setting.</returns>
        public float SetPan(float pan)
        {
            if (mount)
            {
                return mount.SetPan(pan);
            }
            else
            {
                goalPan = Mathf.Clamp(pan, panRange.x, panRange.y);

                return goalPan;
            }
        }

        /// <summary>
        /// Set the current pan location within the pan limits.
        /// </summary>
        /// <param name="tilt"></param>
        /// <returns>The current tilt position</returns>
        public float SetTilt(float tilt)
        {
            if (mount)
            {
                return mount.SetTilt(tilt);
            }
            else
            {
                goalTilt = Mathf.Clamp(tilt, tiltRange.x, tiltRange.y);

                return goalTilt;
            }
        }

        /// <summary>
        /// Update FOV cones in the Editor, update pan/tilt in flight.
        /// </summary>
        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                Vector3 origin = cameraTransform.TransformPoint(Vector3.zero);
                Vector3 direction = cameraTransform.forward;

                // TODO: Renderers don't show up if the part is added.
                // only when a craft is loaded with camera attached.
                // RPM used callbacks for onattach / ondetach - maybe
                // they need to be used here.
                if (minFovRenderer != null)
                {
                    minFovRenderer.enabled = showFov;

                    minFovRenderer.SetPosition(0, origin);
                    minFovRenderer.SetPosition(1, origin + direction * rayLength);
                }
                if (maxFovRenderer != null)
                {
                    maxFovRenderer.enabled = showFov;

                    maxFovRenderer.SetPosition(0, origin);
                    maxFovRenderer.SetPosition(1, origin + direction * rayLength);
                }
            }
            if (showGui)
            {
                if (nameMenu == null)
                {
                    ShowNameMenu();
                }
            }
            else if (nameMenu != null)
            {
                InputLockManager.RemoveControlLock("MASCamera-UI");
                nameMenu.Dismiss();
                nameMenu = null;
            }

            if (HighLogic.LoadedSceneIsFlight)
            {
                if (goalFov != currentFov)
                {
                    if (fovRate > 0.0f)
                    {
                        float fovDelta = Mathf.Min(Mathf.Abs(goalFov - currentFov), panRate * TimeWarp.deltaTime);
                        if (goalFov > currentFov)
                        {
                            currentFov += fovDelta;
                        }
                        else
                        {
                            currentFov -= fovDelta;
                        }
                    }
                    else
                    {
                        currentFov = goalFov;
                    }
                }

                if (goalPan != currentPan)
                {
                    if (panRate > 0.0f)
                    {
                        float panDelta = Mathf.Min(Mathf.Abs(goalPan - currentPan), panRate * TimeWarp.deltaTime);
                        if (goalPan > currentPan)
                        {
                            currentPan += panDelta;
                        }
                        else
                        {
                            currentPan -= panDelta;
                        }
                    }
                    else
                    {
                        currentPan = goalPan;
                    }
                }

                if (goalTilt != currentTilt)
                {
                    if (tiltRate > 0.0f)
                    {
                        float tiltDelta = Mathf.Min(Mathf.Abs(goalTilt - currentTilt), tiltRate * TimeWarp.deltaTime);
                        if (goalTilt > currentTilt)
                        {
                            currentTilt += tiltDelta;
                        }
                        else
                        {
                            currentTilt -= tiltDelta;
                        }
                    }
                    else
                    {
                        currentTilt = goalTilt;
                    }
                }

                if (cameraLive != (renderCallback != null))
                {
                    cameraLive = (renderCallback != null);

                    if (cameraLive)
                    {
                        if (cameraRentex == null)
                        {
                            cameraRentex = new RenderTexture(mode[activeMode].cameraResolution, mode[activeMode].cameraResolution, 24);
                        }
                    }
                    else
                    {
                        cameraRentex.Release();
                        cameraRentex = null;
                    }

                    for (int i = cameraBody.Length - 1; i >= 0; --i)
                    {
                        if (cameras[i] == null)
                        {
                            ConstructCamera(i);
                        }

                        // It looks like the FXCamera can be null when the vessel being loaded isn't the current vessel
                        // (such as when entering physics range), so we have to null-check here.
                        if (cameras[i] != null)
                        {
                            cameras[i].enabled = cameraLive;
                            cameras[i].targetTexture = cameraRentex;
                        }
                    }
                }

                if (cameraLive)
                {
                    if (!cameraRentex.IsCreated())
                    {
                        cameraRentex.Create();
                    }

                    cameraRotation = cameraTransform.rotation * Quaternion.Euler(-currentTilt, currentPan, 0.0f);

                    if (refreshRate == 1 || (frameCount % refreshRate) == 0)
                    {
                        renderCallback.Invoke(cameraRentex, mode[activeMode].postProcShader);
                        cameraRentex.DiscardContents();
                    }

                    ++frameCount;
                }
            }
        }

        /// <summary>
        /// Callback triggered prior to culling.  We use this event to make sure the cameras
        /// are pointed in the right direction and that the cameras that are attached to the
        /// vessel are positioned correctly.
        /// </summary>
        /// <param name="whichCamera"></param>
        private void CameraPreCull(Camera whichCamera)
        {
            int cameraIndex = Array.FindIndex(cameras, x => x == whichCamera);
            if (cameraIndex >= 0)
            {
                whichCamera.gameObject.transform.rotation = cameraRotation;
                if (cameraIndex > 1)
                {
                    // GalaxyCamera and ScaledSpace cameras should not move - only rotate.
                    // The remainder of them move here:
                    whichCamera.gameObject.transform.position = cameraTransform.position;
                }
                else if (cameraIndex == 1)
                {
                    whichCamera.gameObject.transform.position = scaledSpace.gameObject.transform.position;
                }
                whichCamera.fieldOfView = currentFov;
            }
        }

        /// <summary>
        /// Make sure the shaders have access to a flight computer, so they can update
        /// variables.
        /// </summary>
        /// <param name="comp"></param>
        public void UpdateFlightComputer(MASFlightComputer comp)
        {
            for (int i = 0; i < mode.Length; ++i)
            {
                mode[i].UpdateShaderProperties(comp);
            }
        }

        #endregion

        #region Editor
        private static readonly Material fovRendererMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
        private LineRenderer minFovRenderer;
        private GameObject maxFovPosition;
        private LineRenderer maxFovRenderer;
        /// <summary>
        /// Configure the editor field-of-view cones.  There are two - one of the min angle, and one for the
        /// max - but we only configure one if min == max.
        /// </summary>
        private void CreateFovRenderer()
        {
            float minSpan = rayLength * 2.0f * (float)Math.Tan(Mathf.Deg2Rad * fovRange.x * 0.5f);

            part.OnEditorAttach += AttachPart;
            part.OnEditorDetach += DetachPart;
            part.OnEditorDestroy += DetachPart;

            minFovRenderer = cameraTransform.gameObject.AddComponent<LineRenderer>();
            minFovRenderer.material = fovRendererMaterial;
            minFovRenderer.startWidth = 0.054f;
            minFovRenderer.endWidth = minSpan;
            minFovRenderer.positionCount = 2;
            minFovRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            minFovRenderer.receiveShadows = false;
            Vector3 origin = cameraTransform.TransformPoint(Vector3.zero);
            Vector3 direction = cameraTransform.forward;
            minFovRenderer.SetPosition(0, origin);
            minFovRenderer.SetPosition(1, origin + direction * rayLength);
            Color startColor = (fovRange.y > fovRange.x) ? new Color(0.0f, 1.0f, 0.0f, 0.75f) : new Color(0.0f, 1.0f, 1.0f, 0.75f);
            Color endColor = startColor;
            endColor.a = 0.0f;
            minFovRenderer.startColor = startColor;
            minFovRenderer.endColor = endColor;
            minFovRenderer.enabled = showFov;

            if (fovRange.y > fovRange.x)
            {
                float maxSpan = rayLength * 2.0f * (float)Math.Tan(Mathf.Deg2Rad * fovRange.y * 0.5f);

                maxFovPosition = new GameObject();
                maxFovPosition.name = cameraTransform.gameObject.name + "-MaxFoV";
                maxFovPosition.transform.parent = cameraTransform;
                maxFovPosition.transform.position = cameraTransform.position;
                maxFovRenderer = maxFovPosition.AddComponent<LineRenderer>();
                maxFovRenderer.material = fovRendererMaterial;
                maxFovRenderer.startWidth = 0.054f;
                maxFovRenderer.endWidth = maxSpan;
                maxFovRenderer.positionCount = 2;
                maxFovRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                maxFovRenderer.receiveShadows = false;
                maxFovRenderer.SetPosition(0, origin);
                maxFovRenderer.SetPosition(1, origin + direction * rayLength);
                startColor = new Color(0.0f, 0.0f, 1.0f, 0.65f);
                endColor = startColor;
                endColor.a = 0.0f;
                maxFovRenderer.startColor = startColor;
                maxFovRenderer.endColor = endColor;
                maxFovRenderer.enabled = showFov;
            }
        }

        private bool showGui = false;
        private PopupDialog nameMenu = null;

        /// <summary>
        /// Create the PopupDialog that allows the user to edit the name of the camera.
        /// </summary>
        private void ShowNameMenu()
        {
            if (nameMenu == null)
            {
                nameMenu = PopupDialog.SpawnPopupDialog(
                   new Vector2(0.5f, 0.5f),
                   new Vector2(0.5f, 0.5f),
                   new MultiOptionDialog(
                       "MASCamera-Name",
                       KSP.Localization.Localizer.GetStringByTag("#MAS_Camera_Name_Prompt"),
                       KSP.Localization.Localizer.GetStringByTag("#MAS_Camera_Name_Label"),
                       HighLogic.UISkin,
                       new Rect(0.5f, 0.5f, 150.0f, 60.0f),
                       new DialogGUIFlexibleSpace(),
                       new DialogGUIVerticalLayout(
                           new DialogGUIFlexibleSpace(),
                           new DialogGUITextInput(newCameraName, false, 40, SetTextCallback, 140.0f, 30.0f),
                           new DialogGUIButton(KSP.Localization.Localizer.GetStringByTag("#autoLOC_174783"), // Cancel
                               delegate { showGui = false; }, 140.0f, 30.0f, false),
                           new DialogGUIButton(KSP.Localization.Localizer.GetStringByTag("#autoLOC_174814"), // OK
                               delegate { showGui = false; cameraName = newCameraName; }, 140.0f, 30.0f, false)
                           )
                       ),
                       false,
                       HighLogic.UISkin);
                InputLockManager.SetControlLock(ControlTypes.KEYBOARDINPUT, "MASCamera-UI");
            }
        }

        /// <summary>
        /// I assume the return parameter means I can edit this string before returning it.
        /// </summary>
        /// <param name="newString"></param>
        /// <returns></returns>
        private string SetTextCallback(string newString)
        {
            newCameraName = newString.Trim();
            return newString;
        }

        /// <summary>
        /// Return the radar module's name for the Editor.
        /// </summary>
        /// <returns></returns>
        public override string GetModuleDisplayName()
        {
            return "#MAS_Camera_Module_DisplayName";
        }

        /// <summary>
        /// TODO: Meaningful string.
        /// </summary>
        /// <returns></returns>
        public override string GetInfo()
        {
            return "#MAS_Camera_GetInfo";
        }
        #endregion

        /// <summary>
        /// Open the 'MASCamera Name' GUI to allow changing camera name.
        /// </summary>
        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "#MAS_Camera_Set_Name")]
        public void SetCameraName()
        {
            showGui = !showGui;
            if (showGui)
            {
                newCameraName = cameraName;
            }
        }
    }

    /// <summary>
    /// A minimal module that allows a MASCamera to be designated as a deployable camera, taking advantage
    /// of the stock mechanism for deployable parts that can be broken by the wind.
    /// </summary>
    public class MASDeployableCamera : ModuleDeployablePart
    {
        public MASDeployableCamera()
        {
            isTracking = false;
        }

        public override void OnLoad(ConfigNode node)
        {
            // Unclear why this needs to be done here, but partType won't be correct without it.
            base.OnLoad(node);

            this.subPartName = "#MAS_DeployableCamera_SubPart";
            this.subPartMass = Mathf.Min(0.001f, this.part.mass * 0.5f);

            this.partType = "#MAS_DeployableCamera_Part";
        }

        public override string GetModuleDisplayName()
        {
            return "#MAS_DeployableCamera_ModuleName";
        }
    }
}
