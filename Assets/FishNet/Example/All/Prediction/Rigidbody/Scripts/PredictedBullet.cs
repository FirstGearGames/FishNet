using FishNet.Component.Prediction;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using UnityEngine;

public class PredictedBullet : NetworkBehaviour
{
    //SyncVar to set spawn force. This is set by predicted spawner and sent to the server.
    [SyncVar(OnChange = nameof(_startingForce_OnChange))]
    private Vector3 _startingForce;
    //Tick to set rb to kinematic.
    private uint _stopTick = uint.MaxValue;

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

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        uint timeToTicks = base.TimeManager.TimeToTicks(0.65f);
        /* If server or predicted spawner then add the kinematic
         * tick onto local. Predicted spawner and server should behave
         * as though no time has elapsed since this spawned. */
        if (base.IsServer || base.Owner.IsLocalClient)
        {
            _stopTick = base.TimeManager.LocalTick + timeToTicks;
        }
        /* If not server or a client that did not predicted spawn this
         * then calculate time passed and set kinematic tick to the same
         * amount in the future while subtracting already passed ticks. */
        else
        {
            uint passed = (uint)Mathf.Max(1, base.TimeManager.Tick - base.TimeManager.LastPacketTick);
            long stopTick = (base.TimeManager.Tick + timeToTicks - passed - 1);
            if (stopTick > 0)
                _stopTick = (uint)stopTick;
            else
                _stopTick = 0;
        }

        base.TimeManager.OnTick += TimeManager_OnTick;
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        base.TimeManager.OnTick -= TimeManager_OnTick;
    }
    private void TimeManager_OnTick()
    {
        if (base.TimeManager.LocalTick >= _stopTick)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
        }
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
