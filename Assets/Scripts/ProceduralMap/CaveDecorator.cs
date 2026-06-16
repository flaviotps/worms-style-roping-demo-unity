using UnityEngine;

// Cyberpunk/synthwave dressing (background, neon glows) plus the Super-Meat-Boy-style
// hazards (saws, spikes, kill floor) placed by CaveGenerator. Everything here is derived
// from the same CaveData the terrain was built from, so a given seed always reproduces
// the same map, hazards included.
public class CaveDecorator : MonoBehaviour {
    public void Decorate(CaveData data) {
        StyleCamera();
        SpawnBackdrop(data);
        SpawnSaws(data);
        SpawnSpikes(data);
        SpawnMovingSaws(data);
        SpawnLasers(data);
        SpawnSafePoints(data);
        SpawnWaterZones(data);
        SpawnKillFloor(data);
    }

    void StyleCamera() {
        var cam = Camera.main;
        if (!cam) return;
        cam.backgroundColor = new Color(0.04f, 0.02f, 0.1f);
        cam.clearFlags = CameraClearFlags.SolidColor;
    }

    void SpawnBackdrop(CaveData data) {
        var go = new GameObject("Backdrop");
        go.transform.position = new Vector3(data.Width / 2f, data.Height / 2f, 10f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeGradientSprite();
        sr.sortingOrder = -100;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(data.Width + 60, data.Height + 60);
    }

    Sprite MakeGradientSprite() {
        const int S = 64;
        var tex = new Texture2D(4, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color top = new Color(0.05f, 0.0f, 0.12f);
        Color mid = new Color(0.22f, 0.02f, 0.32f);
        Color bottom = new Color(0.45f, 0.08f, 0.3f);

        for (int y = 0; y < S; y++) {
            float t = y / (float)(S - 1);
            Color c = t < 0.6f ? Color.Lerp(top, mid, t / 0.6f) : Color.Lerp(mid, bottom, (t - 0.6f) / 0.4f);
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, 4, S), new Vector2(0.5f, 0.5f), 4, 0, SpriteMeshType.FullRect, Vector4.one);
    }

    Sprite MakeCoreSprite() {
        const int S = 16;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Vector2 c = new Vector2(S / 2f, S / 2f);
        float r = S / 2f - 1;
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++) {
            float d = Vector2.Distance(new Vector2(x, y), c);
            float a = d <= r ? 1f : Mathf.Clamp01(1f - (d - r));
            tex.SetPixel(x, y, new Color(1, 1, 1, a));
        }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }

    void SpawnSaws(CaveData data) {
        var armSprite = MakeArmSprite();
        var mountSprite = MakeMountSprite();

        foreach (var spot in data.SawSpots) {
            Vector2 toSaw = spot.Pos - spot.MountPoint;
            float length = Mathf.Max(toSaw.magnitude, 0.1f);
            float angle = Mathf.Atan2(toSaw.y, toSaw.x) * Mathf.Rad2Deg;

            var mount = new GameObject("SawMount");
            mount.transform.position = spot.MountPoint;
            var mountSr = mount.AddComponent<SpriteRenderer>();
            mountSr.sprite = mountSprite;
            mountSr.sortingOrder = 3;

            var arm = new GameObject("SawArm");
            arm.transform.position = spot.MountPoint;
            arm.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            arm.transform.localScale = new Vector3(length, 0.18f, 1f);
            var armSr = arm.AddComponent<SpriteRenderer>();
            armSr.sprite = armSprite;
            armSr.sortingOrder = 2;

            var go = new GameObject("Saw");
            go.transform.position = spot.Pos;
            go.layer = CaveRenderer.TerrainLayer;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeSawSprite();
            sr.color = Color.white; // colors are baked into the texture
            sr.sortingOrder = 4;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = SawWorldDiameter / 2f * 0.92f; // matches the blade's average tooth radius
            col.isTrigger = true;

            go.AddComponent<SawTrap>();
        }
    }

    const float SawWorldDiameter = 2f; // big, readable blade — not a tiny dot

    // A rusty metal rod, pivoted at its left edge so scaling localScale.x stretches
    // it from the wall mount point out to the saw without needing per-instance meshes.
    Sprite MakeArmSprite() {
        const int S = 8;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++) {
            float shade = 0.22f + 0.12f * (y / (float)S);
            tex.SetPixel(x, y, new Color(shade * 1.15f, shade * 0.85f, shade * 0.65f));
        }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0f, 0.5f), S);
    }

    Sprite MakeMountSprite() {
        const int S = 12;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
            tex.SetPixel(x, y, new Color(0.28f, 0.28f, 0.31f));
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }

    // A metal circular-saw blade with even triangular teeth, a dark hub and bolt.
    // Uses a smooth cosine for the tooth edge (continuous at the angle wraparound,
    // unlike a modulo-based wave) so the blade is a clean, perfectly round disc.
    Sprite MakeSawSprite() {
        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Vector2 c = new Vector2(S / 2f, S / 2f);
        float outerR = S / 2f - 1;
        const int teeth = 16;

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++) {
            Vector2 p = new Vector2(x, y) - c;
            float dist = p.magnitude;
            float angle = Mathf.Atan2(p.y, p.x);
            // Smooth, perfectly periodic tooth ripple — no seam at +-pi.
            float ripple = 0.5f + 0.5f * Mathf.Cos(angle * teeth);
            float edge = outerR * (0.82f + 0.18f * ripple);

            if (dist > edge) { tex.SetPixel(x, y, Color.clear); continue; }

            float hubR = outerR * 0.28f;
            float boltR = outerR * 0.09f;
            Color col;
            if (dist < boltR) col = new Color(0.12f, 0.12f, 0.13f);
            else if (dist < hubR) col = new Color(0.42f, 0.42f, 0.46f);
            else {
                float rim = Mathf.Clamp01((dist - hubR) / (edge - hubR));
                col = Color.Lerp(new Color(0.85f, 0.85f, 0.9f), new Color(0.6f, 0.08f, 0.06f), Mathf.Pow(rim, 3f));
            }
            tex.SetPixel(x, y, col);
        }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S / SawWorldDiameter);
    }

    // Patrolling saws that ride a rail between two points — same blade visual as the
    // stationary saws, no mount/arm since they aren't wall-fixed.
    void SpawnMovingSaws(CaveData data) {
        foreach (var spot in data.MovingSawSpots) {
            var go = new GameObject("MovingSaw");
            go.transform.position = spot.PointA;
            go.layer = CaveRenderer.TerrainLayer;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeSawSprite();
            sr.color = Color.white;
            sr.sortingOrder = 4;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = SawWorldDiameter / 2f * 0.92f;
            col.isTrigger = true;

            var trap = go.AddComponent<MovingSawTrap>();
            trap.PointA = spot.PointA;
            trap.PointB = spot.PointB;
            trap.Speed = spot.Speed;
        }
    }

    // A thin, pivot-bottom beam — stretching it via transform.localScale.y (instead of
    // SpriteDrawMode.Sliced, whose border insets clipped most of the beam's length out)
    // is what makes the full length actually render.
    Sprite MakeLaserBeamSprite() {
        const int W = 8, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++) {
            float cx = Mathf.Abs(x - (W - 1) / 2f) / (W / 2f);
            float a = Mathf.Clamp01(1f - cx);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
        }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), H, 0, SpriteMeshType.FullRect);
    }

    // Vertical laser gates that only cover part of the swing lane band (a gap is left
    // at the top or bottom — see CaveGenerator.PlaceLasers), so there's always a route
    // around even while the beam is "on"; toggling it adds a timing option/shortcut.
    void SpawnLasers(CaveData data) {
        var beamSprite = MakeLaserBeamSprite();
        var coreSprite = MakeCoreSprite();
        Color laserColor = new Color(1f, 0.15f, 0.2f);

        foreach (var spot in data.LaserSpots) {
            Vector2 bottom = spot.PointA.y <= spot.PointB.y ? spot.PointA : spot.PointB;
            Vector2 top    = spot.PointA.y <= spot.PointB.y ? spot.PointB : spot.PointA;
            float length = top.y - bottom.y;

            var go = new GameObject("Laser");
            go.transform.position = bottom;

            var beam = new GameObject("LaserBeam");
            beam.transform.SetParent(go.transform);
            beam.transform.localPosition = Vector3.zero;
            beam.transform.localScale = new Vector3(0.35f, length, 1f);
            var beamSr = beam.AddComponent<SpriteRenderer>();
            beamSr.sprite = beamSprite;
            beamSr.color = laserColor;
            beamSr.sortingOrder = 4;

            var nodeA = new GameObject("LaserNodeA");
            nodeA.transform.SetParent(go.transform);
            nodeA.transform.localPosition = Vector3.zero;
            nodeA.transform.localScale = Vector3.one * 0.3f;
            var nodeASr = nodeA.AddComponent<SpriteRenderer>();
            nodeASr.sprite = coreSprite;
            nodeASr.color = laserColor;
            nodeASr.sortingOrder = 5;

            var nodeB = new GameObject("LaserNodeB");
            nodeB.transform.SetParent(go.transform);
            nodeB.transform.localPosition = new Vector3(0f, length, 0f);
            nodeB.transform.localScale = Vector3.one * 0.3f;
            var nodeBSr = nodeB.AddComponent<SpriteRenderer>();
            nodeBSr.sprite = coreSprite;
            nodeBSr.color = laserColor;
            nodeBSr.sortingOrder = 5;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.offset = new Vector2(0f, length / 2f);
            col.size = new Vector2(0.5f, length);

            var laser = go.AddComponent<LaserTrap>();
            laser.BeamRenderer = beamSr;
            laser.OnTime = spot.OnTime;
            laser.OffTime = spot.OffTime;
            laser.PhaseOffset = spot.PhaseOffset;
        }
    }

    void SpawnSpikes(CaveData data) {
        foreach (var spot in data.SpikeSpots) {
            var go = new GameObject("Spike");
            go.transform.position = spot.Pos;
            go.transform.up = spot.Dir;
            go.layer = CaveRenderer.TerrainLayer;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeSpikeSprite();
            sr.color = new Color(0.95f, 0.1f, 0.95f);
            sr.sortingOrder = 4;

            var col = go.AddComponent<PolygonCollider2D>();
            col.isTrigger = true;
            col.points = new Vector2[] { new Vector2(-0.35f, -0.35f), new Vector2(0.35f, -0.35f), new Vector2(0f, 0.4f) };

            go.AddComponent<SpikeTrap>();
        }
    }

    Sprite MakeSpikeSprite() {
        const int S = 16;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++) {
            float nx = (x / (float)(S - 1)) * 2f - 1f; // -1..1
            float ny = y / (float)(S - 1);              // 0 (base) .. 1 (tip)
            bool inside = Mathf.Abs(nx) <= 1f - ny;
            tex.SetPixel(x, y, new Color(1, 1, 1, inside ? 1f : 0f));
        }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0f), S);
    }

    // Flat platforms marked by a thick cyan beacon beam spanning the entire map
    // height. The trigger covers that full height too, so simply crossing this
    // x position — even while swinging up near the ceiling — saves the checkpoint.
    // SafePointTrigger moves checkpoint 0's marker here on contact.
    void SpawnSafePoints(CaveData data) {
        var beamPrefab = Resources.Load<GameObject>("SafeBeam");
        var coreSprite = MakeCoreSprite();
        Color beaconColor = new Color(0.5f, 1f, 0.95f);

        foreach (var pos in data.SafeSpots) {
            var go = new GameObject("SafePoint");
            go.transform.position = new Vector3(pos.x, data.Height / 2f, 0f);

            // SafeBeam.prefab + SafeBeamVisual.cs — drag a sprite/color/width onto the
            // prefab in the Inspector to art-direct the beacon without touching this code.
            GameObject beam = beamPrefab ? Object.Instantiate(beamPrefab) : new GameObject("SafeBeam");
            beam.name = "SafeBeam";
            beam.transform.SetParent(go.transform);
            beam.transform.localPosition = new Vector3(0f, -data.Height / 2f, 0f);

            var beamVisual = beam.GetComponent<SafeBeamVisual>();
            if (beamVisual) beamVisual.SetHeight(data.Height);
            else beam.transform.localScale = new Vector3(3.2f, data.Height, 1f);

            var dot = new GameObject("SafeDot");
            dot.transform.SetParent(go.transform);
            dot.transform.localPosition = new Vector3(0f, data.Height / 2f - 1.5f, 0f);
            dot.transform.localScale = Vector3.one * 0.45f;
            var dotSr = dot.AddComponent<SpriteRenderer>();
            dotSr.sprite = coreSprite;
            dotSr.color = beaconColor;
            dotSr.sortingOrder = 5;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(3.2f, data.Height);

            var trigger = go.AddComponent<SafePointTrigger>();
            trigger.RespawnPosition = new Vector3(pos.x, pos.y + 0.6f, 0f);
        }
    }

    // Translucent water covering long flat ground stretches — stepping in it sends
    // the player back to their last checkpoint, the same as any other hazard.
    void SpawnWaterZones(CaveData data) {
        var sprite = MakeWaterSprite();
        foreach (var zone in data.WaterSpots) {
            float width = zone.EndX - zone.StartX;
            var go = new GameObject("Water");
            go.transform.position = new Vector3(zone.StartX + width / 2f, zone.Y + 0.9f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled; // tiles the body+wave texture, no border-slicing artifacts
            sr.size = new Vector2(width, 1.8f);
            sr.sortingOrder = 1;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(width, 1.8f);

            go.AddComponent<KillZone>();
        }
    }

    // A tileable water texture — bright wavy surface line on top, translucent body
    // fading downward — instead of a single flat-tinted rectangle.
    Sprite MakeWaterSprite() {
        const int W = 32, H = 16;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;

        Color surface = new Color(0.55f, 0.95f, 1f, 0.9f);
        Color shallow = new Color(0.15f, 0.55f, 0.85f, 0.6f);
        Color deep    = new Color(0.05f, 0.2f, 0.45f, 0.75f);

        for (int y = 0; y < H; y++) {
            float depth = 1f - y / (float)(H - 1); // 1 at top, 0 at bottom
            for (int x = 0; x < W; x++) {
                float wave = Mathf.Sin((x / (float)W) * Mathf.PI * 4f) * 1.5f;
                float surfaceY = H - 2f + wave;
                float distToSurface = Mathf.Abs(y - surfaceY);

                Color c = Color.Lerp(deep, shallow, depth);
                if (distToSurface < 1.2f)
                    c = Color.Lerp(c, surface, 1f - distToSurface / 1.2f);

                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
    }

    void SpawnKillFloor(CaveData data) {
        var go = new GameObject("KillFloor");
        go.transform.position = new Vector3(data.Width / 2f, -6f, 0f);

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(data.Width + 80, 4f);

        go.AddComponent<KillZone>();
    }
}
