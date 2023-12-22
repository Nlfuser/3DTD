using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using System.Linq;

public class Grid : MonoBehaviour
{
    public GameObject[,] tiles;
    public Dictionary<Vector3, GameObject> tilesPathBuild = new();
    public Dictionary<Vector3, int> tilesPathIndex = new();
    public Dictionary<GameObject, List<int>> buildingLatticeMapping = new();
    public Dictionary<int, GameObject> path = new();
    public Material[] tileMat = new Material[4];
    public GameObject tilePrefab;
    public GameObject pathPrefab;
    public Dictionary<GameObject, int> enemyPathMapping = new();
    public List<GameObject> enemies = new();
    public Dictionary<GameObject,int> enemyHP = new();
    public GameObject enemyPrefab;
    public Vector3[] pathing;
    public float pathightoffset = 0.6f;
    public static readonly int gridimens = 9;
    private static Vector3 gridpos;
    enum TileTerrain { water, arid, plane, mountain }
    public int tilerandT = 20;
    public int tilerandTm = 15;
    public int tiledevM = 3;
    public int tiledevm = 1;
    int tilerandM = 4;
    int tilerandm = 1;
    public int elevationHigh = 10;
    public int elevationMez = 8;
    public int elevationMid = 6;
    public int elevationLow = 4;
    bool lok;
    int duration;
    void Start()
    {
        gridpos = transform.position;
        tiles = new GameObject[gridimens, gridimens];
        lok = true;
        GenerateGrid();
        RandomizeGrid();
        GeneratePath();
        ExtractPathing();
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && lok)
        {
            GameManager._Instance.Wave();
        }
    }
    private void GeneratePath()
    {
        bool endReach = false;
        for (int i = 0; !endReach; i++)
        {
            if (!(i < gridimens)) endReach = true;
            else
            {
                Transform TilePosition = tiles[4, i].transform;
                SavePathTile(TilePosition, i);
            }
            /*
            GameObject clone = Instantiate(pathPrefab);
            clone.transform.position = new Vector3(TileT.position.x, TileT.position.y + pathightoffset, TileT.position.z);
            tilesPathBuild.Add(TileT.position, clone);
            tilesPathIndex.Add(TileT.position, i);
            path.Add(i, clone);
            clone.name = "" + i + "path";*/
        }
    }
    private void SavePathTile(Transform TileT, int index)
    {
        
        GameObject clone = Instantiate(pathPrefab);
        clone.transform.position = new Vector3(TileT.position.x, TileT.position.y + pathightoffset, TileT.position.z);
        tilesPathBuild.Add(TileT.position, clone);
        tilesPathIndex.Add(TileT.position, index);
        path.Add(index, clone);
        clone.name = "" + index + "path";
    }
    private void GenerateGrid()
    {
        for (int i = 0; i < gridimens; i++)
        {
            for (int j = 0; j < gridimens; j++)
            {
                GameObject clone = Instantiate(tilePrefab);
                clone.transform.position = new Vector3(gridpos.x + i, 0, gridpos.z + j);
                tiles[i, j] = clone;
                clone.name = "" + i + "," + j + "tile";
            }
        }
    }
    private void RandomizeGrid()
    {
        int terrainMain = UnityEngine.Random.Range(tilerandTm, tilerandT);
        for (int i = 0; i < gridimens; i++)
        {
            int terraindev = UnityEngine.Random.Range(tiledevm, tiledevM);
            int terrainTotal = UnityEngine.Random.Range(terrainMain - terraindev, terrainMain + terraindev);
            for (int j = 0; j < gridimens; j++)
            {
                TileTerrain currenTile = 0;
                if (terrainTotal > 0)
                {
                    if (terrainTotal > elevationHigh)
                    {
                        tilerandM = 4;
                        tilerandm = 2;
                    }
                    else if (terrainTotal > elevationMez)
                    {
                        tilerandM = 3;
                        tilerandm = 1;
                    }
                    else if (terrainTotal > elevationMid)
                    {
                        tilerandM = 2;
                        tilerandm = 1;
                    }
                    else if (terrainTotal > elevationLow)
                    {
                        tilerandM = 2;
                        tilerandm = 0;
                    }
                    int safetylock = 0;
                    do
                    {
                        currenTile = (TileTerrain)UnityEngine.Random.Range(tilerandm, tilerandM);
                        ++safetylock;
                    } while (terrainTotal - (int)currenTile < 0 && safetylock < 100);
                    terrainTotal -= (int)currenTile;
                }
                GameObject mytile = tiles[i, j];
                mytile.GetComponent<MeshRenderer>().material = tileMat[(int)currenTile];
                mytile.transform.position = new Vector3(mytile.transform.position.x, ((float)currenTile) / 2, mytile.transform.position.z);
                mytile.name = currenTile.ToString();
            }
        }
    }
    private void ExtractPathing()
    {
        pathing = new Vector3[path.Count];
        foreach (KeyValuePair<int, GameObject> p in path)
        {
            Vector3 pathPos = p.Value.transform.position;
            Vector3 pathingPos = new(pathPos.x, pathPos.y + 0.3f, pathPos.z);
            pathing[path.Count - p.Key - 1] = pathingPos;
        }
    }
    public void GenerateEnemy()
    {
        GameObject clone = Instantiate(enemyPrefab);
        enemies.Add(clone);        
        enemyPathMapping.Add(clone, 1);
        clone.name = "enemy" + enemies.Count;
        clone.transform.position = pathing[0];
        GameManager._Instance.GenerateEnemyUI(clone);
        GameManager._Instance.UpdateEnemyUIdmg(clone);
    }
    public bool MoveEnemies(Dictionary<GameObject, GameObject> enemyUIMapping, Vector3 heightenemyUIoffset)
    {
        bool res = true;
        foreach (GameObject E in enemies.ToList())
        {
            if (enemyPathMapping[E] < pathing.Length)
            {
                StartCoroutine(MoveEnemy(E, pathing[enemyPathMapping[E]], 1f, enemyUIMapping, heightenemyUIoffset));
                ++enemyPathMapping[E];
            }
            else
            {
                res = GameManager._Instance.EnemyOut(E);
            }
        }
        return res;
    }
    IEnumerator MoveEnemy(GameObject enemy, Vector3 targetPosition, float duration, Dictionary<GameObject, GameObject> enemyUIMapping, Vector3 heightenemyUIoffset)
    {
        float t = 0f;
        bool killsafelock = false;
        Vector3 startPosition = enemy.transform.position;
        while (!killsafelock & t < 1f & !enemy.Equals(null))
        {
            t += Time.deltaTime / duration;
            enemy.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            if (t > 0.5f){
                enemy.transform.position = new Vector3(enemy.transform.position.x, targetPosition.y, enemy.transform.position.z);
            }
            else
            {
                enemy.transform.position = new Vector3(enemy.transform.position.x, startPosition.y, enemy.transform.position.z);
            }
            try
            {
                enemyUIMapping[enemy].transform.position = enemy.transform.position + heightenemyUIoffset;
                enemyUIMapping[enemy].transform.rotation = GameManager._Instance.MainCameraObject.transform.rotation;
            }
            catch { killsafelock = true; }
            yield return null;
        }
    }
    public void Erasenemy(GameObject e)//migrate
    {
        //Destroy(e);
        enemies.Remove(e);
        //enemyPathMapping.Remove(e);
    }
}