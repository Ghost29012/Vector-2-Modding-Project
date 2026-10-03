using Nekki.Vector.Core;
using Nekki.Vector.Core.User;
using UnityEngine;
using UnityEngine.UI;

namespace Nekki.Vector.GUI
{
	[AddComponentMenu("UI_Nekki/ResolutionImage")]
	public class ResolutionImage : Image
	{
		private const string _DefaultTexturesPath = "UI/Textures/";

		private const string _DefaultAtlasPath = "UI/Atlases/";

		public const string LowQualitySuffix = "_low";

		[SerializeField]
		private string _TexturePath;

		[SerializeField]
		private string _SpriteName;

		private Vector2 _PrefabSizeDelta;

		private bool _HasPrefabSize;

		private bool _PrefabPreserveAspect;

		public bool IsCustomSprite { get; private set; }

		public string TexturePath
		{
			get
			{
				return _TexturePath;
			}
			set
			{
				_TexturePath = value;
			}
		}

		public string SpriteName
		{
			get
			{
				return _SpriteName;
			}
			set
			{
				_SpriteName = value;
				SetSprite();
			}
		}

		public float Alpha
		{
			get
			{
				return color.a;
			}
			set
			{
				Color color = this.color;
				color.a = value;
				this.color = color;
			}
		}

		public void SetDirectSprite(Sprite p_sprite)
		{
			_SpriteName = string.Empty;
			IsCustomSprite = true;
			base.sprite = p_sprite;
			preserveAspect = true;
		}

		protected override void Awake()
		{
			if (!Application.isPlaying)
			{
				return;
			}
			base.Awake();
			RectTransform rectTransform = GetComponent<RectTransform>();
			if (rectTransform != null)
			{
				_PrefabSizeDelta = rectTransform.sizeDelta;
				_HasPrefabSize = true;
			}
			_PrefabPreserveAspect = preserveAspect;
			SetSprite();
		}

		private void SetSprite()
		{
			if (DataLocal.IsCurrentExists)
			{
				string[] array = _SpriteName.Split('.');
				if (array.Length == 1)
				{
					SetSingleSprite();
				}
				else
				{
					SetAtlasSprite(array[0]);
				}
			}
		}

		private void SetSingleSprite()
		{
			IsCustomSprite = false;
			preserveAspect = _PrefabPreserveAspect;
			Sprite sprite = ResourcesAndBundles.Load<Sprite>(_TexturePath + GetSingleSpriteName());
			if (sprite == null)
			{
				sprite = ResourcesAndBundles.Load<Sprite>("UI/Textures/" + GetSingleSpriteName());
			}
			// Try stock sprites first, then resolve custom card image filenames.
			if (sprite == null)
			{
				sprite = ResourcesMap.GetSprite(_SpriteName);
				IsCustomSprite = sprite != null;
			}
			base.sprite = sprite;
			if (IsCustomSprite) preserveAspect = true;
		}

		private void SetAtlasSprite(string p_atlasName)
		{
			IsCustomSprite = false;
			preserveAspect = _PrefabPreserveAspect;
			Sprite spriteFromAtlas = AtlasCache.GetSpriteFromAtlas(_TexturePath + GetAtlasName(p_atlasName), _SpriteName);
			if (spriteFromAtlas == null)
			{
				spriteFromAtlas = AtlasCache.GetSpriteFromAtlas("UI/Atlases/" + GetAtlasName(p_atlasName), _SpriteName);
			}
			// A dot in a filename can be mistaken for atlas notation.
			// If the atlas lookup fails, try the custom image file.
			if (spriteFromAtlas == null)
			{
				spriteFromAtlas = ResourcesMap.GetSprite(_SpriteName);
				IsCustomSprite = spriteFromAtlas != null;
			}
			base.sprite = spriteFromAtlas;
			if (IsCustomSprite) preserveAspect = true;
		}

		private string GetSingleSpriteName()
		{
			if (DataLocal.Current.Settings.UseLowResGraphics)
			{
				return _SpriteName + "_low";
			}
			return _SpriteName;
		}

		private string GetAtlasName(string p_name)
		{
			if (DataLocal.Current.Settings.UseLowResGraphics)
			{
				return p_name + "_low";
			}
			return p_name;
		}

		public override void SetNativeSize()
		{
			// Keep custom images inside the prefab's original box, regardless of resolution.
			if (IsCustomSprite)
			{
				RectTransform customRect = GetComponent<RectTransform>();
				if (_HasPrefabSize && customRect != null) customRect.sizeDelta = _PrefabSizeDelta;
				preserveAspect = true;
				return;
			}
			base.SetNativeSize();
			if (DataLocal.Current.Settings.UseLowResGraphics)
			{
				RectTransform component = GetComponent<RectTransform>();
				component.sizeDelta = new Vector2(component.sizeDelta.x * 2f, component.sizeDelta.y * 2f);
			}
		}

		protected override void OnDestroy()
		{
			base.sprite = null;
			base.OnDestroy();
		}
	}
}
