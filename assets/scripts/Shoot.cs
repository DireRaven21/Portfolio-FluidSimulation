using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shoot : MonoBehaviour {
    public float Rate = 1f;
    public float Force = 30f;
    public bool AutoShoot = false;
    public Transform prefab;
    public Transform FirePos;

    float timer = 0;

	void Update () {
        if (timer > 0) {
            timer -= Time.deltaTime;
        }

        if ((Input.GetButton("Fire1") || AutoShoot) && timer <= 0) {
            Transform t = Instantiate(prefab, FirePos.position, Quaternion.LookRotation(transform.forward, transform.up));
            t.GetComponent<Rigidbody>().AddForce(t.forward * Force, ForceMode.Impulse);
            timer = Rate;
        }
	}
}
