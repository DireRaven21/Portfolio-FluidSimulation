using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class linearMove : MonoBehaviour {

    public float Speed = 10f;
    public float Magnitude = 4f;

    public bool enabled = false;

    Vector3 dest;
    Vector3 dir;
    public Vector3 Dir{
        get { return dir.normalized * Speed; }
    }

    void Start() {
        dir = new Vector3(1, 0, 0).normalized * Magnitude;
        dest = transform.position + dir;
    }
	void Update () {
        if (Input.GetKeyDown(KeyCode.Return)) {
            enabled = !enabled;
        }
        if (!enabled) {
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, dest, Speed * Time.deltaTime);
        if (transform.position == dest) {
            dir *= -1;
            dest = transform.position + dir * 2;
        }
	}
}
