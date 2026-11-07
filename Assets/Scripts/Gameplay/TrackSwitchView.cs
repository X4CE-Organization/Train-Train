using System.Collections.Generic;
using UnityEngine;

namespace TrainTrain
{
    public sealed class SwitchAnchor : MonoBehaviour
    {
        public TrackSwitchView View;
    }

    public sealed class TrackSwitchView
    {
        public readonly Vector2Int Node;
        public readonly IReadOnlyList<Vector2Int> Options;
        public int SelectedIndex { get; private set; }
        public bool Hovered { get; set; }

        private readonly GameObject root;
        private readonly Transform lamp;
        private readonly Transform needle;
        private readonly List<LineRenderer> routeLines;
        private readonly Color accent;

        private TrackSwitchView(Vector2Int node, IReadOnlyList<Vector2Int> options, GameObject root, Transform lamp, Transform needle, List<LineRenderer> routeLines, Color accent)
        {
            Node = node;
            Options = options;
            this.root = root;
            this.lamp = lamp;
            this.needle = needle;
            this.routeLines = routeLines;
            this.accent = accent;
            SelectedIndex = Mathf.Abs(node.x * 3 + node.y * 5) % Mathf.Max(1, options.Count);
            Refresh();
        }

        public static TrackSwitchView Create(
            Vector2Int node,
            List<Vector2Int> options,
            int seed,
            Transform parent,
            Color accent)
        {
            GameObject root = new GameObject("Switch_" + node.x + "_" + node.y);
            root.layer = TrainTrainApp.InteractableLayer;
            root.transform.SetParent(parent, false);
            root.transform.position = WorldBuilder.GridToWorld(node);

            Color baseColor = new Color(0.19f, 0.23f, 0.24f);
            Color pale = new Color(0.72f, 0.76f, 0.72f);
            LowPolyAssets.Cylinder(
                "SignalBase",
                root.transform,
                new Vector3(0f, 0.04f, 0f),
                0.57f,
                0.1f,
                baseColor,
                10);
            LowPolyAssets.Cylinder(
                "SignalPlate",
                root.transform,
                new Vector3(0f, 0.105f, 0f),
                0.43f,
                0.08f,
                pale,
                10);
            LowPolyAssets.Cube(
                "SignalPost",
                root.transform,
                new Vector3(0.18f, 0.45f, -0.18f),
                new Vector3(0.09f, 0.72f, 0.09f),
                baseColor);

            GameObject lampObject = LowPolyAssets.Sphere(
                "SignalLamp",
                root.transform,
                new Vector3(0.18f, 0.84f, -0.18f),
                Vector3.one * 0.19f,
                accent,
                true);
            lampObject.transform.SetParent(root.transform, true);

            GameObject arrowObject = new GameObject("SwitchArrow");
            arrowObject.transform.SetParent(root.transform, false);
            arrowObject.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            arrowObject.transform.localScale = Vector3.one * 1.55f;
            LowPolyAssets.ClosedArrow(
                "ArrowFill",
                arrowObject.transform,
                Vector3.zero,
                RailPalette.Paper);

            List<LineRenderer> lines = new List<LineRenderer>(options.Count);
            for (int i = 0; i < options.Count; i++)
            {
                LineRenderer line = LowPolyAssets.Line(
                    "Route_" + i,
                    root.transform,
                    new Color(0.15f, 0.19f, 0.19f),
                    0.055f,
                    TrainTrainApp.InteractableLayer);
                Vector3 localEnd = root.transform.InverseTransformPoint(WorldBuilder.GridToWorld(options[i]));
                line.SetPosition(0, new Vector3(0f, 0.19f, 0f));
                line.SetPosition(1, localEnd + Vector3.up * 0.19f);
                lines.Add(line);
            }

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.28f, 0f);
            collider.size = new Vector3(1.55f, 0.95f, 1.55f);
            SwitchAnchor anchor = root.AddComponent<SwitchAnchor>();

            TrackSwitchView view = new TrackSwitchView(node, options, root, lampObject.transform, arrowObject.transform, lines, accent);
            view.SelectedIndex = Mathf.Abs(seed * 17 + node.x * 7 + node.y * 13) % Mathf.Max(1, options.Count);
            view.Refresh();
            anchor.View = view;
            LowPolyAssets.SetLayerRecursively(root, TrainTrainApp.InteractableLayer);
            return view;
        }

        public void Cycle()
        {
            if (Options.Count == 0)
            {
                return;
            }

            SelectedIndex = (SelectedIndex + 1) % Options.Count;
            Refresh();
        }

        public bool SelectNeighbor(Vector2Int neighbor)
        {
            int index = -1;
            for (int i = 0; i < Options.Count; i++)
            {
                if (Options[i] == neighbor)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                return false;
            }

            SelectedIndex = index;
            Refresh();
            return true;
        }

        public bool TryGetNext(Vector2Int incoming, out Vector2Int next)
        {
            if (Options.Count == 0)
            {
                next = default;
                return false;
            }

            Vector2Int preferred = Options[SelectedIndex];
            if (preferred != incoming)
            {
                next = preferred;
                return true;
            }

            for (int i = 0; i < Options.Count; i++)
            {
                int index = (SelectedIndex + 1 + i) % Options.Count;
                if (Options[index] != incoming)
                {
                    next = Options[index];
                    return true;
                }
            }

            next = default;
            return false;
        }

        public void UpdateVisual(float time)
        {
            float pulse = 1f + (Hovered ? 0.16f : 0.06f) * Mathf.Sin(time * 6f);
            lamp.localScale = Vector3.one * (Hovered ? 0.24f : 0.19f) * pulse;
            needle.localScale = Vector3.one * (Hovered ? 1.7f : 1.55f) * pulse;
        }

        public Vector3 WorldPosition
        {
            get { return root.transform.position; }
        }

        private void Refresh()
        {
            if (Options.Count == 0)
            {
                return;
            }

            Vector2Int selected = Options[SelectedIndex];
            for (int i = 0; i < routeLines.Count; i++)
            {
                bool active = i == SelectedIndex;
                routeLines[i].sharedMaterial = LowPolyAssets.Material(
                    active ? LowPolyAssets.Lighten(accent, 0.3f) : new Color(0.12f, 0.15f, 0.15f),
                    0f,
                    active ? 0.9f : 0f);
                routeLines[i].startColor = active ? LowPolyAssets.Lighten(accent, 0.3f) : new Color(0.12f, 0.15f, 0.15f);
                routeLines[i].endColor = routeLines[i].startColor;
                routeLines[i].startWidth = active ? 0.27f : 0.045f;
                routeLines[i].endWidth = routeLines[i].startWidth;
            }

            Vector3 direction = WorldBuilder.GridToWorld(selected) - WorldBuilder.GridToWorld(Node);
            if (direction.sqrMagnitude > 0.001f)
            {
                needle.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }
    }
}
