namespace R3E.YaHud.Components.UI.Helpers
{
    public static class ColorUtils
    {
        /// <summary>
        /// Linearly interpolates between two RGB colors.
        /// </summary>
        /// <param name="color1">Start color as [R, G, B].</param>
        /// <param name="color2">End color as [R, G, B].</param>
        /// <param name="t">
        /// Interpolation factor in the range [0, 1].
        /// 0 returns color1, 1 returns color2, values in between blend the two.
        /// </param>
        /// <returns>A CSS-style "rgb(r, g, b)" string representing the interpolated color.</returns>
        public static string LerpRGB(int[] color1, int[] color2, double t)
        {
            // Clamp t to [0, 1]
            t = Math.Max(0, Math.Min(t, 1));

            int r = (int)(color1[0] + (color2[0] - color1[0]) * t);
            int g = (int)(color1[1] + (color2[1] - color1[1]) * t);
            int b = (int)(color1[2] + (color2[2] - color1[2]) * t);

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
            int[] color1,
            int[] color2,
            int[] color3,
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
    }
}
