#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace YourPark.PlayerData
{
    // Development-build harness only. Uses exclusively synthetic data in a dedicated temporary root.
    internal static class PlayerDataProcessSmoke
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--playerdata-smoke");
            if (index < 0) return;
            try
            {
                Require(index + 2 < args.Length && PlayerDataValidation.ValidId(args[index + 2]), "A synthetic test run ID is required.");
                string phase = args[index + 1];
                string root = Path.Combine(Path.GetTempPath(), "YourPark-process-smoke", args[index + 2]);
                var store = new LocalPlayerDataStore(root);
                string control = Path.Combine(root, "control.json"); string story = Path.Combine(root, "StoryProgress.json");
                if (phase == "save")
                {
                    Require(!Directory.Exists(root), "A new run ID is required; existing data is never overwritten.");
                    Directory.CreateDirectory(root); File.WriteAllText(story, "{\"day\":7}");
                    var clip = MockPoseTracker.MakeClip();
                    var motion = store.Save(SampleMetadata.Create(SampleKind.Motion, CaptureSource.Mock, 10, clip.frames.Length), PlayerDataValidation.EncodePose(clip));
                    Require(motion.Success, motion.Message);
                    var voice = WavCodec.Encode(new VoiceSample(new float[800], 1, 8000));
                    Require(voice.Success, voice.Message);
                    Require(store.Save(SampleMetadata.Create(SampleKind.Voice, CaptureSource.Mock, .1, 800), voice.Value).Success, "Voice save failed.");
                    var image = new Texture2D(2, 2); byte[] png = image.EncodeToPNG(); UnityEngine.Object.Destroy(image);
                    Require(store.Save(SampleMetadata.Create(SampleKind.Image, CaptureSource.Mock), png).Success, "PNG save failed.");
                    File.WriteAllText(control, motion.Value.id);
                }
                else if (phase == "read-delete")
                {
                    string id = File.ReadAllText(control);
                    var manifest = store.ReadManifest(); Require(manifest.Success && manifest.Value.samples.Length == 3, "Manifest restart read failed.");
                    foreach (var entry in manifest.Value.samples) Require(store.Load(entry.id).Success, "Payload restart read failed.");
                    using (var session = new PlayerDataSession(store))
                    {
                        var loaded = session.Load(id); Require(loaded.Success, loaded.Message);
                        byte[] snapshot = (byte[])loaded.Value.Bytes.Clone();
                        Require(session.DeleteDiskData().Success, "Delete failed.");
                        Require(!Directory.Exists(store.RootPath), "Disk data remains.");
                        Require(session.Load(id).Value.Bytes.SequenceEqual(snapshot), "Loaded RAM changed after delete.");
                        Require(PlayerDataValidation.Pose(PlayerDataValidation.DecodePose(session.Load(id).Value.Bytes)), "RAM motion replay payload failed.");
                        Require(File.ReadAllText(story) == "{\"day\":7}", "StoryProgress changed.");
                    }
                }
                else if (phase == "check-deleted")
                {
                    using (var session = new PlayerDataSession(store))
                        Require(session.Load(File.ReadAllText(control)).Error == DataError.Missing, "Deleted sample reappeared after restart.");
                    Require(File.ReadAllText(story) == "{\"day\":7}", "StoryProgress changed after restart.");
                }
                else throw new InvalidOperationException("Unknown smoke test phase.");
                Debug.Log("PLAYERDATA_PROCESS_SMOKE_PASS " + phase); Application.Quit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("PLAYERDATA_PROCESS_SMOKE_FAIL " + error.GetType().Name + ": " + error.Message);
                Application.Quit(1);
            }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif
