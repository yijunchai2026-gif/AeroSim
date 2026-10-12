using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using AeroSim.Input;

// Synthetic descriptor fixtures only. No Unity, device events, user data or profile IO.
internal static class InputIdentityRegistryDeveloperTests
{
    private const string Run = "0123456789abcdef0123456789abcdef";
    private static int _checks, _failures;
    private static void Check(string name, bool condition)
    { _checks++; if (!condition) { _failures++; Console.WriteLine("FAIL " + name); } }
    private static InputDeviceDescriptor Device(string serial = "SYNTH-A", string layout = "Stick",
        FlightInputSourceKind kind = FlightInputSourceKind.Joystick, bool evidence = true,
        string display = null, string version = null, string capabilities = null) =>
        new InputDeviceDescriptor(kind, evidence ? "HID" : null, "Synthetic", "Test Device", layout,
            serial: serial, displayName: display, version: version, capabilitiesJson: capabilities);
    private static LiveIdentityObservation Obs(int id = 1, long token = 101, long invalidation = 1,
        long usage = 0, LiveIdentityState state = LiveIdentityState.Active, InputDeviceDescriptor descriptor = null,
        FlightInputSourceKind kind = FlightInputSourceKind.Joystick) =>
        new LiveIdentityObservation(id, kind, token, invalidation, usage, state, descriptor ?? Device(kind: kind));
    private static string Source(int id, int generation = 1, FlightInputSourceKind kind = FlightInputSourceKind.Joystick) =>
        (kind == FlightInputSourceKind.Keyboard ? "keyboard" : kind == FlightInputSourceKind.Gamepad ? "gamepad" : "joystick") +
        ":" + Run + ":" + id.ToString("D10", CultureInfo.InvariantCulture) + ":" + generation.ToString("D10", CultureInfo.InvariantCulture);
    private static LiveIdentityEntry Entry(LiveIdentitySnapshot snapshot, int id, int generation = 1,
        FlightInputSourceKind kind = FlightInputSourceKind.Joystick)
    {
        foreach (var entry in snapshot.Sources) if (entry.Identity.SourceId == Source(id, generation, kind)) return entry;
        return null;
    }
    private static bool Contains(IReadOnlyList<string> ids, string source)
    { foreach (var id in ids) if (id == source) return true; return false; }
    private static bool Error(LiveRegistryApplyResult result, LiveRegistryError error)
    { foreach (var item in result.Errors) if (item.Error == error) return true; return false; }
    private static void Stable(string label, LiveRegistryApplyResult result, long catalog, int sourceCount)
    {
        Check(label + " applied", result.Status == LiveRegistryStatus.Applied && result.Errors.Count == 0);
        Check(label + " snapshot", result.Snapshot.CatalogRevision == catalog && result.Snapshot.Sources.Count == sourceCount);
        Check(label + " no repeated deltas", result.RevokedSourceIds.Count == 0 && result.InvalidatedBindingSourceIds.Count == 0 &&
            result.MappingRevalidationRequiredSourceIds.Count == 0);
    }
    private static string Fingerprint(LiveRegistryApplyResult result)
    {
        var text = new StringBuilder(); text.Append(result.Status).Append('/').Append(result.Snapshot.CatalogRevision);
        foreach (var entry in result.Snapshot.Sources)
            text.Append('|').Append(entry.Identity.SourceId).Append('/').Append(entry.Identity.Confidence).Append('/')
                .Append(entry.Identity.ModelKey).Append('/').Append(entry.Identity.SerialKey).Append('/').Append(entry.Identity.DeviceIdentity)
                .Append('/').Append(entry.SourceGeneration).Append('/').Append(entry.InstanceToken).Append('/').Append(entry.InvalidationRevision)
                .Append('/').Append(entry.UsageRevision).Append('/').Append(entry.IdentityEpoch);
        foreach (var value in result.RevokedSourceIds) text.Append("|R:").Append(value);
        foreach (var value in result.InvalidatedBindingSourceIds) text.Append("|B:").Append(value);
        foreach (var value in result.MappingRevalidationRequiredSourceIds) text.Append("|M:").Append(value);
        foreach (var value in result.Errors) text.Append("|E:").Append(value.RuntimeDeviceId).Append('/').Append(value.Error).Append('/').Append(value.IdentityError);
        return text.ToString();
    }

    private static void Lifecycle()
    {
        foreach (var inactive in new[] { LiveIdentityState.Disabled, LiveIdentityState.Disconnected, LiveIdentityState.Removed,
            LiveIdentityState.RemoteExcluded, LiveIdentityState.PlatformUnsupported })
        {
            var registry = new InputIdentityRegistry(Run);
            Check("initial zero", registry.Current.CatalogRevision == 0 && registry.Current.Sources.Count == 0);
            var input = new List<LiveIdentityObservation> { Obs(descriptor: Device(null)) };
            var first = registry.ApplySnapshot(input);
            Check("first generation one", Entry(first.Snapshot, 1).SourceGeneration == 1 && Entry(first.Snapshot, 1).IdentityEpoch == 1);
            Check("first exact resolver identity", Entry(first.Snapshot, 1).Identity.DeviceIdentity == "session:" + Source(1));
            Check("current snapshot same returned", ReferenceEquals(registry.Current, first.Snapshot));
            input.Clear();
            Check("caller mutation cannot clear snapshot", first.Snapshot.Sources.Count == 1);
            Stable("same active", registry.ApplySnapshot(new[] { Obs(descriptor: Device(null)) }), 1, 1);
            var remove = registry.ApplySnapshot(new[] { Obs(state: inactive, descriptor: Device(null)) });
            Check("inactive revokes once", remove.Snapshot.Sources.Count == 0 && remove.Snapshot.CatalogRevision == 2 &&
                remove.RevokedSourceIds.Count == 1 && Contains(remove.InvalidatedBindingSourceIds, Source(1)));
            Stable("duplicate inactive", registry.ApplySnapshot(new[] { Obs(state: inactive, descriptor: Device(null)) }), 2, 0);
            Stable("omitted duplicate inactive", registry.ApplySnapshot(new LiveIdentityObservation[0]), 2, 0);
            var reconnect = registry.ApplySnapshot(new[] { Obs(descriptor: Device(null)) });
            Check("reconnect new weak generation", Entry(reconnect.Snapshot, 1, 2).Identity.DeviceIdentity == "session:" + Source(1, 2));
            Check("old immutable snapshot remains gen one", first.Snapshot.Sources[0].Identity.SourceId == Source(1));
            var config = registry.ApplySnapshot(new[] { Obs(invalidation: 2, descriptor: Device(null)) });
            Check("configuration barrier gen three", Entry(config.Snapshot, 1, 3) != null && Contains(config.RevokedSourceIds, Source(1, 2)));
            Stable("same reset stage", registry.ApplySnapshot(new[] { Obs(invalidation: 2, descriptor: Device(null)) }), 4, 1);
            var usage = registry.ApplySnapshot(new[] { Obs(invalidation: 2, usage: 1, descriptor: Device(null)) });
            Check("usage retains source and epoch", Entry(usage.Snapshot, 1, 3).IdentityEpoch == 1 && usage.RevokedSourceIds.Count == 0);
            Check("usage only delta", usage.MappingRevalidationRequiredSourceIds.Count == 1 && Contains(usage.MappingRevalidationRequiredSourceIds, Source(1, 3)));
            Stable("same usage stage", registry.ApplySnapshot(new[] { Obs(invalidation: 2, usage: 1, descriptor: Device(null)) }), 5, 1);
            var configUsage = registry.ApplySnapshot(new[] { Obs(invalidation: 3, usage: 2, descriptor: Device(null)) });
            Check("root addendum new generation no usage delta", Entry(configUsage.Snapshot, 1, 4) != null &&
                configUsage.MappingRevalidationRequiredSourceIds.Count == 0 && Contains(configUsage.RevokedSourceIds, Source(1, 3)));
            var replacement = registry.ApplySnapshot(new[] { Obs(token: 102, descriptor: Device(null)) });
            Check("new object token revokes gen four", Entry(replacement.Snapshot, 1, 5) != null && Contains(replacement.RevokedSourceIds, Source(1, 4)));
            var layout = registry.ApplySnapshot(new[] { Obs(token: 102, descriptor: Device(null, "StickV2")) });
            Check("unannounced layout change gen six", Entry(layout.Snapshot, 1, 6) != null && Contains(layout.RevokedSourceIds, Source(1, 5)));
            var metadata = registry.ApplySnapshot(new[] { Obs(token: 102, descriptor: Device(null, "StickV2", display: "Not Identity",
                version: "99", capabilities: "{not identity}") ) });
            Stable("ignored metadata unchanged", metadata, 8, 1);
            var bad = registry.ApplySnapshot(null);
            Check("null snapshot revokes without stale output", bad.Status == LiveRegistryStatus.Rejected && bad.Snapshot.Sources.Count == 0 &&
                Contains(bad.RevokedSourceIds, Source(1, 6)) && bad.Snapshot.CatalogRevision == 9);
            var repeat = registry.ApplySnapshot(null);
            Check("repeat rejection no generation catalog churn", repeat.Snapshot.CatalogRevision == 9 && repeat.RevokedSourceIds.Count == 0);
            var recovery = registry.ApplySnapshot(new[] { Obs(token: 102, descriptor: Device(null, "StickV2")) });
            Check("recovery new generation seven", Entry(recovery.Snapshot, 1, 7) != null && recovery.Snapshot.CatalogRevision == 10);
        }
        var empty = new InputIdentityRegistry(Run);
        empty.ApplySnapshot(new[] { Obs(state: LiveIdentityState.Disabled) });
        Stable("inactive-only history no catalog", empty.ApplySnapshot(new[] { Obs(invalidation: 2, usage: 4, state: LiveIdentityState.Disabled) }), 0, 0);
        var firstActive = empty.ApplySnapshot(new[] { Obs(invalidation: 2, usage: 4) });
        Check("first active after inactive still generation one", Entry(firstActive.Snapshot, 1) != null && firstActive.Snapshot.CatalogRevision == 1);
    }

    private static void Collisions()
    {
        var registry = new InputIdentityRegistry(Run); var a = Obs(); var b = Obs(2, 202); var c = Obs(3, 303);
        var unique = registry.ApplySnapshot(new[] { a });
        Check("unique strong exact key", unique.Snapshot.Sources[0].Identity.DeviceIdentity == Device().SerialKey && unique.Snapshot.Sources[0].IdentityEpoch == 1);
        var pair = registry.ApplySnapshot(new[] { b, a });
        Check("both ambiguous no first winner", pair.Snapshot.Sources.Count == 2 && Entry(pair.Snapshot, 1).Identity.Confidence == IdentityConfidence.Ambiguous &&
            Entry(pair.Snapshot, 2).Identity.Confidence == IdentityConfidence.Ambiguous);
        Check("entry epoch and new initial epoch", Entry(pair.Snapshot, 1).IdentityEpoch == 2 && Entry(pair.Snapshot, 2).IdentityEpoch == 1);
        Check("pair binding invalidates both without revoke", pair.InvalidatedBindingSourceIds.Count == 2 && pair.RevokedSourceIds.Count == 0);
        Check("ambiguous valid retains key but session scoped", Entry(pair.Snapshot, 1).Identity.IsValid && Entry(pair.Snapshot, 1).Identity.SerialKey == Device().SerialKey &&
            Entry(pair.Snapshot, 1).Identity.DeviceIdentity == "session:" + Source(1) && Entry(pair.Snapshot, 2).Identity.DeviceIdentity == "session:" + Source(2));
        Stable("pair reversed no deltas", registry.ApplySnapshot(new[] { a, b }), 2, 2);
        var triple = registry.ApplySnapshot(new[] { c, b, a });
        Check("third addition no repeat pair invalidation", triple.InvalidatedBindingSourceIds.Count == 1 && Contains(triple.InvalidatedBindingSourceIds, Source(3)) &&
            Entry(triple.Snapshot, 1).IdentityEpoch == 2 && Entry(triple.Snapshot, 2).IdentityEpoch == 1);
        var dropThird = registry.ApplySnapshot(new[] { b, a });
        Check("third removal pair stays ambiguous", dropThird.InvalidatedBindingSourceIds.Count == 1 && Contains(dropThird.InvalidatedBindingSourceIds, Source(3)));
        var dropB = registry.ApplySnapshot(new[] { a });
        Check("collision exit restores serial key with barrier", Entry(dropB.Snapshot, 1).Identity.Confidence == IdentityConfidence.ReportedSerial &&
            Entry(dropB.Snapshot, 1).Identity.DeviceIdentity == Device().SerialKey && Entry(dropB.Snapshot, 1).IdentityEpoch == 3);
        Check("exit invalidates survivor plus retired member", Contains(dropB.InvalidatedBindingSourceIds, Source(1)) && Contains(dropB.InvalidatedBindingSourceIds, Source(2)) &&
            dropB.RevokedSourceIds.Count == 1 && Contains(dropB.RevokedSourceIds, Source(2)));
        Check("old pair immutable ambiguous", Entry(pair.Snapshot, 1).Identity.Confidence == IdentityConfidence.Ambiguous && Entry(pair.Snapshot, 1).IdentityEpoch == 2);
        var initialPair = new InputIdentityRegistry(Run).ApplySnapshot(new[] { a, b });
        Check("root initially ambiguous epochs one", Entry(initialPair.Snapshot, 1).IdentityEpoch == 1 && Entry(initialPair.Snapshot, 2).IdentityEpoch == 1 &&
            initialPair.InvalidatedBindingSourceIds.Count == 2);
        var weak = new InputIdentityRegistry(Run).ApplySnapshot(new[] { Obs(descriptor: Device(null)), Obs(2, 202, descriptor: Device(null)) });
        Check("model only independent session keys", weak.Snapshot.Sources[0].Identity.ModelKey == weak.Snapshot.Sources[1].Identity.ModelKey &&
            weak.Snapshot.Sources[0].Identity.DeviceIdentity != weak.Snapshot.Sources[1].Identity.DeviceIdentity && weak.InvalidatedBindingSourceIds.Count == 0 &&
            weak.Snapshot.Sources[0].Identity.Confidence == IdentityConfidence.ModelOnly);
        var insufficient = new InputIdentityRegistry(Run).ApplySnapshot(new[] {
            Obs(descriptor: Device(evidence: false)), Obs(2, 202, descriptor: Device(evidence: false)) });
        Check("insufficient evidence nonempty serial still collides", insufficient.Snapshot.Sources[0].Identity.Confidence == IdentityConfidence.Ambiguous &&
            insufficient.Snapshot.Sources[1].Identity.Confidence == IdentityConfidence.Ambiguous);
        var caseDifferent = new InputIdentityRegistry(Run).ApplySnapshot(new[] { a, Obs(2, 202, descriptor: Device("synth-a")) });
        Check("ordinal serial case does not collide", caseDifferent.Snapshot.Sources[0].Identity.Confidence == IdentityConfidence.ReportedSerial &&
            caseDifferent.Snapshot.Sources[1].Identity.Confidence == IdentityConfidence.ReportedSerial);
        var excluded = new InputIdentityRegistry(Run).ApplySnapshot(new[] { a, Obs(2, 202, state: LiveIdentityState.RemoteExcluded) });
        Check("excluded nonlocal entry does not collide", excluded.Snapshot.Sources.Count == 1 && excluded.Snapshot.Sources[0].Identity.Confidence == IdentityConfidence.ReportedSerial);
    }

    private static void InvalidInputs()
    {
        foreach (string run in new[] { null, "", "bad", Run.ToUpperInvariant(), " " + Run, Run + "\n" })
        {
            bool threw = false;
            try { new InputIdentityRegistry(run); }
            catch (ArgumentException error) { threw = true; Check("constructor fixed diagnostic", error.Message.StartsWith("Invalid registry run ID.", StringComparison.Ordinal)); }
            Check("invalid constructor rejected", threw);
        }
        var invalid = new List<KeyValuePair<LiveIdentityObservation[], LiveRegistryError>> {
            new KeyValuePair<LiveIdentityObservation[], LiveRegistryError>(null, LiveRegistryError.MissingSnapshot),
            new KeyValuePair<LiveIdentityObservation[], LiveRegistryError>(new LiveIdentityObservation[] { null }, LiveRegistryError.MissingObservation)
        };
        var badObservations = new[] {
            Obs(0), Obs(-1), Obs(token: 0), Obs(invalidation: 0), Obs(usage: -1), Obs(kind: FlightInputSourceKind.Touch),
            Obs(state: (LiveIdentityState)99), new LiveIdentityObservation(1, FlightInputSourceKind.Joystick, 101, 1, 0, LiveIdentityState.Active, null),
            Obs(descriptor: Device(kind: FlightInputSourceKind.Gamepad)), Obs(descriptor: Device("\ud800"))
        };
        var badCodes = new[] { LiveRegistryError.InvalidRuntimeDeviceId, LiveRegistryError.InvalidRuntimeDeviceId, LiveRegistryError.InvalidInstanceToken,
            LiveRegistryError.InvalidInvalidationRevision, LiveRegistryError.InvalidUsageRevision, LiveRegistryError.UnsupportedKind,
            LiveRegistryError.UnsupportedState, LiveRegistryError.IdentityResolutionFailed, LiveRegistryError.IdentityResolutionFailed, LiveRegistryError.IdentityResolutionFailed };
        for (int index = 0; index < badObservations.Length; index++)
            invalid.Add(new KeyValuePair<LiveIdentityObservation[], LiveRegistryError>(new[] { badObservations[index] }, badCodes[index]));
        invalid.Add(new KeyValuePair<LiveIdentityObservation[], LiveRegistryError>(new[] { Obs(), Obs(token: 202) }, LiveRegistryError.DuplicateRuntimeDeviceId));
        invalid.Add(new KeyValuePair<LiveIdentityObservation[], LiveRegistryError>(new[] { Obs(), Obs(2, 101) }, LiveRegistryError.DuplicateInstanceToken));
        foreach (var vector in invalid)
        {
            var registry = new InputIdentityRegistry(Run); registry.ApplySnapshot(new[] { Obs() });
            var rejected = registry.ApplySnapshot(vector.Key);
            Check("invalid whole batch clears current", rejected.Status == LiveRegistryStatus.Rejected && rejected.Snapshot.Sources.Count == 0 &&
                rejected.RevokedSourceIds.Count == 1 && Error(rejected, vector.Value));
            Check("invalid leaves generation tombstone", Entry(registry.ApplySnapshot(new[] { Obs() }).Snapshot, 1, 2) != null);
        }
        var history = new InputIdentityRegistry(Run);
        history.ApplySnapshot(new[] { Obs(invalidation: 3, usage: 5) }); history.ApplySnapshot(new LiveIdentityObservation[0]);
        var regress = history.ApplySnapshot(new[] { Obs(invalidation: 2, usage: 5) });
        Check("removed token keeps revision history", regress.Status == LiveRegistryStatus.Rejected && Error(regress, LiveRegistryError.RevisionRegression));
        var theft = history.ApplySnapshot(new[] { Obs(2, 101, 3, 5) });
        Check("removed token keeps runtime ownership", theft.Status == LiveRegistryStatus.Rejected && Error(theft, LiveRegistryError.InstanceTokenOwnershipConflict));
        Check("history recovery still generation two", Entry(history.ApplySnapshot(new[] { Obs(invalidation: 3, usage: 5) }).Snapshot, 1, 2) != null);
        var transactional = new InputIdentityRegistry(Run);
        transactional.ApplySnapshot(new[] { Obs(invalidation: 3) });
        transactional.ApplySnapshot(new[] { Obs(invalidation: 10), Obs(2, 101, 10) });
        Check("rejected higher revision not installed", Entry(transactional.ApplySnapshot(new[] { Obs(invalidation: 3) }).Snapshot, 1, 2) != null);
        var absentDescriptor = new InputIdentityRegistry(Run).ApplySnapshot(new[] {
            new LiveIdentityObservation(1, FlightInputSourceKind.Joystick, 101, 1, 0, LiveIdentityState.Disabled, null) });
        Check("inactive null descriptor valid and no catalog", absentDescriptor.Status == LiveRegistryStatus.Applied && absentDescriptor.Snapshot.CatalogRevision == 0);
        var duplicateForward = new InputIdentityRegistry(Run).ApplySnapshot(new[] { Obs(), Obs(2, 101) });
        var duplicateReverse = new InputIdentityRegistry(Run).ApplySnapshot(new[] { Obs(2, 101), Obs() });
        Check("malformed duplicate diagnostics also order independent", Fingerprint(duplicateForward) == Fingerprint(duplicateReverse));
    }

    private static void OverflowAndImmutability()
    {
        var registry = new InputIdentityRegistry(Run); registry.ApplySnapshot(new[] { Obs() });
        var slots = (IDictionary)typeof(InputIdentityRegistry).GetField("_slots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(registry);
        object slot = slots[1]; slot.GetType().GetField("LastGeneration", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(slot, int.MaxValue);
        var exhausted = registry.ApplySnapshot(new[] { Obs(invalidation: 2), Obs(2, 202, descriptor: Device("OTHER")) });
        Check("generation exhausted does not wrap or disable other slot", exhausted.Status == LiveRegistryStatus.AppliedWithErrors &&
            Error(exhausted, LiveRegistryError.GenerationExhausted) && exhausted.Snapshot.Sources.Count == 1 && Entry(exhausted.Snapshot, 2) != null &&
            Contains(exhausted.RevokedSourceIds, Source(1)));
        var exhaustedAgain = registry.ApplySnapshot(new[] { Obs(invalidation: 2), Obs(2, 202, descriptor: Device("OTHER")) });
        Check("repeat generation exhaustion no churn", exhaustedAgain.Snapshot.CatalogRevision == exhausted.Snapshot.CatalogRevision && exhaustedAgain.RevokedSourceIds.Count == 0);

        var maxLive = new InputIdentityRegistry(Run); maxLive.ApplySnapshot(new[] { Obs() });
        var maxSlots = (IDictionary)typeof(InputIdentityRegistry).GetField("_slots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(maxLive);
        object maxSlot = maxSlots[1];
        var maxEntry = new LiveIdentityEntry(InputDeviceIdentity.Resolve(Device(),
            new RuntimeSourceHandle(FlightInputSourceKind.Joystick, Run, 1, int.MaxValue)), int.MaxValue, 101, 1, 0, 1);
        maxSlot.GetType().GetField("LastGeneration", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(maxSlot, int.MaxValue);
        maxSlot.GetType().GetField("ActiveEntry", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(maxSlot, maxEntry);
        typeof(InputIdentityRegistry).GetField("_current", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(maxLive,
            new LiveIdentitySnapshot(1, new List<LiveIdentityEntry> { maxEntry }));
        Stable("live max generation stays usable unchanged", maxLive.ApplySnapshot(new[] { Obs() }), 1, 1);
        var maxNewGeneration = maxLive.ApplySnapshot(new[] { Obs(invalidation: 2) });
        Check("live max barrier revokes without wrapping", maxNewGeneration.Snapshot.Sources.Count == 0 &&
            Contains(maxNewGeneration.RevokedSourceIds, Source(1, int.MaxValue)) && Error(maxNewGeneration, LiveRegistryError.GenerationExhausted));

        var epochRegistry = new InputIdentityRegistry(Run); var initial = epochRegistry.ApplySnapshot(new[] { Obs() });
        typeof(LiveIdentityEntry).GetField("<IdentityEpoch>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(initial.Snapshot.Sources[0], long.MaxValue);
        var epochFault = epochRegistry.ApplySnapshot(new[] { Obs(), Obs(2, 202) });
        Check("epoch overflow faults entire registry", epochFault.Status == LiveRegistryStatus.Faulted && Error(epochFault, LiveRegistryError.IdentityEpochExhausted) &&
            epochFault.Snapshot.Sources.Count == 0 && epochFault.RevokedSourceIds.Count == 1 && epochFault.Snapshot.CatalogRevision == 2);
        var faultRepeat = epochRegistry.ApplySnapshot(new[] { Obs() });
        Check("fault terminal no revival or repeat delta", faultRepeat.Status == LiveRegistryStatus.Faulted && faultRepeat.Snapshot.Sources.Count == 0 &&
            faultRepeat.RevokedSourceIds.Count == 0 && faultRepeat.InvalidatedBindingSourceIds.Count == 0 &&
            faultRepeat.MappingRevalidationRequiredSourceIds.Count == 0 && ReferenceEquals(faultRepeat.Snapshot, epochFault.Snapshot) &&
            faultRepeat.Snapshot.CatalogRevision == 2 && Error(faultRepeat, LiveRegistryError.RegistryFaulted));

        var catalogRegistry = new InputIdentityRegistry(Run); var current = catalogRegistry.ApplySnapshot(new[] { Obs() });
        typeof(InputIdentityRegistry).GetField("_current", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(catalogRegistry,
            new LiveIdentitySnapshot(long.MaxValue, new List<LiveIdentityEntry>(current.Snapshot.Sources)));
        Stable("catalog maximum accepts unchanged snapshot", catalogRegistry.ApplySnapshot(new[] { Obs() }), long.MaxValue, 1);
        var catalogFault = catalogRegistry.ApplySnapshot(new[] { Obs(usage: 1) });
        Check("catalog overflow no wrap", catalogFault.Status == LiveRegistryStatus.Faulted && Error(catalogFault, LiveRegistryError.CatalogRevisionExhausted) &&
            catalogFault.Snapshot.CatalogRevision == long.MaxValue && catalogFault.Snapshot.Sources.Count == 0);

        string overflowFingerprint = null;
        foreach (bool reverse in new[] { false, true })
        {
            var overflowOrder = new InputIdentityRegistry(Run);
            var before = overflowOrder.ApplySnapshot(new[] { Obs(), Obs(2, 202, descriptor: Device("SYNTH-B")) });
            foreach (var item in before.Snapshot.Sources)
                typeof(LiveIdentityEntry).GetField("<IdentityEpoch>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, long.MaxValue);
            var vector = new[] { Obs(), Obs(2, 202, descriptor: Device("SYNTH-B")), Obs(3, 303), Obs(4, 404, descriptor: Device("SYNTH-B")) };
            if (reverse) Array.Reverse(vector);
            var fault = overflowOrder.ApplySnapshot(vector);
            if (overflowFingerprint == null) overflowFingerprint = Fingerprint(fault);
            Check("multiple overflow order deterministic", Fingerprint(fault) == overflowFingerprint && fault.Errors[0].RuntimeDeviceId == 1);
        }

        const string secretFixture = "SYNTHETIC-PRIVATE-REGISTRY-FIXTURE-NOT-REAL";
        var fixture = Obs(descriptor: Device(secretFixture));
        var result = new InputIdentityRegistry(Run).ApplySnapshot(new[] { fixture, Obs(2, 202, descriptor: Device(secretFixture)) });
        var errorResult = new InputIdentityRegistry(Run).ApplySnapshot(null);
        foreach (object dto in new object[] { fixture, result, result.Snapshot, result.Snapshot.Sources[0], result.Snapshot.Sources[0].Identity, errorResult.Errors[0] })
        {
            Type type = dto.GetType(); Check("DTO sealed " + type.Name, type.IsSealed);
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Check("DTO get only " + type.Name + property.Name, property.GetSetMethod(true) == null);
                if (property.PropertyType == typeof(string)) Check("no raw serial property " + property.Name,
                    (property.GetValue(dto, null) as string ?? "").IndexOf(secretFixture, StringComparison.Ordinal) < 0);
            }
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Check("DTO readonly backing " + type.Name + field.Name, field.IsInitOnly);
                if (field.FieldType == typeof(string)) Check("no raw serial backing " + field.Name,
                    (field.GetValue(dto) as string ?? "").IndexOf(secretFixture, StringComparison.Ordinal) < 0);
            }
        }
        bool rejectedWrite = false;
        try { ((IList<LiveIdentityEntry>)result.Snapshot.Sources)[0] = null; } catch (NotSupportedException) { rejectedWrite = true; }
        Check("snapshot collection defensive readonly", rejectedWrite && result.Snapshot.Sources[0] != null);
        rejectedWrite = false;
        try { ((IList<string>)result.InvalidatedBindingSourceIds)[0] = "corrupt"; } catch (NotSupportedException) { rejectedWrite = true; }
        Check("delta collection defensive readonly", rejectedWrite && result.InvalidatedBindingSourceIds[0] == Source(1));
        rejectedWrite = false;
        try { ((IList<LiveRegistryDiagnostic>)errorResult.Errors)[0] = null; } catch (NotSupportedException) { rejectedWrite = true; }
        Check("error collection defensive readonly", rejectedWrite && errorResult.Errors[0] != null);
        Check("registry has no profile/calibration/raw surface", typeof(InputIdentityRegistry).GetMethod("Save") == null &&
            typeof(InputIdentityRegistry).GetMethod("CollectSamples") == null && typeof(LiveIdentityEntry).GetProperty("ProfileKey") == null);
    }

    private static void OrderAndStress()
    {
        var observations = new[] { Obs(), Obs(2, 202), Obs(3, 303, descriptor: Device(null)) };
        int[][] permutations = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
        string baseline = null;
        foreach (var permutation in permutations)
        {
            var registry = new InputIdentityRegistry(Run);
            var result = registry.ApplySnapshot(new[] { observations[permutation[0]], observations[permutation[1]], observations[permutation[2]] });
            if (baseline == null) baseline = Fingerprint(result);
            Check("all initial permutations identical", Fingerprint(result) == baseline);
            Stable("all repeated permutations no churn", registry.ApplySnapshot(observations), 1, 3);
        }
        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach (string name in new[] { "tr-TR", "ar-SA", "fr-FR", "ja-JP" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                Check("culture ordinal snapshots", Fingerprint(new InputIdentityRegistry(Run).ApplySnapshot(observations)) == baseline);
            }
        }
        finally { CultureInfo.CurrentCulture = oldCulture; }

        var kinds = new[] { FlightInputSourceKind.Keyboard, FlightInputSourceKind.Gamepad, FlightInputSourceKind.Joystick };
        for (int vector = 0; vector < 900; vector++)
        {
            var kind = kinds[vector % kinds.Length]; string serial = vector % 2 == 0 ? null : "SYNTH-STRESS-" + vector.ToString(CultureInfo.InvariantCulture);
            var device = Device(serial, kind: kind); var registry = new InputIdentityRegistry(Run);
            var a = Obs(1, 101, descriptor: device, kind: kind); var b = Obs(2, 202, descriptor: device, kind: kind);
            var first = registry.ApplySnapshot(new[] { b, a });
            Check("stress two entries", first.Snapshot.Sources.Count == 2);
            var ea = Entry(first.Snapshot, 1, kind: kind); var eb = Entry(first.Snapshot, 2, kind: kind);
            Check("stress initial confidence", ea.Identity.Confidence == (serial == null ? IdentityConfidence.ModelOnly : IdentityConfidence.Ambiguous));
            Check("stress two session identities independent", ea.Identity.DeviceIdentity != eb.Identity.DeviceIdentity);
            Check("stress exact resolver model", ea.Identity.ModelKey == InputDeviceIdentity.Resolve(device, new RuntimeSourceHandle(kind, Run, 1, 1)).ModelKey);
            Check("stress initial epochs one", ea.IdentityEpoch == 1 && eb.IdentityEpoch == 1);
            Stable("stress reverse same stage", registry.ApplySnapshot(new[] { a, b }), 1, 2);
            var onlyB = registry.ApplySnapshot(new[] { b });
            Check("stress retired A revoked", onlyB.RevokedSourceIds.Count == 1 && Contains(onlyB.RevokedSourceIds, Source(1, kind: kind)));
            Check("stress B epoch only collision exit", Entry(onlyB.Snapshot, 2, kind: kind).IdentityEpoch == (serial == null ? 1 : 2));
            var again = registry.ApplySnapshot(new[] { b, a });
            Check("stress A new generation two", Entry(again.Snapshot, 1, 2, kind) != null);
            Check("stress no retired source resurrected", Entry(again.Snapshot, 1, 1, kind) == null);
            Check("stress weak/new ambiguous A epoch one", Entry(again.Snapshot, 1, 2, kind).IdentityEpoch == 1);
            Check("stress B epoch monotonic", Entry(again.Snapshot, 2, kind: kind).IdentityEpoch == (serial == null ? 1 : 3));
            Check("stress immutable original snapshot", Entry(first.Snapshot, 1, 1, kind) != null && Entry(first.Snapshot, 1, 1, kind).IdentityEpoch == 1);
            var usage = registry.ApplySnapshot(new[] { Obs(1, 101, usage: 1, descriptor: device, kind: kind), Obs(2, 202, usage: 1, descriptor: device, kind: kind) });
            Check("stress usage per current source", usage.MappingRevalidationRequiredSourceIds.Count == 2 && usage.RevokedSourceIds.Count == 0 && usage.InvalidatedBindingSourceIds.Count == 0);
            Check("stress new A strong or weak session isolated", Entry(usage.Snapshot, 1, 2, kind).Identity.DeviceIdentity != Entry(usage.Snapshot, 2, 1, kind).Identity.DeviceIdentity);
        }
    }

    private static int Main()
    {
        Lifecycle(); Collisions(); InvalidInputs(); OverflowAndImmutability(); ExhaustedActiveCollisions(); OrderAndStress(); RandomTransitions();
        Console.WriteLine("InputIdentityRegistry developer pure C# checks: " + _checks + "; failures: " + _failures);
        Console.WriteLine("Scope: synthetic identity snapshots only; no raw/mapping/profile/Unity/native/physical/iOS integration.");
        return _failures == 0 ? 0 : 1;
    }

    private static void ExhaustedActiveCollisions()
    {
        string baseline = null;
        foreach (bool reverse in new[] { false, true })
        {
            var registry = new InputIdentityRegistry(Run);
            var pair = registry.ApplySnapshot(new[] { Obs(), Obs(2, 202) });
            var slots = (IDictionary)typeof(InputIdentityRegistry).GetField("_slots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(registry);
            object slot = slots[2];
            var maxResolved = InputDeviceIdentity.Resolve(Device(), new RuntimeSourceHandle(FlightInputSourceKind.Joystick, Run, 2, int.MaxValue));
            var maxAmbiguous = new InputIdentityResult(InputIdentityError.None, maxResolved.Kind, maxResolved.SourceId,
                IdentityConfidence.Ambiguous, maxResolved.ModelKey, maxResolved.SerialKey, "session:" + maxResolved.SourceId,
                maxResolved.IdentityDescriptor, maxResolved.HasModelEvidence);
            var maxEntry = new LiveIdentityEntry(maxAmbiguous, int.MaxValue, 202, 1, 0, 1);
            slot.GetType().GetField("LastGeneration", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(slot, int.MaxValue);
            slot.GetType().GetField("ActiveEntry", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(slot, maxEntry);
            typeof(InputIdentityRegistry).GetField("_current", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(registry,
                new LiveIdentitySnapshot(pair.Snapshot.CatalogRevision, new List<LiveIdentityEntry> { pair.Snapshot.Sources[0], maxEntry }));
            var vector = new[] { Obs(), Obs(2, 202, invalidation: 2) };
            if (reverse) Array.Reverse(vector);
            var exhausted = registry.ApplySnapshot(vector);
            var survivor = Entry(exhausted.Snapshot, 1);
            Check("v3 exhausted active does not prove unique", exhausted.Status == LiveRegistryStatus.AppliedWithErrors &&
                Error(exhausted, LiveRegistryError.GenerationExhausted) && exhausted.Snapshot.Sources.Count == 1 &&
                survivor.Identity.Confidence == IdentityConfidence.Ambiguous && survivor.Identity.DeviceIdentity == "session:" + Source(1));
            Check("v3 existing ambiguous epoch unchanged", survivor.IdentityEpoch == 1 &&
                !Contains(exhausted.InvalidatedBindingSourceIds, Source(1)) && exhausted.Snapshot.CatalogRevision == 2);
            Check("v3 exhausted old ID revoked never recreated", Contains(exhausted.RevokedSourceIds, Source(2, int.MaxValue)) &&
                Entry(exhausted.Snapshot, 2, int.MaxValue) == null && Entry(exhausted.Snapshot, 2) == null);
            if (baseline == null) baseline = Fingerprint(exhausted);
            Check("v3 exhausted collision permutations identical", Fingerprint(exhausted) == baseline);
            var repeat = registry.ApplySnapshot(vector);
            Check("v3 repeated exhausted observer maintains barrier", repeat.Status == LiveRegistryStatus.AppliedWithErrors &&
                Entry(repeat.Snapshot, 1).Identity.Confidence == IdentityConfidence.Ambiguous && Entry(repeat.Snapshot, 1).IdentityEpoch == 1 &&
                repeat.Snapshot.CatalogRevision == 2 && repeat.RevokedSourceIds.Count == 0 && repeat.InvalidatedBindingSourceIds.Count == 0);
            var removed = registry.ApplySnapshot(new[] { Obs() });
            Check("v3 actual omitted peer permits epoch-gated exit", Entry(removed.Snapshot, 1).Identity.Confidence == IdentityConfidence.ReportedSerial &&
                Entry(removed.Snapshot, 1).IdentityEpoch == 2 && Contains(removed.InvalidatedBindingSourceIds, Source(1)) && removed.Snapshot.CatalogRevision == 3);
            Stable("v3 inactive exhausted peer not collision eligible", registry.ApplySnapshot(new[] { Obs(), Obs(2, 202, 2, state: LiveIdentityState.Disabled) }), 3, 1);
            var returnLive = registry.ApplySnapshot(vector);
            Check("v3 exhausted live peer returns ambiguity barrier", Entry(returnLive.Snapshot, 1).Identity.Confidence == IdentityConfidence.Ambiguous &&
                Entry(returnLive.Snapshot, 1).IdentityEpoch == 3 && Contains(returnLive.InvalidatedBindingSourceIds, Source(1)) && returnLive.Snapshot.CatalogRevision == 4);
        }

        foreach (bool evidence in new[] { true, false })
        {
            var registry = new InputIdentityRegistry(Run); var descriptor = Device(evidence: evidence);
            registry.ApplySnapshot(new[] { Obs(2, 202, descriptor: descriptor) }); registry.ApplySnapshot(new LiveIdentityObservation[0]);
            var slots = (IDictionary)typeof(InputIdentityRegistry).GetField("_slots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(registry);
            object slot = slots[2]; slot.GetType().GetField("LastGeneration", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(slot, int.MaxValue);
            var first = registry.ApplySnapshot(new[] { Obs(descriptor: descriptor), Obs(2, 202, descriptor: descriptor) });
            var survivor = Entry(first.Snapshot, 1);
            Check("v3 newly seen survivor initially ambiguous", first.Status == LiveRegistryStatus.AppliedWithErrors && survivor.Identity.Confidence == IdentityConfidence.Ambiguous &&
                survivor.IdentityEpoch == 1 && first.InvalidatedBindingSourceIds.Count == 1 && Contains(first.InvalidatedBindingSourceIds, Source(1)));
            Check("v3 insufficient evidence nonempty serial counted too", survivor.Identity.HasModelEvidence == evidence &&
                survivor.Identity.SerialKey == descriptor.SerialKey && first.Snapshot.Sources.Count == 1 && first.RevokedSourceIds.Count == 0);
            var different = registry.ApplySnapshot(new[] { Obs(descriptor: descriptor), Obs(2, 202, descriptor: Device("SYNTH-OTHER", evidence: evidence)) });
            Check("v3 distinct exhausted serial does not collide", Entry(different.Snapshot, 1).Identity.Confidence ==
                (evidence ? IdentityConfidence.ReportedSerial : IdentityConfidence.SessionOnly) && Entry(different.Snapshot, 1).IdentityEpoch == 2 &&
                Contains(different.InvalidatedBindingSourceIds, Source(1)) && different.Snapshot.Sources.Count == 1);
        }
    }

    // A fixture-state oracle tracks generations and collision epochs using serial
    // groups, not registry internals. Stable seed, complete snapshots and shuffled order.
    private static void RandomTransitions()
    {
        const int count = 6, ticks = 1200;
        var random = new Random(1251011); var registry = new InputIdentityRegistry(Run);
        var active = new bool[count]; var oldActive = new bool[count];
        var token = new long[count]; var oldToken = new long[count];
        var invalidation = new long[count]; var oldInvalidation = new long[count];
        var usage = new long[count]; var oldUsage = new long[count];
        var serial = new string[count]; var oldSerial = new string[count];
        var generation = new int[count]; var epochs = new long[count];
        var confidence = new IdentityConfidence[count]; long nextToken = 1000, catalog = 0;
        for (int index = 0; index < count; index++) { token[index] = ++nextToken; invalidation[index] = 1; }
        for (int tick = 0; tick < ticks; tick++)
        {
            int selected = random.Next(count);
            switch (random.Next(7))
            {
                case 0: active[selected] = !active[selected]; break;
                case 1: invalidation[selected]++; break;
                case 2: usage[selected]++; break;
                case 3: token[selected] = ++nextToken; invalidation[selected] = 1; usage[selected] = 0; break;
                case 4: serial[selected] = random.Next(3) == 0 ? null : "SYNTH-RANDOM-GROUP-" + random.Next(3).ToString(CultureInfo.InvariantCulture); break;
                case 5: active[selected] = false; break;
                case 6: break;
            }
            var observations = new List<LiveIdentityObservation>();
            for (int index = 0; index < count; index++)
                if (active[index] || random.Next(2) == 0)
                    observations.Add(Obs(index + 1, token[index], invalidation[index], usage[index],
                        active[index] ? LiveIdentityState.Active : LiveIdentityState.Disabled, Device(serial[index])));
            for (int index = observations.Count - 1; index > 0; index--)
            { int position = random.Next(index + 1); var swap = observations[position]; observations[position] = observations[index]; observations[index] = swap; }

            var revoked = new HashSet<string>(StringComparer.Ordinal);
            var invalidated = new HashSet<string>(StringComparer.Ordinal);
            var revalidate = new HashSet<string>(StringComparer.Ordinal);
            var expectedGeneration = (int[])generation.Clone(); var expectedEpoch = (long[])epochs.Clone();
            var expectedConfidence = (IdentityConfidence[])confidence.Clone();
            bool changed = false; int activeCount = 0;
            for (int index = 0; index < count; index++)
            {
                bool same = active[index] && oldActive[index] && token[index] == oldToken[index] &&
                    invalidation[index] == oldInvalidation[index] && serial[index] == oldSerial[index];
                if (oldActive[index] && !same)
                { revoked.Add(Source(index + 1, generation[index])); invalidated.Add(Source(index + 1, generation[index])); changed = true; }
                if (!active[index]) continue;
                activeCount++;
                if (!same) { expectedGeneration[index]++; expectedEpoch[index] = 1; changed = true; }
                int matches = 0;
                if (serial[index] != null)
                    for (int other = 0; other < count; other++) if (active[other] && serial[other] == serial[index]) matches++;
                var nextConfidence = serial[index] == null ? IdentityConfidence.ModelOnly :
                    matches > 1 ? IdentityConfidence.Ambiguous : IdentityConfidence.ReportedSerial;
                expectedConfidence[index] = nextConfidence;
                string source = Source(index + 1, expectedGeneration[index]);
                if (same && confidence[index] != nextConfidence)
                { expectedEpoch[index]++; invalidated.Add(source); changed = true; }
                else if (!same && nextConfidence == IdentityConfidence.Ambiguous) invalidated.Add(source);
                if (same && usage[index] != oldUsage[index]) { revalidate.Add(source); changed = true; }
            }
            if (changed) catalog++;
            var result = registry.ApplySnapshot(observations);
            Check("random applied no errors " + tick, result.Status == LiveRegistryStatus.Applied && result.Errors.Count == 0);
            Check("random exact catalog/count " + tick, result.Snapshot.CatalogRevision == catalog && result.Snapshot.Sources.Count == activeCount);
            Check("random revoked set " + tick, EqualSet(result.RevokedSourceIds, revoked));
            Check("random binding set " + tick, EqualSet(result.InvalidatedBindingSourceIds, invalidated));
            Check("random usage set " + tick, EqualSet(result.MappingRevalidationRequiredSourceIds, revalidate));
            string previousSource = null;
            foreach (var item in result.Snapshot.Sources)
            {
                Check("random source ordinal sorted " + tick, previousSource == null || StringComparer.Ordinal.Compare(previousSource, item.Identity.SourceId) < 0);
                previousSource = item.Identity.SourceId;
            }
            for (int index = 0; index < count; index++)
            {
                if (active[index])
                {
                    var actual = Entry(result.Snapshot, index + 1, expectedGeneration[index]);
                    Check("random generation exists " + tick, actual != null);
                    if (actual != null)
                    {
                        Check("random exact confidence epoch " + tick, actual.Identity.Confidence == expectedConfidence[index] && actual.IdentityEpoch == expectedEpoch[index]);
                        Check("random exact metadata " + tick, actual.InstanceToken == token[index] && actual.InvalidationRevision == invalidation[index] && actual.UsageRevision == usage[index]);
                        Check("random exact isolated identity " + tick, actual.Identity.DeviceIdentity ==
                            (expectedConfidence[index] == IdentityConfidence.ReportedSerial ? Device(serial[index]).SerialKey : "session:" + Source(index + 1, expectedGeneration[index])));
                    }
                }
                oldActive[index] = active[index]; oldToken[index] = token[index]; oldInvalidation[index] = invalidation[index];
                oldUsage[index] = usage[index]; oldSerial[index] = serial[index]; generation[index] = expectedGeneration[index];
                epochs[index] = expectedEpoch[index]; confidence[index] = expectedConfidence[index];
            }
        }
        Console.WriteLine("Registry author randomized reference ticks: " + ticks + "; seed: 1251011; synthetic devices: " + count);
    }

    private static bool EqualSet(IReadOnlyList<string> values, HashSet<string> expected)
    {
        if (values.Count != expected.Count) return false;
        string previous = null;
        foreach (string value in values)
        {
            if (!expected.Contains(value) || (previous != null && StringComparer.Ordinal.Compare(previous, value) >= 0)) return false;
            previous = value;
        }
        return true;
    }
}
