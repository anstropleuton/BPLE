using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public class DataExtractor : MonoBehaviour
{
	public static string prefabsFolder = "Assets/GameObject";
	public static string filenameExpression = "Part_*.prefab";

	public static string levelBytesRootFolder = "Assets/assetbundles";
	public static string levelBytesFileExpression = "Level_*_data.bytes";

	public static string dataPath;
	public static bool isInitialized = false;
	public static bool splitFiles = false;

	private const string SchemaName = "DataExtractor.Json.v2";
	private const string JsonExtension = ".json";

	[MenuItem("Data Extractor/Reinitialize")]
	public static void Initialize()
	{
		dataPath = Path.Combine(Application.persistentDataPath, "DataExtractorData");
		Directory.CreateDirectory(dataPath);
		isInitialized = true;
		Debug.Log("Data extractor initialized at: " + dataPath);
	}

	[MenuItem("Data Extractor/Set Split Files")]
	public static void SetSplitFiles()
	{
		splitFiles = true;
		Debug.Log("Data extractor set to split-file mode.");
	}

	[MenuItem("Data Extractor/Set Combine Files")]
	public static void SetCombineFiles()
	{
		splitFiles = false;
		Debug.Log("Data extractor set to combined-file mode.");
	}

	[MenuItem("Data Extractor/Extract All Data")]
	public static void ExtractAllData()
	{
		EnsureInitialized();

		string generatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
		WriteReadmeFile(generatedAtUtc);
		ExtractPartData(generatedAtUtc);
		ExtractSpriteData(generatedAtUtc);
		ExtractLevelData(generatedAtUtc);

		Debug.Log("Export complete.");
	}

	[MenuItem("Data Extractor/Extract Part Data")]
	public static void ExtractPartData()
	{
		EnsureInitialized();
		ExtractPartData(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
	}

	[MenuItem("Data Extractor/Extract Sprite Data")]
	public static void ExtractSpriteData()
	{
		EnsureInitialized();
		ExtractSpriteData(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
	}

	[MenuItem("Data Extractor/Extract Level Data")]
	public static void ExtractLevelData()
	{
		EnsureInitialized();
		ExtractLevelData(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
	}

	private static void EnsureInitialized()
	{
		if (!isInitialized)
		{
			Initialize();
		}
	}

	private static void WriteTextFile(string filePath, string contents)
	{
		string directory = Path.GetDirectoryName(filePath);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		File.WriteAllText(filePath, contents, new UTF8Encoding(false));
	}

	private static string SerializeJson(JToken token)
	{
		return JsonConvert.SerializeObject(token, Formatting.Indented);
	}

	private static string SanitizeFileName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return "unnamed";
		}

		char[] invalid = Path.GetInvalidFileNameChars();
		var sb = new StringBuilder(name.Length);

		foreach (char c in name.Trim())
		{
			sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
		}

		string result = sb.ToString().Trim().TrimEnd('.');
		return string.IsNullOrWhiteSpace(result) ? "unnamed" : result;
	}

	private static string NormalizeAssetPath(string filePath)
	{
		if (string.IsNullOrEmpty(filePath))
		{
			return filePath;
		}

		string normalized = filePath.Replace('\\', '/');
		string dataPathNormalized = Application.dataPath.Replace('\\', '/');

		if (Path.IsPathRooted(normalized) &&
		    normalized.StartsWith(dataPathNormalized, StringComparison.OrdinalIgnoreCase))
		{
			return "Assets" + normalized.Substring(dataPathNormalized.Length);
		}

		return normalized;
	}

	private static JValue Str(string value) => value == null ? JValue.CreateNull() : new JValue(value);
	private static JValue Num(int value) => new JValue(value);
	private static JValue Num(long value) => new JValue(value);
	private static JValue Num(float value) => float.IsNaN(value) || float.IsInfinity(value) ? JValue.CreateNull() : new JValue(value);
	private static JValue Num(double value) => double.IsNaN(value) || double.IsInfinity(value) ? JValue.CreateNull() : new JValue(value);
	private static JValue Bool(bool value) => new JValue(value);

	private static JObject EnumObj(Enum value)
	{
		return new JObject
		{
			["name"] = Str(value.ToString()),
			["value"] = Num(Convert.ToInt64(value, CultureInfo.InvariantCulture))
		};
	}

	private static JObject Vec2Obj(Vector2 v)
	{
		return new JObject
		{
			["x"] = Num(v.x),
			["y"] = Num(v.y)
		};
	}

	private static JObject Vec3Obj(Vector3 v)
	{
		return new JObject
		{
			["x"] = Num(v.x),
			["y"] = Num(v.y),
			["z"] = Num(v.z)
		};
	}

	private static JObject Vec4Obj(Vector4 v)
	{
		return new JObject
		{
			["x"] = Num(v.x),
			["y"] = Num(v.y),
			["z"] = Num(v.z),
			["w"] = Num(v.w)
		};
	}

	private static JObject QuatObj(Quaternion q)
	{
		return new JObject
		{
			["x"] = Num(q.x),
			["y"] = Num(q.y),
			["z"] = Num(q.z),
			["w"] = Num(q.w)
		};
	}

	private static JObject RectObj(Rect r)
	{
		return new JObject
		{
			["x"] = Num(r.x),
			["y"] = Num(r.y),
			["width"] = Num(r.width),
			["height"] = Num(r.height)
		};
	}

	private static JObject ColorObj(Color c)
	{
		return new JObject
		{
			["r"] = Num(c.r),
			["g"] = Num(c.g),
			["b"] = Num(c.b),
			["a"] = Num(c.a)
		};
	}

	private static JObject Int2Obj(int x, int y)
	{
		return new JObject
		{
			["x"] = Num(x),
			["y"] = Num(y)
		};
	}

	private static JObject IntRectObj(int x, int y, int width, int height)
	{
		return new JObject
		{
			["x"] = Num(x),
			["y"] = Num(y),
			["width"] = Num(width),
			["height"] = Num(height)
		};
	}

	private static JObject BoundsObj(Bounds bounds)
	{
		return new JObject
		{
			["center"] = Vec3Obj(bounds.center),
			["size"] = Vec3Obj(bounds.size)
		};
	}

	private static void WriteReadmeFile(string generatedAtUtc)
	{
		var sb = new StringBuilder();

		sb.AppendLine("# Data Extractor JSON Export");
		sb.AppendLine();
		sb.AppendLine("Generated at UTC: " + generatedAtUtc);
		sb.AppendLine();
		sb.AppendLine("This folder is the output of the Data Extractor editor utility.");
		sb.AppendLine("The extractor now writes structured JSON using Newtonsoft.Json instead of the old custom text format.");
		sb.AppendLine("The goal of the export is to preserve as much useful runtime/editor data as possible in a machine-readable form, while still leaving enough human-readable metadata to inspect the results directly.");
		sb.AppendLine();
		sb.AppendLine("## What this exporter does");
		sb.AppendLine();
		sb.AppendLine("The exporter can write three main data families:");
		sb.AppendLine();
		sb.AppendLine("1. Part data");
		sb.AppendLine("2. Sprite data");
		sb.AppendLine("3. Level data");
		sb.AppendLine();
		sb.AppendLine("Part data comes from prefab assets in `prefabsFolder` that match `filenameExpression`.");
		sb.AppendLine("Sprite data comes from the runtime sprite database.");
		sb.AppendLine("Level data is parsed directly from the game’s `.bytes` files used by `LevelLoader`.");
		sb.AppendLine();
		sb.AppendLine("The exporter supports two output layouts:");
		sb.AppendLine();
		sb.AppendLine("- Combined mode: one JSON file per data family.");
		sb.AppendLine("- Split mode: one JSON file per item.");
		sb.AppendLine();
		sb.AppendLine("Split mode is useful when you want to inspect or diff individual records.");
		sb.AppendLine("Combined mode is useful when you want a single file per category for bulk processing.");
		sb.AppendLine();
		sb.AppendLine("## Output directory layout");
		sb.AppendLine();
		sb.AppendLine("All generated files are written under:");
		sb.AppendLine();
		sb.AppendLine("```text");
		sb.AppendLine(Application.persistentDataPath + "/DataExtractorData");
		sb.AppendLine("```");
		sb.AppendLine();
		sb.AppendLine("Common files:");
		sb.AppendLine();
		sb.AppendLine("- `README.md` — this document.");
		sb.AppendLine("- `Parts.json` or `Parts/` — part export output.");
		sb.AppendLine("- `Sprites.json` or `Sprites/` — sprite export output.");
		sb.AppendLine("- `Levels.json` or `Levels/` — level export output.");
		sb.AppendLine();
		sb.AppendLine("## JSON document format");
		sb.AppendLine();
		sb.AppendLine("Every exported JSON document follows the same outer shape:");
		sb.AppendLine();
		sb.AppendLine("```json");
		sb.AppendLine("{");
		sb.AppendLine("  \"schema\": \"DataExtractor.Json.v2\",");
		sb.AppendLine("  \"generatedAtUtc\": \"2026-01-01T00:00:00.0000000Z\",");
		sb.AppendLine("  \"recordType\": \"part | sprite | level\",");
		sb.AppendLine("  \"collection\": true,");
		sb.AppendLine("  \"source\": { ... },");
		sb.AppendLine("  \"data\": { ... }");
		sb.AppendLine("}");
		sb.AppendLine("```");
		sb.AppendLine();
		sb.AppendLine("Field meanings:");
		sb.AppendLine();
		sb.AppendLine("- `schema`: version tag for the current export format.");
		sb.AppendLine("- `generatedAtUtc`: UTC timestamp for the export run.");
		sb.AppendLine("- `recordType`: identifies the data family.");
		sb.AppendLine("- `collection`: `true` for combined files, `false` for single-record files.");
		sb.AppendLine("- `source`: contextual information about where the data came from.");
		sb.AppendLine("- `data`: the actual exported payload.");
		sb.AppendLine();
		sb.AppendLine("## Common value encoding rules");
		sb.AppendLine();
		sb.AppendLine("The exporter uses a few consistent representation rules so the output stays predictable:");
		sb.AppendLine();
		sb.AppendLine("- Strings are written as JSON strings.");
		sb.AppendLine("- Integers are written as JSON numbers.");
		sb.AppendLine("- Floats and doubles are written as JSON numbers when finite.");
		sb.AppendLine("- Non-finite float values (`NaN`, `Infinity`, `-Infinity`) are written as `null`.");
		sb.AppendLine("- Vectors are written as objects with component names like `x`, `y`, `z`, `w`.");
		sb.AppendLine("- Rectangles are written as objects with `x`, `y`, `width`, `height`.");
		sb.AppendLine("- Enums are written as objects containing both `name` and `value`.");
		sb.AppendLine("- Missing optional data is written as `null`.");
		sb.AppendLine();
		sb.AppendLine("This is intentionally verbose so the output can be consumed by scripts without needing to know the Unity runtime types in advance.");
		sb.AppendLine();
		sb.AppendLine("## Part export");
		sb.AppendLine();
		sb.AppendLine("Part export records are built from prefab instances loaded from the prefab folder.");
		sb.AppendLine("The exporter walks the prefab hierarchy recursively and writes one JSON object per GameObject.");
		sb.AppendLine();
		sb.AppendLine("Each part record usually contains:");
		sb.AppendLine();
		sb.AppendLine("- `name`");
		sb.AppendLine("- `path`");
		sb.AppendLine("- `tag`");
		sb.AppendLine("- `layer`");
		sb.AppendLine("- `activeSelf`");
		sb.AppendLine("- `activeInHierarchy`");
		sb.AppendLine("- `transform`");
		sb.AppendLine("- `prefabSource`");
		sb.AppendLine("- `components`");
		sb.AppendLine("- `children`");
		sb.AppendLine();
		sb.AppendLine("The `transform` block stores both absolute and local transform state:");
		sb.AppendLine();
		sb.AppendLine("- world position");
		sb.AppendLine("- local position");
		sb.AppendLine("- world rotation");
		sb.AppendLine("- local rotation");
		sb.AppendLine("- lossy scale");
		sb.AppendLine("- local scale");
		sb.AppendLine("- values relative to the root part");
		sb.AppendLine();
		sb.AppendLine("The `components` block contains any supported attached data found on the GameObject, such as:");
		sb.AppendLine();
		sb.AppendLine("- scripts");
		sb.AppendLine("- box colliders");
		sb.AppendLine("- capsule colliders");
		sb.AppendLine("- sphere colliders");
		sb.AppendLine("- sprite component data");
		sb.AppendLine("- serialized sprite metadata");
		sb.AppendLine("- base part data");
		sb.AppendLine();
		sb.AppendLine("Collider records keep the collider geometry and physical material data together.");
		sb.AppendLine("That means the JSON already contains the values that were previously spread between part data and the separate material export.");
		sb.AppendLine();
		sb.AppendLine("Base part records are intentionally detailed and include both serialized fields and several computed or virtual values when they can be read safely.");
		sb.AppendLine("If a virtual getter throws during export, the exporter skips just that field and continues.");
		sb.AppendLine();
		sb.AppendLine("Child parts are embedded recursively under `children`, so a full prefab subtree is preserved in a single document.");
		sb.AppendLine();
		sb.AppendLine("## Sprite export");
		sb.AppendLine();
		sb.AppendLine("Sprite export records are built from the runtime sprite database.");
		sb.AppendLine("Each sprite record preserves the raw sprite data and also includes derived geometry information that is useful for engine-porting or validation work.");
		sb.AppendLine();
		sb.AppendLine("Typical sprite fields include:");
		sb.AppendLine();
		sb.AppendLine("- `id`");
		sb.AppendLine("- `dataId`");
		sb.AppendLine("- `name`");
		sb.AppendLine("- `materialId`");
		sb.AppendLine("- `selection`");
		sb.AppendLine("- `pivot`");
		sb.AppendLine("- `uvPosition`");
		sb.AppendLine("- `size`");
		sb.AppendLine("- `subdivisions`");
		sb.AppendLine("- `opaqueBorderPixels`");
		sb.AppendLine("- `atlasMaterialPath`");
		sb.AppendLine("- `uvRect`");
		sb.AppendLine("-");
		sb.AppendLine("The `context` block records the scale and pivot values that were used during export.");
		sb.AppendLine("The `computed` block contains derived values such as:");
		sb.AppendLine();
		sb.AppendLine("- scaled size");
		sb.AppendLine("- selection center");
		sb.AppendLine("- UV center");
		sb.AppendLine("- center difference");
		sb.AppendLine("- transformed pivot");
		sb.AppendLine("- synthetic mesh vertices");
		sb.AppendLine("- raylib-style source/destination rectangle data");
		sb.AppendLine();
		sb.AppendLine("This makes it easier to compare the game’s internal sprite layout against other rendering systems.");
		sb.AppendLine();
		sb.AppendLine("## Level export");
		sb.AppendLine();
		sb.AppendLine("Level data is parsed directly from the binary `.bytes` files used by `LevelLoader`.");
		sb.AppendLine("The extractor does not require the scene to be loaded for this export.");
		sb.AppendLine("Instead, it reads the level file format itself and reconstructs the level structure from the binary stream.");
		sb.AppendLine();
		sb.AppendLine("A level file begins with a root object count.");
		sb.AppendLine("Each root object is then read recursively.");
		sb.AppendLine("The object header indicates whether the object is a prefab instance or a parent object that only contains children.");
		sb.AppendLine();
		sb.AppendLine("### Prefab instance records");
		sb.AppendLine();
		sb.AppendLine("Prefab instance records preserve:");
		sb.AppendLine();
		sb.AppendLine("- object name");
		sb.AppendLine("- prefab index");
		sb.AppendLine("- position");
		sb.AppendLine("- Euler rotation");
		sb.AppendLine("- local scale");
		sb.AppendLine("- trailing data block");
		sb.AppendLine();
		sb.AppendLine("The prefab index is kept as a numeric reference into the loader’s prefab list.");
		sb.AppendLine("That makes it possible to map the record back to the original prefab source when the reference table is available.");
		sb.AppendLine();
		sb.AppendLine("### Parent object records");
		sb.AppendLine();
		sb.AppendLine("Parent object records preserve:");
		sb.AppendLine();
		sb.AppendLine("- object name");
		sb.AppendLine("- position");
		sb.AppendLine("- child count");
		sb.AppendLine("- recursively nested children");
		sb.AppendLine();
		sb.AppendLine("Parent objects are structural nodes in the level file and do not directly carry the prefab-instance metadata.");
		sb.AppendLine();
		sb.AppendLine("### Data block types");
		sb.AppendLine();
		sb.AppendLine("The binary data block following a prefab instance begins with a data type byte.");
		sb.AppendLine("Supported types in the current extractor are:");
		sb.AppendLine();
		sb.AppendLine("- `None`");
		sb.AppendLine("- `Terrain`");
		sb.AppendLine("- `PrefabOverrides`");
		sb.AppendLine();
		sb.AppendLine("### Terrain block");
		sb.AppendLine();
		sb.AppendLine("Terrain blocks are exported in a way that preserves the important runtime structure and the raw source data needed for reconstruction.");
		sb.AppendLine();
		sb.AppendLine("Terrain export fields include:");
		sb.AppendLine();
		sb.AppendLine("- fill texture tile offsets");
		sb.AppendLine("- fill mesh data");
		sb.AppendLine("- fill color");
		sb.AppendLine("- fill texture reference index");
		sb.AppendLine("- curve mesh data");
		sb.AppendLine("- curve texture list");
		sb.AppendLine("- control texture bytes when present");
		sb.AppendLine("- collider presence flag");
		sb.AppendLine("- derived collider mesh data");
		sb.AppendLine();
		sb.AppendLine("The fill and curve meshes are written as vertex and triangle arrays.");
		sb.AppendLine("Curve textures are written as a list of texture reference indices plus their size and flags.");
		sb.AppendLine();
		sb.AppendLine("If a control texture blob is present, the exporter writes the raw bytes as Base64.");
		sb.AppendLine("That keeps the export portable and avoids loss of information.");
		sb.AppendLine();
		sb.AppendLine("The collider mesh is reconstructed from the fill mesh outline in the same general style as the runtime collider generation path.");
		sb.AppendLine("The exporter does not pretend this is the exact serialized collider mesh from the file; it is a faithful derived representation useful for inspection and validation.");
		sb.AppendLine();
		sb.AppendLine("### Prefab override blocks");
		sb.AppendLine();
		sb.AppendLine("Prefab override blocks are opaque byte buffers in the loader.");
		sb.AppendLine("The extractor preserves them as:");
		sb.AppendLine();
		sb.AppendLine("- byte length");
		sb.AppendLine("- Base64 raw bytes");
		sb.AppendLine("- UTF-8 text when the bytes decode cleanly");
		sb.AppendLine();
		sb.AppendLine("This means the content is still available for later reverse engineering even when the format is not fully decoded.");
		sb.AppendLine();
		sb.AppendLine("## Combined vs split output");
		sb.AppendLine();
		sb.AppendLine("Combined mode writes one file per data family:");
		sb.AppendLine();
		sb.AppendLine("- `Parts.json`");
		sb.AppendLine("- `Sprites.json`");
		sb.AppendLine("- `Levels.json`");
		sb.AppendLine();
		sb.AppendLine("Split mode writes one file per record:");
		sb.AppendLine();
		sb.AppendLine("- `Parts/<name>.json`");
		sb.AppendLine("- `Sprites/<id>.json`");
		sb.AppendLine("- `Levels/<level>.json`");
		sb.AppendLine();
		sb.AppendLine("Split filenames are sanitized to avoid invalid path characters.");
		sb.AppendLine("The original names remain in the JSON payload.");
		sb.AppendLine();
		sb.AppendLine("## Practical notes");
		sb.AppendLine();
		sb.AppendLine("This export format is intentionally descriptive rather than compact.");
		sb.AppendLine("The priority is to preserve enough information for analysis, comparison, and recreation work.");
		sb.AppendLine();
		sb.AppendLine("A few details are worth keeping in mind:");
		sb.AppendLine();
		sb.AppendLine("- Some exported fields are derived rather than directly serialized from the game file.");
		sb.AppendLine("- Some Unity objects can only be represented by metadata, names, and asset paths.");
		sb.AppendLine("- Some runtime getters may fail during export; those fields are skipped individually.");
		sb.AppendLine("- Raw bytes are preserved where the loader itself treats the content as opaque.");
		sb.AppendLine();
		sb.AppendLine("## Versioning");
		sb.AppendLine();
		sb.AppendLine("If the export format changes again, the `schema` value should be updated.");
		sb.AppendLine("That makes it possible for downstream tools to detect newer layouts without guessing.");
		sb.AppendLine();
		sb.AppendLine("Current schema: `DataExtractor.Json.v2`");
		sb.AppendLine();
		sb.AppendLine("## Summary");
		sb.AppendLine();
		sb.AppendLine("This exporter is designed to turn Unity-side runtime and asset data into structured JSON that is easy to read, diff, parse, and archive.");
		sb.AppendLine("It keeps the output detailed on purpose so that the exported files can serve both as machine input and as a practical reference document for reverse engineering work.");

		WriteTextFile(Path.Combine(dataPath, "README.md"), sb.ToString());
	}

	private static JObject BuildDocument(string recordType, bool collection, JToken data, string generatedAtUtc, JToken source = null)
	{
		var doc = new JObject
		{
			["schema"] = Str(SchemaName),
			["generatedAtUtc"] = Str(generatedAtUtc),
			["recordType"] = Str(recordType),
			["collection"] = Bool(collection),
			["data"] = data
		};

		if (source != null)
		{
			doc["source"] = source;
		}

		return doc;
	}

	// ---------------------------------------------------------------------
	// PART EXPORT
	// ---------------------------------------------------------------------

	private static void ExtractPartData(string generatedAtUtc)
	{
		string[] prefabFiles = Directory.Exists(prefabsFolder)
			? Directory.GetFiles(prefabsFolder, filenameExpression)
			: Array.Empty<string>();

		Array.Sort(prefabFiles, StringComparer.OrdinalIgnoreCase);

		if (splitFiles)
		{
			string partsFolder = Path.Combine(dataPath, "Parts");
			if (Directory.Exists(partsFolder))
			{
				Directory.Delete(partsFolder, true);
			}
			Directory.CreateDirectory(partsFolder);

			HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (string prefabFile in prefabFiles)
			{
				string assetPath = NormalizeAssetPath(prefabFile);
				string prefabName = Path.GetFileNameWithoutExtension(prefabFile);
				GameObject part = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
				if (part == null)
				{
					Debug.Log("Error loading prefab: " + prefabName + " (" + assetPath + ")");
					continue;
				}

				string safeName = MakeUniqueName(SanitizeFileName(prefabName), usedNames);
				JObject doc = BuildDocument(
					"part",
					false,
					BuildPartRecord(part, part, part.name),
					generatedAtUtc,
					new JObject
					{
						["prefabsFolder"] = Str(prefabsFolder),
						["filenameExpression"] = Str(filenameExpression)
					});

				WriteTextFile(Path.Combine(partsFolder, safeName + JsonExtension), SerializeJson(doc));
			}
		}
		else
		{
			var parts = new JArray();

			foreach (string prefabFile in prefabFiles)
			{
				string assetPath = NormalizeAssetPath(prefabFile);
				string prefabName = Path.GetFileNameWithoutExtension(prefabFile);
				GameObject part = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
				if (part == null)
				{
					Debug.Log("Error loading prefab: " + prefabName + " (" + assetPath + ")");
					continue;
				}

				parts.Add(BuildPartRecord(part, part, part.name));
			}

			JObject doc = BuildDocument(
				"part",
				true,
				parts,
				generatedAtUtc,
				new JObject
				{
					["prefabsFolder"] = Str(prefabsFolder),
					["filenameExpression"] = Str(filenameExpression)
				});

			WriteTextFile(Path.Combine(dataPath, "Parts.json"), SerializeJson(doc));
		}
	}

	private static string MakeUniqueName(string baseName, HashSet<string> usedNames)
	{
		string finalName = baseName;
		int suffix = 2;
		while (!usedNames.Add(finalName))
		{
			finalName = baseName + "_" + suffix;
			suffix++;
		}
		return finalName;
	}

	private static JObject BuildPartRecord(GameObject part, GameObject root, string path)
	{
		var children = new JArray();
		foreach (Transform child in part.transform)
		{
			if (child != null)
			{
				children.Add(BuildPartRecord(child.gameObject, root, path + "/" + child.gameObject.name));
			}
		}

		JObject prefabSource = null;
#if UNITY_EDITOR
		GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(part) as GameObject;
		if (prefab != null)
		{
			prefabSource = BuildObjectReference(prefab, null);
		}
#endif

		return new JObject
		{
			["name"] = Str(part.name),
			["path"] = Str(path),
			["tag"] = Str(part.tag),
			["layer"] = Num(part.layer),
			["activeSelf"] = Bool(part.activeSelf),
			["activeInHierarchy"] = Bool(part.activeInHierarchy),
			["transform"] = BuildTransformRecord(part.transform, root.transform),
			["prefabSource"] = (JToken)prefabSource ?? JValue.CreateNull(),
			["components"] = BuildPartComponents(part, root),
			["children"] = children
		};
	}

	private static JObject BuildTransformRecord(Transform transform, Transform root)
	{
		Vector3 absolutePosition = transform.position - root.position;
		Quaternion absoluteRotation = Quaternion.Inverse(root.rotation) * transform.rotation;

		Vector3 rootScale = root.lossyScale;
		Vector3 lossyScale = transform.lossyScale;
		Vector3 absoluteScale = new Vector3(
			rootScale.x == 0f ? 0f : lossyScale.x / rootScale.x,
			rootScale.y == 0f ? 0f : lossyScale.y / rootScale.y,
			rootScale.z == 0f ? 0f : lossyScale.z / rootScale.z);

		return new JObject
		{
			["position"] = Vec3Obj(transform.position),
			["localPosition"] = Vec3Obj(transform.localPosition),
			["rotation"] = QuatObj(transform.rotation),
			["localRotation"] = QuatObj(transform.localRotation),
			["lossyScale"] = Vec3Obj(transform.lossyScale),
			["localScale"] = Vec3Obj(transform.localScale),
			["relativeToRoot"] = new JObject
			{
				["absolutePosition"] = Vec3Obj(absolutePosition),
				["absoluteRotation"] = QuatObj(absoluteRotation),
				["absoluteScale"] = Vec3Obj(absoluteScale)
			}
		};
	}

	private static JObject BuildPartComponents(GameObject part, GameObject root)
	{
		var components = new JObject();

		MonoBehaviour[] scripts = part.GetComponents<MonoBehaviour>();
		if (scripts != null && scripts.Length > 0)
		{
			var scriptArray = new JArray();
			foreach (MonoBehaviour script in scripts)
			{
				if (script == null)
				{
					continue;
				}

				scriptArray.Add(new JObject
				{
					["type"] = Str(script.GetType().Name),
					["name"] = Str(script.name)
				});
			}

			if (scriptArray.Count > 0)
			{
				components["scripts"] = scriptArray;
			}
		}

		BoxCollider boxCollider = part.GetComponent<BoxCollider>();
		if (boxCollider != null)
		{
			components["boxCollider"] = BuildBoxColliderRecord(part, root, boxCollider);
		}

		CapsuleCollider capsuleCollider = part.GetComponent<CapsuleCollider>();
		if (capsuleCollider != null)
		{
			components["capsuleCollider"] = BuildCapsuleColliderRecord(part, root, capsuleCollider);
		}

		SphereCollider sphereCollider = part.GetComponent<SphereCollider>();
		if (sphereCollider != null)
		{
			components["sphereCollider"] = BuildSphereColliderRecord(part, root, sphereCollider);
		}

		Sprite spriteComponent = part.GetComponent<Sprite>();
		if (spriteComponent != null)
		{
			JObject spriteRecord = new JObject
			{
				["name"] = Str(spriteComponent.name),
				["id"] = Str(spriteComponent.Id),
				["scale"] = Vec2Obj(new Vector2(spriteComponent.m_scaleX, spriteComponent.m_scaleY)),
				["pivot"] = Vec2Obj(new Vector2(spriteComponent.m_pivotX, spriteComponent.m_pivotY)),
				["updateCollider"] = Bool(spriteComponent.m_updateCollider),
				["size"] = Vec2Obj(spriteComponent.Size),
				["pixelSize"] = Vec2Obj(spriteComponent.PixelSize),
				["uvRect"] = RectObj(spriteComponent.UVRect),
				["runtimeData"] = JValue.CreateNull()
			};

			try
			{
				SpriteData runtimeData = Singleton<RuntimeSpriteDatabase>.Instance.Find(spriteComponent.Id);
				if (runtimeData != null)
				{
					spriteRecord["runtimeData"] = BuildSpriteDataRecord(runtimeData, spriteComponent.Id, spriteComponent.m_scaleX, spriteComponent.m_scaleY, spriteComponent.m_pivotX, spriteComponent.m_pivotY);
				}
			}
			catch (Exception ex)
			{
				Debug.Log("Runtime sprite lookup failed for " + spriteComponent.Id + ": " + ex.Message);
			}

			components["sprite"] = spriteRecord;
		}

		INSerializedSprite serializedSprite = part.GetComponent<INSerializedSprite>();
		if (serializedSprite != null)
		{
			components["serializedSprite"] = new JObject
			{
				["name"] = Str(serializedSprite.name),
				["spriteName"] = Str(serializedSprite.SpriteName)
			};
		}

		BasePart basePart = part.GetComponent<BasePart>();
		if (basePart != null)
		{
			components["basePart"] = BuildBasePartRecord(basePart);
		}

		return components;
	}

	private static JObject BuildBoxColliderRecord(GameObject part, GameObject root, BoxCollider boxCollider)
	{
		return new JObject
		{
			["name"] = Str(boxCollider.name),
			["center"] = Vec3Obj(boxCollider.center),
			["size"] = Vec3Obj(boxCollider.size),
			["isTrigger"] = Bool(boxCollider.isTrigger),
			["contactOffset"] = Num(boxCollider.contactOffset),
			["materialName"] = boxCollider.sharedMaterial != null ? Str(boxCollider.sharedMaterial.name) : JValue.CreateNull(),
			["material"] = boxCollider.sharedMaterial != null ? BuildPhysicMaterialRecord(boxCollider.sharedMaterial) : JValue.CreateNull(),
			["physxGeometry"] = new JObject
			{
				["centerFromRoot"] = Vec3Obj(boxCollider.center + part.transform.position - root.transform.position),
				["halfExtents"] = Vec3Obj(boxCollider.size / 2.0f)
			}
		};
	}

	private static JObject BuildCapsuleColliderRecord(GameObject part, GameObject root, CapsuleCollider capsuleCollider)
	{
		return new JObject
		{
			["name"] = Str(capsuleCollider.name),
			["center"] = Vec3Obj(capsuleCollider.center),
			["radius"] = Num(capsuleCollider.radius),
			["height"] = Num(capsuleCollider.height),
			["direction"] = Num(capsuleCollider.direction),
			["isTrigger"] = Bool(capsuleCollider.isTrigger),
			["contactOffset"] = Num(capsuleCollider.contactOffset),
			["materialName"] = capsuleCollider.sharedMaterial != null ? Str(capsuleCollider.sharedMaterial.name) : JValue.CreateNull(),
			["material"] = capsuleCollider.sharedMaterial != null ? BuildPhysicMaterialRecord(capsuleCollider.sharedMaterial) : JValue.CreateNull(),
			["physxGeometry"] = new JObject
			{
				["centerFromRoot"] = Vec3Obj(capsuleCollider.center + part.transform.position - root.transform.position),
				["radius"] = Num(capsuleCollider.radius),
				["halfHeight"] = Num(capsuleCollider.height / 2.0f - capsuleCollider.radius)
			}
		};
	}

	private static JObject BuildSphereColliderRecord(GameObject part, GameObject root, SphereCollider sphereCollider)
	{
		return new JObject
		{
			["name"] = Str(sphereCollider.name),
			["center"] = Vec3Obj(sphereCollider.center),
			["radius"] = Num(sphereCollider.radius),
			["isTrigger"] = Bool(sphereCollider.isTrigger),
			["contactOffset"] = Num(sphereCollider.contactOffset),
			["materialName"] = sphereCollider.sharedMaterial != null ? Str(sphereCollider.sharedMaterial.name) : JValue.CreateNull(),
			["material"] = sphereCollider.sharedMaterial != null ? BuildPhysicMaterialRecord(sphereCollider.sharedMaterial) : JValue.CreateNull(),
			["physxGeometry"] = new JObject
			{
				["centerFromRoot"] = Vec3Obj(sphereCollider.center + part.transform.position - root.transform.position),
				["radius"] = Num(sphereCollider.radius)
			}
		};
	}

	private static JObject BuildPhysicMaterialRecord(PhysicMaterial material)
	{
		return new JObject
		{
			["name"] = Str(material.name),
			["bounciness"] = Num(material.bounciness),
			["staticFriction"] = Num(material.staticFriction),
			["dynamicFriction"] = Num(material.dynamicFriction),
			["frictionCombine"] = EnumObj(material.frictionCombine),
			["bounceCombine"] = EnumObj(material.bounceCombine)
		};
	}

	private static JObject BuildBasePartRecord(BasePart basePart)
	{
		JObject virtualValues = new JObject();

		try { virtualValues["jointConnectionStrength"] = EnumObj(basePart.GetJointConnectionStrength()); } catch (Exception ex) { Debug.Log("GetJointConnectionStrength() failed: " + ex.Message); }
		try { virtualValues["hasOnOffToggle"] = Bool(basePart.HasOnOffToggle()); } catch (Exception ex) { Debug.Log("HasOnOffToggle() failed: " + ex.Message); }
		try { virtualValues["isPowered"] = Bool(basePart.IsPowered()); } catch (Exception ex) { Debug.Log("IsPowered() failed: " + ex.Message); }
		try { virtualValues["canEncloseParts"] = Bool(basePart.CanEncloseParts()); } catch (Exception ex) { Debug.Log("CanEncloseParts() failed: " + ex.Message); }
		try { virtualValues["canBeEnclosed"] = Bool(basePart.CanBeEnclosed()); } catch (Exception ex) { Debug.Log("CanBeEnclosed() failed: " + ex.Message); }
		try { virtualValues["jointConnectionType"] = EnumObj(basePart.GetJointConnectionType()); } catch (Exception ex) { Debug.Log("GetJointConnectionType() failed: " + ex.Message); }
		try { virtualValues["isCustomRotated"] = Bool(basePart.IsCustomRotated()); } catch (Exception ex) { Debug.Log("IsCustomRotated() failed: " + ex.Message); }
		try { virtualValues["rotation"] = Num(basePart.GetRotation()); } catch (Exception ex) { Debug.Log("GetRotation() failed: " + ex.Message); }
		try { virtualValues["isTriggerable"] = Bool(basePart.IsTriggerable()); } catch (Exception ex) { Debug.Log("IsTriggerable() failed: " + ex.Message); }

		return new JObject
		{
			["eightWay"] = Bool(basePart.m_eightWay),
			["mass"] = Num(basePart.m_mass),
			["interactiveRadius"] = Num(basePart.m_interactiveRadius),
			["breakVelocity"] = Num(basePart.m_breakVelocity),
			["powerConsumption"] = Num(basePart.m_powerConsumption),
			["enginePower"] = Num(basePart.m_enginePower),
			["zOffset"] = Num(basePart.m_ZOffset),
			["jointType"] = EnumObj(basePart.m_jointType),
			["partTier"] = EnumObj(basePart.m_partTier),
			["partType"] = EnumObj(basePart.m_partType),
			["autoAlignType"] = EnumObj(basePart.m_autoAlign),
			["flipped"] = Bool(basePart.m_flipped),
			["gridMin"] = Int2Obj(basePart.m_gridXmin, basePart.m_gridYmin),
			["gridMax"] = Int2Obj(basePart.m_gridXmax, basePart.m_gridYmax),
			["jointConnectionStrength"] = EnumObj(basePart.m_jointConnectionStrength),
			["jointConnectionType"] = EnumObj(basePart.m_jointConnectionType),
			["jointConnectionDirection"] = EnumObj(basePart.m_jointConnectionDirection),
			["customJointConnectionDirection"] = EnumObj(basePart.m_customJointConnectionDirection),
			["jointPreprocessing"] = Bool(basePart.JointPreprocessing),
			["connectedComponent"] = Num(basePart.ConnectedComponent),
			["windVelocity"] = Vec3Obj(basePart.WindVelocity),
			["strictConnectedComponent"] = Num(basePart.StrictConnectedComponent),
			["generalConnectedComponent"] = Num(basePart.GeneralConnectedComponent),
			["generatorRefCount"] = Num(basePart.GeneratorRefCount),
			["generationLevel"] = Num(basePart.GenerationLevel),
			["generationIndex"] = Num(basePart.GenerationIndex),
			["temperature"] = Num(basePart.Temperature),
			["hasGeneratorRef"] = Bool(basePart.HasGeneratorRef),
			["virtualValues"] = virtualValues
		};
	}

	private static JObject BuildObjectReference(UnityEngine.Object obj, int? referenceIndex = null)
	{
		if (obj == null)
		{
			return null;
		}

		JObject record = new JObject
		{
			["name"] = Str(obj.name),
			["type"] = Str(obj.GetType().FullName),
			["referenceIndex"] = referenceIndex.HasValue ? (JToken)Num(referenceIndex.Value) : JValue.CreateNull()
		};

#if UNITY_EDITOR
		string assetPath = AssetDatabase.GetAssetPath(obj);
		if (!string.IsNullOrEmpty(assetPath))
		{
			record["assetPath"] = Str(assetPath);
		}
#endif

		return record;
	}

	// ---------------------------------------------------------------------
	// SPRITE EXPORT
	// ---------------------------------------------------------------------

	private static void ExtractSpriteData(string generatedAtUtc)
	{
		List<KeyValuePair<string, SpriteData>> sprites = new List<KeyValuePair<string, SpriteData>>();

		try
		{
			var database = Singleton<RuntimeSpriteDatabase>.Instance;
			if (database != null && database.Data != null)
			{
				foreach (KeyValuePair<string, SpriteData> pair in database.Data)
				{
					sprites.Add(pair);
				}
			}
		}
		catch (Exception ex)
		{
			Debug.Log("Sprite database lookup failed: " + ex.Message);
		}

		sprites.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));

		if (splitFiles)
		{
			string spritesFolder = Path.Combine(dataPath, "Sprites");
			if (Directory.Exists(spritesFolder))
			{
				Directory.Delete(spritesFolder, true);
			}
			Directory.CreateDirectory(spritesFolder);

			HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (KeyValuePair<string, SpriteData> pair in sprites)
			{
				string id = pair.Key;
				SpriteData data = pair.Value;
				Debug.Log("Current sprite: " + id);

				string safeName = MakeUniqueName(SanitizeFileName(id), usedNames);
				JObject doc = BuildDocument(
					"sprite",
					false,
					BuildSpriteDataRecord(data, id, 0.4f, 0.4f, 0.0f, 0.0f),
					generatedAtUtc,
					new JObject
					{
						["source"] = Str("Singleton<RuntimeSpriteDatabase>.Instance.Data")
					});

				WriteTextFile(Path.Combine(spritesFolder, safeName + JsonExtension), SerializeJson(doc));
			}
		}
		else
		{
			var spriteRecords = new JArray();

			foreach (KeyValuePair<string, SpriteData> pair in sprites)
			{
				string id = pair.Key;
				SpriteData data = pair.Value;
				Debug.Log("Current sprite: " + id);
				spriteRecords.Add(BuildSpriteDataRecord(data, id, 0.4f, 0.4f, 0.0f, 0.0f));
			}

			JObject doc = BuildDocument(
				"sprite",
				true,
				spriteRecords,
				generatedAtUtc,
				new JObject
				{
					["source"] = Str("Singleton<RuntimeSpriteDatabase>.Instance.Data")
				});

			WriteTextFile(Path.Combine(dataPath, "Sprites.json"), SerializeJson(doc));
		}
	}

	private static JObject BuildSpriteDataRecord(SpriteData data, string id, float m_scaleX, float m_scaleY, float m_pivotX, float m_pivotY)
	{
		int scaledWidth = (int)(m_scaleX * data.width);
		int scaledHeight = (int)(m_scaleY * data.height);

		int selectionCenterX = data.selectionX + data.selectionWidth / 2;
		int selectionCenterY = data.selectionY + data.selectionHeight / 2;

		int uvCenterX = data.UVx + data.width / 2;
		int uvCenterY = data.UVy + data.height / 2;

		int centerDiffX = selectionCenterX - uvCenterX;
		int centerDiffY = selectionCenterY - uvCenterY;

		int transformedPivotX = (int)(m_scaleX * (centerDiffX + data.pivotX + m_pivotX));
		int transformedPivotY = (int)(m_scaleY * (centerDiffY + data.pivotY + m_pivotY));

		float raylibWidth = scaledWidth * 10f / 768f;
		float raylibHeight = scaledHeight * 10f / 768f;

		float destX = -2f * transformedPivotX * 10f / 768f;
		float destY = -2f * transformedPivotY * 10f / 768f;

		Vector3 v0 = new Vector3(destX - raylibWidth / 2f, destY - raylibHeight / 2f, 0f);
		Vector3 v1 = new Vector3(destX - raylibWidth / 2f, destY + raylibHeight / 2f, 0f);
		Vector3 v2 = new Vector3(destX + raylibWidth / 2f, destY + raylibHeight / 2f, 0f);
		Vector3 v3 = new Vector3(destX + raylibWidth / 2f, destY - raylibHeight / 2f, 0f);

		Vector2 origin = new Vector2(-v0.x, v2.y);

		Rect uvPixels = new Rect(
			data.uv.x * 2048f,
			2048f - data.uv.y * 2048f - data.uv.height * 2048f,
			data.uv.width * 2048f,
			data.uv.height * 2048f);

		return new JObject
		{
			["id"] = Str(id),
			["dataId"] = Str(data.id),
			["name"] = Str(data.name),
			["materialId"] = Str(data.materialId),
			["selection"] = IntRectObj(data.selectionX, data.selectionY, data.selectionWidth, data.selectionHeight),
			["pivot"] = Int2Obj(data.pivotX, data.pivotY),
			["uvPosition"] = Int2Obj(data.UVx, data.UVy),
			["size"] = Int2Obj(data.width, data.height),
			["subdivisions"] = Num(data.subdivisions),
			["opaqueBorderPixels"] = Num(data.opaqueBorderPixels),
			["atlasMaterialPath"] = Str(data.atlasMaterialPath),
			["uvRect"] = RectObj(data.uv),
			["context"] = new JObject
			{
				["scale"] = Vec2Obj(new Vector2(m_scaleX, m_scaleY)),
				["pivot"] = Vec2Obj(new Vector2(m_pivotX, m_pivotY))
			},
			["computed"] = new JObject
			{
				["scaledSize"] = Int2Obj(scaledWidth, scaledHeight),
				["selectionCenter"] = Int2Obj(selectionCenterX, selectionCenterY),
				["uvCenter"] = Int2Obj(uvCenterX, uvCenterY),
				["centerDifference"] = Int2Obj(centerDiffX, centerDiffY),
				["transformedPivot"] = Int2Obj(transformedPivotX, transformedPivotY),
				["meshVertices"] = new JArray
				{
					Vec3Obj(v0),
					Vec3Obj(v1),
					Vec3Obj(v2),
					Vec3Obj(v3)
				},
				["raylibTexture"] = new JObject
				{
					["sourceRect"] = RectObj(data.uv),
					["sourceRectPixels"] = RectObj(uvPixels),
					["destRect"] = RectObj(new Rect(0f, 0f, raylibWidth, raylibHeight)),
					["origin"] = Vec2Obj(origin)
				}
			}
		};
	}

	// ---------------------------------------------------------------------
	// LEVEL EXPORT
	// ---------------------------------------------------------------------

	private enum DataType : byte
	{
		None = 0,
		Terrain = 1,
		PrefabOverrides = 2
	}

	private static void ExtractLevelData(string generatedAtUtc)
	{
		List<string> levelFiles = DiscoverLevelBytesFiles();
		if (levelFiles.Count == 0)
		{
			Debug.Log("No level .bytes files found.");
			return;
		}

		if (splitFiles)
		{
			string levelsFolder = Path.Combine(dataPath, "Levels");
			if (Directory.Exists(levelsFolder))
			{
				Directory.Delete(levelsFolder, true);
			}
			Directory.CreateDirectory(levelsFolder);

			HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (string file in levelFiles)
			{
				JObject doc = ParseLevelFile(file, generatedAtUtc);
				string baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(file));
				string safeName = MakeUniqueName(baseName, usedNames);
				WriteTextFile(Path.Combine(levelsFolder, safeName + JsonExtension), SerializeJson(doc));
			}
		}
		else
		{
			var levels = new JArray();
			foreach (string file in levelFiles)
			{
				levels.Add(ParseLevelFile(file, generatedAtUtc));
			}

			JObject doc = BuildDocument(
				"level",
				true,
				levels,
				generatedAtUtc,
				new JObject
				{
					["levelBytesRootFolder"] = Str(levelBytesRootFolder),
					["levelBytesFileExpression"] = Str(levelBytesFileExpression)
				});

			WriteTextFile(Path.Combine(dataPath, "Levels.json"), SerializeJson(doc));
		}
	}

	private static List<string> DiscoverLevelBytesFiles()
	{
		var files = new List<string>();

		if (Directory.Exists(levelBytesRootFolder))
		{
			string[] found = Directory.GetFiles(levelBytesRootFolder, levelBytesFileExpression, SearchOption.AllDirectories);
			files.AddRange(found);
		}

		files.Sort(StringComparer.OrdinalIgnoreCase);
		return files;
	}

	private static JObject ParseLevelFile(string filePath, string generatedAtUtc)
	{
		using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
		using (BinaryReader reader = new BinaryReader(fs))
		{
			int rootCount = reader.ReadInt32();
			var objects = new JArray();
			for (int i = 0; i < rootCount; i++)
			{
				objects.Add(ReadLevelObject(reader, filePath, "root[" + i + "]"));
			}

			JObject doc = BuildDocument(
				"level",
				false,
				new JObject
				{
					["sourceFile"] = Str(filePath),
					["rootCount"] = Num(rootCount),
					["objects"] = objects
				},
				generatedAtUtc,
				new JObject
				{
					["levelBytesFile"] = Str(filePath)
				});

			return doc;
		}
	}

	private static JObject ReadLevelObject(BinaryReader reader, string filePath, string path)
	{
		short typeOrChildCount = reader.ReadInt16();

		if (typeOrChildCount == 0)
		{
			return ReadPrefabInstance(reader, filePath, path);
		}

		return ReadParentObject(reader, filePath, path, typeOrChildCount);
	}

	private static JObject ReadPrefabInstance(BinaryReader reader, string filePath, string path)
	{
		string objectName = reader.ReadString();
		short prefabIndex = reader.ReadInt16();
		Vector3 position = ReadVector3(reader);
		Vector3 euler = ReadVector3(reader);
		Vector3 localScale = ReadVector3(reader);

		JToken data = ReadDataBlock(reader, filePath, path + "/data");

		return new JObject
		{
			["kind"] = Str("prefabInstance"),
			["name"] = Str(objectName),
			["path"] = Str(path),
			["prefabIndex"] = Num(prefabIndex),
			["transform"] = new JObject
			{
				["position"] = Vec3Obj(position),
				["euler"] = Vec3Obj(euler),
				["localScale"] = Vec3Obj(localScale)
			},
			["data"] = data
		};
	}

	private static JObject ReadParentObject(BinaryReader reader, string filePath, string path, short childCount)
	{
		string objectName = reader.ReadString();
		Vector3 position = ReadVector3(reader);

		var children = new JArray();
		for (int i = 0; i < childCount; i++)
		{
			children.Add(ReadLevelObject(reader, filePath, path + "/" + objectName + "/child[" + i + "]"));
		}

		return new JObject
		{
			["kind"] = Str("parentObject"),
			["name"] = Str(objectName),
			["path"] = Str(path),
			["childCount"] = Num(childCount),
			["position"] = Vec3Obj(position),
			["children"] = children
		};
	}

	private static JToken ReadDataBlock(BinaryReader reader, string filePath, string path)
	{
		DataType dataType = (DataType)reader.ReadByte();

		switch (dataType)
		{
			case DataType.Terrain:
				return ReadTerrainBlock(reader, filePath, path);
			case DataType.PrefabOverrides:
				return ReadPrefabOverridesBlock(reader, path);
			default:
				return new JObject
				{
					["type"] = Str("none")
				};
		}
	}

	private static JObject ReadPrefabOverridesBlock(BinaryReader reader, string path)
	{
		int length = reader.ReadInt32();
		byte[] buffer = reader.ReadBytes(length);

		string utf8Text = null;
		try
		{
			utf8Text = Encoding.UTF8.GetString(buffer);
		}
		catch
		{
			utf8Text = null;
		}

		return new JObject
		{
			["type"] = Str("prefabOverrides"),
			["byteLength"] = Num(length),
			["rawBase64"] = Str(Convert.ToBase64String(buffer)),
			["utf8Text"] = utf8Text != null ? Str(utf8Text) : JValue.CreateNull(),
			["path"] = Str(path)
		};
	}

	private static JObject ReadTerrainBlock(BinaryReader reader, string filePath, string path)
	{
		float fillTextureTileOffsetX = reader.ReadSingle();
		float fillTextureTileOffsetY = reader.ReadSingle();

		JObject fillMesh = ReadMeshBlock(reader, fillMesh: true);
		Color fillColor = ReadColor(reader);
		int fillTextureReferenceIndex = reader.ReadInt32();

		JObject curveMesh = ReadMeshBlock(reader, fillMesh: false);

		int curveTextureCount = reader.ReadInt32();
		var curveTextures = new JArray();
		for (int i = 0; i < curveTextureCount; i++)
		{
			int textureReferenceIndex = reader.ReadInt32();
			Vector2 size = ReadVector2(reader);
			bool fixedAngle = reader.ReadBoolean();
			float fadeThreshold = reader.ReadSingle();

			curveTextures.Add(new JObject
			{
				["referenceIndex"] = Num(textureReferenceIndex),
				["size"] = Vec2Obj(size),
				["fixedAngle"] = Bool(fixedAngle),
				["fadeThreshold"] = Num(fadeThreshold)
			});
		}

		int controlTextureMarker = reader.ReadInt32();
		JObject controlTexture = null;
		if (controlTextureMarker > 0)
		{
			int byteCount = reader.ReadInt32();
			byte[] bytes = reader.ReadBytes(byteCount);
			controlTexture = new JObject
			{
				["present"] = Bool(true),
				["marker"] = Num(controlTextureMarker),
				["byteLength"] = Num(byteCount),
				["rawBase64"] = Str(Convert.ToBase64String(bytes))
			};
		}
		else
		{
			controlTexture = new JObject
			{
				["present"] = Bool(false),
				["marker"] = Num(controlTextureMarker)
			};
		}

		bool hasCollider = reader.ReadBoolean();
		JObject colliderMesh = hasCollider ? BuildColliderFromFillMesh(fillMesh) : null;

		return new JObject
		{
			["type"] = Str("terrain"),
			["path"] = Str(path),
			["fillTextureTileOffset"] = Vec2Obj(new Vector2(fillTextureTileOffsetX, fillTextureTileOffsetY)),
			["fillMesh"] = fillMesh,
			["fillColor"] = ColorObj(fillColor),
			["fillTextureReferenceIndex"] = Num(fillTextureReferenceIndex),
			["curveMesh"] = curveMesh,
			["curveTextures"] = curveTextures,
			["controlTexture"] = controlTexture,
			["hasCollider"] = Bool(hasCollider),
			["colliderMesh"] = colliderMesh
		};
	}

	private static JObject BuildColliderFromFillMesh(JObject fillMesh)
	{
		JArray vertices = fillMesh["vertices"] as JArray;
		if (vertices == null)
		{
			return null;
		}

		var source = new List<Vector2>();
		foreach (JToken token in vertices)
		{
			if (token is JObject v)
			{
				float x = v["x"] != null ? v["x"].Value<float>() : 0f;
				float y = v["y"] != null ? v["y"].Value<float>() : 0f;
				source.Add(new Vector2(x, y));
			}
		}

		var extrudedVertices = new JArray();
		var triangles = new JArray();

		for (int i = 0; i < source.Count; i++)
		{
			int num = 2 * i;

			extrudedVertices.Add(new JObject
			{
				["x"] = Num(source[i].x),
				["y"] = Num(source[i].y),
				["z"] = Num(-0.5f * e2dConstants.COLLISION_MESH_Z_DEPTH)
			});
			extrudedVertices.Add(new JObject
			{
				["x"] = Num(source[i].x),
				["y"] = Num(source[i].y),
				["z"] = Num(0.5f * e2dConstants.COLLISION_MESH_Z_DEPTH)
			});

			int triBase = 6 * i;
			triangles.Add(Num(num % (2 * source.Count)));
			triangles.Add(Num((num + 1) % (2 * source.Count)));
			triangles.Add(Num((num + 2) % (2 * source.Count)));
			triangles.Add(Num((num + 2) % (2 * source.Count)));
			triangles.Add(Num((num + 1) % (2 * source.Count)));
			triangles.Add(Num((num + 3) % (2 * source.Count)));
		}

		return new JObject
		{
			["vertices"] = extrudedVertices,
			["triangles"] = triangles
		};
	}

	private static JObject ReadMeshBlock(BinaryReader reader, bool fillMesh)
	{
		int vertexCount = reader.ReadInt32();
		var vertices = new JArray();

		if (fillMesh)
		{
			for (int i = 0; i < vertexCount; i++)
			{
				Vector2 v = ReadVector2(reader);
				vertices.Add(new JObject
				{
					["x"] = Num(v.x),
					["y"] = Num(v.y),
					["z"] = Num(0f)
				});
			}
		}
		else
		{
			for (int i = 0; i < vertexCount; i++)
			{
				Vector2 v = ReadVector2(reader);
				vertices.Add(new JObject
				{
					["x"] = Num(v.x),
					["y"] = Num(v.y),
					["z"] = Num(-0.01f)
				});
			}
		}

		int triangleCount = reader.ReadInt32();
		var triangles = new JArray();
		for (int i = 0; i < triangleCount; i++)
		{
			triangles.Add(Num(reader.ReadInt16()));
		}

		JObject mesh = new JObject
		{
			["vertexCount"] = Num(vertexCount),
			["triangleCount"] = Num(triangleCount),
			["vertices"] = vertices,
			["triangles"] = triangles
		};

		if (!fillMesh)
		{
			mesh["colors"] = BuildCurveMeshColors(vertexCount);
			mesh["computedUv"] = BuildCurveMeshComputedUv(vertexCount);
		}

		return mesh;
	}

	private static JArray BuildCurveMeshColors(int vertexCount)
	{
		var colors = new JArray();
		for (int i = 0; i < vertexCount; i++)
		{
			colors.Add(new JObject
			{
				["r"] = Num((i + 1) % 2),
				["g"] = Num(0),
				["b"] = Num(0),
				["a"] = Num(0)
			});
		}
		return colors;
	}

	private static JArray BuildCurveMeshComputedUv(int vertexCount)
	{
		var uv = new JArray();
		float runningDistance = 0f;

		for (int i = 0; i < vertexCount; i++)
		{
			int pairIndex = i / 2;
			if (i % 2 == 0 && i >= 2)
			{
				// The original loader increments distance per segment pair.
				runningDistance += 0f;
			}

			uv.Add(new JObject
			{
				["x"] = Num(runningDistance),
				["y"] = Num(pairIndex)
			});
		}

		return uv;
	}

	private static Vector2 ReadVector2(BinaryReader reader)
	{
		return new Vector2(reader.ReadSingle(), reader.ReadSingle());
	}

	private static Vector3 ReadVector3(BinaryReader reader)
	{
		return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
	}

	private static Color ReadColor(BinaryReader reader)
	{
		uint packed = reader.ReadUInt32();
		return new Color(
			((packed >> 24) & 0xFF) / 255f,
			((packed >> 16) & 0xFF) / 255f,
			((packed >> 8) & 0xFF) / 255f,
			(packed & 0xFF) / 255f);
	}
}