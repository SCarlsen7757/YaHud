# Telemetry Recording and Replay

YaHud can write RaceRoom's raw telemetry to a file and play it back through the
full pipeline, so the whole HUD behaves exactly as it did live. Three things
that were previously impossible become routine:

- **Reproducing a situation offline.** A bug seen once during a race can be
  replayed as often as you like.
- **Developing widgets without launching RaceRoom.** Anything time-dependent —
  fuel projection, sector deltas, time gaps, radar, lap transitions — can only
  be exercised by data that changes over time. Test mode renders static values;
  a recording does not.
- **Making bug reports reproducible.** A user can attach a recording.

Replay runs **inside YaHud**. There is no separate replay application and no UDP
hop — the file-backed source is simply a third `ISharedSource`, so everything
above that interface is unchanged and unaware it is being replayed.

Recording is **opt-in**: `Enabled` is `false` by default, so nobody writes
multi-GB files by accident.

## Files

| File | Responsibility |
|------|----------------|
| [`FrameTruncation.cs`](../R3E/Core/Recording/FrameTruncation.cs) | Truncates a raw frame to the drivers that exist, and restores it. |
| [`TelemetryRecordingHeader.cs`](../R3E/Core/Recording/TelemetryRecordingHeader.cs) | The header record, the record/marker enums, and the version guard. |
| [`TelemetryRecordingWriter.cs`](../R3E/Core/Recording/TelemetryRecordingWriter.cs) | One file: block buffering, Brotli, index and markers, footer patch on close. |
| [`TelemetryRecordingReader.cs`](../R3E/Core/Recording/TelemetryRecordingReader.cs) | Header validation, index load-or-rebuild, seek, forward record streaming. |
| [`TelemetryRecorder.cs`](../R3E/Core/Recording/TelemetryRecorder.cs) | Capture: raw-frame tap, bounded channel, background writer, session splitting. |
| [`RecordingOptions.cs`](../R3E/Core/Recording/RecordingOptions.cs) | The `YaHud:Recording` settings object. |
| [`FileSharedSource.cs`](../R3E/Core/Replay/FileSharedSource.cs) | An `ISharedSource` backed by a recording instead of by the game. |
| [`SharedSourceSwitch.cs`](../R3E/Core/Replay/SharedSourceSwitch.cs) | Composite source that swaps live↔replay at runtime. |
| [`ReplayController.cs`](../R3E/Core/Replay/ReplayController.cs) | Transport controls and the target-seeking playback pump. |

## Recording

### Options

Every option is settable two ways — a launch argument and an `appsettings.json`
key, one-to-one. A launch argument wins where one was supplied; otherwise the
file keeps providing the value.

| Launch argument | Config key | Default | Meaning |
|---|---|---|---|
| `--record` | `YaHud:Recording:Enabled` | `false` | Makes recording available: registers the recorder and shows the controls. Recording stays inert until started. |
| `--record-autostart` | `YaHud:Recording:AutoStart` | `false` | Begin recording at launch without pressing the button. Implies `--record`. |
| `--recording-dir <dir>` | `YaHud:Recording:Directory` | `null` | Output folder. |
| `--recording-block-seconds <seconds>` | `YaHud:Recording:BlockSeconds` | `3` | Compression block duration, 1–60. |
| `--replay <file>` | `YaHud:Replay:File` | `null` | Start in replay mode on this recording. |

```json
"YaHud": {
  "Recording": {
    "Enabled": false,
    "AutoStart": false,
    "Directory": null,
    "BlockSeconds": 3
  },
  "Replay": { "File": null }
}
```

`Enabled` and `AutoStart` are deliberately separate. `Enabled` makes the feature
*available* but inert, which is what the UI toggle drives; `AutoStart` is for
scripted or headless capture where there is nobody to press the button.

Bad values fail at startup with the same friendly message from either source —
a `BlockSeconds` outside 1–60, a `Directory` that cannot be created, a
`--replay` path that does not exist. That is deliberate: by the time the first
frame arrives the user is driving, the HUD looks fine, and the only symptom
would be that no file ever appears.

When `Directory` is unset, recordings land in
`<LocalApplicationData>/YaHud/recordings`, which resolves on both Windows and
Linux.

### Run folders and session splitting

One recording *run* produces one file **per session**. The recorder watches
`SessionType` and `TrackId` straight out of the raw buffer — no marshalling —
and on a change finalises the current file and opens a new one on the next
frame. So a Practice → Qualify → Race progression becomes three files in one
run folder:

```
recordings/
  2026-07-25_19-04-spa/
    01-practice.yhtl
    02-qualify.yhtl
    03-race.yhtl
```

The folder is named from the time you pressed record plus a slug of the track
name; the file is numbered in run order and named after the `Constant.Session`
value (`session` when the value is not a known enum member). This split is
independent of the start/stop control — the toggle governs *whether* recording
happens, the split governs how it is filed.

The point of the split is that **file start is session start**, which is what
makes replay seeking simple (see [Seek semantics](#seek-semantics)).

### The write path

`RawFrameReceived` fires synchronously on the telemetry polling thread, so the
ingest path must never wait on disk — a disk hiccup, AV scan or full buffer
would otherwise stall telemetry and visibly freeze the HUD.

```
RawFrameReceived -> truncate into an ArrayPool buffer -> bounded channel (DropWrite)
                                                             |
                                        background writer task -> Brotli -> file
```

The channel holds 240 records, roughly four seconds of headroom at 60 Hz. When
it fills, frames are **dropped rather than queued**: for a debug recorder,
dropping beats stuttering the HUD, and every frame is independently decodable so
a gap only shows as a longer inter-frame delta on replay. `DroppedFrames`
surfaces the count; non-zero means the disk could not keep up.

The file is created on the **first frame**, not when recording starts. That is
what lets the header carry track, car and driver metadata — none of which exists
before a frame has arrived. A session that produces no frames simply produces no
file, which is the right outcome.

Recording is suppressed while replaying. The recorder is handed the
`ISharedSource` registration, which resolves to the live/replay switch, so
without that guard it would happily capture replayed frames back into a
near-duplicate file.

### How large is a recording?

The honest answer is **not yet measured against real telemetry**. What is
measured and solid:

- `Marshal.SizeOf<Shared>()` is **43,996 bytes**.
- `NumCars` followed by `DriverData[128]` are the last fields of `Shared`, so
  storing only the drivers that exist is a pure byte-range truncation. At a
  20-car grid that gives **43,996 → ~8,572 bytes, a reliable 5.1× reduction**
  before compression runs at all.

What is *not* pinned down is Brotli's further contribution, which depends
entirely on how byte-stable real frames are from one tick to the next.
Measurements against synthetic profiles could only bracket the result at
**16–148 MB per 10 minutes** at 20 cars. Treat any figure inside that range as
unverified until someone captures a real session and reports the number.

## Replay

### Entry points

`--replay <file>` (or `YaHud:Replay:File`) loads a recording at startup and
switches the HUD into replay mode before the first browser connects; live
telemetry is not read at all. Otherwise a recording is picked and driven from
the record/replay controls in the HUD.

Loading a recording puts the transport at position zero, paused.

### Transport

`ReplayController` exposes `Play`, `Pause`, `Seek`, `Step`, `JumpToLap`,
`Speed`, `Position`, `Duration` and catch-up progress, and raises `StateChanged`
so the UI can re-render.

- **Speed** is clamped to `[0.1, 8]`.
- **`JumpToLap`** resolves the position from the file's `LapStart` markers, so
  it costs no decoding — but the jump itself is an ordinary seek and pays
  whatever that seek costs.
- **Step** advances exactly one record while paused.
- Play/pause state is preserved across a seek, and targets are clamped to
  `[0, Duration]`.

Playback is paced on a dedicated thread with a `Stopwatch`, sleeping to
`target − 2 ms` and then spinning; `Task.Delay` resolution (~15 ms on Windows)
is far too coarse for 60 Hz.

The controller is a **target-seeking pump**, not a queue of seek commands. One
thread owns playback and continuously moves `Position` toward a `TargetPosition`
that the UI writes, so seeking is just an assignment and a new target
automatically supersedes an in-flight catch-up. Rapid scrubbing coalesces for
free, with no cancellation bookkeeping.

### Seek semantics

This is the part worth understanding before you use replay in anger.

Seeking corrupts every consumer that accumulates state across frames. Rather
than teach every feature service to rewind, replay guarantees the one property
that makes rewind awareness unnecessary:

> **YaHud only ever sees a monotonically forward frame stream.**

Seeking from T₀ to T₁ therefore works like this:

| Direction | Action | Cost |
|---|---|---|
| **Forward** (T₁ ≥ T₀) | Consume every frame T₀ → T₁. **No reset** — state at T₀ is already correct. | ∝ (T₁ − T₀) |
| **Backward** (T₁ < T₀) | Reset, rewind to **file start**, consume 0 → T₁. | ∝ T₁ |

There are no forward jumps at all, and backward movement appears downstream only
as a single reset at file start — delivered through the frame stream itself,
because the first frame after the rewind carries a lower `GameSimulationTicks`
than the last one emitted, which is exactly the condition `TelemetryService`
resets on. Because recording splits per session, file start *is* session start,
so the backward case needs no marker lookup: the reader is only ever
repositioned to zero.

Two consequences follow, and they are the trade this design makes deliberately:

- **State after a seek is exact, in both directions.** A warm-up window could
  never rebuild stint fuel totals or tire age since a pitstop several laps back;
  consuming every intervening frame does. Frames are never skipped to go faster —
  level-triggered detection tolerates gaps for most state, but skipping across a
  lap or sector boundary would lose a transition, and exactness is the point.
- **Backward seeks cost time proportional to distance from the start, not from
  where you were.** Nudging the slider back five seconds late in a session costs
  the same as rewinding to the beginning. Rewinding to early in a session is
  cheap regardless of how far in you had played. Forward seeks are cheap.

During a catch-up estimated to take longer than **2 seconds**, the controller
sets a suppression flag that widgets honour, so they skip rendering and draw
once at the destination instead of flooding the Blazor dispatcher. Shorter
scrubs keep rendering, which reads as a fast-forward. The estimate is a
self-correcting moving average of observed catch-up rate, and it escalates to
suppression if a catch-up outruns the threshold in practice. Tearing replay down
mid-catch-up clears the flag, so the HUD can never be left permanently refusing
to render.

## The `.yhtl` format

Extension `.yhtl`, little-endian throughout, written with `BinaryWriter`. Strings
are `BinaryWriter`-style length-prefixed UTF-8 — a 7-bit-encoded length followed
by the bytes — decoded out of the struct's fixed `byte[64]` fields at the null
terminator.

```
[Header]  "YHTL" | formatVer u16 | flags u16 | sizeofShared i32
          | allDriversOffset i32 | driverDataSize i32
          | r3eVersionMajor i32 | r3eVersionMinor i32
          | yaHudVersion str | startUtcTicks i64
          | indexOffset i64          <- patched on clean close, 0 if the app crashed
          |
          | -- metadata, captured from the first frame --
          | sessionType i32 | sessionPhaseAtStart i32
          | trackId i32 | layoutId i32 | trackName str | layoutName str
          | playerName str | playerCarModelId i32 | playerCarName str
          | playerClassId i32 | playerClassPerfIndex i32
          | numCarsAtStart i32

[Block]*  compressedLen i32 | firstFrameMs u32 | frameCount i32
          | Brotli{ ( elapsedMs u32 | recordType u8 | payloadLen i32 | payload )* }
                      recordType: 0 = Frame (truncated Shared bytes)
                                  1 = StartLights (i32)

[Footer]  entryCount i32 | ( firstFrameMs u32 | fileOffset i64 | frameCount i32 )*
          | markerCount i32 | ( elapsedMs u32 | markerType u8 | value i32 )*
          | -- final summary --
          | totalFrames i64 | durationMs u32 | maxNumCars i32
          | classCount i32 | ( classId i32 )*
          | "YHTX"
```

The current `formatVer` is `1`. `flags` is reserved and currently written as
zero.

### The header is not fixed-size

Five of its fields are length-prefixed strings (`yaHudVersion`, `trackName`,
`layoutName`, `playerName`, `playerCarName`), so the header's byte length varies
per file — and `indexOffset` cannot sit at a constant offset, which is why the
writer remembers where it wrote that slot in order to patch it on close.

The property that actually matters is a different one: **the header is complete
after the first frame.** Everything in it is knowable from a single frame, which
is what lets the recordings browser show track, layout, session type, car, class
and driver count for a whole folder without opening or decoding anything.

### Frame truncation

`NumCars` then `DriverData[128]` are the last fields of `Shared`, so the
variable-size part is the tail:

- **Truncate**: `length = offsetOf(NumCars) + 4 + numCars * sizeof(DriverData)`.
  No copy, no re-encoding. `numCars` is clamped to `[0, 128]` so a garbage frame
  cannot produce a wild length.
- **Restore**: a zero-filled `byte[sizeofShared]` plus a copy. Driver slots
  beyond `numCars` are zeros, which is exactly what the game would have had for
  them.

### Blocks

Records accumulate in memory and are flushed as a Brotli-compressed block once
they span `BlockSeconds`. Blocks are independently decodable, and each block
header carries its own compressed length.

`BlockSeconds` is the compression-ratio versus seek-granularity dial, not a
playback setting: smaller blocks seek more finely and grow the index but
compress worse, since each block restarts the Brotli dictionary; larger blocks
compress better but waste more decode per seek. In practice replay's backward
seek always restarts at offset zero anyway, so the index matters mostly for
loading, duration and the timeline.

### Markers

`markerType` is `PhaseChange` (value = the new `Constant.SessionPhase`) or
`LapStart` (value = the completed-lap count at that point). They make
"jump to lap N" cheap. There is no `SessionStart` marker — recording splits per
session, so file start *is* session start.

The recorder builds markers without marshalling anything, reading `SessionType`,
`SessionPhase`, `TrackId` and `CompletedLaps` straight out of the raw buffer at
offsets resolved once via `Marshal.OffsetOf`, the same trick `SharedMemoryService`
already uses for `GameSimulationTicks`.

### Crash recovery

If YaHud is killed mid-recording, `indexOffset` is still zero and the trailing
`YHTX` is absent. The reader then rebuilds the index by walking block headers —
a seek-only scan, not a decode — and decodes only the final block to establish a
sensible duration. Metadata still comes from the header, just as-of-first-frame
rather than the footer's final summary. A killed session is never left
unreadable.

The reader always prefers the footer summary (`maxNumCars`, the observed class
set) and falls back to header values when it is missing, because driver count
and the set of car classes can both grow mid-session.

### The version guard

`sizeofShared`, `allDriversOffset` and `driverDataSize` are checked against the
running build on open, and a mismatch **refuses loudly** with a message naming
both layouts.

This exists because `Shared` mirrors a layout owned by Sector3. When RaceRoom
adds a field, `Marshal.SizeOf<Shared>()` changes and every field after the
insertion point shifts. Without the guard, an old recording would not fail — it
would misparse into plausible-looking garbage, which is a far worse outcome than
a refusal. (The live UDP path has this weakness today: it only length-checks.)

The consequence is that recordings are tied to the exact `Shared` layout. A
future RaceRoom struct change invalidates old ones. The guard makes that loud;
it does not make them forward-compatible.

## Traps and limitations

**`PlayerCarName` is not a car model name.** There is no car-name string
anywhere in `Shared`. The field holds `VehicleInfo.Name`, which for the player
slot is effectively the *driver* name — usually identical to `playerName`. If
you need to identify the car, use **`PlayerCarModelId`**, which is reliable.

**`frameCount` in a block index entry is a *record* count**, covering frames and
start-lights alike, while the footer's `totalFrames` counts frame records only.
They agree on a Windows recording with no start lights and on every Linux
recording. On a **crashed** file, where the index was rebuilt by summing block
`frameCount`s, `TotalFrames` therefore slightly overcounts by the number of
start-light records in the file.

**Start lights replay only from recordings made on Windows.** In-process replay
makes the 400 Hz signal replayable in principle — `FileSharedSource` raises
`StartLightsChanged` from the `StartLights` record type. But only
`SharedMemoryService` raises the event in the first place;
`RemoteSharedMemoryService` declares it and never fires it, so recordings
captured on Linux (over the relay) contain **no start-light records at all**, and
the StartLight widget behaves on replay exactly as it does live there. Fixing
that means a typed envelope in the relay's UDP protocol — a separate concern.
Start-light samples arriving before the first frame are also discarded: there is
no file for them to land in yet.

**Replay bypasses `RemoteSharedMemoryService`**, so it does not exercise the UDP
receive path. Everything of interest lives above `ISharedSource`, but the
relay/receiver code is not covered by replay testing.

**No replaying into a second or remote HUD instance.** That is the price of
dropping the UDP hop. The file format stays readable by a future standalone tool
if that is ever wanted.

**Seeks cost time, not accuracy.** See [Seek semantics](#seek-semantics). A
backward seek replays from file start; the wall-clock cost of a long rewind has
not been measured against real telemetry and is the main open question in this
design.
