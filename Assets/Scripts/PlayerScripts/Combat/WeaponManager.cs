using UnityEngine;
using System.Collections;

public class WeaponManager : MonoBehaviour {

	public float ropeHookSpeed = 150;
	public float ropeHookSpeedDamp = 0.1f;
	public float ropeClimbSpeed = 1;
	public float maxLength = 30;
	public bool hooked;
	public float launcherLength = 1.5f;

	[HideInInspector]
	public GameObject hook;
	[HideInInspector]
	public RopeLogic hookScript;
	[HideInInspector]
	public Transform crosshair;
	
	void Awake()
	{
		crosshair = transform.Find ("Crosshair");
	}

	// Update is called once per frame
	void Update () {
		if(Input.GetButtonDown ("Shoot Rope"))
		{
			if(hook)
			{
				DestroyHook();
			}
			else
				SpawnHook ();
		}
		if(Input.GetButtonDown ("Jump") && hook)
		{
			DestroyHook();
		}
	}

	void SpawnHook()
	{
		if(!crosshair)
			return;

		Vector3 spawnPosition = crosshair.position + crosshair.up * launcherLength;
		hook = Instantiate (Resources.Load ("RopeHook"), spawnPosition, crosshair.rotation) as GameObject;
		hookScript = hook.GetComponent<RopeLogic>();
		hookScript.owner = gameObject;
	}

	public void DestroyHook()
	{
		Destroy (hook);
		gameObject.GetComponent<SpringJoint2D>().enabled = false;
		hook = null;
		hookScript = null;
	}
}
