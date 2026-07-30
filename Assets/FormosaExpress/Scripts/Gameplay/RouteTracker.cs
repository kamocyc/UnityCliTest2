using System.Collections.Generic;
using UnityEngine;
using FormosaExpress.Core;

namespace FormosaExpress.Gameplay
{
    /// <summary>
    /// Road-following navigation for one courier: keeps an A* route to a target and answers
    /// "which way should I be heading right now" via pure pursuit. Plain class, no MonoBehaviour,
    /// so the player and the rival can each own one.
    /// </summary>
    public sealed class RouteTracker
    {
        const float RecomputeInterval = 0.55f;
        const float RecomputeDeviation = 14f;
        const float LookAhead = 20f;

        public bool HasRoute { get; private set; }
        public float DistanceRemaining { get; private set; }
        public Vector3 Target { get; private set; }
        public Vector3 Heading { get; private set; } = Vector3.forward;
        public bool HasTarget { get; private set; }

        /// <summary>Index of the polyline segment the courier is currently closest to.</summary>
        public int NearestSegment { get; private set; } = 1;

        public IReadOnlyList<Vector3> Points => _points;

        readonly List<Vector3> _points = new List<Vector3>(64);
        float _timer;
        Vector3 _lastComputedFrom;

        public void SetTarget(Vector3 target)
        {
            Target = target;
            HasTarget = true;
            _timer = 0f;
        }

        public void ClearTarget()
        {
            HasTarget = false;
            HasRoute = false;
            DistanceRemaining = 0f;
            _points.Clear();
        }

        public void Tick(Vector3 from, float dt)
        {
            if (!HasTarget || Services.City == null) return;

            _timer -= dt;
            if (_timer <= 0f || (from - _lastComputedFrom).sqrMagnitude > RecomputeDeviation * RecomputeDeviation)
                Recompute(from);

            UpdateHeading(from);
        }

        void Recompute(Vector3 from)
        {
            _timer = RecomputeInterval;
            _lastComputedFrom = from;

            HasRoute = Services.City.BuildRoute(from, Target, _points, out float length);
            DistanceRemaining = HasRoute ? length : Vector3.Distance(from, Target);
            if (!HasRoute) _points.Clear();
        }

        /// <summary>
        /// Pure pursuit: project onto the route, then aim at a point a fixed arc length further
        /// along it. Aiming at "the first waypoint more than N metres away" instead sends a
        /// courier in circles whenever the nearest junction happens to be behind them.
        /// </summary>
        void UpdateHeading(Vector3 from)
        {
            if (_points.Count < 2)
            {
                Vector3 direct = Target - from;
                direct.y = 0f;
                Heading = direct.sqrMagnitude > 0.01f ? direct.normalized : Heading;
                DistanceRemaining = direct.magnitude;
                NearestSegment = 1;
                return;
            }

            int nearestSegment = 1;
            float nearestT = 0f;
            float nearestSqr = float.MaxValue;

            for (int i = 1; i < _points.Count; i++)
            {
                Vector3 p = MathX.ClosestPointOnSegment(_points[i - 1], _points[i], from, out float t);
                float sqr = (p - from).sqrMagnitude;
                if (sqr >= nearestSqr) continue;

                nearestSqr = sqr;
                nearestSegment = i;
                nearestT = t;
            }

            NearestSegment = nearestSegment;

            Vector3 cursor = Vector3.Lerp(_points[nearestSegment - 1], _points[nearestSegment], nearestT);
            Vector3 aimPoint = _points[_points.Count - 1];
            float budget = LookAhead;

            for (int i = nearestSegment; i < _points.Count; i++)
            {
                float segment = Vector3.Distance(cursor, _points[i]);
                if (segment >= budget)
                {
                    aimPoint = Vector3.Lerp(cursor, _points[i], budget / Mathf.Max(0.001f, segment));
                    break;
                }

                budget -= segment;
                cursor = _points[i];
                aimPoint = cursor;
            }

            Vector3 aim = aimPoint - from;
            aim.y = 0f;
            if (aim.sqrMagnitude > 0.04f) Heading = aim.normalized;

            float remaining = Vector3.Distance(from, cursor) + Vector3.Distance(cursor, _points[nearestSegment]);
            for (int i = nearestSegment + 1; i < _points.Count; i++)
                remaining += Vector3.Distance(_points[i - 1], _points[i]);

            DistanceRemaining = remaining;
        }
    }
}
