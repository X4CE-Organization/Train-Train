using UnityEngine;

namespace TrainTrain
{
    public sealed class TrainActor : MonoBehaviour
    {
        private const float LostAfterSeconds = 27f;
        public RailColor Color { get; private set; }
        public int Capacity
        {
            get { return cargo != null ? cargo.Capacity : 0; }
        }
        public int CarCount { get; private set; }
        public RailColor? CargoColor
        {
            get { return cargo != null ? cargo.DominantColor : null; }
        }
        public int Passengers
        {
            get { return cargo != null ? cargo.Passengers : 0; }
        }
        public Vector2Int CurrentNode { get; private set; }
        public Vector2Int NextNode { get; private set; }
        public float Progress { get; private set; }
        public float RouteAge { get; private set; }
        public bool IsMoving { get; private set; }
        public bool HasDepartureCollisionGrace
        {
            get { return autoStartAt < float.MaxValue && Time.time < autoStartAt + 3f; }
        }

        private TrainTrainApp app;
        private float segmentDuration;
        private float spawnTime;
        private float autoStartAt = float.MaxValue;
        private Transform visual;
        private Transform[] wheels;
        private Transform stopBadge;
        private Transform movingBadge;
        private Transform[] passengerMarks;
        private TrainCargo cargo;
        private float stationStopUntil;
        private float movementScale;
        private float startElevation;
        private float endElevation;

        public static TrainActor Create(
            TrainTrainApp app,
            RailColor color,
            Vector2Int source,
            Vector2Int next,
            Transform parent,
            TrainStyle? styleOverride = null)
        {
            GameObject root = new GameObject("Train_" + RailPalette.NameCn(color));
            root.layer = TrainTrainApp.InteractableLayer;
            root.transform.SetParent(parent, false);
            root.transform.position = WorldBuilder.GridToWorld(source);

            TrainActor actor = root.AddComponent<TrainActor>();
            actor.app = app;
            actor.Color = color;
            actor.spawnTime = Time.time;
            actor.BuildVisual(styleOverride);
            actor.SetSegment(source, next, true);
            actor.SetMoving(false);

            BoxCollider hitbox = root.AddComponent<BoxCollider>();
            hitbox.center = new Vector3(0f, 0.44f, 0.1f);
            hitbox.size = new Vector3(1.05f, 0.95f, 1.65f);

            LowPolyAssets.SetLayerRecursively(root, TrainTrainApp.InteractableLayer);
            return actor;
        }

        public void ToggleMoving()
        {
            SetMoving(!IsMoving);
        }

        public void SetMoving(bool moving)
        {
            IsMoving = moving;
            if (!moving)
            {
                autoStartAt = float.MaxValue;
            }

            if (stopBadge != null)
            {
                stopBadge.gameObject.SetActive(!moving);
            }

            if (movingBadge != null)
            {
                movingBadge.gameObject.SetActive(moving);
            }
        }

        public void AutoStartIn(float seconds)
        {
            autoStartAt = Time.time + Mathf.Max(0f, seconds);
        }

        public void BeginStationStop(float seconds)
        {
            stationStopUntil = Mathf.Max(stationStopUntil, Time.time + Mathf.Max(0.2f, seconds));
        }

        public int UnloadMatching(RailColor stationColor)
        {
            int count = cargo != null ? cargo.UnloadMatching(stationColor) : 0;
            RefreshPassengerMarks();
            return count;
        }

        public int LoadPassengers(RailColor cargoColor, int available)
        {
            if (cargo == null || available <= 0 || Passengers >= Capacity)
            {
                return 0;
            }

            int loaded = cargo.Load(cargoColor, available);
            RefreshPassengerMarks();
            return loaded;
        }

        public int PassengerCount(RailColor color)
        {
            return cargo != null ? cargo.CountOf(color) : 0;
        }

        public void SetSegment(Vector2Int from, Vector2Int to, bool immediate = false)
        {
            CurrentNode = from;
            NextNode = to;
            Progress = 0f;
            RouteAge = 0f;
            float distance = (WorldBuilder.GridToWorld(from) - WorldBuilder.GridToWorld(to)).magnitude;
            segmentDuration = Mathf.Max(0.24f, distance / Mathf.Max(0.2f, app.CurrentTrainSpeed));
            RailEdge edge = new RailEdge(from, to);
            startElevation = app.CurrentLevel != null
                ? app.CurrentLevel.Map.ElevationAt(edge, from)
                : 0f;
            endElevation = app.CurrentLevel != null
                ? app.CurrentLevel.Map.ElevationAt(edge, to)
                : 0f;

            Vector3 direction = WorldBuilder.GridToWorld(to) - WorldBuilder.GridToWorld(from);
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            if (immediate)
            {
                UpdateWorldPosition();
            }
        }

        private void Update()
        {
            if (app == null || app.State != GameScreen.Playing)
            {
                return;
            }

            float grow = Mathf.Clamp01((Time.time - spawnTime) / 0.3f);
            transform.localScale = Vector3.one * Mathf.SmoothStep(0.35f, 1f, grow);

            if (!IsMoving)
            {
                movementScale = Mathf.MoveTowards(movementScale, 0f, Time.deltaTime * 5f);
                if (Time.time >= autoStartAt)
                {
                    SetMoving(true);
                }

                visual.localPosition = new Vector3(0f, 0.01f + Mathf.Sin(Time.time * 3f) * 0.006f, 0f);
                if (stopBadge != null)
                {
                    stopBadge.localRotation = Quaternion.Euler(0f, Time.time * 55f, 0f);
                    stopBadge.localPosition = new Vector3(0f, 1.38f + Mathf.Sin(Time.time * 3f) * 0.05f, 0f);
                }

                return;
            }

            visual.localPosition = new Vector3(0f, 0.015f + Mathf.Sin(Time.time * 9f) * 0.012f, 0f);
            if (Time.time < stationStopUntil)
            {
                movementScale = Mathf.MoveTowards(movementScale, 0f, Time.deltaTime * 7f);
                return;
            }

            movementScale = Mathf.MoveTowards(movementScale, 1f, Time.deltaTime * 2.8f);
            RouteAge += Time.deltaTime;
            Progress += Time.deltaTime / segmentDuration * movementScale;

            if (wheels != null)
            {
                for (int i = 0; i < wheels.Length; i++)
                {
                    if (wheels[i] != null)
                    {
                        wheels[i].Rotate(Vector3.forward * (Time.deltaTime * 550f), Space.Self);
                    }
                }
            }

            if (Progress >= 1f)
            {
                Progress = 1f;
                UpdateWorldPosition();
                app.TrainReachedNode(this, NextNode);
                return;
            }

            UpdateWorldPosition();

            if (RouteAge > LostAfterSeconds)
            {
                app.TrainLost(this);
            }
        }

        private void UpdateWorldPosition()
        {
            Vector3 a = WorldBuilder.GridToWorld(CurrentNode);
            Vector3 b = WorldBuilder.GridToWorld(NextNode);
            Vector3 position = Vector3.Lerp(a, b, Progress);
            position.y += Mathf.Lerp(startElevation, endElevation, Progress);
            position.y += Mathf.Sin(Progress * Mathf.PI) * 0.035f;
            transform.position = position;
        }

        private void BuildVisual(TrainStyle? styleOverride)
        {
            visual = new GameObject("Model").transform;
            visual.SetParent(transform, false);

            TrainStyleInfo style = TrainStyleCatalog.Get(
                styleOverride ?? app.SelectedTrainInfo.Style);
            cargo = new TrainCargo(style.Capacity);
            Color bodyColor = RailPalette.ForColor(Color);
            Color darkBody = LowPolyAssets.Darken(bodyColor, 0.45f);
            Color lightBody = LowPolyAssets.Lighten(bodyColor, 0.2f);
            Color metal = new Color(0.31f, 0.34f, 0.36f);
            Color dark = new Color(0.11f, 0.14f, 0.16f);

            LowPolyAssets.Cube("Chassis", visual, new Vector3(0f, 0.32f, 0f), new Vector3(0.7f, 0.24f, 1.18f), dark);
            Vector3 bodySize = style.Style == TrainStyle.Express
                ? new Vector3(0.62f, 0.4f, 1.52f)
                : style.Style == TrainStyle.Metro
                    ? new Vector3(0.8f, 0.58f, 1.14f)
                    : style.Style == TrainStyle.Freight
                        ? new Vector3(0.84f, 0.54f, 1.28f)
                        : new Vector3(0.72f, 0.47f, 1.2f);
            LowPolyAssets.Cube("EngineBody", visual, new Vector3(0f, 0.5f, 0f), bodySize, bodyColor, null, false, 0.2f);
            LowPolyAssets.Cube("Cabin", visual, new Vector3(0f, 0.76f, 0.12f), new Vector3(0.6f, style.Style == TrainStyle.Metro ? 0.52f : 0.46f, 0.55f), lightBody, null, false, 0.16f);
            LowPolyAssets.Cube("Roof", visual, new Vector3(0f, 1.0f, 0.12f), new Vector3(0.68f, 0.09f, 0.64f), darkBody);
            LowPolyAssets.Cube("Stripe", visual, new Vector3(0f, 0.45f, 0f), new Vector3(0.75f, 0.09f, 1.22f), RailPalette.Paper);
            LowPolyAssets.Cube("FrontBumper", visual, new Vector3(0f, 0.3f, 0.64f), new Vector3(0.8f, 0.17f, 0.14f), dark);
            CarCount = style.Style == TrainStyle.Metro || style.Style == TrainStyle.Freight ? 3 : 2;
            for (int car = 1; car < CarCount; car++)
            {
                float z = -1.05f - (car - 1) * 0.82f;
                LowPolyAssets.Cube("RearCar_" + car, visual, new Vector3(0f, 0.43f, z), new Vector3(0.62f, 0.48f, 0.68f), darkBody);
                LowPolyAssets.Cube("Cargo_" + car, visual, new Vector3(0f, 0.57f, z), new Vector3(0.48f, 0.35f, 0.55f), RailPalette.Paper);
                LowPolyAssets.Cube("CargoBand_" + car, visual, new Vector3(0f, 0.57f, z), new Vector3(0.51f, 0.1f, 0.58f), bodyColor);
            }
            if (style.Style == TrainStyle.Express)
            {
                LowPolyAssets.Cube("Nose", visual, new Vector3(0f, 0.44f, 0.82f), new Vector3(0.5f, 0.24f, 0.34f), lightBody, Quaternion.Euler(-18f, 0f, 0f), false, 0.45f);
                LowPolyAssets.Cube("SpeedStripe", visual, new Vector3(0f, 0.66f, 0.18f), new Vector3(0.64f, 0.06f, 1.48f), RailPalette.Azure, null, false, 0f, 0.5f);
            }
            else if (style.Style == TrainStyle.Metro)
            {
                LowPolyAssets.Cube("MetroRoof", visual, new Vector3(0f, 1.05f, -0.02f), new Vector3(0.78f, 0.08f, 1.1f), RailPalette.Paper);
                LowPolyAssets.Cube("MetroStripe", visual, new Vector3(0f, 0.53f, 0f), new Vector3(0.83f, 0.08f, 1.18f), RailPalette.Crimson);
            }
            else if (style.Style == TrainStyle.Freight)
            {
                LowPolyAssets.Cylinder("Tank", visual, new Vector3(0f, 0.63f, -1.05f), 0.33f, 0.8f, LowPolyAssets.Darken(bodyColor, 0.28f), 8, Quaternion.Euler(90f, 0f, 0f));
            }

            LowPolyAssets.Cube("WindowLeft", visual, new Vector3(-0.305f, 0.79f, 0.18f), new Vector3(0.015f, 0.22f, 0.26f), new Color(0.62f, 0.9f, 0.95f), null, false, 0.5f, 0.22f);
            LowPolyAssets.Cube("WindowRight", visual, new Vector3(0.305f, 0.79f, 0.18f), new Vector3(0.015f, 0.22f, 0.26f), new Color(0.62f, 0.9f, 0.95f), null, false, 0.5f, 0.22f);
            LowPolyAssets.Cylinder("Chimney", visual, new Vector3(0f, 0.9f, 0.55f), 0.11f, 0.35f, dark, 7, null);
            LowPolyAssets.Cylinder("ChimneyCap", visual, new Vector3(0f, 1.08f, 0.55f), 0.16f, 0.08f, bodyColor, 7, null);

            LowPolyAssets.Sphere("HeadlightLeft", visual, new Vector3(-0.22f, 0.48f, 0.62f), Vector3.one * 0.13f, new Color(1f, 0.92f, 0.55f), true);
            LowPolyAssets.Sphere("HeadlightRight", visual, new Vector3(0.22f, 0.48f, 0.62f), Vector3.one * 0.13f, new Color(1f, 0.92f, 0.55f), true);

            passengerMarks = new Transform[Capacity];
            for (int i = 0; i < passengerMarks.Length; i++)
            {
                int row = i / 3;
                int column = i % 3;
                int car = row % Mathf.Max(1, CarCount - 1) + 1;
                int carRow = row / Mathf.Max(1, CarCount - 1);
                passengerMarks[i] = LowPolyAssets.Sphere(
                    "Passenger",
                    visual,
                    new Vector3(
                        -0.2f + column * 0.2f,
                        0.82f,
                        -0.78f - carRow * 0.2f - (car - 1) * 0.82f),
                    new Vector3(0.16f, 0.22f, 0.16f),
                    RailPalette.Paper).transform;
            }

            RefreshPassengerMarks();

            GameObject stop = new GameObject("StoppedBadge");
            stop.transform.SetParent(visual, false);
            stop.transform.localPosition = new Vector3(0f, 1.38f, 0f);
            LowPolyAssets.Cube("StopBarLeft", stop.transform, new Vector3(-0.1f, 0f, 0f), new Vector3(0.09f, 0.32f, 0.09f), new Color(1f, 0.78f, 0.28f), null, false, 0f, 0.8f);
            LowPolyAssets.Cube("StopBarRight", stop.transform, new Vector3(0.1f, 0f, 0f), new Vector3(0.09f, 0.32f, 0.09f), new Color(1f, 0.78f, 0.28f), null, false, 0f, 0.8f);
            stopBadge = stop.transform;

            GameObject moving = new GameObject("MovingBadge");
            moving.transform.SetParent(visual, false);
            moving.transform.localPosition = new Vector3(0f, 1.42f, 0.1f);
            LowPolyAssets.Cone("TravelArrow", moving.transform, Vector3.zero, 0.12f, 0.32f, new Color(0.52f, 1f, 0.72f), 6);
            movingBadge = moving.transform;

            wheels = new Transform[6];
            int wheelIndex = 0;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = -1; row <= 1; row++)
                {
                    float z = row * 0.42f;
                    wheels[wheelIndex++] = LowPolyAssets.Cylinder(
                        "Wheel",
                        visual,
                        new Vector3(side * 0.39f, 0.2f, z),
                        0.18f,
                        0.1f,
                        metal,
                        8,
                        Quaternion.Euler(0f, 0f, 90f)).transform;
                }
            }

            CreateSmoke();
        }

        private void CreateSmoke()
        {
            GameObject smoke = new GameObject("Steam");
            smoke.transform.SetParent(visual, false);
            smoke.transform.localPosition = new Vector3(0f, 1.13f, 0.55f);
            ParticleSystem particles = smoke.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = 1.2f;
            main.startSpeed = 0.34f;
            main.startSize = 0.18f;
            main.startColor = new Color(0.9f, 0.92f, 0.9f, 0.46f);
            main.maxParticles = 22;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.025f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 4.5f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 7f;
            shape.radius = 0.04f;

            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.88f, 0.9f, 0.87f), 0f),
                    new GradientColorKey(new Color(0.7f, 0.74f, 0.72f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.5f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            fade.color = gradient;

            ParticleSystemRenderer renderer = smoke.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = LowPolyAssets.ParticleMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            LowPolyAssets.SetLayerRecursively(smoke, 0);
        }

        private void RefreshPassengerMarks()
        {
            if (passengerMarks == null || cargo == null)
            {
                return;
            }

            int mark = 0;
            for (int color = 0; color < 4; color++)
            {
                Color cargoColor = RailPalette.ForColor((RailColor)color);
                for (int count = 0; count < this.cargo.CountOf((RailColor)color) && mark < passengerMarks.Length; count++)
                {
                    Transform passenger = passengerMarks[mark++];
                    if (passenger == null)
                    {
                        continue;
                    }

                    passenger.gameObject.SetActive(true);
                    passenger.GetComponent<Renderer>().sharedMaterial = LowPolyAssets.Material(cargoColor);
                }
            }

            for (; mark < passengerMarks.Length; mark++)
            {
                if (passengerMarks[mark] != null)
                {
                    passengerMarks[mark].gameObject.SetActive(false);
                }
            }
        }
    }
}
