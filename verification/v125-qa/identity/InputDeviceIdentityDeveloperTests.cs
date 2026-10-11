using System;
using System.Globalization;
using System.Reflection;
using AeroSim.Input;

// All serial/description strings below are synthetic fixtures, not user/device data.
internal static class InputDeviceIdentityDeveloperTests
{
    private const string RunA = "0123456789abcdef0123456789abcdef";
    private const string RunB = "fedcba9876543210fedcba9876543210";
    private static int _checks, _failures;

    private static void Check(string name, bool condition)
    {
        _checks++;
        if (!condition) { _failures++; Console.WriteLine("FAIL " + name); }
    }

    private static RuntimeSourceHandle Handle(FlightInputSourceKind kind = FlightInputSourceKind.Joystick,
        string runId = RunA, int deviceId = 1, int generation = 1) =>
        new RuntimeSourceHandle(kind, runId, deviceId, generation);

    private static InputDeviceDescriptor Device(string serial = "SYNTH-A", string manufacturer = "Acme",
        string product = "Flight Stick", string interfaceName = "HID", string layout = "HIDStick",
        int? vid = 1234, int? pid = 5678, int? usagePage = 1, int? usage = 4,
        FlightInputSourceKind kind = FlightInputSourceKind.Joystick,
        string displayName = null, string version = null, string capabilities = null) =>
        new InputDeviceDescriptor(kind, interfaceName, manufacturer, product, layout,
            vid, pid, usagePage, usage, serial, displayName, version, capabilities);

    private static InputIdentityResult Resolve(InputDeviceDescriptor descriptor, int id = 1,
        int generation = 1, string run = RunA) =>
        InputDeviceIdentity.Resolve(descriptor, Handle(descriptor.Kind, run, id, generation));

    private static bool Hex(string value, string prefix)
    {
        if (value == null || value.Length != prefix.Length + 64 ||
            !value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        for (int index = prefix.Length; index < value.Length; index++)
            if (!((value[index] >= '0' && value[index] <= '9') ||
                (value[index] >= 'a' && value[index] <= 'f'))) return false;
        return true;
    }

    private static void Invalid(string name, InputIdentityResult result, InputIdentityError error)
    {
        Check(name + " error", !result.IsValid && result.Error == error);
        Check(name + " source-free", result.Kind == FlightInputSourceKind.None && result.SourceId == "" &&
            result.DeviceIdentity == "" && result.ModelKey == "" && result.SerialKey == "");
        Check(name + " no descriptor or confidence", result.IdentityDescriptor == null &&
            !result.HasModelEvidence && result.Confidence == IdentityConfidence.SessionOnly);
    }

    private static void Directed()
    {
        var descriptor = Device();
        var result = Resolve(descriptor);
        Check("ascii fixed model vector", result.ModelKey ==
            "model:5f9e93b7a88010de1be9083d40dd3f30bb92fed76a7a844e6b0c6d9458723990");
        Check("ascii fixed serial vector", result.SerialKey ==
            "serial:55284478cc03b315e91a50606c758495ca7458a9ad23b35fe531b4daa45e2c74");
        Check("serial-only SHA vector", result.IdentityDescriptor.SerialSha256 ==
            "77c22344cafd6732a28f742e491944e11fc235a10478fae8c268f87cc9a43a1e");
        Check("full serial key differs from serial-only hash", result.SerialKey.Substring(7) !=
            result.IdentityDescriptor.SerialSha256);
        Check("ascii source fixed invariant D10", result.SourceId ==
            "joystick:" + RunA + ":0000000001:0000000001");
        Check("strong exact device identity", result.IsValid && result.HasModelEvidence &&
            result.Confidence == IdentityConfidence.ReportedSerial &&
            result.ConfidenceCode == "reported_serial" && result.DeviceIdentity == result.SerialKey);
        Check("normalized descriptor exact fields", result.IdentityDescriptor.FormatVersion == "identity-v1" &&
            result.IdentityDescriptor.Kind == "joystick" && result.IdentityDescriptor.InterfaceName == "HID" &&
            result.IdentityDescriptor.Manufacturer == "Acme" && result.IdentityDescriptor.Product == "Flight Stick" &&
            result.IdentityDescriptor.Layout == "HIDStick" && result.IdentityDescriptor.HidVendorId == 1234 &&
            result.IdentityDescriptor.HidProductId == 5678 && result.IdentityDescriptor.HidUsagePage == 1 &&
            result.IdentityDescriptor.HidUsage == 4);

        var unicode = Device(" Ω:42 ", " Cafe\u0301 ", " 飞行🎮:1 ", " HID ", " 摇杆 ",
            null, 0, -12, int.MaxValue, FlightInputSourceKind.Gamepad);
        var unicodeResult = Resolve(unicode);
        Check("Unicode UTF8-byte model vector", unicodeResult.ModelKey ==
            "model:73f7ecd24a74393041e84937dbd2342df9382e5694a7d08920dee030fe64a237");
        Check("Unicode UTF8-byte serial vector", unicodeResult.SerialKey ==
            "serial:cd1c4e2bf0c05c0d50c343d427546c6e650e4648a236d47aa5496929d9d4b724");
        Check("Unicode serial SHA vector", unicodeResult.IdentityDescriptor.SerialSha256 ==
            "ff806af7199184abecf2a35328b3312bfa987c7ca67adc9302808d52f19dc115");
        Check("NFC canonical descriptor", unicodeResult.IdentityDescriptor.Manufacturer == "Café" &&
            unicodeResult.IdentityDescriptor.Product == "飞行🎮:1" && unicodeResult.IdentityDescriptor.Layout == "摇杆");
        var composed = Resolve(Device("Ω:42", "Café", "飞行🎮:1", "HID", "摇杆",
            null, 0, -12, int.MaxValue, FlightInputSourceKind.Gamepad));
        Check("NFC and trim preserve both exact keys", unicodeResult.ModelKey == composed.ModelKey &&
            unicodeResult.SerialKey == composed.SerialKey);

        var empty = Resolve(new InputDeviceDescriptor(FlightInputSourceKind.Keyboard));
        Check("empty/null fixed model vector", empty.ModelKey ==
            "model:9ca01cc7bfafd3f0e68b5f319a3731bf9dac971dee4465f0a55fbfb86b6820a0");
        Check("empty serial never becomes serial key", empty.SerialKey == "" &&
            empty.IdentityDescriptor.SerialSha256 == "");
        Check("empty descriptor session identity", empty.IsValid && empty.Confidence == IdentityConfidence.SessionOnly &&
            empty.ConfidenceCode == "session_only" && empty.DeviceIdentity == "session:" + empty.SourceId);
        var spaces = Resolve(new InputDeviceDescriptor(FlightInputSourceKind.Keyboard,
            " \t", "\r\n", "\u2003", " ", serial: "\u2003"));
        Check("all null and whitespace equivalent", spaces.ModelKey == empty.ModelKey && spaces.SerialKey == "");

        var modelOnly = Resolve(Device(null));
        Check("known model without serial remains session scoped", modelOnly.Confidence == IdentityConfidence.ModelOnly &&
            modelOnly.ConfidenceCode == "model_only" && modelOnly.DeviceIdentity == "session:" + modelOnly.SourceId);
        Check("serial does not change model key", modelOnly.ModelKey == result.ModelKey);
        var serialB = Resolve(Device("SYNTH-B"));
        Check("same model different serial identities isolated", serialB.ModelKey == result.ModelKey &&
            serialB.SerialKey != result.SerialKey && serialB.DeviceIdentity != result.DeviceIdentity);
        var reconnectStrong = Resolve(descriptor, 7, 6, RunB);
        Check("strong reported identity stable across runtime handle", reconnectStrong.ModelKey == result.ModelKey &&
            reconnectStrong.SerialKey == result.SerialKey && reconnectStrong.DeviceIdentity == result.DeviceIdentity &&
            reconnectStrong.SourceId != result.SourceId);
        var reconnectWeak = Resolve(Device(null), 7, 6, RunB);
        Check("weak identity changes across run/id/generation", reconnectWeak.ModelKey == modelOnly.ModelKey &&
            reconnectWeak.DeviceIdentity != modelOnly.DeviceIdentity);
        Check("weak repeated resolution never allocates fresh GUID", Resolve(Device(null)).DeviceIdentity == modelOnly.DeviceIdentity);
        Check("one-generation change changes weak source", Resolve(Device(null), 1, 2).DeviceIdentity != modelOnly.DeviceIdentity);
        Check("one-id change changes weak source", Resolve(Device(null), 2, 1).DeviceIdentity != modelOnly.DeviceIdentity);
        Check("one-run change changes weak source", Resolve(Device(null), 1, 1, RunB).DeviceIdentity != modelOnly.DeviceIdentity);
        var max = Resolve(descriptor, int.MaxValue, int.MaxValue);
        Check("positive Int32 maximum source invariant D10", max.SourceId.EndsWith(":2147483647:2147483647", StringComparison.Ordinal));

        var metadata = Resolve(Device(displayName: "Another visible name", version: "driver-99",
            capabilities: "{not parsed; malformed and irrelevant}"));
        Check("display/version/capabilities excluded", metadata.ModelKey == result.ModelKey &&
            metadata.SerialKey == result.SerialKey && metadata.DeviceIdentity == result.DeviceIdentity);
        Check("manufacturer ordinal casing changes model", Resolve(Device(manufacturer: "acme")).ModelKey != result.ModelKey);
        Check("serial ordinal casing changes serial only", Resolve(Device("synth-a")).ModelKey == result.ModelKey &&
            Resolve(Device("synth-a")).SerialKey != result.SerialKey);
        Check("layout change changes model and serial", Resolve(Device(layout: "HIDStickV2")).ModelKey != result.ModelKey &&
            Resolve(Device(layout: "HIDStickV2")).SerialKey != result.SerialKey);
        Check("absent numeric different from zero", Resolve(Device(vid: null)).ModelKey != Resolve(Device(vid: 0)).ModelKey);
        Check("nullable int decimal preserves minus and extreme", Resolve(Device(vid: -1)).ModelKey !=
            Resolve(Device(vid: 1)).ModelKey && Resolve(Device(vid: int.MinValue)).ModelKey != Resolve(Device(vid: int.MaxValue)).ModelKey);
        Check("embedded colon fields cannot cross boundaries", Resolve(Device(product: "a:b", layout: "c")).ModelKey !=
            Resolve(Device(product: "a", layout: "b:c")).ModelKey);
        Check("embedded colon serial stays a field", Resolve(Device("a:b")).SerialKey != Resolve(Device("a")).SerialKey);
        Check("zero and absent HID preserve exact fields", Resolve(Device(vid: null)).IdentityDescriptor.HidVendorId == null &&
            Resolve(Device(vid: 0)).IdentityDescriptor.HidVendorId == 0);

        foreach (var kind in new[] { FlightInputSourceKind.Keyboard, FlightInputSourceKind.Gamepad, FlightInputSourceKind.Joystick })
        {
            var source = Resolve(Device(kind: kind));
            string name = kind == FlightInputSourceKind.Keyboard ? "keyboard" :
                kind == FlightInputSourceKind.Gamepad ? "gamepad" : "joystick";
            Check("hardware kind source token " + name, source.SourceId.StartsWith(name + ":", StringComparison.Ordinal));
            Check("hardware kind model field " + name, source.IdentityDescriptor.Kind == name);
            Check("hardware kind success " + name, source.IsValid && Hex(source.ModelKey, "model:") && Hex(source.SerialKey, "serial:"));
        }
        Check("different hardware kind cannot share model", Resolve(Device(kind: FlightInputSourceKind.Keyboard)).ModelKey !=
            Resolve(Device(kind: FlightInputSourceKind.Gamepad)).ModelKey);

        // This single-item resolver intentionally cannot detect two simultaneous live
        // duplicates. A later registry must mark both ambiguous before any binding.
        var duplicateSerialDifferentHandle = Resolve(descriptor, 2);
        Check("single resolver is not live collision detector", duplicateSerialDifferentHandle.SerialKey == result.SerialKey &&
            duplicateSerialDifferentHandle.SourceId != result.SourceId &&
            duplicateSerialDifferentHandle.Confidence == IdentityConfidence.ReportedSerial);
    }

    private static void InvalidInputs()
    {
        Invalid("null descriptor", InputDeviceIdentity.Resolve(null, Handle()), InputIdentityError.MissingDescriptor);
        Invalid("null handle", InputDeviceIdentity.Resolve(Device(), null), InputIdentityError.MissingHandle);
        foreach (var kind in new[] { FlightInputSourceKind.None, FlightInputSourceKind.Touch,
            (FlightInputSourceKind)90, (FlightInputSourceKind)(-1) })
        {
            Invalid("bad descriptor kind " + (int)kind, InputDeviceIdentity.Resolve(Device(kind: kind), Handle()),
                InputIdentityError.UnsupportedKind);
            Invalid("bad handle kind " + (int)kind, InputDeviceIdentity.Resolve(Device(), Handle(kind)),
                InputIdentityError.UnsupportedKind);
        }
        Invalid("kind mismatch", InputDeviceIdentity.Resolve(Device(), Handle(FlightInputSourceKind.Gamepad)),
            InputIdentityError.KindMismatch);
        foreach (string run in new[] { null, "", " ", RunA.ToUpperInvariant(), RunA + "0", RunA.Substring(1),
            "g123456789abcdef0123456789abcdef", " " + RunA, RunA + "\n",
            "０123456789abcdef0123456789abcdef", "01234567-89ab-cdef-0123-456789abcdef" })
            Invalid("invalid run ordinal", InputDeviceIdentity.Resolve(Device(), Handle(runId: run)), InputIdentityError.InvalidRunId);
        foreach (int number in new[] { 0, -1, int.MinValue })
        {
            Invalid("invalid runtime ID", InputDeviceIdentity.Resolve(Device(), Handle(deviceId: number)),
                InputIdentityError.InvalidRuntimeDeviceId);
            Invalid("invalid generation", InputDeviceIdentity.Resolve(Device(), Handle(generation: number)),
                InputIdentityError.InvalidSourceGeneration);
        }
        var invalidUnicode = new[] { "\ud800", "\udfff", "a\ud800b", "\ud800\ud800", "\udfff\ud800" };
        foreach (string broken in invalidUnicode)
        {
            Invalid("invalid interface Unicode", Resolve(Device(interfaceName: broken)), InputIdentityError.InvalidDescriptorText);
            Invalid("invalid manufacturer Unicode", Resolve(Device(manufacturer: broken)), InputIdentityError.InvalidDescriptorText);
            Invalid("invalid product Unicode", Resolve(Device(product: broken)), InputIdentityError.InvalidDescriptorText);
            Invalid("invalid layout Unicode", Resolve(Device(layout: broken)), InputIdentityError.InvalidDescriptorText);
            Invalid("invalid serial Unicode", Resolve(Device(serial: broken)), InputIdentityError.InvalidDescriptorText);
        }
    }

    private static void EvidenceMatrix()
    {
        int vector = 0;
        foreach (bool hasInterface in new[] { false, true })
        foreach (bool hasLayout in new[] { false, true })
        foreach (bool hasProduct in new[] { false, true })
        foreach (int? vid in new int?[] { null, 0, 12 })
        foreach (int? pid in new int?[] { null, 0, 34 })
        foreach (bool hasSerial in new[] { false, true })
        {
            var descriptor = Device(hasSerial ? "MATRIX-SYNTHETIC" : null,
                product: hasProduct ? "Model" : null, interfaceName: hasInterface ? "HID" : null,
                layout: hasLayout ? "Layout" : null, vid: vid, pid: pid);
            var result = Resolve(descriptor);
            bool evidence = hasInterface && hasLayout && (hasProduct ||
                (vid.HasValue && vid.Value != 0 && pid.HasValue && pid.Value != 0));
            var expected = evidence ? (hasSerial ? IdentityConfidence.ReportedSerial : IdentityConfidence.ModelOnly)
                : IdentityConfidence.SessionOnly;
            Check("model evidence matrix " + vector, result.HasModelEvidence == evidence && descriptor.HasModelEvidence == evidence);
            Check("confidence evidence matrix " + vector, result.IsValid && result.Confidence == expected);
            Check("identity evidence matrix " + vector, result.DeviceIdentity ==
                (evidence && hasSerial ? result.SerialKey : "session:" + result.SourceId));
            Check("serial evidence matrix " + vector, hasSerial ? Hex(result.SerialKey, "serial:") : result.SerialKey == "");
            Check("source evidence matrix " + vector, result.SourceId == "joystick:" + RunA + ":0000000001:0000000001");
            vector++;
        }
        Check("matrix covers 144 descriptor combinations", vector == 144);
    }

    private static void PrivacyAndImmutability()
    {
        const string privateFixture = "PRIVATE-SYNTHETIC-SERIAL-DO-NOT-LOG-3917462208";
        var descriptor = Device(privateFixture);
        var result = Resolve(descriptor);
        foreach (object item in new object[] { descriptor, result, result.IdentityDescriptor, Handle() })
        {
            Type type = item.GetType();
            Check("immutable sealed DTO " + type.Name, type.IsSealed);
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Check("no writable DTO property " + type.Name + "." + property.Name, property.GetSetMethod(true) == null);
                if (property.PropertyType == typeof(string))
                    Check("no serial in DTO property " + type.Name + "." + property.Name,
                        (property.GetValue(item, null) as string ?? "").IndexOf(privateFixture, StringComparison.Ordinal) < 0);
            }
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                Check("readonly backing field " + type.Name + "." + field.Name, field.IsInitOnly);
                if (field.FieldType == typeof(string))
                    Check("no serial in backing field " + type.Name + "." + field.Name,
                        (field.GetValue(item) as string ?? "").IndexOf(privateFixture, StringComparison.Ordinal) < 0);
            }
            Check("DTO default string contains no serial " + type.Name, item.ToString().IndexOf(privateFixture, StringComparison.Ordinal) < 0);
        }
        Check("persistent descriptor omits runtime IDs", result.IdentityDescriptor.GetType().GetProperty("SourceId") == null &&
            result.IdentityDescriptor.GetType().GetProperty("RunId") == null &&
            result.IdentityDescriptor.GetType().GetProperty("RuntimeDeviceId") == null);
        Check("persistent descriptor omits ignored metadata", result.IdentityDescriptor.GetType().GetProperty("DisplayName") == null &&
            result.IdentityDescriptor.GetType().GetProperty("Version") == null &&
            result.IdentityDescriptor.GetType().GetProperty("CapabilitiesJson") == null);
        Check("no public raw serial field", descriptor.GetType().GetProperty("Serial") == null &&
            result.IdentityDescriptor.GetType().GetProperty("Serial") == null);
    }

    private static void CultureAndStress()
    {
        var original = CultureInfo.CurrentCulture;
        var originalUi = CultureInfo.CurrentUICulture;
        var baseline = Resolve(Device());
        try
        {
            foreach (string culture in new[] { "tr-TR", "ar-SA", "fr-FR", "ja-JP" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                var actual = Resolve(Device());
                Check("culture-invariant key/source " + culture, actual.ModelKey == baseline.ModelKey &&
                    actual.SerialKey == baseline.SerialKey && actual.SourceId == baseline.SourceId);
            }
        }
        finally { CultureInfo.CurrentCulture = original; CultureInfo.CurrentUICulture = originalUi; }

        for (int index = 1; index <= 1200; index++)
        {
            string marker = index.ToString(CultureInfo.InvariantCulture);
            string serial = index % 3 == 0 ? null : "SYNTHETIC-" + marker + "é:🎮";
            var device = Device(serial, "Café:" + marker, "Stick-" + marker);
            var otherCanonical = Device(serial == null ? null : " " + serial.Replace("é", "e\u0301") + " ",
                " Cafe\u0301:" + marker + " ", " Stick-" + marker + " ");
            var first = Resolve(device, index, index);
            var canonical = Resolve(otherCanonical, index, index);
            var runtimeChange = Resolve(device, index + 1, index + 1, RunB);
            Check("stress success " + index, first.IsValid && canonical.IsValid && runtimeChange.IsValid);
            Check("stress exact model encoding " + index, first.ModelKey == canonical.ModelKey && Hex(first.ModelKey, "model:"));
            Check("stress exact serial encoding " + index, first.SerialKey == canonical.SerialKey &&
                (serial == null ? first.SerialKey == "" : Hex(first.SerialKey, "serial:")));
            Check("stress canonical source " + index, first.SourceId == canonical.SourceId);
            Check("stress runtime does not change model/serial " + index,
                first.ModelKey == runtimeChange.ModelKey && first.SerialKey == runtimeChange.SerialKey);
            Check("stress runtime does change SourceId " + index, first.SourceId != runtimeChange.SourceId);
            Check("stress strong or weak isolation " + index, serial == null
                ? first.DeviceIdentity != runtimeChange.DeviceIdentity
                : first.DeviceIdentity == runtimeChange.DeviceIdentity);
            Check("stress stable repeated Resolve " + index, Resolve(device, index, index).DeviceIdentity == first.DeviceIdentity);
        }
    }

    private static int Main()
    {
        Directed(); InvalidInputs(); EvidenceMatrix(); PrivacyAndImmutability(); CultureAndStress();
        Console.WriteLine("InputDeviceIdentity developer pure C# checks: " + _checks + "; failures: " + _failures);
        Console.WriteLine("Scope: single descriptor only; no live duplicate registry, hardware, profile, Unity or three-device integration.");
        return _failures == 0 ? 0 : 1;
    }
}
