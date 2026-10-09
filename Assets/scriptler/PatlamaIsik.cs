using UnityEngine;
 
// Patlama prefab'inin isigina eklenir. Havuzdan her yeniden oynatildiginda sifirlanir.
public class PatlamaIsik : MonoBehaviour
{
    public float sure = 0.35f;
    Light isik;
    float baslangic;
    float gecen;
 
    void Awake()
    {
        isik = GetComponent<Light>();
        if (isik != null) baslangic = isik.intensity;
    }
 
    void OnEnable()
    {
        gecen = 0f;
        if (isik != null)
        {
            isik.enabled = true;
            isik.intensity = baslangic;
        }
    }
 
    void Update()
    {
        if (isik == null || gecen >= sure) return;
 
        gecen += Time.deltaTime;
        isik.intensity = Mathf.Lerp(baslangic, 0f, gecen / sure);
        if (gecen >= sure) isik.enabled = false;
    }
}