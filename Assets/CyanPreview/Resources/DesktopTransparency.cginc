// Color-key windows cannot composite fractional alpha; ordered coverage keeps the key untouched.
float Bayer2(float2 p) { return p.x < 1 ? (p.y < 1 ? 0 : 3) : (p.y < 1 ? 2 : 1); }
fixed4 DesktopCoverage(fixed4 color, float2 screenPosition, float enabled)
{
    if (enabled > .5)
    {
        float2 pixel = fmod(floor(screenPosition), 4);
        float threshold = (4 * Bayer2(fmod(pixel, 2)) + Bayer2(floor(pixel / 2)) + .5) / 16;
        clip(color.a - threshold);
        color.a = 1;
    }
    return color;
}
