using UnityEngine;
 
// Taret objesine (taret_govdesi) eklenir.
// Taret, namlu noktasinin GERCEK dunya yonunu olcup kameranin baktigi yone kendini hizalar.
// Bu yuzden modelin baslangic yonu ne olursa olsun namlu kameranin tam onune bakar.
public class TaretKontrol : MonoBehaviour
{
    [Header("Baglantilar")]
    public KameraKontrol kamera;
    public Transform rampa;                       // RampaPivot (yoksa fuze_rampasi)
    public Transform yonReferansi;                // BOS BIRAK: MakineliTufek'teki ilk namlu noktasi otomatik bulunur
 
    [Header("Taret (yatay)")]
    public float taretHizi = 60f;                 // saniyede derece
    public float yatayOfset = 0f;                 // namlu hala saga/sola kayiksa ince ayar (derece)
 
    [Header("Rampa (dikey)")]
    public float rampaHizi = 30f;
    public Vector3 rampaEkseni = new Vector3(1f, 0f, 0f);
    public bool rampaTersCevir = false;
    public float rampaMin = 0f;
    public float rampaMax = 75f;
    public float dikeyOfset = 0f;
 
    Quaternion rampaBaslangic;
    float rampaAci;
    Transform referans;
 
    void Start()
    {
        if (rampa != null) rampaBaslangic = rampa.localRotation;
        ReferansBul();
    }
 
    void ReferansBul()
    {
        referans = yonReferansi;
 
        if (referans == null)
        {
            MakineliTufek mt = GetComponentInChildren<MakineliTufek>();
            if (mt == null) mt = FindFirstObjectByType<MakineliTufek>();
            if (mt != null && mt.namlular != null && mt.namlular.Length > 0)
                referans = mt.namlular[0];
        }
 
        if (referans == null) referans = rampa != null ? rampa : transform;
 
        // Referans taretin cocugu degilse taret doner ama referans donmez; sonsuz donmeyi onle
        if (!referans.IsChildOf(transform))
        {
            Debug.LogWarning("TaretKontrol: namlu noktasi tarete bagli degil, taretin kendi yonu kullaniliyor.");
            referans = transform;
        }
    }
 
    void Update()
    {
        if (kamera == null || referans == null) return;
 
        // Yatay: namlunun dunyadaki yonunu olc, kameranin yonune dogru don
        Vector3 f = referans.forward;
        f.y = 0f;
        if (f.sqrMagnitude > 0.0001f)
        {
            float mevcut = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            float fark = Mathf.DeltaAngle(mevcut, kamera.Yaw + yatayOfset);
            float adim = taretHizi * Time.deltaTime;
            transform.Rotate(0f, Mathf.Clamp(fark, -adim, adim), 0f, Space.World);
        }
 
        // Dikey
        if (rampa != null)
        {
            float hedefR = Mathf.Clamp(kamera.Pitch + dikeyOfset, rampaMin, rampaMax);
            rampaAci = Mathf.MoveTowards(rampaAci, hedefR, rampaHizi * Time.deltaTime);
            float uygula = rampaTersCevir ? -rampaAci : rampaAci;
            rampa.localRotation = rampaBaslangic * Quaternion.AngleAxis(uygula, rampaEkseni);
        }
    }
}