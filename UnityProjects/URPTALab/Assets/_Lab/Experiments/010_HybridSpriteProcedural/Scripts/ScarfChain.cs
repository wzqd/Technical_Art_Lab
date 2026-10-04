using System;
using UnityEngine;

namespace TechArtLab.Hybrid
{
    // A 2D chain with XPBD distance constraints. This is not a cloth sheet or a bending solver.
    public sealed class ScarfChain
    {
        [Serializable] public sealed class Settings
        {
            [Range(4, 12)] public int segments = 8;
            [Range(.5f, 1.4f)] public float length = 1.1f;
            [Range(20, 500)] public float stiffness = 120;
            [Range(.3f, 10)] public float damping = 3;
            [Range(0, 10)] public float gravity = 5;
            public int frequency = 60;
            public void Clamp()
            {
                segments = Mathf.Clamp(segments, 4, 12); length = Mathf.Clamp(length, .5f, 1.4f);
                stiffness = Mathf.Clamp(stiffness, 20, 500); damping = Mathf.Clamp(damping, .3f, 10);
                gravity = Mathf.Clamp(gravity, 0, 10); frequency = frequency <= 30 ? 30 : frequency <= 60 ? 60 : 120;
            }
        }
        public readonly Vector2[] positions, velocities;
        readonly Vector2[] previous;
        readonly float[] multipliers;
        public readonly float restLength;
        public const int Iterations = 64;
        public int Segments => positions.Length - 1;
        public float ActualLength { get { float sum = 0; for (int i = 1; i < positions.Length; i++) sum += Vector2.Distance(positions[i - 1], positions[i]); return sum; } }
        public float TipSpeed => velocities[Segments].magnitude;
        public ScarfChain(Settings settings, Vector2 root)
        {
            settings.Clamp(); int n = settings.segments; restLength = settings.length / n;
            positions = new Vector2[n + 1]; velocities = new Vector2[n + 1]; previous = new Vector2[n + 1]; multipliers = new float[n];
            var direction = new Vector2(-1, -.25f).normalized;
            for (int i = 0; i <= n; i++) positions[i] = root + direction * (i * restLength);
        }
        public void Tick(float dt, Vector2 anchor, Settings settings)
        {
            float retain = Mathf.Exp(-settings.damping * dt);
            for (int i = 0; i <= Segments; i++) previous[i] = positions[i];
            positions[0] = anchor;
            for (int i = 1; i <= Segments; i++)
            {
                velocities[i] = velocities[i] * retain + Vector2.down * (settings.gravity * dt);
                positions[i] += velocities[i] * dt;
            }
            Array.Clear(multipliers, 0, multipliers.Length);
            // Total mobile mass is 1 kg. n equal springs in series: k_segment = n * k_chain.
            float inverseMass = Segments;
            float alpha = 1 / (settings.stiffness * Segments * dt * dt);
            for (int iteration = 0; iteration < Iterations; iteration++)
            {
                // Alternate traversal to reduce the bias toward the last processed end.
                for (int j = 0; j < Segments; j++)
                {
                    int edge = iteration % 2 == 0 ? j : Segments - 1 - j;
                    int a = edge, b = edge + 1;
                    Vector2 delta = positions[b] - positions[a]; float distance = delta.magnitude;
                    Vector2 normal = distance > 1e-7f ? delta / distance : Vector2.down;
                    float wa = a == 0 ? 0 : inverseMass, wb = inverseMass;
                    float c = distance - restLength;
                    float dl = (-c - alpha * multipliers[edge]) / (wa + wb + alpha);
                    multipliers[edge] += dl;
                    positions[a] -= wa * dl * normal; positions[b] += wb * dl * normal;
                }
            }
            for (int i = 0; i <= Segments; i++) velocities[i] = (positions[i] - previous[i]) / dt;
        }
    }
}
