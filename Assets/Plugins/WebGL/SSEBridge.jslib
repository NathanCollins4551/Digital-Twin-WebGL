// this file is the bridge between unity's c# and the browser. 
// it handles all the actual web requests and streams so unity doesn't have to deal with CORS or webgl networking quirks.
mergeInto(LibraryManager.library, {

    FetchAllPrinterStatuses: function () {
        // grabs the status of every machine in the farm at once. 
        // unity uses this to keep the 3d animations synced up with reality.
        fetch('https://newtwinbackend.quangphuly.online/api/printers')
            .then(response => response.json())
            .then(data => {
                SendMessage('AnimationManager', 'OnFleetStatusUpdate', JSON.stringify(data));
            })
            .catch(err => console.error("[JS] Fleet sync failed", err));
    },

    CheckPrinterStatus: function (deviceId) {
        // fetches data for just a single machine when the user clicks on it in the scene
        var id = UTF8ToString(deviceId);
        fetch('https://newtwinbackend.quangphuly.online/api/printers/' + id)
            .then(response => response.json())
            .then(data => {
                SendMessage('Sidebar_Dashboard', 'InitializeDashboardState', JSON.stringify(data));
            })
            .catch(err => {
                // if the server is unreachable, we just fake an offline payload so the unity UI handles it gracefully
                console.error("[JS] JSLib Fetch Error:", err);
                SendMessage('Sidebar_Dashboard', 'InitializeDashboardState', JSON.stringify({isRunning: false, isSimulationControlled: false}));
            });
    },

    OpenSSEStream: function (deviceId) {
        var id = UTF8ToString(deviceId);
        
        // kill any old stream before starting a new one so we dont end up with duplicate listeners
        if (window.unitySSE) { window.unitySSE.close(); window.unitySSE = null; }

        // this is a bit of a combo move. first we grab a snapshot of the last minute of data.
        // this makes the UI populate instantly instead of waiting for the first live SSE ping.
        fetch('https://newtwinbackend.quangphuly.online/api/printers/' + id + '/telemetry?minutes=1')
            .then(response => response.json())
            .then(data => {
                if (!data || (Array.isArray(data) && data.length === 0)) return;
                var snap = Array.isArray(data) ? data[0] : data;
                var result = {
                    latestProgressPercent: snap.progressPercent || 0,
                    avgNozzleTempC: snap.nozzleTempC || 0,
                    avgBedTempC: snap.bedTempC || 0,
                    currentLayer: snap.currentLayer || 0,
                    totalLayers: snap.totalLayers || 0
                };
                SendMessage('Sidebar_Dashboard', 'SetInitialTelemetry', JSON.stringify(result));
            })
            .catch(err => console.warn("[JS] Snapshot fetch failed", err));

        // slight delay before opening the live firehose just to ensure the snapshot finishes first
        setTimeout(function() {
            if (window.unitySSE) return;
            window.unitySSE = new EventSource('https://newtwinbackend.quangphuly.online/api/printers/' + id + '/telemetry/stream');
            window.unitySSE.addEventListener("telemetry", (event) => {
                if (window.unitySSE) SendMessage('Sidebar_Dashboard', 'OnStreamUpdate', event.data);
            });
            window.unitySSE.onerror = function() { if (window.unitySSE) { window.unitySSE.close(); window.unitySSE = null; } };
        }, 100);
    },

    CloseSSEStream: function () {
        // important to call this when the sidebar closes so we don't bleed memory
        if (window.unitySSE) { window.unitySSE.close(); window.unitySSE = null; }
    },
    
    OpenInventoryStream: function () {
        if (window.inventorySSE) { window.inventorySSE.close(); }

        console.log("%c[JS] Attempting to connect to Inventory Stream...", "color: cyan; font-weight: bold;");
        
        // this feed runs constantly in the background to watch for filament spools moving around the tables
        window.inventorySSE = new EventSource('https://newtwinbackend.quangphuly.online/api/inventory/zones/stream');
        
        window.inventorySSE.addEventListener("zone_update", (event) => {
            console.log("%c[JS] Inventory Data Received (zone_update):", "color: lime;", event.data);
            SendMessage('InventoryManager', 'OnInventoryUpdate', event.data);
        });

        // catching generic messages too wether or not they have the custom "zone_update" label
        window.inventorySSE.onmessage = function(event) {
            console.log("%c[JS] Inventory Data Received (message):", "color: lime;", event.data);
            SendMessage('InventoryManager', 'OnInventoryUpdate', event.data);
        };

        window.inventorySSE.onopen = function() {
            console.log("%c[JS] Inventory Stream Connection Opened!", "color: green;");
        };

        window.inventorySSE.onerror = function(err) {
            console.error("[JS] Inventory Stream Error. State:", window.inventorySSE.readyState);
        };
    },

    CloseInventoryStream: function () {
        if (window.inventorySSE) {
            window.inventorySSE.close();
            window.inventorySSE = null;
            console.log("[JS] Inventory Stream Closed.");
        }
    }
});