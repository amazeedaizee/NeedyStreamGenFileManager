using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ngov3;
using UnityEngine.CrashReportHandler;

namespace StreamGenFileManager
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Windose.exe")]
    public class Initializer : BaseUnityPlugin
    {
        public const string pluginGuid = "needy.girl.stream_gen_file_manager";
        public const string pluginName = "Stream Generator FIle Manager";
        public const string pluginVersion = "1.0.0.0";

        public static ManualLogSource log;
        public static PluginInfo PInfo { get; private set; }
        public void Awake()
        {
            log = Logger;
            PInfo = Info;

            Harmony harmony = new Harmony(pluginGuid);
            harmony.PatchAll();

            hideFlags = UnityEngine.HideFlags.HideAndDontSave;
        }

        public void Start()
        {



        }

        public void OnApplicationQuit()
        {
            // game crash workaround for the stream generator (weird)
            if (SingletonMonoBehaviour<WindowManager>.Instance.isAppOpen(AppType.ManualHaishin))
                CrashReportHandler.enableCaptureExceptions = false;
        }
    }

}


