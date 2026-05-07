# mmorpg-client (Unity 3D)

Unity client for the [mmorpg](https://github.com/luyuancpp/mmorpg) game server.
Built to be consumed as a git submodule mounted at `client/unity/` of the
super-project so the client and the server share one source of truth for the
`.proto` contract.

## Scope of this initial drop

The demo covers the **login → enter scene → release skill** vertical:

1. `GET  /api/server-list` and `POST /api/assign-gate` against the Java
   gateway to obtain the gate address + a signed connection token.
2. Open a TCP connection to the assigned **Gate** node and frame messages
   with the muduo `ProtobufCodec` wire format
   `[len:i32 BE][nameLen:i32 BE][typeName\0][body][adler32:i32 BE]`.
3. Send `ClientTokenVerifyRequest` as the very first protobuf frame and
   wait for `ClientTokenVerifyResponse.success = true`.
4. Login flow over the Gate channel:
   `Login` → (`CreatePlayer` if empty) → `EnterGame`.
5. Receive server-pushed scene notifications
   (`NotifyEnterScene`, `NotifySceneInfo`, `NotifyActorListCreate`, ...).
6. Cast a skill with `ReleaseSkill` and react to
   `NotifySkillUsed` / `NotifySkillInterrupted`.

Everything above is wired up in [`Assets/Scripts/UI/GameDemo.cs`](Assets/Scripts/UI/GameDemo.cs)
as a plain IMGUI panel. There is no scene art yet — that's the next layer.

## Project layout

```
Assets/
  Plugins/                     <- drop Google.Protobuf.dll here
  Scripts/
    Net/
      Adler32.cs               muduo-compatible adler-32
      MuduoCodec.cs            wire format (encode/decode by full type name)
      GateTcpClient.cs         async TCP client (reader+writer threads, main-thread Poll)
      GatewayHttpClient.cs     UnityWebRequest wrapper for /api/server-list, /api/assign-gate
      MessageIds.cs            curated subset of message_id.txt
    Game/
      GameClient.cs            high-level facade (login -> enter game -> cast skill)
    Proto/
      Generated/               <- protoc output goes here (gitignored placeholder)
    UI/
      GameDemo.cs              IMGUI demo panel
    MmorpgClient.asmdef
tools/
  gen_proto.ps1                regenerate Generated/*.cs from the parent repo's proto/
```

## Setup

1. **Open in Unity 6000.0 LTS** (or newer).
2. **Install Google.Protobuf**.
   Either:
   - Use [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity)
     and install the `Google.Protobuf` package, **or**
   - Manually download `Google.Protobuf.dll` (any 3.21+ build) and drop it
     into `Assets/Plugins/`.
3. **Generate proto C# stubs**:
   ```pwsh
   pwsh -File tools/gen_proto.ps1
   ```
   The script assumes this repo is checked out as a submodule under
   `client/unity/` of the parent `mmorpg` repo. Pass `-ProtoRoot <path>`
   if your layout differs. Requires `protoc` on PATH (the parent repo's
   `dev.bat proto` step also installs one).
4. Open any scene, add an empty GameObject, attach `GameDemo`, hit Play.
   Make sure the parent repo's services are running locally
   (`pwsh tools/scripts/dev_tools.ps1 -Command dev-start`).

## Wire-protocol notes

- Frame sizes on the gate channel are capped at **1 KB per client request**
  by `client_message_processor.cpp` (`kMaxClientMessageSize`). Stay under
  this when implementing new RPCs from the client side.
- Inbound payloads come wrapped in `MessageContent { message_id, id,
  serialized_message, error_message }`. RPC replies match request `id`;
  server-pushed notifications carry `id = 0` and are dispatched purely by
  `message_id`.
- The token from `/api/assign-gate` carries an HMAC-SHA256 signature plus a
  `GateTokenPayload` blob. The client only forwards the bytes verbatim;
  the secret is held by the server.
- Dev mode: if the gate has no `gate_token_secret` configured, all
  sessions are auto-verified -- sending an empty `ClientTokenVerifyRequest`
  is enough to flip the connection into the "verified" state.

## Roadmap

- Wire `EnterScene` C2S (needs `SceneInfoComp` construction from a target zone/scene).
- Drive an actor cache from `NotifyActorListCreate`/`NotifyActorDestroy`
  and visualize it.
- Render skill-cast feedback (cooldown bar, target ring, FX hooks).
- Hook up `RefreshToken` ticker (mirror `robot/login.go runTokenRefresher`).
- Optional: replace the IMGUI panel with a UGUI/UIToolkit login screen.
