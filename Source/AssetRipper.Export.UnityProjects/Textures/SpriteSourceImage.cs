using AssetRipper.Export.Modules.Textures;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Enums;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.TextureDecoder.Rgb.Formats;
using System.Buffers.Binary;
using System.Numerics;

namespace AssetRipper.Export.UnityProjects.Textures;

public static class SpriteSourceImage
{
	public static DirectBitmap Restore(ISprite sprite, DirectBitmap packed)
	{
		var canvas = sprite.GetSourceCanvasSize();
		int width = canvas.Width;
		int height = canvas.Height;
		var restored = new DirectBitmap<ColorRGBA<byte>, byte>(width, height);
		Vector2 pivot = sprite.GetPivot() * sprite.Rect.Size();
		float scaleX = 1, scaleY = 1;
		float offsetX = sprite.RD.TextureRect.X - sprite.RD.TextureRectOffset.X;
		float offsetY = sprite.RD.TextureRect.Y - sprite.RD.TextureRectOffset.Y;
		if (sprite.RD.Has_UvTransform())
		{
			// UvTransform also carries the atlas scale; older versions are always packed 1:1.
			scaleX = sprite.RD.UvTransform.X / sprite.PixelsToUnits;
			scaleY = sprite.RD.UvTransform.Z / sprite.PixelsToUnits;
			offsetX = sprite.RD.UvTransform.Y - pivot.X * scaleX;
			offsetY = sprite.RD.UvTransform.W - pivot.Y * scaleY;
		}
		if (sprite.RD.PackingRotation is SpritePackingRotation.FlipHorizontal or SpritePackingRotation.Rotate180)
		{
			scaleX = -scaleX;
			offsetX = 2 * sprite.RD.TextureRect.X + sprite.RD.TextureRect.Width - offsetX;
		}
		if (sprite.RD.PackingRotation is SpritePackingRotation.FlipVertical or SpritePackingRotation.Rotate180)
		{
			scaleY = -scaleY;
			offsetY = 2 * sprite.RD.TextureRect.Y + sprite.RD.TextureRect.Height - offsetY;
		}
		bool[] mask = GetMask(sprite, width, height, pivot);
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				if (!mask[y * width + x]) continue;
				double sx = (x + 0.5) * scaleX + offsetX - 0.5;
				double sy = (y + 0.5) * scaleY + offsetY - 0.5;
				int destination = ((height - 1 - y) * width + x) * 4;
				Sample(packed, sx, sy, restored.Bits.Slice(destination, 4));
			}
		}
		return restored;
	}

	private static void Sample(DirectBitmap packed, double x, double y, Span<byte> destination)
	{
		if (Math.Abs(x - Math.Round(x)) < .003) x = Math.Round(x);
		if (Math.Abs(y - Math.Round(y)) < .003) y = Math.Round(y);
		int ix = (int)Math.Floor(x);
		int iy = (int)Math.Floor(y);
		double fx = x - ix;
		double fy = y - iy;
		if (Math.Abs(fx) < .003 && Math.Abs(fy) < .003)
		{
			if ((uint)ix < packed.Width && (uint)iy < packed.Height)
				packed.Bits.Slice(((packed.Height - 1 - iy) * packed.Width + ix) * 4, 4).CopyTo(destination);
			return;
		}
		for (int channel = 0; channel < 4; channel++)
		{
			double value = 0;
			for (int dy = 0; dy < 2; dy++)
			{
				for (int dx = 0; dx < 2; dx++)
				{
					int sx = Math.Clamp(ix + dx, 0, packed.Width - 1);
					int sy = Math.Clamp(iy + dy, 0, packed.Height - 1);
					double sample = packed.Bits[((packed.Height - 1 - sy) * packed.Width + sx) * 4 + channel] / 255.0;
					if (channel < 3) sample = sample <= .04045 ? sample / 12.92 : Math.Pow((sample + .055) / 1.055, 2.4);
					value += sample * (dx == 0 ? 1 - fx : fx) * (dy == 0 ? 1 - fy : fy);
				}
			}
			if (channel < 3) value = value <= .0031308 ? value * 12.92 : 1.055 * Math.Pow(value, 1 / 2.4) - .055;
			destination[channel] = (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
		}
	}

	private static bool[] GetMask(ISprite sprite, int width, int height, Vector2 pivot)
	{
		bool[] mask = new bool[width * height];
		Vector3[]? vertices;
		if (sprite.RD.Has_VertexData())
		{
			VertexDataBlob.Create(sprite.RD.VertexData, sprite.Collection.Version, sprite.Collection.EndianType)
				.ReadData(out vertices, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);
		}
		else
		{
			vertices = sprite.RD.Vertices?.Select(v => (Vector3)v.Pos).ToArray();
		}
		int[] indices = sprite.RD.Has_IndexBuffer()
			? Enumerable.Range(0, sprite.RD.IndexBuffer.Length / 2)
				.Select(i => (int)BinaryPrimitives.ReadUInt16LittleEndian(sprite.RD.IndexBuffer.AsSpan(i * 2, 2)))
				.ToArray()
			: sprite.RD.Indices.Select(i => (int)i).ToArray();
		// Without a usable mesh, keep the whole canvas rather than exporting a transparent image.
		if (vertices is null || vertices.Length == 0 || indices.Length < 3)
		{
			System.Array.Fill(mask, true);
			return mask;
		}
		var points = vertices.Select(v => new Vector2(v.X, v.Y) * sprite.PixelsToUnits + pivot).ToArray();
		for (int i = 0; i + 2 < indices.Length; i += 3)
		{
			Vector2 a = points[indices[i]], b = points[indices[i + 1]], c = points[indices[i + 2]];
			int x0 = Math.Clamp((int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X))) - 2, 0, width - 1);
			int y0 = Math.Clamp((int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))) - 2, 0, height - 1);
			int x1 = Math.Clamp((int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))) + 2, 0, width - 1);
			int y1 = Math.Clamp((int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))) + 2, 0, height - 1);
			float direction = Math.Sign(Cross(b - a, c - a));
			if (direction == 0)
			{
				// A degenerate triangle covers no pixels; the edge tolerance below would accept its whole bounding box.
				continue;
			}
			for (int y = y0; y <= y1; y++)
			{
				for (int x = x0; x <= x1; x++)
				{
					Vector2 p = new(x + .5f, y + .5f);
					if (Cross(b - a, p - a) * direction >= -1.5f * Vector2.Distance(a, b)
						&& Cross(c - b, p - b) * direction >= -1.5f * Vector2.Distance(b, c)
						&& Cross(a - c, p - c) * direction >= -1.5f * Vector2.Distance(c, a))
						mask[y * width + x] = true;
				}
			}
		}
		return mask;
	}

	private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}
