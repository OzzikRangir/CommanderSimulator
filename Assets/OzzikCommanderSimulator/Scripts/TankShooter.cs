                                                                                                                                                                                                                                        using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TankShooter : MonoBehaviour {

	public GameObject gameLogic;
	private Pointer pointer;

	// Use this for initialization
	void Start () {
		pointer = gameLogic.GetComponent<Pointer> ();

	}

	// Update is called once per frame
	void Update () {
		Vector3 target = pointer.GetTargetPosition ();
		Quaternion lookRotation = Quaternion.LookRotation (target - transform.position);
		lookRotation = Quaternion.Euler (0, lookRotation.eulerAngles.y-180, 0);
		transform.rotation = Quaternion.Slerp (transform.rotation, lookRotation, Time.deltaTime);
		//transform.LookAt (pointer.GetTargetPosition ());
		

	}
}
