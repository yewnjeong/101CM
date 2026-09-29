using UnityEngine;

namespace CM101.Level
{
    public enum HideoutKind { Practice, Small, Long }

    /// <summary>
    /// 은신처. path는 내부 경로(월드 좌표, 입구 → 안쪽/반대 입구)이며 길이를 레벨 규격과 비교한다.
    /// 작은 은신처 3.8u 이상(30cm 꼬리 2.8u + 1u), 긴 은신 통로 12.7u 이상(100cm 꼬리 11.7u + 1u).
    /// </summary>
    public class HideoutMarker : MonoBehaviour
    {
        public string id;
        public ZoneId zone;
        public HideoutKind kind;
        [Tooltip("입구 → 끝. 양쪽이 뚫린 통로면 twoWay")] public Vector3[] path;
        public bool twoWay;
        [Tooltip("평면 차지 영역 (순찰 경로 검사용)")] public Vector3 footprintCenter;
        public Vector3 footprintSize;

        public int CapacityCm => kind == HideoutKind.Long ? 100 : 30;

        public float RequiredLength => kind == HideoutKind.Long ? 12.7f : kind == HideoutKind.Small ? 3.8f : 0f;

        public float PathLength
        {
            get
            {
                if (path == null) return 0f;
                float l = 0f;
                for (int i = 1; i < path.Length; i++) l += Vector3.Distance(path[i - 1], path[i]);
                return l;
            }
        }

        public Bounds Footprint => new Bounds(footprintCenter, footprintSize);

        void OnDrawGizmos()
        {
            Gizmos.color = kind == HideoutKind.Long ? new Color(0.6f, 0.4f, 1f) : new Color(0.4f, 0.8f, 1f);
            if (path != null)
                for (int i = 1; i < path.Length; i++) Gizmos.DrawLine(path[i - 1] + Vector3.up * 0.3f, path[i] + Vector3.up * 0.3f);
            Gizmos.DrawWireCube(footprintCenter, footprintSize);
        }
    }
}
