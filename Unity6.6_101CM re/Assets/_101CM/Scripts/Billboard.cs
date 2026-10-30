using UnityEngine;

public class Billboard : MonoBehaviour
{
    Camera cam;

    void LateUpdate()
    {
        if (!cam) cam = Camera.main;
        if (cam) transform.rotation = cam.transform.rotation;
    }
}
