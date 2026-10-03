using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Nekki.Vector.Core.Counter;
using Nekki.Vector.Core.Generator;
using Nekki.Vector.Core.Grid;
using Nekki.Vector.Core.Runners.Animation;
using Nekki.Vector.Core.User;
using UnityEngine;

namespace Nekki.Vector.Core.Runners
{
	public class Sets
	{
		private List<Room> _RoomsOnTrack = new List<Room>();

		private Dictionary<string, Dictionary<string, ObjectReference>> _ObjectsNodesByFiles;

		private Dictionary<string, string> _ChoisesDictionary = new Dictionary<string, string>();

		private List<Runner> _Elements = new List<Runner>();

		private List<Data> _UserData = new List<Data>();

		private List<ObjectRunner> _Objects = new List<ObjectRunner>();

		public List<ObjectRunner> MoveToRootObjects = new List<ObjectRunner>();

		private Nekki.Vector.Core.Grid.Grid _Grid = new Nekki.Vector.Core.Grid.Grid();

		public List<ObjectRunner> RoomObjects = new List<ObjectRunner>();

		public List<VisualRunner> Visuals = new List<VisualRunner>();

		public List<PlatformRunner> Platforms = new List<PlatformRunner>();

		public List<TriggerRunner> Triggers = new List<TriggerRunner>();

		public List<AreaRunner> Areas = new List<AreaRunner>();

		public List<Nekki.Vector.Core.Runners.Animation.Animation> Animations = new List<Nekki.Vector.Core.Runners.Animation.Animation>();

		public List<ParticleRunner> Particles = new List<ParticleRunner>();

		public List<TrapezoidRunner> Trapezoids = new List<TrapezoidRunner>();

		public List<CameraRunner> Cameras = new List<CameraRunner>();

		public List<SpawnRunner> Spawns = new List<SpawnRunner>();

		public List<QuadRunner> Quads = new List<QuadRunner>();

		public List<QuadRunner> QuadsAll = new List<QuadRunner>();

		public List<SensorRunner> Sensors = new List<SensorRunner>();

		private GateRunner _LastGate;

		private GameObject _DebugLayer = new GameObject("Debug data");

		public List<Room> RoomsOnTrack
		{
			get
			{
				return _RoomsOnTrack;
			}
		}

		public List<Runner> Elements
		{
			get
			{
				return _Elements;
			}
		}

		public List<Data> UserData
		{
			get
			{
				return _UserData;
			}
		}

		public List<ObjectRunner> Objects
		{
			get
			{
				return _Objects;
			}
		}

		public Nekki.Vector.Core.Grid.Grid Grid
		{
			get
			{
				return _Grid;
			}
		}

		public GateRunner LastGate
		{
			get
			{
				return _LastGate;
			}
		}

		public GameObject DebugLayer
		{
			get
			{
				return _DebugLayer;
			}
		}

		public bool DebugLayerVisible
		{
			set
			{
				_DebugLayer.SetActive(value);
			}
		}

		public Sets()
		{
			VisualRunner.Counter = 0;
			UnityModelRunner.Counter = 0;
			_DebugLayer.SetActive(false);
		}

		public void Init()
		{
			ParseLibrary();
			AddModeles(1);
		}

		public void RemoveTemplaryData()
		{
			_ObjectsNodesByFiles.Clear();
			_ObjectsNodesByFiles = null;
		}

		private void ParseLibrary()
		{
			_ObjectsNodesByFiles = new Dictionary<string, Dictionary<string, ObjectReference>>();
			List<string> libraryFiles = new List<string>(RunMainController.Location.Generator.ObjectsFile);
			foreach (string customLibrary in CustomTrapLibraryCatalog.LibraryFiles())
			{
				if (!libraryFiles.Contains(customLibrary)) libraryFiles.Add(customLibrary);
			}
			foreach (string item in libraryFiles)
			{
				XmlNode xmlNode = XmlUtils.OpenXMLDocument(VectorPaths.RunDataLibs, item);
				if (xmlNode == null || xmlNode["Root"] == null || xmlNode["Root"]["Objects"] == null)
				{
					Debug.Log("Empty node from file \"" + item + "\"!");
					continue;
				}
				XmlNode xmlNode2 = xmlNode["Root"]["Objects"];
				Dictionary<string, ObjectReference> dictionary = new Dictionary<string, ObjectReference>();
				foreach (XmlNode item2 in xmlNode2)
				{
					if (!(item2.Name != "Object"))
					{
						string objectName = item2.Attributes["Name"].Value;
						if (!dictionary.ContainsKey(objectName)) dictionary.Add(objectName, new ObjectReference(item2));
					}
				}
				if (!_ObjectsNodesByFiles.ContainsKey(item)) _ObjectsNodesByFiles.Add(item, dictionary);
			}
		}

		public void PregenerateRoom(int p_count, LocationGenerator p_generator)
		{
			List<ObjectRunner> list = new List<ObjectRunner>();
			PlaceholderManager placeholderManager = new PlaceholderManager();
			int num = CounterController.Current.CounterRoomNumberReversed;
			int num2 = CounterController.Current.CounterRoomNumber;
			DebugUtils.StartTimer("Generation");
			Room[] array = new Room[p_count];
			for (int i = 0; i < p_count; i++)
			{
				array[i] = p_generator.GetRoom(false);
				CounterController current = CounterController.Current;
				current.CounterRoomNumberReversed = (int)current.CounterRoomNumberReversed - 1;
				CounterController current2 = CounterController.Current;
				current2.CounterRoomNumber = (int)current2.CounterRoomNumber + 1;
			}
			CounterController.Current.CounterFloorGenerationTime = (int)DebugUtils.StopTimerWithMessage("Generator", "Generation");
			CounterController.Current.CounterRoomNumberReversed = num;
			CounterController.Current.CounterRoomNumber = num2;
			for (int j = 0; j < p_count; j++)
			{
				Room room = array[j];
				AppendRoomAICharacters(room);
				AddChoiceByRoom(room);
				_RoomsOnTrack.Add(room);
				ObjectRunner objectRunner = (room.Object = new ObjectRunner());
				objectRunner.Parse(room.TmpNode, _ChoisesDictionary);
				room.TmpNode = null;
				room.CollectGates();
				room.CurrentIn = room.Ins[0];
				room.CurrentOut = room.Outs[0];
				objectRunner.UpdatePosition(new Vector3f(((_LastGate == null) ? default(Vector3) : _LastGate.Position) - room.CurrentIn.Position));
				_LastGate = room.CurrentOut;
				list.Add(objectRunner);
				GetAllPlaceholders(objectRunner, placeholderManager);
				CounterController current3 = CounterController.Current;
				current3.CounterRoomNumberReversed = (int)current3.CounterRoomNumberReversed - 1;
				CounterController current4 = CounterController.Current;
				current4.CounterRoomNumber = (int)current4.CounterRoomNumber + 1;
			}
			DebugUtils.StartTimer("Postprocess");
			placeholderManager.Postprocess(_ChoisesDictionary);
			CounterController.Current.CounterFloorPostprocessTime = (int)DebugUtils.TimerElapsed("Postprocess");
			foreach (ObjectRunner item in list)
			{
				_Objects.Add(item);
				RoomObjects.Add(item);
				item.Init();
				GetElements(item);
			}
			for (int k = 0; k < MoveToRootObjects.Count; k++)
			{
				MoveToRootObjects[k].MoveToRoot();
			}
		}

		private void AppendRoomAICharacters(Room room)
		{
			if (room == null) return;
			List<string> protocolLayers = GameManagement.CustomProtocolCatalog.SelectedModelLayers;
			List<string> selectedLayers = room.PlayerSkins != null && room.PlayerSkins.Count > 0 ? room.PlayerSkins : protocolLayers;
			if (selectedLayers != null && selectedLayers.Count > 0)
			{
				foreach (Data player in _UserData)
				{
					if (!player.IsPlayer) continue;
					player.Skins = new List<string> { "0.xml" };
					foreach (string layer in selectedLayers)
					{
						string skin = layer.StartsWith("custom:", StringComparison.OrdinalIgnoreCase) || layer.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ? layer : layer + ".xml";
						if (skin != "0.xml" && !player.Skins.Contains(skin)) player.Skins.Add(skin);
					}
					player.UsePlayerEquipment = false;
					break;
				}
			}
			if (room.AICharacters == null) return;
			foreach (Data candidate in room.AICharacters)
			{
				bool exists = false;
				foreach (Data current in _UserData)
				{
					if (current.Name == candidate.Name)
					{
						exists = true;
						break;
					}
				}
				if (!exists) _UserData.Add(candidate);
			}
		}

		public void AddRoomByXMLNode(Room p_room)
		{
			new Exception("Not use this function!");
		}

		private void GetAllPlaceholders(ObjectRunner objectRunner, PlaceholderManager p_placeholderManager)
		{
			p_placeholderManager.AppendPlaceholders(objectRunner.Element.Placeholders);
			objectRunner.Element.Placeholders.Clear();
			foreach (ObjectRunner child in objectRunner.Childs)
			{
				GetAllPlaceholders(child, p_placeholderManager);
			}
		}

		private void AddChoiceByRoom(Room p_room)
		{
			p_room.AddChoices(_ChoisesDictionary);
		}

		public void ChangeOut(string p_name, int p_x)
		{
			Room room = GetRoom(p_x);
			Point point = room.SwitchOutAndGetDelta(p_name);
			if (point.X != 0f || point.Y != 0f)
			{
				int num = _RoomsOnTrack.LastIndexOf(room);
				for (int i = num + 1; i < RoomObjects.Count; i++)
				{
					_Grid.RemoveQuadByObject(RoomObjects[i]);
					RoomObjects[i].MoveLocalPosition(new Vector3f(point.X, point.Y, 0f));
					_Grid.AddQuadByObject(RoomObjects[i]);
				}
			}
		}

		private void AddModeles(int p_count)
		{
			for (int i = 0; i < p_count; i++)
			{
				Data data = new Data();
				data.Name = ((i != 0) ? "Hunter" : "Player");
				data.BirthSpawn = "DefaultSpawn";
				data.Skins = ((i != 0) ? new List<string>("hunter".Split('|')) : new List<string>());
				data.IsPlayer = i == 0;
				data.AI = i;
				if (data.Skins.Count == 0)
				{
					data.Skins.Add("1");
				}
				data.Init();
				_UserData.Add(data);
			}
		}

		public static ObjectReference ObjectNode(string p_name, string p_fileName)
		{
			Sets sets = RunMainController.Location.Sets;
			if (sets != null && sets._ObjectsNodesByFiles != null && sets._ObjectsNodesByFiles.ContainsKey(p_fileName) && sets._ObjectsNodesByFiles[p_fileName].ContainsKey(p_name))
			{
				return sets._ObjectsNodesByFiles[p_fileName][p_name];
			}
			if (p_fileName != null && p_fileName.StartsWith("v2trap_", StringComparison.OrdinalIgnoreCase))
			{
				ObjectReference customTrap;
				if (TryLoadCustomTrapObject(sets, p_fileName, p_name, out customTrap))
				{
					return customTrap;
				}
				Debug.LogError("[CustomTraps] Missing object " + p_name + " in " + p_fileName + ". Reinstall the project or resave the room from the editor.");
			}
			else
			{
				DebugUtils.Dialog("Error: Not found object Name:" + p_name + " in File:" + p_fileName, true);
			}
			return null;
		}

		// Play can install a freshly compiled trap after this Sets instance has
		// already scanned its libraries. Resolve editor-owned trap files lazily so
		// both hot Play and ordinary room loading use the same reliable path.
		private static bool TryLoadCustomTrapObject(Sets p_sets, string p_fileName, string p_name, out ObjectReference p_reference)
		{
			p_reference = null;
			if (p_sets == null || p_sets._ObjectsNodesByFiles == null) return false;
			if (Path.GetFileName(p_fileName) != p_fileName || !p_fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) return false;
			string path = Path.Combine(CustomProjectCatalog.StorageRoot, "custom_gamedata", "run_data", "libraries", p_fileName);
			if (!File.Exists(path)) return false;
			try
			{
				XmlDocument document = new XmlDocument();
				document.Load(path);
				XmlNode objects = document.SelectSingleNode("/Root/Objects");
				if (objects == null) return false;
				Dictionary<string, ObjectReference> library = new Dictionary<string, ObjectReference>();
				foreach (XmlNode node in objects.ChildNodes)
				{
					if (node.Name != "Object" || node.Attributes == null || node.Attributes["Name"] == null) continue;
					string objectName = node.Attributes["Name"].Value;
					if (!objectName.StartsWith("V2Trap_", StringComparison.OrdinalIgnoreCase)) continue;
					if (!library.ContainsKey(objectName)) library.Add(objectName, new ObjectReference(node));
				}
				p_sets._ObjectsNodesByFiles[p_fileName] = library;
				if (!library.TryGetValue(p_name, out p_reference)) return false;
				Debug.Log("[CustomTraps] Loaded updated library on demand: " + p_fileName);
				return true;
			}
			catch (Exception e)
			{
				Debug.LogError("[CustomTraps] Could not load " + p_fileName + ": " + e.Message);
				return false;
			}
		}

		public void InitRooms()
		{
			for (int i = 2; i < _RoomsOnTrack.Count; i++)
			{
				_RoomsOnTrack[i].Object.IsEnableUnityGO = false;
			}
		}

		private void GetElements(ObjectRunner p_objects)
		{
			_Elements.AddRange(p_objects.Element.Elements);
			Add(p_objects.Element);
			foreach (ObjectRunner child in p_objects.Childs)
			{
				GetElements(child);
			}
		}

		private void Add(Element p_element)
		{
			Visuals.AddRange(p_element.Visuals);
			Platforms.AddRange(p_element.Platforms);
			Triggers.AddRange(p_element.Triggers);
			Areas.AddRange(p_element.Areas);
			Animations.AddRange(p_element.Animations);
			Particles.AddRange(p_element.Particles);
			Trapezoids.AddRange(p_element.Trapezoids);
			Spawns.AddRange(p_element.Spawns);
			Cameras.AddRange(p_element.Cameras);
			Sensors.AddRange(p_element.Sensors);
			QuadsAll.AddRange(p_element.QuadsAll);
			foreach (PlatformRunner platform in p_element.Platforms)
			{
				Quads.Add(platform);
			}
			foreach (TrapezoidRunner trapezoid in p_element.Trapezoids)
			{
				Quads.Add(trapezoid);
			}
		}

		public string GetRoomNameByX(int p_x)
		{
			Room room = GetRoom(p_x);
			if (room != null)
			{
				return room.Name;
			}
			return "Player is outside all rooms (p_x = " + p_x + ")";
		}

		public string GetRoomUniqueNameByX(int p_x)
		{
			Room room = GetRoom(p_x);
			if (room != null)
			{
				return room.UniqueName;
			}
			return "Player is outside all rooms (p_x = " + p_x + ")";
		}

		public Room GetRoom(int p_x)
		{
			int roomIndex = GetRoomIndex(p_x);
			if (roomIndex == -1)
			{
				return null;
			}
			return _RoomsOnTrack[roomIndex];
		}

		public Room GetNextRoom(int p_x)
		{
			int roomIndex = GetRoomIndex(p_x);
			if (roomIndex == -1 || roomIndex + 1 >= _RoomsOnTrack.Count)
			{
				return null;
			}
			return _RoomsOnTrack[roomIndex + 1];
		}

		public int GetRoomIndex(int p_x)
		{
			Room room = null;
			int i = 0;
			for (int count = _RoomsOnTrack.Count; i < count; i++)
			{
				room = _RoomsOnTrack[i];
				if (room.CurrentIn.Position.x < (float)p_x && room.CurrentOut.Position.x > (float)p_x)
				{
					return i;
				}
			}
			return -1;
		}

		public string GetChoisesDebugInfo(string roomName)
		{
			if (string.IsNullOrEmpty(roomName))
			{
				return string.Empty;
			}
			StringBuilder stringBuilder = new StringBuilder();
			foreach (KeyValuePair<string, string> item in _ChoisesDictionary)
			{
				if (item.Key.Contains(roomName))
				{
					string arg = item.Key.Replace(roomName, string.Empty).Replace("_", string.Empty);
					stringBuilder.AppendFormat("<color=red>{0}:</color> {1}\n", arg, item.Value);
				}
			}
			return stringBuilder.ToString().Trim('\n');
		}

		public void End()
		{
			for (int i = 0; i < _Elements.Count; i++)
			{
				_Elements[i].End();
			}
		}
	}
}
