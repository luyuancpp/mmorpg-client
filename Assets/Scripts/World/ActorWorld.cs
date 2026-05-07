using System.Collections.Generic;
using UnityEngine;

namespace MmorpgClient.World
{
    /// <summary>
    /// Per-actor visual state. Cheap GameObjects (cubes for players,
    /// cylinders for NPCs) parented under the world container. The local
    /// player is highlighted in green.
    /// </summary>
    internal sealed class ActorView
    {
        public ulong Entity;
        public ActorKind Kind;
        public ulong ConfigId;
        public GameObject Go;
        public TextMesh Label;
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
