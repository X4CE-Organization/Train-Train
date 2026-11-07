using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrainTrain
{
    public enum RailColor
    {
        Crimson = 0,
        Azure = 1,
        Amber = 2,
        Violet = 3
    }

    public readonly struct RailEdge : IEquatable<RailEdge>
    {
        public readonly Vector2Int A;
        public readonly Vector2Int B;

        public RailEdge(Vector2Int a, Vector2Int b)
        {
            A = a;
            B = b;
        }

        public bool Equals(RailEdge other)
        {
            return (A == other.A && B == other.B) || (A == other.B && B == other.A);
        }

        public override bool Equals(object obj)
        {
            return obj is RailEdge other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int low = Mathf.Min(A.x, B.x) * 397 + Mathf.Min(A.y, B.y);
                int high = Mathf.Max(A.x, B.x) * 397 + Mathf.Max(A.y, B.y);
                return low * 31 + high;
            }
        }
    }

    public readonly struct StationDefinition
    {
        public readonly Vector2Int Position;
        public readonly Vector2Int Link;
        public readonly RailColor Color;
        public readonly bool IsSource;
        public readonly int InitialPassengers;
        public readonly int Capacity;
        public readonly float SpawnInterval;
        public readonly int GenerationMask;

        public StationDefinition(Vector2Int position, Vector2Int link, RailColor color, bool isSource)
            : this(position, link, color, isSource, 5, 20, 2f, 15)
        {
        }

        public StationDefinition(
            Vector2Int position,
            Vector2Int link,
            RailColor color,
            bool isSource,
            int initialPassengers,
            int capacity,
            float spawnInterval,
            int generationMask)
        {
            Position = position;
            Link = link;
            Color = color;
            IsSource = isSource;
            InitialPassengers = initialPassengers;
            Capacity = capacity;
            SpawnInterval = spawnInterval;
            GenerationMask = generationMask;
        }
    }

    public sealed class RailMap
    {
        public const float ElevatedHeight = 0.72f;

        public readonly string Name;
        public readonly List<RailEdge> Edges;
        public readonly List<StationDefinition> Stations;
        public readonly Dictionary<RailEdge, TrackKind> EdgeKinds;

        public RailMap(string name, List<RailEdge> edges, List<StationDefinition> stations)
            : this(name, edges, stations, null)
        {
        }

        public RailMap(
            string name,
            List<RailEdge> edges,
            List<StationDefinition> stations,
            Dictionary<RailEdge, TrackKind> edgeKinds)
        {
            Name = name;
            Edges = edges;
            Stations = stations;
            EdgeKinds = edgeKinds ?? new Dictionary<RailEdge, TrackKind>();
        }

        public TrackKind KindOf(RailEdge edge)
        {
            return EdgeKinds.TryGetValue(edge, out TrackKind kind) ? kind : TrackKind.Standard;
        }

        public bool NodeTouchesBridge(Vector2Int node)
        {
            for (int i = 0; i < Edges.Count; i++)
            {
                RailEdge edge = Edges[i];
                if ((edge.A == node || edge.B == node) && KindOf(edge) == TrackKind.Bridge)
                {
                    return true;
                }
            }

            return false;
        }

        public float ElevationAt(RailEdge edge, Vector2Int node)
        {
            TrackKind kind = KindOf(edge);
            if (kind == TrackKind.Bridge)
            {
                return ElevatedHeight;
            }

            if (kind == TrackKind.Ramp && NodeTouchesBridge(node))
            {
                return ElevatedHeight;
            }

            return 0f;
        }

        public static bool IsGroundKind(TrackKind kind)
        {
            return kind == TrackKind.Standard ||
                   kind == TrackKind.Tunnel ||
                   kind == TrackKind.SideTrack;
        }
    }

    public sealed class RegionDefinition
    {
        public readonly int Index;
        public readonly string NameCn;
        public readonly string NameEn;
        public readonly string TaglineCn;
        public readonly Color Sky;
        public readonly Color Fog;
        public readonly Color Ground;
        public readonly Color GroundAlt;
        public readonly Color Foliage;
        public readonly Color Rock;
        public readonly Color Accent;
        public readonly Color RailWood;
        public readonly RailMap Map;

        public RegionDefinition(
            int index,
            string nameCn,
            string nameEn,
            string taglineCn,
            Color sky,
            Color fog,
            Color ground,
            Color groundAlt,
            Color foliage,
            Color rock,
            Color accent,
            Color railWood,
            RailMap map)
        {
            Index = index;
            NameCn = nameCn;
            NameEn = nameEn;
            TaglineCn = taglineCn;
            Sky = sky;
            Fog = fog;
            Ground = ground;
            GroundAlt = groundAlt;
            Foliage = foliage;
            Rock = rock;
            Accent = accent;
            RailWood = railWood;
            Map = map;
        }
    }

    public sealed class WorldDefinition
    {
        public readonly RegionDefinition Region;
        public readonly List<LevelDefinition> Levels;

        public WorldDefinition(RegionDefinition region, List<LevelDefinition> levels)
        {
            Region = region;
            Levels = levels;
        }
    }

    public sealed class LevelDefinition
    {
        public readonly int Index;
        public readonly int RegionIndex;
        public readonly int LocalIndex;
        public readonly string NameCn;
        public readonly string NameEn;
        public readonly string Hint;
        public readonly int[] Quotas;
        public readonly float Duration;
        public readonly float TrainSpeed;
        public readonly RegionDefinition Region;
        public readonly RailMap Map;
        public readonly int FleetSize;
        public readonly int InitialPassengers;
        public readonly int StationCapacity;
        public readonly float StationSpawnInterval;
        public readonly int SupportedColorMask;

        public int TotalPassengers
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Quotas.Length; i++)
                {
                    total += Quotas[i];
                }

                return total;
            }
        }

        public LevelDefinition(
            int index,
            int regionIndex,
            int localIndex,
            string nameCn,
            string nameEn,
            string hint,
            int[] quotas,
            float duration,
            float trainSpeed,
            RegionDefinition region,
            RailMap map,
            int fleetSize,
            int initialPassengers,
            int stationCapacity,
            float stationSpawnInterval,
            int supportedColorMask)
        {
            Index = index;
            RegionIndex = regionIndex;
            LocalIndex = localIndex;
            NameCn = nameCn;
            NameEn = nameEn;
            Hint = hint;
            Quotas = quotas;
            Duration = duration;
            TrainSpeed = trainSpeed;
            Region = region;
            Map = map;
            FleetSize = fleetSize;
            InitialPassengers = initialPassengers;
            StationCapacity = stationCapacity;
            StationSpawnInterval = stationSpawnInterval;
            SupportedColorMask = supportedColorMask;
        }
    }

    public static class RailPalette
    {
        public static readonly Color Crimson = Hex(0xF04B5A);
        public static readonly Color Azure = Hex(0x42CDEB);
        public static readonly Color Amber = Hex(0xFFC857);
        public static readonly Color Violet = Hex(0xB985FF);
        public static readonly Color Ink = Hex(0x14212B);
        public static readonly Color Paper = Hex(0xF5F1E8);

        public static Color ForColor(RailColor color)
        {
            switch (color)
            {
                case RailColor.Crimson:
                    return Crimson;
                case RailColor.Azure:
                    return Azure;
                case RailColor.Amber:
                    return Amber;
                default:
                    return Violet;
            }
        }

        public static string NameCn(RailColor color)
        {
            switch (color)
            {
                case RailColor.Crimson:
                    return "绛红";
                case RailColor.Azure:
                    return "青蓝";
                case RailColor.Amber:
                    return "琥珀";
                default:
                    return "紫罗兰";
            }
        }

        private static Color Hex(int rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                1f);
        }
    }

    public static class LevelCatalog
    {
        private static List<RegionDefinition> regions;
        private static List<LevelDefinition> levels;
        private static List<WorldDefinition> worlds;

        public static IReadOnlyList<RegionDefinition> Regions
        {
            get
            {
                EnsureBuilt();
                return regions;
            }
        }

        public static IReadOnlyList<LevelDefinition> Levels
        {
            get
            {
                EnsureBuilt();
                return levels;
            }
        }

        public static IReadOnlyList<WorldDefinition> Worlds
        {
            get
            {
                EnsureBuilt();
                return worlds;
            }
        }

        public static LevelDefinition GetLevel(int index)
        {
            EnsureBuilt();
            return levels[Mathf.Clamp(index, 0, levels.Count - 1)];
        }

        public static int FirstLevelIndex(int regionIndex)
        {
            EnsureBuilt();
            int first = 0;
            for (int i = 0; i < worlds.Count; i++)
            {
                if (i == regionIndex)
                {
                    return first;
                }

                first += worlds[i].Levels.Count;
            }

            return Mathf.Max(0, levels.Count - 1);
        }

        private static void EnsureBuilt()
        {
            if (regions != null && levels != null && worlds != null)
            {
                return;
            }

            regions = BuildRegions();
            levels = BuildLevels(regions);
            worlds = new List<WorldDefinition>(regions.Count);
            for (int i = 0; i < regions.Count; i++)
            {
                List<LevelDefinition> worldLevels = new List<LevelDefinition>();
                for (int level = 0; level < levels.Count; level++)
                {
                    if (levels[level].RegionIndex == i)
                    {
                        worldLevels.Add(levels[level]);
                    }
                }

                worlds.Add(new WorldDefinition(regions[i], worldLevels));
            }
        }

        private static List<RegionDefinition> BuildRegions()
        {
            return new List<RegionDefinition>
            {
                new RegionDefinition(
                    0, "松岭", "PINE PASS", "木香、薄雾与第一班早车",
                    Hex(0x8FB8BD), Hex(0xA8C4BF), Hex(0x668A64), Hex(0x7DA36C),
                    Hex(0x315D4B), Hex(0x6B7166), Hex(0xE6C36A), Hex(0x5A4436),
                    BuildPineMap()),
                new RegionDefinition(
                    1, "赤岩", "REDSTONE", "峡谷把轨道折成了剪刀",
                    Hex(0xD79668), Hex(0xD7A076), Hex(0xB96545), Hex(0xD17B53),
                    Hex(0x6F6A3D), Hex(0x7D4737), Hex(0xFFB45E), Hex(0x51352E),
                    BuildCanyonMap()),
                new RegionDefinition(
                    2, "霜原", "FROSTLINE", "每一次换轨都会惊醒风雪",
                    Hex(0xA8C7D6), Hex(0xD3E2E6), Hex(0xCBD6D2), Hex(0xE6EDF0),
                    Hex(0x476A70), Hex(0x7F98A3), Hex(0xF2A85B), Hex(0x4B5558),
                    BuildFrostMap()),
                new RegionDefinition(
                    3, "霓虹港", "NEON HARBOR", "港灯亮起，夜班列车不再等人",
                    Hex(0x202B49), Hex(0x27395A), Hex(0x26354A), Hex(0x33465E),
                    Hex(0x3A9A89), Hex(0x5C607D), Hex(0x42E8D7), Hex(0x302C3F),
                    BuildHarborMap()),
                new RegionDefinition(
                    4, "沙海", "DUNE SEA", "风会抹去脚印，但抹不掉线路",
                    Hex(0xE6B66F), Hex(0xE7C083), Hex(0xC99451), Hex(0xD9AC68),
                    Hex(0x6E8544), Hex(0x9B6D43), Hex(0xFFE098), Hex(0x65452E),
                    BuildDuneMap()),
                new RegionDefinition(
                    5, "云港", "CLOUD TERMINUS", "在云端，终点只是另一条出发线",
                    Hex(0x87A8C3), Hex(0xC2D4DE), Hex(0x7AA6A0), Hex(0x91B8AB),
                    Hex(0x4A7C74), Hex(0x8796A2), Hex(0xFFD36A), Hex(0x465263),
                    BuildCloudMap()),
                new RegionDefinition(
                    6, "金穗谷", "GOLDEN FIELDS", "丰收季的铁路从不缺少乘客",
                    Hex(0x9ECBD1), Hex(0xD5D7AE), Hex(0xB49B52), Hex(0xC9B969),
                    Hex(0x71833D), Hex(0x85633C), Hex(0xFF9F43), Hex(0x62452F),
                    BuildHarvestMap()),
                new RegionDefinition(
                    7, "雾都", "FOGGY DEPOT", "灯火穿过浓雾，终点仍藏在下一道岔后",
                    Hex(0x627782), Hex(0x87949A), Hex(0x526666), Hex(0x687B77),
                    Hex(0x3F5E54), Hex(0x4A5152), Hex(0xE7B65F), Hex(0x3F3430),
                    BuildFogMap())
            };
        }

        private static List<LevelDefinition> BuildLevels(List<RegionDefinition> allRegions)
        {
            int[] levelCounts = { 4, 5, 5, 6, 6, 7, 7, 8 };
            string[][] namesCn =
            {
                new[] { "第一声汽笛", "林间回声", "雾中岔路", "松岭大调度" },
                new[] { "红线回声", "岩桥交错", "峡谷回旋", "赤岩封锁线", "热风换轨" },
                new[] { "冰轨初试", "白色盲区", "霜桥换线", "极夜调度", "雪线长鸣" },
                new[] { "港口夜班", "霓虹逆流", "信号过载", "午夜总站", "箱区潮汐", "灯塔环线" },
                new[] { "风沙起航", "流沙支线", "落日风眼", "沙海风暴", "古井岔口", "长路孤灯" },
                new[] { "云端试运行", "浮站迷航", "高空气流", "终点之后", "悬桥队列", "风眼回廊", "云港总控" },
                new[] { "丰收首发", "麦田回环", "谷仓交汇", "金穗总调度", "草垛迷阵", "双镰线", "谷雨终班" },
                new[] { "雾都初灯", "贝克街口", "雨巷错车", "钟楼回环", "黑桥咽喉", "夜雾分流", "总站封锁", "雾散之前" }
            };

            string[][] namesEn =
            {
                new[] { "FIRST WHISTLE", "FOREST ECHO", "FOG SWITCH", "PINE MARSHAL" },
                new[] { "RED ECHO", "STONE BRIDGES", "CANYON SPIRAL", "REDSTONE LOCK", "HOT SWITCH" },
                new[] { "ICE TRIAL", "WHITE BLIND", "FROST CROSSING", "POLAR NIGHT", "SNOWLINE" },
                new[] { "NIGHT SHIFT", "NEON BACKFLOW", "SIGNAL OVERLOAD", "MIDNIGHT TERMINAL", "CONTAINER TIDE", "LIGHTHOUSE LOOP" },
                new[] { "DUST DEPARTURE", "SHIFTING SANDS", "SUNSET EYE", "DUNE STORM", "OLD WELL", "LONE LIGHT" },
                new[] { "CLOUD TRIAL", "DRIFTING PLATFORM", "HIGH AIR", "BEYOND TERMINUS", "SUSPENSION QUEUE", "WIND EYE", "CLOUD MASTER" },
                new[] { "HARVEST RUN", "WHEAT LOOP", "BARN CROSSING", "GOLDEN MARSHAL", "HAY MAZE", "TWIN SICKLES", "GRAIN RAIN" },
                new[] { "FIRST LAMP", "BAKER STREET", "RAIN ALLEY", "CLOCK LOOP", "BLACK BRIDGE", "NIGHT SPLIT", "TERMINAL LOCK", "BEFORE THE FOG" }
            };

            string[][] hints =
            {
                new[] { "开局列车会自动发车，点击车身可以停车或恢复。", "先停一列车观察路线，确认白色箭头方向后再放行。", "道岔会切换所有经过它的列车，利用停车错开时机。", "连续送达能提高得分，碰撞会立即中断本关。" },
                new[] { "交叉轨道没有自动信号，靠停车让行。", "点击列车可以停在轨道中间，不消耗次数。", "列车即将撞在一起时，及时停住其中一列。", "看清颜色再放行，同色终点站才会接收乘客。", "让两列车依次通过热风汇合口。" },
                new[] { "雪原线路很长，先让快车通过交叉点。", "把列车停在岔口前，再切换出口最稳妥。", "列车可以同时等待，节奏由你决定。", "别让停住的列车堵在另一列车必经的咽喉。", "长线上要提前为对向列车留出位置。" },
                new[] { "港区有多条平行轨道，适合分批放行。", "霓虹线路交叉密集，提前停车比临场换线安全。", "一列车停下后可以随时再次点击发车。", "最后一班乘客也需要安全抵达。", "集装箱区会给列车更多换线时间。", "灯塔环线要避免尾部列车追上。" },
                new[] { "长距离直轨适合拉开列车间距。", "沙海地图的路网稀疏，绕错一次会花很多时间。", "先确认目标颜色，再切换中央道岔。", "利用停车等待，让不同颜色依次通过窄口。", "古井岔口只能按顺序放行。", "孤灯长线要提前减速。" },
                new[] { "云港站台靠得很近，留出制动距离。", "浮桥交叉处的列车需要严格错峰。", "高空气流会提高车速，提前准备停车。", "在倒计时结束前，把四色乘客全部送达。", "悬桥队列需要连续换轨。", "风眼回廊会反复改变列车路线。", "总控台要同时管理四列列车。" },
                new[] { "先让列车装满站台上最多的颜色。", "把不同颜色分流到同名车站。", "谷仓交汇处没有自动信号。", "用支线停车，给满载列车让路。", "草垛迷阵适合短距离反复停车。", "双镰线要让同色客流保持在同一条线上。", "谷雨终班会持续增加客流。" },
                new[] { "雾都线路被建筑遮住，先记住岔口位置。", "贝克街口的列车会连续穿越两条交叉轨。", "雨巷里的列车需要更早停车。", "钟楼回环会反复回到同一枢纽。", "黑桥咽喉一次只能通过一列车。", "夜雾分流要求同时守住两个入口。", "总站封锁时不要让停车队列堵住站台。", "雾散之前完成最后一次调度。" }
            };

            List<LevelDefinition> result = new List<LevelDefinition>(48);
            int global = 0;
            for (int regionIndex = 0; regionIndex < allRegions.Count; regionIndex++)
            {
                RegionDefinition region = allRegions[regionIndex];
                int count = levelCounts[regionIndex];
                for (int local = 0; local < count; local++)
                {
                    int offset = (regionIndex * 3 + local * 5) % 4;
                    int[] quotas = new int[4];
                    for (int color = 0; color < 4; color++)
                    {
                        quotas[color] = 4 + regionIndex * 2 + local + ((color + offset) % 2);
                    }

                    RailMap levelMap = BuildLevelMap(region.Map, global, local);
                    result.Add(new LevelDefinition(
                        global,
                        regionIndex,
                        local,
                        namesCn[regionIndex][local],
                        namesEn[regionIndex][local],
                        hints[regionIndex][local],
                        quotas,
                        65f + regionIndex * 5f + local * 2f,
                        1.65f + regionIndex * 0.1f + local * 0.055f,
                        region,
                        levelMap,
                        Mathf.Clamp(1 + regionIndex / 2 + local / 3, 1, 4),
                        4 + regionIndex + local / 2,
                        16 + regionIndex * 2,
                        Mathf.Max(1.3f, 2.8f - regionIndex * 0.18f - local * 0.05f),
                        regionIndex == 0 && local < 2 ? 3 : 15));
                    global++;
                }
            }

            return result;
        }

        private static RailMap BuildLevelMap(RailMap source, int levelIndex, int local)
        {
            Vector2Int Transform(Vector2Int point)
            {
                switch (local % 8)
                {
                    case 1:
                        return new Vector2Int(-point.x, point.y);
                    case 2:
                        return new Vector2Int(point.x, -point.y);
                    case 3:
                        return new Vector2Int(-point.x, -point.y);
                    case 4:
                        return new Vector2Int(point.y, point.x);
                    case 5:
                        return new Vector2Int(-point.y, point.x);
                    case 6:
                        return new Vector2Int(-point.y, -point.x);
                    case 7:
                        return new Vector2Int(point.y, -point.x);
                    default:
                        return point;
                }
            }

            HashSet<Vector2Int> stationNodes = new HashSet<Vector2Int>();
            for (int i = 0; i < source.Stations.Count; i++)
            {
                stationNodes.Add(Transform(source.Stations[i].Position));
            }

            List<RailEdge> candidates = new List<RailEdge>(source.Edges.Count);
            for (int i = 0; i < source.Edges.Count; i++)
            {
                RailEdge edge = new RailEdge(Transform(source.Edges[i].A), Transform(source.Edges[i].B));
                if (!candidates.Contains(edge))
                {
                    candidates.Add(edge);
                }
            }

            // A level is a controlled simplification of its theme map. Each level
            // keeps one connected backbone and at most two intentional loops, so
            // later levels gain pressure from trains and passengers instead of a
            // wall of short, decorative connections.
            int loopBudget = local == 0 ? 0 : local <= 2 ? 1 : 2;
            List<RailEdge> edges = SparsifyEdges(
                candidates,
                levelIndex * 31 + local * 17 + 7,
                loopBudget);
            edges = SimplifyCorridors(edges, stationNodes);
            HashSet<RailEdge> bridgeEdges;
            HashSet<RailEdge> rampEdges;
            edges = AddElevatedSpans(
                edges,
                stationNodes,
                levelIndex,
                local,
                out bridgeEdges,
                out rampEdges);

            List<StationDefinition> stationsOut = new List<StationDefinition>(source.Stations.Count);
            int sourceColorOffset = (levelIndex + local) % 4;
            int goalColorOffset = (levelIndex * 3 + local + 1) % 4;
            int sourceCount = 0;
            int goalCount = 0;
            for (int i = 0; i < source.Stations.Count; i++)
            {
                StationDefinition original = source.Stations[i];
                int colorIndex = original.IsSource
                    ? (sourceColorOffset + sourceCount++) % 4
                    : (goalColorOffset + goalCount++) % 4;
                Vector2Int stationPosition = Transform(original.Position);
                Vector2Int stationLink = ResolveStationLink(
                    stationPosition,
                    Transform(original.Link),
                    edges);
                stationsOut.Add(new StationDefinition(
                    stationPosition,
                    stationLink,
                    (RailColor)colorIndex,
                    original.IsSource,
                    original.IsSource ? 3 + local / 2 : 4 + local / 2,
                    14 + local,
                    Mathf.Max(0.8f, 2.8f - local * 0.18f),
                    levelIndex < 8 && local < 2 ? 3 : 15));
            }

            Dictionary<RailEdge, TrackKind> kinds = new Dictionary<RailEdge, TrackKind>();
            for (int i = 0; i < edges.Count; i++)
            {
                TrackKind kind = TrackKind.Standard;
                if (bridgeEdges.Contains(edges[i]))
                {
                    kind = TrackKind.Bridge;
                }
                else if (rampEdges.Contains(edges[i]))
                {
                    kind = TrackKind.Ramp;
                }
                else if (edges[i].A.x != edges[i].B.x && edges[i].A.y != edges[i].B.y)
                {
                    kind = TrackKind.SideTrack;
                }
                else if (local > 1 && i % 13 == 5)
                {
                    kind = TrackKind.Tunnel;
                }

                kinds[edges[i]] = kind;
            }

            return new RailMap(
                source.Name + " / 变线 " + (local + 1),
                edges,
                stationsOut,
                kinds);
        }

        private static List<RailEdge> AddElevatedSpans(
            List<RailEdge> input,
            HashSet<Vector2Int> stations,
            int levelIndex,
            int local,
            out HashSet<RailEdge> bridgeEdges,
            out HashSet<RailEdge> rampEdges)
        {
            bridgeEdges = new HashSet<RailEdge>();
            rampEdges = new HashSet<RailEdge>();
            List<RailEdge> result = new List<RailEdge>(input);
            List<RailEdge> candidates = new List<RailEdge>();
            for (int i = 0; i < input.Count; i++)
            {
                RailEdge edge = input[i];
                int length = Mathf.Abs(edge.A.x - edge.B.x) + Mathf.Abs(edge.A.y - edge.B.y);
                if (length >= 5 && !stations.Contains(edge.A) && !stations.Contains(edge.B))
                {
                    candidates.Add(edge);
                }
            }

            candidates.Sort((a, b) =>
            {
                int distanceA = Mathf.Abs(a.A.x - a.B.x) + Mathf.Abs(a.A.y - a.B.y);
                int distanceB = Mathf.Abs(b.A.x - b.B.x) + Mathf.Abs(b.A.y - b.B.y);
                return distanceB.CompareTo(distanceA);
            });

            int requested = local >= 4 ? 2 : 1;
            HashSet<Vector2Int> existingNodes = new HashSet<Vector2Int>();
            for (int i = 0; i < input.Count; i++)
            {
                existingNodes.Add(input[i].A);
                existingNodes.Add(input[i].B);
            }

            int created = 0;
            int cursor = (levelIndex * 3 + local * 5) % Mathf.Max(1, candidates.Count);
            for (int attempt = 0; attempt < candidates.Count && created < requested; attempt++)
            {
                RailEdge candidate = candidates[(cursor + attempt) % candidates.Count];
                int index = result.FindIndex(edge => edge.Equals(candidate));
                if (index < 0)
                {
                    continue;
                }

                Vector2 a = new Vector2(candidate.A.x, candidate.A.y);
                Vector2 b = new Vector2(candidate.B.x, candidate.B.y);
                Vector2Int rampIn = RoundGridPoint(Vector2.Lerp(a, b, 0.28f));
                Vector2Int rampOut = RoundGridPoint(Vector2.Lerp(a, b, 0.72f));
                if (rampIn == candidate.A || rampIn == candidate.B ||
                    rampOut == candidate.A || rampOut == candidate.B ||
                    rampIn == rampOut ||
                    existingNodes.Contains(rampIn) ||
                    existingNodes.Contains(rampOut))
                {
                    continue;
                }

                result.RemoveAt(index);
                RailEdge rampA = new RailEdge(candidate.A, rampIn);
                RailEdge bridge = new RailEdge(rampIn, rampOut);
                RailEdge rampB = new RailEdge(rampOut, candidate.B);
                result.Add(rampA);
                result.Add(bridge);
                result.Add(rampB);
                rampEdges.Add(rampA);
                rampEdges.Add(rampB);
                bridgeEdges.Add(bridge);
                existingNodes.Add(rampIn);
                existingNodes.Add(rampOut);
                created++;
            }

            return result;
        }

        private static Vector2Int RoundGridPoint(Vector2 point)
        {
            return new Vector2Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y));
        }

        private static Vector2Int ResolveStationLink(
            Vector2Int station,
            Vector2Int preferred,
            IReadOnlyList<RailEdge> edges)
        {
            RailEdge? preferredEdge = null;
            for (int i = 0; i < edges.Count; i++)
            {
                RailEdge edge = edges[i];
                if (edge.A == station && edge.B == preferred)
                {
                    return preferred;
                }

                if (edge.B == station && edge.A == preferred)
                {
                    return preferred;
                }

                if (edge.A == station || edge.B == station)
                {
                    if (!preferredEdge.HasValue)
                    {
                        preferredEdge = edge;
                    }
                }
            }

            if (preferredEdge.HasValue)
            {
                return preferredEdge.Value.A == station
                    ? preferredEdge.Value.B
                    : preferredEdge.Value.A;
            }

            return preferred;
        }

        private static List<RailEdge> SimplifyCorridors(List<RailEdge> input, HashSet<Vector2Int> protectedNodes)
        {
            Dictionary<Vector2Int, List<Vector2Int>> graph = new Dictionary<Vector2Int, List<Vector2Int>>();
            for (int i = 0; i < input.Count; i++)
            {
                AddNeighbor(graph, input[i].A, input[i].B);
                AddNeighbor(graph, input[i].B, input[i].A);
            }

            HashSet<Vector2Int> anchors = new HashSet<Vector2Int>(protectedNodes);
            foreach (KeyValuePair<Vector2Int, List<Vector2Int>> pair in graph)
            {
                if (pair.Value.Count != 2)
                {
                    anchors.Add(pair.Key);
                }
            }

            if (anchors.Count == 0 && graph.Count > 0)
            {
                foreach (Vector2Int node in graph.Keys)
                {
                    anchors.Add(node);
                    break;
                }
            }

            List<RailEdge> result = new List<RailEdge>();
            HashSet<RailEdge> visited = new HashSet<RailEdge>();
            foreach (Vector2Int anchor in anchors)
            {
                if (!graph.TryGetValue(anchor, out List<Vector2Int> neighbors))
                {
                    continue;
                }

                for (int i = 0; i < neighbors.Count; i++)
                {
                    RailEdge first = new RailEdge(anchor, neighbors[i]);
                    if (visited.Contains(first))
                    {
                        continue;
                    }

                    Vector2Int previous = anchor;
                    Vector2Int current = neighbors[i];
                    visited.Add(first);
                    while (!anchors.Contains(current))
                    {
                        if (!graph.TryGetValue(current, out List<Vector2Int> chain) || chain.Count != 2)
                        {
                            break;
                        }

                        Vector2Int next = chain[0] == previous ? chain[1] : chain[0];
                        RailEdge step = new RailEdge(current, next);
                        if (visited.Contains(step))
                        {
                            break;
                        }

                        visited.Add(step);
                        previous = current;
                        current = next;
                    }

                    RailEdge simplified = new RailEdge(anchor, current);
                    if (simplified.A != simplified.B && !result.Contains(simplified))
                    {
                        result.Add(simplified);
                    }
                }
            }

            return result.Count > 0 ? result : input;
        }

        private static void AddNeighbor(
            Dictionary<Vector2Int, List<Vector2Int>> graph,
            Vector2Int from,
            Vector2Int to)
        {
            if (!graph.TryGetValue(from, out List<Vector2Int> neighbors))
            {
                neighbors = new List<Vector2Int>(4);
                graph[from] = neighbors;
            }

            if (!neighbors.Contains(to))
            {
                neighbors.Add(to);
            }
        }

        private static List<RailEdge> SparsifyEdges(List<RailEdge> input, int seed, int extraLoops)
        {
            List<RailEdge> ordered = new List<RailEdge>();
            for (int i = 0; i < input.Count; i++)
            {
                if (!ordered.Contains(input[i]))
                {
                    ordered.Add(input[i]);
                }
            }

            ordered.Sort((a, b) =>
            {
                int scoreA = StableEdgeScore(a, seed);
                int scoreB = StableEdgeScore(b, seed);
                return scoreA.CompareTo(scoreB);
            });

            Dictionary<Vector2Int, Vector2Int> parent = new Dictionary<Vector2Int, Vector2Int>();
            for (int i = 0; i < ordered.Count; i++)
            {
                parent[ordered[i].A] = ordered[i].A;
                parent[ordered[i].B] = ordered[i].B;
            }

            List<RailEdge> result = new List<RailEdge>();
            List<RailEdge> loopCandidates = new List<RailEdge>();
            for (int i = 0; i < ordered.Count; i++)
            {
                RailEdge edge = ordered[i];
                if (Union(parent, edge.A, edge.B))
                {
                    result.Add(edge);
                }
                else
                {
                    loopCandidates.Add(edge);
                }
            }

            int loops = Mathf.Clamp(extraLoops, 0, loopCandidates.Count);
            for (int i = 0; i < loops; i++)
            {
                result.Add(loopCandidates[i]);
            }

            return result;
        }

        private static int StableEdgeScore(RailEdge edge, int seed)
        {
            unchecked
            {
                int hash = edge.GetHashCode();
                hash ^= seed + unchecked((int)0x9e3779b9) + (hash << 6) + (hash >> 2);
                return hash;
            }
        }

        private static bool Union(Dictionary<Vector2Int, Vector2Int> parent, Vector2Int a, Vector2Int b)
        {
            Vector2Int rootA = Find(parent, a);
            Vector2Int rootB = Find(parent, b);
            if (rootA == rootB)
            {
                return false;
            }

            parent[rootB] = rootA;
            return true;
        }

        private static Vector2Int Find(Dictionary<Vector2Int, Vector2Int> parent, Vector2Int point)
        {
            if (!parent.TryGetValue(point, out Vector2Int value) || value == point)
            {
                parent[point] = point;
                return point;
            }

            Vector2Int root = Find(parent, value);
            parent[point] = root;
            return root;
        }

        private static RailMap BuildPineMap()
        {
            List<RailEdge> edges = new List<RailEdge>();
            Dictionary<string, Vector2Int> points = AddNetwork(
                "A(-4,2) B(-2,2) C(0,2) D(2,2) E(4,2)",
                "F(-4,0) G(-1,0) H(1,0) I(4,0)",
                "J(-4,-2) K(-2,-2) L(0,-2) M(2,-2) N(4,-2)");
            AddEdges(edges,
                points,
                "A-B", "B-C", "C-D", "D-E",
                "F-G", "G-H", "H-I",
                "J-K", "K-L", "L-M", "M-N",
                "A-F", "F-J", "E-I", "I-N",
                "B-G", "C-H", "D-I", "G-K", "H-M");

            List<StationDefinition> stations = new List<StationDefinition>
            {
                Station(-6, 2, -4, 2, RailColor.Crimson, true),
                Station(-6, 0, -4, 0, RailColor.Azure, true),
                Station(-6, -2, -4, -2, RailColor.Amber, true),
                Station(0, -4, 0, -2, RailColor.Violet, true),
                Station(6, 2, 4, 2, RailColor.Crimson, false),
                Station(6, 0, 4, 0, RailColor.Azure, false),
                Station(6, -2, 4, -2, RailColor.Amber, false),
                Station(0, 4, 0, 2, RailColor.Violet, false)
            };
            AddStationEdges(edges, stations);
            return new RailMap("松岭环线", edges, stations);
        }

        private static RailMap BuildCanyonMap()
        {
            List<RailEdge> edges = new List<RailEdge>();
            Dictionary<string, Vector2Int> points = AddNetwork(
                "A(-4,3) B(-1,3) C(1,3) D(4,3)",
                "E(-4,1) F(0,1) G(4,1)",
                "H(-4,-1) I(-1,-1) J(1,-1) K(4,-1)",
                "L(-2,-3) M(0,-3) N(2,-3)");
            AddEdges(edges,
                points,
                "A-B", "B-C", "C-D",
                "A-E", "E-H", "H-I", "I-L", "L-M", "M-N", "N-K", "K-J",
                "D-G", "G-K",
                "B-F", "F-G", "G-C",
                "F-J", "I-J");

            List<StationDefinition> stations = new List<StationDefinition>
            {
                Station(-6, 3, -4, 3, RailColor.Crimson, true),
                Station(-6, -1, -4, -1, RailColor.Azure, true),
                Station(-2, -5, -2, -3, RailColor.Amber, true),
                Station(2, -5, 2, -3, RailColor.Violet, true),
                Station(6, 3, 4, 3, RailColor.Crimson, false),
                Station(6, -1, 4, -1, RailColor.Azure, false),
                Station(-2, 5, -1, 3, RailColor.Amber, false),
                Station(2, 5, 1, 3, RailColor.Violet, false)
            };
            AddStationEdges(edges, stations);
            return new RailMap("赤岩剪刀线", edges, stations);
        }

        private static RailMap BuildFrostMap()
        {
            List<RailEdge> edges = new List<RailEdge>();
            Dictionary<string, Vector2Int> points = AddNetwork(
                "A(-4,3) B(-2,3) C(1,3) D(4,3)",
                "E(-4,1) F(-1,1) G(2,1) H(4,1)",
                "I(-4,-1) J(0,-1) K(4,-1)",
                "L(-4,-3) M(-2,-3) N(1,-3) O(4,-3)");
            AddEdges(edges,
                points,
                "A-B", "B-C", "C-D",
                "A-E", "E-I", "I-L", "L-M", "M-N", "N-O", "O-K", "K-H", "H-D",
                "B-F", "F-G", "G-C",
                "F-J", "G-J", "J-M", "J-N",
                "E-F", "G-H");

            List<StationDefinition> stations = new List<StationDefinition>
            {
                Station(-6, -1, -4, -1, RailColor.Crimson, true),
                Station(-2, 5, -2, 3, RailColor.Azure, true),
                Station(1, 5, 1, 3, RailColor.Amber, true),
                Station(-2, -5, -2, -3, RailColor.Violet, true),
                Station(6, 3, 4, 3, RailColor.Crimson, false),
                Station(6, -1, 4, -1, RailColor.Azure, false),
                Station(1, -5, 1, -3, RailColor.Amber, false),
                Station(6, -3, 4, -3, RailColor.Violet, false)
            };
            AddStationEdges(edges, stations);
            return new RailMap("霜原交汇", edges, stations);
        }

        private static RailMap BuildHarborMap()
        {
            List<RailEdge> edges = new List<RailEdge>();
            Dictionary<string, Vector2Int> points = AddNetwork(
                "A(-5,2) B(-2,2) C(1,2) D(4,2)",
                "E(-5,0) F(-2,0) G(1,0) H(4,0)",
                "I(-5,-2) J(-2,-2) K(1,-2) L(4,-2)");
            AddEdges(edges,
                points,
                "A-B", "B-C", "C-D",
                "E-F", "F-G", "G-H",
                "I-J", "J-K", "K-L",
                "A-E", "E-I", "D-H", "H-L",
                "B-F", "F-J", "C-G", "G-K",
                "B-G", "C-F");

            List<StationDefinition> stations = new List<StationDefinition>
            {
                Station(-7, 2, -5, 2, RailColor.Crimson, true),
                Station(-7, 0, -5, 0, RailColor.Azure, true),
                Station(-7, -2, -5, -2, RailColor.Amber, true),
                Station(1, -4, 1, -2, RailColor.Violet, true),
                Station(6, 2, 4, 2, RailColor.Crimson, false),
                Station(6, 0, 4, 0, RailColor.Azure, false),
                Station(6, -2, 4, -2, RailColor.Amber, false),
                Station(1, 4, 1, 2, RailColor.Violet, false)
            };
            AddStationEdges(edges, stations);
            return new RailMap("霓虹港环线", edges, stations);
        }

        private static RailMap BuildDuneMap()
        {
            List<RailEdge> edges = new List<RailEdge>();
            Dictionary<string, Vector2Int> points = AddNetwork(
                "A(-4,2) B(-1,2) C(2,2) D(4,2)",
                "E(-3,0) F(0,0) G(3,0)",
                "H(-4,-2) I(-1,-2) J(2,-2) K(4,-2)",
                "L(0,-4)");
            AddEdges(edges,
                points,
                "A-B", "B-C", "C-D",
                "A-E", "E-H", "H-I", "I-L", "L-J", "J-K",
                "D-G", "G-K",
                "E-F", "F-G", "F-I", "G-J",
                "B-F", "C-G");

            List<StationDefinition> stations = new List<StationDefinition>
            {
                Station(-6, 2, -4, 2, RailColor.Crimson, true),
                Station(-6, -2, -4, -2, RailColor.Azure, true),
                Station(-2, -4, 0, -4, RailColor.Amber, true),
                Station(-2, 0, -3, 0, RailColor.Violet, true),
                Station(6, 2, 4, 2, RailColor.Crimson, false),
                Station(6, -2, 4, -2, RailColor.Azure, false),
                Station(0, 4, -1, 2, RailColor.Amber, false),
                Station(3, 4, 2, 2, RailColor.Violet, false)
            };
            AddStationEdges(edges, stations);
            return new RailMap("沙海经纬", edges, stations);
        }

        private static RailMap BuildCloudMap()
        {
            List<RailEdge> edges = new List<RailEdge>();
            Dictionary<string, Vector2Int> points = AddNetwork(
                "A(-4,3) B(-1,3) C(2,3) D(4,3)",
                "E(-3,1) F(0,1) G(3,1)",
                "H(-4,-1) I(-1,-1) J(2,-1) K(4,-1)",
                "L(-2,-3) M(1,-3)");
            AddEdges(edges,
                points,
                "A-B", "B-C", "C-D",
                "A-E", "E-H", "H-L", "L-M",
                "D-G", "G-K", "K-J", "J-M",
                "B-F", "F-G",
                "H-I", "I-J", "F-I", "F-J",
                "C-F");

            List<StationDefinition> stations = new List<StationDefinition>
            {
                Station(-6, 3, -4, 3, RailColor.Crimson, true),
                Station(-6, -1, -4, -1, RailColor.Azure, true),
                Station(-2, -5, -2, -3, RailColor.Amber, true),
                Station(1, 5, 2, 3, RailColor.Violet, true),
                Station(6, 3, 4, 3, RailColor.Crimson, false),
                Station(6, -1, 4, -1, RailColor.Azure, false),
                Station(1, -5, 1, -3, RailColor.Amber, false),
                Station(-4, 5, -4, 3, RailColor.Violet, false)
            };
            AddStationEdges(edges, stations);
            return new RailMap("云港总站", edges, stations);
        }

        private static RailMap BuildHarvestMap()
        {
            List<RailEdge> edges = new List<RailEdge>();
            Dictionary<string, Vector2Int> points = AddNetwork(
                "A(-4,2) B(-1,2) C(2,2) D(4,2)",
                "E(-4,0) F(-1,0) G(2,0) H(4,0)",
                "I(-4,-2) J(-1,-2) K(2,-2) L(4,-2)");
            AddEdges(edges,
                points,
                "A-B", "B-C", "C-D",
                "E-F", "F-G", "G-H",
                "I-J", "J-K", "K-L",
                "A-E", "E-I", "D-H", "H-L",
                "B-F", "F-J", "C-G", "G-K",
                "B-G", "C-F", "F-K", "G-J");

            List<StationDefinition> stations = new List<StationDefinition>
            {
                Station(-6, 2, -4, 2, RailColor.Crimson, true),
                Station(-6, 0, -4, 0, RailColor.Azure, true),
                Station(-6, -2, -4, -2, RailColor.Amber, true),
                Station(0, -4, -1, -2, RailColor.Violet, true),
                Station(6, 2, 4, 2, RailColor.Crimson, false),
                Station(6, 0, 4, 0, RailColor.Azure, false),
                Station(6, -2, 4, -2, RailColor.Amber, false),
                Station(0, 4, -1, 2, RailColor.Violet, false)
            };
            AddStationEdges(edges, stations);
            return new RailMap("金穗谷环线", edges, stations);
        }

        private static RailMap BuildFogMap()
        {
            List<RailEdge> edges = new List<RailEdge>();
            Dictionary<string, Vector2Int> points = AddNetwork(
                "A(-5,3) B(-2,3) C(1,3) D(4,3)",
                "E(-4,1) F(-1,1) G(2,1) H(4,1)",
                "I(-4,-1) J(-1,-1) K(2,-1) L(4,-1)",
                "M(-2,-3) N(1,-3)");
            AddEdges(edges,
                points,
                "A-B", "B-C", "C-D",
                "A-E", "E-I", "I-M", "M-N", "N-L",
                "D-H", "H-L",
                "B-F", "F-G", "G-C",
                "E-F", "F-J", "J-M",
                "G-J", "J-K", "K-N",
                "F-I", "I-J",
                "C-H", "G-K");

            List<StationDefinition> stations = new List<StationDefinition>
            {
                Station(-6, 3, -5, 3, RailColor.Crimson, true),
                Station(-6, 1, -4, 1, RailColor.Azure, true),
                Station(-6, -1, -4, -1, RailColor.Amber, true),
                Station(-2, -5, -2, -3, RailColor.Violet, true),
                Station(6, 3, 4, 3, RailColor.Crimson, false),
                Station(6, 1, 4, 1, RailColor.Azure, false),
                Station(6, -1, 4, -1, RailColor.Amber, false),
                Station(1, 5, 1, 3, RailColor.Violet, false)
            };
            AddStationEdges(edges, stations);
            return new RailMap("雾都总站", edges, stations);
        }

        private static Dictionary<string, Vector2Int> AddNetwork(params string[] rows)
        {
            Dictionary<string, Vector2Int> result = new Dictionary<string, Vector2Int>();
            for (int row = 0; row < rows.Length; row++)
            {
                string text = rows[row];
                int cursor = 0;
                while (cursor < text.Length)
                {
                    while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
                    {
                        cursor++;
                    }

                    int open = text.IndexOf('(', cursor);
                    if (open < 0)
                    {
                        break;
                    }

                    int close = text.IndexOf(')', open);
                    if (close < 0)
                    {
                        break;
                    }

                    string name = text.Substring(cursor, open - cursor).Trim();
                    string[] values = text.Substring(open + 1, close - open - 1).Split(',');
                    if (values.Length == 2 &&
                        int.TryParse(values[0].Trim(), out int x) &&
                        int.TryParse(values[1].Trim(), out int z))
                    {
                        result[name] = new Vector2Int(x, z);
                    }

                    cursor = close + 1;
                }
            }

            return result;
        }

        private static void AddEdges(List<RailEdge> edges, Dictionary<string, Vector2Int> points, params string[] pairs)
        {
            for (int i = 0; i < pairs.Length; i++)
            {
                string[] parts = pairs[i].Split('-');
                if (parts.Length != 2)
                {
                    Debug.LogError("Invalid rail edge: " + pairs[i]);
                    continue;
                }

                if (!points.TryGetValue(parts[0], out Vector2Int a) ||
                    !points.TryGetValue(parts[1], out Vector2Int b))
                {
                    Debug.LogError("Unknown rail token in edge: " + pairs[i]);
                    continue;
                }

                RailEdge edge = new RailEdge(a, b);
                if (!edges.Contains(edge))
                {
                    edges.Add(edge);
                }
            }
        }

        private static StationDefinition Station(int x, int z, int linkX, int linkZ, RailColor color, bool source)
        {
            return new StationDefinition(new Vector2Int(x, z), new Vector2Int(linkX, linkZ), color, source);
        }

        private static void AddStationEdges(List<RailEdge> edges, List<StationDefinition> stations)
        {
            for (int i = 0; i < stations.Count; i++)
            {
                RailEdge edge = new RailEdge(stations[i].Position, stations[i].Link);
                if (!edges.Contains(edge))
                {
                    edges.Add(edge);
                }
            }
        }

        private static Color Hex(int rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                1f);
        }
    }
}
