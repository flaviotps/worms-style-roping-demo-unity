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

	public float airRotationLerpSpeed = 8f;
	public float groundRotationLerpSpeed = 12f;
	public float wallBounceMultiplier = 1.08f;
	public float wallBounceBoost = 1.2f;
	public float minWallBounceSpeed = 1.5f;

	private PhysicsMaterial2D physMatBouncy;
	private PhysicsMaterial2D physMatRegular;
	private Rigidbody2D body;

	void Awake()
	{
		groundCheck = new GameObject();
		groundCheck.transform.name = "GroundCheck";
		groundCheck.transform.parent = transform;
		groundCheck.transform.localPosition = groundCheckPosition;

		weaponManager = gameObject.GetComponent<WeaponManager>();
		body = gameObject.GetComponent<Rigidbody2D>();

		physMatBouncy = Resources.Load<PhysicsMaterial2D>("p_BouncyPhysMat");
		physMatRegular = Resources.Load<PhysicsMaterial2D>("p_RegularPhysMat");
	}
		
	void FixedUpdate()
	{
		grounded = Physics2D.OverlapCircle (groundCheck.transform.position, groundRadius, whatIsGround);

		Vector2 moveInput = new Vector2(Input.GetAxis ("Horizontal"), 0);
		if(moveInput.magnitude > 1)
			moveInput.Normalize ();

		UpdateFacing(moveInput.x);

		if(grounded)
		{
			body.velocity += moveInput * groundSpeed;
			
			float desiredSpeed = body.velocity.x;
			desiredSpeed = Mathf.Clamp (desiredSpeed, -groundSpeed, groundSpeed);
			body.velocity = new Vector2(desiredSpeed, body.velocity.y);

			if(Input.GetButtonDown ("Jump"))
			{
				body.AddForce (new Vector2(0, jumpForce));
			}
		}
		else if(weaponManager.hook && weaponManager.hookScript.hooked)
		{
			body.velocity += moveInput * airSpeed;
		}

		UpdateBodyRotation();

		if(weaponManager.hook && weaponManager.hookScript.hooked)
		{
			ChangeCollMat(physMatBouncy);
		}
		else
		{
			ChangeCollMat(physMatRegular);
		}
	}

	void UpdateFacing(float horizontalInput)
	{
		if(Mathf.Abs(horizontalInput) < 0.01f)
			return;

		Vector3 currentScale = transform.localScale;
		currentScale.x = Mathf.Abs(currentScale.x) * Mathf.Sign(horizontalInput);
		transform.localScale = currentScale;
	}

	void UpdateBodyRotation()
	{
		float targetZ = 0f;
		float lerpSpeed = groundRotationLerpSpeed;

		if(!grounded && body.velocity.sqrMagnitude > 0.01f)
		{
			targetZ = Mathf.Atan2(body.velocity.y, body.velocity.x) * Mathf.Rad2Deg - 90f;
			lerpSpeed = airRotationLerpSpeed;
		}

		float z = Mathf.LerpAngle(transform.eulerAngles.z, targetZ, Time.fixedDeltaTime * lerpSpeed);
		transform.rotation = Quaternion.Euler(0f, 0f, z);
	}

	void OnCollisionEnter2D(Collision2D collision)
	{
		if(grounded || collision.contacts.Length == 0)
			return;

		for(int i = 0; i < collision.contacts.Length; i++)
		{
			Vector2 normal = collision.contacts[i].normal;
			if(Vector2.Dot(normal, Vector2.up) > 0.5f)
				continue;

			Vector2 currentVelocity = body.velocity;
			if(currentVelocity.magnitude < minWallBounceSpeed)
				return;

			Vector2 reflected = Vector2.Reflect(currentVelocity, normal).normalized * currentVelocity.magnitude * wallBounceMultiplier;
			body.velocity = reflected + normal * wallBounceBoost;
			return;
		}
	}

	void ChangeCollMat(PhysicsMaterial2D physMat)
	{
		if(gameObject.GetComponent<Collider2D>().sharedMaterial != physMat)
		{
			gameObject.GetComponent<Collider2D>().sharedMaterial = physMat;
			gameObject.GetComponent<Collider2D>().enabled = false;
			gameObject.GetComponent<Collider2D>().enabled = true;
		}
	}
}
