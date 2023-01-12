using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Remove : MonoBehaviour {

    public float TimeToDestroy = 5f;

	void Start () {
        Destroy(gameObject, TimeToDestroy);
	}
}
