using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class up : MonoBehaviour
{
    public float moveamount = 0.2f;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.F))
        {
            transform.position += new Vector3(0, moveamount, 0);
        }
    }
}
