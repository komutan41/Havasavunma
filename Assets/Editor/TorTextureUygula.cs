#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Bu dosya Assets/Editor klasorune konur (klasor yoksa: Assets'e sag tik > Create > Folder > "Editor").
//
// Kullanim:
//  1) Texture klasorlerini (31ff1ad0_radar_cerceve, 9dddb842_govde_ve_tekerler ... hepsi) Assets icinde herhangi bir yere at
//     (zaten attiysan bir sey yapma; klasorun adi Tor/tor/TorTexture ne olursa olsun script kendisi bulur).
//  2) Project penceresinde torm1.fbx'e TEK TIK yap (secili olsun).
//  3) Ust menu: Tools > Tor > Texture Uygula
//
// Script her texture klasoru icin bir materyal olusturur (albedo + normal + specular), texture ayarlarini
// duzeltir ve FBX'in ayni numarali materyaline baglar. FBX yerine sahnedeki Tor objesi de secilebilir.
public static class TorTextureUygula
{
    static string TextureKlasoru = "Assets/TorTexture";              // otomatik bulunur
    static string MateryalKlasoru = "Assets/TorTexture/Materyaller";
    static bool sonShaderLogu;

    class Kayit
    {
        public string klasorAdi;
        public string onEk;          // klasor adinin basindaki 8 karakterlik kod (materyal numarasi)
        public string etiket;        // klasor adinin geri kalani (govde_1, sag_palet ...)
        public string albedo, normal, spec;
        public bool kesme;           // albedo seffaflik iceriyorsa (palet, izgara): alpha clip
        public Material materyal;
    }

    [MenuItem("Tools/Tor/Texture Uygula")]
    static void Calistir()
    {
        // Texture klasorlerini projede ara: adi "8 harf/rakam + _" ile baslayan ve icinde albedo olan klasorler
        // (31ff1ad0_radar_cerceve, 9dddb842_govde_ve_tekerler ...). Hangi klasorun icinde olduklari onemli degil.
        sonShaderLogu = false;
        List<Kayit> kayitlar = new List<Kayit>();
        HashSet<string> bulunan = new HashSet<string>();
        foreach (string g in AssetDatabase.FindAssets("albedo t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            string klasor = Path.GetDirectoryName(p).Replace('\\', '/');
            string ad = Path.GetFileName(klasor);
            if (!System.Text.RegularExpressions.Regex.IsMatch(ad, "^[0-9a-fA-F]{8}_")) continue;
            if (!bulunan.Add(klasor)) continue;

            Kayit r = KlasorOku(klasor, ad);
            if (r == null) continue;
            if (kayitlar.Count == 0) TextureKlasoru = Path.GetDirectoryName(klasor).Replace('\\', '/');
            kayitlar.Add(r);
        }

        if (kayitlar.Count == 0)
        {
            Hata("Texture klasorleri bulunamadi.\nProject'e 31ff1ad0_radar_cerceve, 9dddb842_govde_ve_tekerler gibi klasorleri (icinde albedo, normal, specular_gloss) adlarini degistirmeden at, sonra tekrar dene.");
            return;
        }
        MateryalKlasoru = TextureKlasoru + "/Materyaller";

        // 1) Texture ayarlari + materyaller
        if (!AssetDatabase.IsValidFolder(MateryalKlasoru)) AssetDatabase.CreateFolder(TextureKlasoru, "Materyaller");
        foreach (Kayit r in kayitlar)
        {
            r.kesme = TextureAyarla(r.albedo, "albedo");
            if (r.normal != null) TextureAyarla(r.normal, "normal");
            if (r.spec != null) TextureAyarla(r.spec, "spec");
        }
        AssetDatabase.Refresh();
        foreach (Kayit r in kayitlar) r.materyal = MateryalYap(r);
        AssetDatabase.SaveAssets();

        // 2) Uygulama: secili FBX ya da sahnedeki obje
        Object secili = Selection.activeObject;
        if (secili == null)
        {
            Hata(kayitlar.Count + " materyal olusturuldu (Assets/TorTexture/Materyaller).\nSimdi Project'te torm1.fbx'e tiklayip bu menuyu tekrar calistir.");
            return;
        }

        string yol = AssetDatabase.GetAssetPath(secili);
        ModelImporter mi = !string.IsNullOrEmpty(yol) ? AssetImporter.GetAtPath(yol) as ModelImporter : null;
        if (mi != null)
        {
            FbxeUygula(mi, yol, kayitlar);
            return;
        }

        GameObject go = secili as GameObject;
        if (go != null)
        {
            ObjeyeUygula(go, kayitlar);
            return;
        }

        Hata("Secili sey ne FBX ne de bir obje. Project'te torm1.fbx'e tikla ve tekrar dene.");
    }

    // ------------------------------------------------------------------ okuma
    static Kayit KlasorOku(string klasor, string ad)
    {
        Kayit r = new Kayit();
        r.klasorAdi = ad;
        int alt = ad.IndexOf('_');
        r.onEk = alt > 0 ? ad.Substring(0, alt) : ad;
        r.etiket = alt > 0 ? ad.Substring(alt + 1) : ad;

        foreach (string g in AssetDatabase.FindAssets("t:Texture2D", new[] { klasor }))
        {
            string yol = AssetDatabase.GUIDToAssetPath(g);
            string dosya = Path.GetFileNameWithoutExtension(yol).ToLowerInvariant();
            if (dosya == "albedo") r.albedo = yol;
            else if (dosya == "normal") r.normal = yol;
            else if (dosya.StartsWith("specular")) r.spec = yol;
        }
        return r.albedo != null ? r : null;
    }

    // Texture'in turune gore import ayarini duzeltir. albedo icin: seffaflik var mi (true/false) dondurur
    static bool TextureAyarla(string yol, string tur)
    {
        TextureImporter ti = AssetImporter.GetAtPath(yol) as TextureImporter;
        if (ti == null) return false;

        bool degisti = false;
        bool seffaf = false;

        if (tur == "normal")
        {
            if (ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; degisti = true; }
        }
        else if (tur == "spec")
        {
            if (ti.sRGBTexture) { ti.sRGBTexture = false; degisti = true; }   // renk degil veri: dogrusal
        }
        else
        {
            seffaf = ti.DoesSourceTextureHaveAlpha();
            if (seffaf && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; degisti = true; }
        }

        if (degisti) ti.SaveAndReimport();
        return seffaf;
    }

    // ------------------------------------------------------------------ materyal
    static Material MateryalYap(Kayit r)
    {
        // Projenin kullandigi render pipeline'a gore DOGRU shader'i sec (yanlis shader = pembe materyal)
        UnityEngine.Rendering.RenderPipelineAsset rp = QualitySettings.renderPipeline;
        if (rp == null) rp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
        if (rp == null) rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;

        Shader sh = null;
        if (rp != null && rp.defaultMaterial != null) sh = rp.defaultMaterial.shader;   // projenin kendi "Lit" shader'i
        if (sh == null) sh = Shader.Find(rp != null ? "Universal Render Pipeline/Lit" : "Standard");
        if (sh == null) sh = Shader.Find("Standard");
        bool urp = sh.name.Contains("Universal");
        if (!sonShaderLogu) { Debug.Log("TorTextureUygula: kullanilan shader = " + sh.name + (rp != null ? "  (pipeline: " + rp.name + ")" : "  (Built-in pipeline)")); sonShaderLogu = true; }

        string yol = MateryalKlasoru + "/Tor_" + r.etiket + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(yol);
        if (m == null)
        {
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, yol);
        }
        else m.shader = sh;

        Texture2D alb = AssetDatabase.LoadAssetAtPath<Texture2D>(r.albedo);
        Texture2D nor = r.normal != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(r.normal) : null;
        Texture2D spec = r.spec != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(r.spec) : null;

        m.SetTexture(urp ? "_BaseMap" : "_MainTex", alb);
        m.SetColor(urp ? "_BaseColor" : "_Color", Color.white);

        if (nor != null)
        {
            m.SetTexture("_BumpMap", nor);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
        }

        if (spec != null)
        {
            // specular_gloss haritasi: karanlik = mat, parlak kenarlar = metal/parlak
            m.SetTexture("_MetallicGlossMap", spec);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat(urp ? "_Smoothness" : "_GlossMapScale", 0.35f);   // parlaklik: 0 mat ... 1 ayna
        }
        else
        {
            m.SetFloat(urp ? "_Smoothness" : "_Glossiness", 0.2f);
            m.SetFloat("_Metallic", 0f);
        }

        if (r.kesme)
        {
            // Seffaf kisimlar (palet bandi delikleri, izgara, radar agi) tam kesilir
            if (urp)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cull", 0f);          // iki yuzlu
            }
            else m.SetFloat("_Mode", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = 2450;
        }

        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ uygulama
    static Kayit Eslestir(string materyalAdi, List<Kayit> kayitlar)
    {
        string kucuk = materyalAdi.ToLowerInvariant();
        foreach (Kayit r in kayitlar)
            if (kucuk.StartsWith(r.onEk.ToLowerInvariant())) return r;
        foreach (Kayit r in kayitlar)
            if (kucuk.Contains(r.etiket.ToLowerInvariant())) return r;
        return null;
    }

    static void FbxeUygula(ModelImporter mi, string yol, List<Kayit> kayitlar)
    {
        List<Material> kaynak = FbxMateryalleri(yol);
        if (kaynak.Count == 0)
        {
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.SaveAndReimport();
            kaynak = FbxMateryalleri(yol);
        }

        if (kaynak.Count == 0)
        {
            Hata("FBX'in icinden materyal okunamadi (Unity materyalleri FBX icinde tutmuyor).\n\nCOZUM: Hierarchy'de sahnedeki 'torm1' objesine tikla (Project'teki FBX'e degil) ve Tools > Tor > Texture Uygula'yi tekrar calistir.");
            return;
        }

        int sayi = 0;
        List<string> eslesmeyen = new List<string>();
        foreach (Material km in kaynak)
        {
            Kayit r = Eslestir(km.name, kayitlar);
            if (r == null) { eslesmeyen.Add(km.name); continue; }
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), km.name), r.materyal);
            sayi++;
        }
        mi.SaveAndReimport();

        string ek = eslesmeyen.Count > 0 ? "\nEslesmeyen: " + string.Join(", ", eslesmeyen.ToArray()) : "";
        Debug.Log("TorTextureUygula: " + sayi + "/" + kaynak.Count + " materyal FBX'e baglandi." + ek);
        EditorUtility.DisplayDialog("Tor Texture", sayi + " materyal FBX'e baglandi." + ek, "Tamam");
    }

    static List<Material> FbxMateryalleri(string yol)
    {
        List<Material> liste = new List<Material>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(yol))
        {
            Material m = o as Material;
            if (m != null) liste.Add(m);
        }
        return liste;
    }

    // Nesne adina gore materyal sirasi (FBX'teki materyal yuva sirasi). Materyal adi eslesmezse kullanilir.
    static string[] OnEkler(string nesneAdi)
    {
        string a = nesneAdi.ToLowerInvariant();
        if (a == "govde") return new[] { "9dddb842", "988af254", "3f2d7c13", "b09469ce" };
        if (a == "antenler") return new[] { "a23b59cf" };
        if (a == "sag_palet") return new[] { "fc84cccd" };
        if (a == "sol_palet") return new[] { "50030ac3" };
        if (a == "taret") return new[] { "42f5f0e6", "b09469ce" };
        if (a == "on_radar") return new[] { "42f5f0e6" };
        if (a == "radar") return new[] { "42f5f0e6", "31ff1ad0" };
        if (a.StartsWith("fuze_kap")) return new[] { "42f5f0e6" };
        if (a.StartsWith("fuze_") && !a.StartsWith("fuze_cikis")) return new[] { "fb697e7c" };
        if (a.StartsWith("sag_alt") || a.StartsWith("sol_alt") || a.EndsWith("_teker")) return new[] { "9dddb842" };
        return null;
    }

    static Kayit OnEkleBul(string onEk, List<Kayit> kayitlar)
    {
        foreach (Kayit r in kayitlar)
            if (r.onEk.ToLowerInvariant() == onEk) return r;
        return null;
    }

    static void ObjeyeUygula(GameObject go, List<Kayit> kayitlar)
    {
        int sayi = 0;
        List<string> eslesmeyen = new List<string>();

        foreach (Renderer rd in go.GetComponentsInChildren<Renderer>(true))
        {
            if (rd is ParticleSystemRenderer || rd is TrailRenderer || rd is LineRenderer) continue;

            Material[] mats = rd.sharedMaterials;
            string[] onekler = OnEkler(rd.gameObject.name);
            bool degisti = false;

            for (int i = 0; i < mats.Length; i++)
            {
                Kayit r = mats[i] != null ? Eslestir(mats[i].name, kayitlar) : null;       // 1) materyal adina gore
                if (r == null && onekler != null)                                           // 2) nesne adina + yuva sirasina gore
                    r = OnEkleBul(onekler[Mathf.Min(i, onekler.Length - 1)], kayitlar);
                if (r == null) continue;

                mats[i] = r.materyal;
                degisti = true;
                sayi++;
            }

            if (degisti)
            {
                Undo.RecordObject(rd, "Tor Texture");
                rd.sharedMaterials = mats;
                EditorUtility.SetDirty(rd);
            }
            else eslesmeyen.Add(rd.gameObject.name);
        }

        string ek = eslesmeyen.Count > 0 ? "\nTexture verilmeyen nesneler: " + string.Join(", ", eslesmeyen.ToArray()) : "";
        Debug.Log("TorTextureUygula: " + sayi + " materyal yuvasina uygulandi." + ek);
        EditorUtility.DisplayDialog("Tor Texture", sayi + " materyal yuvasina uygulandi." + ek, "Tamam");
    }

    static void Hata(string mesaj)
    {
        Debug.LogWarning("TorTextureUygula: " + mesaj);
        EditorUtility.DisplayDialog("Tor Texture", mesaj, "Tamam");
    }
}
#endif
