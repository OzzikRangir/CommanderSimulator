using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour {
	public int maxObjects = 5;
	public GameObject czolg;
	public GameObject biegnacy;
	public GameObject transporter;
	public GameObject spawner;
	private List<GameObject> list = new List<GameObject> ();
	private Collider spawnerCollider;
	// Use this for initialization
	void Start () {
		spawnerCollider = spawner.GetComponent<Collider> ();

	}
	
	// Update is called once per frame
	void Update () {
		foreach (GameObject go in list) {

			if (go == null)
				list.Remove (go);
		}
			

		if( list.Count <= maxObjects){
			int obj =(int) Random.Range (0, 3);
			GameObject prefab = czolg;
			switch (obj) {
			case 0:
				prefab = czolg;
				break;
			case 1:
				prefab = biegnacy;
				break;
			case 2:
				prefab = transporter;
				break;
			}

			float x = Random.Range(spawnerCollider.bounds.min.x, spawnerCollider.bounds.max.x);
			float z = Random.Range(spawnerCollider.bounds.min.z, spawnerCollider.bounds.max.z);
			Vector3 pos = new Vector3(x, 101, z);

			list.Add (Instantiate (prefab, pos, Quaternion.identity));
	}
}
}
