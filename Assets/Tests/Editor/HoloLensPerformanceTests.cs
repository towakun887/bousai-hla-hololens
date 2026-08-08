using NUnit.Framework;

public class HoloLensPerformanceTests
{
    [TestCase(320f, MarkerVisual.LodState.Hidden)]
    [TestCase(295f, MarkerVisual.LodState.Hidden)]
    [TestCase(289f, MarkerVisual.LodState.Text)]
    public void HiddenLod_UsesVisibleEntryHysteresis(float distance, MarkerVisual.LodState expected)
    {
        Assert.AreEqual(expected, MarkerManager.DetermineLod(MarkerVisual.LodState.Hidden, distance * distance));
    }

    [TestCase(110f, MarkerVisual.LodState.Text)]
    [TestCase(101f, MarkerVisual.LodState.Text)]
    [TestCase(94f, MarkerVisual.LodState.Full)]
    [TestCase(311f, MarkerVisual.LodState.Hidden)]
    public void TextLod_UsesFullAndHiddenHysteresis(float distance, MarkerVisual.LodState expected)
    {
        Assert.AreEqual(expected, MarkerManager.DetermineLod(MarkerVisual.LodState.Text, distance * distance));
    }

    [TestCase(102f, MarkerVisual.LodState.Full)]
    [TestCase(106f, MarkerVisual.LodState.Text)]
    [TestCase(311f, MarkerVisual.LodState.Hidden)]
    public void FullLod_UsesExitHysteresis(float distance, MarkerVisual.LodState expected)
    {
        Assert.AreEqual(expected, MarkerManager.DetermineLod(MarkerVisual.LodState.Full, distance * distance));
    }
}
