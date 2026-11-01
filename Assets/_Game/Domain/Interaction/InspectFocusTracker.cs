namespace ConGame.Domain.Interaction
{
    public struct InspectFocusTracker
    {
        public const float DwellSeconds = 0.25f;

        private object _focus;
        private float _elapsed;

        public bool HasDwelled { get; private set; }

        public bool Tick(object focus, float deltaSeconds)
        {
            if (focus == null)
            {
                _focus = null;
                _elapsed = 0f;
                HasDwelled = false;
                return false;
            }

            if (!ReferenceEquals(_focus, focus))
            {
                _focus = focus;
                _elapsed = 0f;
                HasDwelled = false;
            }

            _elapsed += deltaSeconds < 0f ? 0f : deltaSeconds;
            HasDwelled = _elapsed >= DwellSeconds;
            return HasDwelled;
        }
    }
}
