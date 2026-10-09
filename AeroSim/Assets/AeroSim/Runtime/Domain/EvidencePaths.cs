using System;
using System.Globalization;
using System.IO;

namespace AeroSim.Domain
{
    // All evidence viewers/exporters share this policy. A local or imported JSON record
    // is not authority to read/write arbitrary files, including files reached through links.
    public static class EvidencePaths
    {
        public static void ValidateSessionId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 128) throw new ArgumentException("Invalid session ID.");
            foreach (char c in id)
                if (!IsAsciiLetterOrDigit(c) && c != '-' && c != '_') throw new ArgumentException("Invalid session ID.");
        }

        public static string ResolveOutput(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || fileName.Length > 240 || !IsAsciiLetterOrDigit(fileName[0]) || Path.GetFileName(fileName) != fileName)
                throw new ArgumentException("Invalid output file name.");
            foreach (char c in fileName)
                if (!IsAsciiLetterOrDigit(c) && c != '-' && c != '_' && c != '.') throw new ArgumentException("Invalid output file name.");
            if (fileName.Contains("..") || fileName.EndsWith(".", StringComparison.Ordinal)) throw new ArgumentException("Invalid output file name.");
            string directory = Path.GetFullPath(LocalStore.DirectoryPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(directory, fileName));
            if (!path.StartsWith(directory, StringComparison.Ordinal)) throw new ArgumentException("Output must remain in the local record directory.");
            try
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                    throw new IOException("Symbolic links and directories cannot be used as record files.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            return path;
        }

        public static string ResolvePhoto(SessionRecord record, PhotoEvidence evidence)
        {
            if (record == null || evidence == null || evidence.status != "saved" || evidence.sessionId != record.id || string.IsNullOrEmpty(evidence.fileName)) return null;
            try
            {
                ValidateSessionId(record.id);
                if (!evidence.fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || evidence.sequence < 1) return null;
                string expected;
                if (!string.IsNullOrEmpty(evidence.id))
                {
                    if (!Guid.TryParseExact(evidence.id, "N", out _)) return null;
                    expected = record.id + "-photo-" + evidence.sequence.ToString(CultureInfo.InvariantCulture) + "-" + evidence.id + ".png";
                }
                else expected = record.id + "-photo-" + evidence.sequence.ToString(CultureInfo.InvariantCulture) + ".png"; // Legitimate pre-metadata PNG convention.
                if (!string.Equals(expected, evidence.fileName, StringComparison.OrdinalIgnoreCase)) return null;
                string path = ResolveOutput(evidence.fileName);
                return File.Exists(path) ? path : null;
            }
            catch (ArgumentException) { return null; }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private static bool IsAsciiLetterOrDigit(char c) => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9';
    }
}
