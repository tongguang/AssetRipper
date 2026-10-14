using AssetRipper.Numerics;
using System.Numerics;

namespace AssetRipper.SourceGenerated.Extensions;

public sealed class MeshOutlineGenerator
{
	public MeshOutlineGenerator(IReadOnlyList<Vector3> vertices, IReadOnlyList<Vector3i> triangles)
	{
		ArgumentNullException.ThrowIfNull(vertices);
		ArgumentNullException.ThrowIfNull(triangles);
		m_vertices = vertices;
		foreach (Vector3i triangle in triangles)
		{
			if (IsValidTriangle(triangle))
			{
				m_triangles.Add(triangle);
			}
		}
	}

	/// <summary>
	/// Every closed boundary of the mesh, including holes, in triangle winding order.
	/// </summary>
	public List<Vector2[]> GenerateOutlines()
	{
		HashSet<(int, int)> edges = new();
		foreach (Vector3i triangle in m_triangles)
		{
			edges.Add((triangle.X, triangle.Y));
			edges.Add((triangle.Y, triangle.Z));
			edges.Add((triangle.Z, triangle.X));
		}

		// An edge without its reverse belongs to one triangle only, so it lies on a boundary.
		Dictionary<int, List<int>> next = new();
		foreach ((int from, int to) in edges)
		{
			if (!edges.Contains((to, from)))
			{
				if (!next.TryGetValue(from, out List<int>? targets))
				{
					next.Add(from, targets = new());
				}
				targets.Add(to);
			}
		}

		List<Vector2[]> result = new();
		foreach ((int start, List<int> starts) in next)
		{
			while (starts.Count > 0)
			{
				List<Vector2> outline = new();
				int vertex = start;
				// Mesh data comes from game files; an open chain simply ends the outline.
				while (next.TryGetValue(vertex, out List<int>? targets) && targets.Count > 0)
				{
					outline.Add(m_vertices[vertex].AsVector2());
					vertex = targets[^1];
					targets.RemoveAt(targets.Count - 1);
					if (vertex == start)
					{
						break;
					}
				}
				result.Add(outline.ToArray());
			}
		}
		return result;
	}

	private static bool IsValidTriangle(Vector3i triangle)
	{
		return triangle.X != triangle.Y && triangle.X != triangle.Z && triangle.Y != triangle.Z;
	}

	private readonly IReadOnlyList<Vector3> m_vertices;
	private readonly List<Vector3i> m_triangles = new();
}
