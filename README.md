# MMORPG Unity Client

Unity 6000.0.32f1 client for the MMORPG server (parent repo:
[luyuancpp/mmorpg](https://github.com/luyuancpp/mmorpg)). This repo is
mounted as a git submodule at `client/unity/` inside the superproject.

## Quick start

1. **Open in Unity**: Unity Hub -> Open -> select this repo's root.
2. **Generate proto C# stubs** (required after first checkout and after
   any `.proto` change in the parent repo):

   ```pwsh
   pwsh -File tools/gen_proto.ps1
   pwsh -File tools/gen_messageids.ps1
   ```

   `gen_proto.ps1` calls `protoc` against the parent repo's `proto/`
   tree and writes to `Assets/Scripts/Proto/Generated/`.
   `gen_messageids.ps1` regenerates `Assets/Scripts/Net/MessageIds.cs`
   from the server's authoritative `proto/message_id.txt`.

3. **Bootstrap scene**: create an empty scene with one GameObject and
   add the `MmorpgClient.Core.Bootstrap` component, then add a child
   GameObject with `MmorpgClient.UI.GameDemo` for the developer panel.
   `Bootstrap` survives scene loads via `DontDestroyOnLoad` and owns
   the gate connection.

## Architecture

```
Bootstrap (DontDestroyOnLoad)
  +-- GameClient
       +-- GatewayHttpClient   (HTTP -> Java gateway: server-list, assign-gate)
       +-- GateTcpClient       (TCP  -> C++ Gate node, MuduoCodec framing)
       +-- ActorWorld          (entity_id -> GameObject view cache)
       +-- SkillFx             (cast ring / beam / hit flash primitives)
```

* **Wire format** is muduo's `ProtobufCodec`:
  `[len:i32 BE][nameLen:i32 BE][type_name\0][body][adler32:i32 BE]`,
  with adler-32 covering `[nameLen .. body]`.
* **RPC envelope**: C2S = `ClientRequest{ id, message_id, body }`,
  S2C reply/notify = `MessageContent{ id, message_id, serialized_message,
  error_message }`. Replies match by `id`; notifies have `id == 0` and
  dispatch by `message_id`.
* **Token verify** is the first protobuf frame after TCP connect:
  `ClientTokenVerifyRequest{ payload, signature }` from the gateway's
  `assign-gate` response. Server replies `ClientTokenVerifyResponse{ success }`.

## Production checklist

The repo currently ships the client foundation; before going live you
still need to address:

| Area                | Status                                              |
| ------------------- | --------------------------------------------------- |
| Login flow          | done (HTTP gateway + token verify + Login + Enter)  |
| Scene rendering     | placeholder primitives in `ActorWorld`              |
| Skill FX            | placeholder ring/beam/flash in `SkillFx`            |
| Reconnect           | exponential backoff in `GameDemo`                   |
| Refresh token       | wired (`MessageIds.RefreshToken=127`)               |
| Logging             | leveled file sink under `persistentDataPath/logs/`  |
| Settings            | PlayerPrefs (`ClientSettings`) for gateway/account  |
| **Movement**        | **NEEDS server proto** (no `MoveC2S` defined yet)   |
| **Real assets**     | Addressables / animations / audio not yet wired     |
| **Localization**    | tip table loader not yet wired                      |
| **Secure storage**  | refresh token must NOT live in `PlayerPrefs`        |
| **Anti-cheat**      | encrypt/sign critical RPCs at the application layer |
| **Build pipeline**  | CI workflow present; needs `UNITY_LICENSE` secret   |

## Layout

```
Assets/
  Plugins/Google.Protobuf.dll          vendored (netstandard2.0, 3.28.3)
  Scripts/
    Core/Bootstrap.cs                  DontDestroyOnLoad entry
    Core/MmorpgLogger.cs               leveled console + file logger
    Core/ClientSettings.cs             PlayerPrefs settings
    Game/GameClient.cs                 high-level client facade
    Net/                               gateway HTTP, gate TCP, codec, ids
    Proto/Generated/                   protoc output (regenerated)
    UI/GameDemo.cs                     developer IMGUI panel
    World/ActorWorld.cs                entity_id -> GameObject cache
    World/SkillFx.cs                   placeholder skill FX
tools/
  gen_proto.ps1                        protoc invoker
  gen_messageids.ps1                   message_id.txt -> MessageIds.cs
.github/workflows/ci.yml               Unity build matrix + script lint
```

## Releasing

1. Pull the latest server `proto/message_id.txt` and rerun
   `tools/gen_messageids.ps1` and `tools/gen_proto.ps1`.
2. Bump version in `ProjectSettings/ProjectSettings.asset`.
3. Push to `main`. The CI matrix builds Standalone, WebGL, and Android.
4. Bump the submodule pointer in the parent `mmorpg` repo so the server
   side knows which client commit it expects.
