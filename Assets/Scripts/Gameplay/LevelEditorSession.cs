using System.Collections.Generic;
using UnityEngine;

namespace TrainTrain
{
    public enum EditorTool
    {
        Track,
        Bridge,
        Tunnel,
        SideTrack,
        Station,
        Train,
        Erase
    }

    public sealed class LevelEditorSession
    {
        private readonly Stack<string> undo = new Stack<string>();
        private readonly Stack<string> redo = new Stack<string>();
        private Vector2Int? anchor;
        private RailColor stationColor;
        private TrainStyle trainStyle;

        public CustomLevelDocument Document { get; private set; }
        public EditorTool Tool { get; private set; }
        public string Status { get; private set; }
        public RailColor StationColor
        {
            get { return stationColor; }
        }
        public TrainStyle TrainStyle
        {
            get { return trainStyle; }
        }
        public Vector2Int? Anchor
        {
            get { return anchor; }
        }

        public LevelEditorSession()
        {
            Document = CreateStarterDocument();
            Tool = EditorTool.Track;
            stationColor = RailColor.Crimson;
            trainStyle = TrainStyle.Steam;
            Status = "空白线路已就绪；拖动鼠标或在两个格点间铺设轨道";
        }

        public void SetTool(EditorTool tool)
        {
            Tool = tool;
            anchor = null;
            if (tool == EditorTool.Bridge)
            {
                Status = "高架至少三格，两端坡道会自动生成";
            }
            else if (tool == EditorTool.Tunnel)
            {
                Status = "隧道保持在地面层，不会与高架轨道直接连通";
            }
            else
            {
                Status = tool == EditorTool.Train
                    ? "单击任意一段轨道即可放置列车"
                    : "点击网格进行编辑";
            }
        }

        public void ClearAnchor()
        {
            ClearAnchor("已取消当前起点");
        }

        public void ClearAnchor(string status)
        {
            anchor = null;
            Status = status;
        }

        public List<Vector2Int> GetPreviewPath(Vector2Int end)
        {
            if (!anchor.HasValue)
            {
                return new List<Vector2Int>();
            }

            return BuildOrthogonalPath(anchor.Value, end);
        }

        public bool TryPlaceTrain(Vector2 gridPosition)
        {
            if (Document.trains.Count >= 4)
            {
                Status = "最多只能放置四列列车";
                return false;
            }

            CustomRailEdge bestEdge = null;
            float bestDistance = 0.9f;
            Vector2Int bestFrom = default;
            Vector2Int bestTo = default;
            for (int i = 0; i < Document.edges.Count; i++)
            {
                CustomRailEdge edge = Document.edges[i];
                Vector2 a = new Vector2(edge.ax, edge.az);
                Vector2 b = new Vector2(edge.bx, edge.bz);
                Vector2 segment = b - a;
                float lengthSquared = segment.sqrMagnitude;
                if (lengthSquared < 0.001f)
                {
                    continue;
                }

                float t = Mathf.Clamp01(Vector2.Dot(gridPosition - a, segment) / lengthSquared);
                float distance = Vector2.Distance(gridPosition, a + segment * t);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestEdge = edge;
                if (t <= 0.5f)
                {
                    bestFrom = edge.A;
                    bestTo = edge.B;
                }
                else
                {
                    bestFrom = edge.B;
                    bestTo = edge.A;
                }
            }

            if (bestEdge == null)
            {
                Status = "请点击已有轨道放置列车";
                return false;
            }

            PushUndo();
            Document.trains.Add(new CustomTrainSpawn
            {
                fromX = bestFrom.x,
                fromZ = bestFrom.y,
                toX = bestTo.x,
                toZ = bestTo.y,
                style = trainStyle,
                startStopped = false
            });
            Status = "列车已放在轨道上并自动发车（" + Document.trains.Count + "/4）";
            return true;
        }

        public void CycleStationColor()
        {
            stationColor = (RailColor)(((int)stationColor + 1) % 4);
            Status = "车站颜色：" + RailPalette.NameCn(stationColor);
        }

        public void CycleTrainStyle()
        {
            trainStyle = (TrainStyle)(((int)trainStyle + 1) % 4);
            Status = "列车车型：" + TrainStyleCatalog.Get(trainStyle).NameCn;
        }

        public void Apply(Vector2Int point)
        {
            if (point.x < -6 || point.x > 6 || point.y < -4 || point.y > 4)
            {
                Status = "超出编辑边界";
                return;
            }

            switch (Tool)
            {
                case EditorTool.Track:
                case EditorTool.Bridge:
                case EditorTool.Tunnel:
                case EditorTool.SideTrack:
                    ApplyTrack(point);
                    break;
                case EditorTool.Station:
                    ApplyStation(point);
                    break;
                case EditorTool.Train:
                    ApplyTrain(point);
                    break;
                case EditorTool.Erase:
                    ApplyErase(point);
                    break;
            }
        }

        public void Undo()
        {
            if (undo.Count == 0)
            {
                Status = "没有可撤销的操作";
                return;
            }

            redo.Push(JsonUtility.ToJson(Document));
            Document = JsonUtility.FromJson<CustomLevelDocument>(undo.Pop());
            anchor = null;
            Status = "已撤销";
        }

        public void Redo()
        {
            if (redo.Count == 0)
            {
                Status = "没有可重做的操作";
                return;
            }

            undo.Push(JsonUtility.ToJson(Document));
            Document = JsonUtility.FromJson<CustomLevelDocument>(redo.Pop());
            anchor = null;
            Status = "已重做";
        }

        public LevelDefinition ToLevelDefinition()
        {
            return ToLevelDefinition(Document);
        }

        public LevelDefinition ToLevelDefinition(CustomLevelDocument document)
        {
            RegionDefinition region = LevelCatalog.Regions[Mathf.Clamp(document.themeIndex, 0, LevelCatalog.Regions.Count - 1)];
            List<RailEdge> edges = new List<RailEdge>();
            Dictionary<RailEdge, TrackKind> kinds = new Dictionary<RailEdge, TrackKind>();
            for (int i = 0; i < document.edges.Count; i++)
            {
                CustomRailEdge source = document.edges[i];
                RailEdge edge = new RailEdge(source.A, source.B);
                if (!edges.Contains(edge))
                {
                    edges.Add(edge);
                    kinds[edge] = source.kind;
                }
            }

            List<StationDefinition> stations = new List<StationDefinition>();
            for (int i = 0; i < document.stations.Count; i++)
            {
                CustomStationDefinition source = document.stations[i];
                Vector2Int position = new Vector2Int(source.x, source.z);
                Vector2Int link = FindNeighbor(document, position);
                stations.Add(new StationDefinition(
                    position,
                    link,
                    source.color,
                    false,
                    source.initialPassengers,
                    source.capacity,
                    source.spawnInterval,
                    source.generationMask));
            }

            int[] quotas = new int[4];
            for (int i = 0; i < quotas.Length; i++)
            {
                quotas[i] = document.passengerGoal / 4;
            }

            quotas[0] += document.passengerGoal - quotas[0] - quotas[1] - quotas[2] - quotas[3];
            RailMap map = new RailMap(document.name, edges, stations, kinds);
            return new LevelDefinition(
                9000 + Mathf.Abs(document.id.GetHashCode() % 1000),
                region.Index,
                0,
                document.name,
                "CUSTOM LINE",
                "自定义关卡",
                quotas,
                Mathf.Max(10f, document.duration),
                1.8f,
                region,
                map,
                Mathf.Clamp(document.trains.Count, 1, 4),
                Mathf.Max(1, document.stations.Count > 0 ? document.stations[0].initialPassengers : 4),
                Mathf.Max(1, document.stationCapacity),
                Mathf.Max(0.4f, document.stationSpawnInterval),
                document.supportedColorMask);
        }

        public void Import(CustomLevelDocument document)
        {
            Document = document.Clone();
            undo.Clear();
            redo.Clear();
            anchor = null;
            Status = "已导入 " + Document.name;
        }

        public void Reset()
        {
            PushUndo();
            Document = CreateStarterDocument();
            anchor = null;
            Status = "已建立新的空白线路";
        }

        private void ApplyTrack(Vector2Int point)
        {
            if (!anchor.HasValue)
            {
                anchor = point;
                Status = "起点已选择；拖动或再次点击终点";
                return;
            }

            Vector2Int previous = anchor.Value;
            if (point == previous)
            {
                anchor = null;
                Status = "已取消当前起点";
                return;
            }

            List<Vector2Int> path = BuildOrthogonalPath(previous, point);
            if (path.Count < 2)
            {
                anchor = null;
                Status = "已取消当前起点";
                return;
            }

            bool elevatedAtStart = Tool == EditorTool.Bridge && NodeTouchesBridge(previous);
            bool elevatedAtEnd = Tool == EditorTool.Bridge && NodeTouchesBridge(point);
            if (Tool == EditorTool.Bridge &&
                !elevatedAtStart &&
                !elevatedAtEnd &&
                path.Count < 4)
            {
                anchor = null;
                Status = "高架至少需要三格，并会自动生成两端坡道";
                return;
            }

            string before = JsonUtility.ToJson(Document);
            int added = 0;
            for (int i = 1; i < path.Count; i++)
            {
                TrackKind kind = Tool == EditorTool.Bridge
                    ? IsBridgeSegmentElevated(i, path.Count, elevatedAtStart, elevatedAtEnd)
                        ? TrackKind.Bridge
                        : TrackKind.Ramp
                    : Tool == EditorTool.Tunnel
                        ? TrackKind.Tunnel
                        : Tool == EditorTool.SideTrack
                            ? TrackKind.SideTrack
                            : TrackKind.Standard;
                CustomRailEdge edge = new CustomRailEdge(path[i - 1], path[i], kind);
                CustomRailEdge existing = Document.edges.Find(candidate =>
                    (candidate.A == edge.A && candidate.B == edge.B) ||
                    (candidate.A == edge.B && candidate.B == edge.A));
                if (existing == null)
                {
                    Document.edges.Add(edge);
                    added++;
                }
                else if (existing.kind != kind)
                {
                    existing.kind = kind;
                    added++;
                }
            }

            if (added > 0 && HasIllegalLayerMix())
            {
                Document = JsonUtility.FromJson<CustomLevelDocument>(before);
                anchor = null;
                Status = "无法连接：高架与地面之间必须使用坡道，且坡道不能被普通轨道截断";
                return;
            }

            if (added > 0)
            {
                undo.Push(before);
                redo.Clear();
            }

            anchor = null;
            Status = added > 0
                ? (Tool == EditorTool.Bridge
                    ? "已铺设高架，两端坡道已自动连接"
                    : "已铺设 " + added + " 段轨道；点击新的起点继续")
                : "这条路线已经存在";
        }

        private void ApplyStation(Vector2Int point)
        {
            if (!HasNode(point))
            {
                Status = "车站必须放在已有轨道节点上";
                return;
            }

            PushUndo();
            CustomStationDefinition existing = Document.stations.Find(station => station.x == point.x && station.z == point.y);
            if (existing != null)
            {
                existing.color = (RailColor)(((int)existing.color + 1) % 4);
                stationColor = existing.color;
                Status = "车站颜色改为：" + RailPalette.NameCn(existing.color);
                return;
            }

            Document.stations.Add(new CustomStationDefinition
            {
                x = point.x,
                z = point.y,
                color = stationColor,
                initialPassengers = Mathf.Max(1, Document.passengerGoal / Mathf.Max(2, Document.stations.Count + 2)),
                capacity = Document.stationCapacity,
                spawnInterval = Document.stationSpawnInterval,
                generationMask = Document.supportedColorMask
            });
            Status = "已放置" + RailPalette.NameCn(stationColor) + "车站";
        }

        private void ApplyTrain(Vector2Int point)
        {
            TryPlaceTrain(new Vector2(point.x, point.y));
            anchor = null;
        }

        private void ApplyErase(Vector2Int point)
        {
            PushUndo();
            int removed = Document.edges.RemoveAll(edge => edge.A == point || edge.B == point);
            removed += Document.stations.RemoveAll(station => station.x == point.x && station.z == point.y);
            removed += Document.trains.RemoveAll(train =>
                (train.fromX == point.x && train.fromZ == point.y) ||
                (train.toX == point.x && train.toZ == point.y));
            Status = removed > 0 ? "已删除 " + removed + " 个元素" : "此处没有可删除元素";
            anchor = null;
        }

        private bool HasNode(Vector2Int point)
        {
            return Document.edges.Exists(edge => edge.A == point || edge.B == point);
        }

        private bool AreNeighbors(Vector2Int a, Vector2Int b)
        {
            return Document.edges.Exists(edge =>
                (edge.A == a && edge.B == b) || (edge.A == b && edge.B == a));
        }

        private bool NodeTouchesBridge(Vector2Int node)
        {
            return Document.edges.Exists(edge =>
                edge.kind == TrackKind.Bridge &&
                (edge.A == node || edge.B == node));
        }

        private bool HasIllegalLayerMix()
        {
            Dictionary<Vector2Int, bool> bridges = new Dictionary<Vector2Int, bool>();
            Dictionary<Vector2Int, bool> ground = new Dictionary<Vector2Int, bool>();
            for (int i = 0; i < Document.edges.Count; i++)
            {
                CustomRailEdge edge = Document.edges[i];
                ApplyLayerFlags(edge.A, edge.kind, bridges, ground);
                ApplyLayerFlags(edge.B, edge.kind, bridges, ground);
            }

            foreach (Vector2Int node in bridges.Keys)
            {
                if (bridges[node] && ground.TryGetValue(node, out bool hasGround) && hasGround)
                {
                    return true;
                }
            }

            for (int i = 0; i < Document.edges.Count; i++)
            {
                CustomRailEdge edge = Document.edges[i];
                if (edge.kind != TrackKind.Ramp)
                {
                    continue;
                }

                bool highA = bridges.TryGetValue(edge.A, out bool a) && a;
                bool highB = bridges.TryGetValue(edge.B, out bool b) && b;
                if (highA == highB)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBridgeSegmentElevated(
            int segment,
            int pointCount,
            bool elevatedAtStart,
            bool elevatedAtEnd)
        {
            if (!elevatedAtStart && segment == 1)
            {
                return false;
            }

            if (!elevatedAtEnd && segment == pointCount - 1)
            {
                return false;
            }

            return true;
        }

        private static void ApplyLayerFlags(
            Vector2Int node,
            TrackKind kind,
            Dictionary<Vector2Int, bool> bridges,
            Dictionary<Vector2Int, bool> ground)
        {
            if (kind == TrackKind.Bridge)
            {
                bridges[node] = true;
            }
            else if (RailMap.IsGroundKind(kind))
            {
                ground[node] = true;
            }
        }

        private static Vector2Int FindNeighbor(CustomLevelDocument document, Vector2Int point)
        {
            CustomRailEdge edge = document.edges.Find(candidate => candidate.A == point || candidate.B == point);
            return edge == null ? point + Vector2Int.right : edge.A == point ? edge.B : edge.A;
        }

        private void PushUndo()
        {
            undo.Push(JsonUtility.ToJson(Document));
            redo.Clear();
        }

        private static CustomLevelDocument CreateStarterDocument()
        {
            CustomLevelDocument document = new CustomLevelDocument
            {
                name = "我的混乱线路",
                themeIndex = 0,
                duration = 75f,
                passengerGoal = 18,
                stationCapacity = 20,
                stationSpawnInterval = 2f,
                supportedColorMask = 15
            };
            for (int x = -4; x < 4; x++)
            {
                document.edges.Add(new CustomRailEdge(new Vector2Int(x, 0), new Vector2Int(x + 1, 0), TrackKind.Standard));
            }

            document.stations.Add(new CustomStationDefinition { x = -4, z = 0, color = RailColor.Crimson });
            document.stations.Add(new CustomStationDefinition { x = 4, z = 0, color = RailColor.Azure });
            return document;
        }

        private static List<Vector2Int> BuildOrthogonalPath(Vector2Int start, Vector2Int end)
        {
            List<Vector2Int> path = new List<Vector2Int> { start };
            Vector2Int current = start;
            int xStep = current.x < end.x ? 1 : -1;
            while (current.x != end.x)
            {
                current.x += xStep;
                path.Add(current);
            }

            int yStep = current.y < end.y ? 1 : -1;
            while (current.y != end.y)
            {
                current.y += yStep;
                path.Add(current);
            }

            return path;
        }
    }
}
