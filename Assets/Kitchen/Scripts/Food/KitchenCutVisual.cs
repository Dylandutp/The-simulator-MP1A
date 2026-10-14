using System.Collections.Generic;
using UnityEngine;

namespace HouseSim.Kitchen
{
    /// <summary>Clips the original produce geometry into closed halves/quarters under one grab root.</summary>
    public sealed class KitchenCutVisual : MonoBehaviour
    {
        private struct Vertex
        {
            public Vector3 position;
            public Vector3 normal;
            public Vector2 uv;
            public Vertex(Vector3 position, Vector3 normal, Vector2 uv)
            { this.position = position; this.normal = normal; this.uv = uv; }
            public static Vertex Lerp(Vertex a, Vertex b, float t) =>
                new Vertex(Vector3.LerpUnclamped(a.position, b.position, t),
                    Vector3.LerpUnclamped(a.normal, b.normal, t).normalized, Vector2.LerpUnclamped(a.uv, b.uv, t));
        }
        private struct Triangle
        {
            public Vertex a, b, c;
            public bool cap;
            public Triangle(Vertex a, Vertex b, Vertex c, bool cap = false)
            { this.a = a; this.b = b; this.c = c; this.cap = cap; }
        }
        private sealed class Source
        {
            public string name;
            public Material material;
            public bool foodTint;
            public List<Triangle> triangles;
        }

        private readonly List<Source> sources = new List<Source>();
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private readonly List<Renderer> capRenderers = new List<Renderer>();
        private readonly List<bool> capFoodTint = new List<bool>();
        private Transform pieces;
        private Bounds originalBounds;
        private KitchenFood food;
        private KitchenFoodModelVisual modelVisual;
        private MaterialPropertyBlock capColor;
        private Color flesh;
        private float visibleCleanliness;
        public int PieceCount { get; private set; }
        public int Stage { get; private set; }

        public void Configure(KitchenFood sourceWhole)
        {
            food = GetComponent<KitchenFood>();
            modelVisual = GetComponent<KitchenFoodModelVisual>();
            if (modelVisual && modelVisual.HasAuthoredCuts) return;
            if (sourceWhole == null) return;
            sources.Clear();
            Matrix4x4 toRoot = sourceWhole.transform.worldToLocalMatrix;
            foreach (var filter in sourceWhole.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<Renderer>();
                if (mesh == null || renderer == null || !renderer.enabled || !mesh.isReadable) continue;
                Matrix4x4 matrix = toRoot * filter.transform.localToWorldMatrix;
                Matrix4x4 normalMatrix = matrix.inverse.transpose;
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Vector2[] uvs = mesh.uv;
                int[] indices = mesh.triangles;
                var source = new Source { name = filter.name, material = renderer.sharedMaterial, triangles = new List<Triangle>() };
                if (sourceWhole.visualRenderers != null)
                    foreach (var tint in sourceWhole.visualRenderers) if (tint == renderer) source.foodTint = true;
                for (int i = 0; i + 2 < indices.Length; i += 3)
                {
                    Vertex Read(int index) => new Vertex(matrix.MultiplyPoint3x4(vertices[index]),
                        normalMatrix.MultiplyVector(normals.Length == vertices.Length ? normals[index] : Vector3.up).normalized,
                        uvs.Length == vertices.Length ? uvs[index] : Vector2.zero);
                    source.triangles.Add(new Triangle(Read(indices[i]), Read(indices[i + 1]), Read(indices[i + 2])));
                }
                sources.Add(source);
            }
            var box = sourceWhole.GetComponent<BoxCollider>();
            originalBounds = box != null ? new Bounds(box.center, box.size) : new Bounds(new Vector3(0f, .08f, 0f), Vector3.one * .17f);
            visibleCleanliness = sourceWhole.VisibleWashProgress;
            flesh = sourceWhole.definition.kind == FoodKind.Apple ? new Color(1f, .90f, .64f) :
                sourceWhole.definition.kind == FoodKind.Lettuce ? new Color(.64f, .83f, .37f) : new Color(1f, .38f, .24f);
            // Old prefabs contain three decorative disks. They must never appear alongside the new pieces.
            foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        }

        public void SetStage(int stage)
        {
            if (!modelVisual) modelVisual = GetComponent<KitchenFoodModelVisual>();
            if (modelVisual && modelVisual.HasAuthoredCuts && stage >= 1)
            {
                Stage = Mathf.Clamp(stage, 1, 2);
                PieceCount = modelVisual.ApplyCutStage(Stage);
                return;
            }
            if (sources.Count == 0 || stage < 1) return;
            if (food == null) food = GetComponent<KitchenFood>();
            Stage = Mathf.Clamp(stage, 1, 2);
            PieceCount = Stage == 1 ? 2 : 4;
            if (pieces != null) { pieces.gameObject.SetActive(false); Destroy(pieces.gameObject); }
            foreach (var mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
            ownedMeshes.Clear();
            capRenderers.Clear();
            capFoodTint.Clear();
            pieces = new GameObject("Grouped cut pieces").transform;
            pieces.SetParent(transform, false);
            var tintedRenderers = new List<Renderer>();
            const float gap = 0.014f;
            for (int pieceIndex = 0; pieceIndex < PieceCount; pieceIndex++)
            {
                int xSign = pieceIndex % 2 == 0 ? -1 : 1;
                int zSign = Stage == 1 ? 0 : pieceIndex / 2 == 0 ? -1 : 1;
                var piece = new GameObject((Stage == 1 ? "Half " : "Quarter ") + (pieceIndex + 1)).transform;
                piece.SetParent(pieces, false);
                piece.localPosition = new Vector3(xSign * gap * .5f, 0f, zSign * gap * .5f);
                foreach (var source in sources)
                {
                    List<Triangle> clipped = Clip(source.triangles, Vector3.right * xSign, 0f);
                    if (Stage == 2) clipped = Clip(clipped, Vector3.forward * zSign, 0f);
                    if (clipped.Count == 0) continue;
                    Mesh mesh = BuildMesh(clipped);
                    ownedMeshes.Add(mesh);
                    var surface = new GameObject(source.name);
                    surface.transform.SetParent(piece, false);
                    surface.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = surface.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = new[] { source.material, source.material };
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    if (source.foodTint) tintedRenderers.Add(renderer);
                    capRenderers.Add(renderer);
                    capFoodTint.Add(source.foodTint);
                }
            }
            // One collider, Rigidbody, and XRGrabInteractable retain the whole group as one portion.
            var rootBox = GetComponent<BoxCollider>();
            if (rootBox != null)
            {
                rootBox.center = originalBounds.center;
                rootBox.size = originalBounds.size + new Vector3(gap, 0f, Stage == 2 ? gap : 0f);
            }
            if (food != null) food.visualRenderers = tintedRenderers.ToArray();
            GetComponent<KitchenContactStabilizer>()?.RefreshColliders();
            UpdateCapColors();
        }

        private void LateUpdate()
        {
            if (modelVisual && modelVisual.HasAuthoredCuts) return;
            if (food == null) return;
            visibleCleanliness = food.VisibleWashProgress;
            UpdateCapColors();
        }

        private void UpdateCapColors()
        {
            if (capColor == null) capColor = new MaterialPropertyBlock();
            for (int i = 0; i < capRenderers.Count; i++)
            {
                if (!capFoodTint[i] || capRenderers[i] == null) continue;
                Color color = Color.Lerp(flesh * .45f, flesh, visibleCleanliness);
                color.a = 1f;
                capColor.SetColor("_BaseColor", color);
                capColor.SetColor("_Color", color);
                // Per-material overrides preserve the lighter cut flesh while Food tints the skin.
                capRenderers[i].SetPropertyBlock(capColor, 1);
            }
        }

        private static List<Triangle> Clip(List<Triangle> input, Vector3 normal, float distance)
        {
            var result = new List<Triangle>();
            var edgePoints = new List<Vector3>();
            var polygon = new List<Vertex>(4);
            foreach (var triangle in input)
            {
                polygon.Clear();
                Vertex[] vertices = { triangle.a, triangle.b, triangle.c };
                for (int i = 0; i < 3; i++)
                {
                    Vertex a = vertices[i], b = vertices[(i + 1) % 3];
                    float da = Vector3.Dot(normal, a.position) - distance;
                    float db = Vector3.Dot(normal, b.position) - distance;
                    bool inA = da >= -0.000001f, inB = db >= -0.000001f;
                    if (inA) polygon.Add(a);
                    if (inA != inB)
                    {
                        Vertex crossing = Vertex.Lerp(a, b, da / (da - db));
                        polygon.Add(crossing);
                        AddUnique(edgePoints, crossing.position);
                    }
                    else if (Mathf.Abs(da) < 0.000001f) AddUnique(edgePoints, a.position);
                }
                for (int i = 1; i + 1 < polygon.Count; i++)
                    result.Add(new Triangle(polygon[0], polygon[i], polygon[i + 1], triangle.cap));
            }
            if (edgePoints.Count >= 3)
            {
                Vector3 center = Vector3.zero;
                foreach (var point in edgePoints) center += point;
                center /= edgePoints.Count;
                Vector3 u = Vector3.Cross(normal, Vector3.up).normalized;
                if (u.sqrMagnitude < .1f) u = Vector3.right;
                Vector3 v = Vector3.Cross(normal, u);
                Vector3 sortCenter = center;
                edgePoints.Sort((a, b) => Mathf.Atan2(Vector3.Dot(a - sortCenter, v), Vector3.Dot(a - sortCenter, u))
                    .CompareTo(Mathf.Atan2(Vector3.Dot(b - sortCenter, v), Vector3.Dot(b - sortCenter, u))));
                for (int i = 0; i < edgePoints.Count; i++)
                {
                    Vector3 a = edgePoints[i], b = edgePoints[(i + 1) % edgePoints.Count];
                    // Removed half is opposite normal, so its exposed cap faces -normal.
                    if (Vector3.Dot(Vector3.Cross(a - center, b - center), -normal) < 0f) { Vector3 swap = a; a = b; b = swap; }
                    Vertex Cap(Vector3 point) => new Vertex(point, -normal, new Vector2(Vector3.Dot(point, u), Vector3.Dot(point, v)));
                    result.Add(new Triangle(Cap(center), Cap(a), Cap(b), true));
                }
            }
            return result;
        }

        private static void AddUnique(List<Vector3> points, Vector3 point)
        {
            foreach (var existing in points) if ((point - existing).sqrMagnitude < 0.0000000001f) return;
            points.Add(point);
        }

        private static Mesh BuildMesh(List<Triangle> triangles)
        {
            var positions = new List<Vector3>(triangles.Count * 3);
            var normals = new List<Vector3>(triangles.Count * 3);
            var uvs = new List<Vector2>(triangles.Count * 3);
            var outside = new List<int>();
            var caps = new List<int>();
            foreach (var triangle in triangles)
            {
                List<int> indices = triangle.cap ? caps : outside;
                void Add(Vertex vertex)
                {
                    indices.Add(positions.Count);
                    positions.Add(vertex.position); normals.Add(vertex.normal); uvs.Add(vertex.uv);
                }
                Add(triangle.a); Add(triangle.b); Add(triangle.c);
            }
            var mesh = new Mesh { name = "Closed produce segment", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(positions); mesh.SetNormals(normals); mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(outside, 0); mesh.SetTriangles(caps, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            foreach (var mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
        }
    }
}
