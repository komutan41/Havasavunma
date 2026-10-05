using UnityEngine;

// Fuze prefab'ine eklenir. Fuze yok edilirken duman/ates parcacik sistemlerini
// fuzeden koparir, yeni parcacik uretmeyi durdurur ve mevcut parcaciklarin
// kendi omru boyunca (alpha ile) yavasca kaybolmasini bekler.
public class DumanBirakici : MonoBehaviour
{
    public float temizlemeSuresi = 6f;   // koparilan sistemler en gec bu surede silinir (Start Lifetime'dan uzun olsun)

    static bool kapaniyor;

    void OnApplicationQuit() { kapaniyor = true; }

    void OnDestroy()
    {
        if (kapaniyor || !gameObject.scene.isLoaded) return;

        ParticleSystem[] sistemler = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < sistemler.Length; i++)
        {
            ParticleSystem ps = sistemler[i];
            if (ps == null || ps.gameObject == gameObject) continue;   // fuzenin kendisindeyse atla

            var em = ps.emission;
            em.enabled = false;                       // yeni duman uretme, eskiler sonsun
            ps.transform.SetParent(null, true);       // fuzeyle birlikte silinmesin
            Destroy(ps.gameObject, temizlemeSuresi);
        }
    }
}
