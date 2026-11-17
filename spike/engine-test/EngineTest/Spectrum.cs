using System.Diagnostics;

namespace EngineTest;

// Draws the mixer's FFT and counts how many frames per second actually carry new data.
sealed class Spectrum : IDrawable
{
    const int Bars = 48;

    readonly float[] _fft = new float[1024];
    readonly float[] _previous = new float[1024];
    long _windowStart = Stopwatch.GetTimestamp();
    int _frames;
    int _distinct;

    public void Draw(ICanvas canvas, RectF rect)
    {
        if (!Player.Instance.GetSpectrum(_fft))
            return;
        _frames++;
        if (!_fft.AsSpan().SequenceEqual(_previous))
            _distinct++;
        _fft.CopyTo(_previous, 0);
        if (Stopwatch.GetElapsedTime(_windowStart).TotalSeconds >= 1)
        {
            Player.SpectrumRate = $"{_frames} fps, {_distinct} with new data";
            _frames = 0;
            _distinct = 0;
            _windowStart = Stopwatch.GetTimestamp();
        }

        canvas.FillColor = Colors.SteelBlue;
        var width = rect.Width / Bars;
        for (var bar = 0; bar < Bars; bar++)
        {
            var from = (int)Math.Pow(2, bar * 9.0 / Bars) + 1;
            var to = Math.Max(from + 1, (int)Math.Pow(2, (bar + 1) * 9.0 / Bars) + 1);
            var peak = 0f;
            for (var bin = from; bin < to; bin++)
                peak = Math.Max(peak, _fft[bin]);
            var height = Math.Min(1, MathF.Sqrt(peak) * 3) * rect.Height;
            canvas.FillRectangle(bar * width, rect.Height - height, width - 1, height);
        }
    }
}
