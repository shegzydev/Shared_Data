using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallSynchronizer : MonoBehaviour
{
    public Rigidbody2D[] sourceBalls;
    float radius;

    void Start()
    {
        radius = 0.0585f * 100 / 2;
    }

    void Update()
    {

    }

    void LateUpdate()
    {
        foreach (var item in sourceBalls)
        {
            var axis = Vector3.Cross(item.linearVelocity, Vector3.forward);
            var angularVel = Mathf.Rad2Deg * item.linearVelocity.magnitude / radius;
            item.transform.GetChild(0).Rotate(axis, angularVel * Time.deltaTime, Space.World);
            //Debug.DrawRay(item.position, axis * 20);
        }
    }
}
