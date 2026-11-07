using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrainTrain
{
    public enum TrackKind
    {
        Standard = 0,
        Bridge = 1,
        Tunnel = 2,
        SideTrack = 3,
        Ramp = 4
    }

    [Serializable]
    public sealed class CustomRailEdge
    {
        public int ax;
        public int az;
        public int bx;
        public int bz;
        public TrackKind kind;

        public CustomRailEdge()
        {
        }

        public CustomRailEdge(Vector2Int a, Vector2Int b, TrackKind kind)
        {
            ax = a.x;
            az = a.y;
            bx = b.x;
            bz = b.y;
            this.kind = kind;
        }

        public Vector2Int A
        {
            get { return new Vector2Int(ax, az); }
        }

        public Vector2Int B
        {
            get { return new Vector2Int(bx, bz); }
        }
    }

    [Serializable]
    public sealed class CustomStationDefinition
    {
        public int x;
        public int z;
        public RailColor color;
        public int initialPassengers = 5;
        public int capacity = 20;
        public float spawnInterval = 2f;
        public int generationMask = 15;
    }

    [Serializable]
    public sealed class CustomTrainSpawn
    {
        public int fromX;
        public int fromZ;
        public int toX;
        public int toZ;
        public TrainStyle style;
        public bool startStopped;
        public float startDelay;
    }

    [Serializable]
    public sealed class CustomLevelDocument
    {
        public int version = 1;
        public string id = Guid.NewGuid().ToString("N");
        public string name = "我的混乱线路";
        public int themeIndex;
        public float duration = 75f;
        public int passengerGoal = 18;
        public int stationCapacity = 20;
        public float stationSpawnInterval = 2f;
        public int supportedColorMask = 15;
        public bool syncAllSwitches;
        public List<CustomRailEdge> edges = new List<CustomRailEdge>();
        public List<CustomStationDefinition> stations = new List<CustomStationDefinition>();
        public List<CustomTrainSpawn> trains = new List<CustomTrainSpawn>();

        public CustomLevelDocument Clone()
        {
            return JsonUtility.FromJson<CustomLevelDocument>(JsonUtility.ToJson(this));
        }
    }

    public interface ICustomLevelStore
    {
        List<CustomLevelDocument> LoadAll();
        void Save(CustomLevelDocument document);
        void Delete(string id);
    }

    public interface ILevelValidator
    {
        bool Validate(CustomLevelDocument document, out List<string> errors);
    }

    public sealed class CustomLevelStore : ICustomLevelStore
    {
        private const string FileName = "custom_levels.json";

        [Serializable]
        private sealed class LevelList
        {
            public List<CustomLevelDocument> levels = new List<CustomLevelDocument>();
        }

        public List<CustomLevelDocument> LoadAll()
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, FileName);
            if (!System.IO.File.Exists(path))
            {
                return new List<CustomLevelDocument>();
            }

            try
            {
                LevelList list = JsonUtility.FromJson<LevelList>(System.IO.File.ReadAllText(path));
                return list != null && list.levels != null ? list.levels : new List<CustomLevelDocument>();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not read custom levels: " + exception.Message);
                return new List<CustomLevelDocument>();
            }
        }

        public void Save(CustomLevelDocument document)
        {
            List<CustomLevelDocument> levels = LoadAll();
            int index = levels.FindIndex(level => level.id == document.id);
            if (index >= 0)
            {
                levels[index] = document;
            }
            else
            {
                levels.Add(document);
            }

            string path = System.IO.Path.Combine(Application.persistentDataPath, FileName);
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(new LevelList { levels = levels }, true));
        }

        public void Delete(string id)
        {
            List<CustomLevelDocument> levels = LoadAll();
            levels.RemoveAll(level => level.id == id);
            string path = System.IO.Path.Combine(Application.persistentDataPath, FileName);
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(new LevelList { levels = levels }, true));
        }
    }

    public sealed class CustomLevelValidator : ILevelValidator
    {
        public bool Validate(CustomLevelDocument document, out List<string> errors)
        {
            errors = new List<string>();
            if (document == null)
            {
                errors.Add("关卡数据为空");
                return false;
            }

            if (string.IsNullOrWhiteSpace(document.name))
            {
                errors.Add("关卡名称不能为空");
            }

            if (document.edges.Count == 0)
            {
                errors.Add("至少需要一段轨道");
            }

            if (document.stations.Count < 2)
            {
                errors.Add("至少需要两个不同颜色的车站");
            }

            if (document.passengerGoal <= 0 || document.duration <= 0f)
            {
                errors.Add("乘客目标和时间必须大于零");
            }

            HashSet<int> stationColors = new HashSet<int>();

            Dictionary<Vector2Int, List<Vector2Int>> graph = new Dictionary<Vector2Int, List<Vector2Int>>();
            Dictionary<Vector2Int, List<CustomRailEdge>> incident =
                new Dictionary<Vector2Int, List<CustomRailEdge>>();
            for (int i = 0; i < document.edges.Count; i++)
            {
                CustomRailEdge edge = document.edges[i];
                AddNeighbor(graph, edge.A, edge.B);
                AddNeighbor(graph, edge.B, edge.A);
                AddIncident(incident, edge.A, edge);
                AddIncident(incident, edge.B, edge);
            }

            foreach (KeyValuePair<Vector2Int, List<CustomRailEdge>> pair in incident)
            {
                bool hasBridge = false;
                bool hasRamp = false;
                bool hasGround = false;
                int bridgeCount = 0;
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    TrackKind kind = pair.Value[i].kind;
                    hasBridge |= kind == TrackKind.Bridge;
                    hasRamp |= kind == TrackKind.Ramp;
                    hasGround |= RailMap.IsGroundKind(kind);
                    if (kind == TrackKind.Bridge)
                    {
                        bridgeCount++;
                    }
                }

                if (hasBridge && hasGround)
                {
                    errors.Add("高架与地面轨道不能直接共用一个节点，请使用坡道连接");
                }

                if (hasBridge && !hasRamp && bridgeCount == 1)
                {
                    errors.Add("高架线路末端需要坡道");
                }
            }

            for (int i = 0; i < document.edges.Count; i++)
            {
                CustomRailEdge edge = document.edges[i];
                if (edge.kind != TrackKind.Ramp)
                {
                    continue;
                }

                bool highAtA = NodeTouchesBridge(incident, edge.A);
                bool highAtB = NodeTouchesBridge(incident, edge.B);
                if (highAtA == highAtB)
                {
                    errors.Add("每段坡道必须连接一侧高架和一侧地面");
                }
            }

            for (int i = 0; i < document.stations.Count; i++)
            {
                stationColors.Add((int)document.stations[i].color);
                if (!graph.ContainsKey(new Vector2Int(document.stations[i].x, document.stations[i].z)))
                {
                    errors.Add("车站没有连接轨道");
                }
            }

            if (stationColors.Count < 2)
            {
                errors.Add("至少需要两种颜色的车站");
            }

            for (int i = 0; i < document.trains.Count; i++)
            {
                CustomTrainSpawn train = document.trains[i];
                Vector2Int from = new Vector2Int(train.fromX, train.fromZ);
                Vector2Int to = new Vector2Int(train.toX, train.toZ);
                if (!graph.TryGetValue(from, out List<Vector2Int> neighbors) || !neighbors.Contains(to))
                {
                    errors.Add("列车起点或发车方向无效");
                }
            }

            if (graph.Count > 0)
            {
                HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                Vector2Int start = default;
                foreach (Vector2Int key in graph.Keys)
                {
                    start = key;
                    break;
                }

                visited.Add(start);
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    Vector2Int node = queue.Dequeue();
                    foreach (Vector2Int neighbor in graph[node])
                    {
                        if (visited.Add(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                if (visited.Count != graph.Count)
                {
                    errors.Add("轨道网络必须全部连通");
                }
            }

            return errors.Count == 0;
        }

        private static void AddNeighbor(Dictionary<Vector2Int, List<Vector2Int>> graph, Vector2Int a, Vector2Int b)
        {
            if (a == b)
            {
                return;
            }

            if (!graph.TryGetValue(a, out List<Vector2Int> list))
            {
                list = new List<Vector2Int>();
                graph[a] = list;
            }

            if (!list.Contains(b))
            {
                list.Add(b);
            }
        }

        private static void AddIncident(
            Dictionary<Vector2Int, List<CustomRailEdge>> incident,
            Vector2Int node,
            CustomRailEdge edge)
        {
            if (!incident.TryGetValue(node, out List<CustomRailEdge> edges))
            {
                edges = new List<CustomRailEdge>();
                incident[node] = edges;
            }

            edges.Add(edge);
        }

        private static bool NodeTouchesBridge(
            Dictionary<Vector2Int, List<CustomRailEdge>> incident,
            Vector2Int node)
        {
            if (!incident.TryGetValue(node, out List<CustomRailEdge> edges))
            {
                return false;
            }

            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i].kind == TrackKind.Bridge)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
