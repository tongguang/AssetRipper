using AssetRipper.Numerics;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_687078895;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.SpriteAtlasData;
using System.Drawing;
using System.Numerics;

namespace AssetRipper.SourceGenerated.Extensions;

public static class SpriteExtensions
{
	/// <summary>
	/// Pure
	/// </summary>
	/// <param name="sprite"></param>
	/// <param name="atlas"></param>
	/// <param name="sAtlasRect"></param>
	/// <param name="sAtlasPivot"></param>
	/// <param name="sAtlasBorder"></param>
	public static void GetSpriteCoordinatesInAtlas(this ISprite sprite, ISpriteAtlas? atlas, out RectangleF sAtlasRect, out Vector2 sAtlasPivot, out Vector4 sAtlasBorder)
	{
		// sprite values are relative to original image (image, it was created from).
		// since atlas shuffle and crop sprite images, we need to recalculate those values.
		// if sprite doesn't belong to an atlas, consider its image as single sprite atlas

		Vector2 cropBotLeft;
		if (atlas is not null && sprite.Has_RenderDataKey() && atlas.RenderDataMap.TryGetValue(sprite.RenderDataKey, out ISpriteAtlasData? atlasData))
		{
			sAtlasRect = atlasData.TextureRect.CastToStruct();
			cropBotLeft = (Vector2)atlasData.TextureRectOffset;
		}
		else
		{
			sAtlasRect = sprite.RD.TextureRect.CastToStruct();
			cropBotLeft = (Vector2)sprite.RD.TextureRectOffset;
		}

		Vector2 sizeDelta = sprite.Rect.Size() - sAtlasRect.Size();
		Vector2 cropTopRight = new Vector2(sizeDelta.X - cropBotLeft.X, sizeDelta.Y - cropBotLeft.Y);

		Vector2 pivot = sprite.GetPivot();
		Vector2 pivotPosition = new Vector2(pivot.X * sprite.Rect.Size().X, pivot.Y * sprite.Rect.Size().Y);
		Vector2 aAtlasPivotPosition = pivotPosition - cropBotLeft;
		sAtlasPivot = new Vector2(aAtlasPivotPosition.X / sAtlasRect.Size().X, aAtlasPivotPosition.Y / sAtlasRect.Size().Y);

		if (sprite.Has_Border())
		{
			float borderL = sprite.Border.X == 0.0f ? 0.0f : sprite.Border.X - cropBotLeft.X;
			float borderB = sprite.Border.Y == 0.0f ? 0.0f : sprite.Border.Y - cropBotLeft.Y;
			float borderR = sprite.Border.Z == 0.0f ? 0.0f : sprite.Border.Z - cropTopRight.X;
			float borderT = sprite.Border.W == 0.0f ? 0.0f : sprite.Border.W - cropTopRight.Y;
			sAtlasBorder = new Vector4(borderL, borderB, borderR, borderT);
		}
		else
		{
			sAtlasBorder = default;
		}
	}

	/// <summary>
	/// Normalized pivot in the sprite's original rect. Versions without m_Pivot store it as an offset from the rect center.
	/// </summary>
	public static Vector2 GetPivot(this ISprite sprite)
	{
		if (sprite.Has_Pivot())
		{
			return (Vector2)sprite.Pivot;
		}
		Vector2 size = sprite.Rect.Size();
		return (size / 2.0f + (Vector2)sprite.Offset) / size;
	}

	/// <summary>
	/// Whole-pixel size of the sprite's original canvas, shared by the restored image and its importer metadata.
	/// </summary>
	public static Size GetSourceCanvasSize(this ISprite sprite)
	{
		return new Size((int)Math.Round(sprite.Rect.Width), (int)Math.Round(sprite.Rect.Height));
	}

	public static ITexture2D? TryGetTexture(this ISprite sprite)
	{
		return sprite.RD.Texture.TryGetAsset(sprite.Collection);
	}
}
