using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrainTrain.Editor
{
    [InitializeOnLoad]
    public static class TrainTrainProjectSetup
    {
        private const string MainScenePath = "Assets/Scenes/Main.unity";
        private const string SessionKey = "TrainTrain_ProjectSetup_2026";

        static TrainTrainProjectSetup()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += RepairProject;
        }

        [MenuItem("Train Train / Repair Project Setup")]
        public static void RepairProject()
        {
            EnsureMainScene();
            EnsureRuntimeMaterials();
            ApplyPlayerSettings();
            EnsureSceneInBuild();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Train Train / Validate Project")]
        public static void ValidateProject()
        {
            RepairProject();
            int errors = 0;
            if (LevelCatalog.Levels.Count != 48)
            {
                Debug.LogError("Expected 48 levels, found " + LevelCatalog.Levels.Count);
                errors++;
            }

            if (LevelCatalog.Regions.Count != 8)
            {
                Debug.LogError("Expected 8 regions, found " + LevelCatalog.Regions.Count);
                errors++;
            }

            for (int i = 0; i < LevelCatalog.Regions.Count; i++)
            {
                RailMap map = LevelCatalog.Regions[i].Map;
                if (map.Stations.Count != 8)
                {
                    Debug.LogError("Region " + i + " should have eight stations.");
                    errors++;
                }

                HashSet<Vector2Int> nodes = new HashSet<Vector2Int>();
                Dictionary<Vector2Int, List<Vector2Int>> graph = new Dictionary<Vector2Int, List<Vector2Int>>();
                for (int e = 0; e < map.Edges.Count; e++)
                {
                    RailEdge edge = map.Edges[e];
                    nodes.Add(edge.A);
                    nodes.Add(edge.B);
                    AddNeighbor(graph, edge.A, edge.B);
                    AddNeighbor(graph, edge.B, edge.A);
                }

                if (nodes.Count < 12)
                {
                    Debug.LogError("Region " + i + " rail graph is too small.");
                    errors++;
                }

                if (!IsConnected(graph, nodes))
                {
                    Debug.LogError("Region " + i + " has disconnected rail sections.");
                    errors++;
                }

                for (int color = 0; color < 4; color++)
                {
                    int sources = 0;
                    int goals = 0;
                    for (int s = 0; s < map.Stations.Count; s++)
                    {
                        if ((int)map.Stations[s].Color != color)
                        {
                            continue;
                        }

                        if (map.Stations[s].IsSource)
                        {
                            sources++;
                        }
                        else
                        {
                            goals++;
                        }
                    }

                    if (sources != 1 || goals != 1)
                    {
                        Debug.LogError("Region " + i + " color " + color + " must have one source and one goal.");
                        errors++;
                    }
                }
            }

            HashSet<string> levelLayouts = new HashSet<string>();
            int[] expectedCounts = { 4, 5, 5, 6, 6, 7, 7, 8 };
            int totalEdges = 0;
            int maximumEdges = 0;
            int corridorLevels = 0;
            if (LevelCatalog.Worlds.Count != expectedCounts.Length)
            {
                Debug.LogError("Expected 8 world definitions.");
                errors++;
            }

            for (int i = 0; i < Mathf.Min(expectedCounts.Length, LevelCatalog.Worlds.Count); i++)
            {
                if (LevelCatalog.Worlds[i].Levels.Count != expectedCounts[i])
                {
                    Debug.LogError("World " + i + " expected " + expectedCounts[i] + " levels.");
                    errors++;
                }
            }

            for (int i = 0; i < LevelCatalog.Levels.Count; i++)
            {
                RailMap map = LevelCatalog.Levels[i].Map;
                Dictionary<Vector2Int, List<Vector2Int>> graph = new Dictionary<Vector2Int, List<Vector2Int>>();
                Dictionary<Vector2Int, List<TrackKind>> incidentKinds =
                    new Dictionary<Vector2Int, List<TrackKind>>();
                HashSet<Vector2Int> nodes = new HashSet<Vector2Int>();
                HashSet<Vector2Int> stationNodes = new HashSet<Vector2Int>();
                List<string> edgeKeys = new List<string>();
                List<string> stationKeys = new List<string>();
                for (int s = 0; s < map.Stations.Count; s++)
                {
                    if (!stationNodes.Add(map.Stations[s].Position))
                    {
                        Debug.LogError("Level " + i + " has overlapping station positions.");
                        errors++;
                    }

                    StationDefinition station = map.Stations[s];
                    stationKeys.Add(station.Position.x + "," + station.Position.y + ":" + (int)station.Color + ":" + (station.IsSource ? "S" : "G"));
                }

                for (int e = 0; e < map.Edges.Count; e++)
                {
                    RailEdge edge = map.Edges[e];
                    nodes.Add(edge.A);
                    nodes.Add(edge.B);
                    AddNeighbor(graph, edge.A, edge.B);
                    AddNeighbor(graph, edge.B, edge.A);
                    AddIncidentKind(incidentKinds, edge.A, map.KindOf(edge));
                    AddIncidentKind(incidentKinds, edge.B, map.KindOf(edge));
                    edgeKeys.Add(EdgeKey(edge));
                }

                if (!IsConnected(graph, nodes))
                {
                    Debug.LogError("Level " + i + " has disconnected rail sections.");
                    errors++;
                }

                foreach (KeyValuePair<Vector2Int, List<TrackKind>> pair in incidentKinds)
                {
                    bool hasBridge = pair.Value.Contains(TrackKind.Bridge);
                    bool hasGround = false;
                    bool hasRamp = pair.Value.Contains(TrackKind.Ramp);
                    int bridgeAtNodeCount = 0;
                    for (int kindIndex = 0; kindIndex < pair.Value.Count; kindIndex++)
                    {
                        TrackKind kind = pair.Value[kindIndex];
                        hasGround |= RailMap.IsGroundKind(kind);
                        if (kind == TrackKind.Bridge)
                        {
                            bridgeAtNodeCount++;
                        }
                    }

                    if (hasBridge && hasGround)
                    {
                        Debug.LogError(
                            "Level " + i + " connects elevated and ground track at " + pair.Key +
                            " without a ramp.");
                        errors++;
                    }

                    if (hasBridge && !hasRamp && bridgeAtNodeCount == 1)
                    {
                        Debug.LogError("Level " + i + " has a bridge end without a ramp at " + pair.Key + ".");
                        errors++;
                    }
                }

                for (int edgeIndex = 0; edgeIndex < map.Edges.Count; edgeIndex++)
                {
                    RailEdge edge = map.Edges[edgeIndex];
                    if (map.KindOf(edge) != TrackKind.Ramp)
                    {
                        continue;
                    }

                    bool highAtA = map.NodeTouchesBridge(edge.A);
                    bool highAtB = map.NodeTouchesBridge(edge.B);
                    if (highAtA == highAtB)
                    {
                        Debug.LogError("Level " + i + " has a ramp that does not descend from raised track.");
                        errors++;
                    }
                }

                int maxEdgesForControlledLoops = Mathf.Max(1, nodes.Count + 2);
                totalEdges += map.Edges.Count;
                maximumEdges = Mathf.Max(maximumEdges, map.Edges.Count);
                if (map.Edges.Count <= nodes.Count - 1)
                {
                    corridorLevels++;
                }

                if (map.Edges.Count > maxEdgesForControlledLoops)
                {
                    Debug.LogError("Level " + i + " is too dense: " + map.Edges.Count + " edges for " + nodes.Count + " nodes.");
                    errors++;
                }

                for (int s = 0; s < map.Stations.Count; s++)
                {
                    StationDefinition station = map.Stations[s];
                    if (!nodes.Contains(station.Position) ||
                        !graph.TryGetValue(station.Position, out List<Vector2Int> stationLinks) ||
                        !stationLinks.Contains(station.Link))
                    {
                        Debug.LogError(
                            "Level " + i + " station " + station.Position +
                            " is not connected to its declared link " + station.Link + ".");
                        errors++;
                    }

                    if (station.Capacity <= 0 || station.SpawnInterval <= 0f || station.InitialPassengers < 0)
                    {
                        Debug.LogError("Level " + i + " has invalid station generation settings.");
                        errors++;
                        break;
                    }
                }

                edgeKeys.Sort(string.CompareOrdinal);
                stationKeys.Sort(string.CompareOrdinal);
                string layout = string.Join("|", edgeKeys) + "#" + string.Join("|", stationKeys);
                if (!levelLayouts.Add(layout))
                {
                    Debug.LogError("Level " + i + " duplicates another level layout.");
                    errors++;
                }
            }

            LevelEditorSession editor = new LevelEditorSession();
            ILevelValidator validator = new CustomLevelValidator();
            if (!validator.Validate(editor.Document, out List<string> editorErrors))
            {
                Debug.LogError("Starter custom level failed validation: " + string.Join(", ", editorErrors));
                errors++;
            }

            string json = JsonUtility.ToJson(editor.Document, true);
            CustomLevelDocument roundTrip = JsonUtility.FromJson<CustomLevelDocument>(json);
            if (roundTrip == null ||
                roundTrip.edges.Count != editor.Document.edges.Count ||
                roundTrip.stations.Count != editor.Document.stations.Count ||
                roundTrip.trains.Count != editor.Document.trains.Count)
            {
                Debug.LogError("Custom level JSON round-trip failed.");
                errors++;
            }

            LevelEditorSession dragEditor = new LevelEditorSession();
            Vector2Int dragStart = new Vector2Int(-4, 2);
            Vector2Int dragEnd = new Vector2Int(1, 3);
            dragEditor.Apply(dragStart);
            List<Vector2Int> preview = dragEditor.GetPreviewPath(dragEnd);
            if (preview.Count == 0 ||
                preview[0] != dragStart ||
                preview[preview.Count - 1] != dragEnd ||
                dragEditor.Anchor != dragStart)
            {
                Debug.LogError("Editor drag preview does not follow an orthogonal path.");
                errors++;
            }

            int beforeEdges = dragEditor.Document.edges.Count;
            dragEditor.Apply(dragEnd);
            if (dragEditor.Anchor.HasValue ||
                dragEditor.Document.edges.Count <= beforeEdges ||
                dragEditor.Document.trains.Exists(train => train.startStopped))
            {
                Debug.LogError("Editor drag commit did not finish cleanly or trains still start stopped.");
                errors++;
            }

            LevelEditorSession bridgeEditor = new LevelEditorSession();
            bridgeEditor.Document.edges.Clear();
            bridgeEditor.Document.stations.Clear();
            bridgeEditor.Document.trains.Clear();
            bridgeEditor.SetTool(EditorTool.Bridge);
            bridgeEditor.Apply(new Vector2Int(-4, 0));
            bridgeEditor.Apply(new Vector2Int(2, 0));
            int rampCount = 0;
            int bridgeCount = 0;
            for (int i = 0; i < bridgeEditor.Document.edges.Count; i++)
            {
                if (bridgeEditor.Document.edges[i].kind == TrackKind.Ramp)
                {
                    rampCount++;
                }
                else if (bridgeEditor.Document.edges[i].kind == TrackKind.Bridge)
                {
                    bridgeCount++;
                }
            }

            bridgeEditor.Document.stations.Add(new CustomStationDefinition
            {
                x = -4,
                z = 0,
                color = RailColor.Crimson
            });
            bridgeEditor.Document.stations.Add(new CustomStationDefinition
            {
                x = 2,
                z = 0,
                color = RailColor.Azure
            });
            bridgeEditor.Document.trains.Add(new CustomTrainSpawn
            {
                fromX = -4,
                fromZ = 0,
                toX = -3,
                toZ = 0,
                startStopped = false
            });
            List<string> bridgeErrors;
            bool bridgeValid = validator.Validate(bridgeEditor.Document, out bridgeErrors);
            if (rampCount != 2 || bridgeCount < 1 || !bridgeValid)
            {
                Debug.LogError("Editor bridge ramp generation failed: " + string.Join(", ", bridgeErrors));
                errors++;
            }

            string bridgeBeforeInvalidEdit = JsonUtility.ToJson(bridgeEditor.Document);
            bridgeEditor.SetTool(EditorTool.Track);
            bridgeEditor.Apply(new Vector2Int(-1, 0));
            bridgeEditor.Apply(new Vector2Int(-1, 1));
            if (JsonUtility.ToJson(bridgeEditor.Document) != bridgeBeforeInvalidEdit)
            {
                Debug.LogError("Editor allowed ground track to join an elevated node without a ramp.");
                errors++;
            }

            LevelEditorSession trainEditor = new LevelEditorSession();
            int trainsBeforePlacement = trainEditor.Document.trains.Count;
            if (!trainEditor.TryPlaceTrain(new Vector2(-1.15f, 0.08f)) ||
                trainEditor.Document.trains.Count != trainsBeforePlacement + 1 ||
                trainEditor.Document.trains[trainEditor.Document.trains.Count - 1].startStopped)
            {
                Debug.LogError("Editor could not place and start a train by clicking the middle of a rail segment.");
                errors++;
            }

            while (trainEditor.Document.trains.Count < 4)
            {
                trainEditor.TryPlaceTrain(new Vector2(-1.15f, 0.08f));
            }

            if (trainEditor.Document.trains.Count != 4 ||
                trainEditor.TryPlaceTrain(new Vector2(-1.15f, 0.08f)))
            {
                Debug.LogError("Editor train placement limit is not exactly four trains.");
                errors++;
            }

            TrainCargo cargo = new TrainCargo(4);
            cargo.Load(RailColor.Crimson, 2);
            cargo.Load(RailColor.Azure, 2);
            if (cargo.Passengers != 4 ||
                cargo.CountOf(RailColor.Crimson) != 2 ||
                cargo.CountOf(RailColor.Azure) != 2 ||
                cargo.Load(RailColor.Amber, 1) != 0 ||
                cargo.UnloadMatching(RailColor.Crimson) != 2 ||
                cargo.Passengers != 2)
            {
                Debug.LogError("Train cargo capacity or mixed-color handling failed.");
                errors++;
            }

            if (!File.Exists(MainScenePath))
            {
                Debug.LogError("Main scene is missing.");
                errors++;
            }

            if (errors == 0)
            {
                Debug.Log(
                    "Train Train / 混乱火车 project validation passed: 8 regions, 48 unique maps, " +
                    "average " + (totalEdges / (float)LevelCatalog.Levels.Count).ToString("F1") +
                    " track edges, max " + maximumEdges + ", " + corridorLevels +
                    " single-backbone maps, © 2026 X4CE.");
            }
            else
            {
                Debug.LogError("Train Train project validation failed with " + errors + " issue(s).");
            }
        }

        private static void EnsureMainScene()
        {
            if (File.Exists(MainScenePath))
            {
                return;
            }

            Directory.CreateDirectory("Assets/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, MainScenePath);
            AssetDatabase.Refresh();
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "Train Train · 混乱火车";
            PlayerSettings.companyName = "X4CE";
            PlayerSettings.applicationIdentifier = "com.x4ce.traintrain";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }

        private static void EnsureRuntimeMaterials()
        {
            Directory.CreateDirectory("Assets/Resources");
            EnsureMaterialAsset("TrainTrainLit", "Standard");
            EnsureMaterialAsset("TrainTrainUnlit", "Unlit/Color");
            EnsureMaterialAsset("TrainTrainParticle", "Particles/Standard Unlit");
            AssetDatabase.Refresh();
        }

        private static void EnsureMaterialAsset(string name, string shaderName)
        {
            string path = "Assets/Resources/" + name + ".mat";
            if (File.Exists(path))
            {
                return;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("Could not create runtime material because shader was not found: " + shaderName);
                return;
            }

            Material material = new Material(shader)
            {
                name = name,
                color = Color.white
            };
            AssetDatabase.CreateAsset(material, path);
        }

        private static void EnsureSceneInBuild()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == MainScenePath)
                {
                    scenes[i].enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Insert(0, new EditorBuildSettingsScene(MainScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void AddNeighbor(Dictionary<Vector2Int, List<Vector2Int>> graph, Vector2Int from, Vector2Int to)
        {
            if (!graph.TryGetValue(from, out List<Vector2Int> list))
            {
                list = new List<Vector2Int>();
                graph[from] = list;
            }

            list.Add(to);
        }

        private static void AddIncidentKind(
            Dictionary<Vector2Int, List<TrackKind>> incident,
            Vector2Int node,
            TrackKind kind)
        {
            if (!incident.TryGetValue(node, out List<TrackKind> kinds))
            {
                kinds = new List<TrackKind>();
                incident[node] = kinds;
            }

            kinds.Add(kind);
        }

        private static bool IsConnected(Dictionary<Vector2Int, List<Vector2Int>> graph, HashSet<Vector2Int> nodes)
        {
            if (nodes.Count == 0)
            {
                return false;
            }

            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            foreach (Vector2Int start in nodes)
            {
                queue.Enqueue(start);
                visited.Add(start);
                break;
            }

            while (queue.Count > 0)
            {
                Vector2Int node = queue.Dequeue();
                List<Vector2Int> neighbors = graph[node];
                for (int i = 0; i < neighbors.Count; i++)
                {
                    if (visited.Add(neighbors[i]))
                    {
                        queue.Enqueue(neighbors[i]);
                    }
                }
            }

            return visited.Count == nodes.Count;
        }

        private static string EdgeKey(RailEdge edge)
        {
            string first = edge.A.x + "," + edge.A.y;
            string second = edge.B.x + "," + edge.B.y;
            return string.CompareOrdinal(first, second) < 0 ? first + ">" + second : second + ">" + first;
        }
    }
}
