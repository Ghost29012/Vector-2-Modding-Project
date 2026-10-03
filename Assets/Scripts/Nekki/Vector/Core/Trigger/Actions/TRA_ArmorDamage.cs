using System.Xml;
using Nekki.Vector.Core.GameManagement;
using Nekki.Vector.Core.Models;
using Nekki.Vector.Core.Variables;

namespace Nekki.Vector.Core.Trigger.Actions
{
	public class TRA_ArmorDamage : TriggerRunnerAction
	{
		private Variable _model;
		private readonly int _amount;
		private readonly string _slot;

		public TRA_ArmorDamage(XmlNode node, TriggerRunnerLoop parent) : base(parent)
		{
			TriggerRunnerAction.InitActionVar(parent.ParentTrigger, ref _model, Attr(node, "Model", "Player"));
			int parsed;
			_amount = int.TryParse(Attr(node, "Amount", "1"), out parsed) ? parsed : 1;
			_slot = Attr(node, "Slot", "Torso");
		}

		private TRA_ArmorDamage(TRA_ArmorDamage source) : base(source)
		{
			_model = source._model;
			_amount = source._amount;
			_slot = source._slot;
		}

		public override void Activate(ref bool runNext)
		{
			base.Activate(ref runNext);
			runNext = true;
			CustomArmorRuntime.ApplyDamage(GetModel(_model.ValueString), _slot, _amount);
		}

		public override TriggerRunnerAction Copy() { return new TRA_ArmorDamage(this); }

		private static string Attr(XmlNode node, string name, string fallback)
		{
			XmlAttribute attribute = node.Attributes[name];
			return attribute == null || string.IsNullOrEmpty(attribute.Value) ? fallback : attribute.Value;
		}
	}
}
