using UnityEngine;

namespace Nekki.Vector.GUI.Common
{
	public class SceneBackground : MonoBehaviour
	{
		[SerializeField]
		private ResolutionImage _Center;

		[SerializeField]
		private ResolutionImage _Left;

		[SerializeField]
		private ResolutionImage _Right;

		[SerializeField]
		private string _AtlasName;

		private Texture2D _OwnedCustomTexture;
		private Sprite[] _OwnedCustomSprites;
		private bool _CustomPanelsHidden;
		private bool _HasStockPanelState;
		private UnityEngine.UI.Image.Type _StockCenterType;
		private bool _StockCenterPreserveAspect;
		private Vector2 _StockCenterSize;
		private bool _StockLeftEnabled;
		private bool _StockRightEnabled;

		public string AtlasName
		{
			get
			{
				return _AtlasName;
			}
			set
			{
				_AtlasName = value;
				Refresh();
			}
		}

		public void Refresh()
		{
			Texture2D custom = ResourceManager.GetCustomTexture(_AtlasName);
			if (custom != null)
			{
				ReleaseOwnedCustomArtwork();
				CaptureStockPanelState();
				_OwnedCustomTexture = custom;
				_OwnedCustomTexture.hideFlags = HideFlags.DontUnloadUnusedAsset;
				custom.wrapMode = TextureWrapMode.Clamp;
				custom.filterMode = FilterMode.Bilinear;
				// Refresh can run in the same frame as the menu layout changes. Force
				// the RectTransforms current before measuring them or all three widths
				// briefly read as zero and the artwork gets split into fake thirds.
				Canvas.ForceUpdateCanvases();
				// Stock backgrounds are three matching atlas pieces. A creator supplies
				// one complete image, so stretch the centre across the physical width of
				// both rotated side strips instead of mixing stock corners into it.
				RectTransform centerRect = _Center.rectTransform;
				float leftWidth = Mathf.Abs(_Left.rectTransform.rect.height);
				float rightWidth = Mathf.Abs(_Right.rectTransform.rect.height);
				centerRect.sizeDelta = new Vector2(_StockCenterSize.x + leftWidth + rightWidth, _StockCenterSize.y);
				Rect source = CoverRect(custom, Mathf.Max(1f, centerRect.rect.width), Mathf.Max(1f, centerRect.rect.height));
				_OwnedCustomSprites = new[] { Slice(custom, source) };
				foreach (Sprite sprite in _OwnedCustomSprites) sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
				_Center.SetDirectSprite(_OwnedCustomSprites[0]);
				// SetDirectSprite normally contains imported portraits. Backgrounds are
				// the exception: this already-cropped image must fill the canvas.
				PreparePanel(_Center);
				_Center.enabled = true;
				_Left.enabled = false;
				_Right.enabled = false;
				_CustomPanelsHidden = true;
				return;
			}
			ReleaseOwnedCustomArtwork();
			_Center.SpriteName = _AtlasName + ".c";
			_Left.SpriteName = _AtlasName + ".l";
			_Right.SpriteName = _AtlasName + ".r";
		}

		private static void PreparePanel(ResolutionImage image)
		{
			if (image == null) return;
			image.type = UnityEngine.UI.Image.Type.Simple;
			image.preserveAspect = false;
		}

		private static Rect CoverRect(Texture2D texture, float targetWidth, float targetHeight)
		{
			float targetAspect = targetWidth / targetHeight;
			float sourceAspect = (float)texture.width / texture.height;
			if (sourceAspect > targetAspect)
			{
				float width = texture.height * targetAspect;
				return new Rect((texture.width - width) * 0.5f, 0f, width, texture.height);
			}
			float height = texture.width / targetAspect;
			return new Rect(0f, (texture.height - height) * 0.5f, texture.width, height);
		}

		private static Sprite Slice(Texture2D texture, Rect source)
		{
			int x = Mathf.Clamp(Mathf.RoundToInt(source.x), 0, texture.width - 1);
			int width = Mathf.Clamp(Mathf.RoundToInt(source.width), 1, texture.width - x);
			int y = Mathf.Clamp(Mathf.RoundToInt(source.y), 0, texture.height - 1);
			int height = Mathf.Clamp(Mathf.RoundToInt(source.height), 1, texture.height - y);
			return Sprite.Create(texture, new Rect(x, y, width, height), new Vector2(0.5f, 0.5f), 100f);
		}

		private void OnDestroy()
		{
			ReleaseOwnedCustomArtwork();
		}

		private void ReleaseOwnedCustomArtwork()
		{
			if (_HasStockPanelState)
			{
				_Center.type = _StockCenterType;
				_Center.preserveAspect = _StockCenterPreserveAspect;
				_Center.rectTransform.sizeDelta = _StockCenterSize;
				_Left.enabled = _StockLeftEnabled;
				_Right.enabled = _StockRightEnabled;
				_HasStockPanelState = false;
			}
			else if (_CustomPanelsHidden)
			{
				_Left.enabled = true;
				_Right.enabled = true;
			}
			_CustomPanelsHidden = false;
			if (_OwnedCustomSprites != null)
			{
				foreach (Sprite sprite in _OwnedCustomSprites) if (sprite != null) Destroy(sprite);
				_OwnedCustomSprites = null;
			}
			if (_OwnedCustomTexture != null)
			{
				Destroy(_OwnedCustomTexture);
				_OwnedCustomTexture = null;
			}
		}

		private void CaptureStockPanelState()
		{
			if (_HasStockPanelState || _Center == null || _Left == null || _Right == null) return;
			_StockCenterType = _Center.type;
			_StockCenterPreserveAspect = _Center.preserveAspect;
			_StockCenterSize = _Center.rectTransform.sizeDelta;
			_StockLeftEnabled = _Left.enabled;
			_StockRightEnabled = _Right.enabled;
			_HasStockPanelState = true;
		}
	}
}
