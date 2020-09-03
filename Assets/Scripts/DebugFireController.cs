using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugFireController : MonoBehaviour
{
    public GameObject[] fires;

    private List<Room> rooms;
    private int maxNumber = 1;

    public void SetNumber(int number, int maxNumber) {
        this.maxNumber = maxNumber;
        int fireNumber = number * fires.Length / maxNumber;

        for(int i = 0; i < fires.Length; i++) {
            fires[i].SetActive(i < fireNumber);
        }
    }

    public void SetAvailableRooms(List<Room> rooms) {
        this.rooms = rooms;
    }

    private void OnMouseEnter() {
        Debug.Log("Enter");
    }

    private void OnMouseExit() {
        Debug.Log("Exit");
    }

    private void OnMouseDown() {
        GameObject container = transform.Find("[container]")?.gameObject;
        if(container == null) container = new GameObject("[container]");
        container.transform.SetParent(transform, false);
        container.transform.localScale = Vector3.one * 0.25f;

        if(rooms != null) {
            for(int i = 0; i < rooms.Count; i++) {
                Room room = rooms[i];
                GameObject obj = Instantiate(room.gameObject, container.transform);
                float radius = (float) 6 * 4 * rooms.Count / maxNumber;
                float angle = i * 6.283f / rooms.Count;
                obj.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
            }
        }
    }

    private void OnMouseUp() {
        GameObject container = transform.Find("[container]")?.gameObject;
        if(container != null) {
            GameObject.Destroy(container);
        }
    }
}