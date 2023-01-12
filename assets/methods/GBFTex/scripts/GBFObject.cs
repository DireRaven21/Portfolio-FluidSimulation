using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GBFObject : MonoBehaviour {
    Vector3 p_pos;
    Vector3 c_pos;

    public Vector3 velocity {
        get { return (c_pos - p_pos) * (1 / Time.fixedDeltaTime); }
    }
    void Start() {
        c_pos = p_pos = transform.position;
    }

    void FixedUpdate() {
        p_pos = c_pos;
        c_pos = transform.position;
    }
}
