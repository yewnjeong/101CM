using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CM101
{
    /// <summary>
    /// 기본 도형으로 곰 젤리/음식 외형을 만드는 팩토리. 런타임과 에디터(씬 빌더)가 함께 쓴다.
    /// 에디터에서는 PersistHook으로 머티리얼을 에셋으로 저장해 씬 참조가 끊기지 않게 한다.
    /// </summary>
    public static class ProtoFactory
    {
        public static System.Func<Material, Material> PersistHook;

        static Material baseMat;
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        static PhysicsMaterial jellyPhys;

        public static PhysicsMaterial JellyPhysics
        {
            get
            {
                if (jellyPhys == null)
                {
                    jellyPhys = new PhysicsMaterial("JellyPhys")
                    {
                        dynamicFriction = 0f,
                        staticFriction = 0f,
                        bounciness = 0.05f,
                        frictionCombine = PhysicsMaterialCombine.Minimum,
                        bounceCombine = PhysicsMaterialCombine.Average
                    };
                }
                return jellyPhys;
            }
        }

        public static void ClearCache()
        {
            Cache.Clear();
            baseMat = null;
        }

        static Material Base()
        {
            if (baseMat == null) baseMat = Resources.Load<Material>("CM101/ProtoLit");
            return baseMat;
        }

        public static Material Mat(Color c, float smoothness = 0.3f, float emission = 0f)
        {
            string key = ColorUtility.ToHtmlStringRGBA(c) + "_" + smoothness.ToString("0.00") + "_" + emission.ToString("0.00");
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var b = Base();
            var m = b != null ? new Material(b) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = "M_" + key;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smoothness);
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (PersistHook != null) m = PersistHook(m);
            Cache[key] = m;
            return m;
        }

        /// <summary>충돌체 없는 시각용 기본 도형.</summary>
        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, string name = null)
        {
            var g = GameObject.CreatePrimitive(type);
            if (name != null) g.name = name;
            var col = g.GetComponent<Collider>();
            if (col) Object.DestroyImmediate(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = localScale;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
            g.layer = parent ? parent.gameObject.layer : 0;
            return g;
        }

        // ------------------------------------------------------------------ 곰 젤리

        /// <summary>
        /// 곰 젤리 외형. s = 몸 지름(충돌 구 지름과 같음). 두 다리는 붙이지 않고 사이를 띄운다(기획서 08장).
        /// </summary>
        public static Transform BuildBear(Transform root, float s, Color bodyColor, bool isLeader)
        {
            var vis = new GameObject("Visual").transform;
            vis.SetParent(root, false);
            vis.gameObject.layer = root.gameObject.layer;

            var body = Mat(bodyColor, 0.88f);
            var belly = Mat(Color.Lerp(bodyColor, Color.white, 0.35f), 0.88f);
            var dark = Mat(new Color(0.12f, 0.08f, 0.08f), 0.6f);

            Prim(PrimitiveType.Sphere, vis, new Vector3(0f, -0.08f, 0f) * s, new Vector3(0.78f, 0.7f, 0.68f) * s, body, "Body");
            Prim(PrimitiveType.Sphere, vis, new Vector3(0f, -0.1f, 0.2f) * s, new Vector3(0.48f, 0.42f, 0.34f) * s, belly, "Belly");
            Prim(PrimitiveType.Sphere, vis, new Vector3(0f, 0.33f, 0.02f) * s, new Vector3(0.6f, 0.55f, 0.55f) * s, body, "Head");
            Prim(PrimitiveType.Sphere, vis, new Vector3(-0.21f, 0.57f, 0f) * s, Vector3.one * 0.22f * s, body, "EarL");
            Prim(PrimitiveType.Sphere, vis, new Vector3(0.21f, 0.57f, 0f) * s, Vector3.one * 0.22f * s, body, "EarR");
            Prim(PrimitiveType.Sphere, vis, new Vector3(-0.11f, 0.38f, 0.26f) * s, Vector3.one * 0.07f * s, dark, "EyeL");
            Prim(PrimitiveType.Sphere, vis, new Vector3(0.11f, 0.38f, 0.26f) * s, Vector3.one * 0.07f * s, dark, "EyeR");
            Prim(PrimitiveType.Sphere, vis, new Vector3(0f, 0.29f, 0.28f) * s, new Vector3(0.16f, 0.11f, 0.1f) * s, belly, "Muzzle");
            Prim(PrimitiveType.Sphere, vis, new Vector3(-0.37f, -0.02f, 0.05f) * s, new Vector3(0.18f, 0.26f, 0.18f) * s, body, "ArmL");
            Prim(PrimitiveType.Sphere, vis, new Vector3(0.37f, -0.02f, 0.05f) * s, new Vector3(0.18f, 0.26f, 0.18f) * s, body, "ArmR");
            // 다리 사이 간격: 중심 0.34s, 폭 0.24s → 0.1s 틈
            Prim(PrimitiveType.Sphere, vis, new Vector3(-0.17f, -0.4f, 0.03f) * s, new Vector3(0.24f, 0.22f, 0.26f) * s, body, "LegL");
            Prim(PrimitiveType.Sphere, vis, new Vector3(0.17f, -0.4f, 0.03f) * s, new Vector3(0.24f, 0.22f, 0.26f) * s, body, "LegR");

            if (isLeader)
            {
                // 선두 전용 표시: 발밑 링
                Prim(PrimitiveType.Cylinder, vis, new Vector3(0f, -0.49f, 0f) * s, new Vector3(1.25f, 0.012f, 1.25f) * s,
                    Mat(new Color(1f, 0.95f, 0.45f), 0.1f, 1.0f), "LeaderRing");
            }
            return vis;
        }

        /// <summary>먹은 음식 특징을 공통 곰 몸체에 더한다(색 + 작은 토핑).</summary>
        public static void AddTopping(Transform vis, FoodType t, float s)
        {
            switch (t)
            {
                case FoodType.Fruit:
                {
                    var leaf = Prim(PrimitiveType.Cube, vis, new Vector3(0.04f, 0.66f, 0f) * s, new Vector3(0.2f, 0.04f, 0.1f) * s,
                        Mat(new Color(0.3f, 0.75f, 0.25f), 0.5f), "Leaf");
                    leaf.transform.localRotation = Quaternion.Euler(0f, 30f, 20f);
                    break;
                }
                case FoodType.Bread:
                {
                    var m = Mat(new Color(0.72f, 0.45f, 0.2f), 0.4f);
                    Prim(PrimitiveType.Sphere, vis, new Vector3(-0.1f, 0.6f, 0.05f) * s, Vector3.one * 0.1f * s, m, "Crumb1");
                    Prim(PrimitiveType.Sphere, vis, new Vector3(0.08f, 0.62f, -0.03f) * s, Vector3.one * 0.09f * s, m, "Crumb2");
                    Prim(PrimitiveType.Sphere, vis, new Vector3(0f, 0.63f, 0.1f) * s, Vector3.one * 0.08f * s, m, "Crumb3");
                    break;
                }
                case FoodType.Cheese:
                {
                    var m = Mat(new Color(0.85f, 0.65f, 0.1f), 0.5f);
                    Prim(PrimitiveType.Sphere, vis, new Vector3(-0.3f, 0.0f, 0.12f) * s, Vector3.one * 0.1f * s, m, "Hole1");
                    Prim(PrimitiveType.Sphere, vis, new Vector3(0.28f, -0.15f, 0.15f) * s, Vector3.one * 0.08f * s, m, "Hole2");
                    Prim(PrimitiveType.Sphere, vis, new Vector3(0.1f, 0.42f, 0.2f) * s, Vector3.one * 0.07f * s, m, "Hole3");
                    break;
                }
                case FoodType.Cookie:
                {
                    var m = Mat(new Color(0.25f, 0.13f, 0.07f), 0.3f);
                    Prim(PrimitiveType.Sphere, vis, new Vector3(-0.2f, 0.05f, 0.24f) * s, Vector3.one * 0.08f * s, m, "Chip1");
                    Prim(PrimitiveType.Sphere, vis, new Vector3(0.18f, -0.15f, 0.25f) * s, Vector3.one * 0.08f * s, m, "Chip2");
                    Prim(PrimitiveType.Sphere, vis, new Vector3(0.3f, 0.1f, 0.05f) * s, Vector3.one * 0.07f * s, m, "Chip3");
                    Prim(PrimitiveType.Sphere, vis, new Vector3(-0.1f, 0.55f, 0.05f) * s, Vector3.one * 0.07f * s, m, "Chip4");
                    break;
                }
                default:
                {
                    Prim(PrimitiveType.Sphere, vis, new Vector3(0f, 0.62f, 0f) * s, new Vector3(0.3f, 0.12f, 0.3f) * s,
                        Mat(Color.white, 0.6f), "Cream");
                    Prim(PrimitiveType.Sphere, vis, new Vector3(0f, 0.72f, 0f) * s, Vector3.one * 0.15f * s,
                        Mat(new Color(0.95f, 0.15f, 0.2f), 0.7f), "Strawberry");
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ 음식

        public static Transform BuildFood(Transform root, FoodType t, int variant)
        {
            var vis = new GameObject("Visual").transform;
            vis.SetParent(root, false);
            vis.gameObject.layer = root.gameObject.layer;
            Color c = FoodData.FoodColor(t, variant);

            switch (t)
            {
                case FoodType.Fruit:
                    Prim(PrimitiveType.Sphere, vis, Vector3.zero, Vector3.one * 0.36f, Mat(c, 0.7f), "Fruit");
                    Prim(PrimitiveType.Cube, vis, new Vector3(0.05f, 0.19f, 0f), new Vector3(0.14f, 0.03f, 0.07f),
                        Mat(new Color(0.3f, 0.75f, 0.25f), 0.4f), "Leaf").transform.localRotation = Quaternion.Euler(0, 30, 20);
                    break;
                case FoodType.Bread:
                {
                    var loaf = Prim(PrimitiveType.Capsule, vis, Vector3.zero, new Vector3(0.28f, 0.22f, 0.28f), Mat(c, 0.25f), "Loaf");
                    loaf.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Prim(PrimitiveType.Cube, vis, new Vector3(0f, 0.1f, 0f), new Vector3(0.36f, 0.05f, 0.2f),
                        Mat(new Color(0.72f, 0.45f, 0.2f), 0.3f), "Crust");
                    break;
                }
                case FoodType.Cheese:
                {
                    Prim(PrimitiveType.Cube, vis, Vector3.zero, new Vector3(0.42f, 0.28f, 0.32f), Mat(c, 0.35f), "Cheese");
                    var hole = Mat(new Color(0.85f, 0.65f, 0.1f), 0.4f);
                    Prim(PrimitiveType.Sphere, vis, new Vector3(-0.1f, 0.05f, 0.16f), Vector3.one * 0.08f, hole, "Hole1");
                    Prim(PrimitiveType.Sphere, vis, new Vector3(0.1f, -0.06f, 0.16f), Vector3.one * 0.06f, hole, "Hole2");
                    break;
                }
                case FoodType.Cookie:
                {
                    Prim(PrimitiveType.Cylinder, vis, Vector3.zero, new Vector3(0.44f, 0.05f, 0.44f), Mat(c, 0.2f), "Cookie");
                    var chip = Mat(new Color(0.25f, 0.13f, 0.07f), 0.3f);
                    Prim(PrimitiveType.Sphere, vis, new Vector3(-0.1f, 0.05f, 0.05f), Vector3.one * 0.07f, chip, "Chip1");
                    Prim(PrimitiveType.Sphere, vis, new Vector3(0.08f, 0.05f, -0.08f), Vector3.one * 0.07f, chip, "Chip2");
                    Prim(PrimitiveType.Sphere, vis, new Vector3(0.1f, 0.05f, 0.1f), Vector3.one * 0.06f, chip, "Chip3");
                    break;
                }
                default:
                {
                    Prim(PrimitiveType.Cube, vis, new Vector3(0f, -0.05f, 0f), new Vector3(0.44f, 0.3f, 0.44f), Mat(c, 0.4f), "Sponge");
                    Prim(PrimitiveType.Cube, vis, new Vector3(0f, -0.05f, 0f), new Vector3(0.45f, 0.06f, 0.45f),
                        Mat(new Color(1f, 0.55f, 0.65f), 0.4f), "Jam");
                    Prim(PrimitiveType.Sphere, vis, new Vector3(0f, 0.16f, 0f), Vector3.one * 0.14f,
                        Mat(new Color(0.95f, 0.15f, 0.2f), 0.7f), "Strawberry");
                    break;
                }
            }
            return vis;
        }
    }
}
