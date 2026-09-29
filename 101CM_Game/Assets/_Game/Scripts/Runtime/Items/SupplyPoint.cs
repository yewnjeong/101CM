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

        /// <summary>보충 음식 위치. 한곳에 몰리지 않게 반경 1~3 안에서 비어 있는 바닥을 고른다.</summary>
        public Vector3 NextSpawnPosition()
        {
            spawnCount++;
            for (int i = 0; i < 12; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(1f, 3f);
                Vector3 p = transform.position + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 1f, Layers.EnvMask, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.CheckSphere(hit.point + Vector3.up * 0.3f, 0.28f, Layers.EnvMask | Layers.HazardMask, QueryTriggerInteraction.Ignore)) continue;
                bool crowded = false;
                foreach (var f in FoodItem.All)
                    if (f && !f.Consumed && (f.transform.position - hit.point).sqrMagnitude < 0.8f * 0.8f) { crowded = true; break; }
                if (crowded) continue;
                return hit.point;
            }
            return transform.position;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.2f, 0.5f);
        }
    }
}
