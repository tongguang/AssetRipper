using AssetRipper.Assets;
using AssetRipper.Assets.Generics;
using AssetRipper.Export.UnityProjects.Project;
using AssetRipper.Processing.Textures;
using AssetRipper.SourceGenerated.Classes.ClassID_0;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Classes.ClassID_687078895;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Object;

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
		}
		atlas.EditorData.TextureSettings.TextureCompression = 0;
		atlas.EditorData.TextureSettings.SetGenerateMipMaps(false);
		atlas.EditorData.TextureSettings.SetMaxTextureSize(4096);
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
