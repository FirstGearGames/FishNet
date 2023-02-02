using FishNet;
using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletTest : NetworkBehaviour
{
    public float Force;

    public void SendSpeed()
    {
        //Debug.Log("Local tick " + InstanceFinder.TimeManager.LocalTick);
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.AddForce((transform.forward * Force), ForceMode.Impulse);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        SendSpeed();
        StartCoroutine(__DelayDestroy(3f));
    }

    private void Update()
    {
        if (base.TimeManager.FrameTicked)
        {
            //Rigidbody rb = GetComponent<Rigidbody>();
            //Debug.Log(rb.velocity.z);
        }
    }
    private IEnumerator __DelayDestroy(float time)
    {
        yield return new WaitForSeconds(time);
        base.Despawn();
    }
}
