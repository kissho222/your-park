using System;
using System.Collections.Generic;

namespace YourPark.PlayerData
{
    // Explicitly owned by one running application. No static cache, PlayerPrefs or automatic re-save.
    public sealed class PlayerDataSession : IDisposable
    {
        private readonly IPlayerDataStore store;
        private readonly Dictionary<string, LoadedSample> loaded = new Dictionary<string, LoadedSample>();
        private const long MaxSessionBytes = 64L * 1024 * 1024;
        private long loadedBytes;
        private bool disposed;
        public int LoadedCount => loaded.Count;
        public PlayerDataSession(IPlayerDataStore store) { this.store = store ?? throw new ArgumentNullException(nameof(store)); }
        public DataResult<LoadedSample> Load(string id)
        {
            if (disposed) return DataResult<LoadedSample>.Fail(DataError.Unavailable, "セッションは終了しています。");
            if (id == null) return DataResult<LoadedSample>.Fail(DataError.InvalidData, "IDが必要です。");
            if (loaded.TryGetValue(id, out var existing)) return DataResult<LoadedSample>.Ok(existing);
            var result = store.Load(id);
            if (result.Success)
            {
                if (loadedBytes + result.Value.Bytes.Length > MaxSessionBytes)
                    return DataResult<LoadedSample>.Fail(DataError.Unavailable, "RAMロード上限です。セッションを終了してください。");
                loaded.Add(id, result.Value); loadedBytes += result.Value.Bytes.Length;
            }
            return result;
        }
        // Deleting disk data intentionally leaves already-loaded snapshots available.
        public DataResult<bool> DeleteDiskData() => disposed
            ? DataResult<bool>.Fail(DataError.Unavailable, "セッションは終了しています。") : store.DeletePlayerData();
        public void Dispose()
        {
            if (disposed) return;
            foreach (var sample in loaded.Values) Array.Clear(sample.Bytes, 0, sample.Bytes.Length);
            loaded.Clear(); loadedBytes = 0; disposed = true;
        }
    }
}
