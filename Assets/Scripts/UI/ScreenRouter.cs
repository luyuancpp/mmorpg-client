using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MmorpgClient.UI
{
    public interface IScreen
    {
        VisualElement Build(AppBootstrap app);
        void OnEnter();
        void OnExit();
        void Tick(float dt);
    }

    /// <summary>
    /// Single-instance screen stack: at most one screen is mounted in the
    /// UIDocument root. Each screen is constructed on demand the first time
    /// it is shown and reused afterwards.
    /// </summary>
    public sealed class ScreenRouter
    {
        private readonly AppBootstrap _app;
        private readonly VisualElement _host;
        private readonly Dictionary<Type, IScreen> _cache = new();

        public IScreen Current { get; private set; }
        public VisualElement Host => _host;

        public ScreenRouter(AppBootstrap app, VisualElement host)
        {
            _app = app;
            _host = host;
        }

        public T Show<T>() where T : IScreen, new()
        {
            if (Current is T existing) return existing;

            Current?.OnExit();
            _host.Clear();

            if (!_cache.TryGetValue(typeof(T), out var screen))
            {
                screen = new T();
                _cache[typeof(T)] = screen;
            }

            var root = screen.Build(_app);
            root.style.flexGrow = 1;
            _host.Add(root);
            Current = screen;
            screen.OnEnter();
            return (T)screen;
        }

        public void Tick(float dt)
        {
            Current?.Tick(dt);
        }
    }
}
