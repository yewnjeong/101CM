/*
 * Copyright 2024 Oleg Dzhuraev <godlikeaurora@gmail.com>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * Modified for 101CM: URP Lit support, color-only materials, multi-submesh meshes,
 * static object auto-detection, materials that can't be atlased kept as submeshes.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace InsaneOne.DevTools
{
	public sealed class MeshBakerWindow : EditorWindow
	{
		enum TextureType { Albedo, Normal, Metallic, AO, Emission }

		const string LitShaderName = "Universal Render Pipeline/Lit";

		static readonly int baseMapId = Shader.PropertyToID("_BaseMap");
		static readonly int baseColorId = Shader.PropertyToID("_BaseColor");
		static readonly int metallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
		static readonly int metallicId = Shader.PropertyToID("_Metallic");
		static readonly int smoothnessId = Shader.PropertyToID("_Smoothness");
		static readonly int smoothnessChannelId = Shader.PropertyToID("_SmoothnessTextureChannel");
		static readonly int bumpMapId = Shader.PropertyToID("_BumpMap");
		static readonly int aoMapId = Shader.PropertyToID("_OcclusionMap");
		static readonly int aoStrengthId = Shader.PropertyToID("_OcclusionStrength");
		static readonly int emissionMapId = Shader.PropertyToID("_EmissionMap");
		static readonly int emissionColorId = Shader.PropertyToID("_EmissionColor");
		static readonly int surfaceId = Shader.PropertyToID("_Surface");
		static readonly int alphaClipId = Shader.PropertyToID("_AlphaClip");
		static readonly int workflowModeId = Shader.PropertyToID("_WorkflowMode");

		static readonly List<int> supportedAtlasSizes = new List<int> { 512, 1024, 2048, 4096, 8192 };

		/// <summary> One atlas slot: a unique URP Lit material baked to textures. </summary>
		sealed class AtlasEntry
		{
			public Material Material;
			public bool HasTexture;
			public Rect Rect;
		}

		/// <summary> One part of a source mesh which goes to one submesh of the combined mesh. </summary>
		struct Part
		{
			public MeshRenderer Renderer;
			public Mesh Mesh;
			public int SubMesh;
			public Material Material;
			public AtlasEntry Atlas; // null = kept with its own material
		}

		int selectedAtlasSizeId = 3;
		int maxEntrySize = 1024;
		int swatchSize = 32;

		bool requireStaticFlag;
		bool includeCustomShaders;
		bool disableOriginalRenderers = true;
		bool generateLightmapUvs;
		bool saveMesh = true;

		string passiveScripts = "ZoneVolume, PointMarker, HazardMarker, HideoutMarker, FoodSpawn, SupplyPoint";
		string targetName = "Combined_Static";
		string bakePostfix = "00";
		string bakeFolder = "_Game/Baked";

		StaticObjectFinder.Result lastFind;
		Vector2 scroll;

		[MenuItem("Tools/Mesh Baker")]
		public static void ShowWindow()
		{
			var wnd = GetWindow<MeshBakerWindow>();
			wnd.titleContent = new GUIContent("Mesh Baker");
			wnd.minSize = new Vector2(420, 520);
		}

		/// <summary> One click: find static objects in the active scene and bake them with default settings. </summary>
		[MenuItem("Tools/Mesh Baker - Bake Static Objects")]
		public static void BakeStaticObjectsInScene()
		{
			var baker = CreateInstance<MeshBakerWindow>();

			try
			{
				baker.SelectStaticObjects();
				baker.Bake(GetSelectedRenderers());
			}
			finally
			{
				DestroyImmediate(baker);
			}
		}

		void OnSelectionChange() => Repaint();

		public void OnGUI()
		{
			scroll = EditorGUILayout.BeginScrollView(scroll);

			EditorGUILayout.HelpBox("Combines static meshes into one mesh. URP Lit materials (textured or color-only) are packed into one atlas material; other materials are kept as extra submeshes of the same mesh.", MessageType.Info);

			GUILayout.Label("1. Find static objects", EditorStyles.boldLabel);
			requireStaticFlag = EditorGUILayout.Toggle(new GUIContent("Require Batching Static", "Only take objects marked Batching Static. Off = detect by components (no Rigidbody, Animator, gameplay scripts, script references)."), requireStaticFlag);
			includeCustomShaders = EditorGUILayout.Toggle(new GUIContent("Include custom shaders", "Custom shaders can depend on object space / per-object data, which changes after combining. Off by default."), includeCustomShaders);

			passiveScripts = EditorGUILayout.TextField(new GUIContent("Passive marker scripts", "Comma separated script names which never move or change their objects (data / gizmo markers). Objects under any other gameplay script are treated as moving."), passiveScripts);

			if (GUILayout.Button("Select static objects in scene"))
				SelectStaticObjects();

			if (lastFind != null)
			{
				var text = $"Found <b>{lastFind.Renderers.Count}</b> static renderers.\nSkipped:";
				foreach (var kv in lastFind.SkipReasons.OrderByDescending(k => k.Value))
					text += $"\n  {kv.Value} – {kv.Key}";
				GUILayout.Label(text, BakerStyles.RichLabelStyle);
			}

			GUILayout.Label("2. Bake settings", EditorStyles.boldLabel);

			GUILayout.BeginHorizontal();
			GUILayout.Label($"Max atlas size: <b>{GetAtlasSize()}</b>", BakerStyles.RichLabelStyle);
			if (GUILayout.Button("-"))
				ChangeAtlasSize(false);
			if (GUILayout.Button("+"))
				ChangeAtlasSize(true);
			GUILayout.EndHorizontal();

			maxEntrySize = EditorGUILayout.IntPopup("Max size per texture", maxEntrySize, new[] { "256", "512", "1024", "2048" }, new[] { 256, 512, 1024, 2048 });
			swatchSize = EditorGUILayout.IntPopup("Color-only material swatch", swatchSize, new[] { "8", "16", "32", "64" }, new[] { 8, 16, 32, 64 });

			targetName = EditorGUILayout.TextField("Combined object name", targetName);
			disableOriginalRenderers = EditorGUILayout.Toggle(new GUIContent("Disable original renderers", "Only MeshRenderer is disabled; colliders and scripts on originals keep working."), disableOriginalRenderers);
			generateLightmapUvs = EditorGUILayout.Toggle(new GUIContent("Generate lightmap UVs", "Slow on big meshes. Needed only for baked lightmaps."), generateLightmapUvs);
			saveMesh = EditorGUILayout.Toggle(new GUIContent("Save mesh to assets", "Mesh can be stored directly on scene, or, for better management and scene size reduce, it can be stored in assets."), saveMesh);

			bakeFolder = EditorGUILayout.TextField("Bake folder (in Assets)", bakeFolder).Trim('/', '\\');
			bakePostfix = SanitizeFilename(EditorGUILayout.TextField("Baked files postfix", bakePostfix));

			GUILayout.Space(10);

			var selected = GetSelectedRenderers();
			var prevEnabled = GUI.enabled;

			if (selected.Count < 2)
			{
				EditorGUILayout.HelpBox("Select at least 2 meshes (or use the button above) to begin bake process.", MessageType.Info);
				GUI.enabled = false;
			}
			else
			{
				EditorGUILayout.HelpBox($"{selected.Count} renderers selected. Bake process can take some time.", MessageType.Info);
			}

			if (GUILayout.Button("Bake"))
				Bake(selected);

			GUI.enabled = prevEnabled;

			EditorGUILayout.EndScrollView();
		}

		void ChangeAtlasSize(bool increase)
		{
			selectedAtlasSizeId += increase ? 1 : -1;

			if (selectedAtlasSizeId < 0)
				selectedAtlasSizeId = supportedAtlasSizes.Count - 1;
			else if (selectedAtlasSizeId >= supportedAtlasSizes.Count)
				selectedAtlasSizeId = 0;
		}

		int GetAtlasSize() => supportedAtlasSizes[selectedAtlasSizeId];

		string SanitizeFilename(string input)
		{
			var invalids = Path.GetInvalidFileNameChars();
			return string.Join("_", input.Split(invalids, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
		}

		string GetPath() => $"Assets/{bakeFolder}/";

		HashSet<string> GetPassiveScripts() => new HashSet<string>(passiveScripts.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0));

		void SelectStaticObjects()
		{
			lastFind = StaticObjectFinder.Find(requireStaticFlag, includeCustomShaders, GetPath(), GetPassiveScripts());
			Selection.objects = lastFind.Renderers.Select(r => (UnityEngine.Object)r.gameObject).ToArray();
		}

		static List<MeshRenderer> GetSelectedRenderers()
		{
			return Selection.gameObjects
				.SelectMany(go => go.GetComponentsInChildren<MeshRenderer>())
				.Distinct()
				.ToList();
		}

		void Bake(List<MeshRenderer> selected)
		{
			// Manual selection may contain parents of moving objects, so always re-validate.
			var filtered = StaticObjectFinder.Filter(selected, includeCustomShaders, GetPath(), GetPassiveScripts());
			var renderers = filtered.Renderers;

			if (renderers.Count < 2)
			{
				EditorUtility.DisplayDialog("Mesh Baker", "Less than 2 of the selected renderers are static and supported. Nothing to bake.", "OK");
				return;
			}

			if (filtered.SkipReasons.Count > 0)
				Debug.LogWarning($"[Mesh Baker] Skipped {filtered.SkipReasons.Values.Sum()} selected renderers which move or are not supported.");

			try
			{
				Utils.EnsureFolder(GetPath().TrimEnd('/'));

				EditorUtility.DisplayProgressBar("Mesh Baker", "Collecting meshes", 0.1f);
				var parts = CollectParts(renderers, out var atlasEntries);

				var atlasMaterial = atlasEntries.Count > 0 ? BakeAtlas(atlasEntries) : null;

				// Shadow casting is per renderer, not per submesh: e.g. the ceiling must not start casting shadows
				// into the whole level. So one combined object per shadow mode, all sharing the same atlas.
				var shadowGroups = parts.GroupBy(p => (p.Renderer.shadowCastingMode, p.Renderer.receiveShadows)).ToList();
				var targets = new List<GameObject>();

				foreach (var group in shadowGroups)
				{
					EditorUtility.DisplayProgressBar("Mesh Baker", "Combining meshes", 0.7f);

					var suffix = shadowGroups.Count > 1 && group.Key.shadowCastingMode == ShadowCastingMode.Off ? "_NoShadows" : "";
					suffix += shadowGroups.Count > 1 && !group.Key.receiveShadows ? "_NoReceive" : "";

					var groupParts = group.ToList();
					var target = CreateTarget(groupParts.Select(p => p.Renderer).Distinct().ToList(), suffix);
					var materials = new List<Material>();
					var mesh = CombineParts(groupParts, target.transform, atlasMaterial, materials);

					if (generateLightmapUvs)
					{
						EditorUtility.DisplayProgressBar("Mesh Baker", "Generating lightmap UVs", 0.85f);
						Unwrapping.GenerateSecondaryUVSet(mesh);
					}

					if (saveMesh)
						AssetDatabase.CreateAsset(mesh, $"{GetPath()}BakedMesh{bakePostfix}{suffix}.asset");

					target.GetComponent<MeshFilter>().sharedMesh = mesh;
					target.GetComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
					targets.Add(target);

					Debug.Log($"[Mesh Baker] '{target.name}': {mesh.vertexCount} vertices, {mesh.subMeshCount} submeshes, shadows {group.Key.shadowCastingMode}.", target);
				}

				if (disableOriginalRenderers)
				{
					foreach (var renderer in renderers)
					{
						Undo.RecordObject(renderer, "Disable baked renderer");
						renderer.enabled = false;
					}
				}

				AssetDatabase.SaveAssets();
				EditorSceneManager_MarkDirty(targets[0].scene);
				Selection.objects = targets.Cast<UnityEngine.Object>().ToArray();

				Debug.Log($"[Mesh Baker] Combined {renderers.Count} renderers into {targets.Count} object(s), {atlasEntries.Count} materials atlased.");
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}

		static void EditorSceneManager_MarkDirty(Scene scene) => UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

		List<Part> CollectParts(List<MeshRenderer> renderers, out List<AtlasEntry> atlasEntries)
		{
			var parts = new List<Part>();
			var entriesByMaterial = new Dictionary<Material, AtlasEntry>();
			var uvCheckCache = new Dictionary<(Mesh, int), bool>();

			foreach (var renderer in renderers)
			{
				var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
				var mats = renderer.sharedMaterials;

				for (var sub = 0; sub < mesh.subMeshCount; sub++)
				{
					if (mesh.GetTopology(sub) != MeshTopology.Triangles)
						continue;

					var material = mats[Mathf.Min(sub, mats.Length - 1)];
					var part = new Part { Renderer = renderer, Mesh = mesh, SubMesh = sub, Material = material };

					if (!uvCheckCache.TryGetValue((mesh, sub), out var uvsInRange))
						uvCheckCache[(mesh, sub)] = uvsInRange = AreUvsInRange(mesh, sub);

					if (CanAtlas(material, uvsInRange))
					{
						if (!entriesByMaterial.TryGetValue(material, out var entry))
						{
							entry = new AtlasEntry { Material = material, HasTexture = material.GetTexture(baseMapId) };
							entriesByMaterial.Add(material, entry);
						}

						part.Atlas = entry;
					}

					parts.Add(part);
				}
			}

			atlasEntries = entriesByMaterial.Values.ToList();
			return parts;
		}

		static bool CanAtlas(Material material, bool uvsInRange)
		{
			if (material.shader.name != LitShaderName)
				return false;

			if (material.GetFloat(surfaceId) > 0.5f || material.GetFloat(alphaClipId) > 0.5f)
				return false; // transparent / cutout need their own material

			if (material.HasProperty(workflowModeId) && material.GetFloat(workflowModeId) < 0.5f)
				return false; // specular workflow

			if (material.IsKeywordEnabled("_DETAIL_MULX2") || material.IsKeywordEnabled("_PARALLAXMAP"))
				return false;

			var hasTexture = material.GetTexture(baseMapId);
			var st = material.GetVector("_BaseMap_ST");

			// Tiled textures can't live in an atlas. Color-only materials ignore UVs, so they are always fine.
			if (hasTexture && (st != new Vector4(1, 1, 0, 0) || !uvsInRange))
				return false;

			return true;
		}

		static bool AreUvsInRange(Mesh mesh, int subMesh)
		{
			var uvs = new List<Vector2>();
			mesh.GetUVs(0, uvs);

			if (uvs.Count == 0)
				return false;

			const float eps = 0.001f;
			foreach (var index in mesh.GetIndices(subMesh))
			{
				var uv = uvs[index];
				if (uv.x < -eps || uv.y < -eps || uv.x > 1 + eps || uv.y > 1 + eps)
					return false;
			}

			return true;
		}

		Material BakeAtlas(List<AtlasEntry> entries)
		{
			var count = entries.Count;
			var albedos = new Texture2D[count];
			var metallics = new Texture2D[count];
			var bakeNormals = entries.Any(e => e.Material.GetTexture(bumpMapId));
			var bakeAo = entries.Any(e => e.Material.GetTexture(aoMapId));
			var bakeEmission = entries.Any(e => IsEmissive(e.Material));
			var normals = bakeNormals ? new Texture2D[count] : null;
			var aos = bakeAo ? new Texture2D[count] : null;
			var emissions = bakeEmission ? new Texture2D[count] : null;

			// Emission is HDR, atlas is 8-bit: store color / maxIntensity and put maxIntensity into the material.
			// HDR material colors reach the shader as stored (already linear), unlike LDR colors like _BaseColor.
			var maxEmission = bakeEmission ? entries.Where(e => IsEmissive(e.Material)).Max(e => e.Material.GetColor(emissionColorId).maxColorComponent) : 0f;

			for (var q = 0; q < count; q++)
			{
				EditorUtility.DisplayProgressBar("Mesh Baker", $"Reading textures ({q + 1}/{count})", 0.1f + 0.4f * q / count);

				var mat = entries[q].Material;
				var baseMap = mat.GetTexture(baseMapId);
				var baseColor = mat.GetColor(baseColorId);
				int width, height;

				if (baseMap)
				{
					var scale = Mathf.Min(1f, (float)maxEntrySize / Mathf.Max(baseMap.width, baseMap.height));
					width = Mathf.Max(4, Mathf.RoundToInt(baseMap.width * scale));
					height = Mathf.Max(4, Mathf.RoundToInt(baseMap.height * scale));
					albedos[q] = Utils.ReadTexture(baseMap, width, height, false);
					Utils.MultiplyPixels(albedos[q], baseColor);
				}
				else
				{
					width = height = swatchSize;
					albedos[q] = Utils.CreateFilledTexture(width, height, baseColor);
				}

				metallics[q] = BakeMetallic(mat, albedos[q], width, height);

				if (bakeNormals)
				{
					var normal = mat.GetTexture(bumpMapId);
					normals[q] = normal
						? Utils.RestoreNormal(normal, width, height)
						: Utils.CreateFilledTexture(width, height, new Color(0.5f, 0.5f, 1f, 1f), true);
				}

				if (bakeAo)
				{
					var ao = mat.GetTexture(aoMapId);
					if (ao)
					{
						aos[q] = Utils.ReadTexture(ao, width, height, true);
						var strength = mat.GetFloat(aoStrengthId);
						var pixels = aos[q].GetPixels();
						for (var p = 0; p < pixels.Length; p++)
						{
							var v = Mathf.Lerp(1f, pixels[p].g, strength); // URP reads occlusion from G
							pixels[p] = new Color(v, v, v, 1f);
						}
						aos[q].SetPixels(pixels);
						aos[q].Apply();
					}
					else
					{
						aos[q] = Utils.CreateFilledTexture(width, height, Color.white, true);
					}
				}

				if (bakeEmission)
				{
					var emissionLinear = IsEmissive(mat) ? mat.GetColor(emissionColorId) / maxEmission : Color.black;
					var emissionColor = new Color(emissionLinear.r, emissionLinear.g, emissionLinear.b, 1f).gamma;
					var emissionMap = mat.GetTexture(emissionMapId);

					if (IsEmissive(mat) && emissionMap)
					{
						emissions[q] = Utils.ReadTexture(emissionMap, width, height, false);
						Utils.MultiplyPixels(emissions[q], emissionColor);
					}
					else
					{
						emissions[q] = Utils.CreateFilledTexture(width, height, emissionColor);
					}
				}
			}

			EditorUtility.DisplayProgressBar("Mesh Baker", "Packing atlases", 0.55f);

			var material = new Material(Shader.Find(LitShaderName)) { name = $"BakedMaterial{bakePostfix}" };
			material.SetColor(baseColorId, Color.white);
			material.SetFloat(smoothnessId, 1f); // smoothness is fully in the metallic map alpha
			material.SetFloat(smoothnessChannelId, 0f);

			var rects = MakeAtlas(albedos, TextureType.Albedo, material, null);
			MakeAtlas(metallics, TextureType.Metallic, material, rects);
			material.EnableKeyword("_METALLICSPECGLOSSMAP");

			if (bakeNormals)
			{
				MakeAtlas(normals, TextureType.Normal, material, rects);
				material.EnableKeyword("_NORMALMAP");
			}

			if (bakeAo)
			{
				MakeAtlas(aos, TextureType.AO, material, rects);
				material.EnableKeyword("_OCCLUSIONMAP");
			}

			if (bakeEmission)
			{
				MakeAtlas(emissions, TextureType.Emission, material, rects);
				material.SetColor(emissionColorId, new Color(maxEmission, maxEmission, maxEmission, 1f));
				material.EnableKeyword("_EMISSION");
				material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
			}

			for (var q = 0; q < count; q++)
				entries[q].Rect = rects[q];

			AssetDatabase.CreateAsset(material, $"{GetPath()}{material.name}.mat");
			return material;
		}

		static bool IsEmissive(Material mat) => mat.IsKeywordEnabled("_EMISSION") && mat.GetColor(emissionColorId).maxColorComponent > 0.0001f;

		/// <summary> URP Lit metallic map: R = metallic, A = smoothness. </summary>
		static Texture2D BakeMetallic(Material mat, Texture2D albedo, int width, int height)
		{
			var map = mat.GetTexture(metallicGlossMapId);
			var smoothness = mat.GetFloat(smoothnessId);
			var smoothnessFromAlbedo = mat.GetFloat(smoothnessChannelId) > 0.5f;

			if (!map && !smoothnessFromAlbedo)
				return Utils.CreateFilledTexture(width, height, new Color(mat.GetFloat(metallicId), 0f, 0f, smoothness), true);

			var tex = map
				? Utils.ReadTexture(map, width, height, true)
				: Utils.CreateFilledTexture(width, height, new Color(mat.GetFloat(metallicId), 0f, 0f, 1f), true);
			var pixels = tex.GetPixels();
			var albedoPixels = smoothnessFromAlbedo ? albedo.GetPixels() : null;

			for (var p = 0; p < pixels.Length; p++)
				pixels[p].a = (smoothnessFromAlbedo ? albedoPixels[p].a : pixels[p].a) * smoothness;

			tex.SetPixels(pixels);
			tex.Apply();
			return tex;
		}

		Rect[] MakeAtlas(Texture2D[] textures, TextureType type, Material applyMaterial, Rect[] expectedRects)
		{
			var atlasSize = GetAtlasSize();
			var linear = type != TextureType.Albedo && type != TextureType.Emission;
			var texture = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, true, linear);
			var packed = texture.PackTextures(textures, 4, atlasSize);

			// All channels have identical input sizes, so packing must be identical. Guard anyway.
			if (expectedRects != null && !packed.SequenceEqual(expectedRects))
				throw new InvalidOperationException($"Mesh Baker: {type} atlas layout differs from Albedo atlas. Bake process terminated.");

			var fileName = $"Baked{type}{bakePostfix}.png";
			var assetPath = GetPath() + fileName;

			File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath), texture.EncodeToPNG());
			AssetDatabase.ImportAsset(assetPath);

			if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
			{
				importer.textureType = type == TextureType.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
				importer.sRGBTexture = !linear;
				importer.wrapMode = TextureWrapMode.Clamp;
				importer.maxTextureSize = Mathf.Max(texture.width, texture.height);
				importer.SaveAndReimport();
			}

			applyMaterial.SetTexture(GetMaterialId(type), AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath));

			return packed;
		}

		static int GetMaterialId(TextureType type)
		{
			return type switch
			{
				TextureType.Albedo => baseMapId,
				TextureType.Metallic => metallicGlossMapId,
				TextureType.Normal => bumpMapId,
				TextureType.AO => aoMapId,
				TextureType.Emission => emissionMapId,
				_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
			};
		}

		/// <param name="renderers">Renderers sharing the same shadow settings.</param>
		GameObject CreateTarget(List<MeshRenderer> renderers, string suffix)
		{
			var go = new GameObject($"{targetName}{bakePostfix}{suffix}");
			Undo.RegisterCreatedObjectUndo(go, "Mesh Baker combine");
			SceneManager.MoveGameObjectToScene(go, renderers[0].gameObject.scene);

			go.AddComponent<MeshFilter>();
			var renderer = go.AddComponent<MeshRenderer>();
			renderer.shadowCastingMode = renderers[0].shadowCastingMode;
			renderer.receiveShadows = renderers[0].receiveShadows;

			go.layer = renderers.GroupBy(r => r.gameObject.layer).OrderByDescending(g => g.Count()).First().Key;
			GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
				StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.BatchingStatic);

			return go;
		}

		sealed class MeshData
		{
			public readonly List<Vector3> Vertices = new List<Vector3>();
			public readonly List<Vector3> Normals = new List<Vector3>();
			public readonly List<Vector4> Tangents = new List<Vector4>();
			public readonly List<Vector2> Uvs = new List<Vector2>();
			public readonly List<Color> Colors = new List<Color>();
		}

		static Mesh CombineParts(List<Part> parts, Transform target, Material atlasMaterial, List<Material> outMaterials)
		{
			// Submesh 0 = atlas material, then one submesh per kept material.
			var groups = new List<(Material material, List<Part> parts)>();
			var atlasParts = parts.Where(p => p.Atlas != null).ToList();
			if (atlasParts.Count > 0)
				groups.Add((atlasMaterial, atlasParts));
			foreach (var g in parts.Where(p => p.Atlas == null).GroupBy(p => p.Material))
				groups.Add((g.Key, g.ToList()));

			var cache = new Dictionary<Mesh, MeshData>();
			var hasColors = parts.Any(p => p.Mesh.HasVertexAttribute(VertexAttribute.Color));
			var vertices = new List<Vector3>();
			var normals = new List<Vector3>();
			var tangents = new List<Vector4>();
			var uvs = new List<Vector2>();
			var colors = new List<Color>();
			var submeshIndices = new List<List<int>>();
			var remap = new Dictionary<int, int>();

			foreach (var (material, groupParts) in groups)
			{
				var indicesOut = new List<int>();

				foreach (var part in groupParts)
				{
					if (!cache.TryGetValue(part.Mesh, out var data))
						cache[part.Mesh] = data = ReadMesh(part.Mesh);

					var matrix = target.worldToLocalMatrix * part.Renderer.transform.localToWorldMatrix;
					var normalMatrix = matrix.inverse.transpose;
					var flip = matrix.determinant < 0;
					var rect = part.Atlas?.Rect ?? new Rect(0, 0, 1, 1);
					var colorOnly = part.Atlas != null && !part.Atlas.HasTexture;
					var source = part.Mesh.GetIndices(part.SubMesh);

					remap.Clear();
					var mapped = new int[source.Length];

					for (var i = 0; i < source.Length; i++)
					{
						var src = source[i];
						if (!remap.TryGetValue(src, out var dst))
						{
							dst = vertices.Count;
							remap.Add(src, dst);

							vertices.Add(matrix.MultiplyPoint3x4(data.Vertices[src]));
							normals.Add(data.Normals.Count > 0 ? normalMatrix.MultiplyVector(data.Normals[src]).normalized : Vector3.up);

							var t = data.Tangents.Count > 0 ? data.Tangents[src] : new Vector4(1, 0, 0, 1);
							var tDir = matrix.MultiplyVector(t).normalized;
							tangents.Add(new Vector4(tDir.x, tDir.y, tDir.z, flip ? -t.w : t.w));

							var uv = data.Uvs.Count > 0 ? data.Uvs[src] : Vector2.zero;
							if (part.Atlas != null)
								uv = colorOnly
									? rect.center // solid swatch: sample its center to avoid bleeding
									: new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, uv.x), Mathf.Lerp(rect.yMin, rect.yMax, uv.y));
							uvs.Add(uv);

							if (hasColors)
								colors.Add(data.Colors.Count > 0 ? data.Colors[src] : Color.white);
						}

						mapped[i] = dst;
					}

					if (flip)
						for (var i = 0; i < mapped.Length; i += 3)
							(mapped[i + 1], mapped[i + 2]) = (mapped[i + 2], mapped[i + 1]);

					indicesOut.AddRange(mapped);
				}

				submeshIndices.Add(indicesOut);
				outMaterials.Add(material);
			}

			var mesh = new Mesh { name = target.name };
			mesh.indexFormat = vertices.Count >= 65536 ? IndexFormat.UInt32 : IndexFormat.UInt16;
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetTangents(tangents);
			mesh.SetUVs(0, uvs);
			if (hasColors)
				mesh.SetColors(colors);

			mesh.subMeshCount = submeshIndices.Count;
			for (var s = 0; s < submeshIndices.Count; s++)
				mesh.SetTriangles(submeshIndices[s], s, false);

			mesh.RecalculateBounds();
			return mesh;
		}

		static MeshData ReadMesh(Mesh mesh)
		{
			var data = new MeshData();
			mesh.GetVertices(data.Vertices);
			mesh.GetNormals(data.Normals);
			mesh.GetTangents(data.Tangents);
			mesh.GetUVs(0, data.Uvs);
			mesh.GetColors(data.Colors);
			return data;
		}
	}
}
