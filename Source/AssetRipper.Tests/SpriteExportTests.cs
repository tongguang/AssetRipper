using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.UnityProjects.Textures;
using AssetRipper.Export.Configuration;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.Primitives;
using AssetRipper.Processing;
using AssetRipper.Processing.Textures;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_687078895;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.SpriteMetaData;

namespace AssetRipper.Tests;

internal class SpriteExportTests
{
	[TestCase(0.5f, 0.5f)]
	[TestCase(0.2f, 0.8f)]
	public void ProcessingPreservesOriginalSpriteCoordinates(float pivotX, float pivotY)
	{
		(ProcessedAssetCollection collection, ISprite sprite, ISpriteAtlas atlas) = CreateSprite(pivotX, pivotY);
		new SpriteProcessor().Process(CreateGameData(collection));

		using (Assert.EnterMultipleScope())
		{
			Assert.That(sprite.Rect.Width, Is.EqualTo(200));
			Assert.That(sprite.Rect.Height, Is.EqualTo(200));
			Assert.That(sprite.Pivot!.X, Is.EqualTo(pivotX));
			Assert.That(sprite.Pivot.Y, Is.EqualTo(pivotY));
			Assert.That(sprite.RD.TextureRectOffset.X, Is.EqualTo(25));
			Assert.That(sprite.RD.TextureRectOffset.Y, Is.EqualTo(32));
			Assert.That(sprite.SpriteAtlasP, Is.Null);
		}

		ISpriteMetaData metadata = SpriteMetaData.Create(collection.Version);
		metadata.FillSpriteMetaData(sprite, atlas);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(metadata.Pivot.X, Is.EqualTo((pivotX * 200 - 25) / 154).Within(0.00001));
			Assert.That(metadata.Pivot.Y, Is.EqualTo((pivotY * 200 - 32) / 148).Within(0.00001));
			Assert.That(metadata.Rect.Width, Is.EqualTo(154));
			Assert.That(metadata.Rect.Height, Is.EqualTo(148));
		}
	}

	[Test]
	public void CroppedSpriteUsesNativeExport()
	{
		(ProcessedAssetCollection collection, ISprite sprite, _) = CreateSprite(0.5f, 0.5f);
		new SpriteProcessor().Process(CreateGameData(collection));
		Assert.That(CreateTextureExporter().TryCreateCollection(sprite, out _), Is.False);
		Assert.That(new YamlSpriteExporter().TryCreateCollection(sprite, out _), Is.True);
	}

	[Test]
	public void UncroppedSpriteUsesTextureExport()
	{
		(ProcessedAssetCollection collection, ISprite sprite, ISpriteAtlas atlas) = CreateSprite(0.5f, 0.5f);
		var data = atlas.RenderDataMap.Values.Single();
		data.TextureRect.Width = 200;
		data.TextureRect.Height = 200;
		data.TextureRectOffset.X = 0;
		data.TextureRectOffset.Y = 0;
		new SpriteProcessor().Process(CreateGameData(collection));
		Assert.That(CreateTextureExporter().TryCreateCollection(sprite, out _), Is.True);
	}

	[Test]
	public void UnskinnedSpriteHasOneEditorWeightPerVertex()
	{
		(ProcessedAssetCollection collection, ISprite sprite, ISpriteAtlas atlas) = CreateSprite(0.5f, 0.5f);
		sprite.RD.VertexData!.VertexCount = 3;
		sprite.RD.VertexData.Data = new byte[3 * 3 * sizeof(float)];
		var channel = sprite.RD.VertexData.Channels!.AddNew();
		channel.Dimension = 3;
		ISpriteMetaData metadata = SpriteMetaData.Create(collection.Version);
		metadata.FillSpriteMetaData(sprite, atlas);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(metadata.Vertices!.Count, Is.EqualTo(3));
			Assert.That(metadata.Weights!.Count, Is.EqualTo(metadata.Vertices.Count));
			Assert.That(metadata.Bones!.Count, Is.Zero);
		}
	}

	private static (ProcessedAssetCollection, ISprite, ISpriteAtlas) CreateSprite(float pivotX, float pivotY)
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(new UnityVersion(2022, 3));
		ITexture2D texture = collection.CreateTexture2D();
		texture.Name = "Atlas";
		ISprite sprite = collection.CreateSprite();
		sprite.Name = "Sprite";
		sprite.Rect.Width = 200;
		sprite.Rect.Height = 200;
		sprite.Pivot!.X = pivotX;
		sprite.Pivot.Y = pivotY;
		ISpriteAtlas atlas = collection.CreateSpriteAtlas();
		sprite.SpriteAtlasP = atlas;
		var pair = atlas.RenderDataMap.AddNew();
		sprite.RenderDataKey!.Value = 21300000;
		pair.Key.Value = sprite.RenderDataKey.Value;
		pair.Value.Texture.SetAsset(collection, texture);
		pair.Value.TextureRect.Width = 154;
		pair.Value.TextureRect.Height = 148;
		pair.Value.TextureRectOffset.X = 25;
		pair.Value.TextureRectOffset.Y = 32;
		pair.Value.SettingsRaw = 67;
		atlas.PackedSprites!.AddNew().SetAsset(collection, sprite);
		return (collection, sprite, atlas);
	}

	private static GameData CreateGameData(ProcessedAssetCollection collection) =>
		new((GameBundle)collection.Bundle, collection.Version, new BaseManager(_ => { }), null);

	private static TextureAssetExporter CreateTextureExporter()
	{
		FullConfiguration settings = new();
		settings.ExportSettings.SpriteExportMode = SpriteExportMode.Native;
		return new(settings);
	}
}
