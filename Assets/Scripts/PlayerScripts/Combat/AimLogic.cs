using UnityEngine;
using System.Collections;

public class AimLogic : MonoBehaviour {

	private MeshRenderer meshRenderer;
	private PlayerController playerController;
	private Rigidbody2D playerRb;
	private Camera mainCam;

	[Header("PC Controls")]
	public bool allowMouseAim = true;
	public float airborneAutoAimMinSpeed = 0.1f;

	void Start () 
	{
		meshRenderer = gameObject.GetComponent<MeshRenderer>();
		playerController = GetComponentInParent<PlayerController>();
		playerRb = GetComponentInParent<Rigidbody2D>();
		mainCam = Camera.main;
		meshRenderer.enabled = false;
	}
	
	void Update () 
	{
		bool canManualAim = playerController && playerController.grounded;
		Vector2 aimVector = Vector2.zero;

		if (canManualAim)
		{
			aimVector = GetManualAimVector();
		}
		else if(playerRb && playerRb.velocity.magnitude >= airborneAutoAimMinSpeed)
		{
			aimVector = playerRb.velocity.normalized;
		}

		if(aimVector.sqrMagnitude > 0.0001f)
		{
			transform.eulerAngles = new Vector3(
				transform.eulerAngles.x,
				transform.eulerAngles.y,
				Mathf.Atan2(aimVector.x *-1, aimVector.y * -1) * Mathf.Rad2Deg
			);
			meshRenderer.enabled = true;
		}
		else
		{
			meshRenderer.enabled = false;
		}
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
			Vector2 mouseAim = mouseWorld - transform.position;
			if (mouseAim.sqrMagnitude > 0.01f)
				return mouseAim.normalized;
		}

		return Vector2.zero;
	}
}
