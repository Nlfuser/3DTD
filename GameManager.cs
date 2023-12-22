using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;

public class GameManager : MonoBehaviour
{
    public static GameManager _Instance;
    public List<GameObject> Objects;
    public GameObject towerPrefab;
    public GameObject bulletPrefab;
    public GameObject uiEnemyLifePreset;
    public Grid myGrid;
    private GameObject myWorldCanvas;
    public Vector3[,] topology;
    public UnityEvent enemyOutCall;
    public UnityEvent waveCall;
    public UnityEvent buildKillCall;
    GameObject querylev;
    GameObject placeObject;
    public Dictionary<Vector3, GameObject> buildings = new();
    public Dictionary<GameObject, GameObject> enemyUIMapping = new();
    bool clock;
    bool place;
    int lasti;
    int lastj;
    Vector3 buildingheightoffset = new(0, 1, 0);
    Vector3 heightenemyUIoffset = new(0, 0.8f, 0);
    GameObject instructions;
    TMP_Text lifeText;
    TMP_Text moneyText;
    TMP_Text waveText;
    TMP_Text costText;
    public int life;
    public int money;
    public int currentWave;
    int victoryWave;
    int cost;
    int wavecount;
    public AudioSource audioData;
    public AudioClip soundenemy;
    public AudioClip soundTurret;
    public GameObject[] cameraPos;
    public GameObject MainCameraObject;
    int cameraindex;
    bool cameraismovable;
    bool GameOver;

    private void Awake()
    {
        if (_Instance != null) Destroy(gameObject);
        else
        {
            _Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        RecursiveChildrenWrapper(Objects, transform);
    }
    private void Start()
    {
        myGrid = GetObject("Grid").GetComponent<Grid>();
        MainCameraObject = GetObject("Main Camera");
        myWorldCanvas = GetObject("CanvasWorld");
        instructions = GetObject("Instructions");
        lifeText = GetObject("Life").GetComponent<TMP_Text>();
        moneyText = GetObject("Currency").GetComponent<TMP_Text>();
        waveText = GetObject("Wave").GetComponent<TMP_Text>();
        costText = GetObject("Cost").GetComponent<TMP_Text>();
        audioData = GetObject("MainAudio").GetComponent<AudioSource>();
        cameraindex = 0;
        cameraismovable = true;
        MainCameraObject.transform.position = cameraPos[0].transform.position;
        VariableInit();
        ExtractGridTopo();
    }
    void VariableInit()
    {
        life = 10;
        money = 42;
        cost = 6;
        currentWave = 1;
        wavecount = 1;
        victoryWave = 12;
        clock = false;
        place = false;
        Destroy(placeObject);
        placeObject = null;
        lifeText.SetText(life.ToString());
        moneyText.SetText(money.ToString());
        waveText.SetText(currentWave.ToString());
        costText.SetText(cost.ToString());
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            if (Afford(ref cost)) PickUp();
        }
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (cameraismovable)
            {
                ++cameraindex;
                if (cameraindex == cameraPos.Length) cameraindex = 0;
                RotateCamera(cameraPos[cameraindex]);
            }
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if (cameraismovable)
            {
                --cameraindex;
                if (cameraindex < 0) cameraindex = cameraPos.Length - 1;
                RotateCamera(cameraPos[cameraindex]);
            }
        }
        if (Input.GetMouseButtonDown(0) && instructions.activeSelf)
        {
            instructions.SetActive(false);
        }
        if (place)
        {
            PlaceTurret();
        }
    }
    public void PlaceTurret()
    {
        Ray pointer = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(pointer, out RaycastHit hit))
        {
            TileTop(hit.transform.gameObject);
            string problem = "none";
            if (Input.GetMouseButtonDown(0))
            {
                Vector3 placeposition = topology[lasti, lastj];
                bool inPath = false;
                if (hit.transform.GetComponent<MeshRenderer>().material.name.Contains("Atile"))
                {
                    inPath = true;
                    problem = "water";
                }
                foreach (KeyValuePair<Vector3, GameObject> road in myGrid.tilesPathBuild.ToList())
                {
                    if (placeposition == road.Value.transform.position - new Vector3(0, myGrid.pathightoffset, 0))
                    {
                        inPath = true;
                        problem = "road";
                    }
                }
                foreach (KeyValuePair<Vector3, GameObject> building in buildings)
                {
                    if (placeposition == building.Value.transform.position - buildingheightoffset)
                    {
                        inPath = true;
                        problem = "building";
                    }
                }
                if (!inPath)
                {
                    place = false;
                    buildings.Add(topology[lasti, lastj] + buildingheightoffset, placeObject);
                }
                else
                {
                    Debug.Log("Not here: " + problem);
                }
            }
        }
    }
    private void RecursiveChildrenWrapper(List<GameObject> objects, Transform node)
    {
        foreach (Transform child in node)
        {
            objects.Add(child.gameObject);
            if (child.transform.childCount > 0)
            {
                RecursiveChildrenWrapper(objects, child);
            }
        }
    }
    public GameObject GetObject(string query)
    {
        try
        {
            if (querylev?.name != query) querylev = Objects.Where(obj => obj.name == query).SingleOrDefault();
            return querylev;
        }
        catch (Exception e)
        {
            Debug.LogError(e + " getObjecterror");
            return null;
        }
    }
    public void Wave()
    {
        if (GetObject("Loose").activeSelf || GetObject("Win").activeSelf) return;
        if (myGrid.enemies.Count > 0) return;
        if (!clock)
        {
            clock = true;
            StartCoroutine(WaveClock());
        }
        else
        {
            wavecount += currentWave;
        }
        ++currentWave;
        waveText.text = (currentWave-1).ToString();
        if (currentWave > victoryWave)
        {
            GetObject("Win")?.SetActive(true);
            clock = false;
        }
    }
    IEnumerator WaveClock() //migrate to grid.cs
    {
        while (clock)
        {
            if (wavecount > 0)
            {
                myGrid.GenerateEnemy();
                --wavecount;
            }
            ShootBuildings();
            myGrid.MoveEnemies(enemyUIMapping, heightenemyUIoffset);
            yield return new WaitForSeconds(1);
        }
    }
    public bool EnemyOut(GameObject enemy) //dont migrate
    {
        Enemykill(enemy, false);
        Destroy(enemy);
        if (life < 1)
        {
            clock = false;
            cameraismovable = false;
            GetObject("Loose")?.SetActive(true);
        }
        else
        {
            life -= 1;
            lifeText.SetText(life.ToString());
        }
        return life > 0;
    }
    void ExtractGridTopo()
    {
        int dimens = Grid.gridimens;
        topology = new Vector3[dimens, dimens];
        for (int i = 0; i < dimens; ++i)
        {
            for (int j = 0; j < dimens; ++j)
            {
                topology[i, j] = myGrid.tiles[i, j].transform.position;
            }
        }
    }
    //Button referenced
    public void PickUp()
    {
        GameObject clone = Instantiate(towerPrefab);
        clone.name = "building" + buildings.Count;
        clone.transform.position = topology[Grid.gridimens / 2, Grid.gridimens / 2] + buildingheightoffset;
        placeObject = clone;
        place = true;
    }
    private void TileTop(GameObject tile)
    {
        int dimens = Grid.gridimens;
        for (int i = 0; i < dimens; ++i)
        {
            for (int j = 0; j < dimens; ++j)
            {
                if (myGrid.tiles[i, j].Equals(tile))
                {
                    placeObject.transform.position = topology[i, j] + buildingheightoffset;
                    lasti = i;
                    lastj = j;
                }
            }
        }
    }
    public bool ShootBuildings() //(damage * towers >= life * enemies)? win : loose
    {
        foreach (KeyValuePair<Vector3, GameObject> B in buildings)
        {
            ScanPerimeter(B);
        }
        return true;
    }
    public void ScanPerimeter(KeyValuePair<Vector3, GameObject> b)
    {
        if (myGrid.enemies.Count == 0) return;
        foreach (GameObject E in myGrid.enemies.ToList())
        {
            Vector3 enemypos = myGrid.pathing[myGrid.enemyPathMapping[E] - 1];
            Debug.DrawLine(enemypos, b.Key, Color.red, 1);
            float distance = Vector3.Distance(enemypos, b.Key);
            if (distance < 3)
            {
                Debug.DrawLine(enemypos, b.Key, Color.green, 5);
                //audioData.PlayOneShot(soundTurret);
                StartCoroutine(Shoot(0.8f,E,b.Value));
                return;
            }
        }
        return;
    }
    IEnumerator Shoot(float duration, GameObject enemy, GameObject tower)
    {
        float t = 0;
        GameObject bullet = Instantiate(bulletPrefab, tower.transform.position, tower.transform.rotation);
        
        while (t < 1f && !enemy.Equals(null))
        {
            t += Time.deltaTime / duration;
            bullet.transform.position = Vector3.Lerp(tower.transform.position, enemy.transform.position,t);
            yield return null;
        }
        if (!enemy.Equals(null))
        {
            enemy.GetComponent<enemy>().Dmg();
            UpdateEnemyUIdmg(enemy);
            audioData.PlayOneShot(soundenemy);
        }
        Destroy(bullet);
    }
    private bool Afford(ref int amount)
    {
        if (money < amount) return false;
        money -= amount;
        ++amount;
        costText.SetText(cost.ToString());
        moneyText.SetText(money.ToString());
        return true;
    }
    public void Enemykill(GameObject kill, bool reward)
    {
        try
        {
            Destroy(enemyUIMapping[kill]);
        }
        catch { }        
        enemyUIMapping.Remove(kill);
        myGrid.Erasenemy(kill);
        if (reward)
        {
            money++;
            moneyText.SetText(money.ToString());
        }
    }
    public void GenerateEnemyUI(GameObject clone)
    {
        GameObject eUI = Instantiate(uiEnemyLifePreset);
        eUI.transform.SetParent(myWorldCanvas.transform);
        eUI.name = clone.name + "UI";
        eUI.transform.SetPositionAndRotation(new Vector3(clone.transform.position.x, clone.transform.position.y, clone.transform.position.z) + heightenemyUIoffset, myWorldCanvas.transform.rotation);
        eUI.SetActive(true);
        enemyUIMapping.Add(clone, eUI);
    }
    public void UpdateEnemyUIdmg(GameObject enemy)
    {
        if (enemy.Equals(null)) return;
        try
        {
            enemyUIMapping[enemy].GetComponent<Scrollbar>().value = enemy.GetComponent<enemy>().Hpercent();
        }
        catch { }
    }
    public void PressedAfford() { if (Afford(ref cost) && !GetObject("Loose").activeSelf && !GetObject("Win").activeSelf) PickUp(); }
    public void JustLeave() { Application.Quit(); }
    void RotateCamera(GameObject reference)
    {
        //MainCameraObject.transform.position = reference.transform.position;
        //MainCameraObject.transform.rotation = reference.transform.rotation;
        StartCoroutine(LerpCamera(MainCameraObject, 0.5f, reference.transform.position, reference.transform.rotation));
    }
    IEnumerator LerpCamera(GameObject CameraO, float duration, Vector3 finalposition, Quaternion finalrotation)
    {
        float t = 0f;
        cameraismovable = false;
        Vector3 initposition = CameraO.transform.position;
        Quaternion initrotation = CameraO.transform.rotation;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            CameraO.transform.position = Vector3.Lerp(initposition, finalposition, t);
            CameraO.transform.rotation = Quaternion.Lerp(initrotation, finalrotation, t);
            yield return null;
        }
        cameraismovable = true;
    }
    public void RedoGame()
    {
        foreach(GameObject E in myGrid.enemies.ToList())
        {
            Destroy(E);
            Enemykill(E,false);
        }
        foreach(KeyValuePair<Vector3,GameObject> B in buildings.ToList())
        {
            Destroy(B.Value);
        }
        buildings.Clear();
        GetObject("Loose").SetActive(false);
        GetObject("Win").SetActive(false);
        VariableInit();
    }
    /*
    Animation standalone

    float t = 0;
    while(t < 1f)
    {
        t += Time.deltaTime / duration;
        //Lerp(initpos, finalpos,t)
        yield return null;
    }
     
     */
    /*
            switch (current)
            {
                case direction.left:
                    if (x == 0) current = direction.right;
                    else current = (direction)UnityEngine.Random.Range(0, 2);
                    break;
                case direction.up:
                    current = (direction)UnityEngine.Random.Range(-1, 2);
                    break;
                case direction.right:
                    if (x == gridimens - 1) current = direction.left;
                    else current = (direction)UnityEngine.Random.Range(-1, 1);
                    break;
            }
            switch (current)
            {
                case direction.left:
                    --x;
                    break;
                case direction.up:
                    ++y;
                    break;
                case direction.right:
                    ++x;
                    break;
            }
            Transform TilePosition = tiles[x, y].transform;
            SavePathTile(TilePosition, i);
            if (y == 8) endReach = true;*/
    /*
     private void GeneratePath()
    {
        bool endReach = false;
        direction current = direction.up;
        direction pastcurrent = direction.up;
        int[] navigation = new int[2];
        int pasthelm = 0;
        int x = 4;
        int y = 0;
        Transform TilePositionStart = tiles[x, y].transform;
        SavePathTile(TilePositionStart, 0);
        for (int i = 1; !endReach; i++)
        {
            //if(!current.Equals(direction.up)) 
            //helm = UnityEngine.Random.Range(x < 0? 1 : (current == direction.left? 0 : -1), x > gridimens-1 ? 1 : (current == direction.right ? 1 : 2));
            do
            {
                switch (current)
                {
                    case direction.left:
                        if (x == 0) current = direction.right;
                        else current = (direction)UnityEngine.Random.Range(0, 2);
                        break;
                    case direction.up:
                        current = (direction)UnityEngine.Random.Range(-1, 2);
                        break;
                    case direction.right:
                        if (x == gridimens - 1) current = direction.left;
                        else current = (direction)UnityEngine.Random.Range(-1, 1);
                        break;
                }
            } while (pastcurrent.Equals(current) && !current.Equals(direction.up));
            pastcurrent = current;
            switch (current)
            {
                case direction.left:
                    --x;
                    break;
                case direction.up:
                    ++y;
                    break;
                case direction.right:
                    ++x;
                    break;
            }
            Transform TilePosition = tiles[x, y].transform;
            SavePathTile(TilePosition, i);
            if (y == 8) endReach = true;
        }
    }
     
     */
}
