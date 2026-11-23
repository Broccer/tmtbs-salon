// Telefonda yazı girişi: Unity'nin IMGUI alanı sanal klavyeyi açmadığı için
// sayfanın üstüne gerçek bir <input> kutusu açar, sonucu Unity'ye geri yollar.
mergeInto(LibraryManager.library, {
  HtmlInputOpen: function (idPtr, valuePtr, titlePtr, password, numeric) {
    var id = UTF8ToString(idPtr);
    var value = UTF8ToString(valuePtr);
    var title = UTF8ToString(titlePtr);
    var old = document.getElementById("hi-wrap");
    if (old) old.remove();

    var wrap = document.createElement("div");
    wrap.id = "hi-wrap";
    wrap.style.cssText = "position:fixed;inset:0;z-index:9999;background:rgba(5,10,20,.72);display:flex;align-items:flex-start;justify-content:center;padding-top:12vh;font-family:system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;touch-action:auto";
    var box = document.createElement("div");
    box.style.cssText = "background:#121b2e;border:1px solid #ffd54a55;border-radius:14px;padding:18px;width:min(88vw,420px);box-shadow:0 10px 40px #0008";
    var lab = document.createElement("div");
    lab.textContent = title;
    lab.style.cssText = "color:#ffd54a;font-weight:700;font-size:17px;margin-bottom:10px";
    var inp = document.createElement("input");
    inp.type = password ? "password" : (numeric ? "tel" : "text");
    if (numeric) inp.inputMode = "decimal";
    if (!password && !numeric && title.toLowerCase().indexOf("posta") >= 0) { inp.type = "email"; }
    inp.value = value;
    inp.autocomplete = "off";
    inp.autocapitalize = "off";
    inp.spellcheck = false;
    inp.style.cssText = "width:100%;box-sizing:border-box;font-size:18px;padding:12px;border-radius:10px;border:1px solid #3a4a6b;background:#0b1220;color:#fff;outline:none";
    var row = document.createElement("div");
    row.style.cssText = "display:flex;gap:10px;margin-top:14px";
    function mk(t, bg) {
      var b = document.createElement("button");
      b.textContent = t;
      b.style.cssText = "flex:1;font-size:17px;font-weight:700;padding:12px;border:0;border-radius:10px;color:#fff;background:" + bg;
      return b;
    }
    var cancel = mk("Vazgeç", "#3a4a6b");
    var ok = mk("Tamam", "#0fa8ad");
    row.appendChild(cancel);
    row.appendChild(ok);
    box.appendChild(lab);
    box.appendChild(inp);
    box.appendChild(row);
    wrap.appendChild(box);
    document.body.appendChild(wrap);

    function send(accepted) {
      var v = inp.value;
      wrap.remove();
      var msg = (accepted ? "1" : "0") + id + "\u0001" + v;
      try {
        if (window.unityInstance) window.unityInstance.SendMessage("HtmlInput", "OnHtmlInput", msg);
        else if (typeof SendMessage === "function") SendMessage("HtmlInput", "OnHtmlInput", msg);
      } catch (e) { console.warn(e); }
    }
    ok.addEventListener("click", function () { send(true); });
    cancel.addEventListener("click", function () { send(false); });
    inp.addEventListener("keydown", function (e) {
      e.stopPropagation();
      if (e.key === "Enter") { e.preventDefault(); send(true); }
      if (e.key === "Escape") { e.preventDefault(); send(false); }
    });
    ["keyup", "keypress"].forEach(function (n) { inp.addEventListener(n, function (e) { e.stopPropagation(); }); });
    setTimeout(function () { inp.focus(); try { inp.select(); } catch (e) {} }, 30);
  },

  IsTouchDevice: function () {
    return (("ontouchstart" in window) || (navigator.maxTouchPoints || 0) > 0) ? 1 : 0;
  }
});
