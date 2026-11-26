// Trade of MTBS: Google / Apple ile giriş. Kimlik sayfası mutesebbis.tr'de açılır (Google kaynağı ve Apple dönüş
// adresi o alan adına kayıtlı); kimlik jetonu bu sayfaya postMessage ile (açılır pencere engellenirse adres
// parçasıyla, #tmtbs_social=...) geri gelir. Yalnız https://mutesebbis.tr kaynağından gelen ileti kabul edilir.
mergeInto(LibraryManager.library, {
  SocialStart: function (provPtr) {
    var prov = UTF8ToString(provPtr);
    var url = "https://mutesebbis.tr/tmtbs-giris.html?p=" + encodeURIComponent(prov) + (location.origin === "https://mutesebbis.tr" ? "&g=mt" : "");
    function send(m) {
      try {
        if (window.unityInstance) window.unityInstance.SendMessage("SocialLogin", "OnSocial", m);
        else if (typeof SendMessage === "function") SendMessage("SocialLogin", "OnSocial", m);
      } catch (e) { console.warn(e); }
    }
    if (!window.__tmSocialListen) {
      window.__tmSocialListen = true;
      window.addEventListener("message", function (e) {
        if (e.origin !== "https://mutesebbis.tr") return;
        var d = e.data || {};
        if (d.type !== "tmtbs-social") return;
        send([d.provider || "", d.token || "", d.name || "", d.error || ""].join("\u0001"));
      });
    }
    var w = null;
    try { w = window.open(url + "&m=popup", "tmtbs_social", "width=480,height=660"); } catch (e) { w = null; }
    if (!w) location.href = url + "&m=redirect";
  },
  SocialPending: function () {
    var h = location.hash || "";
    var key = "#tmtbs_social=";
    if (h.indexOf(key) !== 0) return 0;
    var v = "";
    try { v = decodeURIComponent(h.substring(key.length)); } catch (e) { v = ""; }
    try { history.replaceState(null, "", location.pathname + location.search); } catch (e) { }
    var size = lengthBytesUTF8(v) + 1;
    var buf = _malloc(size);
    stringToUTF8(v, buf, size);
    return buf;
  }
});
