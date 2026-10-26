using UnityEngine;

namespace YourPark.PlayerData
{
    // The dummy landmark objects are authored by the demo scene builder, never created at runtime.
    public sealed class LandmarkMotionReplayer : MonoBehaviour, IMotionReplayer
    {
        [SerializeField] private Transform[] points;
        private PoseClip clip;
        private double started;
        private int cursor;
        public DataResult<bool> Play(PoseClip value)
        {
            if (!PlayerDataValidation.Pose(value)) return DataResult<bool>.Fail(DataError.InvalidData, "MotionClipが不正です。");
            if (points == null || points.Length < value.frames[0].landmarks.Length)
                return DataResult<bool>.Fail(DataError.Unavailable, "表示用ランドマークが不足しています。");
            clip = value; cursor = 0; started = Time.unscaledTimeAsDouble; Apply(value.frames[0]);
            return DataResult<bool>.Ok(true);
        }
        private void Update()
        {
            if (clip == null) return;
            double elapsed = Time.unscaledTimeAsDouble - started;
            while (cursor + 1 < clip.frames.Length && clip.frames[cursor + 1].timestampSeconds <= elapsed) cursor++;
            Apply(clip.frames[cursor]);
            if (elapsed >= clip.frames[clip.frames.Length - 1].timestampSeconds) Stop();
        }
        private void Apply(PoseFrame frame)
        {
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null) continue;
                points[i].gameObject.SetActive(i < frame.landmarks.Length);
                if (i < frame.landmarks.Length) points[i].localPosition = frame.landmarks[i].position;
            }
        }
        public void Stop() { clip = null; cursor = 0; }
        private void OnDisable() => Stop();
    }
}
