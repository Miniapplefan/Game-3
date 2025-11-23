using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class MarchingCubesGenerator : MonoBehaviour
{
	[Header("Grid Settings")]
	public float resolution = 0.1f; // Distance between vertices
	public float isolevel = 0.5f;  // Threshold for the marching cubes algorithm

	private Vector3[,,] vertexMatrix;
	private float[,,] scalarField;
	private List<Vector3> vertices;     // List of vertices for the mesh
	private List<int> triangles;        // List of triangles for the mesh
	private MeshFilter meshFilter;

	private MeshCollider meshCollider;

	private int damageCount;

	private void Start()
	{

		meshFilter = GetComponent<MeshFilter>();
		meshCollider = GetComponent<MeshCollider>();
		vertices = new List<Vector3>();
		triangles = new List<int>();

		GenerateVertexMatrix();
		GenerateScalarField();
		MarchCubes();
		SetMesh();

		Mesh mesh = GetComponent<MeshFilter>().mesh;
		Vector3[] verts = mesh.vertices;
		Vector2[] uvs = new Vector2[verts.Length];

		for (int i = 0; i < uvs.Length; i++)
		{
			uvs[i] = new Vector2(vertices[i].x, vertices[i].z);
		}
		mesh.uv = uvs;
	}

	private void GenerateVertexMatrix()
	{
		// Calculate object dimensions in local space
		Vector3 dimensions = transform.localScale;

		// Determine the number of vertices needed along each axis
		int xVertices = Mathf.CeilToInt(dimensions.x / resolution) + 2;
		int yVertices = Mathf.CeilToInt(dimensions.y / resolution) + 2;
		int zVertices = Mathf.CeilToInt(dimensions.z / resolution) + 2;

		//Debug.Log(xVertices);

		vertexMatrix = new Vector3[xVertices, yVertices, zVertices];

		// Generate the vertex matrix
		for (int x = 0; x < xVertices; x++)
		{
			for (int y = 0; y < yVertices; y++)
			{
				for (int z = 0; z < zVertices; z++)
				{
					vertexMatrix[x, y, z] = new Vector3(
						x * resolution / dimensions.x - 0.5f,
						y * resolution / dimensions.y - 0.5f,
						z * resolution / dimensions.z - 0.5f
					);
				}
			}
		}
	}

	private void GenerateScalarField()
	{
		// Match the size of vertexMatrix
		scalarField = new float[
			vertexMatrix.GetLength(0),
			vertexMatrix.GetLength(1),
			vertexMatrix.GetLength(2)
		];

		// Generate scalar field (e.g., random values or procedural noise)
		for (int x = 0; x < scalarField.GetLength(0); x++)
		{
			for (int y = 0; y < scalarField.GetLength(1); y++)
			{
				for (int z = 0; z < scalarField.GetLength(2); z++)
				{
					// //Random.Range(0f, 1f)
					// // scalarField[x, y, z] = Random.Range(0f, 1f);
					// //&& y > scalarField.GetLength(1) / 4 && y < scalarField.GetLength(1) / 4 * 3 && z > scalarField.GetLength(2) / 4 && z < scalarField.GetLength(2) / 4 * 3)
					if (x > 0 && x < scalarField.GetLength(0) - 1 && y > 0 && y < scalarField.GetLength(1) - 1 && z > 0 && z < scalarField.GetLength(2) - 1)
					{
						scalarField[x, y, z] = 1f;
					}
					else
					{
						scalarField[x, y, z] = 0f;
					}
					// scalarField[x, y, z] = 1;
				}
			}
		}
	}

	private void SetMesh()
	{
		Mesh mesh = new Mesh();

		// Debug.Log(vertexMatrix.Length);
		// Debug.Log(vertices.ToArray().Length);
		//Debug.Log(triangles.ToArray().Length);

		//List<T> noDupes = withDupes.Distinct().ToList();

		mesh.vertices = vertices.ToArray();
		mesh.triangles = triangles.Distinct().ToArray();
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();

		// Update MeshCollider for collision
		meshCollider.sharedMesh = mesh;
		meshFilter.mesh = mesh;
	}

	private int GetConfigIndex(float[] cubeCorners)
	{
		int configIndex = 0;

		for (int i = 0; i < 8; i++)
		{
			if (cubeCorners[i] < isolevel)
			{
				configIndex |= 1 << i;
			}
		}

		return configIndex;
	}

	private void MarchCubes()
	{
		vertices.Clear();
		triangles.Clear();

		// int xSize = scalarField.GetLength(0) / (int)transform.localScale.x - 1;
		// int ySize = scalarField.GetLength(1) / (int)transform.localScale.y - 1;
		// int zSize = scalarField.GetLength(2) / (int)transform.localScale.z - 1;
		int xSize = scalarField.GetLength(0) - 1;
		int ySize = scalarField.GetLength(1) - 1;
		int zSize = scalarField.GetLength(2) - 1;

		for (int x = 0; x < xSize; x++)
		{
			for (int y = 0; y < ySize; y++)
			{
				for (int z = 0; z < zSize; z++)
				{
					float[] cubeCorners = new float[8];

					for (int i = 0; i < 8; i++)
					{
						Vector3Int corner = new Vector3Int(x, y, z) + MarchingTable.Corners[i];
						cubeCorners[i] = scalarField[corner.x, corner.y, corner.z];
					}
					//MarchCube(new Vector3(vertexMatrix[x, y, z].x, vertexMatrix[x, y, z].y, vertexMatrix[x, y, z].z), cubeCorners);

					MarchCube(transform.TransformPoint(new Vector3(vertexMatrix[x, y, z].x, vertexMatrix[x, y, z].y, vertexMatrix[x, y, z].z)), cubeCorners);
				}
			}
		}
	}

	private void MarchCube(Vector3 position, float[] cubeCorners)
	{
		int configIndex = GetConfigIndex(cubeCorners);

		if (configIndex == 0 || configIndex == 255)
		{
			return;
		}

		int edgeIndex = 0;
		for (int t = 0; t < 5; t++)
		{
			for (int v = 0; v < 3; v++)
			{
				int triTableValue = MarchingTable.Triangles[configIndex, edgeIndex];

				if (triTableValue == -1)
				{
					return;
				}

				Vector3 edgeStart = transform.InverseTransformPoint(position + MarchingTable.Edges[triTableValue, 0]);
				Vector3 edgeEnd = transform.InverseTransformPoint(position + MarchingTable.Edges[triTableValue, 1]);

				Vector3 vertex = (edgeStart + edgeEnd) / 2;

				vertices.Add(vertex);
				triangles.Add(vertices.Count - 1);

				edgeIndex++;
			}
		}
	}

	// public void TakeDamage(Vector3 damagePosition, float radius)
	// {
	// 	for (int x = 0; x < scalarField.GetLength(0); x++)
	// 	{
	// 		for (int y = 0; y < scalarField.GetLength(1); y++)
	// 		{
	// 			for (int z = 0; z < scalarField.GetLength(2); z++)
	// 			{
	// 				Vector3 worldVertex = transform.TransformPoint(vertexMatrix[x, y, z]);
	// 				float distance = Vector3.Distance(worldVertex, damagePosition);

	// 				if (distance <= radius)
	// 				{
	// 					scalarField[x, y, z] = 0;
	// 				}
	// 			}
	// 		}
	// 	}
	// 	MarchCubes();
	// 	SetMesh();
	// }

	// public void TakeDamage(Vector3 damagePosition, int radius)
	// {
	// 	// Create a priority queue with a tiebreaker
	// 	SortedList<float, Vector3Int> closestPoints = new SortedList<float, Vector3Int>();

	// 	// Iterate through the scalar field to calculate distances
	// 	for (int x = 0; x < scalarField.GetLength(0); x++)
	// 	{
	// 		for (int y = 0; y < scalarField.GetLength(1); y++)
	// 		{
	// 			for (int z = 0; z < scalarField.GetLength(2); z++)
	// 			{
	// 				Vector3 worldVertex = transform.TransformPoint(vertexMatrix[x, y, z]);
	// 				float distance = Vector3.Distance(worldVertex, damagePosition);

	// 				// Add a tiebreaker to the distance to ensure unique keys
	// 				float key = distance + (x * 0.0001f) + (y * 0.00001f) + (z * 0.000001f);

	// 				if (closestPoints.Count < radius)
	// 				{
	// 					closestPoints.Add(key, new Vector3Int(x, y, z));
	// 				}
	// 				else if (distance < closestPoints.Keys[closestPoints.Keys.Count - 1])
	// 				{
	// 					// Replace the furthest point if the current point is closer
	// 					closestPoints.RemoveAt(closestPoints.Keys.Count - 1);
	// 					closestPoints.Add(key, new Vector3Int(x, y, z));
	// 				}
	// 			}
	// 		}
	// 	}

	// 	// Adjust the scalar field for the closest points
	// 	bool allZero = true;
	// 	foreach (var point in closestPoints.Values)
	// 	{
	// 		if (scalarField[point.x, point.y, point.z] > 0)
	// 		{
	// 			scalarField[point.x, point.y, point.z] = 0;
	// 			// damageCount++;
	// 			// Debug.Log(damageCount);
	// 			allZero = false;
	// 		}
	// 	}

	// 	// If all the closest points are already zero, find at least one point with a value of 1
	// 	if (allZero)
	// 	{
	// 		Vector3Int closestNonZeroPoint = default;
	// 		float closestDistance = float.MaxValue;

	// 		for (int x = 0; x < scalarField.GetLength(0); x++)
	// 		{
	// 			for (int y = 0; y < scalarField.GetLength(1); y++)
	// 			{
	// 				for (int z = 0; z < scalarField.GetLength(2); z++)
	// 				{
	// 					if (scalarField[x, y, z] > 0)
	// 					{
	// 						Vector3 worldVertex = transform.TransformPoint(vertexMatrix[x, y, z]);
	// 						float distance = Vector3.Distance(worldVertex, damagePosition);

	// 						if (distance < closestDistance)
	// 						{
	// 							closestDistance = distance;
	// 							closestNonZeroPoint = new Vector3Int(x, y, z);
	// 						}
	// 					}
	// 				}
	// 			}
	// 		}

	// 		// Set the closest non-zero point to zero if found
	// 		if (closestDistance < float.MaxValue)
	// 		{
	// 			scalarField[closestNonZeroPoint.x, closestNonZeroPoint.y, closestNonZeroPoint.z] = 0;
	// 			// damageCount++;
	// 			// Debug.Log(damageCount);
	// 		}
	// 	}

	// 	// Regenerate the mesh to reflect the changes
	// 	MarchCubes();
	// 	SetMesh();
	// }

	public void TakeDamage(Vector3 damagePosition, int radius)
	{
		// Precompute world positions if possible to avoid repeated TransformPoint calls
		bool allZero = true;
		float closestNonZeroDistance = float.MaxValue;
		Vector3Int closestNonZeroPoint = default;

		// Use a priority queue (min-heap) for closest points
		PriorityQueue<float, Vector3Int> closestPoints = new PriorityQueue<float, Vector3Int>(radius);

		for (int x = 0; x < scalarField.GetLength(0); x++)
		{
			for (int y = 0; y < scalarField.GetLength(1); y++)
			{
				for (int z = 0; z < scalarField.GetLength(2); z++)
				{
					Vector3 worldVertex = transform.TransformPoint(vertexMatrix[x, y, z]);
					float distance = Vector3.Distance(worldVertex, damagePosition);

					// Apply a tiebreaker to the priority based on x, y, z coordinates to avoid duplicate keys
					float priority = distance + (x * 0.0001f) + (y * 0.00001f) + (z * 0.000001f);

					// Add the point to the priority queue, considering the radius
					closestPoints.TryAdd(priority, new Vector3Int(x, y, z), radius);
					// Track the closest non-zero point
					if (scalarField[x, y, z] > 0 && distance < closestNonZeroDistance)
					{
						closestNonZeroDistance = distance;
						closestNonZeroPoint = new Vector3Int(x, y, z);
					}
				}
			}
		}

		// Adjust scalar field for the closest points
		foreach (var point in closestPoints.Values)
		{
			if (scalarField[point.x, point.y, point.z] > 0)
			{
				scalarField[point.x, point.y, point.z] = 0;
				damageCount++;
				//				Debug.Log(damageCount);
				allZero = false;
			}
		}

		// If all points are zero, set the closest non-zero point to zero
		if (allZero && closestNonZeroDistance < float.MaxValue)
		{
			scalarField[closestNonZeroPoint.x, closestNonZeroPoint.y, closestNonZeroPoint.z] = 0;
			damageCount++;
			//			Debug.Log(damageCount);
		}

		// Regenerate the mesh to reflect changes
		MarchCubes();
		SetMesh();
	}

	// Helper priority queue class
	public class PriorityQueue<TPriority, TValue> where TPriority : System.IComparable<TPriority>
	{
		private SortedList<TPriority, TValue> queue = new SortedList<TPriority, TValue>();
		private int maxSize;

		public PriorityQueue(int maxSize)
		{
			this.maxSize = maxSize;
		}

		public void TryAdd(TPriority priority, TValue value, int radius)
		{
			if (queue.Count < maxSize)
			{
				queue.Add(priority, value);
			}
			else if (priority.CompareTo(queue.Keys[queue.Keys.Count - 1]) < 0)
			{
				queue.RemoveAt(queue.Keys.Count - 1);
				queue.Add(priority, value);
			}
		}

		public IEnumerable<TValue> Values => queue.Values;
	}

	void OnDrawGizmos()
	{
		if (vertexMatrix == null) return;

		// Visualize the vertex grid and scalar field for debugging
		// Gizmos.color = Color.red;
		// foreach (var vertex in vertexMatrix)
		// {
		// 	Gizmos.DrawSphere(transform.TransformPoint(vertex), 0.02f);
		// }
		// Gizmos.color = Color.cyan;
		// foreach (var vertex in vertices)
		// {
		// 	Gizmos.DrawSphere(transform.TransformPoint(vertex), 0.02f);
		// }
		foreach (var vertex in vertexMatrix)
		{
			if (scalarField[(int)vertex.x, (int)vertex.y, (int)vertex.z] == 0f)
			{
				Gizmos.color = Color.red;
				Gizmos.DrawSphere(transform.TransformPoint(vertex), 0.02f);
			}
			else
			{
				Gizmos.color = Color.cyan;
				Gizmos.DrawSphere(transform.TransformPoint(vertex), 0.02f);
			}
		}
		// Gizmos.color = Color.green;
		// foreach (var vertex in vertices)
		// {
		// 	Vector3 worldVertex = transform.TransformPoint(vertex);
		// 	Gizmos.DrawLine(worldVertex, worldVertex + transform.TransformDirection(Vector3.up * 0.1f)); // Adjust normal length
		// }

		// if (meshFilter == null || meshFilter.mesh == null) return;

		// Mesh mesh = meshFilter.mesh;
		// Vector3[] v = mesh.vertices;
		// Vector3[] normals = mesh.normals;

		// Gizmos.color = Color.green;
		// for (int i = 0; i < v.Length; i++)
		// {
		// 	Vector3 worldVertex = transform.TransformPoint(v[i]);
		// 	Gizmos.DrawLine(worldVertex, worldVertex + transform.TransformDirection(normals[i]) * 0.1f);
		// }
	}
}