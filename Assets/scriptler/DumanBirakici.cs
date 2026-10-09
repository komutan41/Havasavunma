using UnityEngine;
 
// Fuze prefab'ine eklenir. FuzeHareket, fuze yok olmadan hemen once Birak()'i cagirir:
// duman/ates parcacik sistemleri fuzeden koparilir, yeni parcacik uretmeyi birakir
// ve mevcut parcaciklar kendi omurleri boyunca yavasca solarak kaybolur.
public class DumanBirakici : MonoBehaviour
{
    public float temizlemeSuresi = 8f;   // koparilan sistemler bu surede silinir (Start Lifetime'dan uzun olsun)
 
    public void Birak()
    {
        ParticleSystem[] sistemler = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < sistemler.Length; i++)
        {
            ParticleSystem ps = sistemler[i];
            if (ps == null || ps.gameObject == gameObject) continue;   // fuzenin kendisindeyse atla
 
            ps.transform.SetParent(null, true);                         // fuzeyle birlikte silinmesin
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);     // yeni parcacik uretme, eskiler sonsun
            Destroy(ps.gameObject, temizlemeSuresi);
        }
    }
}