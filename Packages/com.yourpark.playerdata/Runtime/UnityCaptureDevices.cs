using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace YourPark.PlayerData
{
    public sealed class UnityCaptureDeviceInventory : ICaptureDeviceInventory
    {
        public string[] Cameras => WebCamTexture.devices.Select(device => device.name).ToArray();
        public string[] Microphones => Microphone.devices;
    }

    public sealed class UnityCaptureDevices : MonoBehaviour
    {
        public WebCamTexture CameraTexture { get; private set; }
        public bool IsRecording { get; private set; }
        private string microphoneName;
        private AudioClip recording;
        private int generation;
        public ICaptureDeviceInventory Inventory { get; set; } = new UnityCaptureDeviceInventory();

        public IEnumerator StartCamera(Action<DataResult<bool>> done)
        {
            if (CameraTexture != null) { done(DataResult<bool>.Ok(true)); yield break; }
            var selected = CaptureDeviceSelection.Select(Inventory.Cameras);
            if (!selected.Success) { done(DataResult<bool>.Fail(selected.Error, selected.Message)); yield break; }
            int owner = generation;
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (owner != generation) { done(DataResult<bool>.Fail(DataError.Cancelled, "カメラ開始を中止しました。")); yield break; }
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            { done(DataResult<bool>.Fail(DataError.PermissionDenied, "カメラの使用が許可されていません。")); yield break; }
            var started = TryStartCamera(selected.Value);
            if (!started.Success) { done(started); yield break; }
            double deadline = Time.realtimeSinceStartupAsDouble + 8;
            while (CameraTexture != null && !CameraTexture.didUpdateThisFrame && Time.realtimeSinceStartupAsDouble < deadline && owner == generation)
                yield return null;
            if (owner != generation || CameraTexture == null || !CameraTexture.didUpdateThisFrame)
            { StopCamera(); done(DataResult<bool>.Fail(DataError.Unavailable, "カメラ映像を取得できませんでした。")); yield break; }
            done(DataResult<bool>.Ok(true));
        }
        private DataResult<bool> TryStartCamera(string device)
        {
            try { CameraTexture = new WebCamTexture(device, 640, 480, 30); CameraTexture.Play(); return DataResult<bool>.Ok(true); }
            catch (Exception error) when (error is UnityException || error is InvalidOperationException || error is ArgumentException)
            { StopCamera(); return DataResult<bool>.Fail(DataError.Unavailable, "カメラを開始できませんでした。"); }
        }
        public DataResult<ImageSample> Snapshot()
        {
            if (CameraTexture == null || !CameraTexture.isPlaying || CameraTexture.width <= 16 || CameraTexture.height <= 16)
                return DataResult<ImageSample>.Fail(DataError.Unavailable, "カメラ映像がありません。");
            if (CameraTexture.width > 4096 || CameraTexture.height > 4096)
                return DataResult<ImageSample>.Fail(DataError.Unavailable, "カメラ映像が大きすぎます。");
            Texture2D image = null;
            try
            {
                image = new Texture2D(CameraTexture.width, CameraTexture.height, TextureFormat.RGBA32, false);
                image.SetPixels32(CameraTexture.GetPixels32()); image.Apply();
                return DataResult<ImageSample>.Ok(new ImageSample(image.EncodeToPNG()));
            }
            catch (Exception error) when (error is UnityException || error is InvalidOperationException || error is ArgumentException)
            { return DataResult<ImageSample>.Fail(DataError.Unavailable, "カメラフレームを取得できませんでした。"); }
            finally { if (image != null) Destroy(image); }
        }
        public IEnumerator Record(float seconds, Action<DataResult<VoiceSample>> done)
        {
            if (IsRecording) { done(DataResult<VoiceSample>.Fail(DataError.Unavailable, "録音中です。")); yield break; }
            if (!PlayerDataValidation.Finite(seconds) || seconds <= 0 || seconds > PlayerDataValidation.MaxDurationSeconds)
            { done(DataResult<VoiceSample>.Fail(DataError.InvalidData, "録音時間は0秒より長く30秒以下で指定してください。")); yield break; }
            var selected = CaptureDeviceSelection.Select(Inventory.Microphones);
            if (!selected.Success) { done(DataResult<VoiceSample>.Fail(selected.Error, selected.Message)); yield break; }
            IsRecording = true;
            int owner = generation;
            try
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
                if (owner != generation) { done(DataResult<VoiceSample>.Fail(DataError.Cancelled, "録音を中止しました。")); yield break; }
                if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                { done(DataResult<VoiceSample>.Fail(DataError.PermissionDenied, "マイクの使用が許可されていません。")); yield break; }
                var started = TryStartMicrophone(selected.Value, Mathf.CeilToInt(seconds) + 1);
                if (!started.Success) { done(DataResult<VoiceSample>.Fail(started.Error, started.Message)); yield break; }
                double deadline = Time.realtimeSinceStartupAsDouble + 5;
                while (owner == generation && Microphone.GetPosition(microphoneName) <= 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (owner != generation || Microphone.GetPosition(microphoneName) <= 0)
                { done(DataResult<VoiceSample>.Fail(DataError.Unavailable, "マイク入力を開始できませんでした。")); yield break; }
                double end = Time.realtimeSinceStartupAsDouble + seconds;
                while (owner == generation && Time.realtimeSinceStartupAsDouble < end && Microphone.IsRecording(microphoneName)) yield return null;
                if (owner != generation) { done(DataResult<VoiceSample>.Fail(DataError.Cancelled, "録音を中止しました。")); yield break; }
                done(ReadRecording(seconds));
            }
            finally { if (owner == generation) { StopMicrophone(); IsRecording = false; } }
        }
        private DataResult<bool> TryStartMicrophone(string device, int seconds)
        {
            try
            {
                microphoneName = device; recording = Microphone.Start(device, false, seconds, 44100);
                return recording != null ? DataResult<bool>.Ok(true) : DataResult<bool>.Fail(DataError.Unavailable, "録音を開始できませんでした。");
            }
            catch (Exception error) when (error is UnityException || error is InvalidOperationException || error is ArgumentException)
            { return DataResult<bool>.Fail(DataError.Unavailable, "録音を開始できませんでした。"); }
        }
        private DataResult<VoiceSample> ReadRecording(float seconds)
        {
            try
            {
                int frames = Microphone.GetPosition(microphoneName);
                if (recording == null || frames <= 0)
                    return DataResult<VoiceSample>.Fail(DataError.Unavailable, "録音データがありません。");
                if (frames < Mathf.RoundToInt(seconds * recording.frequency) - recording.frequency / 10)
                    return DataResult<VoiceSample>.Fail(DataError.Unavailable, "録音が途中で停止しました。入力デバイスを確認してください。");
                frames = Math.Min(frames, Mathf.RoundToInt(seconds * recording.frequency));
                var samples = new float[frames * recording.channels];
                if (!recording.GetData(samples, 0)) return DataResult<VoiceSample>.Fail(DataError.Unavailable, "録音データを読み取れません。");
                return DataResult<VoiceSample>.Ok(new VoiceSample(samples, recording.channels, recording.frequency));
            }
            catch (Exception error) when (error is UnityException || error is InvalidOperationException || error is ArgumentException)
            { return DataResult<VoiceSample>.Fail(DataError.Unavailable, "録音の読み取りに失敗しました。"); }
        }
        public void StopCamera()
        {
            if (CameraTexture == null) return;
            CameraTexture.Stop(); Destroy(CameraTexture); CameraTexture = null;
        }
        private void StopMicrophone()
        {
            if (microphoneName != null) Microphone.End(microphoneName);
            microphoneName = null;
            if (recording != null) Destroy(recording);
            recording = null;
        }
        public void CancelAll()
        {
            generation++; StopAllCoroutines(); StopMicrophone(); StopCamera(); IsRecording = false;
        }
        private void OnDisable() => CancelAll();
    }
}
