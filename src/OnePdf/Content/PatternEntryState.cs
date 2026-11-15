namespace OnePdf.Content;

/// <summary>
/// Immutable graphics state inherited by a tiling cell (ISO 32000-1 8.7.3.1 and 11.6.7).
/// Equality covers every inherited parameter, so equivalent form invocations can share the interpreted cell.
/// </summary>
internal sealed class PatternEntryState : IEquatable<PatternEntryState>
{
    private readonly GState _state;
    private Key? _key;

    public PatternEntryState(GState state)
    {
        _state = state.Clone();
        // The outer pattern matrix carries the coordinate space. The implicit group applies the caller's
        // alpha, blend and mask at paint time; those parameters must not be inherited into its cell.
        _state.Ctm = PdfMatrix.Identity;
        _state.FillAlpha = 1;
        _state.StrokeAlpha = 1;
        _state.Blend = "Normal";
        _state.SoftMask = 0;
    }

    public GState CreateState() => _state.Clone();

    public bool Equals(PatternEntryState? other) => ReferenceEquals(this, other)
        || (other is not null && GetKey().Equals(other.GetKey()));

    public override bool Equals(object? obj) => obj is PatternEntryState other && Equals(other);

    public override int GetHashCode() => GetKey().GetHashCode();

    private Key GetKey()
    {
        // Most forms never select a pattern. Capture their entry values, but do not construct or hash
        // the complete cache key unless it is actually needed. The published key is immutable.
        Key? key = Volatile.Read(ref _key);
        if (key is not null)
        {
            return key;
        }

        var made = new Key(_state);
        return Interlocked.CompareExchange(ref _key, made, null) ?? made;
    }

    private sealed class Key : IEquatable<Key>
    {
        private readonly PdfGraphicsState _graphics;
        private readonly PdfColorSpace _fillCs;
        private readonly PdfColorSpace _strokeCs;
        private readonly ReadOnlyMemory<double> _dash;
        private readonly (double CharSpace, double WordSpace, double HorizScale, double Leading, int RenderMode, double Rise, double FontSize, FontInfo? Font) _text;
        private readonly int _hash;

        public Key(GState state)
        {
            _graphics = state.Snapshot() with { Dash = default };
            _fillCs = state.FillCs;
            _strokeCs = state.StrokeCs;
            _dash = state.Dash;
            _text = (state.CharSpace, state.WordSpace, state.HorizScale, state.Leading, state.RenderMode, state.Rise, state.FontSize, state.Font);
            var hash = new HashCode();
            hash.Add(_graphics);
            hash.Add(_text);
            hash.Add(_fillCs);
            hash.Add(_strokeCs);
            foreach (double dash in _dash.Span)
            {
                hash.Add(dash);
            }

            _hash = hash.ToHashCode();
        }

        public bool Equals(Key? other) => ReferenceEquals(this, other)
            || (other is not null && _hash == other._hash && _graphics == other._graphics && _text == other._text
                && _fillCs == other._fillCs && _strokeCs == other._strokeCs && _dash.Span.SequenceEqual(other._dash.Span));

        public override bool Equals(object? obj) => obj is Key other && Equals(other);

        public override int GetHashCode() => _hash;
    }
}
