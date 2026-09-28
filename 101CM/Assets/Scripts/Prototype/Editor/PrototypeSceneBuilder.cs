using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CM101.EditorTools
{
    /// <summary>
    /// 1단계 프로토타입 그레이박스 씬 생성기 v2. 메뉴: 101CM > Build Prototype Scene
    /// 맵 24×76 유닛(1타일 = 1유닛), 5구역:
    ///   창고(팔레트 아래·단상 U자 길·바구니) → 식품 통로(선반 하단 좁은 단·붕괴 매대·엔드캡 바구니·카트 아래·지그재그)
    ///   → 농산물 광장(테이블 아래·직원 발 2개·상자 위·바구니) → 아이스크림(청소기·막다른 틈·지그재그 우회로)
    ///   → 계산대(움직이는 벨트 위 음식·계산대 위 경사로·바구니) → 출구
    /// 창고는 튜토리얼(음식 4개/16cm, 총량 제외). 식품 통로에 들어서면 연습 동료는 사라지고 창고 셔터가 닫힌다.
    /// 본게임 음식 22개, 합계 90cm (3cm×9, 4cm×7, 5cm×3, 6cm×2, 8cm×1).
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Prototype_101CM.unity";
        const string ProtoDir = "Assets/Prototype";
        const string MatDir = "Assets/Prototype/Materials";
        const string GenDir = "Assets/Prototype/Materials/Generated";
        const string ResDir = "Assets/Resources/CM101";

        const float MapW = 24f;
        const float ExitZ = 76f;
        const float MapL = 80f;

        static Transform level;
        static Transform foodRoot;
        static Material signMat;

        static readonly Color CWall = new Color(0.8f, 0.9f, 0.87f);
        static readonly Color CShelf = new Color(0.62f, 0.7f, 0.8f);
        static readonly Color CPlank = new Color(0.9f, 0.92f, 0.95f);
        static readonly Color CCrate = new Color(0.75f, 0.58f, 0.38f);
        static readonly Color CCrateDark = new Color(0.62f, 0.46f, 0.3f);
        static readonly Color CPallet = new Color(0.82f, 0.68f, 0.45f);
        static readonly Color CFreezer = new Color(0.88f, 0.95f, 1f);
        static readonly Color CCounter = new Color(0.6f, 0.42f, 0.28f);
        static readonly Color CMetal = new Color(0.72f, 0.75f, 0.8f);
        static readonly Color CBasket = new Color(0.25f, 0.6f, 0.95f);
        static readonly Color CRamp = new Color(0.86f, 0.76f, 0.46f);
        static readonly Color CSign = new Color(0.3f, 0.4f, 0.45f);

        [MenuItem("101CM/Build Prototype Scene")]
        public static void Build()
        {
            EnsureLayers();
            EnsureFolder(ProtoDir);
            EnsureFolder(MatDir);
            EnsureFolder("Assets/Resources");
            EnsureFolder(ResDir);
            EnsureFolder("Assets/Scenes");
            EnsureBaseMaterial();

            if (AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.DeleteAsset(GenDir);
            EnsureFolder(GenDir);
            ProtoFactory.ClearCache();
            signMat = null;
            ProtoFactory.PersistHook = m =>
            {
                string p = AssetDatabase.GenerateUniqueAssetPath($"{GenDir}/{m.name}.mat");
                AssetDatabase.CreateAsset(m, p);
                return m;
            };

            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                BuildWorld();
                EditorSceneManager.SaveScene(scene, ScenePath);
                AddToBuildSettings(ScenePath);
                Debug.Log($"[101CM] 프로토타입 씬 생성 완료: {ScenePath}");
            }
            finally
            {
                ProtoFactory.PersistHook = null;
                ProtoFactory.ClearCache();
                AssetDatabase.SaveAssets();
            }
        }

        // ------------------------------------------------------------------ 설정

        static void EnsureLayers()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            void Set(int i, string n)
            {
                var p = layers.GetArrayElementAtIndex(i);
                if (p.stringValue == n) return;
                if (!string.IsNullOrEmpty(p.stringValue)) Debug.LogWarning($"[101CM] 레이어 {i} '{p.stringValue}'를 '{n}'으로 바꿉니다.");
                p.stringValue = n;
            }
            Set(Layers.Solid, "Solid");
            Set(Layers.Hazard, "Hazard");
            Set(Layers.Jelly, "Jelly");
            Set(Layers.Lead, "Lead");
            Set(Layers.Food, "Food");
            tm.ApplyModifiedProperties();

            // 선두↔젤리는 물리 충돌을 끄고(올라타기 방지) LeaderController가 수평 충돌만 처리한다.
            Physics.IgnoreLayerCollision(Layers.Jelly, Layers.Lead, true);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static void EnsureBaseMaterial()
        {
            string p = ResDir + "/ProtoLit.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(p) != null) return;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, p);
        }

        static void AddToBuildSettings(string path)
        {
            var list = EditorBuildSettings.scenes.ToList();
            list.RemoveAll(s => s.path == path);
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        static Material CheckerMaterial(Vector2 tiling)
        {
            string tp = MatDir + "/T_Checker.asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
            if (!tex)
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "T_Checker" };
                Color a = new Color(0.96f, 0.96f, 0.94f), b = new Color(0.74f, 0.77f, 0.82f);
                tex.SetPixels(new[] { a, b, b, a });
                tex.Apply();
                AssetDatabase.CreateAsset(tex, tp);
            }
            var baseMat = AssetDatabase.LoadAssetAtPath<Material>(ResDir + "/ProtoLit.mat");
            var m = new Material(baseMat) { name = "M_Floor" };
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", tiling);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.35f);
            AssetDatabase.CreateAsset(m, GenDir + "/M_Floor.mat");
            return m;
        }

        static Material SignMaterial(Font font)
        {
            if (signMat) return signMat;
            var sh = Shader.Find("CM101/WorldText");
            if (!sh) return font.material;
            signMat = new Material(sh) { name = "M_WorldText" };
            signMat.mainTexture = font.material.mainTexture;
            AssetDatabase.CreateAsset(signMat, GenDir + "/M_WorldText.mat");
            return signMat;
        }

        // ------------------------------------------------------------------ 기본 헬퍼

        static GameObject Box(string name, Vector3 min, Vector3 max, Color c, float smooth = 0.2f, Transform parent = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.layer = Layers.Solid;
            g.transform.SetParent(parent ? parent : level, false);
            g.transform.position = (min + max) * 0.5f;
            g.transform.localScale = max - min;
            g.GetComponent<MeshRenderer>().sharedMaterial = ProtoFactory.Mat(c, smooth);
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
            return g;
        }

        /// <summary>회전된 부모 아래 로컬 좌표 상자(충돌 있음).</summary>
        static GameObject LocalBox(Transform parent, string name, Vector3 center, Vector3 size, Color c, float smooth = 0.3f)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.layer = Layers.Solid;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = center;
            g.transform.localScale = size;
            g.GetComponent<MeshRenderer>().sharedMaterial = ProtoFactory.Mat(c, smooth);
            return g;
        }

        static void Wall(float x0, float z0, float x1, float z1, float h = 3f, Color? c = null)
            => Box("Wall", new Vector3(x0, 0f, z0), new Vector3(x1, h, z1), c ?? CWall);

        static void Crate(float x0, float z0, float x1, float z1, float h, bool dark = false)
            => Box("Crate", new Vector3(x0, 0f, z0), new Vector3(x1, h, z1), dark ? CCrateDark : CCrate);

        static FoodItem Food(FoodType t, float x, float z, int variant = 0, float y = 0f)
            => FoodItem.Create(t, variant, new Vector3(x, y, z), foodRoot);

        /// <summary>창고 튜토리얼 전용 음식(90cm 총량에서 제외).</summary>
        static void TutorialFood(FoodType t, float x, float z, int variant = 0, float y = 0f)
        {
            var f = Food(t, x, z, variant, y);
            f.isTutorial = true;
            f.name = "Tutorial_" + f.name;
        }

        static GameObject tutorialGate;

        /// <summary>낮은 끝(low)과 높은 끝(high)의 윗면 중심을 잇는 경사판.</summary>
        static GameObject Ramp(Vector3 low, Vector3 high, float width, Color? c = null)
        {
            const float thick = 0.1f;
            Vector3 flat = high - low;
            flat.y = 0f;
            float len = flat.magnitude;
            float h = high.y - low.y;
            float hyp = Mathf.Sqrt(len * len + h * h);
            float ang = Mathf.Atan2(h, len) * Mathf.Rad2Deg;
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "Ramp";
            g.layer = Layers.Solid;
            g.transform.SetParent(level, false);
            g.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up) * Quaternion.Euler(-ang, 0f, 0f);
            g.transform.localScale = new Vector3(width, thick, hyp);
            g.transform.position = (low + high) * 0.5f - g.transform.up * (thick * 0.5f);
            g.GetComponent<MeshRenderer>().sharedMaterial = ProtoFactory.Mat(c ?? CRamp, 0.2f);
            // 경사판 옆 줄무늬(방향 안내)
            ProtoFactory.Prim(PrimitiveType.Cube, g.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 0.1f, 0.9f),
                ProtoFactory.Mat(new Color(0.95f, 0.55f, 0.2f), 0.2f), "Arrow");
            return g;
        }

        /// <summary>팔레트: 다리 사이로 아래를 지나갈 수 있다(바닥 여유 0.85).</summary>
        static void Pallet(float x0, float z0, float x1, float z1, float deckY = 0.85f)
        {
            var root = new GameObject("Pallet").transform;
            root.SetParent(level, false);
            Box("PalletDeck", new Vector3(x0, deckY, z0), new Vector3(x1, deckY + 0.12f, z1), CPallet, 0.2f, root);
            float[] xs = { x0, (x0 + x1) * 0.5f - 0.15f, x1 - 0.3f };
            float[] zs = { z0, z1 - 0.3f };
            foreach (var x in xs)
                foreach (var z in zs)
                    Box("PalletLeg", new Vector3(x, 0f, z), new Vector3(x + 0.3f, deckY, z + 0.3f), CCrateDark, 0.2f, root);
            // 위에 쌓인 상자 (장식 + 가림)
            Box("PalletLoad", new Vector3(x0 + 0.2f, deckY + 0.12f, z0 + 0.2f), new Vector3(x1 - 0.2f, deckY + 1.3f, z1 - 0.2f), CCrate, 0.2f, root);
        }

        /// <summary>진열 테이블: 다리 네 개, 상판 아래(여유 1.0)로 지나가거나 숨을 수 있다. 위에 과일 더미 장식.</summary>
        static void ProduceTable(float x0, float z0, float x1, float z1, Color pile)
        {
            var root = new GameObject("ProduceTable").transform;
            root.SetParent(level, false);
            Box("TableTop", new Vector3(x0, 1.0f, z0), new Vector3(x1, 1.12f, z1), new Color(0.55f, 0.75f, 0.45f), 0.3f, root);
            float t = 0.18f;
            Box("TableLeg", new Vector3(x0, 0f, z0), new Vector3(x0 + t, 1.0f, z0 + t), CMetal, 0.5f, root);
            Box("TableLeg", new Vector3(x1 - t, 0f, z0), new Vector3(x1, 1.0f, z0 + t), CMetal, 0.5f, root);
            Box("TableLeg", new Vector3(x0, 0f, z1 - t), new Vector3(x0 + t, 1.0f, z1), CMetal, 0.5f, root);
            Box("TableLeg", new Vector3(x1 - t, 0f, z1 - t), new Vector3(x1, 1.0f, z1), CMetal, 0.5f, root);
            // 과일 더미 장식 (충돌 없음)
            for (int i = 0; i < 26; i++)
            {
                Vector3 p = new Vector3(Random.Range(x0 + 0.3f, x1 - 0.3f), 1.12f + Random.Range(0.1f, 0.35f), Random.Range(z0 + 0.3f, z1 - 0.3f));
                Color c = Color.Lerp(pile, Color.HSVToRGB(Random.value, 0.6f, 1f), 0.25f);
                ProtoFactory.Prim(PrimitiveType.Sphere, level, p, Vector3.one * Random.Range(0.28f, 0.42f), ProtoFactory.Mat(c, 0.6f), "Produce");
            }
        }

        /// <summary>장바구니: 낮은 벽(0.55) 네 면 중 한 면에 좁은 입구. 안쪽이 좁아 긴 무리는 방향 전환이 어렵다.</summary>
        static void Basket(float x0, float z0, float x1, float z1, char gapSide, float gap = 0.95f)
        {
            var root = new GameObject("Basket").transform;
            root.SetParent(level, false);
            const float h = 0.55f, t = 0.1f;
            Color c = CBasket;
            void Side(Vector3 a, Vector3 b) => Box("BasketWall", a, b, c, 0.5f, root);
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f, g = gap * 0.5f;
            // 남(z0)
            if (gapSide == 'S') { Side(new Vector3(x0, 0, z0), new Vector3(cx - g, h, z0 + t)); Side(new Vector3(cx + g, 0, z0), new Vector3(x1, h, z0 + t)); }
            else Side(new Vector3(x0, 0, z0), new Vector3(x1, h, z0 + t));
            // 북(z1)
            if (gapSide == 'N') { Side(new Vector3(x0, 0, z1 - t), new Vector3(cx - g, h, z1)); Side(new Vector3(cx + g, 0, z1 - t), new Vector3(x1, h, z1)); }
            else Side(new Vector3(x0, 0, z1 - t), new Vector3(x1, h, z1));
            // 서(x0)
            if (gapSide == 'W') { Side(new Vector3(x0, 0, z0), new Vector3(x0 + t, h, cz - g)); Side(new Vector3(x0, 0, cz + g), new Vector3(x0 + t, h, z1)); }
            else Side(new Vector3(x0, 0, z0), new Vector3(x0 + t, h, z1));
            // 동(x1)
            if (gapSide == 'E') { Side(new Vector3(x1 - t, 0, z0), new Vector3(x1, h, cz - g)); Side(new Vector3(x1 - t, 0, cz + g), new Vector3(x1, h, z1)); }
            else Side(new Vector3(x1 - t, 0, z0), new Vector3(x1, h, z1));
            // 손잡이 장식
            ProtoFactory.Prim(PrimitiveType.Cube, root, new Vector3(cx, h + 0.35f, cz), new Vector3(x1 - x0 - 0.2f, 0.06f, 0.06f), ProtoFactory.Mat(c, 0.5f), "Handle");
        }

        /// <summary>쇼핑 카트: 가는 다리 사이로 아래를 지나갈 수 있다(여유 0.85).</summary>
        static void Cart(Vector3 center, float yaw)
        {
            var root = new GameObject("Cart").transform;
            root.SetParent(level, false);
            root.position = center;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            const float w = 1.4f, d = 2.2f, legT = 0.09f, bottom = 0.85f;
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    LocalBox(root, "CartLeg", new Vector3(sx * (w * 0.5f - legT), bottom * 0.5f, sz * (d * 0.5f - legT)), new Vector3(legT, bottom, legT), CMetal, 0.7f);
            LocalBox(root, "CartBasket", new Vector3(0f, bottom + 0.4f, 0f), new Vector3(w, 0.8f, d), new Color(0.85f, 0.2f, 0.25f), 0.5f);
            LocalBox(root, "CartHandle", new Vector3(0f, bottom + 0.9f, -d * 0.5f - 0.15f), new Vector3(w, 0.08f, 0.08f), CMetal, 0.7f);
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    ProtoFactory.Prim(PrimitiveType.Cylinder, root, new Vector3(sx * (w * 0.5f - legT), 0.08f, sz * (d * 0.5f - legT)),
                        new Vector3(0.16f, 0.03f, 0.16f), ProtoFactory.Mat(new Color(0.15f, 0.15f, 0.18f), 0.3f), "Wheel").transform.localRotation = Quaternion.Euler(0, 0, 90);
        }

        static void Supply(string label, float x, float z)
        {
            var g = new GameObject("Supply_" + label);
            g.transform.SetParent(level, false);
            g.transform.position = new Vector3(x, 0f, z);
            g.AddComponent<SupplyPoint>().label = label;
            var m = ProtoFactory.Mat(new Color(0.35f, 0.9f, 0.5f), 0.1f, 0.6f);
            ProtoFactory.Prim(PrimitiveType.Cube, g.transform, new Vector3(0, 0.012f, 0), new Vector3(0.9f, 0.01f, 0.25f), m, "PlusH");
            ProtoFactory.Prim(PrimitiveType.Cube, g.transform, new Vector3(0, 0.012f, 0), new Vector3(0.25f, 0.01f, 0.9f), m, "PlusV");
        }

        static void Sign(string text, Vector3 pos, float yRot, float size, Color c)
        {
            var g = new GameObject("Sign_" + text);
            g.transform.SetParent(level, false);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
            var tm = g.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = c;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            g.GetComponent<MeshRenderer>().sharedMaterial = SignMaterial(font);
        }

        /// <summary>선반 옆면 장식 상품 줄(충돌 없음). y0 = 가장 아래 줄 높이.</summary>
        static void ShelfDeco(float xFace, float dirX, float z0, float z1, float y0 = 0.95f)
        {
            for (float y = y0; y < 3.2f; y += 0.95f)
            {
                ProtoFactory.Prim(PrimitiveType.Cube, level, new Vector3(xFace + dirX * 0.06f, y - 0.05f, (z0 + z1) * 0.5f),
                    new Vector3(0.12f, 0.06f, z1 - z0), ProtoFactory.Mat(CPlank, 0.3f), "Plank");
                float z = z0 + 0.1f;
                while (z < z1 - 0.4f)
                {
                    float w = Random.Range(0.35f, 0.7f);
                    float h = Random.Range(0.35f, 0.75f);
                    Color c = Color.HSVToRGB(Random.value, Random.Range(0.45f, 0.8f), Random.Range(0.8f, 1f));
                    ProtoFactory.Prim(PrimitiveType.Cube, level, new Vector3(xFace + dirX * 0.14f, y + h * 0.5f, z + w * 0.5f),
                        new Vector3(0.26f, h, w * 0.9f), ProtoFactory.Mat(c, 0.35f), "Product");
                    z += w + 0.04f;
                }
            }
        }

        /// <summary>외벽 안쪽 걸레받이 색띠로 구역 구분.</summary>
        static void Baseboard(float z0, float z1, Color c)
        {
            var m = ProtoFactory.Mat(c, 0.3f);
            ProtoFactory.Prim(PrimitiveType.Cube, level, new Vector3(0.03f, 0.2f, (z0 + z1) * 0.5f), new Vector3(0.06f, 0.4f, z1 - z0), m, "Baseboard");
            ProtoFactory.Prim(PrimitiveType.Cube, level, new Vector3(MapW - 0.03f, 0.2f, (z0 + z1) * 0.5f), new Vector3(0.06f, 0.4f, z1 - z0), m, "Baseboard");
        }

        // ------------------------------------------------------------------ 월드

        static void BuildWorld()
        {
            Random.InitState(101);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(55f, 35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.75f, 0.78f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.6f, 0.62f, 0.66f);
            RenderSettings.ambientGroundColor = new Color(0.4f, 0.4f, 0.42f);

            level = new GameObject("Level").transform;
            foodRoot = new GameObject("Foods").transform;

            var floor = Box("Floor", new Vector3(0f, -0.2f, 0f), new Vector3(MapW, 0f, MapL), Color.white);
            floor.GetComponent<MeshRenderer>().sharedMaterial = CheckerMaterial(new Vector2(MapW * 0.5f, MapL * 0.5f));

            // 외곽 벽
            Wall(-0.5f, -0.5f, 0f, MapL + 0.5f);
            Wall(MapW, -0.5f, MapW + 0.5f, MapL + 0.5f);
            Wall(-0.5f, -0.5f, MapW + 0.5f, 0f);
            Wall(-0.5f, MapL, MapW + 0.5f, MapL + 0.5f, 3f, new Color(0.55f, 0.75f, 0.95f));
            // 출구 벽 + 문
            Wall(0f, ExitZ, 10f, ExitZ + 0.5f);
            Wall(14f, ExitZ, MapW, ExitZ + 0.5f);
            Box("DoorLintel", new Vector3(10f, 2.4f, ExitZ), new Vector3(14f, 3f, ExitZ + 0.5f), CWall);
            var door = Box("ExitDoor", new Vector3(10f, 0f, ExitZ + 0.1f), new Vector3(14f, 2.4f, ExitZ + 0.4f), new Color(0.45f, 0.85f, 0.6f), 0.8f);

            Baseboard(0f, 16f, new Color(0.75f, 0.6f, 0.45f));
            Baseboard(16f, 38f, new Color(0.95f, 0.55f, 0.45f));
            Baseboard(38f, 54f, new Color(0.5f, 0.8f, 0.4f));
            Baseboard(54f, 64f, new Color(0.45f, 0.7f, 1f));
            Baseboard(64f, ExitZ, new Color(0.95f, 0.8f, 0.35f));

            BuildWarehouse();
            BuildAisles();
            BuildPlaza();
            BuildIceCream();
            BuildCheckout();

            // 선두
            var leaderGo = new GameObject("Leader");
            leaderGo.layer = Layers.Lead;
            leaderGo.transform.position = new Vector3(4f, 0.32f, 3f);
            var sc = leaderGo.AddComponent<SphereCollider>();
            sc.radius = 0.3f;
            var rb = leaderGo.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            ProtoFactory.BuildBear(leaderGo.transform, 0.6f, new Color(1f, 0.4f, 0.48f), true);
            var leader = leaderGo.AddComponent<LeaderController>();

            // 카메라
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.22f, 0.28f);
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 60f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(4f, 2f, 0.5f);
            var orbit = camGo.AddComponent<OrbitCamera>();
            orbit.target = leader;
            leader.cam = orbit;

            var chainGo = new GameObject("JellyChain");
            var chain = chainGo.AddComponent<JellyChain>();
            chain.leader = leader;

            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            gmGo.AddComponent<PrototypeHUD>();
            gm.leader = leader;
            gm.chain = chain;
            gm.cam = orbit;
            gm.exitDoor = door;
            gm.tutorialGate = tutorialGate;
            gm.tutorialExitZ = 16.6f;
            var fs = new GameObject("FinalSpawnPoint").transform;
            fs.SetParent(level, false);
            fs.position = new Vector3(12f, 0f, 73.8f);
            gm.finalSpawnPoint = fs;
            var ex = new GameObject("ExitPoint").transform;
            ex.SetParent(level, false);
            ex.position = new Vector3(12f, 0f, ExitZ + 0.3f);
            gm.exitPoint = ex;

            ProtoFactory.Prim(PrimitiveType.Cylinder, level, new Vector3(12f, 5f, ExitZ + 0.25f), new Vector3(0.18f, 5f, 0.18f),
                ProtoFactory.Mat(new Color(0.4f, 1f, 0.6f), 0f, 2f), "ExitBeacon").GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            int total = 0, count = 0, tut = 0, tutCount = 0;
            foreach (var f in foodRoot.GetComponentsInChildren<FoodItem>())
            {
                if (f.isTutorial) { tut += f.Length; tutCount++; }
                else { total += f.Length; count++; }
            }
            if (total != JellyChain.FoodTotal) Debug.LogError($"[101CM] 본게임 음식 합계 {total}cm (90cm 필요)");
            else Debug.Log($"[101CM] 본게임 음식 {count}개, 합계 {total}cm 확인 · 튜토리얼 음식 {tutCount}개 {tut}cm");
        }

        // ---- 1. 창고 (z 0~16)
        static void BuildWarehouse()
        {
            Wall(0f, 15.8f, 9f, 16.2f);
            Wall(15f, 15.8f, MapW, 16.2f);

            // 높은 상자 벽으로 공간을 나눈다
            Crate(13f, 0f, 14.5f, 9f, 2.4f, true);
            Crate(6f, 8f, 8f, 9.5f, 1.2f);
            Crate(11f, 11f, 12.5f, 14.5f, 1.6f);
            Crate(0f, 14f, 2f, 15.8f, 1.8f, true);

            // 튜토리얼: 바닥의 첫 음식 하나
            TutorialFood(FoodType.Fruit, 4f, 6.5f, 0);

            // 팔레트 아래에 숨은 과일 (다리 사이로 들어가야 함)
            Pallet(8f, 2f, 11.2f, 5.2f);
            TutorialFood(FoodType.Fruit, 9.6f, 3.6f, 1);

            // 단상 + 좁은 경사판 + U자 길 끝의 쿠키
            Box("Platform", new Vector3(17f, 0f, 2f), new Vector3(23f, 0.6f, 6f), new Color(0.7f, 0.72f, 0.78f));
            Ramp(new Vector3(18.7f, 0f, 9.6f), new Vector3(18.7f, 0.6f, 6f), 1.3f);
            Box("PlatformCrate", new Vector3(19.4f, 0.6f, 4.3f), new Vector3(23f, 1.2f, 4.8f), CCrate);
            TutorialFood(FoodType.Cookie, 22.3f, 3.1f, 0, 0.6f);

            // 바구니 안의 빵 (동쪽 좁은 입구)
            Basket(2.4f, 11.2f, 4.8f, 13.6f, 'E');
            TutorialFood(FoodType.Bread, 3.4f, 12.4f);

            // 튜토리얼 종료 시 내려오는 셔터 (처음엔 꺼져 있음)
            tutorialGate = Box("TutorialGate", new Vector3(9f, 0f, 15.85f), new Vector3(15f, 3f, 16.15f), new Color(0.55f, 0.58f, 0.62f), 0.5f);
            for (int i = 0; i < 6; i++)
                ProtoFactory.Prim(PrimitiveType.Cube, tutorialGate.transform, new Vector3(0f, -0.4f + i * 0.17f, 0.55f), new Vector3(1.01f, 0.04f, 0.1f),
                    ProtoFactory.Mat(new Color(0.4f, 0.42f, 0.46f), 0.4f), "ShutterLine");
            GameObjectUtility.SetStaticEditorFlags(tutorialGate, 0); // 런타임에 켜므로 정적 배칭 제외
            tutorialGate.SetActive(false);

            Sign("WAREHOUSE  -  TUTORIAL", new Vector3(7f, 2.2f, 0.05f), 180f, 0.07f, CSign);
            Sign("FOOD AISLE", new Vector3(12f, 2.6f, 15.75f), 0f, 0.07f, CSign);
        }

        // ---- 2. 식품 통로 (z 16~38): 선반 3줄, 중간 교차로
        static void BuildAisles()
        {
            float[] shelfX = { 4f, 10f, 16f };
            foreach (float x in shelfX)
            {
                Box("Shelf", new Vector3(x, 0f, 18f), new Vector3(x + 1.6f, 3.5f, 26.5f), CShelf);
                Box("Shelf", new Vector3(x, 0f, 28.5f), new Vector3(x + 1.6f, 3.5f, 36f), CShelf);
            }
            ShelfDeco(4f, -1f, 18f, 26.5f); ShelfDeco(4f, -1f, 28.5f, 36f);
            ShelfDeco(5.6f, 1f, 18f, 26.5f); ShelfDeco(5.6f, 1f, 28.5f, 36f, 1.4f);
            ShelfDeco(10f, -1f, 18f, 26.5f); ShelfDeco(10f, -1f, 28.5f, 36f);
            ShelfDeco(11.6f, 1f, 18f, 26.5f); ShelfDeco(11.6f, 1f, 28.5f, 36f);
            ShelfDeco(16f, -1f, 18f, 18.8f); ShelfDeco(16f, -1f, 25.2f, 26.5f); ShelfDeco(16f, -1f, 28.5f, 36f);
            ShelfDeco(17.6f, 1f, 18f, 26.5f); ShelfDeco(17.6f, 1f, 28.5f, 36f);

            // (a) 선반 하단의 좁은 단(폭 0.8, 높이 0.5) + 경사판. 끝에서 방향을 돌릴 수 없어 떨어져 내려와야 한다.
            Box("BottomLedge", new Vector3(5.6f, 0f, 29f), new Vector3(6.4f, 0.5f, 35.8f), new Color(0.85f, 0.87f, 0.92f), 0.3f);
            Ramp(new Vector3(6f, 0f, 26.2f), new Vector3(6f, 0.5f, 29f), 0.8f);
            Food(FoodType.Cheese, 6f, 35.2f, 0, 0.5f);

            // (b) 붕괴 매대: 가운데-오른쪽 통로, 미끼 빵
            Box("PromoLedge", new Vector3(15.4f, 0f, 19f), new Vector3(16f, 1.4f, 25f), new Color(0.95f, 0.45f, 0.4f));
            Sign("SALE!", new Vector3(15.35f, 0.8f, 22f), 90f, 0.06f, Color.white);
            var stackGo = new GameObject("FallingStack");
            stackGo.transform.SetParent(level, false);
            var stack = stackGo.AddComponent<FallingStack>();
            var items = new List<Rigidbody>();
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 6; i++)
                {
                    var it = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    it.name = "StackItem";
                    it.layer = Layers.Hazard;
                    it.transform.SetParent(stackGo.transform, false);
                    it.transform.position = new Vector3(15.7f, 1.4f + 0.23f + row * 0.47f, 19.5f + i);
                    it.transform.localScale = new Vector3(0.45f, 0.45f, 0.8f);
                    it.GetComponent<MeshRenderer>().sharedMaterial = ProtoFactory.Mat(Color.HSVToRGB((i * 0.13f + row * 0.4f) % 1f, 0.7f, 1f), 0.4f);
                    var irb = it.AddComponent<Rigidbody>();
                    irb.mass = 0.4f;
                    irb.isKinematic = true;
                    irb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    items.Add(irb);
                }
            stack.items = items.ToArray();
            stack.pushDirection = new Vector3(-1f, 0.15f, 0f);
            stack.pushSpeed = 3.2f;
            stack.zoneCenter = new Vector3(13.8f, 1f, 22f);
            stack.zoneSize = new Vector3(4.4f, 2.5f, 7f);
            var marker = ProtoFactory.Prim(PrimitiveType.Cube, level, new Vector3(13.8f, 0.012f, 22f), new Vector3(3.2f, 0.01f, 6.2f),
                ProtoFactory.Mat(new Color(1f, 0.85f, 0.2f), 0.1f, 1.2f), "LandingMarker");
            stack.landingMarker = marker.GetComponent<Renderer>();
            Food(FoodType.Bread, 14.4f, 22f);

            // (c) 선반 끝 엔드캡 바구니 (서쪽 입구)
            Basket(9.9f, 36.1f, 11.7f, 37.75f, 'W', 0.9f);
            Food(FoodType.Fruit, 11f, 36.9f, 2);

            // (d) 오른쪽 넓은 통로: 비스듬히 선 카트 아래의 빵
            Cart(new Vector3(20.8f, 0f, 30.5f), 25f);
            Food(FoodType.Bread, 20.8f, 30.5f);
            Crate(18.5f, 20f, 20.5f, 21.2f, 0.9f);
            Crate(21.5f, 24f, 24f, 25f, 0.9f);

            // (e) 왼쪽 통로 지그재그 상자 사이
            Crate(0f, 20.5f, 2.6f, 21.5f, 0.8f);
            Crate(1.4f, 24f, 4f, 25f, 0.8f);
            Crate(0f, 30f, 2.6f, 31f, 0.8f);
            Crate(1.4f, 33.5f, 4f, 34.5f, 0.8f);
            // (f) 오른쪽 통로 끝 팔레트 아래 쿠키
            Pallet(19.5f, 33f, 22.7f, 36.2f);
            Food(FoodType.Cookie, 21.1f, 34.6f);

            Food(FoodType.Fruit, 0.8f, 23f, 0);
            Food(FoodType.Fruit, 3.2f, 36.6f, 1);

            Supply("식품 통로 보충 지점", 8f, 27.5f);
            Sign("PRODUCE", new Vector3(4f, 2.6f, 37.75f), 0f, 0.07f, CSign);
        }

        // ---- 3. 농산물 광장 (z 38~54): 테이블 아래 공간, 직원 발 2개
        static void BuildPlaza()
        {
            Wall(0f, 37.8f, 1f, 38.2f);
            Wall(7f, 37.8f, 14f, 38.2f);
            Wall(22f, 37.8f, MapW, 38.2f);

            ProduceTable(3f, 41f, 7f, 44f, new Color(1f, 0.4f, 0.3f));
            ProduceTable(10f, 46f, 14f, 49f, new Color(1f, 0.7f, 0.2f));
            ProduceTable(17f, 41f, 21f, 44f, new Color(0.5f, 0.85f, 0.3f));

            // (a) 가운데 테이블 아래 치즈, (b) 오른쪽 테이블 아래 과일
            Food(FoodType.Cheese, 12f, 47.5f);
            Food(FoodType.Bread, 5f, 42.5f);
            Food(FoodType.Fruit, 19f, 42.5f, 2);

            // (c) 상자 단상 위 빵 (동쪽 경사판)
            Box("CratePlatform", new Vector3(6.8f, 0f, 51.2f), new Vector3(9.5f, 0.6f, 53.6f), CCrate);
            Ramp(new Vector3(12.5f, 0f, 52.4f), new Vector3(9.5f, 0.6f, 52.4f), 1.2f);
            Food(FoodType.Bread, 7.6f, 52.6f, 0, 0.6f);

            // (d) 바구니 속 과일 (서쪽 입구)
            Basket(19f, 51.2f, 21.6f, 53.6f, 'W');
            Food(FoodType.Fruit, 20.5f, 52.4f, 1);

            // 낮은 과일 상자
            Crate(3.2f, 47f, 4.8f, 48.5f, 0.6f);
            Crate(15.5f, 46.5f, 16.8f, 49.5f, 0.6f);

            // 직원 발 2개: 테이블 남쪽/북쪽 순찰, 입구 앞 가로 순찰
            MakeStaff("StaffFoot_A", new[] { new Vector3(2f, 0f, 45.2f), new Vector3(22f, 0f, 45.2f), new Vector3(22f, 0f, 50.2f), new Vector3(2f, 0f, 50.2f) }, 1.7f);
            MakeStaff("StaffFoot_B", new[] { new Vector3(22f, 0f, 39.6f), new Vector3(2f, 0f, 39.6f) }, 1.9f);

            Supply("광장 보충 지점", 1.2f, 43f);
            Sign("ICE CREAM", new Vector3(6f, 2.6f, 53.75f), 0f, 0.07f, new Color(0.3f, 0.5f, 0.8f));
        }

        static void MakeStaff(string name, Vector3[] path, float speed)
        {
            var staff = new GameObject(name);
            staff.transform.SetParent(level, false);
            staff.transform.position = path[0];
            Vector3 d = path[1] - path[0];
            staff.transform.rotation = Quaternion.LookRotation(d.normalized);
            var foot = new GameObject("Foot").transform;
            foot.SetParent(staff.transform, false);
            foot.gameObject.layer = Layers.Hazard;
            ProtoFactory.Prim(PrimitiveType.Cube, foot, new Vector3(0f, 0.175f, 0f), new Vector3(1f, 0.35f, 1.6f),
                ProtoFactory.Mat(new Color(0.18f, 0.18f, 0.22f), 0.5f), "Shoe");
            ProtoFactory.Prim(PrimitiveType.Cube, foot, new Vector3(0f, 0.36f, 0.3f), new Vector3(0.9f, 0.04f, 0.9f),
                ProtoFactory.Mat(Color.white, 0.3f), "Lace");
            ProtoFactory.Prim(PrimitiveType.Cylinder, foot, new Vector3(0f, 1.85f, -0.15f), new Vector3(0.6f, 1.5f, 0.6f),
                ProtoFactory.Mat(new Color(0.25f, 0.35f, 0.6f), 0.2f), "Leg");
            var fc = foot.gameObject.AddComponent<BoxCollider>();
            fc.center = new Vector3(0f, 0.175f, 0f);
            fc.size = new Vector3(1f, 0.35f, 1.6f);
            var shadow = ProtoFactory.Prim(PrimitiveType.Cylinder, staff.transform, new Vector3(0f, 0.012f, 0f), new Vector3(1f, 0.005f, 1.6f),
                ProtoFactory.Mat(new Color(0.1f, 0.1f, 0.12f), 0f), "Shadow");
            shadow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var sf = staff.AddComponent<StaffFoot>();
            sf.foot = foot;
            sf.footCollider = fc;
            sf.shadow = shadow.GetComponent<Renderer>();
            sf.waypoints = path;
            sf.moveSpeed = speed;
        }

        // ---- 4. 아이스크림 (z 54~64): 청소기, 막다른 틈, 지그재그 우회로
        static void BuildIceCream()
        {
            Wall(0f, 53.8f, 1f, 54.2f);
            Wall(12f, 53.8f, 19f, 54.2f);
            Wall(23f, 53.8f, MapW, 54.2f);

            Box("FreezerDivider", new Vector3(17.5f, 0f, 54.2f), new Vector3(18.5f, 1.3f, 63.8f), CFreezer, 0.7f);
            Box("FreezerIsland", new Vector3(2f, 0f, 55.5f), new Vector3(6f, 1f, 56.5f), CFreezer, 0.7f);
            Box("FreezerIsland", new Vector3(9f, 0f, 61.8f), new Vector3(13f, 1f, 62.8f), CFreezer, 0.7f);

            // (a) 막다른 틈(폭 1.2) 끝의 케이크: 들어가면 돌아 나오기 어렵다
            Box("FreezerSlotA", new Vector3(13f, 0f, 55f), new Vector3(17.5f, 1f, 56f), CFreezer, 0.7f);
            Box("FreezerSlotB", new Vector3(13f, 0f, 57.2f), new Vector3(17.5f, 1f, 58.2f), CFreezer, 0.7f);
            Food(FoodType.Cake, 16.9f, 56.6f);

            // (b) 청소기 경로 바로 옆 쿠키
            Food(FoodType.Cookie, 8f, 60.9f);
            Food(FoodType.Fruit, 1.1f, 57.4f, 2); // 청소기 출발 지점 구석

            // (c) 지그재그 우회로 (안전하지만 좁고 길다)
            Box("LaneBlock", new Vector3(18.5f, 0f, 57f), new Vector3(21.8f, 1.3f, 57.8f), CFreezer, 0.7f);
            Box("LaneBlock", new Vector3(20.7f, 0f, 60f), new Vector3(MapW, 1.3f, 60.8f), CFreezer, 0.7f);
            Food(FoodType.Bread, 22.8f, 58.8f);
            Food(FoodType.Fruit, 19.4f, 62.9f, 0);

            var vac = new GameObject("Vacuum");
            vac.layer = Layers.Hazard;
            vac.transform.SetParent(level, false);
            vac.transform.position = new Vector3(2f, 0f, 59.3f);
            vac.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var vb = vac.AddComponent<BoxCollider>();
            vb.center = new Vector3(0f, 0.225f, 0f);
            vb.size = new Vector3(1.3f, 0.45f, 1.3f);
            var vrb = vac.AddComponent<Rigidbody>();
            vrb.isKinematic = true;
            ProtoFactory.Prim(PrimitiveType.Cylinder, vac.transform, new Vector3(0f, 0.2f, 0f), new Vector3(1.4f, 0.2f, 1.4f),
                ProtoFactory.Mat(new Color(0.25f, 0.27f, 0.3f), 0.6f), "Body");
            ProtoFactory.Prim(PrimitiveType.Cylinder, vac.transform, new Vector3(0f, 0.41f, 0f), new Vector3(1.15f, 0.015f, 1.15f),
                ProtoFactory.Mat(new Color(0.75f, 0.78f, 0.82f), 0.7f), "Top");
            ProtoFactory.Prim(PrimitiveType.Cube, vac.transform, new Vector3(0f, 0.12f, 0.66f), new Vector3(1.0f, 0.16f, 0.12f),
                ProtoFactory.Mat(new Color(0.08f, 0.08f, 0.1f), 0.3f), "Mouth");
            ProtoFactory.Prim(PrimitiveType.Sphere, vac.transform, new Vector3(0f, 0.45f, 0.35f), Vector3.one * 0.12f,
                ProtoFactory.Mat(new Color(1f, 0.3f, 0.2f), 0.2f, 2f), "Lamp");
            var v = vac.AddComponent<VacuumCleaner>();
            v.pointA = new Vector3(2f, 0f, 59.3f);
            v.pointB = new Vector3(15.5f, 0f, 59.3f);
            var zm = ProtoFactory.Prim(PrimitiveType.Cube, vac.transform, new Vector3(v.zoneLocalCenter.x, 0.012f, v.zoneLocalCenter.z),
                new Vector3(v.zoneSize.x, 0.01f, v.zoneSize.z), ProtoFactory.Mat(new Color(1f, 0.35f, 0.15f), 0.1f, 1f), "SuctionZone");
            zm.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            v.zoneMarker = zm.GetComponent<Renderer>();

            Supply("아이스크림 보충 지점", 1.5f, 62.8f);
            Sign("CHECKOUT", new Vector3(12f, 2.6f, 63.75f), 0f, 0.07f, CSign);
        }

        // ---- 5. 계산대 (z 64~76): 움직이는 벨트, 계산대 위 치즈, 바구니
        static void BuildCheckout()
        {
            Wall(0f, 63.8f, 2f, 64.2f);
            Wall(10f, 63.8f, 14f, 64.2f);
            Wall(22f, 63.8f, MapW, 64.2f);

            float[] counterX = { 3.5f, 9f, 15f, 20f };
            foreach (float x in counterX)
            {
                Box("Counter", new Vector3(x, 0f, 66f), new Vector3(x + 1.5f, 1.1f, 71.5f), CCounter, 0.4f);
                if (x > 4f) Box("Register", new Vector3(x + 0.2f, 1.1f, 70.2f), new Vector3(x + 1.3f, 1.6f, 71.2f), new Color(0.3f, 0.3f, 0.35f), 0.5f);
            }

            // (a)(b) 계산대 사이 바닥 벨트: 입구 쪽(-z)으로 밀어내며 음식을 실어 나른다
            MakeConveyor(new Vector3(7f, 0f, 68.75f), 1.6f, 5.5f, 1.3f);
            MakeConveyor(new Vector3(18.25f, 0f, 68.75f), 1.6f, 5.5f, 1.5f);
            Food(FoodType.Bread, 7f, 69f);
            Food(FoodType.Fruit, 18.25f, 67.5f, 1);

            // (c) 첫 번째 계산대 위 치즈 (북쪽 경사판으로 올라가 계산대 위를 걸어야 함)
            Ramp(new Vector3(4.25f, 0f, 75.2f), new Vector3(4.25f, 1.1f, 71.5f), 1.5f);
            Food(FoodType.Cheese, 4.25f, 66.6f, 0, 1.1f);

            // (d) 포장대 바구니 속 빵 (북쪽 입구)
            Basket(0.3f, 72f, 2.6f, 74.5f, 'N');
            Basket(21.8f, 66.4f, 23.8f, 68.6f, 'N', 0.9f);
            Food(FoodType.Fruit, 22.8f, 67.2f, 0);
            Food(FoodType.Bread, 1.4f, 72.9f);

            // 어린이 키재기 판 + 101CM 표식
            Box("HeightChart", new Vector3(14.6f, 0f, ExitZ - 0.2f), new Vector3(15.6f, 2.2f, ExitZ - 0.02f), new Color(1f, 0.95f, 0.8f), 0.2f);
            for (int i = 1; i <= 10; i++)
                ProtoFactory.Prim(PrimitiveType.Cube, level, new Vector3(15.1f, i * 0.2f, ExitZ - 0.22f), new Vector3(i % 5 == 0 ? 0.8f : 0.4f, 0.02f, 0.01f),
                    ProtoFactory.Mat(new Color(0.9f, 0.4f, 0.45f), 0.2f), "Tick");
            Sign("101CM", new Vector3(15.1f, 2.05f, ExitZ - 0.23f), 0f, 0.035f, new Color(0.9f, 0.3f, 0.4f));
            Sign("EXIT", new Vector3(12f, 2.7f, ExitZ - 0.05f), 0f, 0.09f, new Color(0.2f, 0.7f, 0.35f));

            Supply("계산대 보충 지점", 22.5f, 74f);
        }

        static void MakeConveyor(Vector3 center, float width, float length, float speed)
        {
            var g = new GameObject("Conveyor");
            g.transform.SetParent(level, false);
            g.transform.position = center;
            g.transform.rotation = Quaternion.LookRotation(Vector3.back); // 입구 쪽으로 흐름
            ProtoFactory.Prim(PrimitiveType.Cube, g.transform, new Vector3(0f, 0.011f, 0f), new Vector3(width, 0.02f, length),
                ProtoFactory.Mat(new Color(0.18f, 0.19f, 0.22f), 0.5f), "Belt");
            var stripes = new List<Transform>();
            var sm = ProtoFactory.Mat(new Color(1f, 0.8f, 0.2f), 0.2f, 0.4f);
            int n = Mathf.RoundToInt(length / 0.7f);
            for (int i = 0; i < n; i++)
            {
                var s = ProtoFactory.Prim(PrimitiveType.Cube, g.transform, new Vector3(0f, 0.024f, -length * 0.5f + i * (length / n)),
                    new Vector3(width * 0.8f, 0.01f, 0.1f), sm, "Stripe");
                s.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                stripes.Add(s.transform);
            }
            var cb = g.AddComponent<ConveyorBelt>();
            cb.size = new Vector2(width, length);
            cb.speed = speed;
            cb.stripes = stripes.ToArray();
        }
    }
}
