using System.Collections.Generic;
using UnityEngine;

namespace Nekki.Vector.Core
{
    public sealed class FixedStepInterpolatedTransform : MonoBehaviour
    {
        private static readonly HashSet<FixedStepInterpolatedTransform> _Instances = new HashSet<FixedStepInterpolatedTransform>();
        private static readonly HashSet<FixedStepInterpolatedTransform> _Active = new HashSet<FixedStepInterpolatedTransform>();

        public static IEnumerable<FixedStepInterpolatedTransform> Instances { get { return _Instances; } }
        public static IEnumerable<FixedStepInterpolatedTransform> ActiveInstances { get { return _Active; } }

        private Vector3 _PreviousPosition;
        private Quaternion _PreviousRotation;
        private Vector3 _PreviousScale;
        private Vector3 _CurrentPosition;
        private Quaternion _CurrentRotation;
        private Vector3 _CurrentScale;
        private bool _HasSnapshot;
        private float _LastPositionDelta;

        // Stock Vector 2 uses deliberate one-tick position wraps for seamless
        // looping scenery (for example Shaft_Complex moves its chain 120 px
        // back in one tick after a long smooth scroll). Never interpolate
        // through those teleports: doing so exposes the hidden wrap at >60 Hz.
        // A data scan shows normal multi-frame authored motion peaks below
        // ~45 px/tick, leaving 64 px as a safe absolute discontinuity boundary.
        private const float MaxInterpolatedPositionDelta = 64f;
        private const float RelativeDiscontinuityMinDelta = 24f;
        private const float RelativeDiscontinuityMultiplier = 4f;
        private const float MaxInterpolatedRotationDelta = 100f;

        private void OnEnable()
        {
            _Instances.Add(this);
            SnapToCurrent();
        }
        private void OnDisable()
        {
            _Instances.Remove(this);
            _Active.Remove(this);
        }

        private void OnDestroy()
        {
            _Instances.Remove(this);
            _Active.Remove(this);
        }

        public void Capture()
        {
            Vector3 position = transform.localPosition;
            Quaternion rotation = transform.localRotation;
            Vector3 scale = transform.localScale;

            if (!_HasSnapshot)
            {
                _PreviousPosition = _CurrentPosition = position;
                _PreviousRotation = _CurrentRotation = rotation;
                _PreviousScale = _CurrentScale = scale;
                _HasSnapshot = true;
                _Active.Remove(this);
                return;
            }
            _PreviousPosition = _CurrentPosition;
            _PreviousRotation = _CurrentRotation;
            _PreviousScale = _CurrentScale;

            _CurrentPosition = position;
            _CurrentRotation = rotation;
            _CurrentScale = scale;

            float positionDelta = (_CurrentPosition - _PreviousPosition).magnitude;
            float rotationDelta = Quaternion.Angle(_PreviousRotation, _CurrentRotation);

            bool positionDiscontinuity =
                positionDelta > MaxInterpolatedPositionDelta
                || (_LastPositionDelta > 0.0001f
                    && positionDelta > RelativeDiscontinuityMinDelta
                    && positionDelta > _LastPositionDelta * RelativeDiscontinuityMultiplier);

            if (positionDiscontinuity || rotationDelta > MaxInterpolatedRotationDelta)
            {
                _PreviousPosition = _CurrentPosition;
                _PreviousRotation = _CurrentRotation;
                _PreviousScale = _CurrentScale;
                _LastPositionDelta = 0f;
                _Active.Remove(this);
                return;
            }

            _LastPositionDelta = positionDelta;

            // Vector 2 frequently uses scale as a discrete visual/effect state
            // (laser flashes, halos, trap rays, etc.). Interpolating those authored
            // scale jumps exposes intermediate stretched lines that never existed at 60 Hz.
            bool moved = positionDelta > 0.0001f || rotationDelta > 0.001f;

            if (moved)
            {
                _Active.Add(this);
            }
            else
            {
                _Active.Remove(this);
            }
        }

        public void Restore()
        {
            if (!_HasSnapshot) return;
            transform.localPosition = _CurrentPosition;
            transform.localRotation = _CurrentRotation;
            transform.localScale = _CurrentScale;
        }

        public void Present(float alpha)
        {
            if (!_HasSnapshot) return;
            transform.localPosition = Vector3.LerpUnclamped(_PreviousPosition, _CurrentPosition, alpha);
            transform.localRotation = Quaternion.SlerpUnclamped(_PreviousRotation, _CurrentRotation, alpha);
            transform.localScale = _CurrentScale;
        }

        public void SnapToCurrent()
        {
            _PreviousPosition = _CurrentPosition = transform.localPosition;
            _PreviousRotation = _CurrentRotation = transform.localRotation;
            _PreviousScale = _CurrentScale = transform.localScale;
            _HasSnapshot = false;
            _LastPositionDelta = 0f;
            _Active.Remove(this);
        }

        public void PrimeCurrent()
        {
            _PreviousPosition = _CurrentPosition = transform.localPosition;
            _PreviousRotation = _CurrentRotation = transform.localRotation;
            _PreviousScale = _CurrentScale = transform.localScale;
            _HasSnapshot = true;
            _LastPositionDelta = 0f;
            _Active.Remove(this);
        }
    }
}
