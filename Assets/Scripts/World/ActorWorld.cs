using System.Collections.Generic;
using UnityEngine;

namespace MmorpgClient.World
{
    /// <summary>
    /// Per-actor visual state. Cheap GameObjects (cubes for players,
    /// cylinders for NPCs) parented under the world container. The local
    /// player is highlighted in green.
    /// </summary>
    public sealed class ActorView
    {
        public ulong Entity;
        public ActorKind Kind;
        public ulong ConfigId;
        public GameObject Go;
        public TextMesh Label;

        // Interpolation state. We snap on first sample, then linearly
        // interpolate from current transform toward _target* over
        // InterpDuration seconds. Server send rate is sparse (start / stop /
        // direction-change), so we extrapolate using _velocity in between.
        public Vector3 TargetPos;
        public Vector3 TargetEuler;
        public Vector3 Velocity;     // m/s in Unity space
        public float   InterpStart;  // realtimeSinceStartup
        public float   InterpDuration;
        public Vector3 InterpFromPos;
        public Vector3 InterpFromEuler;
        public bool    HasTarget;
    }

    public enum ActorKind { Unknown = 0, Player = 1, Npc = 2 }

    /// <summary>
    /// Holds the live actor cache and renders one primitive per entity.
    /// Drives the visual layer reactively: <see cref="OnActorCreate"/>,
    /// <see cref="OnActorDestroy"/>, etc. are called from the dispatcher.
    /// </summary>
    public sealed class ActorWorld
    {
        private readonly Dictionary<ulong, ActorView> _actors = new();
        private readonly Transform _root;
        private ulong _localEntity;

        public ActorWorld(string rootName = "[ActorWorld]")
        {
            var go = GameObject.Find(rootName) ?? new GameObject(rootName);
            _root = go.transform;
        }

        public IReadOnlyDictionary<ulong, ActorView> Actors => _actors;
        public ulong LocalEntity => _localEntity;

        public void SetLocalPlayer(ulong entity)
        {
            _localEntity = entity;
            if (_actors.TryGetValue(entity, out var v)) Recolor(v);
        }

        public void SpawnActor(ulong entity, ActorKind kind, ulong configId,
                               Vector3 position, Vector3 eulerDeg)
        {
            if (_actors.ContainsKey(entity)) return; // dedupe
            var prim = kind == ActorKind.Player
                ? GameObject.CreatePrimitive(PrimitiveType.Cube)
                : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            prim.name = $"{kind}#{entity}";
            prim.transform.SetParent(_root, false);
            prim.transform.localPosition = position;
            prim.transform.localEulerAngles = eulerDeg;

            // Floating label
            var labelGo = new GameObject("label");
            labelGo.transform.SetParent(prim.transform, false);
            labelGo.transform.localPosition = new Vector3(0, 1.2f, 0);
            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = $"{kind}#{entity}";
            tm.characterSize = 0.08f;
            tm.fontSize = 32;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;

            var view = new ActorView
            {
                Entity = entity,
                Kind = kind,
                ConfigId = configId,
                Go = prim,
                Label = tm,
            };
            _actors[entity] = view;
            Recolor(view);
        }

        public void DespawnActor(ulong entity)
        {
            if (!_actors.TryGetValue(entity, out var view)) return;
            if (view.Go) Object.Destroy(view.Go);
            _actors.Remove(entity);
        }

        public void Clear()
        {
            foreach (var v in _actors.Values)
                if (v.Go) Object.Destroy(v.Go);
            _actors.Clear();
        }

        public bool TryGetActor(ulong entity, out ActorView view)
            => _actors.TryGetValue(entity, out view);

        /// <summary>
        /// Apply a server <c>ActorMoveS2C</c>: snap target, start a short
        /// interpolation from the current transform so movement looks smooth
        /// even if the server's broadcast rate is sparse.
        /// </summary>
        public void ApplyMove(ulong entity, Vector3 targetPos, Vector3 targetEuler,
                              Vector3 velocity, float interpDuration = 0.15f)
        {
            if (!_actors.TryGetValue(entity, out var v) || v.Go == null) return;
            v.InterpFromPos   = v.Go.transform.localPosition;
            v.InterpFromEuler = v.Go.transform.localEulerAngles;
            v.TargetPos       = targetPos;
            v.TargetEuler     = targetEuler;
            v.Velocity        = velocity;
            v.InterpStart     = Time.realtimeSinceStartup;
            v.InterpDuration  = Mathf.Max(0.001f, interpDuration);
            v.HasTarget       = true;
        }

        /// <summary>
        /// Server forced snap (teleport / anti-cheat correction). Skips
        /// interpolation entirely.
        /// </summary>
        public void Teleport(ulong entity, Vector3 pos, Vector3 euler)
        {
            if (!_actors.TryGetValue(entity, out var v) || v.Go == null) return;
            v.Go.transform.localPosition    = pos;
            v.Go.transform.localEulerAngles = euler;
            v.TargetPos = pos;
            v.TargetEuler = euler;
            v.Velocity = Vector3.zero;
            v.HasTarget = false;
        }

        /// <summary>
        /// Drive interpolation + extrapolation. Call from a MonoBehaviour
        /// Update once per frame. Frame-rate independent.
        /// </summary>
        public void Tick()
        {
            float now = Time.realtimeSinceStartup;
            float dt  = Time.deltaTime;
            foreach (var v in _actors.Values)
            {
                if (!v.HasTarget || v.Go == null) continue;
                float t = (now - v.InterpStart) / v.InterpDuration;
                if (t < 1f)
                {
                    v.Go.transform.localPosition    = Vector3.Lerp(v.InterpFromPos, v.TargetPos, t);
                    v.Go.transform.localEulerAngles = LerpEuler(v.InterpFromEuler, v.TargetEuler, t);
                }
                else
                {
                    // Past the interp window: dead-reckon with last velocity.
                    v.TargetPos += v.Velocity * dt;
                    v.Go.transform.localPosition    = v.TargetPos;
                    v.Go.transform.localEulerAngles = v.TargetEuler;
                }
            }
        }

        private static Vector3 LerpEuler(Vector3 a, Vector3 b, float t)
            => new(Mathf.LerpAngle(a.x, b.x, t),
                   Mathf.LerpAngle(a.y, b.y, t),
                   Mathf.LerpAngle(a.z, b.z, t));

        private void Recolor(ActorView v)
        {
            if (v.Go == null) return;
            var rend = v.Go.GetComponent<Renderer>();
            if (rend == null) return;
            Color c;
            if (v.Entity == _localEntity)            c = new Color(0.2f, 0.9f, 0.3f);
            else if (v.Kind == ActorKind.Player)     c = new Color(0.3f, 0.5f, 0.95f);
            else                                     c = new Color(0.85f, 0.55f, 0.2f);
            // Use a runtime material so we don't share + mutate the shared one.
            rend.material = new Material(rend.sharedMaterial) { color = c };
        }
    }
}
