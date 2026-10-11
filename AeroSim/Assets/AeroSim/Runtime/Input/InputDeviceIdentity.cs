using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AeroSim.Input
{
    public enum IdentityConfidence { ReportedSerial, ModelOnly, SessionOnly, Ambiguous }

    public enum InputIdentityError
    {
        None, MissingDescriptor, MissingHandle, UnsupportedKind, KindMismatch,
        InvalidDescriptorText, InvalidRunId, InvalidRuntimeDeviceId, InvalidSourceGeneration
    }

    // Immutable model evidence suitable for a future persistent record. No raw serial,
    // display name, driver version, capabilities JSON or runtime identity is retained.
    public sealed class InputIdentityDescriptor
    {
        public string FormatVersion => "identity-v1";
        public string Kind { get; }
        public string InterfaceName { get; }
        public string Manufacturer { get; }
        public string Product { get; }
        public string Layout { get; }
        public int? HidVendorId { get; }
        public int? HidProductId { get; }
        public int? HidUsagePage { get; }
        public int? HidUsage { get; }
        public string SerialSha256 { get; }

        internal InputIdentityDescriptor(string kind, string interfaceName, string manufacturer,
            string product, string layout, int? hidVendorId, int? hidProductId,
            int? hidUsagePage, int? hidUsage, string serialSha256)
        {
            Kind = kind; InterfaceName = interfaceName; Manufacturer = manufacturer;
            Product = product; Layout = layout; HidVendorId = hidVendorId;
            HidProductId = hidProductId; HidUsagePage = hidUsagePage; HidUsage = hidUsage;
            SerialSha256 = serialSha256;
        }
    }

    // Raw serial exists only as a constructor argument/local while exact keys are
    // computed. It is not a field/property and must not be persisted by an adapter.
    public sealed class InputDeviceDescriptor
    {
        public FlightInputSourceKind Kind { get; }
        public InputIdentityDescriptor IdentityDescriptor { get; }
        public string ModelKey { get; }
        public string SerialKey { get; }
        public bool HasModelEvidence { get; }
        public InputIdentityError Error { get; }

        public InputDeviceDescriptor(FlightInputSourceKind kind, string interfaceName = null,
            string manufacturer = null, string product = null, string layout = null,
            int? hidVendorId = null, int? hidProductId = null, int? hidUsagePage = null,
            int? hidUsage = null, string serial = null, string displayName = null,
            string version = null, string capabilitiesJson = null)
        {
            Kind = kind; ModelKey = string.Empty; SerialKey = string.Empty;
            if (!InputDeviceIdentity.TryHardwareKind(kind, out var kindName))
            {
                Error = InputIdentityError.UnsupportedKind;
                return;
            }

            try
            {
                string normalizedInterface = InputDeviceIdentity.Normalize(interfaceName);
                string normalizedManufacturer = InputDeviceIdentity.Normalize(manufacturer);
                string normalizedProduct = InputDeviceIdentity.Normalize(product);
                string normalizedLayout = InputDeviceIdentity.Normalize(layout);
                string normalizedSerial = InputDeviceIdentity.Normalize(serial);
                string[] fields = {
                    "identity-v1", kindName, normalizedInterface, normalizedManufacturer,
                    normalizedProduct, normalizedLayout, InputDeviceIdentity.Decimal(hidVendorId),
                    InputDeviceIdentity.Decimal(hidProductId), InputDeviceIdentity.Decimal(hidUsagePage),
                    InputDeviceIdentity.Decimal(hidUsage)
                };
                ModelKey = "model:" + InputDeviceIdentity.HashFields(fields);
                string serialSha256 = string.Empty;
                if (normalizedSerial.Length != 0)
                {
                    // Hash the full canonical model fields plus the original normalized
                    // serial field, not a hash of serial or a hash of ModelKey.
                    string[] serialFields = new string[fields.Length + 1];
                    Array.Copy(fields, serialFields, fields.Length);
                    serialFields[fields.Length] = normalizedSerial;
                    SerialKey = "serial:" + InputDeviceIdentity.HashFields(serialFields);
                    serialSha256 = InputDeviceIdentity.HashUtf8(normalizedSerial);
                }

                HasModelEvidence = normalizedInterface.Length != 0 && normalizedLayout.Length != 0 &&
                    (normalizedProduct.Length != 0 ||
                    (hidVendorId.HasValue && hidVendorId.Value != 0 &&
                     hidProductId.HasValue && hidProductId.Value != 0));
                IdentityDescriptor = new InputIdentityDescriptor(kindName, normalizedInterface,
                    normalizedManufacturer, normalizedProduct, normalizedLayout, hidVendorId,
                    hidProductId, hidUsagePage, hidUsage, serialSha256);
            }
            catch (ArgumentException)
            {
                // Normalization/strict UTF8 errors contain no caller-supplied text.
                ModelKey = string.Empty; SerialKey = string.Empty;
                HasModelEvidence = false; IdentityDescriptor = null;
                Error = InputIdentityError.InvalidDescriptorText;
            }
        }
    }

    public sealed class RuntimeSourceHandle
    {
        public FlightInputSourceKind Kind { get; }
        public string RunId { get; }
        public int RuntimeDeviceId { get; }
        public int SourceGeneration { get; }

        public RuntimeSourceHandle(FlightInputSourceKind kind, string runId,
            int runtimeDeviceId, int sourceGeneration)
        {
            Kind = kind; RunId = runId; RuntimeDeviceId = runtimeDeviceId;
            SourceGeneration = sourceGeneration;
        }
    }

    public sealed class InputIdentityResult
    {
        public bool IsValid => Error == InputIdentityError.None;
        public InputIdentityError Error { get; }
        public FlightInputSourceKind Kind { get; }
        public string SourceId { get; }
        public IdentityConfidence Confidence { get; }
        public string ConfidenceCode => Confidence == IdentityConfidence.ReportedSerial ? "reported_serial" :
            Confidence == IdentityConfidence.ModelOnly ? "model_only" :
            Confidence == IdentityConfidence.Ambiguous ? "ambiguous" : "session_only";
        public string ModelKey { get; }
        public string SerialKey { get; }
        public string DeviceIdentity { get; }
        public InputIdentityDescriptor IdentityDescriptor { get; }
        public bool HasModelEvidence { get; }

        internal InputIdentityResult(InputIdentityError error, FlightInputSourceKind kind = FlightInputSourceKind.None,
            string sourceId = "", IdentityConfidence confidence = IdentityConfidence.SessionOnly,
            string modelKey = "", string serialKey = "", string deviceIdentity = "",
            InputIdentityDescriptor identityDescriptor = null, bool hasModelEvidence = false)
        {
            Error = error; Kind = kind; SourceId = sourceId; Confidence = confidence;
            ModelKey = modelKey; SerialKey = serialKey; DeviceIdentity = deviceIdentity;
            IdentityDescriptor = identityDescriptor; HasModelEvidence = hasModelEvidence;
        }
    }

    // Pure single-descriptor resolution. No device polling, registry, calibration,
    // profile allocation, persistence, live-serial collision detection or Unity types.
    public static class InputDeviceIdentity
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static InputIdentityResult Resolve(InputDeviceDescriptor descriptor, RuntimeSourceHandle handle)
        {
            if (descriptor == null) return Invalid(InputIdentityError.MissingDescriptor);
            if (handle == null) return Invalid(InputIdentityError.MissingHandle);
            if (descriptor.Error != InputIdentityError.None) return Invalid(descriptor.Error);
            if (!TryHardwareKind(handle.Kind, out var kindName)) return Invalid(InputIdentityError.UnsupportedKind);
            if (descriptor.Kind != handle.Kind) return Invalid(InputIdentityError.KindMismatch);
            if (!ValidRunId(handle.RunId)) return Invalid(InputIdentityError.InvalidRunId);
            if (handle.RuntimeDeviceId <= 0) return Invalid(InputIdentityError.InvalidRuntimeDeviceId);
            if (handle.SourceGeneration <= 0) return Invalid(InputIdentityError.InvalidSourceGeneration);

            string sourceId = kindName + ":" + handle.RunId + ":" +
                handle.RuntimeDeviceId.ToString("D10", CultureInfo.InvariantCulture) + ":" +
                handle.SourceGeneration.ToString("D10", CultureInfo.InvariantCulture);
            var confidence = descriptor.HasModelEvidence
                ? (descriptor.SerialKey.Length != 0 ? IdentityConfidence.ReportedSerial : IdentityConfidence.ModelOnly)
                : IdentityConfidence.SessionOnly;
            string deviceIdentity = confidence == IdentityConfidence.ReportedSerial
                ? descriptor.SerialKey : "session:" + sourceId;
            return new InputIdentityResult(InputIdentityError.None, handle.Kind, sourceId, confidence,
                descriptor.ModelKey, descriptor.SerialKey, deviceIdentity, descriptor.IdentityDescriptor,
                descriptor.HasModelEvidence);
        }

        private static InputIdentityResult Invalid(InputIdentityError error) => new InputIdentityResult(error);

        private static bool ValidRunId(string runId)
        {
            if (runId == null || runId.Length != 32) return false;
            foreach (char value in runId)
                if (!((value >= '0' && value <= '9') || (value >= 'a' && value <= 'f'))) return false;
            return true;
        }

        internal static bool TryHardwareKind(FlightInputSourceKind kind, out string kindName)
        {
            switch (kind)
            {
                case FlightInputSourceKind.Keyboard: kindName = "keyboard"; return true;
                case FlightInputSourceKind.Gamepad: kindName = "gamepad"; return true;
                case FlightInputSourceKind.Joystick: kindName = "joystick"; return true;
                default: kindName = string.Empty; return false;
            }
        }

        internal static string Normalize(string value)
        {
            string normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormC).Trim();
            StrictUtf8.GetByteCount(normalized);
            return normalized;
        }

        internal static string Decimal(int? value) => value.HasValue
            ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;

        internal static string HashFields(string[] fields)
        {
            using (var stream = new MemoryStream())
            {
                foreach (string field in fields)
                {
                    byte[] value = StrictUtf8.GetBytes(field);
                    byte[] prefix = StrictUtf8.GetBytes(value.Length.ToString(CultureInfo.InvariantCulture) + ":");
                    stream.Write(prefix, 0, prefix.Length);
                    stream.Write(value, 0, value.Length);
                }
                return HashBytes(stream.ToArray());
            }
        }

        internal static string HashUtf8(string value) => HashBytes(StrictUtf8.GetBytes(value));

        private static string HashBytes(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
            {
                var result = new StringBuilder(64);
                foreach (byte value in algorithm.ComputeHash(bytes))
                    result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }
    }
}
