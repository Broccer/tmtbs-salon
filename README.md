# Trade of MTBS · Seans Salonu (Unity)

Trade of MTBS hesabıyla girilen 3B seans salonu. Fiyatlar, portföy ve emirler uygulamanın sunucusundan
(`https://mutesebbis.tr/api/v1/game/tmtbs/*`) gelir; aynı hesap, aynı portföy.

- **Giriş:** e-posta + şifre. Google/Apple ile kaydolanlar "Şifre belirle" ile e-postaya gelen kodla şifre koyar.
- **Demo:** girişsiz gezinti; uydurma şirketler, sanal para.
- **Salon:** ön duvarda dev piyasa tablosu ve kayan bant, yan duvarlarda saat ve endeks, piyasa değerine göre ilk 12 hisse için işlem kürsüleri,
  sert hareketlerde kürsüye koşup bağıran bot borsacılar, seans zili.
- **İşlem:** kürsüde E (ya da tıkla) → emir defteri, adet, AL (en iyi satış fiyatından) / SAT (en iyi alış fiyatından).

Kontroller: W/S yürü, A/D dön, Shift koş, E işlem, Esc kapat.

Sahne tamamen koddan kurulur (`Assets/Scripts/Salon/SalonBuilder.cs`). Derleme `.github/workflows/webgl.yml` (game-ci, secret'lar
`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`), çıktı GitHub Pages.
