using Cysharp.Threading.Tasks;
using HarmonyLib;
using Newtonsoft.Json;
using ngov3;
using SFB;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

namespace StreamGenFileManager
{
    [HarmonyPatch]
    internal class Patcher
    {
        internal static GameObject background = null;
        internal static StreamSettings currentStream = null;
        internal static Assembly assembly = null;
        internal static AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Initializer.PInfo.Location), "buttons.bundle"));
        internal static GameObject saveBase = bundle.LoadAsset<GameObject>("Save");
        internal static GameObject loadBase = bundle.LoadAsset<GameObject>("Load");

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Live_gen), "Awake")]
        internal static void InitButtons(Live_gen __instance)
        {
            currentStream = new StreamSettings();
            currentStream.StartingAnimation = "stream_cho_akaruku";
            currentStream.StartingBackground = StreamBackground.Default;
            currentStream.StartingMusic = NGO.SoundType.BGM_mainloop_normal;
            currentStream.StartingEffect = EffectType.Kenjo;
            currentStream.HasChair = true;
            Initializer.log.LogInfo(saveBase == null);
            Initializer.log.LogInfo(loadBase == null);
            background = __instance.transform.GetChild(0).GetChild(3).GetChild(0).gameObject;
            Initializer.log.LogInfo(background.name);
            var save = Object.Instantiate(saveBase, __instance.transform);
            save.GetComponent<RectTransform>().localPosition = new Vector3(170, -845, -5);
            var load = Object.Instantiate(loadBase, __instance.transform);
            load.GetComponent<RectTransform>().localPosition = new Vector3(58, -845, -5);
            load.GetComponent<Button>().OnClickAsObservable().Subscribe(async delegate
            {
                await LoadCustomStream(__instance, await LoadFile());
            }).AddTo(__instance.gameObject);
            save.GetComponent<Button>().OnClickAsObservable().Subscribe(delegate
            {
                SaveCustomStream(__instance);
            }).AddTo(__instance.gameObject);
            save.transform.SetAsLastSibling();
            load.transform.SetAsLastSibling();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Live_gen), nameof(Live_gen.ChotenAnimation))]

        static void SetStartingAnim(Live_gen __instance, ref string animationName)
        {
            if (__instance.nowLine == -1)
                animationName = currentStream.StartingAnimation;
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(Live_gen), nameof(Live_gen.closeAnimationKeyView))]
        static void GetStartingAnim(Live_gen __instance)
        {
            if (__instance.textInputs.Count > 0 && __instance.selectedTextInput.Index == 0)
            {
                currentStream.StartingAnimation = string.IsNullOrEmpty(__instance.textInputs[0].AnimationKey.Value) ? "stream_cho_akaruku" : __instance.textInputs[0].AnimationKey.Value;
                if (__instance.nowLine < 0)
                    __instance.ChotenAnimation(currentStream.StartingAnimation, false);
            }

        }

        internal async static UniTask<StreamSettings> LoadFile()
        {
            bool noFile = false;
            StreamSettings streamobj = null;
            var ex = new ExtensionFilter[] { new ExtensionFilter("JSON files", "json") };
            StandaloneFileBrowser.OpenFilePanelAsync("Open Stream", "", "json", false, (string[] file) =>
            {
                if (file.Length > 0 || string.IsNullOrWhiteSpace(file[0]))
                {
                    try
                    {
                        var json = File.ReadAllText(file[0]);
                        string newJson = json.Replace("CustomStreamMaker", "StreamGenFileManager");
                        var jsonSettings = new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.All };
                        streamobj = JsonConvert.DeserializeObject<StreamSettings>(newJson, jsonSettings);
                    }
                    catch { noFile = true; }
                }
                else
                {
                    noFile = true;
                }
            });
            await UniTask.WaitUntil(() => { return noFile || streamobj != null; });
            return streamobj;

        }

        internal static async UniTask LoadCustomStream(Live_gen instance, StreamSettings stream)
        {
            if (stream == null)
            {
                Initializer.log.LogInfo("Did not load file!");
                return;
            };
            var list = instance.textInputs;
            var kAngels = stream.PlayingList.FindAll(p => p.PlayingType == PlayingType.KAngelSays || p.PlayingType == PlayingType.KAngelCallout);
            instance.initializeInputFieldCount = kAngels.Count;
            if (!(instance.initializeInputFieldCount > 0))
                instance.initializeInputFieldCount = 1;
            instance.InitializeLiveInput();
            if (instance.initializeInputFieldCount != kAngels.Count)
            {
                currentStream.StartingAnimation = "stream_cho_akaruku";
                instance.rewind();
                background.GetComponent<Image>().sprite = ChangeStreamBackground(StreamBackground.Default);
                AudioManager.Instance.PlaySeByType(NGO.SoundType.SE_Error);
                return;
            };
            currentStream = stream;
            instance.rewind();
            instance._liveTitleLabel.text = stream.StringTitle;
            background.GetComponent<Image>().sprite = ChangeStreamBackground(stream.StartingBackground);
            await UniTask.WaitUntil(() =>
            {
                return instance.textInputs.Count == kAngels.Count;
            });
            AudioManager.Instance.PlaySeByType(NGO.SoundType.SE_command_execute);
            for (int i = 0; i < kAngels.Count; i++)
            {
                if (kAngels[i].PlayingType == PlayingType.KAngelSays || kAngels[i].PlayingType == PlayingType.KAngelCallout)
                {
                    var orig = (kAngels[i] as KAngelSays);
                    var step = instance.textInputs[i];
                    if (orig.customAnim != null)
                    {
                        step.AnimationKey.Value = "";
                    }
                    else
                    {
                        if (i == 0 && string.IsNullOrEmpty(orig.AnimName) && !string.IsNullOrEmpty(stream.StartingAnimation))
                        {
                            step.AnimationKey.Value = stream.StartingAnimation;
                        }
                        else step.AnimationKey.Value = !string.IsNullOrEmpty(orig.AnimName) ? orig.AnimName : "";
                    }
                    step.SetText(orig.Dialogue.Replace("\r\n", "_").Replace("\n", "_"));
                    step.SetSizeFromTextSize();
                }

            }

        }

        static Sprite ChangeStreamBackground(StreamBackground bg)
        {
            TenchanView view = SingletonMonoBehaviour<TenchanView>.Instance;
            Dictionary<StreamBackground, Sprite> spriteBg = new Dictionary<StreamBackground, Sprite>() {
                { StreamBackground.Default, view.background_no_shield },
                { StreamBackground.Silver, view.background_silver_shield },
                {StreamBackground.Gold, view.background_gold_shield },
                {StreamBackground.MileOne, view.background_kinen1 },
                {StreamBackground.MileTwo, view.background_kinen2 },
                {StreamBackground.MileThree, view.background_kinen3 },
                {StreamBackground.MileFour, view.background_kinen4 },
                {StreamBackground.MileFive, view.background_kinen5 },
                {StreamBackground.Guru, view._background_kyouso },
                {StreamBackground.Horror, view._background_horror },
                {StreamBackground.BigHouse, view._background_happy },
                {StreamBackground.Roof, view._background_sayonara1 }

            };
            if (bg == StreamBackground.Black || bg == StreamBackground.Void || bg == StreamBackground.None)
            {
                return view.background_no_shield;
            }
            return spriteBg[bg];
        }

        internal static void SaveCustomStream(Live_gen instance)
        {
            var ex = new ExtensionFilter[] { new ExtensionFilter("JSON files", "json") };
            var saveSettings = currentStream;
            saveSettings.StringTitle = instance._liveTitleLabel.text;
            saveSettings.StartingAnimation = string.IsNullOrEmpty(instance.textInputs[0].AnimationKey.Value) ? "stream_cho_akaruku" : instance.textInputs[0].AnimationKey.Value;
            saveSettings.PlayingList.Clear();
            for (int i = 0; i < instance.textInputs.Count; i++)
            {
                var kAngel = new KAngelSays(instance.textInputs[i].AnimationKey.Value, instance.textInputs[i]._input.text.Replace("_", "\n"));
                saveSettings.PlayingList.Add(kAngel);
            }

            var json = JsonConvert.SerializeObject(saveSettings, Formatting.Indented, new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.All }).Replace("StreamGenFileManager", "CustomStreamMaker");
            StandaloneFileBrowser.SaveFilePanelAsync("Save Stream", "", "", ex, (string file) =>
            {
                if (!string.IsNullOrEmpty(file))
                {
                    try
                    {
                        File.WriteAllText(file, json);
                        AudioManager.Instance.PlaySeByType(NGO.SoundType.SE_Tetehen);
                    }
                    catch
                    {
                        AudioManager.Instance.PlaySeByType(NGO.SoundType.SE_Error);
                    }
                }
            });
        }
    }
}
