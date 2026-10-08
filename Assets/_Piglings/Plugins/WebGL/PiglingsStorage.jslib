// The browser side of BrowserSave.cs (T8): the save's copy in localStorage, which survives a new itch upload (the
// IndexedDB file doesn't — it's keyed by the upload's URL). Every call is wrapped: a browser that blocks storage
// (private browsing, a full quota) just means no copy — the game plays on.
mergeInto(LibraryManager.library, {
  PiglingsStorageGet: function (key) {
    try {
      var value = window.localStorage.getItem(UTF8ToString(key));
      if (value === null) return null;
      // Returned to C# as a string: C# frees this buffer after copying it.
      var size = lengthBytesUTF8(value) + 1;
      var buffer = _malloc(size);
      stringToUTF8(value, buffer, size);
      return buffer;
    } catch (e) {
      return null;
    }
  },

  PiglingsStorageSet: function (key, value) {
    try {
      window.localStorage.setItem(UTF8ToString(key), UTF8ToString(value));
      return 1;
    } catch (e) {
      return 0;
    }
  },

  PiglingsStorageRemove: function (key) {
    try {
      window.localStorage.removeItem(UTF8ToString(key));
    } catch (e) {}
  }
});
