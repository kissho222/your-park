using System;
using System.IO;
using System.Text;

namespace YourPark.PlayerData
{
    // v0 deliberately accepts only its own canonical little-endian PCM16 WAV format.
    public static class WavCodec
    {
        public static DataResult<byte[]> Encode(VoiceSample voice)
        {
            if (voice == null || voice.InterleavedSamples == null || voice.InterleavedSamples.Length == 0 ||
                voice.Channels < 1 || voice.Channels > 2 || voice.SampleRate < 8000 || voice.SampleRate > 96000 ||
                voice.InterleavedSamples.Length % voice.Channels != 0 ||
                (double)voice.InterleavedSamples.Length / voice.Channels / voice.SampleRate > PlayerDataValidation.MaxDurationSeconds)
                return DataResult<byte[]>.Fail(DataError.InvalidData, "音声形式が不正です。");
            foreach (float value in voice.InterleavedSamples)
                if (!PlayerDataValidation.Finite(value)) return DataResult<byte[]>.Fail(DataError.InvalidData, "音声に無効な値があります。");
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                int length = voice.InterleavedSamples.Length * 2;
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + length);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1);
                writer.Write((short)voice.Channels); writer.Write(voice.SampleRate); writer.Write(voice.SampleRate * voice.Channels * 2);
                writer.Write((short)(voice.Channels * 2)); writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(length);
                foreach (float value in voice.InterleavedSamples) writer.Write((short)Math.Round(Math.Max(-1, Math.Min(1, value)) * 32767));
                return DataResult<byte[]>.Ok(stream.ToArray());
            }
        }
        public static DataResult<VoiceSample> Decode(byte[] bytes)
        {
            var invalid = DataResult<VoiceSample>.Fail(DataError.Corrupt, "PCM16 WAVデータが不正です。");
            if (bytes == null || bytes.Length < 46 || bytes.Length > PlayerDataValidation.MaxPayloadBytes) return invalid;
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream, Encoding.ASCII))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF" || reader.ReadInt32() != bytes.Length - 8 ||
                    Encoding.ASCII.GetString(reader.ReadBytes(8)) != "WAVEfmt " || reader.ReadInt32() != 16 || reader.ReadInt16() != 1) return invalid;
                int channels = reader.ReadInt16(), rate = reader.ReadInt32(), byteRate = reader.ReadInt32();
                int align = reader.ReadInt16(), bits = reader.ReadInt16();
                if (channels < 1 || channels > 2 || rate < 8000 || rate > 96000 || byteRate != rate * channels * 2 ||
                    align != channels * 2 || bits != 16 || Encoding.ASCII.GetString(reader.ReadBytes(4)) != "data") return invalid;
                int length = reader.ReadInt32();
                if (length != bytes.Length - 44 || length % align != 0 || (double)length / byteRate > PlayerDataValidation.MaxDurationSeconds) return invalid;
                var samples = new float[length / 2];
                for (int i = 0; i < samples.Length; i++) samples[i] = reader.ReadInt16() / 32768f;
                return DataResult<VoiceSample>.Ok(new VoiceSample(samples, channels, rate));
            }
        }
    }
}
