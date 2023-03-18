using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using UnityEngine;

public class PredictedBullet : NetworkBehaviour
{
    [SyncVar(OnChange = nameof(_startingForce_OnChange))]
    private Vector3 _startingForce;
    public void SetStartingForce(Vector3 value)
    {
        /* If the object is not yet initialized then
         * this is being set prior to network spawning.
         * Such a scenario occurs because the client which is
         * predicted spawning sets the synctype value before network
         * spawning to ensure the server receives the value.
         * Just as when the server sets synctypes, if they are set
         * before the object is spawned it's gauranteed clients will
         * get the value in the spawn packet; same practice is used here. */
        if (!base.IsSpawned)
            SetVelocity(value);

        _startingForce = value;
    }

    //Simple delay destroy so object does not exist forever.
    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(__DelayDestroy(3f));
    }

    /// <summary>
    /// When starting force changes set that velocity to the rigidbody.
    /// This is an example as how a predicted spawn can modify sync types for server and other clients.
    /// </summary>
    private void _startingForce_OnChange(Vector3 prev, Vector3 next, bool asServer)
    {
        SetVelocity(next);
    }

    /// <summary>
    /// Sets velocity of the rigidbody.
    /// </summary>
    public void SetVelocity(Vector3 value)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.velocity = value;
    }

    /// <summary>
    /// Destroy object after time.
    /// </summary>
    private IEnumerator __DelayDestroy(float time)
    {
        yield return new WaitForSeconds(time);
        base.Despawn();
    }

}
