using System.Collections.Generic;
using UnityEngine;

public class CaveData {
    public bool[,] Grid; // true = solid
    public int Width, Height;
    public Vector2Int SpawnCell;
    public List<SawSpot> SawSpots = new List<SawSpot>();
    public List<SpikeSpot> SpikeSpots = new List<SpikeSpot>();
    public List<Vector2> SafeSpots = new List<Vector2>(); // flat checkpoint platforms
    public List<WaterSpot> WaterSpots = new List<WaterSpot>(); // ground hazards that send you back to your last checkpoint
    public List<MovingSawSpot> MovingSawSpots = new List<MovingSawSpot>(); // saws that ride back and forth on a rail
    public List<LaserSpot> LaserSpots = new List<LaserSpot>(); // gates that toggle on/off, must be timed
    public int Difficulty = 1;
}

public struct MovingSawSpot {
    public Vector2 PointA, PointB;
    public float Speed;
    public MovingSawSpot(Vector2 a, Vector2 b, float speed) { PointA = a; PointB = b; Speed = speed; }
}

public struct LaserSpot {
    public Vector2 PointA, PointB;
    public float OnTime, OffTime, PhaseOffset;
    public LaserSpot(Vector2 a, Vector2 b, float onTime, float offTime, float phase) {
        PointA = a; PointB = b; OnTime = onTime; OffTime = offTime; PhaseOffset = phase;
    }
}

public struct WaterSpot {
    public float StartX, EndX, Y;
    public WaterSpot(float startX, float endX, float y) { StartX = startX; EndX = endX; Y = y; }
}

public struct SawSpot {
    public Vector2 Pos;
    public Vector2 MountPoint; // nearest wall point the saw's mounting arm attaches to
    public SawSpot(Vector2 pos, Vector2 mount) { Pos = pos; MountPoint = mount; }
}

public struct SpikeSpot {
    public Vector2 Pos;
    public Vector2 Dir; // outward-facing normal, away from the wall it's mounted on
    public SpikeSpot(Vector2 pos, Vector2 dir) { Pos = pos; Dir = dir; }
}

public class CaveGenerator : MonoBehaviour {
    [Header("Seed & Size")]
    public int seed = 42;
    public int width = 420;
    public int height = 72;

    [Header("Hazards (0 = auto-scale with width)")]
    public int sawCount = 0;
    public int spikeCount = 0;

    // Higher difficulty = denser hazards (more saws/spikes/stalactites, longer water,
    // plus moving saws and timed lasers that only appear at difficulty 2+). The same
    // seed always produces the same layout for a given difficulty, but raising the
    // difficulty on that same seed adds more obstacles to dodge — so one seed yields
    // effectively infinite difficulty tiers.
    [Header("Difficulty (1 = easiest)")]
    [Range(1, 10)] public int difficulty = 1;

    [Header("Generation")]
    [Range(35, 52)]
    public int fillPercent = 44;
    public int smoothIterations = 5;
    public int stalactiteCount = 0; // 0 = auto-scale with width

    // Higher = wider, gentler ceiling waves with no single-cell spikes. Lower = tighter,
    // more jagged waves (still smooth, never a 1-cell sawtooth like before).
    [Header("Ceiling smoothness (cells per wave — higher = smoother)")]
    [Range(4, 40)] public int ceilingSmoothness = 14;

    [Header("Swing lane (kept clear so there's room to swing)")]
    [Range(0.1f, 0.45f)] public float swingLaneBottom = 0.20f;
    [Range(0.5f, 0.85f)] public float swingLaneTop    = 0.80f;

    // Both must give enough time to react/cross — difficulty only shortens the "on"
    // window down to a floor, it never removes the "off" window entirely.
    [Header("Lasers (difficulty 2+)")]
    public float laserOnTime = 1.4f;
    public float laserOffTime = 1.3f;

    int spawnRoomX, spawnRoomY, spawnRoomW = 14, spawnRoomH = 10;

    // Scales hazard counts/severity with difficulty. 1.0 at difficulty 1, +40% per tier above.
    float DifficultyMultiplier() => 1f + (Mathf.Max(1, difficulty) - 1) * 0.4f;

    public CaveData Generate() {
        difficulty = Mathf.Max(1, difficulty);
        var rng = new System.Random(seed);
        bool[,] grid = InitGrid(rng);

        for (int i = 0; i < smoothIterations; i++)
            grid = Smooth(grid);

        FlattenCeiling(grid, rng);

        int count = Mathf.RoundToInt((stalactiteCount > 0 ? stalactiteCount : width / 3.5f) * DifficultyMultiplier());

        AddStalactites(grid, rng, count);
        AddStalagmites(grid, rng, count);
        CarveSwingLane(grid, rng);
        ClearSpawnArea(grid);
        List<Vector2> safeSpots = PlaceSafePoints(grid, rng);

        return new CaveData {
            Grid = grid,
            Width = width,
            Height = height,
            SpawnCell = FindSpawn(grid),
            SawSpots = PlaceSaws(grid, rng, safeSpots),
            SpikeSpots = PlaceSpikes(grid, rng, safeSpots),
            SafeSpots = safeSpots,
            WaterSpots = FindWaterZones(grid, safeSpots),
            MovingSawSpots = PlaceMovingSaws(grid, rng, safeSpots),
            LaserSpots = PlaceLasers(grid, rng, safeSpots),
            Difficulty = difficulty
        };
    }

    // Long flat stretches of open ground beneath the swing lane get covered in
    // water — pushes the player to stay up top swinging instead of walking the
    // floor. Skips the spawn room and safe-point platforms.
    List<WaterSpot> FindWaterZones(bool[,] grid, List<Vector2> safeSpots) {
        var zones = new List<WaterSpot>();
        int bandLow = Mathf.RoundToInt(height * swingLaneBottom);

        int x = 2;
        while (x < width - 2) {
            if (x >= spawnRoomX - 2 && x <= spawnRoomX + spawnRoomW + 2) { x++; continue; }
            if (NearSafeSpotX(safeSpots, x, 6f)) { x++; continue; }

            int surf = GroundSurfaceY(grid, x, bandLow);
            if (surf < 0) { x++; continue; }

            int startX = x, y0 = surf, runLen = 1;
            x++;
            while (x < width - 2) {
                if (x >= spawnRoomX - 2 && x <= spawnRoomX + spawnRoomW + 2) break;
                if (NearSafeSpotX(safeSpots, x, 6f)) break;
                int s2 = GroundSurfaceY(grid, x, bandLow);
                if (s2 < 0 || Mathf.Abs(s2 - y0) > 1) break;
                runLen++; x++;
            }

            int minRun = Mathf.Max(4, 10 - (difficulty - 1)); // higher difficulty = shorter ground stretches stay safe
            if (runLen >= minRun) zones.Add(new WaterSpot(startX, startX + runLen, y0));
        }
        return zones;
    }

    // Topmost solid cell below the swing lane that has open space directly above it.
    int GroundSurfaceY(bool[,] grid, int x, int bandLow) {
        for (int y = bandLow - 1; y >= 1; y--)
            if (grid[x, y] && !grid[x, y + 1]) return y;
        return -1;
    }

    // Flat little platforms spaced along the level, lit by a vertical light beam —
    // safe checkpoints the player can land on and respawn from.
    List<Vector2> PlaceSafePoints(bool[,] grid, System.Random rng) {
        var spots = new List<Vector2>();
        int bandLow  = Mathf.RoundToInt(height * swingLaneBottom) + 1;
        int bandHigh = Mathf.RoundToInt(height * swingLaneTop) - 1;
        const int platW = 6, platH = 4;
        const int spacing = 55;

        int x = spawnRoomX + spawnRoomW + 35;
        while (x < width - platW - 6) {
            int yRange = Mathf.Max(1, bandHigh - bandLow - platH);
            int y = bandLow + rng.Next(0, yRange);

            for (int dx = 0; dx < platW; dx++)
            for (int dy = 0; dy < platH; dy++)
                if (x + dx < width - 1 && y + dy < height - 1) grid[x + dx, y + dy] = false;

            for (int dx = 0; dx < platW; dx++)
                if (x + dx < width - 1 && y - 1 > 0) grid[x + dx, y - 1] = true;

            spots.Add(new Vector2(x + platW / 2f, y));
            x += spacing + rng.Next(-10, 11);
        }
        return spots;
    }

    bool NearSafeSpot(List<Vector2> safeSpots, float x, float y, float minDist) {
        foreach (var s in safeSpots)
            if ((s - new Vector2(x, y)).sqrMagnitude < minDist * minDist) return true;
        return false;
    }

    bool NearSafeSpotX(List<Vector2> safeSpots, float x, float minDist) {
        foreach (var s in safeSpots)
            if (Mathf.Abs(s.x - x) < minDist) return true;
        return false;
    }

    // Rotating saws need an open pocket of space to spin in — pick spots in the
    // swing lane with clearance on all sides, spaced apart so they don't cluster.
    List<SawSpot> PlaceSaws(bool[,] grid, System.Random rng, List<Vector2> safeSpots) {
        var spots = new List<SawSpot>();
        int target = Mathf.RoundToInt((sawCount > 0 ? sawCount : Mathf.Max(4, width / 35)) * DifficultyMultiplier());
        int bandLow  = Mathf.RoundToInt(height * swingLaneBottom) + 1;
        int bandHigh = Mathf.RoundToInt(height * swingLaneTop) - 1;
        int minX = spawnRoomX + spawnRoomW + 12;
        int attempts = 0;

        while (spots.Count < target && attempts < target * 120 && minX < width - 4 && bandHigh > bandLow) {
            attempts++;
            int x = minX + BiasTowardsEnd(rng, width - 4 - minX);
            int y = rng.Next(bandLow, bandHigh);
            if (!IsOpenClear(grid, x, y, 2)) continue; // 2-cell clearance for the larger 2-unit blade
            if (NearSafeSpot(safeSpots, x, y, 7f)) continue;

            bool tooClose = false;
            foreach (var s in spots)
                if ((s.Pos - new Vector2(x, y)).sqrMagnitude < 9 * 9) { tooClose = true; break; }
            if (tooClose) continue;

            Vector2 pos = new Vector2(x + 0.5f, y + 0.5f);
            spots.Add(new SawSpot(pos, FindNearestWallPoint(grid, x, y, 10)));
        }
        return spots;
    }

    // Saws that ride back and forth along a short horizontal rail through open space —
    // only appear from difficulty 2 up, since timing a moving threat is harder than
    // dodging a stationary one.
    List<MovingSawSpot> PlaceMovingSaws(bool[,] grid, System.Random rng, List<Vector2> safeSpots) {
        var spots = new List<MovingSawSpot>();
        if (difficulty < 2) return spots;

        int target = Mathf.RoundToInt((difficulty - 1) * Mathf.Max(1f, width / 90f));
        int bandLow  = Mathf.RoundToInt(height * swingLaneBottom) + 1;
        int bandHigh = Mathf.RoundToInt(height * swingLaneTop) - 1;
        int minX = spawnRoomX + spawnRoomW + 15;
        int attempts = 0;

        while (spots.Count < target && attempts < target * 150 && minX < width - 20 && bandHigh > bandLow) {
            attempts++;
            int travel = rng.Next(6, 14);
            int x = minX + BiasTowardsEnd(rng, width - 20 - minX - travel);
            int y = rng.Next(bandLow, bandHigh);
            if (!IsOpenClear(grid, x, y, 2) || !IsOpenClear(grid, x + travel, y, 2)) continue;

            bool pathClear = true;
            for (int sx = x; sx <= x + travel; sx += 2)
                if (!IsOpenClear(grid, sx, y, 1)) { pathClear = false; break; }
            if (!pathClear) continue;

            if (NearSafeSpot(safeSpots, x, y, 8f) || NearSafeSpot(safeSpots, x + travel, y, 8f)) continue;

            bool tooClose = false;
            foreach (var s in spots)
                if (Mathf.Abs(s.PointA.x - x) < 12f) { tooClose = true; break; }
            if (tooClose) continue;

            float speed = 3f + (float)rng.NextDouble() * 3f;
            spots.Add(new MovingSawSpot(new Vector2(x + 0.5f, y + 0.5f), new Vector2(x + travel + 0.5f, y + 0.5f), speed));
        }
        return spots;
    }

    // Vertical laser gates that toggle on/off through the swing lane — must be timed
    // to cross. Only appear from difficulty 2 up.
    List<LaserSpot> PlaceLasers(bool[,] grid, System.Random rng, List<Vector2> safeSpots) {
        var spots = new List<LaserSpot>();
        if (difficulty < 2) return spots;

        int target = Mathf.RoundToInt((difficulty - 1) * Mathf.Max(1f, width / 70f));
        int bandLow  = Mathf.RoundToInt(height * swingLaneBottom) + 1;
        int bandHigh = Mathf.RoundToInt(height * swingLaneTop) - 1;
        int minX = spawnRoomX + spawnRoomW + 15;
        int attempts = 0;

        while (spots.Count < target && attempts < target * 150 && minX < width - 4 && bandHigh > bandLow) {
            attempts++;
            int x = minX + BiasTowardsEnd(rng, width - 4 - minX);
            int yMid = (bandLow + bandHigh) / 2;
            if (!IsOpenClear(grid, x, yMid, 1)) continue;
            if (NearSafeSpotX(safeSpots, x, 8f)) continue;

            bool tooClose = false;
            foreach (var s in spots)
                if (Mathf.Abs(s.PointA.x - x) < 10f) { tooClose = true; break; }
            if (tooClose) continue;

            // Leave a permanent gap at the top or bottom of the band so there's always a
            // route around the beam, even while it's "on" — timing it is a shortcut/skill
            // play, not the only way through.
            int bandSpan = bandHigh - bandLow;
            int gap = Mathf.Clamp(Mathf.RoundToInt(bandSpan * 0.3f), 3, 12);
            bool gapAtTop = rng.Next(2) == 0;
            Vector2 a = gapAtTop ? new Vector2(x + 0.5f, bandLow) : new Vector2(x + 0.5f, bandLow + gap);
            Vector2 b = gapAtTop ? new Vector2(x + 0.5f, bandHigh - gap) : new Vector2(x + 0.5f, bandHigh);

            float phase = (float)rng.NextDouble() * 3f;
            float onTime = Mathf.Max(0.6f, laserOnTime - (difficulty - 2) * 0.1f);
            spots.Add(new LaserSpot(a, b, onTime, laserOffTime, phase));
        }
        return spots;
    }

    // Skews random picks toward the high end of [0, range) so hazards get denser
    // closer to the level's end, mirroring the existing stalactite difficulty ramp.
    int BiasTowardsEnd(System.Random rng, int range) {
        if (range <= 0) return 0;
        double u = rng.NextDouble();
        return Mathf.Clamp(Mathf.RoundToInt((float)System.Math.Pow(u, 0.6) * range), 0, range);
    }

    // Walks outward from (x,y) along the 4 cardinal directions to find the nearest
    // solid wall cell, returning a world point on its surface for a mounting arm.
    Vector2 FindNearestWallPoint(bool[,] grid, int x, int y, int maxR) {
        for (int r = 1; r <= maxR; r++) {
            if (x - r >= 0 && grid[x - r, y]) return new Vector2(x - r + 1f, y + 0.5f);
            if (x + r < width && grid[x + r, y]) return new Vector2(x + r, y + 0.5f);
            if (y - r >= 0 && grid[x, y - r]) return new Vector2(x + 0.5f, y - r + 1f);
            if (y + r < height && grid[x, y + r]) return new Vector2(x + 0.5f, y + r);
        }
        return new Vector2(x + 0.5f, y + 0.5f);
    }

    bool IsOpenClear(bool[,] grid, int x, int y, int radius) {
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++) {
            int nx = x + dx, ny = y + dy;
            if (nx < 0 || nx >= width || ny < 0 || ny >= height) return false;
            if (grid[nx, ny]) return false;
        }
        return true;
    }

    // Wall-mounted spikes — pick solid cells that have an open neighbour (a visible
    // wall face), facing the spike outward into that open space.
    List<SpikeSpot> PlaceSpikes(bool[,] grid, System.Random rng, List<Vector2> safeSpots) {
        var spots = new List<SpikeSpot>();
        int target = Mathf.RoundToInt((spikeCount > 0 ? spikeCount : Mathf.Max(6, width / 12)) * DifficultyMultiplier());
        int minX = spawnRoomX + spawnRoomW + 10;
        int attempts = 0;

        while (spots.Count < target && attempts < target * 60 && minX < width - 2) {
            attempts++;
            int x = minX + BiasTowardsEnd(rng, width - 2 - minX);
            int y = rng.Next(2, height - 2);
            if (!grid[x, y]) continue;
            if (NearSafeSpot(safeSpots, x, y, 6f)) continue;

            Vector2 dir;
            if (!grid[x + 1, y]) dir = Vector2.right;
            else if (!grid[x - 1, y]) dir = Vector2.left;
            else if (!grid[x, y + 1]) dir = Vector2.up;
            else if (!grid[x, y - 1]) dir = Vector2.down;
            else continue;

            bool tooClose = false;
            foreach (var s in spots)
                if ((s.Pos - new Vector2(x, y)).sqrMagnitude < 4 * 4) { tooClose = true; break; }
            if (tooClose) continue;

            spots.Add(new SpikeSpot(new Vector2(x + 0.5f, y + 0.5f), dir));
        }
        return spots;
    }

    bool[,] InitGrid(System.Random rng) {
        var grid = new bool[width, height];
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++) {
            bool border = x == 0 || x == width - 1 || y == 0 || y == height - 1;
            grid[x, y] = border || rng.Next(100) < fillPercent;
        }
        return grid;
    }

    bool[,] Smooth(bool[,] grid) {
        var next = new bool[width, height];
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++) {
            bool border = x == 0 || x == width - 1 || y == 0 || y == height - 1;
            if (border) { next[x, y] = true; continue; }
            int n = CountSolidNeighbors(grid, x, y);
            next[x, y] = n > 4 || (n == 4 && grid[x, y]);
        }
        return next;
    }

    int CountSolidNeighbors(bool[,] grid, int x, int y) {
        int count = 0;
        for (int nx = x - 1; nx <= x + 1; nx++)
        for (int ny = y - 1; ny <= y + 1; ny++) {
            if (nx == x && ny == y) continue;
            if (nx < 0 || nx >= width || ny < 0 || ny >= height) count++;
            else if (grid[nx, ny]) count++;
        }
        return count;
    }

    // Replace the chaotic cellular-automata noise above the swing lane with a gently
    // wobbling roofline (small random walk instead of per-cell noise), so the cave's
    // top reads as a mostly-flat ceiling rather than jagged rock. Stalactites are
    // carved down from this roofline afterward.
    void FlattenCeiling(bool[,] grid, System.Random rng) {
        int bandHigh = Mathf.RoundToInt(height * swingLaneTop);
        int baseline = bandHigh + Mathf.RoundToInt((height - bandHigh) * 0.35f);
        const float amplitude = 2f;

        // Pick height keyframes every `ceilingSmoothness` columns and smoothstep-interpolate
        // between them, instead of nudging the height by +/-1 every single column (which
        // produced a sawtooth of 1-cell spikes). Bigger smoothness = wider, gentler waves.
        int segment = Mathf.Max(2, ceilingSmoothness);
        int numKeys = Mathf.CeilToInt((float)width / segment) + 2;
        var keys = new float[numKeys];
        float prevKey = 0f;
        for (int i = 0; i < numKeys; i++) {
            prevKey = Mathf.Clamp(prevKey + (float)(rng.NextDouble() * 2f - 1f) * amplitude, -amplitude, amplitude);
            keys[i] = prevKey;
        }

        for (int x = 1; x < width - 1; x++) {
            float pos = (float)x / segment;
            int k0 = Mathf.Clamp(Mathf.FloorToInt(pos), 0, numKeys - 1);
            int k1 = Mathf.Min(k0 + 1, numKeys - 1);
            float frac = pos - k0;
            float smooth = frac * frac * (3f - 2f * frac); // smoothstep
            float offset = Mathf.Lerp(keys[k0], keys[k1], smooth);
            int ceilingY = Mathf.Clamp(baseline + Mathf.RoundToInt(offset), bandHigh + 2, height - 3);

            for (int y = bandHigh; y < ceilingY; y++)
                grid[x, y] = false;
            for (int y = ceilingY; y < height - 1; y++)
                grid[x, y] = true;
        }
    }

    // Thin stalactites from ceiling — great rope-wrap anchors. They grow longer and
    // denser the further right (closer to the level's end) to ramp up difficulty.
    void AddStalactites(bool[,] grid, System.Random rng, int count) {
        for (int i = 0; i < count; i++) {
            int x = rng.Next(4, width - 4);
            float progress = (float)x / width; // 0 = start, 1 = end of level

            // Skip some near the start so the opening area is easy
            if (progress < 0.08f && rng.Next(100) < 70) continue;

            for (int y = height - 2; y > height / 2; y--) {
                if (grid[x, y] && !grid[x, y - 1]) {
                    int minLen = 3 + Mathf.RoundToInt(progress * 5f);
                    int maxLen = 7 + Mathf.RoundToInt(progress * 14f);
                    int len = rng.Next(minLen, maxLen);
                    for (int d = 0; d < len && y - 1 - d > 1; d++)
                        grid[x, y - 1 - d] = true;
                    break;
                }
            }
        }
    }

    void AddStalagmites(bool[,] grid, System.Random rng, int stalactiteCountRef) {
        int count = stalactiteCountRef / 2;
        for (int i = 0; i < count; i++) {
            int x = rng.Next(4, width - 4);
            for (int y = 1; y < height / 2; y++) {
                if (grid[x, y] && !grid[x, y + 1]) {
                    int len = rng.Next(2, 6);
                    for (int d = 0; d < len && y + 1 + d < height - 1; d++)
                        grid[x, y + 1 + d] = true;
                    break;
                }
            }
        }
    }

    // Carve a mostly-open horizontal band through the middle of the cave so there's
    // generous open space to build up swing momentum, with only sparse partial
    // pillars (good rope anchors) instead of dense clutter.
    void CarveSwingLane(bool[,] grid, System.Random rng) {
        int bandLow  = Mathf.RoundToInt(height * swingLaneBottom);
        int bandHigh = Mathf.RoundToInt(height * swingLaneTop);
        int bandH    = bandHigh - bandLow;
        if (bandH <= 2) return;

        for (int x = 2; x < width - 2; x++)
        for (int y = bandLow; y < bandHigh; y++)
            grid[x, y] = false;

        const int pillarSpacing = 20;
        for (int x = pillarSpacing; x < width - pillarSpacing; x += pillarSpacing) {
            int px = Mathf.Clamp(x + rng.Next(-5, 6), 4, width - 5);
            int len = rng.Next(2, Mathf.Max(3, bandH - 5));
            bool fromTop = rng.Next(2) == 0;
            for (int d = 0; d < len; d++) {
                int y = fromTop ? bandHigh - 1 - d : bandLow + d;
                if (y > 0 && y < height - 1) grid[px, y] = true;
            }
        }
    }

    // Guarantee an open chamber with a solid floor at the spawn area. Placed inside
    // the open swing-lane band (not up near the ceiling) so it's naturally connected
    // to the rest of the carved-open cave instead of becoming a sealed box.
    void ClearSpawnArea(bool[,] grid) {
        int bandLow = Mathf.RoundToInt(height * swingLaneBottom);

        spawnRoomX = 10;
        spawnRoomY = bandLow + 2;

        // Carve out the room
        for (int x = spawnRoomX; x < spawnRoomX + spawnRoomW && x < width - 1; x++)
        for (int y = spawnRoomY; y < spawnRoomY + spawnRoomH && y < height - 1; y++)
            grid[x, y] = false;

        // Solid floor directly beneath the room so the player has something to stand on
        for (int x = spawnRoomX; x < spawnRoomX + spawnRoomW && x < width - 1; x++)
            if (spawnRoomY - 1 > 0) grid[x, spawnRoomY - 1] = true;
    }

    Vector2Int FindSpawn(bool[,] grid) {
        // Center of the cleared, floored spawn room — guaranteed open with solid ground below
        return new Vector2Int(spawnRoomX + spawnRoomW / 2, spawnRoomY);
    }
}
