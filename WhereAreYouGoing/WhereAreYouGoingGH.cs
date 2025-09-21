using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Coroutine;
using GameHelper;
using GameHelper.CoroutineEvents;
using GameHelper.Plugin;
using GameHelper.RemoteEnums;
using GameHelper.RemoteEnums.Entity;
using GameHelper.RemoteObjects.Components;
using GameHelper.RemoteObjects.States.InGameStateObjects;
using GameHelper.Utils;
using GameOffsets.Natives; // StdTuple3D<float>
using ImGuiNET;
using Newtonsoft.Json;

namespace WhereAreYouGoingGH
{
    public sealed class WhereAreYouGoingGH : PCore<WhereAreYouGoingGHSettings>
    {
        private readonly Dictionary<uint, TrailState> _trails = new();
        private ActiveCoroutine _onAreaChange;

        private string SettingsPath =>
            Path.Join(this.DllDirectory, "config", "WhereAreYouGoingGH.settings.json");

        public override void OnEnable(bool isGameOpened)
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var loaded = JsonConvert.DeserializeObject<WhereAreYouGoingGHSettings>(json);
                    if (loaded != null) this.Settings = loaded;
                }
            }
            catch { /* ignore */ }

            _onAreaChange = CoroutineHandler.Start(OnAreaChange(), "", 0);
        }

        public override void OnDisable()
        {
            _onAreaChange?.Cancel();
            _onAreaChange = null;
            _trails.Clear();
        }

        public override void SaveSettings()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this.Settings, Formatting.Indented));
            }
            catch { /* ignore */ }
        }

        public override void DrawSettings()
        {
            ImGui.Checkbox("Enable##WAYG", ref Settings.Enable);
            ImGui.Separator();

            // --- Common Settings category ---
            if (ImGui.CollapsingHeader("Common Settings##WAYG_Common"))
            {
                ImGui.Checkbox("Draw in Town##WAYG", ref Settings.DrawInTown);
                ImGui.Checkbox("Draw in Hideout##WAYG", ref Settings.DrawInHideout);
                ImGui.Checkbox("Draw when game in background##WAYG", ref Settings.DrawWhenGameInBackground);

                ImGui.DragInt("Max draw distance##WAYG", ref Settings.MaxCircleDrawDistance, 1, 0, 1000);
                ImGui.DragInt("Projection horizon (ms)##WAYG", ref Settings.ProjectionMs, 10, 100, 3000);
                ImGui.DragFloat("Min speed to project##WAYG", ref Settings.MinSpeedToProject, 0.01f, 0f, 10f);

                ImGui.DragInt("Max projected screen length (px, 0=none)##WAYG", ref Settings.MaxProjectedScreenLength, 5, 0, 3000);

                ImGui.Checkbox("Draw Lines##WAYG", ref Settings.DrawLine);
                ImGui.Checkbox("Draw Endpoint Circle##WAYG", ref Settings.DrawEndpointCircle);
                ImGui.Checkbox("Draw Origin Circle (at entity)##WAYG", ref Settings.DrawOriginCircle);
                ImGui.Checkbox("Draw Bounding Box Instead of Circle##WAYG", ref Settings.DrawBoundingBoxInstead);
                ImGui.Checkbox("Draw Filled Circles##WAYG", ref Settings.DrawFilledCircles);

                ImGui.DragInt("Circle Thickness##WAYG", ref Settings.CircleThickness, 1, 1, 20);
                ImGui.DragInt("Line Thickness##WAYG", ref Settings.LineThickness, 1, 1, 20);

                ImGui.Separator();
                ImGui.TextUnformatted("Smoothing");
                ImGui.SliderFloat("Velocity Smoothing (0..1)##WAYG", ref Settings.VelocitySmoothing, 0f, 0.95f);
                ImGui.SliderFloat("Screen Smoothing (0..1)##WAYG", ref Settings.ScreenSmoothing, 0f, 0.95f);

                ImGui.Separator();
                ImGui.TextUnformatted("Grouping (Monsters by Rarity)");
                ImGui.Checkbox("Enable Grouping##WAYG", ref Settings.GroupByRarity);
                ImGui.DragFloat("Group Radius (px)##WAYG", ref Settings.GroupRadiusPx, 1f, 20f, 600f);
                ImGui.DragInt("Group Min Count##WAYG", ref Settings.GroupMinCount, 1f, 2, 20);

                ImGui.Checkbox("Show Cluster Members (spokes + dots)##WAYG", ref Settings.ShowClusterMembers);
                ImGui.SliderFloat("Member Spoke Alpha##WAYG", ref Settings.MemberSpokeAlpha, 0f, 1f);
                ImGui.DragInt("Member Spoke Thickness##WAYG", ref Settings.MemberSpokeThickness, 1, 1, 6);
                ImGui.DragFloat("Member Dot Radius##WAYG", ref Settings.MemberDotRadius, 0.1f, 1f, 12f);
                ImGui.Checkbox("Show Cluster Count Label##WAYG", ref Settings.ShowClusterCount);
            }

            ImGui.Separator();
            DrawUnit("Normal", "WAYG_Normal", Settings.Normal);
            DrawUnit("Magic", "WAYG_Magic", Settings.Magic);
            DrawUnit("Rare", "WAYG_Rare", Settings.Rare);
            DrawUnit("Unique", "WAYG_Unique", Settings.Unique);
            DrawUnit("Players", "WAYG_Players", Settings.Players);
            DrawUnit("Self", "WAYG_Self", Settings.Self);
            DrawUnit("Friendly", "WAYG_Friendly", Settings.Friendly);

            static void DrawUnit(string title, string key, WhereAreYouGoingGHSettings.WAYGUnitConfig cfg)
            {
                if (!ImGui.CollapsingHeader($"{title}##{key}")) return;
                ImGui.Checkbox($"Enable##{key}", ref cfg.Enable);
                ImGui.ColorEdit4($"Color##{key}", ref cfg.Color);
            }
        }

        public override void DrawUI()
        {
            if (!Settings.Enable) return;

            // Updated state check per your request
            if (Core.States.GameCurrentState != GameStateTypes.InGameState
                && Core.States.GameCurrentState != GameStateTypes.EscapeState)
                return;

            var ig = Core.States.InGameStateObject;
            var world = ig.CurrentWorldInstance;
            var area = ig.CurrentAreaInstance;

            if ((!Settings.DrawInTown && world.AreaDetails.IsTown) ||
                (!Settings.DrawInHideout && world.AreaDetails.IsHideout))
                return;

            if (!Settings.DrawWhenGameInBackground && !Core.Process.Foreground) return;

            var draw = ImGui.GetBackgroundDrawList();

            // Collect per-rarity monster lines and non-monster samples.
            var monsterSamplesByRarity = new Dictionary<Rarity, List<LineSample>>();
            var monsterMembersByRarity = new Dictionary<Rarity, List<MemberMark>>();
            var nonMonsterSamples = new List<LineSample>();

            foreach (var kv in area.AwakeEntities)
            {
                var e = kv.Value;
                if (e == null || !e.IsValid) continue;

                // Exclude hidden/useless/friendly
                if (e.EntityState == EntityStates.PinnacleBossHidden
                    || e.EntityState == EntityStates.Useless
                    || e.EntityState == EntityStates.MonsterFriendly)
                {
                    continue;
                }

                // Resolve config and rarity
                var cfg = ResolveConfig(e, ig.CurrentAreaInstance.Player.Id, out Rarity? rarityOpt);
                if (cfg == null || !cfg.Enable) continue;

                // Render for positions
                Render r;
                if (!e.TryGetComponent<Render>(out r, true)) continue;

                var pos = r.WorldPosition; // StdTuple3D<float>
                var startScreenRaw = world.WorldToScreen(pos, pos.Z);
                if (startScreenRaw == Vector2.Zero) continue;

                // Trails
                if (!_trails.TryGetValue(e.Id, out var tr))
                {
                    tr = new TrailState
                    {
                        LastTick = Environment.TickCount64,
                        LastPos = new Vector2(pos.X, pos.Y),
                        Velocity = Vector2.Zero,
                        Initialized = false,
                        SmoothedStart = startScreenRaw,
                        SmoothedEnd = startScreenRaw
                    };
                }

                var now = Environment.TickCount64;
                var dtMs = Math.Max(1, now - tr.LastTick);
                var curWorldXY = new Vector2(pos.X, pos.Y);
                var instVel = (curWorldXY - tr.LastPos) * (1000f / dtMs); // world units/sec

                // velocity smoothing
                var vAlpha = Math.Clamp(Settings.VelocitySmoothing, 0f, 0.95f);
                tr.Velocity = tr.Initialized ? Vector2.Lerp(tr.Velocity, instVel, vAlpha) : instVel;
                tr.Initialized = true;
                tr.LastPos = curWorldXY;
                tr.LastTick = now;

                // project endpoint and clamp
                var proj = tr.Velocity * (Settings.ProjectionMs / 1000f);
                var endWorld = new StdTuple3D<float> { X = pos.X + proj.X, Y = pos.Y + proj.Y, Z = pos.Z };
                var endScreenRaw = world.WorldToScreen(endWorld, endWorld.Z);

                if (Settings.MaxProjectedScreenLength > 0)
                {
                    var dir = endScreenRaw - startScreenRaw;
                    var len = dir.Length();
                    if (len > Settings.MaxProjectedScreenLength && len > 0.001f)
                    {
                        dir = dir / len * Settings.MaxProjectedScreenLength;
                        endScreenRaw = startScreenRaw + dir;
                    }
                }

                // screen-space smoothing
                var sAlpha = Math.Clamp(Settings.ScreenSmoothing, 0f, 0.95f);
                tr.SmoothedStart = Vector2.Lerp(tr.SmoothedStart, startScreenRaw, sAlpha);
                tr.SmoothedEnd = Vector2.Lerp(tr.SmoothedEnd, endScreenRaw, sAlpha);

                _trails[e.Id] = tr;

                // draw threshold
                var speedPerFrame = tr.Velocity.Length() / 60f;
                if (speedPerFrame < Settings.MinSpeedToProject) continue;

                var unitColor = cfg.Color;

                var sample = new LineSample
                {
                    Start = tr.SmoothedStart,
                    End = tr.SmoothedEnd,
                    Color = unitColor,
                    Thickness = Settings.LineThickness
                };

                bool isMonster = e.EntityType == EntityTypes.Monster;
                if (isMonster && rarityOpt.HasValue)
                {
                    if (!monsterSamplesByRarity.TryGetValue(rarityOpt.Value, out var list))
                    {
                        list = new List<LineSample>();
                        monsterSamplesByRarity[rarityOpt.Value] = list;
                    }
                    list.Add(sample);

                    if (Settings.ShowClusterMembers)
                    {
                        if (!monsterMembersByRarity.TryGetValue(rarityOpt.Value, out var memberList))
                        {
                            memberList = new List<MemberMark>();
                            monsterMembersByRarity[rarityOpt.Value] = memberList;
                        }
                        memberList.Add(new MemberMark { Position = tr.SmoothedStart, Color = unitColor });
                    }
                }
                else
                {
                    nonMonsterSamples.Add(sample);
                }

                // Optional origin circle
                if (Settings.DrawOriginCircle)
                {
                    var colorU32 = ImGuiHelper.Color(unitColor);
                    if (Settings.DrawBoundingBoxInstead)
                    {
                        var half = Math.Clamp(r.ModelBounds.X, 8, 200);
                        draw.AddRect(tr.SmoothedStart - new Vector2(half, half),
                                     tr.SmoothedStart + new Vector2(half, half),
                                     colorU32, 0, 0, Settings.CircleThickness);
                    }
                    else
                    {
                        var radiusPx = Math.Clamp(r.ModelBounds.X * 1.2f, 5, 120);
                        if (Settings.DrawFilledCircles)
                            draw.AddCircleFilled(tr.SmoothedStart, radiusPx, colorU32);
                        else
                            draw.AddCircle(tr.SmoothedStart, radiusPx, colorU32, 32, Settings.CircleThickness);
                    }
                }
            }

            // Draw grouped monster lines
            if (Settings.GroupByRarity && Settings.GroupMinCount >= 2)
            {
                foreach (var kv in monsterSamplesByRarity)
                {
                    var rarity = kv.Key;
                    var clustered = ClusterAndAverage(kv.Value, Settings.GroupRadiusPx, Settings.GroupMinCount);

                    // Member visuals for clarity
                    if (Settings.ShowClusterMembers && monsterMembersByRarity.TryGetValue(rarity, out var memberList))
                    {
                        foreach (var cluster in clustered)
                            DrawClusterMembers(draw, cluster, memberList, Settings);
                    }

                    DrawSamples(draw, clustered, Settings);

                    if (Settings.ShowClusterCount)
                    {
                        foreach (var cluster in clustered)
                        {
                            int count = 1;
                            if (monsterMembersByRarity.TryGetValue(rarity, out var countList))
                                count = countList.Count(m => Vector2.DistanceSquared(m.Position, cluster.Start)
                                                             <= Settings.GroupRadiusPx * Settings.GroupRadiusPx);

                            var txt = count.ToString();
                            var sz = ImGui.CalcTextSize(txt);
                            var p = cluster.Start - new Vector2(sz.X * 0.5f, sz.Y + 2f);
                            draw.AddText(p, ImGuiHelper.Color(cluster.Color), txt);
                        }
                    }
                }
            }
            else
            {
                foreach (var kv in monsterSamplesByRarity)
                    DrawSamples(draw, kv.Value, Settings);
            }

            // Draw non-monster lines (no grouping)
            DrawSamples(draw, nonMonsterSamples, Settings);
        }

        private static void DrawSamples(ImDrawListPtr draw, List<LineSample> samples, WhereAreYouGoingGHSettings s)
        {
            if (!s.DrawLine && !s.DrawEndpointCircle) return;

            foreach (var smp in samples)
            {
                uint col = ImGuiHelper.Color(smp.Color);

                if (s.DrawLine)
                    draw.AddLine(smp.Start, smp.End, col, smp.Thickness);

                if (s.DrawEndpointCircle)
                {
                    const float rpx = 6f;
                    if (s.DrawFilledCircles)
                        draw.AddCircleFilled(smp.End, rpx, col);
                    else
                        draw.AddCircle(smp.End, rpx, col, 16, s.CircleThickness);
                }
            }
        }

        private static void DrawClusterMembers(ImDrawListPtr draw, LineSample cluster, List<MemberMark> memberList, WhereAreYouGoingGHSettings s)
        {
            // Faint spokes + dots to show membership.
            var col = cluster.Color;
            col.W *= Math.Clamp(s.MemberSpokeAlpha, 0f, 1f);
            uint faded = ImGuiHelper.Color(col);

            float r2 = s.GroupRadiusPx * s.GroupRadiusPx;

            foreach (var m in memberList)
            {
                if (Vector2.DistanceSquared(m.Position, cluster.Start) > r2)
                    continue;

                if (s.MemberSpokeThickness > 0)
                    draw.AddLine(cluster.Start, m.Position, faded, s.MemberSpokeThickness);

                var mc = m.Color; mc.W *= Math.Clamp(s.MemberSpokeAlpha, 0f, 1f);
                uint mcol = ImGuiHelper.Color(mc);
                if (s.MemberDotRadius > 0.1f)
                    draw.AddCircleFilled(m.Position, s.MemberDotRadius, mcol);
            }
        }

        // Simple greedy clustering in screen space
        private static List<LineSample> ClusterAndAverage(List<LineSample> input, float radiusPx, int minCount)
        {
            var remaining = new List<LineSample>(input);
            var result = new List<LineSample>();
            float r2 = radiusPx * radiusPx;

            while (remaining.Count > 0)
            {
                var seed = remaining[0];
                var cluster = new List<LineSample> { seed };
                var rest = new List<LineSample>();

                for (int i = 1; i < remaining.Count; i++)
                {
                    var item = remaining[i];
                    var d2 = Vector2.DistanceSquared(seed.Start, item.Start);
                    if (d2 <= r2) cluster.Add(item);
                    else rest.Add(item);
                }

                remaining = rest;

                if (cluster.Count >= minCount)
                {
                    var avgStart = Vector2.Zero;
                    var avgEnd = Vector2.Zero;
                    float thick = 0f;

                    foreach (var c in cluster)
                    {
                        avgStart += c.Start;
                        avgEnd += c.End;
                        thick += c.Thickness;
                    }

                    avgStart /= cluster.Count;
                    avgEnd /= cluster.Count;
                    float avgThick = thick / cluster.Count;

                    result.Add(new LineSample
                    {
                        Start = avgStart,
                        End = avgEnd,
                        Color = cluster[0].Color,
                        Thickness = avgThick
                    });
                }
                else
                {
                    result.AddRange(cluster);
                }
            }

            return result;
        }

        private WhereAreYouGoingGHSettings.WAYGUnitConfig? ResolveConfig(
            dynamic e, uint selfPlayerId, out Rarity? rarityOpt)
        {
            rarityOpt = null;

            if (e.EntityType == EntityTypes.Player)
                return e.Id == selfPlayerId ? Settings.Self : Settings.Players;

            if (e.EntityType == EntityTypes.Monster)
            {
                ObjectMagicProperties omp;
                if (e.TryGetComponent<ObjectMagicProperties>(out omp, true))
                {
                    rarityOpt = omp.Rarity;
                    return omp.Rarity switch
                    {
                        Rarity.Normal => Settings.Normal,
                        Rarity.Magic => Settings.Magic,
                        Rarity.Rare => Settings.Rare,
                        Rarity.Unique => Settings.Unique,
                        _ => Settings.Normal
                    };
                }
                rarityOpt = Rarity.Normal;
                return Settings.Normal;
            }

            return Settings.Friendly;
        }

        private IEnumerator<Wait> OnAreaChange()
        {
            for (; ; )
            {
                yield return new Wait(RemoteEvents.AreaChanged);
                _trails.Clear();
            }
        }

        private struct TrailState
        {
            public long LastTick;
            public Vector2 LastPos;
            public Vector2 Velocity;
            public bool Initialized;

            public Vector2 SmoothedStart;
            public Vector2 SmoothedEnd;
        }

        private struct LineSample
        {
            public Vector2 Start;
            public Vector2 End;
            public Vector4 Color;
            public float Thickness;
        }

        private struct MemberMark
        {
            public Vector2 Position;
            public Vector4 Color;
        }
    }
}
