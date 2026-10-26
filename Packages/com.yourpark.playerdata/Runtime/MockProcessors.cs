using System;
using UnityEngine;

namespace YourPark.PlayerData
{
    public sealed class PassthroughVoiceConverter : IVoiceConverter
    {
        public DataResult<VoiceSample> Convert(VoiceSample input) => WavCodec.Encode(input).Success
            ? DataResult<VoiceSample>.Ok(input) : DataResult<VoiceSample>.Fail(DataError.InvalidData, "音声が不正です。");
    }
    public sealed class DisabledVoiceGenerator : IVoiceGenerator
    {
        public DataResult<VoiceSample> Generate(string request, VoiceSample reference) =>
            DataResult<VoiceSample>.Fail(DataError.Unavailable, "v0には音声生成モデルがありません。");
    }
    public sealed class RawAppearanceProcessor : IAppearanceProcessor
    {
        public DataResult<ImageSample> Process(ImageSample image) => image?.Png != null
            ? DataResult<ImageSample>.Ok(new ImageSample((byte[])image.Png.Clone()))
            : DataResult<ImageSample>.Fail(DataError.InvalidData, "画像がありません。");
    }
    public sealed class MockPoseTracker : IPoseTracker
    {
        public DataResult<PoseFrame> Track(ImageSample image, double timestampSeconds)
        {
            if (!PlayerDataValidation.Finite(timestampSeconds) || timestampSeconds < 0 || timestampSeconds > PlayerDataValidation.MaxDurationSeconds)
                return DataResult<PoseFrame>.Fail(DataError.InvalidData, "時刻が不正です。");
            float wave = (float)Math.Sin(timestampSeconds * 4) * 0.15f;
            return DataResult<PoseFrame>.Ok(new PoseFrame { timestampSeconds = timestampSeconds, landmarks = new[] {
                Point("head", .5f, .8f), Point("hip", .5f, .4f), Point("left_hand", .2f, .6f + wave),
                Point("right_hand", .8f, .6f - wave), Point("left_foot", .35f, .1f), Point("right_foot", .65f, .1f)
            }});
        }
        private static Landmark Point(string name, float x, float y) => new Landmark { name = name, position = new Vector3(x, y, 0) };
        public static MotionClip MakeClip(double duration = 10, int rate = 30)
        {
            if (!PlayerDataValidation.Finite(duration) || duration <= 0 || duration > 30 || rate < 1 || rate > 59)
                throw new ArgumentOutOfRangeException(nameof(duration));
            int count = (int)Math.Ceiling(duration * rate) + 1;
            var frames = new PoseFrame[count]; var tracker = new MockPoseTracker();
            for (int i = 0; i < count; i++) frames[i] = tracker.Track(null, Math.Min(duration, (double)i / rate)).Value;
            return new MotionClip { frames = frames };
        }
    }
}
