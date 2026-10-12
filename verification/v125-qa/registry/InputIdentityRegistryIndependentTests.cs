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

// Literal trajectory oracle prepared before reading the registry implementation.
// This is not a second registry algorithm. Both frozen JSON files are consumed.
internal static class InputIdentityRegistryIndependentTests
{
    private const string MainHash = "3fba38278ddaee8b6c8e21b75fb8ffa81586530bc48216b1c1e157e47513b4c1";
    private const string V3Hash = "9eaa91f9ff71eb57aa2320f57fd08b785349b3bd4791677aedf13d916c3858de";
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    private static Dictionary<string, object> templates;
    private static Dictionary<string, object> sourceDefinitions;
    private static int checks, failures, cases, applySteps, v3Cases, v3ApplySteps, boundaries, ctorCases;
    private static string context = "preflight";

    private sealed class Saved
    {
        internal LiveIdentitySnapshot Snapshot;
        internal string Before;
    }

    private static void Check(bool condition, string rule)
    {
        checks++;
        if (condition) return;
        failures++;
        if (failures <= 30) Console.Error.WriteLine("FAIL " + context + ":" + rule);
    }

    private static Dictionary<string, object> D(object value) => (Dictionary<string, object>)value;
    private static object[] A(object value) => (object[])value;
    private static string Text(Dictionary<string, object> data, string key, string fallback = null) =>
        data.TryGetValue(key, out var value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : fallback;
    private static long L(Dictionary<string, object> data, string key) => long.Parse(Text(data, key), CultureInfo.InvariantCulture);
    private static int N(Dictionary<string, object> data, string key) => Convert.ToInt32(data[key], CultureInfo.InvariantCulture);
    private static int? Optional(Dictionary<string, object> data, string key) =>
        data.TryGetValue(key, out var value) && value != null ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : (int?)null;
    private static T EnumValue<T>(string name) where T : struct =>
        name.StartsWith("#", StringComparison.Ordinal) ? (T)Enum.ToObject(typeof(T), int.Parse(name.Substring(1), CultureInfo.InvariantCulture)) : (T)Enum.Parse(typeof(T), name, false);

    private static string HashFile(string file)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-", "").ToLowerInvariant();
    }

    private static InputDeviceDescriptor Descriptor(string name)
    {
        if (name == null) return null;
        var d = D(templates[name]);
        return new InputDeviceDescriptor(EnumValue<FlightInputSourceKind>(Text(d, "kind")),
            Text(d, "interfaceName"), Text(d, "manufacturer"), Text(d, "product"), Text(d, "layout"),
            Optional(d, "hidVendorId"), Optional(d, "hidProductId"), Optional(d, "hidUsagePage"), Optional(d, "hidUsage"),
            Text(d, "serial"), Text(d, "displayName"), Text(d, "version"), Text(d, "capabilitiesJson"));
    }

    private static List<LiveIdentityObservation> Observations(object input)
    {
        if (input == null) return null;
        var result = new List<LiveIdentityObservation>();
        foreach (var row in A(input))
        {
            if (row == null) { result.Add(null); continue; }
            var d = D(row);
            result.Add(new LiveIdentityObservation(N(d, "runtimeDeviceId"), EnumValue<FlightInputSourceKind>(Text(d, "kind")),
                L(d, "instanceToken"), L(d, "invalidationRevision"), L(d, "usageRevision"),
                EnumValue<LiveIdentityState>(Text(d, "state")), Descriptor(Text(d, "descriptor"))));
        }
        return result;
    }

    private static string Fingerprint(LiveIdentitySnapshot snapshot) => Json.Serialize(snapshot);

    private static void CheckDescriptor(InputIdentityDescriptor actual, InputIdentityDescriptor expected)
    {
        Check(actual != null && expected != null, "descriptor.present");
        if (actual == null || expected == null) return;
        foreach (var property in typeof(InputIdentityDescriptor).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            Check(object.Equals(property.GetValue(actual, null), property.GetValue(expected, null)), "descriptor." + property.Name);
    }

    private static void CheckEntry(LiveIdentityEntry actual, Dictionary<string, object> expected)
    {
        var i = actual.Identity;
        Check(actual.SourceGeneration == N(expected, "sourceGeneration"), "entry.generation");
        Check(actual.InstanceToken == L(expected, "instanceToken"), "entry.object-token");
        Check(actual.InvalidationRevision == L(expected, "invalidationRevision"), "entry.invalidation-revision");
        Check(actual.UsageRevision == L(expected, "usageRevision"), "entry.usage-revision");
        Check(actual.IdentityEpoch == L(expected, "identityEpoch"), "entry.epoch");
        Check(i.SourceId == Text(expected, "sourceId"), "identity.source-id-exact");
        Check(i.Kind.ToString() == Text(expected, "kind"), "identity.kind");
        Check(i.Confidence.ToString() == Text(expected, "confidence"), "identity.confidence");
        Check(i.ConfidenceCode == Text(expected, "confidenceCode"), "identity.confidence-code");
        Check(i.DeviceIdentity == Text(expected, "deviceIdentity"), "identity.device-exact");
        Check(i.ModelKey == Text(expected, "modelKey"), "identity.model-exact");
        Check(i.SerialKey == Text(expected, "serialKey"), "identity.serial-exact");
        Check(i.HasModelEvidence == (bool)expected["hasModelEvidence"], "identity.model-evidence");
        Check(i.IsValid == (bool)expected["isValid"], "identity.valid");
        Check(i.Error.ToString() == Text(expected, "identityError"), "identity.error");
        // Existing frozen descriptor conversion is independent of the registry.
        CheckDescriptor(i.IdentityDescriptor, Descriptor(Text(expected, "descriptor")).IdentityDescriptor);
    }

    private static void CheckDelta(IReadOnlyList<string> actual, object expected, string rule)
    {
        var fixedValues = A(expected).Select(x => (string)x).ToArray();
        Check(actual.SequenceEqual(fixedValues), "delta." + rule + ".exact");
        Check(actual.SequenceEqual(actual.OrderBy(x => x, StringComparer.Ordinal).Distinct(StringComparer.Ordinal)), "delta." + rule + ".ordered-unique");
    }

    private static void ReadOnlyCollection(object value, string rule)
    {
        var list = value as IList;
        Check(list != null, "collection." + rule + ".inspectable");
        if (list == null) return;
        Check(list.IsReadOnly, "collection." + rule + ".read-only");
        bool blockedAdd = false;
        try { list.Add(null); } catch (NotSupportedException) { blockedAdd = true; }
        Check(blockedAdd, "collection." + rule + ".add-blocked");
        if (list.Count > 0)
        {
            bool blockedSet = false;
            try { list[0] = null; } catch (NotSupportedException) { blockedSet = true; }
            Check(blockedSet, "collection." + rule + ".set-blocked");
        }
    }

    private static LiveRegistryError? RequiredError(string id)
    {
        if (id.Contains("null_list")) return LiveRegistryError.MissingSnapshot;
        if (id.Contains("null_entry")) return LiveRegistryError.MissingObservation;
        if (id.Contains("duplicate_runtime")) return LiveRegistryError.DuplicateRuntimeDeviceId;
        if (id.Contains("duplicate_token")) return LiveRegistryError.DuplicateInstanceToken;
        if (id.Contains("runtime_zero") || id.Contains("runtime_negative")) return LiveRegistryError.InvalidRuntimeDeviceId;
        if (id.Contains("token_zero") || id.Contains("token_negative")) return LiveRegistryError.InvalidInstanceToken;
        if (id.Contains("invalidation_zero") || id.Contains("invalidation_negative")) return LiveRegistryError.InvalidInvalidationRevision;
        if (id.Contains("usage_negative")) return LiveRegistryError.InvalidUsageRevision;
        if (id.Contains("unknown_state")) return LiveRegistryError.UnsupportedState;
        if (id.Contains("unsupported_kind") || id.Contains("unsupported_touch")) return LiveRegistryError.UnsupportedKind;
        if (id.Contains("descriptor_")) return LiveRegistryError.IdentityResolutionFailed;
        if (id.Contains("token_ownership")) return LiveRegistryError.InstanceTokenOwnershipConflict;
        if (id.Contains("revision_tombstone") || id.Contains("usage_only_then_reject")) return LiveRegistryError.RevisionRegression;
        return null;
    }

    private static void CheckResult(InputIdentityRegistry registry, LiveRegistryApplyResult result,
        Dictionary<string, object> expected, string caseId)
    {
        Check(result.Status.ToString() == Text(expected, "status"), "result.status");
        Check(result.Snapshot.CatalogRevision == L(expected, "catalogRevision"), "snapshot.catalog-revision");
        Check(Fingerprint(registry.Current) == Fingerprint(result.Snapshot), "current.matches-result");
        var entries = A(expected["sources"]);
        Check(result.Snapshot.Sources.Count == entries.Length, "snapshot.source-count");
        for (int n = 0; n < Math.Min(entries.Length, result.Snapshot.Sources.Count); n++)
            CheckEntry(result.Snapshot.Sources[n], D(entries[n]));
        var ids = result.Snapshot.Sources.Select(x => x.Identity.SourceId);
        Check(ids.SequenceEqual(ids.OrderBy(x => x, StringComparer.Ordinal).Distinct(StringComparer.Ordinal)), "snapshot.ordered-unique");
        CheckDelta(result.RevokedSourceIds, expected["revokedSourceIds"], "revoked");
        CheckDelta(result.InvalidatedBindingSourceIds, expected["invalidatedBindingSourceIds"], "bindings");
        CheckDelta(result.MappingRevalidationRequiredSourceIds, expected["mappingRevalidationRequiredSourceIds"], "usage");
        string policy = Text(expected, "errorPolicy");
        if (policy == "none") Check(result.Errors.Count == 0, "errors.none");
        if (policy == "nonempty_fixed_codes") Check(result.Errors.Count > 0, "errors.nonempty");
        foreach (var error in result.Errors)
        {
            Check(Enum.IsDefined(typeof(LiveRegistryError), error.Error), "errors.fixed-registry-enum");
            Check(Enum.IsDefined(typeof(InputIdentityError), error.IdentityError), "errors.fixed-resolver-enum");
        }
        string semantic = Text(expected, "requiredErrorSemantic");
        if (semantic != null) Check(result.Errors.Any(x => x.Error.ToString() == semantic), "errors.required-semantic");
        var required = RequiredError(caseId);
        if (result.Status == LiveRegistryStatus.Rejected && required.HasValue)
            Check(result.Errors.Any(x => x.Error == required.Value), "errors.protocol-category");
        if (result.Status == LiveRegistryStatus.Rejected && caseId.Contains("descriptor_kind_mismatch"))
            Check(result.Errors.Any(x => x.IdentityError == InputIdentityError.KindMismatch), "errors.descriptor-kind-detail");
        if (result.Status == LiveRegistryStatus.Rejected && caseId.Contains("descriptor_invalid_unicode"))
            Check(result.Errors.Any(x => x.IdentityError == InputIdentityError.InvalidDescriptorText), "errors.unicode-detail");
        ReadOnlyCollection(result.Snapshot.Sources, "sources");
        ReadOnlyCollection(result.Errors, "errors");
        ReadOnlyCollection(result.RevokedSourceIds, "revoked");
        ReadOnlyCollection(result.InvalidatedBindingSourceIds, "bindings");
        ReadOnlyCollection(result.MappingRevalidationRequiredSourceIds, "usage");
        string projected = Json.Serialize(result);
        foreach (var privateValue in new[] { "Serial:A", "Serial:B", "Serial:X", "Keyboard:A", "SYNTHETIC DISPLAY NEVER LOG", "SYNTHETIC VERSION NEVER LOG", "{\"z\":2,\"a\":1}" })
            Check(!projected.Contains(privateValue), "privacy.dto-no-raw-input");
    }

    private static void ArrangeBoundary(InputIdentityRegistry registry, Dictionary<string, object> step)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        string counter = Text(step, "counter");
        long value = L(step, "value");
        if (counter == "CatalogRevision")
        {
            var seeded = new LiveIdentitySnapshot(value, registry.Current.Sources.ToList());
            typeof(InputIdentityRegistry).GetField("_current", flags).SetValue(registry, seeded);
        }
        else
        {
            var slots = (IDictionary)typeof(InputIdentityRegistry).GetField("_slots", flags).GetValue(registry);
            int id = N(D(sourceDefinitions[Text(step, "slot")]), "runtimeDeviceId");
            object slot = slots[id];
            if (counter == "LastGeneration") slot.GetType().GetField("LastGeneration", flags).SetValue(slot, checked((int)value));
            else if (counter == "IdentityEpoch")
            {
                var field = slot.GetType().GetField("ActiveEntry", flags);
                var e = (LiveIdentityEntry)field.GetValue(slot);
                // Replace private state only, preserving every old public DTO.
                field.SetValue(slot, new LiveIdentityEntry(e.Identity, e.SourceGeneration, e.InstanceToken,
                    e.InvalidationRevision, e.UsageRevision, value));
            }
            else throw new ArgumentException("Unsupported synthetic boundary.");
        }
        boundaries++;
    }

    private static void RunCase(Dictionary<string, object> data, bool isV3)
    {
        string caseId = Text(data, "id");
        context = caseId;
        cases++; if (isV3) v3Cases++;
        try
        {
            var registry = new InputIdentityRegistry(Text(data, "runId"));
            Check(registry.Current.CatalogRevision == 0 && registry.Current.Sources.Count == 0, "constructor.initial-empty");
            var saved = new List<Saved>();
            string priorBadInput = null, priorDiagnostics = null;
            int index = 0;
            foreach (var rawStep in A(data["steps"]))
            {
                var step = D(rawStep); context = caseId + "/" + index++;
                if (Text(step, "operation") == "arrangeBoundary") { ArrangeBoundary(registry, step); continue; }
                applySteps++; if (isV3) v3ApplySteps++;
                var input = Observations(step["observations"]);
                var previousOut = Console.Out; var previousError = Console.Error;
                var capturedOut = new StringWriter(CultureInfo.InvariantCulture);
                var capturedError = new StringWriter(CultureInfo.InvariantCulture);
                LiveRegistryApplyResult result;
                try
                {
                    Console.SetOut(capturedOut); Console.SetError(capturedError);
                    result = registry.ApplySnapshot(input);
                }
                finally { Console.SetOut(previousOut); Console.SetError(previousError); }
                string captured = capturedOut.ToString() + capturedError.ToString();
                foreach (var raw in new[] { "Serial:A", "Serial:B", "Serial:X", "SYNTHETIC DISPLAY NEVER LOG", "SYNTHETIC VERSION NEVER LOG" })
                    Check(!captured.Contains(raw), "privacy.diagnostics-no-raw-input");
                CheckResult(registry, result, D(step["expected"]), caseId);
                foreach (var old in saved) Check(Fingerprint(old.Snapshot) == old.Before, "snapshot.old-deeply-unchanged");
                string beforeListChange = Fingerprint(registry.Current);
                if (input != null) { input.Clear(); input.Add(null); }
                Check(Fingerprint(registry.Current) == beforeListChange, "input.after-apply-list-change-does-not-write-through");
                if (result.Status == LiveRegistryStatus.Rejected)
                {
                    string bad = Json.Serialize(step["observations"]), diagnostics = Json.Serialize(result.Errors);
                    if (bad == priorBadInput) Check(diagnostics == priorDiagnostics, "errors.repeated-bad-input-stable");
                    priorBadInput = bad; priorDiagnostics = diagnostics;
                }
                saved.Add(new Saved { Snapshot = registry.Current, Before = Fingerprint(registry.Current) });
            }
            if (data.ContainsKey("compareWeakIdentityMustDifferFrom"))
                Check(registry.Current.Sources[0].Identity.DeviceIdentity != Text(data, "compareWeakIdentityMustDifferFrom"), "new-run.weak-identity-isolated");
        }
        catch (Exception error) { Check(false, "unexpected-exception." + error.GetType().Name); }
    }

    private static void StructuralTypes()
    {
        context = "immutable-api";
        foreach (var type in new[] { typeof(LiveIdentityObservation), typeof(LiveIdentityEntry), typeof(LiveIdentitySnapshot),
            typeof(LiveRegistryApplyResult), typeof(LiveRegistryDiagnostic), typeof(InputIdentityResult), typeof(InputIdentityDescriptor) })
        {
            Check(type.IsSealed, "sealed." + type.Name);
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                Check(property.GetSetMethod(true) == null, "get-only." + type.Name + "." + property.Name);
        }
    }

    private static void InvalidConstructors(object rows)
    {
        context = "constructor-invalid-run"; string fixedMessage = null;
        foreach (var raw in A(rows))
        {
            ctorCases++; string run = raw as string; bool rejected = false;
            try { new InputIdentityRegistry(run); }
            catch (ArgumentException error)
            {
                rejected = true;
                if (fixedMessage == null) fixedMessage = error.Message;
                Check(error.Message == fixedMessage, "fixed-message");
                Check(string.IsNullOrEmpty(run) || !error.Message.Contains(run), "no-raw-run-in-message");
            }
            catch (Exception) { Check(false, "wrong-exception-type"); }
            Check(rejected, "argument-exception");
        }
    }

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2) throw new ArgumentException("Expected two frozen synthetic vector files.");
            Check(HashFile(args[0]) == MainHash, "main-vector-sha"); Check(HashFile(args[1]) == V3Hash, "v3-vector-sha");
            var main = D(Json.DeserializeObject(File.ReadAllText(args[0], Encoding.UTF8)));
            var v3 = D(Json.DeserializeObject(File.ReadAllText(args[1], Encoding.UTF8)));
            templates = D(main["descriptors"]); sourceDefinitions = D(main["sourceDefinitions"]);
            StructuralTypes(); InvalidConstructors(main["constructorInvalidRunIds"]);
            foreach (var row in A(main["cases"])) RunCase(D(row), false);
            foreach (var row in A(v3["cases"])) RunCase(D(row), true);
            context = "coverage";
            Check(cases == 53, "all-53-cases-consumed"); Check(applySteps == 220, "all-220-apply-steps-consumed");
            Check(v3Cases == 4 && v3ApplySteps == 21, "v3-actually-consumed");
            Check(boundaries == 7 && ctorCases == 9, "all-boundaries-and-constructor-cases-consumed");
        }
        catch (Exception error) { Check(false, "runner-exception." + error.GetType().Name); }
        Console.WriteLine("INDEPENDENT_REGISTRY_RESULT " + Json.Serialize(new Dictionary<string, object> {
            { "checks", checks }, { "failures", failures }, { "cases", cases }, { "applySteps", applySteps },
            { "v3Cases", v3Cases }, { "v3ApplySteps", v3ApplySteps }, { "boundaryArrangements", boundaries },
            { "constructorCases", ctorCases }, { "mainVectorsSha256", MainHash }, { "v3VectorsSha256", V3Hash },
            { "scope", "PURE_SYNTHETIC_IDENTITY_ONLY_NOT_GATE_A_OR_DEVICE_ACCEPTANCE" }
        }));
        return failures == 0 ? 0 : 1;
    }
}
