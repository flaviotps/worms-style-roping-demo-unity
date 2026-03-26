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
	
	void Awake()
	{
		crosshair = transform.Find ("Crosshair");
		rb = GetComponent<Rigidbody2D>();
		playerController = GetComponent<PlayerController>();
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

		// No chão: mira manual livre. No ar: permite tiros em qualquer direção enquanto em movimento.
		return playerController.grounded || rb.velocity.sqrMagnitude > 0.2f;
	}

	void SpawnHook()
	{
		hook = Instantiate (Resources.Load ("RopeHook"), crosshair.position + crosshair.up *1.5f, crosshair.rotation) as GameObject;
		hookScript = hook.GetComponent<RopeLogic>();
		hookScript.owner = gameObject;
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
