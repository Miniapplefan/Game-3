using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; 

public class LevelGenerator : MonoBehaviour
{
	 // Configuration parameters
	[SerializeField] private int minSides = 6;
	[SerializeField] private int maxSides = 10;
	[SerializeField] private float minRoomSize = 5f;
	[SerializeField] private float corridorWidth = 3f;
	[SerializeField] private float wallHeight = 3f;

	// Lists to store room and corridor data
	private List<Room> rooms = new List<Room>();
	private List<Room> corridors = new List<Room>();

	// Data structures
	public class Room
	{
		public List<Vector2> vertices = new List<Vector2>();  // Initialize the list
		public List<Door> doors = new List<Door>();          // Initialize the list
		public bool isCorridor;
	}

	public class Door
	{
		public Vector2 position;
		public Vector2 direction;
		public float width = 2f;
	}
	
	public void Start()
	{
		GenerateLevel();
	}
	
	 public void GenerateLevel()
	{
		// Clear existing rooms and corridors
		rooms.Clear();
		corridors.Clear();

		// Step 1: Generate initial irregular polygon
		List<Vector2> initialPolygon = GenerateIrregularPolygon();
		
		// Step 2: Perform BSP on the polygon
		List<List<Vector2>> bspRegions = PerformBSP(initialPolygon);
		
		// Step 3: Generate corridors along BSP lines
		GenerateCorridors(bspRegions);
		
		// Step 4: Subdivide remaining spaces into rooms
		SubdivideIntoRooms();
		
		// Step 5: Place doors
		PlaceDoors();
		
		// Step 6: Generate 3D geometry
		Generate3DGeometry();
	}
	
	private float CalculatePolygonArea(List<Vector2> vertices)
	{
		float area = 0;
		for (int i = 0; i < vertices.Count; i++)
		{
			Vector2 current = vertices[i];
			Vector2 next = vertices[(i + 1) % vertices.Count];
			area += current.x * next.y - next.x * current.y;
		}
		return Mathf.Abs(area) / 2;
	}

	private List<Vector2> OrderVerticesClockwise(List<Vector2> vertices)
	{
		// Find the center point
		Vector2 center = vertices.Aggregate(Vector2.zero, (acc, v) => acc + v) / vertices.Count;

		// Sort vertices by angle around center
		return vertices.OrderBy(v => {
			Vector2 dir = (v - center).normalized;
			return Mathf.Atan2(dir.y, dir.x);
		}).ToList();
	}

	private void SplitPolygon(List<Vector2> polygon, bool vertical, float splitPos, 
							out List<Vector2> region1, out List<Vector2> region2)
	{
		region1 = new List<Vector2>();
		region2 = new List<Vector2>();

		// Implementation of polygon splitting algorithm
		for (int i = 0; i < polygon.Count; i++)
		{
			Vector2 current = polygon[i];
			Vector2 next = polygon[(i + 1) % polygon.Count];

			float currentPos = vertical ? current.x : current.y;
			float nextPos = vertical ? next.x : next.y;

			if (currentPos < splitPos)
				region1.Add(current);
			else
				region2.Add(current);

			// Check if the edge crosses the split line
			if ((currentPos < splitPos && nextPos > splitPos) ||
				(currentPos > splitPos && nextPos < splitPos))
			{
				// Calculate intersection point
				float t = (splitPos - currentPos) / (nextPos - currentPos);
				Vector2 intersection = vertical ?
					new Vector2(splitPos, Mathf.Lerp(current.y, next.y, t)) :
					new Vector2(Mathf.Lerp(current.x, next.x, t), splitPos);

				region1.Add(intersection);
				region2.Add(intersection);
			}
		}

		// Ensure both regions have properly ordered vertices
		region1 = OrderVerticesClockwise(region1);
		region2 = OrderVerticesClockwise(region2);
	}

	private bool AreEdgesAdjacent(Vector2 edge1Start, Vector2 edge1End, 
								Vector2 edge2Start, Vector2 edge2End)
	{
		// Calculate edge directions
		Vector2 edge1Dir = (edge1End - edge1Start).normalized;
		Vector2 edge2Dir = (edge2End - edge2Start).normalized;

		// Check if edges are parallel (or anti-parallel)
		if (Mathf.Abs(Vector2.Dot(edge1Dir, edge2Dir)) > 0.99f)
		{
			// Check if edges overlap
			float dist = Vector2.Distance(edge1Start, edge2Start);
			return dist < corridorWidth * 1.5f;
		}
		return false;
	}

	private List<List<Vector2>> SubdivideRegion(List<Vector2> region)
	{
		List<List<Vector2>> subRooms = new List<List<Vector2>>();
		Queue<List<Vector2>> regionsToSubdivide = new Queue<List<Vector2>>();
		regionsToSubdivide.Enqueue(region);

		while (regionsToSubdivide.Count > 0)
		{
			List<Vector2> currentRegion = regionsToSubdivide.Dequeue();
			
			float minX = currentRegion.Min(v => v.x);
			float maxX = currentRegion.Max(v => v.x);
			float minY = currentRegion.Min(v => v.y);
			float maxY = currentRegion.Max(v => v.y);
			
			float width = maxX - minX;
			float height = maxY - minY;
			float area = CalculatePolygonArea(currentRegion);

			if (area > minRoomSize * minRoomSize * 4)
			{
				bool splitVertical = width > height;
				float splitRatio = Random.Range(0.3f, 0.7f);
				float splitPos = splitVertical ? 
					minX + width * splitRatio :
					minY + height * splitRatio;

				List<Vector2> room1, room2;
				SplitPolygon(currentRegion, splitVertical, splitPos, out room1, out room2);

				if (HasCorridorAccess(room1))
					regionsToSubdivide.Enqueue(room1);
				else
					subRooms.Add(room1);

				if (HasCorridorAccess(room2))
					regionsToSubdivide.Enqueue(room2);
				else
					subRooms.Add(room2);
			}
			else
			{
				subRooms.Add(currentRegion);
			}
		}

		return subRooms;
	}

	private GameObject CreateFloorMesh(List<Vector2> vertices)
	{
		GameObject floor = new GameObject("Floor");
		MeshFilter meshFilter = floor.AddComponent<MeshFilter>();
		MeshRenderer meshRenderer = floor.AddComponent<MeshRenderer>();

		// Triangulate the polygon
		List<int> triangles = TriangulatePolygon(vertices);

		// Create the mesh
		Mesh mesh = new Mesh();
		
		// Convert Vector2 vertices to Vector3
		Vector3[] meshVertices = vertices.Select(v => new Vector3(v.x, 0, v.y)).ToArray();
		mesh.vertices = meshVertices;
		mesh.triangles = triangles.ToArray();

		// Generate UVs
		Vector2[] uvs = new Vector2[meshVertices.Length];
		for (int i = 0; i < meshVertices.Length; i++)
		{
			uvs[i] = new Vector2(meshVertices[i].x, meshVertices[i].z) / 10f;
		}
		mesh.uv = uvs;

		mesh.RecalculateNormals();
		mesh.RecalculateBounds();

		meshFilter.mesh = mesh;
		
		meshRenderer.material = new Material(Shader.Find("Standard"));
		meshRenderer.material.color = Color.gray;

		floor.AddComponent<MeshCollider>();

		return floor;
	}

	private GameObject CreateWallMesh(Vector2 start, Vector2 end)
	{
		GameObject wall = new GameObject("Wall");
		MeshFilter meshFilter = wall.AddComponent<MeshFilter>();
		MeshRenderer meshRenderer = wall.AddComponent<MeshRenderer>();

		Vector3[] vertices = new Vector3[]
		{
			new Vector3(start.x, 0, start.y),      // Bottom left
			new Vector3(end.x, 0, end.y),          // Bottom right
			new Vector3(end.x, wallHeight, end.y),  // Top right
			new Vector3(start.x, wallHeight, start.y) // Top left
		};

		int[] triangles = new int[]
		{
			0, 1, 2,
			0, 2, 3
		};

		float wallLength = Vector2.Distance(start, end);
		
		Vector2[] uvs = new Vector2[]
		{
			new Vector2(0, 0),
			new Vector2(wallLength/2, 0),
			new Vector2(wallLength/2, wallHeight),
			new Vector2(0, wallHeight)
		};

		Mesh mesh = new Mesh();
		mesh.vertices = vertices;
		mesh.triangles = triangles;
		mesh.uv = uvs;
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();

		meshFilter.mesh = mesh;
		meshRenderer.material = new Material(Shader.Find("Standard"));
		meshRenderer.material.color = Color.white;

		wall.AddComponent<MeshCollider>();
		
		return wall;
	}

	private GameObject CreateDoorPost(Vector3 position, float height)
	{
		GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
		post.transform.position = position + Vector3.up * (height/2);
		post.transform.localScale = new Vector3(0.2f, height, 0.2f);
		post.GetComponent<MeshRenderer>().material.color = Color.red;
		return post;
	}

	private bool HasCorridorAccess(List<Vector2> room)
	{
		for (int i = 0; i < room.Count; i++)
		{
			Vector2 start = room[i];
			Vector2 end = room[(i + 1) % room.Count];

			if (EdgeAdjacentToCorridor(start, end))
				return true;
		}
		return false;
	}

	private bool EdgeAdjacentToCorridor(Vector2 start, Vector2 end)
	{
		foreach (var corridor in corridors)
		{
			for (int i = 0; i < corridor.vertices.Count; i++)
			{
				Vector2 corrStart = corridor.vertices[i];
				Vector2 corrEnd = corridor.vertices[(i + 1) % corridor.vertices.Count];

				if (AreEdgesAdjacent(start, end, corrStart, corrEnd))
					return true;
			}
		}
		return false;
	}

	private List<int> TriangulatePolygon(List<Vector2> vertices)
	{
		List<int> triangles = new List<int>();
		List<int> remainingVertices = Enumerable.Range(0, vertices.Count).ToList();
		
		while (remainingVertices.Count > 3)
		{
			bool earFound = false;
			
			for (int i = 0; i < remainingVertices.Count; i++)
			{
				int prev = remainingVertices[(i - 1 + remainingVertices.Count) % remainingVertices.Count];
				int curr = remainingVertices[i];
				int next = remainingVertices[(i + 1) % remainingVertices.Count];

				if (IsEar(vertices, remainingVertices, prev, curr, next))
				{
					triangles.Add(prev);
					triangles.Add(curr);
					triangles.Add(next);
					remainingVertices.RemoveAt(i);
					earFound = true;
					break;
				}
			}
			
			if (!earFound)
			{
				Debug.LogError("Failed to triangulate polygon - no ear found");
				break;
			}
		}
		
		if (remainingVertices.Count == 3)
		{
			triangles.Add(remainingVertices[0]);
			triangles.Add(remainingVertices[1]);
			triangles.Add(remainingVertices[2]);
		}
		
		return triangles;
	}

	private bool IsEar(List<Vector2> vertices, List<int> remainingVertices, 
					  int prev, int curr, int next)
	{
		Vector2 p1 = vertices[prev];
		Vector2 p2 = vertices[curr];
		Vector2 p3 = vertices[next];

		if (!IsCounterClockwise(p1, p2, p3))
			return false;

		foreach (int index in remainingVertices)
		{
			if (index != prev && index != curr && index != next)
			{
				if (IsPointInTriangle(vertices[index], p1, p2, p3))
					return false;
			}
		}

		return true;
	}

	private bool IsCounterClockwise(Vector2 p1, Vector2 p2, Vector2 p3)
	{
		float area = (p2.x - p1.x) * (p3.y - p1.y) - 
					(p3.x - p1.x) * (p2.y - p1.y);
		return area > 0;
	}

	private bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
	{
		float area = 0.5f * (-b.y * c.x + a.y * (-b.x + c.x) + 
						   a.x * (b.y - c.y) + b.x * c.y);
		float s = 1 / (2 * area) * (a.y * c.x - a.x * c.y + 
				   (c.y - a.y) * p.x + (a.x - c.x) * p.y);
		float t = 1 / (2 * area) * (a.x * b.y - a.y * b.x + 
				   (a.y - b.y) * p.x + (b.x - a.x) * p.y);

		return s > 0 && t > 0 && (1 - s - t) > 0;
	}

	// Add missing methods
	private List<Vector2> GenerateIrregularPolygon()
	{
		List<Vector2> vertices = new List<Vector2>();
		int sides = Random.Range(minSides, maxSides + 1);
		float radius = minRoomSize * 2;

		for (int i = 0; i < sides; i++)
		{
			float angle = (2 * Mathf.PI * i) / sides;
			float randomRadius = radius * Random.Range(0.8f, 1.2f);
			vertices.Add(new Vector2(
				Mathf.Cos(angle) * randomRadius,
				Mathf.Sin(angle) * randomRadius
			));
		}

		return OrderVerticesClockwise(vertices);
	}

	private List<List<Vector2>> PerformBSP(List<Vector2> initialPolygon)
	{
		List<List<Vector2>> regions = new List<List<Vector2>>();
		Queue<List<Vector2>> regionsToProcess = new Queue<List<Vector2>>();
		regionsToProcess.Enqueue(initialPolygon);

		while (regionsToProcess.Count > 0)
		{
			List<Vector2> currentRegion = regionsToProcess.Dequeue();
			float area = CalculatePolygonArea(currentRegion);

			if (area > minRoomSize * minRoomSize * 4)
			{
				List<Vector2> region1, region2;
				bool vertical = Random.value > 0.5f;
				float splitPos = vertical ? 
					Random.Range(currentRegion.Min(v => v.x), currentRegion.Max(v => v.x)) :
					Random.Range(currentRegion.Min(v => v.y), currentRegion.Max(v => v.y));

				SplitPolygon(currentRegion, vertical, splitPos, out region1, out region2);
				regionsToProcess.Enqueue(region1);
				regionsToProcess.Enqueue(region2);
			}
			else
			{
				regions.Add(currentRegion);
			}
		}

		return regions;
	}

	private void GenerateCorridors(List<List<Vector2>> regions)
	{
		// For each pair of adjacent regions, create a corridor
		for (int i = 0; i < regions.Count; i++)
		{
			for (int j = i + 1; j < regions.Count; j++)
			{
				if (AreRegionsAdjacent(regions[i], regions[j]))
				{
					CreateCorridor(regions[i], regions[j]);
				}
			}
		}
	}

	private bool AreRegionsAdjacent(List<Vector2> region1, List<Vector2> region2)
	{
		// Check if any edges are close and parallel
		for (int i = 0; i < region1.Count; i++)
		{
			Vector2 start1 = region1[i];
			Vector2 end1 = region1[(i + 1) % region1.Count];

			for (int j = 0; j < region2.Count; j++)
			{
				Vector2 start2 = region2[j];
				Vector2 end2 = region2[(j + 1) % region2.Count];

				if (AreEdgesAdjacent(start1, end1, start2, end2))
				{
					return true;
				}
			}
		}
		return false;
	}

	private void CreateCorridor(List<Vector2> region1, List<Vector2> region2)
	{
		// Find closest points between regions
		Vector2 center1 = region1.Aggregate(Vector2.zero, (acc, v) => acc + v) / region1.Count;
		Vector2 center2 = region2.Aggregate(Vector2.zero, (acc, v) => acc + v) / region2.Count;
		
		// Create a simple rectangular corridor
		Vector2 direction = (center2 - center1).normalized;
		Vector2 perpendicular = new Vector2(-direction.y, direction.x);
		
		List<Vector2> corridorVertices = new List<Vector2>
		{
			center1 + perpendicular * corridorWidth/2,
			center2 + perpendicular * corridorWidth/2,
			center2 - perpendicular * corridorWidth/2,
			center1 - perpendicular * corridorWidth/2
		};

		Room corridor = new Room
		{
			vertices = corridorVertices,
			isCorridor = true
		};
		
		corridors.Add(corridor);
	}

	private void SubdivideIntoRooms()
	{
		// Process each region that isn't a corridor
		foreach (var region in rooms.Where(r => !r.isCorridor))
		{
			var subRooms = SubdivideRegion(region.vertices);
			foreach (var subRoom in subRooms)
			{
				rooms.Add(new Room { vertices = subRoom, isCorridor = false });
			}
		}
	}

	private void PlaceDoors()
	{
		foreach (var room in rooms)
		{
			foreach (var corridor in corridors)
			{
				PlaceDoorsBetweenRoomAndCorridor(room, corridor);
			}
		}
	}

	private void PlaceDoorsBetweenRoomAndCorridor(Room room, Room corridor)
	{
		for (int i = 0; i < room.vertices.Count; i++)
		{
			Vector2 start = room.vertices[i];
			Vector2 end = room.vertices[(i + 1) % room.vertices.Count];

			for (int j = 0; j < corridor.vertices.Count; j++)
			{
				Vector2 corrStart = corridor.vertices[j];
				Vector2 corrEnd = corridor.vertices[(j + 1) % corridor.vertices.Count];

				if (AreEdgesAdjacent(start, end, corrStart, corrEnd))
				{
					// Place door in the middle of the shared edge
					Vector2 doorPos = (start + end) / 2;
					Vector2 doorDir = (end - start).normalized;
					
					Door door = new Door
					{
						position = doorPos,
						direction = doorDir
					};
					
					room.doors.Add(door);
					corridor.doors.Add(door);
				}
			}
		}
	}

	private void Generate3DGeometry()
	{
		// Create parent object for all level geometry
		GameObject levelParent = new GameObject("LevelGeometry");

		// Generate floors
		foreach (var room in rooms.Concat(corridors))
		{
			GameObject floor = CreateFloorMesh(room.vertices);
			floor.transform.parent = levelParent.transform;

			// Generate walls
			for (int i = 0; i < room.vertices.Count; i++)
			{
				Vector2 start = room.vertices[i];
				Vector2 end = room.vertices[(i + 1) % room.vertices.Count];

				// Check if there's a door on this wall
				bool hasDoor = room.doors.Any(d => 
					Vector2.Distance(d.position, (start + end) / 2) < d.width);

				if (!hasDoor)
				{
					GameObject wall = CreateWallMesh(start, end);
					wall.transform.parent = levelParent.transform;
				}
			}

			// Generate doors
			foreach (var door in room.doors)
			{
				GameObject doorObj = CreateDoorMesh(door);
				doorObj.transform.parent = levelParent.transform;
			}
		}
	}

	// Fixed CreateDoorMesh to return GameObject
	private GameObject CreateDoorMesh(Door door)
	{
		GameObject doorFrame = new GameObject("DoorFrame");
		
		// Create door frame mesh
		Vector3 right = new Vector3(door.direction.x, 0, door.direction.y);
		Vector3 doorPos = new Vector3(door.position.x, 0, door.position.y);
		
		// Create frame posts
		CreateDoorPost(doorPos + right * (door.width/2), wallHeight);
		CreateDoorPost(doorPos - right * (door.width/2), wallHeight);
		
		// Create top frame
		GameObject topFrame = new GameObject("TopFrame");
		topFrame.transform.parent = doorFrame.transform;
		
		MeshFilter meshFilter = topFrame.AddComponent<MeshFilter>();
		MeshRenderer meshRenderer = topFrame.AddComponent<MeshRenderer>();
		
		// Create top frame mesh
		Vector3[] vertices = new Vector3[]
		{
			doorPos + right * (door.width/2) + Vector3.up * (wallHeight - 0.2f),
			doorPos - right * (door.width/2) + Vector3.up * (wallHeight - 0.2f),
			doorPos - right * (door.width/2) + Vector3.up * wallHeight,
			doorPos + right * (door.width/2) + Vector3.up * wallHeight
		};
		
		int[] triangles = new int[] { 0, 1, 2, 0, 2, 3 };
		
		Mesh mesh = new Mesh();
		mesh.vertices = vertices;
		mesh.triangles = triangles;
		mesh.RecalculateNormals();
		
		meshFilter.mesh = mesh;
		meshRenderer.material = new Material(Shader.Find("Standard"));
		meshRenderer.material.color = Color.red;
		
		// Add collider
		topFrame.AddComponent<MeshCollider>();

		return doorFrame;
	}

}
