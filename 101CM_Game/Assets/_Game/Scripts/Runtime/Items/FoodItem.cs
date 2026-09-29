using System.Collections.Generic;
using UnityEngine;

namespace CM101
{
    /// <summary>일반 음식. Space 입력으로 소비한다. 접촉 자동 섭취 없음.</summary>
    public class FoodItem : MonoBehaviour
    {
        public static readonly List<FoodItem> All = new List<FoodItem>();
        static FoodItem highlighted;

        public FoodType type;
        public int variant;
        public bool isReplacement;
        /// <summary>창고 튜토리얼 전용 음식. 90cm 총량 계산에서 빠지고 튜토리얼이 끝나면 사라진다.</summary>
        public bool isTutorial;

        public FoodType Type => type;
        public int Variant => variant;
        public int Length => FoodData.Length(type);
        public bool Consumed { get; private set; }
        public float SpawnTime { get; private set; }

        Transform visual;
        GameObject ring;
        float phase;

        public static int RemainingLength
        {
            get
            {
                int s = 0;
                foreach (var f in All) if (f && !f.Consumed && !f.isTutorial) s += f.Length;
                return s;
            }
        }

        void Awake()
        {
            RefreshRefs();
            phase = Random.value * 6.28f;
            SpawnTime = Time.time;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDestroy()
        {
            All.Remove(this);
            if (highlighted == this) highlighted = null;
        }

        public void RefreshRefs()
        {
            visual = transform.Find("Visual");
            var r = transform.Find("Ring");
            ring = r ? r.gameObject : null;
            if (ring) ring.SetActive(highlighted == this);
        }

        void Update()
        {
            if (visual)
            {
                visual.localPosition = Vector3.up * (Mathf.Sin(Time.time * 2f + phase) * 0.04f);
                visual.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
            }
            if (ring && ring.activeSelf)
            {
                float s = 1f + Mathf.Sin(Time.time * 8f) * 0.08f;
                ring.transform.localScale = new Vector3(0.75f * s, 0.005f, 0.75f * s);
            }
        }

        public static void SetHighlighted(FoodItem f)
        {
            if (highlighted == f) return;
            if (highlighted && highlighted.ring) highlighted.ring.SetActive(false);
            highlighted = f;
            if (f && f.ring) f.ring.SetActive(true);
        }

        public bool TryConsume()
        {
            if (Consumed) return false;
            Consumed = true;
            if (highlighted == this) SetHighlighted(null);
            Destroy(gameObject);
            return true;
        }

        public static FoodItem Create(FoodType t, int variant, Vector3 groundPos, Transform parent = null)
        {
            var go = new GameObject($"Food_{t}_{FoodData.Length(t)}cm");
            go.layer = Layers.Food;
            if (parent) go.transform.SetParent(parent, false);
            go.transform.position = groundPos + Vector3.up * 0.25f;

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.28f;

            var f = go.AddComponent<FoodItem>();
            f.type = t;
            f.variant = variant;

            ProtoFactory.BuildFood(go.transform, t, variant);
            var ring = ProtoFactory.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0f, -0.23f, 0f), new Vector3(0.75f, 0.005f, 0.75f),
                ProtoFactory.Mat(new Color(1f, 0.9f, 0.3f), 0.1f, 1.2f), "Ring");
            ring.SetActive(false);
            f.RefreshRefs();
            return f;
        }
    }
}
