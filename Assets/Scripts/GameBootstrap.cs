// ─────────────────────────────────────────────────────────────────────────────
// GameBootstrap — drop this on an empty GameObject in the Level1 scene.
// It disables the old hardcoded floor/checkpoint geometry, procedurally
// generates a cave terrain, and moves the EXISTING scene Player to the
// cave's spawn point. The Player, its Camera child, RopeHook prefab and all
// original PlayerController / WeaponManager / RopeLogic scripts are left
// untouched so the original (working) controls and physics keep functioning.
//
// SETUP:
//   1. Add an empty GameObject named "Bootstrap" to Level1
//   2. Attach this script
//   3. Press Play — change `seed` to explore different caves
// ─────────────────────────────────────────────────────────────────────────────
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameBootstrap : MonoBehaviour {
    [Header("Cave")]
    public int seed = 42;
    public int caveWidth = 260;
    public int caveHeight = 110;

    // The same seed always produces the same base cave; raising this adds more
    // saws/spikes/stalactites/water and (from 2+) moving saws and timed lasers on
    // top of that same layout, so one seed yields effectively infinite difficulty tiers.
    [Range(1, 10)] public int difficulty = 1;

    // Higher = wider, gentler ceiling waves, no jagged single-cell spikes.
    [Range(4, 40)] public int ceilingSmoothness = 14;

    [HideInInspector] public GameObject previewRoot;

    // Generates and renders the cave under a throwaway root object, purely for
    // looking at it in the Scene view — does not touch the real player or disable
    // any scene geometry, so it's safe to call outside Play mode.
    public void GeneratePreview() {
        ClearPreview();

        var scene = SceneManager.GetActiveScene();
        var before = scene.GetRootGameObjects();

        var root = new GameObject("__MapPreview");

        var gen = root.AddComponent<CaveGenerator>();
        gen.seed = seed;
        gen.width = caveWidth;
        gen.height = caveHeight;
        gen.difficulty = difficulty;
        gen.ceilingSmoothness = ceilingSmoothness;
        CaveData cave = gen.Generate();

        var rend = root.AddComponent<CaveRenderer>();
        rend.Render(cave);

        var decorator = root.AddComponent<CaveDecorator>();
        decorator.Decorate(cave);

        // Render/Decorate spawn plain top-level GameObjects (not parented to root) —
        // gather anything new and tuck it under root so ClearPreview can remove it all.
        var after = scene.GetRootGameObjects();
        foreach (var go in after) {
            if (go == root) continue;
            bool isNew = true;
            foreach (var b in before) if (b == go) { isNew = false; break; }
            if (isNew) go.transform.SetParent(root.transform, true);
        }

        previewRoot = root;
    }

    public void ClearPreview() {
        if (previewRoot != null) {
            DestroyImmediate(previewRoot);
            previewRoot = null;
        }
        var stray = GameObject.Find("__MapPreview");
        if (stray != null) DestroyImmediate(stray);
    }

    void Awake() {
        DisableOldFixedGeometry();

        var gen = gameObject.AddComponent<CaveGenerator>();
        gen.seed   = seed;
        gen.width  = caveWidth;
        gen.height = caveHeight;
        gen.difficulty = difficulty;
        gen.ceilingSmoothness = ceilingSmoothness;
        CaveData cave = gen.Generate();

        var rend = gameObject.AddComponent<CaveRenderer>();
        rend.Render(cave);

        var decorator = gameObject.AddComponent<CaveDecorator>();
        decorator.Decorate(cave);

        PlacePlayer(cave);
    }

    // Disable all of the old hardcoded level geometry — terrain ("Environment"),
    // the four boundary walls ("Controls"/"Controls1-4", which is what was trapping
    // the player at spawn since they weren't covered by the old "environment"/"floor"
    // name filter), the old kill-zone collider, and stray placeholder objects.
    // Checkpoints, the Player and its Camera child are left alone — PlayerStateManager
    // still relies on the checkpoint manager for respawn, so instead of disabling it
    // we just move checkpoint 0 below.
    static readonly string[] OldGeometryNameFragments = {
        "environment", "floor", "controls", "killzone", "killzones", "col_killz", "sphere"
    };

    void DisableOldFixedGeometry() {
        foreach (var go in FindObjectsByType<GameObject>(FindObjectsSortMode.None)) {
            if (go == this.gameObject) continue;
            if (go.CompareTag("Player")) continue;

            string n = go.name.ToLower();
            foreach (var fragment in OldGeometryNameFragments) {
                if (n.Contains(fragment)) {
                    go.SetActive(false);
                    break;
                }
            }
        }
    }

    void PlacePlayer(CaveData cave) {
        Vector3 spawnPos = new Vector3(cave.SpawnCell.x + 0.5f, cave.SpawnCell.y + 1.5f, 0f);

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player) {
            player.transform.position = spawnPos;
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb) rb.linearVelocity = Vector2.zero;
        }

        // Move checkpoint 0 to the new spawn point so PlayerStateManager.Respawn()
        // (which reads _checkpointmanager's checkPointArray[activeCP]) still works.
        var cpManagerGO = GameObject.Find("_checkpointmanager");
        var cpManager   = cpManagerGO ? cpManagerGO.GetComponent<CheckpointManager>() : null;
        if (cpManager != null && cpManager.checkPointArray.Count > 0)
            cpManager.checkPointArray[0].transform.position = spawnPos;
    }
}
