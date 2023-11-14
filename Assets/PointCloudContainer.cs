using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PointCloudContainer : MonoBehaviour {
    private int counter = 0;
    public GameObject keyPrefab;
    public GameObject chairPrefab;
    private GameObject container;

    // Start is called before the first frame update
    void Start() {
        Application.targetFrameRate = 60;
        container = GameObject.Find("PointCloudContainer");
    }

    // Update is called once per frame
    void Update() {
        if (counter % 2 == 0) {
            Destroy(container.transform.GetChild(0).GameObject());
            GameObject newChild;
            if (counter % 4 == 0) {
                newChild = Instantiate(keyPrefab) as GameObject;
            }
            else {
                newChild = Instantiate(chairPrefab) as GameObject;
            }
            newChild.transform.parent = container.transform;
        }
        counter++;
    }
}
