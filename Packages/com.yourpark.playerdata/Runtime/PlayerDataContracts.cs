namespace YourPark.PlayerData
{
    public interface IPlayerDataStore
    {
        string RootPath { get; }
        DataResult<PlayerDataManifest> ReadManifest();
        DataResult<SampleMetadata> Save(SampleMetadata metadata, byte[] payload);
        DataResult<LoadedSample> Load(string id);
        DataResult<bool> DeletePlayerData();
    }

    public interface IVoiceConverter { DataResult<VoiceSample> Convert(VoiceSample input); }
    public interface IVoiceGenerator { DataResult<VoiceSample> Generate(string request, VoiceSample reference); }
    public interface IPoseTracker { DataResult<PoseFrame> Track(ImageSample image, double timestampSeconds); }
    public interface IMotionReplayer { DataResult<bool> Play(PoseClip clip); void Stop(); }
    public interface IAppearanceProcessor { DataResult<ImageSample> Process(ImageSample image); }
    public interface ICaptureDeviceInventory { string[] Cameras { get; } string[] Microphones { get; } }

    public static class CaptureDeviceSelection
    {
        public static DataResult<string> Select(string[] devices, int index = 0)
        {
            if (devices == null || index < 0 || index >= devices.Length || string.IsNullOrWhiteSpace(devices[index]))
                return DataResult<string>.Fail(DataError.Unavailable, "入力デバイスがありません。");
            return DataResult<string>.Ok(devices[index]);
        }
    }
}
