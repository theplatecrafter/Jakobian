using System;
using UnityEngine;

namespace Jakobian.KSP
{
    /// <summary>Late camera endpoint constraint for Jakobian radial bodies, plus an optional human readout.</summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    [DefaultExecutionOrder(10000)]
    public sealed class FlightSurfaceContext : MonoBehaviour
    {
        internal static FlightSurfaceContext Instance;
        internal bool CameraGuardEnabled = true;
        internal int CameraCorrections;
        internal double LastCameraClearance;
        private bool visible = true, uiVisible = true, available;
        private string readout;
        private float nextReadout;
        private Transform correctedTransform, correctedParent;
        private Vector3 originalPosition, appliedPosition;
        private Quaternion originalRotation, appliedRotation;
        private int correctionFrame;
        private bool restorePending;

        private void Awake()
        {
            Instance = this;
            GameEvents.onHideUI.Add(HideUI); GameEvents.onShowUI.Add(ShowUI);
        }
        private void OnDestroy()
        {
            RestorePresentationOffset();
            GameEvents.onHideUI.Remove(HideUI); GameEvents.onShowUI.Remove(ShowUI);
            if (Instance == this) Instance = null;
        }
        private void HideUI() { uiVisible = false; }
        private void ShowUI() { uiVisible = true; }
        private void Update()
        {
            // All Update calls precede native camera LateUpdate. Restore only an
            // offset we still own; never feed collision displacement back into
            // native camera smoothing, heading, pitch or zoom state.
            RestorePresentationOffset();
            if (Input.GetKeyDown(KeyCode.F8)) visible = !visible;
        }

        private void RestorePresentationOffset()
        {
            if (restorePending && correctedTransform != null && correctedTransform.parent == correctedParent
                && Time.frameCount <= correctionFrame + 1
                && (correctedTransform.localPosition - appliedPosition).sqrMagnitude < 1e-8f
                && Quaternion.Angle(correctedTransform.localRotation, appliedRotation) < .001f)
            {
                correctedTransform.localPosition = originalPosition;
                correctedTransform.localRotation = originalRotation;
            }
            restorePending = false;
        }

        private void LateUpdate()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            available = HighLogic.LoadedSceneIsFlight && vessel != null && vessel.mainBody != null && !vessel.packed
                && SystemLoader.OwnedBodies.ContainsKey(vessel.mainBody);
            if (!available) return;
            BodyPointSample sample;
            // Match the native altimeter/cache reference point: vessel centre of mass.
            if (BodyQueries.Sample(vessel.mainBody, vessel.CoMD, out sample) != BodyQueryStatus.Available) { available = false; return; }
            FlightCamera camera = FlightCamera.fetch;
            if (!MapView.MapIsEnabled && camera != null && CameraManager.Instance != null
                && CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Flight)
                ConstrainCamera(vessel.mainBody, camera);
            if (Time.unscaledTime >= nextReadout)
            {
                nextReadout = Time.unscaledTime + .2f;
                readout = vessel.mainBody.bodyDisplayName
                    + "\nGround below: " + sample.LocalSurface.Clearance.ToString("F1") + " m"
                    + "\nDatum altitude: " + sample.LocalSurface.ReferenceAltitude.ToString("F1") + " m"
                    + "\nSurface slope: " + sample.LocalSurface.SlopeDegrees.ToString("F1") + "°"
                    + "\nGravity: " + sample.GravityWorld.magnitude.ToString("F3") + " m/s²"
                    + (sample.LocalSurface.HasHeading ? "" : "\nCompass undefined at pole");
            }
        }

        internal void ConstrainCamera(CelestialBody body, FlightCamera camera)
        {
            Transform transform = camera.GetCameraTransform();
            BodyPointSample sample;
            if (transform == null || BodyQueries.Sample(body, transform.position, out sample) != BodyQueryStatus.Available) return;
            LastCameraClearance = sample.LocalSurface.Clearance;
            if (!CameraGuardEnabled || LastCameraClearance >= 2) return;
            correctedTransform = transform; correctedParent = transform.parent;
            originalPosition = transform.localPosition; originalRotation = transform.localRotation;
            global::Vector3d corrected = sample.GroundWorld + sample.GravityUpWorld * 2;
            transform.position = (Vector3)corrected;
            Transform pivot = camera.GetPivot();
            if (pivot != null && (pivot.position - transform.position).sqrMagnitude > .001f)
                transform.rotation = Quaternion.LookRotation(pivot.position - transform.position, (Vector3)sample.GravityUpWorld);
            appliedPosition = transform.localPosition; appliedRotation = transform.localRotation;
            correctionFrame = Time.frameCount; restorePending = true;
            CameraCorrections++;
            BodyPointSample after;
            if (BodyQueries.Sample(body, transform.position, out after) == BodyQueryStatus.Available)
                LastCameraClearance = after.LocalSurface.Clearance;
        }

        private void OnGUI()
        {
            if (!available || !visible || !uiVisible || MapView.MapIsEnabled) return;
            GUILayout.BeginArea(new Rect(Screen.width - 325, 100, 290, 190), GUI.skin.box);
            GUILayout.Label("Jakobian surface [F8]");
            GUILayout.Label(readout ?? "Reading surface…");
            CameraGuardEnabled = GUILayout.Toggle(CameraGuardEnabled, "Keep camera above ground");
            GUILayout.EndArea();
        }
    }
}
