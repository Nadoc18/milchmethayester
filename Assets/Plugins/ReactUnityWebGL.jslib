mergeInto(LibraryManager.library, {
  z: function (z1) {
    dispatchReactUnityEvent("z", Pointer_stringify(z1));
  },
  x: function (x1) {
    dispatchReactUnityEvent("x", Pointer_stringify(x1));
  },
 t: function (t1) {
    dispatchReactUnityEvent("t", Pointer_stringify(t1));
  },
});
