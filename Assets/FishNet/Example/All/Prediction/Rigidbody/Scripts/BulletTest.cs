using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletTest : NetworkBehaviour
{
    [SyncObject]
    public readonly SyncList<int> SyncListNumbers = new SyncList<int>();

    [SyncVar]
    public Vector3 SyncVarScale = Vector3.one;

    public float Force;

    public void SendSpeed()
    {
        //Debug.Log("Local tick " + InstanceFinder.TimeManager.LocalTick);
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.AddForce((transform.forward * Force), ForceMode.Impulse);
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        transform.localScale = SyncVarScale;

        Debug.Log("Reading numbers");
        foreach (var item in SyncListNumbers)
            Debug.Log(item);

    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        SendSpeed();
        StartCoroutine(__DelayDestroy(3f));
    }

    private void Update()
    {
        if (base.TimeManager == null)
            return;
        if (base.TimeManager.FrameTicked)
        {
//            Debug.Log(_scale);
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
