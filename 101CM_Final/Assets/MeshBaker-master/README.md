![GitHub License](https://img.shields.io/github/license/OlegDzhuraev/MeshBaker)

# Mesh Baker tool
Allows you to combine several meshes into one, and combine their textures to atlases for optimization purposes.

<p align="center">
    <img src="https://dzhuraev.com/GithubData/MeshBakerTool01.png" width="410" height="419" alt="Mesh Baker Tool">
</p>

# Features
- Supports almost unlimited meshes per combine (but it is necessary to use common sense, personally determining the necessary limit).
- Supports bake of Albedo, Normal, Specular/Metallic and AO maps to texture atlases. 
- Generates mesh UVs for Lightmapper.
- Supports mesh save to the Assets folder.
- Supports generated meshes with up to 4 billion vertices (but not guaranteed to rendered on all platforms. In this case, try to keep vertex count lower than 65536).

# Limitations
Limitations may be removed in the future versions.
- Supports only BiRP materials (Standard and Standard (Specular setup)).

# How to
1. Open **Tools -> Mesh Baker** in the top menu. 
2. Select required meshes on scene.
3. Change parameters to fit your needs.
4. Press **Bake** in the tool interface.

# Recommendations
- Textures are better to have square aspect ratio (non-square is supported, but not in the most efficient way). Also, if textures have different sizes, them will be scaled to a smaller one in the final texture atlas.
- Selected meshes should have same material to allow tool produce correct results.

# Additional info
- ~~This tool sets **Read/Write** flag value to **enabled** on all processed textures.~~ Textures are now read through the GPU, import settings are untouched.

# 101CM changes
- **URP Lit** instead of BiRP Standard: `_BaseMap`, `_BaseColor`, `_MetallicGlossMap` (R = metallic, A = smoothness), `_BumpMap`, `_OcclusionMap`, `_EmissionMap`.
- **Color-only materials** (no texture) are baked as small solid swatches, so per-material color / metallic / smoothness / emission survive in one atlas material.
- Materials which can't be atlased (other shaders, transparent / cutout, tiled textures like the floor checker) stay as **extra submeshes** of the same mesh with their original material.
- Multi-submesh meshes, negative scale and vertex colors are supported.
- Originals: only their **MeshRenderer is disabled** — colliders and scripts keep working.
- One combined object per **shadow casting mode** (e.g. `Combined_Static00` and `Combined_Static00_NoShadows`), all sharing one atlas.
- **Tools -> Mesh Baker -> "Select static objects in scene"** finds objects which never move: skips Rigidbody, Animator, LODGroup, anything under a gameplay script or referenced by one (e.g. `GameManager.exitDoor`), inactive / `HideInPlay` objects and custom shaders. Data-only marker scripts (`ZoneVolume`, `PointMarker`, ...) are listed as *passive* and don't block.
- **Tools -> Mesh Baker - Bake Static Objects**: find + bake in one click with default settings.
- Output goes to `Assets/_Game/Baked`.
