#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AeroSim.Editor.ReviewMcp
{
    internal static class AeroSimReviewCapture
    {
        internal static bool TryCapture(ReviewRequest request, string runDirectory, out string artifact, out string message)
        {
            artifact = null;
            message = null;

            Camera camera;
            switch (request.tool)
            {
                case "capture_scene_view":
                    SceneView view = SceneView.lastActiveSceneView;
                    camera = view != null ? view.camera : null;
                    if (camera == null)
                    {
                        message = "No active SceneView camera is available.";
                        return false;
                    }
                    break;
                case "capture_game_view":
                    camera = Camera.main != null ? Camera.main : FindEnabledCamera();
                    if (camera == null)
                    {
                        message = "No enabled scene Camera is available. Game View capture uses an engine Camera RenderTexture, not a desktop screenshot.";
                        return false;
                    }
                    break;
                case "capture_camera":
                    string cameraPath = request.capture != null ? request.capture.cameraPath : null;
                    camera = AeroSimReviewInspection.ResolveCamera(
                        string.IsNullOrEmpty(cameraPath) ? request.target : cameraPath);
                    if (camera == null)
                    {
                        message = "The requested camera could not be resolved.";
                        return false;
                    }
                    break;
                default:
                    message = "NOT_SUPPORTED: this capture operation is not implemented.";
                    return false;
            }

            int width = Mathf.Clamp(request.capture != null ? request.capture.width : 1920, 64, 8192);
            int height = Mathf.Clamp(request.capture != null ? request.capture.height : 1080, 64, 8192);
            string fileName = request.tool + ".png";
            string outputPath = Path.Combine(runDirectory, fileName);

            RenderTexture renderTexture = null;
            Texture2D texture = null;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                renderTexture.name = "AeroSimReviewCapture";
                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                texture.Apply(false, false);
                WriteBytesAtomically(outputPath, texture.EncodeToPNG());
                artifact = fileName;
                message = request.tool == "capture_game_view" && request.capture != null && request.capture.includeUi
                    ? "Captured from the selected scene Camera RenderTexture. Screen Space Overlay UI is not representable by Camera.Render and may be absent."
                    : "Captured from an engine Camera RenderTexture; no desktop or OS screen capture was used.";
                return true;
            }
            catch (Exception exception)
            {
                message = "Capture failed: " + exception.Message;
                return false;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                if (renderTexture != null)
                {
                    RenderTexture.ReleaseTemporary(renderTexture);
                }
            }
        }

        internal static Camera FindEnabledCamera()
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>(true);
            foreach (Camera camera in cameras)
            {
                if (camera != null && camera.enabled && camera.gameObject.activeInHierarchy)
                {
                    return camera;
                }
            }
            return cameras.Length > 0 ? cameras[0] : null;
        }

        internal static void WriteBytesAtomically(string path, byte[] bytes)
        {
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("Output directory is invalid.");
            }
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
    }
}
#endif
