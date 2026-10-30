using System.Collections.Generic;
using UnityEngine;

/// <summary>프로토타입 공용: 색상 팔레트, 머티리얼 캐시, 프리미티브/텍스트 생성 헬퍼.</summary>
public static class Palette
{
    public static readonly Color CatBlack = new Color(0.10f, 0.10f, 0.13f);

    // 검정 → 회색 → 크림 → 핑크 → 하늘 (컨셉 이미지 '메인 캐릭터' 색 변화)
    public static readonly Color[] ProgressColors =
    {
        new Color(0.10f, 0.10f, 0.13f),
        new Color(0.55f, 0.55f, 0.60f),
        new Color(1.00f, 0.93f, 0.78f),
        new Color(1.00f, 0.72f, 0.86f),
        new Color(0.55f, 0.80f, 1.00f),
    };

    public static readonly Color[] KittenColors =
    {
        new Color(0.78f, 0.66f, 0.98f),
        new Color(1.00f, 0.72f, 0.84f),
        new Color(1.00f, 0.93f, 0.72f),
        new Color(0.66f, 0.92f, 0.80f),
        new Color(0.62f, 0.82f, 1.00f),
        new Color(1.00f, 0.80f, 0.62f),
    };

    // 따라오는 고양이용 파스텔 팔레트 (게임 분위기: 솜사탕 핑크·라벤더·크림·하늘·민트)
    public static readonly Color[] KittenPastels =
    {
        new Color(1.00f, 0.74f, 0.84f),   // 솜사탕 핑크
        new Color(0.98f, 0.62f, 0.74f),   // 딸기우유
        new Color(0.80f, 0.70f, 0.98f),   // 라벤더
        new Color(0.68f, 0.62f, 0.92f),   // 포도젤리
        new Color(0.66f, 0.82f, 1.00f),   // 하늘
        new Color(0.62f, 0.90f, 0.86f),   // 민트
        new Color(0.76f, 0.92f, 0.70f),   // 연두
        new Color(1.00f, 0.92f, 0.70f),   // 바닐라 크림
        new Color(1.00f, 0.80f, 0.64f),   // 복숭아
        new Color(0.96f, 0.86f, 0.78f),   // 밀크티
        new Color(0.86f, 0.84f, 0.90f),   // 연회색
    };
    static int lastKittenIdx = -1;

    /// <summary>따라오는 고양이 랜덤 색: 파스텔 팔레트에서 직전과 다른 색을 고르고 살짝 흔들어 같은 색끼리도 조금씩 다르게.</summary>
    public static Color RandomKittenColor()
    {
        int i = Random.Range(0, KittenPastels.Length);
        if (i == lastKittenIdx) i = (i + Random.Range(1, KittenPastels.Length)) % KittenPastels.Length;
        lastKittenIdx = i;
        Color.RGBToHSV(KittenPastels[i], out float h, out float s, out float v);
        h = Mathf.Repeat(h + Random.Range(-0.02f, 0.02f), 1f);
        s = Mathf.Clamp01(s + Random.Range(-0.05f, 0.05f));
        v = Mathf.Clamp01(v + Random.Range(-0.03f, 0.02f));
        return Color.HSVToRGB(h, s, v);
    }

    // 먹은 음식 종류별 새끼 고양이 색 (파스텔)
    public static readonly Color[] BreadPastels =   // 빵: 갈색·주황·노랑
    {
        new Color(0.84f, 0.68f, 0.54f),   // 밀크 브라운
        new Color(1.00f, 0.76f, 0.55f),   // 살구 주황
        new Color(1.00f, 0.91f, 0.60f),   // 버터 노랑
    };
    public static readonly Color[] SnackPastels =   // 과자: 분홍·하늘·보라
    {
        new Color(1.00f, 0.72f, 0.84f),   // 솜사탕 분홍
        new Color(0.66f, 0.84f, 1.00f),   // 하늘
        new Color(0.79f, 0.69f, 0.98f),   // 라벤더 보라
    };
    public static readonly Color[] FruitPastels =   // 과일: 초록·파랑·빨강
    {
        new Color(0.68f, 0.90f, 0.62f),   // 청사과 초록
        new Color(0.56f, 0.70f, 0.98f),   // 블루베리 파랑
        new Color(0.98f, 0.60f, 0.62f),   // 딸기 빨강
    };
    static readonly int[] lastPick = { -1, -1, -1 };

    /// <summary>먹은 음식 종류에 맞는 파스텔 색 중 하나를 랜덤으로 (같은 계열에서 직전과 다른 색, 톤은 살짝 흔듦).</summary>
    public static Color KittenColorFor(FoodType type)
    {
        Color[] set; int slot;
        switch (type)
        {
            case FoodType.Toast:
            case FoodType.Croissant: set = BreadPastels; slot = 0; break;
            case FoodType.Cookie:
            case FoodType.Candy: set = SnackPastels; slot = 1; break;
            case FoodType.Apple:
            case FoodType.Orange: set = FruitPastels; slot = 2; break;
            default: return RandomKittenColor();   // 마지막 조각 등 분류 없는 음식
        }
        int i = Random.Range(0, set.Length);
        if (i == lastPick[slot]) i = (i + Random.Range(1, set.Length)) % set.Length;
        lastPick[slot] = i;
        return Jitter(set[i]);
    }

    static Color Jitter(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        h = Mathf.Repeat(h + Random.Range(-0.015f, 0.015f), 1f);
        s = Mathf.Clamp01(s + Random.Range(-0.05f, 0.05f));
        v = Mathf.Clamp01(v + Random.Range(-0.03f, 0.02f));
        return Color.HSVToRGB(h, s, v);
    }

    public static Color EvaluateProgress(float t)
    {
        t = Mathf.Clamp01(t) * (ProgressColors.Length - 1);
        int i = Mathf.Min(Mathf.FloorToInt(t), ProgressColors.Length - 2);
        return Color.Lerp(ProgressColors[i], ProgressColors[i + 1], t - i);
    }

    // ---------------- Materials ----------------
    static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
    public static IEnumerable<Material> CachedMaterials => cache.Values;
    public static void ClearCache() => cache.Clear();

    static Shader lit;
    static Shader Lit
    {
        get
        {
            if (lit == null)
            {
                lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) lit = Shader.Find("Standard");
            }
            return lit;
        }
    }

    public static Material Get(Color c, float emission = 0f, float smoothness = 0.3f)
    {
        string key = ColorUtility.ToHtmlStringRGBA(c) + "_e" + Mathf.RoundToInt(emission * 100) + "_s" + Mathf.RoundToInt(smoothness * 100);
        if (cache.TryGetValue(key, out var m) && m != null) return m;
        m = new Material(Lit) { name = "M_" + key };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        cache[key] = m;
        return m;
    }

    // ---------------- Shapes ----------------
    public static GameObject Shape(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color,
        bool collider = false, float emission = 0f, Vector3? euler = null, string name = null)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name ?? type.ToString();
        if (parent) g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = localScale;
        if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
        g.GetComponent<Renderer>().sharedMaterial = Get(color, emission);

        var col = g.GetComponent<Collider>();
        if (!collider)
        {
            RemoveCollider(col);
        }
        else if (type == PrimitiveType.Cylinder)
        {
            // 기본 캡슐 콜라이더 대신 볼록 메시 콜라이더 (납작한 원기둥도 정확하게)
            RemoveCollider(col);
            var mc = g.AddComponent<MeshCollider>();
            mc.convex = true;
        }
        return g;
    }

    // 플레이 중(물리 콜백 안 포함)에는 DestroyImmediate 불가 → 즉시 비활성화 후 Destroy
    static void RemoveCollider(Collider col)
    {
        if (!col) return;
        if (Application.isPlaying) { col.enabled = false; Object.Destroy(col); }
        else Object.DestroyImmediate(col);
    }

    /// <summary>XY 평면 위 5각 별 (로컬 -Z 방향이 앞면).</summary>
    public static Transform Star(Transform parent, Vector3 localPos, float size, Color color, float emission)
    {
        var root = new GameObject("Star").transform;
        root.SetParent(parent, false);
        root.localPosition = localPos;
        for (int k = 0; k < 5; k++)
        {
            var rot = Quaternion.Euler(0, 0, k * 72f);
            var arm = new GameObject("Arm").transform;
            arm.SetParent(root, false);
            arm.localRotation = rot;
            arm.localPosition = rot * Vector3.up * 0.33f * size;
            arm.localScale = new Vector3(0.42f, 0.95f, 0.3f) * size;
            Shape(PrimitiveType.Cube, arm, Vector3.zero, new Vector3(0.7f, 0.7f, 1f), color, false, emission, new Vector3(0, 0, 45), "Diamond");
        }
        Shape(PrimitiveType.Sphere, root, Vector3.zero, new Vector3(0.5f, 0.5f, 0.3f) * size, color, false, emission, null, "Core");
        return root;
    }

    // ---------------- Text ----------------
    static Font font;
    public static Font UIFont
    {
        get
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    /// <summary>3D TextMesh. height = 대략적인 글자 높이(월드 단위).</summary>
    public static TextMesh Text(Transform parent, string text, Vector3 localPos, float height, Color color, int fontSize = 96)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var tm = go.AddComponent<TextMesh>();
        tm.font = UIFont;
        tm.text = text;
        tm.fontSize = fontSize;
        tm.characterSize = height / (fontSize * 0.1f);
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = color;
        go.GetComponent<MeshRenderer>().sharedMaterial = UIFont.material;
        return tm;
    }
}

public static class Ease
{
    public static float OutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        x = Mathf.Clamp01(x);
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
