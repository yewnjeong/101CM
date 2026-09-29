using UnityEngine;

namespace CM101.Level
{
    public enum HazardKind { RollingFruit, FallingShelf, Staff, Vacuum, Belt, Rollers, Spill, Melt, Ice }

    /// <summary>
    /// 위험 배치 정보(검사 도구용). 실제 동작 컴포넌트와 함께 놓인다.
    /// 경로(path)가 있으면 경로 ± laneWidth/2, 영역(areaSize)이 있으면 그 상자가 위험 영역이다.
    /// </summary>
    public class HazardMarker : MonoBehaviour
    {
        public string id;
        public ZoneId zone;
        public HazardKind kind;
        [Tooltip("이 구역에서 처음 소개하는 위험인가 (레벨 문서 2장)")] public bool isIntro;
        public Vector3[] path;
        public bool loop;
        public float laneWidth = 1.2f;
        public Vector3 areaCenter;
        public Vector3 areaSize;
        [Tooltip("시스템기획서 예고 시간 (초)")] public float warnSeconds;
        public float speed;

        static float DistXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 P = new Vector2(p.x, p.z), A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z);
            Vector2 ab = B - A;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(P - A, ab) / ab.sqrMagnitude);
            return Vector2.Distance(P, A + ab * t);
        }

        /// <summary>평면 거리로 위험 영역까지의 최소 거리 (안이면 0).</summary>
        public float DistanceTo(Vector3 p)
        {
            float best = float.MaxValue;
            if (path != null && path.Length >= 2)
            {
                int n = loop ? path.Length : path.Length - 1;
                for (int i = 0; i < n; i++)
                {
                    float d = DistXZ(p, path[i], path[(i + 1) % path.Length]) - laneWidth * 0.5f;
                    best = Mathf.Min(best, Mathf.Max(0f, d));
                }
            }
            if (areaSize.sqrMagnitude > 0f)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(p.x - areaCenter.x) - areaSize.x * 0.5f);
                float dz = Mathf.Max(0f, Mathf.Abs(p.z - areaCenter.z) - areaSize.z * 0.5f);
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
            }
            return best;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.9f);
            if (path != null && path.Length >= 2)
            {
                int n = loop ? path.Length : path.Length - 1;
                for (int i = 0; i < n; i++) Gizmos.DrawLine(path[i] + Vector3.up * 0.05f, path[(i + 1) % path.Length] + Vector3.up * 0.05f);
            }
            if (areaSize.sqrMagnitude > 0f) Gizmos.DrawWireCube(areaCenter, areaSize);
        }
    }
}
