using System.Xml;
using Nekki.Vector.Core.Models;
using Nekki.Vector.Core.Variables;

namespace Nekki.Vector.Core.Trigger.Actions
{
	public class TRA_ExitAI : TriggerRunnerAction
	{
		private Variable _ModelNameVar;

		public TRA_ExitAI(XmlNode p_node, TriggerRunnerLoop p_parent)
			: base(p_parent)
		{
			TriggerRunnerAction.InitActionVar(p_parent.ParentTrigger, ref _ModelNameVar, XmlUtils.ParseString(p_node.Attributes["Model"]));
		}

		private TRA_ExitAI(TRA_ExitAI p_copyAction)
			: base(p_copyAction)
		{
			_ModelNameVar = p_copyAction._ModelNameVar;
		}

		public override void Activate(ref bool p_isRunNext)
		{
			base.Activate(ref p_isRunNext);
			p_isRunNext = true;
			ModelHuman model = GetModel(_ModelNameVar.ValueString);
			if (model != null && model.UserData.IsBot)
			{
				model.StopAnimation();
				model.IsEnabled = false;
			}
		}

		public override TriggerRunnerAction Copy()
		{
			return new TRA_ExitAI(this);
		}
	}
}
