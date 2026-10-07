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
        /// <summary>모양(20종). None이면 예전 프로토 모양.</summary>
        public FoodKind kind;
        /// <summary>0보다 크면 이 길이(cm)를 쓴다 (자리별 cm). 0이면 FoodType 기본값.</summary>
        public int cmOverride;

        public FoodType Type => type;
        public int Variant => variant;
        public FoodKind Kind => kind;
        public int Length => cmOverride > 0 ? cmOverride : FoodData.Length(type);
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
            if (Consumed) return; // 먹기 모션 중에는 둥실·회전 멈춤
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
            var col = GetComponent<Collider>();
            if (col) col.enabled = false;
            if (visual && isActiveAndEnabled) StartCoroutine(EatPop());
            else Destroy(gameObject);
            return true;
        }

        // ── 먹기 모션: 살짝 눌렸다가 통 튀며 작아져 사라진다 (길이는 먹는 즉시 반영, 이 모션과 무관) ──
        public static float squashTime = 0.06f;
        public static float popTime = 0.12f;

        System.Collections.IEnumerator EatPop()
        {
            Vector3 baseScale = visual.localScale;
            Vector3 basePos = visual.localPosition;
            const float bottom = 0.2f; // Visual 중심에서 음식 바닥까지 대략 거리
            float t = 0f;
            while (t < squashTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / squashTime);
                float e = 1f - (1f - k) * (1f - k);
                float sy = Mathf.Lerp(1f, 0.65f, e), sx = Mathf.Lerp(1f, 1.2f, e);
                visual.localScale = Vector3.Scale(baseScale, new Vector3(sx, sy, sx));
                visual.localPosition = basePos - Vector3.up * (1f - sy) * bottom;
                yield return null;
            }
            t = 0f;
            while (t < popTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / popTime);
                float st = Mathf.Sin(Mathf.Min(k * 2f, 1f) * Mathf.PI * 0.5f);
                float sx = Mathf.Lerp(1.2f, 0.85f, st), sy = Mathf.Lerp(0.65f, 1.3f, st);
                float shrink = 1f - k * k;
                visual.localScale = Vector3.Scale(baseScale, new Vector3(sx, sy, sx) * shrink);
                visual.localPosition = basePos - Vector3.up * (1f - sy) * bottom + Vector3.up * 0.2f * Mathf.Sin(k * Mathf.PI * 0.5f);
                yield return null;
            }
            Destroy(gameObject);
        }

        /// <summary>모양·길이를 지정해 만든다. 모양이 있으면 프로토 모양 대신 실제 음식 모델을 붙인다.</summary>
        public static FoodItem Create(FoodType t, int variant, Vector3 groundPos, FoodKind kind, int cm, Transform parent = null)
        {
            var f = Create(t, variant, groundPos, parent);
            f.kind = kind;
            f.cmOverride = cm;
            if (kind != FoodKind.None)
            {
                f.name = $"Food_{kind}_{f.Length}cm";
                FoodArt.ApplyFloorVisual(f);
            }
            return f;
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
