using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEditor;
using Pcx;
using UnityEngine.Networking;

public class PointCloudContainer : MonoBehaviour {

    public GameObject keyAsset;
    private GameObject container;
    private GameObject key1Prefab;
    private GameObject key2Prefab;
    private const string BASE_URL = "http://10.0.0.94:38080/";

    // Start is called before the first frame update
    void Start() {
        Application.targetFrameRate = 60;
        container = GameObject.Find("PointCloudContainer");
        key1Prefab = AssetDatabase.LoadAssetAtPath("Assets/Resources/Copper key.ply", typeof(GameObject)) as GameObject;
        key1Prefab.GetComponent<PointCloudRenderer>().pointTint = new Color(0.678f, 0.38f, 0.118f, 1);
    }

    // Update is called once per frame
    void Update() {
        if (Input.GetKeyDown(KeyCode.E)) {
            Debug.Log("E key pressed");
            Destroy(container.transform.GetChild(0).GameObject());
            StartCoroutine(GetPointcloudFile("Copper_key.ply"));
        }
        if (Input.GetKeyDown(KeyCode.R)) {
            Debug.Log("R key pressed");
            Destroy(container.transform.GetChild(0).GameObject());
            GameObject key1 = Instantiate(key1Prefab);
            key1.transform.parent = container.transform;
        }
        if (Input.GetKeyDown(KeyCode.T)) {
            Debug.Log("R key pressed");
            Destroy(container.transform.GetChild(0).GameObject());
            GameObject key2 = Instantiate(key2Prefab);
            key2.transform.parent = container.transform;
        }
    }

    IEnumerator GetPointcloudFile(string filename) {
        string uri = BASE_URL + filename;
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {
            yield return webRequest.SendWebRequest();

            string savePath = "Assets/Resources/" + filename;   
            System.IO.File.WriteAllBytes(savePath, webRequest.downloadHandler.data);

            AssetDatabase.ImportAsset("Assets/Resources/" + filename);
            key2Prefab = AssetDatabase.LoadAssetAtPath("Assets/Resources/" + filename, typeof(GameObject)) as GameObject;
            GameObject key2 = Instantiate(key2Prefab);
            key2.transform.parent = container.transform;
        }
    }
}
