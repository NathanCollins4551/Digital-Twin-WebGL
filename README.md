# Digital Twin WebGL Project

This project is a high-fidelity **Digital Twin** of a 3D printing farm, built in **Unity 6** and optimized for **WebGL**. It features real-time synchronization with physical hardware via Server-Sent Events (SSE), an interactive dashboard for telemetry monitoring, and an AI-driven interface powered by **Convai**.

##  Technical Specifications
- **Unity Editor Version:** `6000.4.0f1` (Unity 6)
- **Render Pipeline:** Universal Render Pipeline (URP)
- **Target Platform:** WebGL (Desktop)
- **AI Integration:** Convai SDK (gRPC-Web)
- **Networking:** Custom JSLib SSE Bridge for real-time telemetry.

---

##  Main Functionality & Features

### 1. Real-Time Printer Monitoring
The project maintains a live 3D representation of a printing farm. 
- **Fleet Sync:** `FleetAnimationManager` pings the backend every 60 seconds to update the "Running/Idle" state of every machine in the scene.
- **Visual Feedback:** Printers that are actively printing play animations and show glowing status indicators, while idle machines remain static.

### 2. Interactive Sidebar Dashboard
When a user clicks on a printer (or asks the AI to show one), a sidebar appears:
- **Telemetry Stream:** Uses a dedicated SSE connection to stream live nozzle temperature, bed temperature, and print progress (layer-by-layer).
- **History Snapshot:** Upon opening, the bridge fetches the last minute of data to populate charts/bars instantly before the live stream takes over.
- **Interactive Gauges:** Real-time progress bars for nozzle/bed temperatures and total print progress.

### 3. Filament & Inventory Management
A specialized `FilamentManager` tracks the physical location of filament spools on workstations.
- **Computer Vision Integration:** The backend sends "Zone Updates" based on camera feeds.
- **3D Visualization:** Spools in the Unity scene automatically move between "Storage" and "Active Zones" (Left/Right tables) based on real-world inventory changes.

### 4. Highlighting & Selection
A robust `HighlightingService` provides visual feedback by generating "Outline Hulls" around complex 3D models. This allows users (and the AI) to clearly identify which machine is being targeted.

---

##  The SSE Bridge (WebGL Integration)

Standard C# `EventSource` implementations often fail in WebGL due to browser security (CORS) and threading limitations. This project uses a custom **SSE Bridge** (`SSEBridge.jslib`) to handle networking.

- **How it works:** Unity calls JavaScript functions via `DllImport`, and JavaScript uses the native browser `EventSource` API to listen to the backend.
- **Data Flow:** When data arrives in JavaScript, it is passed back to Unity using `SendMessage`, targeting specific managers (e.g., `Sidebar_Dashboard`, `InventoryManager`).
- **Memory Management:** The bridge automatically closes streams when dashboards are closed to prevent memory leaks and unnecessary network overhead.

---

##  Convai Actions & AI Integration

The project features a "Digital Twin Assistant" that can perform physical actions in the scene.

### Current Custom Actions:
- **`Highlight`**: AI can visually point out a specific printer.
- **`Display Printer Dashboard`**: AI can open the telemetry sidebar for a specific station.
- **`Show Me`**: AI leads the player's camera to a specific object.
- **`MoveTo` / `PickUp` / `Drop`**: AI can interact with the environment and move spools.

###  How to Setup New Convai Actions
To add a new capability to the AI:

1.  **Create the Action Class:** Create a new C# script in `Assets/Convai/Scripts/Runtime/Features/Actions/CustomActions/` that implements the `ICustomAction` interface.
    ```csharp
    public class MyNewAction : ICustomAction {
        public string ActionName => "Do Something Cool";
        public void Initialize(ConvaiActionsHandler handler) { /* Setup */ }
        public IEnumerator Execute(GameObject target) {
            // Your logic here
            yield return null;
        }
    }
    ```
2.  **Register the Action:** Open `ConvaiActionsHandler.cs` and add your new class to the `RegisterCustomActions()` method.
3.  **Sync with Cloud:** The `ConvaiActionsHandler` automatically sends your new `ActionName` to the Convai Cloud on startup. 
4.  **Configure in Convai Dashboard:** Ensure your Character on the [Convai Dashboard](https://dashboard.convai.com) has the exact same action name (case-insensitive) in its "Actions" list.

---

##  Digital Twin Scene Setup

### Printer Prefabs
Every printer in the scene must have:
- **`PrinterObject` Script:** 
    - `Printer Model`: The name of the hardware (e.g., "Bambu A1").
    - `Device ID`: The unique UUID matching the backend database.
- **Collider:** Set to the `Interactable` layer for raycasting.
- **Naming Convention:** The GameObject name should match the "Station ID" (e.g., "A1", "B5").

### Scene Controllers
- **`HighlightingService`**: Must exist in the scene with a valid `GlowMaterial` assigned.
- **`SidebarController`**: Attached to the UI Canvas; requires references to the Progress Bar prefabs.
- **`PrinterSelector`**: Attached to the Main Camera to handle mouse clicks.

---

##  WebGL Build Settings

For optimal performance and compatibility:
- **Player Settings > WebGL > Publishing Settings:**
    - **Compression Format:** Brotli.
    - **Decompression Fallback:** Enabled (recommended for broader compatibility).
- **WebGL Template:** Use the `Convai PWA Template` located in `Assets/WebGLTemplates`. This template includes the necessary gRPC and SSE handling logic.
- **Graphics:** Ensure "Optimize Mesh Data" is enabled to keep the build size low for web users.
