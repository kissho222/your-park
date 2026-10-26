using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using UnityEngine;

namespace YourPark.PlayerData
{
    // All file I/O lives here. StoryProgress is outside this store's fixed child directory.
    public sealed class LocalPlayerDataStore : IPlayerDataStore
    {
        private readonly object gate = new object();
        public string RootPath { get; }
        private string ManifestPath => Path.Combine(RootPath, "manifest.json");
        public LocalPlayerDataStore(string gameDataDirectory)
        {
            if (string.IsNullOrWhiteSpace(gameDataDirectory)) throw new ArgumentException("ゲーム保存先が必要です。", nameof(gameDataDirectory));
            RootPath = Path.Combine(Path.GetFullPath(gameDataDirectory), "PlayerData");
        }
        public DataResult<PlayerDataManifest> ReadManifest()
        {
            lock (gate) return Guard(() => ReadManifestInternal());
        }
        private DataResult<PlayerDataManifest> ReadManifestInternal()
        {
            CheckLinks(RootPath);
            if (!File.Exists(ManifestPath)) return DataResult<PlayerDataManifest>.Fail(DataError.Missing, "PlayerDataは保存されていません。");
            CheckLinks(ManifestPath);
            var info = new FileInfo(ManifestPath);
            if (info.Length == 0 || info.Length > 256 * 1024) return DataResult<PlayerDataManifest>.Fail(DataError.Corrupt, "manifestのサイズが不正です。");
            PlayerDataManifest manifest;
            try
            {
                // JsonUtility normalizes null arrays to empty arrays. The strict reader must preserve invalid input.
                using (var input = new FileStream(ManifestPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    manifest = (PlayerDataManifest)new DataContractJsonSerializer(typeof(PlayerDataManifest)).ReadObject(input);
            }
            catch (SerializationException) { return DataResult<PlayerDataManifest>.Fail(DataError.Corrupt, "manifestを読み取れません。"); }
            if (manifest != null && manifest.version != 1) return DataResult<PlayerDataManifest>.Fail(DataError.UnsupportedVersion, "未対応のmanifestバージョンです。");
            return PlayerDataValidation.Manifest(manifest) ? DataResult<PlayerDataManifest>.Ok(manifest) :
                DataResult<PlayerDataManifest>.Fail(DataError.Corrupt, "manifestの内容が不正です。");
        }
        public DataResult<SampleMetadata> Save(SampleMetadata metadata, byte[] payload)
        {
            lock (gate) return Guard(() =>
            {
                if (!PlayerDataValidation.Payload(metadata, payload))
                    return DataResult<SampleMetadata>.Fail(DataError.InvalidData, "保存するデータが不正です。");
                // Caller changes must never mutate the persisted manifest or session snapshot.
                var entry = JsonUtility.FromJson<SampleMetadata>(JsonUtility.ToJson(metadata));
                var data = (byte[])payload.Clone();
                var current = ReadManifestInternal();
                if (!current.Success && current.Error != DataError.Missing)
                    return DataResult<SampleMetadata>.Fail(current.Error, current.Message);
                var samples = new List<SampleMetadata>(current.Success ? current.Value.samples : Array.Empty<SampleMetadata>());
                if (samples.Count >= PlayerDataValidation.MaxSamples)
                    return DataResult<SampleMetadata>.Fail(DataError.InvalidData, "保存件数の上限です。");
                if (samples.Exists(sample => sample.id == entry.id))
                    return DataResult<SampleMetadata>.Fail(DataError.InvalidData, "IDが重複しています。");
                entry.relativePath = PlayerDataValidation.RelativePath(entry);
                entry.byteCount = data.Length; entry.sha256 = PlayerDataValidation.Hash(data);
                CheckLinks(RootPath);
                Directory.CreateDirectory(RootPath);
                string sampleDirectory = Path.Combine(RootPath, "samples");
                CheckLinks(sampleDirectory);
                Directory.CreateDirectory(sampleDirectory);
                string target = Path.Combine(RootPath, entry.relativePath);
                CheckLinks(target);
                if (File.Exists(target)) return DataResult<SampleMetadata>.Fail(DataError.Io, "同じIDのファイルが既にあります。");
                string temporary = target + ".tmp";
                string manifestTemporary = ManifestPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                bool published = false;
                try
                {
                    WriteNew(temporary, data);
                    File.Move(temporary, target);
                    samples.Add(entry);
                    WriteNew(manifestTemporary, Encoding.UTF8.GetBytes(JsonUtility.ToJson(new PlayerDataManifest { samples = samples.ToArray() }, true)));
                    CheckLinks(ManifestPath);
                    if (File.Exists(ManifestPath)) File.Replace(manifestTemporary, ManifestPath, null);
                    else File.Move(manifestTemporary, ManifestPath);
                    published = true;
                    return DataResult<SampleMetadata>.Ok(entry);
                }
                finally
                {
                    // Failed writes are never advertised as usable samples.
                    Cleanup(temporary); Cleanup(manifestTemporary);
                    if (!published) Cleanup(target);
                }
            });
        }
        public DataResult<LoadedSample> Load(string id)
        {
            lock (gate) return Guard(() =>
            {
                if (!PlayerDataValidation.ValidId(id)) return DataResult<LoadedSample>.Fail(DataError.InvalidData, "IDが不正です。");
                var manifest = ReadManifestInternal();
                if (!manifest.Success) return DataResult<LoadedSample>.Fail(manifest.Error, manifest.Message);
                var entry = Array.Find(manifest.Value.samples, sample => sample.id == id);
                if (entry == null) return DataResult<LoadedSample>.Fail(DataError.Missing, "指定したデータがありません。");
                string path = Path.Combine(RootPath, entry.relativePath);
                CheckLinks(path);
                if (!File.Exists(path)) return DataResult<LoadedSample>.Fail(DataError.Missing, "データファイルがありません。");
                if (new FileInfo(path).Length != entry.byteCount) return DataResult<LoadedSample>.Fail(DataError.Corrupt, "データの長さが一致しません。");
                byte[] bytes = File.ReadAllBytes(path);
                if (PlayerDataValidation.Hash(bytes) != entry.sha256 || !PlayerDataValidation.Payload(entry, bytes))
                    return DataResult<LoadedSample>.Fail(DataError.Corrupt, "データの整合性を確認できません。");
                return DataResult<LoadedSample>.Ok(new LoadedSample(entry, bytes));
            });
        }
        public DataResult<bool> DeletePlayerData()
        {
            lock (gate) return Guard(() =>
            {
                CheckLinks(RootPath);
                if (!Directory.Exists(RootPath)) return DataResult<bool>.Ok(true);
                // Do not follow junctions/symlinks into StoryProgress or any other directory.
                CheckTree(RootPath);
                Directory.Delete(RootPath, true);
                return DataResult<bool>.Ok(true);
            });
        }
        private static void WriteNew(string path, byte[] bytes)
        {
            CheckLinks(path);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        }
        private static void CheckLinks(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("PlayerData内ではリンクを使用できません。");
                current = Path.GetDirectoryName(current);
            }
        }
        private static void CheckTree(string directory)
        {
            foreach (string path in Directory.GetFileSystemEntries(directory))
            {
                CheckLinks(path);
                if (Directory.Exists(path)) CheckTree(path);
            }
        }
        private static void Cleanup(string path)
        {
            // A cleanup failure is reported by Guard as an I/O failure, never hidden.
            if (File.Exists(path)) { CheckLinks(path); File.Delete(path); }
        }
        private static DataResult<T> Guard<T>(Func<DataResult<T>> operation)
        {
            try { return operation(); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is SecurityException)
            { return DataResult<T>.Fail(DataError.Io, "PlayerDataへのアクセスに失敗しました。空き容量・権限・リンク設定を確認してください。"); }
        }
    }
}
