using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class testScript2 : MonoBehaviour {

    public float speedFactor;

	// Use this for initialization
	void Start () {
        speedFactor = speedFactor * .1f;
	}
	
	// Update is called once per frame
	void Update () {
        this.transform.position = new Vector3(this.transform.position.x - speedFactor, this.transform.position.y, this.transform.position.z + speedFactor);
	}
}
