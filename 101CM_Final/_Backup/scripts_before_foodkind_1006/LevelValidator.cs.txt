using System.Collections.Generic;
using System.Linq;
using System.Text;
using CM101.Level;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CM101.EditorTools
{
    /// <summary>
    /// 레벨 문서 v0.1 8장 '레벨 제작 체크리스트'를 열린 씬에 대해 검사한다. 메뉴: 101CM > Level > Validate Open Level
    /// 사람이 봐야 하는 항목(좁은 단 폭, 체크 타일 크기 등)은 '수동'으로 표시한다.
    /// </summary>
    public static class LevelValidator
    {
        const float Step = 0.25f;
        const float JellyR = 0.28f;
        const float JellyY = 0.31f;
        const int SolidMask = 1 << Layers.Solid;
        const float JumpStep = 1.05f; // 선두 점프(몸 중심 1.15 상승)로 오를 수 있는 한 단 높이

        static readonly Dictionary<ZoneId, int> SheetZoneCm = new Dictionary<ZoneId, int>
        {
            { ZoneId.Z0, 16 }, { ZoneId.Z1, 16 }, { ZoneId.Z2, 20 }, { ZoneId.Z3, 20 }, { ZoneId.Z4, 19 }, { ZoneId.Z5, 15 }, { ZoneId.Z6, 0 },
        };

        [MenuItem("101CM/Level/Validate Open Level")]
        public static void ValidateMenu()
        {
            string r = Run(out int fails);
            if (fails > 0) Debug.LogWarning(r); else Debug.Log(r);
        }

        static List<T> All<T>() where T : Component
        {
            var list = new List<T>();
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                list.AddRange(root.GetComponentsInChildren<T>(true));
            return list;
        }

        public static string Run(out int fails)
        {
            Physics.SyncTransforms();
            var sb = new StringBuilder();
            int fail = 0, ok = 0;
            void Pass(string s) { ok++; sb.AppendLine("  OK   " + s); }
            void Fail(string s) { fail++; sb.AppendLine("  실패 " + s); }
            void Note(string s) { sb.AppendLine("  수동 " + s); }

            var zones = All<ZoneVolume>().OrderBy(z => z.zone).ToList();
            var foods = All<FoodSpawn>();
            var points = All<PointMarker>();
            var hides = All<HideoutMarker>();
            var hazards = All<HazardMarker>();
            sb.AppendLine($"[101CM] 레벨 검사: {SceneManager.GetActiveScene().name}  (구역 {zones.Count} · 음식 {foods.Count} · 지점 {points.Count} · 은신처 {hides.Count} · 위험 {hazards.Count})");

            // 1. 음식 합계
            sb.AppendLine("■ 음식");
            var main = foods.Where(f => !f.tutorial).ToList();
            int total = main.Sum(f => f.Cm);
            if (total == 90) Pass($"본게임 음식 합계 90cm"); else Fail($"본게임 음식 합계 {total}cm (90 필요)");
            if (main.Count >= 18 && main.Count <= 30) Pass($"본게임 음식 {main.Count}개 (18~30)"); else Fail($"본게임 음식 {main.Count}개 (18~30 필요)");
            foreach (var z in zones)
            {
                int cm = foods.Where(f => f.zone == z.zone).Sum(f => f.Cm);
                int want = SheetZoneCm.TryGetValue(z.zone, out int w) ? w : z.foodTargetCm;
                if (cm == want) Pass($"{z.zone} 음식 {cm}cm = 시트"); else Fail($"{z.zone} 음식 {cm}cm (시트 {want})");
            }
            var dupIds = foods.Select(f => f.id).Concat(points.Select(p => p.id)).Concat(hides.Select(h => h.id)).Concat(hazards.Select(h => h.id))
                              .GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupIds.Count == 0) Pass("ID 중복 없음"); else Fail("ID 중복: " + string.Join(", ", dupIds));
            foreach (var f in foods)
            {
                var z = zones.FirstOrDefault(zz => zz.Contains(f.GroundPos));
                if (z == null || z.zone != f.zone) Fail($"{f.id}가 표기 구역 {f.zone} 밖에 있음");
            }
            int n5 = points.Count(p => p.kind == PointKind.ZoneSign), s5 = points.Count(p => p.kind == PointKind.Supply);
            if (n5 == 5 && s5 == 5) Pass("구역 안내판 5 · 보충 지점 5"); else Fail($"구역 안내판 {n5} · 보충 지점 {s5} (각 5 필요)");

            // 2. 도달 가능 (바닥 높이 격자 탐색, 젤리 반지름 0.28)
            sb.AppendLine("■ 도달·막힘");
            var start = points.FirstOrDefault(p => p.kind == PointKind.Start);
            if (start == null) { Fail("시작 지점 없음"); fails = fail; return sb.ToString(); }
            float maxX = zones.Count > 0 ? zones.Max(z => z.xMax) : 48f;
            float maxZ = zones.Count > 0 ? zones.Max(z => z.zMax) : 58f;
            int nx = Mathf.CeilToInt(maxX / Step), nz = Mathf.CeilToInt(maxZ / Step);
            var reach = new bool[nx, nz];
            var blocked = new bool[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                    blocked[i, k] = Physics.CheckSphere(new Vector3((i + 0.5f) * Step, JellyY, (k + 0.5f) * Step), JellyR, SolidMask, QueryTriggerInteraction.Ignore);
            var q = new Queue<Vector2Int>();
            Vector2Int s0 = Cell(start.transform.position);
            if (!blocked[s0.x, s0.y]) { reach[s0.x, s0.y] = true; q.Enqueue(s0); }
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in dirs)
                {
                    var n = c + d;
                    if (n.x < 0 || n.y < 0 || n.x >= nx || n.y >= nz || reach[n.x, n.y] || blocked[n.x, n.y]) continue;
                    reach[n.x, n.y] = true;
                    q.Enqueue(n);
                }
            }
            bool Reached(Vector3 p)
            {
                var c = Cell(p);
                for (int dx = -2; dx <= 2; dx++)
                    for (int dz = -2; dz <= 2; dz++)
                    {
                        int x = c.x + dx, z = c.y + dz;
                        if (x >= 0 && z >= 0 && x < nx && z < nz && reach[x, z]) return true;
                    }
                return false;
            }
            Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(p.x / Step), 0, nx - 1), Mathf.Clamp(Mathf.FloorToInt(p.z / Step), 0, nz - 1));

            // 점프 도달: 높은 곳 지점에서 JumpStep 이하로 낮은 발판을 따라 바닥(도달 가능 칸)까지 내려갈 수 있는가
            bool JumpReachable(Vector3 stand, int depth)
            {
                if (stand.y <= 0.05f) return Reached(stand);
                if (depth > 5) return false;
                for (float r = 0.9f; r <= 2.45f; r += 0.5f)
                    for (int a = 0; a < 12; a++)
                    {
                        Vector3 q = stand + Quaternion.Euler(0f, a * 30f, 0f) * Vector3.forward * r;
                        if (!Physics.Raycast(new Vector3(q.x, stand.y + 0.4f, q.z), Vector3.down, out RaycastHit h, stand.y + 0.6f, SolidMask, QueryTriggerInteraction.Ignore)) continue;
                        float ys = h.point.y;
                        if (ys >= stand.y - 0.05f || stand.y - ys > JumpStep) continue;
                        if (Physics.CheckSphere(h.point + Vector3.up * (JellyY + 0.02f), JellyR, SolidMask, QueryTriggerInteraction.Ignore)) continue;
                        if (JumpReachable(h.point, depth + 1)) return true;
                    }
                return false;
            }

            var unreached = new List<string>();
            foreach (var f in foods)
            {
                Vector3 p = f.GroundPos;
                if (p.y > 0.1f)
                {
                    bool clear = !Physics.CheckSphere(p + Vector3.up * (JellyY + 0.02f), JellyR, SolidMask, QueryTriggerInteraction.Ignore);
                    bool ground = Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, 0.5f, SolidMask, QueryTriggerInteraction.Ignore);
                    if (!clear || !ground) Fail($"{f.id} 높은 곳 음식이 묻혀 있거나 떠 있음");
                    else if (JumpReachable(p, 0)) Pass($"{f.id} 높이 {p.y:0.0} — 점프로 도달 가능 (한 번에 {JumpStep}u 이하)");
                    else Fail($"{f.id} 높이 {p.y:0.0} — 점프로 오를 발판이 없음");
                }
                else if (!Reached(p)) unreached.Add(f.id);
            }
            foreach (var pt in points) if (!Reached(pt.transform.position)) unreached.Add(pt.id);
            foreach (var h in hides) if (h.path != null && h.path.Length > 0 && !Reached(h.path[0])) unreached.Add(h.id + " 입구");
            if (unreached.Count == 0) Pass("시작 지점에서 모든 바닥 음식·지점·은신처 입구에 도달 가능");
            else Fail("도달 불가: " + string.Join(", ", unreached));

            // 무너진 상품이 유일한 통로를 막지 않는가: 낙하 영역을 막은 채 다시 탐색
            foreach (var hz in hazards.Where(h => h.kind == HazardKind.FallingShelf))
            {
                var r2 = new bool[nx, nz];
                bool InLanding(int i, int k)
                {
                    float x = (i + 0.5f) * Step, z = (k + 0.5f) * Step;
                    return Mathf.Abs(x - hz.areaCenter.x) <= hz.areaSize.x * 0.5f && Mathf.Abs(z - hz.areaCenter.z) <= hz.areaSize.z * 0.5f;
                }
                var q2 = new Queue<Vector2Int>();
                r2[s0.x, s0.y] = true;
                q2.Enqueue(s0);
                while (q2.Count > 0)
                {
                    var c = q2.Dequeue();
                    foreach (var d in dirs)
                    {
                        var n = c + d;
                        if (n.x < 0 || n.y < 0 || n.x >= nx || n.y >= nz || r2[n.x, n.y] || blocked[n.x, n.y] || InLanding(n.x, n.y)) continue;
                        r2[n.x, n.y] = true;
                        q2.Enqueue(n);
                    }
                }
                var exit = points.FirstOrDefault(p => p.kind == PointKind.Exit);
                bool exitOk = exit != null && r2[Cell(exit.transform.position).x, Cell(exit.transform.position).y];
                if (exitOk) Pass($"{hz.id} 낙하 영역을 막아도 출구까지 우회로 있음"); else Fail($"{hz.id} 낙하 영역이 유일한 통로를 막음");
            }

            // 3. 구역 안내판·보충 지점은 위험 영역 밖
            sb.AppendLine("■ 안전 지점");
            foreach (var p in points.Where(p => p.kind == PointKind.ZoneSign || p.kind == PointKind.Supply))
            {
                var near = hazards.Where(h => h.kind != HazardKind.Rollers && h.zone == p.zone).Select(h => (h, d: h.DistanceTo(p.transform.position))).OrderBy(x => x.d).FirstOrDefault();
                if (near.h == null || near.d >= 1.0f) Pass($"{p.id} 위험 영역 밖 (가장 가까운 {near.h?.id ?? "-"} {near.d:0.0}u)");
                else Fail($"{p.id} 위험 영역에서 {near.d:0.0}u ({near.h.id}) — 1u 이상 필요");
            }

            // 4. 출구 구역(골 홀)에는 위험이 없어야 한다
            var goal = zones.FirstOrDefault(z => z.zone == ZoneId.Z6);
            var final = points.FirstOrDefault(p => p.kind == PointKind.FinalJelly);
            if (final && goal)
            {
                var bad = hazards.Where(h => h.zone == ZoneId.Z6 || h.DistanceTo(final.transform.position) < 6f).Select(h => h.id).ToList();
                if (bad.Count == 0) Pass("골 홀에 위험 없음 · 최종 젤리 6타일 안에 위험 없음"); else Fail("골 홀/최종 젤리 근처 위험: " + string.Join(", ", bad));
            }
            else Fail("최종 젤리 지점 또는 골 홀 없음");

            // 5. 출구 가시성
            Vector3 eye = LevelBuilder.WarehouseDoorEye;
            Vector3 beacon = LevelBuilder.BeaconTop;
            if (!Physics.Linecast(eye, beacon, out RaycastHit lh, SolidMask, QueryTriggerInteraction.Ignore)) Pass("창고 문에서 출구 빛기둥이 보임");
            else Fail($"창고 문에서 출구 빛기둥이 가려짐: {lh.collider.name} (z {lh.point.z:0.0})");

            // 6. 은신처
            sb.AppendLine("■ 은신처");
            foreach (var h in hides.Where(h => h.kind != HideoutKind.Practice))
            {
                float len = h.PathLength;
                if (len >= h.RequiredLength) Pass($"{h.id} 내부 경로 {len:0.0}u ≥ {h.RequiredLength}u ({h.CapacityCm}cm)");
                else Fail($"{h.id} 내부 경로 {len:0.0}u < {h.RequiredLength}u");
                bool clear = true;
                for (int i = 1; i < h.path.Length && clear; i++)
                {
                    Vector3 a = h.path[i - 1], b = h.path[i];
                    // 경로는 내부 벽면까지의 길이다. 젤리 중심은 끝 벽에서 반지름만큼 떨어지므로 양 끝을 줄여 검사한다.
                    Vector3 sd = (b - a).normalized;
                    if (i == 1) a += sd * (JellyR + 0.02f);
                    if (i == h.path.Length - 1) b -= sd * (JellyR + 0.02f);
                    int steps = Mathf.CeilToInt(Vector3.Distance(a, b) / 0.2f);
                    for (int s = 0; s <= steps; s++)
                        if (Physics.CheckSphere(Vector3.Lerp(a, b, s / (float)steps) + Vector3.up * JellyY, JellyR, SolidMask, QueryTriggerInteraction.Ignore)) { clear = false; break; }
                }
                if (clear) Pass($"{h.id} 내부 경로에 막힘 없음"); else Fail($"{h.id} 내부 경로가 막힘");

                var fp = h.Footprint;
                foreach (var st in hazards.Where(x => x.kind == HazardKind.Staff))
                {
                    var probe = new Vector3(fp.center.x, 0f, fp.center.z);
                    float dx = Mathf.Max(0f, fp.extents.x), dz = Mathf.Max(0f, fp.extents.z);
                    bool over = false;
                    for (float x = -dx; x <= dx && !over; x += 0.25f)
                        for (float z = -dz; z <= dz && !over; z += 0.25f)
                            if (st.DistanceTo(probe + new Vector3(x, 0f, z)) <= 0f) over = true;
                    if (over) Fail($"{st.id} 순찰 경로가 {h.id} 위를 지남");
                }
                if (h.kind == HideoutKind.Long && h.path.Length >= 2)
                {
                    var ends = new List<(Vector3 p, Vector3 dir)> { (h.path[0], (h.path[0] - h.path[1]).normalized) };
                    if (h.twoWay) ends.Add((h.path[h.path.Length - 1], (h.path[h.path.Length - 1] - h.path[h.path.Length - 2]).normalized));
                    foreach (var vac in hazards.Where(x => x.kind == HazardKind.Vacuum))
                        foreach (var e in ends)
                            for (float t = 0.5f; t <= 6f; t += 0.5f)
                                if (vac.DistanceTo(e.p + e.dir * t) <= 0f) { Fail($"{h.id} 입구가 {vac.id} 경로를 향함"); goto nextVac; }
                    nextVac:;
                }
            }

            // 7. 구역마다 U턴 가능한 3×3 공간
            sb.AppendLine("■ 구역 공간");
            foreach (var z in zones)
            {
                bool found = false;
                for (float x = z.xMin + 1.7f; x <= z.xMax - 1.7f && !found; x += 0.5f)
                    for (float zz = z.zMin + 1.7f; zz <= z.zMax - 1.7f && !found; zz += 0.5f)
                        if (!Physics.CheckBox(new Vector3(x, 0.35f, zz), new Vector3(1.5f, 0.3f, 1.5f), Quaternion.identity, SolidMask, QueryTriggerInteraction.Ignore))
                            found = true;
                if (found) Pass($"{z.zone} 3×3 U턴 공간 있음"); else Fail($"{z.zone} 3×3 U턴 공간 없음");
            }
            Note("좁은 단 최소 1.2u · 주 통로 3u 이상 — 캡처와 플레이로 확인");
            Note("위험 예고 시간 손실(100cm 꼬리)은 관문 3 테스트 항목");

            sb.Insert(0, $"결과: 통과 {ok} · 실패 {fail}\n");
            fails = fail;
            return sb.ToString();
        }
    }
}
