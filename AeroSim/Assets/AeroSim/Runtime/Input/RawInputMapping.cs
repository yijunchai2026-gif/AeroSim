using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AeroSim.Input
{
    public enum RawControlValueKind { AxisFloat, ButtonFloat, Other }
    public enum RawAxisClamp { None, BeforeNormalize, AfterNormalize, ToConstantBeforeNormalize }
    public enum RawMappingValidationStatus { BuiltInValid, JoystickCandidate, Missing, Rejected }
    public enum RawMappingError
    {
        None, UnsupportedBuiltInKind, UnsupportedHardwareKind, MappingNotEditable, MappingMissing,
        InvalidLayout, MissingCatalog, MissingControlDescriptor, InvalidCatalogPath,
        UnsupportedControlValueKind, DuplicateCatalogPath, InvalidMappingPath, DuplicateMappedPath,
        AxisNotFound, ButtonAxisNotAllowed, AxisTypeMismatch, InvalidControlType, InvalidStateFormat,
        InvalidStateSize, MissingPreprocess, UnsupportedClamp, NonFinitePreprocess,
        InvalidClampRange, InvalidNormalizeRange
    }

    // Only four device-relative canonical paths. No indices or validation authorization.
    public sealed class AxisMapping
    {
        public string ThrottlePath { get; }
        public string YawPath { get; }
        public string PitchPath { get; }
        public string RollPath { get; }

        public AxisMapping(string throttlePath, string yawPath, string pitchPath, string rollPath)
        { ThrottlePath = throttlePath; YawPath = yawPath; PitchPath = pitchPath; RollPath = rollPath; }
    }

    // Declared metadata, not a Unity object or proof of device ownership/type.
    public sealed class RawControlDescriptor
    {
        public string Path { get; }
        public RawControlValueKind ValueKind { get; }
        public string ControlTypeFullName { get; }
        public uint StateFormatCode { get; }
        public uint SizeInBits { get; }
        public RawAxisPreprocess Preprocess { get; }

        public RawControlDescriptor(string path, RawControlValueKind valueKind,
            string controlTypeFullName, uint stateFormatCode, uint sizeInBits, RawAxisPreprocess preprocess)
        {
            Path = path; ValueKind = valueKind; ControlTypeFullName = controlTypeFullName;
            StateFormatCode = stateFormatCode; SizeInBits = sizeInBits; Preprocess = preprocess;
        }
    }

    // Captures layout-level configuration; this module never executes preprocessing.
    public sealed class RawAxisPreprocess
    {
        public RawAxisClamp Clamp { get; }
        public float ClampMin { get; }
        public float ClampMax { get; }
        public float ClampConstant { get; }
        public bool Normalize { get; }
        public float NormalizeMin { get; }
        public float NormalizeMax { get; }
        public float NormalizeZero { get; }
        public bool Invert { get; }
        public bool Scale { get; }
        public float ScaleFactor { get; }

        public RawAxisPreprocess(RawAxisClamp clamp, float clampMin, float clampMax,
            float clampConstant, bool normalize, float normalizeMin, float normalizeMax,
            float normalizeZero, bool invert, bool scale, float scaleFactor)
        {
            Clamp = clamp; ClampMin = clampMin; ClampMax = clampMax; ClampConstant = clampConstant;
            Normalize = normalize; NormalizeMin = normalizeMin; NormalizeMax = normalizeMax;
            NormalizeZero = normalizeZero; Invert = invert; Scale = scale; ScaleFactor = scaleFactor;
        }
    }

    public sealed class RawMappingDiagnostic
    {
        public RawMappingError Error { get; }
        public int AxisIndex { get; }
        public string Path { get; }
        public string Field { get; }

        internal RawMappingDiagnostic(RawMappingError error, int axisIndex, string path, string field)
        { Error = error; AxisIndex = axisIndex; Path = path; Field = field; }
    }

    public sealed class RawMappingValidationResult
    {
        public RawMappingValidationStatus Status { get; }
        // Structural validity is not live UserValidated or SourceAvailable authorization.
        public bool IsValid => Status == RawMappingValidationStatus.BuiltInValid ||
            Status == RawMappingValidationStatus.JoystickCandidate;
        public FlightInputSourceKind Kind { get; }
        public string RawConvention { get; }
        public AxisMapping Mapping { get; }
        public IReadOnlyList<RawControlDescriptor> Controls { get; }
        public string MappingFingerprint { get; }
        public IReadOnlyList<RawMappingDiagnostic> Diagnostics { get; }

        internal RawMappingValidationResult(RawMappingValidationStatus status, FlightInputSourceKind kind,
            string convention, AxisMapping mapping, RawControlDescriptor[] controls, string fingerprint,
            List<RawMappingDiagnostic> diagnostics)
        {
            Status = status; Kind = kind; RawConvention = convention; Mapping = mapping;
            Controls = Array.AsReadOnly((RawControlDescriptor[])controls.Clone());
            MappingFingerprint = fingerprint;
            diagnostics.Sort(CompareDiagnostic);
            var distinct = new List<RawMappingDiagnostic>();
            foreach (var diagnostic in diagnostics)
                if (distinct.Count == 0 || CompareDiagnostic(distinct[distinct.Count - 1], diagnostic) != 0)
                    distinct.Add(diagnostic);
            Diagnostics = Array.AsReadOnly(distinct.ToArray());
        }

        private static int CompareDiagnostic(RawMappingDiagnostic a, RawMappingDiagnostic b)
        {
            int order = Stage(a.Error).CompareTo(Stage(b.Error));
            if (order == 0) order = a.AxisIndex.CompareTo(b.AxisIndex);
            if (order == 0) order = StringComparer.Ordinal.Compare(a.Path, b.Path);
            if (order == 0) order = StringComparer.Ordinal.Compare(a.Field, b.Field);
            return order == 0 ? a.Error.CompareTo(b.Error) : order;
        }

        private static int Stage(RawMappingError error)
        {
            if (error <= RawMappingError.MappingNotEditable) return 1;
            if (error == RawMappingError.MappingMissing) return 2;
            if (error == RawMappingError.InvalidLayout) return 3;
            if (error <= RawMappingError.DuplicateCatalogPath) return 4;
            if (error <= RawMappingError.DuplicateMappedPath) return 5;
            return 6;
        }
    }

    // Pure, stateless metadata validation and fingerprinting only. No polling, raw
    // values, sample/cache, filters, calibration, identity binding, file IO or Unity.
    public static class RawInputMapping
    {
        private const string HardwareConvention = "unity-unprocessed-control-v1";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static RawMappingValidationResult GetBuiltIn(FlightInputSourceKind kind)
        {
            string convention, binding;
            if (kind == FlightInputSourceKind.Keyboard)
            { convention = "logical-key-state-v1"; binding = "keyboard-bindings-v1"; }
            else if (kind == FlightInputSourceKind.Touch)
            { convention = "touch-normalized-v1"; binding = "dual-stick-typr-v1"; }
            else return Failure(kind, RawMappingError.UnsupportedBuiltInKind, "kind");

            string name = kind == FlightInputSourceKind.Keyboard ? "keyboard" : "touch";
            string fingerprint = InputDeviceIdentity.HashFields(new[] { "mapping-v1", name, convention, binding });
            return new RawMappingValidationResult(RawMappingValidationStatus.BuiltInValid, kind, convention,
                null, new RawControlDescriptor[0], fingerprint, new List<RawMappingDiagnostic>());
        }

        public static RawMappingValidationResult ValidateHardware(FlightInputSourceKind kind, string layout,
            IReadOnlyList<RawControlDescriptor> catalog, AxisMapping requestedMapping = null)
        {
            if (kind != FlightInputSourceKind.Joystick && kind != FlightInputSourceKind.Gamepad)
                return Failure(kind, RawMappingError.UnsupportedHardwareKind, "kind");
            if (kind == FlightInputSourceKind.Gamepad && requestedMapping != null)
                return Failure(kind, RawMappingError.MappingNotEditable, "mapping");
            if (kind == FlightInputSourceKind.Joystick && requestedMapping == null)
                return Failure(kind, RawMappingError.MappingMissing, "mapping", RawMappingValidationStatus.Missing);
            if (!ValidLayout(layout)) return Failure(kind, RawMappingError.InvalidLayout, "layout");
            if (catalog == null) return Failure(kind, RawMappingError.MissingCatalog, "catalog");

            var copiedCatalog = new RawControlDescriptor[catalog.Count];
            for (int index = 0; index < copiedCatalog.Length; index++) copiedCatalog[index] = catalog[index];
            var diagnostics = new List<RawMappingDiagnostic>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var byPath = new Dictionary<string, RawControlDescriptor>(StringComparer.Ordinal);
            foreach (var control in copiedCatalog)
            {
                if (control == null)
                { Add(diagnostics, RawMappingError.MissingControlDescriptor, -1, "", "catalog"); continue; }
                bool validPath = ValidPath(control.Path);
                if (!validPath) Add(diagnostics, RawMappingError.InvalidCatalogPath, -1, "", "path");
                if (!DeclaredValueKind(control.ValueKind))
                    Add(diagnostics, RawMappingError.UnsupportedControlValueKind, -1,
                        validPath ? control.Path : "", "valueKind");
                if (!validPath) continue;
                counts.TryGetValue(control.Path, out int count);
                counts[control.Path] = count + 1;
                if (count == 0) byPath.Add(control.Path, control);
            }
            foreach (var count in counts)
                if (count.Value > 1) Add(diagnostics, RawMappingError.DuplicateCatalogPath, -1, count.Key, "catalog");
            if (diagnostics.Count != 0) return Rejected(kind, diagnostics);

            var mapping = kind == FlightInputSourceKind.Gamepad
                ? new AxisMapping("leftStick/y", "leftStick/x", "rightStick/y", "rightStick/x")
                : requestedMapping;
            string[] paths = { mapping.ThrottlePath, mapping.YawPath, mapping.PitchPath, mapping.RollPath };
            counts.Clear();
            var validPaths = new bool[4];
            for (int axis = 0; axis < 4; axis++)
            {
                validPaths[axis] = ValidPath(paths[axis]);
                if (!validPaths[axis])
                { Add(diagnostics, RawMappingError.InvalidMappingPath, axis, "", "path"); continue; }
                counts.TryGetValue(paths[axis], out int count);
                counts[paths[axis]] = count + 1;
            }
            for (int axis = 0; axis < 4; axis++)
                if (validPaths[axis] && counts[paths[axis]] > 1)
                    Add(diagnostics, RawMappingError.DuplicateMappedPath, axis, paths[axis], "path");
            if (diagnostics.Count != 0) return Rejected(kind, diagnostics);

            var selected = new RawControlDescriptor[4];
            for (int axis = 0; axis < 4; axis++)
            {
                string path = paths[axis];
                if (!byPath.TryGetValue(path, out var control))
                { Add(diagnostics, RawMappingError.AxisNotFound, axis, path, "path"); continue; }
                if (control.ValueKind != RawControlValueKind.AxisFloat)
                {
                    Add(diagnostics, control.ValueKind == RawControlValueKind.ButtonFloat
                        ? RawMappingError.ButtonAxisNotAllowed : RawMappingError.AxisTypeMismatch,
                        axis, path, "valueKind");
                    continue;
                }
                selected[axis] = control;
                ValidateMetadata(control, axis, diagnostics);
            }
            if (diagnostics.Count != 0) return Rejected(kind, diagnostics);

            string fingerprint = HardwareFingerprint(layout, paths, selected);
            return new RawMappingValidationResult(kind == FlightInputSourceKind.Gamepad
                ? RawMappingValidationStatus.BuiltInValid : RawMappingValidationStatus.JoystickCandidate,
                kind, HardwareConvention, mapping, selected, fingerprint, diagnostics);
        }

        private static void ValidateMetadata(RawControlDescriptor control, int axis,
            List<RawMappingDiagnostic> diagnostics)
        {
            string path = control.Path;
            if (!ValidControlType(control.ControlTypeFullName))
                Add(diagnostics, RawMappingError.InvalidControlType, axis, path, "controlTypeFullName");
            if (control.StateFormatCode == 0)
                Add(diagnostics, RawMappingError.InvalidStateFormat, axis, path, "stateFormatCode");
            if (control.SizeInBits == 0)
                Add(diagnostics, RawMappingError.InvalidStateSize, axis, path, "sizeInBits");
            var preprocessing = control.Preprocess;
            if (preprocessing == null)
            { Add(diagnostics, RawMappingError.MissingPreprocess, axis, path, "preprocess"); return; }
            bool declaredClamp = DeclaredClamp(preprocessing.Clamp);
            if (!declaredClamp) Add(diagnostics, RawMappingError.UnsupportedClamp, axis, path, "clamp");
            FiniteField(preprocessing.ClampMin, "clampMin", axis, path, diagnostics);
            FiniteField(preprocessing.ClampMax, "clampMax", axis, path, diagnostics);
            FiniteField(preprocessing.ClampConstant, "clampConstant", axis, path, diagnostics);
            FiniteField(preprocessing.NormalizeMin, "normalizeMin", axis, path, diagnostics);
            FiniteField(preprocessing.NormalizeMax, "normalizeMax", axis, path, diagnostics);
            FiniteField(preprocessing.NormalizeZero, "normalizeZero", axis, path, diagnostics);
            FiniteField(preprocessing.ScaleFactor, "scaleFactor", axis, path, diagnostics);
            if (declaredClamp && preprocessing.Clamp != RawAxisClamp.None &&
                Finite(preprocessing.ClampMin) && Finite(preprocessing.ClampMax) &&
                preprocessing.ClampMin > preprocessing.ClampMax)
                Add(diagnostics, RawMappingError.InvalidClampRange, axis, path, "clampRange");
            if (preprocessing.Normalize &&
                ((Finite(preprocessing.NormalizeMin) && Finite(preprocessing.NormalizeMax) &&
                  preprocessing.NormalizeMin >= preprocessing.NormalizeMax) ||
                 (Finite(preprocessing.NormalizeZero) && Finite(preprocessing.NormalizeMax) &&
                  preprocessing.NormalizeZero >= preprocessing.NormalizeMax)))
                Add(diagnostics, RawMappingError.InvalidNormalizeRange, axis, path, "normalizeRange");
        }

        private static string HardwareFingerprint(string layout, string[] paths, RawControlDescriptor[] controls)
        {
            var fields = new List<string> { "mapping-v1", layout, HardwareConvention };
            fields.AddRange(paths);
            foreach (var control in controls)
            {
                var p = control.Preprocess;
                fields.Add(control.ControlTypeFullName);
                fields.Add(control.StateFormatCode.ToString(CultureInfo.InvariantCulture));
                fields.Add(control.SizeInBits.ToString(CultureInfo.InvariantCulture));
                fields.Add(ClampCode(p.Clamp));
                fields.Add(FloatCode(p.ClampMin)); fields.Add(FloatCode(p.ClampMax));
                fields.Add(FloatCode(p.ClampConstant)); fields.Add(p.Normalize ? "1" : "0");
                fields.Add(FloatCode(p.NormalizeMin)); fields.Add(FloatCode(p.NormalizeMax));
                fields.Add(FloatCode(p.NormalizeZero)); fields.Add(p.Invert ? "1" : "0");
                fields.Add(p.Scale ? "1" : "0"); fields.Add(FloatCode(p.ScaleFactor));
            }
            return InputDeviceIdentity.HashFields(fields.ToArray());
        }

        private static string FloatCode(float value) => value == 0 ? "0" : value.ToString("R", CultureInfo.InvariantCulture);
        private static string ClampCode(RawAxisClamp clamp)
        {
            switch (clamp)
            {
                case RawAxisClamp.BeforeNormalize: return "before_normalize";
                case RawAxisClamp.AfterNormalize: return "after_normalize";
                case RawAxisClamp.ToConstantBeforeNormalize: return "to_constant_before_normalize";
                default: return "none";
            }
        }

        private static bool DeclaredClamp(RawAxisClamp clamp) => clamp == RawAxisClamp.None ||
            clamp == RawAxisClamp.BeforeNormalize || clamp == RawAxisClamp.AfterNormalize ||
            clamp == RawAxisClamp.ToConstantBeforeNormalize;
        private static bool DeclaredValueKind(RawControlValueKind kind) => kind == RawControlValueKind.AxisFloat ||
            kind == RawControlValueKind.ButtonFloat || kind == RawControlValueKind.Other;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void FiniteField(float value, string field, int axis, string path,
            List<RawMappingDiagnostic> diagnostics)
        { if (!Finite(value)) Add(diagnostics, RawMappingError.NonFinitePreprocess, axis, path, field); }

        private static bool Utf8Text(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            try { StrictUtf8.GetByteCount(value); return true; }
            catch (ArgumentException) { return false; }
        }

        private static bool ValidPath(string path)
        {
            if (!Utf8Text(path) || path[0] == '/' || path[path.Length - 1] == '/') return false;
            foreach (char value in path)
                if (char.IsWhiteSpace(value) || char.IsControl(value) ||
                    "*?{}<>[]:\\".IndexOf(value) >= 0) return false;
            foreach (string segment in path.Split('/'))
                if (segment.Length == 0 || segment == "." || segment == "..") return false;
            return true;
        }

        private static bool ValidLayout(string layout)
        {
            if (!Utf8Text(layout) || char.IsWhiteSpace(layout[0]) || char.IsWhiteSpace(layout[layout.Length - 1])) return false;
            foreach (char value in layout) if (char.IsControl(value)) return false;
            return true;
        }

        private static bool ValidControlType(string type)
        {
            if (!Utf8Text(type)) return false;
            foreach (char value in type)
                if (char.IsWhiteSpace(value) || char.IsControl(value) || ",/\\=:".IndexOf(value) >= 0) return false;
            return true;
        }

        private static void Add(List<RawMappingDiagnostic> diagnostics, RawMappingError error,
            int axis, string path, string field) =>
            diagnostics.Add(new RawMappingDiagnostic(error, axis, path, field));

        private static RawMappingValidationResult Failure(FlightInputSourceKind kind, RawMappingError error,
            string field, RawMappingValidationStatus status = RawMappingValidationStatus.Rejected) =>
            Failed(kind, status, new List<RawMappingDiagnostic> { new RawMappingDiagnostic(error, -1, "", field) });
        private static RawMappingValidationResult Rejected(FlightInputSourceKind kind,
            List<RawMappingDiagnostic> diagnostics) => Failed(kind, RawMappingValidationStatus.Rejected, diagnostics);
        private static RawMappingValidationResult Failed(FlightInputSourceKind kind, RawMappingValidationStatus status,
            List<RawMappingDiagnostic> diagnostics) => new RawMappingValidationResult(status, kind, "",
                null, new RawControlDescriptor[0], "", diagnostics);
    }
}
