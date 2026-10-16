mergeInto(LibraryManager.library, {
    $SleepyTapTap__deps: ['$UTF8ToString', '$lengthBytesUTF8', '$stringToUTF8', '$WEBAudio', 'JS_Sound_ResumeIfNeeded', 'malloc'],
    $SleepyTapTap: {
        receiver: null,
        visibility: null,
        pageHide: null,
        pageShow: null,
        focus: null,
        blur: null,
        hidden: false,
        result: function (value) {
            var json = JSON.stringify(value);
            var size = lengthBytesUTF8(json) + 1;
            var pointer = _malloc(size);
            stringToUTF8(json, pointer, size);
            return pointer;
        },
        error: function (error) {
            return error && error.message || String(error);
        },
        updateVisibility: function (hidden) {
            if (SleepyTapTap.hidden === hidden) return;
            SleepyTapTap.hidden = hidden;
            Module.SendMessage(SleepyTapTap.receiver, 'OnTapLifecycle', hidden ? '1' : '0');
            SleepyTapTap.updateAudio();
        },
        updateAudio: function () {
            if (!WEBAudio.audioWebEnabled || !WEBAudio.audioContext) return;
            if (SleepyTapTap.hidden) {
                // UI sounds can ignore AudioListener.pause, so backgrounding also suspends the browser output.
                WEBAudio.audioContext.suspend().then(function () {
                    if (!SleepyTapTap.hidden) _JS_Sound_ResumeIfNeeded();
                }).catch(function (error) { console.warn('H5 audio suspend failed: ' + SleepyTapTap.error(error)); });
            } else {
                // Unity also retries from mouse/touch gestures when browser autoplay policy blocks resume.
                _JS_Sound_ResumeIfNeeded();
            }
        },
        validatePath: function (relative) {
            if (!relative || /[\\:\x00]/.test(relative) || relative.split('/').some(function (part) {
                return !part || part === '.' || part === '..';
            })) throw new Error('Save path must remain inside the persistent directory.');
        }
    },

    SleepyTapTap_Storage__deps: ['$SleepyTapTap'],
    SleepyTapTap_Storage: function (operation, gameIdPointer, pathPointer, valuePointer) {
        try {
            var gameId = UTF8ToString(gameIdPointer);
            if (!gameId) throw new Error('The release GameId is required.');
            var key = UTF8ToString(pathPointer);
            if (!key) throw new Error('A storage key is required.');
            if (operation >= 2) SleepyTapTap.validatePath(key);
            // One setItem replaces the whole record atomically; quota/security failures preserve the previous save.
            // shortcut: browser localStorage quota limits snapshots; use async IndexedDB only when saves outgrow it.
            var storageKey = 'SleepyDemos:' + gameId + (operation < 2 ? ':value:' : ':file:') + key;
            if (operation === 0 || operation === 2 || operation === 4) {
                var stored = localStorage.getItem(storageKey);
                if (operation === 0) return SleepyTapTap.result({ ok: true, value: stored });
                if (stored === null) return SleepyTapTap.result({ ok: false, notFound: true, error: 'Save file does not exist.' });
                var snapshot = JSON.parse(stored);
                if (!snapshot || typeof snapshot.value !== 'string' || !Number.isSafeInteger(snapshot.modifiedMilliseconds)
                    || snapshot.modifiedMilliseconds < 0) throw new Error('Save snapshot is corrupt.');
                return SleepyTapTap.result({ ok: true, value: snapshot.value, modifiedMilliseconds: snapshot.modifiedMilliseconds });
            }
            var value = UTF8ToString(valuePointer);
            if (operation === 1) {
                localStorage.setItem(storageKey, value);
            } else if (operation === 3) {
                localStorage.setItem(storageKey, JSON.stringify({ value: value, modifiedMilliseconds: Date.now() }));
            } else {
                throw new Error('Unknown H5 storage operation.');
            }
            return SleepyTapTap.result({ ok: true });
        } catch (error) {
            return SleepyTapTap.result({ ok: false, error: SleepyTapTap.error(error) });
        }
    },

    SleepyTapTap_Initialize__deps: ['$SleepyTapTap'],
    SleepyTapTap_Initialize: function (receiverPointer) {
        try {
            if (SleepyTapTap.receiver) throw new Error('H5 lifecycle is already registered.');
            SleepyTapTap.receiver = UTF8ToString(receiverPointer);
            SleepyTapTap.hidden = document.hidden || !document.hasFocus();
            SleepyTapTap.visibility = function () { SleepyTapTap.updateVisibility(document.hidden || !document.hasFocus()); };
            SleepyTapTap.pageHide = function () { SleepyTapTap.updateVisibility(true); };
            SleepyTapTap.pageShow = function () { SleepyTapTap.visibility(); };
            SleepyTapTap.blur = function () { SleepyTapTap.updateVisibility(true); };
            SleepyTapTap.focus = function () { SleepyTapTap.visibility(); };
            document.addEventListener('visibilitychange', SleepyTapTap.visibility);
            window.addEventListener('pagehide', SleepyTapTap.pageHide);
            window.addEventListener('pageshow', SleepyTapTap.pageShow);
            window.addEventListener('blur', SleepyTapTap.blur);
            window.addEventListener('focus', SleepyTapTap.focus);
            SleepyTapTap.updateAudio();
            return SleepyTapTap.result({ ok: true, hidden: SleepyTapTap.hidden });
        } catch (error) {
            return SleepyTapTap.result({ ok: false, error: SleepyTapTap.error(error) });
        }
    },

    SleepyTapTap_Shutdown__deps: ['$SleepyTapTap'],
    SleepyTapTap_Shutdown: function () {
        if (!SleepyTapTap.receiver) return;
        document.removeEventListener('visibilitychange', SleepyTapTap.visibility);
        window.removeEventListener('pagehide', SleepyTapTap.pageHide);
        window.removeEventListener('pageshow', SleepyTapTap.pageShow);
        window.removeEventListener('blur', SleepyTapTap.blur);
        window.removeEventListener('focus', SleepyTapTap.focus);
        SleepyTapTap.receiver = null;
        SleepyTapTap.visibility = SleepyTapTap.pageHide = SleepyTapTap.pageShow = null;
        SleepyTapTap.blur = SleepyTapTap.focus = null;
    }
});
