using UnityEngine;
using System.Collections;

public class AimLogic : MonoBehaviour {

	private MeshRenderer meshRenderer;
	private Camera mainCamera;
	public float stickAimDeadZone = 0.2f;

	// Use this for initialization
	void Start () 
	{
		meshRenderer = gameObject.GetComponent<MeshRenderer>();
		mainCamera = Camera.main;
	}

	// Update is called once per frame
	void Update () 
	{
		if(!mainCamera)
			mainCamera = Camera.main;

		Vector2 stickAimVector = new Vector2(Input.GetAxis ("RightHorizontal"), Input.GetAxis ("RightVertical"));
		bool hasStickAim = stickAimVector.magnitude > stickAimDeadZone;

		bool hasMouseAim = false;
		Vector2 mouseAimVector = transform.up;

		if(mainCamera)
		{
			Vector3 mouseScreenPosition = Input.mousePosition;
			mouseScreenPosition.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
			Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
			mouseAimVector = mouseWorldPosition - transform.position;
			hasMouseAim = mouseAimVector.sqrMagnitude > 0.0001f;
		}

		Vector2 aimVector = hasStickAim ? stickAimVector : mouseAimVector;

		if(hasMouseAim || hasStickAim)
		{
			float aimAngle = Mathf.Atan2(aimVector.y, aimVector.x) * Mathf.Rad2Deg - 90f;
			transform.rotation = Quaternion.Euler(0f, 0f, aimAngle);
		}

		if(meshRenderer)
			meshRenderer.enabled = true;
	}
}
