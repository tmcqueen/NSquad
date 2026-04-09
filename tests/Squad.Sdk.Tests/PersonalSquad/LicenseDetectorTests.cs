using Squad.Sdk.PersonalSquad;
using Shouldly;

namespace Squad.Sdk.Tests.PersonalSquad;

public class LicenseDetectorTests
{
    [Test]
    public void Detect_mit_is_permissive()
    {
        var result = LicenseDetector.Detect("MIT License\n\nCopyright...");
        result.Type.ShouldBe("permissive");
        result.SpdxId.ShouldBe("MIT");
    }

    [Test]
    public void Detect_apache2_is_permissive()
    {
        var result = LicenseDetector.Detect("Apache License, Version 2.0");
        result.Type.ShouldBe("permissive");
        result.SpdxId.ShouldBe("Apache-2.0");
    }

    [Test]
    public void Detect_gpl_is_copyleft()
    {
        var result = LicenseDetector.Detect("GNU GENERAL PUBLIC LICENSE\nVersion 3");
        result.Type.ShouldBe("copyleft");
        result.SpdxId.ShouldBe("GPL-3.0");
    }

    [Test]
    public void Detect_gpl2_is_copyleft()
    {
        var result = LicenseDetector.Detect("GNU GENERAL PUBLIC LICENSE\nVersion 2");
        result.Type.ShouldBe("copyleft");
    }

    [Test]
    public void Detect_unknown_on_empty()
    {
        var result = LicenseDetector.Detect("");
        result.Type.ShouldBe("unknown");
        result.SpdxId.ShouldBeNull();
    }

    [Test]
    public void Detect_bsd_is_permissive()
    {
        var result = LicenseDetector.Detect("BSD 3-Clause License");
        result.Type.ShouldBe("permissive");
    }
}
