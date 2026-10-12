using System;
using System.Collections.Generic;

namespace AeroSim.Input
{
    public enum LiveIdentityState { Active, Disabled, Disconnected, Removed, RemoteExcluded, PlatformUnsupported }
    public enum LiveRegistryStatus { Applied, AppliedWithErrors, Rejected, Faulted }
    public enum LiveRegistryError
    {
        MissingSnapshot, MissingObservation, InvalidRuntimeDeviceId, UnsupportedKind,
        InvalidInstanceToken, InvalidInvalidationRevision, InvalidUsageRevision, UnsupportedState,
        DuplicateRuntimeDeviceId, DuplicateInstanceToken, InstanceTokenOwnershipConflict,
        RevisionRegression, IdentityResolutionFailed, GenerationExhausted,
        IdentityEpochExhausted, CatalogRevisionExhausted, RegistryFaulted
    }

    public sealed class LiveIdentityObservation
    {
        public int RuntimeDeviceId { get; }
        public FlightInputSourceKind Kind { get; }
        public long InstanceToken { get; }
        public long InvalidationRevision { get; }
        public long UsageRevision { get; }
        public LiveIdentityState State { get; }
        public InputDeviceDescriptor Descriptor { get; }

        public LiveIdentityObservation(int runtimeDeviceId, FlightInputSourceKind kind, long instanceToken,
            long invalidationRevision, long usageRevision, LiveIdentityState state, InputDeviceDescriptor descriptor)
        {
            RuntimeDeviceId = runtimeDeviceId; Kind = kind; InstanceToken = instanceToken;
            InvalidationRevision = invalidationRevision; UsageRevision = usageRevision;
            State = state; Descriptor = descriptor;
        }
    }

    public sealed class LiveIdentityEntry
    {
        public InputIdentityResult Identity { get; }
        public int SourceGeneration { get; }
        public long InstanceToken { get; }
        public long InvalidationRevision { get; }
        public long UsageRevision { get; }
        public long IdentityEpoch { get; }

        internal LiveIdentityEntry(InputIdentityResult identity, int generation, long instanceToken,
            long invalidationRevision, long usageRevision, long identityEpoch)
        {
            Identity = identity; SourceGeneration = generation; InstanceToken = instanceToken;
            InvalidationRevision = invalidationRevision; UsageRevision = usageRevision; IdentityEpoch = identityEpoch;
        }
    }

    public sealed class LiveRegistryDiagnostic
    {
        public LiveRegistryError Error { get; }
        public int RuntimeDeviceId { get; }
        public InputIdentityError IdentityError { get; }
        internal LiveRegistryDiagnostic(LiveRegistryError error, int runtimeDeviceId,
            InputIdentityError identityError = InputIdentityError.None)
        { Error = error; RuntimeDeviceId = runtimeDeviceId; IdentityError = identityError; }
    }

    public sealed class LiveIdentitySnapshot
    {
        public long CatalogRevision { get; }
        public IReadOnlyList<LiveIdentityEntry> Sources { get; }
        internal LiveIdentitySnapshot(long revision, IList<LiveIdentityEntry> sources)
        { CatalogRevision = revision; Sources = Array.AsReadOnly(Copy(sources)); }

        private static LiveIdentityEntry[] Copy(IList<LiveIdentityEntry> sources)
        {
            var copy = new LiveIdentityEntry[sources.Count];
            sources.CopyTo(copy, 0);
            return copy;
        }
    }

    public sealed class LiveRegistryApplyResult
    {
        public LiveRegistryStatus Status { get; }
        public LiveIdentitySnapshot Snapshot { get; }
        public IReadOnlyList<LiveRegistryDiagnostic> Errors { get; }
        public IReadOnlyList<string> RevokedSourceIds { get; }
        public IReadOnlyList<string> InvalidatedBindingSourceIds { get; }
        public IReadOnlyList<string> MappingRevalidationRequiredSourceIds { get; }

        internal LiveRegistryApplyResult(LiveRegistryStatus status, LiveIdentitySnapshot snapshot,
            List<LiveRegistryDiagnostic> errors, HashSet<string> revoked,
            HashSet<string> invalidated, HashSet<string> revalidate)
        {
            Status = status; Snapshot = snapshot;
            errors.Sort((a, b) => {
                int order = a.RuntimeDeviceId.CompareTo(b.RuntimeDeviceId);
                if (order == 0) order = a.Error.CompareTo(b.Error);
                return order == 0 ? a.IdentityError.CompareTo(b.IdentityError) : order;
            });
            var distinct = new List<LiveRegistryDiagnostic>();
            foreach (var error in errors)
            {
                if (distinct.Count != 0)
                {
                    var last = distinct[distinct.Count - 1];
                    if (last.RuntimeDeviceId == error.RuntimeDeviceId && last.Error == error.Error &&
                        last.IdentityError == error.IdentityError) continue;
                }
                distinct.Add(error);
            }
            Errors = Array.AsReadOnly(distinct.ToArray());
            RevokedSourceIds = Sorted(revoked); InvalidatedBindingSourceIds = Sorted(invalidated);
            MappingRevalidationRequiredSourceIds = Sorted(revalidate);
        }

        private static IReadOnlyList<string> Sorted(HashSet<string> values)
        {
            var copy = new string[values.Count]; values.CopyTo(copy);
            Array.Sort(copy, StringComparer.Ordinal);
            return Array.AsReadOnly(copy);
        }
    }

    // Single-writer, pure identity snapshots only. Adapter tokens/revisions are a
    // protocol input, not OS event detection or proof of physical device identity.
    public sealed class InputIdentityRegistry
    {
        private sealed class Slot
        {
            internal int LastGeneration;
            internal LiveIdentityEntry ActiveEntry;
            internal Slot(int generation, LiveIdentityEntry entry)
            { LastGeneration = generation; ActiveEntry = entry; }
        }

        private sealed class TokenHistory
        {
            internal readonly int RuntimeDeviceId;
            internal readonly long InvalidationRevision, UsageRevision;
            internal TokenHistory(int id, long invalidation, long usage)
            { RuntimeDeviceId = id; InvalidationRevision = invalidation; UsageRevision = usage; }
        }

        private readonly string _runId;
        private Dictionary<int, Slot> _slots = new Dictionary<int, Slot>();
        private Dictionary<long, TokenHistory> _tokens = new Dictionary<long, TokenHistory>();
        private LiveIdentitySnapshot _current = new LiveIdentitySnapshot(0, new List<LiveIdentityEntry>());
        private bool _faulted;
        public LiveIdentitySnapshot Current => _current;

        public InputIdentityRegistry(string runId)
        {
            // Use the frozen resolver for run validation; do not make a second key codec.
            var probe = InputDeviceIdentity.Resolve(new InputDeviceDescriptor(FlightInputSourceKind.Keyboard),
                new RuntimeSourceHandle(FlightInputSourceKind.Keyboard, runId, 1, 1));
            if (!probe.IsValid) throw new ArgumentException("Invalid registry run ID.", nameof(runId));
            _runId = runId;
        }

        public LiveRegistryApplyResult ApplySnapshot(IReadOnlyList<LiveIdentityObservation> observations)
        {
            if (_faulted) return Result(LiveRegistryStatus.Faulted,
                new List<LiveRegistryDiagnostic> { new LiveRegistryDiagnostic(LiveRegistryError.RegistryFaulted, 0) });
            var errors = new List<LiveRegistryDiagnostic>();
            var copy = observations == null ? null : new LiveIdentityObservation[observations.Count];
            if (copy == null) errors.Add(new LiveRegistryDiagnostic(LiveRegistryError.MissingSnapshot, 0));
            else
            {
                for (int index = 0; index < copy.Length; index++) copy[index] = observations[index];
                Validate(copy, errors);
            }
            if (errors.Count != 0) return Reject(errors);

            var nextSlots = new Dictionary<int, Slot>();
            foreach (var item in _slots) nextSlots.Add(item.Key, new Slot(item.Value.LastGeneration, null));
            var nextTokens = new Dictionary<long, TokenHistory>(_tokens);
            var entries = new List<LiveIdentityEntry>();
            var serialCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            // Live identity ambiguity is independent of whether a slot can allocate
            // its next generation. Exhaustion is not proof that a peer is unique.
            foreach (var observation in copy)
            {
                if (observation.State != LiveIdentityState.Active) continue;
                string serialKey = observation.Descriptor.SerialKey;
                if (serialKey.Length == 0) continue;
                serialCounts.TryGetValue(serialKey, out int count);
                serialCounts[serialKey] = count + 1;
            }
            foreach (var observation in copy)
            {
                nextTokens[observation.InstanceToken] = new TokenHistory(observation.RuntimeDeviceId,
                    observation.InvalidationRevision, observation.UsageRevision);
                if (!nextSlots.TryGetValue(observation.RuntimeDeviceId, out var slot))
                { slot = new Slot(0, null); nextSlots.Add(observation.RuntimeDeviceId, slot); }
                if (observation.State != LiveIdentityState.Active) continue;

                _slots.TryGetValue(observation.RuntimeDeviceId, out var previousSlot);
                var previous = previousSlot == null ? null : previousSlot.ActiveEntry;
                var descriptorIdentity = Resolve(observation, 1);
                bool sameGeneration = previous != null && previous.InstanceToken == observation.InstanceToken &&
                    previous.InvalidationRevision == observation.InvalidationRevision &&
                    SameModel(previous.Identity, descriptorIdentity);
                int generation = slot.LastGeneration;
                if (!sameGeneration)
                {
                    if (generation == int.MaxValue)
                    {
                        errors.Add(new LiveRegistryDiagnostic(LiveRegistryError.GenerationExhausted, observation.RuntimeDeviceId));
                        continue;
                    }
                    generation++;
                }
                var identity = Resolve(observation, generation);
                var entry = new LiveIdentityEntry(identity, generation, observation.InstanceToken,
                    observation.InvalidationRevision, observation.UsageRevision, sameGeneration ? previous.IdentityEpoch : 1);
                slot.LastGeneration = generation; slot.ActiveEntry = entry; entries.Add(entry);
            }

            entries.Sort((a, b) => StringComparer.Ordinal.Compare(a.Identity.SourceId, b.Identity.SourceId));
            var revoked = Set(); var invalidated = Set(); var revalidate = Set();
            var finalEntries = new List<LiveIdentityEntry>();
            foreach (var entry in entries)
            {
                var identity = entry.Identity;
                bool ambiguous = identity.SerialKey.Length != 0 && serialCounts[identity.SerialKey] > 1;
                if (ambiguous)
                    identity = new InputIdentityResult(InputIdentityError.None, identity.Kind, identity.SourceId,
                        IdentityConfidence.Ambiguous, identity.ModelKey, identity.SerialKey, "session:" + identity.SourceId,
                        identity.IdentityDescriptor, identity.HasModelEvidence);
                int id = nextTokens[entry.InstanceToken].RuntimeDeviceId;
                _slots.TryGetValue(id, out var previousSlot);
                var previous = previousSlot == null ? null : previousSlot.ActiveEntry;
                bool sameSource = previous != null && previous.Identity.SourceId == identity.SourceId;
                long epoch = entry.IdentityEpoch;
                if (sameSource && (previous.Identity.Confidence != identity.Confidence ||
                    previous.Identity.DeviceIdentity != identity.DeviceIdentity))
                {
                    if (epoch == long.MaxValue)
                        return Fault(new LiveRegistryDiagnostic(LiveRegistryError.IdentityEpochExhausted, id));
                    epoch++; invalidated.Add(identity.SourceId);
                }
                else if (!sameSource && ambiguous) invalidated.Add(identity.SourceId);
                if (sameSource && previous.UsageRevision != entry.UsageRevision) revalidate.Add(identity.SourceId);
                var final = new LiveIdentityEntry(identity, entry.SourceGeneration, entry.InstanceToken,
                    entry.InvalidationRevision, entry.UsageRevision, epoch);
                nextSlots[id].ActiveEntry = final; finalEntries.Add(final);
            }
            foreach (var item in _slots)
            {
                var old = item.Value.ActiveEntry;
                if (old == null) continue;
                nextSlots.TryGetValue(item.Key, out var next);
                if (next == null || next.ActiveEntry == null || next.ActiveEntry.Identity.SourceId != old.Identity.SourceId)
                { revoked.Add(old.Identity.SourceId); invalidated.Add(old.Identity.SourceId); }
            }
            finalEntries.Sort((a, b) => StringComparer.Ordinal.Compare(a.Identity.SourceId, b.Identity.SourceId));
            bool changed = !SameSnapshot(_current.Sources, finalEntries);
            if (changed && _current.CatalogRevision == long.MaxValue)
                return Fault(new LiveRegistryDiagnostic(LiveRegistryError.CatalogRevisionExhausted, 0));
            long revision = _current.CatalogRevision + (changed ? 1 : 0);
            _slots = nextSlots; _tokens = nextTokens;
            _current = new LiveIdentitySnapshot(revision, finalEntries);
            return new LiveRegistryApplyResult(errors.Count == 0 ? LiveRegistryStatus.Applied : LiveRegistryStatus.AppliedWithErrors,
                _current, errors, revoked, invalidated, revalidate);
        }

        private InputIdentityResult Resolve(LiveIdentityObservation observation, int generation) =>
            InputDeviceIdentity.Resolve(observation.Descriptor, new RuntimeSourceHandle(observation.Kind,
                _runId, observation.RuntimeDeviceId, generation));

        private void Validate(LiveIdentityObservation[] observations, List<LiveRegistryDiagnostic> errors)
        {
            var ids = new Dictionary<int, int>(); var tokens = new Dictionary<long, int>();
            foreach (var observation in observations)
            {
                if (observation == null) continue;
                ids.TryGetValue(observation.RuntimeDeviceId, out int idCount);
                ids[observation.RuntimeDeviceId] = idCount + 1;
                tokens.TryGetValue(observation.InstanceToken, out int tokenCount);
                tokens[observation.InstanceToken] = tokenCount + 1;
            }
            foreach (var observation in observations)
            {
                if (observation == null)
                { errors.Add(new LiveRegistryDiagnostic(LiveRegistryError.MissingObservation, 0)); continue; }
                int id = observation.RuntimeDeviceId;
                if (id <= 0) errors.Add(new LiveRegistryDiagnostic(LiveRegistryError.InvalidRuntimeDeviceId, id));
                if (!InputDeviceIdentity.TryHardwareKind(observation.Kind, out _))
                    errors.Add(new LiveRegistryDiagnostic(LiveRegistryError.UnsupportedKind, id));
                if (observation.InstanceToken <= 0) errors.Add(new LiveRegistryDiagnostic(LiveRegistryError.InvalidInstanceToken, id));
                if (observation.InvalidationRevision <= 0) errors.Add(new LiveRegistryDiagnostic(LiveRegistryError.InvalidInvalidationRevision, id));
                if (observation.UsageRevision < 0) errors.Add(new LiveRegistryDiagnostic(LiveRegistryError.InvalidUsageRevision, id));
                if (!Enum.IsDefined(typeof(LiveIdentityState), observation.State))
                    errors.Add(new LiveRegistryDiagnostic(LiveRegistryError.UnsupportedState, id));
                if (ids[id] > 1) errors.Add(new LiveRegistryDiagnostic(LiveRegistryError.DuplicateRuntimeDeviceId, id));
                if (tokens[observation.InstanceToken] > 1)
                    errors.Add(new LiveRegistryDiagnostic(LiveRegistryError.DuplicateInstanceToken, id));
                if (_tokens.TryGetValue(observation.InstanceToken, out var history))
                {
                    if (history.RuntimeDeviceId != id)
                        errors.Add(new LiveRegistryDiagnostic(LiveRegistryError.InstanceTokenOwnershipConflict, id));
                    if (observation.InvalidationRevision < history.InvalidationRevision || observation.UsageRevision < history.UsageRevision)
                        errors.Add(new LiveRegistryDiagnostic(LiveRegistryError.RevisionRegression, id));
                }
                if (observation.State == LiveIdentityState.Active)
                {
                    var result = Resolve(observation, 1);
                    if (!result.IsValid) errors.Add(new LiveRegistryDiagnostic(LiveRegistryError.IdentityResolutionFailed, id, result.Error));
                }
            }
        }

        private LiveRegistryApplyResult Reject(List<LiveRegistryDiagnostic> errors)
        {
            if (_current.Sources.Count != 0 && _current.CatalogRevision == long.MaxValue)
                return Fault(new LiveRegistryDiagnostic(LiveRegistryError.CatalogRevisionExhausted, 0));
            var revoked = RevokeAll();
            long revision = _current.CatalogRevision + (revoked.Count != 0 ? 1 : 0);
            _current = new LiveIdentitySnapshot(revision, new List<LiveIdentityEntry>());
            return new LiveRegistryApplyResult(LiveRegistryStatus.Rejected, _current, errors, revoked, revoked, Set());
        }

        private LiveRegistryApplyResult Fault(LiveRegistryDiagnostic error)
        {
            var revoked = RevokeAll(); _faulted = true;
            long revision = _current.CatalogRevision;
            if (revoked.Count != 0 && revision < long.MaxValue) revision++;
            _current = new LiveIdentitySnapshot(revision, new List<LiveIdentityEntry>());
            return new LiveRegistryApplyResult(LiveRegistryStatus.Faulted, _current,
                new List<LiveRegistryDiagnostic> { error }, revoked, revoked, Set());
        }

        private HashSet<string> RevokeAll()
        {
            var revoked = Set();
            foreach (var slot in _slots.Values)
                if (slot.ActiveEntry != null)
                { revoked.Add(slot.ActiveEntry.Identity.SourceId); slot.ActiveEntry = null; }
            return revoked;
        }

        private LiveRegistryApplyResult Result(LiveRegistryStatus status, List<LiveRegistryDiagnostic> errors) =>
            new LiveRegistryApplyResult(status, _current, errors, Set(), Set(), Set());
        private static HashSet<string> Set() => new HashSet<string>(StringComparer.Ordinal);
        private static bool SameModel(InputIdentityResult a, InputIdentityResult b) =>
            a.Kind == b.Kind && a.ModelKey == b.ModelKey && a.SerialKey == b.SerialKey && a.HasModelEvidence == b.HasModelEvidence;
        private static bool SameSnapshot(IReadOnlyList<LiveIdentityEntry> old, IList<LiveIdentityEntry> next)
        {
            if (old.Count != next.Count) return false;
            for (int index = 0; index < old.Count; index++)
            {
                var a = old[index]; var b = next[index];
                if (a.Identity.SourceId != b.Identity.SourceId || !SameModel(a.Identity, b.Identity) ||
                    a.Identity.Confidence != b.Identity.Confidence || a.Identity.DeviceIdentity != b.Identity.DeviceIdentity ||
                    a.SourceGeneration != b.SourceGeneration || a.InstanceToken != b.InstanceToken ||
                    a.InvalidationRevision != b.InvalidationRevision || a.UsageRevision != b.UsageRevision ||
                    a.IdentityEpoch != b.IdentityEpoch) return false;
            }
            return true;
        }
    }
}
