using System.Collections.Generic;
using Nekki.Vector.Core;
using Nekki.Vector.Core.GameManagement;
using Nekki.Vector.GUI.InputControllers;
using UnityEngine;

namespace Nekki.Vector.GUI.MainScene
{
	public class SelectZone : MonoBehaviour
	{
		[SerializeField]
		private GameObject _SelectorPrefab;

		private List<ZoneSelector> _Selectors = new List<ZoneSelector>();

		private ZoneSelector _CurrentSelector;

		public void Refresh()
		{
			ClearSelectors();
			CreateSelectors();
			string currentZone = ZoneManager.CurrentZoneId;
			ZoneSelector selected = _Selectors.Find((ZoneSelector p_selector) => p_selector.ZoneId == currentZone);
			if (selected == null && _Selectors.Count > 0) selected = _Selectors[0];
			SetCurrentSelector(selected, false);
		}

		public void OnSlide(int p_index, Vector2 p_from, Vector2 p_to)
		{
			switch (TouchController.GetDirection(p_from, p_to))
			{
			case Direction.Left:
				SelectPrev();
				break;
			case Direction.Right:
				SelectNext();
				break;
			}
		}

		private void ClearSelectors()
		{
			if (_Selectors.Count > 0)
			{
				foreach (ZoneSelector selector in _Selectors)
				{
					Object.DestroyImmediate(selector.gameObject);
				}
				_Selectors.Clear();
			}
			_CurrentSelector = null;
		}

		private void CreateSelectors()
		{
			HashSet<Zone> availableZones = ZoneManager.AvailableZones;
			foreach (Zone item in availableZones)
			{
				CreateZoneSelector(item);
			}
			List<CustomProjectCatalog.ZoneDefinition> customZones = new List<CustomProjectCatalog.ZoneDefinition>(CustomProjectCatalog.Zones);
			customZones.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase));
			foreach (CustomProjectCatalog.ZoneDefinition zone in customZones) CreateCustomZoneSelector(zone.Id);
			base.gameObject.SetActive(_Selectors.Count > 1);
		}

		private void CreateCustomZoneSelector(string p_zoneId)
		{
			GameObject gameObject = Object.Instantiate(_SelectorPrefab);
			gameObject.transform.SetParent(base.transform, false);
			gameObject.transform.SetAsLastSibling();
			gameObject.name = "Selector_" + p_zoneId;
			ZoneSelector component = gameObject.GetComponent<ZoneSelector>();
			component.InitCustom(p_zoneId);
			_Selectors.Add(component);
		}

		private void CreateZoneSelector(Zone p_zone)
		{
			GameObject gameObject = Object.Instantiate(_SelectorPrefab);
			gameObject.transform.SetParent(base.transform, false);
			gameObject.transform.SetAsLastSibling();
			gameObject.name = string.Format("Selector_{0}", p_zone);
			ZoneSelector component = gameObject.GetComponent<ZoneSelector>();
			component.Init(p_zone);
			_Selectors.Add(component);
		}

		public void SelectNext()
		{
			if (_Selectors.Count == 0) return;
			int num = _Selectors.IndexOf(_CurrentSelector) + 1;
			if (num >= _Selectors.Count)
			{
				num = 0;
			}
			SetCurrentSelector(_Selectors[num]);
		}

		public void SelectPrev()
		{
			if (_Selectors.Count == 0) return;
			int num = _Selectors.IndexOf(_CurrentSelector) - 1;
			if (num < 0)
			{
				num = _Selectors.Count - 1;
			}
			SetCurrentSelector(_Selectors[num]);
		}

		public void SetCurrentSelector(ZoneSelector p_selector, bool p_manualSelect = true)
		{
			if (p_selector == null) return;
			if (!(_CurrentSelector == p_selector))
			{
				if (_CurrentSelector != null)
				{
					_CurrentSelector.Unselect();
				}
				_CurrentSelector = p_selector;
				_CurrentSelector.Select();
				if (p_manualSelect)
				{
					if (string.IsNullOrEmpty(_CurrentSelector.CustomZoneId)) Scene<MainScene>.Current.SwitchZone(_CurrentSelector.Zone);
					else Scene<MainScene>.Current.SwitchCustomZone(_CurrentSelector.CustomZoneId);
				}
			}
		}

		public void OnTap()
		{
			SelectNext();
		}
	}
}
