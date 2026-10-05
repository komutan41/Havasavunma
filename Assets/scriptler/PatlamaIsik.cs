using UnityEngine;

public class PatlamaIsik : MonoBehaviour
{
    public float sure = 0.35f;
    Light isik;
    float baslangic;
    float gecen;

    void Start()
    {
        isik = GetComponent<Light>();
        baslangic = isik.intensity;
    }

    void Update()
    {
        gecen += Time.deltaTime;
        isik.intensity = Mathf.Lerp(baslangic, 0f, gecen / sure);
        if (gecen >= sure) isik.enabled = false;
    }
}
