using UnityEngine;

namespace CM101.Level
{
    public enum ZoneId { Z0, Z1, Z2, Z3, Z4, Z5, Z6 }

    /// <summary>구역 범위(평면 사각형)와 목표 수치 (레벨 문서 1.1 구역 요약, 밸런스 시트 '구역').</summary>
    public class ZoneVolume : MonoBehaviour
    {
        public ZoneId zone;
        public string displayName;
        public float xMin;
        public float xMax;
        public float zMin;
        public float zMax;
        [Tooltip("목표 플레이 시간 (분)")] public float targetMinutes;
        [Tooltip("구역 음식 합계 목표 (cm). 창고는 튜토리얼 전용")] public int foodTargetCm;
        public int entryCm;
        public int exitCm;
        public Color color = Color.white;

        public bool Contains(Vector3 p) => p.x >= xMin && p.x < xMax && p.z >= zMin && p.z < zMax;
        public Vector3 Center => new Vector3((xMin + xMax) * 0.5f, 0f, (zMin + zMax) * 0.5f);

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.6f);
            Gizmos.DrawWireCube(new Vector3((xMin + xMax) * 0.5f, 1.5f, (zMin + zMax) * 0.5f), new Vector3(xMax - xMin, 3f, zMax - zMin));
        }
    }
}
