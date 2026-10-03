using System;
using System.IO;
using Nekki.Vector.Core.Animation;
using Nekki.Vector.Core.GameManagement;
using Nekki.Vector.GUI;
using UnityEngine;

namespace Nekki.Vector.Core
{
	// Check for file changes every half second on Unity's main thread.
	// FileSystemWatcher callbacks would need to be marshalled back to this thread.
	public static class CustomProjectHotReload
	{
		private static readonly string[] WatchedFolders = {
			"custom_rooms", "custom_chapters", "custom_zones", "custom_quests",
			"custom_dialogue", "custom_characters", "custom_localization", "custom_models",
			"custom_textures", "custom_backgrounds", "custom_backgrounds_pool", "custom_tricks", "custom_audio",
			"custom_gamedata", "custom_economy", "custom_upgrades",
			"custom_missions", "custom_rewards", "custom_tutorials", "custom_protocols", "custom_traps", "custom_story"
		};
		private static float _NextCheck;
		private static long _Signature;
		private static bool _Ready;

		public static void Tick()
		{
			if (!Preloader.IsInited || Time.realtimeSinceStartup < _NextCheck) return;
			_NextCheck = Time.realtimeSinceStartup + 0.5f;
			long signature = BuildSignature();
			// Preloader already performs the startup load in the correct order. The
			// watcher only establishes its baseline here; reloading cards and scenes
			// during boot races Unity initialization and can crash before the menu.
			if (!_Ready) { _Signature = signature; _Ready = true; return; }
			if (signature == _Signature) return;
			_Signature = signature;
			Reload();
		}

		private static long BuildSignature()
		{
			unchecked
			{
				long value = 17;
				foreach (string folder in WatchedFolders)
				{
					string path = Path.Combine(CustomProjectCatalog.StorageRoot, folder);
					if (!Directory.Exists(path)) continue;
					try
					{
						foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
						{
							FileInfo info = new FileInfo(file);
							value = value * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(file);
							value = value * 31 + info.LastWriteTimeUtc.Ticks;
							value = value * 31 + info.Length;
						}
					}
					catch (IOException) { }
				}
				return value;
			}
		}

		private static void Reload()
		{
			try
			{
				CustomProjectCatalog.Reload();
				Models.CustomModelCatalog.Reload();
				ResourcesMap.ResetSpriteAtlasCache();
				CardsManager.Current.Reload();
				CustomTrickLoader.Reload();
				CustomTrickCards.AddToCurrentShop();
				Nekki.Vector.Core.Audio.AudioManager.ReloadCustomAudio();
				if (Manager.Scene == SceneKind.Main && Nekki.Vector.GUI.MainScene.MainScene.Current != null)
					Nekki.Vector.GUI.MainScene.MainScene.Current.RefreshCustomContentWhenSafe();
				if (Manager.Scene == SceneKind.Shop && Nekki.Vector.GUI.ShopScene.ShopScene.Current != null)
					Nekki.Vector.GUI.ShopScene.ShopScene.Current.RefreshCustomContent();
				Debug.Log("[CustomProject] Live reload complete");
			}
			catch (Exception e) { Debug.LogWarning("[CustomProject] Live reload failed: " + e.Message); }
		}
	}
}
