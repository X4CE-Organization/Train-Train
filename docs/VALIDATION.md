# Validation

Validation date: 2026-11-07  
Build version: 1.0.0

## Unity project checks

Command:

```bash
Unity -batchmode -quit -projectPath . \
  -executeMethod TrainTrain.Editor.TrainTrainProjectSetup.ValidateProject
```

Result:

```text
Train Train / 混乱火车 project validation passed:
8 regions, 48 unique maps, average 17.6 track edges, max 25,
8 single-backbone maps, © 2026 X4CE.
```

The validator checks:

- eight regions and `4 / 5 / 5 / 6 / 6 / 7 / 7 / 8` level counts;
- every level is connected and has a unique layout;
- each color has one source and one destination;
- generated levels stay within the connected-backbone plus at most two loops density budget;
- per-station capacity, spawn interval and initial passenger values are valid;
- map JSON save/load preserves tracks, stations and trains;
- editor drag preview is orthogonal and commits one route without leaving a chained anchor;
- new custom trains start moving by default;
- mixed-color cargo respects capacity and only unloads the matching color.
- every destination/source station's declared link exists in the final simplified rail graph.
- elevated edges terminate through ramps and never share an unramped node with ground track;
- an attempted ground connection to an elevated interior node is rejected atomically by the editor;
- map generation inserts bridge ramp, raised span and descending ramp as a complete sequence.
- the train tool accepts a click anywhere within one grid unit of a rail segment, chooses facing automatically and enforces the four-train editor limit.
- a new editor document starts with zero trains, and one placement adds exactly one train; empty drafts can be saved, while playtesting requires at least one train.
- placed editor trains are rendered as persistent world previews and rebuilt from JSON after undo, redo and import.

## Runtime smoke checks

The macOS universal build was launched with the built-in developer harness.

- `松岭`, `赤岩`, `霜原`, `霓虹港`, `沙海`, `云港`, `金穗谷`, and `雾都` all loaded into `Playing`.
- The maps used 1, 1, 2, 2, 3, 3, 4, and 4 trains respectively.
- The in-game status showed moving trains on every map, confirming immediate departure.
- A 12-second route regression on every first map reported zero route-break events after the station-link repair.
- No `NullReferenceException`, missing-source-station error or startup exception was found in the player logs.
- The level-select and editor screens rendered at 2780 × 974.
- The macOS bundle is a signed universal binary with `x86_64` and `arm64` slices.

Windows was compiled as a 64-bit GUI executable with all `TrainTrain_Data` dependencies. The local Parallels Windows 11 VM could not be started because its Parallels license has expired, so a native Windows startup test remains to be repeated on an active Windows host.

## Release artifacts

- `Builds/TrainTrain-Windows-x64.zip`
- `Builds/TrainTrain-macOS-universal.zip`
- `Builds/SHA256SUMS.txt`
