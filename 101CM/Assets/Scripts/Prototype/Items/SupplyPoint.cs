using System.Collections.Generic;
using UnityEngine;

namespace CM101
{
    /// <summary>
    /// 젤리가 소멸하면 같은 증가량의 음식을 생성하는 지정 위치(기획서 04장).
    /// 프로토타입은 직선거리 기준 가장 가까운 지점을 쓴다. Alpha에서 구역 연결 기준으로 바꾼다.
    /// </summary>
    public class SupplyPoint : MonoBehaviour
    {
        public static readonly List<SupplyPoint> All = new List<SupplyPoint>();

        public string label = "보충 지점";
        int spawnCount;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static SupplyPoint Nearest(Vector3 p)
        {
            SupplyPoint best = null;
            float bd = float.MaxValue;
            foreach (var s in All)
            {
                if (!s) continue;
                float d = (s.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        public Vector3 NextSpawnPosition()
        {
            float ang = spawnCount * 2.4f;
            float r = spawnCount == 0 ? 0f : Mathf.Min(1.4f, 0.45f + 0.12f * spawnCount);
            spawnCount++;
            return transform.position + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.2f, 0.5f);
        }
    }
}
