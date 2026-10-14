using AssetRipper.Assets;
using AssetRipper.Export.Modules.Textures;
using AssetRipper.SourceGenerated;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_1006;
using AssetRipper.SourceGenerated.Enums;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Export.UnityProjects.Textures;

/// <summary>Restores a packed Sprite's source canvas for the standard SpriteAtlas workflow.</summary>
public sealed class SourceSpriteExporter : BinaryAssetExporter
{
	public override bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out IExportCollection? exportCollection)
	{
		if (asset is ISprite sprite && CanRestore(sprite))
		{
			exportCollection = new SourceCollection(this, sprite);
			return true;
		}
		exportCollection = null;
		return false;
	}

	internal static bool CanRestore(ISprite sprite) =>
		sprite.TryGetTexture() is not null
			&& sprite.GetSourceCanvasSize() is { Width: >= 1, Height: >= 1 }
			&& (sprite.Bones?.Count ?? 0) == 0
			&& (!sprite.RD.Has_AlphaTexture() || sprite.RD.AlphaTexture.TryGetAsset(sprite.Collection) is null)
			&& (sprite.RD.SecondaryTextures?.Count ?? 0) == 0
			&& sprite.RD.PackingRotation is SpritePackingRotation.None or SpritePackingRotation.FlipHorizontal
				or SpritePackingRotation.FlipVertical or SpritePackingRotation.Rotate180;

	public override bool Export(IExportContainer container, IUnityObjectBase asset, string path, FileSystem fileSystem)
	{
		ISprite sprite = (ISprite)asset;
		ITexture2D texture = sprite.TryGetTexture()!;
		// Sprites of one atlas are exported together; decode the shared texture only once.
		if (texture != lastTexture)
		{
			if (!TextureConverter.TryConvertToBitmap(texture, out DirectBitmap packed, rgba32: true))
			{
				return false;
			}
			lastTexture = texture;
			lastBitmap = packed;
		}
		DirectBitmap restored = SpriteSourceImage.Restore(sprite, lastBitmap!);
		using Stream stream = fileSystem.File.Create(path);
		restored.SaveAsPng(stream);
		return true;
	}

	private ITexture2D? lastTexture;
	private DirectBitmap? lastBitmap;

	private sealed class SourceCollection(IAssetExporter exporter, ISprite sprite) : AssetExportCollection<ISprite>(exporter, sprite)
	{
		protected override string GetExportExtension(IUnityObjectBase asset) => "png";

		protected override IUnityObjectBase CreateImporter(IExportContainer container)
		{
			ITextureImporter importer = ImporterFactory.GenerateTextureImporter(container, Asset.TryGetTexture()!);
			importer.TextureTypeE = TextureImporterType.Sprite;
			importer.SpriteModeE = SpriteImportMode.Multiple;
			importer.SpritePixelsToUnits = Asset.PixelsToUnits;
			importer.MipMaps.EnableMipMap = 0;
			importer.MaxTextureSize = 16384;
			importer.SpriteMeshType = (int)Asset.RD.MeshType;
			importer.SpriteExtrude = Asset.Extrude;
			foreach (var platform in importer.PlatformSettings)
			{
				platform.MaxTextureSize = 16384;
				platform.TextureCompression = 0;
			}
			var metadata = importer.SpriteSheet!.Sprites.AddNew();
			metadata.FillSpriteMetaData(Asset, null, originalCanvas: true);
			long exportID = GetExportID(container, Asset);
			metadata.InternalID = exportID;
			if (importer.Has_InternalIDToNameTable())
			{
				var pair = importer.InternalIDToNameTable.AddNew();
				pair.Key.Key = (int)ClassIDType.Sprite;
				pair.Key.Value = exportID;
				pair.Value = Asset.Name;
			}
			else if (importer.Has_FileIDToRecycleName_AssetDictionary_Int64_Utf8String())
			{
				importer.FileIDToRecycleName_AssetDictionary_Int64_Utf8String.Add(exportID, Asset.Name);
			}
			else if (importer.Has_FileIDToRecycleName_AssetDictionary_Int32_Utf8String())
			{
				importer.FileIDToRecycleName_AssetDictionary_Int32_Utf8String.Add((int)exportID, Asset.Name);
			}
			return importer;
		}
	}
}
