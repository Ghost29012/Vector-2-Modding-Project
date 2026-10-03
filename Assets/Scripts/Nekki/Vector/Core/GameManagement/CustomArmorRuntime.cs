using Nekki.Vector.Core.Models;
using Nekki.Vector.Core.User;

namespace Nekki.Vector.Core.GameManagement
{
	// Map custom trap damage onto equipped gadget charges.
	// The existing HUD reads those charges to display lost armor segments.
	public static class CustomArmorRuntime
	{
		public static void BeginRun()
		{
			CustomProtocolCatalog.Definition protocol;
			if (!CustomProtocolCatalog.TryGetSelected(out protocol)) return;
			SetDurability("Head", protocol.HelmetDurability);
			SetDurability("Torso", protocol.TorsoDurability);
			SetDurability("Hands", protocol.HandsDurability);
			SetDurability("Legs", protocol.LegsDurability);
			SetDurability("Belt", protocol.BeltDurability);
		}

		private static void SetDurability(string slot, int durability)
		{
			GadgetItem equipped = DataLocalHelper.GetEquippedGadgetBySlot(slot);
			if (equipped == null) return;
			int requested = UnityEngine.Mathf.Max(0, durability);
			int nativeCapacity = UnityEngine.Mathf.Max(0, equipped.TotalCharges);
			equipped.CurrentCharges = UnityEngine.Mathf.Min(requested, nativeCapacity);
			equipped.BonusCharges = UnityEngine.Mathf.Max(0, requested - nativeCapacity);
		}

		public static void ApplyDamage(ModelHuman model, string slot, int amount)
		{
			if (model == null || amount <= 0 || model.IsDeath) return;
			string gameSlot = NormalizeSlot(slot);
			GadgetItem equipped = DataLocalHelper.GetEquippedGadgetBySlot(gameSlot);
			if (equipped == null)
			{
				UnityEngine.Debug.Log("[CustomArmor] No equipped " + gameSlot + " armour; hit is fatal.");
				model.OnDeath();
				return;
			}

			int remainingDamage = amount;
			int bonusSpent = UnityEngine.Mathf.Min(equipped.BonusCharges, remainingDamage);
			equipped.BonusCharges = UnityEngine.Mathf.Max(0, equipped.BonusCharges - bonusSpent);
			remainingDamage -= bonusSpent;
			if (remainingDamage > 0)
				equipped.CurrentCharges = UnityEngine.Mathf.Max(0, equipped.CurrentCharges - remainingDamage);

			int remaining = equipped.CurrentCharges + equipped.BonusCharges;
			UnityEngine.Debug.Log("[CustomArmor] " + gameSlot + " lost " + amount + " charge(s); " + remaining + " remain.");
			if (remaining == 0) model.OnDeath();
		}

		private static string NormalizeSlot(string slot)
		{
			if (string.IsNullOrEmpty(slot)) return "Torso";
			if (slot.Equals("Helmet", System.StringComparison.OrdinalIgnoreCase)) return "Head";
			return slot;
		}
	}
}
