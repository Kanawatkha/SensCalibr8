using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SensCalibr8.Calibration.Editor
{
    public static class P0R4ReferenceRenderer
    {
        private const int Width = 1920;
        private const int Height = 1080;

        public static void RenderReference()
        {
            string evidencePath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "..",
                "evidence",
                "p0-r4",
                "p0-r4-geometry-derived-v1.json"));
            string outputPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "..",
                "evidence",
                "p0-r4",
                "p0-r4-reference-render-v1.png"));
            if (!File.Exists(evidencePath))
            {
                throw new FileNotFoundException("Missing P0-R4 geometry evidence", evidencePath);
            }
            if (File.Exists(outputPath))
            {
                throw new IOException("Refusing to overwrite P0-R4 reference render: " + outputPath);
            }

            GeometryEvidence evidence = JsonUtility.FromJson<GeometryEvidence>(
                File.ReadAllText(evidencePath));
            if (evidence == null || !evidence.accepted)
            {
                throw new InvalidOperationException("P0-R4 mathematical evidence is not accepted");
            }

            List<UnityEngine.Object> created = new List<UnityEngine.Object>();
            RenderTexture renderTexture = null;
            Texture2D screenshot = null;
            try
            {
                Texture2D checker = CreateCheckerTexture();
                created.Add(checker);
                Material wallMaterial = CreateUnlitTextureMaterial(checker, new Vector2(10f, 6f));
                Material floorMaterial = CreateUnlitTextureMaterial(checker, new Vector2(10f, 10.5f));
                Material sideMaterial = CreateUnlitTextureMaterial(checker, new Vector2(10.5f, 6f));
                Material targetMaterial = CreateUnlitColorMaterial(new Color(0f, 1f, 1f, 1f));
                created.Add(wallMaterial);
                created.Add(floorMaterial);
                created.Add(sideMaterial);
                created.Add(targetMaterial);

                CreateRoomSurface("Back", new Vector3(0f, 1.6f, 20f), new Vector3(20f, 12f, 0.1f), wallMaterial, created);
                CreateRoomSurface("Floor", new Vector3(0f, -4.4f, 9.5f), new Vector3(20f, 0.1f, 21f), floorMaterial, created);
                CreateRoomSurface("Ceiling", new Vector3(0f, 7.6f, 9.5f), new Vector3(20f, 0.1f, 21f), floorMaterial, created);
                CreateRoomSurface("Left", new Vector3(-10f, 1.6f, 9.5f), new Vector3(0.1f, 12f, 21f), sideMaterial, created);
                CreateRoomSurface("Right", new Vector3(10f, 1.6f, 9.5f), new Vector3(0.1f, 12f, 21f), sideMaterial, created);

                double distance = evidence.derived.target_plane_distance_world;
                CreateTarget(-20f, 8f, distance, evidence.derived.target_geometry.small.world_diameter, targetMaterial, created);
                CreateTarget(0f, 0f, distance, evidence.derived.target_geometry.medium.world_diameter, targetMaterial, created);
                CreateTarget(20f, -8f, distance, evidence.derived.target_geometry.large.world_diameter, targetMaterial, created);
                CreateTarget(-40f, -18f, distance, evidence.derived.target_geometry.large.world_diameter, targetMaterial, created);
                CreateTarget(40f, 18f, distance, evidence.derived.target_geometry.small.world_diameter, targetMaterial, created);

                GameObject cameraObject = new GameObject("P0-R4 Reference Camera");
                created.Add(cameraObject);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 1.6f, 0f);
                camera.transform.rotation = Quaternion.identity;
                camera.fieldOfView = (float)evidence.derived.vertical_fov_deg;
                camera.aspect = 16f / 9f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 100f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);

                renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                renderTexture.antiAliasing = 1;
                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = renderTexture;
                screenshot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                screenshot.Apply();
                RenderTexture.active = previous;

                DrawOverlay(screenshot);
                File.WriteAllBytes(outputPath, screenshot.EncodeToPNG());
                Debug.Log("P0-R4 reference render written: " + outputPath);
                camera.targetTexture = null;
            }
            finally
            {
                if (screenshot != null)
                {
                    UnityEngine.Object.DestroyImmediate(screenshot);
                }
                if (renderTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
                for (int index = created.Count - 1; index >= 0; index--)
                {
                    if (created[index] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(created[index]);
                    }
                }
            }

            EditorApplication.Exit(0);
        }

        private static Texture2D CreateCheckerTexture()
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            Color light = new Color(0.62f, 0.62f, 0.62f, 1f);
            Color dark = new Color(0.26f, 0.26f, 0.26f, 1f);
            texture.SetPixels(new[] { light, dark, dark, light });
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.Apply();
            return texture;
        }

        private static Material CreateUnlitTextureMaterial(Texture texture, Vector2 scale)
        {
            Shader shader = Shader.Find("Unlit/Texture");
            if (shader == null)
            {
                throw new InvalidOperationException("Unlit/Texture shader is unavailable");
            }
            Material material = new Material(shader);
            material.mainTexture = texture;
            material.mainTextureScale = scale;
            return material;
        }

        private static Material CreateUnlitColorMaterial(Color color)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                throw new InvalidOperationException("Unlit/Color shader is unavailable");
            }
            Material material = new Material(shader);
            material.color = color;
            return material;
        }

        private static void CreateRoomSurface(
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            ICollection<UnityEngine.Object> created)
        {
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = name;
            surface.transform.position = position;
            surface.transform.localScale = scale;
            surface.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(surface.GetComponent<Collider>());
            created.Add(surface);
        }

        private static void CreateTarget(
            float horizontalAngleDegrees,
            float verticalAngleDegrees,
            double distance,
            double diameter,
            Material material,
            ICollection<UnityEngine.Object> created)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            float distanceValue = (float)distance;
            target.transform.position = new Vector3(
                distanceValue * Mathf.Tan(horizontalAngleDegrees * Mathf.Deg2Rad),
                1.6f + distanceValue * Mathf.Tan(verticalAngleDegrees * Mathf.Deg2Rad),
                distanceValue);
            target.transform.localScale = Vector3.one * (float)diameter;
            target.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(target.GetComponent<Collider>());
            created.Add(target);
        }

        private static void DrawOverlay(Texture2D texture)
        {
            Color32 hud = new Color32(20, 20, 20, 255);
            for (int y = Height - 64; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    texture.SetPixel(x, y, hud);
                }
            }

            Color32 guide = new Color32(70, 255, 120, 255);
            for (int x = 32; x < Width - 32; x++)
            {
                texture.SetPixel(x, 32, guide);
                texture.SetPixel(x, Height - 65, guide);
            }
            for (int y = 32; y < Height - 64; y++)
            {
                texture.SetPixel(32, y, guide);
                texture.SetPixel(Width - 33, y, guide);
            }

            Color32 crosshair = new Color32(255, 80, 255, 255);
            for (int y = Height / 2 - 2; y < Height / 2 + 2; y++)
            {
                for (int x = Width / 2 - 2; x < Width / 2 + 2; x++)
                {
                    texture.SetPixel(x, y, crosshair);
                }
            }
            texture.Apply();
        }

        [Serializable]
        private sealed class GeometryEvidence
        {
            public bool accepted;
            public DerivedGeometry derived;
        }

        [Serializable]
        private sealed class DerivedGeometry
        {
            public double vertical_fov_deg;
            public double target_plane_distance_world;
            public TargetGeometrySet target_geometry;
        }

        [Serializable]
        private sealed class TargetGeometrySet
        {
            public TargetGeometry small;
            public TargetGeometry medium;
            public TargetGeometry large;
        }

        [Serializable]
        private sealed class TargetGeometry
        {
            public double world_diameter;
        }
    }
}
