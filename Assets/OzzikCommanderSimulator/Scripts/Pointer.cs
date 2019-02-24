using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pointer : MonoBehaviour {
	public float fireRate = 1.0f;
	public GameObject rightHand;
	public Material mainMaterial;
	public Material hitMaterial;
	public GameObject controller;
	public GameObject mainCamera;
	public GameObject explosionPrefab;
	public GameObject shotPrefab;
	public GameObject score;
	public List<GameObject> particle;
	private LineRenderer line;
	private KinectManager manager;
	private Camera camera;
	private Vector3 target;
	private WaitForSeconds shotDuration = new WaitForSeconds (0.7f);
	private float nextFire;
	private int scoreInt = 1;
	// Use this for initialization
	void Start () {
		line = GetComponent<LineRenderer> ();
		manager = controller.GetComponent<KinectManager>();
		camera = mainCamera.GetComponent<Camera> ();
	}
	public Vector3 GetTargetPosition(){
		return target;
	}
	// Update is called once per frame
	void Update () {
		RaycastHit hit;
		Vector3 origin = rightHand.transform.position;
		target = origin + rightHand.transform.rotation * new Vector3(0,300,0);
		line.material = mainMaterial;
		Debug.DrawRay (origin, rightHand.transform.rotation * new Vector3 (0, 1, 0) * 300, Color.green);


		if(Physics.Raycast(origin + rightHand.transform.rotation * new Vector3(0,1,0), rightHand.transform.rotation * new Vector3(0,10,0),out hit, 300) ){
			target = hit.point;
			if (hit.collider.gameObject.tag == "Enemy") {
				line.material = hitMaterial;
				if (manager.GetRightHandState (manager.GetPrimaryUserID ()) == KinectInterop.HandState.Closed && Time.time > nextFire) {					
					Instantiate (explosionPrefab, hit.transform.position, Quaternion.identity);
					foreach (GameObject ps in particle) {
						ps.GetComponent<AudioSource> ().Play ();
						Instantiate (shotPrefab, ps.transform.position, Quaternion.identity);
					}
					Destroy (hit.collider.gameObject);
					nextFire = Time.time + fireRate;
					score.GetComponent<GUIText> ().text = "Wynik: " + scoreInt;
					scoreInt++;
				}
			}
		}
		Quaternion lookRotation = Quaternion.LookRotation (target - mainCamera.transform.position);
		mainCamera.transform.rotation = Quaternion.Slerp (mainCamera.transform.rotation, lookRotation, Time.deltaTime);
		line.SetPosition (0, origin);
		line.SetPosition (1, target);

	}
}
