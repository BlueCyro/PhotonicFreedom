using BaseX;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
using NeosModLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace NeosModloaderMod
{
    public class ModClass : NeosMod
    {
        public override string Author => "Cyro";
        public override string Name => "Photonic Freedom";
        public override string Version => "1.0.0";

        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony("net.cyro.PhotonicFreedom");
            harmony.PatchAll();

        }


        [HarmonyPatch(typeof(SettingsDialog), "OnAttach")]
        class SettingsPatcher{

            private static string BlurPath = "Settings.PostProcessing.MotionBlurState";
            private static string AOPath = "Settings.PostProcessing.AmbientOcclusionState";
            private static void ToggleBlurs(IChangeable c){
                //Blatant copy of MotionBlurDisable because I haven't researched unity enough yet :)
                PostProcessLayer[] postProcessLayers = Resources.FindObjectsOfTypeAll<PostProcessLayer>();
                //For each post process layer, find the motion blur effect and save it to an array
                MotionBlur[] motionBlurs = postProcessLayers.Select(x => x.defaultProfile.GetSetting<MotionBlur>()).ToArray();
                //For each motion blur effect, invert the enabled value
                foreach (MotionBlur motionBlur in motionBlurs)
                {
                    motionBlur.enabled.value = Settings.ReadValue(BlurPath, false);
                }
                
            }

            
            private static void ToggleAO(IChangeable c){
                //Find all cameras in the unity scene
                UnityEngine.Camera[] cameras = Resources.FindObjectsOfTypeAll<UnityEngine.Camera>();
                //For each camera, find the AmplifyOcclusionEffect
                foreach (UnityEngine.Camera camera in cameras)
                {
                    var ao = camera.GetComponent<AmplifyOcclusionEffect>();
                    //If the camera has the AmplifyOcclusionEffect, invert the enabled value
                    try
                    {
                        ao.enabled = Settings.ReadValue(AOPath, false);
                    }
                    catch(Exception e){
                        UniLog.Log(e.Message);
                    }
                }

            }

            static void Postfix(SettingsDialog __instance){
                
                //Find the rect transform on the right side of the settings dialog
                var RightRectSlot = __instance.Slot.FindChild(s => s.Name == "Right").FindChild(s => s.Name == "Content");
                var RightRect = RightRectSlot.GetComponent<FrooxEngine.UIX.RectTransform>();

                //Find one of the LayoutElements so that we can read the MinHeight and the PreferredHeight from it (I felt bad hardcoding these values so I wanted to get them dynamically))
                var LayoutTemplate = RightRectSlot.GetComponentInChildren<LayoutElement>();

                //Create a new UIBuilder on the rect transform we found and set it up with the default style of the rest of the menu
                var Builder = new UIBuilder(RightRect);
                RadiantUI_Constants.SetupDefaultStyle(Builder, false);

                //Reading those aforementioned layout element values
                Builder.Style.MinHeight = LayoutTemplate.MinHeight.Value;
                Builder.Style.PreferredHeight = LayoutTemplate.PreferredHeight.Value;

                //Create checkboxes for the blur and AO settings
                var BlurCheckBox = Builder.Checkbox("Enable Motion Blur", false, true, 4f);
                var AOCheckBox = Builder.Checkbox("Enable Ambient Occlusion", false, true, 4f);                

                //Add a settingsync for both checkboxes and subsequently set up listeners to see if they've changed.
                var BlurSync = BlurCheckBox.State.SyncWithSetting(BlurPath, SettingSync.LocalChange.UpdateSetting);
                var AOSync = AOCheckBox.State.SyncWithSetting(AOPath, SettingSync.LocalChange.UpdateSetting);

                BlurSync.Changed += ToggleBlurs;
                AOSync.Changed += ToggleAO;
            }
        }

    }
}
