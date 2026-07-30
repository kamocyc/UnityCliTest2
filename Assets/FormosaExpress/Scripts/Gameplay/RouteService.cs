using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using FormosaExpress.Core;

namespace FormosaExpress.Gameplay
{
    /// <summary>
    /// The player's navigation: wraps a <see cref="RouteTracker"/> and draws the route as a faint
    /// ribbon on the asphalt. The rival courier owns its own tracker and no ribbon.
    /// </summary>
    public sealed class RouteService : MonoBehaviour
    {
        public RouteTracker Tracker { get; } = new RouteTracker();

        public bool HasRoute => Tracker.HasRoute;
        public float DistanceRemaining => Tracker.DistanceRemaining;
        public Vector3 Target => Tracker.Target;
        public Vector3 Heading => Tracker.Heading;
        public IReadOnlyList<Vector3> Points => Tracker.Points;

        readonly List<Vector3> _renderPoints = new List<Vector3>(96);
        LineRenderer _line;
        bool _lineEnabled = true;

        public void Initialise(MaterialLibrary mats)
        {
            var go = new GameObject("RouteLine");
            go.transform.SetParent(transform, false);

            _line = go.AddComponent<LineRenderer>();

            // A clear hint, not a spotlight: bright enough to read at a glance against dark
            // asphalt, without competing with the beacons.
            _line.material = MaterialLibrary.MakeFlatUnlit("FE_RouteLine", new Color(0.10f, 0.62f, 0.30f, 1f), true);
            _line.widthMultiplier = 0.62f;
            _line.numCapVertices = 2;
            _line.numCornerVertices = 3;
            _line.alignment = LineAlignment.TransformZ;
            _line.textureMode = LineTextureMode.Stretch;
            _line.shadowCastingMode = ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.useWorldSpace = true;
            _line.positionCount = 0;

            // Lay the ribbon flat on the road.
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // The ribbon is additive, so it has to fade towards black rather than towards
            // transparent. Built once: rebuilding a Gradient every frame would allocate.
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.9f, 1f, 0.95f), 0f),
                    new GradientColorKey(new Color(0.55f, 0.95f, 0.68f), 0.55f),
                    new GradientColorKey(Color.black, 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            _line.colorGradient = gradient;
        }

        public void SetTarget(Vector3 target) => Tracker.SetTarget(target);

        public void ClearTarget()
        {
            Tracker.ClearTarget();
            if (_line != null) _line.positionCount = 0;
        }

        public void SetLineVisible(bool visible)
        {
            _lineEnabled = visible;
            if (_line != null) _line.enabled = visible && Tracker.HasRoute;
        }

        void Update()
        {
            Vector3 from = Services.PlayerPosition;
            Tracker.Tick(from, Time.deltaTime);
            UpdateLine(from);
        }

        void UpdateLine(Vector3 from)
        {
            if (_line == null) return;

            if (!Tracker.HasRoute || !_lineEnabled || Tracker.Points.Count < 2)
            {
                _line.enabled = false;
                return;
            }

            _line.enabled = true;
            _renderPoints.Clear();

            // Start well ahead of the rider so the ribbon never glows under the scooter.
            const float leadIn = 9f;
            Vector3 ahead = from + Tracker.Heading * leadIn;
            _renderPoints.Add(new Vector3(ahead.x, 0.06f, ahead.z));

            // Only draw the part of the route still ahead of the rider, starting from the segment
            // they are currently on. Filtering purely by distance would leave far-away waypoints
            // from earlier in the route dangling behind them.
            IReadOnlyList<Vector3> points = Tracker.Points;
            for (int i = Mathf.Clamp(Tracker.NearestSegment, 1, points.Count - 1); i < points.Count; i++)
            {
                Vector3 p = points[i];
                if ((p - from).sqrMagnitude < leadIn * leadIn) continue;

                _renderPoints.Add(new Vector3(p.x, 0.06f, p.z));
                if (_renderPoints.Count > 40) break;
            }

            if (_renderPoints.Count < 2)
            {
                _line.enabled = false;
                return;
            }

            _line.positionCount = _renderPoints.Count;
            for (int i = 0; i < _renderPoints.Count; i++) _line.SetPosition(i, _renderPoints[i]);
        }
    }
}
