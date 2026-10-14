using AssetRipper.Assets;
using AssetRipper.Assets.Generics;
using AssetRipper.Export.UnityProjects.Project;
using AssetRipper.Processing.Textures;
using AssetRipper.SourceGenerated.Classes.ClassID_0;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_687078895;
using AssetRipper.SourceGenerated.Enums;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Object;
using AssetRipper.SourceGenerated.Subclasses.TextureImporterPlatformSettings;

namespace AssetRipper.Export.UnityProjects.Textures;

/// <summary>Exports editable atlases that Unity packs from the restored source sprites.</summary>
public sealed class SourceSpriteAtlasExporter : YamlExporterBase
{
	public override bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out IExportCollection? exportCollection)
	{
		if (asset is ISpriteAtlas atlas && CanRepack(atlas))
		{
			exportCollection = new AtlasCollection(this, atlas);
			return true;
		}
		exportCollection = null;
		return false;
	}

	internal static bool CanRepack(ISprite sprite) =>
		sprite.MainAsset is SpriteInformationObject info
			&& info.Sprites.TryGetValue(sprite, out ISpriteAtlas? atlas)
			&& atlas is not null
			&& CanRepack(atlas);

	// Packables are filled by EditorFormatProcessor and survive PrepareForRepacking, so repeated exports agree.
	private static bool CanRepack(ISpriteAtlas atlas) =>
		atlas.EditorData.Packables.Count > 0
			&& atlas.EditorData.Packables.ToPPtrAccessList<PPtr_Object_5, IObject>(atlas.Collection)
				.All(packable => packable is ISprite sprite && SourceSpriteExporter.CanRestore(sprite));

	public static void PrepareForRepacking(ISpriteAtlas atlas)
	{
		if (atlas.EditorData.PackingSettings is { } packing)
		{
			packing.EnableRotation = false;
			packing.EnableTightPacking = false;
			// Restored images keep the original colors of transparent edge texels, which bilinear sampling blends in.
			packing.EnableAlphaDilation = false;
		}
		// The packed texture is the only record of how the original atlas was sampled.
		if (atlas.RenderDataMap.Values.FirstOrDefault()?.Texture.TryGetAsset(atlas.Collection) is ITexture2D texture)
		{
			atlas.EditorData.TextureSettings.FilterMode = texture.TextureSettings_C28.FilterMode;
			atlas.EditorData.TextureSettings.SetAnisoLevel(texture.TextureSettings_C28.Aniso);
			atlas.EditorData.TextureSettings.SetSRGB(texture.ColorSpace_C28E == ColorSpace.Linear);
		}
		atlas.EditorData.TextureSettings.TextureCompression = 0;
		atlas.EditorData.TextureSettings.SetGenerateMipMaps(false);
		atlas.EditorData.TextureSettings.SetMaxTextureSize(4096);
		// Unity takes the atlas format from the default platform; without one it compresses the repacked atlas.
		atlas.EditorData.PlatformSettings.Clear();
		ITextureImporterPlatformSettings platform = atlas.EditorData.PlatformSettings.AddNew();
		platform.BuildTarget = "DefaultTexturePlatform";
		platform.MaxTextureSize = 4096;
		platform.Format = (int)TextureImporterFormat.Automatic;
		platform.TextureCompression = (int)TextureImporterCompression.Uncompressed;
		platform.CompressionQuality = 50;
		atlas.PackedSprites!.Clear();
		atlas.PackedSpriteNamesToIndex?.Clear();
		atlas.RenderDataMap.Clear();
	}

	private sealed class AtlasCollection(IAssetExporter exporter, ISpriteAtlas atlas) : AssetExportCollection<ISpriteAtlas>(exporter, atlas)
	{
		protected override string GetExportExtension(IUnityObjectBase asset) => "spriteatlas";

		protected override bool ExportInner(IExportContainer container, string filePath, string dirPath, FileSystem fileSystem)
		{
			PrepareForRepacking(Asset);
			return base.ExportInner(container, filePath, dirPath, fileSystem);
		}
	}
}
