using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrainTrain
{
    public enum GameScreen
    {
        Title,
        LevelSelect,
        Playing,
        Paused,
        Editor,
        Result
    }

    public enum GameMode
    {
        Campaign,
        Endless
    }

    internal sealed class StationRuntime
    {
        public readonly StationDefinition Definition;
        public readonly int[] Waiting;
        public float SpawnTimer;
        public int Capacity;
        public float SpawnInterval;
        public int GenerationMask;

        public StationRuntime(StationDefinition definition)
        {
            Definition = definition;
            Waiting = new int[4];
            SpawnTimer = Mathf.Max(0.2f, definition.SpawnInterval);
            Capacity = definition.Capacity;
            SpawnInterval = Mathf.Max(0.2f, definition.SpawnInterval);
            GenerationMask = definition.GenerationMask;
        }

        public int Total
        {
            get
            {
                return Waiting[0] + Waiting[1] + Waiting[2] + Waiting[3];
            }
        }
    }

    public sealed class TrainTrainApp : MonoBehaviour
    {
        public const int InteractableLayer = 8;
        public const float GlobalTrainSpeedMultiplier = 1.6f;

        public static TrainTrainApp Instance { get; private set; }

        public GameScreen State { get; private set; }
        public GameMode CurrentMode { get; private set; }
        public WorldView World { get; private set; }
        public LevelDefinition CurrentLevel { get; private set; }
        public TrainTrainUI UI { get; private set; }
        public SynthAudio Audio { get; private set; }
        public LevelEditorSession Editor { get; private set; }
        public bool IsCustomSession { get; private set; }
        public IReadOnlyList<TrainActor> Trains
        {
            get { return trains; }
        }
        public int MovingTrains
        {
            get
            {
                int count = 0;
                for (int i = 0; i < trains.Count; i++)
                {
                    if (trains[i] != null && trains[i].IsMoving)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
        public int FleetSize
        {
            get { return fleetSize; }
        }
        public int[] Delivered
        {
            get { return delivered; }
        }
        public int Combo { get; private set; }
        public int Score { get; private set; }
        public int DeliveredTotal { get; private set; }
        public int TotalPassengers
        {
            get { return totalRequired; }
        }
        public float TimeRemaining { get; private set; }
        public float CurrentTrainSpeed
        {
            get
            {
                return CurrentLevel == null
                    ? 2f * GlobalTrainSpeedMultiplier
                    : CurrentLevel.TrainSpeed
                        * TrainStyleCatalog.Get(PlayerProgress.SelectedTrainStyle).SpeedMultiplier
                        * GlobalTrainSpeedMultiplier;
            }
        }
        public TrainStyleInfo SelectedTrainInfo
        {
            get { return TrainStyleCatalog.Get(PlayerProgress.SelectedTrainStyle); }
        }
        public bool ResultWon { get; private set; }
        public int ResultStars { get; private set; }
        public bool ResultNewBest { get; private set; }
        public int ResultBestScore { get; private set; }
        public string ResultReason { get; private set; }
        public int SelectedLevelIndex { get; private set; }
        public int SelectedRegionIndex { get; private set; }
        public TrainActor HoveredTrain { get; private set; }
        public TrackSwitchView HoveredSwitch { get; private set; }
        public TrackSwitchView KeyboardFocusedSwitch { get; private set; }
        public string ToastMessage { get; private set; }
        public float ToastAlpha
        {
            get { return Time.unscaledTime < toastUntil ? Mathf.Clamp01((toastUntil - Time.unscaledTime) / 0.35f) : 0f; }
        }

        private readonly List<TrainActor> trains = new List<TrainActor>();
        private readonly int[] delivered = new int[4];
        private Dictionary<Vector2Int, List<Vector2Int>> graph;
        private Dictionary<Vector2Int, StationDefinition> stationsByNode;
        private Dictionary<Vector2Int, StationRuntime> stationQueues;
        private System.Random passengerRandom;
        private float toastUntil;
        private float shakeUntil;
        private float zoomMultiplier = 1f;
        private float builtAspect = 1.6f;
        private bool resultRecorded;
        private int totalRequired;
        private int fleetSize;
        private int keyboardSwitchCursor = -1;
        private bool devMode;
        private bool devAutoPlay;
        private bool devSingleTrain;
        private bool devEditorDemoTrain;
        private bool devCaptured;
        private int devStartLevel = -1;
        private string devScreen;
        private string devForceResult;
        private float devCaptureAt = -1f;
        private float devQuitAt = -1f;
        private float devNextActionAt;
        private float devNextLogAt;
        private float devForcedAt;
        private string devCapturePath;
        private CustomLevelDocument activeCustomLevel;
        private readonly CustomLevelStore customLevelStore = new CustomLevelStore();
        private readonly CustomLevelValidator customLevelValidator = new CustomLevelValidator();
        private LineRenderer editorPreviewLine;
        private Transform editorPreviewStart;
        private Transform editorPreviewEnd;
        private Vector2Int? editorDragStart;
        private bool editorIgnoreUntilMouseUp;
        private float editorNextTrainPlacementAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<TrainTrainApp>() != null)
            {
                return;
            }

            GameObject root = new GameObject("Train Train - 混乱火车");
            root.AddComponent<TrainTrainApp>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Physics.IgnoreLayerCollision(InteractableLayer, InteractableLayer, false);

            Audio = gameObject.AddComponent<SynthAudio>();
            UI = gameObject.AddComponent<TrainTrainUI>();
            UI.Initialize(this);
            Editor = new LevelEditorSession();

            SelectedLevelIndex = PlayerProgress.HighestUnlocked;
            SelectedRegionIndex = LevelCatalog.GetLevel(SelectedLevelIndex).RegionIndex;
            ShowTitlePreview();
            ParseDeveloperArguments();
            if (devStartLevel >= 0)
            {
                StartLevel(devStartLevel);
            }
            else if (devScreen == "levels")
            {
                ShowLevelSelect();
            }
            else if (devScreen == "modes")
            {
                UI.OpenModeOverlay();
            }
            else if (devScreen == "garage")
            {
                UI.OpenDepotOverlay();
            }
            else if (devScreen == "editor")
            {
                ShowEditor();
                if (devEditorDemoTrain && Editor.Document.trains.Count == 0)
                {
                    Editor.TryPlaceTrain(Vector2.zero);
                    RefreshEditorWorld();
                }
            }
            else if (devScreen == "custom")
            {
                Editor = new LevelEditorSession();
                StartCustomLevel(Editor.Document.Clone());
            }
        }

        private void Update()
        {
            HandleKeyboard();
            UpdateDeveloperHarness();

            if (State != GameScreen.Playing)
            {
                if (State == GameScreen.Editor)
                {
                    UpdateCamera();
                    UpdateEditorPointer();
                    UpdateSwitches();
                }

                UpdateCamera();
                return;
            }

            UpdateCamera();
            UpdatePointer();
            UpdateHover();
            UpdateSwitches();
            UpdateRun();
        }

        private void LateUpdate()
        {
            if (World == null || World.Camera == null)
            {
                return;
            }

            Vector3 shake = Vector3.zero;
            if (Time.unscaledTime < shakeUntil)
            {
                float strength = Mathf.Clamp01((shakeUntil - Time.unscaledTime) / 0.3f) * 0.22f;
                shake = new Vector3(
                    Mathf.Sin(Time.unscaledTime * 77f) * strength,
                    Mathf.Cos(Time.unscaledTime * 63f) * strength,
                    0f);
            }

            World.Camera.transform.position = World.CameraHome + shake;
        }

        public void StartLevel(int index)
        {
            StartLevelInternal(index, GameMode.Campaign);
        }

        public void ShowEditor()
        {
            if (Editor == null)
            {
                Editor = new LevelEditorSession();
            }

            Editor.ClearAnchor("点击网格设置起点");
            editorDragStart = null;
            editorIgnoreUntilMouseUp = Input.GetMouseButton(0);
            IsCustomSession = false;
            SetState(GameScreen.Editor);
            RefreshEditorWorld();
        }

        public void RefreshEditorWorld()
        {
            CurrentLevel = Editor.ToLevelDefinition();
            if (World != null)
            {
                WorldBuilder.Destroy(World);
            }

            World = WorldBuilder.Build(CurrentLevel, CurrentLevel.Index + 17, true);
            BuildEditorTrainPreviews();
            CreateEditorPreview();
            builtAspect = Mathf.Max(0.5f, Screen.width / (float)Mathf.Max(1, Screen.height));
            zoomMultiplier = 1f;
        }

        private void BuildEditorTrainPreviews()
        {
            if (World == null || Editor == null)
            {
                return;
            }

            int count = Mathf.Min(4, Editor.Document.trains.Count);
            if (devMode)
            {
                Debug.Log("Editor train previews rendered=" + count);
            }

            for (int i = 0; i < count; i++)
            {
                CustomTrainSpawn spawn = Editor.Document.trains[i];
                TrainActor preview = TrainActor.Create(
                    this,
                    (RailColor)i,
                    new Vector2Int(spawn.fromX, spawn.fromZ),
                    new Vector2Int(spawn.toX, spawn.toZ),
                    World.TrainsRoot,
                    spawn.style);
                preview.gameObject.name = "EditorTrain_" + (i + 1);
                preview.SetMoving(true);
                LowPolyAssets.Cylinder(
                    "EditorTrainMarker_" + (i + 1),
                    preview.transform,
                    new Vector3(0f, 0.035f, 0f),
                    0.7f,
                    0.035f,
                    RailPalette.Amber,
                    16,
                    null,
                    false,
                    0f,
                    0.4f);
            }
        }

        public void SaveEditorLevel()
        {
            if (customLevelValidator.Validate(Editor.Document, out List<string> errors))
            {
                customLevelStore.Save(Editor.Document);
                ShowToast("关卡已保存");
            }
            else
            {
                ShowToast("无法保存：" + errors[0]);
            }
        }

        public void ExportEditorJson()
        {
            string folder = System.IO.Path.Combine(Application.persistentDataPath, "custom_exports");
            System.IO.Directory.CreateDirectory(folder);
            string path = System.IO.Path.Combine(folder, "custom_level_export.json");
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(Editor.Document, true));
            ShowToast("JSON 已导出到 " + path);
        }

        public void ImportEditorJson()
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, "custom_exports", "custom_level_export.json");
            if (!System.IO.File.Exists(path))
            {
                ShowToast("没有找到可导入的 JSON");
                return;
            }

            CustomLevelDocument document = JsonUtility.FromJson<CustomLevelDocument>(System.IO.File.ReadAllText(path));
            if (customLevelValidator.Validate(document, out List<string> errors))
            {
                Editor = new LevelEditorSession();
                Editor.Import(document);
                RefreshEditorWorld();
                ShowToast("JSON 已导入");
            }
            else
            {
                ShowToast("JSON 无效：" + errors[0]);
            }
        }

        public void LoadLastSavedEditorLevel()
        {
            List<CustomLevelDocument> levels = customLevelStore.LoadAll();
            if (levels.Count == 0)
            {
                ShowToast("还没有保存过自定义关卡");
                return;
            }

            Editor.Import(levels[levels.Count - 1]);
            RefreshEditorWorld();
            ShowToast("已载入 " + levels[levels.Count - 1].name);
        }

        public void TestEditorLevel()
        {
            if (Editor.Document.trains.Count == 0)
            {
                ShowToast("请先在轨道上放置至少一列列车");
                return;
            }

            if (!customLevelValidator.Validate(Editor.Document, out List<string> errors))
            {
                ShowToast("无法测试：" + errors[0]);
                return;
            }

            StartCustomLevel(Editor.Document.Clone());
        }

        public void StartEndless()
        {
            StartLevelInternal(PlayerProgress.HighestUnlocked, GameMode.Endless);
        }

        private void StartLevelInternal(int index, GameMode mode)
        {
            if (index < 0 || index >= LevelCatalog.Levels.Count ||
                (!PlayerProgress.IsUnlocked(index) && !devMode))
            {
                ShowToast("先完成前一关，才能进入这里。");
                return;
            }

            CurrentMode = mode;
            IsCustomSession = false;
            activeCustomLevel = null;
            SelectedLevelIndex = index;
            SelectedRegionIndex = LevelCatalog.GetLevel(index).RegionIndex;
            CurrentLevel = LevelCatalog.GetLevel(index);
            RebuildWorld(false);
            BuildRunData(CurrentLevel);
            SetState(GameScreen.Playing);
            Audio.Play(AudioCue.Spawn);
        }

        public void RetryLevel()
        {
            if (IsCustomSession && activeCustomLevel != null)
            {
                StartCustomLevel(activeCustomLevel.Clone());
            }
            else
            {
                StartLevelInternal(SelectedLevelIndex, CurrentMode);
            }
        }

        public void NextLevel()
        {
            if (IsCustomSession)
            {
                ShowEditor();
                return;
            }

            if (SelectedLevelIndex + 1 < LevelCatalog.Levels.Count)
            {
                StartLevel(SelectedLevelIndex + 1);
            }
            else
            {
                ShowLevelSelect();
            }
        }

        public void ShowTitle()
        {
            IsCustomSession = false;
            SetState(GameScreen.Title);
            Audio.SetPlaying(false);
            ShowTitlePreview();
        }

        public void ShowLevelSelect()
        {
            IsCustomSession = false;
            SetState(GameScreen.LevelSelect);
            Audio.SetPlaying(false);
            SelectedRegionIndex = Mathf.Clamp(SelectedRegionIndex, 0, LevelCatalog.Regions.Count - 1);
            RebuildWorld(true, LevelCatalog.FirstLevelIndex(SelectedRegionIndex));
        }

        private void StartCustomLevel(CustomLevelDocument document)
        {
            activeCustomLevel = document;
            IsCustomSession = true;
            CurrentMode = GameMode.Campaign;
            CurrentLevel = Editor.ToLevelDefinition(document);
            SelectedLevelIndex = 0;
            SelectedRegionIndex = CurrentLevel.RegionIndex;
            if (World != null)
            {
                WorldBuilder.Destroy(World);
            }

            World = WorldBuilder.Build(CurrentLevel, CurrentLevel.Index + 23, false);
            builtAspect = Mathf.Max(0.5f, Screen.width / (float)Mathf.Max(1, Screen.height));
            zoomMultiplier = 1f;
            BuildRunData(CurrentLevel);
            SetState(GameScreen.Playing);
        }

        public void PreviewRegion(int regionIndex)
        {
            SelectedRegionIndex = Mathf.Clamp(regionIndex, 0, LevelCatalog.Regions.Count - 1);
            RebuildWorld(true, LevelCatalog.FirstLevelIndex(SelectedRegionIndex));
        }

        public void TogglePause()
        {
            if (State == GameScreen.Playing)
            {
                SetState(GameScreen.Paused);
            }
            else if (State == GameScreen.Paused)
            {
                SetState(GameScreen.Playing);
            }
        }

        public void ToggleMute()
        {
            Audio.ToggleMute();
            Audio.SetPlaying(State == GameScreen.Playing);
        }

        public void SelectTrainStyle(TrainStyle style)
        {
            if (!TrainStyleCatalog.IsUnlocked(style))
            {
                ShowToast("需要累计更多星星才能解锁这列火车");
                return;
            }

            PlayerProgress.SelectedTrainStyle = style;
            Audio.Play(AudioCue.Button);
        }

        public void TrainReachedNode(TrainActor train, Vector2Int node)
        {
            if (train == null || !trains.Contains(train) || State != GameScreen.Playing)
            {
                return;
            }

            Vector2Int incoming = train.CurrentNode;
            if (stationsByNode.TryGetValue(node, out StationDefinition station))
            {
                RailColor? cargoBefore = train.CargoColor;
                int passengersBefore = train.Passengers;
                int unloaded = train.UnloadMatching(station.Color);
                if (unloaded > 0)
                {
                    delivered[(int)station.Color] += unloaded;
                    DeliveredTotal += unloaded;
                    Combo++;
                    int gained = unloaded * 100 + Mathf.Min(Combo, 12) * 20;
                    Score += gained;
                    Audio.Play(AudioCue.Deliver);
                    ShowToast(RailPalette.NameCn(station.Color) + "乘客下车 +" + unloaded + "  ·  连击 " + Combo);
                    if (DeliveredTotal >= totalRequired)
                    {
                        if (CurrentMode == GameMode.Endless)
                        {
                            totalRequired += 30;
                            TimeRemaining = Mathf.Min(180f, TimeRemaining + 20f);
                            ShowToast("无尽阶段完成 · 目标提升至 " + totalRequired + " · 时间 +20 秒");
                        }
                        else
                        {
                            Win();
                        }

                        return;
                    }
                }

                LoadPassengersFromStation(train, station);
                if (devMode)
                {
                    Debug.Log(
                        "Station stop " + station.Position +
                        " stationColor=" + station.Color +
                        " cargoBefore=" + (cargoBefore.HasValue ? cargoBefore.Value.ToString() : "empty") +
                        " carried=" + passengersBefore +
                        " unloaded=" + unloaded +
                        " cargoAfter=" + (train.CargoColor.HasValue ? train.CargoColor.Value.ToString() : "empty") +
                        " carriedAfter=" + train.Passengers);
                }

                if (TryChooseRegularNext(node, incoming, out Vector2Int stationNext))
                {
                    train.SetSegment(node, stationNext);
                    train.BeginStationStop(1.6f);
                }

                return;
            }

            Vector2Int next;
            bool found;
            if (World.Switches.TryGetValue(node, out TrackSwitchView railSwitch))
            {
                found = railSwitch.TryGetNext(incoming, out next);
            }
            else
            {
                found = TryChooseRegularNext(node, incoming, out next);
            }

            if (!found)
            {
                TrainLost(train);
                return;
            }

            train.SetSegment(node, next);
        }

        public void TrainLost(TrainActor train)
        {
            if (train == null || !trains.Contains(train) || State != GameScreen.Playing)
            {
                return;
            }

            if (devMode)
            {
                Debug.LogWarning(
                    "TrainTrain route break: color=" + train.Color +
                    " node=" + train.NextNode +
                    " graphHasNode=" + (graph != null && graph.ContainsKey(train.NextNode)));
            }

            RemoveTrain(train);
            ShowToast(RailPalette.NameCn(train.Color) + "列车偏离线路，已重新派出");
        }

        public void ShowToast(string message)
        {
            ToastMessage = message;
            toastUntil = Time.unscaledTime + 2.4f;
        }

        public bool IsObjectiveComplete(RailColor color)
        {
            return CurrentLevel != null && delivered[(int)color] >= CurrentLevel.Quotas[(int)color];
        }

        public int WaitingPassengers(Vector2Int stationNode)
        {
            return stationQueues != null &&
                   stationQueues.TryGetValue(stationNode, out StationRuntime queue)
                ? queue.Total
                : 0;
        }

        public bool WorldPointerBlocked(Vector2 screenPoint)
        {
            float scale = TrainTrainUI.ScaleForScreen();
            float top = 108f * scale;
            float bottom = 142f * scale;
            return screenPoint.y > Screen.height - top || screenPoint.y < bottom;
        }

        public bool EditorPointerBlocked(Vector2 screenPoint)
        {
            float scale = TrainTrainUI.ScaleForScreen();
            return screenPoint.x < 250f * scale ||
                   screenPoint.x > Screen.width - 230f * scale ||
                   screenPoint.y > Screen.height - 84f * scale;
        }

        private void BuildRunData(LevelDefinition level)
        {
            graph = WorldBuilder.BuildAdjacency(level.Map.Edges);
            stationsByNode = new Dictionary<Vector2Int, StationDefinition>();
            stationQueues = new Dictionary<Vector2Int, StationRuntime>();
            passengerRandom = new System.Random(level.Index * 7919 + 37);
            int availableStationColorMask = 0;
            for (int i = 0; i < level.Map.Stations.Count; i++)
            {
                availableStationColorMask |= 1 << (int)level.Map.Stations[i].Color;
            }

            for (int i = 0; i < level.Map.Stations.Count; i++)
            {
                StationDefinition station = level.Map.Stations[i];
                stationsByNode[station.Position] = station;
                StationRuntime queue = new StationRuntime(station);
                queue.Capacity = Mathf.Max(station.Capacity, level.StationCapacity);
                queue.SpawnInterval = Mathf.Max(station.SpawnInterval, level.StationSpawnInterval);
                queue.GenerationMask = availableStationColorMask != 0
                    ? availableStationColorMask
                    : station.GenerationMask;
                int initial = station.InitialPassengers;
                for (int passenger = 0; passenger < initial; passenger++)
                {
                    int color = NextSupportedColor(queue.GenerationMask, passengerRandom);
                    queue.Waiting[color]++;
                }

                stationQueues[station.Position] = queue;
            }

            trains.Clear();
            for (int i = 0; i < delivered.Length; i++)
            {
                delivered[i] = 0;
            }

            Combo = 0;
            Score = 0;
            DeliveredTotal = 0;
            totalRequired = level.TotalPassengers;
            fleetSize = IsCustomSession && activeCustomLevel != null
                ? Mathf.Clamp(activeCustomLevel.trains.Count, 1, 4)
                : Mathf.Clamp(level.FleetSize, 1, 4);
            if (CurrentMode == GameMode.Endless)
            {
                TimeRemaining = 120f;
                totalRequired = 30;
            }
            else
            {
                TimeRemaining = level.Duration;
            }
            zoomMultiplier = 1f;
            resultRecorded = false;
            ResultWon = false;
            ResultReason = string.Empty;
            builtAspect = Mathf.Max(0.5f, Screen.width / (float)Mathf.Max(1, Screen.height));
            HoveredTrain = null;
            HoveredSwitch = null;
            KeyboardFocusedSwitch = null;
            keyboardSwitchCursor = -1;
            for (int color = 0; color < fleetSize; color++)
            {
                if (IsCustomSession && activeCustomLevel != null)
                {
                    CustomTrainSpawn spawn = activeCustomLevel.trains[color];
                    TrainActor customTrain = TrainActor.Create(
                        this,
                        (RailColor)(color % 4),
                        new Vector2Int(spawn.fromX, spawn.fromZ),
                        new Vector2Int(spawn.toX, spawn.toZ),
                        World.TrainsRoot,
                        spawn.style);
                    customTrain.SetMoving(!spawn.startStopped);
                    if (spawn.startDelay > 0f && spawn.startStopped)
                    {
                        customTrain.AutoStartIn(spawn.startDelay);
                    }

                    trains.Add(customTrain);
                }
                else
                {
                    TrainActor initialTrain = SpawnTrainForColor(
                        (RailColor)color,
                        color == 0);
                    if (initialTrain != null && color > 0)
                    {
                        initialTrain.AutoStartIn(color * 15f);
                    }
                }
            }

            ShowToast("列车将自动错峰发车；点击车身可停车  ·  " + level.Hint);
        }

        private void UpdateRun()
        {
            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                if (CurrentMode == GameMode.Endless)
                {
                    FinishEndless();
                }
                else
                {
                    Lose("倒计时结束，调度表还没有清空");
                }
                return;
            }

            UpdateStationSpawns(Time.deltaTime);

            CheckCollisions();

            if (State != GameScreen.Playing)
            {
                return;
            }

            if (DeliveredTotal >= totalRequired)
            {
                if (CurrentMode == GameMode.Endless)
                {
                    totalRequired += 30;
                    TimeRemaining = Mathf.Min(180f, TimeRemaining + 20f);
                }
                else
                {
                    Win();
                }
            }
            else
            {
                MaintainWaitingTrains();
            }
        }

        private void MaintainWaitingTrains()
        {
            for (int color = 0; color < fleetSize; color++)
            {
                if (DeliveredTotal < totalRequired && !HasTrainForColor((RailColor)color))
                {
                    SpawnTrainForColor((RailColor)color);
                }
            }
        }

        private void UpdateStationSpawns(float deltaTime)
        {
            if (stationQueues == null)
            {
                return;
            }

            foreach (StationRuntime station in stationQueues.Values)
            {
                station.SpawnTimer -= deltaTime;
                if (station.SpawnTimer > 0f)
                {
                    continue;
                }

                station.SpawnTimer = Mathf.Max(0.2f, station.SpawnInterval);
                if (station.Total < station.Capacity)
                {
                    int color = NextSupportedColor(station.GenerationMask, passengerRandom);
                    station.Waiting[color]++;
                }
            }
        }

        private static int NextSupportedColor(int mask, System.Random random)
        {
            int count = 0;
            for (int i = 0; i < 4; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return random.Next(0, 4);
            }

            int selected = random.Next(0, count);
            for (int i = 0; i < 4; i++)
            {
                if ((mask & (1 << i)) == 0)
                {
                    continue;
                }

                if (selected-- == 0)
                {
                    return i;
                }
            }

            return 0;
        }

        private bool HasTrainForColor(RailColor color)
        {
            for (int i = 0; i < trains.Count; i++)
            {
                if (trains[i] != null && trains[i].Color == color)
                {
                    return true;
                }
            }

            return false;
        }

        private TrainActor SpawnTrainForColor(RailColor color, bool autoStart = false)
        {
            StationDefinition source = default;
            bool found = false;
            for (int i = 0; i < CurrentLevel.Map.Stations.Count; i++)
            {
                StationDefinition candidate = CurrentLevel.Map.Stations[i];
                if (candidate.IsSource && candidate.Color == color)
                {
                    source = candidate;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Debug.LogError("No source station for " + color + " on " + CurrentLevel.Map.Name);
                return null;
            }

            TrainActor train = TrainActor.Create(
                this,
                color,
                source.Position,
                source.Link,
                World.TrainsRoot);
            trains.Add(train);
            Audio.Play(AudioCue.Spawn);
            train.SetMoving(autoStart);
            return train;
        }

        private bool TryChooseRegularNext(Vector2Int node, Vector2Int incoming, out Vector2Int next)
        {
            next = default;
            if (!graph.TryGetValue(node, out List<Vector2Int> neighbors))
            {
                return false;
            }

            List<Vector2Int> choices = new List<Vector2Int>(neighbors.Count);
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (neighbors[i] != incoming)
                {
                    choices.Add(neighbors[i]);
                }
            }

            if (choices.Count == 0)
            {
                if (neighbors.Contains(incoming))
                {
                    next = incoming;
                    return true;
                }

                return false;
            }

            Vector2Int forward = node - incoming;
            Vector2Int selected = choices[0];
            int bestDot = int.MinValue;
            for (int i = 0; i < choices.Count; i++)
            {
                Vector2Int candidate = choices[i] - node;
                int dot = forward.x * candidate.x + forward.y * candidate.y;
                if (dot > bestDot)
                {
                    bestDot = dot;
                    selected = choices[i];
                }
            }

            next = selected;
            return true;
        }

        private void LoadPassengersFromStation(TrainActor train, StationDefinition station)
        {
            if (stationQueues == null ||
                !stationQueues.TryGetValue(station.Position, out StationRuntime queue) ||
                queue.Total == 0)
            {
                return;
            }

            RailColor cargoType = RailColor.Crimson;
            int best = -1;
            for (int i = 0; i < queue.Waiting.Length; i++)
            {
                if (queue.Waiting[i] > best)
                {
                    best = queue.Waiting[i];
                    cargoType = (RailColor)i;
                }
            }

            int loaded = train.LoadPassengers(cargoType, queue.Waiting[(int)cargoType]);
            if (loaded > 0)
            {
                queue.Waiting[(int)cargoType] -= loaded;
                if (train.Passengers == train.Capacity)
                {
                    Audio.Play(AudioCue.Spawn);
                }
            }
        }

        private void CheckCollisions()
        {
            for (int i = 0; i < trains.Count; i++)
            {
                TrainActor a = trains[i];
                if (a == null)
                {
                    continue;
                }

                for (int j = i + 1; j < trains.Count; j++)
                {
                    TrainActor b = trains[j];
                    if (b == null)
                    {
                        continue;
                    }

                    Vector3 delta = a.transform.position - b.transform.position;
                    bool sameLayer = Mathf.Abs(delta.y) < 0.38f;
                    delta.y = 0f;
                    if (sameLayer &&
                        !a.HasDepartureCollisionGrace &&
                        !b.HasDepartureCollisionGrace &&
                        (a.IsMoving || b.IsMoving) &&
                        delta.sqrMagnitude < 0.46f)
                    {
                        Vector3 explosionPosition = (a.transform.position + b.transform.position) * 0.5f;
                        RemoveTrain(a);
                        RemoveTrain(b);
                        Audio.Play(AudioCue.Crash);
                        shakeUntil = Time.unscaledTime + 0.32f;
                        CreateExplosion(explosionPosition);
                        Lose("两列列车发生碰撞");
                        return;
                    }
                }
            }
        }

        private void CreateExplosion(Vector3 position)
        {
            GameObject effect = new GameObject("TrainExplosion");
            effect.transform.SetParent(World.Root, false);
            effect.transform.position = position + Vector3.up * 0.35f;
            ParticleSystem particles = effect.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.duration = 0.45f;
            main.loop = false;
            main.startLifetime = 0.65f;
            main.startSpeed = 3.2f;
            main.startSize = 0.22f;
            main.startColor = new Color(1f, 0.58f, 0.18f, 0.9f);
            main.gravityModifier = 0.65f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            ParticleSystemRenderer renderer = effect.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = LowPolyAssets.ParticleMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            effect.AddComponent<Light>().range = 4f;
            effect.GetComponent<Light>().color = new Color(1f, 0.52f, 0.18f);
            effect.GetComponent<Light>().intensity = 2.5f;
            Destroy(effect, 1.2f);
        }

        private void RemoveTrain(TrainActor train)
        {
            if (train == null)
            {
                return;
            }

            trains.Remove(train);
            if (HoveredTrain == train)
            {
                HoveredTrain = null;
            }

            train.gameObject.SetActive(false);
            Destroy(train.gameObject);
        }

        private void Win()
        {
            if (State != GameScreen.Playing)
            {
                return;
            }

            ResultWon = true;
            int timeBonus = Mathf.RoundToInt(TimeRemaining) * 5;
            Score += timeBonus;
            ResultStars = TimeRemaining > 20f ? 3 : TimeRemaining > 10f ? 2 : 1;
            ResultBestScore = Mathf.Max(PlayerProgress.GetBestScore(SelectedLevelIndex), Score);
            ResultNewBest = Score > PlayerProgress.GetBestScore(SelectedLevelIndex);
            ResultReason = IsCustomSession
                ? "自定义关卡完成  ·  时间奖励 +" + timeBonus
                : "全部乘客送达  ·  时间奖励 +" + timeBonus;
            if (!resultRecorded && !devMode && !IsCustomSession)
            {
                PlayerProgress.RecordVictory(SelectedLevelIndex, ResultStars, Score);
                resultRecorded = true;
            }

            Audio.Play(AudioCue.Win);
            SetState(GameScreen.Result);
        }

        private void Lose(string reason)
        {
            if (State != GameScreen.Playing)
            {
                return;
            }

            ResultWon = false;
            ResultStars = 0;
            ResultReason = reason;
            ResultBestScore = PlayerProgress.GetBestScore(SelectedLevelIndex);
            Audio.Play(AudioCue.Lose);
            SetState(GameScreen.Result);
        }

        private void FinishEndless()
        {
            if (State != GameScreen.Playing)
            {
                return;
            }

            ResultWon = true;
            ResultStars = 0;
            ResultReason = "无尽挑战结束 · 共送达 " + DeliveredTotal + " 名乘客";
            ResultBestScore = Score;
            Audio.Play(AudioCue.Win);
            SetState(GameScreen.Result);
        }

        private void SetState(GameScreen next)
        {
            State = next;
            Audio.SetPlaying(next == GameScreen.Playing);
        }

        private void UpdateSwitches()
        {
            if (World == null)
            {
                return;
            }

            foreach (TrackSwitchView railSwitch in World.Switches.Values)
            {
                railSwitch.Hovered = railSwitch == HoveredSwitch;
                railSwitch.UpdateVisual(Time.unscaledTime);
            }
        }

        private void UpdateEditorPointer()
        {
            if (editorIgnoreUntilMouseUp)
            {
                if (!Input.GetMouseButton(0))
                {
                    editorIgnoreUntilMouseUp = false;
                }

                return;
            }

            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) > 0.001f)
            {
                zoomMultiplier = Mathf.Clamp(zoomMultiplier - wheel * 0.18f, 0.72f, 1.45f);
            }

            UpdateEditorPreview();
            bool trackTool = IsTrackTool(Editor.Tool);
            if (Input.GetMouseButtonUp(0) && trackTool && editorDragStart.HasValue &&
                Editor.Anchor.HasValue && !EditorPointerBlocked(Input.mousePosition))
            {
                Vector2Int released = PointerToGrid(Input.mousePosition);
                if (released != editorDragStart.Value)
                {
                    Editor.Apply(released);
                    RefreshEditorWorld();
                }

                editorDragStart = null;
                return;
            }

            if (Input.GetMouseButtonDown(1))
            {
                Editor.ClearAnchor();
                editorDragStart = null;
                return;
            }

            if (!Input.GetMouseButtonDown(0) || EditorPointerBlocked(Input.mousePosition))
            {
                return;
            }

            if (Editor.Tool == EditorTool.Train)
            {
                if (Time.unscaledTime >= editorNextTrainPlacementAt &&
                    TryPointerToGrid(Input.mousePosition, out Vector2 trainGridPosition) &&
                    Editor.TryPlaceTrain(trainGridPosition))
                {
                    editorNextTrainPlacementAt = Time.unscaledTime + 0.18f;
                    RefreshEditorWorld();
                }

                return;
            }

            Vector2Int point = PointerToGrid(Input.mousePosition);
            if (trackTool && Editor.Anchor.HasValue && point != Editor.Anchor.Value)
            {
                Editor.Apply(point);
                editorDragStart = null;
                RefreshEditorWorld();
                return;
            }

            Editor.Apply(point);
            editorDragStart = trackTool && Editor.Anchor.HasValue ? point : (Vector2Int?)null;
            RefreshEditorWorld();
        }

        private Vector2Int PointerToGrid(Vector2 screenPoint)
        {
            if (!TryPointerToGrid(screenPoint, out Vector2 grid))
            {
                return new Vector2Int(int.MinValue, int.MinValue);
            }

            return new Vector2Int(
                Mathf.RoundToInt(grid.x),
                Mathf.RoundToInt(grid.y));
        }

        private bool TryPointerToGrid(Vector2 screenPoint, out Vector2 grid)
        {
            Ray ray = World.Camera.ScreenPointToRay(screenPoint);
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float distance))
            {
                grid = default;
                return false;
            }

            Vector3 world = ray.GetPoint(distance);
            grid = new Vector2(world.x / WorldBuilder.GridSize, world.z / WorldBuilder.GridSize);
            return true;
        }

        private static bool IsTrackTool(EditorTool tool)
        {
            return tool == EditorTool.Track ||
                   tool == EditorTool.Bridge ||
                   tool == EditorTool.Tunnel ||
                   tool == EditorTool.SideTrack;
        }

        private void CreateEditorPreview()
        {
            if (World == null)
            {
                return;
            }

            editorPreviewLine = LowPolyAssets.Line(
                "EditorTrackPreview",
                World.Root,
                RailPalette.Amber,
                0.24f,
                0);
            editorPreviewLine.useWorldSpace = true;
            editorPreviewLine.gameObject.SetActive(false);

            GameObject startMarker = LowPolyAssets.Cylinder(
                "EditorTrackPreviewStart",
                World.Root,
                Vector3.zero,
                0.34f,
                0.08f,
                RailPalette.Paper,
                12);
            editorPreviewStart = startMarker.transform;
            editorPreviewStart.gameObject.SetActive(false);

            GameObject marker = LowPolyAssets.Cylinder(
                "EditorTrackPreviewEnd",
                World.Root,
                Vector3.zero,
                0.38f,
                0.08f,
                RailPalette.Amber,
                12);
            editorPreviewEnd = marker.transform;
            editorPreviewEnd.gameObject.SetActive(false);
        }

        private void UpdateEditorPreview()
        {
            if (World == null || editorPreviewLine == null || editorPreviewEnd == null)
            {
                return;
            }

            bool trackTool = IsTrackTool(Editor.Tool);
            if (!trackTool || !Editor.Anchor.HasValue || EditorPointerBlocked(Input.mousePosition))
            {
                editorPreviewLine.gameObject.SetActive(false);
                if (editorPreviewStart != null)
                {
                    editorPreviewStart.gameObject.SetActive(false);
                }
                editorPreviewEnd.gameObject.SetActive(false);
                return;
            }

            Ray ray = World.Camera.ScreenPointToRay(Input.mousePosition);
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float distance))
            {
                return;
            }

            Vector3 mouseWorld = ray.GetPoint(distance);
            Vector2Int snapped = new Vector2Int(
                Mathf.RoundToInt(mouseWorld.x / WorldBuilder.GridSize),
                Mathf.RoundToInt(mouseWorld.z / WorldBuilder.GridSize));
            Vector3 start = WorldBuilder.GridToWorld(Editor.Anchor.Value) + Vector3.up * 0.35f;
            Color color = Editor.Tool == EditorTool.Bridge
                ? RailPalette.Azure
                : Editor.Tool == EditorTool.Tunnel
                    ? RailPalette.Violet
                    : Editor.Tool == EditorTool.SideTrack
                        ? RailPalette.Amber
                        : RailPalette.Paper;
            editorPreviewLine.sharedMaterial = LowPolyAssets.Material(color, 0f, 0.7f);
            editorPreviewLine.startColor = color;
            editorPreviewLine.endColor = color;
            List<Vector2Int> path = Editor.GetPreviewPath(snapped);
            editorPreviewLine.positionCount = Mathf.Max(2, path.Count);
            editorPreviewLine.SetPosition(0, start);
            for (int i = 1; i < path.Count; i++)
            {
                editorPreviewLine.SetPosition(i, WorldBuilder.GridToWorld(path[i]) + Vector3.up * 0.35f);
            }

            editorPreviewLine.gameObject.SetActive(true);
            editorPreviewStart.position = WorldBuilder.GridToWorld(Editor.Anchor.Value) + Vector3.up * 0.08f;
            editorPreviewStart.gameObject.SetActive(true);
            editorPreviewEnd.position = WorldBuilder.GridToWorld(snapped) + Vector3.up * 0.08f;
            editorPreviewEnd.gameObject.SetActive(true);
        }

        private void UpdatePointer()
        {
            if (Input.GetAxis("Mouse ScrollWheel") != 0f && !WorldPointerBlocked(Input.mousePosition))
            {
                float wheel = Input.GetAxis("Mouse ScrollWheel");
                zoomMultiplier = Mathf.Clamp(zoomMultiplier - wheel * 0.16f, 0.78f, 1.35f);
            }

            if (!Input.GetMouseButtonDown(0) || WorldPointerBlocked(Input.mousePosition))
            {
                return;
            }

            Ray ray = World.Camera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f, 1 << InteractableLayer))
            {
                return;
            }

            TrainActor train = hit.collider.GetComponentInParent<TrainActor>();
            if (train != null)
            {
                train.ToggleMoving();
                Audio.Play(train.IsMoving ? AudioCue.Spawn : AudioCue.Brake);
                ShowToast(train.IsMoving ? "列车发车" : "列车停车");
                return;
            }

            SwitchAnchor anchor = hit.collider.GetComponentInParent<SwitchAnchor>();
            if (anchor != null && anchor.View != null)
            {
                CycleSwitch(anchor.View);
                Audio.Play(AudioCue.Switch);
            }
        }

        private void CycleSwitch(TrackSwitchView selected)
        {
            selected.Cycle();
            if (IsCustomSession && activeCustomLevel != null && activeCustomLevel.syncAllSwitches && World != null)
            {
                foreach (TrackSwitchView railSwitch in World.Switches.Values)
                {
                    if (railSwitch != selected)
                    {
                        railSwitch.Cycle();
                    }
                }
            }
        }

        private void UpdateHover()
        {
            HoveredTrain = null;
            HoveredSwitch = KeyboardFocusedSwitch;
            if (WorldPointerBlocked(Input.mousePosition))
            {
                return;
            }

            Ray ray = World.Camera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f, 1 << InteractableLayer))
            {
                return;
            }

            HoveredTrain = hit.collider.GetComponentInParent<TrainActor>();
            SwitchAnchor anchor = hit.collider.GetComponentInParent<SwitchAnchor>();
            if (anchor != null)
            {
                HoveredSwitch = anchor.View;
            }
        }

        private void UpdateCamera()
        {
            if (World == null || World.Camera == null)
            {
                return;
            }

            float aspect = Mathf.Max(0.5f, Screen.width / (float)Mathf.Max(1, Screen.height));
            float aspectScale = Mathf.Clamp(builtAspect / aspect, 0.86f, 1.42f);
            World.Camera.orthographicSize = World.BaseOrthographicSize * zoomMultiplier * aspectScale;
        }

        private void HandleKeyboard()
        {
            if (State == GameScreen.Editor)
            {
                bool command = Input.GetKey(KeyCode.LeftControl) ||
                               Input.GetKey(KeyCode.RightControl) ||
                               Input.GetKey(KeyCode.LeftCommand) ||
                               Input.GetKey(KeyCode.RightCommand);
                if (command && Input.GetKeyDown(KeyCode.Z))
                {
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    {
                        Editor.Redo();
                    }
                    else
                    {
                        Editor.Undo();
                    }

                    RefreshEditorWorld();
                    return;
                }

                if (command && Input.GetKeyDown(KeyCode.Y))
                {
                    Editor.Redo();
                    RefreshEditorWorld();
                    return;
                }

                if (command && Input.GetKeyDown(KeyCode.S))
                {
                    SaveEditorLevel();
                    return;
                }

                if (Input.GetKeyDown(KeyCode.F5))
                {
                    TestEditorLevel();
                    return;
                }
            }

            if (State == GameScreen.Playing || State == GameScreen.Paused)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
                {
                    TogglePause();
                }

                if (Input.GetKeyDown(KeyCode.R))
                {
                    RetryLevel();
                }
            }

            if (State == GameScreen.Title && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                StartLevel(PlayerProgress.HighestUnlocked);
            }
            else if (State == GameScreen.LevelSelect && Input.GetKeyDown(KeyCode.Escape))
            {
                ShowTitle();
            }
            else if (State == GameScreen.Editor && Input.GetKeyDown(KeyCode.Escape))
            {
                if (Editor.Anchor.HasValue)
                {
                    Editor.ClearAnchor();
                    editorDragStart = null;
                }
                else
                {
                    ShowTitle();
                }
            }
            else if (State == GameScreen.Result && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                if (ResultWon)
                {
                    NextLevel();
                }
                else
                {
                    RetryLevel();
                }
            }

            if (State == GameScreen.Playing)
            {
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    FocusNextSwitch();
                }

                if (Input.GetKeyDown(KeyCode.E) && KeyboardFocusedSwitch != null)
                {
                    CycleSwitch(KeyboardFocusedSwitch);
                    Audio.Play(AudioCue.Switch);
                }

                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    ToggleTrainByColor(RailColor.Crimson);
                }

                if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    ToggleTrainByColor(RailColor.Azure);
                }

                if (Input.GetKeyDown(KeyCode.Alpha3))
                {
                    ToggleTrainByColor(RailColor.Amber);
                }

                if (Input.GetKeyDown(KeyCode.Alpha4))
                {
                    ToggleTrainByColor(RailColor.Violet);
                }
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                ToggleMute();
            }
        }

        private void FocusNextSwitch()
        {
            if (World == null || World.Switches.Count == 0)
            {
                return;
            }

            keyboardSwitchCursor = (keyboardSwitchCursor + 1) % World.Switches.Count;
            int index = 0;
            foreach (TrackSwitchView railSwitch in World.Switches.Values)
            {
                if (index == keyboardSwitchCursor)
                {
                    KeyboardFocusedSwitch = railSwitch;
                    ShowToast("已选中道岔  ·  E 切换线路");
                    return;
                }

                index++;
            }
        }

        private void ToggleTrainByColor(RailColor color)
        {
            for (int i = 0; i < trains.Count; i++)
            {
                if (trains[i] != null && trains[i].Color == color)
                {
                    trains[i].ToggleMoving();
                    Audio.Play(trains[i].IsMoving ? AudioCue.Spawn : AudioCue.Brake);
                    ShowToast(RailPalette.NameCn(color) + (trains[i].IsMoving ? "列车发车" : "列车停车"));
                    return;
                }
            }

            ShowToast("该颜色列车已完成或正在进站");
        }

        private void ParseDeveloperArguments()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--smoke-test" || arg == "--dev")
                {
                    devMode = true;
                }
                else if (arg == "--autoplay")
                {
                    devMode = true;
                    devAutoPlay = true;
                }
                else if (arg == "--single-train")
                {
                    devMode = true;
                    devSingleTrain = true;
                }
                else if (arg == "--editor-demo-train")
                {
                    devMode = true;
                    devScreen = "editor";
                    devEditorDemoTrain = true;
                }
                else if (arg.StartsWith("--level=", StringComparison.Ordinal))
                {
                    devMode = true;
                    if (int.TryParse(arg.Substring("--level=".Length), out int level))
                    {
                        devStartLevel = Mathf.Clamp(level, 0, LevelCatalog.Levels.Count - 1);
                    }
                }
                else if (arg.StartsWith("--screen=", StringComparison.Ordinal))
                {
                    devMode = true;
                    devScreen = arg.Substring("--screen=".Length);
                }
                else if (arg.StartsWith("--force-result=", StringComparison.Ordinal))
                {
                    devMode = true;
                    devForceResult = arg.Substring("--force-result=".Length);
                }
                else if (arg.StartsWith("--capture=", StringComparison.Ordinal))
                {
                    devMode = true;
                    devCapturePath = arg.Substring("--capture=".Length);
                    devCaptureAt = Time.unscaledTime + 2.2f;
                }
                else if (arg.StartsWith("--quit-after=", StringComparison.Ordinal))
                {
                    devMode = true;
                    if (float.TryParse(arg.Substring("--quit-after=".Length), out float seconds))
                    {
                        devQuitAt = Time.unscaledTime + Mathf.Max(0.5f, seconds);
                    }
                }
            }

            if (devMode)
            {
                devNextActionAt = Time.unscaledTime + 1.2f;
                devNextLogAt = Time.unscaledTime + 1f;
                devForcedAt = Time.unscaledTime + 1.2f;
                Debug.Log("Train Train developer harness enabled. Level=" + devStartLevel + " autoplay=" + devAutoPlay);
            }
        }

        private void UpdateDeveloperHarness()
        {
            if (!devMode)
            {
                return;
            }

            if (devCaptureAt > 0f && !devCaptured && Time.unscaledTime >= devCaptureAt)
            {
                devCaptured = true;
                string path = string.IsNullOrEmpty(devCapturePath)
                    ? "/tmp/train_train_capture.png"
                    : devCapturePath;
                ScreenCapture.CaptureScreenshot(path, 1);
                Debug.Log("Train Train screenshot requested: " + path);
            }

            if (devQuitAt > 0f && Time.unscaledTime >= devQuitAt)
            {
                Debug.Log("Train Train harness exit.");
                Application.Quit();
                devQuitAt = float.MaxValue;
            }

            if (devAutoPlay && State == GameScreen.Playing && Time.unscaledTime >= devNextActionAt)
            {
                devNextActionAt = Time.unscaledTime + 1.1f;
                bool releasedTrain = false;
                for (int i = 0; i < trains.Count; i++)
                {
                    if (devSingleTrain && i > 0)
                    {
                        break;
                    }

                    if (trains[i] != null && !trains[i].IsMoving)
                    {
                        trains[i].SetMoving(true);
                        releasedTrain = true;
                        break;
                    }
                }

                if (!releasedTrain)
                {
                    TrainActor guideTrain = null;
                    for (int i = 0; i < trains.Count; i++)
                    {
                        if (devSingleTrain && i > 0)
                        {
                            break;
                        }

                        if (trains[i] != null && trains[i].IsMoving)
                        {
                            guideTrain = trains[i];
                            break;
                        }
                    }

                    if (guideTrain != null)
                    {
                        GuideDeveloperTrain(guideTrain);
                    }
                }
            }

            if (!string.IsNullOrEmpty(devForceResult) && State == GameScreen.Playing && Time.unscaledTime >= devForcedAt)
            {
                string forced = devForceResult;
                devForceResult = null;
                if (forced == "win")
                {
                    for (int i = 0; i < delivered.Length; i++)
                    {
                        delivered[i] = CurrentLevel.Quotas[i];
                    }

                    DeliveredTotal = totalRequired;
                    TimeRemaining = Mathf.Max(1f, TimeRemaining);
                    Win();
                }
                else
                {
                    Lose("开发者测试：线路中断");
                }
            }

            if (Time.unscaledTime >= devNextLogAt)
            {
                devNextLogAt = Time.unscaledTime + 2f;
                Debug.Log(
                    "Train Train state=" + State +
                    " trains=" + trains.Count +
                    " moving=" + MovingTrains +
                    " delivered=" + DeliveredTotal + "/" + totalRequired +
                    " time=" + TimeRemaining.ToString("F1"));
            }
        }

        private void GuideDeveloperTrain(TrainActor train)
        {
            Vector2Int target = default;
            float best = float.MaxValue;
            foreach (StationRuntime station in stationQueues.Values)
            {
                if (train.CargoColor.HasValue && station.Definition.Color != train.CargoColor.Value)
                {
                    continue;
                }

                float distance = (station.Definition.Position - train.CurrentNode).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    target = station.Definition.Position;
                }
            }

            if (!stationsByNode.ContainsKey(target))
            {
                return;
            }

            Dictionary<Vector2Int, Vector2Int> nextHop = new Dictionary<Vector2Int, Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int> { target };
            queue.Enqueue(target);
            while (queue.Count > 0)
            {
                Vector2Int node = queue.Dequeue();
                foreach (Vector2Int neighbor in graph[node])
                {
                    if (!visited.Add(neighbor))
                    {
                        continue;
                    }

                    nextHop[neighbor] = node;
                    queue.Enqueue(neighbor);
                }
            }

            foreach (TrackSwitchView railSwitch in World.Switches.Values)
            {
                if (nextHop.TryGetValue(railSwitch.Node, out Vector2Int hop))
                {
                    railSwitch.SelectNeighbor(hop);
                }
            }
        }

        private void ShowTitlePreview()
        {
            int previewIndex = Mathf.Clamp(SelectedLevelIndex, 0, LevelCatalog.Levels.Count - 1);
            RebuildWorld(true, previewIndex);
        }

        private void RebuildWorld(bool preview, int levelIndex = -1)
        {
            if (World != null)
            {
                WorldBuilder.Destroy(World);
            }

            LevelDefinition previewLevel = levelIndex >= 0
                ? LevelCatalog.GetLevel(levelIndex)
                : LevelCatalog.GetLevel(SelectedLevelIndex);
            World = WorldBuilder.Build(previewLevel, previewLevel.Index + 7, preview);
            builtAspect = Mathf.Max(0.5f, Screen.width / (float)Mathf.Max(1, Screen.height));
            zoomMultiplier = 1f;
        }
    }
}
