using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SensCalibr8.Core.Configuration;
using SensCalibr8.Core.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace SensCalibr8.TestLogic
{
    public sealed class FrozenArenaGeometry
    {
        private FrozenArenaGeometry(Vector3 cameraPosition, float verticalFov, float nearClip, float farClip, float referenceAspect,
            Vector3 arenaCenter, Vector3 arenaDimensions, float checkerCell, Color targetColor,
            IReadOnlyDictionary<string, float> targetDiameters, float crosshairDiameter, float hudReserve)
        { CameraPosition = cameraPosition; VerticalFov = verticalFov; NearClip = nearClip; FarClip = farClip; ReferenceAspect = referenceAspect;
          ArenaCenter = arenaCenter; ArenaDimensions = arenaDimensions; CheckerCell = checkerCell; TargetColor = targetColor;
          TargetDiameters = targetDiameters; CrosshairDiameter = crosshairDiameter; HudReserve = hudReserve; }
        public Vector3 CameraPosition { get; } public float VerticalFov { get; } public float NearClip { get; } public float FarClip { get; } public float ReferenceAspect { get; }
        public Vector3 ArenaCenter { get; } public Vector3 ArenaDimensions { get; } public float CheckerCell { get; }
        public Color TargetColor { get; } public IReadOnlyDictionary<string, float> TargetDiameters { get; }
        public float CrosshairDiameter { get; } public float HudReserve { get; }
        public static FrozenArenaGeometry From(FrozenCalibrationConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            try
            {
                using JsonDocument d = JsonDocument.Parse(configuration.Record.TargetGeometryJson); JsonElement r = d.RootElement;
                if (!string.Equals(r.GetProperty("test_geometry_version").GetString(), configuration.Record.TestGeometryVersion, StringComparison.Ordinal) ||
                    !string.Equals(r.GetProperty("status").GetString(), "accepted", StringComparison.Ordinal)) throw new InvalidDataException("Accepted frozen geometry is required.");
                JsonElement c=r.GetProperty("camera"), a=r.GetProperty("arena"), t=r.GetProperty("target"), x=r.GetProperty("crosshair"), s=r.GetProperty("spawn_safety"), v=r.GetProperty("reference_viewport");
                if (!a.GetProperty("enclosed").GetBoolean() || !string.Equals(a.GetProperty("lighting").GetString(),"unlit-fixed",StringComparison.Ordinal) || a.GetProperty("shadows").GetBoolean() ||
                    !string.Equals(t.GetProperty("shape").GetString(),"sphere",StringComparison.Ordinal) || !string.Equals(x.GetProperty("style").GetString(),"filled-dot",StringComparison.Ordinal)) throw new InvalidDataException("Frozen geometry is unsupported.");
                var sizes=new Dictionary<string,float>(StringComparer.Ordinal); foreach (JsonProperty p in t.GetProperty("sizes").EnumerateObject()) sizes.Add(p.Name,(float)p.Value.GetProperty("world_diameter").GetDouble());
                if (sizes.Count == 0 || !ColorUtility.TryParseHtmlString(t.GetProperty("color_hex").GetString(), out Color color)) throw new InvalidDataException("Target geometry is incomplete.");
                return new FrozenArenaGeometry(V3(c.GetProperty("position_world")), (float)c.GetProperty("vertical_fov_deg").GetDouble(), (float)c.GetProperty("near_clip_world").GetDouble(), (float)c.GetProperty("far_clip_world").GetDouble(), (float)v.GetProperty("aspect_ratio").GetDouble(), V3(a.GetProperty("center_world")), V3(a.GetProperty("dimensions_world")), (float)a.GetProperty("checker_cell_world").GetDouble(), color, sizes, (float)x.GetProperty("diameter_px").GetDouble(), (float)s.GetProperty("hud_reserved_top_px").GetDouble());
            } catch (JsonException e) { throw new InvalidDataException("target_geometry_json is invalid.",e); }
        }
        private static Vector3 V3(JsonElement e) { return new Vector3((float)e[0].GetDouble(),(float)e[1].GetDouble(),(float)e[2].GetDouble()); }
    }

    public static class LetterboxedViewport
    {
        public static Rect Calculate(float screenWidth, float screenHeight, float desiredAspect)
        {
            if (screenWidth <= 0 || screenHeight <= 0) throw new ArgumentOutOfRangeException();
            if (desiredAspect <= 0f) throw new ArgumentOutOfRangeException(nameof(desiredAspect)); float actual = screenWidth / screenHeight;
            return actual > desiredAspect ? new Rect((1f - desiredAspect / actual) / 2f, 0f, desiredAspect / actual, 1f) : new Rect(0f, (1f - actual / desiredAspect) / 2f, 1f, actual / desiredAspect);
        }
    }

    public sealed class CalibratedArenaRuntime : MonoBehaviour
    {
        private readonly List<GameObject> arenaObjects = new List<GameObject>(); private UnityFramePolicyScope frameScope; private Camera arenaCamera; private FrozenArenaGeometry geometry;
        public ArenaTargetService Targets { get; private set; }
        public void Configure(FrozenCalibrationConfiguration configuration, string crosshairColor, bool adaptiveSyncConfirmedOff)
        {
            Shutdown(); geometry=FrozenArenaGeometry.From(configuration); frameScope=new UnityFramePolicyScope(FrozenFramePolicy.From(configuration),adaptiveSyncConfirmedOff);
            arenaCamera=GetComponent<Camera>() ?? gameObject.AddComponent<Camera>(); arenaCamera.transform.SetPositionAndRotation(geometry.CameraPosition,Quaternion.identity); arenaCamera.fieldOfView=geometry.VerticalFov; arenaCamera.nearClipPlane=geometry.NearClip; arenaCamera.farClipPlane=geometry.FarClip; arenaCamera.clearFlags=CameraClearFlags.SolidColor; arenaCamera.backgroundColor=Color.black;
            BuildRoom(); Targets=new GameObject("Arena Target Service").AddComponent<ArenaTargetService>(); Targets.Configure(geometry); var dot=new GameObject("Fixed Dot Crosshair").AddComponent<FixedDotCrosshair>(); dot.Configure(crosshairColor,geometry.CrosshairDiameter); var hud=new GameObject("Arena HUD").AddComponent<MinimalArenaHud>(); hud.Configure(geometry.HudReserve); new GameObject("First Person Feedback").AddComponent<MinimalFirstPersonFeedback>();
        }
        private void LateUpdate() { if (arenaCamera != null) arenaCamera.rect=LetterboxedViewport.Calculate(Screen.width,Screen.height,geometry.ReferenceAspect); }
        private void BuildRoom()
        { CreateFace("Floor",new Vector3(geometry.ArenaCenter.x,geometry.ArenaCenter.y-geometry.ArenaDimensions.y/2f,geometry.ArenaCenter.z),new Vector3(geometry.ArenaDimensions.x,geometry.CheckerCell,geometry.ArenaDimensions.z));
          CreateFace("Ceiling",new Vector3(geometry.ArenaCenter.x,geometry.ArenaCenter.y+geometry.ArenaDimensions.y/2f,geometry.ArenaCenter.z),new Vector3(geometry.ArenaDimensions.x,geometry.CheckerCell,geometry.ArenaDimensions.z));
          CreateFace("Left Wall",new Vector3(geometry.ArenaCenter.x-geometry.ArenaDimensions.x/2f,geometry.ArenaCenter.y,geometry.ArenaCenter.z),new Vector3(geometry.CheckerCell,geometry.ArenaDimensions.y,geometry.ArenaDimensions.z));
          CreateFace("Right Wall",new Vector3(geometry.ArenaCenter.x+geometry.ArenaDimensions.x/2f,geometry.ArenaCenter.y,geometry.ArenaCenter.z),new Vector3(geometry.CheckerCell,geometry.ArenaDimensions.y,geometry.ArenaDimensions.z));
          CreateFace("Front Wall",new Vector3(geometry.ArenaCenter.x,geometry.ArenaCenter.y,geometry.ArenaCenter.z-geometry.ArenaDimensions.z/2f),new Vector3(geometry.ArenaDimensions.x,geometry.ArenaDimensions.y,geometry.CheckerCell));
          CreateFace("Back Wall",new Vector3(geometry.ArenaCenter.x,geometry.ArenaCenter.y,geometry.ArenaCenter.z+geometry.ArenaDimensions.z/2f),new Vector3(geometry.ArenaDimensions.x,geometry.ArenaDimensions.y,geometry.CheckerCell)); }
        private void CreateFace(string name, Vector3 position, Vector3 scale) { var o=GameObject.CreatePrimitive(PrimitiveType.Cube); o.name=name; o.transform.SetPositionAndRotation(position,Quaternion.identity); o.transform.localScale=scale; var r=o.GetComponent<Renderer>(); r.sharedMaterial=CheckerboardMaterial(name,scale,geometry.CheckerCell); r.shadowCastingMode=ShadowCastingMode.Off; r.receiveShadows=false; arenaObjects.Add(o); }
        private static Material CheckerboardMaterial(string name, Vector3 scale, float cell) { var texture=new Texture2D(2,2); texture.SetPixels(new[]{Color.white,Color.gray,Color.gray,Color.white}); texture.Apply(); var m=new Material(Shader.Find("Unlit/Texture")); m.name=name+" Gray Checkerboard"; m.mainTexture=texture; m.mainTextureScale=new Vector2(scale.x/cell,scale.y/cell); return m; }
        public void Shutdown() { if (Targets != null) Destroy(Targets.gameObject); Targets=null; foreach(var o in arenaObjects) if(o!=null) Destroy(o); arenaObjects.Clear(); frameScope?.Dispose(); frameScope=null; }
        private void OnDestroy() => Shutdown();
    }

    public sealed class ArenaTargetService : MonoBehaviour
    { private FrozenArenaGeometry geometry; private GameObject target; public void Configure(FrozenArenaGeometry value) { geometry=value??throw new ArgumentNullException(nameof(value)); }
      public void Show(string size, Vector3 position) { if(geometry==null || !geometry.TargetDiameters.TryGetValue(size,out float diameter)) throw new ArgumentException("Unknown frozen target size.",nameof(size)); if(target==null){target=GameObject.CreatePrimitive(PrimitiveType.Sphere);target.name="Cyan Calibration Target";var r=target.GetComponent<Renderer>();r.sharedMaterial=new Material(Shader.Find("Unlit/Color")){color=geometry.TargetColor};r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;} target.transform.position=position;target.transform.localScale=Vector3.one*diameter;target.SetActive(true); }
      public void Hide(){if(target!=null)target.SetActive(false);} }

    public sealed class FixedDotCrosshair : MonoBehaviour { private Color color; private float diameter; public void Configure(string hex,float fixedDiameter) { if(!CrosshairPalette.IsSupported(hex)||!ColorUtility.TryParseHtmlString(hex,out color))throw new ArgumentException("An approved profile crosshair color is required.",nameof(hex)); diameter=fixedDiameter; } private void OnGUI(){if(diameter<=0)return;float x=(Screen.width-diameter)/2f,y=(Screen.height-diameter)/2f;var prior=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(x,y,diameter,diameter),Texture2D.whiteTexture);GUI.color=prior;} }
    public sealed class MinimalArenaHud : MonoBehaviour { private float topReserve; public void Configure(float value){topReserve=value;} private void OnGUI(){if(topReserve<=0)return;GUI.Label(new Rect(0,0,Screen.width,topReserve),"Progress: --    Timer: --    Accuracy: --");} }
    /// <summary>Visual-only first-person feedback. The authoritative shot path is supplied by a mode in Phase 4.</summary>
    public sealed class MinimalFirstPersonFeedback : MonoBehaviour { private bool shotFeedback; public void PlayShotFeedback(){shotFeedback=true;StartCoroutine(ClearAfterRender());} private void Update(){if(Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame)PlayShotFeedback();} private IEnumerator ClearAfterRender(){yield return new WaitForEndOfFrame();shotFeedback=false;} private void OnGUI(){var prior=GUI.color;GUI.color=Color.gray;GUI.DrawTexture(new Rect(Screen.width/2f,Screen.height-32f,80f,20f),Texture2D.whiteTexture);if(shotFeedback){GUI.color=Color.white;GUI.DrawTexture(new Rect(Screen.width/2f-2f,Screen.height/2f-2f,4f,4f),Texture2D.whiteTexture);}GUI.color=prior;} }
}
