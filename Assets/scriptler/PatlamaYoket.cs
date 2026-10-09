using UnityEngine;
 
// Patlama prefab'inin kok objesine eklenir.
// Efektler havuzundan oynatiliyorsa kapatmayi Efektler yapar (havuzlu = true), yoksa omur sonunda kendini siler.
public class PatlamaYoket : MonoBehaviour
{
    public float omur = 6f;
    [HideInInspector] public bool havuzlu;
 
    void Start()
    {
        if (!havuzlu) Destroy(gameObject, omur);
    }
}