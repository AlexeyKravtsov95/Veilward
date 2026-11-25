using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using Veilward.Presentation;
using Veilward.Editor;

namespace Veilward.Tests.EditMode
{
    public sealed class AudioCatalogTests
    {
        private const string CatalogPath = "Assets/_Game/Audio/VeilwardAudioCatalog.asset";

        [Test]
        public void Audio101_InvalidLevelsClampAndMutePreservesValues()
        {
            var invalid = new AudioPreferences(float.NaN, float.PositiveInfinity, float.NegativeInfinity, false);
            Assert.That(invalid.Master, Is.EqualTo(1f));
            Assert.That(invalid.Music, Is.EqualTo(.65f));
            Assert.That(invalid.Sfx, Is.EqualTo(.8f));
            var clamped = new AudioPreferences(2f, -.02f, .43f, true);
            Assert.That(clamped.Master, Is.EqualTo(1f));
            Assert.That(clamped.Music, Is.Zero);
            Assert.That(clamped.Sfx, Is.EqualTo(.43f));
            Assert.That(clamped.Muted, Is.True);
        }

        [TestCase(0f, -80f)]
        [TestCase(.00001f, -80f)]
        [TestCase(.1f, -20f)]
        [TestCase(1f, 0f)]
        public void Audio101_ZeroAndFiniteLinearLevelsConvertToValidDecibels(float linear, float db)
        {
            Assert.That(AudioPreferences.ToDecibels(linear), Is.EqualTo(db).Within(.001f));
        }

        [Test]
        public void Audio101_CatalogHasAuthenticRoutingAndDistinctImportedClips()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<VeilwardAudioCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            var mixer = catalog.Music.audioMixer;
            Assert.That(catalog.Sfx.audioMixer, Is.SameAs(mixer));
            Assert.That(mixer.FindMatchingGroups("Master/Music").Single(), Is.SameAs(catalog.Music));
            Assert.That(mixer.FindMatchingGroups("Master/SFX").Single(), Is.SameAs(catalog.Sfx));
            foreach (var parameter in new[] { VeilwardAudio.MasterParameter, VeilwardAudio.MusicParameter, VeilwardAudio.SfxParameter })
                Assert.That(mixer.GetFloat(parameter, out var db) && float.IsFinite(db), Is.True, parameter);

            var clips = new System.Collections.Generic.HashSet<AudioClip>();
            for (var index = 0; index < (int)AudioCue.Count; index++)
            {
                var cue = (AudioCue)index;
                AssertClip(catalog.Clip(cue, false), false);
                Assert.That(clips.Add(catalog.Clip(cue, false)), Is.True, cue.ToString());
                if (cue < AudioCue.SteamBurst)
                {
                    AssertClip(catalog.Clip(cue, true), false);
                    Assert.That(clips.Add(catalog.Clip(cue, true)), Is.True, cue.ToString());
                }
            }
            Assert.That(clips.Count, Is.EqualTo(48));
            var menu = catalog.MusicClip(false);
            var battle = catalog.MusicClip(true);
            AssertClip(menu, true);
            AssertClip(battle, true);
            Assert.That(menu, Is.Not.EqualTo(battle));
        }

        [TestCase("Battle", "catalog")]
        [TestCase("Battle", "audioOwner")]
        [TestCase("MainMenu", "catalog")]
        [TestCase("MainMenu", "audioOwner")]
        public void Audio104_SavedSceneValidatorRejectsBrokenAudioReferences(string scene, string property)
        {
            var path = $"Assets/_Game/Scenes/{scene}.unity";
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            try
            {
                EditorSceneManager.OpenScene(path);
                var battle = scene == "Battle";
                Component target = property == "catalog" ? Object.FindFirstObjectByType<VeilwardAudio>()
                    : battle ? Object.FindFirstObjectByType<BattleSceneController>()
                    : Object.FindFirstObjectByType<MainMenuController>();
                var serialized = new SerializedObject(target);
                serialized.FindProperty(property).objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var errors = battle ? BattleAssetValidator.ValidateSavedBattle() : BattleAssetValidator.ValidateSavedMainMenu();
                Assert.That(errors, Has.Some.Contains(property));
            }
            finally { EditorSceneManager.OpenScene(string.IsNullOrEmpty(previous) ? path : previous); }
        }

        private static void AssertClip(AudioClip clip, bool music)
        {
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.channels, Is.EqualTo(music ? 2 : 1), clip.name);
            Assert.That(clip.length, Is.InRange(music ? 20f : .08f, music ? 28f : 2.5f), clip.name);
            var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
            Assert.That(importer.forceToMono, Is.EqualTo(!music), clip.name);
            var settings = importer.defaultSampleSettings;
            Assert.That(settings.loadType, Is.EqualTo(music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad), clip.name);
            Assert.That(settings.compressionFormat, Is.EqualTo(music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM), clip.name);
        }
    }
}
