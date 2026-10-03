using System.Collections.Generic;
using Nekki.Vector.Core;
using Nekki.Vector.Core.Node;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nekki.Vector.Core.Scripts.Projection
{
	public class Mesh : MonoBehaviour
	{
		private Color _Color = new Color(0f, 0f, 0f, 1f);

		protected MeshNode _MeshNode;

		private string _SortingLayerName = string.Empty;

		private int _SortingOrder;

		private UnityEngine.Mesh _Mesh;

		private static Shader _Shader;

		private static Material _SharedMaterial;

		private static readonly HashSet<Mesh> _Instances = new HashSet<Mesh>();
		private Vector3[] _PreviousVertices;
		private Vector3[] _CurrentVertices;
		private Vector3[] _PresentedVertices;
		private bool _HasSnapshot;

		public static IEnumerable<Mesh> Instances
		{
			get { return _Instances; }
		}

		public Color Color
		{
			get
			{
				return _Color;
			}
			set
			{
				_Color = value;
				if (_SharedMaterial != null)
				{
					_SharedMaterial.SetVector("_Color", _Color);
				}
			}
		}

		public MeshNode Base
		{
			set
			{
				_MeshNode = value;
			}
		}

		public string SortingLayerName
		{
			get
			{
				return _SortingLayerName;
			}
			set
			{
				_SortingLayerName = value;
			}
		}

		public int SortingOrder
		{
			get
			{
				return _SortingOrder;
			}
			set
			{
				_SortingOrder = value;
			}
		}

		private void OnEnable()
		{
			_Instances.Add(this);
		}

		private void OnDisable()
		{
			_Instances.Remove(this);
		}

		private void OnDestroy()
		{
			_Instances.Remove(this);
		}

		private void Start()
		{
			if (_Shader == null)
			{
				_Shader = Shader.Find("Mesh/Colored");
				_SharedMaterial = new Material(_Shader);
			}
			_Mesh = new UnityEngine.Mesh();
			base.gameObject.AddComponent<MeshFilter>().mesh = _Mesh;
			MeshRenderer meshRenderer = base.gameObject.AddComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = _SharedMaterial;
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			meshRenderer.receiveShadows = false;
			meshRenderer.lightProbeUsage = LightProbeUsage.Off;
			meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
			meshRenderer.sortingLayerName = _SortingLayerName;
			meshRenderer.sortingOrder = _SortingOrder;
			_SharedMaterial.SetVector("_Color", _Color);
			Init();
		}

		private void Init()
		{
			EnsureBuffers();
			CaptureSimulationPose();
			_Mesh.vertices = _CurrentVertices;
			_Mesh.triangles = _MeshNode.Triangles;
		}

		private void EnsureBuffers()
		{
			if (_MeshNode == null || _MeshNode.Vertices == null) return;
			int count = _MeshNode.Vertices.Length;
			if (_CurrentVertices != null && _CurrentVertices.Length == count) return;
			_PreviousVertices = new Vector3[count];
			_CurrentVertices = new Vector3[count];
			_PresentedVertices = new Vector3[count];
			_HasSnapshot = false;
		}

		public void CaptureSimulationPose()
		{
			if (_MeshNode == null) return;
			EnsureBuffers();
			if (_CurrentVertices == null) return;

			Vector3[] next = _PresentedVertices;
			_MeshNode.CopyCurrentTo(next);

			if (!_HasSnapshot)
			{
				for (int i = 0; i < next.Length; i++)
				{
					_PreviousVertices[i] = next[i];
					_CurrentVertices[i] = next[i];
				}
				_HasSnapshot = true;
			}
			else
			{
				for (int i = 0; i < next.Length; i++)
				{
					_PreviousVertices[i] = _CurrentVertices[i];
					_CurrentVertices[i] = next[i];
					if ((_CurrentVertices[i] - _PreviousVertices[i]).sqrMagnitude < 0.25f)
					{
						_PreviousVertices[i] = _CurrentVertices[i];
					}
				}
			}

			RestoreCurrentPose();
		}

		public void Present(float alpha)
		{
			if (!_HasSnapshot || _Mesh == null) return;
			for (int i = 0; i < _PresentedVertices.Length; i++)
			{
				_PresentedVertices[i] = Vector3.LerpUnclamped(_PreviousVertices[i], _CurrentVertices[i], alpha);
			}
			_Mesh.vertices = _PresentedVertices;
			_Mesh.RecalculateBounds();
		}

		public void RestoreCurrentPose()
		{
			if (!_HasSnapshot || _Mesh == null) return;
			_Mesh.vertices = _CurrentVertices;
			_Mesh.RecalculateBounds();
		}
	}
}
