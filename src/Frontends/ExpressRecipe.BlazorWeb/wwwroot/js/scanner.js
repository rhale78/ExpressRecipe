// scanner.js - Camera & barcode scanning interop for Blazor
// Uses ZXing browser barcode reader (loaded via CDN script tag)
// Loaded conditionally only on /scanner page

window.scannerInterop = (function () {
    'use strict';

    let _stream = null;
    let _animFrameId = null;
    let _dotNetRef = null;
    let _videoEl = null;
    let _canvasEl = null;
    let _lastDecodeTime = 0;
    const DEBOUNCE_MS = 800;

    // ZXing reader instance (lazy-created)
    let _reader = null;

    function getReader() {
        if (!_reader && window.ZXing) {
            _reader = new window.ZXing.BrowserMultiFormatReader();
        }
        return _reader;
    }

    async function startCamera(dotNetRef, videoId, canvasId, facingMode) {
        _dotNetRef = dotNetRef;
        _videoEl = document.getElementById(videoId);
        _canvasEl = document.getElementById(canvasId);

        if (!_videoEl || !_canvasEl) {
            console.error('scanner.js: video or canvas element not found', videoId, canvasId);
            return;
        }

        try {
            _stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: facingMode || 'environment', width: 1280, height: 720 }
            });
            _videoEl.srcObject = _stream;
            await _videoEl.play();
            scheduleFrame();
        } catch (err) {
            const errorName = err.name || 'UnknownError';
            if (errorName === 'NotAllowedError' || errorName === 'NotFoundError') {
                if (_dotNetRef) {
                    await _dotNetRef.invokeMethodAsync('OnCameraUnavailable', errorName);
                }
            } else {
                if (_dotNetRef) {
                    await _dotNetRef.invokeMethodAsync('OnCameraUnavailable', errorName);
                }
            }
        }
    }

    function scheduleFrame() {
        _animFrameId = requestAnimationFrame(processFrame);
    }

    async function processFrame() {
        if (!_stream || !_videoEl || !_canvasEl) {
            return;
        }

        const ctx = _canvasEl.getContext('2d');
        _canvasEl.width = _videoEl.videoWidth || 640;
        _canvasEl.height = _videoEl.videoHeight || 480;
        ctx.drawImage(_videoEl, 0, 0, _canvasEl.width, _canvasEl.height);

        const now = Date.now();
        if (now - _lastDecodeTime >= DEBOUNCE_MS) {
            const reader = getReader();
            if (reader) {
                try {
                    const result = await reader.decodeFromCanvas(_canvasEl);
                    if (result) {
                        _lastDecodeTime = now;
                        const captureBase64 = _canvasEl.toDataURL('image/jpeg', 0.7).split(',')[1];
                        if (_dotNetRef) {
                            await _dotNetRef.invokeMethodAsync('OnBarcodeDetected', {
                                upc: result.getText(),
                                captureBase64: captureBase64
                            });
                        }
                    }
                } catch (decodeError) {
                    // ZXing throws NotFoundException when no barcode is found — this is normal
                    if (decodeError && decodeError.name !== 'NotFoundException') {
                        console.warn('scanner.js: unexpected decode error', decodeError);
                    }
                }
            }
        }

        // Continue loop only if stream is still active
        if (_stream) {
            scheduleFrame();
        }
    }

    function stopCamera() {
        if (_animFrameId !== null) {
            cancelAnimationFrame(_animFrameId);
            _animFrameId = null;
        }

        if (_stream) {
            _stream.getTracks().forEach(function (track) { track.stop(); });
            _stream = null;
        }

        if (_videoEl) {
            _videoEl.srcObject = null;
            _videoEl = null;
        }

        _canvasEl = null;
        _dotNetRef = null;
        _reader = null;
    }

    function captureFrame(canvasId) {
        if (!_stream) {
            return null;
        }

        const canvas = document.getElementById(canvasId);
        if (!canvas) {
            return null;
        }

        const dataUrl = canvas.toDataURL('image/jpeg', 0.7);
        return dataUrl.split(',')[1] || null;
    }

    function checkCameraAvailable() {
        return !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia);
    }

    function savePreference(key, value) {
        try {
            localStorage.setItem('scanner.' + key, JSON.stringify(value));
        } catch (storageError) {
            // localStorage may be unavailable (private browsing, quota exceeded)
            void storageError;
        }
    }

    function loadPreference(key) {
        try {
            const raw = localStorage.getItem('scanner.' + key);
            return raw !== null ? JSON.parse(raw) : null;
        } catch (storageError) {
            // localStorage may be unavailable or contain unparseable data
            void storageError;
            return null;
        }
    }

    function canGoBack() {
        return window.history.length > 1;
    }

    function goBack() {
        if (window.history.length > 1) {
            window.history.back();
        }
    }

    return {
        startCamera: startCamera,
        stopCamera: stopCamera,
        captureFrame: captureFrame,
        checkCameraAvailable: checkCameraAvailable,
        savePreference: savePreference,
        loadPreference: loadPreference,
        canGoBack: canGoBack,
        goBack: goBack
    };
})();
