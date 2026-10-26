using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace YourPark.PlayerData.Tests
{
    public sealed class PlayerDataStoreTests
    {
        private string directory;
        private LocalPlayerDataStore store;
        private sealed class MissingDevices : ICaptureDeviceInventory
        {
            public string[] Cameras => Array.Empty<string>();
            public string[] Microphones => Array.Empty<string>();
        }
        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "YourPark-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); store = new LocalPlayerDataStore(directory);
        }
        [TearDown] public void Teardown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        private SampleMetadata SaveMotion()
        {
            var clip = MockPoseTracker.MakeClip();
            var metadata = SampleMetadata.Create(SampleKind.Motion, CaptureSource.Mock, 10, clip.frames.Length);
            var result = store.Save(metadata, PlayerDataValidation.EncodePose(clip));
            Assert.That(result.Success, Is.True, result.Message); return result.Value;
        }
        [Test] public void StableIdsAreUnique()
        {
            var ids = Enumerable.Range(0, 10000).Select(_ => SampleMetadata.Create(SampleKind.Image, CaptureSource.Mock).id).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
        }
        [Test] public void ManifestAndPayloadRoundTripAcrossStoreInstances()
        {
            var saved = SaveMotion(); var restarted = new LocalPlayerDataStore(directory);
            var manifest = restarted.ReadManifest(); var loaded = restarted.Load(saved.id);
            Assert.That(manifest.Success && loaded.Success, Is.True);
            Assert.That(manifest.Value.samples.Single().id, Is.EqualTo(saved.id));
            Assert.That(loaded.Value.Metadata.createdUtc, Is.EqualTo(saved.createdUtc));
            Assert.That(loaded.Value.Metadata.source, Is.EqualTo(CaptureSource.Mock));
            Assert.That(PlayerDataValidation.DecodePose(loaded.Value.Bytes).frames.Length, Is.EqualTo(301));
        }
        [Test] public void DeletePreservesLoadedRamAndStoryButRestartCannotReuse()
        {
            string story = Path.Combine(directory, "StoryProgress.json"); File.WriteAllText(story, "{\"day\":7}");
            var saved = SaveMotion(); var notLoaded = SaveMotion();
            var session = new PlayerDataSession(store); var loaded = session.Load(saved.id);
            var expected = (byte[])loaded.Value.Bytes.Clone();
            Assert.That(session.DeleteDiskData().Success, Is.True);
            Assert.That(Directory.Exists(store.RootPath), Is.False);
            Assert.That(File.ReadAllText(story), Is.EqualTo("{\"day\":7}"));
            Assert.That(session.Load(saved.id).Value.Bytes, Is.EqualTo(expected));
            Assert.That(session.Load(notLoaded.id).Error, Is.EqualTo(DataError.Missing));
            var restarted = new PlayerDataSession(new LocalPlayerDataStore(directory));
            Assert.That(restarted.Load(saved.id).Error, Is.EqualTo(DataError.Missing));
            session.Dispose(); Assert.That(loaded.Value.Bytes.All(b => b == 0), Is.True);
            Assert.That(session.Load(saved.id).Error, Is.EqualTo(DataError.Unavailable)); restarted.Dispose();
        }
        [Test] public void DeleteDoesNotRecreateDirectoryAndIsIdempotent()
        {
            Assert.That(store.DeletePlayerData().Success, Is.True); SaveMotion();
            Assert.That(store.DeletePlayerData().Success, Is.True); Assert.That(store.DeletePlayerData().Success, Is.True);
            Assert.That(store.ReadManifest().Error, Is.EqualTo(DataError.Missing));
            Assert.That(Directory.Exists(store.RootPath), Is.False);
        }
        [Test] public void DuplicateIdIsRejectedWithoutChangingOriginal()
        {
            var saved = SaveMotion(); var original = File.ReadAllText(Path.Combine(store.RootPath, "manifest.json"));
            Assert.That(store.Save(saved, PlayerDataValidation.EncodePose(MockPoseTracker.MakeClip())).Error, Is.EqualTo(DataError.InvalidData));
            Assert.That(File.ReadAllText(Path.Combine(store.RootPath, "manifest.json")), Is.EqualTo(original));
        }
        [TestCase("{broken")]
        [TestCase("null")]
        [TestCase("{}")]
        [TestCase("{\"version\":1}")]
        [TestCase("{\"version\":1,\"samples\":null}")]
        public void BrokenManifestFailsWithoutOverwrite(string json)
        {
            Directory.CreateDirectory(store.RootPath); var path = Path.Combine(store.RootPath, "manifest.json"); File.WriteAllText(path, json);
            Assert.That(store.ReadManifest().Success, Is.False);
            var clip = MockPoseTracker.MakeClip();
            Assert.That(store.Save(SampleMetadata.Create(SampleKind.Motion, CaptureSource.Mock, 10, clip.frames.Length), PlayerDataValidation.EncodePose(clip)).Success, Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(json));
        }
        [Test] public void FutureManifestIsNotOverwritten()
        {
            Directory.CreateDirectory(store.RootPath); File.WriteAllText(Path.Combine(store.RootPath, "manifest.json"), "{\"version\":999,\"samples\":[]}");
            Assert.That(store.ReadManifest().Error, Is.EqualTo(DataError.UnsupportedVersion));
        }
        [Test] public void PathTraversalInManifestIsRejected()
        {
            var saved = SaveMotion(); string path = Path.Combine(store.RootPath, "manifest.json");
            File.WriteAllText(path, File.ReadAllText(path).Replace(saved.relativePath, "../StoryProgress.json"));
            Assert.That(store.Load(saved.id).Error, Is.EqualTo(DataError.Corrupt));
        }
        [TestCase("../StoryProgress")]
        [TestCase("")]
        [TestCase(null)]
        public void InvalidIdFails(string id) => Assert.That(store.Load(id).Error, Is.EqualTo(DataError.InvalidData));
        [Test] public void PayloadTamperingFailsChecksum()
        {
            var saved = SaveMotion(); var path = Path.Combine(store.RootPath, saved.relativePath); var bytes = File.ReadAllBytes(path);
            bytes[10] ^= 1; File.WriteAllBytes(path, bytes);
            Assert.That(store.Load(saved.id).Error, Is.EqualTo(DataError.Corrupt));
        }
        [Test] public void MissingPayloadIsExplicitFailure()
        {
            var saved = SaveMotion(); File.Delete(Path.Combine(store.RootPath, saved.relativePath));
            Assert.That(store.Load(saved.id).Error, Is.EqualTo(DataError.Missing));
        }
        [Test] public void LockedManifestReportsIoWithoutLeakingNewSample()
        {
            SaveMotion(); string path = Path.Combine(store.RootPath, "manifest.json");
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var clip = MockPoseTracker.MakeClip();
                Assert.That(store.Save(SampleMetadata.Create(SampleKind.Motion, CaptureSource.Mock, 10, clip.frames.Length), PlayerDataValidation.EncodePose(clip)).Error, Is.EqualTo(DataError.Io));
            }
            Assert.That(Directory.GetFiles(Path.Combine(store.RootPath, "samples")).Length, Is.EqualTo(1));
        }
        [Test] public void VoiceWavSaveLoadAndPcmRoundTrip()
        {
            var input = Enumerable.Range(0, 1600).Select(i => (float)Math.Sin(i * .1) * .5f).ToArray();
            var encoded = WavCodec.Encode(new VoiceSample(input, 2, 8000)); Assert.That(encoded.Success, Is.True);
            var saved = store.Save(SampleMetadata.Create(SampleKind.Voice, CaptureSource.MicrophoneTest, .1, 800), encoded.Value);
            Assert.That(saved.Success, Is.True, saved.Message);
            var decoded = WavCodec.Decode(store.Load(saved.Value.id).Value.Bytes);
            Assert.That(decoded.Success, Is.True); Assert.That(decoded.Value.Channels, Is.EqualTo(2));
            for (int i = 0; i < input.Length; i++) Assert.That(decoded.Value.InterleavedSamples[i], Is.EqualTo(input[i]).Within(.0001));
        }
        [Test] public void ImageAndSilhouettePngRoundTrip()
        {
            var texture = new Texture2D(2, 2); byte[] png = texture.EncodeToPNG(); UnityEngine.Object.DestroyImmediate(texture);
            foreach (var kind in new[] { SampleKind.Image, SampleKind.Silhouette })
            {
                var saved = store.Save(SampleMetadata.Create(kind, CaptureSource.Mock), png);
                Assert.That(saved.Success, Is.True, saved.Message); Assert.That(store.Load(saved.Value.id).Value.Bytes, Is.EqualTo(png));
            }
        }
        [Test] public void PoseIsStoredWithTimestampsAndLandmarkIdentity()
        {
            var clip = MockPoseTracker.MakeClip(1, 10);
            var saved = store.Save(SampleMetadata.Create(SampleKind.Pose, CaptureSource.Mock, 1, clip.frames.Length), PlayerDataValidation.EncodePose(clip));
            Assert.That(saved.Success, Is.True, saved.Message);
            var read = PlayerDataValidation.DecodePose(store.Load(saved.Value.id).Value.Bytes);
            Assert.That(read.frames[5].timestampSeconds, Is.EqualTo(.5)); Assert.That(read.frames[5].landmarks[0].name, Is.EqualTo("head"));
        }
        [Test] public void InvalidPoseTimestampsAndCoordinatesFail()
        {
            var clip = MockPoseTracker.MakeClip(); clip.frames[1].timestampSeconds = 0;
            Assert.That(PlayerDataValidation.Pose(clip), Is.False);
            clip = MockPoseTracker.MakeClip(); clip.frames[0].landmarks[0].position = new Vector3(float.NaN, 0, 0);
            Assert.That(PlayerDataValidation.Pose(clip), Is.False);
            clip = MockPoseTracker.MakeClip(); clip.frames[2].landmarks[0].name = "changed-topology";
            Assert.That(PlayerDataValidation.Pose(clip), Is.False);
        }
        [Test] public void MismatchedMetadataAndOversizedPayloadFailWithoutWriting()
        {
            var clip = MockPoseTracker.MakeClip();
            Assert.That(store.Save(SampleMetadata.Create(SampleKind.Motion, CaptureSource.Mock, 2, clip.frames.Length), PlayerDataValidation.EncodePose(clip)).Error, Is.EqualTo(DataError.InvalidData));
            Assert.That(store.Save(SampleMetadata.Create(SampleKind.Image, CaptureSource.Camera), new byte[PlayerDataValidation.MaxPayloadBytes + 1]).Error, Is.EqualTo(DataError.InvalidData));
            Assert.That(Directory.Exists(store.RootPath), Is.False);
        }
        [Test] public void UnversionedPosePayloadIsRejected()
        {
            var clip = MockPoseTracker.MakeClip();
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(clip).Replace("\"version\":1,", ""));
            Assert.That(store.Save(SampleMetadata.Create(SampleKind.Pose, CaptureSource.Mock, 10, clip.frames.Length), bytes).Error, Is.EqualTo(DataError.InvalidData));
        }
        [Test] public void PoseWithMissingLandmarkIdentityIsRejected()
        {
            var clip = MockPoseTracker.MakeClip();
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(clip).Replace("\"name\":\"head\",", ""));
            Assert.That(store.Save(SampleMetadata.Create(SampleKind.Motion, CaptureSource.Mock, 10, clip.frames.Length), bytes).Error, Is.EqualTo(DataError.InvalidData));
        }
        [Test] public void InvalidWavAndNaNAudioFail()
        {
            Assert.That(WavCodec.Decode(new byte[44]).Success, Is.False);
            Assert.That(WavCodec.Encode(new VoiceSample(new[] { float.NaN }, 1, 8000)).Success, Is.False);
            Assert.That(WavCodec.Encode(new VoiceSample(new float[801], 2, 8000)).Success, Is.False);
        }
        [Test] public void MissingDevicesAreExplicitFailures()
        {
            Assert.That(CaptureDeviceSelection.Select(Array.Empty<string>()).Error, Is.EqualTo(DataError.Unavailable));
            Assert.That(CaptureDeviceSelection.Select(null).Error, Is.EqualTo(DataError.Unavailable));
            Assert.That(CaptureDeviceSelection.Select(new[] { "camera" }, 1).Error, Is.EqualTo(DataError.Unavailable));
        }
        [Test] public void RawAndDisabledProcessorsAreUsableWithoutAiPackages()
        {
            Assert.That(new DisabledVoiceGenerator().Generate("test", null).Error, Is.EqualTo(DataError.Unavailable));
            Assert.That(new RawAppearanceProcessor().Process(null).Success, Is.False);
            Assert.That(new MockPoseTracker().Track(null, 0).Success, Is.True);
            Assert.That(new MockPoseTracker().Track(null, double.NaN).Success, Is.False);
        }
        [Test] public void CaptureWithoutCameraAndInvalidRecordingDurationDoNotThrow()
        {
            var gameObject = new GameObject("Capture test");
            try
            {
                var capture = gameObject.AddComponent<UnityCaptureDevices>();
                Assert.That(capture.Snapshot().Error, Is.EqualTo(DataError.Unavailable));
                DataResult<VoiceSample> result = null;
                var routine = capture.Record(-1, value => result = value); Assert.That(routine.MoveNext(), Is.False);
                Assert.That(result.Error, Is.EqualTo(DataError.InvalidData)); capture.CancelAll(); capture.CancelAll();
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); }
        }
        [Test] public void MotionReplayerRejectsInvalidOrUnboundClips()
        {
            var gameObject = new GameObject("Replay test");
            try
            {
                var replay = gameObject.AddComponent<LandmarkMotionReplayer>();
                Assert.That(replay.Play(null).Error, Is.EqualTo(DataError.InvalidData));
                Assert.That(replay.Play(MockPoseTracker.MakeClip()).Error, Is.EqualTo(DataError.Unavailable)); replay.Stop();
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); }
        }
        [Test] public void MissingCameraAndMicrophoneReturnFailureBeforeRequestingPermissions()
        {
            var gameObject = new GameObject("Missing device test");
            try
            {
                var capture = gameObject.AddComponent<UnityCaptureDevices>(); capture.Inventory = new MissingDevices();
                DataResult<bool> camera = null; DataResult<VoiceSample> voice = null;
                Assert.That(capture.StartCamera(result => camera = result).MoveNext(), Is.False);
                Assert.That(capture.Record(10, result => voice = result).MoveNext(), Is.False);
                Assert.That(camera.Error, Is.EqualTo(DataError.Unavailable));
                Assert.That(voice.Error, Is.EqualTo(DataError.Unavailable));
                Assert.That(capture.IsRecording, Is.False); Assert.That(capture.CameraTexture, Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); }
        }
        [Test] public void StoreDoesNotAliasCallerMetadataOrPayload()
        {
            var clip = MockPoseTracker.MakeClip(); var metadata = SampleMetadata.Create(SampleKind.Motion, CaptureSource.Mock, 10, clip.frames.Length);
            byte[] payload = PlayerDataValidation.EncodePose(clip);
            var saved = store.Save(metadata, payload); metadata.id = "changed"; Array.Clear(payload, 0, payload.Length);
            Assert.That(store.Load(saved.Value.id).Success, Is.True);
        }
    }
}
