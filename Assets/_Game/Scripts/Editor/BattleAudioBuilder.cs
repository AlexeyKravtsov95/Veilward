using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using Veilward.Presentation;

namespace Veilward.Editor
{
    public static partial class BattleSceneBuilder
    {
        internal const string AudioMixerPath = "Assets/_Game/Audio/Veilward.mixer";
        internal const string AudioCatalogPath = "Assets/_Game/Audio/VeilwardAudioCatalog.asset";

        [MenuItem("Veilward/Audio/Reimport Audio Catalog")]
        public static void ReimportAudioCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<VeilwardAudioCatalog>(AudioCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<VeilwardAudioCatalog>();
                AssetDatabase.CreateAsset(catalog, AudioCatalogPath);
            }
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AudioMixerPath);
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("music").objectReferenceValue = FindMixerGroup(mixer, "Master/Music");
            serialized.FindProperty("sfx").objectReferenceValue = FindMixerGroup(mixer, "Master/SFX");
            serialized.FindProperty("menuMusic").objectReferenceValue = ImportAudio("MenuMusic", true);
            serialized.FindProperty("battleMusic").objectReferenceValue = ImportAudio("BattleMusic", true);
            var clips = serialized.FindProperty("clips");
            var variations = serialized.FindProperty("variations");
            clips.arraySize = (int)AudioCue.Count;
            variations.arraySize = (int)AudioCue.SteamBurst;
            for (var i = 0; i < clips.arraySize; i++)
            {
                var name = ((AudioCue)i).ToString();
                var hasVariation = i < variations.arraySize;
                clips.GetArrayElementAtIndex(i).objectReferenceValue = ImportAudio(hasVariation ? name + "1" : name, false);
                if (hasVariation) variations.GetArrayElementAtIndex(i).objectReferenceValue = ImportAudio(name + "2", false);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        private static AudioMixerGroup FindMixerGroup(AudioMixer mixer, string path)
        {
            var groups = mixer != null ? mixer.FindMatchingGroups(path) : null;
            if (groups == null || groups.Length != 1)
                throw new InvalidOperationException("Expected one AudioMixer group: " + path);
            return groups[0];
        }

        private static AudioClip ImportAudio(string name, bool music)
        {
            var path = $"Assets/_Game/Audio/Clips/{name}.wav";
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
                throw new InvalidOperationException("Missing AudioClip: " + path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            settings.sampleRateSetting = music ? AudioSampleRateSetting.PreserveSampleRate : AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = music ? 48000u : 32000u;
            settings.quality = music ? .78f : 1f;
            settings.preloadAudioData = !music;
            var serialized = new SerializedObject(importer);
            var normalize = serialized.FindProperty("m_Normalize");
            var needsImport = !settings.Equals(importer.defaultSampleSettings) || importer.forceToMono != !music;
            if (normalize != null && normalize.boolValue)
            {
                normalize.boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                needsImport = true;
            }
            if (needsImport)
            {
                importer.forceToMono = !music;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static VeilwardAudio AddAudioOwner(GameObject host, bool battle)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<VeilwardAudioCatalog>(AudioCatalogPath);
            if (catalog == null) throw new InvalidOperationException("Reimport the Veilward Audio Catalog first.");
            var owner = host.AddComponent<VeilwardAudio>();
            var serialized = new SerializedObject(owner);
            serialized.FindProperty("catalog").objectReferenceValue = catalog;
            serialized.FindProperty("battleMusic").boolValue = battle;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return owner;
        }

        private static AudioSettingsPanel CreateAudioPanel(RectTransform canvas, Button openButton, CanvasGroup actions,
            out RectTransform safe)
        {
            var overlay = Modal(canvas, "Audio Settings Overlay", out safe);
            var dialog = Card("Audio Dialog", safe, Center, 0, 0, 480, 322);
            Label("Audio Title", dialog, Top, 0, -9, 440, 38, "Audio", 27, Gilt, TextAnchor.MiddleCenter, true);
            var (master, masterValue) = AudioSlider(dialog, "Master", -51);
            var (music, musicValue) = AudioSlider(dialog, "Music", -115);
            var (sfx, sfxValue) = AudioSlider(dialog, "SFX", -179);
            var mute = Action("Mute Button", dialog, "Mute: Off", Vector2.zero, 14, 12, 218, Raised);
            var close = Action("Close Audio Button", dialog, "Done", BottomRight, -14, 12, 218, Jade);
            var panel = canvas.gameObject.AddComponent<AudioSettingsPanel>();
            Bind(panel, ("overlay", overlay.gameObject), ("underlyingActions", actions), ("openButton", openButton),
                ("closeButton", close), ("muteButton", mute), ("master", master), ("music", music), ("sfx", sfx),
                ("masterValue", masterValue), ("musicValue", musicValue), ("sfxValue", sfxValue),
                ("muteLabel", mute.GetComponentInChildren<Text>()));
            return panel;
        }

        private static (Slider, Text) AudioSlider(RectTransform dialog, string title, float y)
        {
            var row = Image(title + " Volume", dialog, Top, 0, y, 452, 58, Raised, true);
            Label(title + " Label", row.rectTransform, Left, 14, 0, 82, 46, title, 16, Stone, bold: true);
            var value = Label(title + " Value", row.rectTransform, Right, -12, 0, 56, 46, "", 16, Gilt, TextAnchor.MiddleRight);
            Image("Track", row.rectTransform, Center, 13, 0, 258, 5, Ink, false);
            var handleRange = Rect("Handle Area", row.rectTransform, Center, 13, 0, 248, 26);
            var handle = Image("Handle", handleRange, Center, 0, 0, 18, 26, Gilt, false);
            var slider = row.gameObject.AddComponent<Slider>();
            slider.targetGraphic = row;
            slider.handleRect = handle.rectTransform;
            slider.direction = Slider.Direction.LeftToRight;
            return (slider, value);
        }
    }
}
