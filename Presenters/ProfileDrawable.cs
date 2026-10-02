namespace Hiker.Presenters;

/// <summary>El perfil de desnivel: area rellena bajo la linea, con ejes y unas pocas marcas.</summary>
public sealed class ProfileDrawable : IDrawable
{
    public List<(double Km, double Alt)> Profile { get; set; } = [];
    public bool Dark { get; set; }
    public string NoData { get; set; } = string.Empty;

    public void Draw(ICanvas canvas, RectF rect)
    {
        var text = Dark ? Colors.White.WithAlpha(0.8f) : Colors.Black.WithAlpha(0.7f);
        var grid = Dark ? Colors.White.WithAlpha(0.15f) : Colors.Black.WithAlpha(0.12f);
        var accent = Color.FromArgb("#3525CD");
        canvas.FontSize = 11;
        canvas.FontColor = text;
        if (Profile.Count < 2)
        {
            canvas.DrawString(NoData, rect, HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }
        const float left = 44, bottom = 22, top = 8, right = 8;
        var plot = new RectF(rect.X + left, rect.Y + top, rect.Width - left - right, rect.Height - top - bottom);
        var minAlt = Profile.Min(p => p.Alt);
        var maxAlt = Profile.Max(p => p.Alt);
        var maxKm = Profile[^1].Km;
        if (maxAlt - minAlt < 10) { maxAlt += 5; minAlt -= 5; }
        // Margen arriba y abajo para que la linea no toque los bordes; redondeo a decenas en el eje.
        var lo = Math.Floor((minAlt - (maxAlt - minAlt) * 0.08) / 10) * 10;
        var hi = Math.Ceiling((maxAlt + (maxAlt - minAlt) * 0.08) / 10) * 10;
        float X(double km) => (float)(plot.X + (maxKm > 0 ? km / maxKm : 0) * plot.Width);
        float Y(double alt) => (float)(plot.Bottom - (alt - lo) / (hi - lo) * plot.Height);

        // Rejilla horizontal: 4 tramos
        canvas.StrokeColor = grid;
        canvas.StrokeSize = 1;
        for (var i = 0; i <= 4; i++)
        {
            var alt = lo + (hi - lo) * i / 4;
            var y = Y(alt);
            canvas.DrawLine(plot.X, y, plot.Right, y);
            canvas.DrawString($"{alt:0}", new RectF(rect.X, y - 7, left - 6, 14), HorizontalAlignment.Right, VerticalAlignment.Center);
        }
        // Eje X: marcas de distancia
        var ticks = maxKm >= 20 ? 5.0 : maxKm >= 8 ? 2.0 : maxKm >= 3 ? 1.0 : 0.5;
        for (var km = 0.0; km <= maxKm + 0.001; km += ticks)
        {
            var x = X(km);
            canvas.DrawLine(x, plot.Bottom, x, plot.Bottom + 4);
            canvas.DrawString(km % 1 == 0 ? $"{km:0}" : $"{km:0.#}", new RectF(x - 20, plot.Bottom + 6, 40, 14), HorizontalAlignment.Center, VerticalAlignment.Top);
        }

        // Area y linea
        var path = new PathF();
        path.MoveTo(X(Profile[0].Km), plot.Bottom);
        foreach (var (km, alt) in Profile)
            path.LineTo(X(km), Y(alt));
        path.LineTo(X(Profile[^1].Km), plot.Bottom);
        path.Close();
        canvas.FillColor = accent.WithAlpha(0.25f);
        canvas.FillPath(path);

        var line = new PathF();
        line.MoveTo(X(Profile[0].Km), Y(Profile[0].Alt));
        foreach (var (km, alt) in Profile.Skip(1))
            line.LineTo(X(km), Y(alt));
        canvas.StrokeColor = accent;
        canvas.StrokeSize = 2;
        canvas.DrawPath(line);
    }
}
