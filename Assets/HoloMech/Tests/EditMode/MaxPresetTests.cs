using HoloMech;
using NUnit.Framework;

public class MaxPresetTests
{
    [Test]
    public void MaxPreset_FillsEveryListToItsLimit_AndSolves()
    {
        var i = new HoloMechInputs { lengthCm = 300f };
        i.ApplyMaxLoadPreset();

        Assert.AreEqual(HoloMechInputs.MaxSupports, i.supports.Count);
        Assert.AreEqual(HoloMechInputs.MaxPointLoads, i.pointLoads.Count);
        Assert.AreEqual(HoloMechInputs.MaxUniformLoads, i.uniformLoads.Count);
        Assert.That(i.Validate(), Is.Empty);
        Assert.That(i.TrySolve(out _, out var error), Is.True, error);
    }

    [Test]
    public void LabGantry_GivesTheHandCheckStressAtStaticCapacity()
    {
        var i = new HoloMechInputs();
        i.ApplyLabGantryDimensions();
        i.pointLoads[0].forceN = 8900f;

        var sec = i.SectionProperties();
        Assert.That(sec.I, Is.EqualTo(1.3872e-5).Within(1e-8), "I of the 160 x 85 x 14 x 6 mm section");
        Assert.That(i.TrySolve(out var s, out var error), Is.True, error);
        double sigma = System.Math.Abs(s.PeakMoment.Value) * sec.C / sec.I;
        Assert.That(sigma / 1e6, Is.EqualTo(27.38).Within(0.02), "P L_pin c / 4I with L_pin = 84 in (2026-11-13 tape), Table 2 C12");
    }
}
