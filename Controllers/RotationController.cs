

using Game.Rendering.Utilities;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Controllers
{
    /// <summary>
    /// core behaviour from ObjectToolSystem.OnUpdate, there is code when ObjectToolSystem.m_State is State.Rotating
    /// </summary>
    public class VanillaLikeRotationController
    {
        private quaternion _currentQuaternion;

        private quaternion _startQuaternion;

        private float3 _pivotWorldPosM;

        private ControllerState _state;

        public ControllerState State { get => _state; }

        public quaternion Rotation { get => _currentQuaternion; }

        public VanillaLikeRotationController()
        {
            _startQuaternion = quaternion.identity;
            _currentQuaternion = quaternion.identity;
            _state = ControllerState.Stop;
        }

        public void StartRotation(float3 pivotWorldPosM)
        {
            _pivotWorldPosM = pivotWorldPosM;
            _state = ControllerState.Rotating;
        }

        public void UpdateRotation(float3 movedWorldPosM)
        {
            if (_state != ControllerState.Rotating) return;
            _currentQuaternion = ComputeRotation(_pivotWorldPosM, movedWorldPosM, _startQuaternion);
        }

        public void StopRotation()
        {
            _startQuaternion = _currentQuaternion;
            _state = ControllerState.Stop;
        }

        /// <summary>
        /// vanilla like object rotation, reference from ObjectToolSystem.OnUpdate decompiled code (1.1.7f1 version)
        /// 
        /// 
        /// </summary>
        /// <param name="source">the source rotation point</param>
        /// <param name="offseted">in pratical, related to current player's mouse hit position</param>
        /// <param name="atWall">is rotating in vertical or not</param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        private static quaternion ComputeRotation(float3 source, float3 offseted, quaternion startRotation, bool atWall = false)
        {
            // based on x offset
            var diffM = offseted.x - source.x; // postfix `M`: variable is meter unit
            var scaleFactor = 0.002f;
            var angleRad = diffM * (Mathf.PI * 2) * scaleFactor;

            quaternion rotatingQuaternion;
            if (atWall) rotatingQuaternion = quaternion.RotateZ(angleRad);
            else rotatingQuaternion = quaternion.RotateY(angleRad);

            var updatedRotation = math.mul(startRotation, rotatingQuaternion);
            return math.normalizesafe(updatedRotation, quaternion.identity);
        }


        public enum ControllerState
        {
            Stop = 1,
            Rotating = 2
        }
    }
}