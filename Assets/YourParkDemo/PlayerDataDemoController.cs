using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using YourPark.PlayerData;

namespace YourPark.Demo
{
    public sealed class PlayerDataDemoController : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private UnityCaptureDevices capture;
        [SerializeField] private AudioSource speaker;
        [SerializeField] private LandmarkMotionReplayer motion;
        private IPlayerDataStore store;
        private PlayerDataSession session;
        private VisualElement root;
        private readonly List<string> ids = new List<string>();
        private readonly List<string> choices = new List<string>();
        private string storyPath;
        private Texture2D replayImage;
        private AudioClip replayAudio;
        private bool busy;
        private bool livePreview;

        private void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--playerdata-smoke") >= 0) return;
#endif
            store = new LocalPlayerDataStore(Application.persistentDataPath);
            session = new PlayerDataSession(store);
            root = document.rootVisualElement;
            root.Q<Label>("folder").text = store.RootPath;
            storyPath = Path.Combine(Application.persistentDataPath, "StoryProgress.demo.json");
            // Test-only progress fixture: never access a real game's StoryProgress.
            if (!File.Exists(storyPath)) TryStoryFixture();
            Bind("camera", () => Run(CameraStart()));
            Bind("stop-camera", () => { livePreview = false; capture.StopCamera(); root.Q<Image>("preview").image = null; });
            Bind("snapshot", Snapshot);
            Bind("record", () => Run(Record()));
            Bind("cancel", () => { capture.CancelAll(); StopAllCoroutines(); busy = false; Status("Capture cancelled."); });
            Bind("mock", SaveMock);
            Bind("reload", Refresh);
            Bind("load", LoadSelected);
            Bind("play", Replay);
            Bind("folder-open", () => {
                if (Directory.Exists(store.RootPath)) Application.OpenURL(new Uri(store.RootPath + Path.DirectorySeparatorChar).AbsoluteUri);
                else Status("PlayerData folder is absent. Save a sample first.");
            });
            Bind("delete", () => root.Q("delete-confirm").style.display = DisplayStyle.Flex);
            Bind("delete-no", () => root.Q("delete-confirm").style.display = DisplayStyle.None);
            Bind("delete-yes", Delete);
            Bind("story", CheckStory);
            Refresh();
        }
        private void Bind(string name, Action action) => root.Q<Button>(name).clicked += action;
        private void Status(string message) => root.Q<Label>("status").text = message;
        private void Run(IEnumerator task)
        {
            if (busy) { Status("Capture is already running."); return; }
            busy = true; StartCoroutine(Owned(task));
        }
        private IEnumerator Owned(IEnumerator task) { try { yield return task; } finally { busy = false; } }
        private IEnumerator CameraStart()
        {
            Status("Starting camera...");
            yield return capture.StartCamera(result => {
                if (result.Success) { livePreview = true; root.Q<Image>("preview").image = capture.CameraTexture; }
                Status(result.Success ? "Camera ready. Save frame captures one raw PNG." : result.Message);
            });
        }
        private void Update()
        {
            if (root != null && livePreview) root.Q<Image>("preview").image = capture.CameraTexture;
        }
        private void Snapshot()
        {
            var result = capture.Snapshot();
            if (!result.Success) { Status(result.Message); return; }
            Save(SampleMetadata.Create(SampleKind.Image, CaptureSource.Camera), result.Value.Png);
        }
        private IEnumerator Record()
        {
            Status("Recording microphone for 10 seconds...");
            yield return capture.Record(10, result => {
                if (!result.Success) { Status(result.Message); return; }
                var wav = WavCodec.Encode(result.Value);
                if (!wav.Success) { Status(wav.Message); return; }
                int frames = result.Value.InterleavedSamples.Length / result.Value.Channels;
                Save(SampleMetadata.Create(SampleKind.Voice, CaptureSource.MicrophoneTest, (double)frames / result.Value.SampleRate, frames), wav.Value);
            });
        }
        private void SaveMock()
        {
            var clip = MockPoseTracker.MakeClip();
            Save(SampleMetadata.Create(SampleKind.Motion, CaptureSource.Mock, 10, clip.frames.Length), PlayerDataValidation.EncodePose(clip));
        }
        private void Save(SampleMetadata metadata, byte[] payload)
        {
            var result = store.Save(metadata, payload);
            if (!result.Success) { Status(result.Message); return; }
            Refresh(); Select(result.Value.id);
            Status("Saved " + result.Value.type + ". Load into RAM before deleting disk data.");
        }
        private string Selected => root.Q<DropdownField>("selection").index >= 0 && root.Q<DropdownField>("selection").index < ids.Count
            ? ids[root.Q<DropdownField>("selection").index] : null;
        private void Select(string id) { int index = ids.IndexOf(id); if (index >= 0) root.Q<DropdownField>("selection").index = index; }
        private void Refresh()
        {
            string previous = Selected;
            var manifest = store.ReadManifest();
            root.Q<Label>("manifest").text = manifest.Success ? JsonUtility.ToJson(manifest.Value, true) : manifest.Message;
            if (manifest.Success)
            {
                foreach (var entry in manifest.Value.samples)
                    if (!ids.Contains(entry.id)) { ids.Add(entry.id); choices.Add(entry.type + " / " + entry.source + " / " + entry.id); }
            }
            root.Q<DropdownField>("selection").choices = new List<string>(choices);
            if (previous != null) Select(previous); else if (ids.Count > 0) root.Q<DropdownField>("selection").index = 0;
            Status("RAM loaded: " + session.LoadedCount + ". " + (manifest.Success ? "Manifest read." : manifest.Message));
        }
        private void LoadSelected()
        {
            var result = session.Load(Selected);
            Status(result.Success ? "Loaded into RAM. " + result.Value.Metadata.type : result.Message);
        }
        private void Replay()
        {
            var loaded = session.Load(Selected);
            if (!loaded.Success) { Status(loaded.Message); return; }
            var sample = loaded.Value;
            if (sample.Metadata.type == SampleKind.Voice)
            {
                var decoded = WavCodec.Decode(sample.Bytes);
                if (!decoded.Success) { Status(decoded.Message); return; }
                speaker.Stop(); if (replayAudio != null) Destroy(replayAudio);
                var voice = decoded.Value;
                replayAudio = AudioClip.Create("PlayerData RAM replay", voice.InterleavedSamples.Length / voice.Channels, voice.Channels, voice.SampleRate, false);
                replayAudio.SetData(voice.InterleavedSamples, 0); speaker.clip = replayAudio; speaker.Play();
            }
            else if (sample.Metadata.type == SampleKind.Motion || sample.Metadata.type == SampleKind.Pose)
            {
                var result = motion.Play(PlayerDataValidation.DecodePose(sample.Bytes));
                if (!result.Success) { Status(result.Message); return; }
            }
            else
            {
                if (replayImage != null) Destroy(replayImage);
                replayImage = new Texture2D(2, 2);
                if (!replayImage.LoadImage(sample.Bytes)) { Destroy(replayImage); replayImage = null; Status("PNG decode failed."); return; }
                livePreview = false; root.Q<Image>("preview").image = replayImage;
            }
            Status("Replaying loaded RAM data. " + sample.Metadata.id);
        }
        private void Delete()
        {
            if (busy || capture.IsRecording) { Status("Cancel capture before deleting."); return; }
            var result = session.DeleteDiskData();
            root.Q("delete-confirm").style.display = DisplayStyle.None;
            Refresh();
            Status(result.Success ? "Disk PlayerData deleted. Loaded RAM remains until exit; restart cannot reload it." : result.Message);
        }
        private void TryStoryFixture()
        {
            try { Directory.CreateDirectory(Application.persistentDataPath); File.WriteAllText(storyPath, "{\"demoProgress\":7}"); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { Status("Demo StoryProgress fixture could not be written."); }
        }
        private void CheckStory()
        {
            try { Status(File.Exists(storyPath) && File.ReadAllText(storyPath) == "{\"demoProgress\":7}" ? "StoryProgress demo is intact (7)." : "StoryProgress demo is absent or changed."); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { Status("StoryProgress demo could not be read."); }
        }
        private void OnDestroy()
        {
            capture.CancelAll(); motion.Stop(); speaker.Stop(); speaker.clip = null;
            if (replayAudio != null) Destroy(replayAudio);
            if (replayImage != null) Destroy(replayImage);
            session?.Dispose();
        }
    }
}
