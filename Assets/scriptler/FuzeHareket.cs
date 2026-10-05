using System.Collections.Generic;
using UnityEngine;

public class FuzeHareket : MonoBehaviour
{
    public static List<FuzeHareket> tumu = new List<FuzeHareket>();

    [Header("Ucus")]
    public float hiz = 250f;
    public float omur = 20f;
    public float donusHizi = 45f;
    public float ileriBakis = 150f;

    [Header("Lazer huzmesi")]
    public float minGenislik = 60f;
    public float lazerKoniAcisi = 10f;

    [Header("Carpma")]
    public GameObject patlamaPrefab;
    public float vurmaMesafesi = 10f;
    public float chaffKilitAcisi = 4f;   // lazer chaff'e bu kadar derece yakinsa fuze ona carpar

    [HideInInspector] public bool kumandaVar;
    [HideInInspector] public Vector3 losKaynak;
    [HideInInspector] public Vector3 losYon;

    public bool BaglantiVar { get; private set; }

    void OnEnable() { tumu.Add(this); }
    void OnDisable() { tumu.Remove(this); }

    void Start()
    {
        Destroy(gameObject, omur);
    }

    bool LazerUstunde(HedefUcus h)
    {
        if (!kumandaVar) return false;
        float aci = Vector3.Angle(losYon, h.transform.position - losKaynak);
        return aci < chaffKilitAcisi;
    }

    void Update()
    {
        BaglantiVar = false;

        if (kumandaVar)
        {
            Vector3 rel = transform.position - losKaynak;
            float boyuna = Vector3.Dot(rel, losYon);
            float yanal = (rel - losYon * boyuna).magnitude;
            float izin = minGenislik + Mathf.Max(boyuna, 0f) * Mathf.Tan(lazerKoniAcisi * Mathf.Deg2Rad);
            BaglantiVar = boyuna > 0f && yanal < izin;
        }

        if (BaglantiVar)
        {
            float ilerleme = Vector3.Dot(transform.position - losKaynak, losYon);
            Vector3 nokta = losKaynak + losYon * (ilerleme + ileriBakis);
            Vector3 hedefYon = nokta - transform.position;
            if (hedefYon.sqrMagnitude > 0.0001f)
            {
                Quaternion istenen = Quaternion.LookRotation(hedefYon.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, istenen, donusHizi * Time.deltaTime);
            }
        }

        transform.position += transform.forward * hiz * Time.deltaTime;

        foreach (var h in HedefUcus.tumu)
        {
            if (h == null || h.olu) continue;
            if (h.sahte && !LazerUstunde(h)) continue;
            if (Vector3.Distance(transform.position, h.transform.position) < vurmaMesafesi)
            {
                Patla(h);
                return;
            }
        }

        if (transform.position.y < 0.5f) Patla(null);
    }

    void Patla(HedefUcus vurulan)
    {
        if (patlamaPrefab != null)
            Instantiate(patlamaPrefab, transform.position, Quaternion.identity);
        if (vurulan != null) vurulan.Vurul();
        Destroy(gameObject);
    }
}