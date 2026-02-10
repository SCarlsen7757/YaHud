namespace R3E.YaHud.Components.UI.Helpers
{
    public static class ColorUtils
    {
        /// <summary>
        /// Linearly interpolates between two RGB colors.
        /// </summary>
        /// <param name="color1">Start color.</param>
        /// <param name="color2">End color.</param>
        /// <param name="t">
        /// Interpolation factor in the range [0, 1].
        /// 0 returns color1, 1 returns color2, values in between blend the two.
        /// </param>
        /// <returns>A CSS-style "rgb(r, g, b)" string representing the interpolated color.</returns>
        public static string LerpRGB(RgbColor color1, RgbColor color2, double t)
        {
            // Clamp t to [0, 1]
            t = Math.Max(0, Math.Min(t, 1));

            int r = (int)(color1.R + (color2.R - color1.R) * t);
            int g = (int)(color1.G + (color2.G - color1.G) * t);
            int b = (int)(color1.B + (color2.B - color1.B) * t);

            return $"rgb({r}, {g}, {b})";
        }

        /// <summary>
        /// Linearly interpolates across three RGB colors using a configurable midpoint.
        /// </summary>
        /// <param name="color1">First color (start of the gradient).</param>
        /// <param name="color2">Second color (middle of the gradient).</param>
        /// <param name="color3">Third color (end of the gradient).</param>
        /// <param name="middle">
        /// The position (0–1) where the transition reaches color2.
        /// </param>
        /// <param name="t">
        /// Interpolation factor in the range [0, 1].
        /// </param>
        /// <returns>
        /// A CSS-style "rgb(r, g, b)" string representing the interpolated color.
        /// </returns>
        public static string LerpRGB3(
            RgbColor color1,
            RgbColor color2,
            RgbColor color3,
            double middle,
            double t
        )
        {
            // Guard against invalid middle values
            if (middle <= 0)
            {
                return LerpRGB(color2, color3, t);
            }

            if (middle >= 1)
            {
                return LerpRGB(color1, color2, t);
            }

            if (t < middle)
            {
                return LerpRGB(color1, color2, t / middle);
            }

            return LerpRGB(color2, color3, (t - middle) / (1 - middle));
        }

        public readonly record struct RgbColor(int R, int G, int B)
        {
            public int[] ToArray() => [R, G, B];
        }
    }
}
