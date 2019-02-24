using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Menu : MonoBehaviour {
	public List<GameObject> objectsEnabled;
	public List<GameObject> objectsFalsed;
	// Use this for initialization
	void Start () {
	}
	
	// Update is called once per frame
	void Update () {
		if (gameObject.GetComponent<RaiseHandListener> ().IsRaiseLeftHand ()) {
			Cursor.visible = true;;
			foreach(GameObject go in objectsEnabled){
				go.SetActive (true);
			}
			foreach(GameObject go in objectsFalsed){
				go.SetActive (false);
			}
			gameObject.GetComponent<InteractionManager> ().enabled = true;
		}
	}
	public void EndGame(){
		Application.Quit ();
	}
	public void HideCursor(){
		Cursor.visible = false;
	}
}
