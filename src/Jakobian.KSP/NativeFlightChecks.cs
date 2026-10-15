using System;
using UnityEngine;
using KSP.UI.Screens.Flight;

namespace Jakobian.KSP
{
    internal static class NativeFlightChecks
    {
        internal static void Run(Vessel vessel, string phase)
        {
            BodyPointSample context;
            if (BodyQueries.Sample(vessel.mainBody, vessel.CoMD, out context) != BodyQueryStatus.Available)
                throw new InvalidOperationException("Flight context unavailable.");
            double upError = Vector3.Angle((Vector3)vessel.upAxis, (Vector3)context.GravityUpWorld);
            double globalUpError = Vector3.Angle((Vector3)FlightGlobals.upAxis, (Vector3)context.GravityUpWorld);
            double altitudeError = Math.Abs(vessel.altitude - context.LocalSurface.ReferenceAltitude);
            double radarError = Math.Abs(vessel.radarAltitude - context.LocalSurface.Clearance);
            double velocityError = (vessel.srf_velocity - (vessel.obt_velocity - context.CorotationVelocityRelativeToBodyWorld)).magnitude;
            NavBall navball = UnityEngine.Object.FindObjectOfType<NavBall>();
            double navError = double.NaN;
            if (navball != null && context.LocalSurface.HasHeading)
                navError = Quaternion.Angle(navball.relativeGymbal, navball.attitudeGymbal
                    * Quaternion.LookRotation((Vector3)context.NorthWorld, (Vector3)context.GravityUpWorld));
            Debug.Log("[Jakobian] NAVIGATION_TRACE " + vessel.mainBody.bodyName + " phase=" + phase + " upDeg=" + upError
                + " globalUpDeg=" + globalUpError + " datumErrorM=" + altitudeError + " groundErrorM=" + radarError
                + " velocityError=" + velocityError + " navballDeg=" + navError);
            if (upError > .1 || globalUpError > .1 || altitudeError > 2 || radarError > 3 || velocityError > .1
                || double.IsNaN(navError) || navError > .1)
                throw new InvalidOperationException("Native navigation/cache mismatch; see NAVIGATION_TRACE.");
            if (vessel.staticPressurekPa != 0 || vessel.atmDensity != 0 || context.HasAtmosphere || context.InFluid)
                throw new InvalidOperationException("Airless/dry context mismatch.");
            AbundanceRequest request;
            if (BodyQueries.CreatePlanetaryResourceRequest(vessel.mainBody, vessel.CoMD, "Ore", false, out request) != BodyQueryStatus.Available
                || request.BodyId != vessel.mainBody.flightGlobalsIndex
                || Math.Abs(request.Latitude - vessel.mainBody.GetLatitude(vessel.CoMD)) > .0001
                || Math.Abs(request.Longitude - vessel.mainBody.GetLongitude(vessel.CoMD)) > .0001)
                throw new InvalidOperationException("Resource coordinate bridge mismatch.");
            float abundance = ResourceMap.Instance.GetAbundance(request);
            if (ResourceMap.Instance.DetermineBiomeName(request) != context.BiomeName)
                throw new InvalidOperationException("Native resource/science biome mismatch.");
            if (float.IsNaN(abundance) || float.IsInfinity(abundance) || abundance < 0 || abundance > 1)
                throw new InvalidOperationException("Invalid native resource abundance.");
            Debug.Log("[Jakobian] NATIVE_NAVIGATION_PASS " + vessel.mainBody.bodyName + " phase=" + phase
                + " nativeUp=gravity navball=gravity radar=ground datum=reference air=vacuum biome=" + context.BiomeName + " Ore=" + abundance);
        }
    }
}
