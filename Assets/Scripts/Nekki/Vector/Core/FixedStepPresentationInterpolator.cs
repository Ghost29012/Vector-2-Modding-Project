using UnityEngine;
using Nekki.Vector.Core.Scripts.Geometry;
using ProjectionMesh = Nekki.Vector.Core.Scripts.Projection.Mesh;

namespace Nekki.Vector.Core
{
    public sealed class FixedStepPresentationInterpolator
    {
        private UnityEngine.Camera _Camera;
        private Vector3 _PreviousCameraPosition;
        private Quaternion _PreviousCameraRotation;
        private Vector3 _CurrentCameraPosition;
        private Quaternion _CurrentCameraRotation;
        private float _PreviousOrthographicSize;
        private float _CurrentOrthographicSize;
        private bool _HasCameraSnapshot;
        private bool _Presented;

        public FixedStepPresentationInterpolator(UnityEngine.Camera camera)
        {
            SetCamera(camera);
            UnityEngine.Camera.onPreCull += OnCameraPreCull;
            UnityEngine.Camera.onPostRender += OnCameraPostRender;
        }

        public void SetCamera(UnityEngine.Camera camera)
        {
            _Camera = camera;
            SnapCamera();
        }
        public void RestoreAuthoritativePose()
        {
            if (!_Presented) return;

            foreach (FixedStepInterpolatedTransform item in FixedStepInterpolatedTransform.ActiveInstances)
            {
                if (item != null) item.Restore();
            }
            foreach (Capsule capsule in Capsule.Instances)
            {
                if (capsule != null) capsule.RestoreCurrentPose();
            }
            foreach (ProjectionMesh mesh in ProjectionMesh.Instances)
            {
                if (mesh != null) mesh.RestoreCurrentPose();
            }

            if (_Camera != null && _HasCameraSnapshot)
            {
                _Camera.transform.localPosition = _CurrentCameraPosition;
                _Camera.transform.localRotation = _CurrentCameraRotation;
                if (_Camera.orthographic)
                {
                    _Camera.orthographicSize = _CurrentOrthographicSize;
                }
            }

            _Presented = false;
        }

        public void CaptureSimulationPose()
        {
            RestoreAuthoritativePose();

            foreach (FixedStepInterpolatedTransform item in FixedStepInterpolatedTransform.Instances)
            {
                if (item != null) item.Capture();
            }
            if (_Camera != null)
            {
                if (_HasCameraSnapshot)
                {
                    _PreviousCameraPosition = _CurrentCameraPosition;
                    _PreviousCameraRotation = _CurrentCameraRotation;
                    _PreviousOrthographicSize = _CurrentOrthographicSize;
                }
                else
                {
                    _PreviousCameraPosition = _Camera.transform.localPosition;
                    _PreviousCameraRotation = _Camera.transform.localRotation;
                    _PreviousOrthographicSize = _Camera.orthographicSize;
                }

                _CurrentCameraPosition = _Camera.transform.localPosition;
                _CurrentCameraRotation = _Camera.transform.localRotation;
                _CurrentOrthographicSize = _Camera.orthographicSize;
                _HasCameraSnapshot = true;
            }
        }

        private void OnCameraPreCull(UnityEngine.Camera camera)
        {
            if (camera != _Camera || !GameTiming.ShouldInterpolate || _Presented) return;

            float alpha = GameTiming.InterpolationAlpha;
            foreach (FixedStepInterpolatedTransform item in FixedStepInterpolatedTransform.ActiveInstances)
            {
                if (item != null) item.Present(alpha);
            }
            foreach (Capsule capsule in Capsule.Instances)
            {
                if (capsule != null) capsule.Present(alpha);
            }
            foreach (ProjectionMesh mesh in ProjectionMesh.Instances)
            {
                if (mesh != null) mesh.Present(alpha);
            }
            if (_HasCameraSnapshot)
            {
                _Camera.transform.localPosition =
                    Vector3.LerpUnclamped(_PreviousCameraPosition, _CurrentCameraPosition, alpha);
                _Camera.transform.localRotation =
                    Quaternion.SlerpUnclamped(_PreviousCameraRotation, _CurrentCameraRotation, alpha);
                if (_Camera.orthographic)
                {
                    _Camera.orthographicSize =
                        Mathf.LerpUnclamped(_PreviousOrthographicSize, _CurrentOrthographicSize, alpha);
                }
            }

            _Presented = true;
        }

        private void OnCameraPostRender(UnityEngine.Camera camera)
        {
            if (camera == _Camera)
            {
                RestoreAuthoritativePose();
            }
        }

        public void Clear()
        {
            RestoreAuthoritativePose();
            UnityEngine.Camera.onPreCull -= OnCameraPreCull;
            UnityEngine.Camera.onPostRender -= OnCameraPostRender;
            _HasCameraSnapshot = false;
            _Camera = null;
        }

        private void SnapCamera()
        {
            RestoreAuthoritativePose();
            if (_Camera == null) return;

            _PreviousCameraPosition = _CurrentCameraPosition = _Camera.transform.localPosition;
            _PreviousCameraRotation = _CurrentCameraRotation = _Camera.transform.localRotation;
            _PreviousOrthographicSize = _CurrentOrthographicSize = _Camera.orthographicSize;
            _HasCameraSnapshot = false;
        }
    }
}
