using UnityEditor;
using UnityEngine;

namespace InsaneOne.DevTools
{
	public static class Utils
	{
		/// <summary> Reads any texture (compressed, non-readable, any import settings) into a new readable RGBA32 texture
		/// of the given size through the GPU. Source import settings are left untouched.
		/// Use linear = true for data textures (normal, metallic, AO), false for color textures.</summary>
		public static Texture2D ReadTexture(Texture source, int width, int height, bool linear)
		{
			var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
				linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
			var prev = RenderTexture.active;

			Graphics.Blit(source, rt);
			RenderTexture.active = rt;

			var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
			tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
			tex.Apply();

			RenderTexture.active = prev;
			RenderTexture.ReleaseTemporary(rt);

			return tex;
		}

		/// <summary> Normal maps are stored packed by the importer (DXT5nm uses AG, BC5 uses RG, mobile formats use RGB).
		/// Decodes to a plain tangent-space RGB normal, same as Unity's UnpackNormalmapRGorAG.</summary>
		public static Texture2D RestoreNormal(Texture normal, int width, int height)
		{
			var tex = ReadTexture(normal, width, height, true);
			var pixels = tex.GetPixels();

			for (var q = 0; q < pixels.Length; q++)
			{
				var x = pixels[q].r * pixels[q].a * 2f - 1f;
				var y = pixels[q].g * 2f - 1f;
				var z = Mathf.Sqrt(Mathf.Max(0f, 1f - x * x - y * y));
				pixels[q] = new Color(x * 0.5f + 0.5f, y * 0.5f + 0.5f, z * 0.5f + 0.5f, 1f);
			}

			tex.SetPixels(pixels);
			tex.Apply();

			return tex;
		}

		public static Texture2D CreateFilledTexture(int width, int height, Color color, bool linear = false)
		{
			var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
			var pixels = new Color[width * height];

			for (var q = 0; q < pixels.Length; q++)
				pixels[q] = color;

			tex.SetPixels(pixels);
			tex.Apply();

			return tex;
		}

		public static void MultiplyPixels(Texture2D texture, Color color)
		{
			var pixels = texture.GetPixels();

			for (var q = 0; q < pixels.Length; q++)
				pixels[q] *= color;

			texture.SetPixels(pixels);
			texture.Apply();
		}

		public static void EnsureFolder(string assetPath)
		{
			if (AssetDatabase.IsValidFolder(assetPath))
				return;

			var parent = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
			EnsureFolder(parent);
			AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(assetPath));
		}
	}
}
