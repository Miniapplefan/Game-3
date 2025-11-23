//ProceduralMeshDeformerWithSubdivision
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshCollider))]
public class AdvancedProceduralMeshDeformer : MonoBehaviour
{
	public float subdivisionStep = 0.1f; // Step size for subdivisions
	private Mesh debugMesh; // Mesh for debugging visualization

	void Start()
	{
		// Get the MeshFilter from the GameObject
		MeshFilter meshFilter = GetComponent<MeshFilter>();
		if (meshFilter == null || meshFilter.sharedMesh == null)
		{
			Debug.LogError("No MeshFilter or Mesh found on the GameObject. Please add one.");
			return;
		}

		// Retrieve the mesh bounds and calculate the scaled dimensions
		Mesh mesh = meshFilter.sharedMesh;
		Vector3 unscaledDimensions = mesh.bounds.size;
		Vector3 scaledDimensions = Vector3.Scale(unscaledDimensions, transform.localScale);
		// Debug.Log("unscale " + unscaledDimensions);
		// Debug.Log("scale " + scaledDimensions);

		debugMesh = GenerateSubdividedCube(scaledDimensions, subdivisionStep);
		GetComponent<MeshFilter>().mesh = debugMesh;
	}

	Mesh GenerateSubdividedCube(Vector3 dimensions, float step)
	{
		Mesh mesh = new Mesh();
		List<Vector3> vertices = new List<Vector3>();
		List<int> triangles = new List<int>();

		// Define the face directions and their corresponding axes
		Vector3[] faceCenters = {
			new Vector3(0, 0, dimensions.z/transform.localScale.z/2),  // Forward
			new Vector3(0, 0, -dimensions.z/transform.localScale.z/2), // Back
			new Vector3(-dimensions.x/transform.localScale.x/2, 0, 0), // Left
			new Vector3(dimensions.x/transform.localScale.x/2, 0, 0),  // Right
			new Vector3(0, dimensions.y/transform.localScale.y/2, 0),  // Up
			new Vector3(0, -dimensions.y/transform.localScale.y/2, 0)  // Down
		};

		Vector3[] faceAxes1 = {
			Vector3.right,  // Forward face
			Vector3.right,  // Back face
			Vector3.up,     // Left face
			Vector3.up,     // Right face
			Vector3.right,  // Up face
			Vector3.right   // Down face
		};

		Vector3[] faceAxes2 = {
			Vector3.up,     // Forward face
			Vector3.up,     // Back face
			Vector3.forward,// Left face
			Vector3.forward,// Right face
			Vector3.forward,// Up face
			Vector3.forward // Down face
		};

		Vector3[] faceDimensions = {
			new Vector3(dimensions.x, dimensions.y, 0), // Forward
			new Vector3(dimensions.x, dimensions.y, 0), // Back
			new Vector3(0, dimensions.y, dimensions.z), // Left
			new Vector3(0, dimensions.y, dimensions.z), // Right
			new Vector3(dimensions.x, 0, dimensions.z), // Up
			new Vector3(dimensions.x, 0, dimensions.z)  // Down
		};

		for (int faceIndex = 0; faceIndex < 6; faceIndex++)
		{
			Vector3 center = faceCenters[faceIndex];
			Vector3 axis1 = faceAxes1[faceIndex];
			Vector3 axis2 = faceAxes2[faceIndex];
			Vector3 faceDim = faceDimensions[faceIndex];

			// Calculate the actual lengths along each axis
			float length1 = Vector3.Dot(faceDim, axis1.normalized);
			float length2 = Vector3.Dot(faceDim, axis2.normalized);

			// Calculate number of steps needed to cover each length
			int count1 = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(length1) / (step / length1) / length1));
			int count2 = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(length2) / (step / length2) / length2));

			//Debug.Log(faceIndex + " :" + count1 + ", " + count2);

			// Calculate start position for vertex placement
			Vector3 start = center - (axis1 * length1 / 2) / length1 - (axis2 * length2 / 2) / length2;
			int baseIndex = vertices.Count;

			// Generate grid of vertices
			for (int i = 0; i <= count1; i++)
			{
				float t1 = i / (float)count1; // Normalized position along first axis

				for (int j = 0; j <= count2; j++)
				{
					float t2 = j / (float)count2; // Normalized position along second axis

					// Calculate vertex position using normalized coordinates
					Vector3 vertex = start + (axis1 * t1) + (axis2 * t2);
					vertices.Add(vertex);

					if (i < count1 && j < count2)
					{
						int i0 = baseIndex + i * (count2 + 1) + j;
						int i1 = i0 + 1;
						int i2 = i0 + (count2 + 1);
						int i3 = i2 + 1;



						// Add triangles with correct winding order
						//0, 3, 5
						if (faceIndex == 0 || faceIndex == 3 || faceIndex == 5) // Forward 0, Left 2, Up 4faces
						{
							triangles.AddRange(new int[] { i0, i2, i1, i1, i2, i3 });
						}
						else // Back, Right, Down faces // 1, 2, 4
						{
							triangles.AddRange(new int[] { i0, i1, i2, i1, i3, i2 });
						}
					}
				}
			}
		}

		mesh.vertices = vertices.ToArray();
		mesh.triangles = triangles.ToArray();
		mesh.RecalculateNormals();
		return mesh;
	}

	public void TakeDamage(Vector3 damagePosition, float radius, float displacementAmount)
	{
		if (debugMesh == null) return;

		Vector3[] vertices = debugMesh.vertices;
		Vector3[] normals = debugMesh.normals;

		for (int i = 0; i < vertices.Length; i++)
		{
			Vector3 worldVertex = transform.TransformPoint(vertices[i]);
			float distance = Vector3.Distance(worldVertex, damagePosition);

			if (distance <= radius)
			{
				// Calculate displacement based on distance (optional: add falloff)
				float falloff = Mathf.Clamp01(1 - (distance / radius));
				Vector3 displacement = normals[i] * (-displacementAmount * falloff);

				// Apply displacement to the vertex
				vertices[i] += displacement;
			}
		}

		// Update the mesh with modified vertices
		debugMesh.vertices = vertices;
		debugMesh.RecalculateNormals();
		debugMesh.RecalculateBounds();

		// Update MeshCollider for collision
		GetComponent<MeshCollider>().sharedMesh = debugMesh;
	}

	// public void TakeDamage(Vector3 damagePosition, float radius)
	// {
	// 	if (debugMesh == null) return;

	// 	// Get the current mesh data
	// 	Vector3[] vertices = debugMesh.vertices;
	// 	int[] triangles = debugMesh.triangles;

	// 	HashSet<int> verticesToRemove = new HashSet<int>();

	// 	// Identify vertices within the damage radius
	// 	for (int i = 0; i < vertices.Length; i++)
	// 	{
	// 		Vector3 worldVertex = transform.TransformPoint(vertices[i]);
	// 		if (Vector3.Distance(worldVertex, damagePosition) <= radius)
	// 		{
	// 			verticesToRemove.Add(i);
	// 		}
	// 	}

	// 	// Create a map for new vertex indices
	// 	Dictionary<int, int> indexMap = new Dictionary<int, int>();
	// 	List<Vector3> finalVertices = new List<Vector3>();
	// 	int newIndex = 0;

	// 	// Rebuild the vertex list and create the index map
	// 	for (int i = 0; i < vertices.Length; i++)
	// 	{
	// 		if (!verticesToRemove.Contains(i))
	// 		{
	// 			finalVertices.Add(vertices[i]);
	// 			indexMap[i] = newIndex++;
	// 		}
	// 	}

	// 	// Rebuild the triangle list
	// 	List<int> finalTriangles = new List<int>();
	// 	for (int i = 0; i < triangles.Length; i += 3)
	// 	{
	// 		// Check if all vertices of the triangle exist in the index map
	// 		int v0 = triangles[i];
	// 		int v1 = triangles[i + 1];
	// 		int v2 = triangles[i + 2];

	// 		if (indexMap.ContainsKey(v0) && indexMap.ContainsKey(v1) && indexMap.ContainsKey(v2))
	// 		{
	// 			finalTriangles.Add(indexMap[v0]);
	// 			finalTriangles.Add(indexMap[v1]);
	// 			finalTriangles.Add(indexMap[v2]);
	// 		}
	// 	}

	// 	// Update the mesh
	// 	debugMesh.Clear();
	// 	debugMesh.vertices = finalVertices.ToArray();
	// 	debugMesh.triangles = finalTriangles.ToArray();
	// 	debugMesh.RecalculateNormals();
	// 	debugMesh.RecalculateBounds();

	// 	// Update the collider
	// 	GetComponent<MeshCollider>().sharedMesh = debugMesh;
	// }

	void OnDrawGizmos()
	{
		if (debugMesh == null) return;

		Gizmos.color = Color.red;

		Vector3[] vertices = debugMesh.vertices;
		int[] triangles = debugMesh.triangles;

		// Draw lines along the edges of the triangles
		for (int i = 0; i < triangles.Length; i += 3)
		{
			Vector3 v0 = transform.TransformPoint(vertices[triangles[i]]);
			Vector3 v1 = transform.TransformPoint(vertices[triangles[i + 1]]);
			Vector3 v2 = transform.TransformPoint(vertices[triangles[i + 2]]);

			Gizmos.DrawLine(v0, v1);
			Gizmos.DrawLine(v1, v2);
			Gizmos.DrawLine(v2, v0);
		}
	}
}