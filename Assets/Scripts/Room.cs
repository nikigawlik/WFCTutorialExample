using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Room : MonoBehaviour
{
    public enum ExitType {
        None,
        Small,
        Big,
    }

    public ExitType up;
    public ExitType down;
    public ExitType left;
    public ExitType right;

    void Start()
    {
        
    }

    void Update()
    {
        
    }
}
