#if FISHNET_THREADED_COLLIDER_ROLLBACK
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
#if UNITY_6000_5_OR_NEWER
using SceneHandle = System.UInt64;
#else
using SceneHandle = System.Int32;
#endif

namespace FishNet.Component.ColliderRollback
{
    public partial class RollbackManager
    {
	    internal enum DeferredRollbackOrder : byte
	    {
		    PreTick = 0,
		    Tick = 1,
		    PostTick = 2
	    }
	    
        internal enum BoundingBoxType
        {
            /// <summary>
            /// Disable this feature.
            /// </summary>
            Disabled,
            /// <summary>
            /// Manually specify the dimensions of a bounding box.
            /// </summary>
            Manual
        }

        internal struct BoundingBoxData
        {
	        public RollbackPhysicsType rollbackPhysicsType;
	        public BoundingBoxType boundingBoxType;
	        public float3 extends;
	        public float3 center;
	        public quaternion localRotation;

	        public BoundingBoxData(RollbackPhysicsType rollbackPhysicsType, BoundingBoxType boundingBoxType,
		        float3 extends, float3 center, quaternion localRotation)
	        {
		        this.rollbackPhysicsType = rollbackPhysicsType;
		        this.boundingBoxType = boundingBoxType;
		        this.extends = extends;
		        this.center = center;
		        this.localRotation = localRotation;
	        }
        }
        
        /// <summary>
        /// A deferred rollback request.
        /// </summary>
        public struct RollbackRequest
        {
	        public SceneHandle sceneHandle;
	        public float3 origin;
	        public float3 direction;
	        public float distance;
	        public float time;
	        public RollbackPhysicsType rollbackPhysicsType;

	        public RollbackRequest(SceneHandle sceneHandle, float3 origin, float3 direction, float distance, float time, RollbackPhysicsType rollbackPhysicsType)
	        {
		        this.sceneHandle = sceneHandle;
		        this.origin = origin;
		        this.direction = direction;
		        this.distance = distance;
		        this.time = time;
		        this.rollbackPhysicsType = rollbackPhysicsType;
	        }
        }

        // PROSTART
        internal enum FrameRollbackTypes
        {
            LerpFirst,
            LerpMiddle,
            Exact
        }
        
        #region Jobs

        /// <summary>
        /// Increments available frames per ColliderRollback (saturates at MaxSnapshots) if not frozen.
        /// </summary>
        [BurstCompile]
        internal struct IncrementGroupsFramesJob : IJobParallelFor
        {
	        public int maxSnapshots;
	        public NativeArray<int> colliderRollbacksLerpFrames; // [colliderRollbackIdx]
	        [ReadOnly] public NativeArray<byte> colliderRollbacksRolledBackMask; // [colliderRollbackIdx]

	        public void Execute(int colliderRollbackIdx)
	        {
		        if (colliderRollbacksRolledBackMask[colliderRollbackIdx] != 0)
			        return;
		        int f = colliderRollbacksLerpFrames[colliderRollbackIdx];
		        if (f < maxSnapshots) colliderRollbacksLerpFrames[colliderRollbackIdx] = f + 1;
	        }
        }
        /// <summary>
        /// Reads TRS from TransformAccess and appends a snapshot into the ring buffer
        /// for every non-rolled-back collider rollback transform.
        /// </summary>
        [BurstCompile]
        internal struct PopulateColliderRollbackSnapshotsJob : IJobParallelForTransform
        {
	        public NativeArray<ColliderSnapshot> colliderRollbackSnapshots;       // [colliderRollbackIdx]
	        [ReadOnly] public NativeArray<byte> colliderRollbacksRolledBackMask;  // [colliderRollbackIdx]
			
	        public void Execute(int colliderRollbackIdx, TransformAccess transform)
	        {
		        if (colliderRollbacksRolledBackMask[colliderRollbackIdx] != 0)
			        return;
		        
		        colliderRollbackSnapshots[colliderRollbackIdx] = new(transform);
	        }
        }
        /// <summary>
        /// Reads TRS from TransformAccess and appends a snapshot into the ring buffer
        /// for every non-rolled-back rolling collider transform.
        /// </summary>
        [BurstCompile]
        internal struct PopulateRollingColliderSnapshotsJob : IJobParallelForTransform
        {
	        public int maxSnapshots;
			
	        [NativeDisableParallelForRestriction]
	        public NativeArray<ColliderSnapshot> rollingCollidersSnapshots;       // [rollingColliderIdx * Max + frame]
	        public NativeArray<int> rollingCollidersWriteIndices;                 // [rollingColliderIdx]
			
	        [NativeDisableParallelForRestriction]
	        [ReadOnly] public NativeArray<byte> colliderRollbacksRolledBackMask;  // [colliderRollbackIdx]
	        [ReadOnly] public NativeArray<int> colliderToColliderRollbacks;       // [rollingColliderIdx] -> colliderRollbackIdx
			
	        public void Execute(int rollingColliderIdx, TransformAccess transform)
	        {
		        int colliderRollbacksIndex = colliderToColliderRollbacks[rollingColliderIdx];
		        if (colliderRollbacksRolledBackMask[colliderRollbacksIndex] != 0)
			        return;
				
		        int writeIndex = rollingCollidersWriteIndices[rollingColliderIdx];
		        int baseOffset = rollingColliderIdx * maxSnapshots;
				
		        // Can`t update values in exist collection, so recreate this value
		        rollingCollidersSnapshots[baseOffset + writeIndex] = new(transform);

		        writeIndex++;
		        if (writeIndex >= maxSnapshots)
			        writeIndex = 0;
		        rollingCollidersWriteIndices[rollingColliderIdx] = writeIndex;
	        }
        }
		/// <summary>
		/// Applies rollback to all transforms in parallel.
		/// </summary>
		[BurstCompile]
		internal struct ApplyRollbackJob : IJobParallelForTransform
		{
			public SceneHandle sceneHandle;
		    public int maxSnapshots;
		    public float decimalFrame;

		    [ReadOnly] public NativeArray<int> colliderToColliderRollbacks;            // [rollingColliderIdx] -> colliderRollbacksIdx
		    [ReadOnly] public NativeArray<SceneHandle> colliderRollbacksSceneHandles;          // [colliderRollbackIdx], scene handles
		    [ReadOnly] public NativeArray<int> colliderRollbacksLerpFrames;            // [colliderRollbackIdx], available frames
		    [ReadOnly] public NativeArray<int> rollingCollidersWriteIndices;           // [rollingColliderIdx]
		    
		    [NativeDisableParallelForRestriction]
		    public NativeArray<byte> colliderRollbacksRolledBackMask;                  // [colliderRollbackIdx]
		    
		    [NativeDisableParallelForRestriction]
		    [ReadOnly] public NativeArray<ColliderSnapshot> rollingCollidersSnapshots; // [rollingColliderIdx * Max + frame]

		    public void Execute(int rollingColliderIdx, TransformAccess transform)
		    {
		        int colliderRollbackIdx = colliderToColliderRollbacks[rollingColliderIdx];
		        SceneHandle colliderRollbacksSceneHandle = colliderRollbacksSceneHandles[colliderRollbackIdx];
				if (sceneHandle != 0 && sceneHandle != colliderRollbacksSceneHandle)
					return;
		        
		        int frames = colliderRollbacksLerpFrames[colliderRollbackIdx];
		        if (frames <= 0)
		            return;

		        /* If time were 0.3f and delta was 0.2f then the
		         * result would be 1.5f. This indicates to lerp between
		         * the first snapshot, and one after. */
		        GetRollbackSettings(decimalFrame, frames,
			        out FrameRollbackTypes mode, out int endFrame, out float percent);

		        int writeIdx = rollingCollidersWriteIndices[rollingColliderIdx];
		        int baseOffset = rollingColliderIdx * maxSnapshots;
		        int lastIdx = (writeIdx - 1 + maxSnapshots) % maxSnapshots;
		        bool isRecycled = frames >= maxSnapshots;
		        
		        ApplyFromSnapshots(
			        mode, endFrame, percent,
			        baseOffset, lastIdx, isRecycled,
			        maxSnapshots, rollingCollidersSnapshots, transform);

		        colliderRollbacksRolledBackMask[colliderRollbackIdx] = 1;
		    }
		}
		/// <summary>
		/// Applies rollback for transforms whose colliderRollbacks are hit by the ray (origin/dir/dist) against per-colliderRollbacks OBB,
		/// filtered by scene and physics type. No masks, pure math test in the job.
		/// </summary>
		[BurstCompile]
		internal struct ApplyRollbackRaycastJob : IJobParallelForTransform
		{
		    public SceneHandle sceneHandle;
		    public int maxSnapshots;
		    public float decimalFrame;
		    
		    public float3 origin;
		    public float3 dir;
		    public float distance;
		    public int physicsType; // RollbackPhysicsType

		    [ReadOnly] public NativeArray<int> colliderToColliderRollbacks;                  // [rollingColliderIdx] -> colliderRollbacksIdx
		    [ReadOnly] public NativeArray<SceneHandle> colliderRollbacksSceneHandles;                // [colliderRollbackIdx] -> scene.handle
		    [ReadOnly] public NativeArray<int> colliderRollbacksLerpFrames;                  // [colliderRollbackIdx] available frames
		    [ReadOnly] public NativeArray<int> rollingCollidersWriteIndices;                 // [rollingColliderIdx]
		    [ReadOnly] public NativeArray<BoundingBoxData> colliderRollbacksBoundingBoxData; // [colliderRollbackIdx]

		    [NativeDisableParallelForRestriction]
		    public NativeArray<byte> colliderRollbacksRolledBackMask;                        // [colliderRollbackIdx]
		    [NativeDisableParallelForRestriction]
		    [ReadOnly] public NativeArray<ColliderSnapshot> colliderRollbacksSnapshots;      // [colliderRollbackIdx]
		    [NativeDisableParallelForRestriction]
		    [ReadOnly] public NativeArray<ColliderSnapshot> rollingCollidersSnapshots;       // [rollingColliderIdx]

		    public void Execute(int rollingColliderIdx, TransformAccess transform)
		    {
		        int colliderRollbackIdx = colliderToColliderRollbacks[rollingColliderIdx];
		        if (sceneHandle != 0 && colliderRollbacksSceneHandles[colliderRollbackIdx] != sceneHandle)
		            return;

		        int frames = colliderRollbacksLerpFrames[colliderRollbackIdx];
		        if (frames <= 0)
			        return;
				
		        BoundingBoxData boundingBoxData = colliderRollbacksBoundingBoxData[colliderRollbackIdx];
		        if ((int)boundingBoxData.rollbackPhysicsType != physicsType)
		            return;

		        ColliderSnapshot colliderRollbackSnapshot = colliderRollbacksSnapshots[colliderRollbackIdx];
		        float3 groupPosWS = colliderRollbackSnapshot.WorldPosition;
		        quaternion groupRotWS = colliderRollbackSnapshot.WorldRotation;
		        quaternion obbRotWS = math.mul(groupRotWS, boundingBoxData.localRotation);
		        float3 centerWS = groupPosWS + math.rotate(groupRotWS, boundingBoxData.center);
		        float3 extendsWS = math.abs(boundingBoxData.extends);
				
		        // skip if invalid (zero extents)
		        if (extendsWS.x <= 0f || extendsWS.y <= 0f ||
		            (physicsType == (int)RollbackPhysicsType.Physics && extendsWS.z <= 0f))
		            return;
		        
				// Ray -> OBB local space (use the OBB world rotation)
		        quaternion invObbRot = math.inverse(obbRotWS);
		        float3 rayOriginLocal = math.rotate(invObbRot, origin - centerWS);
		        float3 rayDirLocal    = math.rotate(invObbRot, dir);

		        // For 2D case: treat OBB as thin XY box; guard Z numerics
		        if (physicsType == (int)RollbackPhysicsType.Physics2D)
		        {
			        rayOriginLocal.z = 0f;
		            if (math.abs(rayDirLocal.z) < 1e-6f) rayDirLocal.z = 0f;
		            if (extendsWS.z < 1e-4f) extendsWS.z = 1e-4f;
		        }

		        if (!RayAabbSlab(rayOriginLocal, rayDirLocal, extendsWS, distance))
		            return;
		        
		        GetRollbackSettings(decimalFrame, frames,
			        out FrameRollbackTypes mode, out int endFrame, out float percent);

		        int writeIdx = rollingCollidersWriteIndices[rollingColliderIdx];
		        int baseOffset = rollingColliderIdx * maxSnapshots;
		        int lastIdx = (writeIdx - 1 + maxSnapshots) % maxSnapshots;
		        bool isRecycled = frames >= maxSnapshots;
		        
		        ApplyFromSnapshots(
			        mode, endFrame, percent,
			        baseOffset, lastIdx, isRecycled,
			        maxSnapshots, rollingCollidersSnapshots, transform);
		        
		        colliderRollbacksRolledBackMask[colliderRollbackIdx] = 1;
		    }
		}
		
		/// <summary>
        /// Computes per-colliderRollback sum/count of requested rollback decimal frames.
        /// One iteration per colliderRollbackIdx; loops over all requests (typically small).
        /// </summary>
        [BurstCompile]
        internal struct ComputeDeferredRollbackSumsJob : IJobParallelFor
        {
            public float tickDelta;

            [ReadOnly] public NativeArray<RollbackRequest> requests;

            [ReadOnly] public NativeArray<SceneHandle> colliderRollbacksSceneHandles;                // [colliderRollbackIdx]
            [ReadOnly] public NativeArray<int> colliderRollbacksLerpFrames;                  // [colliderRollbackIdx]
            [ReadOnly] public NativeArray<BoundingBoxData> colliderRollbacksBoundingBoxData; // [colliderRollbackIdx]
            [ReadOnly] public NativeArray<ColliderSnapshot> colliderRollbacksSnapshots;      // [colliderRollbackIdx]

            public NativeArray<float> sumDecimalFrame;                                       // [colliderRollbackIdx]
            public NativeArray<int> hitCount;                                                // [colliderRollbackIdx]

            public void Execute(int colliderRollbackIdx)
            {
	            int frames = colliderRollbacksLerpFrames[colliderRollbackIdx];
	            if (frames <= 0)
		            return;

                SceneHandle sceneHandle = colliderRollbacksSceneHandles[colliderRollbackIdx];
                BoundingBoxData boundingBoxData = colliderRollbacksBoundingBoxData[colliderRollbackIdx];

                ColliderSnapshot colliderRollbackSnapshot = colliderRollbacksSnapshots[colliderRollbackIdx];
                float3 groupPosWS = colliderRollbackSnapshot.WorldPosition;
                quaternion groupRotWS = colliderRollbackSnapshot.WorldRotation;
                quaternion obbRotWS = math.mul(groupRotWS, boundingBoxData.localRotation);
                float3 centerWS = groupPosWS + math.rotate(groupRotWS, boundingBoxData.center);
                float3 extendsWS = math.abs(boundingBoxData.extends);
                
		        // skip if invalid (zero extents)
		        if (extendsWS.x <= 0f || extendsWS.y <= 0f ||
		            (boundingBoxData.rollbackPhysicsType == RollbackPhysicsType.Physics && extendsWS.z <= 0f))
		            return;

                float sum = 0f;
                int cnt = 0;

                for (int i = 0; i < requests.Length; i++)
                {
                    RollbackRequest r = requests[i];

                    if (r.sceneHandle != 0 && r.sceneHandle != sceneHandle)
                        continue;

                    if ((r.rollbackPhysicsType & boundingBoxData.rollbackPhysicsType) == 0)
                        continue;

                    // Ray -> OBB local space (use the OBB world rotation)
                    quaternion invObbRot = math.inverse(obbRotWS);
                    float3 rayOriginLocal = math.rotate(invObbRot, r.origin - centerWS);
                    float3 rayDirLocal = math.rotate(invObbRot, r.direction);
                    
                    // For 2D case: treat OBB as thin XY box; guard Z numerics
                    if (boundingBoxData.rollbackPhysicsType == RollbackPhysicsType.Physics2D)
                    {
	                    rayOriginLocal.z = 0f;
	                    if (math.abs(rayDirLocal.z) < 1e-6f) rayDirLocal.z = 0f;
	                    if (extendsWS.z < 1e-4f) extendsWS.z = 1e-4f;
                    }

                    if (!RayAabbSlab(rayOriginLocal, rayDirLocal, extendsWS, r.distance))
                        continue;

                    float decimalFrame = (tickDelta > 0f) ? (r.time / tickDelta) : 0f;
                    sum += decimalFrame;
                    cnt++;
                }

                if (cnt > 0)
                {
                    sumDecimalFrame[colliderRollbackIdx] = sum;
                    hitCount[colliderRollbackIdx] = cnt;
                }
            }
        }
		
		/// <summary>
        /// Applies rollback using per-colliderRollback averaged decimal frame (sum/count).
        /// </summary>
        [BurstCompile]
        internal struct ApplyDeferredRollbackJob : IJobParallelForTransform
        {
            public int maxSnapshots;

            [ReadOnly] public NativeArray<int> colliderToColliderRollbacks;            // [rollingColliderIdx] -> colliderRollbackIdx
            [ReadOnly] public NativeArray<int> colliderRollbacksLerpFrames;            // [colliderRollbackIdx]
            [ReadOnly] public NativeArray<int> rollingCollidersWriteIndices;           // [rollingColliderIdx]

            [ReadOnly] public NativeArray<float> sumDecimalFrame;                      // [colliderRollbackIdx]
            [ReadOnly] public NativeArray<int> hitCount;                               // [colliderRollbackIdx]

            [NativeDisableParallelForRestriction]
            public NativeArray<byte> colliderRollbacksRolledBackMask;                  // [colliderRollbackIdx]

            [NativeDisableParallelForRestriction]
            [ReadOnly] public NativeArray<ColliderSnapshot> rollingCollidersSnapshots; // [rollingColliderIdx * Max + frame]

            public void Execute(int rollingColliderIdx, TransformAccess transform)
            {
                int colliderRollbackIdx = colliderToColliderRollbacks[rollingColliderIdx];

                int cnt = hitCount[colliderRollbackIdx];
                if (cnt <= 0)
                    return;

                int frames = colliderRollbacksLerpFrames[colliderRollbackIdx];
                if (frames <= 0)
                    return;

                float decimalFrame = sumDecimalFrame[colliderRollbackIdx] / cnt;

                GetRollbackSettings(decimalFrame, frames, out FrameRollbackTypes mode, out int endFrame, out float percent);

                int writeIdx = rollingCollidersWriteIndices[rollingColliderIdx];
                int baseOffset = rollingColliderIdx * maxSnapshots;
                int lastIdx = (writeIdx - 1 + maxSnapshots) % maxSnapshots;
                bool isRecycled = frames >= maxSnapshots;

                ApplyFromSnapshots(
                    mode, endFrame, percent,
                    baseOffset, lastIdx, isRecycled,
                    maxSnapshots, rollingCollidersSnapshots, transform);

                colliderRollbacksRolledBackMask[colliderRollbackIdx] = 1;
            }
        }
		
		/// <summary>
		/// Returns rollback state to all transforms in parallel.
		/// </summary>
		[BurstCompile]
		internal struct ReturnRollbackAllJob : IJobParallelForTransform
		{
			public int maxSnapshots;
			
			[ReadOnly] public NativeArray<int> colliderToColliderRollbacks;            // [rollingColliderIdx] -> colliderRollbacksIdx
			[ReadOnly] public NativeArray<int> colliderRollbacksLerpFrames;            // [colliderRollbackIdx] available frames
			[ReadOnly] public NativeArray<int> rollingCollidersWriteIndices;           // [rollingColliderIdx]
			
			[NativeDisableParallelForRestriction]
			public NativeArray<byte> colliderRollbacksRolledBackMask;                  // [colliderRollbackIdx]
			[NativeDisableParallelForRestriction]
			[ReadOnly] public NativeArray<ColliderSnapshot> rollingCollidersSnapshots; // [rollingColliderIdx * Max + frame]

			public void Execute(int index, TransformAccess transform)
			{
				int colliderRollbackIdx = colliderToColliderRollbacks[index];
				
				int frames = colliderRollbacksLerpFrames[colliderRollbackIdx];
				if (frames <= 0)
					return;

				int writeIdx = rollingCollidersWriteIndices[index];
				int baseOffset = index * maxSnapshots;
				int lastIdx = (writeIdx - 1 + maxSnapshots) % maxSnapshots;

				// Return to the newest (last written) snapshot
				int snapshotIndex = baseOffset + lastIdx;
				ColliderSnapshot s = rollingCollidersSnapshots[snapshotIndex];
				transform.SetPositionAndRotation(s.WorldPosition, s.WorldRotation);

				colliderRollbacksRolledBackMask[colliderRollbackIdx] = 0;
			}
		}
		
		/// <summary>
	    /// Slab test in OBB local space: returns true if segment [0, maxDist] hits the box.
	    /// </summary>
	    [MethodImpl(MethodImplOptions.AggressiveInlining)]
	    private static bool RayAabbSlab(in float3 rayOrigin, in float3 rayDir, in float3 ext, float maxDist)
	    {
		    float tmin = 0f, tmax = maxDist;

		    // X
		    if (!AxisSlab(rayOrigin.x, rayDir.x, -ext.x, ext.x, ref tmin, ref tmax)) return false;
		    // Y
		    if (!AxisSlab(rayOrigin.y, rayDir.y, -ext.y, ext.y, ref tmin, ref tmax)) return false;
		    // Z
		    if (!AxisSlab(rayOrigin.z, rayDir.z, -ext.z, ext.z, ref tmin, ref tmax)) return false;

		    return tmax >= 0f && tmin <= maxDist;
	    }
	    
	    /// <summary>
	    /// 1D slab intersection along a single axis in OBB local space.
	    /// Shrinks [tmin, tmax] by intersecting with [tEntryAxis, tExitAxis].
	    /// Returns false if the interval becomes empty (no hit).
	    /// </summary>
	    [MethodImpl(MethodImplOptions.AggressiveInlining)]
	    private static bool AxisSlab(float rayOrigin, float rayDir, float min, float max, ref float tmin, ref float tmax)
	    {
		    const float EPS = 1e-6f;
		    if (math.abs(rayDir) < EPS)
			    return rayOrigin >= min && rayOrigin <= max;

		    float inv = 1f / rayDir;
		    float t1 = (min - rayOrigin) * inv;
		    float t2 = (max - rayOrigin) * inv;
		    if (t1 > t2) (t1, t2) = (t2, t1);

		    if (t1 > tmin) tmin = t1;
		    if (t2 < tmax) tmax = t2;
		    return tmin <= tmax;
	    }
	    
	    [MethodImpl(MethodImplOptions.AggressiveInlining)]
	    private static void GetRollbackSettings(float decimalFrame, int frames,
		    out FrameRollbackTypes mode, out int endFrame, out float percent)
	    {
		    /* Rollback is beyond written quantity.
		     * Set to use the last snapshot. */
		    if (decimalFrame > frames)
		    {
			    mode = FrameRollbackTypes.Exact;
			    // Be sure to subtract 1 to get last entry in snapshots.
			    endFrame = frames - 1;
			    // Not needed for exact but must be set.
			    percent = 1f;
		    }
		    else
		    {
			    percent  = decimalFrame % 1f;
			    endFrame = (int)math.ceil(decimalFrame);
			
			    /* If the end frame is larger than or equal to 1
			     * then a lerp between two snapshots can occur. If
			     * equal to 1 then the lerp would occur between 0 and 1. */
			    if (endFrame >= 1)
			    {
				    mode = FrameRollbackTypes.LerpMiddle;
			    }
			    // Rolling back only 1 frame.
			    else
			    {
				    endFrame = 0;
				    mode = FrameRollbackTypes.LerpFirst;
			    }
		    }
	    }
	    
	    [MethodImpl(MethodImplOptions.AggressiveInlining)]
	    private static int BufIndex(int baseOffset, int history, int lastIdx, bool isRecycled, int maxSnapshots)
	    {
		    int idx = baseOffset + lastIdx - history;
		    // If negative value start taking from the back.
		    if (idx < 0)
		    {
			    /* Cannot take from back, snapshots aren't filled yet.
			     * Instead take the oldest snapshot, which in this case
			     * would be index baseOffset. */
			    if (!isRecycled)
				    return baseOffset;
			    // Snapshots filled, take from back.
			    else
				    return idx + maxSnapshots;
		    }
		    // Not a negative value, return as is.
		    else return idx;
	    }
	    
	    /// <summary>
	    /// Applies transform from snapshots according to the selected mode.
	    /// </summary>
	    [MethodImpl(MethodImplOptions.AggressiveInlining)]
	    private static void ApplyFromSnapshots(
		    FrameRollbackTypes mode, int endFrame, float percent,
		    int baseOffset, int lastIdx, bool isRecycled, int maxSnapshots,
		    NativeArray<ColliderSnapshot> snapshots, TransformAccess t)
	    {
		    if (mode == FrameRollbackTypes.Exact)
		    {
			    ColliderSnapshot s = snapshots[BufIndex(baseOffset, endFrame, lastIdx, isRecycled, maxSnapshots)];
			    t.SetPositionAndRotation(s.WorldPosition, s.WorldRotation);
		    }
		    else if (mode == FrameRollbackTypes.LerpFirst)
		    {
			    ColliderSnapshot s = snapshots[BufIndex(baseOffset, endFrame, lastIdx, isRecycled, maxSnapshots)];
			    t.GetPositionAndRotation(out Vector3 curPos, out Quaternion curRot);
			    t.SetPositionAndRotation(
				    math.lerp(curPos, s.WorldPosition, percent),
				    math.nlerp(curRot, s.WorldRotation, percent));
		    }
		    else // LerpMiddle
		    {
			    ColliderSnapshot s0 = snapshots[BufIndex(baseOffset, endFrame - 1, lastIdx, isRecycled, maxSnapshots)];
			    ColliderSnapshot s1 = snapshots[BufIndex(baseOffset, endFrame,     lastIdx, isRecycled, maxSnapshots)];
			    t.SetPositionAndRotation(
				    math.lerp(s0.WorldPosition, s1.WorldPosition, percent),
				    math.nlerp(s0.WorldRotation, s1.WorldRotation, percent));
		    }
	    }
		#endregion
        // PROEND
    }
}
#endif