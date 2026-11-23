using UnityEngine;

/// <summary>MTBS Bankası veznesi: kredi durumu, ödeme planı, hareketler; kredi çek ve borç öde.</summary>
public partial class SalonDirector
{
    string payText = "";
    string bankMsg;
    bool bankOk, bankBusy;

    void OpenBank()
    {
        OpenPanelCommon(Panel.Bank);
        bankMsg = null;
        payText = "";
        Market.I.RefreshStatus();
    }

    static string CreditState(string d)
    {
        switch (d)
        {
            case "aktif": return "<color=#5BE38A>Aktif</color>";
            case "bitti": return "Kapandı";
            case "gecikmede": return "<color=#FF7A7A>Gecikmede</color>";
            case "": return "Kredi yok";
            default: return d;
        }
    }

    void DrawBank()
    {
        var m = Market.I;
        var r = PanelRect(480);
        BeginPanel(r);
        GUILayout.BeginHorizontal();
        GUILayout.Label("<b>MTBS Bankası · Vezne</b>", h2);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("✕", btn, GUILayout.Width(44), GUILayout.Height(36))) { EndPanel(); ClosePanel(); return; }
        GUILayout.EndHorizontal();

        if (!m.Live)
        {
            GUILayout.Label("Kredi çekmek ve borç ödemek için hesabınla giriş yapmalısın. Demo kipinde banka işlemi yapılamaz.", label);
            GUILayout.Space(6);
            GUILayout.Label("Kredi: 100.000 MLS, beş günde 20.000'lik dilimler halinde hesabına yatar. 30 günde bir 10.000 MLS taksit otomatik tahsil edilir; geciken taksitte %5 ceza işler ve borç ödenene kadar alış yapılamaz.", small);
            GUILayout.Space(10);
            if (GUILayout.Button("Kapat", GUILayout.Height(38))) { EndPanel(); ClosePanel(); return; }
            EndPanel();
            return;
        }

        var k = m.credit;
        Row("Nakit", Fmt.Mls(m.cash));
        string durum = J.S(k, "durum");
        Row("Kredi", CreditState(durum));
        if (k.Count > 0 && durum != "")
        {
            Row("Kredi tutarı", Fmt.Mls(J.D(k, "anapara")));
            Row("Hesabına yatan", Fmt.Mls(J.D(k, "verilen")));
            if (J.D(k, "dilim_sayisi") > 0)
                Row("Dilimler", $"{Fmt.N(J.D(k, "dilim_verilen"))} / {Fmt.N(J.D(k, "dilim_sayisi"))} · {Fmt.Mls(J.D(k, "dilim"))}");
            if (J.S(k, "sonraki_dilim") != "") Row("Sonraki dilim", ShortDate(J.S(k, "sonraki_dilim")));
            Row("Kalan borç", $"<b>{Fmt.Mls(J.D(k, "kalan"))}</b>");
            Row("Ödenen", Fmt.Mls(J.D(k, "odenen")));
            Row("Taksit", Fmt.Mls(J.D(k, "taksit")));
            if (J.S(k, "sonraki_vade") != "") Row("Sonraki vade", $"{ShortDate(J.S(k, "sonraki_vade"))} · {Fmt.N(J.D(k, "kalan_gun"))} gün");
            if (J.D(k, "gecikmis") > 0) Row("<color=#FF7A7A>Gecikmiş</color>", $"<color=#FF7A7A>{Fmt.Mls(J.D(k, "gecikmis"))}</color>");
            if (J.D(k, "ceza_toplam") > 0) Row("Ceza toplamı", Fmt.Mls(J.D(k, "ceza_toplam")));
            if (J.B(k, "alis_yasak")) GUILayout.Label("<color=#FF7A7A>Gecikmiş taksidin var: ödeyene kadar borsada alış yapamazsın.</color>", small);
        }
        else GUILayout.Label("Henüz kredi kullanmadın. 100.000 MLS'lik kredi beş günde 20.000'lik dilimlerle hesabına yatar.", small);

        GUILayout.Space(8);
        GUI.enabled = !bankBusy;
        GUI.backgroundColor = new Color(0.35f, 0.7f, 1f);
        if (GUILayout.Button(durum == "aktif" ? "Sıradaki dilimi şimdi çek" : "Kredi çek (100.000 MLS)", btn, GUILayout.Height(46)))
        {
            bankBusy = true;
            bankMsg = null;
            m.CreditDraw((msg, ok) => { bankBusy = false; bankMsg = msg; bankOk = ok; if (ok && Bell.I) Bell.I.Ring(); });
        }
        GUI.backgroundColor = Color.white;

        double kalan = J.D(k, "kalan");
        if (kalan > 0)
        {
            GUILayout.Label("Borç ödemesi (nakitten)", small);
            GUILayout.BeginHorizontal();
            payText = HtmlInput.Field("odeme", payText, "Ödenecek tutar (MLS)", field, false, true, 12, GUILayout.Height(40), GUILayout.MinWidth(120));
            if (GUILayout.Button("Öde", btn, GUILayout.Width(80), GUILayout.Height(40)))
            {
                long amt = (long)ParsePrice(payText);
                if (amt <= 0) { bankMsg = "Bir tutar yaz."; bankOk = false; }
                else Pay(amt);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            double tk = J.D(k, "taksit");
            if (tk > 0 && GUILayout.Button("Bir taksit (" + Fmt.N(System.Math.Min(tk, kalan)) + ")", GUILayout.Height(34))) Pay((long)System.Math.Min(tk, kalan));
            if (GUILayout.Button("Tamamını öde (" + Fmt.N(kalan) + ")", GUILayout.Height(34))) Pay((long)System.Math.Ceiling(kalan));
            GUILayout.EndHorizontal();
        }
        GUI.enabled = true;
        if (bankBusy) GUILayout.Label("İşleniyor…", small);
        Msg(bankMsg, bankOk);

        var plan = J.A(k, "plan");
        if (plan.Count > 0)
        {
            Title("Ödeme planı");
            for (int i = 0; i < plan.Count && i < 12; i++) Row(ShortDate(J.S(plan[i], "vade")), Fmt.Mls(J.D(plan[i], "tutar")));
        }
        var moves = J.A(k, "hareketler");
        if (moves.Count > 0)
        {
            Title("Hareketler");
            for (int i = 0; i < moves.Count && i < 15; i++)
            {
                var h = moves[i];
                Row($"{ShortDate(J.S(h, "tarih"))} · {Pretty(J.S(h, "tur"))}", $"{Fmt.Mls(J.D(h, "tutar"))} <size=12>(kalan {Fmt.N(J.D(h, "kalan"))})</size>");
            }
        }
        GUILayout.Space(10);
        if (GUILayout.Button("Kapat (Esc)", GUILayout.Height(36))) { EndPanel(); ClosePanel(); return; }
        EndPanel();
    }

    void Pay(long amt)
    {
        bankBusy = true;
        bankMsg = null;
        Market.I.CreditPay(amt, (msg, ok) => { bankBusy = false; bankMsg = msg; bankOk = ok; if (ok) payText = ""; });
    }
}
