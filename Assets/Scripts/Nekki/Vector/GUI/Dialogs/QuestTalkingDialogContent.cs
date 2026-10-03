using System;
using System.Collections.Generic;
using Nekki.Vector.Core.Localization;
using Nekki.Vector.GUI.Common;
using UnityEngine;

namespace Nekki.Vector.GUI.Dialogs
{
	public class QuestTalkingDialogContent : DialogContent
	{
		[SerializeField]
		private LabelAlias _Title;

		[SerializeField]
		private LabelAlias _TextLabel;

		[SerializeField]
		private ResolutionImage _Image;

		private Action _OnClose;

		private void SetPortrait(string p_image)
		{
			if (!string.IsNullOrEmpty(p_image))
			{
				_Image.SpriteName = p_image;
				// Stock portraits include transparent full-screen padding. Imported
				// photos do not, so they must be fitted rather than stretched.
				if (_Image.IsCustomSprite)
				{
					_Image.type = UnityEngine.UI.Image.Type.Simple;
					_Image.preserveAspect = true;
				}
				_Image.enabled = true;
			}
			else
			{
				_Image.enabled = false;
			}
		}

		public void Init(string p_title, string p_text, string p_buttonText, string p_image, Action p_onClose)
		{
			_OnClose = p_onClose;
			List<DialogButtonData> list = new List<DialogButtonData>();
			list.Add(new DialogButtonData(OnCloseTap, p_buttonText, ButtonUI.Type.Blue));
			Init(list);
			SetPortrait(p_image);
			_Title.SetAlias(p_title);
			_TextLabel.SetAlias(p_text);
		}

		public void Init(string p_title, string p_text, string p_image, List<DialogButtonData> p_buttons)
		{
			Init(p_buttons);
			SetPortrait(p_image);
			_Title.SetAlias(p_title);
			_TextLabel.SetAlias(p_text);
		}

		private void OnCloseTap(BaseDialog p_dialog)
		{
			if (_OnClose != null)
			{
				_OnClose();
			}
			base.Parent.Dismiss();
		}
	}
}
