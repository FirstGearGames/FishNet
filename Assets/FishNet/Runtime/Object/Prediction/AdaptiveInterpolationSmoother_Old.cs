//using GameKit.Utilities; //Remove on 04/01/01
//using FishNet.Transporting;
//using FishNet.Utility.Extension;
//using System.Runtime.CompilerServices;
//using UnityEngine;
//using FishNet.Managing.Timing;
//using System.Collections.Generic;
//using FishNet.Managing.Scened;

//namespace FishNet.Object.Prediction
//{
//	internal class AdaptiveInterpolationSmoother
//	{
//		#if PREDICTION_V2

//		#region Types.

//		/// <summary>
//		/// Data on a goal to move towards.
//		/// </summary>
//		private class GoalData : IResettable
//		{
//			/// <summary>
//			/// True if this GoalData is valid.
//			/// </summary>
//			public bool IsValid;

//			/// <summary>
//			/// Tick of the data this GoalData is for.
//			/// </summary>
//			public uint DataTick;

//			/// <summary>
//			/// Data on how fast to move to transform values.
//			/// </summary>
//			public RateData MoveRates = new RateData();

//			/// <summary>
//			/// Transform values to move towards.
//			/// </summary> 
//			public TransformPropertiesCls TransformProperties = new TransformPropertiesCls();

//			public GoalData() { }

//			public void InitializeState() { }

//			public void ResetState()
//			{
//				DataTick = 0;
//				TransformProperties.ResetState();
//				MoveRates.ResetState();
//				IsValid = false;
//			}

//			/// <summary>
//			/// Updates values using a GoalData.
//			/// </summary>
//			public void Update(GoalData gd)
//			{
//				DataTick = gd.DataTick;
//				MoveRates.Update(gd.MoveRates);
//				TransformProperties.Update(gd.TransformProperties);
//				IsValid = true;
//			}

//			public void Update(uint dataTick, RateData rd, TransformPropertiesCls tp)
//			{
//				DataTick = dataTick;
//				MoveRates = rd;
//				TransformProperties = tp;
//				IsValid = true;
//			}
//		}

//		/// <summary>
//		/// How fast to move to values.
//		/// </summary>
//		private class RateData : IResettable
//		{
//			/// <summary>
//			/// Rate for position after smart calculations.
//			/// </summary>
//			public float Position;

//			/// <summary>
//			/// Rate for rotation after smart calculations.
//			/// </summary>
//			public float Rotation;

//			/// <summary>
//			/// Number of ticks the rates are calculated for.
//			/// If TickSpan is 2 then the rates are calculated under the assumption the transform changed over 2 ticks.
//			/// </summary>
//			public uint TickSpan;

//			/// <summary>
//			/// Time remaining until transform is expected to reach it's goal.
//			/// </summary>
//			internal float TimeRemaining;

//			public RateData() { }

//			public void InitializeState() { }

//			/// <summary>
//			/// Resets values for re-use.
//			/// </summary>
//			public void ResetState()
//			{
//				Position = 0f;
//				Rotation = 0f;
//				TickSpan = 0;
//				TimeRemaining = 0f;
//			}

//			[MethodImpl(MethodImplOptions.AggressiveInlining)]
//			public void Update(RateData rd)
//			{
//				Update(rd.Position, rd.Rotation, rd.TickSpan, rd.TimeRemaining);
//			}

//			/// <summary>
//			/// Updates rates.
//			/// </summary>
//			public void Update(float position, float rotation, uint tickSpan, float timeRemaining)
//			{
//				Position = position;
//				Rotation = rotation;
//				TickSpan = tickSpan;
//				TimeRemaining = timeRemaining;
//			}
//		}

//		#endregion

//		#region Private.

//		/// <summary>
//		/// Offsets of the graphical object when this was initialized.
//		/// </summary>
//		private TransformProperties _graphicalInitializedValues;

//		/// <summary>
//		/// Offsets of the graphical object during PreTick. This could also be the offset PreReplay.
//		/// </summary>
//		private TransformProperties _graphicalPretickValues;

//		/// <summary>
//		/// Offsets of the root transform before simulating. This could be PreTick, or PreReplay.
//		/// </summary>
//		private TransformProperties _rootPreSimulateValues;

//		/// <summary>
//		/// SmoothingData to use.
//		/// </summary>
//		private AdaptiveInterpolationSmoothingData _smoothingData;

//		/// <summary>
//		/// Current interpolation value. This changes based on ping and settings.
//		/// </summary>
//		private long _currentInterpolation = 2;

//		/// <summary>
//		/// Target interpolation when collision is exited. This changes based on ping and settings.
//		/// </summary>
//		private uint _targetInterpolation;

//		/// <summary>
//		/// Target interpolation when collision is entered. This changes based on ping and settings.
//		/// </summary>
//		private uint _targetCollisionInterpolation;

//		/// <summary>
//		/// Current GoalData being used.
//		/// </summary>
//		private GoalData _currentGoalData = new GoalData();

//		/// <summary>
//		/// GoalDatas to move towards.
//		/// </summary>
//		//private RingBuffer<GoalData> _goalDatas = new RingBuffer<GoalData>();
//		private List<GoalData> _goalDatas = new List<GoalData>();

//		/// <summary>
//		/// Last ping value when it was checked.
//		/// </summary>
//		private long _lastPing = long.MinValue;

//		/// <summary>
//		/// Cached NetworkObject reference in SmoothingData for performance.
//		/// </summary>
//		private NetworkObject _networkObject;

//		#endregion

//		#region Const.

//		/// <summary>
//		/// Multiplier to apply to movement speed when buffer is over interpolation.
//		/// </summary>
//		private const float OVERFLOW_MULTIPLIER = 0.1f;

//		/// <summary>
//		/// Multiplier to apply to movement speed when buffer is under interpolation.
//		/// </summary>
//		private const float UNDERFLOW_MULTIPLIER = 0.02f;

//		#endregion

//		public AdaptiveInterpolationSmoother()
//		{
//			/* Initialize for up to 50
//			 * goal datas. Anything beyond that
//			 * is unreasonable. */
//			//_goalDatas.Initialize(50);
//		}

//		/// <summary>
//		/// Initializes this for use.
//		/// </summary>
//		internal void Initialize(AdaptiveInterpolationSmoothingData data)
//		{
//			_smoothingData = data;
//			_networkObject = data.NetworkObject;
//			SetGraphicalObject(data.GraphicalObject);
//		}

//		/// <summary>
//		/// <summary>
//		/// Called every frame.
//		/// </summary>
//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		public void Update()
//		{
//			if (CanSmooth())
//				MoveToTarget();
//		}

//		private string GetGoalDataTicks()
//		{
//			string result = string.Empty;
//			foreach (var item in _goalDatas)
//				result += item.DataTick + ", ";

//			return result;
//		}

//		private string _preTickGoalDatas = string.Empty;

//		/// <summary>
//		/// Called when the TimeManager invokes OnPreTick.
//		/// </summary>
//		public void OnPreTick()
//		{
//			_preTickGoalDatas = GetGoalDataTicks();

//			if (CanSmooth())
//			{
//				UpdatePingInterpolation();
//				_graphicalPretickValues.Update(_smoothingData.GraphicalObject);

//				//Update the last post simulate data.
//				UpdateRootPreSimulateOffsets();
//				//UpdateRootPreSimulateOffsets(_networkObject.ReplicateTick.Value(_networkObject.TimeManager) - 1);
//			}
//		}

//		/// <summary>
//		/// Called when the TimeManager invokes OnPostTick.
//		/// </summary>
//		public void OnPostTick()
//		{
//			if (CanSmooth())
//			{
//				//Move towards target interpolation.
//				UpdateCurrentInterpolation();
//				//Reset graphics to start graphicals transforms properties.
//				_smoothingData.GraphicalObject.SetPositionAndRotation(_graphicalPretickValues.Position, _graphicalPretickValues.Rotation);
//				//Create a goal data for new transform position.
//				uint tick = _networkObject.LastUnorderedReplicateTick;
//				//Debug.Log($"GoalDatas count {_goalDatas.Count}. Tick {tick}. LocalTick {_networkObject.TimeManager.LocalTick}");
//				CreatePostSimulateGoalData(tick, true);
//			}
//		}

//		/// <summary>
//		/// Called before a reconcile runs a replay.
//		/// </summary>
//		public void OnPreReplicateReplay(uint clientTick, uint serverTick)
//		{
//			//Update the last post simulate data.
//			if (CanSmooth())
//			{
//				UpdateRootPreSimulateOffsets();
//			}
//		}

//		/// <summary>
//		/// Called after a reconcile runs a replay.
//		/// </summary>
//		public void OnPostReplicateReplay(uint clientTick, uint serverTick)
//		{
//			if (CanSmooth())
//			{
//				/* Create new goal data from the replay.
//				 * This must be done every replay. If a desync
//				 * did occur then the goaldatas would be different
//				 * from what they were previously. */
//				uint tick = _networkObject.LastUnorderedReplicateTick;
//				CreatePostSimulateGoalData(tick, false);
//			}
//		}

//		/// <summary>
//		/// Updates rootPostSimulateOffsets value with root transform's current values.
//		/// </summary>
//		private void UpdateRootPreSimulateOffsets()
//		{
//			Transform t = _networkObject.transform;
//			_rootPreSimulateValues.Update(t.position, t.rotation);
//		}

//		/// <summary>
//		/// Moves current interpolation to target interpolation.
//		/// </summary>
//		private void UpdateCurrentInterpolation()
//		{
//			AdaptiveInterpolationSmoothingData data = _smoothingData;
//			bool colliding = _networkObject.CollidingWithLocalClient();
//			if (colliding)
//				_currentInterpolation -= data.InterpolationDecreaseStep;
//			else
//				_currentInterpolation += data.InterpolationIncreaseStep;

//			_currentInterpolation = (long)Mathf.Clamp(_currentInterpolation, _targetCollisionInterpolation, _targetInterpolation);
//		}

//		/// <summary>
//		/// Updates interpolation values based on ping.
//		/// </summary>
//		private void UpdatePingInterpolation()
//		{
//			/* Only update if ping has changed considerably.
//			 * This will prevent random lag spikes from throwing
//			 * off the interpolation. */
//			long ping = _networkObject.TimeManager.RoundTripTime;
//			ulong difference = (ulong)Mathf.Abs(ping - _lastPing);
//			_lastPing = ping;
//			//Allow update if ping jump is large enough.
//			if (difference > 25)
//				SetTargetSmoothing(ping, false);
//		}

//		/// <summary>
//		/// Sets target smoothing values.
//		/// </summary>
//		/// <param name="setImmediately">True to set current values to targets immediately.</param>
//		private void SetTargetSmoothing(long ping, bool setImmediately)
//		{
//			AdaptiveInterpolationSmoothingData data = _smoothingData;
//			TimeManager tm = _networkObject.TimeManager;
//			double interpolationTime = (ping / 1000d) * data.InterpolationPercent;
//			_targetInterpolation = tm.TimeToTicks(interpolationTime, TickRounding.RoundUp);
//			double collisionInterpolationTime = (ping / 1000d) * data.CollisionInterpolationPercent;
//			_targetCollisionInterpolation = tm.TimeToTicks(collisionInterpolationTime, TickRounding.RoundUp);

//			//If to apply values to targets immediately.
//			if (setImmediately)
//				_currentInterpolation = (_networkObject.CollidingWithLocalClient()) ? _targetCollisionInterpolation : _targetInterpolation;
//		}

//		/// <summary>
//		/// Sets GraphicalObject.
//		/// </summary>
//		/// <param name="value"></param>
//		public void SetGraphicalObject(Transform value)
//		{
//			_smoothingData.GraphicalObject = value;
//			_graphicalInitializedValues = _networkObject.transform.GetTransformOffsets(_smoothingData.GraphicalObject);
//		}

//		/// <summary>
//		/// Returns if the graphics can be smoothed.
//		/// </summary>
//		/// <returns></returns>
//		private bool CanSmooth()
//		{
//			if (_networkObject.IsOwner)
//				return false;

//			if (_networkObject.IsServerOnly)
//				return false;

//			return true;
//		}

//		/// <summary>
//		/// Returns if this transform matches arguments.
//		/// </summary>
//		/// <returns></returns>
//		private bool GraphicalObjectMatches(Vector3 position, Quaternion rotation)
//		{
//			bool positionMatches = (!_smoothingData.SmoothPosition || (_smoothingData.GraphicalObject.position == position));
//			bool rotationMatches = (!_smoothingData.SmoothRotation || (_smoothingData.GraphicalObject.rotation == rotation));

//			return (positionMatches && rotationMatches);
//		}

//		/// <summary>
//		/// Returns if there is any change between two datas.
//		/// </summary>
//		private bool HasChanged(TransformPropertiesCls a, TransformPropertiesCls b)
//		{
//			return (a.Position != b.Position) ||
//				   (a.Rotation != b.Rotation);
//		}

//		///// <summary>
//		///// Returns if the transform differs from td.
//		///// </summary>
//		//private bool HasChanged(TransformPropertiesCls tp)
//		//{
//		//    Transform t = _networkObject.transform;
//		//    bool changed = (tp.Position != t.position) || (tp.Rotation != t.rotation);

//		//    return changed;
//		//}

//		/// <summary>
//		/// Sets CurrentGoalData to the next in queue. Returns if was set successfully.
//		/// </summary>
//		private bool SetCurrentGoalData()
//		{
//			if (_goalDatas.Count == 0)
//			{
//				_currentGoalData.IsValid = false;

//				return false;
//			}
//			else
//			{
//				/* Update to the next goal data.
//				 * We could assign _goalDatas[0] as the
//				 * current and then just remove it from
//				 * the collection. But if did that _currentGoalData
//				 * would have to be disposed first. So all the same,
//				 * we're using the Update then dispose because it's
//				 * a little easier to follow. */
//				_currentGoalData.Update(_goalDatas[0]);
//				Vector3 offset = new Vector3(0f, 10f, 0f);
//				Debug.DrawLine(_goalDatas[0].TransformProperties.Position + offset, _goalDatas[0].TransformProperties.Position - offset, Color.red, 2f);
//				//Store old and remove it.
//				ResettableObjectCaches<GoalData>.Store(_goalDatas[0]);
//				//_goalDatas.RemoveRange(true, 1);
//				_goalDatas.RemoveRange(0, 1);

//				//Debug.LogWarning($"Frame {Time.frameCount}. CurrentGoalData set to Tick {_currentGoalData.DataTick}. PosX/Y {_currentGoalData.TransformProperties.Position.x}, {_currentGoalData.TransformProperties.Position.y}. Rate {_currentGoalData.MoveRates.Position}");
//				return true;
//			}
//		}

//		/// <summary>
//		/// Moves to a GoalData. Automatically determins if to use data from server or client.
//		/// </summary>
//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		private void MoveToTarget(float deltaOverride = -1f)
//		{
//			/* If the current goal data is not valid then
//			 * try to set a new one. If none are available
//			 * it will remain inactive. */
//			if (!_currentGoalData.IsValid)
//			{
//				if (!SetCurrentGoalData())
//					return;
//			}

//			float delta = (deltaOverride != -1f) ? deltaOverride : Time.deltaTime;
//			/* Once here it's safe to assume the object will be moving.
//			 * Any checks which would stop it from moving be it client
//			 * auth and owner, or server controlled and server, ect,
//			 * would have already been run. */
//			TransformPropertiesCls td = _currentGoalData.TransformProperties;
//			RateData rd = _currentGoalData.MoveRates;

//			int queueCount = _goalDatas.Count;
//			/* Begin moving even if interpolation buffer isn't
//			 * met to provide more real-time interactions but
//			 * speed up when buffer is too large. This should
//			 * provide a good balance of accuracy. */

//			float multiplier;
//			int countOverInterpolation = (queueCount - (int)_currentInterpolation - (int)_currentInterpolation);

//			if (countOverInterpolation > 0)
//			{
//				float overflowMultiplier = OVERFLOW_MULTIPLIER;
//				overflowMultiplier = 0f;
//				multiplier = 1f + overflowMultiplier; //(overflowMultiplier * countOverInterpolation);
//			}
//			else if (countOverInterpolation < 0)
//			{
//				float value = (UNDERFLOW_MULTIPLIER * Mathf.Abs(countOverInterpolation));
//				const float maximum = 0.9f;
//				if (value > maximum)
//					value = maximum;

//				multiplier = 1f - value;
//			}
//			else
//			{
//				multiplier = 1f;
//			}

//			//Rate to update. Changes per property.
//			float rate;
//			Transform t = _smoothingData.GraphicalObject;

//			//Position.
//			if (_smoothingData.SmoothPosition)
//			{
//				rate = rd.Position;
//				Vector3 posGoal = td.Position;
//				//Debug.Log($"Rate {rate}. PosY {posGoal.y}. Multiplier {multiplier}. QueueCount {queueCount}");
//				if (rate == -1f)
//					t.position = td.Position;
//				else if (rate > 0f)
//					t.position = Vector3.MoveTowards(t.position, posGoal, rate * delta * multiplier);
//			}

//			//Rotation.
//			if (_smoothingData.SmoothRotation)
//			{
//				rate = rd.Rotation;
//				if (rate == -1f)
//					t.rotation = td.Rotation;
//				else if (rate > 0f)
//					t.rotation = Quaternion.RotateTowards(t.rotation, td.Rotation, rate * delta);
//			}

//			//Subtract time remaining for movement to complete.
//			if (rd.TimeRemaining > 0f)
//			{
//				float subtractionAmount = (delta * multiplier);
//				float timeRemaining = rd.TimeRemaining - subtractionAmount;
//				rd.TimeRemaining = timeRemaining;
//			}

//			//If movement shoudl be complete.
//			if (rd.TimeRemaining <= 0f)
//			{
//				//_smoothingData.GraphicalObject.transform.position = _currentGoalData.TransformProperties.Position;
//				//_smoothingData.GraphicalObject.transform.rotation = _currentGoalData.TransformProperties.Rotation;
//				float leftOver = Mathf.Abs(rd.TimeRemaining);

//				if (SetCurrentGoalData())
//				{
//					if (leftOver > 0f)
//						MoveToTarget(leftOver);
//				}
//				//No more in buffer, see if can extrapolate.
//				else
//				{
//					/* Everything should line up when
//					 * time remaining is <= 0f but incase it's not,
//					 * such as if the user manipulated the grapihc object
//					 * somehow, then set goaldata active again to continue
//					 * moving it until it lines up with the goal. */
//					if (!GraphicalObjectMatches(td.Position, td.Rotation))
//						_currentGoalData.IsValid = true;
//				}
//			}
//		}

//		#region Rates.

//		/// <summary>
//		/// Sets move rates which will occur instantly.
//		/// </summary>
//		private void SetInstantRates(RateData rd)
//		{
//			Debug.LogError($"Instant rates set.");
//			rd.Update(MoveRates.INSTANT_VALUE, MoveRates.INSTANT_VALUE, 1, MoveRates.INSTANT_VALUE);
//		}

//		/// <summary>
//		/// Sets move rates which will occur over time.
//		/// </summary>
//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		private void SetCalculatedRates(GoalData prevGd, GoalData nextGd, bool datasCleared, Channel channel)
//		{
//			datasCleared = false;

//			/* Only update rates if data has changed.
//			 * When data comes in reliably for eventual consistency
//			 * it's possible that it will be the same as the last
//			 * unreliable packet. When this happens no change has occurred
//			 * and the distance of change would also be 0; this prevents
//			 * the object from moving. Only need to compare data if channel is reliable. */
//			if (channel == Channel.Reliable && !HasChanged(prevGd.TransformProperties, nextGd.TransformProperties))
//			{
//				Debug.LogError("Reliable and unchanged.");
//				nextGd.MoveRates.Update(prevGd.MoveRates);
//				//Set to 0 to indicate settled.
//				nextGd.DataTick = 0;

//				return;
//			}

//			uint lastTick = prevGd.DataTick;
//			/* How much time has passed between last update and current.
//			 * If set to 0 then that means the transform has
//			 * settled. */
//			if (lastTick == 0)
//				lastTick = (nextGd.DataTick - 1);

//			uint tickDifference = (nextGd.DataTick - lastTick);
//			float timePassed = (float)_networkObject.TimeManager.TicksToTime(tickDifference);
//			RateData nextRd = nextGd.MoveRates;

//			float rateMultiplier;

//			if (!datasCleared)
//			{
//				rateMultiplier = 1f;
//			}
//			else
//			{
//				float tickDelta = (float)_networkObject.TimeManager.TickDelta;
//				rateMultiplier = (_currentGoalData.MoveRates.TimeRemaining / tickDelta);
//			}

//			//Distance between properties.
//			float distance;
//			//Position.
//			Vector3 lastPosition = prevGd.TransformProperties.Position;
//			distance = Vector3.Distance(lastPosition, nextGd.TransformProperties.Position);

//			if (tickDifference == 0)
//				Debug.LogError($"0 tick difference");

//			//If distance teleports assume rest do.
//			if (_smoothingData.TeleportThreshold != MoveRates.UNSET_VALUE && distance >= _smoothingData.TeleportThreshold)
//			{
//				SetInstantRates(nextRd);

//				return;
//			}

//			//Position distance already calculated.
//			float positionRate = (distance / timePassed);
//			if (positionRate <= 0f || positionRate > 5.6f && !_networkObject.IsServer && !_networkObject.IsOwner)
//				//Debug.LogError($"Position Rate {positionRate} for tick {nextGd.LocalTick}. PrevY {prevGd.TransformProperties.Position.y}. NextY {nextGd.TransformProperties.Position.y}");
//				//Rotation.
//				distance = prevGd.TransformProperties.Rotation.Angle(nextGd.TransformProperties.Rotation, true);

//			float rotationRate = (distance / timePassed);

//			//if (positionRate > 5.1f || positionRate <= 0.05f)
//			//Debug.Log($"Rate {positionRate}. Distance {distance}. TickDifference {tickDifference}.");
//			/* If no speed then snap just in case.
//			 * 0f could be from floating errors. */
//			if (positionRate == 0f)
//				positionRate = MoveRates.INSTANT_VALUE;

//			if (rotationRate == 0f)
//				rotationRate = MoveRates.INSTANT_VALUE;

//			nextRd.Update(positionRate * rateMultiplier, rotationRate * rateMultiplier, tickDifference, timePassed);
//		}

//		#endregion

//		/// <summary>
//		/// Removes GoalDatas which make the queue excessive.
//		/// This could cause teleportation but would rarely occur, only potentially during sever network issues.
//		/// </summary>
//		private void RemoveExcessiveGoalDatas()
//		{
//			if (_goalDatas.Count > 100)
//				Debug.LogError($"Whoa getting kind of high with count of {_goalDatas.Count}");
//			///* Remove entries which are excessive to the buffer.
//			//* This could create a starting jitter but it will ensure
//			//* the buffer does not fill too much. The buffer next should
//			//* actually get unreasonably high but rather safe than sorry. */
//			//int maximumBufferAllowance = ((int)_currentInterpolation * 8);
//			//int removedBufferCount = (_goalDatas.Count - maximumBufferAllowance);
//			////If there are some to remove.
//			//if (removedBufferCount > 0)
//			//{
//			//    for (int i = 0; i < removedBufferCount; i++)
//			//        ResettableObjectCaches<GoalData>.Store(_goalDatas[0 + i]);
//			//    //_goalDatas.RemoveRange(true, removedBufferCount);
//			//    _goalDatas.RemoveRange(0, removedBufferCount);
//			//}
//		}

//		/// <summary>
//		/// Returns if a tick is older than or equal to the current GoalData and outputs current GoalData tick.
//		/// </summary>
//		private bool OldGoalDataTick(uint tick, out uint currentGoalDataTick)
//		{
//			currentGoalDataTick = _currentGoalData.DataTick;

//			return (tick <= currentGoalDataTick);
//		}

//		/// <summary>
//		/// Creates the next GoalData using previous goalData and tick.
//		/// </summary>
//		/// <param name="tick">Tick to apply for the next goal data.</param>
//		private GoalData CreateNextGoalData(uint tick, GoalData prevGoalData, bool datasCleared)
//		{
//			//Debug.Log($"Creating next GoalData for tick {tick}. PrevGoalData tick {prevGoalData.LocalTick}");
//			//Begin building next goal data.
//			GoalData nextGoalData = ResettableObjectCaches<GoalData>.Retrieve();
//			nextGoalData.DataTick = tick;
//			//Set next transform data.
//			TransformPropertiesCls nextTp = nextGoalData.TransformProperties;
//			nextTp.Update(_networkObject.transform);
//			/* Reset properties if smoothing is not enabled
//			 * for them. It's less checks and easier to do it
//			 * after the nextGoalData is populated. */
//			if (!_smoothingData.SmoothPosition)
//				nextTp.Position = _graphicalPretickValues.Position;

//			if (!_smoothingData.SmoothRotation)
//				nextTp.Rotation = _graphicalPretickValues.Rotation;

//			// Debug.Log($"Creating NextGd X {nextTp.Position.x} for tick {tick}.");
//			//Calculate rates for prev vs next data.
//			SetCalculatedRates(prevGoalData, nextGoalData, datasCleared, Channel.Unreliable);

//			return nextGoalData;
//		}

//		/// <summary>
//		/// Makes a GoalData using transform values from rootPostSimulateOffsets.
//		/// </summary>
//		/// <returns></returns>
//		private GoalData CreateGoalDataFromRootPreSimulate(uint tick)
//		{
//			GoalData gd = ResettableObjectCaches<GoalData>.Retrieve();
//			//RigidbodyData contains the data from preTick.
//			// Debug.Log($"Creating goalData from X {_rootPostSimulateValues.Position.x}. Tick {tick}");
//			gd.TransformProperties.Update(_rootPreSimulateValues);
//			gd.DataTick = tick;

//			//No need to update rates because this is just a starting point reference for interpolation.
//			return gd;
//		}

//		/// <summary>
//		/// Clears all goalDatas.
//		/// </summary>
//		private void ClearGoalData(bool clearCurrent)
//		{
//			if (clearCurrent)
//				ResettableObjectCaches<GoalData>.Store(_currentGoalData);

//			int count = _goalDatas.Count;
//			for (int i = 0; i < count; i++)
//				ResettableObjectCaches<GoalData>.Store(_goalDatas[i]);

//			_goalDatas.Clear();
//		}

//		private uint _jumpTick;

//		private uint _lastPostTickDataTick;

//		/// <summary>
//		/// Creates a GoalData after a simulate.
//		/// </summary>
//		/// <param name="postTick">True if being created for OnPostTick.</param>
//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		private void CreatePostSimulateGoalData(uint tick, bool postTick)
//		{
//			bool jumping = (_networkObject.transform.position.y > 0.5f);
//			bool dataCleared = false;
//			int dataIndex = -1;
//			bool useUpdate = false;
//			RemoveExcessiveGoalDatas();

//			if (tick <= _currentGoalData.DataTick)
//			{
//				if (!postTick)
//				{
//					if (jumping)
//						Debug.LogWarning($"Frame {Time.frameCount}. Old tick. Tick {tick}. Current {_currentGoalData.DataTick}. QueueCount {_goalDatas.Count}");

//					return;
//				}
//				else
//				{
//					dataCleared = true;
//					ClearGoalData(false);
//					Debug.LogWarning($"Frame {Time.frameCount}. Tick {tick}. Current {_currentGoalData.DataTick}. CLEARING!");
//				}
//			}

//			uint prevArrTick = 0;

//			for (int i = 0; i < _goalDatas.Count; i++)
//			{
//				uint arrTick = _goalDatas[i].DataTick;

//				if (tick == arrTick)
//				{
//					dataIndex = i;
//					useUpdate = true;

//					break;
//				}
//				else if (i > 0 && tick > prevArrTick && tick < arrTick)
//				{
//					dataIndex = i;

//					break;
//				}

//				prevArrTick = arrTick;
//			}

//			if (dataIndex == -1)
//			{
//				if (_goalDatas.Count > 0 && tick < _goalDatas[0].DataTick)
//				{
//					// Insert at the beginning.
//					dataIndex = 0;
//				}
//				else
//				{
//					// Insert at the end.
//					dataIndex = _goalDatas.Count;
//				}
//			}

//			GoalData prevGd;

//			if (dataCleared)
//			{
//				prevGd = ResettableObjectCaches<GoalData>.Retrieve();
//				prevGd.Update(_currentGoalData);
//				prevGd.DataTick = (tick - 1);
//			}
//			else
//			{
//				prevGd = CreateGoalDataFromRootPreSimulate(tick - 1);
//			}

//			GoalData nextGd = CreateNextGoalData(tick, prevGd, dataCleared);

//			if (jumping)
//			{
//				Debug.Log($"Frame {Time.frameCount}. CreateGoalData. Tick {tick}. Next Rate {nextGd.MoveRates.Position}. Next PosY {nextGd.TransformProperties.Position.y}");
//				SceneLoadData sld = new SceneLoadData(_networkObject.gameObject.scene);
//				_jumpTick = nextGd.DataTick;
//			}

//			if (useUpdate && _goalDatas[dataIndex].DataTick == _jumpTick)
//			{
//				Debug.LogError($"Frame {Time.frameCount}. Overwriting jump. Tick {tick}. IndexTick {_goalDatas[dataIndex].DataTick}. CurrentGoalY {_goalDatas[dataIndex].TransformProperties.Position.y}. Next Rate {nextGd.MoveRates.Position}. Next PosY {nextGd.TransformProperties.Position.y}.");
//			}

//			if (useUpdate)
//				_goalDatas[dataIndex].Update(nextGd);
//			else
//				_goalDatas.Insert(dataIndex, nextGd);

//			//Debug.
//			if (postTick)
//			{
//				Vector3 offset = new Vector3(0.15f, 4f, 0f);
//				Debug.DrawLine(nextGd.TransformProperties.Position + offset, nextGd.TransformProperties.Position - offset, Color.green, 2f);
//			}
//			else
//			{
//				Vector3 offset = new Vector3(-0.15f, 4f, 0f);
//				Debug.DrawLine(nextGd.TransformProperties.Position + offset, nextGd.TransformProperties.Position - offset, Color.cyan, 2f);
//			}
//		}

//		uint _postTickGdCount;

//		#endif
//	}
//}
