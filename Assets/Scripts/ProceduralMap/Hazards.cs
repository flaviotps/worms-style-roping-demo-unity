using UnityEngine;

// Super-Meat-Boy-style instant-death triggers. All rely on PlayerStateManager.KillPlayer(),
// the same respawn path the rest of the game already uses for checkpoints.

public class SawTrap : MonoBehaviour {
    public float rotateSpeed = 320f;

    void Update() {
        transform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag("Player")) return;
        var psm = other.GetComponent<PlayerStateManager>();
        if (psm) psm.KillPlayer();
    }
}

public class SpikeTrap : MonoBehaviour {
    void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag("Player")) return;
        var psm = other.GetComponent<PlayerStateManager>();
        if (psm) psm.KillPlayer();
    }
}

// A saw that rides back and forth along a rail between two world points
// (Super Meat Boy-style patrolling buzzsaw) instead of staying in one spot.
public class MovingSawTrap : MonoBehaviour {
    public Vector2 PointA, PointB;
    public float Speed = 4f;
    public float RotateSpeed = 320f;

    float length;

    void Start() {
        length = Vector2.Distance(PointA, PointB);
        if (length < 0.01f) length = 0.01f;
    }

    void Update() {
        transform.Rotate(0f, 0f, RotateSpeed * Time.deltaTime);
        float t = Mathf.PingPong(Time.time * Speed, length) / length;
        transform.position = Vector2.Lerp(PointA, PointB, t);
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag("Player")) return;
        var psm = other.GetComponent<PlayerStateManager>();
        if (psm) psm.KillPlayer();
    }
}

// A beam that toggles on/off — must be timed to cross (or routed around the gap left
// at the top/bottom of the band, see CaveGenerator.PlaceLasers). Only damages while in
// the "on" phase; the beam visual (a child object, since the beam is stretched via its
// own transform.localScale) follows the same clock as the trigger collider.
public class LaserTrap : MonoBehaviour {
    public float OnTime = 1.4f;
    public float OffTime = 1f;
    public float PhaseOffset = 0f;
    public SpriteRenderer BeamRenderer;

    Collider2D col;

    void Awake() {
        col = GetComponent<Collider2D>();
    }

    void Update() {
        float cycle = OnTime + OffTime;
        float t = (Time.time + PhaseOffset) % cycle;
        bool on = t < OnTime;
        if (BeamRenderer) BeamRenderer.enabled = on;
        if (col) col.enabled = on;
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag("Player")) return;
        var psm = other.GetComponent<PlayerStateManager>();
        if (psm) psm.KillPlayer();
    }
}

public class KillZone : MonoBehaviour {
    void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag("Player")) return;
        var psm = other.GetComponent<PlayerStateManager>();
        if (psm) psm.KillPlayer();
    }
}

// Moves the player's respawn point to this safe point when they pass through it.
// Repositions the existing checkpoint-0 marker rather than adding new entries to
// CheckpointManager's array, since that array's CheckpointLogic/material-swap code
// expects 3D mesh/light children our procedural markers don't have.
public class SafePointTrigger : MonoBehaviour {
    // Where the player should actually land on respawn (the flat platform), separate
    // from this trigger's transform which spans the full map height for x-crossing detection.
    public Vector3 RespawnPosition;

    void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag("Player")) return;
        var psm = other.GetComponent<PlayerStateManager>();
        if (!psm) return;

        var cpManagerGO = GameObject.Find("_checkpointmanager");
        var cpManager = cpManagerGO ? cpManagerGO.GetComponent<CheckpointManager>() : null;
        if (cpManager == null || cpManager.checkPointArray.Count == 0) return;

        cpManager.checkPointArray[0].transform.position = RespawnPosition;
        psm.activeCP = 0;
    }
}
