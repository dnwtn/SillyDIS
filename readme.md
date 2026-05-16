# 📡 SillyDis

> A developer-centric WPF application for real-time eavesdropping, inspection, and manipulation of **Distributed Interactive Simulation (DIS)** traffic over UDP multicast networks — built on the same architecture as [SillyRabbitMQ](../SillyRabbitMQ).

SillyDis gives simulation engineers a live window into IEEE-1278.1 DIS protocol traffic without deploying any middleware. Capture Entity State PDUs mid-exercise, decode binary payloads into readable JSON, filter by Exercise ID or PDU type, and spoof modified PDUs back onto the simulation network — all from one desktop app.

---

## ✨ Features

- **Multi-Tabbed Session Architecture** — Run multiple independent capture sessions simultaneously, each with its own filter configuration and PDU stream.
- **UDP Multicast Listener** — Join any multicast group (or bind to unicast/broadcast) on any local network interface with zero configuration overhead.
- **NIC Auto-Enumeration** — Local interfaces are enumerated at runtime; selecting one auto-fills the binding IP address — no manual entry required.
- **DIS PDU Decoding (IEEE-1278.1 v7)** — Powered by the [OpenDIS CSharpDis7](https://github.com/open-dis/open-dis-csharp) library. Automatically decodes Entity State, Fire, Detonation, Collision, Create/Remove Entity, Electromagnetic Emissions, Designator, Comment, Event Report, and more.
- **SISO-REF-010 Entity Resolution** — Embedded SISO enumerations XML resolves raw entity type tuples (kind·domain·country·category…) to human-readable platform names (e.g. `M1A2 Abrams`) and force affiliations (`Friendly` / `Opposing` / `Neutral`) in every grid row.
- **Layered Traffic Filtering:**
  - **Exercise ID** — Editable dropdown auto-populated from observed traffic; isolate a specific exercise on a shared network without typing.
  - **PDU Type** — Focus on a single PDU family (e.g., only `Entity State` or `Fire` PDUs).
  - **Entity ID** — Filter to a specific entity by `Site.Application.Entity` notation.
  - **Regex Filter** — Apply regular expressions against the decoded JSON payload body for deep content inspection.
- **High-Throughput Channel Pipeline** — A `System.Threading.Channels` producer-consumer pipeline decouples the UDP socket from PDU parsing. Packets are queued at up to 50,000 slots (DropOldest back-pressure) so no PDUs are dropped under burst load.
- **Pause / Resume with Buffering** — Pause the live display without losing data. In **Buffer** mode, incoming PDUs are held in memory (up to a configurable limit) and flushed atomically on resume. In **Drop** mode, incoming PDUs are discarded to save memory.
- **Hex Dump Inspector** — Every captured PDU exposes a structured hex view (16-byte rows, offset column, ASCII sidebar) alongside the JSON tab.
- **Intelligent PDU Inspection** — Decoded PDUs are formatted as syntax-highlighted, foldable JSON in an embedded AvalonEdit editor.
- **Spoof & Re-Broadcast** — Select any captured PDU, edit its decoded fields directly in the editor, and re-broadcast the modified PDU back onto the simulation network multicast group.
- **Capture Export** — Export the current session's PDU list to **NDJSON** (one record per line — grep-able, `jq`-compatible) or a pretty-printed **JSON array**. Exported files include ISO-8601 timestamps, all SISO-resolved fields, and Base64-encoded raw bytes for later replay.
- **Tactical 2D Map** — Live entity positions plotted on an OpenStreetMap tile layer via Mapsui. ECEF geocentric coordinates (from EntityStatePdu) are converted to WGS-84 geodetic using the Bowring iterative method. Entities are symbolized by force affiliation (blue / red / green / grey) and fade to 35% opacity when stale (no ESPDU received for >5 s).
- **Real-Time Telemetry** — Multi-series ScottPlot chart showing total PDU/second throughput plus a colour-coded signal per live PDU type, with a 60-second rolling window.
- **Exercise ID Auto-Discovery** — The Exercise ID filter bar is an editable ComboBox that populates automatically as unique exercise IDs are observed in live traffic.
- **Profile Management** — Save named network configurations (multicast address, port, local NIC IP) to disk. Profiles persist across sessions.
- **Terminal Tactical Theme** — Deep navy (`#0A0E1A`) background, `#00FF88` active-green accents, amber pause indicators, red drop counters, and Consolas monospaced fonts throughout ID and hex fields.

---

## 🗺️ Functional Mapping (SillyRabbitMQ → SillyDis)

| Concept | SillyRabbitMQ | SillyDis |
|:--------|:--------------|:---------|
| **Connection** | AMQP URI, credentials, vhost | Multicast IP, UDP port, local NIC |
| **Routing** | Exchange + routing key | Exercise ID + PDU type |
| **Payload** | UTF-8 JSON string | Binary PDU → decoded JSON (OpenDIS) |
| **Replay** | Edit JSON & re-publish | Edit PDU fields & **re-broadcast** to multicast |
| **DLQ Rescue** | Requeue dead-lettered messages | **Continuous PDU Replay Loop** (fire-and-forget) |
| **Telemetry** | Messages / second | **PDUs / second** |

---

## 🛠️ Tech Stack

| Layer | Technology |
|:------|:-----------|
| **Framework** | C# / .NET 10.0 |
| **UI** | WPF (Windows Presentation Foundation) |
| **Architecture** | MVVM — CommunityToolkit.Mvvm |
| **DIS Protocol** | [open-dis/open-dis-csharp](https://github.com/open-dis/open-dis-csharp) (CSharpDis7, bundled as source) |
| **UDP Transport** | `System.Net.Sockets.UdpClient` |
| **Payload Serialization** | Newtonsoft.Json |
| **Syntax Highlighting** | AvalonEdit |
| **Telemetry Graphs** | ScottPlot 5 |
| **UI Theme** | Material Design In XAML Themes (Teal / Amber) |
| **Dependency Injection** | Microsoft.Extensions.DependencyInjection |
| **Profile Persistence** | JSON via Newtonsoft.Json (`%AppData%\SillyDis\profiles.json`) |

---

## 🏗️ Project Structure

```
SillyDIS/
├── SillyDis.slnx
│
├── SillyDis.Core/                    # Platform-agnostic core
│   ├── Models/
│   │   ├── EntityTrack.cs            # Live entity position + force for tactical map
│   │   ├── NetworkProfile.cs         # Multicast connection config
│   │   ├── NicInfo.cs                # Network interface record
│   │   └── PduItem.cs                # Captured + decoded PDU (SISO-enriched, hex dump)
│   ├── Services/
│   │   ├── CaptureExportService.cs   # NDJSON / JSON export
│   │   ├── CoordinateConverter.cs    # ECEF → WGS-84 geodetic (Bowring) + Mercator
│   │   ├── DisParserService.cs       # OpenDIS decode bridge + SISO enrichment
│   │   ├── IUdpNetworkService.cs     # Service abstraction
│   │   ├── NetworkInterfaceService.cs# Runtime NIC enumeration
│   │   ├── ProfileManager.cs         # Profile load/save
│   │   ├── SisoEnumService.cs        # SISO-REF-010 XML parser + entity type resolver
│   │   └── UdpNetworkService.cs      # Channel<byte[]> producer-consumer pipeline
│   ├── Resources/
│   │   └── SISO-REF-010.xml          # Embedded enumeration database (6 MB)
│   └── OpenDIS/                      # CSharpDis7 source (174 PDU classes)
│       └── DataStreamUtilities/
│
└── SillyDis.UI/                      # WPF presentation layer
    ├── ViewModels/
    │   ├── MainViewModel.cs           # Root VM — profiles, sessions, connect, export
    │   └── SimulationSession.cs       # Per-tab VM — filters, PDU list, entity tracking,
    │                                  #   pause buffer, auto-discovery, telemetry histories
    ├── Helpers/
    │   ├── BraceFoldingStrategy.cs    # AvalonEdit JSON folding
    │   └── EntityMapLayer.cs          # Mapsui MemoryLayer synced to EntityTrack collection
    ├── Themes/
    │   └── TerminalTheme.xaml         # Deep-navy tactical color palette + component styles
    ├── App.xaml / App.xaml.cs         # DI container wiring + dark theme bootstrap
    └── MainWindow.xaml / .cs          # 3-pane UI: profiles · traffic+map · inspector
```

---

## 🚀 Getting Started

### Prerequisites

- [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (or SDK for development)
- Windows 10/11
- Visual Studio 2022 (v17.8+) **or** `dotnet CLI`
- A network with active DIS traffic — or use a DIS PDU generator/replayer (e.g., [COTS DIS tools](https://www.sisostds.org/) or Wireshark PCAP replay)

### Build & Run

```bash
git clone https://github.com/yourusername/SillyDIS.git
cd SillyDIS
dotnet build SillyDis.slnx
dotnet run --project SillyDis.UI
```

Or open `SillyDis.slnx` in Visual Studio and press **F5**.

### Quick Start

1. **Add a Network Profile** — Click **Add Profile** in the left pane. Enter your multicast group address (e.g., `239.1.2.3`), UDP port (e.g., `3000`), and optionally a specific local NIC IP.
2. **Connect** — Click the profile to start listening. The status dot turns **green** when the socket is bound and the multicast group is joined.
3. **Filter** — In the session tab, set your **Exercise ID** (0 = any), choose a **PDU Type** from the dropdown, and optionally add an **Entity ID** or **Regex** filter.
4. **Inspect** — Click any row in the PDU list to load the fully-decoded JSON into the right-pane editor.
5. **Spoof** — Modify fields in the editor and click **Spoof & Re-Broadcast** to inject the PDU back onto the network.

---

## 📡 Supported PDU Types (DIS v7)

| Type ID | PDU Name |
|:-------:|:---------|
| 1 | Entity State |
| 2 | Fire |
| 3 | Detonation |
| 4 | Collision |
| 11 | Create Entity |
| 12 | Remove Entity |
| 20 | Data |
| 21 | Set Data |
| 22 | Event Report |
| 23 | Comment |
| 24 | Electromagnetic Emissions |
| 25 | Designator |
| *other* | Raw hex dump |

---

## 🛡️ Safety Notes

- SillyDis **only reads** from the network by default. It does **not** transmit anything until you explicitly click **Spoof & Re-Broadcast**.
- UDP multicast is fire-and-forget. Re-broadcasting a PDU injects it into the simulation just like any other participant — use with caution on live exercises.
- The app binds with `SO_REUSEADDR` so it can coexist with other DIS participants on the same machine.

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome! Feel free to open an issue or submit a pull request.

---

## 📝 License

This project is licensed under the **MIT License**. The bundled OpenDIS source (`SillyDis.Core/OpenDIS/`) is licensed under the **BSD License** — see the original [open-dis/open-dis-csharp](https://github.com/open-dis/open-dis-csharp) repository for details.

---

## 🔭 Future Capabilities

| Feature | Notes |
|:--------|:------|
| **Offline / WMS Map Tiles** | Replace OpenStreetMap with a local tile server or WMS endpoint for OPSEC-sensitive environments. Mapsui supports custom tile sources. |
| **PDU Replay Loop** | Continuously re-broadcast a captured PDU at a configurable interval (equivalent to SillyRabbitMQ's DLQ rescue loop). |
| **PCAP Import** | Load Wireshark `.pcap` files containing DIS traffic for offline analysis without a live network. |
| **Entity History Trail** | Draw breadcrumb trails on the tactical map showing an entity's position history. |
| **Custom SISO Database** | Allow users to point at a local or updated `SISO-REF-010.xml` instead of the embedded version. |
