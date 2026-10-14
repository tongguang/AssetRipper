using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.Modules.Textures;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.UnityProjects.Textures;
using AssetRipper.IO.Files;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Primitives;
using AssetRipper.Processing;
using AssetRipper.Processing.Textures;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_687078895;
using AssetRipper.SourceGenerated.Enums;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.SpriteMetaData;
using AssetRipper.TextureDecoder.Rgb.Formats;

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
	public void CroppedSpriteUsesSourceImageExport()
	{
		(ProcessedAssetCollection collection, ISprite sprite, _) = CreateSprite(0.5f, 0.5f);
		new SpriteProcessor().Process(CreateGameData(collection));
		Assert.That(CreateTextureExporter().TryCreateCollection(sprite, out _), Is.False);
		Assert.That(new SourceSpriteExporter().TryCreateCollection(sprite, out _), Is.True);
		Assert.That(new YamlSpriteExporter().TryCreateCollection(sprite, out _), Is.True);
	}

	[TestCase(SpritePackingRotation.None, false, false)]
	[TestCase(SpritePackingRotation.FlipHorizontal, true, false)]
	[TestCase(SpritePackingRotation.FlipVertical, false, true)]
	[TestCase(SpritePackingRotation.Rotate180, true, true)]
	public void SourceImageRestoresOriginalPixelOrientation(SpritePackingRotation rotation, bool flipX, bool flipY)
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(new UnityVersion(2022, 3));
		ISprite sprite = collection.CreateSprite();
		sprite.Rect.Width = 3;
		sprite.Rect.Height = 2;
		sprite.Pivot!.SetValues(.2f, .8f);
		sprite.PixelsToUnits = 100;
		sprite.RD.TextureRect.X = 2;
		sprite.RD.TextureRect.Y = 1;
		sprite.RD.TextureRect.Width = 3;
		sprite.RD.TextureRect.Height = 2;
		sprite.RD.UvTransform!.SetValues(100, 2.6f, 100, 2.6f);
		sprite.RD.SettingsRaw = 1u | ((uint)rotation << 2);
		var packed = new DirectBitmap<ColorRGBA<byte>, byte>(8, 5);
		for (int y = 0; y < 5; y++)
		{
			for (int x = 0; x < 8; x++)
			{
				int index = ((4 - y) * 8 + x) * 4;
				packed.Bits[index] = (byte)(x + 10 * y);
				packed.Bits[index + 3] = 255;
			}
		}
		var restored = SpriteSourceImage.Restore(sprite, packed);
		for (int y = 0; y < 2; y++)
		{
			for (int x = 0; x < 3; x++)
			{
				int expectedX = 2 + (flipX ? 2 - x : x);
				int expectedY = 1 + (flipY ? 1 - y : y);
				Assert.That(restored.Bits[((1 - y) * 3 + x) * 4], Is.EqualTo(expectedX + 10 * expectedY));
			}
		}
	}

	[Test]
	public void SourceMetadataPreservesOriginalPivotAndCanvas()
	{
		(ProcessedAssetCollection collection, ISprite sprite, ISpriteAtlas atlas) = CreateSprite(.2f, .8f);
		ISpriteMetaData metadata = SpriteMetaData.Create(collection.Version);
		metadata.FillSpriteMetaData(sprite, atlas, originalCanvas: true);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(metadata.Rect.Width, Is.EqualTo(200));
			Assert.That(metadata.Rect.Height, Is.EqualTo(200));
			Assert.That(metadata.Pivot.X, Is.EqualTo(.2f));
			Assert.That(metadata.Pivot.Y, Is.EqualTo(.8f));
		}
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
	public void SourceImageModeRestoresUncroppedSpritesOfRepackedAtlas()
	{
		(ProcessedAssetCollection collection, ISprite sprite, ISpriteAtlas atlas) = CreateSprite(0.5f, 0.5f);
		var data = atlas.RenderDataMap.Values.Single();
		data.TextureRect.Width = 200;
		data.TextureRect.Height = 200;
		data.TextureRectOffset.X = 0;
		data.TextureRectOffset.Y = 0;
		atlas.ConvertToEditorFormat(); // EditorFormatProcessor
		new SpriteProcessor().Process(CreateGameData(collection));
		using (Assert.EnterMultipleScope())
		{
			Assert.That(CreateTextureExporter(SpriteExportMode.SourceImage).TryCreateCollection(sprite, out _), Is.False);
			Assert.That(new SourceSpriteExporter().TryCreateCollection(sprite, out _), Is.True);
		}
	}

	[Test]
	public void UncroppedSpriteWithFloatNoiseUsesTextureExport()
	{
		(ProcessedAssetCollection collection, ISprite sprite, ISpriteAtlas atlas) = CreateSprite(0.5f, 0.5f);
		sprite.Rect.Width = 200.00003f;
		var data = atlas.RenderDataMap.Values.Single();
		data.TextureRect.Width = 200;
		data.TextureRect.Height = 200;
		data.TextureRectOffset.X = 0;
		data.TextureRectOffset.Y = 0;
		new SpriteProcessor().Process(CreateGameData(collection));
		Assert.That(CreateTextureExporter().TryCreateCollection(sprite, out _), Is.True);
	}

	[Test]
	public void DegenerateSpriteIsNotRestored()
	{
		(ProcessedAssetCollection collection, ISprite sprite, _) = CreateSprite(0.5f, 0.5f);
		sprite.Rect.Width = 0.4f;
		new SpriteProcessor().Process(CreateGameData(collection));
		Assert.That(new SourceSpriteExporter().TryCreateCollection(sprite, out _), Is.False);
	}

	[Test]
	public void ImporterVerticesFollowPivotInCroppedRect()
	{
		(ProcessedAssetCollection collection, ISprite sprite, ISpriteAtlas atlas) = CreateSprite(.2f, .8f);
		sprite.RD.VertexData!.VertexCount = 1;
		sprite.RD.VertexData.Data = new byte[3 * sizeof(float)];
		sprite.RD.VertexData.Channels!.AddNew().Dimension = 3;
		ISpriteMetaData metadata = SpriteMetaData.Create(collection.Version);
		metadata.FillSpriteMetaData(sprite, atlas);
		using (Assert.EnterMultipleScope())
		{
			// The pivot sits at (.2 * 200 - 25, .8 * 200 - 32) inside the cropped 154x148 rect.
			Assert.That(metadata.Vertices![0].X, Is.EqualTo(15).Within(0.0001));
			Assert.That(metadata.Vertices[0].Y, Is.EqualTo(128).Within(0.0001));
		}
	}

	[Test]
	public void SourceCanvasSnapsFloatingPointNoiseWithoutMovingMesh()
	{
		(ProcessedAssetCollection collection, ISprite sprite, ISpriteAtlas atlas) = CreateSprite(.2f, .8f);
		sprite.Rect.Width = 200.00003f;
		sprite.Rect.Height = 200.00003f;
		sprite.RD.VertexData!.VertexCount = 1;
		sprite.RD.VertexData.Data = new byte[3 * sizeof(float)];
		sprite.RD.VertexData.Channels!.AddNew().Dimension = 3;
		ISpriteMetaData metadata = SpriteMetaData.Create(collection.Version);
		metadata.FillSpriteMetaData(sprite, atlas, originalCanvas: true);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(metadata.Rect.Width, Is.EqualTo(200));
			Assert.That(metadata.Rect.Height, Is.EqualTo(200));
			Assert.That(metadata.Vertices![0].X, Is.EqualTo(40).Within(0.0001));
			Assert.That(metadata.Vertices[0].Y, Is.EqualTo(160).Within(0.0001));
		}
	}

	[Test]
	public void FractionalCanvasMatchesRestoredImageAndKeepsPivotPixel()
	{
		(ProcessedAssetCollection collection, ISprite sprite, ISpriteAtlas atlas) = CreateSprite(.5f, .5f);
		sprite.Rect.Width = 100.5f;
		ISpriteMetaData metadata = SpriteMetaData.Create(collection.Version);
		metadata.FillSpriteMetaData(sprite, atlas, originalCanvas: true);
		var restored = SpriteSourceImage.Restore(sprite, new DirectBitmap<ColorRGBA<byte>, byte>(1, 1));
		using (Assert.EnterMultipleScope())
		{
			Assert.That(metadata.Rect.Width, Is.EqualTo(restored.Width));
			Assert.That(metadata.Rect.Width * metadata.Pivot.X, Is.EqualTo(100.5f * .5f).Within(0.0001));
		}
	}

	[Test]
	public void LegacySpritePivotComesFromOffset()
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(new UnityVersion(5, 0));
		ISprite sprite = collection.CreateSprite();
		sprite.Rect.Width = 200;
		sprite.Rect.Height = 100;
		sprite.Offset.X = -60;
		sprite.Offset.Y = 30;
		using (Assert.EnterMultipleScope())
		{
			Assert.That(sprite.Has_Pivot(), Is.False);
			Assert.That(sprite.GetPivot().X, Is.EqualTo(.2f).Within(0.0001));
			Assert.That(sprite.GetPivot().Y, Is.EqualTo(.8f).Within(0.0001));
		}
	}

	[Test]
	public void MeshWithoutIndicesKeepsWholeCanvas()
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(new UnityVersion(2022, 3));
		ISprite sprite = collection.CreateSprite();
		sprite.Rect.Width = 4;
		sprite.Rect.Height = 4;
		sprite.Pivot!.SetValues(0, 0);
		sprite.PixelsToUnits = 1;
		sprite.RD.TextureRect.Width = 4;
		sprite.RD.TextureRect.Height = 4;
		sprite.RD.UvTransform!.SetValues(1, 0, 1, 0);
		sprite.RD.VertexData!.VertexCount = 1;
		sprite.RD.VertexData.Data = new byte[3 * sizeof(float)];
		sprite.RD.VertexData.Channels!.AddNew().Dimension = 3;
		var packed = new DirectBitmap<ColorRGBA<byte>, byte>(4, 4);
		packed.Bits.Fill(255);
		var restored = SpriteSourceImage.Restore(sprite, packed);
		Assert.That(restored.Bits.ToArray(), Is.All.EqualTo(255));
	}

	[Test]
	public void PaddingOutsideAtlasStaysTransparent()
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(new UnityVersion(2022, 3));
		ISprite sprite = collection.CreateSprite();
		sprite.Rect.Width = 4;
		sprite.Rect.Height = 2;
		sprite.Pivot!.SetValues(0, 0);
		sprite.PixelsToUnits = 1;
		sprite.RD.TextureRect.Width = 2;
		sprite.RD.TextureRect.Height = 2;
		// The canvas starts two pixels left of the atlas edge, like a sprite trimmed against x = 0.
		sprite.RD.UvTransform!.SetValues(1, -2, 1, 0);
		var packed = new DirectBitmap<ColorRGBA<byte>, byte>(2, 2);
		packed.Bits.Fill(255);
		var restored = SpriteSourceImage.Restore(sprite, packed);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(restored.Bits[0 * 4 + 3], Is.Zero);
			Assert.That(restored.Bits[3 * 4 + 3], Is.EqualTo(255));
		}
	}

	[Test]
	public void DegenerateTriangleMasksNoPixels()
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(new UnityVersion(2022, 3));
		ISprite sprite = collection.CreateSprite();
		sprite.Rect.Width = 4;
		sprite.Rect.Height = 4;
		sprite.Pivot!.SetValues(0, 0);
		sprite.PixelsToUnits = 1;
		sprite.RD.TextureRect.Width = 4;
		sprite.RD.TextureRect.Height = 4;
		sprite.RD.UvTransform!.SetValues(1, 0, 1, 0);
		sprite.RD.VertexData!.VertexCount = 3;
		float[] coordinates = [0, 0, 0, 2, 2, 0, 4, 4, 0];
		byte[] data = new byte[coordinates.Length * sizeof(float)];
		Buffer.BlockCopy(coordinates, 0, data, 0, data.Length);
		sprite.RD.VertexData.Data = data;
		sprite.RD.VertexData.Channels!.AddNew().Dimension = 3;
		sprite.RD.IndexBuffer = [0, 0, 1, 0, 2, 0];
		var packed = new DirectBitmap<ColorRGBA<byte>, byte>(4, 4);
		packed.Bits.Fill(255);
		var restored = SpriteSourceImage.Restore(sprite, packed);
		Assert.That(restored.Bits.ToArray(), Is.All.Zero);
	}

	[Test]
	public void SourceAtlasKeepsPackablesAndRemovesBakedCache()
	{
		(ProcessedAssetCollection collection, ISprite sprite, ISpriteAtlas atlas) = CreateSprite(.2f, .8f);
		atlas.ConvertToEditorFormat(); // EditorFormatProcessor
		new SpriteProcessor().Process(CreateGameData(collection));
		Assert.That(new SourceSpriteAtlasExporter().TryCreateCollection(atlas, out _), Is.True);
		SourceSpriteAtlasExporter.PrepareForRepacking(atlas);
		SourceSpriteAtlasExporter.PrepareForRepacking(atlas);
		using (Assert.EnterMultipleScope())
		{
			// A second export of the same loaded data must make the same decisions.
			Assert.That(new SourceSpriteAtlasExporter().TryCreateCollection(atlas, out _), Is.True);
			Assert.That(CreateTextureExporter(SpriteExportMode.SourceImage).TryCreateCollection(sprite, out _), Is.False);
			Assert.That(atlas.EditorData.Packables.Count, Is.EqualTo(1));
			Assert.That(atlas.PackedSprites!.Count, Is.Zero);
			Assert.That(atlas.PackedSpriteNamesToIndex!.Count, Is.Zero);
			Assert.That(atlas.RenderDataMap.Count, Is.Zero);
			Assert.That(atlas.EditorData.PackingSettings!.EnableRotation, Is.False);
			Assert.That(atlas.EditorData.PackingSettings.EnableTightPacking, Is.False);
			Assert.That(sprite.TryGetTexture(), Is.Not.Null);
			Assert.That(new SourceSpriteExporter().TryCreateCollection(sprite, out _), Is.True);
		}
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

	[TestCase(false)]
	[TestCase(true)]
	public void ZeroAreaOutlineIsExcludedWithoutChangingMesh(bool originalCanvas)
	{
		(ProcessedAssetCollection collection, ISprite sprite, ISpriteAtlas atlas) = CreateSprite(.5f, .5f);
		sprite.RD.VertexData!.VertexCount = 3;
		float[] coordinates = [0, 0, 0, 1, 0, 0, 2, 0, 0];
		byte[] data = new byte[coordinates.Length * sizeof(float)];
		Buffer.BlockCopy(coordinates, 0, data, 0, data.Length);
		sprite.RD.VertexData.Data = data;
		sprite.RD.VertexData.Channels!.AddNew().Dimension = 3;
		sprite.RD.IndexBuffer = [0, 0, 1, 0, 2, 0];
		var submesh = sprite.RD.SubMeshes!.AddNew();
		submesh.IndexCount = 3;
		submesh.VertexCount = 3;
		ISpriteMetaData metadata = SpriteMetaData.Create(collection.Version);
		metadata.FillSpriteMetaData(sprite, atlas, originalCanvas);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(metadata.Outline, Is.Empty);
			Assert.That(metadata.Vertices!.Count, Is.EqualTo(3));
			Assert.That(metadata.Indices!.Count, Is.EqualTo(3));
		}
	}

	private static (ProcessedAssetCollection, ISprite, ISpriteAtlas) CreateSprite(float pivotX, float pivotY)
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(new UnityVersion(2022, 3));
		ITexture2D texture = collection.CreateTexture2D();
		texture.Name = "Atlas";
		ISprite sprite = collection.CreateSprite();
		sprite.Name = "Sprite";
		sprite.PixelsToUnits = 100;
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

	private static TextureAssetExporter CreateTextureExporter(SpriteExportMode mode = SpriteExportMode.Native)
	{
		FullConfiguration settings = new();
		settings.ExportSettings.SpriteExportMode = mode;
		return new(settings);
	}
}
