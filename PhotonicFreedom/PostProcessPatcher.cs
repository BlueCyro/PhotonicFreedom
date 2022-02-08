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
        public override string Version => "1.0.1";

        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony("net.cyro.PhotonicFreedom");
            harmony.PatchAll();

        }


        [HarmonyPatch(typeof(SettingsDialog), "OnAttach")]
        class SettingsPatcher{

            private static string BlurPath = "Settings.PostProcessing.MotionBlurState";
            private static string AOPath = "Settings.PostProcessing.AmbientOcclusionState";
            private static string BlurQualityPath = "Settings.PostProcessing.MotionBlurQuality";

            private static MotionBlur[] GetBlurs(){
                //For each post process layer, find the motion blur effect and save it to an array
                return Resources.FindObjectsOfTypeAll<PostProcessLayer>().Select(x => x.defaultProfile.GetSetting<MotionBlur>()).ToArray();
            }

            private static AmplifyOcclusionEffect[] GetAOs(){
                //For each post process layer, find the AO effect and save it to an array
                return Resources.FindObjectsOfTypeAll<UnityEngine.Camera>().Select(x => x.GetComponent<AmplifyOcclusionEffect>()).ToArray();
            }

            private static void ToggleBlurs(IChangeable c){
                //For each motion blur effect, invert the enabled value
                MotionBlur[] motionBlurs = GetBlurs();
                foreach (MotionBlur motionBlur in motionBlurs)
                {
                    motionBlur.enabled.value = Settings.ReadValue(BlurPath, false);
                }
                
            }

            
            private static void ToggleAO(IChangeable c){
                //Find all cameras in the unity scene
                AmplifyOcclusionEffect[] AOs = GetAOs();
                //For each camera, find the AmplifyOcclusionEffect
                foreach (var AO in AOs)
                {
                    AO.enabled = Settings.ReadValue(AOPath, false);
                }

            }

            private static void ChangeBlurQuality(IChangeable c){
                //Find all motion blur effects
                MotionBlur[] motionBlurs = GetBlurs();
                //For each motion blur effect, set the quality to the value of the slider
                foreach (MotionBlur motionBlur in motionBlurs)
                {
                    motionBlur.sampleCount.value = Settings.ReadValue(BlurQualityPath, 0);
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
                var BlurQualityText = Builder.HorizontalElementWithLabel<IntTextEditorParser>("Motion Blur Quality", 0.7f, () => Builder.IntegerField(0, 100, 1, true));
                var BlurQualitySlider = Builder.Slider<int>(Builder.Style.MinHeight, 10, 0, 100, true);
                var AOCheckBox = Builder.Checkbox("Enable Ambient Occlusion", false, true, 4f);                
                
                //Add a settingsync for the blur quality and listen for changes
                var BlurQualitySync = BlurQualityText.ParsedValue.SyncWithSetting(BlurQualityPath, SettingSync.LocalChange.UpdateSetting);
                BlurQualitySlider.Value.SyncWithSetting(BlurQualityPath, SettingSync.LocalChange.UpdateSetting);

                BlurQualitySync.Changed += ChangeBlurQuality;


                //Add a settingsync for both checkboxes and subsequently set up listeners to see if they've changed.
                var BlurSync = BlurCheckBox.State.SyncWithSetting(BlurPath, SettingSync.LocalChange.UpdateSetting);
                var AOSync = AOCheckBox.State.SyncWithSetting(AOPath, SettingSync.LocalChange.UpdateSetting);

                BlurSync.Changed += ToggleBlurs;
                AOSync.Changed += ToggleAO;
            }
        }

    }
}
