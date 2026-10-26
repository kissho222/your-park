using System;
using System.Runtime.Serialization;
using UnityEngine;

namespace YourPark.PlayerData
{
    public enum SampleKind { Voice, Image, Silhouette, Pose, Motion }
    public enum CaptureSource { MicrophoneTest, Camera, PoseTracker, Mock }

    [Serializable, DataContract]
    public sealed class SampleMetadata
    {
        [DataMember(IsRequired = true)]
        public string id;
        [DataMember(IsRequired = true)]
        public SampleKind type;
        [DataMember(IsRequired = true)]
        public string createdUtc;
        [DataMember(IsRequired = true)]
        public CaptureSource source;
        [DataMember(IsRequired = true)]
        public int version = 1;
        [DataMember(IsRequired = true)]
        public double durationSeconds;
        [DataMember(IsRequired = true)]
        public int frameCount;
        [DataMember(IsRequired = true)]
        public string relativePath;
        [DataMember(IsRequired = true)]
        public int byteCount;
        [DataMember(IsRequired = true)]
        public string sha256;

        public static SampleMetadata Create(SampleKind kind, CaptureSource origin, double duration = 0, int frames = 1)
        {
            return new SampleMetadata {
                id = Guid.NewGuid().ToString("N"), type = kind, source = origin,
                createdUtc = DateTime.UtcNow.ToString("O"), durationSeconds = duration, frameCount = frames
            };
        }
    }

    [Serializable, DataContract]
    public sealed class PlayerDataManifest
    {
        [DataMember(IsRequired = true)]
        public int version = 1;
        [DataMember(IsRequired = true)]
        public SampleMetadata[] samples = Array.Empty<SampleMetadata>();
    }

    [Serializable, DataContract]
    public sealed class Landmark
    {
        [DataMember(IsRequired = true)]
        public string name;
        [DataMember(IsRequired = true)]
        public Vector3 position;
        [DataMember(IsRequired = true)]
        public float confidence = 1;
    }

    [Serializable, DataContract]
    public sealed class PoseFrame
    {
        [DataMember(IsRequired = true)]
        public double timestampSeconds;
        [DataMember(IsRequired = true)]
        public Landmark[] landmarks = Array.Empty<Landmark>();
    }

    [Serializable, DataContract]
    public class PoseClip
    {
        [DataMember(IsRequired = true)]
        public int version = 1;
        // Normalized image coordinates: x right, y up; z is tracker-relative depth.
        [DataMember(IsRequired = true)]
        public string coordinateSpace = "normalized-image-y-up";
        [DataMember(IsRequired = true)]
        public PoseFrame[] frames = Array.Empty<PoseFrame>();
    }

    [Serializable]
    public sealed class MotionClip : PoseClip { }

    public sealed class VoiceSample
    {
        public float[] InterleavedSamples { get; }
        public int Channels { get; }
        public int SampleRate { get; }
        public VoiceSample(float[] samples, int channels, int sampleRate)
        { InterleavedSamples = samples; Channels = channels; SampleRate = sampleRate; }
    }

    public class ImageSample
    {
        public byte[] Png { get; }
        public ImageSample(byte[] png) { Png = png; }
    }

    public sealed class SilhouetteSample : ImageSample
    {
        public SilhouetteSample(byte[] png) : base(png) { }
    }

    public sealed class LoadedSample
    {
        public SampleMetadata Metadata { get; }
        public byte[] Bytes { get; }
        public LoadedSample(SampleMetadata metadata, byte[] bytes) { Metadata = metadata; Bytes = bytes; }
    }

    public enum DataError { None, InvalidData, Missing, Corrupt, UnsupportedVersion, Io, Unavailable, PermissionDenied, Cancelled }

    public sealed class DataResult<T>
    {
        public bool Success => Error == DataError.None;
        public T Value { get; }
        public DataError Error { get; }
        public string Message { get; }
        private DataResult(T value, DataError error, string message) { Value = value; Error = error; Message = message; }
        public static DataResult<T> Ok(T value) => new DataResult<T>(value, DataError.None, "");
        public static DataResult<T> Fail(DataError error, string message) => new DataResult<T>(default, error, message);
    }
}
