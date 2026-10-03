using System.Collections.Generic;
using System.Xml;
using Nekki.Vector.Core.Counter;
using Nekki.Vector.Core.Statistics;
using Nekki.Vector.Core.Variables;
using Nekki.Vector.GUI;
using Nekki.Vector.GUI.Common;
using Nekki.Vector.GUI.Dialogs;

namespace Nekki.Vector.Core.Trigger.Actions
{
	public class TQA_Dialog : TriggerQuestAction
	{
		private Variable _Title;

		private Variable _Text;

		private Variable _Button;

		private Variable _Image;

		private Variable _RetainImage;

		private Variable _Order;

		private Variable _RunEndButton;

		private string _DialogueId;

		public TQA_Dialog(XmlNode p_node, TriggerQuestLoop p_parent)
			: base(p_parent)
		{
			_Title = Variable.CreateVariable(XmlUtils.ParseString(p_node.Attributes["Title"], string.Empty), string.Empty);
			_Text = Variable.CreateVariable(XmlUtils.ParseString(p_node.Attributes["Text"], string.Empty), string.Empty);
			_Button = Variable.CreateVariable(XmlUtils.ParseString(p_node.Attributes["Button"], string.Empty), string.Empty);
			_Image = Variable.CreateVariable(XmlUtils.ParseString(p_node.Attributes["Image"], string.Empty), string.Empty);
			_RetainImage = Variable.CreateVariable(XmlUtils.ParseString(p_node.Attributes["RetainImage"], "0"), string.Empty);
			_Order = Variable.CreateVariable(XmlUtils.ParseString(p_node.Attributes["Order"], "0"), string.Empty);
			_DialogueId = XmlUtils.ParseString(p_node.Attributes["DialogueId"], string.Empty);
			if (p_node.Attributes["RunEndButton"] != null)
			{
				_RunEndButton = Variable.CreateVariable(XmlUtils.ParseString(p_node.Attributes["RunEndButton"], string.Empty), string.Empty);
			}
		}

		public override void Activate(ref bool p_runNext)
		{
			p_runNext = false;
			string title = _Title.ValueString;
			string text = _Text.ValueString;
			string button = _Button.ValueString;
			string image = _Image.ValueString;
			CustomProjectCatalog.Dialogue dialogue;
			if (!string.IsNullOrEmpty(_DialogueId) && CustomProjectCatalog.TryGetDialogue(_DialogueId, out dialogue))
			{
				title = dialogue.Title;
				text = dialogue.Text;
				if (!string.IsNullOrEmpty(dialogue.Button)) button = dialogue.Button;
				if (!string.IsNullOrEmpty(dialogue.Image)) image = dialogue.Image;
				CustomProjectCatalog.Speaker speaker;
				if (CustomProjectCatalog.TryGetSpeaker(dialogue.SpeakerId, out speaker))
				{
					if (string.IsNullOrEmpty(title)) title = speaker.Name;
					if (string.IsNullOrEmpty(image)) image = speaker.Portrait;
				}
			}
			if (_RunEndButton != null)
			{
				List<DialogButtonData> list = new List<DialogButtonData>();
				list.Add(new DialogButtonData(delegate(BaseDialog p_dialog)
				{
					OkButtonCallback();
					p_dialog.Dismiss();
				}, button, ButtonUI.Type.Blue));
				list.Add(new DialogButtonData(delegate(BaseDialog p_dialog)
				{
					p_dialog.Dismiss();
					OnRunEndButtonCallback();
				}, _RunEndButton.ValueString, ButtonUI.Type.Green));
				DialogNotificationManager.ShowQuestTalkingDialog(title, text, image, list, _Order.ValueInt);
			}
			else
			{
				DialogNotificationManager.ShowQuestTalkingDialog(title, text, button, image, OkButtonCallback, _Order.ValueInt);
			}
		}

		private void OkButtonCallback()
		{
			_Parent.ActivateActions();
		}

		private void OnRunEndButtonCallback()
		{
			StatisticsCollector.SetEvent(StatisticsEvent.EventType.Run_end);
			RunMainController.RunEnd();
			CounterController.Current.CounterFloor = 0;
			Manager.Load(SceneKind.Terminal);
		}
	}
}
