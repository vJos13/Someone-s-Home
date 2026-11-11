// Someone's Home · entrada del navegador (ver Scripts/Player/BrowserInput.cs).
// Pellizco en touchpad = evento "wheel" con ctrlKey: se evita el zoom de la página para que Unity lo use
// como zoom de cámara. La rueda normal no se toca (Unity ya la recibe).
mergeInto(LibraryManager.library, {
  SH_BlockPageZoom: function () {
    var canvas = (typeof Module !== "undefined" && Module.canvas) ? Module.canvas : document.querySelector("canvas");
    if (!canvas) return;
    canvas.addEventListener("wheel", function (e) {
      if (e.ctrlKey) e.preventDefault();
    }, { passive: false });
    // Safari (macOS): gestos propios de pellizco.
    ["gesturestart", "gesturechange", "gestureend"].forEach(function (name) {
      canvas.addEventListener(name, function (e) { e.preventDefault(); }, { passive: false });
    });
  }
});
