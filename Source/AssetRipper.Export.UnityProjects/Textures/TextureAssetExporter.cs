using AssetRipper.Assets;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.Modules.Textures;
using AssetRipper.Import.Logging;
using AssetRipper.Processing.Textures;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Enums;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Export.UnityProjects.Textures;

public class TextureAssetExporter : BinaryAssetExporter
{
	public ImageExportFormat ImageExportFormat { get; }
	private SpriteExportMode SpriteExportMode { get; }
	public bool PreferOriginalTextureExtension { get; }
	private bool ExportSprites => SpriteExportMode is not SpriteExportMode.Yaml;

	internal bool CanExportSpriteAsTexture(ISprite sprite)
	{
		// A texture importer cannot represent an original rect extending outside a
		// packed texture rect. Export the native Sprite instead of discarding its padding.
		// Tolerate float noise such as 200.00003 in serialized rects.
		static bool Near(float a, float b) => MathF.Abs(a - b) < .001f;
		return Near(sprite.Rect.Width, sprite.RD.TextureRect.Width)
			&& Near(sprite.Rect.Height, sprite.RD.TextureRect.Height)
			&& Near(sprite.RD.TextureRectOffset.X, 0)
			&& Near(sprite.RD.TextureRectOffset.Y, 0)
			&& sprite.RD.PackingRotation == SpritePackingRotation.None
			&& (sprite.MainAsset is not SpriteInformationObject info
				|| info.Sprites.Keys.Select(s => s.PixelsToUnits).Distinct().Count() <= 1)
			// All sprites of a repacked atlas are restored, so Unity never packs from the old atlas texture.
			&& !(SpriteExportMode is SpriteExportMode.SourceImage && SourceSpriteAtlasExporter.CanRepack(sprite));
	}

	public TextureAssetExporter(FullConfiguration configuration)
	{
		ImageExportFormat = configuration.ExportSettings.ImageExportFormat;
		SpriteExportMode = configuration.ExportSettings.SpriteExportMode;
		PreferOriginalTextureExtension = configuration.ExportSettings.PreferOriginalTextureExtension;
	}

	public override bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out IExportCollection? exportCollection)
	{
		if (asset.MainAsset is SpriteInformationObject spriteInformationObject
			&& (asset is not ISprite sprite || ExportSprites && CanExportSpriteAsTexture(sprite)))
		{
			exportCollection = new TextureExportCollection(this, spriteInformationObject, ExportSprites);
			return true;
		}
		else
		{
			exportCollection = null;
			return false;
		}
	}

	public override bool Export(IExportContainer container, IUnityObjectBase asset, string path, FileSystem fileSystem)
	{
		ITexture2D texture = (ITexture2D)asset;
		if (!texture.CheckAssetIntegrity())
		{
			Logger.Log(LogType.Warning, LogCategory.Export, $"Can't export '{texture.Name}' because resources file '{texture.StreamData_C28?.Path}' hasn't been found");
			return false;
		}

		if (TextureConverter.TryConvertToBitmap(texture, out DirectBitmap bitmap))
		{
			using Stream stream = fileSystem.File.Create(path);
			bitmap.Save(stream, texture.GetTextureExportFormat(PreferOriginalTextureExtension, ImageExportFormat));
			return true;
		}
		else
		{
			Logger.Log(LogType.Warning, LogCategory.Export, $"Unable to convert '{texture.Name}' to bitmap");
			return false;
		}
	}
}
