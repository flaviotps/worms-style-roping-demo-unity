using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RopeLogic : Projectile {
	
	public bool hooked;
	public GameObject hook;
	private DistanceJoint2D rope;
	public List<Vector2> anchors;
	public float linecastOffset = 0.01f;
	public float returnLCDist = 0.2f;
	private LineRenderer LR;
	public float combinedAnchorLen;
	public float totalLength;
	
	// Use this for initialization
	protected override void Start () {
		base.Start ();

		speed = weaponManager.ropeHookSpeed;
		transform.name = "RopeHook";
		Physics2D.IgnoreLayerCollision (layerPlayer, layerHook, true);
		GetComponent<Rigidbody2D>().linearVelocity = transform.TransformDirection(Vector3.up * speed);
		GetComponent<Rigidbody2D>().linearDamping = weaponManager.ropeHookSpeedDamp;
		
		rope = owner.GetComponent<DistanceJoint2D>();
		if (!rope) rope = owner.AddComponent<DistanceJoint2D>(); // scene Player only has the old SpringJoint2D serialized
		rope.enableCollision      = true;
		rope.autoConfigureDistance = false;
		rope.maxDistanceOnly       = true; // Worms2 feel: rope only stops you stretching past its length, free swing when slack
		anchors = new List<Vector2>();
		LR = gameObject.GetComponent<LineRenderer>();
		LR.positionCount = 2;
		StyleRopeVisual();
	}

	// The prefab's LineRenderer ships with no material assigned, which Unity renders
	// as flat magenta (its "missing shader" fallback) — give it a proper glowing neon
	// cable look instead. Also swaps the hook's plain default-material primitive mesh
	// for a small glowing diamond sprite.
	void StyleRopeVisual() {
		if (LR.sharedMaterial == null)
			LR.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

		Color cable = new Color(0.55f, 0.95f, 1f);
		LR.startColor = cable;
		LR.endColor = cable;
		LR.startWidth = 0.06f;
		LR.endWidth = 0.06f;
		LR.numCapVertices = 4;

		// "Mesh" already carries a MeshRenderer — Unity won't let a SpriteRenderer (also
		// a Renderer) live on the same GameObject, so the icon gets its own child instead.
		var meshChild = transform.Find("Mesh");
		if (meshChild) {
			var oldRenderer = meshChild.GetComponent<MeshRenderer>();
			if (oldRenderer) oldRenderer.enabled = false;
		}

		var icon = new GameObject("HookIcon");
		icon.transform.SetParent(transform);
		icon.transform.localPosition = Vector3.zero;
		icon.transform.localRotation = Quaternion.identity;

		var sr = icon.AddComponent<SpriteRenderer>();
		sr.sprite = MakeHookSprite();
		sr.color = cable;
		sr.sortingOrder = 6;
	}

	Sprite MakeHookSprite() {
		const int S = 16;
		var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
		tex.filterMode = FilterMode.Bilinear;
		Vector2 c = new Vector2((S - 1) / 2f, (S - 1) / 2f);
		for (int y = 0; y < S; y++)
		for (int x = 0; x < S; x++) {
			float d = Mathf.Abs(x - c.x) / (S / 2f) + Mathf.Abs(y - c.y) / (S / 2f); // diamond distance
			float a = Mathf.Clamp01(1f - d * 1.15f);
			tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
		}
		tex.Apply(false, true);
		return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S * 2.5f);
	}
	
	void FixedUpdate () 
	{
		if(!hooked)
		{
			EarlyHookCheck();

			if(Vector2.Distance (owner.transform.position, transform.position) > weaponManager.maxLength)
				weaponManager.DestroyHook ();
		}
		
		if(hooked && anchors.Count == 0)
			AddAnchor (transform.position);
		
		if(anchors.Count > 0)
			RopeJointManager();

		float allowedDistance = weaponManager.maxLength - combinedAnchorLen;
		// GetAxisRaw (not GetAxis) for instant stop on release — GetAxis smooths via the
		// Input Manager's Gravity/Sensitivity settings. ropeClimbSpeed is a units-per-SECOND
		// rate, so it must be scaled by fixedDeltaTime — it previously applied the full
		// speed value every single FixedUpdate (50/sec), making W/S change rope length up
		// to 50x faster than intended and overshoot well past where the key was released.
		rope.distance = Mathf.Clamp ( rope.distance + Input.GetAxisRaw ("Vertical") * -1 * weaponManager.ropeClimbSpeed * Time.fixedDeltaTime, 1, allowedDistance);
	}
	void EarlyHookCheck()
	{
		Debug.DrawLine (owner.transform.position, transform.position);
		RaycastHit2D hit = Physics2D.Linecast (owner.transform.position, transform.position);

		if(hit && hit.collider.gameObject.CompareTag ("Hookable"))
		{
			transform.position = hit.point + (hit.normal.normalized * linecastOffset);
			ProcessHit ();
		}
		else
			return;
	}
	//Manages the creation and removal of joints in a rope
	void RopeJointManager()
	{
		//Creates an anchor point when a linecast from player to previous anchor is broken
		RaycastHit2D hit = Physics2D.Linecast (owner.transform.position, anchors[anchors.Count-1]);

		if(hit && hit.collider.gameObject.CompareTag ("Hookable"))
		{
			Vector2 anchorPoint = hit.point + (hit.normal.normalized * linecastOffset);
			AddAnchor(anchorPoint);
		}

		//Removes anchors when player has line of sight on the previous anchor
		if(anchors.Count > 1)
		{
			Vector2 ABVector = new Vector2(anchors[anchors.Count - 1].x - owner.transform.position.x, anchors[anchors.Count - 1].y - owner.transform.position.y).normalized;
			Vector2 shortLCStart = anchors[anchors.Count - 1] + (-returnLCDist * ABVector);
			RaycastHit2D returnHitShort = Physics2D.Linecast (shortLCStart, anchors[anchors.Count-2]);
			
			if (!returnHitShort) 
			{
				KillAnchor();
			}
		}

		LR.SetPosition (0,transform.position);
		if(!hooked)
			LR.SetPosition (anchors.Count+1,owner.transform.position);
		else
			LR.SetPosition (anchors.Count,owner.transform.position);
	}
	
	void AddAnchor(Vector2 pos)
	{
		anchors.Add (pos);
		if(anchors.Count > 1)
		{
			combinedAnchorLen += Vector2.Distance (anchors[anchors.Count-1], anchors[anchors.Count-2]);
			combinedAnchorLen = Mathf.Round (combinedAnchorLen * 100f) / 100f;
		}
		SetSpring ();
		
	}
	void KillAnchor()
	{
		if(anchors.Count > 1)
		{
			combinedAnchorLen -= Vector2.Distance (anchors[anchors.Count-1], anchors[anchors.Count-2]);
			combinedAnchorLen = Mathf.Round (combinedAnchorLen * 100f) / 100f;
		}
		
		anchors.RemoveAt (anchors.Count-1);
		
		SetSpring ();
	}
	
	void SetSpring()
	{
		float dist = Vector2.Distance (owner.transform.position, anchors[anchors.Count-1]);

		rope.connectedAnchor = anchors[anchors.Count-1];
		rope.distance = dist;
		rope.enabled = true;
		LineRenderer();
	}
	
	void LineRenderer()
	{
		LR.SetPosition (0,anchors[0]);
		LR.positionCount = anchors.Count+1;
		LR.SetPosition (anchors.Count,owner.transform.position);
		LR.SetPosition (anchors.Count-1, anchors[anchors.Count-1]);
	}
	
	void OnCollisionEnter2D(Collision2D col) 
	{
		if(!col.gameObject.CompareTag ("Hookable"))
			return;
		else
		{
			ProcessHit();
		}
	}

	void ProcessHit()
	{
		hooked = true;
		GetComponent<Collider2D>().enabled = false;
		//rigidbody2D.isKinematic = true;
		Destroy (GetComponent<Rigidbody2D>());
	}
}
