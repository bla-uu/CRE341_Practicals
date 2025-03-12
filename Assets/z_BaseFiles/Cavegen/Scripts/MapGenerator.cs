using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Unity.AI.Navigation;
using UnityEngine.AI;
using Random = UnityEngine.Random;
using UnityEditor.ShaderGraph.Internal;
using System.Linq;

public class MapGenerator : MonoBehaviour {

	public GameObject player; // Reference to your player prefab
	public GameObject npcPrefab, waypointsPrefab; // Reference to your NPC prefab
	public GameObject groundObject;
	[SerializeField] private GameObject cubePrefab; // Reference to a cube prefab
	public int width;
	public int height;

	public string seed;
	public bool useRandomSeed;

	[Range(0,75)]
	public int randomFillPercent;

	[SerializeField] int numberOfNPCs = 5;
	[SerializeField] List<GameObject> npcs = new List<GameObject>();
	[SerializeField] int numberWaypoints = 4;
	[SerializeField] List<GameObject> waypoints = new List<GameObject>();

	int[,] map;

	[SerializeField] public NavMeshSurface surface;
	[SerializeField] private float raycastHeight = 50f; // Height above the plane from which to cast rays.
    [SerializeField] private int maxAttempts = 1000; // Safety limit to avoid an infinite loop.

	[SerializeField] private Transform cubeParent; // Parent object to organize cubes
	[SerializeField] private float cubeSize = 1f; // Size of each cube
	[SerializeField] private float wallHeight = 5f; // Height of wall cubes

	void Start() {
		if (groundObject == null) {
			Debug.LogError("No object tagged 'Ground' found. Make sure your ground plane is tagged correctly.");
			return;
		}

		// Configure the NavMeshSurface
		if (surface != null) {
			// Set the NavMeshSurface to include the Default layer (where our floor is)
			// and exclude the Wall layer
			surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
			surface.layerMask = LayerMask.GetMask("Default") & ~LayerMask.GetMask("Wall");
			
			// Make sure the NavMeshSurface is set to collect objects from children
			surface.collectObjects = CollectObjects.Children;
			
			Debug.Log("NavMeshSurface configured to include Default layer and exclude Wall layer");
		} else {
			Debug.LogError("NavMeshSurface component not assigned!");
		}

		// Generate the map and build the NavMesh
		GenerateMap();
		
		// Wait a frame to ensure NavMesh is fully built
		StartCoroutine(SpawnEntitiesAfterNavMeshBuild());
	}

	// Coroutine to wait for NavMesh to build before spawning entities
	IEnumerator SpawnEntitiesAfterNavMeshBuild() {
		// Wait for the end of the frame to ensure NavMesh is built
		yield return new WaitForEndOfFrame();
		
		// Place player and spawn entities
		PlacePlayer();
		SpawnWayPoints(numberWaypoints);
		SpawnNPCs(numberOfNPCs);
		
		Debug.Log("All entities spawned after NavMesh build");
	}

	void Update() {
		if (Input.GetMouseButtonDown(1)) {
			// Generate a new map
			GenerateMap();
			
			// Delete existing NPCs and waypoints
			GameObject[] go_npcs = GameObject.FindGameObjectsWithTag("NPC");
			foreach (GameObject npc in go_npcs) Destroy(npc);

			GameObject[] go_wps = GameObject.FindGameObjectsWithTag("Waypoint");
			foreach (GameObject wp in go_wps) Destroy(wp);
			
			// Wait a frame to ensure NavMesh is fully built, then spawn entities
			StartCoroutine(SpawnEntitiesAfterNavMeshBuild());
		}
	}

	void GenerateMap() {
		// Clear any existing cubes
		if (cubeParent != null) {
			foreach (Transform child in cubeParent) {
				Destroy(child.gameObject);
			}
		} else {
			// Create a parent object if it doesn't exist
			GameObject parent = new GameObject("CubeParent");
			cubeParent = parent.transform;
			cubeParent.parent = transform;
			
			// Set the tag to "Walls"
			parent.tag = "Walls";
			
			// Set the layer to "Wall"
			int wallLayer = LayerMask.NameToLayer("Wall");
			if (wallLayer != -1) {
				parent.layer = wallLayer;
			} else {
				Debug.LogWarning("Wall layer not found. Please create a 'Wall' layer in the Unity Editor.");
			}
		}

		map = new int[width,height];
		RandomFillMap();

		// Draw a circle in the center of the map
		DrawCircleAtLocation(width / 2, height / 2, 10);

		// Draw circles that split the distance between the center and the corners
		DrawCircleAtLocation(width / 4, height / 4, 10);
		DrawCircleAtLocation(3 * width / 4, height / 4, 10);
		DrawCircleAtLocation(width / 4, 3 * height / 4, 10);
		DrawCircleAtLocation(3 * width / 4, 3 * height / 4, 10);

		for (int i = 0; i < 5; i ++) {
			SmoothMap();
		}

		ProcessMap();

		int borderSize = 1;
		int[,] borderedMap = new int[width + borderSize * 2,height + borderSize * 2];

		for (int x = 0; x < borderedMap.GetLength(0); x ++) {
			for (int y = 0; y < borderedMap.GetLength(1); y ++) {
				if (x >= borderSize && x < width + borderSize && y >= borderSize && y < height + borderSize) {
					borderedMap[x,y] = map[x-borderSize,y-borderSize];
				}
				else {
					borderedMap[x,y] = 1;
				}
			}
		}

		// Comment out the mesh generation
		// MeshGenerator meshGen = GetComponent<MeshGenerator>();
		// meshGen.GenerateMesh(borderedMap, 1);
		
		// Generate cubes instead
		GenerateCubes(borderedMap);
		
		// Ensure the NavMesh is rebuilt
		if (surface != null) {
			Debug.Log("Rebuilding NavMesh after map generation");
			surface.BuildNavMesh();
		} else {
			Debug.LogError("NavMeshSurface component not assigned!");
		}
	}

	// New method to generate cubes based on the map
	void GenerateCubes(int[,] map) {
		int mapWidth = map.GetLength(0);
		int mapHeight = map.GetLength(1);
		
		// Create a parent object if it doesn't exist
		if (cubeParent == null) {
			GameObject parent = new GameObject("CubeParent");
			cubeParent = parent.transform;
			cubeParent.parent = transform;
			
			// Set the tag to "Walls"
			parent.tag = "Walls";
			
			// Set the layer to "Wall"
			int wallLayer = LayerMask.NameToLayer("Wall");
			if (wallLayer != -1) {
				cubeParent.gameObject.layer = wallLayer;
			} else {
				Debug.LogWarning("Wall layer not found. Please create a 'Wall' layer in the Unity Editor.");
			}
		} else {
			// Ensure the existing parent has the correct tag and layer
			cubeParent.gameObject.tag = "Walls";
			
			int wallLayer = LayerMask.NameToLayer("Wall");
			if (wallLayer != -1) {
				cubeParent.gameObject.layer = wallLayer;
			}
		}
		
		// Clear any existing meshes
		foreach (Transform child in cubeParent) {
			Destroy(child.gameObject);
		}
		
		// Calculate the offset to center the map
		float offsetX = -mapWidth * cubeSize / 2;
		float offsetZ = -mapHeight * cubeSize / 2;
		
		// Unity has a vertex limit per mesh (65535 for older versions, higher for newer)
		// Each cube has 8 vertices, so we can have at most ~8000 cubes per mesh
		const int maxCubesPerMesh = 8000;
		
		// Create lists to store mesh data for the current mesh
		List<Vector3> vertices = new List<Vector3>();
		List<int> triangles = new List<int>();
		List<Vector2> uvs = new List<Vector2>();
		
		int currentMeshIndex = 0;
		int cubesInCurrentMesh = 0;
		GameObject currentMeshObj = CreateMeshObject($"CombinedMesh_{currentMeshIndex}");
		
		// Loop through the map and create cube meshes for walls
		for (int x = 0; x < mapWidth; x++) {
			for (int y = 0; y < mapHeight; y++) {
				// 1 represents a wall, 0 represents empty space
				if (map[x, y] == 1) {
					// Check which faces are visible (adjacent to empty spaces)
					bool[] visibleFaces = new bool[6]; // Bottom, Top, Front, Back, Left, Right
					
					// Bottom face is always visible for now (floor)
					visibleFaces[0] = true;
					
					// Top face is always visible (ceiling)
					visibleFaces[1] = true;
					
					// Front face (positive Z) - visible if adjacent to empty space
					visibleFaces[2] = (y + 1 >= mapHeight) || (map[x, y + 1] == 0);
					
					// Back face (negative Z) - visible if adjacent to empty space
					visibleFaces[3] = (y - 1 < 0) || (map[x, y - 1] == 0);
					
					// Left face (negative X) - visible if adjacent to empty space
					visibleFaces[4] = (x - 1 < 0) || (map[x - 1, y] == 0);
					
					// Right face (positive X) - visible if adjacent to empty space
					visibleFaces[5] = (x + 1 >= mapWidth) || (map[x + 1, y] == 0);
					
					// Skip this wall if no faces are visible (shouldn't happen with our current logic)
					if (!visibleFaces.Any(f => f)) continue;
					
					// Check if we need to start a new mesh
					if (cubesInCurrentMesh >= maxCubesPerMesh) {
						// Finalize the current mesh
						FinalizeMesh(currentMeshObj, vertices, triangles, uvs);
						
						// Start a new mesh
						currentMeshIndex++;
						vertices.Clear();
						triangles.Clear();
						uvs.Clear();
						cubesInCurrentMesh = 0;
						currentMeshObj = CreateMeshObject($"CombinedMesh_{currentMeshIndex}");
					}
					
					// Position for the cube
					Vector3 pos = new Vector3(
						offsetX + x * cubeSize + cubeSize/2, 
						wallHeight/2, // Center the cube vertically
						offsetZ + y * cubeSize + cubeSize/2
					);
					
					// Add cube to the combined mesh
					AddCubeToMesh(vertices, triangles, uvs, pos, new Vector3(cubeSize, wallHeight, cubeSize), visibleFaces);
					cubesInCurrentMesh++;
				}
			}
		}
		
		// Create floor and ceiling for empty spaces (where map value is 0)
		// We'll create a separate mesh for floor and ceiling to keep things organized
		List<Vector3> floorVertices = new List<Vector3>();
		List<int> floorTriangles = new List<int>();
		List<Vector2> floorUVs = new List<Vector2>();
		
		List<Vector3> ceilingVertices = new List<Vector3>();
		List<int> ceilingTriangles = new List<int>();
		List<Vector2> ceilingUVs = new List<Vector2>();
		
		GameObject floorMeshObj = CreateMeshObject("FloorMesh");
		GameObject ceilingMeshObj = CreateMeshObject("CeilingMesh");
		
		// Set the floor mesh to the Default layer for NavMesh generation
		floorMeshObj.layer = LayerMask.NameToLayer("Default");
		floorMeshObj.tag = "Floor";
		
		// Loop through the map and create floor and ceiling planes for empty spaces
		for (int x = 0; x < mapWidth; x++) {
			for (int y = 0; y < mapHeight; y++) {
				// 0 represents empty space
				if (map[x, y] == 0) {
					// Position for the floor and ceiling
					Vector3 pos = new Vector3(
						offsetX + x * cubeSize + cubeSize/2, 
						0, // Floor is at y=0
						offsetZ + y * cubeSize + cubeSize/2
					);
					
					// Add floor quad
					AddQuadToMesh(floorVertices, floorTriangles, floorUVs, pos, new Vector2(cubeSize, cubeSize), Vector3.up);
					
					// Add ceiling quad
					Vector3 ceilingPos = pos + new Vector3(0, wallHeight, 0);
					AddQuadToMesh(ceilingVertices, ceilingTriangles, ceilingUVs, ceilingPos, new Vector2(cubeSize, cubeSize), Vector3.down);
				}
			}
		}
		
		// Finalize the floor mesh
		if (floorVertices.Count > 0) {
			FinalizeMesh(floorMeshObj, floorVertices, floorTriangles, floorUVs);
			
			// Make sure the floor mesh has a MeshCollider for proper NavMesh generation
			MeshCollider floorCollider = floorMeshObj.GetComponent<MeshCollider>();
			if (floorCollider != null) {
				floorCollider.convex = false; // Non-convex for complex floor shapes
			}
			
			// Ensure the floor is on the Default layer for NavMesh generation
			floorMeshObj.layer = LayerMask.NameToLayer("Default");
			
			Debug.Log("Floor mesh created with " + floorVertices.Count/4 + " quads");
		} else {
			Destroy(floorMeshObj);
		}
		
		// Finalize the ceiling mesh
		if (ceilingVertices.Count > 0) {
			FinalizeMesh(ceilingMeshObj, ceilingVertices, ceilingTriangles, ceilingUVs);
			
			// Set the ceiling to the Wall layer to exclude it from NavMesh
			ceilingMeshObj.layer = LayerMask.NameToLayer("Wall");
			
			Debug.Log("Ceiling mesh created with " + ceilingVertices.Count/4 + " quads");
		} else {
			Destroy(ceilingMeshObj);
		}
		
		// Finalize the last wall mesh
		if (vertices.Count > 0) {
			FinalizeMesh(currentMeshObj, vertices, triangles, uvs);
		}
		
		// Set all children to the Wall layer recursively, except for the floor
		foreach (Transform child in cubeParent) {
			if (child.name != "FloorMesh") {
				SetLayerRecursively(child.gameObject, LayerMask.NameToLayer("Wall"));
			}
		}
	}

	// Helper method to create a mesh GameObject
	GameObject CreateMeshObject(string name) {
		GameObject meshObj = new GameObject(name);
		meshObj.transform.parent = cubeParent;
		meshObj.transform.localPosition = Vector3.zero;
		meshObj.AddComponent<MeshFilter>();
		meshObj.AddComponent<MeshRenderer>();
		
		// Set the tag to "Walls"
		meshObj.tag = "Walls";
		
		// Set the layer to "Wall" - assuming the Wall layer exists
		// If the Wall layer doesn't exist, you'll need to create it in the Unity Editor
		// (Edit > Project Settings > Tags and Layers)
		int wallLayer = LayerMask.NameToLayer("Wall");
		if (wallLayer != -1) {
			meshObj.layer = wallLayer;
		} else {
			Debug.LogWarning("Wall layer not found. Please create a 'Wall' layer in the Unity Editor.");
		}
		
		return meshObj;
	}

	// Helper method to finalize a mesh
	void FinalizeMesh(GameObject meshObj, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs) {
		MeshFilter meshFilter = meshObj.GetComponent<MeshFilter>();
		MeshRenderer meshRenderer = meshObj.GetComponent<MeshRenderer>();
		
		// Create and assign the mesh
		Mesh mesh = new Mesh();
		mesh.vertices = vertices.ToArray();
		mesh.triangles = triangles.ToArray();
		mesh.uv = uvs.ToArray();
		
		// Recalculate normals to ensure they're pointing in the correct direction
		mesh.RecalculateNormals();
		
		// Check if this is a floor mesh
		bool isFloor = meshObj.name == "FloorMesh";
		bool isCeiling = meshObj.name == "CeilingMesh";
		
		// Single debug message about mesh creation
		if (isFloor || isCeiling) {
			Debug.Log($"Created {meshObj.name} with {vertices.Count/4} quads and normals pointing inward");
		}
		
		mesh.RecalculateBounds();
		
		meshFilter.mesh = mesh;
		
		// Add a mesh collider
		MeshCollider meshCollider = meshObj.AddComponent<MeshCollider>();
		meshCollider.sharedMesh = mesh;
		
		// Assign a material
		if (cubePrefab != null && cubePrefab.GetComponent<Renderer>() != null) {
			// Use the cube prefab material for walls
			meshRenderer.material = cubePrefab.GetComponent<Renderer>().sharedMaterial;
			
			// For floor, we might want to use a different material
			if (isFloor) {
				// If you have a specific floor material, assign it here
				// For now, we'll just use the same material but with a different color
				Material floorMaterial = new Material(cubePrefab.GetComponent<Renderer>().sharedMaterial);
				floorMaterial.color = new Color(0.5f, 0.5f, 0.5f); // Gray color for floor
				meshRenderer.material = floorMaterial;
			}
		} else {
			// Create a default material if none is available
			Material defaultMaterial = new Material(Shader.Find("Standard"));
			
			// Set different colors for floor vs walls
			if (isFloor) {
				defaultMaterial.color = new Color(0.5f, 0.5f, 0.5f); // Gray color for floor
			} else {
				defaultMaterial.color = new Color(0.8f, 0.8f, 0.8f); // Light gray for walls
			}
			
			meshRenderer.material = defaultMaterial;
		}
		
		// If this is a floor mesh, ensure it's properly configured for NavMesh
		if (isFloor) {
			// Make sure the mesh collider is not convex for complex floor shapes
			meshCollider.convex = false;
			
			// Set the layer to Default for NavMesh generation
			meshObj.layer = LayerMask.NameToLayer("Default");
			
			// Tag it as "Floor"
			meshObj.tag = "Floor";
		}
	}

	// Helper method to add a cube to the mesh
	void AddCubeToMesh(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Vector3 position, Vector3 size, bool[] visibleFaces) {
		// Calculate half size for vertex positions
		Vector3 halfSize = size * 0.5f;
		
		// Get the current vertex count to use as offset for triangle indices
		int vertexOffset = vertices.Count;
		
		// Define the 8 vertices of the cube
		Vector3[] cubeVertices = new Vector3[] {
			position + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z), // 0: Bottom left back
			position + new Vector3(halfSize.x, -halfSize.y, -halfSize.z),  // 1: Bottom right back
			position + new Vector3(halfSize.x, -halfSize.y, halfSize.z),   // 2: Bottom right front
			position + new Vector3(-halfSize.x, -halfSize.y, halfSize.z),  // 3: Bottom left front
			position + new Vector3(-halfSize.x, halfSize.y, -halfSize.z),  // 4: Top left back
			position + new Vector3(halfSize.x, halfSize.y, -halfSize.z),   // 5: Top right back
			position + new Vector3(halfSize.x, halfSize.y, halfSize.z),    // 6: Top right front
			position + new Vector3(-halfSize.x, halfSize.y, halfSize.z)    // 7: Top left front
		};
		
		// Add vertices to the list
		vertices.AddRange(cubeVertices);
		
		// Add UVs for each vertex (simple mapping)
		for (int i = 0; i < 8; i++) {
			uvs.Add(new Vector2((i % 4) / 3f, (i / 4) / 1f));
		}
		
		// Add triangles for each visible face
		// For walls, we need to reverse the winding order to make normals point inward (toward the empty spaces)
		
		// Bottom face - Always add to ensure the mesh is closed
		// For bottom face, normal should point up (into the room)
		triangles.Add(vertexOffset + 0);
		triangles.Add(vertexOffset + 1);
		triangles.Add(vertexOffset + 2);
		triangles.Add(vertexOffset + 0);
		triangles.Add(vertexOffset + 2);
		triangles.Add(vertexOffset + 3);
		
		// Top face - Always add to ensure the mesh is closed
		// For top face, normal should point down (into the room)
		triangles.Add(vertexOffset + 4);
		triangles.Add(vertexOffset + 6);
		triangles.Add(vertexOffset + 5);
		triangles.Add(vertexOffset + 4);
		triangles.Add(vertexOffset + 7);
		triangles.Add(vertexOffset + 6);
		
		// Front face - normal should point back (into the room)
		if (visibleFaces[2]) {
			triangles.Add(vertexOffset + 3);
			triangles.Add(vertexOffset + 6);
			triangles.Add(vertexOffset + 7);
			triangles.Add(vertexOffset + 3);
			triangles.Add(vertexOffset + 2);
			triangles.Add(vertexOffset + 6);
		}
		
		// Back face - normal should point forward (into the room)
		if (visibleFaces[3]) {
			triangles.Add(vertexOffset + 0);
			triangles.Add(vertexOffset + 5);
			triangles.Add(vertexOffset + 1);
			triangles.Add(vertexOffset + 0);
			triangles.Add(vertexOffset + 4);
			triangles.Add(vertexOffset + 5);
		}
		
		// Left face - normal should point right (into the room)
		if (visibleFaces[4]) {
			triangles.Add(vertexOffset + 0);
			triangles.Add(vertexOffset + 7);
			triangles.Add(vertexOffset + 4);
			triangles.Add(vertexOffset + 0);
			triangles.Add(vertexOffset + 3);
			triangles.Add(vertexOffset + 7);
		}
		
		// Right face - normal should point left (into the room)
		if (visibleFaces[5]) {
			triangles.Add(vertexOffset + 1);
			triangles.Add(vertexOffset + 5);
			triangles.Add(vertexOffset + 6);
			triangles.Add(vertexOffset + 1);
			triangles.Add(vertexOffset + 6);
			triangles.Add(vertexOffset + 2);
		}
	}

	// Helper method to add a quad (rectangle) to a mesh
	void AddQuadToMesh(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Vector3 center, Vector2 size, Vector3 normal) {
		// Get the current vertex count to use as offset for triangle indices
		int vertexOffset = vertices.Count;
		
		// Calculate half size
		float halfWidth = size.x * 0.5f;
		float halfLength = size.y * 0.5f;
		
		// Calculate the right and forward vectors based on the normal
		Vector3 right = Vector3.Cross(normal, Vector3.forward).normalized;
		if (right.magnitude < 0.01f) {
			right = Vector3.Cross(normal, Vector3.up).normalized;
		}
		Vector3 forward = Vector3.Cross(right, normal).normalized;
		
		// Define the 4 vertices of the quad
		Vector3[] quadVertices = new Vector3[] {
			center + (-right * halfWidth) + (-forward * halfLength), // Bottom left
			center + (right * halfWidth) + (-forward * halfLength),  // Bottom right
			center + (right * halfWidth) + (forward * halfLength),   // Top right
			center + (-right * halfWidth) + (forward * halfLength)   // Top left
		};
		
		// Add vertices to the list
		vertices.AddRange(quadVertices);
		
		// Add UVs for each vertex
		uvs.Add(new Vector2(0, 0));
		uvs.Add(new Vector2(1, 0));
		uvs.Add(new Vector2(1, 1));
		uvs.Add(new Vector2(0, 1));
		
		// Check if this is a floor or ceiling (normal is up or down)
		bool isFloor = normal == Vector3.up;
		bool isCeiling = normal == Vector3.down;
		
		// Add triangles (2 triangles to form a quad)
		// For floor and ceiling, we need to reverse the winding order to make normals point inward
		if (isFloor) {
			// Floor - normals should point up (into the room)
			// First triangle
			triangles.Add(vertexOffset + 0);
			triangles.Add(vertexOffset + 2);
			triangles.Add(vertexOffset + 1);
			
			// Second triangle
			triangles.Add(vertexOffset + 0);
			triangles.Add(vertexOffset + 3);
			triangles.Add(vertexOffset + 2);
		} else if (isCeiling) {
			// Ceiling - normals should point down (into the room)
			// First triangle
			triangles.Add(vertexOffset + 0);
			triangles.Add(vertexOffset + 1);
			triangles.Add(vertexOffset + 2);
			
			// Second triangle
			triangles.Add(vertexOffset + 0);
			triangles.Add(vertexOffset + 2);
			triangles.Add(vertexOffset + 3);
		} else {
			// For walls, we'll handle this in the AddCubeToMesh method
			// First triangle
			triangles.Add(vertexOffset + 0);
			triangles.Add(vertexOffset + 1);
			triangles.Add(vertexOffset + 2);
			
			// Second triangle
			triangles.Add(vertexOffset + 0);
			triangles.Add(vertexOffset + 2);
			triangles.Add(vertexOffset + 3);
		}
	}

	void ProcessMap() {
		List<List<Coord>> wallRegions = GetRegions (1);
		int wallThresholdSize = 50;

		foreach (List<Coord> wallRegion in wallRegions) {
			if (wallRegion.Count < wallThresholdSize) {
				foreach (Coord tile in wallRegion) {
					map[tile.tileX,tile.tileY] = 0;
				}
			}
		}

		List<List<Coord>> roomRegions = GetRegions (0);
		int roomThresholdSize = 50;
		List<Room> survivingRooms = new List<Room> ();
		
		foreach (List<Coord> roomRegion in roomRegions) {
			if (roomRegion.Count < roomThresholdSize) {
				foreach (Coord tile in roomRegion) {
					map[tile.tileX,tile.tileY] = 1;
				}
			}
			else {
				survivingRooms.Add(new Room(roomRegion, map));
			}
		}
		survivingRooms.Sort ();
		survivingRooms [0].isMainRoom = true;
		survivingRooms [0].isAccessibleFromMainRoom = true;

		ConnectClosestRooms(survivingRooms);
	}

	void ConnectClosestRooms(List<Room> allRooms, bool forceAccessibilityFromMainRoom = false) {

		List<Room> roomListA = new List<Room> ();
		List<Room> roomListB = new List<Room> ();

		if (forceAccessibilityFromMainRoom) {
			foreach (Room room in allRooms) {
				if (room.isAccessibleFromMainRoom) {
					roomListB.Add (room);
				} else {
					roomListA.Add (room);
				}
			}
		} else {
			roomListA = allRooms;
			roomListB = allRooms;
		}

		int bestDistance = 0;
		Coord bestTileA = new Coord ();
		Coord bestTileB = new Coord ();
		Room bestRoomA = new Room ();
		Room bestRoomB = new Room ();
		bool possibleConnectionFound = false;

		foreach (Room roomA in roomListA) {
			if (!forceAccessibilityFromMainRoom) {
				possibleConnectionFound = false;
				if (roomA.connectedRooms.Count > 0) {
					continue;
				}
			}

			foreach (Room roomB in roomListB) {
				if (roomA == roomB || roomA.IsConnected(roomB)) {
					continue;
				}
			
				for (int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA ++) {
					for (int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB ++) {
						Coord tileA = roomA.edgeTiles[tileIndexA];
						Coord tileB = roomB.edgeTiles[tileIndexB];
						int distanceBetweenRooms = (int)(Mathf.Pow (tileA.tileX-tileB.tileX,2) + Mathf.Pow (tileA.tileY-tileB.tileY,2));

						if (distanceBetweenRooms < bestDistance || !possibleConnectionFound) {
							bestDistance = distanceBetweenRooms;
							possibleConnectionFound = true;
							bestTileA = tileA;
							bestTileB = tileB;
							bestRoomA = roomA;
							bestRoomB = roomB;
						}
					}
				}
			}
			if (possibleConnectionFound && !forceAccessibilityFromMainRoom) {
				CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
			}
		}

		if (possibleConnectionFound && forceAccessibilityFromMainRoom) {
			CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
			ConnectClosestRooms(allRooms, true);
		}

		if (!forceAccessibilityFromMainRoom) {
			ConnectClosestRooms(allRooms, true);
		}
	}

	void CreatePassage(Room roomA, Room roomB, Coord tileA, Coord tileB) {
		Room.ConnectRooms (roomA, roomB);
		//Debug.DrawLine (CoordToWorldPoint (tileA), CoordToWorldPoint (tileB), Color.green, 100);

		List<Coord> line = GetLine (tileA, tileB);
		foreach (Coord c in line) {
			DrawCircle(c,5);
		}
	}

	void DrawCircle(Coord c, int r) {
		for (int x = -r; x <= r; x++) {
			for (int y = -r; y <= r; y++) {
				if (x*x + y*y <= r*r) {
					int drawX = c.tileX + x;
					int drawY = c.tileY + y;
					if (IsInMapRange(drawX, drawY)) {
						map[drawX,drawY] = 0;
					}
				}
			}
		}
	}

	List<Coord> GetLine(Coord from, Coord to) {
		List<Coord> line = new List<Coord> ();

		int x = from.tileX;
		int y = from.tileY;

		int dx = to.tileX - from.tileX;
		int dy = to.tileY - from.tileY;

		bool inverted = false;
		int step = Math.Sign (dx);
		int gradientStep = Math.Sign (dy);

		int longest = Mathf.Abs (dx);
		int shortest = Mathf.Abs (dy);

		if (longest < shortest) {
			inverted = true;
			longest = Mathf.Abs(dy);
			shortest = Mathf.Abs(dx);

			step = Math.Sign (dy);
			gradientStep = Math.Sign (dx);
		}

		int gradientAccumulation = longest / 2;
		for (int i =0; i < longest; i ++) {
			line.Add(new Coord(x,y));

			if (inverted) {
				y += step;
			}
			else {
				x += step;
			}

			gradientAccumulation += shortest;
			if (gradientAccumulation >= longest) {
				if (inverted) {
					x += gradientStep;
				}
				else {
					y += gradientStep;
				}
				gradientAccumulation -= longest;
			}
		}

		return line;
	}

	Vector3 CoordToWorldPoint(Coord tile) {
		return new Vector3 (-width / 2 + .5f + tile.tileX, 2, -height / 2 + .5f + tile.tileY);
	}

	List<List<Coord>> GetRegions(int tileType) {
		List<List<Coord>> regions = new List<List<Coord>> ();
		int[,] mapFlags = new int[width,height];

		for (int x = 0; x < width; x ++) {
			for (int y = 0; y < height; y ++) {
				if (mapFlags[x,y] == 0 && map[x,y] == tileType) {
					List<Coord> newRegion = GetRegionTiles(x,y);
					regions.Add(newRegion);

					foreach (Coord tile in newRegion) {
						mapFlags[tile.tileX, tile.tileY] = 1;
					}
				}
			}
		}

		return regions;
	}

	List<Coord> GetRegionTiles(int startX, int startY) {
		List<Coord> tiles = new List<Coord> ();
		int[,] mapFlags = new int[width,height];
		int tileType = map [startX, startY];

		Queue<Coord> queue = new Queue<Coord> ();
		queue.Enqueue (new Coord (startX, startY));
		mapFlags [startX, startY] = 1;

		while (queue.Count > 0) {
			Coord tile = queue.Dequeue();
			tiles.Add(tile);

			for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++) {
				for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++) {
					if (IsInMapRange(x,y) && (y == tile.tileY || x == tile.tileX)) {
						if (mapFlags[x,y] == 0 && map[x,y] == tileType) {
							mapFlags[x,y] = 1;
							queue.Enqueue(new Coord(x,y));
						}
					}
				}
			}
		}
		return tiles;
	}

	bool IsInMapRange(int x, int y) {
		return x >= 0 && x < width && y >= 0 && y < height;
	}


	void RandomFillMap() {
		if (useRandomSeed) {
			seed = DateTime.Now.Ticks.ToString();
		}

		System.Random pseudoRandom = new System.Random(seed.GetHashCode());

		for (int x = 0; x < width; x ++) {
			for (int y = 0; y < height; y ++) {
				if (x == 0 || x == width-1 || y == 0 || y == height -1) {
					map[x,y] = 1;
				}
				else {
					map[x,y] = (pseudoRandom.Next(0,100) < randomFillPercent)? 1: 0;
				}
			}
		}
	}

	void SmoothMap() {
		for (int x = 0; x < width; x ++) {
			for (int y = 0; y < height; y ++) {
				int neighbourWallTiles = GetSurroundingWallCount(x,y);

				if (neighbourWallTiles > 4)
					map[x,y] = 1;
				else if (neighbourWallTiles < 4)
					map[x,y] = 0;

			}
		}
	}

	int GetSurroundingWallCount(int gridX, int gridY) {
		int wallCount = 0;
		for (int neighbourX = gridX - 1; neighbourX <= gridX + 1; neighbourX ++) {
			for (int neighbourY = gridY - 1; neighbourY <= gridY + 1; neighbourY ++) {
				if (IsInMapRange(neighbourX,neighbourY)) {
					if (neighbourX != gridX || neighbourY != gridY) {
						wallCount += map[neighbourX,neighbourY];
					}
				}
				else {
					wallCount ++;
				}
			}
		}

		return wallCount;
	}

	struct Coord {
		public int tileX;
		public int tileY;

		public Coord(int x, int y) {
			tileX = x;
			tileY = y;
		}
	}


	class Room : IComparable<Room> {
		public List<Coord> tiles;
		public List<Coord> edgeTiles;
		public List<Room> connectedRooms;
		public int roomSize;
		public bool isAccessibleFromMainRoom;
		public bool isMainRoom;

		public Room() {
		}

		public Room(List<Coord> roomTiles, int[,] map) {
			tiles = roomTiles;
			roomSize = tiles.Count;
			connectedRooms = new List<Room>();

			edgeTiles = new List<Coord>();
			foreach (Coord tile in tiles) {
				for (int x = tile.tileX-1; x <= tile.tileX+1; x++) {
					for (int y = tile.tileY-1; y <= tile.tileY+1; y++) {
						if (x == tile.tileX || y == tile.tileY) {
							if (map[x,y] == 1) {
								edgeTiles.Add(tile);
							}
						}
					}
				}
			}
		}

		public void SetAccessibleFromMainRoom() {
			if (!isAccessibleFromMainRoom) {
				isAccessibleFromMainRoom = true;
				foreach (Room connectedRoom in connectedRooms) {
					connectedRoom.SetAccessibleFromMainRoom();
				}
			}
		}

		public static void ConnectRooms(Room roomA, Room roomB) {
			if (roomA.isAccessibleFromMainRoom) {
				roomB.SetAccessibleFromMainRoom ();
			} else if (roomB.isAccessibleFromMainRoom) {
				roomA.SetAccessibleFromMainRoom();
			}
			roomA.connectedRooms.Add (roomB);
			roomB.connectedRooms.Add (roomA);
		}

		public bool IsConnected(Room otherRoom) {
			return connectedRooms.Contains(otherRoom);
		}

		public int CompareTo(Room otherRoom) {
			return otherRoom.roomSize.CompareTo (roomSize);
		}
	}

	
	// Fix the player placement method to spawn inside the maze
	private void PlacePlayer()
	{
		// Find valid empty spaces in the map (where map value is 0)
		List<Vector3> validPositions = new List<Vector3>();
		int mapWidth = map.GetLength(0);
		int mapHeight = map.GetLength(1);
		
		// Calculate the offset to center the map (same as in GenerateCubes)
		float offsetX = -mapWidth * cubeSize / 2;
		float offsetZ = -mapHeight * cubeSize / 2;
		
		// Find all valid positions (empty spaces)
		for (int x = 0; x < mapWidth; x++) {
			for (int y = 0; y < mapHeight; y++) {
				if (map[x, y] == 0) {
					// This is an empty space, add it to valid positions
					Vector3 pos = new Vector3(
						offsetX + x * cubeSize + cubeSize/2, 
						0.1f, // Slightly above the floor
						offsetZ + y * cubeSize + cubeSize/2
					);
					validPositions.Add(pos);
				}
			}
		}
		
		// If we found valid positions, place the player
		if (validPositions.Count > 0) {
			// Choose a random valid position
			int randomIndex = UnityEngine.Random.Range(0, validPositions.Count);
			Vector3 position = validPositions[randomIndex];
			
			// Sample the NavMesh to ensure the player is placed on a valid position
			NavMeshHit hit;
			if (NavMesh.SamplePosition(position, out hit, 2.0f, NavMesh.AllAreas)) {
				position = hit.position;
				// Ensure the player is slightly above the floor to avoid clipping
				position.y = 0.5f;
				Debug.Log($"Player placed at valid NavMesh position: {position}");
			} else {
				Debug.LogWarning($"Could not find valid NavMesh position for player. Using original position: {position}");
				// Still ensure the player is slightly above the floor
				position.y = 0.5f;
			}
			
			// Place the player
			player.transform.position = position;
			
			// Ensure the player has the "Player" tag
			player.tag = "Player";
			
			Debug.Log($"Player placed at {position}");
		} else {
			Debug.LogError("No valid positions found for player placement!");
		}
	}
	
	// Fix the waypoint spawning method
	private void SpawnWayPoints(int count)
	{
		// Find valid empty spaces in the map (where map value is 0)
		List<Vector3> validPositions = new List<Vector3>();
		int mapWidth = map.GetLength(0);
		int mapHeight = map.GetLength(1);
		
		// Calculate the offset to center the map (same as in GenerateCubes)
		float offsetX = -mapWidth * cubeSize / 2;
		float offsetZ = -mapHeight * cubeSize / 2;
		
		// Find all valid positions (empty spaces)
		for (int x = 0; x < mapWidth; x++) {
			for (int y = 0; y < mapHeight; y++) {
				if (map[x, y] == 0) {
					// This is an empty space, add it to valid positions
					Vector3 pos = new Vector3(
						offsetX + x * cubeSize + cubeSize/2, 
						0.1f, // Slightly above the floor
						offsetZ + y * cubeSize + cubeSize/2
					);
					validPositions.Add(pos);
				}
			}
		}
		
		// Clear existing waypoints list
		waypoints.Clear();
		
		// If we found valid positions, place the waypoints
		if (validPositions.Count > 0) {
			// Make sure we don't try to spawn more waypoints than we have valid positions
			count = Mathf.Min(count, validPositions.Count);
			
			// Create a list to track used positions to avoid duplicates
			List<Vector3> usedPositions = new List<Vector3>();
			
			for (int i = 0; i < count; i++) {
				// Find a position that hasn't been used yet
				Vector3 position;
				do {
					int randomIndex = UnityEngine.Random.Range(0, validPositions.Count);
					position = validPositions[randomIndex];
				} while (usedPositions.Contains(position));
				
				// Mark this position as used
				usedPositions.Add(position);
				
				// Sample the NavMesh to ensure the waypoint is placed on a valid position
				NavMeshHit hit;
				if (NavMesh.SamplePosition(position, out hit, 2.0f, NavMesh.AllAreas)) {
					position = hit.position;
					// Ensure the waypoint is slightly above the floor to avoid clipping
					position.y = 0.5f;
				} else {
					Debug.LogWarning($"Could not find valid NavMesh position for waypoint. Using original position: {position}");
					// Still ensure the waypoint is slightly above the floor
					position.y = 0.5f;
				}
				
				// Spawn the waypoint
				GameObject waypoint = Instantiate(waypointsPrefab, position, Quaternion.identity);
				waypoint.name = $"Waypoint_{i}";
				
				// IMPORTANT: Ensure the waypoint has the "Waypoint" tag
				waypoint.tag = "Waypoint";
				
				// Add to the waypoints list
				waypoints.Add(waypoint);
				
				Debug.Log($"Waypoint {i} spawned at {position} with tag 'Waypoint'");
			}
		} else {
			Debug.LogError("No valid positions found for waypoint placement!");
		}
	}

	// Fix the NPC spawning method to use the same approach
	private void SpawnNPCs(int count)
	{
		// Find valid empty spaces in the map (where map value is 0)
		List<Vector3> validPositions = new List<Vector3>();
		int mapWidth = map.GetLength(0);
		int mapHeight = map.GetLength(1);
		
		// Calculate the offset to center the map (same as in GenerateCubes)
		float offsetX = -mapWidth * cubeSize / 2;
		float offsetZ = -mapHeight * cubeSize / 2;
		
		// Find all valid positions (empty spaces)
		for (int x = 0; x < mapWidth; x++) {
			for (int y = 0; y < mapHeight; y++) {
				if (map[x, y] == 0) {
					// This is an empty space, add it to valid positions
					Vector3 pos = new Vector3(
						offsetX + x * cubeSize + cubeSize/2, 
						0.1f, // Slightly above the floor
						offsetZ + y * cubeSize + cubeSize/2
					);
					validPositions.Add(pos);
				}
			}
		}
		
		// Clear existing NPCs list
		npcs.Clear();
		
		// If we found valid positions, place the NPCs
		if (validPositions.Count > 0) {
			// Make sure we don't try to spawn more NPCs than we have valid positions
			count = Mathf.Min(count, validPositions.Count);
			
			// Create a list to track used positions to avoid duplicates
			List<Vector3> usedPositions = new List<Vector3>();
			
			for (int i = 0; i < count; i++) {
				// Find a position that hasn't been used yet
				Vector3 position;
				do {
					int randomIndex = UnityEngine.Random.Range(0, validPositions.Count);
					position = validPositions[randomIndex];
				} while (usedPositions.Contains(position));
				
				// Mark this position as used
				usedPositions.Add(position);
				
				// Sample the NavMesh to ensure the NPC is placed on a valid position
				NavMeshHit hit;
				if (NavMesh.SamplePosition(position, out hit, 2.0f, NavMesh.AllAreas)) {
					position = hit.position;
					// Ensure the NPC is slightly above the floor to avoid clipping
					position.y = 0.5f;
				} else {
					Debug.LogWarning($"Could not find valid NavMesh position for NPC. Using original position: {position}");
					// Still ensure the NPC is slightly above the floor
					position.y = 0.5f;
				}
				
				// Spawn the NPC
				GameObject npc = Instantiate(npcPrefab, position, Quaternion.identity);
				
				// IMPORTANT: Name the first NPC "NPC_00" to match what FSM_WaypointPatrol is looking for
				if (i == 0) {
					npc.name = "NPC_00";
				} else {
					npc.name = $"NPC_{i}";
				}
				
				// Make sure the NPC has the "NPC" tag
				npc.tag = "NPC";
				
				// Add to the NPCs list
				npcs.Add(npc);
				
				Debug.Log($"NPC {i} spawned at {position} with name '{npc.name}'");
			}
		} else {
			Debug.LogError("No valid positions found for NPC placement!");
		}
	}

	void DrawSquareAreaInCenter(int size) {
		int centerX = width / 2;
		int centerY = height / 2;

		int startX = centerX - size / 2;
		int startY = centerY - size / 2;

		for (int x = startX; x < startX + size; x++) {
			for (int y = startY; y < startY + size; y++) {
				if (IsInMapRange(x, y)) {
					map[x, y] = 0;
				}
			}
		}
	}

	void DrawRectangleAtLocation(int startX, int startY, int width, int height) {
    for (int x = startX; x < startX + width; x++) {
        for (int y = startY; y < startY + height; y++) {
            if (IsInMapRange(x, y)) {
                map[x, y] = 0;
            	}
        	}
    	}
	}

	void DrawCircleAreaInCenter(int radius) {
    int centerX = width / 2;
    int centerY = height / 2;

    for (int x = -radius; x <= radius; x++) {
        for (int y = -radius; y <= radius; y++) {
            if (x * x + y * y <= radius * radius) {
                int drawX = centerX + x;
                int drawY = centerY + y;
                if (IsInMapRange(drawX, drawY)) {
                    map[drawX, drawY] = 0;
                	}
            	}
        	}
    	}
	}

	void DrawCircleAtLocation(int centerX, int centerY, int radius) {
    for (int x = -radius; x <= radius; x++) {
        for (int y = -radius; y <= radius; y++) {
            if (x * x + y * y <= radius * radius) {
                int drawX = centerX + x;
                int drawY = centerY + y;
                if (IsInMapRange(drawX, drawY)) {
                    map[drawX, drawY] = 0;
                	}
           		}
        	}
    	}
	}

	// Helper method to set layer recursively for all children
	void SetLayerRecursively(GameObject obj, int layer) {
		if (layer == -1) return; // Invalid layer
		
		obj.layer = layer;
		
		foreach (Transform child in obj.transform) {
			SetLayerRecursively(child.gameObject, layer);
		}
	}

}
