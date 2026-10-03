using Nekki.Vector.Core.GameManagement;
using Nekki.Vector.Core.Utilites;
using UIFigures;
using UnityEngine;

namespace Nekki.Vector.GUI.MainScene
{
	public class ZoneSelector : MonoBehaviour
	{
		[SerializeField]
		private UICircle _Circle;

		private Zone _Zone;
		private string _CustomZoneId;

		private static Color _ActiveColor = ColorUtils.FromHex("d2ebed");

		private static Color _InactiveColor = ColorUtils.FromHex("526778");

		public Zone Zone
		{
			get
			{
				return _Zone;
			}
		}

		public string CustomZoneId { get { return _CustomZoneId; } }
		public string ZoneId { get { return string.IsNullOrEmpty(_CustomZoneId) ? _Zone.ToString() : _CustomZoneId; } }

		public void Init(Zone p_zone)
		{
			_Zone = p_zone;
			_CustomZoneId = string.Empty;
			Unselect();
		}

		public void InitCustom(string p_zoneId)
		{
			_Zone = Zone.Default;
			_CustomZoneId = p_zoneId;
			Unselect();
		}

		public void Select()
		{
			_Circle.color = _ActiveColor;
		}

		public void Unselect()
		{
			_Circle.color = _InactiveColor;
		}
	}
}
