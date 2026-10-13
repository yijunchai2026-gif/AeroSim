using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using AeroSim.Input;

// Independent contract consumer. All literal vectors/cases are loaded from the
// pre-source frozen JSON files, including the final stateful retention sequence.
internal static class IndependentRawMappingQa
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    private static readonly string[] BasePaths = { "throttle/y", "yaw/x", "pitch/y", "roll/x" };
    private static readonly string[] FloatFields = { "clampMin", "clampMax", "clampConstant", "normalizeMin", "normalizeMax", "normalizeZero", "scaleFactor" };
    private static readonly HashSet<string> CaseIds = new HashSet<string>(StringComparer.Ordinal);
    private static readonly HashSet<string> VectorIds = new HashSet<string>(StringComparer.Ordinal);
    private static readonly List<string> Failures = new List<string>();
    private static int Checks, ZeroSignChecks, CulturePasses;
    private static Dictionary<string, object> Defaults, BaseMetadata;

    private sealed class Fixture
    {
        internal FlightInputSourceKind Kind;
        internal string Layout;
        internal List<RawControlDescriptor> Catalog;
        internal AxisMapping Mapping;
        internal string Operation;
    }

    private static Dictionary<string, object> Obj(object x) => (Dictionary<string, object>)x;
    private static object[] Arr(object x) => ((IEnumerable)x).Cast<object>().ToArray();
    private static string Str(object x) => x == null ? null : Convert.ToString(x, CultureInfo.InvariantCulture);
    private static Dictionary<string, object> Copy(Dictionary<string, object> x) => new Dictionary<string, object>(x, StringComparer.Ordinal);
    private static void Merge(Dictionary<string, object> target, Dictionary<string, object> changes)
    { foreach (var kv in changes) target[kv.Key] = kv.Value; }
    private static Dictionary<string, object> Map(params object[] pairs)
    {
        var d = new Dictionary<string, object>(StringComparer.Ordinal);
        for (int i = 0; i < pairs.Length; i += 2) d.Add((string)pairs[i], pairs[i + 1]);
        return d;
    }

    private static void Check(bool condition, string label)
    {
        Checks++;
        if (!condition) { Failures.Add(label); if (Failures.Count <= 40) Console.Error.WriteLine("FAIL " + label); }
    }
    private static void Test(string id, Action action)
    {
        try { action(); }
        catch (Exception e) { Check(false, id + ": unexpected " + e.GetType().Name + " " + e.Message); }
    }
    private static void Equal(object expected, object actual, string label)
    {
        if (expected == null || actual == null) { Check(expected == null && actual == null, label); return; }
        var eo = expected as Dictionary<string, object>;
        var ao = actual as Dictionary<string, object>;
        if (eo != null || ao != null)
        {
            Check(eo != null && ao != null, label + ".object");
            if (eo == null || ao == null) return;
            Check(eo.Count == ao.Count, label + ".key-count");
            foreach (var kv in eo)
            {
                Check(ao.ContainsKey(kv.Key), label + ".has-" + kv.Key);
                if (ao.ContainsKey(kv.Key)) Equal(kv.Value, ao[kv.Key], label + "." + kv.Key);
            }
            return;
        }
        if (!(expected is string) && expected is IEnumerable)
        {
            Check(!(actual is string) && actual is IEnumerable, label + ".array");
            if (actual is string || !(actual is IEnumerable)) return;
            var e = Arr(expected); var a = Arr(actual);
            Check(e.Length == a.Length, label + ".length");
            for (int i = 0; i < Math.Min(e.Length, a.Length); i++) Equal(e[i], a[i], label + "[" + i + "]");
            return;
        }
        Check(expected.Equals(actual) || (expected is IConvertible && actual is IConvertible &&
            !(expected is string) && !(actual is string) && !(expected is bool) && !(actual is bool) &&
            Convert.ToDecimal(expected, CultureInfo.InvariantCulture) == Convert.ToDecimal(actual, CultureInfo.InvariantCulture)), label);
    }

    private static int Bits(float x) => BitConverter.ToInt32(BitConverter.GetBytes(x), 0);
    private static float Single(object x)
    {
        string text = Str(x);
        if (text == "-0")
        {
            float negative = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0);
            ZeroSignChecks++; Check(Bits(negative) == int.MinValue, "negative-zero.builder-sign-bit");
            return negative;
        }
        return float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
    private static T EnumValue<T>(object x) where T : struct
    { return (T)Enum.Parse(typeof(T), Str(x), false); }
    private static RawAxisPreprocess Preprocess(Dictionary<string, object> p)
    {
        return new RawAxisPreprocess(EnumValue<RawAxisClamp>(p["clamp"]), Single(p["clampMin"]), Single(p["clampMax"]),
            Single(p["clampConstant"]), (bool)p["normalize"], Single(p["normalizeMin"]), Single(p["normalizeMax"]),
            Single(p["normalizeZero"]), (bool)p["invert"], (bool)p["scale"], Single(p["scaleFactor"]));
    }
    private static RawControlDescriptor Descriptor(string path, Dictionary<string, object> m)
    {
        return new RawControlDescriptor(path, EnumValue<RawControlValueKind>(m["valueKind"]), Str(m["controlTypeFullName"]),
            Convert.ToUInt32(m["stateFormatCode"], CultureInfo.InvariantCulture), Convert.ToUInt32(m["sizeInBits"], CultureInfo.InvariantCulture),
            m["preprocess"] == null ? null : Preprocess(Obj(m["preprocess"])));
    }
    private static AxisMapping Mapping(object paths)
    {
        if (paths == null) return null;
        var a = Arr(paths); Check(a.Length == 4, "fixture-mapping-four-paths");
        return new AxisMapping(Str(a[0]), Str(a[1]), Str(a[2]), Str(a[3]));
    }
    private static Fixture Build(Dictionary<string, object> input)
    {
        var options = Copy(Defaults); Merge(options, input);
        var f = new Fixture { Operation = Str(options["operation"]), Kind = EnumValue<FlightInputSourceKind>(options["kind"]),
            Layout = Str(options["layout"]), Mapping = Mapping(options["mappingPaths"]) };
        if (options["catalogPaths"] == null) return f;
        f.Catalog = new List<RawControlDescriptor>();
        var paths = Arr(options["catalogPaths"]);
        for (int i = 0; i < paths.Length; i++)
        {
            var m = Copy(BaseMetadata); m["preprocess"] = Copy(Obj(BaseMetadata["preprocess"]));
            if (input.ContainsKey("metadataOverrides"))
            {
                var overrides = Obj(input["metadataOverrides"]);
                if (overrides.ContainsKey(i.ToString(CultureInfo.InvariantCulture)))
                {
                    var changed = Obj(overrides[i.ToString(CultureInfo.InvariantCulture)]);
                    Merge(m, changed);
                }
            }
            if (input.ContainsKey("allAxisPreprocessOverrides"))
            {
                var p = Copy(Obj(m["preprocess"])); Merge(p, Obj(input["allAxisPreprocessOverrides"])); m["preprocess"] = p;
            }
            f.Catalog.Add(Descriptor(Str(paths[i]), m));
        }
        return f;
    }
    private static RawMappingValidationResult Invoke(Fixture f) => f.Operation == "GetBuiltIn"
        ? RawInputMapping.GetBuiltIn(f.Kind) : RawInputMapping.ValidateHardware(f.Kind, f.Layout, f.Catalog, f.Mapping);
    private static object[] Paths(AxisMapping m) => m == null ? null : new object[] { m.ThrottlePath, m.YawPath, m.PitchPath, m.RollPath };
    private static Dictionary<string, object> Project(RawMappingValidationResult r)
    {
        return Map("status", r.Status.ToString(), "isValid", r.IsValid, "kind", r.Kind.ToString(), "rawConvention", r.RawConvention,
            "mappingPaths", Paths(r.Mapping), "controlsPaths", r.Controls.Select(c => (object)c.Path).ToArray(),
            "fingerprint", r.MappingFingerprint, "diagnostics", r.Diagnostics.Select(d => (object)Map("error", d.Error.ToString(),
                "axisIndex", d.AxisIndex, "path", d.Path, "field", d.Field)).ToArray());
    }
    private static Dictionary<string, object> DescriptorSnapshot(RawControlDescriptor c)
    {
        var p = c.Preprocess;
        return Map("path", c.Path, "valueKind", c.ValueKind.ToString(), "controlTypeFullName", c.ControlTypeFullName,
            "stateFormatCode", c.StateFormatCode, "sizeInBits", c.SizeInBits, "preprocess", p == null ? null : Map(
            "clamp", p.Clamp.ToString(), "clampMinBits", Bits(p.ClampMin), "clampMaxBits", Bits(p.ClampMax),
            "clampConstantBits", Bits(p.ClampConstant), "normalize", p.Normalize, "normalizeMinBits", Bits(p.NormalizeMin),
            "normalizeMaxBits", Bits(p.NormalizeMax), "normalizeZeroBits", Bits(p.NormalizeZero), "invert", p.Invert,
            "scale", p.Scale, "scaleFactorBits", Bits(p.ScaleFactor)));
    }
    private static void SuccessMetadata(Fixture f, RawMappingValidationResult r, string id)
    {
        if (!r.IsValid || r.Controls.Count == 0) return;
        foreach (var c in r.Controls)
        {
            var input = f.Catalog.Single(x => x.Path == c.Path);
            Equal(DescriptorSnapshot(input), DescriptorSnapshot(c), id + ".metadata." + c.Path);
        }
    }
    private static void ReadOnly<T>(IReadOnlyList<T> values, string id)
    {
        Check(!(values is T[]), id + ".no-array-exposure");
        var list = values as IList<T>;
        if (list == null) return;
        Check(list.IsReadOnly, id + ".read-only");
        bool threw = false; try { list.Add(default(T)); } catch (NotSupportedException) { threw = true; }
        Check(threw, id + ".add-rejected");
        threw = false; try { list.Clear(); } catch (NotSupportedException) { threw = true; }
        Check(threw, id + ".clear-rejected");
        if (list.Count == 0) return;
        threw = false; try { list[0] = default(T); } catch (NotSupportedException) { threw = true; }
        Check(threw, id + ".replace-rejected");
    }
    private static void LiteralCases(object[] cases)
    {
        foreach (var entry in cases)
        {
            var c = Obj(entry); string id = Str(c["id"]); CaseIds.Add(id);
            Test(id, () =>
            {
                var input = Obj(c["input"]);
                if (input.ContainsKey("sequence"))
                {
                    Equal(new object[] { "validate-base-as-old-result", "clear-input-list", "validate-cleared-list-as-new-result", "attempt-IList-mutation-if-exposed" }, input["sequence"], id + ".sequence-consumed");
                    var f = Build(new Dictionary<string, object>()); var before = f.Catalog.Select(DescriptorSnapshot).ToArray();
                    var old = Invoke(f); Equal(c["expectedOldResult"], Project(old), id + ".initial-old");
                    SuccessMetadata(f, old, id); f.Catalog.Clear(); var next = Invoke(f);
                    Equal(c["expectedNewResult"], Project(next), id + ".new");
                    ReadOnly(old.Controls, id + ".old-controls"); ReadOnly(next.Diagnostics, id + ".new-diagnostics");
                    ReadOnly(old.Diagnostics, id + ".old-diagnostics");
                    Equal(c["expectedOldResult"], Project(old), id + ".retained-old");
                    Equal(before, old.Controls.Select(DescriptorSnapshot).ToArray(), id + ".retained-metadata");
                    return;
                }
                var fixture = Build(input);
                var snapshot = fixture.Catalog == null ? null : fixture.Catalog.Select(DescriptorSnapshot).ToArray();
                var result = Invoke(fixture); Equal(c["expected"], Project(result), id);
                SuccessMetadata(fixture, result, id);
                Equal(snapshot, fixture.Catalog == null ? null : fixture.Catalog.Select(DescriptorSnapshot).ToArray(), id + ".input-unmodified");
                if (input.ContainsKey("allAxisPreprocessOverrides"))
                {
                    foreach (var axis in result.Controls)
                    {
                        ZeroSignChecks += 2;
                        Check(Bits(axis.Preprocess.ClampConstant) == int.MinValue, id + ".retained-clampConstant-negative-zero");
                        Check(Bits(axis.Preprocess.NormalizeZero) == int.MinValue, id + ".retained-normalizeZero-negative-zero");
                    }
                }
            });
        }
    }

    private static string Sha(byte[] bytes)
    { using (var s = SHA256.Create()) return BitConverter.ToString(s.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    private static void LiteralVectors(object[] vectors)
    {
        foreach (var entry in vectors)
        {
            var v = Obj(entry); string id = Str(v["id"]); VectorIds.Add(id);
            Test(id, () =>
            {
                var raw = Convert.FromBase64String(Str(v["encodedUtf8Base64"]));
                Check(raw.Length == Convert.ToInt32(v["byteLength"], CultureInfo.InvariantCulture), id + ".preencoded-length");
                Check(Sha(raw) == Str(v["expectedFingerprint"]), id + ".preencoded-hash");
                var fields = Arr(v["fieldSequence"]).Select(Str).ToArray();
                // This reconstructs only the frozen oracle bytes, not production metadata.
                var utf8 = new UTF8Encoding(false, true);
                var bytes = new List<byte>();
                foreach (var s in fields) { var b = utf8.GetBytes(s); bytes.AddRange(Encoding.ASCII.GetBytes(b.Length.ToString(CultureInfo.InvariantCulture) + ":")); bytes.AddRange(b); }
                Check(bytes.SequenceEqual(raw), id + ".preencoded-fields");
                RawMappingValidationResult actual;
                if (fields.Length == 4)
                    actual = RawInputMapping.GetBuiltIn(id == "keyboard" ? FlightInputSourceKind.Keyboard : FlightInputSourceKind.Touch);
                else
                {
                    Check(fields.Length == 63, id + ".full-hardware-field-count");
                    string[] paths = fields.Skip(3).Take(4).ToArray(); var catalog = new List<RawControlDescriptor>();
                    for (int axis = 0; axis < 4; axis++)
                    {
                        int o = 7 + axis * 14;
                        var clamp = new Dictionary<string, RawAxisClamp> { { "none", RawAxisClamp.None }, { "before_normalize", RawAxisClamp.BeforeNormalize }, { "after_normalize", RawAxisClamp.AfterNormalize }, { "to_constant_before_normalize", RawAxisClamp.ToConstantBeforeNormalize } };
                        var p = new RawAxisPreprocess(clamp[fields[o + 3]], Single(fields[o + 4]), Single(fields[o + 5]), Single(fields[o + 6]),
                            fields[o + 7] == "1", Single(fields[o + 8]), Single(fields[o + 9]), Single(fields[o + 10]), fields[o + 11] == "1", fields[o + 12] == "1", Single(fields[o + 13]));
                        catalog.Add(new RawControlDescriptor(paths[axis], RawControlValueKind.AxisFloat, fields[o],
                            uint.Parse(fields[o + 1], CultureInfo.InvariantCulture), uint.Parse(fields[o + 2], CultureInfo.InvariantCulture), p));
                    }
                    var kind = id == "gamepad-fixed" ? FlightInputSourceKind.Gamepad : FlightInputSourceKind.Joystick;
                    var f = new Fixture { Kind = kind, Layout = fields[1], Catalog = catalog, Operation = "ValidateHardware", Mapping = kind == FlightInputSourceKind.Gamepad ? null : Mapping(paths) };
                    actual = Invoke(f); SuccessMetadata(f, actual, id);
                    Check(actual.Status == (kind == FlightInputSourceKind.Gamepad ? RawMappingValidationStatus.BuiltInValid : RawMappingValidationStatus.JoystickCandidate), id + ".candidate-not-authorization");
                    Equal(paths, Paths(actual.Mapping), id + ".mapped-paths");
                }
                Check(actual.IsValid, id + ".valid"); Check(actual.Diagnostics.Count == 0, id + ".diagnostics-empty");
                Check(actual.MappingFingerprint == Str(v["expectedFingerprint"]), id + ".literal-production-hash");
                Check(actual.MappingFingerprint.Length == 64 && actual.MappingFingerprint.All(c => "0123456789abcdef".Contains(c)), id + ".lowercase-unprefixed");
            });
        }
    }

    private static RawMappingValidationResult BaseResult(Dictionary<string, object> options = null) => Invoke(Build(options ?? new Dictionary<string, object>()));
    private static Dictionary<string, object> ChangeMetadata(int axis, params object[] pairs) => Map("metadataOverrides", Map(axis.ToString(CultureInfo.InvariantCulture), Map(pairs)));
    private static Dictionary<string, object> ChangePreprocess(int axis, params object[] pairs)
    { var p = Copy(Obj(BaseMetadata["preprocess"])); Merge(p, Map(pairs)); return ChangeMetadata(axis, "preprocess", p); }
    private static void Rejected(RawMappingValidationResult r, RawMappingError error, int axis, string path, string field, string id)
    {
        Equal(Map("status", "Rejected", "isValid", false, "kind", r.Kind.ToString(), "rawConvention", "", "mappingPaths", null,
            "controlsPaths", new object[0], "fingerprint", "", "diagnostics", new object[] { Map("error", error.ToString(), "axisIndex", axis, "path", path, "field", field) }), Project(r), id);
    }
    private static void ApiShape(string sourcePath)
    {
        Type[] dtoTypes = { typeof(AxisMapping), typeof(RawControlDescriptor), typeof(RawAxisPreprocess), typeof(RawMappingDiagnostic), typeof(RawMappingValidationResult) };
        foreach (var t in dtoTypes)
        {
            Check(t.IsSealed, t.Name + ".sealed"); Check(t.GetFields(BindingFlags.Public | BindingFlags.Instance).Length == 0, t.Name + ".no-public-fields");
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance)) Check(p.GetSetMethod(true) == null, t.Name + ".get-only-" + p.Name);
        }
        Check(typeof(RawMappingDiagnostic).GetConstructors().Length == 0, "diagnostic-no-public-constructor");
        Check(typeof(RawMappingValidationResult).GetConstructors().Length == 0, "result-no-public-constructor");
        Check(typeof(AxisMapping).GetProperties().Length == 4, "mapping-only-four-path-properties");
        Check(typeof(AxisMapping).GetConstructors().Single().GetParameters().All(p => p.ParameterType == typeof(string)), "mapping-no-indices-or-array");
        Equal(new[] { "BuiltInValid", "JoystickCandidate", "Missing", "Rejected" }, Enum.GetNames(typeof(RawMappingValidationStatus)), "status-no-user-validation-authority");
        Check(typeof(RawMappingValidationResult).GetProperty("Available") == null && typeof(RawMappingValidationResult).GetProperty("SourceId") == null && typeof(RawMappingValidationResult).GetProperty("UserValidated") == null, "no-live-authorization-fields");
        Equal(new[] { "GetBuiltIn", "ValidateHardware" }, typeof(RawInputMapping).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(m => m.Name).OrderBy(s => s, StringComparer.Ordinal).ToArray(), "pure-public-surface");
        var untouched = new AxisMapping(" a ", null, "*", "../x"); Equal(new object[] { " a ", null, "*", "../x" }, Paths(untouched), "constructor-does-not-fix-invalid-values");
        var source = File.ReadAllText(sourcePath);
        foreach (string forbidden in new[] { "using Unity", "UnityEngine.", "LocalStore.", "ReadValue(", "ReadUnprocessedValue(", "File.", "Directory.", "InputSystem.", "SetMapping(" })
            Check(!source.Contains(forbidden), "static-source-scope-no-" + forbidden);
    }
    private static IEnumerable<int[]> Permutations(int[] values)
    {
        if (values.Length == 0) { yield return new int[0]; yield break; }
        for (int i = 0; i < values.Length; i++)
        {
            var rest = values.Where((v, n) => n != i).ToArray();
            foreach (var suffix in Permutations(rest)) yield return new[] { values[i] }.Concat(suffix).ToArray();
        }
    }
    private static void Boundaries()
    {
        foreach (var kind in new[] { FlightInputSourceKind.None, FlightInputSourceKind.Keyboard, FlightInputSourceKind.Touch, FlightInputSourceKind.Gamepad, FlightInputSourceKind.Joystick, (FlightInputSourceKind)999, (FlightInputSourceKind)(-1) })
        {
            if (kind != FlightInputSourceKind.Keyboard && kind != FlightInputSourceKind.Touch) Rejected(RawInputMapping.GetBuiltIn(kind), RawMappingError.UnsupportedBuiltInKind, -1, "", "kind", "kind-builtin-" + kind);
            if (kind != FlightInputSourceKind.Gamepad && kind != FlightInputSourceKind.Joystick) Rejected(RawInputMapping.ValidateHardware(kind, null, null, null), RawMappingError.UnsupportedHardwareKind, -1, "", "kind", "kind-hardware-" + kind);
        }
        foreach (string layout in new[] { null, "", " ", " QA", "QA ", "\u00a0QA", "QA\u2003", "Q\0A", "Q\nA", "\ud800", "\udc00" })
            Rejected(BaseResult(Map("layout", layout)), RawMappingError.InvalidLayout, -1, "", "layout", "layout-invalid-" + (layout ?? "null"));
        Check(BaseResult(Map("layout", "QA Internal Space")).IsValid, "layout-internal-space-allowed");
        var invalidPaths = new[] { null, "", "/a", "a/", "a//b", ".", "..", "a/./b", "a/../b", "a*b", "a?b", "a{b", "a}b", "a<b", "a>b", "a[b", "a]b", "a:b", "a\\b", "a b", "a\t", "a\n", "a\0", "a\u00a0b", "a\u2003b", "\ud800", "\udc00", "<Gamepad>/x", "/device/axis" };
        for (int axis = 0; axis < 4; axis++) foreach (string invalid in invalidPaths)
        {
            var paths = BasePaths.ToArray(); paths[axis] = invalid;
            Rejected(BaseResult(Map("mappingPaths", paths)), RawMappingError.InvalidMappingPath, axis, "", "path", "mapped-invalid-axis-" + axis);
            var catalog = BasePaths.ToArray(); catalog[axis] = invalid;
            Rejected(BaseResult(Map("catalogPaths", catalog)), RawMappingError.InvalidCatalogPath, -1, "", "path", "catalog-invalid-axis-" + axis);
        }
        foreach (string valid in new[] { "axis.v1/x", "é/y", "e\u0301/y", "😀/x", "a%2Fb", "a#b/x" })
        {
            var paths = BasePaths.ToArray(); paths[0] = valid; Check(BaseResult(Map("catalogPaths", paths, "mappingPaths", paths)).Status == RawMappingValidationStatus.JoystickCandidate, "literal-path-allowed-" + valid);
        }
        Check(BaseResult(Map("catalogPaths", new[] { "x", "X", "é", "e\u0301" }, "mappingPaths", new[] { "x", "X", "é", "e\u0301" })).IsValid, "ordinal-distinct-case-unicode-paths");
        Rejected(BaseResult(Map("catalogPaths", null)), RawMappingError.MissingCatalog, -1, "", "catalog", "catalog-null");
        var nullCatalog = Build(new Dictionary<string, object>()); nullCatalog.Catalog.Add(null);
        Rejected(Invoke(nullCatalog), RawMappingError.MissingControlDescriptor, -1, "", "catalog", "catalog-null-entry");
        foreach (var permutation in Permutations(new[] { 0, 1, 2, 3 }))
        {
            var f = Build(new Dictionary<string, object>()); f.Catalog = permutation.Select(i => f.Catalog[i]).ToList();
            Equal(Project(BaseResult()), Project(Invoke(f)), "catalog-permutation-" + string.Join("", permutation));
            f.Catalog.Add(new RawControlDescriptor("aux", RawControlValueKind.Other, null, 0, 0, null));
            f.Catalog.Add(new RawControlDescriptor("aux", RawControlValueKind.ButtonFloat, null, 0, 0, null));
            Rejected(Invoke(f), RawMappingError.DuplicateCatalogPath, -1, "aux", "catalog", "duplicate-permutation");
        }
        for (int first = 0; first < 4; first++) for (int second = first + 1; second < 4; second++)
        {
            var paths = BasePaths.ToArray(); paths[second] = paths[first]; var r = BaseResult(Map("mappingPaths", paths));
            Check(r.Status == RawMappingValidationStatus.Rejected && r.Diagnostics.Count == 2, "duplicate-pair-rejected");
            Equal(new[] { first, second }, r.Diagnostics.Select(d => d.AxisIndex).ToArray(), "duplicate-every-involved-axis");
            Check(r.Diagnostics.All(d => d.Error == RawMappingError.DuplicateMappedPath && d.Path == paths[first] && d.Field == "path"), "duplicate-pair-diagnostics");
        }
        foreach (string type in new[] { null, "", "QA Axis", "QA\tAxis", "QA,Assembly", "QA/Axis", "QA\\Axis", "QA=Axis", "QA:Axis", "QA\0Axis", "\ud800", "\udc00" })
            Rejected(BaseResult(ChangeMetadata(0, "controlTypeFullName", type)), RawMappingError.InvalidControlType, 0, BasePaths[0], "controlTypeFullName", "type-invalid");
        Rejected(BaseResult(ChangeMetadata(0, "stateFormatCode", 0)), RawMappingError.InvalidStateFormat, 0, BasePaths[0], "stateFormatCode", "format-zero");
        Rejected(BaseResult(ChangeMetadata(0, "sizeInBits", 0)), RawMappingError.InvalidStateSize, 0, BasePaths[0], "sizeInBits", "size-zero");
        Rejected(BaseResult(ChangeMetadata(0, "preprocess", null)), RawMappingError.MissingPreprocess, 0, BasePaths[0], "preprocess", "preprocess-null");
        Rejected(BaseResult(ChangePreprocess(0, "clamp", 999)), RawMappingError.UnsupportedClamp, 0, BasePaths[0], "clamp", "clamp-unknown");
        Rejected(BaseResult(ChangeMetadata(0, "valueKind", "ButtonFloat")), RawMappingError.ButtonAxisNotAllowed, 0, BasePaths[0], "valueKind", "valid-button-still-not-axis");
        Rejected(BaseResult(ChangeMetadata(0, "valueKind", "Other")), RawMappingError.AxisTypeMismatch, 0, BasePaths[0], "valueKind", "other-not-axis");
        var extra = Build(new Dictionary<string, object>()); var hash = Invoke(extra).MappingFingerprint;
        extra.Catalog.Add(new RawControlDescriptor("auxButton", RawControlValueKind.ButtonFloat, null, 0, 0, null));
        extra.Catalog.Add(new RawControlDescriptor("auxOther", RawControlValueKind.Other, null, 0, 0, null));
        Check(Invoke(extra).MappingFingerprint == hash, "unselected-button-other-irrelevant-metadata");
        for (int axis = 0; axis < 4; axis++) foreach (string field in FloatFields) foreach (string value in new[] { "NaN", "Infinity", "-Infinity" })
            Rejected(BaseResult(ChangePreprocess(axis, field, value)), RawMappingError.NonFinitePreprocess, axis, BasePaths[axis], field, "nonfinite-" + axis + "-" + field + "-" + value);
        foreach (RawAxisClamp clamp in Enum.GetValues(typeof(RawAxisClamp)))
        {
            Check(BaseResult(ChangePreprocess(0, "clamp", clamp.ToString(), "clampMin", .125f, "clampMax", .125f)).IsValid, "equal-clamp-range-" + clamp);
            var r = BaseResult(ChangePreprocess(0, "clamp", clamp.ToString(), "clampMin", 10f, "clampMax", -10f));
            if (clamp == RawAxisClamp.None) Check(r.IsValid, "disabled-clamp-inverted-finite-range");
            else Rejected(r, RawMappingError.InvalidClampRange, 0, BasePaths[0], "clampRange", "enabled-clamp-inverted-" + clamp);
        }
        foreach (float zero in new[] { -10f, -20f }) Check(BaseResult(ChangePreprocess(0, "normalize", true, "normalizeMin", -10f, "normalizeMax", 20f, "normalizeZero", zero)).IsValid, "normalize-zero-not-calibration-center-" + zero);
        foreach (var pair in new[] { new[] { -1f, 1f, 1f }, new[] { -1f, 1f, 2f }, new[] { 1f, 1f, 0f }, new[] { 2f, 1f, 0f } })
            Rejected(BaseResult(ChangePreprocess(0, "normalize", true, "normalizeMin", pair[0], "normalizeMax", pair[1], "normalizeZero", pair[2])), RawMappingError.InvalidNormalizeRange, 0, BasePaths[0], "normalizeRange", "normalize-invalid-range");
        Check(BaseResult(ChangePreprocess(0, "normalize", false, "normalizeMin", 10f, "normalizeMax", -10f, "normalizeZero", 99f)).IsValid, "normalize-disabled-finite-reversed-range");
        foreach (float scale in new[] { -1f, 0f, -0.125f }) Check(BaseResult(ChangePreprocess(0, "scale", true, "scaleFactor", scale)).IsValid, "negative-or-zero-scale-allowed");
        Check(BaseResult(ChangePreprocess(0, "normalize", true, "normalizeMin", -0.0000001f, "normalizeMax", 0.0000001f, "normalizeZero", 0f)).IsValid, "small-finite-range-no-calibration-threshold");
        Check(BaseResult(ChangeMetadata(0, "stateFormatCode", uint.MaxValue, "sizeInBits", uint.MaxValue)).IsValid, "uint32-full-format-size-not-float32-assumption");
        string original = BaseResult().MappingFingerprint;
        Check(BaseResult(Map("layout", "QA.Other")).MappingFingerprint != original, "hash-layout-sensitive");
        foreach (var change in new[] { ChangeMetadata(0, "controlTypeFullName", "QA.SubclassAxis"), ChangeMetadata(0, "stateFormatCode", 17), ChangeMetadata(0, "sizeInBits", 33) })
            Check(BaseResult(change).MappingFingerprint != original, "hash-selected-metadata-sensitive");
        foreach (var change in new[] { Map("clamp", "BeforeNormalize"), Map("clampMin", -2f), Map("clampMax", 2f), Map("clampConstant", .125f), Map("normalize", true), Map("normalizeMin", -2f), Map("normalizeMax", 2f), Map("normalizeZero", .125f), Map("invert", true), Map("scale", true), Map("scaleFactor", .125f) })
        for (int axis = 0; axis < 4; axis++)
        {
            var p = Copy(Obj(BaseMetadata["preprocess"])); Merge(p, change);
            var r = BaseResult(ChangeMetadata(axis, "preprocess", p));
            Check(r.IsValid && r.MappingFingerprint != original, "hash-all-preprocess-field-axis-" + axis + "-" + change.Keys.Single());
        }
        for (int axis = 0; axis < 4; axis++)
        {
            var paths = BasePaths.ToArray(); paths[axis] += "/unique";
            Check(BaseResult(Map("catalogPaths", paths, "mappingPaths", paths)).MappingFingerprint != original, "hash-every-axis-path-" + axis);
        }
        var immutable = Build(new Dictionary<string, object>()); var oldResult = Invoke(immutable);
        var retained = Project(oldResult); var descriptorCopies = oldResult.Controls.Select(DescriptorSnapshot).ToArray();
        immutable.Catalog[0] = Descriptor("replacement", BaseMetadata);
        Check(!Invoke(immutable).IsValid, "input-list-item-replacement-affects-only-new-call");
        Equal(retained, Project(oldResult), "old-result-unchanged-after-list-item-replacement");
        Equal(descriptorCopies, oldResult.Controls.Select(DescriptorSnapshot).ToArray(), "old-control-metadata-unchanged-after-list-item-replacement");
        ReadOnly(oldResult.Controls, "immutable-controls"); ReadOnly(BaseResult(Map("catalogPaths", null)).Diagnostics, "immutable-diagnostics");
    }

    public static int Main(string[] args)
    {
        if (args.Length != 2) { Console.Error.WriteLine("Usage: independent-driver <qa-directory> <compiled-mapping-source>"); return 2; }
        var vectors = Obj(Json.DeserializeObject(File.ReadAllText(Path.Combine(args[0], "independent-preencoded-vectors.json"))));
        var expected = Obj(Json.DeserializeObject(File.ReadAllText(Path.Combine(args[0], "independent-prepared-expectations.json"))));
        var fixtures = Obj(expected["fixtures"]); Defaults = Obj(fixtures["defaults"]); BaseMetadata = Obj(fixtures["baseMetadata"]);
        Test("API shape", () => ApiShape(args[1]));
        var initialCulture = CultureInfo.CurrentCulture; var initialUiCulture = CultureInfo.CurrentUICulture;
        foreach (string culture in new[] { "", "en-US", "fr-FR", "tr-TR", "ar-EG" })
        {
            CultureInfo.CurrentCulture = culture == "" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
            LiteralVectors(Arr(vectors["vectors"])); LiteralCases(Arr(expected["cases"])); CulturePasses++;
        }
        CultureInfo.CurrentCulture = initialCulture; CultureInfo.CurrentUICulture = initialUiCulture;
        Test("independent boundaries", Boundaries);
        Check(VectorIds.Count == 8, "all-eight-frozen-vectors-consumed"); Check(CaseIds.Count == 26, "all-twenty-six-frozen-cases-consumed");
        Check(ZeroSignChecks == 80, "negative-zero-sign-bit-checked-build-and-result-under-five-cultures");
        var summary = Map("status", Failures.Count == 0 ? "PURE_MAPPING_VALIDATION_PASSED" : "FAILED", "checks", Checks, "failures", Failures.Count,
            "preencodedVectorsConsumed", VectorIds.Count, "literalCasesConsumed", CaseIds.Count, "culturePasses", CulturePasses,
            "cultureNames", new[] { "invariant", "en-US", "fr-FR", "tr-TR", "ar-EG" }, "negativeZeroSignBitChecks", ZeroSignChecks,
            "failureExamples", Failures.Take(40).ToArray(), "liveRawOrUserValidated", "NOT_TESTED_NOT_AUTHORIZED");
        Console.WriteLine("INDEPENDENT_SUMMARY " + Json.Serialize(summary));
        return Failures.Count == 0 ? 0 : 1;
    }
}
