using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;

public class WFCLevelGenerator : MonoBehaviour
{
    public List<GameObject> roomPrefabs;
    public GameObject errorPrefab;

    public float roomWidth = 9;
    public float roomHeight = 7;

    public int levelWidth = 7;
    public int levelHeight = 5;

    public GameObject debugTextPrefab;
    
    public enum ErrorBehavior {
        Restart,
        ErrorTile
    }
    public ErrorBehavior errorBehavior = ErrorBehavior.ErrorTile;

    public Dropdown errorBehaviourDropdown;

    private float[,] debugValues;
    private GameObject[,] debugTexts;

    // this is similar to a coroutine, except we manage it manually
    // we only need this to visualize the generation process step by step
    private IEnumerator generationEnumerator = null;
    private bool isRunning = false;

    void Start() {
        GeneratorAutoRun();
    }

    private void OnEnable() {
        errorBehaviourDropdown.onValueChanged.AddListener(GeneratorSetErrorBehavior);
    }

    private void OnDisable() {
        errorBehaviourDropdown.onValueChanged.RemoveListener(GeneratorSetErrorBehavior);
    }

    void Update() {
        if(isRunning && generationEnumerator != null) {
            generationEnumerator.MoveNext();
        }
    }

    public void GeneratorReset() {
        // clear objects
        for(int i = transform.childCount - 1; i >= 0; i--) {
            GameObject.Destroy(transform.GetChild(i).gameObject);
        }

        // debug / visualisation
        debugValues = new float[levelWidth, levelHeight];
        debugTexts = new GameObject[levelWidth, levelHeight];
        for(int x = 0; x < levelWidth; x++)
        for(int y = 0; y < levelHeight; y++) {
            Vector3 pos = GetRoomPosition(x, y) + new Vector3(roomWidth / 2f, -roomHeight / 2f, 0);
            debugTexts[x, y] = Instantiate(debugTextPrefab, pos, Quaternion.identity, transform);
        }

        // start generation and save the enumerator
        generationEnumerator = WFCGenerateLevel(levelWidth, levelHeight);
        generationEnumerator.MoveNext();
    }

    public void GeneratorStep() {
        if(generationEnumerator == null) {
            GeneratorReset();
        }
        isRunning = false;
        generationEnumerator.MoveNext();
    }

    public void GeneratorAutoRun() {
        if(generationEnumerator == null) {
            GeneratorReset();
        }
        isRunning = true;
    }

    public void GeneratorSetErrorBehavior(int behaviour) {
        if(behaviour == 0) {
            errorBehavior = ErrorBehavior.ErrorTile;
        } else if(behaviour == 1) {
            errorBehavior = ErrorBehavior.Restart;
        }
    }

    private void PreprocessRooms() {
        // create mirrored versions
        foreach(GameObject prefab in roomPrefabs) {;
            GameObject mirroredRoomPrefab = Instantiate(prefab, new Vector3(-10000, 0, 0), Quaternion.identity);
            roomPrefabs.Add(mirroredRoomPrefab);
        }
    }

    private IEnumerator WFCGenerateLevel(int width, int height) {
        int numberOfRooms = roomPrefabs.Count;
        List<Room> allRooms = new List<Room>();
        foreach(GameObject roomPrefab in roomPrefabs) {
            allRooms.Add(roomPrefab.GetComponent<Room>());
        }
        // Create the array representing our room options at every position
        List<Room>[,] roomOptions = new List<Room>[width, height];
        
        // initially all rooms are allowed everywhere, except at the edges, where we don't allow exits
        for(int x = 0; x < width; x++)
        for(int y = 0; y < height; y++) {
            List<Room> availableRoomsHere = new List<Room>(allRooms);
            roomOptions[x, y] = availableRoomsHere;
            // handle the edges:
            // top edge
            if(y == height - 1) 
                availableRoomsHere.RemoveAll(room => room.up != Room.ExitType.None);
            // bottom edge
            if(y == 0) 
                availableRoomsHere.RemoveAll(room => room.down != Room.ExitType.None);
            // left edge
            if(x == 0) 
                availableRoomsHere.RemoveAll(room => room.left != Room.ExitType.None);
            // right edge
            if(x == width - 1) 
                availableRoomsHere.RemoveAll(room => room.right != Room.ExitType.None);

            SetDebugInfo(x, y, availableRoomsHere, numberOfRooms - 1);
        }

        // initialization done
        Debug.Log("WFC initialization done");
        yield return new WaitForEndOfFrame();

        // WFC main loop
        while(true) {
            // observation -> Find a position that is highly contrained and place a room there
            int biggestConstraintX = -1;
            int biggestConstraintY = -1;
            float biggestConstraint = -1; // number of unavailable rooms

            for(int x = 0; x < width; x++)
            for(int y = 0; y < height; y++) {
                if(roomOptions[x, y] == null) {
                    // already placed here
                }
                else if(roomOptions[x, y].Count == 0) {
                    // !!! zero room options, everything is 0, we can't find a room at this position 
                    // ignore for now, alternatively we might just return and start over completely
                    Vector3 debugPos1 = GetRoomPosition(x, y) + new Vector3(roomWidth / 2f, -roomHeight / 2f, 0);
                    Debug.DrawLine(debugPos1 + Vector3.left + Vector3.up, debugPos1 + Vector3.right + Vector3.down, Color.red, 1f);
                    Debug.DrawLine(debugPos1 + Vector3.left + Vector3.down, debugPos1 + Vector3.right + Vector3.up, Color.red, 1f);

                    if(errorBehavior == ErrorBehavior.ErrorTile) {
                        if(errorPrefab) {
                            Instantiate(errorPrefab, debugPos1, Quaternion.identity, transform);
                        }
                    } else if(errorBehavior == ErrorBehavior.Restart) {
                        GeneratorReset();
                        yield break;
                    }
                } 
                else {
                    float constraint = numberOfRooms - roomOptions[x, y].Count;
                    // we fuzz the constraint a little bit. This effectively makes it random which of the rooms we choose when multiple are available
                    constraint += Random.Range(-0.1f, 0.1f);
                    if(constraint > biggestConstraint) {
                        biggestConstraint = constraint;
                        biggestConstraintX = x;
                        biggestConstraintY = y;
                    }
                }
            }

            if(biggestConstraint == -1) {
                // no non-0 positions found
                yield break;
            }

            // "rename" these variables for clarity
            int pickedX = biggestConstraintX;
            int pickedY = biggestConstraintY;

            // randomly pick a room to place ("collapse")
            List<Room> availableRooms = roomOptions[pickedX, pickedY];
            Room pickedRoom = availableRooms[Random.Range(0, availableRooms.Count)];
            roomOptions[pickedX, pickedY] = null; // mark this position as already placed

            PlaceRoom(pickedX, pickedY, pickedRoom);

            // debug drawing
            Vector3 debugPos = GetRoomPosition(pickedX, pickedY) + new Vector3(roomWidth / 2f, -roomHeight / 2f, 0);
            Debug.DrawLine(debugPos + Vector3.left, debugPos + Vector3.right, Color.white, 1f);

            SetDebugInfo(pickedX, pickedY, null, numberOfRooms - 1);


            // propagation -> update the surrounding area according tho the new observation
            // we do all directions manually because it's easier to write and understand than a more generic version
            // we look at the neighboring room lists and remove all options where the exits don't match
            // up
            if(pickedY + 1 < levelHeight) {
                List<Room> availableRoomsHere = roomOptions[pickedX, pickedY + 1];
                if(availableRoomsHere != null) {
                    availableRoomsHere.RemoveAll(room => room.down != pickedRoom.up);
                    SetDebugInfo(pickedX, pickedY + 1, availableRoomsHere, numberOfRooms - 1);
                }
            }
            // down
            if(pickedY - 1 >= 0) {
                List<Room> availableRoomsHere = roomOptions[pickedX, pickedY - 1];
                if(availableRoomsHere != null) {
                    availableRoomsHere.RemoveAll(room => room.up != pickedRoom.down);
                    SetDebugInfo(pickedX, pickedY - 1, availableRoomsHere, numberOfRooms - 1);
                }
            }
            // left
            if(pickedX - 1 >= 0) {
                List<Room> availableRoomsHere = roomOptions[pickedX - 1, pickedY];
                if(availableRoomsHere != null) {
                    availableRoomsHere.RemoveAll(room => room.right != pickedRoom.left);
                    SetDebugInfo(pickedX - 1, pickedY, availableRoomsHere, numberOfRooms - 1);
                }
            }
            // right
            if(pickedX + 1 < levelWidth) {
                List<Room> availableRoomsHere = roomOptions[pickedX + 1, pickedY];
                if(availableRoomsHere != null) {
                    availableRoomsHere.RemoveAll(room => room.left != pickedRoom.right);
                    SetDebugInfo(pickedX + 1, pickedY, availableRoomsHere, numberOfRooms - 1);
                }
            }

            yield return null;
        }

    }

    private void PlaceRoom(int x, int y, Room room) {
        Instantiate(room.gameObject, GetRoomPosition(x, y), Quaternion.identity, transform);
    }

    private void SetDebugInfo(int x, int y, List<Room> availableRooms, int maxNumber) {
        Text text = debugTexts[x, y].GetComponentInChildren<Text>();
        DebugFireController fireController = debugTexts[x, y].GetComponentInChildren<DebugFireController>();
        if(availableRooms == null) {
            text.text = "";
            fireController.SetNumber(-1, maxNumber);
            return;
        }
        
        fireController.SetAvailableRooms(availableRooms);

        int number = availableRooms.Count;

        text.text = $"{number}";
        text.color = Color.Lerp(Color.green, Color.red, 1 - (float)number / maxNumber);

        fireController.SetNumber(maxNumber - number, maxNumber);
    }

    private Vector3 GetRoomPosition(int x, int y) {
        return transform.right * x * roomWidth + transform.up * y * roomHeight;
    }

    private void OnDrawGizmos() {
        
    }
}
