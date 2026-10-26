using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace YourPark.PlayerData
{
    public static class PlayerDataValidation
    {
        public const int MaxPayloadBytes = 16 * 1024 * 1024;
        public const int MaxSamples = 128;
        public const int MaxFrames = 1800;
        public const int MaxLandmarks = 128;
        public const double MaxDurationSeconds = 30;

        public static string Extension(SampleKind kind) => kind == SampleKind.Voice ? ".wav" :
            kind == SampleKind.Image || kind == SampleKind.Silhouette ? ".png" : ".json";
        public static string RelativePath(SampleMetadata sample) => "samples/" + sample.id + Extension(sample.type);
        public static bool ValidId(string id) => Guid.TryParseExact(id, "N", out var parsed) && parsed.ToString("N") == id;
        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        public static bool Metadata(SampleMetadata sample)
        {
            return sample != null && ValidId(sample.id) && sample.version == 1 &&
                Enum.IsDefined(typeof(SampleKind), sample.type) && Enum.IsDefined(typeof(CaptureSource), sample.source) &&
                DateTimeOffset.TryParseExact(sample.createdUtc, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) &&
                date.Offset == TimeSpan.Zero && Finite(sample.durationSeconds) && sample.durationSeconds >= 0 &&
                sample.durationSeconds <= MaxDurationSeconds && sample.frameCount >= 1 && sample.frameCount <= 5760000;
        }
        public static bool Manifest(PlayerDataManifest manifest)
        {
            if (manifest == null || manifest.version != 1 || manifest.samples == null || manifest.samples.Length > MaxSamples) return false;
            var ids = new HashSet<string>();
            foreach (var sample in manifest.samples)
            {
                if (!Metadata(sample) || !ids.Add(sample.id) || sample.relativePath != RelativePath(sample) ||
                    sample.byteCount <= 0 || sample.byteCount > MaxPayloadBytes || sample.sha256 == null || sample.sha256.Length != 64) return false;
                foreach (char c in sample.sha256) if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
            }
            return true;
        }
        public static bool Pose(PoseClip clip)
        {
            if (clip == null || clip.version != 1 || clip.coordinateSpace != "normalized-image-y-up" ||
                clip.frames == null || clip.frames.Length == 0 || clip.frames.Length > MaxFrames) return false;
            double previous = -1;
            string[] names = null;
            foreach (var frame in clip.frames)
            {
                if (frame == null || !Finite(frame.timestampSeconds) || frame.timestampSeconds < 0 ||
                    frame.timestampSeconds > MaxDurationSeconds || frame.timestampSeconds <= previous ||
                    frame.landmarks == null || frame.landmarks.Length == 0 || frame.landmarks.Length > MaxLandmarks) return false;
                var seen = new HashSet<string>();
                if (names != null && names.Length != frame.landmarks.Length) return false;
                for (int i = 0; i < frame.landmarks.Length; i++)
                {
                    var point = frame.landmarks[i];
                    if (point == null || string.IsNullOrWhiteSpace(point.name) || point.name.Length > 64 || !seen.Add(point.name) ||
                        !Finite(point.position.x) || !Finite(point.position.y) || !Finite(point.position.z) ||
                        !Finite(point.confidence) || point.confidence < 0 || point.confidence > 1 ||
                        (names != null && names[i] != point.name)) return false;
                }
                if (names == null) { names = new string[frame.landmarks.Length]; for (int i = 0; i < names.Length; i++) names[i] = frame.landmarks[i].name; }
                previous = frame.timestampSeconds;
            }
            return true;
        }
        public static byte[] EncodePose(PoseClip clip) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(clip));
        public static PoseClip DecodePose(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes, false))
                return (PoseClip)new DataContractJsonSerializer(typeof(PoseClip)).ReadObject(stream);
        }
        public static bool Payload(SampleMetadata metadata, byte[] bytes)
        {
            if (!Metadata(metadata) || bytes == null || bytes.Length == 0 || bytes.Length > MaxPayloadBytes) return false;
            if (metadata.type == SampleKind.Voice)
            {
                var voice = WavCodec.Decode(bytes);
                return voice.Success && metadata.frameCount == voice.Value.InterleavedSamples.Length / voice.Value.Channels &&
                    Math.Abs(metadata.durationSeconds - (double)metadata.frameCount / voice.Value.SampleRate) < 0.0001;
            }
            if (metadata.type == SampleKind.Image || metadata.type == SampleKind.Silhouette)
            {
                // Bound dimensions before Unity's image decoder allocates memory.
                if (bytes.Length < 33 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71 ||
                    bytes[4] != 13 || bytes[5] != 10 || bytes[6] != 26 || bytes[7] != 10 ||
                    Encoding.ASCII.GetString(bytes, 12, 4) != "IHDR") return false;
                uint width = BigEndian(bytes, 16), height = BigEndian(bytes, 20);
                return width > 0 && height > 0 && width <= 4096 && height <= 4096 && metadata.frameCount == 1 && metadata.durationSeconds == 0;
            }
            try
            {
                var clip = DecodePose(bytes);
                return Pose(clip) && metadata.frameCount == clip.frames.Length &&
                    Math.Abs(metadata.durationSeconds - clip.frames[clip.frames.Length - 1].timestampSeconds) < 0.0001;
            }
            catch (SerializationException) { return false; }
        }
        private static uint BigEndian(byte[] bytes, int offset) =>
            (uint)bytes[offset] << 24 | (uint)bytes[offset + 1] << 16 | (uint)bytes[offset + 2] << 8 | bytes[offset + 3];
    }
}
