using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Nekki.Vector.Core.Counter;
using Nekki.Vector.Core.Game;
using Nekki.Vector.Core.GameManagement;
using Nekki.Vector.Core.Generator.Test;
using Nekki.Vector.Core.User;
using UnityEngine;

namespace Nekki.Vector.Core.Generator
{
	public class LocationGenerator
	{
		public static int MaxAttemptCount = 5;

		private static int _GeneratedRoomId;

		private List<RoomData> _Rooms = new List<RoomData>();

		private List<RoomData> _CustomRooms = new List<RoomData>();

		private List<RoomData> _CustomStartRooms = new List<RoomData>();

		private List<RoomData> _CustomFinishRooms = new List<RoomData>();

		private List<RoomData> _SelectedRooms = new List<RoomData>();

		private List<string> _ObjectsFiles = new List<string>();

		public List<string> ObjectsFile
		{
			get
			{
				return _ObjectsFiles;
			}
		}

		public LocationGenerator(string p_file, NekkiRandom p_generator)
		{
			_GeneratedRoomId = 0;
			Parse(p_file);
		}

		public static string GetGeneratedRoomId()
		{
			string result = _GeneratedRoomId.ToString();
			_GeneratedRoomId++;
			return result;
		}

		private void Parse(string p_file)
		{
			XmlDocument xmlDocument = XmlUtils.OpenXMLDocument(p_file, string.Empty);
			foreach (XmlNode childNode in xmlDocument["Root"].ChildNodes)
			{
				if (!(childNode.Name != "Room"))
				{
					RoomData item = new RoomData(childNode);
					_Rooms.Add(item);
				}
			}
			ParseIncludes(xmlDocument["Root"]["Includes"]);
			LoadCustomRooms();
		}

		private void LoadCustomRooms()
		{
			// Use VectorPaths.CurrentStorage so the path matches where Vector stores all its data at runtime
			string storageRoot = !string.IsNullOrEmpty(VectorPaths.CurrentStorage)
				? VectorPaths.CurrentStorage
				: Application.persistentDataPath;
			string customRoot = Path.Combine(storageRoot, "custom_rooms");
			bool isPlayCommand = GeneratorHelper.IsPlayCommand;
			if (GameManagement.ZoneManager.IsCustomZoneActive)
			{
				CustomProjectCatalog.ZoneDefinition zone;
				if (CustomProjectCatalog.TryGetZone(GameManagement.ZoneManager.CurrentZoneId, out zone))
				{
					customRoot = CustomProjectCatalog.ResolveProjectFolder(zone.RoomsPath, "custom_rooms");
					Debug.Log("[CustomRooms] Active zone=" + zone.Id + " pool=" + customRoot);
					// A custom zone owns this folder as its complete room namespace.
					// Structural and normal room selection must stay inside this root.
				}
			}
			string customTexturesRoot = Path.Combine(storageRoot, "custom_textures");

			if (!Directory.Exists(customTexturesRoot))
			{
				Directory.CreateDirectory(customTexturesRoot);
			}

			if (!Directory.Exists(customRoot))
			{
				Directory.CreateDirectory(customRoot);
				Directory.CreateDirectory(Path.Combine(customRoot, "zone_2"));
				return;
			}

			// If a play command is active, only load the rooms that were explicitly requested.
			// This prevents old rooms in custom_rooms from sneaking into the pool when a
			// specific room is requested via the console/play command.
			List<string> files = new List<string>(Directory.GetFiles(customRoot, "*.xml", SearchOption.AllDirectories));
			Debug.Log("[CustomRooms] Candidate files=" + files.Count + " playCommand=" + isPlayCommand);
			foreach (string roomXmlPath in files)
			{
				if (roomXmlPath.EndsWith(".meta.xml", StringComparison.OrdinalIgnoreCase))
					continue;

				string roomName = Path.GetFileNameWithoutExtension(roomXmlPath);
				// In play command mode, skip rooms that were not explicitly requested
				if (isPlayCommand && !GeneratorHelper.ToLoadRooms.Contains(roomName))
					continue;

				// Native Lab and Maintenance rooms use different generators and balance
				// trees. Mixing them looks harmless until a room asks the wrong zone's
				// YAML for a value, then room generation falls over with a null reference.
				// An explicit editor/console play command is allowed through so a creator
				// can still test one room regardless of the menu's currently selected zone.
				if (!isPlayCommand && !IsRoomCompatibleWithCurrentZone(roomXmlPath))
				{
					Debug.Log("[CustomRooms] Skipped room from another zone: " + roomName);
					continue;
				}

				try
				{
					XmlDocument helperDoc = new XmlDocument();
					XmlElement roomNode = helperDoc.CreateElement("Room");
					roomNode.SetAttribute("Name", roomName);
					roomNode.SetAttribute("File", roomXmlPath);
					roomNode.SetAttribute("IncludeInPlayCommand", "0");

					// Generator labels are optional legacy metadata. Layout choices live
					// in the room XML itself, just like Vector's stock rooms.
					string sidecarPath = Path.ChangeExtension(roomXmlPath, ".meta.xml");
					bool importedLabels = false;
					if (File.Exists(sidecarPath))
					{
						try
						{
							XmlDocument sidecarDoc = new XmlDocument();
							sidecarDoc.Load(sidecarPath);
							XmlNode labelsNode = sidecarDoc["RoomMeta"]?["GeneratorLabels"];
							if (labelsNode != null)
							{
								XmlNode imported = helperDoc.ImportNode(labelsNode, true);
								roomNode.AppendChild(imported);
								importedLabels = true;
							}
						}
						catch
						{
							// A malformed sidecar must not stop an otherwise valid custom room.
						}
					}
					if (!importedLabels)
					{
						roomNode.AppendChild(helperDoc.CreateElement("GeneratorLabels"));
					}
					AppendEmbeddedRoomSelection(helperDoc, roomNode, roomXmlPath);

					helperDoc.AppendChild(roomNode);
					RoomData customRoom = new RoomData(roomNode);
					if (isPlayCommand)
					{
						_Rooms.Add(customRoom);
					}
					else if (IsStructuralPool(roomXmlPath, "start_rooms"))
					{
						_CustomStartRooms.Add(customRoom);
					}
					else if (IsStructuralPool(roomXmlPath, "finish_rooms"))
					{
						_CustomFinishRooms.Add(customRoom);
					}
					else
					{
						_CustomRooms.Add(customRoom);
					}
					Debug.Log("[CustomRooms] Loaded: " + roomName + " from " + roomXmlPath);
				}
				catch (Exception e)
				{
					Debug.LogWarning("[CustomRooms] Failed to load " + roomXmlPath + ": " + e.Message);
				}
			}
		}

		private static bool IsStructuralPool(string path, string folderName)
		{
			string normalized = path.Replace('\\', '/').ToLowerInvariant();
			return normalized.Contains("/" + folderName.ToLowerInvariant() + "/");
		}

		/// <summary>
		/// Room XML stores its choice catalogue at Track/Properties/Selection.
		/// RoomData receives a smaller room-properties node, so copy the catalogue
		/// onto that transient node before RoomData parses it.
		/// </summary>
		internal static bool AppendEmbeddedRoomSelection(XmlDocument owner, XmlElement roomNode, string roomXmlPath)
		{
			try
			{
				XmlDocument room = new XmlDocument();
				room.Load(roomXmlPath);
				XmlNode selection = room.SelectSingleNode("/Root/Track/Properties/Selection");
				if (selection == null)
					return false;
				roomNode.AppendChild(owner.ImportNode(selection, true));
				return true;
			}
			catch (Exception e)
			{
				Debug.LogWarning("[CustomRooms] Could not read embedded room choices: " + e.Message);
				return false;
			}
		}

		private static bool IsRoomCompatibleWithCurrentZone(string roomXmlPath)
		{
			// A custom zone's RoomsPath defines its room pool. The legacy stock
			// Zone1/Zone2 enum only controls native resources, so texture names in
			// authored rooms must not reclassify a custom chapter as Zone2.
			if (GameManagement.ZoneManager.IsCustomZoneActive) return true;
			Zone? declaredZone = ReadDeclaredRoomZone(roomXmlPath);
			return !declaredZone.HasValue || declaredZone.Value == GameManagement.ZoneManager.CurrentZone;
		}

		private static Zone? ReadDeclaredRoomZone(string roomXmlPath)
		{
			string normalized = roomXmlPath.Replace('\\', '/').ToLowerInvariant();
			if (normalized.Contains("/zone_2/") || normalized.Contains("/zone2/")) return Zone.Zone2;
			if (normalized.Contains("/zone_1/") || normalized.Contains("/zone1/")) return Zone.Zone1;

			string sidecarPath = Path.ChangeExtension(roomXmlPath, ".meta.xml");
			if (File.Exists(sidecarPath))
			{
				try
				{
					XmlDocument sidecar = new XmlDocument();
					sidecar.Load(sidecarPath);
					XmlElement meta = sidecar["RoomMeta"];
					string zone = meta == null ? string.Empty : meta.GetAttribute("Zone");
					if (string.Equals(zone, "Zone2", StringComparison.OrdinalIgnoreCase) || zone == "2") return Zone.Zone2;
					if (string.Equals(zone, "Zone1", StringComparison.OrdinalIgnoreCase) || zone == "1") return Zone.Zone1;
				}
				catch (Exception e)
				{
					Debug.LogWarning("[CustomRooms] Could not read room zone metadata for " + roomXmlPath + ": " + e.Message);
				}
			}

			// Older exported rooms did not have metadata. Maintenance Area artwork is
			// consistently namespaced zone2, so this keeps those legacy rooms out of
			// the Lab without forcing users to re-export everything immediately.
			try
			{
				string xml = File.ReadAllText(roomXmlPath);
				if (xml.IndexOf("ClassName=\"zone2.", StringComparison.OrdinalIgnoreCase) >= 0) return Zone.Zone2;
				if (xml.IndexOf("ClassName=\"zone1.", StringComparison.OrdinalIgnoreCase) >= 0) return Zone.Zone1;
			}
			catch (Exception e)
			{
				Debug.LogWarning("[CustomRooms] Could not identify room zone for " + roomXmlPath + ": " + e.Message);
			}

			return null; // Generic/custom artwork remains backwards compatible.
		}

		private void ParseIncludes(XmlNode p_node)
		{
			foreach (XmlNode childNode in p_node.ChildNodes)
			{
				if (childNode.Name != "Library")
				{
					break;
				}
				_ObjectsFiles.Add(childNode.Attributes["Filename"].Value);
			}
		}

		public Room GetRoom(bool p_testMode = false)
		{
			// Capture this before AddCountersToSelectedGeneratorLabels advances the
			// run. Custom start rooms do not have to contain Vector's stock placeholder.
			bool isRunStartRoom = GameManagement.ZoneManager.IsCustomZoneActive &&
				(int)CounterController.Current.CounterRoomNumber == 0;
			Room randomRoom = GetRandomRoom();
			if (randomRoom == null)
			{
				DebugUtils.Dialog("No usable rooms were found in this custom zone. Check its RoomsPath and room XML.", true);
				return null;
			}
			Debug.Log("[CustomRooms] Selected room=" + randomRoom.Name + " file=" + randomRoom.File);
			CounterController.Current.AddCountersToSelectedGeneratorLabels(randomRoom);
			if (p_testMode)
			{
				return randomRoom;
			}
			// Handle absolute paths for custom rooms — on Windows, Path.IsPathRooted()
			// correctly detects both C:\... and \\server\... style paths.
			XmlDocument xmlDocument;
			if (Path.IsPathRooted(randomRoom.File))
			{
				try
				{
					xmlDocument = new XmlDocument();
					xmlDocument.Load(randomRoom.File);
				}
				catch (Exception e)
				{
					DebugUtils.Dialog("Fatal: Error read XML (custom room: " + randomRoom.File + ")\n" + e.Message, true);
					return null;
				}
			}
			else
			{
				xmlDocument = XmlUtils.OpenXMLDocument(VectorPaths.Rooms, randomRoom.File);
			}
			ParseAICharacters(xmlDocument, randomRoom);
			ParsePlayerAppearance(xmlDocument, randomRoom);
			Nekki.Vector.Core.CustomBackgroundCatalog.ApplyToRoom(xmlDocument, randomRoom.Name, isRunStartRoom);

			if (xmlDocument["Root"]["Track"]["Properties"] != null)
			{
				randomRoom.CounterActions = CounterActions.Create(xmlDocument["Root"]["Track"]["Properties"]["CounterActions"], "ST_Default");
			}
			XmlNode newChild = xmlDocument["Root"]["Track"]["Content"];
			XmlNodeList xmlNodeList = xmlDocument.SelectNodes("//Selection[@Choice]");
			foreach (XmlNode item in xmlNodeList)
			{
				string text = XmlUtils.ParseString(item.Attributes["Parent"], string.Empty);
				if (text.Length > 0)
				{
					text += "/";
				}
				item.Attributes["Choice"].Value = randomRoom.UniqueName + "_" + text + item.Attributes["Choice"].Value;
			}
			XmlElement xmlElement = xmlDocument.CreateElement("Object");
			xmlElement.SetAttribute("Name", randomRoom.Name);
			xmlElement.SetAttribute("X", "0");
			xmlElement.SetAttribute("Y", "0");
			xmlElement.SetAttribute("Factor", "0");
			xmlElement.AppendChild(newChild);
			XmlNode xmlNode2 = xmlDocument.SelectSingleNode("Root/Track");
			xmlNode2.InsertBefore(xmlElement, xmlNode2.FirstChild);
			randomRoom.TmpNode = xmlDocument["Root"]["Track"]["Object"];
			return randomRoom;
		}

		private static void ParseAICharacters(XmlDocument document, Room room)
		{
			room.AICharacters.Clear();
			if (document == null) return;
			XmlNodeList nodes = document.SelectNodes("/Root/AICharacters/AICharacter");
			if (nodes == null) return;
			foreach (XmlNode node in nodes)
			{
				Data data = new Data();
				data.Name = XmlUtils.ParseString(node.Attributes["Name"], "AI");
				data.BirthSpawn = XmlUtils.ParseString(node.Attributes["BirthSpawn"], "DefaultSpawn");
				data.AI = XmlUtils.ParseInt(node.Attributes["AI"], 1);
				data.StartTime = XmlUtils.ParseFloat(node.Attributes["Time"], 0f);
				data.IsPlayer = false;
				data.UsePlayerEquipment = false;
				data.IsEnemy = XmlUtils.ParseString(node.Attributes["Kind"], "Friendly") == "Enemy";
				data.SpawnOnStart = XmlUtils.ParseBool(node.Attributes["SpawnOnStart"], true);
				string skinsValue = XmlUtils.ParseString(node.Attributes["Skins"], "1.xml");
				foreach (string skinValue in skinsValue.Split('|'))
				{
					string skin = skinValue.Trim();
					if (skin.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
						skin = skin.Substring(0, skin.Length - 4);
					if (skin.Length > 0 && skin != "0") data.Skins.Add(skin);
				}
				if (data.Skins.Count == 0) data.Skins.Add("1");
				data.Init();
				room.AICharacters.Add(data);
			}
		}

		private static void ParsePlayerAppearance(XmlDocument document, Room room)
		{
			room.PlayerSkins.Clear();
			XmlNode node = document.SelectSingleNode("/Root/PlayerAppearance");
			if (node == null) return;
			string skins = XmlUtils.ParseString(node.Attributes["Skins"], string.Empty);
			foreach (string value in skins.Split('|'))
			{
				string skin = value.Trim();
				if (skin.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) skin = skin.Substring(0, skin.Length - 4);
				if (skin.Length > 0 && skin != "0") room.PlayerSkins.Add(skin);
			}
		}

		private RoomData GetRoom(string p_name)
		{
			foreach (RoomData room in _Rooms)
			{
				if (room.Name == p_name)
				{
					return room;
				}
			}
			return null;
		}

		private Room GetRandomRoom()
		{
			bool isPlayCommand = GeneratorHelper.IsPlayCommand;
			if (GameManagement.ZoneManager.IsCustomZoneActive && !isPlayCommand)
			{
				string structuralRoom = null;
				List<RoomData> customStructuralPool = null;
				if ((int)CounterController.Current.CounterRoomNumber == 0)
				{
					structuralRoom = "StartRoom";
					customStructuralPool = _CustomStartRooms;
				}
				else if ((int)CounterController.Current.CounterRoomNumberReversed == 1)
				{
					structuralRoom = "FinishRoom";
					customStructuralPool = _CustomFinishRooms;
				}

				if (structuralRoom != null)
				{
					if (customStructuralPool != null && customStructuralPool.Count > 0)
					{
						Debug.Log("[CustomRooms] Using custom " + structuralRoom + " pool; candidates=" + customStructuralPool.Count);
						return GetRandomRoom(customStructuralPool);
					}
					// A custom zone owns its entire structural-room namespace. Falling
					// back to stock StartRoom/FinishRoom here mixes Lab/Maintenance
					// content into a custom chapter, so use a normal custom room instead
					// when the creator has not supplied a structural room yet.
					Debug.LogWarning("[CustomRooms] No custom " + structuralRoom + " found; falling back to the zone's custom room pool.");
				}
				return GetRandomRoom(_CustomRooms);
			}
			return GetRandomRoom(_Rooms);
		}

		private Room GetRandomRoom(List<RoomData> p_rooms, int p_iteration = 0)
		{
			if (p_rooms.Count == 0 || (GeneratorTester.IsActive && GeneratorTester.IsIterationExpired))
			{
				return null;
			}
			CounterController.Current.CounterGenerationAttempt = p_iteration + 1;
			if (p_iteration == MaxAttemptCount)
			{
				DebugUtils.LogToConsole("The generator room fail on " + _SelectedRooms.Count + " room");
				VectorLog.GeneratorLog("FAIL !! ROOM NUMBER " + _SelectedRooms.Count);
				return null;
			}
			// A custom zone's folder is already its complete room pool. Stock zone
			// balance conditions (room length, hazard quotas, etc.) cannot be
			// satisfied by a simple custom room with no generator labels, which left
			// the pool non-empty but made every candidate fail and returned null.
			bool isCustomPool = GameManagement.ZoneManager.IsCustomZoneActive &&
				(p_rooms == _CustomRooms || p_rooms == _CustomStartRooms || p_rooms == _CustomFinishRooms);
			RoomConditionList conditionList = isCustomPool
				? new RoomConditionList(new List<CounterCondition>())
				: ZoneResource<RoomConditions>.Current.GetConditionList();
			if (Settings.WriteGeneratorLogs)
			{
				VectorLog.GeneratorLog(" ");
				VectorLog.GeneratorLog(conditionList);
			}
			Room randomRoom = GetRandomRoom(p_rooms, conditionList, out RoomData roomdata);
			if (randomRoom == null && (int)CounterController.Current.CounterEnableRoomReuse != 0 && p_rooms != _SelectedRooms)
			{
				if (Settings.WriteGeneratorLogs)
				{
					VectorLog.GeneratorLog(" ");
					VectorLog.GeneratorLog("The generator could not find room in list!");
					VectorLog.GeneratorLog("Try to generate from selected rooms list.");
				}
				DebugUtils.LogToConsole("The generator could not find room in list! Try to generate from selected rooms list.");
				randomRoom = GetRandomRoom(_SelectedRooms, p_iteration);
			}
			if (randomRoom == null)
			{
				if (Settings.WriteGeneratorLogs)
				{
					VectorLog.GeneratorLog(" ");
					VectorLog.GeneratorLog("The generator could not find room in list!");
					VectorLog.GeneratorLog("Try to generate againe");
				}
				DebugUtils.LogToConsole($"The generator could not find {roomdata.Name} in list! Try to generate againe.");
				return GetRandomRoom(p_rooms, p_iteration + 1);
			}
			return randomRoom;
		}

		private Room GetRandomRoom(List<RoomData> p_rooms, RoomConditionList p_conditions, out RoomData roomdata)
		{
			MainRandom.ShuffleList(p_rooms);
			Room room = null;
			roomdata = null;
			for (int i = 0; i < p_rooms.Count; i++)
			{
				RoomData roomData = p_rooms[i];
				roomdata = roomData;
				if (!roomData.Check() || !p_conditions.CheckRanges(roomData.Ranges))
				{
					continue;
				}
				room = roomData.CheckConditions(p_conditions);
				if (room == null)
				{
					continue;
				}
				// A custom zone folder is its full reusable room pool. Stock generation
				// consumes each candidate once because it has hundreds of rooms; doing
				// that to a one-room custom zone empties the pool during pre-generation.
				if (p_rooms != _SelectedRooms && !GameManagement.ZoneManager.IsCustomZoneActive)
				{
					p_rooms.Remove(roomData);
					if (!roomData.IsIncludeInPlayCommand)
					{
						roomData.ResetChoiceToIndefined();
						_SelectedRooms.Add(roomData);
					}
				}
				else
				{
					roomData.ResetChoiceToIndefined();
				}
				break;
			}
			return room;
		}
	}
}
