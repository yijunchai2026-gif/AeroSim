using System;
using System.IO;
using AeroSim.Domain;
using AeroSim.Simulation;
using UnityEngine;

namespace AeroSim
{
    public sealed partial class AeroSimApplication
    {
        // Render immediately on the main thread, before the accepted capture can be followed by
        // another capture, Finish, or navigation. No mutable current-session fields are read after a yield.
        private void SavePhotoEvidence(PhotoCaptureRequest request)
        {
            var camera = request?.Camera;
            if (request == null || request.Evidence.status != "pending") return;
            if (_sim == null || _sim.Record != request.Record || _sim.View != camera || camera == null || _sim.ReplayMode)
            {
                request.Owner.CompletePhotoCapture(request, false, "The capture session or camera is no longer active.");
                return;
            }
            RenderTexture texture = null;
            Texture2D image = null;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var previousPosition = camera.transform.position;
            var previousRotation = camera.transform.rotation;
            float previousFieldOfView = camera.fieldOfView;
            string temporaryPath = null;
            bool ownsTemporaryFile = false;
            bool saved = false;
            string failure = "";
            try
            {
                if (Path.GetFileName(request.FileName) != request.FileName || request.Record.id != request.SessionId)
                    throw new IOException("Invalid capture identity.");
                EvidencePaths.ValidateSessionId(request.SessionId);
                string path = EvidencePaths.ResolveOutput(request.FileName);
                temporaryPath = path + ".tmp";
                texture = new RenderTexture(1280, 720, 24) { name = "AeroSim photo evidence " + request.Evidence.id };
                if (!texture.Create()) throw new IOException("The photo render target could not be created.");
                camera.transform.SetPositionAndRotation(request.CameraPosition, request.CameraRotation);
                camera.fieldOfView = request.CameraFieldOfView;
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                var bytes = image.EncodeToPNG();
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { ownsTemporaryFile = true; stream.Write(bytes, 0, bytes.Length); stream.Flush(); }
                File.Move(temporaryPath, path); // Unique GUID identity; no overwrite of existing evidence.
                temporaryPath = null;
                saved = true;
                Debug.Log("Training photo saved: " + path);
            }
            catch (Exception ex)
            {
                failure = ex.Message;
                Debug.LogWarning("Training photo evidence was not saved: " + failure);
            }
            finally
            {
                if (camera != null)
                {
                    camera.targetTexture = previousTarget;
                    camera.transform.SetPositionAndRotation(previousPosition, previousRotation);
                    camera.fieldOfView = previousFieldOfView;
                }
                RenderTexture.active = previousActive;
                if (texture != null) { texture.Release(); Destroy(texture); }
                if (image != null) Destroy(image);
                if (ownsTemporaryFile && temporaryPath != null && File.Exists(temporaryPath))
                { try { File.Delete(temporaryPath); } catch (Exception cleanup) { Debug.LogWarning("Capture temporary file cleanup: " + cleanup.Message); } }
            }
            request.Owner.CompletePhotoCapture(request, saved, failure);
        }
    }
}
