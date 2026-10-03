using System.Collections.Generic;
using Nekki.Vector.Core;
using Nekki.Vector.Core.Node;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nekki.Vector.Core.Scripts.Geometry
{
	public class Capsule : MonoBehaviour
	{
		private float _Stroke = 1f;

		protected ModelLine _Base;

		private string _SortingLayerName = string.Empty;

		private int _SortingOrder;

		private static Material _SharedQuadMaterial;

		private static Material _SharedCircleMaterial;

		private Transform _MiddleRect;

		private Transform _BeginCircle;

		private Transform _EndCircle;

		private static readonly HashSet<Capsule> _Instances = new HashSet<Capsule>();

		private Vector2 _PreviousA;
		private Vector2 _PreviousB;
		private Vector2 _CurrentA;
		private Vector2 _CurrentB;
		private bool _HasSnapshot;

		public static IEnumerable<Capsule> Instances
		{
			get { return _Instances; }
		}

		public float Stroke
		{
			get
			{
				return _Stroke;
			}
			set
			{
				_Stroke = value;
			}
		}

		public ModelLine Base
		{
			get
			{
				return _Base;
			}
			set
			{
				_Base = value;
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

		private static Material SharedQuadMaterial
		{
			get
			{
				if (_SharedQuadMaterial == null)
				{
					_SharedQuadMaterial = new Material(Shader.Find("Sprites/Colored"));
				}
				return _SharedQuadMaterial;
			}
		}

		private static Material SharedCircleMaterial
		{
			get
			{
				if (_SharedCircleMaterial == null)
				{
					_SharedCircleMaterial = new Material(Shader.Find("Capsule/Circle"));
				}
				return _SharedCircleMaterial;
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
			_Stroke = _Base.Stroke;
			GameObject gameObject = new GameObject("CircleBegin");
			InitGameObject(gameObject, SharedCircleMaterial);
			_BeginCircle = gameObject.transform;
			_BeginCircle.SetParent(base.transform, false);
			gameObject = new GameObject("CircleEnd");
			InitGameObject(gameObject, SharedCircleMaterial);
			_EndCircle = gameObject.transform;
			_EndCircle.SetParent(base.transform, false);
			gameObject = new GameObject("Rect");
			InitGameObject(gameObject, SharedQuadMaterial);
			_MiddleRect = gameObject.transform;
			_MiddleRect.SetParent(base.transform, false);
			float num = _Stroke * 2f;
			_BeginCircle.localScale = new Vector3(num, num, 1f);
			_EndCircle.localScale = new Vector3(num, num, 1f);
			Render();
		}

		private void InitGameObject(GameObject p_object, Material p_material)
		{
			MeshFilter meshFilter = p_object.AddComponent<MeshFilter>();
			MeshRenderer meshRenderer = p_object.AddComponent<MeshRenderer>();
			Mesh mesh = new Mesh();
			mesh.vertices = new Vector3[4]
			{
				new Vector3(-0.5f, -0.5f, 0f),
				new Vector3(0.5f, -0.5f, 0f),
				new Vector3(-0.5f, 0.5f, 0f),
				new Vector3(0.5f, 0.5f, 0f)
			};
			mesh.uv = new Vector2[4]
			{
				new Vector2(0f, 0f),
				new Vector2(1f, 0f),
				new Vector2(0f, 1f),
				new Vector2(1f, 1f)
			};
			mesh.triangles = new int[6] { 0, 1, 2, 1, 3, 2 };
			mesh.RecalculateBounds();
			meshFilter.mesh = mesh;
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			meshRenderer.receiveShadows = false;
			meshRenderer.lightProbeUsage = LightProbeUsage.Off;
			meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
			meshRenderer.sharedMaterial = p_material;
			meshRenderer.sortingLayerName = _SortingLayerName;
			meshRenderer.sortingOrder = _SortingOrder;
		}

		public void Render()
		{
			CaptureSimulationPose();
			RestoreCurrentPose();
		}

		public void CaptureSimulationPose()
		{
			if (_Base == null || _Base.Start == null || _Base.End == null)
			{
				return;
			}

			Vector2 nextA = new Vector2(_Base.Start.Start.X, _Base.Start.Start.Y);
			Vector2 nextB = new Vector2(_Base.End.Start.X, _Base.End.Start.Y);

			if (!_HasSnapshot)
			{
				_PreviousA = _CurrentA = nextA;
				_PreviousB = _CurrentB = nextB;
				_HasSnapshot = true;
				return;
			}

			_PreviousA = _CurrentA;
			_PreviousB = _CurrentB;
			_CurrentA = nextA;
			_CurrentB = nextB;

			if ((_CurrentA - _PreviousA).sqrMagnitude < 0.25f)
			{
				_PreviousA = _CurrentA;
			}
			if ((_CurrentB - _PreviousB).sqrMagnitude < 0.25f)
			{
				_PreviousB = _CurrentB;
			}
		}

		public void Present(float alpha)
		{
			if (!_HasSnapshot) return;
			RenderPose(Vector2.LerpUnclamped(_PreviousA, _CurrentA, alpha),
				Vector2.LerpUnclamped(_PreviousB, _CurrentB, alpha));
		}

		public void RestoreCurrentPose()
		{
			if (!_HasSnapshot) return;
			RenderPose(_CurrentA, _CurrentB);
		}

		private void RenderPose(Vector2 a, Vector2 b)
		{
			if (_BeginCircle == null || _EndCircle == null || _MiddleRect == null)
			{
				return;
			}

			float num = a.x - b.x;
			float num2 = a.y - b.y;
			float num3 = a.x - num * _Base.Margin1;
			float num4 = a.y - num2 * _Base.Margin1;
			float num5 = b.x + num * _Base.Margin2;
			float num6 = b.y + num2 * _Base.Margin2;
			_BeginCircle.localPosition = new Vector2(num3, num4);
			_EndCircle.localPosition = new Vector2(num5, num6);
			num = num3 - num5;
			num2 = num4 - num6;
			float y = Mathf.Sqrt(num * num + num2 * num2);
			_MiddleRect.transform.up = new Vector3(num, num2, 0f);
			_MiddleRect.localPosition = new Vector3((num3 + num5) / 2f, (num4 + num6) / 2f);
			if (_Stroke != _Base.Stroke)
			{
				_Stroke = _Base.Stroke;
				float diameter = _Stroke * 2f;
				_BeginCircle.localScale = new Vector3(diameter, diameter, 1f);
				_EndCircle.localScale = new Vector3(diameter, diameter, 1f);
			}
			_MiddleRect.localScale = new Vector3(_Stroke * 2f, y, 1f);
		}
	}
}
