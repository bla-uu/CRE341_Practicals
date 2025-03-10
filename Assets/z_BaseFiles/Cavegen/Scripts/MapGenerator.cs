using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Unity.AI.Navigation;
using UnityEngine.AI;
using Random = UnityEngine.Random;
using UnityEditor.ShaderGraph.Internal;

public class MapGenerator : MonoBehaviour {

	public GameObject player; // Reference to your player prefab
	public GameObject npcPrefab, waypointsPrefab; // Reference to your NPC prefab
	public GameObject groundObject;
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

	void Start() {


        if (groundObject == null)
        {
            Debug.LogError("No object tagged 'Ground' found. Make sure your ground plane is tagged correctly.");
            return;
        }

        GenerateMap();
		surface.BuildNavMesh();

        // After the NavMesh is generated/baked, place the player
        PlacePlayer();

		SpawnWayPoints(numberWaypoints);
		SpawnNPCs(numberOfNPCs);
	}



    void Update() {
		if (Input.GetMouseButtonDown(1)) {
			GenerateMap();
			surface.BuildNavMesh();
			PlacePlayer();

			// delete existing NPCs and spawn new ones
			GameObject[] go_npcs = GameObject.FindGameObjectsWithTag("NPC");
			foreach (GameObject npc in go_npcs) Destroy(npc);


			GameObject[] go_wps = GameObject.FindGameObjectsWithTag("Waypoint");
			foreach (GameObject wp in go_wps) Destroy(wp);

			SpawnWayPoints(numberWaypoints);
			SpawnNPCs(numberOfNPCs);
		}
	}

	void GenerateMap() {
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



		ProcessMap ();

		int borderSize = 1;
		int[,] borderedMap = new int[width + borderSize * 2,height + borderSize * 2];

		for (int x = 0; x < borderedMap.GetLength(0); x ++) {
			for (int y = 0; y < borderedMap.GetLength(1); y ++) {
				if (x >= borderSize && x < width + borderSize && y >= borderSize && y < height + borderSize) {
					borderedMap[x,y] = map[x-borderSize,y-borderSize];
				}
				else {
					borderedMap[x,y] =1;
				}
			}
		}

		MeshGenerator meshGen = GetComponent<MeshGenerator>();
		meshGen.GenerateMesh(borderedMap, 1);
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

		ConnectClosestRooms (survivingRooms);
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

	
	// another approach that didn't quite work
	private void PlacePlayer()
    {
        Vector3 randomPlayerPos = GetRandomGroundPoint();

		player.transform.position = randomPlayerPos;
    }
	
    // Call this method to obtain a random point on an object tagged "Ground".
    public Vector3 GetRandomGroundPoint()
    {
        Bounds groundBounds = groundObject.GetComponent<Renderer>().bounds;
		
		for (int i = 0; i < maxAttempts; i++)
        {
            // Pick a random position within the specified X-Z range, at a fixed height.
            float randX = Random.Range(groundBounds.min.x, groundBounds.max.x);
            float randZ = Random.Range(groundBounds.min.z, groundBounds.max.z);
            Vector3 origin = new Vector3(randX, raycastHeight, randZ);

            // Cast a ray straight down.
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, Mathf.Infinity))
            {
                // Check if the first hit collider is tagged "Ground".
                if (hit.collider.CompareTag("Ground"))
                {
                    return hit.point; 
                }
            }
        }

        // If no suitable point is found after maxAttempts, return a default.
        Debug.LogWarning("No valid 'Ground' point found.");
        return Vector3.zero;
	}
	private void SpawnNPCs(int count)
	{
		int maxAttempts = 1000;
		for (int i = 0; i < count; i++)
		{
			Vector3 randomNPCPos = Vector3.zero;
			bool validPositionFound = false;
			int attempts = 0;

			while (!validPositionFound && attempts < maxAttempts)
			{
				randomNPCPos = GetRandomGroundPoint();
				if (randomNPCPos != Vector3.zero)
				{
					NavMeshHit hit;
					if (NavMesh.SamplePosition(randomNPCPos, out hit, 1.0f, NavMesh.AllAreas))
					{
						randomNPCPos = hit.position;
						validPositionFound = true;
					}
				}
				attempts++;
			}

			if (validPositionFound)
			{
				Instantiate(npcPrefab, randomNPCPos, Quaternion.identity);
				// add the NPC to the list
				npcs.Add(npcPrefab);
			}
			else
			{
				Debug.LogWarning("Failed to find a valid NavMesh point for NPC.");
			}
		}
	}

    private void SpawnWayPoints(int count)
	{

        for (int i = 0; i < count; i++)
		{
			Vector3 randomNPCPos = Vector3.zero;
			bool validPositionFound = false;
			int attempts = 0;

			while (!validPositionFound && attempts < maxAttempts)
			{
				randomNPCPos = GetRandomGroundPoint();
				if (randomNPCPos != Vector3.zero)
				{
					NavMeshHit hit;
					if (NavMesh.SamplePosition(randomNPCPos, out hit, 1.0f, NavMesh.AllAreas))
					{
						randomNPCPos = hit.position;
						validPositionFound = true;
					}
				}
				attempts++;
			}

			if (validPositionFound)
			{
				Instantiate(waypointsPrefab, randomNPCPos, Quaternion.identity);
				// add the NPC to the list
				waypoints.Add(waypointsPrefab);
			}
			else
			{
				Debug.LogWarning("Failed to find a valid NavMesh point for Waypoint.");
			}
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

}
