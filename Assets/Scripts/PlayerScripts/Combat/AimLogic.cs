using UnityEngine;
using System.Collections;

public class AimLogic : MonoBehaviour {

	private MeshRenderer meshRenderer;
	public float aimSpeed = 120f;
	public float minAimAngle = 10f;
	public float maxAimAngle = 170f;
	public float defaultAimAngle = 45f;
	private float currentAimAngle;

	// Use this for initialization
	void Start () 
	{
		meshRenderer = gameObject.GetComponent<MeshRenderer>();
		currentAimAngle = defaultAimAngle;
	}

	// Update is called once per frame
	void Update () 
	{
		float verticalInput = Input.GetAxis("Vertical");
		currentAimAngle += verticalInput * aimSpeed * Time.deltaTime;
		currentAimAngle = Mathf.Clamp(currentAimAngle, minAimAngle, maxAimAngle);

		float facing = 1f;
		if(transform.parent && transform.parent.localScale.x < 0)
			facing = -1f;

		float zAngle = facing > 0 ? -currentAimAngle : currentAimAngle;
		transform.localRotation = Quaternion.Euler(0f, 0f, zAngle);

		if(meshRenderer)
			meshRenderer.enabled = true;
	}
}
