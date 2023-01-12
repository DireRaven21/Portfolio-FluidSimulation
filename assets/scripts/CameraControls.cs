using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraControls : MonoBehaviour {
    public float Speed = 5f;
    public float Distance = 15f;
    public Transform Orbit;

    float a = 0;
   
	void Update () {
        Vector3 pos = Orbit.position;
        pos.y = transform.position.y;

        a += Speed * Time.deltaTime;
        if (a > 360) {
            a -= 360;
        }

        float rad = a * Mathf.Deg2Rad;
        pos.x = Distance * Mathf.Cos(rad) + Distance * Mathf.Sin(rad);
        pos.z = -Distance * Mathf.Sin(rad) + Distance * Mathf.Cos(rad);

        transform.position = pos;
        transform.rotation = Quaternion.LookRotation(Orbit.position - transform.position, Vector3.up);
    }
}
