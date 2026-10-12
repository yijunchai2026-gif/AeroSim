using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AeroSim.Input;

// Independent contract tests. Fixed wire/SHA vectors were prepared before reading
// the production implementation. No Unity API, real device, profile or registry.
internal static class InputDeviceIdentityIndependentTests
{
    private const string RunA = "0123456789abcdef0123456789abcdef";
    private const string RunB = "fedcba9876543210fedcba9876543210";
    private const int Seed = 1251011;
    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private static int checks;
    private static int failures;
    private static int directedCases;
    private static int randomCases;
    private static int invalidCases;
    private static int cultureCases;

    private sealed class Data
    {
        internal FlightInputSourceKind Kind = FlightInputSourceKind.Joystick;
        internal string Interface = "HID";
        internal string Manufacturer = "Acme";
        internal string Product = "Flight Stick";
        internal string Layout = "Joystick";
        internal int? Vendor = 4660;
        internal int? ProductId = 22136;
        internal int? Page = 1;
        internal int? Usage = 4;
        internal string Serial = "Serial:A";

        internal Data Copy()
        {
            return new Data { Kind = Kind, Interface = Interface, Manufacturer = Manufacturer,
                Product = Product, Layout = Layout, Vendor = Vendor, ProductId = ProductId,
                Page = Page, Usage = Usage, Serial = Serial };
        }
    }

    private sealed class Expected
    {
        internal string[] Fields;
        internal string ModelKey;
        internal string SerialKey;
        internal string SerialDigest;
        internal bool Evidence;
        internal string Confidence;
        internal string Source;
        internal string Device;
    }

    private static void Check(bool condition, string label)
    {
        checks++;
        if (condition) return;
        failures++;
        // Never print raw descriptor or serial on failures.
        if (failures <= 25) Console.Error.WriteLine("FAIL: " + label);
    }

    private static string Normalize(string value)
    {
        return (value ?? string.Empty).Normalize(NormalizationForm.FormC).Trim();
    }

    private static string KindText(FlightInputSourceKind kind)
    {
        switch (kind)
        {
            case FlightInputSourceKind.Keyboard: return "keyboard";
            case FlightInputSourceKind.Joystick: return "joystick";
            case FlightInputSourceKind.Gamepad: return "gamepad";
            default: throw new ArgumentException("unsupported synthetic kind");
        }
    }

    private static string Number(int? value)
    {
        return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    // Byte-oriented reference, not a copy of the implementation's serializer.
    private static byte[] Wire(params string[] fields)
    {
        using (var buffer = new MemoryStream())
        {
            foreach (string field in fields)
            {
                byte[] bytes = StrictUtf8.GetBytes(field);
                byte[] prefix = Encoding.ASCII.GetBytes(bytes.LongLength.ToString(CultureInfo.InvariantCulture) + ":");
                buffer.Write(prefix, 0, prefix.Length);
                buffer.Write(bytes, 0, bytes.Length);
            }
            return buffer.ToArray();
        }
    }

    private static string Digest(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(bytes);
            var chars = new char[digest.Length * 2];
            const string hex = "0123456789abcdef";
            for (int i = 0; i < digest.Length; i++)
            {
                chars[i * 2] = hex[digest[i] >> 4];
                chars[i * 2 + 1] = hex[digest[i] & 15];
            }
            return new string(chars);
        }
    }

    private static Expected Oracle(Data data, string run, int id, int generation)
    {
        string serial = Normalize(data.Serial);
        string[] fields = { "identity-v1", KindText(data.Kind), Normalize(data.Interface),
            Normalize(data.Manufacturer), Normalize(data.Product), Normalize(data.Layout),
            Number(data.Vendor), Number(data.ProductId), Number(data.Page), Number(data.Usage) };
        bool evidence = fields[2].Length != 0 && fields[5].Length != 0 &&
            (fields[4].Length != 0 || (data.Vendor.HasValue && data.ProductId.HasValue &&
                data.Vendor.Value != 0 && data.ProductId.Value != 0));
        string model = "model:" + Digest(Wire(fields));
        string serialKey = string.Empty;
        string serialDigest = string.Empty;
        if (serial.Length != 0)
        {
            var extended = new string[fields.Length + 1];
            Array.Copy(fields, extended, fields.Length);
            extended[fields.Length] = serial;
            serialKey = "serial:" + Digest(Wire(extended));
            serialDigest = Digest(StrictUtf8.GetBytes(serial));
        }
        string source = fields[1] + ":" + run + ":" + id.ToString("D10", CultureInfo.InvariantCulture) +
            ":" + generation.ToString("D10", CultureInfo.InvariantCulture);
        string confidence = evidence ? (serial.Length != 0 ? "reported_serial" : "model_only") : "session_only";
        return new Expected { Fields = fields, ModelKey = model, SerialKey = serialKey,
            SerialDigest = serialDigest, Evidence = evidence, Confidence = confidence,
            Source = source, Device = confidence == "reported_serial" ? serialKey : "session:" + source };
    }

    private static InputDeviceDescriptor Descriptor(Data data, string display = null,
        string version = null, string capabilities = null)
    {
        return new InputDeviceDescriptor(data.Kind, data.Interface, data.Manufacturer, data.Product,
            data.Layout, data.Vendor, data.ProductId, data.Page, data.Usage, data.Serial,
            display, version, capabilities);
    }

    private static InputIdentityResult Resolve(Data data, string run = RunA, int id = 7, int generation = 1)
    {
        return InputDeviceIdentity.Resolve(Descriptor(data), new RuntimeSourceHandle(data.Kind, run, id, generation));
    }

    private static void Result(Data data, string label, string run = RunA, int id = 7, int generation = 1)
    {
        Expected expected = Oracle(data, run, id, generation);
        InputDeviceDescriptor descriptor = Descriptor(data);
        InputIdentityResult actual = InputDeviceIdentity.Resolve(descriptor,
            new RuntimeSourceHandle(data.Kind, run, id, generation));
        Check(actual != null, label + " nonnull result");
        if (actual == null) return;
        Check(actual.IsValid, label + " valid");
        Check(actual.Error == InputIdentityError.None, label + " no error");
        Check(actual.Kind == data.Kind, label + " enum kind");
        Check(actual.SourceId == expected.Source, label + " SourceId exact invariant session");
        Check(actual.HasModelEvidence == expected.Evidence, label + " model evidence truth table");
        Check(actual.ConfidenceCode == expected.Confidence, label + " confidence code");
        Check(actual.ModelKey == expected.ModelKey, label + " model UTF8 hash");
        Check(actual.SerialKey == expected.SerialKey, label + " serial complete descriptor hash");
        Check(actual.DeviceIdentity == expected.Device, label + " strong vs session identity");
        Check(actual.Confidence != IdentityConfidence.Ambiguous, label + " pure resolver not fake live collision result");
        IdentityConfidence confidence = expected.Confidence == "reported_serial" ? IdentityConfidence.ReportedSerial :
            expected.Confidence == "model_only" ? IdentityConfidence.ModelOnly : IdentityConfidence.SessionOnly;
        Check(actual.Confidence == confidence, label + " confidence enum");
        Check(descriptor.Error == InputIdentityError.None, label + " descriptor valid");
        Check(descriptor.HasModelEvidence == expected.Evidence, label + " descriptor evidence");
        Check(descriptor.ModelKey == expected.ModelKey, label + " descriptor model hash");
        Check(descriptor.SerialKey == expected.SerialKey, label + " descriptor serial hash");
        InputIdentityDescriptor persisted = actual.IdentityDescriptor;
        Check(persisted != null, label + " persisted descriptor present");
        if (persisted == null) return;
        Check(persisted.FormatVersion == expected.Fields[0], label + " format");
        Check(persisted.Kind == expected.Fields[1], label + " canonical kind");
        Check(persisted.InterfaceName == expected.Fields[2], label + " canonical interface");
        Check(persisted.Manufacturer == expected.Fields[3], label + " canonical manufacturer");
        Check(persisted.Product == expected.Fields[4], label + " canonical product");
        Check(persisted.Layout == expected.Fields[5], label + " canonical layout");
        Check(persisted.HidVendorId == data.Vendor, label + " nullable VID");
        Check(persisted.HidProductId == data.ProductId, label + " nullable PID");
        Check(persisted.HidUsagePage == data.Page, label + " nullable usage page");
        Check(persisted.HidUsage == data.Usage, label + " nullable usage");
        Check(persisted.SerialSha256 == expected.SerialDigest, label + " raw serial digest only");
        Check(IsImmutable(persisted.GetType()), label + " persisted DTO get-only");
        Check(IsImmutable(actual.GetType()), label + " result DTO get-only");
    }

    private static bool IsImmutable(Type type)
    {
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            if (property.CanWrite) return false;
        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            if (!field.IsInitOnly) return false;
        return true;
    }

    private static void Invalid(InputDeviceDescriptor descriptor, RuntimeSourceHandle handle,
        InputIdentityError expected, string label)
    {
        invalidCases++;
        InputIdentityResult result = InputDeviceIdentity.Resolve(descriptor, handle);
        Check(result != null, label + " invalid returns result rather than throwing");
        if (result == null) return;
        Check(!result.IsValid, label + " invalid flag");
        Check(result.Error == expected, label + " stable error code");
        Check(result.Kind == FlightInputSourceKind.None, label + " rejected kind neutral");
        Check(result.SourceId == string.Empty, label + " no usable source");
        Check(result.ModelKey == string.Empty, label + " no model key");
        Check(result.SerialKey == string.Empty, label + " no serial key");
        Check(result.DeviceIdentity == string.Empty, label + " no bindable identity");
        Check(result.IdentityDescriptor == null, label + " no persisted descriptor");
        Check(!result.HasModelEvidence, label + " no evidence");
        Check(result.Confidence == IdentityConfidence.SessionOnly, label + " rejected confidence enum");
        Check(result.ConfidenceCode == "session_only", label + " rejected confidence code");
    }

    private static void FixedVectors()
    {
        Data ascii = new Data();
        Data unicode = new Data { Kind = FlightInputSourceKind.Gamepad, Interface = "蓝牙", Manufacturer = "Café",
            Product = "🎮:座", Layout = "GámePad", Vendor = null, ProductId = null, Page = 1, Usage = 5, Serial = "序:列🎛" };
        Data empty = new Data { Kind = FlightInputSourceKind.Keyboard, Interface = null, Manufacturer = null,
            Product = null, Layout = "Keyboard", Vendor = null, ProductId = null, Page = null, Usage = null, Serial = null };
        Data signed = new Data { Interface = "HID", Manufacturer = null, Product = null, Layout = "Joystick",
            Vendor = -1, ProductId = -2, Page = -3, Usage = -4, Serial = "S" };
        Data[] vectors = { ascii, unicode, empty, signed };
        string[] modelHashes = { "89ce3642f4c8ca8f8664b0ed80c31a9e036cc881dc4f2822ddd24f591fbeed5f",
            "7077f0cd0172402640bc31170a2eb52d2fa89ec273879f3544ff6581d93ebc1f",
            "6857a938d81f8d909c785f051faf346bf4fdcad6ea69fb36a5f0742c882af942",
            "72cfa0966a586f6894112b5bb54b8c136b605670f51e6efdb1f536afe2bea048" };
        string[] serialHashes = { "14cadee8e2b28ca1271bc628538fb77e1e8c4acc7d7568d3d7efc0d2c93eba28",
            "d2cc060745b44b0d3249598dbf76d646d37cb783f72a08f108e13ce7e479207c", "",
            "ce128a51bc6341c9433c5dca16bd21209727056cd90d747cc151939e382aaeeb" };
        string[] serialDigests = { "ac7a7dbb90fd9b056ad3852108eb0c16b3788d3ccb71caa244f7a2ec72ab8847",
            "d438477d07103d554a5e3a525a081eb7d63c53ee891043d163082cb6fe480bb4", "",
            "8de0b3c47f112c59745f717a626932264c422a7563954872e237b223af4ad643" };
        for (int i = 0; i < vectors.Length; i++)
        {
            directedCases++;
            string label = "fixed vector " + i;
            Expected oracle = Oracle(vectors[i], RunA, 7, 1);
            Check(oracle.ModelKey == "model:" + modelHashes[i], label + " reference fixed model digest");
            Check(oracle.SerialKey == (serialHashes[i].Length == 0 ? "" : "serial:" + serialHashes[i]), label + " reference fixed serial digest");
            Check(oracle.SerialDigest == serialDigests[i], label + " reference fixed raw digest");
            InputIdentityResult actual = Resolve(vectors[i]);
            Check(actual.ModelKey == "model:" + modelHashes[i], label + " actual fixed model digest");
            Check(actual.SerialKey == oracle.SerialKey, label + " actual fixed complete serial hash");
            Result(vectors[i], label);
        }
    }

    private static void EvidenceTruthTable()
    {
        string[] texts = { null, "", "  \t\n", "HID" };
        int?[] numbers = { null, 0, 1, -1 };
        string[] serials = { null, " \t", "opaque:serial" };
        foreach (string iface in texts)
        foreach (string layout in texts)
        foreach (string product in texts)
        foreach (int? vendor in numbers)
        foreach (int? productId in numbers)
        foreach (string serial in serials)
        {
            directedCases++;
            var data = new Data { Interface = iface, Layout = layout, Product = product,
                Vendor = vendor, ProductId = productId, Serial = serial };
            Result(data, "evidence case " + directedCases);
        }
    }

    private static void RuntimeIsolation()
    {
        foreach (FlightInputSourceKind kind in new[] { FlightInputSourceKind.Keyboard, FlightInputSourceKind.Joystick, FlightInputSourceKind.Gamepad })
        foreach (string serial in new[] { "unique:serial", null })
        {
            Data data = new Data { Kind = kind, Serial = serial };
            InputIdentityResult baseline = Resolve(data);
            foreach (string run in new[] { RunA, RunB, "00000000000000000000000000000000", "ffffffffffffffffffffffffffffffff" })
            foreach (int id in new[] { 1, 2, int.MaxValue })
            foreach (int generation in new[] { 1, 2, int.MaxValue })
            {
                directedCases++;
                string label = "runtime isolation " + directedCases;
                InputIdentityResult actual = Resolve(data, run, id, generation);
                Result(data, label, run, id, generation);
                Check(actual.ModelKey == baseline.ModelKey, label + " runtime not persistent model");
                Check(actual.SerialKey == baseline.SerialKey, label + " runtime not persistent serial");
                Check(actual.DeviceIdentity == (serial == null ? "session:" + actual.SourceId : baseline.DeviceIdentity),
                    label + " weak isolated / strong reusable identity");
            }
        }
        var weak = new Data { Serial = null };
        InputIdentityResult a = Resolve(weak, RunA, 1, 1);
        InputIdentityResult b = Resolve(weak, RunA, 2, 1);
        Check(a.ModelKey == b.ModelKey, "same model without serial shares model key only");
        Check(a.DeviceIdentity != b.DeviceIdentity, "same model without serial cannot share calibration identity");
        Check(a.SourceId != b.SourceId, "same model without serial unique source");
        var strong = new Data { Serial = "same:serial" };
        InputIdentityResult c = Resolve(strong, RunA, 1, 1);
        InputIdentityResult d = Resolve(strong, RunA, 2, 1);
        Check(c.SerialKey == d.SerialKey, "same reported serial pure result remains same key");
        Check(c.Confidence == IdentityConfidence.ReportedSerial && d.Confidence == IdentityConfidence.ReportedSerial,
            "pure resolver does not claim live duplicate serial registry handling");
        Check(c.SourceId != d.SourceId, "even repeated serial must keep session source IDs separate");
    }

    private static void CanonicalProperties()
    {
        var normalized = new Data { Interface = "蓝牙", Manufacturer = "Café", Product = "🎮:座", Layout = "GámePad", Serial = "Sérial:🎛" };
        var alternate = normalized.Copy();
        alternate.Interface = " \t蓝牙\n";
        alternate.Manufacturer = "Cafe\u0301 ";
        alternate.Product = "\u00a0🎮:座\u00a0";
        alternate.Layout = " Ga\u0301mePad ";
        alternate.Serial = " Se\u0301rial:🎛 ";
        InputIdentityResult a = Resolve(normalized);
        InputIdentityResult b = Resolve(alternate);
        Result(alternate, "NFC and Unicode trim equivalence"); directedCases++;
        Check(a.ModelKey == b.ModelKey, "NFC/trim model equivalent");
        Check(a.SerialKey == b.SerialKey, "NFC/trim serial equivalent");
        foreach (string member in new[] { "Interface", "Manufacturer", "Product", "Layout", "Serial" })
        {
            Data changed = normalized.Copy();
            switch (member)
            {
                case "Interface": changed.Interface = "蓝牙a"; break;
                case "Manufacturer": changed.Manufacturer = "CAFÉ"; break;
                case "Product": changed.Product = "🎮:座a"; break;
                case "Layout": changed.Layout = "gámePad"; break;
                case "Serial": changed.Serial = "sérial:🎛"; break;
            }
            directedCases++;
            Result(changed, "case sensitivity " + member);
            InputIdentityResult changedResult = Resolve(changed);
            Check(changedResult.SerialKey != a.SerialKey, "changed canonical " + member + " changes complete serial hash");
            Check((changedResult.ModelKey == a.ModelKey) == (member == "Serial"), "serial alone excluded from model hash " + member);
        }
        var absent = new Data { Vendor = null, ProductId = null, Page = null, Usage = null };
        var zero = new Data { Vendor = 0, ProductId = 0, Page = 0, Usage = 0 };
        Check(Resolve(absent).ModelKey != Resolve(zero).ModelKey, "absent is not numeric zero");
        var boundaryA = new Data { Manufacturer = "ab", Product = "c" };
        var boundaryB = new Data { Manufacturer = "a", Product = "bc" };
        Check(Resolve(boundaryA).ModelKey != Resolve(boundaryB).ModelKey, "length prefix stops adjacent field concatenation collision");
        var colon = new Data { Manufacturer = "2:A:1:B", Product = "p:0:", Serial = "s:12:x" };
        Result(colon, "colon is literal field bytes"); directedCases++;
        var nul = new Data { Manufacturer = "A\0B", Serial = "S\0T" };
        Result(nul, "embedded nul is length-prefixed literal"); directedCases++;
        InputDeviceDescriptor original = Descriptor(normalized);
        InputDeviceDescriptor decorated = Descriptor(normalized, "different display", "999.5", "{not parsed capabilities order}");
        Check(original.ModelKey == decorated.ModelKey, "display/version/capabilities absent from model identity");
        Check(original.SerialKey == decorated.SerialKey, "display/version/capabilities absent from serial identity");
        const string privateSyntheticSerial = "private-synthetic-serial-not-a-real-device:😺";
        Data privacy = new Data { Serial = privateSyntheticSerial };
        InputDeviceDescriptor privacyDescriptor = Descriptor(privacy);
        InputIdentityResult privacyResult = Resolve(privacy);
        Check(!RetainsRawString(privacyDescriptor, privateSyntheticSerial), "descriptor fields do not retain raw serial");
        Check(!RetainsRawString(privacyResult, privateSyntheticSerial), "result fields do not retain raw serial");
        Check(!RetainsRawString(privacyResult.IdentityDescriptor, privateSyntheticSerial), "persisted descriptor fields do not retain raw serial");
    }

    private static bool RetainsRawString(object value, string forbidden)
    {
        if (value == null) return false;
        foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            object fieldValue = field.GetValue(value);
            string text = fieldValue as string;
            if (text != null && text.Contains(forbidden)) return true;
            if (fieldValue is InputIdentityDescriptor && RetainsRawString(fieldValue, forbidden)) return true;
        }
        foreach (PropertyInfo property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length != 0) continue;
            string text = property.GetValue(value, null) as string;
            if (text != null && text.Contains(forbidden)) return true;
        }
        return false;
    }

    private static void InvalidInputs()
    {
        var data = new Data();
        var validDescriptor = Descriptor(data);
        var validHandle = new RuntimeSourceHandle(data.Kind, RunA, 7, 1);
        Invalid(null, validHandle, InputIdentityError.MissingDescriptor, "null descriptor");
        Invalid(validDescriptor, null, InputIdentityError.MissingHandle, "null handle");
        Invalid(validDescriptor, new RuntimeSourceHandle(FlightInputSourceKind.Gamepad, RunA, 7, 1), InputIdentityError.KindMismatch, "kind mismatch");
        foreach (FlightInputSourceKind kind in new[] { FlightInputSourceKind.None, FlightInputSourceKind.Touch, (FlightInputSourceKind)(-1), (FlightInputSourceKind)999 })
        {
            var invalid = data.Copy(); invalid.Kind = kind;
            Invalid(Descriptor(invalid), new RuntimeSourceHandle(kind, RunA, 7, 1), InputIdentityError.UnsupportedKind, "unsupported kind " + (int)kind);
        }
        foreach (string run in new[] { null, "", "123", "0123456789ABCDEF0123456789ABCDEF", "0123456789abcdef0123456789abcdeg", " " + RunA, RunA + " ", RunA + "0" })
            Invalid(validDescriptor, new RuntimeSourceHandle(data.Kind, run, 7, 1), InputIdentityError.InvalidRunId, "invalid run index " + invalidCases);
        foreach (int id in new[] { 0, -1, int.MinValue })
            Invalid(validDescriptor, new RuntimeSourceHandle(data.Kind, RunA, id, 1), InputIdentityError.InvalidRuntimeDeviceId, "invalid device ID " + id);
        foreach (int generation in new[] { 0, -1, int.MinValue })
            Invalid(validDescriptor, new RuntimeSourceHandle(data.Kind, RunA, 7, generation), InputIdentityError.InvalidSourceGeneration, "invalid generation " + generation);
        foreach (string text in new[] { "\ud800", "\udfff", "A\ud800B", "\udc00\ud800", "valid\ud800" })
        foreach (string member in new[] { "Interface", "Manufacturer", "Product", "Layout", "Serial" })
        {
            var broken = data.Copy();
            switch (member)
            {
                case "Interface": broken.Interface = text; break;
                case "Manufacturer": broken.Manufacturer = text; break;
                case "Product": broken.Product = text; break;
                case "Layout": broken.Layout = text; break;
                case "Serial": broken.Serial = text; break;
            }
            InputDeviceDescriptor descriptor = Descriptor(broken);
            Check(descriptor.Error == InputIdentityError.InvalidDescriptorText, "malformed text descriptor error " + member);
            Invalid(descriptor, validHandle, InputIdentityError.InvalidDescriptorText, "malformed text " + member + " case " + invalidCases);
        }
    }

    private static string RandomText(Random random)
    {
        string[] items = { null, "", "  ", " A ", "a", "A", "Café", "Cafe\u0301", "\tBlue:1:2\n", "🎮", "蓝牙", "é:🎛:座", "\u00a0abc\u00a0", "A\0B", "Å", "A\u030a" };
        return items[random.Next(items.Length)];
    }

    private static int? RandomNumber(Random random)
    {
        int?[] items = { null, 0, 1, -1, int.MinValue, int.MaxValue, 4660, 65535 };
        return items[random.Next(items.Length)];
    }

    private static void RandomProperties()
    {
        var random = new Random(Seed);
        FlightInputSourceKind[] kinds = { FlightInputSourceKind.Keyboard, FlightInputSourceKind.Joystick, FlightInputSourceKind.Gamepad };
        for (int i = 0; i < 6000; i++)
        {
            randomCases++;
            var data = new Data { Kind = kinds[random.Next(kinds.Length)], Interface = RandomText(random),
                Manufacturer = RandomText(random), Product = RandomText(random), Layout = RandomText(random),
                Serial = RandomText(random), Vendor = RandomNumber(random), ProductId = RandomNumber(random),
                Page = RandomNumber(random), Usage = RandomNumber(random) };
            int id = random.Next(1, int.MaxValue);
            int generation = random.Next(1, int.MaxValue);
            string run = (i & 1) == 0 ? RunA : RunB;
            string label = "random " + i;
            Result(data, label, run, id, generation);
            InputIdentityResult first = Resolve(data, run, id, generation);
            InputIdentityResult next = Resolve(data, run == RunA ? RunB : RunA, id, generation);
            Check(first.ModelKey == next.ModelKey && first.SerialKey == next.SerialKey, label + " run independent persistent hashes");
            Check(first.SourceId != next.SourceId, label + " restart source changes");
            Check((first.DeviceIdentity == next.DeviceIdentity) == (first.Confidence == IdentityConfidence.ReportedSerial),
                label + " restart strong stable / all weak invalidated");
            InputIdentityResult repeat = Resolve(data, run, id, generation);
            Check(first.SourceId == repeat.SourceId && first.DeviceIdentity == repeat.DeviceIdentity, label + " pure repeat stable");
            Data normalized = data.Copy(); normalized.Interface = Normalize(data.Interface);
            normalized.Manufacturer = Normalize(data.Manufacturer); normalized.Product = Normalize(data.Product);
            normalized.Layout = Normalize(data.Layout); normalized.Serial = Normalize(data.Serial);
            InputIdentityResult norm = Resolve(normalized, run, id, generation);
            Check(first.ModelKey == norm.ModelKey && first.SerialKey == norm.SerialKey, label + " normalize idempotence");
        }
    }

    private static void CultureInvariance()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        CultureInfo originalUi = CultureInfo.CurrentUICulture;
        try
        {
            foreach (string name in new[] { "en-US", "tr-TR", "ar-SA", "fr-FR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
                cultureCases++;
                Data data = new Data { Manufacturer = "I:İ:i:ı", Product = "Stick", Vendor = int.MinValue, ProductId = int.MaxValue, Page = -35, Usage = 10000 };
                Result(data, "culture " + name, RunA, int.MaxValue, int.MaxValue);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
            CultureInfo.CurrentUICulture = originalUi;
        }
    }

    private static int Main()
    {
        try
        {
            FixedVectors(); EvidenceTruthTable(); RuntimeIsolation(); CanonicalProperties();
            InvalidInputs(); RandomProperties(); CultureInvariance();
        }
        catch (Exception error)
        {
            failures++;
            // Exception message might contain input: retain type only, never raw serial.
            Console.Error.WriteLine("UNEXPECTED_EXCEPTION_TYPE: " + error.GetType().FullName);
        }
        Console.WriteLine("Identity independent QA: checks=" + checks + " failures=" + failures +
            " directedCases=" + directedCases + " randomCases=" + randomCases + " invalidCases=" + invalidCases +
            " cultureCases=" + cultureCases + " seed=" + Seed);
        return failures == 0 ? 0 : 1;
    }
}
