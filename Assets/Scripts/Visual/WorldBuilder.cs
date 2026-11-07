using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrainTrain
{
    public readonly struct StationView
    {
        public readonly Vector2Int Position;
        public readonly Vector2Int Link;
        public readonly RailColor Color;
        public readonly bool IsSource;
        public readonly Vector3 WorldPosition;
        public readonly Transform Root;

        public StationView(StationDefinition definition)
        {
            Position = definition.Position;
            Link = definition.Link;
            Color = definition.Color;
            IsSource = definition.IsSource;
            WorldPosition = WorldBuilder.GridToWorld(definition.Position);
            Root = null;
        }

        public StationView(StationDefinition definition, Transform root)
        {
            Position = definition.Position;
            Link = definition.Link;
            Color = definition.Color;
            IsSource = definition.IsSource;
            WorldPosition = WorldBuilder.GridToWorld(definition.Position);
            Root = root;
        }
    }

    public sealed class WorldView
    {
        public Camera Camera;
        public Transform Root;
        public Transform TrainsRoot;
        public Dictionary<Vector2Int, TrackSwitchView> Switches;
        public List<StationView> Stations;
        public Vector3 CameraHome;
        public float BaseOrthographicSize;
    }

    public static class WorldBuilder
    {
        public const float GridSize = 2f;

        public static Vector3 GridToWorld(Vector2Int point)
        {
            return new Vector3(point.x * GridSize, 0.18f, point.y * GridSize);
        }

        public static WorldView Build(LevelDefinition level, int variationSeed, bool preview)
        {
            WorldView world = new WorldView
            {
                Root = new GameObject("TrainTrain_World").transform,
                Switches = new Dictionary<Vector2Int, TrackSwitchView>(),
                Stations = new List<StationView>()
            };

            ConfigureRendering(level.Region);
            BuildGround(level, world.Root, variationSeed);
            BuildLighting(level.Region, world.Root);
            BuildTracks(level, world.Root);
            BuildStations(level, world.Root, world.Stations);
            BuildSwitches(level, world.Root, world.Switches, preview);
            BuildDecor(level, world.Root, variationSeed);

            GameObject trainRoot = new GameObject("Trains");
            trainRoot.transform.SetParent(world.Root, false);
            world.TrainsRoot = trainRoot.transform;

            world.Camera = BuildCamera(level, world);
            return world;
        }

        public static void Destroy(WorldView world)
        {
            if (world == null)
            {
                return;
            }

            if (world.Root != null)
            {
                world.Root.gameObject.SetActive(false);
                Object.Destroy(world.Root.gameObject);
            }

            if (world.Camera != null)
            {
                world.Camera.gameObject.SetActive(false);
                Object.Destroy(world.Camera.gameObject);
            }
        }

        public static Dictionary<Vector2Int, List<Vector2Int>> BuildAdjacency(IReadOnlyList<RailEdge> edges)
        {
            Dictionary<Vector2Int, List<Vector2Int>> result = new Dictionary<Vector2Int, List<Vector2Int>>();
            for (int i = 0; i < edges.Count; i++)
            {
                AddNeighbor(result, edges[i].A, edges[i].B);
                AddNeighbor(result, edges[i].B, edges[i].A);
            }

            foreach (List<Vector2Int> neighbors in result.Values)
            {
                neighbors.Sort(ComparePoint);
            }

            return result;
        }

        private static void AddNeighbor(Dictionary<Vector2Int, List<Vector2Int>> graph, Vector2Int from, Vector2Int to)
        {
            if (!graph.TryGetValue(from, out List<Vector2Int> list))
            {
                list = new List<Vector2Int>(4);
                graph[from] = list;
            }

            if (!list.Contains(to))
            {
                list.Add(to);
            }
        }

        private static int ComparePoint(Vector2Int a, Vector2Int b)
        {
            int z = a.y.CompareTo(b.y);
            return z != 0 ? z : a.x.CompareTo(b.x);
        }

        private static void ConfigureRendering(RegionDefinition region)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = region.Fog;
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 70f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(region.Sky, Color.white, 0.38f);
            QualitySettings.shadowDistance = 65f;
            QualitySettings.shadows = ShadowQuality.All;
        }

        private static Camera BuildCamera(LevelDefinition level, WorldView world)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = level.Region.Sky;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            cameraObject.AddComponent<AudioListener>();

            Bounds bounds = CalculateBounds(level);
            Vector3 center = bounds.center;
            Vector3 offset = new Vector3(-13f, 17f, -13f);
            camera.transform.position = center + offset;
            camera.transform.LookAt(center + Vector3.up * 0.2f);

            float aspect = Mathf.Max(0.5f, Screen.width / (float)Mathf.Max(1, Screen.height));
            float projected = (bounds.extents.x + bounds.extents.z) * 0.31f;
            float aspectFit = (bounds.extents.x * 0.67f + bounds.extents.z * 0.37f) / Mathf.Max(1f, aspect);
            camera.orthographicSize = Mathf.Max(7.4f, Mathf.Max(projected * 2.05f, aspectFit * 1.35f) + 2f);

            world.CameraHome = camera.transform.position;
            world.BaseOrthographicSize = camera.orthographicSize;
            return camera;
        }

        private static Bounds CalculateBounds(LevelDefinition level)
        {
            bool has = false;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            IReadOnlyList<StationDefinition> stations = level.Map.Stations;
            for (int i = 0; i < stations.Count; i++)
            {
                Vector3 p = GridToWorld(stations[i].Position);
                if (!has)
                {
                    min = p;
                    max = p;
                    has = true;
                }
                else
                {
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }

            if (!has)
            {
                return new Bounds(Vector3.zero, Vector3.one * 10f);
            }

            Bounds bounds = new Bounds((min + max) * 0.5f, max - min + Vector3.up * 1.5f);
            return bounds;
        }

        private static void BuildGround(LevelDefinition level, Transform root, int seed)
        {
            RegionDefinition region = level.Region;
            Bounds bounds = CalculateBounds(level);
            float width = bounds.size.x + 16f;
            float depth = bounds.size.z + 16f;

            Transform groundRoot = new GameObject("Ground").transform;
            groundRoot.SetParent(root, false);
            LowPolyAssets.Cube(
                "BaseGround",
                groundRoot,
                new Vector3(bounds.center.x, -0.16f, bounds.center.z),
                new Vector3(width, 0.32f, depth),
                region.Ground);

            System.Random random = new System.Random(seed * 1879 + 31);
            for (int i = 0; i < 18; i++)
            {
                float x = bounds.center.x + ((float)random.NextDouble() - 0.5f) * (width - 2f);
                float z = bounds.center.z + ((float)random.NextDouble() - 0.5f) * (depth - 2f);
                float sizeX = 1.6f + (float)random.NextDouble() * 4.5f;
                float sizeZ = 1.2f + (float)random.NextDouble() * 3.8f;
                Color tint = Color.Lerp(region.Ground, region.GroundAlt, 0.25f + (float)random.NextDouble() * 0.55f);
                LowPolyAssets.Cube(
                    "GroundPatch",
                    groundRoot,
                    new Vector3(x, -0.02f, z),
                    new Vector3(sizeX, 0.08f, sizeZ),
                    tint,
                    Quaternion.Euler(0f, (float)random.NextDouble() * 22f - 11f, 0f));
            }

            Color border = LowPolyAssets.Darken(region.Ground, 0.18f);
            LowPolyAssets.Cube(
                "EdgeNorth",
                groundRoot,
                new Vector3(bounds.center.x, -0.04f, bounds.center.z + depth * 0.5f),
                new Vector3(width, 0.18f, 0.8f),
                border);
            LowPolyAssets.Cube(
                "EdgeSouth",
                groundRoot,
                new Vector3(bounds.center.x, -0.04f, bounds.center.z - depth * 0.5f),
                new Vector3(width, 0.18f, 0.8f),
                border);
        }

        private static void BuildLighting(RegionDefinition region, Transform root)
        {
            GameObject sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(root, false);
            sunObject.transform.rotation = Quaternion.Euler(48f, -34f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Color.Lerp(new Color(1f, 0.9f, 0.76f), region.Accent, 0.08f);
            sun.intensity = region.Index == 3 ? 0.92f : 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.62f;
            sun.shadowBias = 0.04f;
        }

        private static void BuildTracks(LevelDefinition level, Transform root)
        {
            Transform tracksRoot = new GameObject("Tracks").transform;
            tracksRoot.SetParent(root, false);

            RegionDefinition region = level.Region;
            Color ballast = LowPolyAssets.Darken(region.GroundAlt, 0.28f);
            Color sleeper = region.RailWood;
            Color rail = region.Index == 3 ? new Color(0.48f, 0.56f, 0.61f) : new Color(0.36f, 0.39f, 0.39f);
            IReadOnlyList<RailEdge> edges = level.Map.Edges;
            for (int i = 0; i < edges.Count; i++)
            {
                RailEdge edge = edges[i];
                TrackKind kind = level.Map.KindOf(edge);
                Vector3 a = GridToWorld(edge.A);
                Vector3 b = GridToWorld(edge.B);
                float elevationA = level.Map.ElevationAt(edge, edge.A);
                float elevationB = level.Map.ElevationAt(edge, edge.B);
                a.y += elevationA;
                b.y += elevationB;
                Vector3 delta = b - a;
                float length = delta.magnitude;
                if (length < 0.01f)
                {
                    continue;
                }

                Vector3 direction = delta.normalized;
                Vector3 perpendicular = Vector3.Cross(Vector3.up, direction).normalized;
                Transform edgeRoot = new GameObject("Track").transform;
                edgeRoot.SetParent(tracksRoot, false);

                LowPolyAssets.BeamBetween(
                    "Ballast",
                    edgeRoot,
                    a + Vector3.down * 0.075f,
                    b + Vector3.down * 0.075f,
                    kind == TrackKind.SideTrack ? 0.48f : 0.72f,
                    0.08f,
                    ballast);

                int sleepers = Mathf.Clamp(Mathf.RoundToInt(length / 0.52f), 3, 26);
                for (int sleeperIndex = 0; sleeperIndex <= sleepers; sleeperIndex++)
                {
                    float t = sleeperIndex / (float)sleepers;
                    Vector3 position = Vector3.Lerp(a, b, t) + Vector3.down * 0.025f;
                    Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
                    LowPolyAssets.Cube(
                        "Sleeper",
                        edgeRoot,
                        position,
                        new Vector3(kind == TrackKind.SideTrack ? 0.4f : 0.54f, 0.055f, 0.11f),
                        sleeper,
                        rotation);
                }

                float railOffset = 0.2f;
                Vector3 railA = a + perpendicular * railOffset;
                Vector3 railB = b + perpendicular * railOffset;
                Vector3 railC = a - perpendicular * railOffset;
                Vector3 railD = b - perpendicular * railOffset;
                LowPolyAssets.BeamBetween("RailL", edgeRoot, railA, railB, 0.065f, 0.07f, rail, 0.7f);
                LowPolyAssets.BeamBetween("RailR", edgeRoot, railC, railD, 0.065f, 0.07f, rail, 0.7f);

                if (kind == TrackKind.Bridge)
                {
                    for (int support = 1; support <= 3; support++)
                    {
                        Vector3 point = Vector3.Lerp(a, b, support / 4f);
                        LowPolyAssets.Cube(
                            "BridgeSupport",
                            edgeRoot,
                            new Vector3(point.x, RailMap.ElevatedHeight * 0.33f, point.z),
                            new Vector3(0.18f, RailMap.ElevatedHeight * 0.72f, 0.18f),
                            LowPolyAssets.Darken(ballast, 0.18f));
                    }
                }
                else if (kind == TrackKind.Tunnel)
                {
                    Color tunnel = LowPolyAssets.Darken(rail, 0.55f);
                    Vector3 cover = Vector3.Lerp(a, b, 0.5f) + Vector3.up * 0.46f;
                    Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
                    LowPolyAssets.Cube(
                        "TunnelCover",
                        edgeRoot,
                        cover,
                        new Vector3(0.9f, 0.52f, length * 0.58f),
                        tunnel,
                        rotation);
                    for (int portal = 0; portal <= 1; portal++)
                    {
                        Vector3 center = Vector3.Lerp(a, b, portal == 0 ? 0.2f : 0.8f);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            LowPolyAssets.Cube(
                                "TunnelPost",
                                edgeRoot,
                                center + perpendicular * (side * 0.43f) + Vector3.up * 0.43f,
                                new Vector3(0.12f, 0.82f, 0.12f),
                                tunnel,
                                rotation);
                        }

                        LowPolyAssets.Cube(
                            "TunnelLintel",
                            edgeRoot,
                            center + Vector3.up * 0.84f,
                            new Vector3(0.98f, 0.12f, 0.14f),
                            LowPolyAssets.Lighten(tunnel, 0.1f),
                            rotation);
                    }
                }
            }

            BuildCrossingMarkers(tracksRoot, level.Map, region);
            BuildSidingBuffers(tracksRoot, level);
        }

        private static void BuildCrossingMarkers(Transform tracksRoot, RailMap map, RegionDefinition region)
        {
            Color warning = LowPolyAssets.Lighten(region.Accent, 0.18f);
            Color plate = LowPolyAssets.Darken(region.GroundAlt, 0.38f);
            IReadOnlyList<RailEdge> edges = map.Edges;
            for (int i = 0; i < edges.Count; i++)
            {
                for (int j = i + 1; j < edges.Count; j++)
                {
                    if (edges[i].A == edges[j].A ||
                        edges[i].A == edges[j].B ||
                        edges[i].B == edges[j].A ||
                        edges[i].B == edges[j].B)
                    {
                        continue;
                    }

                    if (!RailMap.IsGroundKind(map.KindOf(edges[i])) ||
                        !RailMap.IsGroundKind(map.KindOf(edges[j])))
                    {
                        continue;
                    }

                    Vector2 a = new Vector2(edges[i].A.x, edges[i].A.y) * GridSize;
                    Vector2 b = new Vector2(edges[i].B.x, edges[i].B.y) * GridSize;
                    Vector2 c = new Vector2(edges[j].A.x, edges[j].A.y) * GridSize;
                    Vector2 d = new Vector2(edges[j].B.x, edges[j].B.y) * GridSize;
                    if (!TrySegmentIntersection(a, b, c, d, out Vector2 crossing))
                    {
                        continue;
                    }

                    Vector3 position = new Vector3(crossing.x, 0.095f, crossing.y);
                    LowPolyAssets.Cube(
                        "CrossingPlate",
                        tracksRoot,
                        position,
                        new Vector3(0.82f, 0.035f, 0.82f),
                        plate,
                        Quaternion.Euler(0f, 45f, 0f));
                    LowPolyAssets.Cube(
                        "CrossingWarningA",
                        tracksRoot,
                        position + new Vector3(0f, 0.055f, 0.33f),
                        new Vector3(0.58f, 0.035f, 0.055f),
                        warning,
                        Quaternion.Euler(0f, 45f, 0f),
                        false,
                        0f,
                        0.45f);
                    LowPolyAssets.Cube(
                        "CrossingWarningB",
                        tracksRoot,
                        position + new Vector3(0f, 0.055f, -0.33f),
                        new Vector3(0.58f, 0.035f, 0.055f),
                        warning,
                        Quaternion.Euler(0f, 45f, 0f),
                        false,
                        0f,
                        0.45f);
                }
            }
        }

        private static bool TrySegmentIntersection(
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Vector2 d,
            out Vector2 point)
        {
            Vector2 r = b - a;
            Vector2 s = d - c;
            float denominator = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(denominator) < 0.0001f)
            {
                point = default;
                return false;
            }

            Vector2 offset = c - a;
            float t = (offset.x * s.y - offset.y * s.x) / denominator;
            float u = (offset.x * r.y - offset.y * r.x) / denominator;
            if (t > 0.08f && t < 0.92f && u > 0.08f && u < 0.92f)
            {
                point = a + r * t;
                return true;
            }

            point = default;
            return false;
        }

        private static void BuildSidingBuffers(Transform tracksRoot, LevelDefinition level)
        {
            Dictionary<Vector2Int, List<Vector2Int>> graph = BuildAdjacency(level.Map.Edges);
            HashSet<Vector2Int> stationNodes = new HashSet<Vector2Int>();
            for (int i = 0; i < level.Map.Stations.Count; i++)
            {
                stationNodes.Add(level.Map.Stations[i].Position);
            }

            Color bufferColor = new Color(0.22f, 0.25f, 0.26f);
            Color stripeColor = LowPolyAssets.Lighten(level.Region.Accent, 0.3f);
            foreach (KeyValuePair<Vector2Int, List<Vector2Int>> entry in graph)
            {
                if (entry.Value.Count != 1 || stationNodes.Contains(entry.Key))
                {
                    continue;
                }

                Vector3 end = GridToWorld(entry.Key);
                Vector3 inward = GridToWorld(entry.Value[0]) - end;
                Quaternion rotation = inward.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(inward.normalized, Vector3.up)
                    : Quaternion.identity;
                LowPolyAssets.Cylinder(
                    "SidingStop",
                    tracksRoot,
                    end + Vector3.up * 0.06f,
                    0.34f,
                    0.1f,
                    bufferColor,
                    8);
                LowPolyAssets.Cube(
                    "SidingBumper",
                    tracksRoot,
                    end + Vector3.up * 0.34f + rotation * Vector3.forward * 0.28f,
                    new Vector3(0.66f, 0.5f, 0.18f),
                    bufferColor,
                    rotation);
                LowPolyAssets.Cube(
                    "SidingWarning",
                    tracksRoot,
                    end + Vector3.up * 0.46f + rotation * Vector3.forward * 0.28f,
                    new Vector3(0.7f, 0.08f, 0.2f),
                    stripeColor,
                    rotation,
                    false,
                    0f,
                    0.6f);
            }
        }

        private static void BuildStations(LevelDefinition level, Transform root, List<StationView> views)
        {
            Transform stationsRoot = new GameObject("Stations").transform;
            stationsRoot.SetParent(root, false);
            IReadOnlyList<StationDefinition> stations = level.Map.Stations;
            for (int i = 0; i < stations.Count; i++)
            {
                StationDefinition station = stations[i];
                Vector3 position = GridToWorld(station.Position);
                GameObject stationRoot = new GameObject((station.IsSource ? "Source_" : "Goal_") + RailPalette.NameCn(station.Color));
                stationRoot.transform.SetParent(stationsRoot, false);
                stationRoot.transform.position = position;

                Vector3 direction = GridToWorld(station.Link) - position;
                Quaternion rotation = direction.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                    : Quaternion.identity;
                Vector3 side = rotation * Vector3.right;
                Color stationColor = RailPalette.ForColor(station.Color);
                Color roofColor = station.IsSource ? LowPolyAssets.Lighten(stationColor, 0.22f) : LowPolyAssets.Darken(stationColor, 0.15f);

                LowPolyAssets.Cube(
                    "Platform",
                    stationRoot.transform,
                    side * 0.82f + Vector3.up * 0.08f,
                    new Vector3(1.45f, 0.16f, 1.6f),
                    LowPolyAssets.Lighten(level.Region.Ground, 0.24f),
                    rotation);
                LowPolyAssets.Cube(
                    "StationBody",
                    stationRoot.transform,
                    side * 0.82f + Vector3.up * 0.42f,
                    new Vector3(1.12f, 0.62f, 1.3f),
                    stationColor,
                    rotation,
                    false,
                    0.05f);
                LowPolyAssets.Cube(
                    "StationRoof",
                    stationRoot.transform,
                    side * 0.82f + Vector3.up * 0.8f,
                    new Vector3(1.34f, 0.12f, 1.5f),
                    roofColor,
                    rotation);
                LowPolyAssets.Cube(
                    "StationDoor",
                    stationRoot.transform,
                    side * 0.82f + Vector3.up * 0.36f + rotation * Vector3.forward * 0.66f,
                    new Vector3(0.42f, 0.5f, 0.035f),
                    new Color(0.12f, 0.17f, 0.17f),
                    rotation);
                LowPolyAssets.Cube(
                    "StationSign",
                    stationRoot.transform,
                    side * 0.82f + Vector3.up * 1.02f,
                    new Vector3(1.25f, 0.22f, 0.1f),
                    station.IsSource ? RailPalette.Paper : roofColor,
                    rotation);

                if (station.IsSource)
                {
                    LowPolyAssets.Cone(
                        "SourceAntenna",
                        stationRoot.transform,
                        side * 0.82f + Vector3.up * 1.24f,
                        0.13f,
                        0.44f,
                        RailPalette.Paper,
                        6);
                    LowPolyAssets.Sphere(
                        "SourceSignal",
                        stationRoot.transform,
                        side * 0.82f + Vector3.up * 1.48f,
                        Vector3.one * 0.2f,
                        stationColor,
                        true);
                }
                else
                {
                    LowPolyAssets.Cylinder(
                        "GoalDisk",
                        stationRoot.transform,
                        side * 0.82f + Vector3.up * 1.02f,
                        0.35f,
                        0.09f,
                        RailPalette.Paper,
                        10);
                    LowPolyAssets.Cylinder(
                        "GoalCenter",
                        stationRoot.transform,
                        side * 0.82f + Vector3.up * 1.08f,
                        0.18f,
                        0.08f,
                        stationColor,
                        10,
                        null,
                        false,
                        0f,
                        0.8f);
                }

                for (int passenger = 0; passenger < 4; passenger++)
                {
                    float row = passenger / 2;
                    float column = passenger % 2;
                    Vector3 passengerPosition =
                        side * (0.34f + row * 0.22f) +
                        Vector3.up * 0.2f +
                        rotation * Vector3.forward * (-0.42f + column * 0.34f);
                    Color passengerColor = RailPalette.ForColor((RailColor)passenger);
                    LowPolyAssets.Cylinder(
                        "WaitingPassenger",
                        stationRoot.transform,
                        passengerPosition,
                        0.08f,
                        0.18f,
                        passengerColor,
                        6);
                    LowPolyAssets.Sphere(
                        "WaitingHead",
                        stationRoot.transform,
                        passengerPosition + Vector3.up * 0.13f,
                        Vector3.one * 0.13f,
                        RailPalette.Paper);
                }

                views.Add(new StationView(station, stationRoot.transform));
                LowPolyAssets.SetLayerRecursively(stationRoot, 0);
            }
        }

        private static void BuildSwitches(
            LevelDefinition level,
            Transform root,
            Dictionary<Vector2Int, TrackSwitchView> switches,
            bool preview)
        {
            Transform switchRoot = new GameObject("Switches").transform;
            switchRoot.SetParent(root, false);
            Dictionary<Vector2Int, List<Vector2Int>> graph = BuildAdjacency(level.Map.Edges);
            foreach (KeyValuePair<Vector2Int, List<Vector2Int>> pair in graph)
            {
                if (pair.Value.Count < 3 || HasForbiddenLevelMix(level.Map, pair.Key))
                {
                    continue;
                }

                TrackSwitchView view = TrackSwitchView.Create(
                    pair.Key,
                    pair.Value,
                    level.Index + 11,
                    switchRoot,
                    level.Region.Accent);
                switches[pair.Key] = view;
            }

            if (!preview)
            {
                return;
            }
        }

        private static bool HasForbiddenLevelMix(RailMap map, Vector2Int node)
        {
            bool elevated = false;
            bool ground = false;
            for (int i = 0; i < map.Edges.Count; i++)
            {
                RailEdge edge = map.Edges[i];
                if (edge.A != node && edge.B != node)
                {
                    continue;
                }

                TrackKind kind = map.KindOf(edge);
                elevated |= kind == TrackKind.Bridge;
                ground |= RailMap.IsGroundKind(kind);
            }

            return elevated && ground;
        }

        private static void BuildDecor(LevelDefinition level, Transform root, int seed)
        {
            RegionDefinition region = level.Region;
            Transform decorRoot = new GameObject("Scenery").transform;
            decorRoot.SetParent(root, false);
            Bounds bounds = CalculateBounds(level);
            System.Random random = new System.Random(seed * 7919 + level.Index * 97 + 3);
            IReadOnlyList<RailEdge> edges = level.Map.Edges;

            int target = region.Index == 3 ? 46 : 54;
            int created = 0;
            int attempts = 0;
            while (created < target && attempts < target * 8)
            {
                attempts++;
                float x = bounds.center.x + ((float)random.NextDouble() - 0.5f) * (bounds.size.x + 12f);
                float z = bounds.center.z + ((float)random.NextDouble() - 0.5f) * (bounds.size.z + 12f);
                Vector2 point = new Vector2(x, z);
                if (DistanceToRail(point, edges) < 1.45f)
                {
                    continue;
                }

                float size = 0.72f + (float)random.NextDouble() * 0.75f;
                float rotation = (float)random.NextDouble() * 360f;
                switch (region.Index)
                {
                    case 0:
                        CreatePine(decorRoot, point, size, rotation, region, false);
                        break;
                    case 1:
                        if (created % 4 == 0)
                        {
                            CreateRailShack(decorRoot, point, size, rotation, region);
                        }
                        else
                        {
                            CreateRock(decorRoot, point, size, rotation, region);
                        }
                        break;
                    case 2:
                        CreatePine(decorRoot, point, size, rotation, region, true);
                        break;
                    case 3:
                        CreateHarborProp(decorRoot, point, size, rotation, region, created);
                        break;
                    case 4:
                        if (created % 5 == 0)
                        {
                            CreateCactus(decorRoot, point, size, region);
                        }
                        else
                        {
                            CreateDuneRock(decorRoot, point, size, rotation, region);
                        }
                        break;
                    case 5:
                        CreateCloudProp(decorRoot, point, size, rotation, region, created);
                        break;
                    case 6:
                        if (created % 5 == 0)
                        {
                            CreateRailShack(decorRoot, point, size, rotation, region);
                        }
                        else
                        {
                            CreateFarmProp(decorRoot, point, size, rotation, region, created);
                        }
                        break;
                    default:
                        CreateFogProp(decorRoot, point, size, rotation, region, created);
                        break;
                }

                created++;
            }

            if (region.Index == 2 || region.Index == 5)
            {
                for (int i = 0; i < 8; i++)
                {
                    float x = bounds.center.x + ((float)random.NextDouble() - 0.5f) * (bounds.size.x + 8f);
                    float z = bounds.center.z + ((float)random.NextDouble() - 0.5f) * (bounds.size.z + 8f);
                    if (DistanceToRail(new Vector2(x, z), edges) < 2f)
                    {
                        continue;
                    }

                    CreateCloud(
                        decorRoot,
                        new Vector3(x, 3.8f + (float)random.NextDouble() * 2.4f, z),
                        new Color(0.88f, 0.92f, 0.93f),
                        0.75f + (float)random.NextDouble() * 0.8f);
                }
            }
        }

        private static void CreatePine(Transform root, Vector2 point, float size, float rotation, RegionDefinition region, bool frost)
        {
            GameObject tree = new GameObject(frost ? "SnowPine" : "Pine");
            tree.transform.SetParent(root, false);
            tree.transform.position = new Vector3(point.x, 0f, point.y);
            tree.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
            Color trunk = LowPolyAssets.Darken(region.RailWood, 0.18f);
            Color leaf = Color.Lerp(region.Foliage, LowPolyAssets.Darken(region.Foliage, 0.22f), (rotation % 10f) / 10f);
            LowPolyAssets.Cylinder("Trunk", tree.transform, new Vector3(0f, 0.62f * size, 0f), 0.15f * size, 1.25f * size, trunk, 6);
            LowPolyAssets.Cone("CrownLow", tree.transform, new Vector3(0f, 1.44f * size, 0f), 0.72f * size, 1.6f * size, leaf, 7);
            LowPolyAssets.Cone("CrownHigh", tree.transform, new Vector3(0f, 2.32f * size, 0f), 0.52f * size, 1.35f * size, LowPolyAssets.Lighten(leaf, 0.1f), 7);
            if (frost)
            {
                LowPolyAssets.Cone("SnowCap", tree.transform, new Vector3(0f, 2.62f * size, 0f), 0.37f * size, 0.75f * size, new Color(0.92f, 0.97f, 0.98f), 7);
            }
        }

        private static void CreateRock(Transform root, Vector2 point, float size, float rotation, RegionDefinition region)
        {
            GameObject rock = new GameObject("CanyonRock");
            rock.transform.SetParent(root, false);
            rock.transform.position = new Vector3(point.x, 0f, point.y);
            rock.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
            Color color = Color.Lerp(region.Rock, region.GroundAlt, (rotation % 20f) / 20f);
            LowPolyAssets.Cylinder("RockBody", rock.transform, new Vector3(0f, 0.28f * size, 0f), 0.48f * size, 0.56f * size, color, 7);
            LowPolyAssets.Cone("RockTop", rock.transform, new Vector3(0f, 0.8f * size, 0f), 0.49f * size, 0.75f * size, LowPolyAssets.Lighten(color, 0.08f), 7);
        }

        private static void CreateDuneRock(Transform root, Vector2 point, float size, float rotation, RegionDefinition region)
        {
            GameObject rock = new GameObject("Sandstone");
            rock.transform.SetParent(root, false);
            rock.transform.position = new Vector3(point.x, 0f, point.y);
            rock.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
            LowPolyAssets.Cube("Stone", rock.transform, new Vector3(0f, 0.28f * size, 0f), new Vector3(0.9f, 0.58f, 0.68f) * size, region.Rock, Quaternion.Euler(8f, 17f, 12f));
            LowPolyAssets.Cube("StoneTop", rock.transform, new Vector3(0.08f, 0.57f * size, 0f), new Vector3(0.68f, 0.26f, 0.56f) * size, LowPolyAssets.Lighten(region.Rock, 0.12f), Quaternion.Euler(-13f, 7f, 18f));
        }

        private static void CreateCactus(Transform root, Vector2 point, float size, RegionDefinition region)
        {
            GameObject cactus = new GameObject("Cactus");
            cactus.transform.SetParent(root, false);
            cactus.transform.position = new Vector3(point.x, 0f, point.y);
            LowPolyAssets.Cylinder("Stem", cactus.transform, new Vector3(0f, 0.82f * size, 0f), 0.13f * size, 1.65f * size, region.Foliage, 7);
            LowPolyAssets.Cylinder("Arm", cactus.transform, new Vector3(0.23f * size, 0.92f * size, 0f), 0.09f * size, 0.62f * size, region.Foliage, 7, Quaternion.Euler(0f, 0f, -70f));
            LowPolyAssets.Cylinder("ArmUp", cactus.transform, new Vector3(0.39f * size, 1.08f * size, 0f), 0.09f * size, 0.4f * size, region.Foliage, 7);
        }

        private static void CreateRailShack(Transform root, Vector2 point, float size, float rotation, RegionDefinition region)
        {
            GameObject shack = new GameObject("RailShack");
            shack.transform.SetParent(root, false);
            shack.transform.position = new Vector3(point.x, 0f, point.y);
            shack.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
            LowPolyAssets.Cube("Body", shack.transform, new Vector3(0f, 0.48f * size, 0f), new Vector3(1.4f, 0.96f, 1.08f) * size, region.GroundAlt);
            LowPolyAssets.Cube("Roof", shack.transform, new Vector3(0f, 1.02f * size, 0f), new Vector3(1.62f, 0.18f, 1.28f) * size, region.RailWood);
            LowPolyAssets.Cube("Door", shack.transform, new Vector3(0f, 0.42f * size, 0.56f * size), new Vector3(0.34f, 0.62f, 0.04f) * size, new Color(0.13f, 0.16f, 0.16f));
        }

        private static void CreateHarborProp(Transform root, Vector2 point, float size, float rotation, RegionDefinition region, int index)
        {
            GameObject prop = new GameObject(index % 4 == 0 ? "HarborLamp" : "Container");
            prop.transform.SetParent(root, false);
            prop.transform.position = new Vector3(point.x, 0f, point.y);
            prop.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
            if (index % 4 == 0)
            {
                LowPolyAssets.Cylinder("LampPost", prop.transform, new Vector3(0f, 1.1f, 0f), 0.06f, 2.2f, region.Rock, 7);
                LowPolyAssets.Sphere("Lamp", prop.transform, new Vector3(0f, 2.25f, 0f), Vector3.one * 0.3f, region.Accent, true);
            }
            else
            {
                Color crate = index % 3 == 0 ? RailPalette.Amber : index % 3 == 1 ? RailPalette.Azure : RailPalette.Crimson;
                LowPolyAssets.Cube("Crate", prop.transform, new Vector3(0f, 0.42f * size, 0f), new Vector3(1.45f, 0.82f, 0.82f) * size, crate, null, false, 0.2f);
                LowPolyAssets.Cube("CrateBand", prop.transform, new Vector3(0f, 0.42f * size, 0f), new Vector3(1.5f, 0.1f, 0.87f) * size, LowPolyAssets.Darken(crate, 0.24f));
            }
        }

        private static void CreateCloudProp(Transform root, Vector2 point, float size, float rotation, RegionDefinition region, int index)
        {
            if (index % 3 == 0)
            {
                CreateCloud(root, new Vector3(point.x, 3.3f + size, point.y), new Color(0.9f, 0.94f, 0.95f), size);
                return;
            }

            GameObject pillar = new GameObject("SkyPillar");
            pillar.transform.SetParent(root, false);
            pillar.transform.position = new Vector3(point.x, 0f, point.y);
            pillar.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
            LowPolyAssets.Cylinder("Base", pillar.transform, new Vector3(0f, 0.4f * size, 0f), 0.52f * size, 0.8f * size, region.Rock, 7);
            LowPolyAssets.Cone("Peak", pillar.transform, new Vector3(0f, 1.15f * size, 0f), 0.5f * size, 1.05f * size, LowPolyAssets.Lighten(region.Rock, 0.14f), 7);
        }

        private static void CreateFarmProp(Transform root, Vector2 point, float size, float rotation, RegionDefinition region, int index)
        {
            GameObject prop = new GameObject(index % 4 == 0 ? "HayBale" : "CropRow");
            prop.transform.SetParent(root, false);
            prop.transform.position = new Vector3(point.x, 0f, point.y);
            prop.transform.rotation = Quaternion.Euler(0f, rotation, 0f);

            if (index % 4 == 0)
            {
                Color hay = new Color(0.91f, 0.72f, 0.31f);
                LowPolyAssets.Cylinder("Bale", prop.transform, new Vector3(0f, 0.32f * size, 0f), 0.43f * size, 0.58f * size, hay, 9, Quaternion.Euler(0f, 0f, 90f));
                LowPolyAssets.Cube("HayBand", prop.transform, new Vector3(0f, 0.32f * size, 0f), new Vector3(0.63f, 0.08f, 0.72f) * size, LowPolyAssets.Darken(hay, 0.24f));
            }
            else
            {
                Color crop = index % 2 == 0 ? new Color(0.83f, 0.69f, 0.18f) : region.Foliage;
                for (int row = -1; row <= 1; row++)
                {
                    LowPolyAssets.Cylinder(
                        "Crop",
                        prop.transform,
                        new Vector3(row * 0.28f * size, 0.3f * size, 0f),
                        0.16f * size,
                        0.6f * size,
                        crop,
                        5);
                }
            }
        }

        private static void CreateFogProp(Transform root, Vector2 point, float size, float rotation, RegionDefinition region, int index)
        {
            GameObject prop = new GameObject(index % 3 == 0 ? "GasLamp" : "FogTree");
            prop.transform.SetParent(root, false);
            prop.transform.position = new Vector3(point.x, 0f, point.y);
            prop.transform.rotation = Quaternion.Euler(0f, rotation, 0f);

            if (index % 3 == 0)
            {
                LowPolyAssets.Cylinder("Post", prop.transform, new Vector3(0f, 1.1f * size, 0f), 0.05f * size, 2.2f * size, region.Rock, 7);
                LowPolyAssets.Sphere("Lamp", prop.transform, new Vector3(0f, 2.25f * size, 0f), Vector3.one * 0.28f * size, region.Accent, true);
            }
            else
            {
                LowPolyAssets.Cylinder("Trunk", prop.transform, new Vector3(0f, 0.75f * size, 0f), 0.13f * size, 1.5f * size, region.RailWood, 6);
                LowPolyAssets.Cone("Canopy", prop.transform, new Vector3(0f, 1.75f * size, 0f), 0.68f * size, 1.8f * size, region.Foliage, 7);
            }
        }

        private static void CreateCloud(Transform root, Vector3 position, Color color, float size)
        {
            GameObject cloud = new GameObject("Cloud");
            cloud.transform.SetParent(root, false);
            cloud.transform.position = position;
            LowPolyAssets.Sphere("CloudA", cloud.transform, new Vector3(-0.55f, 0f, 0f) * size, new Vector3(1.4f, 0.65f, 1f) * size, color);
            LowPolyAssets.Sphere("CloudB", cloud.transform, new Vector3(0.05f, 0.18f, 0f) * size, new Vector3(1.65f, 0.9f, 1.15f) * size, LowPolyAssets.Lighten(color, 0.08f));
            LowPolyAssets.Sphere("CloudC", cloud.transform, new Vector3(0.72f, 0f, 0.08f) * size, new Vector3(1.18f, 0.58f, 0.95f) * size, color);
        }

        private static float DistanceToRail(Vector2 point, IReadOnlyList<RailEdge> edges)
        {
            float best = float.MaxValue;
            for (int i = 0; i < edges.Count; i++)
            {
                Vector2 a = new Vector2(edges[i].A.x * GridSize, edges[i].A.y * GridSize);
                Vector2 b = new Vector2(edges[i].B.x * GridSize, edges[i].B.y * GridSize);
                float distance = DistanceToSegment(point, a, b);
                if (distance < best)
                {
                    best = distance;
                }
            }

            return best;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            if (lengthSquared < 0.0001f)
            {
                return Vector2.Distance(point, a);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
            return Vector2.Distance(point, a + ab * t);
        }
    }
}
