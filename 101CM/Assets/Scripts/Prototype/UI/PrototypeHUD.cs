using System.Collections.Generic;
using UnityEngine;

namespace CM101
{
    /// <summary>
    /// 프로토타입 HUD(IMGUI). 기획서 05장 배치를 따른다.
    /// 좌상단: "현재 길이 / 101cm" + 100cm 표시 게이지 + 현재 목표. 중앙은 비운다.
    /// 음식 근처: Space 안내와 증가량. 화면 밖 분리 젤리·최종 목표·보충 음식은 가장자리 방향 표시.
    /// Beta에서 uGUI/Figma 시안으로 교체한다.
    /// </summary>
    public class PrototypeHUD : MonoBehaviour
    {
        Font font;
        GUIStyle sBig, sMid, sSmall, sCenter, sTitle, sButton, sPrompt;
        Texture2D white;
        float scale;
        float W, H;

        static readonly Color CInfo = new Color(1f, 1f, 1f);
        static readonly Color CGood = new Color(0.55f, 1f, 0.6f);
        static readonly Color CWarn = new Color(1f, 0.62f, 0.35f);
        static readonly Color CGold = new Color(1f, 0.85f, 0.35f);
        static readonly Color CPink = new Color(1f, 0.6f, 0.85f);
        static readonly Color CPanel = new Color(0f, 0f, 0f, 0.55f);

        void Awake()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 24);
            white = Texture2D.whiteTexture;
        }

        void EnsureStyles()
        {
            if (sBig != null) return;
            sBig = Make(44, FontStyle.Bold, TextAnchor.MiddleLeft);
            sMid = Make(24, FontStyle.Bold, TextAnchor.MiddleLeft);
            sSmall = Make(18, FontStyle.Normal, TextAnchor.MiddleLeft);
            sCenter = Make(26, FontStyle.Bold, TextAnchor.MiddleCenter);
            sTitle = Make(64, FontStyle.Bold, TextAnchor.MiddleCenter);
            sPrompt = Make(20, FontStyle.Bold, TextAnchor.MiddleCenter);
            sButton = new GUIStyle(GUI.skin.button) { font = font, fontSize = 26, fontStyle = FontStyle.Bold };
        }

        GUIStyle Make(int size, FontStyle st, TextAnchor a)
        {
            var s = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, fontStyle = st, alignment = a, wordWrap = false, richText = true };
            s.normal.textColor = Color.white;
            return s;
        }

        static Color KindColor(MsgKind k)
        {
            switch (k)
            {
                case MsgKind.Good: return CGood;
                case MsgKind.Warn: return CWarn;
                case MsgKind.Gold: return CGold;
                default: return CInfo;
            }
        }

        void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        void Label(Rect r, string text, GUIStyle s, Color c, bool shadow = true)
        {
            var old = s.normal.textColor;
            if (shadow)
            {
                s.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
                GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, s);
            }
            s.normal.textColor = c;
            GUI.Label(r, text, s);
            s.normal.textColor = old;
        }

        void OnGUI()
        {
            var gm = GameManager.I;
            if (!gm) return;
            EnsureStyles();
            scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            W = Screen.width / scale;
            H = 1080f;

            if (gm.State == GameState.Playing || gm.State == GameState.Won)
            {
                // 기분 표현 말풍선은 끔(모션만 보여준다)
                DrawPrompt(gm);
                DrawIndicators(gm);
            }
            DrawLengthPanel(gm);
            DrawToasts(gm);
            DrawControls();

            if (gm.State == GameState.Paused) DrawPause(gm);
            else if (gm.State == GameState.Failed) DrawFail(gm);
            else if (gm.ShowResult) DrawResult(gm);
        }

        // ------------------------------------------------------------------ 길이 패널

        void DrawLengthPanel(GameManager gm)
        {
            var chain = gm.chain;
            int len = gm.DisplayLength;
            int detachedCount = chain ? chain.Detached.Count : 0;
            float h = detachedCount > 0 || gm.TutorialActive ? 176f : 146f;
            Fill(new Rect(24, 24, 480, h), CPanel);

            Label(new Rect(44, 34, 440, 56), $"{len} <size=28>/ 101cm</size>", sBig, len >= 100 ? CGold : Color.white);

            // 게이지 (100cm 지점 표시)
            Rect g = new Rect(44, 96, 440, 16);
            Fill(g, new Color(1f, 1f, 1f, 0.15f));
            float fill = Mathf.Clamp01(len / 101f);
            Fill(new Rect(g.x, g.y, g.width * fill, g.height), len >= 100 ? CGold : new Color(1f, 0.55f, 0.75f));
            float m100 = g.x + g.width * (100f / 101f);
            Fill(new Rect(m100 - 1.5f, g.y - 5, 3, g.height + 10), Color.white);
            Label(new Rect(m100 - 30, g.y + 16, 60, 20), "100", sSmall, new Color(1, 1, 1, 0.8f), false);

            Label(new Rect(44, 124, 440, 32), Objective(gm), sMid, Color.white);

            if (detachedCount > 0)
                Label(new Rect(44, 154, 440, 28), $"떨어진 동료 {detachedCount}마리 · {chain.DetachedLength}cm", sSmall, CWarn);
            else if (gm.TutorialActive)
                Label(new Rect(44, 154, 460, 28), "FOOD AISLE로 나가면 본게임 시작 (연습 동료는 사라져요)", sSmall, new Color(1, 1, 1, 0.7f));

            DrawRunGauge(gm, 24 + h + 10);
        }

        static readonly Color CRun = new Color(0.45f, 0.85f, 1f);

        void DrawRunGauge(GameManager gm, float y)
        {
            var ld = gm.leader;
            if (!ld) return;
            Fill(new Rect(24, y, 480, 44), CPanel);
            Label(new Rect(44, y + 7, 90, 30), "달리기", sSmall, Color.white, false);
            Rect bar = new Rect(130, y + 16, 250, 12);
            Fill(bar, new Color(1f, 1f, 1f, 0.15f));
            bool cooling = ld.RunCooldown > 0f;
            Color c = cooling ? new Color(0.6f, 0.65f, 0.7f) : (ld.IsRunning ? CGold : CRun);
            Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(ld.RunGauge), bar.height), c);
            string state = cooling ? $"대기 {ld.RunCooldown:0.0}초" : (ld.IsRunning ? "달리는 중" : "준비");
            Label(new Rect(392, y + 7, 110, 30), state, sSmall, cooling ? new Color(1, 1, 1, 0.7f) : c, false);
        }

        string Objective(GameManager gm)
        {
            if (gm.FinalConsumed) return "탈출!";
            if (gm.TutorialActive) return "튜토리얼: 먹어서 동료 붙여 보기";
            int cur = gm.chain ? gm.chain.CurrentLength : 10;
            if (!gm.FirstHundredReached) return "음식을 먹어 100cm 만들기";
            if (cur >= JellyChain.Goal) return "마지막 1cm 찾기";
            return $"100cm까지 {JellyChain.Goal - cur}cm 더 모으기";
        }

        // ------------------------------------------------------------------ 기분 표현 말풍선

        void DrawEmoteBubble(GameManager gm)
        {
            var cam = Camera.main;
            var ld = gm.leader;
            if (!cam || !ld || !ld.Emote || ld.Emote.Current == EmoteType.None) return;
            var e = ld.Emote.Current;
            float t = ld.Emote.Elapsed, d = JellyEmote.Duration(e);
            float a = Mathf.Clamp01(t / 0.12f) * Mathf.Clamp01((d - t) / 0.25f);
            if (!WorldToGui(cam, ld.transform.position + Vector3.up * 0.75f, out var p, out _)) return;
            string text = JellyEmote.Label(e);
            float pop = 1f + Mathf.Max(0f, 0.3f - t) * 1.5f;
            var st = new GUIStyle(sCenter) { fontSize = Mathf.RoundToInt(30 * pop) };
            var size = st.CalcSize(new GUIContent(text));
            Rect r = new Rect(p.x - size.x * 0.5f - 14, p.y - 44 - Mathf.Sin(t * 6f) * 4f, size.x + 28, 46);
            Fill(r, new Color(1f, 1f, 1f, 0.92f * a));
            Label(r, text, st, new Color(1f, 0.35f, 0.55f, a), false);
        }

        // ------------------------------------------------------------------ 섭취 안내

        bool WorldToGui(Camera cam, Vector3 world, out Vector2 gui, out bool behind)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            behind = sp.z < 0f;
            gui = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
            return !behind && gui.x >= 0 && gui.x <= W && gui.y >= 0 && gui.y <= H;
        }

        void DrawPrompt(GameManager gm)
        {
            var cam = Camera.main;
            var ld = gm.leader;
            if (!cam || !ld || gm.State != GameState.Playing) return;

            string text = null;
            Color c = Color.white;
            Vector3 world = Vector3.zero;
            if (ld.CandidateFood)
            {
                var f = ld.CandidateFood;
                world = f.transform.position + Vector3.up * 0.55f;
                int free = JellyChain.Goal - gm.chain.CurrentLength;
                if (f.Length > free) { text = $"{f.Length}cm 공간이 필요해요"; c = CWarn; }
                else text = $"[Space] {FoodData.DisplayName(f.Type)} +{f.Length}cm";
            }
            else if (ld.CandidateFinal)
            {
                world = ld.CandidateFinal.transform.position + Vector3.up * 0.7f;
                if (ld.CandidateFinal.Locked) { text = $"100cm가 되어야 먹을 수 있어요 ({JellyChain.Goal - gm.chain.CurrentLength}cm 부족)"; c = CWarn; }
                else { text = "[Space] 마지막 1cm 먹기"; c = CPink; }
            }
            if (text == null) return;
            if (!WorldToGui(cam, world, out var p, out _)) return;
            var size = sPrompt.CalcSize(new GUIContent(text));
            Rect r = new Rect(p.x - size.x * 0.5f - 12, p.y - 20, size.x + 24, 38);
            Fill(r, CPanel);
            Label(r, text, sPrompt, c);
        }

        // ------------------------------------------------------------------ 방향 표시

        struct Indicator
        {
            public Vector3 pos;
            public string label;
            public Color color;
        }

        readonly List<Indicator> indicators = new List<Indicator>();
        readonly HashSet<JellyChunk> seenChunks = new HashSet<JellyChunk>();

        void DrawIndicators(GameManager gm)
        {
            var cam = Camera.main;
            if (!cam) return;
            indicators.Clear();
            seenChunks.Clear();

            if (gm.chain)
            {
                foreach (var u in gm.chain.Detached)
                {
                    if (!u) continue;
                    if (u.Chunk != null)
                    {
                        if (!seenChunks.Add(u.Chunk)) continue;
                        indicators.Add(new Indicator { pos = u.transform.position, label = $"동료 덩어리 {u.Chunk.Members.Count}", color = CWarn });
                    }
                    else indicators.Add(new Indicator { pos = u.transform.position, label = "동료", color = CWarn });
                }
            }
            if (gm.Final) indicators.Add(new Indicator { pos = gm.Final.transform.position, label = "마지막 1cm", color = CPink });
            foreach (var f in gm.RecentReplacements)
                if (f) indicators.Add(new Indicator { pos = f.transform.position, label = $"보충 +{f.Length}cm", color = CGood });

            Vector2 center = new Vector2(W * 0.5f, H * 0.5f);
            const float margin = 70f;
            foreach (var ind in indicators)
            {
                bool on = WorldToGui(cam, ind.pos, out var p, out bool behind);
                if (on) continue; // 화면 안: 월드의 빛 표시로 충분
                Vector2 dir = p - center;
                if (behind) dir = -dir;
                if (dir.sqrMagnitude < 1e-3f) dir = Vector2.down;
                dir.Normalize();
                float hx = W * 0.5f - margin, hy = H * 0.5f - margin;
                float k = Mathf.Min(hx / Mathf.Max(1e-3f, Mathf.Abs(dir.x)), hy / Mathf.Max(1e-3f, Mathf.Abs(dir.y)));
                Vector2 e = center + dir * k;

                string arrow = Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? (dir.x > 0 ? "▶" : "◀") : (dir.y > 0 ? "▼" : "▲");
                float dist = (ind.pos - gm.leader.transform.position).magnitude;
                string text = $"{arrow} {ind.label} {dist:0}m";
                var size = sPrompt.CalcSize(new GUIContent(text));
                Rect r = new Rect(e.x - size.x * 0.5f - 8, e.y - 16, size.x + 16, 32);
                Fill(r, CPanel);
                Label(r, text, sPrompt, ind.color);
            }
        }

        // ------------------------------------------------------------------ 알림 / 조작 안내

        void DrawToasts(GameManager gm)
        {
            float y = 150f;
            for (int i = gm.Toasts.Count - 1; i >= 0; i--)
            {
                var t = gm.Toasts[i];
                float age = Time.unscaledTime - t.time;
                float a = Mathf.Clamp01((t.duration - age) / 0.4f) * Mathf.Clamp01(age / 0.12f);
                var size = sCenter.CalcSize(new GUIContent(t.text));
                Rect r = new Rect(W * 0.5f - size.x * 0.5f - 18, y, size.x + 36, 44);
                Fill(r, new Color(0, 0, 0, 0.5f * a));
                Color c = KindColor(t.kind);
                c.a = a;
                Label(r, t.text, sCenter, c);
                y += 50f;
            }
        }

        void DrawControls()
        {
            Label(new Rect(24, H - 44, 1200, 30),
                "WASD 이동 · Shift 달리기(쿨타임) · 마우스 시점 · Space 먹기 · 1 손 흔들기 · 2 빙글빙글 · 3 폴짝 · 4 인사 · Esc 일시정지",
                sSmall, new Color(1, 1, 1, 0.75f));
        }

        // ------------------------------------------------------------------ 메뉴

        void Dim() => Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.6f));

        bool Button(float y, string text)
        {
            return GUI.Button(new Rect(W * 0.5f - 180, y, 360, 64), text, sButton);
        }

        void DrawPause(GameManager gm)
        {
            Dim();
            Label(new Rect(0, 260, W, 90), "일시정지", sTitle, Color.white);
            if (Button(420, "재개")) gm.Resume();
            if (Button(500, "처음부터 다시")) gm.RestartFromBeginning();
            if (Button(580, "종료")) gm.QuitGame();
            Label(new Rect(0, 680, W, 30), "체크포인트에서 다시하기는 Alpha 단계에서 추가됩니다", sPrompt, new Color(1, 1, 1, 0.6f), false);
        }

        void DrawStats(GameManager gm, float y)
        {
            Label(new Rect(0, y, W, 34), $"플레이 시간 {GameManager.FormatTime(gm.PlayTime)} · 먹은 음식 {gm.FoodEatenCount}개", sPrompt, new Color(1, 1, 1, 0.9f), false);
            string lost = $"떨어뜨린 동료 {gm.LostCount}마리 · 다시 모은 동료 {gm.RecoveredCount}마리 · 청소기에 잃은 동료 {gm.AbsorbedCount}마리";
            Label(new Rect(0, y + 34, W, 34), lost, sPrompt, gm.LostCount > 0 ? CWarn : new Color(1, 1, 1, 0.9f), false);
        }

        void DrawFail(GameManager gm)
        {
            Dim();
            Label(new Rect(0, 250, W, 90), "실패", sTitle, CWarn);
            Label(new Rect(0, 340, W, 40), gm.FailReason, sCenter, Color.white);
            DrawStats(gm, 395);
            if (Button(490, "다시 시작 (R)")) gm.RestartFromBeginning();
            if (Button(570, "종료")) gm.QuitGame();
        }

        void DrawResult(GameManager gm)
        {
            Dim();
            Label(new Rect(0, 200, W, 90), "101CM 달성!", sTitle, CGold);
            Label(new Rect(0, 290, W, 40), "동료들과 함께 마트를 탈출했어요", sCenter, Color.white);
            DrawStats(gm, 350);
            if (Button(460, "재시작 (R)")) gm.RestartFromBeginning();
            if (Button(540, "종료")) gm.QuitGame();
        }
    }
}
