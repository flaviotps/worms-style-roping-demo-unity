using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;

public class CaveRenderer : MonoBehaviour {
    // Use Default layer (0) so no custom layer naming is required in Project Settings
    public const int TerrainLayer = 0;

    public void Render(CaveData data) {
        var gridGO = new GameObject("CaveGrid");
        var grid = gridGO.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 1f);

        var tilemapGO = new GameObject("Terrain");
        tilemapGO.transform.SetParent(gridGO.transform);
        tilemapGO.layer = TerrainLayer;

        // "Hookable" tag must exist in Project Settings/Tags for rope to latch
        tilemapGO.tag = "Hookable";

        var tilemap = tilemapGO.AddComponent<Tilemap>();
        var tmr = tilemapGO.AddComponent<TilemapRenderer>();
        tmr.sortingOrder = 0;

        var tc = tilemapGO.AddComponent<TilemapCollider2D>();
        tc.usedByComposite = true;

        var rb = tilemapGO.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        var cc = tilemapGO.AddComponent<CompositeCollider2D>();
        cc.geometryType = CompositeCollider2D.GeometryType.Polygons;

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = MakeRockSprite();
        tile.colliderType = Tile.ColliderType.Grid;

        for (int x = 0; x < data.Width; x++)
        for (int y = 0; y < data.Height; y++) {
            if (!data.Grid[x, y]) continue;
            tilemap.SetTile(new Vector3Int(x, y, 0), tile);
        }

        SpawnOutline(data, gridGO.transform);
    }

    // Solid fill with a subtle top-lit gradient — readability over texture detail.
    // Deliberately lighter/bluer than the backdrop's darkest tone (which a near-black
    // fill matched almost exactly, making solid rock read as a hollow outline against
    // it) so terrain mass is unmistakably solid at every height in the level.
    Sprite MakeRockSprite() {
        const int S = 16;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color top = new Color(0.16f, 0.13f, 0.24f);
        Color bottom = new Color(0.08f, 0.06f, 0.15f);
        for (int y = 0; y < S; y++) {
            Color c = Color.Lerp(bottom, top, y / (float)(S - 1));
            for (int x = 0; x < S; x++)
                tex.SetPixel(x, y, c);
        }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }

    // Builds two combined meshes tracing every solid/open boundary in the grid: a
    // wide, faded "glow" pass behind a crisp, full-opacity line pass on top — cheap
    // stand-in for bloom that reads as an intentional neon light source rather than
    // a flat line. Built straight from the grid (not the physics collider) so it's
    // available immediately, including in Editor preview.
    void SpawnOutline(CaveData data, Transform parent) {
        // One color per solid run along x, chosen from a coarse low-frequency noise
        // instead of per-edge modulo — the old version recolored individual edges
        // almost at random, scattering single magenta pixels across an otherwise
        // cyan cave that read as noisy/broken rather than deliberate.
        Color cyan = new Color(0.4f, 0.85f, 1f);
        Color magenta = new Color(0.95f, 0.3f, 0.95f);
        Color EdgeColor(int x) => (Mathf.PerlinNoise(x * 0.04f, 7.31f) > 0.82f) ? magenta : cyan;

        BuildOutlineMesh(data, parent, "TerrainGlow", 0.35f, 0.22f, 5, EdgeColor);
        BuildOutlineMesh(data, parent, "TerrainOutline", 0.07f, 1f, 6, EdgeColor);
    }

    void BuildOutlineMesh(CaveData data, Transform parent, string name, float thickness, float alpha, int sortingOrder, System.Func<int, Color> colorAt) {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var cols = new List<Color>();

        void AddEdge(Vector2 a, Vector2 b, Color col) {
            Vector2 dir = (b - a).normalized;
            Vector2 n = new Vector2(-dir.y, dir.x) * (thickness / 2f);
            int vi = verts.Count;
            verts.Add(new Vector3(a.x - n.x, a.y - n.y, 0f));
            verts.Add(new Vector3(a.x + n.x, a.y + n.y, 0f));
            verts.Add(new Vector3(b.x + n.x, b.y + n.y, 0f));
            verts.Add(new Vector3(b.x - n.x, b.y - n.y, 0f));
            tris.Add(vi); tris.Add(vi + 1); tris.Add(vi + 2);
            tris.Add(vi); tris.Add(vi + 2); tris.Add(vi + 3);
            cols.Add(col); cols.Add(col); cols.Add(col); cols.Add(col);
        }

        for (int x = 0; x < data.Width; x++)
        for (int y = 0; y < data.Height; y++) {
            if (!data.Grid[x, y]) continue;
            Color col = colorAt(x);
            col.a = alpha;

            if (x + 1 >= data.Width || !data.Grid[x + 1, y]) AddEdge(new Vector2(x + 1, y), new Vector2(x + 1, y + 1), col);
            if (x - 1 < 0 || !data.Grid[x - 1, y])           AddEdge(new Vector2(x, y + 1), new Vector2(x, y), col);
            if (y + 1 >= data.Height || !data.Grid[x, y + 1]) AddEdge(new Vector2(x, y + 1), new Vector2(x + 1, y + 1), col);
            if (y - 1 < 0 || !data.Grid[x, y - 1])           AddEdge(new Vector2(x + 1, y), new Vector2(x, y), col);
        }

        if (verts.Count == 0) return;

        var go = new GameObject(name);
        go.transform.SetParent(parent);

        var mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetColors(cols);
        mesh.RecalculateBounds();

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        mr.sortingOrder = sortingOrder;
    }
}
