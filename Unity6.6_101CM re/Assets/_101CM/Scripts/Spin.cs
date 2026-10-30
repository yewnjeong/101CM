using UnityEngine;

public class Spin : MonoBehaviour
{
    public Vector3 degreesPerSecond = new Vector3(0f, 45f, 0f);
    public float bobAmplitude;
    public float bobFrequency = 0.5f;
    Vector3 basePos;

    void Start() => basePos = transform.localPosition;

    void Update()
    {
        transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
        if (bobAmplitude > 0f)
            transform.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
    }
}
