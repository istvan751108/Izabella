using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public enum SaleType
    {
        [Display(Name = "Vágás")] Slaughter = 1,
        [Display(Name = "Továbbtartás")] FurtherBreeding = 2,
        [Display(Name = "Export")] Export = 3,
        [Display(Name = "Tulajdonosváltás")] OwnershipChange = 4
    }

    public enum TreatmentCategory
    { Tőgy, Láb, Szaporodás, Apasztás, Egyéb }

    public enum FootTreatmentType
    {
        // Gyógyszer nélküli - Tömeges
        Lábfürösztés,

        Körmözés,

        // Gyógyszeres - Alkalmi / Egyedi
        CsülökirhaGyulladás,

        LábszerkezetBetegség,
        Lábszétcsúszás,
        Lábtörés,
        NyíltSeb,
        Sántaság,
        Talpfekély
    }

    public enum ReproTreatmentType
    {
        // Tömeges protokoll (Ovsynch)
        Tömeges_Ovsynch_Protokoll,

        // Egyedi kezelések
        Cseppenő_iv, Császármetszés, Ellés_El_elfekv, Ellés_ut_elfekv,

        Embriófelszivódás, Farfekvéses_ellés, Follikul_ciszta, Gennyes_Ivarzás,
        Gennyes_Méhgyulladás, Gátrepedés, Hüvelyelőesés, Inaktív_Pf_Leivarzott,
        Lezúzva, Mb, Meddőség, Méh_Pf_Zsugor, Méhdaganat, Méhelőesés,
        Méhkezelés, Nehéz_ellés, Perimitritis, Petevezeték_gyulladás,
        Pf_ciszta, Péra_hüvely_Szk, Selejt_Méh, Sorvadt_petef,
        Sárgatest_ciszta, Tüsző_Bal_pf, Tüsző_jobb_Pf, Vetélés, Véres_ivarzás, Üres
    }

    public enum OtherTreatmentType
    {
        Acidózis, Alacsony_termelés, Alkati_gyenge, Általános_gyengeség,
        Anyagforgalmi_betegség, Belső_vérzés, Bélcsavarodás, Coli_hasmenés,
        Deformált_tőgy, Felfúvódás, Fulladás, Gennyes_orfolyás,
        Hashártya_gyulladás, Hasmenés, Ismeretlen, Ketózis,
        Koros_Váladék_kif, Köldöksérv, Kóros_soványság, Leszakadt_Tőgy,
        Mérgezés, Nem_fertőző_betegség, Nyílt_seb, Oltógyomor_helyz_va,
        Szarvtalanítás, Szemgyulladás, Szívburok_gyulladás, Sérülés,
        Taposás, Tüdőgyulladás, Vak, Vastagbél_perforáció,
        Vesegyulladás, Zsírmáj_szindróma
    }
}