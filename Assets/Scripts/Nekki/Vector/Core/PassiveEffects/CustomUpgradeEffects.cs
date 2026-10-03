using System;
using System.Collections.Generic;
using System.Globalization;
using Nekki.Vector.Core.GameManagement;
using Nekki.Vector.Core.User;
using UnityEngine;

namespace Nekki.Vector.Core.PassiveEffects
{
	/// Applies supported custom upgrade effects. Unknown effect IDs are ignored.
	public sealed class CustomUpgradeEffects
	{
		private sealed class ChargeRegeneration
		{
			public string CardName;
			public float Interval;
			public int Amount;
			public int Maximum;
			public float Elapsed;
		}

		private readonly List<ChargeRegeneration> _ChargeRegeneration = new List<ChargeRegeneration>();

		public CustomUpgradeEffects()
		{
			HashSet<string> customCards = new HashSet<string>(CustomTrickCards.ManifestCardNames, StringComparer.OrdinalIgnoreCase);
			HashSet<string> activeCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (GadgetItem gadget in DataLocalHelper.GetUserGadgets())
			foreach (CardsGroupAttribute equippedCard in gadget.Cards)
				if (equippedCard != null && customCards.Contains(equippedCard.CardName)) activeCards.Add(equippedCard.CardName);

			foreach (string cardName in activeCards)
			{
				CardsGroupAttribute card = DataLocalHelper.GetCard(cardName);
				if (card == null) continue;
				int level = Math.Max(1, card.UserCardTotalLevel);
				Apply(card.CardEffectId, cardName,
					FloatParameter(cardName, "Interval", level, 5f), IntParameter(cardName, "Amount", level, 1), IntParameter(cardName, "Maximum", level, 12));
			}

			CustomProtocolCatalog.Definition protocol;
			StarterPackItem selected = StarterPacksManager.SelectedStarterPack;
			if (selected != null && CustomProtocolCatalog.TryGet(selected.Name, out protocol))
				Apply(protocol.EffectId, "Protocol:" + protocol.Id, protocol.EffectInterval, protocol.EffectAmount, protocol.EffectMaximum);
		}

		private void Apply(string effectId, string source, float interval, int amount, int maximum)
		{
			if (string.IsNullOrEmpty(effectId) || effectId.Equals("None", StringComparison.OrdinalIgnoreCase)) return;
			if (effectId.Equals("RegenerateCharges", StringComparison.OrdinalIgnoreCase))
				_ChargeRegeneration.Add(new ChargeRegeneration { CardName = source, Interval = Math.Max(0.1f, interval), Amount = Math.Max(1, amount), Maximum = Math.Max(1, maximum) });
			else if (effectId.Equals("RestoreChargesOnFloorStart", StringComparison.OrdinalIgnoreCase)) RestoreCharges(Math.Max(1, amount), false);
			else if (effectId.Equals("FillChargesOnFloorStart", StringComparison.OrdinalIgnoreCase)) RestoreCharges(0, true);
		}

		private static void RestoreCharges(int amount, bool fill)
		{
			foreach (GadgetItem gadget in DataLocalHelper.GetUserGadgets())
			{
				int limit = Math.Max(0, gadget.TotalCharges);
				gadget.CurrentCharges = fill ? limit : Math.Min(limit, gadget.CurrentCharges + amount);
			}
		}

		public void Render()
		{
			foreach (ChargeRegeneration effect in _ChargeRegeneration)
			{
				effect.Elapsed += Time.deltaTime;
				if (effect.Elapsed < effect.Interval) continue;
				effect.Elapsed %= effect.Interval;
				foreach (GadgetItem gadget in DataLocalHelper.GetUserGadgets())
				{
					int limit = gadget.TotalCharges > 0 ? Math.Min(effect.Maximum, gadget.TotalCharges) : effect.Maximum;
					gadget.CurrentCharges = Math.Min(limit, gadget.CurrentCharges + effect.Amount);
				}
			}
		}

		private static int IntParameter(string card, string key, int level, int fallback)
		{
			int result;
			return int.TryParse(CustomTrickCards.GetLevelParameter(card, key, level), out result) ? result : fallback;
		}

		private static float FloatParameter(string card, string key, int level, float fallback)
		{
			float result;
			return float.TryParse(CustomTrickCards.GetLevelParameter(card, key, level), NumberStyles.Float, CultureInfo.InvariantCulture, out result) ? result : fallback;
		}
	}
}
