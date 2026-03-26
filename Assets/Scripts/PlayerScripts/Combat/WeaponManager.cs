using UnityEngine;
using System.Collections;

public class WeaponManager : MonoBehaviour {

	public float ropeHookSpeed = 150;
	public float ropeHookSpeedDamp = 0.1f;
	public float ropeClimbSpeed = 1;
	public float maxLength = 30;
	public bool hooked;

	[Header("Worms2-style release")]
	public float ropeReleaseBoost = 1.15f;
	public float ropeReleaseTangentialBias = 0.75f;

	[HideInInspector]
	public GameObject hook;
	[HideInInspector]
	public RopeLogic hookScript;
	[HideInInspector]
	public Transform crosshair;

	private Rigidbody2D rb;
	private PlayerController playerController;
	private Camera mainCam;
	
	void Awake()
	{
		rb = GetComponent<Rigidbody2D>();
		playerController = GetComponent<PlayerController>();
		mainCam = Camera.main;
		ResolveCrosshair();
	}

	void Update () {
		if(ShouldShootPressed())
		{
			if(hook)
			{
				DestroyHook(true);
			}
			else if(CanShootRope())
			{
				SpawnHook ();
			}
		}

		if(ShouldReleasePressed() && hook)
		{
			DestroyHook(true);
		}
	}

	bool ShouldShootPressed()
	{
		return Input.GetButtonDown("Shoot Rope") || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E);
	}

	bool ShouldReleasePressed()
	{
		return Input.GetButtonDown("Jump") || Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Q);
	}

	bool CanShootRope()
	{
		if (!playerController)
			return true;

		return playerController.grounded || rb.velocity.sqrMagnitude > 0.2f;
	}

	void ResolveCrosshair()
	{
		if (crosshair)
			return;

		crosshair = transform.Find("Crosshair");
		if (crosshair)
			return;

		GameObject runtimeCrosshair = new GameObject("Crosshair");
		runtimeCrosshair.transform.SetParent(transform);
		runtimeCrosshair.transform.localPosition = new Vector3(0f, 0.8f, 0f);
		runtimeCrosshair.transform.localRotation = Quaternion.identity;
		crosshair = runtimeCrosshair.transform;
	}

	void SpawnHook()
	{
		ResolveCrosshair();

		Vector2 aimDir = GetCurrentAimDirection();
		Quaternion hookRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aimDir.x * -1f, aimDir.y * -1f) * Mathf.Rad2Deg);
		Vector3 spawnPos = (crosshair ? crosshair.position : transform.position) + (Vector3)(aimDir * 1.5f);

		hook = Instantiate (Resources.Load ("RopeHook"), spawnPos, hookRotation) as GameObject;
		hookScript = hook.GetComponent<RopeLogic>();
		hookScript.owner = gameObject;
	}

	Vector2 GetCurrentAimDirection()
	{
		if (crosshair)
		{
			Vector2 crosshairUp = crosshair.up;
			if (crosshairUp.sqrMagnitude > 0.001f)
				return crosshairUp.normalized;
		}

		Vector2 stickAim = new Vector2(Input.GetAxis("RightHorizontal"), Input.GetAxis("RightVertical"));
		if (stickAim.sqrMagnitude > 0.01f)
			return stickAim.normalized;

		if (mainCam)
		{
			Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
			Vector2 mouseAim = mouseWorld - transform.position;
			if (mouseAim.sqrMagnitude > 0.01f)
				return mouseAim.normalized;
		}

		if (rb && rb.velocity.sqrMagnitude > 0.01f)
			return rb.velocity.normalized;

		return Vector2.up;
	}

	public void DestroyHook()
	{
		DestroyHook(false);
	}

	public void DestroyHook(bool applyReleaseBoost)
	{
		if (applyReleaseBoost)
		{
			ApplyReleaseMomentum();
		}

		Destroy (hook);
		gameObject.GetComponent<SpringJoint2D>().enabled = false;
		hook = null;
		hookScript = null;
	}

	void ApplyReleaseMomentum()
	{
		if (!rb || !hookScript || hookScript.anchors == null || hookScript.anchors.Count == 0)
			return;

		Vector2 pivot = hookScript.anchors[hookScript.anchors.Count - 1];
		Vector2 radiusDir = ((Vector2)transform.position - pivot).normalized;
		if (radiusDir.sqrMagnitude <= 0.0001f)
			return;

		Vector2 tangentA = new Vector2(-radiusDir.y, radiusDir.x);
		Vector2 tangentB = -tangentA;
		Vector2 currentVelocity = rb.velocity;

		Vector2 bestTangent = Vector2.Dot(currentVelocity, tangentA) >= Vector2.Dot(currentVelocity, tangentB) ? tangentA : tangentB;
		float speed = Mathf.Max(currentVelocity.magnitude, 0.01f);
		Vector2 boostedVelocity = (currentVelocity + (bestTangent * speed * ropeReleaseTangentialBias)) * ropeReleaseBoost;

		rb.velocity = boostedVelocity;
	}
}
