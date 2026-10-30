using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InsaneOne.DevTools
{
	/// <summary> Finds MeshRenderers in the active scene which never move or change at runtime, so they are safe to combine.
	/// Conservative: anything driven by physics, animation, gameplay scripts, or referenced from a gameplay script is skipped.</summary>
	public static class StaticObjectFinder
	{
		public sealed class Result
		{
			public readonly List<MeshRenderer> Renderers = new List<MeshRenderer>();
			public readonly Dictionary<string, int> SkipReasons = new Dictionary<string, int>();

			public void Skip(string reason) => SkipReasons[reason] = SkipReasons.TryGetValue(reason, out var c) ? c + 1 : 1;
		}

		/// <param name="passiveScripts">Script type names which only hold data / gizmos (zone or spawn markers). They never move
		/// their objects, so objects under them are still static.</param>
		public static Result Find(bool requireStaticFlag, bool includeCustomShaders, string bakedAssetsFolder, ICollection<string> passiveScripts)
		{
			var result = new Result();
			var scriptDriven = new HashSet<Transform>();
			var referenced = new HashSet<Transform>();
			var scene = SceneManager.GetActiveScene();

			foreach (var root in scene.GetRootGameObjects())
			foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
			{
				if (!mb || IsUnityType(mb) || passiveScripts.Contains(mb.GetType().Name))
					continue;

				scriptDriven.Add(mb.transform);
				CollectSceneReferences(mb, referenced);
			}

			foreach (var root in scene.GetRootGameObjects())
			foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
				Check(renderer, result, scriptDriven, referenced, requireStaticFlag, includeCustomShaders, bakedAssetsFolder);

			return result;
		}

		/// <summary> Re-validates an arbitrary set of renderers (e.g. manual selection) against the same rules. </summary>
		public static Result Filter(IEnumerable<MeshRenderer> renderers, bool includeCustomShaders, string bakedAssetsFolder, ICollection<string> passiveScripts)
		{
			var all = Find(false, includeCustomShaders, bakedAssetsFolder, passiveScripts);
			var allowed = new HashSet<MeshRenderer>(all.Renderers);
			var result = new Result();

			foreach (var renderer in renderers)
			{
				if (allowed.Contains(renderer))
					result.Renderers.Add(renderer);
				else
					result.Skip("selected, but moves / is script-driven / unsupported");
			}

			return result;
		}

		static void Check(MeshRenderer renderer, Result result, HashSet<Transform> scriptDriven, HashSet<Transform> referenced,
			bool requireStaticFlag, bool includeCustomShaders, string bakedAssetsFolder)
		{
			var go = renderer.gameObject;

			if (!go.activeInHierarchy || !renderer.enabled)
			{
				result.Skip("inactive or renderer disabled");
				return;
			}

			if (!go.TryGetComponent<MeshFilter>(out var meshFilter) || !meshFilter.sharedMesh)
			{
				result.Skip("no mesh");
				return;
			}

			var meshPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
			if (!string.IsNullOrEmpty(meshPath) && meshPath.StartsWith(bakedAssetsFolder))
			{
				result.Skip("already a baked mesh");
				return;
			}

			if (requireStaticFlag && !GameObjectUtility.GetStaticEditorFlags(go).HasFlag(StaticEditorFlags.BatchingStatic))
			{
				result.Skip("no Batching Static flag");
				return;
			}

			if (go.GetComponentInParent<Rigidbody>(true) || go.GetComponentInParent<Rigidbody2D>(true))
			{
				result.Skip("physics (Rigidbody)");
				return;
			}

			if (go.GetComponentInParent<Animator>(true) || go.GetComponentInParent<Animation>(true))
			{
				result.Skip("animated");
				return;
			}

			if (go.GetComponentInParent<LODGroup>(true) || go.GetComponent<ParticleSystem>())
			{
				result.Skip("LODGroup / particles");
				return;
			}

			for (var t = go.transform; t; t = t.parent)
			{
				if (scriptDriven.Contains(t))
				{
					result.Skip("under gameplay script");
					return;
				}

				if (referenced.Contains(t))
				{
					result.Skip("referenced by gameplay script");
					return;
				}
			}

			foreach (var material in renderer.sharedMaterials)
			{
				if (!material)
				{
					result.Skip("missing material");
					return;
				}

				if (!includeCustomShaders && !material.shader.name.StartsWith("Universal Render Pipeline/"))
				{
					result.Skip("custom shader (" + material.shader.name + ")");
					return;
				}
			}

			result.Renderers.Add(renderer);
		}

		static bool IsUnityType(MonoBehaviour mb)
		{
			var assembly = mb.GetType().Assembly.GetName().Name;
			return assembly.StartsWith("Unity") || assembly.StartsWith("UnityEngine");
		}

		static void CollectSceneReferences(MonoBehaviour mb, HashSet<Transform> referenced)
		{
			var so = new SerializedObject(mb);
			var it = so.GetIterator();

			while (it.Next(true))
			{
				if (it.propertyType != SerializedPropertyType.ObjectReference || !it.objectReferenceValue)
					continue;

				var value = it.objectReferenceValue;
				if (EditorUtility.IsPersistent(value))
					continue;

				var refTransform = value is GameObject refGo ? refGo.transform : (value as Component)?.transform;

				// Own object (m_GameObject, GetComponent-like fields) is already covered by the script itself.
				if (refTransform && refTransform != mb.transform)
					referenced.Add(refTransform);
			}
		}
	}
}
