/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2017-2018 MOARdV
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
using KSP.Localization;
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// MASSettings implements the AppLauncher button and user interface to change MAS
    /// settings in game.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    class MASSettings : MonoBehaviour
    {
        private static ApplicationLauncherButton appLauncherButton = null;

        private Texture2D iconDisableTexture;
        private Texture2D iconEnableTexture;
        private PopupDialog settingsMenu = null;
        private bool guiVisible = false;

        public void Awake()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(AddAppLauncherButton);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(RemoveAppLauncherButton);
            GameEvents.onGUIApplicationLauncherUnreadifying.Add(UnreadifyAppLauncherButton);
        }

        public void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(AddAppLauncherButton);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(RemoveAppLauncherButton);
            GameEvents.onGUIApplicationLauncherUnreadifying.Remove(UnreadifyAppLauncherButton);
            RemoveAppLauncherButton();
        }

        private void AddAppLauncherButton()
        {
            if (appLauncherButton == null && MASConfig.HideGui == false)
            {
                appLauncherButton = InitAppLauncherButton();
            }
        }

        private void RemoveAppLauncherButton()
        {
            if (appLauncherButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
                appLauncherButton = null;
            }
        }

        private void UnreadifyAppLauncherButton(GameScenes scene)
        {
            if (scene != GameScenes.SPACECENTER)
            {
                RemoveAppLauncherButton();
            }
            else
            {
                AddAppLauncherButton();
            }
        }

        private ApplicationLauncherButton InitAppLauncherButton()
        {
            iconDisableTexture = GameDatabase.Instance.GetTexture("MOARdV/AvionicsSystems/Icons/toolbar_disabled", false);
            iconEnableTexture = GameDatabase.Instance.GetTexture("MOARdV/AvionicsSystems/Icons/toolbar_enabled", false);

            return ApplicationLauncher.Instance.AddModApplication(onAppLauncherShow, onAppLauncherHide,
                null, null, null, null,
                ApplicationLauncher.AppScenes.SPACECENTER,
                iconDisableTexture);
        }

        private void ShowGui()
        {
            if (settingsMenu == null)
            {
                InitValues();
                settingsMenu = PopupDialog.SpawnPopupDialog(
                   new Vector2(0.5f, 0.5f),
                   new Vector2(0.5f, 0.5f),
                   new MultiOptionDialog(
                       "MASSettings-Config",
                       Localizer.GetStringByTag("#MAS_Settings_Caption"),
                       Localizer.GetStringByTag("#MAS_Settings_Title"),
                       HighLogic.UISkin,
                       new Rect(0.5f, 0.5f, 300.0f, 60.0f),
                       new DialogGUIFlexibleSpace(),
                       new DialogGUIVerticalLayout(
                           new DialogGUIHorizontalLayout(
                               new DialogGUIVerticalLayout(
                                   new DialogGUILabel(Localizer.GetStringByTag("#MAS_Settings_General_Section"), true),
                                   new DialogGUISpace(10.0f),
                                   new DialogGUIToggle(verboseLogging, Localizer.GetStringByTag("#MAS_Settings_Verbose_Logging"), (bool newValue) => { verboseLogging = newValue; }),
                                   new DialogGUISpace(5.0f),
                                   new DialogGUILabel(delegate { return Localizer.Format("#MAS_Settings_Lua_Update_Priority", luaUpdatePriority.ToString()); }, true),
                                   new DialogGUISlider(delegate { return (float)luaUpdatePriority; }, 1.0f, 4.0f, true, 140.0f, 30.0f, (float newValue) => { luaUpdatePriority = (int)newValue; }),
                                   new DialogGUISpace(5.0f),
                                   new DialogGUILabel(delegate { return Localizer.Format("#MAS_Settings_Camera_Texture_Size", (1 << cameraTextureScale).ToString()); }, true),
                                   new DialogGUISlider(delegate { return (float)cameraTextureScale; }, 0.0f, 2.0f, true, 140.0f, 30.0f, (float newValue) => { cameraTextureScale = (int)newValue; }),
                                   new DialogGUISpace(5.0f),
                                   new DialogGUIToggle(enableCommNetWaypoints, Localizer.GetStringByTag("#MAS_Settings_CommNet_Waypoints"), (bool newValue) => { enableCommNetWaypoints = newValue; }),
                                   new DialogGUISpace(5.0f),
                                   new DialogGUIFlexibleSpace()
                                   ),
                                new DialogGUIVerticalLayout(
                                   new DialogGUILabel(Localizer.GetStringByTag("#MAS_Settings_RadioNav_Section"), true),
                                   new DialogGUISpace(10.0f),
                                   new DialogGUIToggle(enableNavBeacons, Localizer.GetStringByTag("#MAS_Settings_Nav_Beacons"), (bool newValue) => { enableNavBeacons = newValue; }),
                                   new DialogGUISpace(5.0f),
                                   new DialogGUILabel(delegate { return Localizer.Format("#MAS_Settings_Radio_Propagation", generalPropagation.ToString("P0")); }, true),
                                   new DialogGUISlider(delegate { return generalPropagation; }, 1.0f, 3.0f, false, 140.0f, 30.0f, (float newValue) => { generalPropagation = newValue; }),
                                   new DialogGUISpace(5.0f),
                                   new DialogGUILabel(delegate { return Localizer.Format("#MAS_Settings_NDB_Propagation", NDBPropagation.ToString("P0")); }, true),
                                   new DialogGUISlider(delegate { return NDBPropagation; }, 1.0f, 2.0f, false, 140.0f, 30.0f, (float newValue) => { NDBPropagation = newValue; }),
                                   new DialogGUISpace(5.0f),
                                   new DialogGUILabel(delegate { return Localizer.Format("#MAS_Settings_VOR_Propagation", VORPropagation.ToString("P0")); }, true),
                                   new DialogGUISlider(delegate { return VORPropagation; }, 1.0f, 2.0f, false, 140.0f, 30.0f, (float newValue) => { VORPropagation = newValue; }),
                                   new DialogGUISpace(5.0f),
                                   new DialogGUILabel(delegate { return Localizer.Format("#MAS_Settings_DME_Propagation", DMEPropagation.ToString("P0")); }, true),
                                   new DialogGUISlider(delegate { return DMEPropagation; }, 1.0f, 2.0f, false, 140.0f, 30.0f, (float newValue) => { DMEPropagation = newValue; }),
                                   new DialogGUISpace(5.0f),
                                   new DialogGUIToggle(resetWaypoints, Localizer.GetStringByTag("#MAS_Settings_Reset_Waypoints"), (bool selected) => { resetWaypoints = selected; }, 140.0f, 30.0f),
                                   new DialogGUIFlexibleSpace()
                                    )
                               ),
                           new DialogGUIHorizontalLayout(
                               new DialogGUIFlexibleSpace(),
                               new DialogGUIButton(Localizer.GetStringByTag("#autoLOC_174783"), // Cancel
                                   delegate
                                   {
                                       onAppLauncherHide();
                                       appLauncherButton.SetFalse(false);
                                   }, 140.0f, 30.0f, false),
                               new DialogGUIButton(Localizer.GetStringByTag("#autoLOC_174814"), // OK
                                   delegate
                                   {
                                       onAppLauncherHide();
                                       appLauncherButton.SetFalse(false);
                                       ApplyChanges();
                                   }, 140.0f, 30.0f, false)
                               )
                           )
                       ),
                       false,
                       HighLogic.UISkin);
            }
        }

        private bool verboseLogging;
        private int luaUpdatePriority;
        private int cameraTextureScale;
        private bool enableNavBeacons;
        private bool enableCommNetWaypoints;
        private float generalPropagation;
        private float NDBPropagation;
        private float VORPropagation;
        private float DMEPropagation;
        private bool resetWaypoints;
        private void InitValues()
        {
            verboseLogging = MASConfig.VerboseLogging;
            luaUpdatePriority = MASConfig.LuaUpdatePriority;
            cameraTextureScale = MASConfig.CameraTextureScale;
            enableCommNetWaypoints = MASConfig.EnableCommNetWaypoints;
            enableNavBeacons = MASConfig.navigation.enableNavBeacons;
            generalPropagation = MASConfig.navigation.generalPropagation;
            NDBPropagation = MASConfig.navigation.NDBPropagation;
            VORPropagation = MASConfig.navigation.VORPropagation;
            DMEPropagation = MASConfig.navigation.DMEPropagation;
            resetWaypoints = MASConfig.ResetWaypoints;
        }

        private void ApplyChanges()
        {
            MASConfig.VerboseLogging = verboseLogging;
            MASConfig.LuaUpdatePriority = luaUpdatePriority;
            MASConfig.CameraTextureScale = cameraTextureScale;
            MASConfig.EnableCommNetWaypoints = enableCommNetWaypoints;
            MASConfig.navigation.enableNavBeacons = enableNavBeacons;
            MASConfig.navigation.generalPropagation = generalPropagation;
            MASConfig.navigation.NDBPropagation = NDBPropagation;
            MASConfig.navigation.VORPropagation = VORPropagation;
            MASConfig.navigation.DMEPropagation = DMEPropagation;
            MASConfig.ResetWaypoints = resetWaypoints;

            int numNavAids = MASLoader.navaids.Count;
            for (int i = 0; i < numNavAids; ++i)
            {
                MASLoader.navaids[i].UpdateHorizonDistance();
            }
        }

        private void onAppLauncherShow()
        {
            guiVisible = true;
            ToggleIcon();
            ShowGui();
        }

        private void onAppLauncherHide()
        {
            guiVisible = false;
            ToggleIcon();
            if (settingsMenu != null)
            {
                settingsMenu.Dismiss();
                settingsMenu = null;
            }
        }

        private void ToggleIcon()
        {
            if (guiVisible)
            {
                appLauncherButton.SetTexture(iconEnableTexture);
            }
            else
            {
                appLauncherButton.SetTexture(iconDisableTexture);
            }
        }
    }
}
