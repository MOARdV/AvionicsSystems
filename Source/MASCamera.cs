/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2017 MOARdV
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
using UnityEngine;

namespace AvionicsSystems
{
    class MASCamera : PartModule
    {
        /// <summary>
        /// Defines the minimum and maximum field of view of the camera lens
        /// as measured across the vertical (Y) axis, in degrees.  Automatically
        /// clamps values between 1 and 90.
        /// </summary>
        [KSPField]
        public Vector2 fovRange = new Vector2(50.0f, 50.0f);

        /// <summary>
        /// Used internally to allow current FoV to persist.  Should only
        /// be changed programmatically through AddFoV() and SetFoV() to
        /// manage the FoV limits.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentFov = 50.0f;

        /// <summary>
        /// Defines the minimum and maximum pan angle (left-right camera rotation)
        /// of the camera lens in degrees.  Automatically clamps between
        /// -180 and +180.  Negative values indicate to the left of the center
        /// position. Positive values indicate to the right.
        /// </summary>
        [KSPField]
        public Vector2 panRange = new Vector2(0.0f, 0.0f);

        /// <summary>
        /// Used internally to allow current pan angle to persist.  Should only
        /// be changed programmatically through AddPan() and SetPan() to
        /// manage the pan limits.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentPan = 0.0f;

        /// <summary>
        /// Defines the minimum and maximum tilt angle (up-down camera rotation)
        /// of the camera lens in degrees.  Automatically clamps between
        /// -180 and +180.  Negative values indicate (direction), positive
        /// values indicate (other direction).
        /// </summary>
        [KSPField]
        public Vector2 tiltRange = new Vector2(0.0f, 0.0f);

        /// <summary>
        /// Used internally to allow current tilt angle to persist.  Should only
        /// be changed programmatically through AddTilt() and SetTilt() to
        /// manage the tilt limits.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentTilt = 0.0f;

        /// <summary>
        /// Name of the transform that the camera is attached to.
        /// </summary>
        [KSPField]
        public string cameraTransformName = string.Empty;
        internal Transform cameraTransform = null;

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
        /// A unique name for the camera.  Note that cameras missing a name can not
        /// be selected in-flight, and if several cameras have the same name,
        /// only one of them will be selectable.
        /// </summary>
        [KSPField(isPersistant = true)]
        public string cameraName = string.Empty;
        private string newCameraName = string.Empty;

        [UI_Toggle(disabledText = "Off", enabledText = "On")]
        [KSPField(guiActiveEditor = true, guiName = "FOV marker")]
        public bool showFov = false;
        const float rayLength = 10.0f;

        private static readonly string[] knownCameraNames = 
        {
            "GalaxyCamera",
            "Camera ScaledSpace",
            //"Camera VE Underlay", // Environmental Visual Enhancements plugin camera
            //"Camera VE Overlay",  // Environmental Visual Enhancements plugin camera
            "Camera 01",
            "Camera 00",
            "FXCamera"
        };
        private readonly Camera[] cameras = { null, null, null, null, null };//, null, null };
        private Quaternion cameraRotation = Quaternion.identity;

        /// <summary>
        /// Is this object ready to use?
        /// </summary>
        /// <returns>true if a this object will function, false otherwise</returns>
        public bool IsValid()
        {
            return cameraTransform != null;
        }

        #region Setup - Teardown
        private static bool dumped = false;
        /// <summary>
        /// Configure everything.
        /// </summary>
        public void Start()
        {
            if (!(HighLogic.LoadedScene == GameScenes.EDITOR || HighLogic.LoadedScene == GameScenes.FLIGHT))
            {
                return;
            }
            if (!dumped)
            {
                dumped = true;
                for (int i = 0; i < Camera.allCamerasCount; ++i)
                {
                    Utility.LogMessage(this, "AllCam[{0,2}]: {1} on {2:X}", i, Camera.allCameras[i].name, Camera.allCameras[i].cullingMask);
                }
                FlightCamera flight = FlightCamera.fetch;
                Utility.LogMessage(this, "CameraMain: {0} on {1:X}", flight.mainCamera.name, flight.mainCamera.cullingMask);
                for (int i = 0; i < flight.cameras.Length; ++i)
                {
                    Utility.LogMessage(this, "FltCam[{0,2}]: {1} on {2:X}", i, flight.cameras[i].name, flight.cameras[i].cullingMask);
                }
            }

            if (knownCameraNames.Length != cameras.Length)
            {
                throw new NotImplementedException("MASCamera: Camera Names array has a different size than cameras array!");
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
                // TODO: And clamp the ranges to legal ranges.
                if (fovRange.y < fovRange.x)
                {
                    fovRange = new Vector2(fovRange.y, fovRange.x);
                }
                fovRange.x = Mathf.Clamp(fovRange.x, 1.0f, 90.0f);
                fovRange.y = Mathf.Clamp(fovRange.y, 1.0f, 90.0f);
                currentFov = Mathf.Clamp(currentFov, fovRange.x, fovRange.y);

                if (panRange.y < panRange.x)
                {
                    panRange = new Vector2(panRange.y, panRange.x);
                }
                panRange.x = Mathf.Clamp(panRange.x, -180.0f, 180.0f);
                panRange.y = Mathf.Clamp(panRange.y, -180.0f, 180.0f);
                currentPan = Mathf.Clamp(currentPan, panRange.x, panRange.y);

                if (tiltRange.y < tiltRange.x)
                {
                    tiltRange = new Vector2(tiltRange.y, tiltRange.x);
                }
                tiltRange.x = Mathf.Clamp(tiltRange.x, -180.0f, 180.0f);
                tiltRange.y = Mathf.Clamp(tiltRange.y, -180.0f, 180.0f);
                currentTilt = Mathf.Clamp(currentTilt, tiltRange.x, tiltRange.y);

                cameraRotation = cameraTransform.rotation * Quaternion.Euler(currentPan, currentTilt, 0.0f);

                if (HighLogic.LoadedSceneIsEditor)
                {
                    CreateFovRenderer();
                }
                else // must be flight
                {
                    CreateFlightCameras();
                }
            }
            else if (string.IsNullOrEmpty(cameraTransformName))
            {
                Utility.LogErrorMessage(this, "No 'cameraTransformName' provided in part.");
                throw new NotImplementedException("MASCamera: Missing 'cameraTransformName' in module config node.");
            }
            else
            {
                Utility.LogErrorMessage(this, "Unable to find transform \"{0}\" in part", cameraTransformName);
            }
        }

        /// <summary>
        /// Helper function to locate flight cameras.
        /// </summary>
        /// <param name="cameraName">Name of the camera we're looking for.</param>
        /// <returns>The named camera, or null if it was not found.</returns>
        private static Camera GetCameraByName(string cameraName)
        {
            for (int i = 0; i < Camera.allCamerasCount; ++i)
            {
                if (Camera.allCameras[i].name == cameraName)
                {
                    return Camera.allCameras[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Create the cameras used during flight.
        /// </summary>
        private void CreateFlightCameras()
        {
            for (int i = 0; i < cameras.Length; ++i)
            {
                Camera sourceCamera = GetCameraByName(knownCameraNames[i]);
                if (sourceCamera != null)
                {
                    GameObject cameraBody = new GameObject();
                    cameraBody.name = "MASCamera-" + i + "-" + cameraBody.GetInstanceID();
                    cameras[i] = cameraBody.AddComponent<Camera>();

                    // Just in case to support JSITransparentPod.
                    cameras[i].cullingMask &= ~(1 << 16 | 1 << 20);

                    cameras[i].CopyFrom(sourceCamera);
                    cameras[i].enabled = false;
                    cameras[i].aspect = 1.0f;
                    cameras[i].fieldOfView = currentFov;
                    cameras[i].transform.rotation = cameraRotation;

                    // Minor hack to bring the near clip plane for the "up close"
                    // cameras drastically closer to where the cameras notionally
                    // are.  Experimentally, these two cameras have N/F of 0.4 / 300.0,
                    // or 750:1 Far/Near ratio.  Changing this to 8192:1 brings the
                    // near plane to 37cm or so, which hopefully is close enough to
                    // see nearby details without creating z-fighting artifacts.
                    if (i == 3 || i == 4)
                    {
                        cameras[i].nearClipPlane = cameras[i].farClipPlane / 8192.0f;
                    }
                }
            }
        }

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

        private void DetachPart()
        {
            showFov = false;
        }

        /// <summary>
        /// Tear down and release resources.
        /// </summary>
        public void OnDestroy()
        {
            if (minFovRenderer != null)
            {
                Destroy(minFovRenderer);
                minFovRenderer = null;
                Destroy(minFovPosition);
                minFovPosition = null;
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
            if (cameras[0] != null)
            {
                for (int i = 0; i < cameras.Length; ++i)
                {
                    try
                    {
                        UnityEngine.Object.Destroy(cameras[i]);
                    }
                    catch
                    {

                    }
                    finally
                    {
                        cameras[i] = null;
                    }
                }
            }

            if (nameMenu != null)
            {
                InputLockManager.RemoveControlLock("MASCamera-UI");
                nameMenu.Dismiss();
                nameMenu = null;
            }
        }
        #endregion

        #region Flight

        /// <summary>
        /// Change the current field of view by `deltaFoV` degrees, remaining within
        /// camera FoV limits.
        /// </summary>
        /// <param name="deltaFoV">The amount to add or subtract to FoV in degrees.</param>
        public void AddFoV(float deltaFoV)
        {
            currentFov = Mathf.Clamp(currentFov + deltaFoV, fovRange.x, fovRange.y);
            for (int i = cameras.Length - 1; i >= 0; --i)
            {
                if (cameras[i] != null)
                {
                    cameras[i].fieldOfView = currentFov;
                }
            }
        }

        /// <summary>
        /// Set the current field of view, remaining within camera FoV limits.
        /// </summary>
        /// <param name="fieldOfView">The new FoV in degrees.</param>
        public void SetFoV(float fieldOfView)
        {
            currentFov = Mathf.Clamp(fieldOfView, fovRange.x, fovRange.y);
            for (int i = cameras.Length - 1; i >= 0; --i)
            {
                if (cameras[i] != null)
                {
                    cameras[i].fieldOfView = currentFov;
                }
            }
        }

        /// <summary>
        /// Enable / disable the FOV cones.  Valid only in the editor.
        /// </summary>
        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                // TODO: Renderers don't show up if the part is added.
                // only when a craft is loaded with camera attached.
                // RPM used callbacks for onattach / ondetach - maybe
                // they need to be used here.
                if (minFovRenderer != null)
                {
                    minFovRenderer.enabled = showFov;
                }
                if (maxFovRenderer != null)
                {
                    maxFovRenderer.enabled = showFov;
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
                cameraRotation = cameraTransform.rotation * Quaternion.Euler(currentPan, currentTilt, 0.0f);
                for (int i = 0; i < cameras.Length; ++i)
                {
                    if (cameras[i] != null)
                    {
                        // Comment from RPM:
                        // ScaledSpace camera and its derived cameras from Visual Enhancements mod are special - they don't move.
                        // TODO: But the EVE overlay camera is at 3 - so is this right?  Or should it be 4?
                        // Actually, it seems only the galaxy camera is a problem.
                        if (i > 0)
                        {
                            cameras[i].transform.position = cameraTransform.position;
                        }
                        //cameras[i].targetTexture = renderTarget;
                        //cameras[i].aspect = aspectRatio;
                        cameras[i].transform.rotation = cameraRotation;
                        //cameras[i].fieldOfView = currentFov;
                        //cameras[i].Render();
                    }

                }
            }
        }

        /// <summary>
        /// Render the scenes onto the supplied render texture.
        /// </summary>
        /// <param name="renderTarget">The RenderTexture to draw on.</param>
        /// <returns>true if cameras rendered, false otherwise.</returns>
        public bool Render(RenderTexture renderTarget)
        {
            if (!HighLogic.LoadedSceneIsFlight)
            {
                return false;
            }

            float width = renderTarget.width;
            float height = renderTarget.height;
            float aspectRatio = width / height;
            int camerasLength = cameras.Length;

            //cameraRotation = cameraTransform.rotation * Quaternion.Euler(currentPan, currentTilt, 0.0f);

            // Comment from RPM:
            // This is a hack - FXCamera isn't always available, so I need to add and remove it in flight.
            // I don't know if there's a callback I can use to find when it's added, so brute force it for now.
            // TODO: Is that still operational?  It does not appear to be the case.

            for (int i = 0; i < camerasLength; ++i)
            {
                if (cameras[i] != null)
                {
                    // Comment from RPM:
                    // ScaledSpace camera and its derived cameras from Visual Enhancements mod are special - they don't move.
                    // TODO: But the EVE overlay camera is at 3 - so is this right?  Or should it be 4?
                    //if (i >= 1)
                    //{
                    //    cameras[i].transform.position = cameraTransform.position;
                    //}
                    cameras[i].targetTexture = renderTarget;
                    cameras[i].aspect = aspectRatio;
                    //cameras[i].transform.rotation = cameraRotation;
                    cameras[i].fieldOfView = currentFov;
                    cameras[i].Render();
                }
            }
            return true;
        }
        #endregion

        #region Editor
        private static readonly Material fovRendererMaterial = new Material(Shader.Find("Particles/Additive"));
        private GameObject minFovPosition;
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

            minFovPosition = new GameObject();
            minFovRenderer = minFovPosition.AddComponent<LineRenderer>();
            minFovRenderer.useWorldSpace = true;
            minFovRenderer.material = fovRendererMaterial;
            minFovRenderer.SetWidth(0.054f, minSpan);
            minFovRenderer.SetVertexCount(2);
            minFovRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            minFovRenderer.receiveShadows = false;
            Vector3 origin = cameraTransform.TransformPoint(Vector3.zero);
            Vector3 direction = cameraTransform.forward;
            minFovRenderer.SetPosition(0, origin);
            minFovRenderer.SetPosition(1, origin + direction * rayLength);
            Color startColor = (fovRange.y > fovRange.x) ? new Color(0.0f, 1.0f, 0.0f, 0.75f) : new Color(0.0f, 1.0f, 1.0f, 0.75f);
            Color endColor = startColor;
            endColor.a = 0.0f;
            minFovRenderer.SetColors(startColor, endColor);
            minFovRenderer.enabled = showFov;

            if (fovRange.y > fovRange.x)
            {
                float maxSpan = rayLength * 2.0f * (float)Math.Tan(Mathf.Deg2Rad * fovRange.y * 0.5f);

                maxFovPosition = new GameObject();
                maxFovRenderer = maxFovPosition.AddComponent<LineRenderer>();
                maxFovRenderer.useWorldSpace = true;
                maxFovRenderer.material = fovRendererMaterial;
                maxFovRenderer.SetWidth(0.054f, maxSpan);
                maxFovRenderer.SetVertexCount(2);
                maxFovRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                maxFovRenderer.receiveShadows = false;
                maxFovRenderer.SetPosition(0, origin);
                maxFovRenderer.SetPosition(1, origin + direction * rayLength);
                startColor = new Color(0.0f, 0.0f, 1.0f, 0.65f);
                endColor = startColor;
                endColor.a = 0.0f;
                maxFovRenderer.SetColors(startColor, endColor);
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
                       "Name this camera:",
                       "MASCamera Name",
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
            newCameraName = newString;
            return newString;
        }

        /// <summary>
        /// TODO: Meaningful string.
        /// </summary>
        /// <returns></returns>
        public override string GetInfo()
        {
            return "Cameras rule!";
        }
        #endregion

        /// <summary>
        /// Open the 'MASCamera Name' GUI to allow changing camera name.
        /// </summary>
        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Set Camera Name")]
        public void SetCameraName()
        {
            showGui = !showGui;
            if (showGui)
            {
                newCameraName = cameraName;
            }
        }
    }
}
