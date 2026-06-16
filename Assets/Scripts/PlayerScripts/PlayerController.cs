using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour {

	public float groundSpeed = 10f;
	public float airSpeed = 0.2f;
	public float speedDamp = 0.4f;
	public float jumpForce = 1000;
	public bool grounded = false;
	private GameObject groundCheck;
	public Vector2 groundCheckPosition = new Vector2(0,-0.5f);
	public float groundRadius = 0.2f;
	public LayerMask whatIsGround;
	public Vector2 targetVelocity;
	private WeaponManager weaponManager;

	private PhysicsMaterial2D physMatBouncy;
	private PhysicsMaterial2D physMatRegular;
	private Rigidbody2D rb;
	private Transform playerMesh;

	[Header("Worms2-style tuning")]
	public float wallBounceMultiplier = 1.15f;
	public float minWallBounceSpeed = 3f;
	public float bounceNormalThreshold = 0.55f;
	public Vector3 wormBodyScale = new Vector3(1.35f, 0.82f, 1f);
	public Vector3 wormBodyOffset = new Vector3(0f, -0.06f, 0f);
	public float maxFallSpeed = 22f; // terminal velocity so falls feel controlled, not freefall-accelerating forever

	[Header("Wall climb")]
	public float wallClimbSpeed = 2.5f; // slow, deliberate climb — not a fast air-dash substitute
	public float wallCheckDistance = 0.55f;
	public float wallJumpPushForce = 6f;
	public bool isWallGrabbing { get; private set; }

	void Awake()
	{
		rb = GetComponent<Rigidbody2D>();

		groundCheck = new GameObject();
		groundCheck.transform.name = "GroundCheck";
		groundCheck.transform.parent = transform;
		groundCheck.transform.localPosition = groundCheckPosition;

		weaponManager = gameObject.GetComponent<WeaponManager>();

		physMatBouncy = Resources.Load<PhysicsMaterial2D>("p_BouncyPhysMat");
		physMatRegular = Resources.Load<PhysicsMaterial2D>("p_RegularPhysMat");

		playerMesh = transform.Find("PlayerMesh");
		if (playerMesh)
		{
			playerMesh.localScale = wormBodyScale;
			playerMesh.localPosition = wormBodyOffset;
		}
	}

	void FixedUpdate()
	{
		grounded = Physics2D.OverlapCircle (groundCheck.transform.position, groundRadius, whatIsGround);

		Vector2 newVelocity;
		newVelocity = new Vector2(Input.GetAxis ("Horizontal"), 0);
		if(newVelocity.magnitude > 1)
			newVelocity.Normalize ();

		bool swinging = weaponManager.hook && weaponManager.hookScript.hooked;
		UpdateWallGrab(swinging);

		if(grounded)
		{
			rb.linearVelocity += newVelocity * groundSpeed;

			float desiredSpeed = rb.linearVelocity.x;
			desiredSpeed = Mathf.Clamp (desiredSpeed, -groundSpeed, groundSpeed);
			rb.linearVelocity = new Vector2(desiredSpeed, rb.linearVelocity.y);

			if(Input.GetButtonDown ("Jump"))
			{
				rb.AddForce (new Vector2(0, jumpForce));
			}
		}
		else if(swinging)
		{
			rb.linearVelocity += newVelocity * airSpeed;
		}

		if(swinging)
		{
			ChangeCollMat(physMatBouncy);
		}
		else
		{
			ChangeCollMat(physMatRegular);
		}

		if (!isWallGrabbing && rb.linearVelocity.y < -maxFallSpeed)
			rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
	}

	// Hold the stick/key toward a wall while airborne (and not swinging) to grab it
	// and creep up/down slowly with W/S — no automatic grip, you have to lean into it.
	// Jump while grabbing to push off and let go.
	void UpdateWallGrab(bool swinging)
	{
		if (grounded || swinging)
		{
			isWallGrabbing = false;
			return;
		}

		float h = Input.GetAxisRaw ("Horizontal");
		Vector2 dir = h < -0.1f ? Vector2.left : (h > 0.1f ? Vector2.right : Vector2.zero);
		if (dir == Vector2.zero)
		{
			isWallGrabbing = false;
			return;
		}

		RaycastHit2D wallHit = Physics2D.Raycast (transform.position, dir, wallCheckDistance, whatIsGround);
		isWallGrabbing = wallHit.collider != null;
		if (!isWallGrabbing)
			return;

		if (Input.GetButtonDown ("Jump"))
		{
			rb.linearVelocity = new Vector2 (-dir.x * wallJumpPushForce, 0f);
			rb.AddForce (new Vector2(0, jumpForce));
			isWallGrabbing = false;
			return;
		}

		float climbInput = Input.GetAxisRaw ("Vertical");
		rb.linearVelocity = new Vector2 (0f, climbInput * wallClimbSpeed);
	}

	void OnCollisionEnter2D(Collision2D collision)
	{
		if (collision.contactCount == 0)
			return;

		for (int i = 0; i < collision.contactCount; i++)
		{
			Vector2 normal = collision.GetContact(i).normal;
			if (Mathf.Abs(normal.x) < bounceNormalThreshold)
				continue;

			Vector2 currentVelocity = rb.linearVelocity;
			if (currentVelocity.magnitude < minWallBounceSpeed)
				continue;

			if (Vector2.Dot(currentVelocity, normal) >= 0f)
				continue;

			Vector2 reflected = Vector2.Reflect(currentVelocity, normal) * wallBounceMultiplier;
			rb.linearVelocity = reflected;
			break;
		}
	}

	void ChangeCollMat(PhysicsMaterial2D physMat)
	{
		if(!physMat)
			return;

		Collider2D col = gameObject.GetComponent<Collider2D>();
		if(col.sharedMaterial != physMat)
		{
			col.sharedMaterial = physMat;
			col.enabled = false;
			col.enabled = true;
		}
	}
}
