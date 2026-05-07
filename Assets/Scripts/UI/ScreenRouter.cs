using System;
using System.Collections.Generic;
using FairyGUI;

namespace MmorpgClient.UI
{
    public interface IScreen
    {
        GComponent Build(AppBootstrap app);
        void OnEnter();
        void OnExit();
        void Tick(float dt);
    }

    /// <summary>
    /// Single-instance screen stack on top of FairyGUI's GRoot. At most one
    /// screen lives in the host container; each screen is constructed lazily
    /// and reused.
    /// </summary>
    public sealed class ScreenRouter
    {
        private readonly AppBootstrap _app;
        private readonly GComponent _host;
        private readonly Dictionary<Type, IScreen> _cache = new();

        public IScreen Current { get; private set; }
        public GComponent Host => _host;

        public ScreenRouter(AppBootstrap app, GComponent host)
        {
            _app = app;
            _host = host;
        }

        public T Show<T>() where T : IScreen, new()
        {
            if (Current is T existing) return existing;

            Current?.OnExit();
            // Remove (do not dispose) prior screen so it can be reused next Show<T>().
            for (int i = _host.numChildren - 1; i >= 0; i--)
                _host.RemoveChildAt(i, false);

            if (!_cache.TryGetValue(typeof(T), out var screen))
            {
                screen = new T();
                _cache[typeof(T)] = screen;
            }
            var root = screen.Build(_app);
            // Stretch to host
            root.SetSize(_host.width, _host.height);
            root.AddRelation(_host, RelationType.Size);
            _host.AddChild(root);

            Current = screen;
            screen.OnEnter();
            return (T)screen;
        }

        public void Tick(float dt) => Current?.Tick(dt);

        /// <summary>Called when GRoot is resized so the host stretches.</summary>
        public void OnRootResize()
        {
            _host.SetSize(GRoot.inst.width, GRoot.inst.height);
        }
    }
}
