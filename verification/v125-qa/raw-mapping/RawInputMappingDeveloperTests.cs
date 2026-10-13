using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AeroSim.Input;

// Author checks: synthetic immutable metadata only. No Unity/device/user-profile IO.
internal static class RawInputMappingDeveloperTests
{
    private const string Layout = "SyntheticStick";
    private const string AxisType = "UnityEngine.InputSystem.Controls.AxisControl";
    private const uint Format = 0x54455354u; // Synthetic FourCC bits, not an SDK read.
    private const int Seed = 1251012, RandomCases = 1200;
    private const string KeyboardHash = "54de1323081596043fac0089d2d021ed51d91512c64f5e4d6360a49d4b0b5051";
    private const string TouchHash = "6587ee3d86628693f0d9d924451b86f5230c71828068f589341200f989ca69b6";
    private const string HardwareHash = "621a4bb6c1a96203a841e4db2a9bfdafa9da7988c3c15364295c815b888e9f0a";
    private const string UnicodeHash = "467475a3add4fc269a5a5493d4f67f8d7f79e40f0eeb1ebb83f251ea90840598";
    private static readonly string[] FloatFields = {
        "clampMin", "clampMax", "clampConstant", "normalizeMin", "normalizeMax", "normalizeZero", "scaleFactor"
    };
    private static int _checks, _failures;
    private static AxisMapping Mapping => new AxisMapping("lever/z", "stick/twist", "stick/y", "stick/x");
    private static RawAxisPreprocess Plain => new RawAxisPreprocess(RawAxisClamp.None,
        -1, 1, 0, false, -1, 1, 0, false, false, 1);

    private static void Check(string name, bool condition)
    { _checks++; if (!condition) { _failures++; Console.WriteLine("FAIL " + name); } }
    private static void Equal(string name, string actual, string expected)
    {
        Check(name, actual == expected);
        if (actual != expected) Console.WriteLine("  expected=" + expected + " actual=" + actual);
    }
    private static RawControlDescriptor Control(string path, RawAxisPreprocess p = null,
        RawControlValueKind kind = RawControlValueKind.AxisFloat, string type = AxisType,
        uint format = Format, uint size = 32) =>
        new RawControlDescriptor(path, kind, type, format, size, p ?? Plain);
    private static string[] Paths(AxisMapping mapping) =>
        new[] { mapping.ThrottlePath, mapping.YawPath, mapping.PitchPath, mapping.RollPath };
    private static RawControlDescriptor[] Catalog(AxisMapping mapping = null, RawAxisPreprocess p = null)
    {
        var paths = Paths(mapping ?? Mapping); var controls = new RawControlDescriptor[4];
        for (int axis = 0; axis < 4; axis++) controls[axis] = Control(paths[axis], p);
        return controls;
    }
    private static RawMappingValidationResult Validate(IReadOnlyList<RawControlDescriptor> catalog,
        AxisMapping mapping = null, string layout = Layout) =>
        RawInputMapping.ValidateHardware(FlightInputSourceKind.Joystick, layout, catalog, mapping ?? Mapping);
    private static AxisMapping ReplacePath(AxisMapping mapping, int axis, string path)
    {
        var paths = Paths(mapping); paths[axis] = path;
        return new AxisMapping(paths[0], paths[1], paths[2], paths[3]);
    }
    private static RawControlDescriptor With(RawControlDescriptor c, RawAxisPreprocess p = null,
        string type = null, uint? format = null, uint? size = null, RawControlValueKind? kind = null) =>
        new RawControlDescriptor(c.Path, kind ?? c.ValueKind, type ?? c.ControlTypeFullName,
            format ?? c.StateFormatCode, size ?? c.SizeInBits, p ?? c.Preprocess);
    private static RawAxisPreprocess FloatField(RawAxisPreprocess p, int field, float value)
    {
        float[] floats = { p.ClampMin, p.ClampMax, p.ClampConstant, p.NormalizeMin, p.NormalizeMax, p.NormalizeZero, p.ScaleFactor };
        floats[field] = value;
        return new RawAxisPreprocess(p.Clamp, floats[0], floats[1], floats[2], p.Normalize,
            floats[3], floats[4], floats[5], p.Invert, p.Scale, floats[6]);
    }
    private static bool Has(RawMappingValidationResult result, RawMappingError error,
        int axis = -2, string field = null)
    {
        foreach (var diagnostic in result.Diagnostics)
            if (diagnostic.Error == error && (axis == -2 || diagnostic.AxisIndex == axis) &&
                (field == null || diagnostic.Field == field)) return true;
        return false;
    }
    private static string DiagnosticText(RawMappingValidationResult result)
    {
        var text = new StringBuilder();
        foreach (var d in result.Diagnostics)
            text.Append(d.AxisIndex).Append('|').Append(d.Path).Append('|').Append(d.Field).Append('|').Append(d.Error).Append(';');
        return text.ToString();
    }
    private static string ResultText(RawMappingValidationResult result) =>
        result.Status + "/" + result.IsValid + "/" + result.Kind + "/" + result.RawConvention + "/" +
        result.MappingFingerprint + "/" + DiagnosticText(result);
    private static void Rejected(string name, RawMappingValidationResult result, RawMappingError expected,
        RawMappingValidationStatus status = RawMappingValidationStatus.Rejected)
    {
        Check(name + " status", result.Status == status && !result.IsValid);
        Check(name + " empty activation payload", result.Mapping == null && result.Controls.Count == 0 &&
            result.MappingFingerprint == "" && result.RawConvention == "");
        Check(name + " diagnostic present", result.Diagnostics.Count > 0 && Has(result, expected));
        foreach (var d in result.Diagnostics)
        {
            Check(name + " defined diagnostic", Enum.IsDefined(typeof(RawMappingError), d.Error) && d.Error != RawMappingError.None);
            Check(name + " bounded axis", d.AxisIndex >= -1 && d.AxisIndex <= 3);
            Check(name + " field fixed", ExpectedField(d));
            Check(name + " no null text", d.Path != null && d.Field != null);
        }
    }
    private static bool ExpectedField(RawMappingDiagnostic d)
    {
        switch (d.Error)
        {
            case RawMappingError.UnsupportedBuiltInKind:
            case RawMappingError.UnsupportedHardwareKind: return d.Field == "kind";
            case RawMappingError.MappingNotEditable:
            case RawMappingError.MappingMissing: return d.Field == "mapping";
            case RawMappingError.InvalidLayout: return d.Field == "layout";
            case RawMappingError.MissingCatalog:
            case RawMappingError.MissingControlDescriptor:
            case RawMappingError.DuplicateCatalogPath: return d.Field == "catalog";
            case RawMappingError.InvalidCatalogPath:
            case RawMappingError.InvalidMappingPath:
            case RawMappingError.DuplicateMappedPath:
            case RawMappingError.AxisNotFound: return d.Field == "path";
            case RawMappingError.UnsupportedControlValueKind:
            case RawMappingError.ButtonAxisNotAllowed:
            case RawMappingError.AxisTypeMismatch: return d.Field == "valueKind";
            case RawMappingError.InvalidControlType: return d.Field == "controlTypeFullName";
            case RawMappingError.InvalidStateFormat: return d.Field == "stateFormatCode";
            case RawMappingError.InvalidStateSize: return d.Field == "sizeInBits";
            case RawMappingError.MissingPreprocess: return d.Field == "preprocess";
            case RawMappingError.UnsupportedClamp: return d.Field == "clamp";
            case RawMappingError.NonFinitePreprocess: return Array.IndexOf(FloatFields, d.Field) >= 0;
            case RawMappingError.InvalidClampRange: return d.Field == "clampRange";
            case RawMappingError.InvalidNormalizeRange: return d.Field == "normalizeRange";
            default: return false;
        }
    }
    private static void Single(string name, RawMappingValidationResult result, RawMappingError error,
        int axis = -1, string path = "", RawMappingValidationStatus status = RawMappingValidationStatus.Rejected)
    {
        Rejected(name, result, error, status);
        Check(name + " single diagnostic", result.Diagnostics.Count == 1);
        if (result.Diagnostics.Count == 1)
        { Check(name + " exact axis/path", result.Diagnostics[0].AxisIndex == axis && result.Diagnostics[0].Path == path); }
    }
    private static void Valid(string name, RawMappingValidationResult result, bool hardware = true,
        RawMappingValidationStatus status = RawMappingValidationStatus.JoystickCandidate)
    {
        Check(name + " valid status", result.IsValid && result.Status == status);
        Check(name + " empty diagnostics", result.Diagnostics.Count == 0);
        Check(name + " activation data shape", hardware
            ? result.Mapping != null && result.Controls.Count == 4 && result.RawConvention == "unity-unprocessed-control-v1"
            : result.Mapping == null && result.Controls.Count == 0 && result.RawConvention.Length != 0);
        bool hash = result.MappingFingerprint.Length == 64;
        foreach (char c in result.MappingFingerprint) hash &= (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
        Check(name + " lowercase SHA256", hash);
    }

    private static void StatusAndStages()
    {
        foreach (var kind in new[] { FlightInputSourceKind.None, FlightInputSourceKind.Keyboard,
            FlightInputSourceKind.Joystick, FlightInputSourceKind.Gamepad, FlightInputSourceKind.Touch,
            (FlightInputSourceKind)(-1), (FlightInputSourceKind)99, (FlightInputSourceKind)int.MaxValue })
        {
            var built = RawInputMapping.GetBuiltIn(kind);
            if (kind == FlightInputSourceKind.Keyboard || kind == FlightInputSourceKind.Touch)
            {
                Valid("logical builtin", built, false, RawMappingValidationStatus.BuiltInValid);
                Check("builtin retains kind", built.Kind == kind);
            }
            else Single("unsupported builtin", built, RawMappingError.UnsupportedBuiltInKind);
            var hardware = RawInputMapping.ValidateHardware(kind, null, null, Mapping);
            if (kind == FlightInputSourceKind.Gamepad) Single("pad edit gate first", hardware, RawMappingError.MappingNotEditable);
            else if (kind == FlightInputSourceKind.Joystick) Single("layout gate before catalog", hardware, RawMappingError.InvalidLayout);
            else Single("unsupported hardware gate first", hardware, RawMappingError.UnsupportedHardwareKind);
            Check("failure preserves requested kind only", hardware.Kind == kind);
        }
        Single("joystick missing before all other stages",
            RawInputMapping.ValidateHardware(FlightInputSourceKind.Joystick, null, null),
            RawMappingError.MappingMissing, status: RawMappingValidationStatus.Missing);
        Single("null catalog", Validate(null), RawMappingError.MissingCatalog);
        foreach (string layout in new[] { null, "", " x", "x ", "\u00a0x", "x\u202f", "x\ny", "x\0y", "\ud800", "x\udfff" })
            Single("bad layout", Validate(Catalog(), layout: layout), RawMappingError.InvalidLayout);
        Valid("interior layout whitespace kept", Validate(Catalog(), layout: "Synthetic Stick"));
        var candidate = Validate(Catalog());
        Valid("joystick candidate", candidate);
        Check("mapping four axes order", candidate.Mapping.ThrottlePath == "lever/z" &&
            candidate.Mapping.YawPath == "stick/twist" && candidate.Mapping.PitchPath == "stick/y" &&
            candidate.Mapping.RollPath == "stick/x");
        for (int axis = 0; axis < 4; axis++) Check("control order", candidate.Controls[axis].Path == Paths(Mapping)[axis]);
        var padMapping = new AxisMapping("leftStick/y", "leftStick/x", "rightStick/y", "rightStick/x");
        var pad = RawInputMapping.ValidateHardware(FlightInputSourceKind.Gamepad, Layout, Catalog(padMapping));
        Valid("pad fixed mapping", pad, true, RawMappingValidationStatus.BuiltInValid);
        Equal("kind not in hardware hash", pad.MappingFingerprint, Validate(Catalog(padMapping), padMapping).MappingFingerprint);
        Single("pad supplied fixed mapping still not editable",
            RawInputMapping.ValidateHardware(FlightInputSourceKind.Gamepad, Layout, Catalog(padMapping), padMapping),
            RawMappingError.MappingNotEditable);
        for (int axis = 0; axis < 4; axis++)
        {
            var missing = new List<RawControlDescriptor>(Catalog(padMapping)); missing.RemoveAt(axis);
            Single("pad missing axis Rejected",
                RawInputMapping.ValidateHardware(FlightInputSourceKind.Gamepad, Layout, missing),
                RawMappingError.AxisNotFound, axis, Paths(padMapping)[axis]);
        }
        var empty = Validate(new RawControlDescriptor[0]);
        Rejected("empty directory no fallback", empty, RawMappingError.AxisNotFound);
        Check("empty directory four missing", empty.Diagnostics.Count == 4);
        var invalidMap = ReplacePath(Mapping, 0, "*");
        Single("catalog failure precedes invalid requested path",
            Validate(new RawControlDescriptor[] { null }, invalidMap), RawMappingError.MissingControlDescriptor);
    }

    private static void ExactPathsAndDuplicates()
    {
        string[] badPaths = { null, "", "/x", "x/", "x//y", ".", "..", "x/./y", "x/../y",
            "*", "a?", "{axis}", "<Joystick>/x", "x[0]", "x:y", "x\\y", "x y", "x\tz", "x\n",
            "x\0y", "x\u00a0y", "x\u202fy", "x\u3000y", "\ud800", "\udfff", "x\ud800y" };
        foreach (string bad in badPaths)
        {
            for (int axis = 0; axis < 4; axis++)
                Single("invalid request path not echoed", Validate(Catalog(), ReplacePath(Mapping, axis, bad)),
                    RawMappingError.InvalidMappingPath, axis);
            Single("invalid catalog path not echoed", Validate(new[] { Control(bad) }), RawMappingError.InvalidCatalogPath);
        }
        foreach (string valid in new[] { "a.b", ".x", "..x", "x.", "lever%2Fz", "合成/轴", "e\u0301/x", "é/x", "\ud834\udd1e/x" })
        {
            var map = ReplacePath(Mapping, 0, valid); Valid("legal literal path", Validate(Catalog(map), map));
        }
        foreach (string notExact in new[] { "LEVER/z", "Lever/z", "z", "SyntheticStick/lever/z", "lever%2Fz" })
            Single("no case suffix prefix percent decode", Validate(Catalog(), ReplacePath(Mapping, 0, notExact)),
                RawMappingError.AxisNotFound, 0, notExact);
        var composed = ReplacePath(Mapping, 0, "é/x");
        Single("no Unicode normalization", Validate(Catalog(composed), ReplacePath(composed, 0, "e\u0301/x")),
            RawMappingError.AxisNotFound, 0, "e\u0301/x");
        var duplicateMap = new AxisMapping("lever/z", "lever/z", "lever/z", "stick/x");
        var repeated = Validate(Catalog(), duplicateMap);
        Rejected("all three duplicated mapped axes", repeated, RawMappingError.DuplicateMappedPath);
        Equal("all implicated axes",
            DiagnosticText(repeated), "0|lever/z|path|DuplicateMappedPath;1|lever/z|path|DuplicateMappedPath;2|lever/z|path|DuplicateMappedPath;");
        var malformedRepeat = Validate(Catalog(), new AxisMapping("bad path", "bad path", "bad path", "bad path"));
        Check("invalid mapping paths do not duplicate group", malformedRepeat.Diagnostics.Count == 4 &&
            !Has(malformedRepeat, RawMappingError.DuplicateMappedPath));
        var mixedMap = Validate(Catalog(), new AxisMapping(null, "same", "same", "same"));
        Equal("invalid and valid duplicates independent", DiagnosticText(mixedMap),
            "0||path|InvalidMappingPath;1|same|path|DuplicateMappedPath;2|same|path|DuplicateMappedPath;3|same|path|DuplicateMappedPath;");
        foreach (var otherKind in new[] { RawControlValueKind.AxisFloat, RawControlValueKind.ButtonFloat, RawControlValueKind.Other })
        {
            var extras = new List<RawControlDescriptor>(Catalog()) { Control("unused"), Control("unused", kind: otherKind) };
            Single("unused catalog duplicate also rejects", Validate(extras), RawMappingError.DuplicateCatalogPath, path: "unused");
        }
        var errors = new[] { null, null, Control("", kind: (RawControlValueKind)99),
            Control("aa", kind: (RawControlValueKind)(-1)), Control("aa"), Control("bb"), Control("bb", kind: RawControlValueKind.ButtonFloat) };
        var multi = Validate(errors);
        Rejected("stage4 all independent errors", multi, RawMappingError.UnsupportedControlValueKind);
        const string expected = "-1||catalog|MissingControlDescriptor;-1||path|InvalidCatalogPath;-1||valueKind|UnsupportedControlValueKind;" +
            "-1|aa|catalog|DuplicateCatalogPath;-1|aa|valueKind|UnsupportedControlValueKind;-1|bb|catalog|DuplicateCatalogPath;";
        Equal("catalog sorted de-duplicated diagnostics", DiagnosticText(multi), expected);
        var random = new Random(Seed);
        for (int step = 0; step < 100; step++)
        { Shuffle(errors, random); Equal("bad catalog permutation invariant", DiagnosticText(Validate(errors)), expected); }
    }

    private static void TypesAndMetadata()
    {
        for (int axis = 0; axis < 4; axis++)
        {
            foreach (var kind in new[] { RawControlValueKind.ButtonFloat, RawControlValueKind.Other })
            {
                var catalog = Catalog(); catalog[axis] = new RawControlDescriptor(catalog[axis].Path, kind, null, 0, 0, null);
                Single("non axis skips only its metadata", Validate(catalog), kind == RawControlValueKind.ButtonFloat
                    ? RawMappingError.ButtonAxisNotAllowed : RawMappingError.AxisTypeMismatch, axis, catalog[axis].Path);
            }
            foreach (string bad in new[] { null, "", " A", "A B", "A\nB", "A\0B", "\ud800", "A,B", "A/B", "A\\B", "A=B", "A:B" })
            {
                var catalog = Catalog(); catalog[axis] = new RawControlDescriptor(catalog[axis].Path,
                    RawControlValueKind.AxisFloat, bad, Format, 32, Plain);
                Single("bad type does not invent actual CLR type", Validate(catalog),
                    RawMappingError.InvalidControlType, axis, catalog[axis].Path);
            }
            var missingMetadata = Catalog(); missingMetadata[axis] = new RawControlDescriptor(missingMetadata[axis].Path,
                RawControlValueKind.AxisFloat, null, 0, 0, null);
            var multiple = Validate(missingMetadata);
            Rejected("type format size and missing preprocess independent", multiple, RawMappingError.MissingPreprocess);
            Check("four metadata errors", multiple.Diagnostics.Count == 4 &&
                Has(multiple, RawMappingError.InvalidControlType, axis) &&
                Has(multiple, RawMappingError.InvalidStateFormat, axis) &&
                Has(multiple, RawMappingError.InvalidStateSize, axis));
        }
        var missing = Catalog(); missing[0] = new RawControlDescriptor("lever/z", RawControlValueKind.AxisFloat, null, 0, 0, null);
        Equal("metadata diagnostic field sorting", DiagnosticText(Validate(missing)),
            "0|lever/z|controlTypeFullName|InvalidControlType;0|lever/z|preprocess|MissingPreprocess;" +
            "0|lever/z|sizeInBits|InvalidStateSize;0|lever/z|stateFormatCode|InvalidStateFormat;");
        var nested = Catalog(); nested[0] = With(nested[0], type: "Synthetic.Axis+Nested", format: uint.MaxValue, size: uint.MaxValue);
        var nestedResult = Validate(nested); Valid("unusual valid size format subtype not guessed", nestedResult);
        Check("actual descriptor unchanged", nestedResult.Controls[0].SizeInBits == uint.MaxValue &&
            nestedResult.Controls[0].StateFormatCode == uint.MaxValue && nestedResult.Controls[0].ControlTypeFullName == "Synthetic.Axis+Nested");
        var spare = new List<RawControlDescriptor>(Catalog()) {
            new RawControlDescriptor("unused/button", RawControlValueKind.ButtonFloat, null, 0, 0, null),
            new RawControlDescriptor("unused/other", RawControlValueKind.Other, null, 0, 0, null),
            new RawControlDescriptor("unused/axis", RawControlValueKind.AxisFloat, null, 0, 0, null)
        };
        Equal("unselected metadata does not pollute hash", Validate(spare).MappingFingerprint, HardwareHash);
        for (int axis = 0; axis < 4; axis++)
        {
            var invalidKind = Catalog(); invalidKind[axis] = With(invalidKind[axis], kind: (RawControlValueKind)int.MaxValue);
            Single("unknown declared kind catalog-level", Validate(invalidKind), RawMappingError.UnsupportedControlValueKind,
                path: invalidKind[axis].Path);
        }
    }

    private static void PreprocessingRanges()
    {
        foreach (var enabled in new[] { false, true })
        {
            var p = new RawAxisPreprocess(enabled ? RawAxisClamp.BeforeNormalize : RawAxisClamp.None,
                -10, 20, .5f, enabled, -10, 20, 2, false, enabled, -2);
            Valid("finite asymmetric metadata preserved", Validate(Catalog(p: p)));
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                for (int field = 0; field < 7; field++)
                    for (int axis = 0; axis < 4; axis++)
                    {
                        var catalog = Catalog(p: p); catalog[axis] = With(catalog[axis], p: FloatField(p, field, invalid));
                        var result = Validate(catalog);
                        Rejected("each enabled/disabled nonfinite field", result, RawMappingError.NonFinitePreprocess);
                        Check("exact nonfinite field retained", Has(result, RawMappingError.NonFinitePreprocess, axis, FloatFields[field]));
                    }
        }
        foreach (var clamp in new[] { RawAxisClamp.None, RawAxisClamp.BeforeNormalize,
            RawAxisClamp.AfterNormalize, RawAxisClamp.ToConstantBeforeNormalize })
        {
            var equal = new RawAxisPreprocess(clamp, .001f, .001f, -.25f, false, 2, -2, 8, false, true, 0);
            Valid("equal clamp allowed disabled normalize arbitrary finite", Validate(Catalog(p: equal)));
            var reversed = new RawAxisPreprocess(clamp, 2, -2, .25f, false, 1, -1, 9, false, true, -3);
            if (clamp == RawAxisClamp.None) Valid("disabled clamp reversed range allowed", Validate(Catalog(p: reversed)));
            else
            {
                var result = Validate(Catalog(p: reversed));
                Rejected("all non-none clamps enforce order", result, RawMappingError.InvalidClampRange);
                Check("all four clamp diagnostics", result.Diagnostics.Count == 4);
            }
        }
        foreach (float zero in new[] { -10f, -1f, 0f, .999f })
            Valid("normalize zero below/equal min allowed", Validate(Catalog(p: new RawAxisPreprocess(
                RawAxisClamp.None, 0, 0, 0, true, -1, 1, zero, false, false, 1))));
        foreach (float zero in new[] { 1f, 2f })
            Rejected("normalize zero at/above max candidate rejected", Validate(Catalog(p: new RawAxisPreprocess(
                RawAxisClamp.None, 0, 0, 0, true, -1, 1, zero, false, false, 1))), RawMappingError.InvalidNormalizeRange);
        foreach (float minimum in new[] { 1f, 2f })
            Rejected("normalize min equals/above max", Validate(Catalog(p: new RawAxisPreprocess(
                RawAxisClamp.None, 0, 0, 0, true, minimum, 1, 0, false, false, 1))), RawMappingError.InvalidNormalizeRange);
        foreach (float factor in new[] { 0f, -1f, -float.MaxValue, float.MaxValue, float.Epsilon, -float.Epsilon })
            Valid("finite scaling does not execute or bound values", Validate(Catalog(p: FloatField(Plain, 6, factor))));
        var unknown = new RawAxisPreprocess((RawAxisClamp)99, 2, -2, 0, false, 0, 0, 0, false, false, 1);
        var unknownResult = Validate(Catalog(p: unknown));
        Rejected("unknown clamp collected not guessed", unknownResult, RawMappingError.UnsupportedClamp);
        Check("unknown clamp no range assumption", !Has(unknownResult, RawMappingError.InvalidClampRange));
        var allBad = new RawAxisPreprocess((RawAxisClamp)99, float.NaN, float.PositiveInfinity,
            float.NegativeInfinity, true, float.NaN, float.PositiveInfinity, float.NegativeInfinity, true, true, float.NaN);
        var bad = Catalog(); bad[0] = With(bad[0], p: allBad);
        var allBadResult = Validate(bad);
        Rejected("all nonfinite operands and unknown clamp", allBadResult, RawMappingError.UnsupportedClamp);
        Check("seven independent float diagnostics plus clamp", allBadResult.Diagnostics.Count == 8);
        for (int field = 0; field < 7; field++) Check("all nonfinite fields named", Has(allBadResult, RawMappingError.NonFinitePreprocess, 0, FloatFields[field]));
        var cases = new[] {
            new RawAxisPreprocess(RawAxisClamp.BeforeNormalize, 2, -2, float.NaN, true, 2, 1, 0, false, false, 1),
            new RawAxisPreprocess(RawAxisClamp.None, -1, 1, 0, true, 2, 1, float.NaN, false, false, 1),
            new RawAxisPreprocess(RawAxisClamp.None, -1, 1, 0, true, float.NaN, 1, 2, false, false, 1),
            new RawAxisPreprocess(RawAxisClamp.BeforeNormalize, 2, -2, 0, true, -1, float.NaN, 0, false, false, 1)
        };
        for (int index = 0; index < cases.Length; index++)
        {
            var catalog = Catalog(); catalog[0] = With(catalog[0], p: cases[index]);
            var result = Validate(catalog);
            Check("unrelated nonfinite does not suppress clamp comparison",
                Has(result, RawMappingError.InvalidClampRange, 0) == (index == 0 || index == 3));
            Check("independent finite normalize operands still compared",
                Has(result, RawMappingError.InvalidNormalizeRange, 0) == (index != 3));
            Check("range and nonfinite coexist", Has(result, RawMappingError.NonFinitePreprocess, 0));
        }
        var tiny = new RawAxisPreprocess(RawAxisClamp.None, -float.Epsilon, float.Epsilon, 0,
            true, -float.Epsilon, float.Epsilon, 0, false, false, float.Epsilon);
        Valid("no minimum-amplitude calibration threshold", Validate(Catalog(p: tiny)));
        var unsignedZero = new RawAxisPreprocess(RawAxisClamp.None, 0, 0, 0, false, 0, 0, 0, false, false, 0);
        float negativeZero = BitConverter.ToSingle(BitConverter.GetBytes(0x80000000u), 0);
        var signedZero = new RawAxisPreprocess(RawAxisClamp.None, negativeZero, negativeZero, negativeZero,
            false, negativeZero, negativeZero, negativeZero, false, false, negativeZero);
        Equal("all signed-zero fields have one canonical encoding", Validate(Catalog(p: unsignedZero)).MappingFingerprint,
            Validate(Catalog(p: signedZero)).MappingFingerprint);
    }

    // Independently framed UTF8/SHA encoder; never calls the identity/module codec.
    private static string ReferenceHash(IList<string> fields)
    {
        var utf8 = new UTF8Encoding(false, true);
        using (var bytes = new MemoryStream())
        {
            foreach (string text in fields)
            {
                var value = utf8.GetBytes(text);
                var length = Encoding.ASCII.GetBytes(value.Length.ToString(CultureInfo.InvariantCulture));
                bytes.Write(length, 0, length.Length); bytes.WriteByte((byte)':'); bytes.Write(value, 0, value.Length);
            }
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
        }
    }
    private static string Number(float value) => value == 0 ? "0" : value.ToString("R", CultureInfo.InvariantCulture);
    private static string ReferenceHardware(string layout, AxisMapping mapping, IReadOnlyList<RawControlDescriptor> catalog)
    {
        var paths = Paths(mapping);
        var fields = new List<string> { "mapping-v1", layout, "unity-unprocessed-control-v1" };
        foreach (string path in paths) fields.Add(path);
        foreach (string path in paths)
        {
            RawControlDescriptor selected = null;
            foreach (var c in catalog) if (c.Path == path) selected = c;
            if (selected == null) throw new InvalidOperationException("Reference vector missing synthetic control.");
            var p = selected.Preprocess;
            string[] modes = { "none", "before_normalize", "after_normalize", "to_constant_before_normalize" };
            fields.AddRange(new[] { selected.ControlTypeFullName, selected.StateFormatCode.ToString(CultureInfo.InvariantCulture),
                selected.SizeInBits.ToString(CultureInfo.InvariantCulture), modes[(int)p.Clamp], Number(p.ClampMin),
                Number(p.ClampMax), Number(p.ClampConstant), p.Normalize ? "1" : "0", Number(p.NormalizeMin),
                Number(p.NormalizeMax), Number(p.NormalizeZero), p.Invert ? "1" : "0", p.Scale ? "1" : "0", Number(p.ScaleFactor) });
        }
        return ReferenceHash(fields);
    }

    private static void LiteralHashesAndChanges()
    {
        Equal("literal keyboard precomputed outside implementation", RawInputMapping.GetBuiltIn(FlightInputSourceKind.Keyboard).MappingFingerprint, KeyboardHash);
        Equal("literal touch precomputed outside implementation", RawInputMapping.GetBuiltIn(FlightInputSourceKind.Touch).MappingFingerprint, TouchHash);
        Equal("literal hardware four paths before metadata", Validate(Catalog()).MappingFingerprint, HardwareHash);
        Equal("reference codec agrees literal fixture", ReferenceHardware(Layout, Mapping, Catalog()), HardwareHash);
        var unicodeMap = new AxisMapping("油门/轴", "方向/轴", "俯仰/轴", "横滚/轴");
        var unicodeP = new RawAxisPreprocess(RawAxisClamp.BeforeNormalize, -10, 20, .5f, true, -10, 20, 2, false, true, -2);
        var unicodeCatalog = Catalog(unicodeMap, unicodeP);
        for (int axis = 0; axis < 4; axis++) unicodeCatalog[axis] = With(unicodeCatalog[axis], type: "Synthetic.轴Control", format: uint.MaxValue, size: 64);
        var unicodeResult = Validate(unicodeCatalog, unicodeMap, "合成🎮");
        Valid("nonASCII byte count fixture", unicodeResult);
        Equal("literal nonASCII SHA", unicodeResult.MappingFingerprint, UnicodeHash);
        Equal("reference nonASCII encoding", ReferenceHardware("合成🎮", unicodeMap, unicodeCatalog), UnicodeHash);
        Check("length prefix prevents simple delimiter collisions",
            ReferenceHash(new[] { "a", "bc" }) != ReferenceHash(new[] { "ab", "c" }) &&
            ReferenceHash(new[] { "a:1", "b" }) != ReferenceHash(new[] { "a", "1:b" }));
        Check("layout change fingerprint", Validate(Catalog(), layout: "SyntheticStickV2").MappingFingerprint != HardwareHash);
        for (int axis = 0; axis < 4; axis++)
        {
            var map = ReplacePath(Mapping, axis, Paths(Mapping)[axis] + ".v2");
            Check("each path changes fingerprint", Validate(Catalog(map), map).MappingFingerprint != HardwareHash);
            var type = Catalog(); type[axis] = With(type[axis], type: "Synthetic.DifferentAxis");
            Check("each type changes fingerprint", Validate(type).MappingFingerprint != HardwareHash);
            var format = Catalog(); format[axis] = With(format[axis], format: Format + 1);
            Check("each format changes fingerprint", Validate(format).MappingFingerprint != HardwareHash);
            var size = Catalog(); size[axis] = With(size[axis], size: 31);
            Check("each bit size changes fingerprint", Validate(size).MappingFingerprint != HardwareHash);
            for (int field = 0; field < 7; field++)
            {
                var controls = Catalog(); var original = Plain;
                float[] values = { original.ClampMin, original.ClampMax, original.ClampConstant, original.NormalizeMin,
                    original.NormalizeMax, original.NormalizeZero, original.ScaleFactor };
                controls[axis] = With(controls[axis], p: FloatField(original, field, values[field] + .25f));
                Check("each float including disabled fields hashes", Validate(controls).MappingFingerprint != HardwareHash);
            }
            var switchP = new[] {
                new RawAxisPreprocess(RawAxisClamp.BeforeNormalize, -1, 1, 0, false, -1, 1, 0, false, false, 1),
                new RawAxisPreprocess(RawAxisClamp.None, -1, 1, 0, true, -1, 1, 0, false, false, 1),
                new RawAxisPreprocess(RawAxisClamp.None, -1, 1, 0, false, -1, 1, 0, true, false, 1),
                new RawAxisPreprocess(RawAxisClamp.None, -1, 1, 0, false, -1, 1, 0, false, true, 1)
            };
            foreach (var p in switchP)
            { var catalog = Catalog(); catalog[axis] = With(catalog[axis], p: p); Check("each enum/bool hashes", Validate(catalog).MappingFingerprint != HardwareHash); }
        }
        var modeHashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mode in new[] { RawAxisClamp.None, RawAxisClamp.BeforeNormalize, RawAxisClamp.AfterNormalize, RawAxisClamp.ToConstantBeforeNormalize })
            modeHashes.Add(Validate(Catalog(p: new RawAxisPreprocess(mode, -1, 1, 0, false, -1, 1, 0, false, false, 1))).MappingFingerprint);
        Check("all clamp symbolic codes distinct", modeHashes.Count == 4);
        var spare = new List<RawControlDescriptor>(Catalog()) { Control("unused/axis"), Control("unused/button", kind: RawControlValueKind.ButtonFloat) };
        var rnd = new Random(Seed);
        var shuffled = spare.ToArray();
        for (int step = 0; step < 100; step++)
        { Shuffle(shuffled, rnd); Equal("selected order not catalog order", Validate(shuffled).MappingFingerprint, HardwareHash); }
        Equal("Unicode layout no NFC rewrite", Validate(Catalog(), layout: "e\u0301").MappingFingerprint,
            ReferenceHardware("e\u0301", Mapping, Catalog()));
        Check("Unicode layouts not merged", Validate(Catalog(), layout: "e\u0301").MappingFingerprint != Validate(Catalog(), layout: "é").MappingFingerprint);
        var priorCulture = CultureInfo.CurrentCulture; var priorUI = CultureInfo.CurrentUICulture;
        try
        {
            foreach (string culture in new[] { "en-US", "fr-FR", "tr-TR", "ar-SA", "zh-CN" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture); CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                Equal("culture independent base", Validate(Catalog()).MappingFingerprint, HardwareHash);
                Equal("culture independent unicode", Validate(unicodeCatalog, unicodeMap, "合成🎮").MappingFingerprint, UnicodeHash);
                Equal("culture independent builtin", RawInputMapping.GetBuiltIn(FlightInputSourceKind.Keyboard).MappingFingerprint, KeyboardHash);
                var boundary = Catalog(p: new RawAxisPreprocess(RawAxisClamp.None, float.Epsilon, -float.MaxValue,
                    .0000001f, false, float.MaxValue, -float.Epsilon, 1.00000012f, true, true, -1.2345678f));
                Equal("round-trip numeric invariant", Validate(boundary).MappingFingerprint, ReferenceHardware(Layout, Mapping, boundary));
            }
        }
        finally { CultureInfo.CurrentCulture = priorCulture; CultureInfo.CurrentUICulture = priorUI; }
    }

    private static void ImmutableSurface()
    {
        var input = new List<RawControlDescriptor>(Catalog()); var mapping = Mapping;
        var first = Validate(input, mapping); var text = ResultText(first);
        var firstControl = first.Controls[0];
        input[0] = Control("replacement"); input.Clear();
        Check("caller collection cannot mutate old resolved controls", first.Controls.Count == 4 && ReferenceEquals(first.Controls[0], firstControl));
        Equal("old result immutable after unrelated calls", ResultText(first), text);
        Rejected("subsequent mutated caller list not cached", Validate(input), RawMappingError.AxisNotFound);
        Equal("old result still stable", ResultText(first), text);
        foreach (Type type in new[] { typeof(AxisMapping), typeof(RawControlDescriptor), typeof(RawAxisPreprocess),
            typeof(RawMappingValidationResult), typeof(RawMappingDiagnostic) })
        {
            Check("DTO sealed", type.IsSealed);
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                Check("public DTO getter only " + type.Name + "." + property.Name, property.GetSetMethod(true) == null);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                Check("instance DTO storage readonly", field.IsInitOnly);
        }
        Check("only validator creates result", typeof(RawMappingValidationResult).GetConstructors().Length == 0);
        Check("only validator creates diagnostic", typeof(RawMappingDiagnostic).GetConstructors().Length == 0);
        var controls = (IList<RawControlDescriptor>)first.Controls;
        ThrowsNotSupported("controls setter blocked", () => controls[0] = Control("replacement"));
        ThrowsNotSupported("controls add blocked", () => controls.Add(Control("replacement")));
        ThrowsNotSupported("controls remove blocked", () => controls.RemoveAt(0));
        ThrowsNotSupported("controls clear blocked", () => controls.Clear());
        Check("controls not escaped array/list", !(first.Controls is RawControlDescriptor[]) && !(first.Controls is List<RawControlDescriptor>));
        var failure = Validate(new[] { Control("x") });
        var diagnostics = (IList<RawMappingDiagnostic>)failure.Diagnostics;
        ThrowsNotSupported("diagnostics setter blocked", () => diagnostics[0] = null);
        ThrowsNotSupported("diagnostics clear blocked", () => diagnostics.Clear());
        Check("diagnostics not escaped array/list", !(failure.Diagnostics is RawMappingDiagnostic[]) && !(failure.Diagnostics is List<RawMappingDiagnostic>));
        var logical = RawInputMapping.GetBuiltIn(FlightInputSourceKind.Touch);
        ThrowsNotSupported("empty builtin controls blocked", () => ((IList)logical.Controls).Add(null));
        Check("no UserValidated enum or sample flag", Array.IndexOf(Enum.GetNames(typeof(RawMappingValidationStatus)), "UserValidated") < 0 &&
            typeof(RawMappingValidationResult).GetProperty("Available") == null &&
            typeof(RawMappingValidationResult).GetProperty("MappingRevision") == null &&
            typeof(RawMappingValidationResult).GetProperty("SourceId") == null);
        Check("mapping only four public string fields", typeof(AxisMapping).GetProperties().Length == 4);
        var methods = typeof(RawInputMapping).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Check("exactly two public stateless functions", methods.Length == 2);
        Check("no instance state or side effects object", typeof(RawInputMapping).GetConstructors().Length == 0);
        foreach (var reference in typeof(RawInputMapping).Assembly.GetReferencedAssemblies())
            Check("pure compilation no Unity reference", reference.Name.IndexOf("Unity", StringComparison.OrdinalIgnoreCase) < 0);
    }
    private static void ThrowsNotSupported(string name, Action mutation)
    {
        bool blocked = false;
        try { mutation(); } catch (NotSupportedException) { blocked = true; }
        Check(name, blocked);
    }
    private static void Shuffle<T>(T[] values, Random random)
    {
        for (int last = values.Length - 1; last > 0; last--)
        { int other = random.Next(last + 1); T temp = values[last]; values[last] = values[other]; values[other] = temp; }
    }
    private static float RandomFinite(Random random)
    {
        for (;;)
        {
            uint bits = (uint)random.Next() | (random.Next(2) == 0 ? 0u : 0x80000000u);
            if ((bits & 0x7f800000u) == 0x7f800000u) continue;
            return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
        }
    }

    private static void DeterministicRandom()
    {
        var random = new Random(Seed);
        var culture = CultureInfo.CurrentCulture;
        try
        {
            for (int tick = 0; tick < RandomCases; tick++)
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(new[] { "en-US", "fr-FR", "tr-TR" }[tick % 3]);
                var map = new AxisMapping("组" + tick + "/t", "组" + tick + "/y", "组" + tick + "/p", "组" + tick + "/r");
                var catalog = Catalog(map);
                for (int axis = 0; axis < 4; axis++)
                {
                    var p = new RawAxisPreprocess(RawAxisClamp.None, RandomFinite(random), RandomFinite(random),
                        RandomFinite(random), false, RandomFinite(random), RandomFinite(random), RandomFinite(random),
                        random.Next(2) == 0, random.Next(2) == 0, RandomFinite(random));
                    catalog[axis] = new RawControlDescriptor(catalog[axis].Path, RawControlValueKind.AxisFloat,
                        "Synthetic.Axis" + axis, (uint)random.Next(1, int.MaxValue), (uint)random.Next(1, 257), p);
                }
                string layout = "RandomLayout" + tick;
                var valid = Validate(catalog, map, layout); Valid("random valid metadata", valid);
                Equal("random independent encoding", valid.MappingFingerprint, ReferenceHardware(layout, map, catalog));
                var extended = new List<RawControlDescriptor>(catalog) {
                    new RawControlDescriptor("spare/axis", RawControlValueKind.AxisFloat, null, 0, 0, null),
                    new RawControlDescriptor("spare/button", RawControlValueKind.ButtonFloat, null, 0, 0, null)
                }.ToArray();
                Shuffle(extended, random);
                Equal("random reorder and unused metadata invariant", Validate(extended, map, layout).MappingFingerprint, valid.MappingFingerprint);
                int axisIndex = random.Next(4), choice = tick % 10;
                var bad = (RawControlDescriptor[])catalog.Clone();
                AxisMapping badMap = map;
                RawMappingError expected;
                switch (choice)
                {
                    case 0:
                        var duplicated = new List<RawControlDescriptor>(catalog) { catalog[axisIndex] };
                        Rejected("random duplicate", Validate(duplicated, map, layout), RawMappingError.DuplicateCatalogPath);
                        continue;
                    case 1:
                        badMap = ReplacePath(map, axisIndex, Paths(map)[(axisIndex + 1) % 4]); expected = RawMappingError.DuplicateMappedPath; break;
                    case 2:
                        bad[axisIndex] = With(bad[axisIndex], p: FloatField(bad[axisIndex].Preprocess, random.Next(7), float.NaN));
                        expected = RawMappingError.NonFinitePreprocess; break;
                    case 3:
                        bad[axisIndex] = With(bad[axisIndex], kind: RawControlValueKind.ButtonFloat);
                        expected = RawMappingError.ButtonAxisNotAllowed; break;
                    case 4:
                        badMap = ReplacePath(map, axisIndex, "notFound/" + tick); expected = RawMappingError.AxisNotFound; break;
                    case 5:
                        bad[axisIndex] = With(bad[axisIndex], p: new RawAxisPreprocess(RawAxisClamp.BeforeNormalize,
                            20, -10, 0, false, 0, 1, 0, false, false, 1)); expected = RawMappingError.InvalidClampRange; break;
                    case 6:
                        bad[axisIndex] = With(bad[axisIndex], p: new RawAxisPreprocess(RawAxisClamp.None,
                            0, 0, 0, true, -10, 20, 20, false, false, 1)); expected = RawMappingError.InvalidNormalizeRange; break;
                    case 7:
                        badMap = ReplacePath(map, axisIndex, "bad/*/" + tick); expected = RawMappingError.InvalidMappingPath; break;
                    case 8:
                        bad[axisIndex] = With(bad[axisIndex], type: "Axis,Assembly=Bad"); expected = RawMappingError.InvalidControlType; break;
                    default:
                        bad[axisIndex] = With(bad[axisIndex], kind: (RawControlValueKind)99);
                        expected = RawMappingError.UnsupportedControlValueKind; break;
                }
                var rejected = Validate(bad, badMap, layout); Rejected("random one fault explicit", rejected, expected);
                Shuffle(bad, random);
                Equal("random rejection diagnostic order invariant", ResultText(Validate(bad, badMap, layout)), ResultText(rejected));
                Check("random failure cannot mutate prior result", valid.IsValid && valid.Diagnostics.Count == 0 &&
                    valid.Controls[axisIndex].Path == Paths(map)[axisIndex] && valid.MappingFingerprint.Length == 64);
            }
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    public static int Main()
    {
        try
        {
            StatusAndStages(); ExactPathsAndDuplicates(); TypesAndMetadata(); PreprocessingRanges();
            LiteralHashesAndChanges(); ImmutableSurface(); DeterministicRandom();
        }
        catch (Exception error)
        {
            _failures++; Console.WriteLine("UNEXPECTED " + error.GetType().FullName + ": " + error.Message);
            Console.WriteLine(error.StackTrace);
        }
        Console.WriteLine("RAW_MAPPING_DEVELOPER checks=" + _checks + " failures=" + _failures +
            " randomCases=" + RandomCases + " seed=" + Seed);
        return _failures == 0 ? 0 : 1;
    }
}
