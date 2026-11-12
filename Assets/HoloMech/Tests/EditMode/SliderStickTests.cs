using HoloMech;
using NUnit.Framework;

public class SliderStickTests
{
    const float Frame = 1f / 72f;

    [Test]
    public void Flick_GivesOneStep()
    {
        var r = new StickRepeater();
        Assert.AreEqual(1, r.Tick(0.7f, Frame));
        for (int i = 0; i < 10; i++) Assert.AreEqual(0, r.Tick(0.7f, Frame)); // before the repeat delay
        Assert.AreEqual(0, r.Tick(0f, Frame));                                // released
        Assert.AreEqual(-1, r.Tick(-0.7f, Frame));                            // new flick to the left
    }

    [Test]
    public void SmallDeflection_DoesNothing()
    {
        var r = new StickRepeater();
        for (int i = 0; i < 200; i++) Assert.AreEqual(0, r.Tick(0.4f, Frame));
    }

    [Test]
    public void Hold_RepeatsAfterDelay_FasterAtFullDeflection()
    {
        int Count(float x, float seconds)
        {
            var r = new StickRepeater();
            int n = 0;
            for (float t = 0; t < seconds; t += Frame) n += r.Tick(x, Frame);
            return n;
        }

        int slow = Count(0.7f, 1.4f); // 1 + about (1.4 - 0.4) * 8
        int fast = Count(1.0f, 1.4f); // 1 + about (1.4 - 0.4) * 30
        Assert.That(slow, Is.InRange(8, 10));
        Assert.That(fast, Is.InRange(29, 32));
    }

    [Test]
    public void StepSize_UsesSliderStep_OrFractionOfRange()
    {
        Assert.AreEqual(1f, SliderStick.StepSize(1f, 0f, 241f), 1e-6f);
        Assert.AreEqual(0.5f, SliderStick.StepSize(0f, 0f, 100f), 1e-6f);
    }

    [Test]
    public void Nudge_StaysInRange()
    {
        Assert.AreEqual(121f, SliderStick.Nudge(120f, 1f, 0f, 241f, 1), 1e-6f);
        Assert.AreEqual(241f, SliderStick.Nudge(240.5f, 1f, 0f, 241f, 3), 1e-6f);
        Assert.AreEqual(0f, SliderStick.Nudge(0.2f, 1f, 0f, 241f, -1), 1e-6f);
    }
}
