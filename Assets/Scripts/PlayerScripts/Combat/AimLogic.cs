using UnityEngine;
using System.Collections;

public class AimLogic : MonoBehaviour {

	private MeshRenderer meshRenderer;
	private Camera mainCamera;
	public float mouseAimDeadZone = 0.05f;

	// Use this for initialization
	void Start () 
	{
		meshRenderer = gameObject.GetComponent<MeshRenderer>();
		mainCamera = Camera.main;
		if(meshRenderer)
			meshRenderer.enabled = false;
	}

	// Update is called once per frame
	void Update () 
	{
		Vector2 stickAimVector = new Vector2(Input.GetAxis ("RightHorizontal"), Input.GetAxis ("RightVertical"));
		Vector2 mouseAimVector = Vector2.zero;
		bool hasMouseAim = false;

		if(mainCamera)
		{
			Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
			mouseAimVector = mouseWorldPosition - transform.position;
			hasMouseAim = mouseAimVector.magnitude > mouseAimDeadZone;
		}

		bool hasStickAim = stickAimVector.sqrMagnitude > 0.0001f;
		Vector2 aimVector = hasMouseAim ? mouseAimVector : stickAimVector;

		//If player aims, activate crosshair and set rotation
		if(hasMouseAim || hasStickAim)
		{
			transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y, Mathf.Atan2(aimVector.x *-1, aimVector.y * -1) * Mathf.Rad2Deg);
			if(meshRenderer)
				meshRenderer.enabled = true;
		}
		else if(meshRenderer)
			//Deactivate crosshair when not aiming;
			meshRenderer.enabled = false;
	}
}
