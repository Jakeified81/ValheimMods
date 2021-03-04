﻿using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace CustomAudio
{
    [BepInPlugin("aedenthorn.CustomAudio", "Custom Audio", "0.5.2")]
    public class BepInExPlugin: BaseUnityPlugin
    {
        private static readonly bool isDebug = true;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> dumpInfo;
        public static Dictionary<string, AudioClip> customMusic = new Dictionary<string, AudioClip>();
        public static Dictionary<string, Dictionary<string, AudioClip>> customMusicList = new Dictionary<string, Dictionary<string,AudioClip>>();
        public static Dictionary<string, AudioClip> customAmbient = new Dictionary<string, AudioClip>();
        public static Dictionary<string, Dictionary<string, AudioClip>> customAmbientList = new Dictionary<string, Dictionary<string, AudioClip>>();
        public static Dictionary<string, AudioClip> customSFX = new Dictionary<string, AudioClip>();
        public static Dictionary<string, Dictionary<string, AudioClip>> customSFXList = new Dictionary<string, Dictionary<string, AudioClip>>();
        public static ConfigEntry<int> nexusID;

        private static string lastMusicName = "";
        private static string[] audioFiles;
        private static BepInExPlugin context;

        public static void Dbgl(string str = "", bool pref = true)
        {
            if (isDebug)
                Debug.Log((pref ? typeof(BepInExPlugin).Namespace + " " : "") + str);
        }
        private void Awake()
        {
            context = this;
            modEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            dumpInfo = Config.Bind<bool>("General", "DumpInfo", true, "Dump audio info to the console");
            nexusID = Config.Bind<int>("General", "NexusID", 90, "Nexus mod ID for updates");

            if (!modEnabled.Value)
                return;
            PreloadAudioClips();

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }
        private static void PreloadAudioClips()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "CustomAudio");
            if (Directory.Exists(path))
            {
                customMusic.Clear();
                customAmbient.Clear();
                customSFX.Clear();
                customMusicList.Clear();
                customAmbientList.Clear();
                customSFXList.Clear();

                if (Directory.Exists(Path.Combine(path, "Music")))
                {
                    CollectAudioFiles(Path.Combine(path, "Music"), customMusic, customMusicList);
                }
                else 
                {
                    Directory.CreateDirectory(Path.Combine(path, "Music"));
                }
                if (Directory.Exists(Path.Combine(path, "SFX")))
                {
                    CollectAudioFiles(Path.Combine(path, "SFX"), customSFX, customSFXList);
                }
                else 
                {
                    Directory.CreateDirectory(Path.Combine(path, "SFX"));
                }
                if (Directory.Exists(Path.Combine(path, "Ambient")))
                {
                    CollectAudioFiles(Path.Combine(path, "Ambient"), customAmbient, customAmbientList);
                }
                else 
                {
                    Directory.CreateDirectory(Path.Combine(path, "Ambient"));
                }
            }
            else
            {
                Dbgl($"Directory {path} does not exist! Creating.");
                Directory.CreateDirectory(path);
                Directory.CreateDirectory(Path.Combine(path, "Ambient"));
                Directory.CreateDirectory(Path.Combine(path, "Music"));
                Directory.CreateDirectory(Path.Combine(path, "SFX"));
            }
        }

        private static void CollectAudioFiles(string path, Dictionary<string, AudioClip> customDict, Dictionary<string, Dictionary<string, AudioClip>> customDictDict)
        {
            Dbgl($"checking folder {Path.GetFileName(path)}");
            audioFiles = Directory.GetFiles(path);
            foreach (string file in audioFiles)
            {
                Dbgl($"\tchecking single file {Path.GetFileName(file)}");

                if (Path.GetExtension(file).ToLower().Equals(".ogg"))
                    context.StartCoroutine(PreloadClipCoroutine(file, AudioType.OGGVORBIS, customDict));
                else if(Path.GetExtension(file).ToLower().Equals(".wav"))
                    context.StartCoroutine(PreloadClipCoroutine(file, AudioType.WAV, customDict));
            }
            foreach(string folder in Directory.GetDirectories(path))
            {
                Dbgl($"\tchecking folder {Path.GetFileName(folder)}");
                string folderName = Path.GetFileName(folder);
                audioFiles = Directory.GetFiles(folder);
                customDictDict[folderName] = new Dictionary<string, AudioClip>();
                foreach (string file in audioFiles)
                {
                    Dbgl($"\tchecking file {Path.GetFileName(file)}");
                    if (Path.GetExtension(file).ToLower().Equals(".ogg"))
                        context.StartCoroutine(PreloadClipCoroutine(file, AudioType.OGGVORBIS, customDictDict[folderName]));
                    else if (Path.GetExtension(file).ToLower().Equals(".wav"))
                        context.StartCoroutine(PreloadClipCoroutine(file, AudioType.WAV, customDictDict[folderName]));
                }
            }
        }

        public static IEnumerator PreloadClipCoroutine(string path, AudioType audioType, Dictionary<string, AudioClip> whichDict)
        {
            Dbgl($"\t\tpath: {path}");
            
            path = "file:///" + path.Replace("\\", "/");


            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(path, audioType))
            {
                www.SendWebRequest();
                yield return null;
                //Dbgl($"checking downloaded {filename}");
                if (www != null)
                {
                    //Dbgl("www not null. errors: " + www.error);
                    DownloadHandlerAudioClip dac = ((DownloadHandlerAudioClip)www.downloadHandler);
                    if (dac != null)
                    {
                        AudioClip ac = dac.audioClip;
                        if (ac != null)
                        {
                            string name = Path.GetFileNameWithoutExtension(path);
                            ac.name = name;
                            if (!whichDict.ContainsKey(name))
                                whichDict[name] = ac;
                            Dbgl($"Added audio clip {name} to dict");
                        }
                        else
                        {
                            Dbgl("audio clip is null. data: " + dac.text);
                        }
                    }
                    else
                    {
                        Dbgl("DownloadHandler is null. bytes downloaded: " + www.downloadedBytes);
                    }
                }
                else
                {
                    Dbgl("www is null " + www.url);
                }
            }
        }

        [HarmonyPatch(typeof(ZSFX), "Awake")]
        static class ZSFX_Awake_Patch
        {
            static void Postfix(ZSFX __instance)
            {
                string name = GetZSFXName(__instance);
                if (dumpInfo.Value)
                    Dbgl($"Checking SFX: {name}");
                if (customSFXList.ContainsKey(name))
                {
                    Dbgl($"replacing SFX list by name: {name}");
                    __instance.m_audioClips = customSFXList[name].Values.ToArray();
                    return;
                }
                for (int i = 0; i < __instance.m_audioClips.Length; i++)
                {
                    if (dumpInfo.Value)
                        Dbgl($"checking SFX: {name}, clip: {__instance.m_audioClips[i].name}");
                    if (customSFX.ContainsKey(__instance.m_audioClips[i].name))
                    {
                        Dbgl($"replacing SFX: {name}, clip: {__instance.m_audioClips[i].name}");
                        __instance.m_audioClips[i] = customSFX[__instance.m_audioClips[i].name];
                    }
                }
            }
        }
        [HarmonyPatch(typeof(MusicMan), "Awake")]
        static class MusicMan_Awake_Patch
        {
            static void Postfix(MusicMan __instance, List<MusicMan.NamedMusic> ___m_music)
            {
                List<string> dump = new List<string>();

                for (int i = 0; i < ___m_music.Count; i++)
                {
                    dump.Add($"Music list name: {___m_music[i].m_name}");
                    for (int j = 0; j < ___m_music[i].m_clips.Length; j++)
                    {
                        if (!___m_music[i].m_clips[j])
                            continue;
                        dump.Add($"\ttrack name: {___m_music[i].m_clips[j].name}");
                        //Dbgl($"checking music: { ___m_music[i].m_name}, clip: {___m_music[i].m_clips[j].name}");
                        if (customMusic.ContainsKey(___m_music[i].m_clips[j].name))
                        {
                            Dbgl($"replacing music: { ___m_music[i].m_name}, clip: {___m_music[i].m_clips[j].name}");
                            ___m_music[i].m_clips[j] = customMusic[___m_music[i].m_clips[j].name];
                        }
                    }
                    if (customMusicList.ContainsKey(___m_music[i].m_name))
                    {
                        Dbgl($"replacing music list by name: {___m_music[i].m_name}");
                        ___m_music[i].m_clips = customMusicList[___m_music[i].m_name].Values.ToArray();
                    }
                }
                if (dumpInfo.Value)
                    Dbgl(string.Join("\n", dump));
            }
        }
        [HarmonyPatch(typeof(AudioMan), "Awake")] 
        static class AudioMan_Awake_Patch
        {
            static void Postfix(MusicMan __instance, List<AudioMan.BiomeAmbients> ___m_randomAmbients)
            {
                List<string> dump = new List<string>();

                for (int i = 0; i <___m_randomAmbients.Count; i++)
                {
                    dump.Add($"Ambient list name: {___m_randomAmbients[i].m_name}");

                    dump.Add($"\tAmbient tracks: (use {___m_randomAmbients[i].m_name})");
                    for (int j = 0; j < ___m_randomAmbients[i].m_randomAmbientClips.Count; j++)
                    {
                        dump.Add($"\t\ttrack name: {___m_randomAmbients[i].m_randomAmbientClips[j].name}");

                        //Dbgl($"checking ambient: { ___m_randomAmbients[i].m_name}, clip: {___m_randomAmbients[i].m_randomAmbientClips[j].name}");
                        if (customAmbient.ContainsKey(___m_randomAmbients[i].m_randomAmbientClips[j].name))
                        {
                            Dbgl($"replacing ambient: { ___m_randomAmbients[i].m_name}, clip: {___m_randomAmbients[i].m_randomAmbientClips[j].name}");
                            ___m_randomAmbients[i].m_randomAmbientClips[j] = customMusic[___m_randomAmbients[i].m_randomAmbientClips[j].name];
                        }
                    }
                    dump.Add($"\tAmbient day tracks: (use {___m_randomAmbients[i].m_name}_day)");
                    for (int j = 0; j < ___m_randomAmbients[i].m_randomAmbientClipsDay.Count; j++)
                    {
                        dump.Add($"\t\ttrack name: {___m_randomAmbients[i].m_randomAmbientClipsDay[j].name}");

                        //Dbgl($"checking ambient day: { ___m_randomAmbients[i].m_name}, clip: {___m_randomAmbients[i].m_randomAmbientClipsDay[j].name}");
                        if (customAmbient.ContainsKey(___m_randomAmbients[i].m_randomAmbientClipsDay[j].name))
                        {
                            Dbgl($"replacing ambient day: { ___m_randomAmbients[i].m_name}, clip: {___m_randomAmbients[i].m_randomAmbientClipsDay[j].name}");
                            ___m_randomAmbients[i].m_randomAmbientClipsDay[j] = customMusic[___m_randomAmbients[i].m_randomAmbientClipsDay[j].name];
                        }
                    }
                    dump.Add($"\tAmbient night tracks: (use {___m_randomAmbients[i].m_name}_night)");
                    for (int j = 0; j < ___m_randomAmbients[i].m_randomAmbientClipsNight.Count; j++)
                    {
                        dump.Add($"\t\ttrack name: {___m_randomAmbients[i].m_randomAmbientClipsNight[j].name}");

                        //Dbgl($"checking ambient night: { ___m_randomAmbients[i].m_name}, clip: {___m_randomAmbients[i].m_randomAmbientClipsNight[j].name}");
                        if (customAmbient.ContainsKey(___m_randomAmbients[i].m_randomAmbientClipsNight[j].name))
                        {
                            Dbgl($"replacing ambient night: { ___m_randomAmbients[i].m_name}, clip: {___m_randomAmbients[i].m_randomAmbientClipsNight[j].name}");
                            ___m_randomAmbients[i].m_randomAmbientClipsNight[j] = customMusic[___m_randomAmbients[i].m_randomAmbientClipsNight[j].name];
                        }
                    }
                    if (customAmbientList.ContainsKey(___m_randomAmbients[i].m_name + "_day"))
                    {
                        Dbgl($"replacing ambient day list by name: {___m_randomAmbients[i].m_name}");
                        ___m_randomAmbients[i].m_randomAmbientClipsDay = new List<AudioClip>(customAmbientList[___m_randomAmbients[i].m_name].Values.ToList());
                    }
                    else if (customAmbientList.ContainsKey(___m_randomAmbients[i].m_name + "_night"))
                    {
                        Dbgl($"replacing ambient night list by name: {___m_randomAmbients[i].m_name}");
                        ___m_randomAmbients[i].m_randomAmbientClipsNight = new List<AudioClip>(customAmbientList[___m_randomAmbients[i].m_name].Values.ToList());
                    }
                    else if (customAmbientList.ContainsKey(___m_randomAmbients[i].m_name))
                    {
                        Dbgl($"replacing ambient list by name: {___m_randomAmbients[i].m_name}");
                        ___m_randomAmbients[i].m_randomAmbientClips = new List<AudioClip>(customAmbientList[___m_randomAmbients[i].m_name].Values.ToList());
                    }
                }
                if (dumpInfo.Value)
                    Dbgl(string.Join("\n", dump));
            }

        }
        public static string GetZSFXName(ZSFX zfx)
        {
            string name = zfx.name;
            char[] anyOf = new char[]
            {
            '(',
            ' '
            };
            int num = name.IndexOfAny(anyOf);
            if (num != -1)
            {
                return name.Remove(num);
            }
            return name;
        }

        [HarmonyPatch(typeof(MusicMan), "UpdateMusic")]
        static class UpdateMusic_Patch
        {
            static void Prefix(ref MusicMan.NamedMusic ___m_currentMusic, ref MusicMan.NamedMusic ___m_queuedMusic, AudioSource ___m_musicSource)
            {
                if(___m_musicSource?.clip != null &&  lastMusicName != ___m_musicSource.clip.name)
                {
                    Dbgl($"Switching music from {lastMusicName} to {___m_musicSource.clip.name}");
                    lastMusicName = ___m_musicSource.clip.name;
                }
                if (___m_currentMusic != null && !___m_musicSource.isPlaying && PlayerPrefs.GetInt("ContinousMusic", 1) == 1)
                {
                    //Dbgl($"done {___m_musicSource.clip?.name}, setting queued to current {___m_currentMusic.m_clips.Length}");
                    ___m_queuedMusic = ___m_currentMusic;
                }
                if (___m_queuedMusic != null)
                {
                    //Dbgl($"queued, setting loop to false {___m_queuedMusic.m_clips.Length}");
                    ___m_queuedMusic.m_loop = false;
                }
            }
        }


        [HarmonyPatch(typeof(Console), "InputText")]
        static class InputText_Patch
        {
            static bool Prefix(Console __instance)
            {
                if (!modEnabled.Value)
                    return true;
                string text = __instance.m_input.text;
                if (text.ToLower().Equals("customaudio reset"))
                {
                    context.Config.Reload();
                    context.Config.Save();
                    PreloadAudioClips();
                    Traverse.Create(__instance).Method("AddString", new object[] { text }).GetValue();
                    Traverse.Create(__instance).Method("AddString", new object[] { "Reloaded custom audio mod" }).GetValue();
                    return false;
                }
                return true;
            }
        }
    }
}
