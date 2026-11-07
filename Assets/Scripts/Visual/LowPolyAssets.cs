using System.Collections.Generic;
using UnityEngine;

namespace TrainTrain
{
    public static class LowPolyAssets
    {
        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();
        private static readonly Dictionary<int, Mesh> CylinderMeshes = new Dictionary<int, Mesh>();
        private static Material baseMaterial;
        private static Material particleMaterial;

        public static Material Material(Color color, float metallic = 0f, float emission = 0f)
        {
            string key = ColorUtility.ToHtmlStringRGBA(color) + "_" + metallic.ToString("F2") + "_" + emission.ToString("F2");
            if (MaterialCache.TryGetValue(key, out Material cached))
            {
                return cached;
            }

            if (baseMaterial == null)
            {
                baseMaterial = Resources.Load<Material>("TrainTrainLit");
            }

            if (baseMaterial == null)
            {
                baseMaterial = Resources.Load<Material>("TrainTrainUnlit");
            }

            if (baseMaterial == null)
            {
                Shader fallbackShader = Shader.Find("Standard");
                if (fallbackShader == null)
                {
                    fallbackShader = Shader.Find("Sprites/Default");
                }

                if (fallbackShader == null)
                {
                    throw new System.InvalidOperationException(
                        "Train Train is missing its runtime material. Run 'Train Train / Repair Project Setup' in Unity.");
                }

                baseMaterial = new Material(fallbackShader);
            }

            Material material = new Material(baseMaterial)
            {
                name = "Runtime_" + key,
                color = color,
                enableInstancing = true
            };
            material.SetFloat("_Glossiness", metallic > 0f ? 0.48f : 0.08f);
            material.SetFloat("_Metallic", metallic);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }

            MaterialCache[key] = material;
            return material;
        }

        public static Material ParticleMaterial()
        {
            if (particleMaterial == null)
            {
                particleMaterial = Resources.Load<Material>("TrainTrainParticle");
            }

            return particleMaterial != null ? particleMaterial : Material(new Color(0.82f, 0.84f, 0.82f));
        }

        public static GameObject Cube(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            Quaternion? localRotation = null,
            bool collider = false,
            float metallic = 0f,
            float emission = 0f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = Material(color, metallic, emission);
            Collider hitbox = go.GetComponent<Collider>();
            if (!collider && hitbox != null)
            {
                hitbox.enabled = false;
            }

            return go;
        }

        public static GameObject Cylinder(
            string name,
            Transform parent,
            Vector3 localPosition,
            float radius,
            float height,
            Color color,
            int sides = 8,
            Quaternion? localRotation = null,
            bool collider = false,
            float metallic = 0f,
            float emission = 0f)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = GetCylinderMesh(sides);
            go.transform.localScale = new Vector3(radius, height, radius);

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material(color, metallic, emission);

            if (collider)
            {
                CapsuleCollider hit = go.AddComponent<CapsuleCollider>();
                hit.radius = 0.6f;
                hit.height = 1f;
            }

            return go;
        }

        public static GameObject ClosedArrow(
            string name,
            Transform parent,
            Vector3 localPosition,
            Color color,
            Quaternion? localRotation = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildClosedArrowMesh();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material(color, 0f, 0.72f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        public static GameObject Cone(
            string name,
            Transform parent,
            Vector3 localPosition,
            float radius,
            float height,
            Color color,
            int sides = 7,
            Quaternion? localRotation = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = new Vector3(radius, height, radius);

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildConeMesh(sides);
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material(color);
            return go;
        }

        public static GameObject Sphere(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            bool emission = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = Material(color, 0f, emission ? 1.3f : 0f);
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return go;
        }

        public static GameObject BeamBetween(
            string name,
            Transform parent,
            Vector3 from,
            Vector3 to,
            float width,
            float height,
            Color color,
            float metallic = 0f,
            float emission = 0f)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.001f)
            {
                return null;
            }

            Vector3 midpoint = (from + to) * 0.5f;
            Quaternion rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            return Cube(
                name,
                parent,
                midpoint,
                new Vector3(width, height, length),
                color,
                rotation,
                false,
                metallic,
                emission);
        }

        public static LineRenderer Line(
            string name,
            Transform parent,
            Color color,
            float width,
            int layer)
        {
            GameObject go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 5;
            line.sharedMaterial = Material(color, 0f, 0.9f);
            line.startColor = color;
            line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        public static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            for (int i = 0; i < target.transform.childCount; i++)
            {
                SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
            }
        }

        public static Color Lighten(Color color, float amount)
        {
            return Color.Lerp(color, Color.white, Mathf.Clamp01(amount));
        }

        public static Color Darken(Color color, float amount)
        {
            return Color.Lerp(color, Color.black, Mathf.Clamp01(amount));
        }

        private static Mesh GetCylinderMesh(int sides)
        {
            int key = Mathf.Clamp(sides, 4, 16);
            if (CylinderMeshes.TryGetValue(key, out Mesh mesh))
            {
                return mesh;
            }

            mesh = BuildCylinderMesh(key);
            CylinderMeshes[key] = mesh;
            return mesh;
        }

        private static Mesh BuildCylinderMesh(int sides)
        {
            float half = 0.5f;
            MeshBuilder builder = new MeshBuilder();
            for (int side = 0; side < sides; side++)
            {
                float a0 = side / (float)sides * Mathf.PI * 2f;
                float a1 = (side + 1f) / sides * Mathf.PI * 2f;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * 0.5f, -half, Mathf.Sin(a0) * 0.5f);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * 0.5f, -half, Mathf.Sin(a1) * 0.5f);
                Vector3 t0 = new Vector3(Mathf.Cos(a0) * 0.5f, half, Mathf.Sin(a0) * 0.5f);
                Vector3 t1 = new Vector3(Mathf.Cos(a1) * 0.5f, half, Mathf.Sin(a1) * 0.5f);
                builder.Triangle(b0, t0, t1);
                builder.Triangle(b0, t1, b1);
                builder.Triangle(Vector3.up * half, t0, t1);
                builder.Triangle(Vector3.down * half, b1, b0);
            }

            return builder.ToMesh("LowPolyCylinder_" + sides);
        }

        private static Mesh BuildConeMesh(int sides)
        {
            sides = Mathf.Clamp(sides, 4, 14);
            MeshBuilder builder = new MeshBuilder();
            Vector3 apex = Vector3.up * 0.5f;
            for (int side = 0; side < sides; side++)
            {
                float a0 = side / (float)sides * Mathf.PI * 2f;
                float a1 = (side + 1f) / sides * Mathf.PI * 2f;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * 0.5f, -0.5f, Mathf.Sin(a0) * 0.5f);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * 0.5f, -0.5f, Mathf.Sin(a1) * 0.5f);
                builder.Triangle(b0, apex, b1);
                builder.Triangle(Vector3.down * 0.5f, b1, b0);
            }

            return builder.ToMesh("LowPolyCone_" + sides);
        }

        private static Mesh BuildClosedArrowMesh()
        {
            Vector2[] shape =
            {
                new Vector2(-0.085f, -0.30f),
                new Vector2(0.085f, -0.30f),
                new Vector2(0.085f, 0.02f),
                new Vector2(0.25f, 0.02f),
                new Vector2(0f, 0.34f),
                new Vector2(-0.25f, 0.02f),
                new Vector2(-0.085f, 0.02f)
            };

            float halfHeight = 0.045f;
            Vector3[] top = new Vector3[shape.Length];
            Vector3[] bottom = new Vector3[shape.Length];
            for (int i = 0; i < shape.Length; i++)
            {
                top[i] = new Vector3(shape[i].x, halfHeight, shape[i].y);
                bottom[i] = new Vector3(shape[i].x, -halfHeight, shape[i].y);
            }

            MeshBuilder builder = new MeshBuilder();
            AddArrowFace(builder, top, true);
            AddArrowFace(builder, bottom, false);

            for (int i = 0; i < shape.Length; i++)
            {
                int next = (i + 1) % shape.Length;
                builder.Triangle(bottom[i], top[i], top[next]);
                builder.Triangle(bottom[i], top[next], bottom[next]);
            }

            return builder.ToMesh("ClosedSwitchArrow");
        }

        private static void AddArrowFace(MeshBuilder builder, Vector3[] points, bool upward)
        {
            int[] triangles = { 0, 6, 2, 0, 2, 1, 5, 6, 4, 6, 2, 4, 2, 4, 3 };
            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (upward)
                {
                    builder.Triangle(points[triangles[i]], points[triangles[i + 1]], points[triangles[i + 2]]);
                }
                else
                {
                    builder.Triangle(points[triangles[i]], points[triangles[i + 2]], points[triangles[i + 1]]);
                }
            }
        }

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> indices = new List<int>();

            public void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                indices.Add(start);
                indices.Add(start + 1);
                indices.Add(start + 2);
            }

            public Mesh ToMesh(string name)
            {
                Mesh mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
