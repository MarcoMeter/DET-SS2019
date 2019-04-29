﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Realtime.Messaging.Internal;


public class World : MonoBehaviour
{
	public GameObject player;
	public Material textureAtlas;
	public Material fluidTexture;
	public static int columnHeight = 16;
	public static int chunkSize = 8;
	public static int worldSize = 1;
	public static int radius = 3;
	public static uint maxCoroutines = 1000;
	public static ConcurrentDictionary<string, Chunk> chunks;
	public static List<string> toRemove = new List<string>();

	public static bool firstbuild = true;

	float startTime;

	public static CoroutineQueue queue;

	public Vector3 lastbuildPos;

    /// <summary>
    /// Creates a name for the chunk based on its position
    /// </summary>
    /// <param name="v">Position of tje chunk</param>
    /// <returns>Returns a string witht he chunk's name</returns>
	public static string BuildChunkName(Vector3 v)
	{
		return (int)v.x + "_" + 
			         (int)v.y + "_" + 
			         (int)v.z;
	}

    /// <summary>
    /// Creates a name for the column based on its position
    /// </summary>
    /// <param name="v">Position of the column</param>
    /// <returns>Returns a string witht he column's name</returns>
	public static string BuildColumnName(Vector3 v)
	{
		return (int)v.x + "_" + (int)v.z;
	}

    /// <summary>
    /// Get block based on world coordinates
    /// </summary>
    /// <param name="pos">Rough position of the block to be returned</param>
    /// <returns>Returns the block related to the input position</returns>
	public static Block GetWorldBlock(Vector3 pos)
	{
        // Cast float to int to specify the actual chunk and block, which might got hit a by a raycast
        // Chunk
		int cx = (int) (Mathf.Round(pos.x)/(float)chunkSize) * chunkSize;
		int cy = (int) (Mathf.Round(pos.y)/(float)chunkSize) * chunkSize;
		int cz = (int) (Mathf.Round(pos.z)/(float)chunkSize) * chunkSize;

        // Block
		int blx = (int) (Mathf.Round(pos.x) - cx);
		int bly = (int) (Mathf.Round(pos.y) - cy);
		int blz = (int) (Mathf.Round(pos.z) - cz);

        // Create chunk name 
		string cn = BuildChunkName(new Vector3(cx,cy,cz));
		Chunk c;
		Debug.Log("World Hit: " + pos);
		Debug.Log("Chunk Hit: " + cn);
		Debug.Log("Block " + blx + " " + bly + " " + blz);
        // Find block in chunk
		if(chunks.TryGetValue(cn, out c))
		{
			return c.chunkData[blx,bly,blz];
		}
		else
			return null;
	}

	void BuildChunkAt(int x, int y, int z)
	{
		Vector3 chunkPosition = new Vector3(x*chunkSize, 
											y*chunkSize, 
											z*chunkSize);
					
		string n = BuildChunkName(chunkPosition);
		Chunk c;

		if(!chunks.TryGetValue(n, out c))
		{
			c = new Chunk(chunkPosition, textureAtlas, fluidTexture);
			c.chunk.transform.parent = this.transform;
			c.fluid.transform.parent = this.transform;
			chunks.TryAdd(c.chunk.name, c);
		}
	}

	IEnumerator BuildRecursiveWorld(int x, int y, int z, int startrad, int rad)
	{
		int nextrad = rad-1;
		if(rad <= 0 || y < 0 || y > columnHeight) yield break;
		//build chunk front
		BuildChunkAt(x,y,z+1);
		queue.Run(BuildRecursiveWorld(x,y,z+1,rad,nextrad));
		yield return null;

		//build chunk back
		BuildChunkAt(x,y,z-1);
		queue.Run(BuildRecursiveWorld(x,y,z-1,rad,nextrad));
		yield return null;
		
		//build chunk left
		BuildChunkAt(x-1,y,z);
		queue.Run(BuildRecursiveWorld(x-1,y,z,rad,nextrad));
		yield return null;

		//build chunk right
		BuildChunkAt(x+1,y,z);
		queue.Run(BuildRecursiveWorld(x+1,y,z,rad,nextrad));
		yield return null;
		
		//build chunk up
		BuildChunkAt(x,y+1,z);
		queue.Run(BuildRecursiveWorld(x,y+1,z,rad,nextrad));
		yield return null;
		
		//build chunk down
		BuildChunkAt(x,y-1,z);
		queue.Run(BuildRecursiveWorld(x,y-1,z,rad,nextrad));
		yield return null;

	}

	IEnumerator DrawChunks()
	{
		toRemove.Clear();
		foreach(KeyValuePair<string, Chunk> c in chunks)
		{
			if(c.Value.status == Chunk.ChunkStatus.DRAW) 
			{
				c.Value.DrawChunk();
			}
			if(c.Value.chunk && Vector3.Distance(player.transform.position,
								c.Value.chunk.transform.position) > radius*chunkSize)
				toRemove.Add(c.Key);

			yield return null;
		}
	}

	IEnumerator RemoveOldChunks()
	{
		for(int i = 0; i < toRemove.Count; i++)
		{
			string n = toRemove[i];
			Chunk c;
			if(chunks.TryGetValue(n, out c))
			{
				Destroy(c.chunk);
				c.Save();
				chunks.TryRemove(n, out c);
				yield return null;
			}
		}
	}

	public void BuildNearPlayer()
	{
		StopCoroutine("BuildRecursiveWorld");
		queue.Run(BuildRecursiveWorld((int)(player.transform.position.x/chunkSize),
											(int)(player.transform.position.y/chunkSize),
											(int)(player.transform.position.z/chunkSize),radius,radius));
	}

	// Use this for initialization
	void Start ()
    {
		Vector3 ppos = player.transform.position;
		player.transform.position = new Vector3(ppos.x,
											Utils.GenerateHeight(ppos.x,ppos.z) + 1,
											ppos.z);
		lastbuildPos = player.transform.position;
		player.SetActive(false);

		firstbuild = true;
		chunks = new ConcurrentDictionary<string, Chunk>();
		this.transform.position = Vector3.zero;
		this.transform.rotation = Quaternion.identity;	

		queue = new CoroutineQueue(maxCoroutines, StartCoroutine);
		startTime = Time.time;
		Debug.Log("Start Build");

		//build starting chunk
		BuildChunkAt((int)(player.transform.position.x/chunkSize),
											(int)(player.transform.position.y/chunkSize),
											(int)(player.transform.position.z/chunkSize));
		//draw it
		queue.Run(DrawChunks());

		//create a bigger world
		queue.Run(BuildRecursiveWorld((int)(player.transform.position.x/chunkSize),
											(int)(player.transform.position.y/chunkSize),
											(int)(player.transform.position.z/chunkSize),radius,radius));
	}
	
	void Update ()
    {
		Vector3 movement = lastbuildPos - player.transform.position;

		if(movement.magnitude > chunkSize )
		{
			lastbuildPos = player.transform.position;
			BuildNearPlayer();
		}

		if(!player.activeSelf)
		{
			player.SetActive(true);	
			Debug.Log("Built in " + (Time.time - startTime));
			firstbuild = false;
		}

		queue.Run(DrawChunks());
		queue.Run(RemoveOldChunks());

	}
}
