using System.Collections.Generic;
using UnityEngine;

namespace TechArtLab.Balatro.Particles
{
    public sealed class EdgeParticleEmitter : MonoBehaviour
    {
        public ParticleSystem system;
        public bool emitParticles=true,showBirths;
        [Range(0,1)] public float probability=.04f;
        public Vector2 sizePixels=new Vector2(1,3),lifetime=new Vector2(.4f,.8f),speedHeights=new Vector2(.1f,.3f);
        [Range(1,4096)] public int capacity=512;
        public readonly List<int> lastCells=new List<int>(512);
        public readonly List<Vector3> lastBirths=new List<Vector3>(512);
        public int TotalEmitted { get; private set; }
        public uint BirthHash { get; private set; }
        System.Random random=new System.Random(1);
        float Range(Vector2 v)=>Mathf.Lerp(v.x,v.y,(float)random.NextDouble());
        public void ResetParticles(int seed)
        {
            random=new System.Random(seed^0x31f62); TotalEmitted=0; BirthHash=2166136261;
            lastCells.Clear(); lastBirths.Clear();
            if(system) system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        public void Step(float dt,DissolveGrid grid,Transform card,bool emit,Color orange,Color dark)
        {
            lastCells.Clear(); lastBirths.Clear();
            if(!system) return;
            var main=system.main; main.maxParticles=Mathf.Max(1,capacity);
            system.Simulate(dt,false,false,false);
            if(!emit || !emitParticles) return;
            for(int i=0;i<DissolveGrid.Count;i++) {
                if(!grid.crossed[i] || random.NextDouble()>probability || system.particleCount>=capacity) continue;
                Vector2 uv=DissolveGrid.UV(i);
                Vector3 position=card.TransformPoint(new Vector3(uv.x-.5f,uv.y-.5f,-.015f));
                Vector2 jitter=new Vector2((float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1);
                Vector2 direction=grid.Outward(i);
                direction=(direction.sqrMagnitude<.01f ? jitter : direction+.35f*jitter).normalized;
                float h=Mathf.Abs(card.lossyScale.y);
                var p=new ParticleSystem.EmitParams {
                    position=position,velocity=card.TransformDirection(direction)*Range(speedHeights)*h,
                    startSize=Range(sizePixels)*h/DissolveGrid.Height,startLifetime=Range(lifetime),
                    startColor=random.NextDouble()<.7 ? orange : dark,rotation=Range(new Vector2(0,360)),
                    angularVelocity=Range(new Vector2(-180,180)),randomSeed=(uint)random.Next(1,int.MaxValue)
                };
                system.Emit(p,1); TotalEmitted++; lastCells.Add(i); lastBirths.Add(position);
                unchecked { BirthHash=(BirthHash^(uint)i)*16777619; }
            }
        }
        void OnDrawGizmos()
        {
            if(!showBirths) return;
            Gizmos.color=Color.magenta;
            foreach(var p in lastBirths) Gizmos.DrawWireSphere(p,.025f);
        }
    }
}
