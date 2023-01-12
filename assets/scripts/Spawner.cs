using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour {
    public float Rate;
    public Transform Prefab;

    float timer;
	void Update () {
        timer += Time.deltaTime;
        if (timer >= Rate) {
            timer = 0;

            Instantiate(Prefab, transform.position + new Vector3(Random.value, Random.value, Random.value), Quaternion.identity);
        }
	}
}
