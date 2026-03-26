using UnityEngine;
using System.Collections;

public class AimLogic : MonoBehaviour {

	private MeshRenderer meshRenderer;
	private SpriteRenderer spriteRenderer;
	private PlayerController playerController;
	private Rigidbody2D playerRb;
	private Camera mainCam;
	private Transform playerTransform;
	private Vector2 currentAimDirection = Vector2.up;

	[Header("PC Controls")]
	public bool allowMouseAim = true;
	public float airborneAutoAimMinSpeed = 0.1f;
	public float orbitRadius = 1.35f;
	[Range(0.01f, 0.4f)]
	public float minUpperHemisphereY = 0.05f;
	public string crosshairSpriteResource = "spr_Crosshair";
	public int generatedSpriteSize = 64;

	void Start () 
	{
		meshRenderer = gameObject.GetComponent<MeshRenderer>();
		spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
		playerController = GetComponentInParent<PlayerController>();
		playerRb = GetComponentInParent<Rigidbody2D>();
		playerTransform = playerController ? playerController.transform : transform.parent;
		mainCam = Camera.main;

		SetupCrosshairVisual();
		SetCrosshairVisible(true);
	}

	void SetupCrosshairVisual()
	{
		if (!spriteRenderer)
			spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

		if (meshRenderer)
			meshRenderer.enabled = false;

		if (!spriteRenderer.sprite)
		{
			Sprite resourceSprite = Resources.Load<Sprite>(crosshairSpriteResource);
			if (resourceSprite)
			{
				spriteRenderer.sprite = resourceSprite;
			}
			else
			{
				Texture2D tex = Resources.Load<Texture2D>(crosshairSpriteResource);
				if (tex)
					spriteRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 64f);
				else
					spriteRenderer.sprite = CreateProceduralCrosshairSprite(generatedSpriteSize);
			}
		}

		spriteRenderer.sortingOrder = 100;
	}

	Sprite CreateProceduralCrosshairSprite(int size)
	{
		int safeSize = Mathf.Max(32, size);
		Texture2D tex = new Texture2D(safeSize, safeSize, TextureFormat.RGBA32, false);
		tex.filterMode = FilterMode.Bilinear;
		tex.wrapMode = TextureWrapMode.Clamp;

		Color clear = new Color(0f, 0f, 0f, 0f);
		Color line = new Color(0.67f, 1f, 0.47f, 1f);
		float center = (safeSize - 1) * 0.5f;
		float innerGap = safeSize * 0.12f;
		float ringInner = safeSize * 0.31f;
		float ringOuter = safeSize * 0.36f;
		float lineHalf = 1.5f;
		float armStart = safeSize * 0.2f;
		float armEnd = safeSize * 0.44f;

		for (int y = 0; y < safeSize; y++)
		{
			for (int x = 0; x < safeSize; x++)
			{
				float dx = x - center;
				float dy = y - center;
				float dist = Mathf.Sqrt(dx * dx + dy * dy);
				bool paint = false;

				if (dist >= ringInner && dist <= ringOuter)
					paint = true;

				bool verticalArm = Mathf.Abs(dx) <= lineHalf && Mathf.Abs(dy) >= armStart && Mathf.Abs(dy) <= armEnd;
				bool horizontalArm = Mathf.Abs(dy) <= lineHalf && Mathf.Abs(dx) >= armStart && Mathf.Abs(dx) <= armEnd;
				if (verticalArm || horizontalArm)
					paint = true;

				if (dist < innerGap)
					paint = false;

				tex.SetPixel(x, y, paint ? line : clear);
			}
		}

		tex.Apply(false, true);
		return Sprite.Create(tex, new Rect(0, 0, safeSize, safeSize), new Vector2(0.5f, 0.5f), safeSize);
	}
	
	void Update () 
	{
		Vector2 manualAim = GetManualAimVector();
		Vector2 aimVector = Vector2.zero;

		if (manualAim.sqrMagnitude > 0.0001f)
		{
			aimVector = manualAim;
		}
		else if(playerRb && playerRb.velocity.magnitude >= airborneAutoAimMinSpeed)
		{
			aimVector = playerRb.velocity.normalized;
		}

		aimVector = ClampToUpperHemisphere(aimVector);
		if (aimVector.sqrMagnitude > 0.0001f)
			currentAimDirection = aimVector.normalized;

		ApplyCrosshairTransform(currentAimDirection);
		SetCrosshairVisible(true);
	}

	void ApplyCrosshairTransform(Vector2 direction)
	{
		if (playerTransform)
		{
			transform.position = (Vector2)playerTransform.position + direction * orbitRadius;
		}

		transform.eulerAngles = new Vector3(
			transform.eulerAngles.x,
			transform.eulerAngles.y,
			Mathf.Atan2(direction.x *-1, direction.y * -1) * Mathf.Rad2Deg
		);
	}

	Vector2 ClampToUpperHemisphere(Vector2 aimVector)
	{
		if (aimVector.sqrMagnitude <= 0.0001f)
			return aimVector;

		Vector2 clamped = aimVector.normalized;
		if (clamped.y < minUpperHemisphereY)
		{
			clamped.y = minUpperHemisphereY;
			clamped = clamped.normalized;
		}

		return clamped;
	}

	void SetCrosshairVisible(bool value)
	{
		if (spriteRenderer)
			spriteRenderer.enabled = value;
		if (meshRenderer)
			meshRenderer.enabled = false;
	}

	Vector2 GetManualAimVector()
	{
		Vector2 stickAim = new Vector2(Input.GetAxis ("RightHorizontal"), Input.GetAxis ("RightVertical"));
		if (stickAim.sqrMagnitude > 0.01f)
			return stickAim;

		Vector2 keyboardAim = Vector2.zero;
		if (Input.GetKey(KeyCode.UpArrow)) keyboardAim.y += 1f;
		if (Input.GetKey(KeyCode.DownArrow)) keyboardAim.y -= 1f;
		if (Input.GetKey(KeyCode.LeftArrow)) keyboardAim.x -= 1f;
		if (Input.GetKey(KeyCode.RightArrow)) keyboardAim.x += 1f;
		if (keyboardAim.sqrMagnitude > 0.01f)
			return keyboardAim.normalized;

		if (allowMouseAim && mainCam)
		{
			Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
			Vector2 origin = playerTransform ? (Vector2)playerTransform.position : (Vector2)transform.position;
			Vector2 mouseAim = (Vector2)mouseWorld - origin;
			if (mouseAim.sqrMagnitude > 0.01f)
				return mouseAim.normalized;
		}

		return Vector2.zero;
	}
}
