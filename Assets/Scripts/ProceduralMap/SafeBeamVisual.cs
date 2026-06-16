using UnityEngine;

// Attach to the "Beam" child of the SafeBeam prefab. Lets the beacon's look be tuned
// from the Inspector (sprite, color, alpha, width) instead of editing CaveDecorator's
// procedural-generation code. Leave beamSprite empty to keep the generated default
// soft vertical gradient.
[RequireComponent(typeof(SpriteRenderer))]
public class SafeBeamVisual : MonoBehaviour {
    public Sprite beamSprite;
    public Color beamColor = new Color(0.5f, 1f, 0.95f);
    [Range(0f, 1f)] public float alpha = 0.65f;
    public float width = 3.2f;
    public int sortingOrder = 2;

    SpriteRenderer sr;
    static Sprite cachedDefaultSprite;

    void Awake() {
        Apply();
    }

    void OnValidate() {
        Apply();
    }

    public void Apply() {
        if (!sr) sr = GetComponent<SpriteRenderer>();
        sr.sprite = beamSprite ? beamSprite : GetDefaultSprite();
        sr.color = new Color(beamColor.r, beamColor.g, beamColor.b, alpha);
        sr.sortingOrder = sortingOrder;
        transform.localScale = new Vector3(width, transform.localScale.y, 1f);
    }

    // Called by CaveDecorator once per safe point — stretches the beam (pivoted at its
    // base) to span this cave's full height so it reads as a continuous light column.
    public void SetHeight(float height) {
        transform.localScale = new Vector3(width, height, 1f);
    }

    Sprite GetDefaultSprite() {
        if (cachedDefaultSprite) return cachedDefaultSprite;

        const int W = 8, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++) {
            float cx = Mathf.Abs(x - (W - 1) / 2f) / (W / 2f);
            float horiz = Mathf.Clamp01(1f - cx);
            float vert = Mathf.Clamp01(1f - (1f - y / (float)H) * 0.7f);
            float a = horiz * horiz * vert;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply(false, true);
        cachedDefaultSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), H, 0, SpriteMeshType.FullRect);
        return cachedDefaultSprite;
    }
}
