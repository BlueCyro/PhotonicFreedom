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
using System.Reflection;
using PhotonicFreedom;
using System.Threading.Tasks;

namespace NeosModloaderMod
{
    public class ModClass : NeosMod
    {
        public override string Author => "Cyro";
        public override string Name => "Photonic Freedom";
        public override string Version => "1.2.1";

        public static string BaseSettingPath => "Settings.PostProcessing.";
        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony("net.cyro.PhotonicFreedom");
            harmony.PatchAll();

        }

        [HarmonyPatch(typeof(Userspace), "FinishCloudSettingsLoad")]
        class SettingComponentPatcher
        {
            
            [HarmonyPostfix]
            public static void LoadAuxSettings()
            {
                UniLog.Log("Loading Aux Settings");
                SettingsHelper.InstantiateSettings(false, false, true);
            }

        }

        [HarmonyPatch(typeof(SettingsDialog), "OnAttach")]
        class SettingsPatcher
        {
            static void ChangedCallback(FieldInfo f, object val, Type t)
            {
                SettingsHelper.SetValueToAllFields(f, GameObject.FindObjectsOfType(t), val);
                SettingsHelper.UpdateSettings(f.DeclaringType.Name, f.Name, val.ToString());
            }

            static void Postfix(SettingsDialog __instance)
            {
                
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

                Builder.Text("<b>Post Processing Settings</b>", true, null, true, null);
                
                var settings = SettingsHelper.RetrieveClassSettings();

                foreach (var hold in settings)
                {
                    Type type = Type.GetType(hold.type);

                    Builder.Text("<b>" + type.Name + " settings</b>", true, null, true, null);

                    foreach(KeyValuePair<string, string> p in hold.fields)
                    {
                        FieldInfo field = type.GetField(p.Key);

                        Type SanitizedType = SettingsHelper.FieldSanitizer(field.FieldType);
                        object val = null;
                        
                        try
                        {
                            val = Convert.ToBoolean(p.Value);
                        } 
                        catch
                        {
                            val = Convert.ChangeType(p.Value, SanitizedType);
                        }

                        if(val.GetType() == typeof(int))
                        {
                            var parser = Builder.HorizontalElementWithLabel<IntTextEditorParser>(field.Name, 0.7f, () => Builder.IntegerField(int.MinValue, int.MaxValue, 1, true));
                            parser.ParsedValue.Value = (int)val;

                            parser.ParsedValue.Changed += (IChangeable c) => ChangedCallback(field, parser.ParsedValue.Value, type);
                        }
                        if(val.GetType() == typeof(float))
                        {
                            var parser = Builder.HorizontalElementWithLabel<FloatTextEditorParser>(field.Name, 0.7f, () => Builder.FloatField(float.MinValue, float.MaxValue, 2, null, true));
                            parser.ParsedValue.Value = (float)val;

                            parser.ParsedValue.Changed += (IChangeable c) => ChangedCallback(field, parser.ParsedValue.Value, type);
                        }
                        if(val.GetType() == typeof(bool))
                        {
                            var check = Builder.Checkbox(field.Name, (bool)val, true);

                            check.Changed += (IChangeable c) => ChangedCallback(field, check.IsChecked, type);
                        }
                    }
                }
                
            }
        }
    }
}
